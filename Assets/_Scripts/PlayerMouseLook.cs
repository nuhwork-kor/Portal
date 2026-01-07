using UnityEngine;

public class PlayerMouseLook : MonoBehaviour
{
    public Transform playerBody;
    public Transform cameraRoot;

    [Header("Setting")]
    public float mouseSensitivity = 0.15f;
    public float minPitch = -90f;
    public float maxPitch = 90f;

    float pitch;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 look = InputManager.Look;

        float mouseX = look.x * mouseSensitivity;
        float mouseY = look.y * mouseSensitivity;

        //Yaw(좌우)
        playerBody.Rotate(Vector3.up * mouseX);

        //Pitch(상하)
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public void SyncPitchFromCamera()
    {
        pitch = cameraRoot.localEulerAngles.x;
        if (pitch > 180) pitch -= 360f;
    }

    public void ForceSetYaw(Quaternion worldYawRot)
    {
        //body yaw 세팅
        playerBody.rotation = worldYawRot;

        //pitch는 그대로 유지
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
