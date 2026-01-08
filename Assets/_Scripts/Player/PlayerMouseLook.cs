using UnityEngine;

public class PlayerMouseLook : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform playerBody;          //yaw
    [SerializeField] Transform cameraRoot;          //pitch

    [Header("Settings")]
    [SerializeField] float mouseSensitivity = 0.15f;
    [SerializeField] float minPitch = -90f;
    [SerializeField] float maxPitch = 90f;

    Vector2 lookInput;
    float pitch;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetLookInput(Vector2 input)
    {
        lookInput = input;
    }

    private void Update()
    {
        if (!playerBody || !cameraRoot) return;

        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        //Yaw
        playerBody.Rotate(Vector3.up * mouseX, Space.World);

        //Pitch
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }


    /// <summary>
    /// 텔레포트 후 카메라 각도 튐 방지용
    /// </summary>
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
