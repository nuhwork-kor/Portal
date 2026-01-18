using UnityEngine;

[DisallowMultipleComponent]
public class ElevatorTriggerForwarder : MonoBehaviour
{
    public enum TriggerType { Enter, Ride }

    [SerializeField] private ElevatorController elevator;
    [SerializeField] private TriggerType type = TriggerType.Enter;

    [Tooltip("플레이어 레이어. 비워두면 'Player' 레이어 자동 시도")]
    [SerializeField] private LayerMask playerMask;

    private void Awake()
    {
        if (!elevator) elevator = GetComponentInParent<ElevatorController>();

        if (playerMask.value == 0)
        {
            int layer = LayerMask.NameToLayer("Player");
            if (layer >= 0) playerMask = (1 << layer);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other) return;
        if (!elevator) return;
        if ((playerMask.value & (1 << other.gameObject.layer)) == 0) return;

        Transform root = ResolvePlayerRoot(other);

        if (type == TriggerType.Enter)
            elevator.OnEnterTrigger_PlayerEnter(root);
        else
            elevator.OnRideTrigger_PlayerEnter(root);
    }

    private Transform ResolvePlayerRoot(Collider other)
    {
        // PlayerController가 있는 쪽을 우선으로 넘기면 "잘못된 루트 parenting" 문제 대부분이 해결됨
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc) return pc.transform;

        // attachedRigidbody가 있으면 그쪽이 보통 실제 캐릭터 바디
        if (other.attachedRigidbody) return other.attachedRigidbody.transform;

        return other.transform.root;
    }
}
