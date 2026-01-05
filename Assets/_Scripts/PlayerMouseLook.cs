using UnityEngine;

public class PlayerMouseLook : MonoBehaviour
{
    public Transform playerBody;
    public Transform cameraRoot;

    [Header("Setting")]
    public float mouseSensitivity = 700f;
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
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        //Y축 좌우 회전 Player
        playerBody.Rotate(Vector3.up * mouseX);

        //X축 상하 회전 CameraRoot
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraRoot.localRotation = Quaternion.Euler(pitch, 0, 0);
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
