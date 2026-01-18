using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class StageActiveToggleTrigger : MonoBehaviour
{
    public enum ToggleType { SetActiveTrue, SetActiveFalse }

    [Header("Target")]
    [SerializeField] private StageManager stageManager;

    [Tooltip("토글할 스테이지 인덱스 (예: 위 트리거=다음 스테이지, 아래 트리거=클리어 스테이지)")]
    [SerializeField] private int stageIndex = 0;

    [SerializeField] private ToggleType toggle = ToggleType.SetActiveTrue;

    [Header("Current Stage Update (Optional)")]
    [Tooltip("트리거 시 CurrentStageIndex를 이 값으로 갱신. 필요 없으면 -1.")]
    [SerializeField] private int setCurrentStageIndex = -1;

    [Header("Player Filter")]
    [Tooltip("플레이어 레이어. 비워두면 'Player' 레이어 자동 시도")]
    [SerializeField] private LayerMask playerMask;

    [Header("One Shot")]
    [SerializeField] private bool oneShot = true;

    private bool _fired;

    private void Awake()
    {
        if (!stageManager) stageManager = StageManager.Instance;

        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (playerMask.value == 0)
        {
            int layer = LayerMask.NameToLayer("Player");
            if (layer >= 0) playerMask = (1 << layer);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_fired && oneShot) return;
        if (!other) return;

        if ((playerMask.value & (1 << other.gameObject.layer)) == 0) return;
        if (!stageManager) stageManager = StageManager.Instance;
        if (!stageManager) return;

        bool active = (toggle == ToggleType.SetActiveTrue);
        stageManager.SetStageActive(stageIndex, active);

        if (setCurrentStageIndex >= 0)
            stageManager.SetCurrentStage(setCurrentStageIndex);

        if (oneShot)
        {
            _fired = true;
            // 재진입 방지: 콜라이더만 꺼도 충분
            GetComponent<Collider>().enabled = false;
        }
    }
}
