using UnityEngine;

public partial class PlayerController
{
    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 90f;

    private float pitch;

    // 마지막 안정 yaw forward (월드 up 기준)
    private Vector3 lastStableYawForward = Vector3.forward;

    // ✅ 추가: 내 리그에서 "시선 forward"가 +Z인지 -Z인지
    [Header("View Axis Fix")]
    [Tooltip("자동 판별이 틀리면 이걸 켜고 아래 값으로 강제하세요.")]
    [SerializeField] private bool overrideViewForwardAxis = false;

    [Tooltip("true면 viewWorldRot*Vector3.back을 시선 forward로 사용")]
    [SerializeField] private bool viewForwardIsBack = false;

    private bool autoViewForwardIsBack = false;

    private bool UseBackForward => overrideViewForwardAxis ? viewForwardIsBack : autoViewForwardIsBack;

    private void InitLook()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraRoot)
        {
            pitch = cameraRoot.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
        }

        if (playerBody)
        {
            Vector3 f = Vector3.ProjectOnPlane(playerBody.forward, Vector3.up);
            if (f.sqrMagnitude > 1e-6f) lastStableYawForward = f.normalized;
        }

        // ✅ 자동 판별: cameraRoot.forward가 playerBody.forward와 반대면
        // "시선은 -Z(=back)로 봐야 맞는" 리그일 가능성이 큼
        if (playerBody && cameraRoot)
        {
            autoViewForwardIsBack = Vector3.Dot(cameraRoot.forward, playerBody.forward) < 0f;
        }
        else
        {
            autoViewForwardIsBack = false;
        }
    }

    private void TickLook(Vector2 look)
    {
        if (!playerBody || !cameraRoot) return;

        float mouseX = look.x * mouseSensitivity;
        float mouseY = look.y * mouseSensitivity;

        // Yaw
        playerBody.Rotate(Vector3.up * mouseX, Space.World);

        // Pitch
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // 안정 yaw 갱신
        Vector3 yawFwd = Vector3.ProjectOnPlane(playerBody.forward, Vector3.up);
        if (yawFwd.sqrMagnitude > 1e-6f)
            lastStableYawForward = yawFwd.normalized;
    }

    public void SyncPitchFromCamera()
    {
        if (!cameraRoot) return;
        pitch = cameraRoot.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
    }

    public void ForceSetYaw(Quaternion worldYawRot)
    {
        if (!playerBody || !cameraRoot) return;

        playerBody.rotation = worldYawRot;
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 yawFwd = Vector3.ProjectOnPlane(playerBody.forward, Vector3.up);
        if (yawFwd.sqrMagnitude > 1e-6f)
            lastStableYawForward = yawFwd.normalized;
    }

    public Quaternion GetViewWorldRotation()
    {
        if (cameraRoot) return cameraRoot.rotation;
        if (playerBody) return playerBody.rotation;
        return transform.rotation;
    }

    public Vector3 GetStableYawForward()
    {
        if (lastStableYawForward.sqrMagnitude < 1e-6f)
        {
            Vector3 f = playerBody ? Vector3.ProjectOnPlane(playerBody.forward, Vector3.up) : Vector3.forward;
            if (f.sqrMagnitude > 1e-6f) lastStableYawForward = f.normalized;
            else lastStableYawForward = Vector3.forward;
        }
        return lastStableYawForward;
    }

    public void ForceSetViewUpright(Quaternion viewWorldRot)
    {
        ForceSetViewUpright(viewWorldRot, lastStableYawForward);
    }

    public void ForceSetViewUpright(Quaternion viewWorldRot, Vector3 yawFallbackWorld)
    {
        if (!playerBody || !cameraRoot) return;

        // ✅ 핵심: 어떤 리그는 "실제 시선"이 +Z가 아니라 -Z임
        Vector3 fwd = viewWorldRot * (UseBackForward ? Vector3.back : Vector3.forward);

        // 1) yaw: 월드 up 기준
        Vector3 yawFwd = Vector3.ProjectOnPlane(fwd, Vector3.up);

        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.ProjectOnPlane(yawFallbackWorld, Vector3.up);

        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = lastStableYawForward;

        yawFwd.Normalize();

        Quaternion yaw = Quaternion.LookRotation(yawFwd, Vector3.up);
        playerBody.rotation = yaw;

        // 2) pitch
        Vector3 localFwd = Quaternion.Inverse(yaw) * fwd;

        float xz = Mathf.Sqrt(localFwd.x * localFwd.x + localFwd.z * localFwd.z);
        float newPitch = Mathf.Atan2(-localFwd.y, xz) * Mathf.Rad2Deg;

        pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        lastStableYawForward = yawFwd;
    }
}
