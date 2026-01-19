// - 기본은 "Velocity Servo (accel clamp)" : 빠르게 따라가되 안정적
// - 타겟 근처에서는 "Lock Mode"로 들어가 MovePosition/MoveRotation으로 떨림 제거
using UnityEngine;

// 이 스크립트는 "홀드된 Rigidbody"를 목표 위치/회전에 안정적으로 수렴시키는 물리 모터이다.
// - Follow 모드: 목표 속도 = (targetVel + posError/catchTime), 가속도/속도 클램프로 안정화
// - Lock 모드: 충분히 가까워지면 MovePosition/MoveRotation으로 강제 고정하여 미세 떨림을 제거(히스테리시스/연속 프레임 조건)
[DisallowMultipleComponent]
public class HeldObjectMotorSpring : MonoBehaviour
{
    [Header("Position Servo (Follow)")]
    [Tooltip("작을수록 빨리 따라감. 0.04~0.10 권장")]
    [SerializeField] private float positionCatchTime = 0.06f;                        // 위치 오차를 보정하는 시간(작을수록 강하게 끌림)
    [SerializeField] private float maxSpeed = 35f;                                   // Follow 모드에서의 최대 선속도(m/s)
    [SerializeField] private float maxAccel = 300f;                                  // Follow 모드에서의 최대 선가속도(m/s^2)

    [Header("Rotation Servo (Follow)")]
    [SerializeField] private bool driveRotation = true;                              // 회전 추종을 사용할지 여부
    [SerializeField] private float rotationCatchTime = 0.08f;                         // 회전 오차를 보정하는 시간(작을수록 강하게 끌림)
    [SerializeField] private float maxAngularSpeed = 50f;                             // Follow 모드에서의 최대 각속도(rad/s)
    [SerializeField] private float maxAngularAccel = 800f;                            // Follow 모드에서의 최대 각가속도(rad/s^2)

    [Header("Lock Mode (No Jitter Zone)")]
    [SerializeField] private bool enableLock = true;                                 // Lock 모드 활성화 여부

    [Tooltip("이 값 이하로 '연속' 들어오면 Lock 진입(거리, m)")]
    [SerializeField] private float lockEnterPosEps = 0.010f;                          // Lock 진입 거리 임계값(m)

    [Tooltip("이 값 이하로 '연속' 들어오면 Lock 진입(속도 오차, m/s)")]
    [SerializeField] private float lockEnterVelEps = 0.20f;                           // Lock 진입 속도오차 임계값(m/s)

    [Tooltip("Lock 상태에서 이 값 이상 벗어나면 Lock 해제(거리, m)")]
    [SerializeField] private float lockExitPosEps = 0.030f;                           // Lock 해제 거리 임계값(m) (히스테리시스)

    [Tooltip("Lock 상태에서 이 값 이상 벗어나면 Lock 해제(속도 오차, m/s)")]
    [SerializeField] private float lockExitVelEps = 0.60f;                            // Lock 해제 속도오차 임계값(m/s) (히스테리시스)

    [Tooltip("Lock 진입에 필요한 연속 프레임 수(2~6 권장)")]
    [SerializeField] private int lockFramesRequired = 3;                              // Lock 진입 조건을 만족해야 하는 연속 Fixed 프레임 수

    [Tooltip("Lock 상태에서 MovePosition/MoveRotation 사용")]
    [SerializeField] private bool lockUseMove = true;                                 // Lock에서 MovePosition/MoveRotation을 쓸지 여부

    [Tooltip("Lock 상태에서 스윕으로 막히면 Lock 유지하지 않음(벽 끼임 방지)")]
    [SerializeField] private bool lockSafeSweep = true;                               // Lock 시도 전에 SweepTest로 막힘 체크할지 여부

