using UnityEngine;

public partial class PlayerController
{
    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 90f;

    [Header("View Ref (READ ONLY)")]
    [Tooltip("실제 PlayerCamera Transform을 넣어라. (카메라를 '제어'하지 않고, 회전만 '읽기' 용도)")]
    [SerializeField] private Transform viewTransform; // ✅ 읽기 전용

    private float pitch;

    // 마지막 안정 yaw forward (월드 up 기준) - 특이점 fallback용
    private Vector3 lastStableYawForward = Vector3.forward;

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

        // 안정 yaw 갱신(특이점 fallback)
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

    // ✅ 워프 계산에 쓸 "현재 시야 회전"(읽기만)
    public Quaternion GetViewWorldRotation()
    {
        if (viewTransform) return viewTransform.rotation; // ✅ 강추(실제 PlayerCamera)
        if (cameraRoot) return cameraRoot.rotation;
        if (playerBody) return playerBody.rotation;
        return transform.rotation;
    }

    // ✅ 특이점 fallback으로 쓸 "안정 yaw forward"
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

    /// <summary>
    /// ✅ 포탈 워프 후 Portal1처럼 시야가 "딸려가게" 만들기
    /// - 카메라(PlayerCamera) 트랜스폼 직접 제어 X
    /// - playerBody(yaw) + cameraRoot(pitch)만 세팅
    /// - roll 제거(오뚜기) 포함
    /// </summary>
    public void ApplyViewAfterUpright(Quaternion viewWorldRot, Vector3 yawFallbackWorld)
    {
        if (!playerBody || !cameraRoot) return;

        Vector3 fwd = viewWorldRot * Vector3.forward;

        // 1) yaw = 월드 up 기준으로 roll 제거
        Vector3 yawFwd = Vector3.ProjectOnPlane(fwd, Vector3.up);

        // 수직 특이점이면 포탈 변환된 fallback 우선
        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.ProjectOnPlane(yawFallbackWorld, Vector3.up);

        // 그래도 안되면 마지막 안정값
        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.ProjectOnPlane(lastStableYawForward, Vector3.up);

        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.forward;

        yawFwd.Normalize();
        Quaternion yaw = Quaternion.LookRotation(yawFwd, Vector3.up);
        playerBody.rotation = yaw;

        // 2) pitch = yaw 제거한 로컬 forward에서 계산
        Vector3 localFwd = Quaternion.Inverse(yaw) * fwd;

        float xz = Mathf.Sqrt(localFwd.x * localFwd.x + localFwd.z * localFwd.z);
        // ✅ Unity 관례: pitch 음수 = 위 보기, pitch 양수 = 아래 보기
        float newPitch = Mathf.Atan2(-localFwd.y, xz) * Mathf.Rad2Deg;

        pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // 3) 안정 yaw 갱신
        lastStableYawForward = yawFwd;
    }

    /// <summary>
    /// (옵션) 시야 안 건드리고 바디만 세우기(롤 제거)
    /// </summary>
    public void ForceUprightOnly()
    {
        if (!playerBody) return;

        Vector3 yawFwd = Vector3.ProjectOnPlane(playerBody.forward, Vector3.up);

        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.ProjectOnPlane(lastStableYawForward, Vector3.up);

        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.forward;

        yawFwd.Normalize();
        playerBody.rotation = Quaternion.LookRotation(yawFwd, Vector3.up);

        lastStableYawForward = yawFwd;
    }
}
