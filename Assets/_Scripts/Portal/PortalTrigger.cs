using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PortalTrigger : MonoBehaviour
{
    [SerializeField] private Portal portal;                                       // 이 트리거가 속한 포탈
    private Collider triggerCol;                                                  // 트리거 콜라이더 캐시

    /// <summary>
    /// Unity Reset: 에디터에서 붙일 때 기본 참조 및 isTrigger 설정을 자동으로 맞춘다.
    /// </summary>
    private void Reset()
    {
        portal = GetComponentInParent<Portal>();                                  // 부모에서 Portal 컴포넌트 자동 탐색
        triggerCol = GetComponent<Collider>();                                    // 콜라이더 캐싱
        if (triggerCol) triggerCol.isTrigger = true;                              // 트리거로 강제 설정
    }

    /// <summary>
    /// Unity Awake: 런타임에서 참조를 다시 확인하고 트리거 설정을 보장한다.
    /// </summary>
    private void Awake()
    {
        if (!portal) portal = GetComponentInParent<Portal>();                     // portal이 비어있으면 부모에서 탐색

        triggerCol = GetComponent<Collider>();                                    // 콜라이더 캐싱
        if (triggerCol) triggerCol.isTrigger = true;                              // 트리거로 강제 설정
    }

    /// <summary>
    /// 포탈이 "쌍으로 배치 완료" 상태인지 확인한다.
    /// </summary>
    /// <returns>준비되었으면 true</returns>
    private bool IsPortalReady()
    {
        return portal &&                                                          // 포탈이 존재하고
               portal.IsPlaced &&                                                  // 포탈이 배치되어 있고
               portal.OtherPortal &&                                               // 반대 포탈이 존재하며
               portal.OtherPortal.IsPlaced;                                        // 반대 포탈도 배치되어 있을 때 true
    }

    /// <summary>
    /// 트리거 진입: PortalTraveller(워프/충돌무시)와 PortalCloneVisual(시각 클론)을 시작시킨다.
    /// </summary>
    /// <param name="other">진입한 콜라이더</param>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsPortalReady()) return;                                             // 포탈 쌍이 준비되지 않았으면 중단
        if (other.isTrigger) return;                                              // 트리거끼리 충돌은 무시

        var traveller = other.GetComponentInParent<PortalTraveller>();            // PortalTraveller를 부모에서 찾기
        if (traveller != null)
        {
            traveller.EnterPortal(portal, portal.OtherPortal, portal.WallColliderCached); // 워프 상태 진입 + 벽 충돌 무시 설정
        }

        var clone = other.GetComponentInParent<PortalCloneVisual>();              // PortalCloneVisual을 부모에서 찾기
        if (clone != null)
        {
            clone.Begin(portal, portal.OtherPortal);                              // 클론 표시 시작
        }
    }

    /// <summary>
    /// 트리거 이탈: PortalTraveller/PortalCloneVisual에 exit를 알려 상태를 정리한다.
    /// </summary>
    /// <param name="other">이탈한 콜라이더</param>
    private void OnTriggerExit(Collider other)
    {
        if (other.isTrigger) return;                                              // 트리거끼리 충돌은 무시

        var traveller = other.GetComponentInParent<PortalTraveller>();            // PortalTraveller 찾기
        if (traveller != null)
        {
            traveller.NotifyTriggerExit(portal);                                  // 포탈 트리거 이탈 알림
        }

        var clone = other.GetComponentInParent<PortalCloneVisual>();              // PortalCloneVisual 찾기
        if (clone != null)
        {
            clone.NotifyTriggerExit(portal);                                      // 포탈 트리거 이탈 알림
        }
    }
}
