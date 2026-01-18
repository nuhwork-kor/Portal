// PortalManager.cs
using UnityEngine;

[DisallowMultipleComponent]
public class PortalManager : MonoBehaviour
{
    public enum PortalType { Blue, Orange }

    [Header("Refs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("Placement Mask")]
    [Tooltip("포탈을 설치할 수 있는 레이어(Wall/Ground 등)")]
    [SerializeField] private LayerMask placeableMask;

    [Tooltip("겹침/차단 검사에 사용할 레이어(보통 placeableMask와 동일 추천)")]
    [SerializeField] private LayerMask blockingMask;

    [Header("Placement Tuning")]
    [SerializeField] private float surfaceOffset = 0.002f;

    [Header("Fit / Slide On Surface")]
    [Tooltip("포탈이 표면 가장자리에 걸리면 자동으로 '밀어서' 들어오게 함")]
    [SerializeField] private bool autoSlideToFit = true;

    [Tooltip("표면 경계에서 이만큼 안쪽으로 들어와야 설치 허용(포탈 테두리/여유)")]
    [SerializeField] private float fitPadding = 0.02f; // 2cm

    [Header("Optional Overlap Check")]
    [Tooltip("주변 오브젝트와 겹치면 설치 거부(선택)")]
    [SerializeField] private bool useOverlapCheck = true;

    [Tooltip("겹침 검사 깊이(표면 법선 방향)")]
    [SerializeField] private float overlapCheckDepth = 0.15f;

    [Tooltip("겹침 검사에서 포탈 가로/세로 추가 여유(패딩)")]
    [SerializeField] private float overlapPadding = 0.01f;

    public Camera PlayerCamera => playerCamera;
    public Portal BluePortal => bluePortal;
    public Portal OrangePortal => orangePortal;

    private void Awake()
    {
        ResolveRefs();

        // blockingMask 비어있으면 placeableMask로
        if (blockingMask.value == 0) blockingMask = placeableMask;

        EnsureLinked();

        // 시작은 “미설치”
        ResetPortals();
    }

    private void ResolveRefs()
    {
        if (!playerCamera) playerCamera = Camera.main;

        // 포탈 자동탐색(인스펙터 할당이 최우선)
        if (!bluePortal || !orangePortal)
        {
            var portals = Object.FindObjectsByType<Portal>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (var p in portals)
            {
                if (!p) continue;

                string n = p.name.ToLowerInvariant();
                if (!bluePortal && n.Contains("blue")) bluePortal = p;
                else if (!orangePortal && n.Contains("orange")) orangePortal = p;
            }
        }
    }

    /// <summary>
    /// 포탈 쌍 링크 보장(한쪽만 링크되어도 다시 맞춰줌)
    /// </summary>
    public void EnsureLinked()
    {
        if (!bluePortal || !orangePortal) return;

        bluePortal.LinkTo(orangePortal);
        orangePortal.LinkTo(bluePortal);
    }

    /// <summary>
    /// 스테이지 넘어갈 때 등: 포탈 “초기화(제거)” 용도
    /// - 두 포탈을 미설치 상태로 되돌림
    /// - SurfaceVisibility도 갱신
    /// </summary>
    public void ResetPortals()
    {
        if (bluePortal) bluePortal.SetPlaced(false);
        if (orangePortal) orangePortal.SetPlaced(false);

        if (bluePortal) bluePortal.RefreshSurfaceVisibility();
        if (orangePortal) orangePortal.RefreshSurfaceVisibility();
    }

    /// <summary>
    /// 특정 포탈만 초기화하고 싶을 때
    /// </summary>
    public void ResetPortal(PortalType type)
    {
        Portal p = GetPortal(type);
        if (!p) return;

        p.SetPlaced(false);
        p.RefreshSurfaceVisibility();

        // 상대 포탈도 화면 갱신은 해주는 게 자연스러움
        if (p.OtherPortal) p.OtherPortal.RefreshSurfaceVisibility();
    }

    public Portal GetPortal(PortalType type)
    {
        return (type == PortalType.Blue) ? bluePortal : orangePortal;
    }

    public bool TryPlacePortal(PortalType type, Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider)
    {
        if (!playerCamera || !hitCollider) return false;

        // 설치 가능 레이어인지
        if ((placeableMask.value & (1 << hitCollider.gameObject.layer)) == 0)
            return false;

        Portal target = GetPortal(type);
        if (!target) return false;

        // 회전 계산(표면 법선이 forward)
        Quaternion rot = ComputeRotationFromSurface(hitNormal, playerCamera.transform);

        // hitPoint를 “포탈이 완전히 들어오도록” 피팅/슬라이드
        if (!TryGetFittedSurfacePoint(target, hitCollider, hitPoint, rot, out Vector3 fittedSurfacePoint))
            return false;

        // 실제 배치
        target.Reposition(hitCollider, fittedSurfacePoint, rot, surfaceOffset);

        // 표면/가림 갱신
        target.RefreshSurfaceVisibility();
        if (target.OtherPortal) target.OtherPortal.RefreshSurfaceVisibility();

        var openAnim = target.GetComponent<PortalSurfaceOpenAnimator>();
        if (openAnim) openAnim.Play();

        return true;
    }

    private bool TryGetFittedSurfacePoint(Portal portal, Collider surface, Vector3 hitPoint, Quaternion portalRot, out Vector3 fittedPointOnSurface)
    {
        fittedPointOnSurface = hitPoint;

        if (!portal || !portal.SurfaceCollider)
            return false;

        // 포탈 반사이즈(월드)
        GetPortalHalfSizeWorld(portal.SurfaceCollider, out float halfW, out float halfH);

        Vector3 axisR = portalRot * Vector3.right;
        Vector3 axisU = portalRot * Vector3.up;
        Vector3 axisN = portalRot * Vector3.forward;

        // 표면 콜라이더를 axisR/axisU로 투영한 min/max
        if (!TryGetProjectionRange(surface, axisR, out float minR, out float maxR)) return false;
        if (!TryGetProjectionRange(surface, axisU, out float minU, out float maxU)) return false;

        // 설치 가능한 투영 범위(포탈 사이즈 + 패딩 고려)
        float loR = minR + halfW + fitPadding;
        float hiR = maxR - halfW - fitPadding;
        float loU = minU + halfH + fitPadding;
        float hiU = maxU - halfH - fitPadding;

        if (loR > hiR || loU > hiU)
            return false;

        float sR = Vector3.Dot(axisR, hitPoint);
        float sU = Vector3.Dot(axisU, hitPoint);

        if (!autoSlideToFit)
        {
            if (sR < loR || sR > hiR) return false;
            if (sU < loU || sU > hiU) return false;
            fittedPointOnSurface = hitPoint;
        }
        else
        {
            float cR = Mathf.Clamp(sR, loR, hiR);
            float cU = Mathf.Clamp(sU, loU, hiU);

            Vector3 shifted = hitPoint + axisR * (cR - sR) + axisU * (cU - sU);
            fittedPointOnSurface = shifted;
        }

        // 겹침 검사(선택)
        if (useOverlapCheck)
        {
            Vector3 center = fittedPointOnSurface + axisN * surfaceOffset;
            if (IsPlacementBlocked(portal, surface, center, portalRot, halfW, halfH))
                return false;
        }

        return true;
    }

    private bool IsPlacementBlocked(Portal portal, Collider surface, Vector3 center, Quaternion rot, float halfW, float halfH)
    {
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

            if (c == surface) continue;

            if (portal.transform == c.transform || c.transform.IsChildOf(portal.transform))
                continue;

            if (portal.OtherPortal && (portal.OtherPortal.transform == c.transform || c.transform.IsChildOf(portal.OtherPortal.transform)))
                continue;

            return true;
        }

        return false;
    }

    private void GetPortalHalfSizeWorld(BoxCollider portalSurfaceCollider, out float halfW, out float halfH)
    {
        Transform t = portalSurfaceCollider.transform;
        Vector3 lossy = t.lossyScale;

        float worldW = Mathf.Abs(portalSurfaceCollider.size.x * lossy.x);
        float worldH = Mathf.Abs(portalSurfaceCollider.size.y * lossy.y);

        halfW = worldW * 0.5f;
        halfH = worldH * 0.5f;
    }

    private bool TryGetProjectionRange(Collider col, Vector3 axis, out float min, out float max)
    {
        float mag = axis.magnitude;
        if (mag < 1e-6f)
        {
            min = max = 0f;
            return false;
        }
        Vector3 a = axis / mag;

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

        // fallback: bounds(AABB)
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
