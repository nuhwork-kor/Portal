// - 플레이어의 "눈 높이"를 관리하는 리그.
// - CapsuleCollider 높이를 기준으로 CameraRoot(localPosition.y)를 자동 계산하거나, 수동 eyeLocalY를 사용한다.
using UnityEngine;                                      // MonoBehaviour, Transform, CapsuleCollider 등

public class PlayerCameraRig : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraRoot;        // pitch pivot (CameraRoot)                         // 카메라 피치 회전 축(자식 트랜스폼)
    [SerializeField] private CapsuleCollider capsule;     // optional auto calc                              // 캡슐 기준 자동 눈높이 계산에 사용(옵션)

    [Header("Eye Height")]
    [Tooltip("Capsule 기준 자동 계산을 사용할지")]
    [SerializeField] private bool autoFromCapsule = true; // 캡슐 기반 자동 눈높이 사용 여부

    [Tooltip("Capsule bottom~top 사이에서 눈 위치 비율(0.5~0.95 권장)")]
    [Range(0.5f, 0.95f)]
    [SerializeField] private float eyeHeightRatio = 0.8f; // 캡슐 하단~상단 사이에서 눈 위치 비율

    [Tooltip("수동 눈높이(local Y). autoFromCapsule=false일 때 사용")]
    [SerializeField] private float eyeLocalY = 0.6f;      // 수동 눈높이(local Y)

    [Tooltip("필요하면 로컬 오프셋(XYZ). 예: 살짝 아래(-0.05)")]
    [SerializeField] private Vector3 localOffset = Vector3.zero; // 카메라 루트 로컬 오프셋

    private void Reset()
    {
        cameraRoot = transform.Find("CameraRoot");        // 기본: 자식 이름이 CameraRoot인 트랜스폼 찾기
        capsule = GetComponent<CapsuleCollider>();        // 같은 오브젝트의 캡슐 콜라이더 자동 연결
        Apply();                                          // 현재 설정으로 즉시 적용
    }

    private void Awake()
    {
        if (!cameraRoot) cameraRoot = transform.Find("CameraRoot"); // cameraRoot 미할당이면 자동 탐색
        if (!capsule) capsule = GetComponent<CapsuleCollider>();    // capsule 미할당이면 자동 탐색
        Apply();                                                    // 시작 시 적용
    }

    private void LateUpdate()
    {
        Apply();                                                    // 애니메이션/IK 이후 눈높이를 최종 반영
    }

    /// <summary>
    /// 현재 설정(autoFromCapsule/eyeLocalY/eyeHeightRatio/localOffset)에 따라 CameraRoot의 localPosition을 갱신한다.
    /// </summary>
    private void Apply()
    {
        if (!cameraRoot) return;                                    // cameraRoot 없으면 처리 불가

        float y = eyeLocalY;                                        // 기본은 수동 eyeLocalY 사용

        if (autoFromCapsule && capsule)
        {
            float bottom = capsule.center.y - (capsule.height * 0.5f); // 캡슐 로컬 하단 Y
            float top = capsule.center.y + (capsule.height * 0.5f);    // 캡슐 로컬 상단 Y
            y = Mathf.Lerp(bottom, top, eyeHeightRatio);               // 하단~상단 사이 눈 위치 보간
        }

        var p = cameraRoot.localPosition;                           // 기존 로컬 위치 읽기
        p.x = localOffset.x;                                        // X 오프셋 적용
        p.y = y + localOffset.y;                                    // 계산된 눈높이 + Y 오프셋
        p.z = localOffset.z;                                        // Z 오프셋 적용
        cameraRoot.localPosition = p;                               // CameraRoot 위치 반영
    }

    /// <summary>
    /// 수동 eyeLocalY를 설정한다.
    /// 자동 캡슐 계산(autoFromCapsule)을 끄고, 전달된 로컬 Y를 적용한다.
    /// </summary>
    /// <param name="newEyeLocalY">설정할 눈높이(local Y)</param>
    public void SetEyeLocalY(float newEyeLocalY)
    {
        autoFromCapsule = false;                                    // 수동 모드로 전환
        eyeLocalY = newEyeLocalY;                                   // 눈높이 갱신
    }

    /// <summary>
    /// 캡슐 기준 눈높이 비율을 설정한다.
    /// 자동 캡슐 계산(autoFromCapsule)을 켜고, ratio를 0~1로 클램프한다.
    /// </summary>
    /// <param name="ratio">캡슐 하단~상단 사이의 비율(0~1)</param>
    public void SetEyeHeightRatio(float ratio)
    {
        autoFromCapsule = true;                                     // 자동 모드로 전환
        eyeHeightRatio = Mathf.Clamp01(ratio);                      // 안전 클램프
    }
}
