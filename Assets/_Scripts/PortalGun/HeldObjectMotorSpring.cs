// HeldObjectMotorSpring.cs  (Velocity-Servo version)
using UnityEngine;

[DisallowMultipleComponent]
public class HeldObjectMotorSpring : MonoBehaviour
{
    [Header("Position Servo")]
    [Tooltip("목표까지 따라붙는 시간(초). 작을수록 빡세게 따라감. 0.05~0.12 권장")]
    [SerializeField] private float positionCatchTime = 0.07f;

    [Tooltip("최대 이동 속도(m/s)")]
    [SerializeField] private float maxSpeed = 25f;

    [Tooltip("최대 가속도(m/s^2). 너무 크면 충돌 시 튐. 80~300")]
    [SerializeField] private float maxAccel = 200f;

    [Header("Rotation Servo")]
    [SerializeField] private bool driveRotation = true;

    [Tooltip("목표 회전에 따라붙는 시간(초). 0.05~0.12 권장")]
    [SerializeField] private float rotationCatchTime = 0.08f;

    [Tooltip("최대 각속도(rad/s). Unity 기본 maxAngularVelocity도 같이 올려야 함")]
    [SerializeField] private float maxAngularSpeed = 40f;

    [Tooltip("최대 각가속도(rad/s^2)")]
    [SerializeField] private float maxAngularAccel = 500f;

    [Header("Target Velocity Estimate (optional)")]
    [SerializeField] private bool estimateTargetVelocity = true;

    private Vector3 _prevTargetPos;
    private Quaternion _prevTargetRot;
    private bool _hasHistory;

    public void ResetTargetHistory() => _hasHistory = false;

    /// <summary>
    /// 워프/스냅 직후 타겟 점프에 의한 오차 누적 방지용 스냅.
    /// </summary>
    public void Snap(Rigidbody rb, Vector3 pos, Quaternion rot, Vector3 vel)
    {
        if (!rb) return;

        rb.position = pos;
        rb.rotation = rot;
        rb.linearVelocity = vel;
        rb.angularVelocity = Vector3.zero;

        ResetTargetHistory();
    }

    // (A) 기존 시그니처 유지: 내부 추정
    public void Apply(Rigidbody rb, Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 targetVel = Vector3.zero;
        Vector3 targetAngVel = Vector3.zero;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.02f;

        if (estimateTargetVelocity)
        {
            if (!_hasHistory)
            {
                _prevTargetPos = targetPos;
                _prevTargetRot = targetRot;
                _hasHistory = true;
            }
            else
            {
                targetVel = (targetPos - _prevTargetPos) / dt;
                targetAngVel = CalcTargetAngularVelocity(_prevTargetRot, targetRot, dt);

                _prevTargetPos = targetPos;
                _prevTargetRot = targetRot;
            }
        }

        Apply(rb, targetPos, targetRot, targetVel, targetAngVel);
    }

    // (B) 외부에서 targetVel/targetAngVel을 준 경우
    public void Apply(Rigidbody rb, Vector3 targetPos, Quaternion targetRot, Vector3 targetVel, Vector3 targetAngVel)
    {
        if (!rb) return;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.02f;

        // =========================
        // Position velocity servo
        // =========================
        Vector3 com = rb.worldCenterOfMass;
        Vector3 posError = targetPos - com;

        float tPos = Mathf.Max(0.001f, positionCatchTime);
        Vector3 desiredVel = targetVel + (posError / tPos);
        desiredVel = Vector3.ClampMagnitude(desiredVel, maxSpeed);

        Vector3 curVel = rb.linearVelocity;
        Vector3 nextVel = Vector3.MoveTowards(curVel, desiredVel, maxAccel * dt);
        rb.linearVelocity = nextVel;

        // =========================
        // Rotation velocity servo
        // =========================
        if (!driveRotation) return;

        Vector3 rotErrorRad = CalcRotationErrorAxisAngle(rb.rotation, targetRot); // axis * rad
        float tRot = Mathf.Max(0.001f, rotationCatchTime);

        Vector3 desiredAngVel = targetAngVel + (rotErrorRad / tRot);
        desiredAngVel = Vector3.ClampMagnitude(desiredAngVel, maxAngularSpeed);

        Vector3 curAngVel = rb.angularVelocity;
        Vector3 nextAngVel = Vector3.MoveTowards(curAngVel, desiredAngVel, maxAngularAccel * dt);
        rb.angularVelocity = nextAngVel;
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

    private static Vector3 CalcTargetAngularVelocity(Quaternion prev, Quaternion cur, float dt)
    {
        Quaternion dq = cur * Quaternion.Inverse(prev);
        dq.ToAngleAxis(out float angleDeg, out Vector3 axis);

        if (axis.sqrMagnitude < 1e-8f) return Vector3.zero;
        if (angleDeg > 180f) angleDeg -= 360f;

        axis.Normalize();
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return axis * (angleRad / Mathf.Max(1e-6f, dt));
    }
}
