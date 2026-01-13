using UnityEngine;

public class Portal : MonoBehaviour
{
    [Header("Link")]
    [SerializeField] private Portal otherPortal;
    public Portal OtherPortal => otherPortal;

    [Header("Refs")]
    [SerializeField] private Transform plane;
    public Transform Plane => plane ? plane : transform;

    [SerializeField] private Renderer surfaceRenderer; // PortalSurface의 MeshRenderer
    public Renderer SurfaceRenderer => surfaceRenderer;

    [Header("Colliders")]
    [SerializeField] private BoxCollider surfaceCollider;
    public BoxCollider SurfaceCollider => surfaceCollider;

    [SerializeField] private BoxCollider triggerCollider;
    public BoxCollider TriggerCollider => triggerCollider;

    public bool IsPlaced { get; private set; }
    public Collider WallColliderCached { get; private set; }

    private void Awake()
    {
        if (!plane)
        {
            var t = transform.Find("PortalPlane");
            if (t) plane = t;
        }

        if (!surfaceRenderer)
        {
            var t = transform.Find("PortalSurface");
            if (t) surfaceRenderer = t.GetComponent<Renderer>();
        }
    }

    public void LinkTo(Portal other) => otherPortal = other;

    public void SetPlaced(bool placed)
    {
        IsPlaced = placed;

        if (surfaceCollider) surfaceCollider.enabled = placed;
        if (triggerCollider) triggerCollider.enabled = placed;

        // 포탈 화면은 "상대 포탈도 설치되어 있을 때만" 보이게
        if (surfaceRenderer)
            surfaceRenderer.enabled = placed && otherPortal != null && otherPortal.IsPlaced;

        if (!placed) WallColliderCached = null;
    }

    public void Reposition(Collider wallCollider, Vector3 pos, Quaternion rot, float surfaceOffset)
    {
        WallColliderCached = wallCollider;
        transform.SetPositionAndRotation(pos + (rot * Vector3.forward) * surfaceOffset, rot);

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // placed 처리 + 상대 포탈 상태에 따라 surface on/off
        SetPlaced(true);
    }

    // PortalSystem에서 다른 포탈 배치 후, 화면 on/off 갱신 용도
    public void RefreshSurfaceVisibility()
    {
        if (surfaceRenderer)
            surfaceRenderer.enabled = IsPlaced && otherPortal != null && otherPortal.IsPlaced;
    }
}
