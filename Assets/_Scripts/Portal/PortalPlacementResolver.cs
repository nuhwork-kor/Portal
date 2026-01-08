using UnityEngine;

public class PortalPlacementResolver : MonoBehaviour
{
    [Header("Up vector resolve")]
    [SerializeField] private float minUpSqrMag = 0.0001f;

    public bool TryResolve(
        Camera playerCamera,
        Vector3 hitPoint,
        Vector3 hitNormal,
        float surfaceOffset,
        out Vector3 placePos,
        out Quaternion placeRot)
    {
        placePos = default;
        placeRot = default;

        if (!playerCamera) return false;
        if (hitNormal.sqrMagnitude < 1e-6f) return false; // ✅ 이게 맞음

        Vector3 normal = hitNormal.normalized;

        // 위치: 살짝 띄우기
        placePos = hitPoint + normal * surfaceOffset;

        // forward: 표면을 향하게 (포탈 앞면이 플레이어를 향하게 하려면 -normal)
        Vector3 forward = -normal;

        // up: 카메라 up을 forward 평면에 투영
        Vector3 up = Vector3.ProjectOnPlane(playerCamera.transform.up, forward);

        if (up.sqrMagnitude < minUpSqrMag)
            up = Vector3.ProjectOnPlane(playerCamera.transform.forward, forward);

        if (up.sqrMagnitude < minUpSqrMag)
            up = Vector3.ProjectOnPlane(Vector3.up, forward);

        if (up.sqrMagnitude < minUpSqrMag) return false;

        up.Normalize();
        placeRot = Quaternion.LookRotation(forward, up);
        return true;
    }
}
