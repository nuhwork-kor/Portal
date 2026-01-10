using UnityEngine;

public class PortalSystem : MonoBehaviour
{
    public enum PortalType { Blue, Orange }

    [Header("Refs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("Placement Mask")]
    [SerializeField] private LayerMask placeableMask;   // Wall/Ground

    [Header("Placement Tuning")]
    [SerializeField] private float surfaceOffset = 0.002f;

    private void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;

        // 링크 고정
        if (bluePortal && orangePortal)
        {
            bluePortal.LinkTo(orangePortal);
            orangePortal.LinkTo(bluePortal);

            bluePortal.SetPlaced(false);
            orangePortal.SetPlaced(false);
        }
    }

    public bool TryPlacePortal(PortalType type, Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider)
    {
        if (!playerCamera || !hitCollider) return false;

        // 배치 가능 레이어인지 체크
        if ((placeableMask.value & (1 << hitCollider.gameObject.layer)) == 0)
            return false;

        Portal target = (type == PortalType.Blue) ? bluePortal : orangePortal;
        if (!target) return false;

        Quaternion rot = ComputeRotationFromSurface(hitNormal, playerCamera.transform);

        target.Reposition(hitCollider, hitPoint, rot, surfaceOffset);
        return true;
    }

    private Quaternion ComputeRotationFromSurface(Vector3 surfaceNormal, Transform cam)
    {
        // ★중요: 포탈 "앞면"이 플레이어를 보게 하려면 forward = surfaceNormal
        // 애매하면 애매할수도있는데, 네 Screen 메쉬가 뒤집혀 있으면 -surfaceNormal이 맞을 수도 있음.
        Vector3 forward = surfaceNormal.normalized;

        // 카메라 오른쪽을 표면에 투영해서 "포탈의 오른쪽"으로 사용
        Vector3 right = Vector3.ProjectOnPlane(cam.right, forward);
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.ProjectOnPlane(cam.forward, forward);

        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right).normalized;

        // right를 다시 정규화(수치 오차)
        right = Vector3.Cross(up, forward).normalized;

        return Quaternion.LookRotation(forward, up);
    }
}
