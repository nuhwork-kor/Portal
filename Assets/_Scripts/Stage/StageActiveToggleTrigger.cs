using UnityEngine;

// 이 스크립트는 특정 Trigger에 플레이어가 들어오면
// StageManager를 통해 특정 스테이지(GameObject)를 Active/Inactive로 토글해주는 트리거다.
// (예: 위 트리거 = 다음 스테이지 활성화 / 아래 트리거 = 이전 스테이지 비활성화 등)
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class StageActiveToggleTrigger : MonoBehaviour
{
    public enum ToggleType { SetActiveTrue, SetActiveFalse } // 토글 타입(Active true / Active false)

    [Header("Target")]
    [SerializeField] private StageManager stageManager; // 스테이지 활성/비활성 관리를 수행하는 StageManager

    [Tooltip("토글할 스테이지 인덱스 (예: 위 트리거=다음 스테이지, 아래 트리거=클리어 스테이지)")]
    [SerializeField] private int stageIndex = 0; // Active를 변경할 대상 스테이지 인덱스

    [SerializeField] private ToggleType toggle = ToggleType.SetActiveTrue; // 이 트리거가 True/False 중 무엇을 수행할지

    [Header("Current Stage Update (Optional)")]
    [Tooltip("트리거 시 CurrentStageIndex를 같이 갱신. 필요 없으면 -1.")]
    [SerializeField] private int setCurrentStageIndex = -1; // CurrentStageIndex도 같이 바꿀지(미사용은 -1)

    [Header("Player Filter")]
    [Tooltip("플레이어 레이어. 비어있으면 'Player' 레이어 자동 시도")]
    [SerializeField] private LayerMask playerMask; // 플레이어 판별용 레이어 마스크

    [Header("One Shot")]
    [SerializeField] private bool oneShot = true; // 한 번만 발동할지 여부

    private bool _fired; // oneShot에서 이미 발동했는지 여부

    /// <summary>
    /// stageManager 자동 할당, 트리거 콜라이더 강제 설정, playerMask 자동 설정을 수행한다.
    /// </summary>
    private void Awake()
    {
        if (!stageManager) stageManager = StageManager.Instance;               // 인스펙터 미할당이면 싱글톤 참조 시도

        var col = GetComponent<Collider>();                                    // 현재 오브젝트의 Collider 가져오기
        col.isTrigger = true;                                                  // 트리거로 강제 설정

        if (playerMask.value == 0)                                             // 플레이어 마스크가 비어있으면
        {
            int layer = LayerMask.NameToLayer("Player");                       // 'Player' 레이어 탐색
            if (layer >= 0) playerMask = (1 << layer);                         // 존재하면 마스크로 세팅
        }
    }

    /// <summary>
    /// 플레이어가 트리거에 들어오면 stageIndex 스테이지를 toggle 규칙에 따라 활성/비활성 처리한다.
    /// 옵션으로 CurrentStageIndex도 갱신하고, oneShot이면 자기 콜라이더를 꺼서 재발동을 막는다.
    /// </summary>
    /// <param name="other">트리거에 들어온 Collider</param>
    private void OnTriggerEnter(Collider other)
    {
        if (_fired && oneShot) return;                                         // oneShot인데 이미 발동했으면 종료
        if (!other) return;                                                    // collider가 없으면 종료

        if ((playerMask.value & (1 << other.gameObject.layer)) == 0) return;   // 플레이어 레이어가 아니면 종료
        if (!stageManager) stageManager = StageManager.Instance;               // stageManager가 null이면 다시 참조 시도
        if (!stageManager) return;                                             // 그래도 없으면 종료

        bool active = (toggle == ToggleType.SetActiveTrue);                    // toggle 타입에 따라 목표 active 결정
        stageManager.SetStageActive(stageIndex, active);                       // 해당 스테이지를 active on/off

        if (setCurrentStageIndex >= 0)                                         // CurrentStageIndex 갱신 옵션이 있으면
            stageManager.SetCurrentStage(setCurrentStageIndex);                // 현재 스테이지 인덱스를 강제로 세팅

        if (oneShot)                                                          // oneShot이면
        {
            _fired = true;                                                     // 발동 플래그 세팅
            GetComponent<Collider>().enabled = false;                          // 트리거 콜라이더 비활성화로 재발동 방지
        }
    }
}
