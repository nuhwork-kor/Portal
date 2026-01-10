using UnityEngine;

public class Portal : MonoBehaviour
{
    [Header("Link")]
    [SerializeField] private Portal otherPortal;
    public Portal OtherPortal => otherPortal;

    [Header("Refs")]
    [SerializeField] private Transform plane;
    public Transform Plane => plane ? plane : transform;

    [SerializeField] private Renderer surfaceRenderer;        // ★추가: PortalSurface의 MeshRenderer
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

        // 포탈 화면은 "상대 포탈이 놓였을 때만" 켜는 게 일반적으로 안전함
        if (surfaceRenderer)
            surfaceRenderer.enabled = placed && otherPortal != null && otherPortal.IsPlaced;

        if (!placed) WallColliderCached = null;
    }

    public void Reposition(Collider wallCollider, Vector3 pos, Quaternion rot, float surfaceOffset)
    {
        WallColliderCached = wallCollider;
        transform.SetPositionAndRotation(pos + (rot * Vector3.forward) * surfaceOffset, rot);

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // 먼저 placed 켜고, 상대 포탈 상태에 따라 surface 표시 여부가 결정됨
        SetPlaced(true);
    }

    // PortalSystem에서 다른 포탈을 배치했을 때, 화면 on/off를 갱신할 용도
    public void RefreshSurfaceVisibility()
    {
        if (surfaceRenderer)
            surfaceRenderer.enabled = IsPlaced && otherPortal != null && otherPortal.IsPlaced;
    }
}
