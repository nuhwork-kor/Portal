// Portal.cs
using UnityEngine;

public class Portal : MonoBehaviour
{
    [Header("Link")]
    public Portal linkedPortal;

    [Header("Core")]
    public Transform portalPlane;

    [Header("Components")]
    public PortalPhysics physics;
    public PortalTeleport teleport;
    public PortalRenderer portalRenderer;
    public PortalVisual portalVisual;

    public bool IsLinked => linkedPortal != null;

    private void Awake()
    {
        if (!physics) physics = GetComponent<PortalPhysics>();
        if (!teleport) teleport = GetComponent<PortalTeleport>();
        if (!portalRenderer) portalRenderer = GetComponentInChildren<PortalRenderer>(true);
        if (!portalVisual) portalVisual = GetComponentInChildren<PortalVisual>(true);

        if (portalVisual) portalVisual.SetLinked(false);
    }

    public void LinkTo(Portal other)
    {
        linkedPortal = other;
        if (portalVisual) portalVisual.SetLinked(other != null);
    }

    public void Reposition(Vector3 worldPos, Quaternion worldRot)
    {
        transform.SetPositionAndRotation(worldPos, worldRot);

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // 리셋 알림
        physics?.OnPortalRepositioned();
        // teleport는 상태 없으면 굳이 없어도 됨. 있으면 호출해도 OK.
        // portalRenderer는 RT 유지라 보통 리셋 불필요
    }
}
