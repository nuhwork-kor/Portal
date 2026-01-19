// - 플레이어 시야(마우스 룩)를 담당하는 PlayerController의 Partial.
// - playerBody(yaw) + cameraRoot(pitch)로 시야를 구성하며, 포탈 워프 후 시야 보정 함수도 제공한다.
using UnityEngine;                                                  // Quaternion, Vector3, Mathf 등

public partial class PlayerController
{
    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.15f;        // 마우스 감도(회전 스케일)
    [SerializeField] private float minPitch = -90f;                 // 위로 보기 한계(도)
    [SerializeField] private float maxPitch = 90f;                  // 아래로 보기 한계(도)

    [Header("View Ref (READ ONLY)")]
    [Tooltip("실제 PlayerCamera Transform을 넣어라. (카메라를 '제어'하지 않고, 회전만 '읽기' 용도)")]
    [SerializeField] private Transform viewTransform;               // ✅ 실제 카메라 트랜스폼(읽기 전용)

    private float pitch;                                            // 현재 pitch(도) 상태값

    private Vector3 lastStableYawForward = Vector3.forward;         // 마지막으로 안정적이었던 yaw forward(특이점 fallback)

    /// <summary>
    /// MouseLook 파트 초기화.
    /// 커서 잠금/숨김을 설정하고, 초기 pitch 및 안정 yaw forward를 캐시한다.
    /// </summary>
    private void InitLook()
    {
        Cursor.lockState = CursorLockMode.Locked;                   // 마우스 커서 잠금
        Cursor.visible = false;                                     // 마우스 커서 숨김

        if (cameraRoot)
        {
            pitch = cameraRoot.localEulerAngles.x;                  // cameraRoot의 로컬 x에서 초기 pitch 읽기
            if (pitch > 180f) pitch -= 360f;                        // 0~360을 -180~180으로 변환
        }

        if (playerBody)
        {
            Vector3 f = Vector3.ProjectOnPlane(playerBody.forward, Vector3.up); // 월드 up 기준 yaw forward(roll 제거)
            if (f.sqrMagnitude > 1e-6f) lastStableYawForward = f.normalized;    // 안정값 저장
        }
    }

