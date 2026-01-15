// HeldObjectMotorSpring.cs  (Critically-damped PD + settle deadzone + micro snap)
using UnityEngine;

[DisallowMultipleComponent]
public class HeldObjectMotorSpring : MonoBehaviour
{
    [Header("Position Servo")]
    [Tooltip("작을수록 더 빡세게 따라감. 0.04~0.10 권장")]
    [SerializeField] private float positionCatchTime = 0.06f;

    [Tooltip("최대 이동 속도(m/s)")]
    [SerializeField] private float maxSpeed = 35f;

    [Tooltip("최대 가속도(m/s^2)")]
    [SerializeField] private float maxAccel = 350f;

    [Header("Rotation Servo")]
    [SerializeField] private bool driveRotation = true;

    [Tooltip("0.05~0.12 권장")]
    [SerializeField] private float rotationCatchTime = 0.08f;

    [Tooltip("최대 각속도(rad/s). rb.maxAngularVelocity도 같이 올려야 함")]
    [SerializeField] private float maxAngularSpeed = 50f;

    [Tooltip("최대 각가속도(rad/s^2)")]
    [SerializeField] private float maxAngularAccel = 800f;

    [Header("Settle / Micro Snap (떨림 제거 핵심)")]
    [Tooltip("타겟에 거의 붙었을 때, 속도 보정 대신 '붙여놓기'로 떨림 제거")]
    [SerializeField] private bool enableSettle = true;

    [Tooltip("COM-타겟 거리(m) 이하면 정착 후보. 0.008~0.02")]
    [SerializeField] private float settlePosEps = 0.012f;

    [Tooltip("속도 오차(m/s) 이하면 정착 후보. 0.10~0.35")]
    [SerializeField] private float settleVelEps = 0.20f;

    [Tooltip("정착 시 이동은 MovePosition/MoveRotation 사용(충돌/보간 안정)")]
    [SerializeField] private bool useMoveWhenSettle = true;

    [Tooltip("정착 시 스냅하기 전에 SweepTest로 경로 충돌 체크")]
    [SerializeField] private bool safeSweepBeforeSettle = true;

    public void Snap(Rigidbody rb, Vector3 pos, Quaternion rot, Vector3 vel)
    {
        if (!rb) return;
        rb.position = pos;
        rb.rotation = rot;
        rb.linearVelocity = vel;
        rb.angularVelocity = Vector3.zero;
    }

    public void Apply(Rigidbody rb, Vector3 targetPos, Quaternion targetRot, Vector3 targetVel, Vector3 targetAngVel)
    {
        if (!rb) return;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.02f;

        rb.WakeUp();

        // ====== errors ======
        Vector3 com = rb.worldCenterOfMass;
        Vector3 posError = targetPos - com;

        Vector3 curVel = rb.linearVelocity;
        Vector3 velError = targetVel - curVel;

        // =========================
        // ✅ Settle deadzone: 떨림 제거의 핵심
        // =========================
        if (enableSettle)
        {
            float posEpsSqr = settlePosEps * settlePosEps;
            float velEpsSqr = settleVelEps * settleVelEps;

            if (posError.sqrMagnitude <= posEpsSqr && velError.sqrMagnitude <= velEpsSqr)
            {
                // 충돌 안전장치: 아주 조금 움직여도 벽에 박힐 수 있으면 스냅 금지
                if (safeSweepBeforeSettle && posError.sqrMagnitude > 1e-12f)
                {
                    Vector3 dir = posError.normalized;
                    float dist = Mathf.Sqrt(posError.sqrMagnitude);
                    if (rb.SweepTest(dir, out _, dist, QueryTriggerInteraction.Ignore))
                    {
                        // 스냅 못하면 아래 PD로 처리
                    }
                    else
                    {
                        SettleNow(rb, posError, targetRot, targetVel, targetAngVel);
                        return;
                    }
                }
                else
                {
                    SettleNow(rb, posError, targetRot, targetVel, targetAngVel);
                    return;
                }
            }
        }

        // =========================
        // Position: critically-damped PD in acceleration form
        // a = w^2 * posError + 2w * velError
        // =========================
        float tPos = Mathf.Max(0.001f, positionCatchTime);

        // ✅ 4/t는 상황에 따라 과격해서 미세 떨림이 남을 수 있음.
        // 좀 더 안정적인 3/t로 낮춤(애매하면 애매할수도있는데, 이게 대부분 더 안정적임)
        float wPos = 3f / tPos;

        Vector3 accel = (wPos * wPos) * posError + (2f * wPos) * velError;
        accel = Vector3.ClampMagnitude(accel, maxAccel);

        Vector3 nextVel = curVel + accel * dt;
        nextVel = Vector3.ClampMagnitude(nextVel, maxSpeed);
        rb.linearVelocity = nextVel;

        // =========================
        // Rotation: critically-damped PD in angular accel form
        // =========================
        if (!driveRotation) return;

        Vector3 rotErrorRad = CalcRotationErrorAxisAngle(rb.rotation, targetRot);
        Vector3 curAngVel = rb.angularVelocity;
        Vector3 angVelError = targetAngVel - curAngVel;

        float tRot = Mathf.Max(0.001f, rotationCatchTime);
        float wRot = 3f / tRot;

        Vector3 angAccel = (wRot * wRot) * rotErrorRad + (2f * wRot) * angVelError;
        angAccel = Vector3.ClampMagnitude(angAccel, maxAngularAccel);

        Vector3 nextAngVel = curAngVel + angAccel * dt;
        nextAngVel = Vector3.ClampMagnitude(nextAngVel, maxAngularSpeed);
        rb.angularVelocity = nextAngVel;
    }

    private void SettleNow(Rigidbody rb, Vector3 posError, Quaternion targetRot, Vector3 targetVel, Vector3 targetAngVel)
    {
        // ✅ COM이 targetPos에 붙도록 rb.position을 같은 델타만큼 이동
        Vector3 newPos = rb.position + posError;

        if (useMoveWhenSettle)
            rb.MovePosition(newPos);
        else
            rb.position = newPos;

        // 속도는 타겟 속도로 “정착”
        rb.linearVelocity = targetVel;

        if (!driveRotation) return;

        // 회전도 정착: 각속도는 타겟AngVel or 0
        if (useMoveWhenSettle)
            rb.MoveRotation(targetRot);
        else
            rb.rotation = targetRot;

        rb.angularVelocity = targetAngVel; // 필요 없으면 Vector3.zero로 바꿔도 됨
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
