using UnityEngine;

public partial class PlayerController
{
    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 90f;

    private float pitch;

    private void InitLook()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // pitch 초기값 동기화(에디터에서 시작 각도 있을 수 있음)
        if (cameraRoot)
        {
            pitch = cameraRoot.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
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
    }

    // 포탈 텔레포트 후 카메라 튐 방지용
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
    }
}
