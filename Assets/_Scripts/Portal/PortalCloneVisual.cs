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

    // ✅ Instantiate로 생성되는 '복제본'의 Awake가 다시 Clone을 만들지 않게 가드
    private static int s_cloneBuildDepth = 0;

    private void Awake()
    {
        // ✅ 지금 이 PortalCloneVisual이 "클론 생성 과정에서" 같이 복제된 놈이면 중단
        if (s_cloneBuildDepth > 0)
        {
            enabled = false;
            return;
        }

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

        if (active && (inPortal != inP || outPortal != outP))
            ForceEnd();

        if (insideCount == 0)
        {
            inPortal = inP;
            outPortal = outP;
            SetCloneActive(true);

            UpdateClonePose();
            SyncChildTransforms();
        }

        insideCount++;
        active = true;
    }

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
        if (cloneGO != null) return;

        s_cloneBuildDepth++;
        try
        {
            cloneGO = Instantiate(visualRoot.gameObject);
        }
        finally
        {
            s_cloneBuildDepth--;
        }

        cloneGO.name = $"{visualRoot.name}_PortalClone";
        cloneRoot = cloneGO.transform;

        // ✅ 생성 즉시 비활성화 (Start/Update 최대한 차단)
        cloneGO.SetActive(false);

        // 씬에 그대로 떠있게
        cloneRoot.SetParent(null, true);

        int layer = LayerMask.NameToLayer(cloneLayerName);
        if (layer >= 0) SetLayerRecursively(cloneRoot, layer);

        // ✅ 중요: "삭제(Destroy)" 금지. 의존성(RequireComponent) 때문에 에러 난다.
        // 대신 물리/스크립트를 전부 무력화한다.
        DisableClonePhysics(cloneGO);
        DisableCloneBehaviours(cloneGO);

        // 트랜스폼 매핑
        pairs.Clear();
        BuildPairsRecursive(visualRoot, cloneRoot);

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

        Vector3 newPos = PortalMath.TransformPoint(visualRoot.position, inT, outT);
        Quaternion newRot = PortalMath.TransformRotation(visualRoot.rotation, inT, outT);

        cloneRoot.SetPositionAndRotation(newPos, newRot);
        cloneRoot.localScale = visualRoot.lossyScale;
    }

    private void SyncChildTransforms()
    {
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

    // =========================
    // ✅ Clone 무력화 유틸
    // =========================

    private static void DisableClonePhysics(GameObject go)
    {
        // 콜라이더 전부 OFF (트리거/충돌/레이캐스트 영향 제거)
        var cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (!cols[i]) continue;
            cols[i].enabled = false;
        }

        // 리지드바디 전부 완전 무력화
        var rbs = go.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            if (!rbs[i]) continue;
            rbs[i].linearVelocity = Vector3.zero;
            rbs[i].angularVelocity = Vector3.zero;
            rbs[i].useGravity = false;
            rbs[i].isKinematic = true;
            rbs[i].detectCollisions = false;
        }
    }

    private static void DisableCloneBehaviours(GameObject go)
    {
        // MonoBehaviour 전부 Disable (렌더러/트랜스폼은 그대로)
        var mbs = go.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < mbs.Length; i++)
        {
            if (!mbs[i]) continue;
            mbs[i].enabled = false;
        }

        // Animator도 필요 없으면 Disable (스켈레톤은 Transform이니까 유지됨)
        var anims = go.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            if (!anims[i]) continue;
            anims[i].enabled = false;
        }
    }
}
