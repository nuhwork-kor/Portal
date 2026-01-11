using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PortalTrigger : MonoBehaviour
{
    [SerializeField] private Portal portal;
    private Collider triggerCol;

    private void Reset()
    {
        portal = GetComponentInParent<Portal>();
        triggerCol = GetComponent<Collider>();
        if (triggerCol) triggerCol.isTrigger = true;
    }

    private void Awake()
    {
        if (!portal) portal = GetComponentInParent<Portal>();

        triggerCol = GetComponent<Collider>();
        triggerCol.isTrigger = true;
    }

    private bool IsPortalReady()
    {
        return portal &&
               portal.IsPlaced &&
               portal.OtherPortal &&
               portal.OtherPortal.IsPlaced;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPortalReady()) return;
        if (other.isTrigger) return;

        // 워프 대상
        var traveller = other.GetComponentInParent<PortalTraveller>();
        if (traveller != null)
        {
            traveller.EnterPortal(portal, portal.OtherPortal, portal.WallColliderCached);
        }

        // 클론(반쯤 걸쳤을 때 반대편 표시)
        var clone = other.GetComponentInParent<PortalObjectClone>();
        if (clone != null)
        {
            clone.Begin(portal, portal.OtherPortal);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.isTrigger) return;

        var traveller = other.GetComponentInParent<PortalTraveller>();
        if (traveller != null)
        {
            // 벽 ignore 해제는 traveller가 안전할 때 하도록
            traveller.NotifyTriggerExit(portal);
        }

        var clone = other.GetComponentInParent<PortalObjectClone>();
        if (clone != null)
        {
            clone.End(portal);
        }
    }
}
