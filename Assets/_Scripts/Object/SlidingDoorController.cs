using System.Collections;
using UnityEngine;

// 이 스크립트는 "양쪽 패널이 좌/우로 슬라이딩하는 문"을 제어한다.
// - Open(): leftPanel은 -X 방향, rightPanel은 +X 방향으로 slideDistance만큼 이동
// - Close(): 초기 위치로 복귀
// - MoveRoutine 코루틴으로 부드럽게 보간 이동
// - 문 열고 닫을 때 Door_Interact SFX를 문 위치에서 3D로 재생한다.
[DisallowMultipleComponent]
public class SlidingDoor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform leftPanel;                                      // 왼쪽 문 패널 트랜스폼(로컬 이동)
    [SerializeField] private Transform rightPanel;                                     // 오른쪽 문 패널 트랜스폼(로컬 이동)

    [Header("Motion")]
    [SerializeField] private float slideDistance = 2.6f;                               // 패널이 움직일 거리(절대값 사용)
    [SerializeField] private float moveDuration = 0.6f;                                // 열고/닫는 이동 시간(초)
    [SerializeField] private bool smoothStep = true;                                   // true면 SmoothStep 보간

    private Vector3 leftClosed;                                                        // 왼쪽 패널 닫힘 로컬 위치
    private Vector3 rightClosed;                                                       // 오른쪽 패널 닫힘 로컬 위치
    private Vector3 leftOpen;                                                          // 왼쪽 패널 열림 로컬 위치
    private Vector3 rightOpen;                                                         // 오른쪽 패널 열림 로컬 위치

    private Coroutine co;                                                              // 이동 코루틴 핸들
    public bool IsOpen { get; private set; }                                           // 현재 문 열림 여부

    /// <summary>
    /// 유니티 생명주기: 패널 참조 검증 및 열림/닫힘 목표 위치를 계산한다.
    /// </summary>
    private void Awake()
    {
        if (!leftPanel || !rightPanel)                                                 // 패널이 누락되면
        {
            Debug.LogError("[SlidingDoor] leftPanel/rightPanel 누락", this);           // 에러 로그
            enabled = false;                                                           // 스크립트 비활성화
            return;                                                                    // 종료
        }

        leftClosed = leftPanel.localPosition;                                          // 닫힘 위치 캐시
        rightClosed = rightPanel.localPosition;                                        // 닫힘 위치 캐시

        leftOpen = leftClosed + Vector3.left * Mathf.Abs(slideDistance);               // 왼쪽은 -X로 이동
        rightOpen = rightClosed + Vector3.right * Mathf.Abs(slideDistance);            // 오른쪽은 +X로 이동
    }

    /// <summary>
    /// 문을 연다. 이미 열려있으면 무시한다.
    /// </summary>
    public void Open()
    {
        if (IsOpen) return;                                                            // 이미 열려있으면 종료
        IsOpen = true;                                                                 // 상태 갱신

        // ✅ SFX: 문 인터랙트(3D로 문 위치에서)
        SoundManager.PlaySFX(SfxId.Door_Interact, worldPos: transform.position);       // 문 사운드 재생(3D)

        StartMove(leftOpen, rightOpen);                                                // 열림 목표로 이동 시작
    }

    /// <summary>
    /// 문을 닫는다. 이미 닫혀있으면 무시한다.
    /// </summary>
    public void Close()
    {
        if (!IsOpen) return;                                                           // 이미 닫혀있으면 종료
        IsOpen = false;                                                                // 상태 갱신

        // ✅ SFX: 문 인터랙트
        SoundManager.PlaySFX(SfxId.Door_Interact, worldPos: transform.position);       // 문 사운드 재생(3D)

        StartMove(leftClosed, rightClosed);                                            // 닫힘 목표로 이동 시작
    }

    /// <summary>
    /// 이동 코루틴을 시작한다(중복 실행 방지).
    /// </summary>
    /// <param name="leftTarget">왼쪽 패널 목표 로컬 위치</param>
    /// <param name="rightTarget">오른쪽 패널 목표 로컬 위치</param>
    private void StartMove(Vector3 leftTarget, Vector3 rightTarget)
    {
        if (co != null) StopCoroutine(co);                                             // 기존 코루틴 중지
        co = StartCoroutine(MoveRoutine(leftTarget, rightTarget));                     // 새 코루틴 시작
    }

    /// <summary>
    /// 두 패널을 동시에 보간 이동시키는 코루틴.
    /// </summary>
    /// <param name="leftTarget">왼쪽 패널 목표 로컬 위치</param>
    /// <param name="rightTarget">오른쪽 패널 목표 로컬 위치</param>
    /// <returns>IEnumerator(코루틴)</returns>
    private IEnumerator MoveRoutine(Vector3 leftTarget, Vector3 rightTarget)
    {
        Vector3 l0 = leftPanel.localPosition;                                          // 왼쪽 시작 위치
        Vector3 r0 = rightPanel.localPosition;                                         // 오른쪽 시작 위치

        float dur = Mathf.Max(0.0001f, moveDuration);                                  // 0 방지 보정
        float t = 0f;                                                                  // 정규화 시간(0~1)

        while (t < 1f)                                                                 // 1까지 진행
        {
            t += Time.deltaTime / dur;                                                 // 진행도 증가
            float k = Mathf.Clamp01(t);                                                // 0~1 클램프
            if (smoothStep) k = k * k * (3f - 2f * k);                                 // SmoothStep 보정

            leftPanel.localPosition = Vector3.LerpUnclamped(l0, leftTarget, k);        // 왼쪽 보간
            rightPanel.localPosition = Vector3.LerpUnclamped(r0, rightTarget, k);      // 오른쪽 보간
            yield return null;                                                         // 다음 프레임 대기
        }

        leftPanel.localPosition = leftTarget;                                          // 최종 위치 고정
        rightPanel.localPosition = rightTarget;                                        // 최종 위치 고정
        co = null;                                                                     // 코루틴 핸들 해제
    }
}
