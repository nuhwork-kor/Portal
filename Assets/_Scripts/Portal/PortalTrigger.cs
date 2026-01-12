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
        if (triggerCol) triggerCol.isTrigger = true;
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

        // 워프/충돌 Ignore 담당
        var traveller = other.GetComponentInParent<PortalTraveller>();
        if (traveller != null)
        {
            traveller.EnterPortal(portal, portal.OtherPortal, portal.WallColliderCached);
        }

        // 비주얼 클론 담당
        var clone = other.GetComponentInParent<PortalCloneVisual>();
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
            traveller.NotifyTriggerExit(portal);
        }

        var clone = other.GetComponentInParent<PortalCloneVisual>();
        if (clone != null)
        {
            clone.NotifyTriggerExit(portal);
        }
    }
}
