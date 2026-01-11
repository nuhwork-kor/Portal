// PortalSystem.cs
using UnityEngine;

public class PortalSystem : MonoBehaviour
{
    public enum PortalType { Blue, Orange }

    [Header("Refs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("Placement Mask")]
    [Tooltip("포탈을 붙일 수 있는 레이어(Wall/Ground 등)")]
    [SerializeField] private LayerMask placeableMask;

    [Tooltip("겹침 검사에 포함할 레이어(보통 placeableMask와 동일 추천)")]
    [SerializeField] private LayerMask blockingMask;

    [Header("Placement Tuning")]
    [SerializeField] private float surfaceOffset = 0.002f;

    [Header("Fit / Slide On Surface")]
    [Tooltip("포탈이 면 밖으로 나가면, 면 안쪽으로 자동으로 '밀어서' 온전히 들어오게 함")]
    [SerializeField] private bool autoSlideToFit = true;

    [Tooltip("면의 경계에서 이만큼 떨어진 곳까지를 유효 영역으로 봄(포탈 테두리/여유)")]
    [SerializeField] private float fitPadding = 0.02f; // 2cm

    [Header("Optional Overlap Check")]
    [Tooltip("주변 지오메트리와 겹치면 설치 실패 처리(코너/옆벽 침범 방지). 필요 없으면 끄기.")]
    [SerializeField] private bool useOverlapCheck = true;

    [Tooltip("포탈 평면 법선 방향으로 겹침 검사 두께(미터)")]
    [SerializeField] private float overlapCheckDepth = 0.15f;

    [Tooltip("겹침 검사에서 포탈 가로/세로에 추가로 더할 여유(미터)")]
    [SerializeField] private float overlapPadding = 0.01f;

    private void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;

        // blockingMask 미지정이면 placeableMask로
        if (blockingMask.value == 0) blockingMask = placeableMask;

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

        // 회전 계산(네 기존 방식 유지)
        Quaternion rot = ComputeRotationFromSurface(hitNormal, playerCamera.transform);

        // 핵심: 포탈이 "온전히" 들어가도록 hitPoint를 면 위에서 슬라이드/보정
        if (!TryGetFittedSurfacePoint(target, hitCollider, hitPoint, rot, out Vector3 fittedSurfacePoint))
            return false;

        // 최종 배치
        target.Reposition(hitCollider, fittedSurfacePoint, rot, surfaceOffset);

        // 상대 포탈 표면 갱신(있으면)
        target.RefreshSurfaceVisibility();
        if (target.OtherPortal) target.OtherPortal.RefreshSurfaceVisibility();

        return true;
    }

    private bool TryGetFittedSurfacePoint(Portal portal, Collider surface, Vector3 hitPoint, Quaternion portalRot, out Vector3 fittedPointOnSurface)
    {
        fittedPointOnSurface = hitPoint;

        if (!portal || !portal.SurfaceCollider)
            return false;

        // 포탈 가로/세로 반치수(월드 기준)
        GetPortalHalfSizeWorld(portal.SurfaceCollider, out float halfW, out float halfH);

        // 포탈 평면 축(월드)
        Vector3 axisR = portalRot * Vector3.right;
        Vector3 axisU = portalRot * Vector3.up;
        Vector3 axisN = portalRot * Vector3.forward;

        // 표면 콜라이더를 axisR/axisU로 투영했을 때의 min/max 범위
        if (!TryGetProjectionRange(surface, axisR, out float minR, out float maxR)) return false;
        if (!TryGetProjectionRange(surface, axisU, out float minU, out float maxU)) return false;

        // 유효 범위(포탈 반치수 + 패딩)
        float loR = minR + halfW + fitPadding;
        float hiR = maxR - halfW - fitPadding;
        float loU = minU + halfH + fitPadding;
        float hiU = maxU - halfH - fitPadding;

        // 아예 들어갈 공간이 없으면 설치 불가
        if (loR > hiR || loU > hiU)
            return false;

        // 현재 hitPoint의 투영값
        float sR = Vector3.Dot(axisR, hitPoint);
        float sU = Vector3.Dot(axisU, hitPoint);

        if (!autoSlideToFit)
        {
            // 자동 보정 없이 "완전히 들어가야만" 배치 허용
            if (sR < loR || sR > hiR) return false;
            if (sU < loU || sU > hiU) return false;

            fittedPointOnSurface = hitPoint;
        }
        else
        {
            // 자동 보정: 범위 안으로 클램프해서 X/Z 혹은 Y가 자동으로 밀림
            float cR = Mathf.Clamp(sR, loR, hiR);
            float cU = Mathf.Clamp(sU, loU, hiU);

            Vector3 shifted = hitPoint + axisR * (cR - sR) + axisU * (cU - sU);
            fittedPointOnSurface = shifted;
        }

        // 선택: 주변 지오메트리 침범하면 실패 처리
        if (useOverlapCheck)
        {
            Vector3 center = fittedPointOnSurface + axisN * surfaceOffset; // 실제 포탈 중심(벽에서 살짝 띄움)
            if (IsPlacementBlocked(portal, surface, center, portalRot, halfW, halfH))
                return false;
        }

        return true;
    }

    private bool IsPlacementBlocked(Portal portal, Collider surface, Vector3 center, Quaternion rot, float halfW, float halfH)
    {
        // 평면에 거의 붙어있는 얇은 박스로 주변 침범 검사
        // depth는 법선방향, 가로/세로는 포탈 반치수 + 여유
        Vector3 halfExtents = new Vector3(halfW + overlapPadding, halfH + overlapPadding, overlapCheckDepth * 0.5f);

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            rot,
            blockingMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (!c) continue;

            // 붙인 표면 자기 자신은 무시
            if (c == surface) continue;

            // 포탈 자기 콜라이더 무시(프리팹 구조 따라 자식 포함)
            if (portal.transform == c.transform || c.transform.IsChildOf(portal.transform))
                continue;

            // 반대 포탈도 무시(겹침 검사에 잡히면 귀찮음)
            if (portal.OtherPortal && (portal.OtherPortal.transform == c.transform || c.transform.IsChildOf(portal.OtherPortal.transform)))
                continue;

            // 그 외는 침범으로 판단
            return true;
        }

        return false;
    }

