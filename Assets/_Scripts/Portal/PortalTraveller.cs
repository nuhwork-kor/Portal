using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PortalTraveller : MonoBehaviour
{
    [Header("Warp Rule")]
    [SerializeField] private float planeCrossEpsilon = 0.02f;                     // 평면을 이만큼 넘었을 때만 워프 판정(오차/떨림 방지)
    [SerializeField] private float teleportCooldown = 0.05f;                      // 워프 직후 재진입 방지 쿨다운(초)

    [Header("Tuning")]
    [SerializeField] private float exitOffset = 0.0f;                             // 출구 포탈 방향으로 추가로 밀어내는 오프셋

    [Header("Impact SFX (optional)")]
    [Tooltip("충돌 속도가 이 이상이면 ObjImpact_Cube 재생")]
    [SerializeField] private float impactMinSpeed = 1.5f;                         // 충돌 사운드 재생 최소 속도

    private Rigidbody rb;                                                         // 이 오브젝트의 Rigidbody
    private Collider col;                                                         // 이 오브젝트의 Collider
    private CapsuleCollider capsule;                                              // 캐릭터면 캡슐 콜라이더(센터 계산에 사용)

    private Portal inPortal;                                                      // 현재 입구 포탈
    private Portal outPortal;                                                     // 현재 출구 포탈

    private Collider wallCollider;                                                // 입구 포탈이 붙은 벽 콜라이더(충돌 무시용)
    private Collider inSurfaceCol;                                                // 입구 포탈 표면 콜라이더(충돌 무시용)
    private Collider outSurfaceCol;                                               // 출구 포탈 표면 콜라이더(충돌 무시용)

    private int insideCount = 0;                                                  // 트리거 중첩 카운트(겹침 안정화)
    private int entrySideSign = +1;                                               // 입구 평면의 진입 측 부호(+/-)
    private float cooldownUntil = 0f;                                             // 쿨다운 종료 시간(Time.time 기준)

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f); // 포탈 통과 시 기본 180도 회전

    private PlayerController playerController;                                    // 플레이어면 시야/속도 처리에 사용
    private PortalCloneVisual cloneVisual;                                        // 비주얼 클론이 있으면 워프 후 포탈 쌍 갱신

    public event Action<Portal, Portal> Warped;                                   // 워프가 발생했을 때 알림(입구, 출구)

    /// <summary>
    /// Unity Awake: Rigidbody/Collider/옵션 컴포넌트(PlayerController/CloneVisual)를 캐싱한다.
    /// </summary>
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();                                           // Rigidbody 캐싱
        col = GetComponent<Collider>();                                           // Collider 캐싱
        capsule = GetComponent<CapsuleCollider>();                                // CapsuleCollider 캐싱(있으면)

        playerController = GetComponent<PlayerController>();                      // PlayerController 캐싱(있으면)
        cloneVisual = GetComponent<PortalCloneVisual>();                          // PortalCloneVisual 캐싱(있으면)
    }

    /// <summary>
    /// Unity FixedUpdate: 트리거 내부에서 평면을 넘어갔는지 확인하고, 넘어가면 워프를 수행한다.
    /// </summary>
    private void FixedUpdate()
    {
        if (insideCount <= 0) return;                                             // 트리거 내부가 아니면 중단
        if (inPortal == null || outPortal == null) return;                        // 포탈 쌍이 없으면 중단
        if (Time.time < cooldownUntil) return;                                    // 쿨다운 중이면 중단

        if (HasCenterCrossedPlane(inPortal.Plane, entrySideSign))                 // 평면을 진입 반대쪽으로 넘었는지 체크
            WarpNow();                                                            // 워프 실행
    }

    /// <summary>
    /// 포탈 트리거 진입 시 호출: 포탈 상태를 세팅하고 충돌 무시(벽/표면)를 적용한다.
    /// </summary>
    /// <param name="inP">입구 포탈</param>
    /// <param name="outP">출구 포탈</param>
    /// <param name="inWall">입구 포탈이 붙은 벽 콜라이더</param>
    public void EnterPortal(Portal inP, Portal outP, Collider inWall)
    {
        if (Time.time < cooldownUntil) return;                                    // 쿨다운 중이면 무시
        if (!inP || !outP) return;                                                // 포탈 참조가 없으면 무시

        if (insideCount > 0 && inPortal != null && inPortal != inP)               // 이미 트리거 내부인데 다른 포탈로 들어오면
        {
            ForceClearState();                                                    // 상태 꼬임 방지로 강제 초기화
        }

        if (insideCount == 0)                                                     // 최초 진입이면
        {
            inPortal = inP;                                                       // 입구 포탈 저장
            outPortal = outP;                                                     // 출구 포탈 저장

            SetIgnoredWall(inWall);                                               // 벽 충돌 무시 설정
            SetIgnoredPortalSurfaces(inPortal, outPortal);                        // 포탈 표면 충돌 무시 설정

            float d = SignedDistanceToPlane(inPortal.Plane, GetCenterWorld());    // 현재 센터의 평면 부호 거리
            entrySideSign = SignWithEps(d, +1);                                   // 진입 측 부호 저장
        }
        else                                                                      // 중첩 진입(같은 포탈 트리거 내부에서 추가 enter)면
        {
            if (inPortal == inP && inWall != null && inWall != wallCollider)      // 벽 콜라이더가 바뀌었으면
                SetIgnoredWall(inWall);                                           // 최신 벽으로 갱신

            outPortal = outP;                                                     // 출구 포탈 갱신
            SetIgnoredPortalSurfaces(inPortal, outPortal);                        // 표면 무시 갱신
        }

        insideCount++;                                                            // 중첩 카운트 증가
    }

    /// <summary>
    /// 포탈 트리거에서 나갈 때 호출: 중첩 카운트를 감소시키고 0이면 상태를 정리한다.
    /// </summary>
    /// <param name="exitedPortal">나간 포탈(입구 포탈과 다르면 무시)</param>
    public void NotifyTriggerExit(Portal exitedPortal)
    {
        if (exitedPortal != null && inPortal != null && exitedPortal != inPortal) // 다른 포탈 exit이면
            return;                                                               // 무시(중첩 안전)

        ExitPortalInternal();                                                     // 내부 exit 처리
    }

    /// <summary>
    /// 트리거 exit 처리: insideCount를 줄이고, 0이 되면 강제 상태 정리.
    /// </summary>
    private void ExitPortalInternal()
    {
        if (insideCount <= 0) return;                                             // 이미 0이면 중단

        insideCount = Mathf.Max(insideCount - 1, 0);                               // 중첩 카운트 감소
        if (insideCount > 0) return;                                              // 아직 남아있으면 종료하지 않음

        ForceClearState();                                                        // 완전히 나갔으면 상태 정리
    }

    /// <summary>
    /// 워프를 실행하고, 충돌 무시/쿨다운/포탈 쌍 교체 등 후처리를 수행한다.
    /// </summary>
    private void WarpNow()
    {
        Portal oldInPortal = inPortal;                                            // 워프 전 입구 포탈 저장
        Portal oldOutPortal = outPortal;                                          // 워프 전 출구 포탈 저장
        Collider oldInWall = wallCollider;                                        // 워프 전 벽 콜라이더 저장

        Warp();                                                                   // 실제 워프(위치/회전/속도 변환)

        if (oldInPortal != null)                                                  // 입구 포탈이 있으면
        {
            string n = oldInPortal.name.ToLowerInvariant();                       // 이름 기반 타입 판별(네이밍 의존)
            if (n.Contains("blue"))                                               // 이름에 blue면
                SoundManager.PlaySFX(SfxId.Portal_BlueEnter);                      // 블루 진입 사운드
            else                                                                  // 아니면
                SoundManager.PlaySFX(SfxId.Portal_OrangeEnter);                    // 오렌지 진입 사운드
        }

        Warped?.Invoke(oldInPortal, oldOutPortal);                                // 워프 이벤트 발행(입구/출구)

        Collider newWall = null;                                                  // 워프 후 새 벽 콜라이더
        if (oldOutPortal != null)                                                 // 출구 포탈이 있으면
            newWall = oldOutPortal.WallColliderCached;                            // 출구 포탈이 붙은 벽을 가져옴

        if (oldInWall) Physics.IgnoreCollision(col, oldInWall, false);            // 이전 벽 충돌 무시 해제

        wallCollider = null;                                                      // 벽 콜라이더 초기화
        SetIgnoredWall(newWall);                                                  // 새 벽 충돌 무시 설정

        cooldownUntil = Time.time + teleportCooldown;                             // 쿨다운 설정(재워프 방지)

        inPortal = oldOutPortal;                                                  // 입구/출구 포탈 스왑(연속 통과 처리)
        outPortal = oldInPortal;                                                  // 입구/출구 포탈 스왑

        SetIgnoredPortalSurfaces(inPortal, outPortal);                            // 표면 충돌 무시 갱신

        insideCount = Mathf.Max(insideCount, 1);                                  // 내부 카운트 최소 1 유지(연속 판정 안정화)

        if (inPortal != null)                                                     // 새 입구 포탈이 있으면
        {
            float d = SignedDistanceToPlane(inPortal.Plane, GetCenterWorld());    // 센터의 평면 거리
            entrySideSign = SignWithEps(d, +1);                                   // 진입 부호 갱신
        }
        else                                                                      // 입구 포탈이 사라졌으면
        {
            ForceClearState();                                                    // 상태 정리
            return;                                                               // 종료
        }

        if (cloneVisual != null)                                                  // 클론 비주얼이 있으면
            cloneVisual.OnWarped(inPortal, outPortal);                            // 워프 후 포탈 쌍 갱신
    }

    /// <summary>
    /// 충돌 무시/포탈 참조/중첩 카운트 등 모든 상태를 초기화한다.
    /// </summary>
    private void ForceClearState()
    {
        if (wallCollider)                                                         // 벽 콜라이더가 있으면
            Physics.IgnoreCollision(col, wallCollider, false);                    // 충돌 무시 해제

        if (inSurfaceCol) Physics.IgnoreCollision(col, inSurfaceCol, false);      // 입구 표면 충돌 무시 해제
        if (outSurfaceCol) Physics.IgnoreCollision(col, outSurfaceCol, false);    // 출구 표면 충돌 무시 해제
        inSurfaceCol = null;                                                      // 입구 표면 콜라이더 참조 해제
        outSurfaceCol = null;                                                     // 출구 표면 콜라이더 참조 해제

        inPortal = null;                                                          // 입구 포탈 참조 해제
        outPortal = null;                                                         // 출구 포탈 참조 해제
        wallCollider = null;                                                      // 벽 콜라이더 참조 해제
        entrySideSign = +1;                                                       // 진입 부호 초기화
        insideCount = 0;                                                          // 중첩 카운트 초기화
    }

    /// <summary>
    /// 플레이어(오브젝트) 콜라이더와 벽 콜라이더 간 IgnoreCollision을 설정/갱신한다.
    /// </summary>
    /// <param name="newWall">새 벽 콜라이더</param>
    private void SetIgnoredWall(Collider newWall)
    {
        if (wallCollider == newWall) return;                                      // 동일하면 갱신 불필요

        if (wallCollider)                                                         // 기존 벽이 있으면
            Physics.IgnoreCollision(col, wallCollider, false);                    // 기존 벽 충돌 무시 해제

        wallCollider = newWall;                                                   // 벽 콜라이더 갱신

        if (wallCollider)                                                         // 새 벽이 있으면
            Physics.IgnoreCollision(col, wallCollider, true);                     // 새 벽 충돌 무시 설정
    }

    /// <summary>
    /// 입구/출구 포탈 표면 콜라이더와의 충돌을 무시하도록 설정한다. (포탈 표면에 걸리지 않게)
    /// </summary>
    /// <param name="inP">입구 포탈</param>
    /// <param name="outP">출구 포탈</param>
    private void SetIgnoredPortalSurfaces(Portal inP, Portal outP)
    {
        Collider newIn = (inP != null) ? inP.SurfaceCollider : null;              // 새 입구 표면 콜라이더
        Collider newOut = (outP != null) ? outP.SurfaceCollider : null;           // 새 출구 표면 콜라이더

        if (inSurfaceCol != newIn)                                                // 입구 표면이 변경됐으면
        {
            if (inSurfaceCol) Physics.IgnoreCollision(col, inSurfaceCol, false);  // 기존 입구 표면 무시 해제
            inSurfaceCol = newIn;                                                 // 참조 갱신
            if (inSurfaceCol) Physics.IgnoreCollision(col, inSurfaceCol, true);   // 새 입구 표면 무시 적용
        }

        if (outSurfaceCol != newOut)                                              // 출구 표면이 변경됐으면
        {
            if (outSurfaceCol) Physics.IgnoreCollision(col, outSurfaceCol, false);// 기존 출구 표면 무시 해제
            outSurfaceCol = newOut;                                               // 참조 갱신
            if (outSurfaceCol) Physics.IgnoreCollision(col, outSurfaceCol, true); // 새 출구 표면 무시 적용
        }
    }

    /// <summary>
    /// 오브젝트의 "센터" 월드 좌표를 계산한다. (캡슐센터 > bounds.center > worldCenterOfMass > position 순)
    /// </summary>
    /// <returns>센터 월드 좌표</returns>
    private Vector3 GetCenterWorld()
    {
        if (capsule) return transform.TransformPoint(capsule.center);             // 캡슐 콜라이더 중심 사용
        if (col) return col.bounds.center;                                        // 콜라이더 bounds 중심 사용
        if (rb) return rb.worldCenterOfMass;                                      // 리지드바디 COM 사용
        return transform.position;                                                // 최후: transform.position
    }

    /// <summary>
    /// 평면(plane.forward, plane.position) 기준으로 점까지의 signed distance를 구한다.
    /// </summary>
    /// <param name="plane">평면 Transform</param>
    /// <param name="worldPoint">월드 점</param>
    /// <returns>signed distance</returns>
    private static float SignedDistanceToPlane(Transform plane, Vector3 worldPoint)
    {
        return Vector3.Dot(plane.forward, worldPoint - plane.position);           // forward 방향 투영값
    }

    /// <summary>
    /// 거리 값이 0에 아주 가까우면 defaultSign을 반환하고, 아니면 부호(+/-)를 반환한다.
    /// </summary>
    /// <param name="d">거리</param>
    /// <param name="defaultSign">0 근처일 때 사용할 기본 부호</param>
    /// <returns>+1 또는 -1</returns>
    private static int SignWithEps(float d, int defaultSign)
    {
        if (Mathf.Abs(d) < 1e-6f) return defaultSign;                             // 0 근처면 기본값
        return (d > 0f) ? +1 : -1;                                                // 양수면 +1, 음수면 -1
    }

    /// <summary>
    /// 오브젝트 센터가 평면을 entrySign 기준으로 반대쪽으로 충분히 넘었는지 검사한다.
    /// </summary>
    /// <param name="plane">입구 포탈 평면</param>
    /// <param name="entrySign">진입 당시 부호</param>
    /// <returns>넘었으면 true</returns>
    private bool HasCenterCrossedPlane(Transform plane, int entrySign)
    {
        if (!plane) return false;                                                 // 평면이 없으면 실패

        float d = SignedDistanceToPlane(plane, GetCenterWorld());                 // 현재 센터 거리
        return (d * entrySign) <= -planeCrossEpsilon;                             // 진입 부호 반대 방향으로 epsilon 이상 넘었는지
    }

    /// <summary>
    /// 실제 워프 처리: 위치/회전/속도를 포탈 변환으로 바꾸고 PlayerController가 있으면 시야/속도 규칙으로 적용한다.
    /// </summary>
    private void Warp()
    {
        Transform inT = inPortal.Plane;                                           // 입구 포탈 평면
        Transform outT = outPortal.Plane;                                         // 출구 포탈 평면

        Quaternion viewBefore = Quaternion.identity;                              // 워프 전 시야 회전(플레이어용)
        Vector3 stableYawBefore = Vector3.forward;                                // 워프 전 안정 yaw forward(플레이어용)

        if (playerController != null)                                             // 플레이어면
        {
            viewBefore = playerController.GetViewWorldRotation();                 // 시야 회전 저장
            stableYawBefore = playerController.GetStableYawForward();             // 안정 yaw forward 저장
        }

        Vector3 center = GetCenterWorld();                                        // 현재 오브젝트 센터
        Vector3 pivotToCenter = center - transform.position;                      // transform.position -> center 오프셋

        Vector3 relativePos = inT.InverseTransformPoint(center);                  // 센터를 입구 기준 로컬로
        relativePos = HalfTurn * relativePos;                                     // 180도 뒤집기
        Vector3 newCenter = outT.TransformPoint(relativePos);                     // 출구 기준 월드 센터로

        if (exitOffset != 0f)                                                     // 필요하면
            newCenter += outT.forward * exitOffset;                               // 출구 방향으로 추가 밀기

        Vector3 newPos = newCenter - pivotToCenter;                               // center 기준으로 transform.position 재계산

        Vector3 relativeVel = inT.InverseTransformDirection(rb.linearVelocity);   // 속도를 입구 기준 로컬로
        relativeVel = HalfTurn * relativeVel;                                     // 180도 뒤집기
        Vector3 newVel = outT.TransformDirection(relativeVel);                    // 출구 기준 월드 속도로

        rb.position = newPos;                                                     // 리지드바디 위치 적용
        transform.position = newPos;                                              // 트랜스폼 위치도 동기화

        if (playerController != null)                                             // 플레이어면
        {
            Quaternion viewAfter =
                outT.rotation *                                                   // 출구 회전
                HalfTurn *                                                        // 180도 뒤집기
                Quaternion.Inverse(inT.rotation) *                                 // 입구 회전 역
                viewBefore;                                                       // 워프 전 시야

            Vector3 stableYawAfter = TransformDirectionThroughPortal(stableYawBefore, inT, outT); // 안정 yaw forward 변환

            playerController.ApplyViewAfterUpright(viewAfter, stableYawAfter);    // upright 규칙으로 시야 적용
            playerController.SetVelocity(newVel);                                 // 플레이어 속도 적용(컨트롤러 규칙)
            rb.angularVelocity = Vector3.zero;                                    // 플레이어는 각속도 0으로 안정화
        }
        else                                                                      // 일반 오브젝트면
        {
            Quaternion relativeRot = Quaternion.Inverse(inT.rotation) * transform.rotation; // 회전을 입구 로컬로
            relativeRot = HalfTurn * relativeRot;                                 // 180도 뒤집기
            Quaternion newWorldRot = outT.rotation * relativeRot;                 // 출구 월드 회전
            transform.rotation = newWorldRot;                                     // 회전 적용

            Vector3 relAng = inT.InverseTransformDirection(rb.angularVelocity);   // 각속도 입구 로컬로
            relAng = HalfTurn * relAng;                                           // 180도 뒤집기
            rb.angularVelocity = outT.TransformDirection(relAng);                 // 출구 월드 각속도

            rb.linearVelocity = newVel;                                           // 선속도 적용
        }
    }

    /// <summary>
    /// 플레이어의 yaw 안정 벡터 같은 "방향"을 포탈을 통해 변환한다(정규화 포함).
    /// </summary>
    /// <param name="worldDir">월드 방향</param>
    /// <param name="inPlane">입구 평면</param>
    /// <param name="outPlane">출구 평면</param>
    /// <returns>출구 기준 월드 방향(정규화)</returns>
    private static Vector3 TransformDirectionThroughPortal(Vector3 worldDir, Transform inPlane, Transform outPlane)
    {
        Vector3 local = inPlane.InverseTransformDirection(worldDir);              // 로컬 방향
        local = HalfTurn * local;                                                 // 180도 뒤집기
        return outPlane.TransformDirection(local).normalized;                     // 월드 방향으로 변환 후 정규화
    }

    /// <summary>
    /// 큐브 충돌 시 일정 속도 이상이면 충돌 SFX를 재생한다.
    /// </summary>
    /// <param name="collision">충돌 정보</param>
    private void OnCollisionEnter(Collision collision)
    {
        if (rb == null) return;                                                   // 리지드바디 없으면 중단
        if (collision == null || collision.contactCount == 0) return;             // 유효한 충돌이 아니면 중단

        if (!(CompareTag("Cube")))                                                // Cube 태그만 처리
            return;                                                               // 그 외는 무시

        float speed = collision.relativeVelocity.magnitude;                       // 상대 속도 크기
        if (speed < impactMinSpeed) return;                                       // 임계값 미만이면 무시

        Vector3 p = collision.GetContact(0).point;                                // 첫 접촉점
        SoundManager.PlaySFX(SfxId.ObjImpact_Cube, worldPos: p);                  // 충돌 SFX 재생(3D)
    }
}
