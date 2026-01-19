using UnityEngine;

[DisallowMultipleComponent]
public class PortalManager : MonoBehaviour
{
    public enum PortalType { Blue, Orange }                                       // 포탈 종류(블루/오렌지)

    [Header("Refs")]
    [SerializeField] private Camera playerCamera;                                 // 포탈 설치/회전 계산에 사용할 플레이어 카메라
    [SerializeField] private Portal bluePortal;                                   // 블루 포탈 참조
    [SerializeField] private Portal orangePortal;                                 // 오렌지 포탈 참조

    [Header("Placement Mask")]
    [Tooltip("포탈을 배치할 수 있는 레이어(Wall/Ground 등)")]
    [SerializeField] private LayerMask placeableMask;                             // 포탈 설치 가능한 표면 레이어 마스크

    [Tooltip("겹침/방해 검사에 사용할 레이어(보통 placeableMask와 동일 추천)")]
    [SerializeField] private LayerMask blockingMask;                              // 설치를 막는 오브젝트 검사 레이어 마스크

    [Header("Placement Tuning")]
    [SerializeField] private float surfaceOffset = 0.002f;                        // 표면에서 살짝 띄우는 오프셋(z-fighting 방지)

    [Header("Fit / Slide On Surface")]
    [Tooltip("포탈이 표면 경계에 걸리면 자동으로 '밀어서' 들어오게 할지")]
    [SerializeField] private bool autoSlideToFit = true;                          // 경계 밖이면 자동으로 안쪽으로 클램프할지 여부

    [Tooltip("표면 경계에서 이만큼 여유를 두고 배치(포탈 테두리/여백)")]
    [SerializeField] private float fitPadding = 0.02f;                            // 표면 가장자리로부터 최소 여유 거리(미터)

    [Header("Optional Overlap Check")]
    [Tooltip("주변 오브젝트와 겹치면 배치 실패(옵션)")]
    [SerializeField] private bool useOverlapCheck = true;                         // OverlapBox로 설치 방해물 검사 여부

    [Tooltip("겹침 검사 깊이(표면 법선 방향으로 박스 두께)")]
    [SerializeField] private float overlapCheckDepth = 0.15f;                     // 겹침 검사 깊이(미터)

    [Tooltip("겹침 검사에서 포탈 폭/높이에 추가로 더할 패딩")]
    [SerializeField] private float overlapPadding = 0.01f;                        // 겹침 박스 패딩(미터)

    public Camera PlayerCamera => playerCamera;                                   // 외부에서 플레이어 카메라 접근
    public Portal BluePortal => bluePortal;                                       // 외부에서 블루 포탈 접근
    public Portal OrangePortal => orangePortal;                                   // 외부에서 오렌지 포탈 접근

    /// <summary>
    /// Unity Awake: 참조 해결, 링크 보장, 초기 포탈 상태를 리셋한다.
    /// </summary>
    private void Awake()
    {
        ResolveRefs();                                                            // 카메라/포탈 참조 자동 탐색

        if (blockingMask.value == 0) blockingMask = placeableMask;                // blockingMask가 비어있으면 placeableMask를 기본값으로 사용

        EnsureLinked();                                                           // 블루/오렌지 포탈 상호 링크 설정

        ResetPortals();                                                           // 시작 시 포탈을 미배치 상태로 초기화
    }

