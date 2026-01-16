// HeldObjectMotorSpring.cs
// - 기본은 "Velocity Servo (accel clamp)" : 빠르게 따라가되 안정적
// - 타겟 근처에서는 "Lock Mode"로 들어가 MovePosition/MoveRotation으로 떨림 제거
using UnityEngine;

[DisallowMultipleComponent]
public class HeldObjectMotorSpring : MonoBehaviour
{
    [Header("Position Servo (Follow)")]
    [Tooltip("작을수록 빨리 따라감. 0.04~0.10 권장")]
    [SerializeField] private float positionCatchTime = 0.06f;
    [SerializeField] private float maxSpeed = 35f;
    [SerializeField] private float maxAccel = 300f;

    [Header("Rotation Servo (Follow)")]
    [SerializeField] private bool driveRotation = true;
    [SerializeField] private float rotationCatchTime = 0.08f;
    [SerializeField] private float maxAngularSpeed = 50f;     // rad/s
    [SerializeField] private float maxAngularAccel = 800f;    // rad/s^2

    [Header("Lock Mode (No Jitter Zone)")]
    [SerializeField] private bool enableLock = true;

    [Tooltip("이 값 이하로 '연속' 들어오면 Lock 진입(거리, m)")]
    [SerializeField] private float lockEnterPosEps = 0.010f;

    [Tooltip("이 값 이하로 '연속' 들어오면 Lock 진입(속도 오차, m/s)")]
    [SerializeField] private float lockEnterVelEps = 0.20f;

    [Tooltip("Lock 상태에서 이 값 이상 벗어나면 Lock 해제(거리, m)")]
    [SerializeField] private float lockExitPosEps = 0.030f;

    [Tooltip("Lock 상태에서 이 값 이상 벗어나면 Lock 해제(속도 오차, m/s)")]
    [SerializeField] private float lockExitVelEps = 0.60f;

    [Tooltip("Lock 진입에 필요한 연속 프레임 수(2~6 권장)")]
    [SerializeField] private int lockFramesRequired = 3;

    [Tooltip("Lock 상태에서 MovePosition/MoveRotation 사용")]
    [SerializeField] private bool lockUseMove = true;

    [Tooltip("Lock 상태에서 스윕으로 막히면 Lock 유지하지 않음(벽 끼임 방지)")]
    [SerializeField] private bool lockSafeSweep = true;

    private bool _locked;
    private int _lockCounter;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    public void ResetMotorState()
    {
        _locked = false;
        _lockCounter = 0;
    }

    public void Snap(Rigidbody rb, Vector3 pos, Quaternion rot, Vector3 vel)
    {
        if (!rb) return;
        rb.position = pos;
        rb.rotation = rot;
        rb.linearVelocity = vel;
        rb.angularVelocity = Vector3.zero;
        ResetMotorState();
    }