    private bool _locked;                                                            // 현재 Lock 상태인지 여부
    private int _lockCounter;                                                        // Lock 진입을 위한 연속 프레임 카운터

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);     // (현재 스크립트에서는 직접 사용하지 않지만, 포탈계 확장 대비 상수)

    /// <summary>
    /// Lock/Follower 내부 상태를 초기화한다.
    /// - Snap 직후 또는 강제 상태 리셋 시 호출한다.
    /// </summary>
    public void ResetMotorState()
    {
        _locked = false;                                                             // Lock 상태 해제
        _lockCounter = 0;                                                            // 연속 프레임 카운터 초기화
    }

    /// <summary>
    /// Rigidbody를 목표 위치/회전/속도로 "즉시" 맞춘다.
    /// - 떨림/스파이크 방지를 위해 각속도는 0으로 초기화한다.
    /// </summary>
    /// <param name="rb">스냅할 Rigidbody</param>
    /// <param name="pos">목표 위치</param>
    /// <param name="rot">목표 회전</param>
    /// <param name="vel">목표 선속도</param>
    public void Snap(Rigidbody rb, Vector3 pos, Quaternion rot, Vector3 vel)
    {
        if (!rb) return;                                                             // RB가 없으면 종료
        rb.position = pos;                                                           // 위치 즉시 설정
        rb.rotation = rot;                                                           // 회전 즉시 설정
        rb.linearVelocity = vel;                                                     // 선속도 설정
        rb.angularVelocity = Vector3.zero;                                           // 각속도 0(안정)
        ResetMotorState();                                                           // 모터 상태 리셋(락/카운터 초기화)
    }

    /// <summary>
    /// Rigidbody를 목표 위치/회전/속도/각속도로 수렴시키는 메인 함수(FixedUpdate에서 호출).
    /// - Lock 모드가 켜져 있고 조건을 만족하면 Lock을 시도한다.
    /// - 아니면 Follow(velocity servo)로 선속도/각속도를 갱신한다.
    /// </summary>
    /// <param name="rb">제어할 Rigidbody</param>
    /// <param name="targetPos">목표 위치(주의: COM 기준으로 오차 계산)</param>
    /// <param name="targetRot">목표 회전</param>
    /// <param name="targetVel">목표 선속도</param>
    /// <param name="targetAngVel">목표 각속도</param>
    public void Apply(Rigidbody rb, Vector3 targetPos, Quaternion targetRot, Vector3 targetVel, Vector3 targetAngVel)
    {
        if (!rb) return;                                                             // RB가 없으면 종료

        float dt = Time.fixedDeltaTime;                                              // 물리 델타 시간
        if (dt <= 0f) dt = 0.02f;                                                    // 비정상 dt면 기본값으로 보정

        rb.WakeUp();                                                                 // 슬립 상태일 수 있으니 깨움

        // ===== Errors based on COM (핵심: COM 기준으로 잡아야 안정) =====
        Vector3 com = rb.worldCenterOfMass;                                          // 월드 기준 질량중심(COM)
        Vector3 posError = targetPos - com;                                          // 위치 오차(목표 - 현재 COM)

        Vector3 curVel = rb.linearVelocity;                                          // 현재 선속도
        Vector3 velError = targetVel - curVel;                                       // 선속도 오차(목표 - 현재)

        // =========================
        // 1) Lock mode (hysteresis + consecutive frames)
        // =========================
        if (enableLock)                                                               // Lock 모드가 켜져있다면
        {
            float enterPos2 = lockEnterPosEps * lockEnterPosEps;                      // Lock 진입 거리 임계값^2
            float enterVel2 = lockEnterVelEps * lockEnterVelEps;                      // Lock 진입 속도오차 임계값^2
            float exitPos2 = lockExitPosEps * lockExitPosEps;                         // Lock 해제 거리 임계값^2
            float exitVel2 = lockExitVelEps * lockExitVelEps;                         // Lock 해제 속도오차 임계값^2

            if (_locked)                                                              // 이미 Lock 상태라면
            {
                // exit 조건
                if (posError.sqrMagnitude > exitPos2 || velError.sqrMagnitude > exitVel2) // 해제 임계값을 넘으면
                {
                    _locked = false;                                                  // Lock 해제
                    _lockCounter = 0;                                                 // 카운터 초기화
                }
                else                                                                  // 아직 해제 조건이 아니면
                {
                    // lock 유지: "딱 붙여놓기" (떨림 제거)
                    if (TryLockStep(rb, posError, targetRot, targetVel, targetAngVel)) // Lock 방식으로 한 스텝 시도
                        return;                                                       // 성공했으면 Follow로 내려가지 않고 종료

                    // 스윕에 막히면 lock 유지 금지 → 서보로 내려가서 해결
                    _locked = false;                                                  // Lock 유지 실패(막힘) => Lock 해제
                    _lockCounter = 0;                                                 // 카운터 초기화
                }
            }
            else                                                                      // 현재 Lock 상태가 아니라면
            {
                // enter 조건을 "연속"으로 만족해야 lock 진입(토글 방지)
                if (posError.sqrMagnitude <= enterPos2 && velError.sqrMagnitude <= enterVel2) // 진입 임계값 이내면
                {
                    _lockCounter++;                                                   // 연속 프레임 카운트 증가
                    if (_lockCounter >= Mathf.Max(1, lockFramesRequired))             // 요구 프레임 수를 만족하면
                    {
                        _locked = true;                                               // Lock 상태로 전환
                        _lockCounter = 0;                                             // 카운터 초기화

                        if (TryLockStep(rb, posError, targetRot, targetVel, targetAngVel)) // Lock 스텝 시도
                            return;                                                   // 성공하면 종료

                        _locked = false;                                              // 실패하면 Lock 해제(Follow로 처리)
                    }
                }
                else                                                                   // 진입 조건을 만족하지 않으면
                {
                    _lockCounter = 0;                                                  // 연속 조건이 깨졌으니 카운터 초기화
                }
            }
        }

        // =========================
        // 2) Follow mode: Velocity Servo + accel clamp (안 떨리는 쪽)
        // desiredVel = targetVel + posError / catchTime
        // =========================
        float tPos = Mathf.Max(0.001f, positionCatchTime);                             // catchTime이 0이 되지 않게 보정
        Vector3 desiredVel = targetVel + (posError / tPos);                            // 목표 선속도 = 목표속도 + 위치오차 보정분
        desiredVel = Vector3.ClampMagnitude(desiredVel, maxSpeed);                     // 최대 속도 제한

        Vector3 nextVel = Vector3.MoveTowards(curVel, desiredVel, maxAccel * dt);      // 가속도 제한으로 현재->목표 속도 이동
        rb.linearVelocity = nextVel;                                                   // RB 선속도 갱신

        // =========================
        // Rotation follow
        // =========================
        if (!driveRotation) return;                                                   // 회전 추종이 꺼져있으면 종료

        Vector3 rotErrorRad = CalcRotationErrorAxisAngle(rb.rotation, targetRot);     // 회전 오차(axis * rad)
        float tRot = Mathf.Max(0.001f, rotationCatchTime);                            // rotationCatchTime이 0이 되지 않게 보정

        Vector3 curAngVel = rb.angularVelocity;                                       // 현재 각속도
        Vector3 desiredAngVel = targetAngVel + (rotErrorRad / tRot);                  // 목표 각속도 = 목표각속도 + 회전오차 보정분
        desiredAngVel = Vector3.ClampMagnitude(desiredAngVel, maxAngularSpeed);       // 최대 각속도 제한

        Vector3 nextAngVel = Vector3.MoveTowards(curAngVel, desiredAngVel, maxAngularAccel * dt); // 각가속도 제한으로 현재->목표 각속도 이동
        rb.angularVelocity = nextAngVel;                                              // RB 각속도 갱신
    }

    /// <summary>
    /// Lock 모드에서 한 스텝을 수행한다.
    /// - COM을 목표로 맞추기 위해 rb.position에 같은 델타를 적용한다.
    /// - lockSafeSweep가 켜져있으면 SweepTest로 막힘을 검사하고 막히면 실패한다.
    /// - lockUseMove가 켜져있으면 MovePosition/MoveRotation을 사용한다.
    /// </summary>
    /// <param name="rb">제어할 Rigidbody</param>
    /// <param name="posError">목표Pos - 현재COM 위치 오차</param>
    /// <param name="targetRot">목표 회전</param>
    /// <param name="targetVel">목표 선속도</param>
    /// <param name="targetAngVel">목표 각속도</param>
    /// <returns>Lock 스텝 성공 여부(true면 Lock 유지 가능)</returns>
    private bool TryLockStep(Rigidbody rb, Vector3 posError, Quaternion targetRot, Vector3 targetVel, Vector3 targetAngVel)
    {
        // COM을 targetPos에 붙이기 위해 rb.position을 같은 델타로 이동
        Vector3 moveDelta = posError;                                                 // COM 오차만큼 이동해야 할 델타(월드)

        if (lockSafeSweep && moveDelta.sqrMagnitude > 1e-12f)                         // 안전 스윕이 켜져있고 이동량이 유의미하면
        {
            Vector3 dir = moveDelta.normalized;                                       // 스윕 방향
            float dist = moveDelta.magnitude;                                         // 스윕 거리
            if (rb.SweepTest(dir, out _, dist, QueryTriggerInteraction.Ignore))       // 스윕 중 충돌이 감지되면
                return false;                                                         // 막히므로 Lock 강제 고정 실패
        }

        Vector3 newPos = rb.position + moveDelta;                                     // rb.position을 COM 오차만큼 이동시킨 새 위치

        if (lockUseMove) rb.MovePosition(newPos);                                     // MovePosition 사용(물리 스텝에 친화적)
        else rb.position = newPos;                                                    // 즉시 위치 변경(더 강제)

        rb.linearVelocity = targetVel;                                                // Lock 상태에서 선속도는 목표로 맞춤(보통 플레이어 속도)

        if (!driveRotation) return true;                                              // 회전 추종이 꺼져있으면 여기서 성공 처리

        if (lockUseMove) rb.MoveRotation(targetRot);                                  // MoveRotation 사용
        else rb.rotation = targetRot;                                                 // 즉시 회전 변경

        rb.angularVelocity = targetAngVel;                                            // 각속도는 목표로 맞춤(보통 0)
        return true;                                                                  // Lock 스텝 성공
    }

    /// <summary>
    /// 현재 회전에서 목표 회전으로 가기 위한 회전 오차를 axis-angle(rad) 형태로 반환한다.
    /// - 반환값은 "axis * angleRad" 벡터(회전 축 방향으로 라디안 크기).
    /// </summary>
    /// <param name="current">현재 회전</param>
    /// <param name="target">목표 회전</param>
    /// <returns>axis * angleRad 형태의 회전 오차 벡터</returns>
    private static Vector3 CalcRotationErrorAxisAngle(Quaternion current, Quaternion target)
    {
        Quaternion q = target * Quaternion.Inverse(current);                          // 현재->목표로 가는 상대 회전
        q.ToAngleAxis(out float angleDeg, out Vector3 axis);                          // 상대 회전을 axis-angle로 변환

        if (axis.sqrMagnitude < 1e-8f) return Vector3.zero;                           // 축이 거의 0이면 오차 0 처리
        if (angleDeg > 180f) angleDeg -= 360f;                                       // -180~180으로 정규화

        axis.Normalize();                                                             // 축 정규화
        float angleRad = angleDeg * Mathf.Deg2Rad;                                    // 각도를 라디안으로 변환
        return axis * angleRad;                                                       // axis * rad 반환
    }
}
