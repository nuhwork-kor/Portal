using System.Collections.Generic;
using UnityEngine;

// 이 스크립트는 "플레이어가 오브젝트를 집고(홀드) / 드랍"하는 전체 흐름을 담당한다.
// - PortalRaycaster로 집을 대상(Rigidbody)을 찾는다.
// - HeldObjectMotorSpring으로 고정된 HoldPoint 위치/회전에 물체를 물리적으로 따라오게 만든다.
// - 포탈을 사이에 두고 물체/플레이어가 분리된 상황(through-portal holding)을 추적하고, 변환된 목표 위치/회전을 계산한다.
// - 잡고 있는 동안 플레이어와의 충돌 무시, 카메라 회전 속도(ω) 기반 feed-forward 속도 보정 등을 처리한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(PortalGunController))]
[RequireComponent(typeof(PortalRaycaster))]
[RequireComponent(typeof(HeldObjectMotorSpring))]
public class HeldObjectController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PortalGunController ctx;                          // 플레이어 카메라/홀드포인트/리짓바디/포탈매니저 참조 제공 컨텍스트
    [SerializeField] private PortalRaycaster raycaster;                        // 상호작용(집기) 레이캐스트 담당
    [SerializeField] private HeldObjectMotorSpring motor;                      // 홀드 대상 Rigidbody를 목표로 따라가게 하는 모터(스프링/서보)

    [Header("Hold Rotation (Portal1-like)")]
    [SerializeField, Range(1f, 45f)] private float snapAngleDeg = 15f;         // 집을 때 표면 노멀을 기준으로 축 스냅(각도 임계값)
    [SerializeField] private bool snapFaceTowardPlayer = true;                 // 스냅이 성립하면 "플레이어를 바라보는" 방향으로 정렬할지 여부
    [SerializeField] private bool useYawOnlyFrame = true;                      // 홀드 기준 프레임을 카메라 Yaw만 사용해서 만들지 여부(상하 Pitch 무시)

    [Header("Collision")]
    [SerializeField] private bool ignoreCollisionWithPlayerWhileHolding = true;// 잡고 있는 동안 플레이어 콜라이더와 충돌 무시 여부

    [Header("Camera Rotation Feed-Forward")]
    [SerializeField] private bool useCameraRotationVelocity = true;            // 카메라 회전 속도 기반(ω×r) 속도 feed-forward 적용 여부
    [SerializeField] private float maxCameraOmega = 60f;                       // 카메라 ω(rad/s) 클램프 최대치(급격한 회전 튐 방지)

    public bool IsHolding => heldRb != null;                                   // 현재 Rigidbody를 잡고 있는지 여부(heldRb 존재로 판정)

    private Rigidbody heldRb;                                                  // 현재 잡고 있는 대상 Rigidbody
    private PortalTraveller heldTraveller;                                      // 잡힌 오브젝트에 붙은 PortalTraveller(워프 이벤트 받기용)
    private PortalTraveller playerTraveller;                                    // 플레이어에 붙은 PortalTraveller(워프 이벤트 받기용)

    private readonly List<Collider> heldCols = new();                           // 잡힌 오브젝트의 콜라이더 목록(충돌 무시용)
    private readonly List<Collider> playerCols = new();                         // 플레이어 콜라이더 목록(충돌 무시용)

    // Through-portal holding state
    private bool holdingThroughPortal;                                          // 현재 "포탈을 사이에 두고" 잡고 있는 상태인지 여부
    private Portal holdingInPortal;                                             // 홀드 목표를 변환할 때 기준이 되는 inPortal(플레이어 쪽)
    private Portal holdingOutPortal;                                            // 홀드 목표를 변환할 때 기준이 되는 outPortal(오브젝트 쪽)
    private Portal playerSidePortal;                                            // 플레이어가 마지막으로 워프/감지된 포탈(플레이어 측 포탈 추론)
    private Portal objectSidePortal;                                            // 오브젝트가 마지막으로 워프/감지된 포탈(오브젝트 측 포탈 추론)

    private Quaternion holdRotOffset = Quaternion.identity;                     // 홀드 프레임(카메라 기반) 대비 오브젝트 회전 오프셋

    // Restore rb params
    private float prevMaxAngularVel;                                            // 잡기 전 maxAngularVelocity 백업값
    private int prevSolverIter;                                                 // 잡기 전 solverIterations 백업값
    private int prevSolverVelIter;                                              // 잡기 전 solverVelocityIterations 백업값

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);// 포탈 통과 시 방향 뒤집기(180도)용 회전

    // ===== Camera omega cached per frame (Update) =====
    private Vector3 cachedCamOmega;                                             // Update에서 계산/캐시한 카메라 ω(world, rad/s)
    private Quaternion prevCamRotFrame;                                         // 이전 프레임 카메라 회전(ω 계산용)
    private bool hasPrevCamRotFrame;                                            // prevCamRotFrame 초기화 여부

    /// <summary>
    /// 유니티 생명주기: 컴포넌트 참조를 확보하고(없으면 GetComponent),
    /// 플레이어 콜라이더/PortalTraveller 캐시를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (!ctx) ctx = GetComponent<PortalGunController>();                    // 컨텍스트 참조 자동 확보
        if (!raycaster) raycaster = GetComponent<PortalRaycaster>();            // 레이캐스터 참조 자동 확보
        if (!motor) motor = GetComponent<HeldObjectMotorSpring>();              // 모터 참조 자동 확보

        CachePlayerColliders();                                                 // 플레이어 콜라이더 목록 캐시(충돌 무시용)
        CachePlayerTraveller();                                                 // 플레이어 PortalTraveller 캐시(워프 이벤트용)
    }

    /// <summary>
    /// 유니티 생명주기: 활성화 시 입력 이벤트를 구독한다.
    /// </summary>
    private void OnEnable() => InputManager.OnInteract += ToggleHold;           // 상호작용 키 입력에 ToggleHold 연결

    /// <summary>
    /// 유니티 생명주기: 비활성화 시 입력 이벤트 구독 해제 + 잡고 있던 것 정리 + 워프 이벤트 언바인드.
    /// </summary>
    private void OnDisable()
    {
        InputManager.OnInteract -= ToggleHold;                                  // 상호작용 키 입력 구독 해제

        if (IsHolding) Drop();                                                  // 비활성화될 때 잡고 있으면 강제 드랍

        UnbindHeldTraveller();                                                  // 잡힌 오브젝트 워프 이벤트 해제
        UnbindPlayerTraveller();                                                // 플레이어 워프 이벤트 해제
    }

    /// <summary>
    /// 유니티 생명주기(Update): 카메라 회전 변화량으로 ω(rad/s)를 계산해 캐시한다.
    /// - FixedUpdate에서 ω 계산하면 튐이 생길 수 있어서 Update에서만 계산한다.
    /// </summary>
    private void Update()
    {
        // omega는 프레임(Update)에서만 계산해서 Fixed에서 튀는 현상 제거
        if (!IsHolding || !useCameraRotationVelocity || ctx == null || ctx.PlayerCamera == null) // 홀드 중이 아니거나 기능이 꺼져있거나 참조가 없으면
            return;                                                                // ω 계산을 수행하지 않음

        float dt = Time.deltaTime;                                                 // 프레임 델타 시간
        if (dt <= 0f)                                                              // dt가 비정상(0 이하)이면
        {
            cachedCamOmega = Vector3.zero;                                         // ω를 0으로 처리
            return;                                                                // 종료
        }

        Quaternion now = ctx.PlayerCamera.transform.rotation;                      // 현재 카메라 회전

        if (!hasPrevCamRotFrame)                                                   // 이전 프레임 회전이 아직 없으면
        {
            prevCamRotFrame = now;                                                 // 현재 회전을 이전값으로 저장
            hasPrevCamRotFrame = true;                                             // 초기화 완료 표시
            cachedCamOmega = Vector3.zero;                                         // 첫 프레임은 ω=0
            return;                                                                // 종료
        }

        Quaternion dq = now * Quaternion.Inverse(prevCamRotFrame);                 // 이전->현재 회전 변화량(쿼터니언)
        prevCamRotFrame = now;                                                     // 다음 프레임을 위해 현재를 저장

        dq.ToAngleAxis(out float angleDeg, out Vector3 axis);                      // dq를 axis-angle로 변환(각도/축)
        if (axis.sqrMagnitude < 1e-8f)                                             // 축이 거의 0이면(회전 변화 없음)
        {
            cachedCamOmega = Vector3.zero;                                         // ω=0
            return;                                                                // 종료
        }

        if (angleDeg > 180f) angleDeg -= 360f;                                     // 0~360을 -180~180으로 정규화(방향성 유지)

        float angleRad = angleDeg * Mathf.Deg2Rad;                                 // 각도를 라디안으로 변환
        axis.Normalize();                                                          // 축 정규화

        Vector3 omega = axis * (angleRad / Mathf.Max(1e-6f, dt));                  // ω = 축 * (각속도), rad/s

        float mag = omega.magnitude;                                               // ω 크기
        if (mag > maxCameraOmega)                                                  // 최대값을 넘으면
            omega *= (maxCameraOmega / mag);                                       // 클램프(스케일 다운)

        cachedCamOmega = omega;                                                    // 계산된 ω를 캐시에 저장
    }

    /// <summary>
    /// 입력(Interact) 시 호출되는 토글 함수.
    /// - 이미 잡고 있으면 Drop
    /// - 아니면 TryPickup
    /// </summary>
    public void ToggleHold()
    {
        if (IsHolding) Drop();                                                     // 잡고 있으면 드랍
        else TryPickup();                                                          // 아니면 집기 시도
    }

    /// <summary>
    /// 집기를 시도한다.
    /// - 플레이어 카메라/홀드포인트/레이캐스트 성공 여부를 검사한다.
    /// </summary>
    private void TryPickup()
    {
        if (!ctx || !ctx.PlayerCamera || !ctx.HoldPoint) return;                   // 필수 참조가 없으면 집기 불가
        if (!raycaster.TryGetInteractHit(out var hit)) return;                     // 레이캐스트로 상호작용 대상 못 찾으면 종료
        Pickup(hit);                                                               // 상호작용 히트 결과로 집기 처리
    }

    /// <summary>
    /// 실제로 Rigidbody를 잡는 처리.
    /// - RB 파라미터 튜닝
    /// - 플레이어 충돌 무시
    /// - 회전 오프셋 설정
    /// - 워프 이벤트 바인딩
    /// - through-portal 상태 초기화/갱신
    /// - 홀드포인트로 스냅
    /// </summary>
    /// <param name="hit">PortalRaycaster가 반환한 집기 대상 정보</param>
    private void Pickup(PortalRaycaster.InteractHit hit)
    {
        if (!hit.hit || hit.rb == null) return;                                    // 히트가 아니거나 RB가 없으면 종료

        heldRb = hit.rb;                                                            // 잡힌 Rigidbody 저장

        // ---- 이하 기존 코드 그대로 ----
        prevMaxAngularVel = heldRb.maxAngularVelocity;                              // 기존 maxAngularVelocity 백업
        prevSolverIter = heldRb.solverIterations;                                   // 기존 solverIterations 백업
        prevSolverVelIter = heldRb.solverVelocityIterations;                        // 기존 solverVelocityIterations 백업

        heldRb.maxAngularVelocity = 50f;                                            // 홀드 중 각속도 제한 상향(회전 추종 안정)
        heldRb.solverIterations = 12;                                               // 홀드 중 solver 반복 증가(물리 안정)
        heldRb.solverVelocityIterations = 12;                                       // 홀드 중 velocity solver 반복 증가(물리 안정)
        heldRb.interpolation = RigidbodyInterpolation.Interpolate;                  // 홀드 중 보간(시각적 부드러움)

        heldCols.Clear();                                                           // 잡힌 콜라이더 목록 초기화
        heldRb.GetComponentsInChildren(true, heldCols);                              // 잡힌 오브젝트의 모든 콜라이더 수집

        if (ignoreCollisionWithPlayerWhileHolding)                                   // 옵션이 켜져있으면
            SetIgnorePlayerCollision(true);                                          // 플레이어와 충돌 무시 설정

        SetupGrabRotation(hit.rbHit);                                                // 집은 표면 노멀 기반 회전 오프셋 설정

        BindHeldTraveller();                                                         // 잡힌 오브젝트 워프 이벤트 바인딩
        BindPlayerTraveller();                                                       // 플레이어 워프 이벤트 바인딩

        playerSidePortal = null;                                                     // 플레이어 측 포탈 초기화
        objectSidePortal = null;                                                     // 오브젝트 측 포탈 초기화

        if (hit.throughPortal && hit.inPortal && hit.outPortal)                      // 포탈을 통해 집은 경우라면
        {
            playerSidePortal = hit.inPortal;                                         // 플레이어 쪽 포탈 설정
            objectSidePortal = hit.outPortal;                                        // 오브젝트 쪽 포탈 설정
        }

        RefreshThroughPortalState(force: true);                                      // through-portal 상태 갱신(강제)

        PrimeCameraOmegaHistory();                                                   // 카메라 ω 계산용 이전 회전 초기화
        SnapHeldToHoldPoint();                                                       // 홀드포인트로 즉시 스냅(튀는 현상 방지)
    }

    /// <summary>
    /// 잡고 있는 물체를 드랍한다.
    /// - 충돌 무시 해제
    /// - 워프 이벤트 해제
    /// - RB 파라미터 복구
    /// - 내부 상태 초기화
    /// </summary>
    public void Drop()
    {
        if (!heldRb) return;                                                         // 잡고 있는 RB가 없으면 종료

        if (ignoreCollisionWithPlayerWhileHolding)                                   // 옵션이 켜져있다면
            SetIgnorePlayerCollision(false);                                         // 충돌 무시 해제

        UnbindHeldTraveller();                                                       // 잡힌 오브젝트 워프 이벤트 해제
        UnbindPlayerTraveller();                                                     // 플레이어 워프 이벤트 해제

        heldRb.maxAngularVelocity = prevMaxAngularVel;                               // maxAngularVelocity 복구
        heldRb.solverIterations = prevSolverIter;                                    // solverIterations 복구
        heldRb.solverVelocityIterations = prevSolverVelIter;                         // solverVelocityIterations 복구

        heldRb = null;                                                               // 잡힌 RB 참조 제거
        heldCols.Clear();                                                            // 잡힌 콜라이더 목록 초기화

        holdingThroughPortal = false;                                                // through-portal 상태 해제
        holdingInPortal = null;                                                      // inPortal 초기화
        holdingOutPortal = null;                                                     // outPortal 초기화
        playerSidePortal = null;                                                     // 플레이어 측 포탈 초기화
        objectSidePortal = null;                                                     // 오브젝트 측 포탈 초기화

        holdRotOffset = Quaternion.identity;                                         // 회전 오프셋 초기화

        hasPrevCamRotFrame = false;                                                  // ω 계산 히스토리 초기화
        cachedCamOmega = Vector3.zero;                                               // ω 캐시 초기화
    }

    /// <summary>
    /// 유니티 생명주기(FixedUpdate): 물리 프레임에서 목표 위치/회전/속도를 계산하고 모터로 적용한다.
    /// - through-portal 상태라면 포탈 변환된 목표를 계산한다.
    /// - 포탈이 사라졌거나 유효하지 않으면 드랍한다(안전장치).
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsHolding) return;                                                      // 잡고 있지 않으면 물리 적용 안 함
        if (!ctx || !ctx.HoldPoint) { Drop(); return; }                              // 필수 참조가 사라지면 드랍 후 종료

        RefreshThroughPortalState();                                                 // through-portal 상태 갱신(워프/머지 등)

        if (holdingThroughPortal)                                                    // 포탈 사이로 잡는 상태라면
        {
            if (!holdingInPortal || !holdingOutPortal || !holdingInPortal.IsPlaced || !holdingOutPortal.IsPlaced) // 포탈이 없거나 설치 해제되면
            {
                Drop();                                                              // 안전하게 드랍
                return;                                                              // 종료
            }
        }

        ComputeTargetFixed(out var targetPos, out var targetRot, out var targetVel, out var targetAngVel); // 목표값 계산
        motor.Apply(heldRb, targetPos, targetRot, targetVel, targetAngVel);          // 모터로 목표를 RB에 적용
    }

    // =============================
    // ThroughPortal state
    // =============================

    /// <summary>
    /// 포탈을 사이에 두고 잡고 있는 상태(through-portal holding)를 갱신한다.
    /// - playerSidePortal / objectSidePortal 조합으로 through 여부를 결정한다.
    /// - split -> merged 순간(양쪽 포탈이 같은 포탈로 합쳐지는 상황)에는 기준 재정렬과 스냅으로 튐을 방지한다.
    /// </summary>
    /// <param name="force">강제로 머지 처리(스냅/재정렬)를 유발할지 여부</param>
    private void RefreshThroughPortalState(bool force = false)
    {
        bool wasThrough = holdingThroughPortal;                                      // 이전 프레임 through 상태 저장

        if (playerSidePortal != null && objectSidePortal != null && playerSidePortal != objectSidePortal) // 양쪽 포탈이 존재하고 서로 다르면
        {
            holdingThroughPortal = true;                                             // through 상태로 전환
            holdingInPortal = playerSidePortal;                                      // inPortal은 플레이어 측
            holdingOutPortal = objectSidePortal;                                     // outPortal은 오브젝트 측
        }
        else                                                                          // 한쪽이 없거나 둘이 같으면
        {
            holdingThroughPortal = false;                                            // through 상태 해제
            holdingInPortal = null;                                                  // inPortal 해제
            holdingOutPortal = null;                                                 // outPortal 해제
        }

        // split -> merged 순간: 기준 재정렬 + 스파이크 방지 + 스냅
        if ((force || wasThrough) && !holdingThroughPortal)                           // 이전에 through였거나 강제 처리인데 지금 through가 아니면
        {
            if (playerSidePortal != null && objectSidePortal != null && playerSidePortal == objectSidePortal) // 양쪽이 같은 포탈로 합쳐졌다면
            {
                RebaseHoldRotationToCurrent();                                       // 현재 오브젝트 회전 기준으로 holdRotOffset 재계산
                PrimeCameraOmegaHistory();                                           // ω 히스토리 리셋(급격한 회전 튐 방지)
                SnapHeldToHoldPoint();                                               // 홀드포인트로 다시 스냅(위치 튐 방지)
            }
        }
    }

    /// <summary>
    /// 현재 오브젝트의 실제 회전을 기준으로 holdRotOffset을 다시 계산한다.
    /// - 머지(same portal) 시 기존 offset으로 튀는 현상을 줄이기 위해 사용한다.
    /// </summary>
    private void RebaseHoldRotationToCurrent()
    {
        if (!heldRb) return;                                                          // 잡힌 RB가 없으면 종료
        Quaternion frameRot = GetHoldFrameRotation();                                 // 홀드 기준 프레임 회전 계산
        holdRotOffset = Quaternion.Inverse(frameRot) * heldRb.rotation;               // frameRot 대비 현재 오브젝트 회전 오프셋 재계산
    }

    /// <summary>
    /// 현재 홀드포인트 위치/회전에 오브젝트를 즉시 스냅한다.
    /// - through-portal 상태면 포탈 변환된 위치/회전/속도로 스냅한다.
    /// </summary>
    private void SnapHeldToHoldPoint()
    {
        if (!heldRb || !ctx || !ctx.HoldPoint) return;                                // 필수 참조가 없으면 종료

        Vector3 targetPos = ctx.HoldPoint.position;                                   // 기본 목표 위치(플레이어 앞 홀드포인트)
        Quaternion targetRot = GetHoldFrameRotation() * holdRotOffset;                // 기본 목표 회전(프레임 회전 + 오프셋)

        Vector3 targetVel = (ctx.PlayerRigidbody != null) ? ctx.PlayerRigidbody.linearVelocity : Vector3.zero; // 기본 목표 속도(플레이어 속도)

        PrimeCameraOmegaHistory();                                                    // 스냅 직후 ω 계산 안정화를 위해 히스토리 초기화

        if (holdingThroughPortal && holdingInPortal && holdingOutPortal)              // 포탈 사이로 잡는 상태라면
        {
            targetPos = PortalMath.TransformPoint(targetPos, holdingInPortal.Plane, holdingOutPortal.Plane);   // 포탈 변환된 목표 위치
            targetRot = PortalMath.TransformRotation(targetRot, holdingInPortal.Plane, holdingOutPortal.Plane);// 포탈 변환된 목표 회전
            targetVel = TransformDirection(targetVel, holdingInPortal.Plane, holdingOutPortal.Plane);          // 포탈 변환된 목표 속도
        }

        motor.Snap(heldRb, targetPos, targetRot, targetVel);                           // 모터 스냅으로 RB를 즉시 목표에 맞춤
    }

    /// <summary>
    /// FixedUpdate에서 사용할 목표 값(위치/회전/선속도/각속도)을 계산한다.
    /// - 카메라 회전 ω를 이용해 ω×r 속도를 추가하여 빠른 시점 회전 시 홀드 오브젝트가 뒤늦게 따라오는 느낌을 줄인다.
    /// - through-portal 상태면 포탈 변환을 적용한다.
    /// </summary>
    /// <param name="targetPos">계산된 목표 위치</param>
    /// <param name="targetRot">계산된 목표 회전</param>
    /// <param name="targetVel">계산된 목표 선속도</param>
    /// <param name="targetAngVel">계산된 목표 각속도</param>
    private void ComputeTargetFixed(out Vector3 targetPos, out Quaternion targetRot, out Vector3 targetVel, out Vector3 targetAngVel)
    {
        Vector3 basePos = ctx.HoldPoint.position;                                      // 기본 목표 위치(홀드포인트)

        Quaternion frameRot = GetHoldFrameRotation();                                  // 홀드 기준 프레임(카메라 기반) 회전
        Quaternion baseRot = frameRot * holdRotOffset;                                 // 기본 목표 회전(프레임 회전 + 오프셋)

        Vector3 baseVel = (ctx.PlayerRigidbody != null) ? ctx.PlayerRigidbody.linearVelocity : Vector3.zero; // 기본 목표 속도(플레이어 속도)

        // ✅ 프레임에서 캐시된 omega로 회전 유도 속도(ω×r) 추가
        Vector3 velFromRot = Vector3.zero;                                            // 카메라 회전으로 인한 추가 속도 초기값
        if (useCameraRotationVelocity && ctx.PlayerCamera != null)                    // 옵션이 켜져있고 카메라가 있으면
        {
            Transform camT = ctx.PlayerCamera.transform;                              // 카메라 트랜스폼
            Vector3 r = basePos - camT.position;                                      // 카메라 위치에서 홀드포인트까지의 벡터 r
            velFromRot = Vector3.Cross(cachedCamOmega, r);                            // 선속도 = ω × r
        }

        Vector3 baseTargetVel = baseVel + velFromRot;                                 // 최종 목표 속도(플레이어 속도 + 회전 유도 속도)

        targetPos = basePos;                                                          // 출력 목표 위치 설정
        targetRot = baseRot;                                                          // 출력 목표 회전 설정
        targetVel = baseTargetVel;                                                    // 출력 목표 선속도 설정
        targetAngVel = Vector3.zero;                                                  // 출력 목표 각속도(안정 우선으로 0)

        if (holdingThroughPortal && holdingInPortal && holdingOutPortal)              // through-portal 상태라면
        {
            targetPos = PortalMath.TransformPoint(basePos, holdingInPortal.Plane, holdingOutPortal.Plane);    // 포탈 변환된 목표 위치
            targetRot = PortalMath.TransformRotation(baseRot, holdingInPortal.Plane, holdingOutPortal.Plane); // 포탈 변환된 목표 회전
            targetVel = TransformDirection(baseTargetVel, holdingInPortal.Plane, holdingOutPortal.Plane);     // 포탈 변환된 목표 선속도
        }
    }

    /// <summary>
    /// 카메라 ω 계산을 위한 이전 회전값(prevCamRotFrame)을 현재 회전으로 초기화한다.
    /// - 스냅/워프 직후 튐을 줄이기 위해 사용한다.
    /// </summary>
    private void PrimeCameraOmegaHistory()
    {
        if (ctx != null && ctx.PlayerCamera != null)                                  // 카메라 참조가 있으면
        {
            prevCamRotFrame = ctx.PlayerCamera.transform.rotation;                   // 이전 회전을 현재로 초기화
            hasPrevCamRotFrame = true;                                               // 초기화 완료 표시
            cachedCamOmega = Vector3.zero;                                           // ω는 0부터 시작
        }
        else                                                                          // 카메라 참조가 없으면
        {
            hasPrevCamRotFrame = false;                                              // 히스토리 없음
            cachedCamOmega = Vector3.zero;                                           // ω=0
        }
    }

    /// <summary>
    /// 포탈 기준으로 방향 벡터를 변환한다(월드 방향).
    /// - inPlane 로컬로 변환 -> 180도 뒤집기 -> outPlane 월드로 변환
    /// </summary>
    /// <param name="dir">변환할 월드 방향 벡터</param>
    /// <param name="inPlane">입구 포탈 평면 트랜스폼</param>
    /// <param name="outPlane">출구 포탈 평면 트랜스폼</param>
    /// <returns>변환된 월드 방향 벡터</returns>
    private static Vector3 TransformDirection(Vector3 dir, Transform inPlane, Transform outPlane)
    {
        Vector3 rel = inPlane.InverseTransformDirection(dir);                         // inPlane 기준 로컬 방향으로 변환
        rel = HalfTurn * rel;                                                        // 포탈 통과 시 방향 반전(180도 회전)
        return outPlane.TransformDirection(rel);                                     // outPlane 기준 월드 방향으로 변환
    }

    // =============================
    // Warp events
    // =============================

    /// <summary>
    /// 플레이어 Rigidbody에서 PortalTraveller를 캐시한다.
    /// </summary>
    private void CachePlayerTraveller()
    {
        if (ctx != null && ctx.PlayerRigidbody != null)                               // 컨텍스트/플레이어 RB가 있으면
            playerTraveller = ctx.PlayerRigidbody.GetComponent<PortalTraveller>();   // PortalTraveller를 가져와 캐시
    }

    /// <summary>
    /// 플레이어 PortalTraveller의 Warped 이벤트를 구독한다.
    /// </summary>
    private void BindPlayerTraveller()
    {
        CachePlayerTraveller();                                                      // 최신 traveller 캐시 갱신
        if (playerTraveller != null)                                                 // traveller가 있으면
            playerTraveller.Warped += OnPlayerWarped;                                // 워프 이벤트 구독
    }

    /// <summary>
    /// 플레이어 PortalTraveller의 Warped 이벤트를 구독 해제한다.
    /// </summary>
    private void UnbindPlayerTraveller()
    {
        if (playerTraveller != null)                                                 // traveller가 있으면
            playerTraveller.Warped -= OnPlayerWarped;                                // 워프 이벤트 해제
    }

    /// <summary>
    /// 잡힌 오브젝트 PortalTraveller의 Warped 이벤트를 구독한다.
    /// </summary>
    private void BindHeldTraveller()
    {
        UnbindHeldTraveller();                                                       // 중복 구독 방지를 위해 먼저 해제
        if (!heldRb) return;                                                         // 잡힌 RB가 없으면 종료

        heldTraveller = heldRb.GetComponent<PortalTraveller>();                      // 잡힌 RB에서 PortalTraveller 가져오기
        if (heldTraveller != null)                                                   // traveller가 있으면
            heldTraveller.Warped += OnHeldWarped;                                    // 워프 이벤트 구독
    }

    /// <summary>
    /// 잡힌 오브젝트 PortalTraveller의 Warped 이벤트를 구독 해제한다.
    /// </summary>
    private void UnbindHeldTraveller()
    {
        if (heldTraveller != null)                                                   // traveller가 있으면
            heldTraveller.Warped -= OnHeldWarped;                                    // 워프 이벤트 해제
        heldTraveller = null;                                                        // 캐시 제거
    }

    /// <summary>
    /// 잡힌 오브젝트가 포탈 워프했을 때 호출된다.
    /// - objectSidePortal을 갱신하고, 플레이어 측 포탈이 비어있으면 from으로 추론한다.
    /// - 상태 갱신/ω 리셋/스냅으로 튐을 방지한다.
    /// </summary>
    /// <param name="from">입구 포탈</param>
    /// <param name="to">출구 포탈</param>
    private void OnHeldWarped(Portal from, Portal to)
    {
        if (!IsHolding) return;                                                      // 홀드 중이 아니면 무시

        objectSidePortal = to;                                                       // 오브젝트 측 포탈은 출구(to)
        if (playerSidePortal == null) playerSidePortal = from;                       // 플레이어 측 포탈이 비어있으면 입구(from)로 추론

        RefreshThroughPortalState(force: true);                                      // 상태 강제 갱신(머지/스플릿 처리)
        PrimeCameraOmegaHistory();                                                   // ω 히스토리 리셋
        SnapHeldToHoldPoint();                                                       // 홀드포인트로 스냅
    }

    /// <summary>
    /// 플레이어가 포탈 워프했을 때 호출된다.
    /// - playerSidePortal을 갱신하고, 오브젝트 측 포탈이 비어있으면 from으로 추론한다.
    /// - 상태 갱신/ω 리셋/스냅으로 튐을 방지한다.
    /// </summary>
    /// <param name="from">입구 포탈</param>
    /// <param name="to">출구 포탈</param>
    private void OnPlayerWarped(Portal from, Portal to)
    {
        if (!IsHolding) return;                                                      // 홀드 중이 아니면 무시

        playerSidePortal = to;                                                       // 플레이어 측 포탈은 출구(to)
        if (objectSidePortal == null) objectSidePortal = from;                       // 오브젝트 측 포탈이 비어있으면 입구(from)로 추론

        RefreshThroughPortalState(force: true);                                      // 상태 강제 갱신(머지/스플릿 처리)
        PrimeCameraOmegaHistory();                                                   // ω 히스토리 리셋
        SnapHeldToHoldPoint();                                                       // 홀드포인트로 스냅
    }

    // =============================
    // Rotation snap at pickup
    // =============================

    /// <summary>
    /// 집는 순간의 표면 노멀(rbHit.normal)을 기준으로 회전 스냅을 적용해 holdRotOffset을 설정한다.
    /// - snapAngleDeg 내로 축 정렬이 가능하면, 로컬 축(+/-X/Y/Z) 중 가장 가까운 축으로 스냅한다.
    /// - snapFaceTowardPlayer가 true면 플레이어를 바라보는 방향으로 회전 오프셋을 만든다.
    /// </summary>
    /// <param name="rbHit">실제 잡힌 오브젝트에 대한 RaycastHit(표면 노멀 포함)</param>
    private void SetupGrabRotation(RaycastHit rbHit)
    {
        Quaternion frameRot = GetHoldFrameRotation();                                 // 홀드 기준 프레임 회전

        Vector3 localN = heldRb.transform.InverseTransformDirection(rbHit.normal).normalized; // 노멀을 오브젝트 로컬로 변환/정규화
        float cos = Mathf.Cos(snapAngleDeg * Mathf.Deg2Rad);                          // 스냅 임계값(코사인)

        float ax = Mathf.Abs(localN.x);                                               // 로컬 노멀 X 성분 절댓값
        float ay = Mathf.Abs(localN.y);                                               // 로컬 노멀 Y 성분 절댓값
        float az = Mathf.Abs(localN.z);                                               // 로컬 노멀 Z 성분 절댓값
        float m = Mathf.Max(ax, Mathf.Max(ay, az));                                   // 가장 큰 축 성분(가장 가까운 축)

        bool snapped = (m >= cos);                                                    // 임계값을 넘으면 축 스냅 가능

        if (!snapped || !snapFaceTowardPlayer)                                        // 스냅 불가거나 '플레이어 바라보기' 옵션이 꺼져있으면
        {
            holdRotOffset = Quaternion.Inverse(frameRot) * heldRb.rotation;           // 현재 회전을 그대로 오프셋으로 저장
            return;                                                                   // 종료
        }

        Vector3 localAxis;                                                            // 스냅될 로컬 축
        if (m == ax) localAxis = (localN.x >= 0f) ? Vector3.right : Vector3.left;     // X 축이 가장 크면 +/-X 선택
        else if (m == ay) localAxis = (localN.y >= 0f) ? Vector3.up : Vector3.down;   // Y 축이 가장 크면 +/-Y 선택
        else localAxis = (localN.z >= 0f) ? Vector3.forward : Vector3.back;           // Z 축이 가장 크면 +/-Z 선택

        Vector3 holdForward = frameRot * Vector3.forward;                             // 홀드 프레임의 forward 방향(월드)

        Quaternion B = Quaternion.LookRotation(-holdForward, Vector3.up);             // 플레이어를 향하도록(대략) 바라보는 기준 회전
        Quaternion M = Quaternion.FromToRotation(localAxis, Vector3.forward);         // 로컬 축을 "앞(+Z)"으로 맞추는 보정 회전
        Quaternion desiredWorld = B * M;                                              // 최종 목표 월드 회전

        holdRotOffset = Quaternion.Inverse(frameRot) * desiredWorld;                  // 프레임 대비 목표 회전 오프셋 저장
    }

    /// <summary>
    /// 홀드 기준 프레임 회전을 계산한다.
    /// - useYawOnlyFrame이 true면 카메라 forward를 수평면(Up)으로 투영한 Yaw만 사용한다.
    /// - false면 카메라 전체 회전을 그대로 사용한다.
    /// </summary>
    /// <returns>홀드 기준 프레임 회전(월드)</returns>
    private Quaternion GetHoldFrameRotation()
    {
        if (!ctx || !ctx.PlayerCamera) return transform.rotation;                     // 카메라가 없으면 자기 회전 기준

        Transform camT = ctx.PlayerCamera.transform;                                  // 카메라 트랜스폼

        if (!useYawOnlyFrame)                                                        // Yaw 전용을 쓰지 않으면
            return camT.rotation;                                                     // 카메라 전체 회전 반환

        Vector3 f = Vector3.ProjectOnPlane(camT.forward, Vector3.up);                 // 카메라 forward를 수평면으로 투영
        if (f.sqrMagnitude < 1e-6f) f = transform.forward;                            // 투영이 너무 작으면 자기 forward로 대체
        f.Normalize();                                                                // 정규화

        return Quaternion.LookRotation(f, Vector3.up);                                 // 수평 forward + up으로 프레임 회전 생성
    }

    // =============================
    // Collision ignore
    // =============================

    /// <summary>
    /// 플레이어 Rigidbody 하위의 모든 콜라이더를 캐시한다.
    /// - 홀드 중 Physics.IgnoreCollision을 걸기 위해 필요하다.
    /// </summary>
    private void CachePlayerColliders()
    {
        playerCols.Clear();                                                           // 기존 목록 초기화
        Rigidbody prb = ctx ? ctx.PlayerRigidbody : null;                             // 플레이어 RB 참조
        if (prb != null)                                                              // 플레이어 RB가 있으면
            prb.GetComponentsInChildren(true, playerCols);                            // 하위 콜라이더 전부 수집
    }

    /// <summary>
    /// 잡힌 오브젝트의 콜라이더와 플레이어 콜라이더 간 충돌을 무시/복구한다.
    /// </summary>
    /// <param name="ignore">true면 충돌 무시, false면 충돌 복구</param>
    private void SetIgnorePlayerCollision(bool ignore)
    {
        if (heldCols.Count == 0 || playerCols.Count == 0) return;                     // 콜라이더 목록이 비어있으면 종료

        for (int i = 0; i < heldCols.Count; i++)                                      // 잡힌 콜라이더 루프
        {
            var hc = heldCols[i];                                                     // 잡힌 콜라이더
            if (!hc) continue;                                                        // null이면 스킵

            for (int j = 0; j < playerCols.Count; j++)                                // 플레이어 콜라이더 루프
            {
                var pc = playerCols[j];                                               // 플레이어 콜라이더
                if (!pc) continue;                                                    // null이면 스킵

                Physics.IgnoreCollision(hc, pc, ignore);                               // 충돌 무시/복구 적용
            }
        }
    }
}
