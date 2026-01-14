using UnityEngine;

public class PlayerCameraRig : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraRoot;     // pitch pivot (CameraRoot)
    [SerializeField] private CapsuleCollider capsule;  // optional auto calc

    [Header("Eye Height")]
    [Tooltip("Capsule ���� �ڵ� ����� ���� ����")]
    [SerializeField] private bool autoFromCapsule = true;

    [Tooltip("Capsule bottom~top ���̿��� ���� ��ġ�� ���� (Height=2, Center=0 ���� 0.8�̸� localY=0.6��)")]
    [Range(0.5f, 0.95f)]
    [SerializeField] private float eyeHeightRatio = 0.8f;

    [Tooltip("���� ������(local Y). autoFromCapsule=false�� �� ���")]
    [SerializeField] private float eyeLocalY = 0.6f;

    [Tooltip("�ʿ��ϸ� ��/�� ����(��: -0.05)")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    private void Reset()
    {
        cameraRoot = transform.Find("CameraRoot");
        capsule = GetComponent<CapsuleCollider>();
        Apply();
    }

    private void Awake()
    {
        if (!cameraRoot) cameraRoot = transform.Find("CameraRoot");
        if (!capsule) capsule = GetComponent<CapsuleCollider>();
        Apply();
    }

    // �ִϸ��̼�/IK ���� �� ������ LateUpdate�� �� ������
    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (!cameraRoot) return;

        float y = eyeLocalY;

        if (autoFromCapsule && capsule)
        {
            // Capsule local ���� bottom/top ���
            float bottom = capsule.center.y - (capsule.height * 0.5f);
            float top = capsule.center.y + (capsule.height * 0.5f);
            y = Mathf.Lerp(bottom, top, eyeHeightRatio);
        }

        var p = cameraRoot.localPosition;
        p.x = localOffset.x;
        p.y = y + localOffset.y;
        p.z = localOffset.z;
        cameraRoot.localPosition = p;
    }

    // �� �ٲ�� ����� ���� ����
    public void SetEyeLocalY(float newEyeLocalY)
    {
        autoFromCapsule = false;
        eyeLocalY = newEyeLocalY;
    }

    public void SetEyeHeightRatio(float ratio)
    {
        autoFromCapsule = true;
        eyeHeightRatio = Mathf.Clamp01(ratio);
    }
}
