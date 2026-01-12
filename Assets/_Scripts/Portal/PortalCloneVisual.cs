using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PortalCloneVisual : MonoBehaviour
{
    [Header("Visual Root")]
    [Tooltip("클론으로 복제할 비주얼 루트(모델 루트). 비우면 자기 자신(transform).")]
    [SerializeField] private Transform visualRoot;

    [Header("Clone Layer (optional)")]
    [Tooltip("클론을 넣을 레이어 이름. 없으면 원본 레이어 그대로 둠.")]
    [SerializeField] private string cloneLayerName = "PortalClone";

    private Portal inPortal;
    private Portal outPortal;

    private GameObject cloneGO;
    private Transform cloneRoot;

    private bool active;
    private int insideCount;

    private readonly List<(Transform src, Transform dst)> pairs = new();

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    private void Awake()
    {
        if (!visualRoot) visualRoot = transform;
        BuildCloneOnce();
        SetCloneActive(false);
    }

    private void OnDestroy()
    {
        if (cloneGO) Destroy(cloneGO);
    }

    private void LateUpdate()
    {
        if (!active) return;
        if (!inPortal || !outPortal) { SetCloneActive(false); return; }
        if (!inPortal.IsPlaced || !outPortal.IsPlaced) { SetCloneActive(false); return; }

        UpdateClonePose();
        SyncChildTransforms();
    }

    // ---------- Called from PortalTrigger ----------
    public void Begin(Portal inP, Portal outP)
    {
        if (!inP || !outP) return;

        // 포탈쌍이 바뀌면 리셋
        if (active && (inPortal != inP || outPortal != outP))
        {
            ForceEnd();
        }

        if (insideCount == 0)
        {
            inPortal = inP;
            outPortal = outP;
            SetCloneActive(true);
            // 들어가자마자 1프레임 갱신
            UpdateClonePose();
            SyncChildTransforms();
        }

        insideCount++;
        active = true;
    }

    // PortalTrigger.OnTriggerExit에서 호출(중첩 안전)
    public void NotifyTriggerExit(Portal exitedPortal)
    {
        if (!active) return;
        if (exitedPortal != null && inPortal != null && exitedPortal != inPortal)
            return;

        insideCount = Mathf.Max(insideCount - 1, 0);
        if (insideCount > 0) return;

        ForceEnd();
    }

    public void ForceEnd()
    {
        insideCount = 0;
        active = false;
        inPortal = null;
        outPortal = null;
        SetCloneActive(false);
    }

    /// <summary>
    /// 워프 직후에도 트리거 안에 잠깐 남아있을 수 있어서,
    /// traveller 쪽에서 포탈쌍이 스왑되면 여기에도 알려줘야 깔끔해.
    /// </summary>
    public void OnWarped(Portal newIn, Portal newOut)
    {
        if (!active) return;
        if (!newIn || !newOut) return;

        inPortal = newIn;
        outPortal = newOut;
    }

    // ---------- Clone building ----------
    private void BuildCloneOnce()
    {
        // visualRoot 전체를 그대로 복제(머티리얼/라이트 그대로)
        cloneGO = Instantiate(visualRoot.gameObject);
        cloneGO.name = $"{visualRoot.name}_PortalClone";
        cloneRoot = cloneGO.transform;

        // 씬 루트로 빼기(부모 영향 제거)
        cloneRoot.SetParent(null, true);

        // 레이어(선택)
        int layer = LayerMask.NameToLayer(cloneLayerName);
        if (layer >= 0) SetLayerRecursively(cloneRoot, layer);

        // 클론은 "렌더만" 남기기: 물리/스크립트 제거
        foreach (var c in cloneGO.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var rb in cloneGO.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        foreach (var j in cloneGO.GetComponentsInChildren<Joint>(true)) Destroy(j);

        // MonoBehaviour 전부 제거(PortalTraveller/AI/애니 스크립트 등)
        foreach (var mb in cloneGO.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);

        // Animator는 MonoBehaviour가 아니라서 별도 제거(원하면)
        foreach (var an in cloneGO.GetComponentsInChildren<Animator>(true)) Destroy(an);

        // 트랜스폼 매핑(원본 트리 <-> 클론 트리)
        pairs.Clear();
        BuildPairsRecursive(visualRoot, cloneRoot);

        // 클론은 절대 트리거를 건드리면 안 되니까(혹시 남았을 경우 대비)
        cloneGO.tag = "Untagged";
    }

    private void SetCloneActive(bool on)
    {
        if (!cloneGO) return;
        if (cloneGO.activeSelf == on) return;
        cloneGO.SetActive(on);
    }

    private void UpdateClonePose()
    {
        Transform inT = inPortal.Plane;
        Transform outT = outPortal.Plane;

        // visualRoot 월드 포즈를 포탈 변환
        Vector3 relPos = inT.InverseTransformPoint(visualRoot.position);
        relPos = HalfTurn * relPos;
        Vector3 newPos = outT.TransformPoint(relPos);

        Quaternion relRot = Quaternion.Inverse(inT.rotation) * visualRoot.rotation;
        relRot = HalfTurn * relRot;
        Quaternion newRot = outT.rotation * relRot;

        cloneRoot.SetPositionAndRotation(newPos, newRot);

        // 스케일은 그대로(대부분 동일 스케일 전제)
        cloneRoot.localScale = visualRoot.lossyScale;
    }

    private void SyncChildTransforms()
    {
        // Turret 같은 “부분 회전/애니 구조” 대비: 로컬 트랜스폼 동기화
        for (int i = 0; i < pairs.Count; i++)
        {
            var (s, d) = pairs[i];
            if (!s || !d) continue;
            d.localPosition = s.localPosition;
            d.localRotation = s.localRotation;
            d.localScale = s.localScale;
        }
    }

    private void BuildPairsRecursive(Transform src, Transform dst)
    {
        int n = Mathf.Min(src.childCount, dst.childCount);
        for (int i = 0; i < n; i++)
        {
            Transform s = src.GetChild(i);
            Transform d = dst.GetChild(i);
            pairs.Add((s, d));
            BuildPairsRecursive(s, d);
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }
}
