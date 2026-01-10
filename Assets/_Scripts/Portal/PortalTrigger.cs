using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PortalTrigger : MonoBehaviour
{
    [SerializeField] private Portal portal;

    private void Reset()
    {
        // 같은 프리팹/계층이면 자동으로 잡힘
        portal = GetComponentInParent<Portal>();
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    private void Awake()
    {
        if (!portal) portal = GetComponentInParent<Portal>();
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!portal || !portal.IsPlaced) return;
        if (!portal.OtherPortal || !portal.OtherPortal.IsPlaced) return;

        // 플레이어(혹은 오브젝트)에 PortalTraveller가 있어야 함
        var traveller = other.GetComponentInParent<PortalTraveller>();
        if (!traveller) return;

        traveller.EnterPortal(portal, portal.OtherPortal, portal.WallColliderCached);
    }

    private void OnTriggerExit(Collider other)
    {
        var traveller = other.GetComponentInParent<PortalTraveller>();
        if (!traveller) return;

        traveller.ExitPortal(portal ? portal.WallColliderCached : null);
    }
}
