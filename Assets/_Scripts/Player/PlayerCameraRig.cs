using UnityEngine;

public class PlayerCameraRig : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraRoot;     // pitch pivot (CameraRoot)
    [SerializeField] private CapsuleCollider capsule;  // optional auto calc

    [Header("Eye Height")]
    [Tooltip("Capsule 기준 자동 계산을 쓸지 여부")]
    [SerializeField] private bool autoFromCapsule = true;

    [Tooltip("Capsule bottom~top 사이에서 눈이 위치할 비율 (Height=2, Center=0 기준 0.8이면 localY=0.6쯤)")]
    [Range(0.5f, 0.95f)]
    [SerializeField] private float eyeHeightRatio = 0.8f;

    [Tooltip("수동 눈높이(local Y). autoFromCapsule=false일 때 사용")]
    [SerializeField] private float eyeLocalY = 0.6f;

    [Tooltip("필요하면 앞/뒤 보정(예: -0.05)")]
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

    // 애니메이션/IK 같은 거 붙으면 LateUpdate가 더 안전함
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
            // Capsule local 기준 bottom/top 계산
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

    // 모델 바뀌면 여기로 세팅 가능
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