    /// <summary>
    /// 인스펙터에 참조가 없을 때 자동으로 Camera/Portal을 찾아 할당한다.
    /// </summary>
    private void ResolveRefs()
    {
        if (!playerCamera) playerCamera = Camera.main;                            // playerCamera가 없으면 메인 카메라 사용

        if (!bluePortal || !orangePortal)                                         // 포탈 참조가 비어있으면
        {
            var portals = Object.FindObjectsByType<Portal>(                       // 씬의 Portal 컴포넌트를 모두 찾고
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (var p in portals)                                            // 포탈들을 순회하면서
            {
                if (!p) continue;                                                 // null이면 스킵

                string n = p.name.ToLowerInvariant();                             // 이름을 소문자로 변환
                if (!bluePortal && n.Contains("blue")) bluePortal = p;            // 이름에 blue가 있으면 블루 포탈로
                else if (!orangePortal && n.Contains("orange")) orangePortal = p; // 이름에 orange가 있으면 오렌지 포탈로
            }
        }
    }

    /// <summary>
    /// 두 포탈을 서로 링크한다. (이미 링크돼 있어도 재설정)
    /// </summary>
    public void EnsureLinked()
    {
        if (!bluePortal || !orangePortal) return;                                 // 둘 중 하나라도 없으면 링크 불가

        bluePortal.LinkTo(orangePortal);                                          // 블루 -> 오렌지 링크
        orangePortal.LinkTo(bluePortal);                                          // 오렌지 -> 블루 링크
    }

    /// <summary>
    /// 모든 포탈을 미배치 상태로 리셋한다. (스테이지 시작/전환 등 초기화용)
    /// </summary>
    public void ResetPortals()
    {
        if (bluePortal) bluePortal.SetPlaced(false);                              // 블루 포탈 미배치 처리
        if (orangePortal) orangePortal.SetPlaced(false);                          // 오렌지 포탈 미배치 처리

        if (bluePortal) bluePortal.RefreshSurfaceVisibility();                    // 표면 표시 여부 갱신
        if (orangePortal) orangePortal.RefreshSurfaceVisibility();                // 표면 표시 여부 갱신
    }

    /// <summary>
    /// 특정 포탈만 미배치 상태로 리셋한다.
    /// </summary>
    /// <param name="type">리셋할 포탈 타입</param>
    public void ResetPortal(PortalType type)
    {
        Portal p = GetPortal(type);                                               // 타입에 해당하는 포탈 가져오기
        if (!p) return;                                                           // 없으면 중단

        p.SetPlaced(false);                                                       // 미배치 처리
        p.RefreshSurfaceVisibility();                                             // 표면 표시 갱신

        if (p.OtherPortal) p.OtherPortal.RefreshSurfaceVisibility();              // 반대편 포탈도 표면 표시 갱신(쌍 동기화)
    }

    /// <summary>
    /// 타입에 해당하는 포탈을 반환한다.
    /// </summary>
    /// <param name="type">포탈 타입(Blue/Orange)</param>
    /// <returns>해당 포탈(없으면 null)</returns>
    public Portal GetPortal(PortalType type)
    {
        return (type == PortalType.Blue) ? bluePortal : orangePortal;             // 타입에 따라 포탈 반환
    }

    /// <summary>
    /// 레이캐스트 히트 정보로 포탈 배치를 시도한다.
    /// </summary>
    /// <param name="type">배치할 포탈 타입</param>
    /// <param name="hitPoint">히트 지점</param>
    /// <param name="hitNormal">히트 표면 노멀</param>
    /// <param name="hitCollider">히트된 표면 콜라이더</param>
    /// <returns>배치 성공 여부</returns>
    public bool TryPlacePortal(PortalType type, Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider)
    {
        if (!playerCamera || !hitCollider) return false;                          // 카메라/표면 콜라이더가 없으면 실패

        if ((placeableMask.value & (1 << hitCollider.gameObject.layer)) == 0)     // 설치 가능 레이어가 아니면
            return false;                                                         // 배치 실패

        Portal target = GetPortal(type);                                          // 배치할 포탈 선택
        if (!target) return false;                                                // 포탈이 없으면 실패

        Quaternion rot = ComputeRotationFromSurface(hitNormal, playerCamera.transform); // 표면 노멀 기반 포탈 회전 계산

        if (!TryGetFittedSurfacePoint(target, hitCollider, hitPoint, rot, out Vector3 fittedSurfacePoint)) // 경계/겹침 검사
            return false;                                                         // 배치 불가면 실패

        target.Reposition(hitCollider, fittedSurfacePoint, rot, surfaceOffset);   // 실제 포탈 위치/회전 적용 및 placed 처리

        target.RefreshSurfaceVisibility();                                        // 표면 표시 갱신
        if (target.OtherPortal) target.OtherPortal.RefreshSurfaceVisibility();    // 반대편도 표시 갱신

        var openAnim = target.GetComponent<PortalSurfaceOpenAnimator>();          // 생성 애니메이터가 붙어있으면
        if (openAnim) openAnim.Play();                                            // 점->타원 확장 애니메이션 실행

        return true;                                                              // 성공
    }

    /// <summary>
    /// 표면 경계 안에 포탈이 들어오도록(또는 실패하도록) 히트 지점을 보정한다.
    /// </summary>
    /// <param name="portal">배치할 포탈</param>
    /// <param name="surface">배치할 표면 콜라이더</param>
    /// <param name="hitPoint">원래 히트 지점</param>
    /// <param name="portalRot">포탈 회전</param>
    /// <param name="fittedPointOnSurface">보정된 표면 위 포인트</param>
    /// <returns>보정 성공 여부</returns>
    private bool TryGetFittedSurfacePoint(Portal portal, Collider surface, Vector3 hitPoint, Quaternion portalRot, out Vector3 fittedPointOnSurface)
    {
        fittedPointOnSurface = hitPoint;                                          // 기본은 입력 히트 포인트

        if (!portal || !portal.SurfaceCollider)                                   // 포탈/표면 콜라이더가 없으면
            return false;                                                         // 보정 불가

        GetPortalHalfSizeWorld(portal.SurfaceCollider, out float halfW, out float halfH); // 포탈 반폭/반높이 계산

        Vector3 axisR = portalRot * Vector3.right;                                // 포탈의 오른쪽 축(월드)
        Vector3 axisU = portalRot * Vector3.up;                                   // 포탈의 위쪽 축(월드)
        Vector3 axisN = portalRot * Vector3.forward;                              // 포탈의 노멀 축(월드)

        if (!TryGetProjectionRange(surface, axisR, out float minR, out float maxR)) return false; // 표면을 axisR로 투영한 min/max
        if (!TryGetProjectionRange(surface, axisU, out float minU, out float maxU)) return false; // 표면을 axisU로 투영한 min/max

        float loR = minR + halfW + fitPadding;                                    // axisR 방향 하한(포탈이 완전히 들어오도록)
        float hiR = maxR - halfW - fitPadding;                                    // axisR 방향 상한
        float loU = minU + halfH + fitPadding;                                    // axisU 방향 하한
        float hiU = maxU - halfH - fitPadding;                                    // axisU 방향 상한

        if (loR > hiR || loU > hiU)                                               // 들어갈 공간이 없으면
            return false;                                                         // 배치 불가

        float sR = Vector3.Dot(axisR, hitPoint);                                  // 히트 포인트의 axisR 스칼라 좌표
        float sU = Vector3.Dot(axisU, hitPoint);                                  // 히트 포인트의 axisU 스칼라 좌표

        if (!autoSlideToFit)                                                      // 자동 슬라이드가 꺼져 있으면
        {
            if (sR < loR || sR > hiR) return false;                               // 경계 밖이면 실패
            if (sU < loU || sU > hiU) return false;                               // 경계 밖이면 실패
            fittedPointOnSurface = hitPoint;                                      // 그대로 사용
        }
        else                                                                      // 자동 슬라이드가 켜져 있으면
        {
            float cR = Mathf.Clamp(sR, loR, hiR);                                 // axisR 좌표를 경계 안으로 클램프
            float cU = Mathf.Clamp(sU, loU, hiU);                                 // axisU 좌표를 경계 안으로 클램프

            Vector3 shifted = hitPoint + axisR * (cR - sR) + axisU * (cU - sU);   // 차이만큼 표면 위에서 이동
            fittedPointOnSurface = shifted;                                       // 보정된 포인트 사용
        }

        if (useOverlapCheck)                                                      // 겹침 검사 옵션이 켜져 있으면
        {
            Vector3 center = fittedPointOnSurface + axisN * surfaceOffset;        // overlap 박스 중심(표면 바깥쪽으로 약간)
            if (IsPlacementBlocked(portal, surface, center, portalRot, halfW, halfH)) // 방해물이 있으면
                return false;                                                     // 배치 실패
        }

        return true;                                                              // 최종 보정 성공
    }

    /// <summary>
    /// OverlapBox로 포탈 설치 위치 주변의 방해 오브젝트 존재 여부를 검사한다.
    /// </summary>
    /// <param name="portal">배치할 포탈</param>
    /// <param name="surface">배치 표면 콜라이더</param>
    /// <param name="center">검사 박스 중심</param>
    /// <param name="rot">검사 박스 회전</param>
    /// <param name="halfW">포탈 반폭</param>
    /// <param name="halfH">포탈 반높이</param>
    /// <returns>방해물이 있으면 true</returns>
    private bool IsPlacementBlocked(Portal portal, Collider surface, Vector3 center, Quaternion rot, float halfW, float halfH)
    {
        Vector3 halfExtents = new Vector3(halfW + overlapPadding, halfH + overlapPadding, overlapCheckDepth * 0.5f); // 검사 박스 반크기

        Collider[] hits = Physics.OverlapBox(                                     // OverlapBox로 주변 콜라이더들 획득
            center,
            halfExtents,
            rot,
            blockingMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)                                     // 히트된 콜라이더들을 순회하며
        {
            Collider c = hits[i];                                                 // 현재 콜라이더
            if (!c) continue;                                                     // null이면 스킵

            if (c == surface) continue;                                           // 배치 표면 자체는 무시

            if (portal.transform == c.transform || c.transform.IsChildOf(portal.transform)) // 자기 포탈 계층은 무시
                continue;

            if (portal.OtherPortal && (portal.OtherPortal.transform == c.transform || c.transform.IsChildOf(portal.OtherPortal.transform))) // 반대편 포탈 계층도 무시
                continue;

            return true;                                                          // 그 외가 걸리면 방해물 존재
        }

        return false;                                                             // 방해물 없음
    }

    /// <summary>
    /// 포탈 표면(BoxCollider)의 월드 반폭/반높이를 계산한다.
    /// </summary>
    /// <param name="portalSurfaceCollider">포탈 표면 콜라이더</param>
    /// <param name="halfW">월드 반폭</param>
    /// <param name="halfH">월드 반높이</param>
    private void GetPortalHalfSizeWorld(BoxCollider portalSurfaceCollider, out float halfW, out float halfH)
    {
        Transform t = portalSurfaceCollider.transform;                            // 콜라이더 Transform
        Vector3 lossy = t.lossyScale;                                             // 월드 스케일(손실 스케일)

        float worldW = Mathf.Abs(portalSurfaceCollider.size.x * lossy.x);         // 월드 폭
        float worldH = Mathf.Abs(portalSurfaceCollider.size.y * lossy.y);         // 월드 높이

        halfW = worldW * 0.5f;                                                    // 반폭
        halfH = worldH * 0.5f;                                                    // 반높이
    }

    /// <summary>
    /// 표면 콜라이더를 특정 축(axis)으로 투영했을 때의 스칼라 min/max 범위를 구한다.
    /// </summary>
    /// <param name="col">대상 콜라이더</param>
    /// <param name="axis">투영 축(월드)</param>
    /// <param name="min">최소</param>
    /// <param name="max">최대</param>
    /// <returns>성공 여부</returns>
    private bool TryGetProjectionRange(Collider col, Vector3 axis, out float min, out float max)
    {
        float mag = axis.magnitude;                                               // 축 길이
        if (mag < 1e-6f)                                                          // 축이 너무 짧으면
        {
            min = max = 0f;                                                       // 안전 초기화
            return false;                                                         // 실패 반환
        }
        Vector3 a = axis / mag;                                                   // 정규화된 축

        if (col is BoxCollider bc)                                                // BoxCollider면 코너를 정확히 계산
        {
            Vector3[] corners = GetBoxColliderWorldCorners(bc);                   // 월드 코너 8개
            min = float.PositiveInfinity;                                         // min 초기화
            max = float.NegativeInfinity;                                         // max 초기화
            for (int i = 0; i < corners.Length; i++)                              // 코너 순회
            {
                float s = Vector3.Dot(a, corners[i]);                             // 축에 대한 투영 스칼라
                if (s < min) min = s;                                             // min 갱신
                if (s > max) max = s;                                             // max 갱신
            }
            return true;                                                          // 성공
        }

        // fallback: bounds(AABB)
        {
            Vector3[] corners = GetBoundsWorldCorners(col.bounds);                // AABB 코너 8개
            min = float.PositiveInfinity;                                         // min 초기화
            max = float.NegativeInfinity;                                         // max 초기화
            for (int i = 0; i < corners.Length; i++)                              // 코너 순회
            {
                float s = Vector3.Dot(a, corners[i]);                             // 축 투영
                if (s < min) min = s;                                             // min 갱신
                if (s > max) max = s;                                             // max 갱신
            }
            return true;                                                          // 성공
        }
    }

    /// <summary>
    /// BoxCollider의 월드 코너 8개를 계산한다. (중심+축 벡터 조합)
    /// </summary>
    /// <param name="bc">BoxCollider</param>
    /// <returns>월드 코너 8개 배열</returns>
    private Vector3[] GetBoxColliderWorldCorners(BoxCollider bc)
    {
        Transform t = bc.transform;                                               // 콜라이더 Transform

        Vector3 c = t.TransformPoint(bc.center);                                  // 월드 중심점
        Vector3 half = Vector3.Scale(bc.size, t.lossyScale) * 0.5f;               // 월드 반크기

        Vector3 r = t.right * half.x;                                             // 오른쪽 방향 벡터
        Vector3 u = t.up * half.y;                                                // 위 방향 벡터
        Vector3 f = t.forward * half.z;                                           // 전방 방향 벡터

        return new Vector3[]                                                      // 8개 코너 조합
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

    /// <summary>
    /// Bounds(AABB)의 월드 코너 8개를 반환한다.
    /// </summary>
    /// <param name="b">Bounds</param>
    /// <returns>월드 코너 8개 배열</returns>
    private Vector3[] GetBoundsWorldCorners(Bounds b)
    {
        Vector3 min = b.min;                                                      // AABB 최소점
        Vector3 max = b.max;                                                      // AABB 최대점

        return new Vector3[]                                                      // 8개 코너 조합
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

    /// <summary>
    /// 표면 노멀 + 카메라 방향을 이용해, 포탈이 자연스럽게 "카메라를 기준으로" 회전하도록 포탈 회전을 계산한다.
    /// </summary>
    /// <param name="surfaceNormal">표면 노멀</param>
    /// <param name="cam">플레이어 카메라 Transform</param>
    /// <returns>포탈 배치 회전</returns>
    private Quaternion ComputeRotationFromSurface(Vector3 surfaceNormal, Transform cam)
    {
        Vector3 forward = surfaceNormal.normalized;                               // 포탈 forward는 표면 노멀

        Vector3 right = Vector3.ProjectOnPlane(cam.right, forward);               // 카메라 right를 노멀 평면에 투영
        if (right.sqrMagnitude < 1e-6f)                                           // right가 불안정하면
            right = Vector3.ProjectOnPlane(cam.forward, forward);                 // cam.forward를 대신 투영

        right.Normalize();                                                        // right 정규화
        Vector3 up = Vector3.Cross(forward, right).normalized;                    // forward/right로 up 생성
        right = Vector3.Cross(up, forward).normalized;                            // 직교성 보정(정확한 right 재계산)

        return Quaternion.LookRotation(forward, up);                               // 최종 회전 반환
    }
}
