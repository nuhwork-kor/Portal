using UnityEngine;

// 이 스크립트는 "엘리베이터 트리거 콜라이더"에 붙어서,
// 플레이어가 트리거에 들어오면 ElevatorController의 API를 대신 호출(Forward)해주는 중계기 역할을 한다.
// (Enter 트리거 / Ride 트리거 2종류를 하나의 스크립트로 처리)
[DisallowMultipleComponent]
public class ElevatorTriggerForwarder : MonoBehaviour
{
    public enum TriggerType { Enter, Ride } // 트리거 타입(문 열기용 Enter / 탑승-이동용 Ride)

    [SerializeField] private ElevatorController elevator; // 호출 대상 엘리베이터 컨트롤러
    [SerializeField] private TriggerType type = TriggerType.Enter; // 이 트리거가 어떤 역할인지(Enter or Ride)

    [Tooltip("플레이어 레이어. 비어있으면 'Player' 레이어 자동 시도")]
    [SerializeField] private LayerMask playerMask; // 플레이어 판별용 레이어 마스크

    /// <summary>
    /// 레퍼런스를 자동으로 찾아 세팅하고, playerMask가 비어있으면 'Player' 레이어를 자동으로 할당한다.
    /// </summary>
    private void Awake()
    {
        if (!elevator) elevator = GetComponentInParent<ElevatorController>(); // elevator가 비어있으면 부모에서 ElevatorController 탐색

        if (playerMask.value == 0)                                            // playerMask가 비어있으면(0이면)
        {
            int layer = LayerMask.NameToLayer("Player");                      // 'Player' 레이어 인덱스 찾기
            if (layer >= 0) playerMask = (1 << layer);                        // 레이어가 존재하면 해당 비트로 마스크 구성
        }
    }

    /// <summary>
    /// 트리거에 무언가 들어오면 호출된다.
    /// playerMask에 해당하는 오브젝트만 통과시키고, type에 따라 엘리베이터 API를 호출한다.
    /// </summary>
    /// <param name="other">트리거에 들어온 Collider</param>
    private void OnTriggerEnter(Collider other)
    {
        if (!other) return;                                                   // collider가 없으면 종료
        if (!elevator) return;                                                // 엘리베이터 레퍼런스가 없으면 종료
        if ((playerMask.value & (1 << other.gameObject.layer)) == 0) return;  // 플레이어 레이어가 아니면 종료

        Transform root = ResolvePlayerRoot(other);                             // 플레이어의 "대표 루트 Transform"을 찾기

        if (type == TriggerType.Enter)                                         // Enter 트리거면
            elevator.OnEnterTrigger_PlayerEnter(root);                         // 문 열기 API 호출
        else                                                                   // Ride 트리거면
            elevator.OnRideTrigger_PlayerEnter(root);                          // 탑승/이동 API 호출
    }

    /// <summary>
    /// 트리거에 들어온 콜라이더를 기준으로 플레이어의 "루트 Transform"을 최대한 안정적으로 찾는다.
    /// 우선순위: PlayerController -> attachedRigidbody -> transform.root
    /// </summary>
    /// <param name="other">트리거에 들어온 Collider</param>
    /// <returns>플레이어 루트로 사용할 Transform</returns>
    private Transform ResolvePlayerRoot(Collider other)
    {
        var pc = other.GetComponentInParent<PlayerController>();               // 부모 계층에서 PlayerController 탐색
        if (pc) return pc.transform;                                           // 찾았으면 PlayerController Transform을 루트로 사용

        if (other.attachedRigidbody) return other.attachedRigidbody.transform; // Rigidbody가 붙어있으면 그 Transform을 루트로 사용

        return other.transform.root;                                           // 마지막 fallback: hierarchy 최상단 루트를 사용
    }
}
