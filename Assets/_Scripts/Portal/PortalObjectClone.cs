using System.Collections.Generic;
using UnityEngine;

public class PortalObjectClone : MonoBehaviour
{
    [Header("Visual Root (optional)")]
    [Tooltip("비주얼만 따로 루트가 있으면 지정. 비워두면 자기 자신을 루트로 사용.")]
    [SerializeField] private Transform visualRoot;

    [Header("Slice Shader")]
    [SerializeField] private Shader sliceShader; // Portals/PortalSliceUnlit

    [Tooltip("포탈 평면에서 살짝 숨겨서 경계 깜빡임/실금 방지(0.001~0.01 권장)")]
    [SerializeField] private float sliceEpsilon = 0.002f;

    [Tooltip("클론이 보이면 안 되는 레이어가 필요하면 여기서 바꿔(없으면 -1)")]
    [SerializeField] private int cloneLayer = -1;

    private Portal inPortal;
    private Portal outPortal;

    private GameObject cloneGO;
    private Transform cloneRoot;

    private bool active;
    private int entrySign = +1;
    private int outKeepSign = +1;

    private readonly List<(Transform src, Transform dst)> transformPairs = new();
    private Renderer[] srcRenderers;
    private Renderer[] cloneRenderers;

    // 원본 머티리얼 백업
    private Material[][] srcOriginalMats;

    // 포탈 클립용 머티리얼(원본/클론)
    private Material[][] srcSliceMats;
    private Material[][] cloneSliceMats;

    private MaterialPropertyBlock mpb;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    private void Awake()
    {
        if (!visualRoot) visualRoot = transform;
        mpb = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (!active || !inPortal || !outPortal) return;

        // 1) 클론 루트 트랜스폼을 포탈 변환으로 맞춤
        UpdateCloneRootTransform();

        // 2) (원본 비주얼이 자식 애니메이션/회전이 있으면) 트랜스폼 동기화
        SyncVisualTransforms();

        // 3) 원본/클론 각각 “반쪽만” 보이도록 클리핑 평면 업데이트
        UpdateSlicePlanes();
    }

    // PortalTrigger.cs에서 호출
    public void Begin(Portal inP, Portal outP)
    {
        if (!inP || !outP) return;

        // 이미 활성인데 포탈이 바뀌면 재설정
        if (active && (inPortal != inP || outPortal != outP))
        {
            End(null);
        }

        inPortal = inP;
        outPortal = outP;

        if (!cloneGO)
            BuildClone();

        // entrySign: 진입한 쪽(포탈 forward 기준으로 어느 쪽에서 들어왔는지)
        float d = SignedDistanceToPlane(inPortal.Plane, GetBoundsCenterWorld());
        entrySign = (d >= 0f) ? +1 : -1;

        // outKeepSign: “포탈을 넘은 쪽(through side)”이 outPortal에서는 어느 쪽이 되는지 자동 판정
        outKeepSign = ComputeOutKeepSign(inPortal, outPortal, entrySign);

        ApplySliceMaterials(); // 원본/클론 모두 slice shader로 교체
        cloneGO.SetActive(true);

        active = true;
    }

    // PortalTrigger.cs에서 호출
    public void End(Portal exitedPortal)
    {
        if (!active) return;

        // 특정 포탈 exit만 받아서 끄고 싶으면 여기서 필터 가능
        RestoreOriginalMaterials();

        if (cloneGO)
        {
            Destroy(cloneGO);
            cloneGO = null;
            cloneRoot = null;
        }

        transformPairs.Clear();

        inPortal = null;
        outPortal = null;

        active = false;
    }

