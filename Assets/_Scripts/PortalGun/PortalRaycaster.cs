using UnityEngine;

// 이 스크립트는 "플레이어 시점에서 집기(Interact) 대상"을 찾아주는 레이캐스터다.
// - Direct: 카메라 전방에서 Interactable을 찾는다.
// - Through-Portal: 먼저 PortalSurface를 맞추면, 반대편 포탈 공간으로 Ray/SphereCast를 변환해서 2차로 Interactable을 찾는다.
// - occluderMask가 먼저 맞으면(가려짐) 즉시 실패 처리한다.
// - 반환값(InteractHit)에는 "실제로 잡힌 오브젝트의 RaycastHit(rbHit)"가 포함되어 회전 스냅 등에 사용된다.
[DisallowMultipleComponent]
[RequireComponent(typeof(PortalGunController))]
public class PortalRaycaster : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PortalGunController ctx;                                   // PlayerCamera 참조 제공

    [Header("Pick Settings")]
    [SerializeField] private float maxInteractDistance = 5.0f;                          // 상호작용 최대 거리(직접/포탈 합산 거리 제한)
    [SerializeField] private LayerMask interactableMask = ~0;                           // 집을 수 있는 대상 레이어
    [SerializeField] private LayerMask portalSurfaceMask = 0;                           // 포탈 표면(Portal 콜라이더) 레이어
    [SerializeField] private LayerMask occluderMask = 0;                                // 가림막(시야 차단) 레이어
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore; // 트리거 처리 방식

    [Header("Tuning")]
    [SerializeField] private float sphereRadius = 0.10f;                                // 관대하게 잡기 위한 스피어캐스트 반경
    [SerializeField] private float eps = 0.01f;                                         // 포탈 통과 후 시작점 살짝 밀어내는 값(자기 표면 재히트 방지)
    [SerializeField] private float extraRaySlack = 10f;                                 // 1차 레이캐스트 여유 거리(포탈 표면 먼저 찾기용)

    public float MaxInteractDistance => maxInteractDistance;                            // 외부에서 읽기용 프로퍼티
    public LayerMask InteractableMask => interactableMask;                              // 외부에서 읽기용 프로퍼티
    public LayerMask PortalSurfaceMask => portalSurfaceMask;                            // 외부에서 읽기용 프로퍼티
    public LayerMask OccluderMask => occluderMask;                                      // 외부에서 읽기용 프로퍼티
    public float SphereRadius => sphereRadius;                                          // 외부에서 읽기용 프로퍼티

    public struct InteractHit
    {
        public bool hit;                                                                // 최종적으로 유효한 히트인지 여부
        public Rigidbody rb;                                                            // 잡을 대상 Rigidbody

        public bool throughPortal;                                                      // 포탈을 통해 집었는지 여부
        public Portal inPortal;                                                         // 플레이어 측 포탈(입구)
        public Portal outPortal;                                                        // 반대편 포탈(출구)

        // ✅ “실제 잡힌 오브젝트”에 대한 hit (direct면 1차 hit, through면 2차 hit)
        public RaycastHit rbHit;                                                        // 잡힌 오브젝트 표면 정보(노멀 등)

        public float totalDistance;                                                     // direct면 거리, through면 (입구까지 + 출구공간 거리)
    }

    /// <summary>
    /// 유니티 생명주기: 참조 자동 확보.
    /// </summary>
    private void Awake()
    {
        if (!ctx) ctx = GetComponent<PortalGunController>();                            // 컨텍스트 자동 연결
    }

    /// <summary>
    /// 카메라 전방으로 집기 가능한 대상을 찾는다.
    /// - occluder가 먼저면 실패
    /// - interactable이 먼저면 direct 성공
    /// - portalSurface가 먼저면 through-portal로 2차 캐스트 후 성공/실패
    /// </summary>
    /// <param name="result">집기 결과 구조체</param>
    /// <returns>유효한 집기 대상이 있으면 true</returns>
    public bool TryGetInteractHit(out InteractHit result)
    {
        result = default;                                                               // 기본값 초기화

        Camera cam = ctx ? ctx.PlayerCamera : null;                                     // 플레이어 카메라
        if (!cam) return false;                                                         // 카메라 없으면 실패

        Vector3 origin = cam.transform.position;                                        // 캐스트 시작점
        Vector3 dir = cam.transform.forward;                                            // 캐스트 방향(전방)

        int mask = interactableMask | portalSurfaceMask | occluderMask;                 // 검사할 레이어 마스크 합성
        float firstMax = Mathf.Max(maxInteractDistance, 0.01f) + Mathf.Max(extraRaySlack, 0f); // 1차 탐색 거리(여유 포함)

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, firstMax, mask, triggerInteraction); // 1차 레이캐스트(모든 히트)
        if (hits == null || hits.Length == 0) return false;                             // 히트 없으면 실패

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));            // 가까운 순 정렬

        for (int i = 0; i < hits.Length; i++)                                           // 히트 순회
        {
            RaycastHit h = hits[i];                                                     // 현재 히트
            if (!h.collider) continue;                                                  // 콜라이더 없으면 스킵

            int layerBit = 1 << h.collider.gameObject.layer;                            // 레이어 비트

            // 0) occluder가 먼저면 끝
            if ((occluderMask.value & layerBit) != 0)                                   // 가림막이면
                return false;                                                           // 시야 차단 => 즉시 실패

            // 1) Direct Interactable
            if ((interactableMask.value & layerBit) != 0)                               // 집기 대상 레이어면
            {
                if (h.distance > maxInteractDistance) return false;                     // 최대 거리 초과면 실패

                // 관대하게 스피어캐스트로 RB 확정
                if (!Physics.SphereCast(origin, sphereRadius, dir, out RaycastHit sh, maxInteractDistance, interactableMask, triggerInteraction)) // 스피어캐스트로 재확정
                    return false;                                                       // 스피어캐스트 실패

                var rb = sh.collider.attachedRigidbody;                                  // 붙어있는 RB
                if (!rb) return false;                                                  // RB 없으면 실패

                result.hit = true;                                                      // 성공 표시
                result.rb = rb;                                                         // 대상 RB 저장
                result.throughPortal = false;                                           // direct임
                result.rbHit = sh;                                                      // 실제 표면 히트 저장
                result.totalDistance = sh.distance;                                     // 거리 저장
                return true;                                                            // 성공
            }

            // 2) PortalSurface hit => Through Portal
            if ((portalSurfaceMask.value & layerBit) != 0)                              // 포탈 표면 레이어면
            {
                Portal inPortal = h.collider.GetComponentInParent<Portal>();            // 입구 포탈 탐색
                if (!inPortal) return false;                                            // 포탈 컴포넌트 없으면 실패

                Portal outPortal = inPortal.OtherPortal;                                // 반대편 포탈
                if (!outPortal) return false;                                           // 반대편 없으면 실패
                if (!inPortal.IsPlaced || !outPortal.IsPlaced) return false;           // 둘 중 하나라도 미설치면 실패

                Vector3 outOrigin = PortalMath.TransformPoint(h.point, inPortal.Plane, outPortal.Plane);      // 포탈 반대편 공간의 시작점
                Vector3 outDir = PortalMath.TransformDirection(dir, inPortal.Plane, outPortal.Plane);         // 포탈 반대편 공간의 방향
                outOrigin += outDir * eps;                                              // 표면 재히트 방지로 살짝 전진

                float secondMax = maxInteractDistance + Mathf.Max(extraRaySlack, 0f);   // 2차 탐색 거리(여유 포함)
                int secondMask = interactableMask | occluderMask;                       // 2차에서는 interactable + occluder만 체크

                if (!Physics.SphereCast(outOrigin, sphereRadius, outDir, out RaycastHit outHit, secondMax, secondMask, triggerInteraction)) // 반대편에서 스피어캐스트
                    return false;                                                       // 실패

                int outLayerBit = 1 << outHit.collider.gameObject.layer;                // 2차 히트 레이어 비트
                if ((occluderMask.value & outLayerBit) != 0)                            // 가림막이면
                    return false;                                                       // 실패

                var rb = outHit.collider.attachedRigidbody;                              // 붙어있는 RB
                if (!rb) return false;                                                  // RB 없으면 실패

                float total = h.distance + outHit.distance;                             // 합산 거리(입구까지 + 출구공간 거리)
                if (total > maxInteractDistance) return false;                          // 최대 거리 초과면 실패

                result.hit = true;                                                      // 성공 표시
                result.rb = rb;                                                         // 대상 RB 저장
                result.throughPortal = true;                                            // through-portal임
                result.inPortal = inPortal;                                             // 입구 포탈 저장
                result.outPortal = outPortal;                                           // 출구 포탈 저장
                result.rbHit = outHit;                                                  // 실제 표면 히트 저장(출구 공간에서 맞춘 표면)
                result.totalDistance = total;                                           // 합산 거리 저장
                return true;                                                            // 성공
            }
        }

        return false;                                                                   // 끝까지 못 찾으면 실패
    }
}