    private void GetPortalHalfSizeWorld(BoxCollider portalSurfaceCollider, out float halfW, out float halfH)
    {
        // SurfaceCollider의 local size를 lossyScale로 월드 크기로 변환
        Transform t = portalSurfaceCollider.transform;
        Vector3 lossy = t.lossyScale;

        float worldW = Mathf.Abs(portalSurfaceCollider.size.x * lossy.x);
        float worldH = Mathf.Abs(portalSurfaceCollider.size.y * lossy.y);

        halfW = worldW * 0.5f;
        halfH = worldH * 0.5f;
    }

    private bool TryGetProjectionRange(Collider col, Vector3 axis, out float min, out float max)
    {
        // axis는 정규화되어 있을 필요는 없지만, 수치 안정성을 위해 normalize 권장
        float mag = axis.magnitude;
        if (mag < 1e-6f)
        {
            min = max = 0f;
            return false;
        }
        Vector3 a = axis / mag;

        // BoxCollider면 OBB 코너로 더 정확하게
        if (col is BoxCollider bc)
        {
            Vector3[] corners = GetBoxColliderWorldCorners(bc);
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                float s = Vector3.Dot(a, corners[i]);
                if (s < min) min = s;
                if (s > max) max = s;
            }
            return true;
        }

        // 그 외는 bounds(AABB)로 대충(정밀도 떨어질 수 있음)
        {
            Vector3[] corners = GetBoundsWorldCorners(col.bounds);
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                float s = Vector3.Dot(a, corners[i]);
                if (s < min) min = s;
                if (s > max) max = s;
            }
            return true;
        }
    }

    private Vector3[] GetBoxColliderWorldCorners(BoxCollider bc)
    {
        Transform t = bc.transform;

        Vector3 c = t.TransformPoint(bc.center);
        Vector3 half = Vector3.Scale(bc.size, t.lossyScale) * 0.5f;

        Vector3 r = t.right * half.x;
        Vector3 u = t.up * half.y;
        Vector3 f = t.forward * half.z;

        // 8 corners
        return new Vector3[]
        {
            c + r + u + f,
            c + r + u - f,
            c + r - u + f,
            c + r - u - f,
            c - r + u + f,
            c - r + u - f,
            c - r - u + f,
            c - r - u - f
        };
    }

    private Vector3[] GetBoundsWorldCorners(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;

        return new Vector3[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };
    }

    private Quaternion ComputeRotationFromSurface(Vector3 surfaceNormal, Transform cam)
    {
        Vector3 forward = surfaceNormal.normalized;

        Vector3 right = Vector3.ProjectOnPlane(cam.right, forward);
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.ProjectOnPlane(cam.forward, forward);

        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right).normalized;
        right = Vector3.Cross(up, forward).normalized;

        return Quaternion.LookRotation(forward, up);
    }
}