    private void BuildClone()
    {
        // visualRoot 전체를 복제
        cloneGO = Instantiate(visualRoot.gameObject);
        cloneGO.name = $"{visualRoot.name}_PortalClone";
        cloneGO.transform.SetPositionAndRotation(visualRoot.position, visualRoot.rotation);
        cloneGO.transform.localScale = visualRoot.lossyScale;

        cloneRoot = cloneGO.transform;

        // 레이어 지정(옵션)
        if (cloneLayer >= 0)
        {
            SetLayerRecursively(cloneRoot, cloneLayer);
        }

        // Clone에서 물리/스크립트 제거 (렌더링만 남김)
        foreach (var c in cloneGO.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var r in cloneGO.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);

        // MonoBehaviour 싹 제거 (Renderer/MeshFilter는 MonoBehaviour 아님)
        foreach (var mb in cloneGO.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(mb);

        // 트랜스폼 매핑(원본 visualRoot 트리 <-> cloneRoot 트리)
        transformPairs.Clear();
        BuildTransformPairs(visualRoot, cloneRoot);

        // 렌더러 캐시
        srcRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        cloneRenderers = cloneRoot.GetComponentsInChildren<Renderer>(true);

        // 머티리얼 백업
        srcOriginalMats = new Material[srcRenderers.Length][];
        for (int i = 0; i < srcRenderers.Length; i++)
            srcOriginalMats[i] = srcRenderers[i].sharedMaterials;

        // Slice 머티리얼 준비
        srcSliceMats = BuildSliceMaterialsFrom(srcRenderers);
        cloneSliceMats = BuildSliceMaterialsFrom(cloneRenderers);

        cloneGO.SetActive(false);
    }

    private Material[][] BuildSliceMaterialsFrom(Renderer[] renderers)
    {
        var result = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            var shared = renderers[i].sharedMaterials;
            result[i] = new Material[shared.Length];

            for (int m = 0; m < shared.Length; m++)
            {
                // 원본 머티리얼을 복사한 뒤 셰이더만 교체
                var srcMat = shared[m];
                var mat = new Material(srcMat);

                if (sliceShader != null)
                    mat.shader = sliceShader;
                else
                    mat.shader = Shader.Find("Portals/PortalSliceUnlit");

                // 텍스처 이름 호환: _MainTex -> _BaseMap
                if (srcMat != null)
                {
                    if (srcMat.HasProperty("_BaseMap") && srcMat.GetTexture("_BaseMap") != null)
                        mat.SetTexture("_BaseMap", srcMat.GetTexture("_BaseMap"));
                    else if (srcMat.HasProperty("_MainTex") && srcMat.GetTexture("_MainTex") != null)
                        mat.SetTexture("_BaseMap", srcMat.GetTexture("_MainTex"));

                    if (srcMat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", srcMat.GetColor("_BaseColor"));
                    else if (srcMat.HasProperty("_Color"))
                        mat.SetColor("_BaseColor", srcMat.GetColor("_Color"));
                }

                result[i][m] = mat;
            }
        }

        return result;
    }

    private void ApplySliceMaterials()
    {
        if (sliceShader == null)
            sliceShader = Shader.Find("Portals/PortalSliceUnlit");

        // 원본에 slice 적용
        for (int i = 0; i < srcRenderers.Length; i++)
            srcRenderers[i].sharedMaterials = srcSliceMats[i];

        // 클론에 slice 적용
        for (int i = 0; i < cloneRenderers.Length; i++)
            cloneRenderers[i].sharedMaterials = cloneSliceMats[i];
    }

    private void RestoreOriginalMaterials()
    {
        if (srcRenderers != null && srcOriginalMats != null)
        {
            for (int i = 0; i < srcRenderers.Length; i++)
            {
                if (srcRenderers[i])
                    srcRenderers[i].sharedMaterials = srcOriginalMats[i];
            }
        }
    }

    private void UpdateCloneRootTransform()
    {
        Transform inT = inPortal.Plane;
        Transform outT = outPortal.Plane;

        // visualRoot의 월드 포즈를 포탈로 변환
        Vector3 relPos = inT.InverseTransformPoint(visualRoot.position);
        relPos = HalfTurn * relPos;
        Vector3 newPos = outT.TransformPoint(relPos);

        Quaternion relRot = Quaternion.Inverse(inT.rotation) * visualRoot.rotation;
        relRot = HalfTurn * relRot;
        Quaternion newRot = outT.rotation * relRot;

        cloneRoot.SetPositionAndRotation(newPos, newRot);

        // 스케일은 단순 복사(포탈 스케일 변형 안 한다는 전제)
        cloneRoot.localScale = visualRoot.lossyScale;
    }

    private void SyncVisualTransforms()
    {
        // 원본 visualRoot의 자식 트랜스폼을 clone에도 그대로 복사
        // (Cube는 필요 없지만, Turret/Player 모델 넣을 때 유용)
        for (int i = 0; i < transformPairs.Count; i++)
        {
            var (src, dst) = transformPairs[i];
            if (!src || !dst) continue;

            dst.localPosition = src.localPosition;
            dst.localRotation = src.localRotation;
            dst.localScale = src.localScale;
        }
    }

    private void UpdateSlicePlanes()
    {
        // 원본: "진입한 쪽"만 남기기
        Vector3 inCenter = inPortal.Plane.position;
        Vector3 inNormal = inPortal.Plane.forward * entrySign;

        // 클론: "넘어간 쪽"만 남기기
        Vector3 outCenter = outPortal.Plane.position;
        Vector3 outNormal = outPortal.Plane.forward * outKeepSign;

        // 원본 렌더러에 평면 세팅
        mpb.Clear();
        mpb.SetVector("_SliceCenter", inCenter);
        mpb.SetVector("_SliceNormal", inNormal);
        mpb.SetFloat("_SliceOffset", -sliceEpsilon);
        for (int i = 0; i < srcRenderers.Length; i++)
            if (srcRenderers[i]) srcRenderers[i].SetPropertyBlock(mpb);

        // 클론 렌더러에 평면 세팅
        mpb.Clear();
        mpb.SetVector("_SliceCenter", outCenter);
        mpb.SetVector("_SliceNormal", outNormal);
        mpb.SetFloat("_SliceOffset", -sliceEpsilon);
        for (int i = 0; i < cloneRenderers.Length; i++)
            if (cloneRenderers[i]) cloneRenderers[i].SetPropertyBlock(mpb);
    }

    private static float SignedDistanceToPlane(Transform plane, Vector3 worldPoint)
    {
        return Vector3.Dot(plane.forward, worldPoint - plane.position);
    }

    private Vector3 GetBoundsCenterWorld()
    {
        // 렌더러 바운드 중심(콜라이더 말고 “보이는 것” 기준)
        if (srcRenderers != null && srcRenderers.Length > 0)
        {
            Bounds b = srcRenderers[0].bounds;
            for (int i = 1; i < srcRenderers.Length; i++)
                b.Encapsulate(srcRenderers[i].bounds);
            return b.center;
        }
        return visualRoot.position;
    }

    private static int ComputeOutKeepSign(Portal inP, Portal outP, int entrySign)
    {
        // inPortal에서 "넘어간 쪽(through side)"의 아주 작은 점을 하나 찍고,
        // 그 점을 포탈 변환해서 outPortal 평면 기준 어느 쪽인지로 keep 방향 자동 결정
        Transform inT = inP.Plane;
        Transform outT = outP.Plane;

        Vector3 throughPoint = inT.position - inT.forward * entrySign * 0.01f;

        Vector3 rel = inT.InverseTransformPoint(throughPoint);
        rel = HalfTurn * rel;
        Vector3 mapped = outT.TransformPoint(rel);

        float dOut = Vector3.Dot(outT.forward, mapped - outT.position);
        return (dOut >= 0f) ? +1 : -1;
    }

    private void BuildTransformPairs(Transform src, Transform dst)
    {
        if (!src || !dst) return;

        int childCount = Mathf.Min(src.childCount, dst.childCount);
        for (int i = 0; i < childCount; i++)
        {
            Transform s = src.GetChild(i);
            Transform d = dst.GetChild(i);

            transformPairs.Add((s, d));
            BuildTransformPairs(s, d);
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }
}