    public void Apply(Rigidbody rb, Vector3 targetPos, Quaternion targetRot, Vector3 targetVel, Vector3 targetAngVel)
    {
        if (!rb) return;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.02f;

        rb.WakeUp();

        // ===== Errors based on COM (핵심: COM 기준으로 잡아야 안정) =====
        Vector3 com = rb.worldCenterOfMass;
        Vector3 posError = targetPos - com;

        Vector3 curVel = rb.linearVelocity;
        Vector3 velError = targetVel - curVel;

        // =========================
        // 1) Lock mode (hysteresis + consecutive frames)
        // =========================
        if (enableLock)
        {
            float enterPos2 = lockEnterPosEps * lockEnterPosEps;
            float enterVel2 = lockEnterVelEps * lockEnterVelEps;
            float exitPos2 = lockExitPosEps * lockExitPosEps;
            float exitVel2 = lockExitVelEps * lockExitVelEps;

            if (_locked)
            {
                // exit 조건
                if (posError.sqrMagnitude > exitPos2 || velError.sqrMagnitude > exitVel2)
                {
                    _locked = false;
                    _lockCounter = 0;
                }
                else
                {
                    // lock 유지: "딱 붙여놓기" (떨림 제거)
                    if (TryLockStep(rb, posError, targetRot, targetVel, targetAngVel))
                        return;

                    // 스윕에 막히면 lock 유지 금지 → 서보로 내려가서 해결
                    _locked = false;
                    _lockCounter = 0;
                }
            }
            else
            {
                // enter 조건을 "연속"으로 만족해야 lock 진입(토글 방지)
                if (posError.sqrMagnitude <= enterPos2 && velError.sqrMagnitude <= enterVel2)
                {
                    _lockCounter++;
                    if (_lockCounter >= Mathf.Max(1, lockFramesRequired))
                    {
                        _locked = true;
                        _lockCounter = 0;

                        if (TryLockStep(rb, posError, targetRot, targetVel, targetAngVel))
                            return;

                        _locked = false;
                    }
                }
                else
                {
                    _lockCounter = 0;
                }
            }
        }

        // =========================
        // 2) Follow mode: Velocity Servo + accel clamp (안 떨리는 쪽)
        // desiredVel = targetVel + posError / catchTime
        // =========================
        float tPos = Mathf.Max(0.001f, positionCatchTime);
        Vector3 desiredVel = targetVel + (posError / tPos);
        desiredVel = Vector3.ClampMagnitude(desiredVel, maxSpeed);

        Vector3 nextVel = Vector3.MoveTowards(curVel, desiredVel, maxAccel * dt);
        rb.linearVelocity = nextVel;

        // =========================
        // Rotation follow
        // =========================
        if (!driveRotation) return;

        Vector3 rotErrorRad = CalcRotationErrorAxisAngle(rb.rotation, targetRot); // axis * rad
        float tRot = Mathf.Max(0.001f, rotationCatchTime);

        Vector3 curAngVel = rb.angularVelocity;
        Vector3 desiredAngVel = targetAngVel + (rotErrorRad / tRot);
        desiredAngVel = Vector3.ClampMagnitude(desiredAngVel, maxAngularSpeed);

        Vector3 nextAngVel = Vector3.MoveTowards(curAngVel, desiredAngVel, maxAngularAccel * dt);
        rb.angularVelocity = nextAngVel;
    }

    private bool TryLockStep(Rigidbody rb, Vector3 posError, Quaternion targetRot, Vector3 targetVel, Vector3 targetAngVel)
    {
        // COM을 targetPos에 붙이기 위해 rb.position을 같은 델타로 이동
        Vector3 moveDelta = posError;

        if (lockSafeSweep && moveDelta.sqrMagnitude > 1e-12f)
        {
            Vector3 dir = moveDelta.normalized;
            float dist = moveDelta.magnitude;
            if (rb.SweepTest(dir, out _, dist, QueryTriggerInteraction.Ignore))
                return false; // 막히면 lock으로 억지로 붙이지 않음
        }

        Vector3 newPos = rb.position + moveDelta;

        if (lockUseMove) rb.MovePosition(newPos);
        else rb.position = newPos;

        rb.linearVelocity = targetVel;

        if (!driveRotation) return true;

        if (lockUseMove) rb.MoveRotation(targetRot);
        else rb.rotation = targetRot;

        rb.angularVelocity = targetAngVel; // 보통 0으로 들어옴(컨트롤러에서 0 줌)
        return true;
    }

    private static Vector3 CalcRotationErrorAxisAngle(Quaternion current, Quaternion target)
    {
        Quaternion q = target * Quaternion.Inverse(current);
        q.ToAngleAxis(out float angleDeg, out Vector3 axis);

        if (axis.sqrMagnitude < 1e-8f) return Vector3.zero;
        if (angleDeg > 180f) angleDeg -= 360f;

        axis.Normalize();
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return axis * angleRad;
    }
}
