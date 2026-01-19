using UnityEngine;

// 이 스크립트는 "여러 개의 압력 버튼(PressurePlateTrigger)이 모두 눌렸을 때 문(SlidingDoor)을 여는" 그룹 컨트롤러다.
// - requiredButtons 배열의 모든 버튼이 눌려야 door.Open() 호출
// - stayOpenOnceUnlocked가 true면 한 번 열리면 이후 다시 닫지 않는다.
[DisallowMultipleComponent]
public class DoorButtonGroup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SlidingDoor door;                                         // 제어할 슬라이딩 도어

    [Header("Required Buttons (1~N)")]
    [SerializeField] private PressurePlateTrigger[] requiredButtons;                   // 모두 눌려야 하는 버튼 목록

    [Header("Behavior")]
    [Tooltip("한 번 열리면 다시 안 닫힘")]
    [SerializeField] private bool stayOpenOnceUnlocked = false;                        // true면 해금 후 Close를 하지 않음

    private bool unlocked;                                                             // 한 번 해금(Open) 상태가 되었는지 여부

    /// <summary>
    /// 유니티 생명주기: 참조 자동 확보를 수행한다.
    /// </summary>
    private void Awake()
    {
        if (!door) door = GetComponent<SlidingDoor>();                                 // 같은 오브젝트에 SlidingDoor가 있으면 자동 연결
    }

    /// <summary>
    /// 유니티 생명주기: 활성화 시 버튼 이벤트를 구독하고 현재 상태를 평가한다.
    /// </summary>
    private void OnEnable()
    {
        Bind(true);                                                                    // 버튼 PressedChanged 이벤트 구독
        Evaluate();                                                                    // 현재 버튼 상태로 문 상태 반영
    }

    /// <summary>
    /// 유니티 생명주기: 비활성화 시 버튼 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        Bind(false);                                                                   // 버튼 PressedChanged 이벤트 해제
    }

    /// <summary>
    /// requiredButtons의 PressedChanged 이벤트를 구독/해제한다.
    /// </summary>
    /// <param name="on">true면 구독, false면 해제</param>
    private void Bind(bool on)
    {
        if (requiredButtons == null) return;                                           // 버튼 목록이 없으면 종료

        for (int i = 0; i < requiredButtons.Length; i++)                               // 버튼 배열 순회
        {
            var b = requiredButtons[i];                                                // i번째 버튼
            if (!b) continue;                                                          // null이면 스킵

            if (on) b.PressedChanged += OnButtonChanged;                               // 이벤트 구독
            else b.PressedChanged -= OnButtonChanged;                                  // 이벤트 해제
        }
    }

    /// <summary>
    /// 버튼 상태 변경 이벤트 콜백.
    /// - 어떤 버튼이 바뀌었든 전체 상태를 다시 평가한다.
    /// </summary>
    /// <param name="_">이벤트를 보낸 PressurePlateTrigger(사용 안 함)</param>
    /// <param name="__">눌림 여부(사용 안 함)</param>
    private void OnButtonChanged(PressurePlateTrigger _, bool __)
    {
        Evaluate();                                                                    // 전체 버튼 상태 재평가
    }

    /// <summary>
    /// 모든 requiredButtons가 눌렸는지 검사하고 문을 열거나 닫는다.
    /// </summary>
    private void Evaluate()
    {
        if (!door) return;                                                             // 문 참조가 없으면 종료

        if (stayOpenOnceUnlocked && unlocked)                                          // 한 번 해금되면 유지 옵션 + 이미 해금이면
            return;                                                                    // 더 이상 평가/닫기 금지

        bool allPressed = true;                                                        // 전부 눌렸다고 가정하고 검사 시작

        if (requiredButtons == null || requiredButtons.Length == 0)                    // 버튼 목록이 비어있으면
            allPressed = false;                                                        // 조건을 만족할 수 없으니 false

        for (int i = 0; i < requiredButtons.Length; i++)                               // 버튼 순회
        {
            var b = requiredButtons[i];                                                // i번째 버튼
            if (!b || !b.IsPressed)                                                    // 버튼이 없거나 눌리지 않았으면
            {
                allPressed = false;                                                    // 전체 조건 실패
                break;                                                                 // 더 볼 필요 없음
            }
        }

        if (allPressed)                                                                // 모두 눌렸다면
        {
            door.Open();                                                               // 문 열기
            if (stayOpenOnceUnlocked) unlocked = true;                                 // 유지 옵션이면 해금 상태로 기록
        }
        else                                                                           // 모두 눌린 상태가 아니라면
        {
            if (!stayOpenOnceUnlocked)                                                 // 유지 옵션이 아니라면
                door.Close();                                                          // 문 닫기
        }
    }
}