    /// <summary>
    /// 시야 입력(Vector2)을 받아 yaw(playerBody)와 pitch(cameraRoot)를 갱신한다.
    /// </summary>
    /// <param name="look">Look 입력(마우스 델타/스틱)</param>
    private void TickLook(Vector2 look)
    {
        if (!playerBody || !cameraRoot) return;                     // 참조 누락이면 처리 불가

        float mouseX = look.x * mouseSensitivity;                   // yaw 입력 스케일
        float mouseY = look.y * mouseSensitivity;                   // pitch 입력 스케일

        playerBody.Rotate(Vector3.up * mouseX, Space.World);        // 월드 up 기준으로 yaw 회전

        pitch -= mouseY;                                            // Unity 관례: 위로 보면 pitch 감소
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);             // pitch 제한
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);  // cameraRoot pitch 적용

        Vector3 yawFwd = Vector3.ProjectOnPlane(playerBody.forward, Vector3.up); // roll 제거한 yaw forward
        if (yawFwd.sqrMagnitude > 1e-6f)
            lastStableYawForward = yawFwd.normalized;               // 안정 yaw 업데이트
    }

    /// <summary>
    /// cameraRoot의 현재 로컬 회전값에서 pitch 상태를 다시 동기화한다.
    /// (예: 외부에서 cameraRoot를 강제로 세팅했을 때 상태값 복구용)
    /// </summary>
    public void SyncPitchFromCamera()
    {
        if (!cameraRoot) return;                                    // cameraRoot 없으면 불가
        pitch = cameraRoot.localEulerAngles.x;                      // 현재 로컬 x 읽기
        if (pitch > 180f) pitch -= 360f;                            // -180~180로 변환
    }

    /// <summary>
    /// playerBody의 yaw를 외부에서 강제로 세팅한다.
    /// pitch는 현재 pitch 상태값을 유지하며 cameraRoot에 다시 적용한다.
    /// </summary>
    /// <param name="worldYawRot">월드 기준 yaw 회전(roll/pitch는 보통 제거된 값 권장)</param>
    public void ForceSetYaw(Quaternion worldYawRot)
    {
        if (!playerBody || !cameraRoot) return;                     // 참조 누락이면 불가

        playerBody.rotation = worldYawRot;                          // yaw 적용
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);  // pitch 재적용

        Vector3 yawFwd = Vector3.ProjectOnPlane(playerBody.forward, Vector3.up); // 안정 yaw 갱신
        if (yawFwd.sqrMagnitude > 1e-6f)
            lastStableYawForward = yawFwd.normalized;               // 안정 yaw 저장
    }

    /// <summary>
    /// 워프 계산 등에 사용할 "현재 시야 월드 회전"을 반환한다.
    /// viewTransform이 있으면 그것(실제 PlayerCamera)을 우선 사용하고, 없으면 cameraRoot/playerBody 순서로 fallback한다.
    /// </summary>
    /// <returns>현재 시야 월드 회전(Quaternion)</returns>
    public Quaternion GetViewWorldRotation()
    {
        if (viewTransform) return viewTransform.rotation;           // ✅ 실제 카메라 회전(권장)
        if (cameraRoot) return cameraRoot.rotation;                 // fallback: cameraRoot 월드 회전
        if (playerBody) return playerBody.rotation;                 // fallback: playerBody 월드 회전
        return transform.rotation;                                  // 최후 fallback
    }

    /// <summary>
    /// 특이점(거의 수직) 상황에서 yaw 계산이 불가능할 때 사용할 "안정 yaw forward"를 반환한다.
    /// 값이 비정상일 경우 playerBody 기반으로 재계산한다.
    /// </summary>
    /// <returns>월드 up 기준 안정 yaw forward(Vector3)</returns>
    public Vector3 GetStableYawForward()
    {
        if (lastStableYawForward.sqrMagnitude < 1e-6f)
        {
            Vector3 f = playerBody ? Vector3.ProjectOnPlane(playerBody.forward, Vector3.up) : Vector3.forward; // yaw forward 재계산
            if (f.sqrMagnitude > 1e-6f) lastStableYawForward = f.normalized;                                     // 안정값 갱신
            else lastStableYawForward = Vector3.forward;                                                         // 최후 fallback
        }
        return lastStableYawForward;                                // 안정 yaw 반환
    }

    /// <summary>
    /// 포탈 워프 후 Portal1처럼 시야가 "딸려가게" 만들기.
    /// - 카메라(PlayerCamera) 트랜스폼을 직접 제어하지 않고,
    ///   playerBody(yaw) + cameraRoot(pitch)만으로 시야를 재구성한다.
    /// - roll 제거(오뚜기) 포함.
    /// </summary>
    /// <param name="viewWorldRot">워프 후 목표 시야 월드 회전</param>
    /// <param name="yawFallbackWorld">수직 특이점일 때 사용할 yaw fallback 방향(월드)</param>
    public void ApplyViewAfterUpright(Quaternion viewWorldRot, Vector3 yawFallbackWorld)
    {
        if (!playerBody || !cameraRoot) return;                      // 참조 누락이면 처리 불가

        Vector3 fwd = viewWorldRot * Vector3.forward;                // 목표 시야 forward(월드)

        Vector3 yawFwd = Vector3.ProjectOnPlane(fwd, Vector3.up);    // roll 제거한 yaw forward(월드)

        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.ProjectOnPlane(yawFallbackWorld, Vector3.up); // 수직 특이점이면 fallback 사용

        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.ProjectOnPlane(lastStableYawForward, Vector3.up); // 그래도 안되면 마지막 안정값

        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.forward;                                // 최후 fallback

        yawFwd.Normalize();                                          // yaw forward 정규화
        Quaternion yaw = Quaternion.LookRotation(yawFwd, Vector3.up); // yaw 회전 생성
        playerBody.rotation = yaw;                                   // playerBody yaw 적용

        Vector3 localFwd = Quaternion.Inverse(yaw) * fwd;            // yaw를 제거한 로컬 forward(= pitch 계산용)

        float xz = Mathf.Sqrt(localFwd.x * localFwd.x + localFwd.z * localFwd.z);   // 로컬 xz 평면 길이
        float newPitch = Mathf.Atan2(-localFwd.y, xz) * Mathf.Rad2Deg;              // pitch 계산(위보기 음수 관례)

        pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);           // pitch 제한
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);   // cameraRoot pitch 적용

        lastStableYawForward = yawFwd;                               // 안정 yaw 갱신
    }

    /// <summary>
    /// 시야는 건드리지 않고 바디만 세운다(roll 제거).
    /// </summary>
    public void ForceUprightOnly()
    {
        if (!playerBody) return;                                     // playerBody 없으면 불가

        Vector3 yawFwd = Vector3.ProjectOnPlane(playerBody.forward, Vector3.up); // roll 제거 yaw forward

        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.ProjectOnPlane(lastStableYawForward, Vector3.up);  // 특이점이면 안정값 사용

        if (yawFwd.sqrMagnitude < 1e-6f)
            yawFwd = Vector3.forward;                                 // 최후 fallback

        yawFwd.Normalize();                                           // 정규화
        playerBody.rotation = Quaternion.LookRotation(yawFwd, Vector3.up); // yaw만으로 upright

        lastStableYawForward = yawFwd;                                // 안정 yaw 갱신
    }
}
