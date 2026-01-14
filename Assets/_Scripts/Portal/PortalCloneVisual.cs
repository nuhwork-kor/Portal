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

        if (active && (inPortal != inP || outPortal != outP))
        {
            ForceEnd();
        }

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
        cloneGO = Instantiate(visualRoot.gameObject);
        cloneGO.name = $"{visualRoot.name}_PortalClone";
        cloneRoot = cloneGO.transform;

        cloneRoot.SetParent(null, true);

        int layer = LayerMask.NameToLayer(cloneLayerName);
        if (layer >= 0) SetLayerRecursively(cloneRoot, layer);

        foreach (var c in cloneGO.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var rb in cloneGO.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        foreach (var j in cloneGO.GetComponentsInChildren<Joint>(true)) Destroy(j);

        foreach (var mb in cloneGO.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
        foreach (var an in cloneGO.GetComponentsInChildren<Animator>(true)) Destroy(an);

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
}
