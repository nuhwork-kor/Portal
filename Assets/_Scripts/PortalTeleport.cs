using UnityEngine;

/// <summary>
/// 포탈 통과 수학
/// Player 위치 / 회전 보정
/// </summary>
public class PortalTeleport : MonoBehaviour
{
    Portal portal;

    [SerializeField]
    float exitOffset = 0.6f;

    private void Awake()
    {
        portal = GetComponent<Portal>();
    }

    public void Teleport(Transform target)
    {
        if (!portal.linkedPortal) return;
        Transform inPlane = portal.portalPlane;
        Transform outPlane = portal.linkedPortal.portalPlane;

        Vector3 newPos =
            PortalMath.TransformPosition(
                inPlane,
                outPlane,
                target.position,
                exitOffset
                );

        Quaternion newRot =
            PortalMath.TransformRotation(
                inPlane,
                outPlane,
                target.rotation
                );

        target.SetPositionAndRotation(newPos, newRot);
    }
}
