using UnityEngine;

public class PortalSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera playerCamera;

    [Header("Portals")]
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("Modules")]
    [SerializeField] private PortalPlacementResolver resolver;
    [SerializeField] private PortalPlacementValidator validator;

    [Header("Masks")]
    [SerializeField] private LayerMask placeableSurfaceMask;
    [SerializeField] private LayerMask overlapMask;

    [Header("Placement")]
    [SerializeField] private float surfaceOffset = 0.01f;

    private void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;
        if (!resolver) resolver = FindAnyObjectByType<PortalPlacementResolver>();
        if (!validator) validator = FindAnyObjectByType<PortalPlacementValidator>();

        if (!bluePortal || !orangePortal)
        {
            Debug.LogError("[PortalSystem] Missing portal refs. Assign Blue/Orange portals.");
            enabled = false;
            return;
        }

        // 링크 관계는 미리 세팅해도 됨(PortalRender가 “둘 다 배치됨” 조건으로만 사용)
        bluePortal.LinkTo(orangePortal);
        orangePortal.LinkTo(bluePortal);

        // 시작은 배치 안 됨
        bluePortal.SetPlaced(false);
        orangePortal.SetPlaced(false);
    }

    public bool PlacePortal(
        PortalGunController.PortalShotType type,
        Vector3 hitPoint,
        Vector3 hitNormal,
        Collider hitCollider)
    {
        if (!playerCamera || !resolver || !validator) return false;
        if (!hitCollider) return false;

        // 표면 레이어 체크
        if ((placeableSurfaceMask.value & (1 << hitCollider.gameObject.layer)) == 0)
            return false;

        Portal target = (type == PortalGunController.PortalShotType.Blue) ? bluePortal : orangePortal;
        if (!target) return false;

        // Resolver
        if (!resolver.TryResolve(playerCamera, hitPoint, hitNormal, surfaceOffset, out Vector3 placePos, out Quaternion placeRot))
            return false;

        // Validator
        var req = new PortalPlacementValidator.Request
        {
            portal = target,
            surfaceCollider = hitCollider,
            placePosition = placePos,
            placeRotation = placeRot,
            placeableMask = placeableSurfaceMask,
            overlapMask = overlapMask
        };

        if (!validator.CanPlace(req, out string reason))
        {
            Debug.Log($"[PortalSystem] Place denied: {reason}");
            return false;
        }

        // 확정
        target.Reposition(placePos, placeRot);
        return true;
    }
}
