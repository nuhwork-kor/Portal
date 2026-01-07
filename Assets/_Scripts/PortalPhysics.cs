using System.Collections.Generic;
using UnityEngine;

public class PortalPhysics : MonoBehaviour
{
    Portal portal;

    float previousSide;
    PortalTravellerState state = PortalTravellerState.Outside;

    [SerializeField] Collider[] boundaryColliders;
    List<Collider> behindWalls = new List<Collider>();

    private void Awake()
    {
        portal = GetComponent<Portal>();
    }

    public void OnEnter(Collider other)
    {
        if (!portal.IsLinked) return;

        var traveller = other.GetComponentInParent<PortalTraveller>();
        if (!traveller) return;

        ResolveBehindWalls();

        foreach (var wall in behindWalls)
            traveller.IgnoreWall(wall, true);

        previousSide = Vector3.Dot(
            traveller.rb.worldCenterOfMass - portal.portalPlane.position,
            portal.portalPlane.forward
            );
        state = PortalTravellerState.HalfInside;
        EnableBoundary(true);
    }

    public void OnStay(Collider other)
    {
        if (state != PortalTravellerState.HalfInside) return;
        if (!portal.IsLinked) return;

        var traveller = other.GetComponentInParent<PortalTraveller>();
        if (!traveller) return;

        float currentSide = Vector3.Dot(
            traveller.rb.worldCenterOfMass - portal.portalPlane.position,
            portal.portalPlane.forward
            );

        if (previousSide < 0f && currentSide >= 0f)
        {
            portal.teleport.Teleport(traveller.transform);

            traveller.RestoreAllWalls();
            EnableBoundary(false);

            state = PortalTravellerState.Outside;
            previousSide = 0f;
            return;
        }

        previousSide = currentSide;
    }

    void ResolveBehindWalls()
    {
        behindWalls.Clear();

        Vector3 center =
            portal.portalPlane.position
            - portal.portalPlane.forward * 0.05f;

        Vector3 halfExtents = new Vector3(0.8f, 1.4f, 0.1f);

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            portal.portalPlane.rotation,
            LayerMask.GetMask("Wall"),
            QueryTriggerInteraction.Ignore
        );

        foreach (var col in hits)
            behindWalls.Add(col);
    }

    void EnableBoundary(bool active)
    {
        foreach (var col in boundaryColliders)
            col.enabled = active;
    }

    public void OnPortalRepositioned()
    {
        previousSide = 0f;
        state = PortalTravellerState.Outside;
    }
}
