using UnityEngine;

public class PortalPlacementResolver : MonoBehaviour
{
    [Header("Up vector resolve")]
    [SerializeField] private float minUpSqrMag = 0.0001f;

    [Header("Surface classify")]
    [Tooltip("normal·up 절대값이 이 값 이상이면 바닥/천장으로 간주")]
    [SerializeField, Range(0.5f, 0.99f)] private float floorCeilDotThreshold = 0.85f;

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
        if (hitNormal.sqrMagnitude < 1e-6f) return false;

        Vector3 normal = hitNormal.normalized;

        // 위치: 표면에서 살짝 띄움
        placePos = hitPoint + normal * surfaceOffset;

        // 너 프로젝트는 "forward = -normal"을 기준으로 맞춰놨으니 그대로 유지
        Vector3 forward = -normal;

        // 바닥/천장 판정
        float upDot = Mathf.Abs(Vector3.Dot(normal, Vector3.up));
        bool isFloorOrCeil = upDot >= floorCeilDotThreshold;

        Vector3 up;

        if (isFloorOrCeil)
        {
            // 바닥/천장: 카메라가 바라보는 방향을 기준으로 회전(수평 방향)
            up = Vector3.ProjectOnPlane(playerCamera.transform.forward, forward);
        }
        else
        {
            // 벽: 무조건 월드업 기준으로 수직 유지
            up = Vector3.ProjectOnPlane(Vector3.up, forward);
        }

        // fallback들
        if (up.sqrMagnitude < minUpSqrMag)
            up = Vector3.ProjectOnPlane(playerCamera.transform.up, forward);

        if (up.sqrMagnitude < minUpSqrMag)
            up = Vector3.ProjectOnPlane(Vector3.forward, forward);

        if (up.sqrMagnitude < minUpSqrMag) return false;

        up.Normalize();
        placeRot = Quaternion.LookRotation(forward, up);
        return true;
    }
}
