using UnityEngine;

// 이 스크립트는 "포탈 탄환"의 수명/이동/충돌을 관리한다.
// - Launch로 타입/방향/속도/최대거리/PortalManager를 주입받는다.
// - Rigidbody 속도로 이동하며, 시간(maxLifeSeconds) 또는 거리(maxDistance)를 소진하면 풀로 반환한다.
// - 충돌 시 PortalManager.TryPlacePortal로 포탈 배치를 시도하고, 실패면 Impact FX를 재생한다.
// - 발사 타입(Blue/Orange)에 따라 프리팹 내부 자식 FX(Shoot_B / Shoot_O)를 토글하여 트레일 연출을 전환한다.
public class PortalBullet : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private string poolKey = "PortalBullet";                          // 반환할 풀 키

    [Header("Life")]
    [SerializeField] private float maxLifeSeconds = 3f;                                // 최대 생존 시간(초)

    [Header("Child FX Names (under PortalBullet prefab)")]
    [SerializeField] private string shootBlueChildName = "Shoot_B";                    // 블루 트레일 루트 자식 이름
    [SerializeField] private string shootOrangeChildName = "Shoot_O";                  // 오렌지 트레일 루트 자식 이름

    private Rigidbody rb;                                                             // 탄환 RB

    private PortalManager.PortalType shotType;                                        // 발사된 포탈 타입
    private PortalManager portalManager;                                              // 포탈 배치 요청을 보낼 매니저

    private float lifeTimer;                                                          // 생존 시간 누적
    private float remainingDistance;                                                   // 남은 거리(감소)

    private Transform shootBlueRoot;                                                   // 블루 트레일 루트
    private Transform shootOrangeRoot;                                                 // 오렌지 트레일 루트
    private ParticleSystem[] shootBluePS;                                              // 블루 트레일 파티클 배열
    private ParticleSystem[] shootOrangePS;                                            // 오렌지 트레일 파티클 배열

    /// <summary>
    /// 유니티 생명주기: Rigidbody 세팅 및 자식 FX 캐시.
    /// </summary>
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();                                                // RB 캐시
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;          // 빠른 탄환용 연속 충돌
        rb.interpolation = RigidbodyInterpolation.Interpolate;                         // 보간

        CacheChildFX();                                                               // 자식 FX 캐시
    }

    /// <summary>
    /// 유니티 생명주기: 활성화 시 트레일을 정리해 중복 재생을 방지한다.
    /// </summary>
    private void OnEnable()
    {
        StopAllTrail();                                                               // 트레일 초기화
    }

    /// <summary>
    /// 프리팹 내부에서 트레일 FX 루트/파티클 배열을 찾아 캐시한다.
    /// </summary>
    private void CacheChildFX()
    {
        shootBlueRoot = FindDeepChild(transform, shootBlueChildName);                 // 깊이 탐색으로 자식 찾기
        shootOrangeRoot = FindDeepChild(transform, shootOrangeChildName);             // 깊이 탐색으로 자식 찾기

        shootBluePS = shootBlueRoot ? shootBlueRoot.GetComponentsInChildren<ParticleSystem>(true) : null;     // 블루 파티클 배열
        shootOrangePS = shootOrangeRoot ? shootOrangeRoot.GetComponentsInChildren<ParticleSystem>(true) : null;// 오렌지 파티클 배열
    }

    /// <summary>
    /// 탄환 발사 초기화.
    /// - 타입/매니저/수명/거리 초기화
    /// - RB 속도 설정
    /// - 트레일 재생
    /// </summary>
    public void Launch(PortalManager.PortalType type, Vector3 dir, float speed, float maxDistance, PortalManager manager)
    {
        shotType = type;                                                               // 발사 타입 저장
        portalManager = manager;                                                       // 매니저 저장

        lifeTimer = 0f;                                                                // 수명 타이머 리셋
        remainingDistance = maxDistance;                                               // 남은 거리 초기화

        rb.linearVelocity = Vector3.zero;                                              // 기존 속도 제거
        rb.angularVelocity = Vector3.zero;                                             // 기존 각속도 제거

        Vector3 v = dir.normalized * speed;                                            // 발사 속도 벡터
        rb.linearVelocity = v;                                                         // RB에 속도 적용
        transform.rotation = Quaternion.LookRotation(v.normalized, Vector3.up);        // 진행 방향으로 회전 맞춤

        // 발사 트레일 재생
        PlayTrail(type);                                                               // 타입에 맞는 트레일 선택 재생
    }

    /// <summary>
    /// 유니티 생명주기(Update): 수명/거리 소진 체크 후 디스폰.
    /// </summary>
    private void Update()
    {
        lifeTimer += Time.deltaTime;                                                   // 시간 누적
        remainingDistance -= rb.linearVelocity.magnitude * Time.deltaTime;             // 이동 거리만큼 감소

        if (lifeTimer >= maxLifeSeconds || remainingDistance <= 0f)                    // 시간/거리 한계 도달이면
            Despawn();                                                                 // 풀로 반환
    }

    /// <summary>
    /// 충돌 처리:
    /// - Portal 태그면 그냥 디스폰(포탈 표면에 막히는 탄환 방지)
    /// - 그 외면 TryPlacePortal 시도
    /// - 배치 실패면 Impact FX 재생 후 디스폰
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0) { Despawn(); return; }                        // 접촉점 없으면 안전하게 디스폰

        // 포탈과 충돌하면 그냥 소멸
        if (collision.collider.CompareTag("Portal"))                                   // 포탈 태그면
        {
            Despawn();                                                                 // 디스폰
            return;                                                                    // 종료
        }

        ContactPoint cp = collision.contacts[0];                                       // 첫 접촉점

        bool placed = false;                                                          // 배치 성공 여부
        if (portalManager != null)                                                    // 매니저가 있으면
            placed = portalManager.TryPlacePortal(shotType, cp.point, cp.normal, collision.collider); // 포탈 배치 시도

        // 배치 실패 시 Impact FX 재생
        if (!placed && PortalEffectManager.Instance != null)                           // 실패 + 이펙트 매니저 있으면
            PortalEffectManager.Instance.PlayImpact(shotType, cp.point, cp.normal);   // 임팩트 재생

        Despawn();                                                                     // 디스폰
    }

    /// <summary>
    /// 타입에 맞는 트레일(FX)을 켜고 재생한다.
    /// </summary>
    private void PlayTrail(PortalManager.PortalType type)
    {
        StopAllTrail();                                                                // 기존 트레일 정리

        bool blue = (type == PortalManager.PortalType.Blue);                          // 블루 여부

        if (shootBlueRoot) shootBlueRoot.gameObject.SetActive(blue);                   // 블루 루트 토글
        if (shootOrangeRoot) shootOrangeRoot.gameObject.SetActive(!blue);              // 오렌지 루트 토글

        var arr = blue ? shootBluePS : shootOrangePS;                                  // 재생할 파티클 배열 선택
        if (arr == null) return;                                                       // 없으면 종료

        for (int i = 0; i < arr.Length; i++)                                           // 파티클 순회
        {
            var ps = arr[i];                                                           // 파티클
            if (!ps) continue;                                                         // null이면 스킵

            if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);              // 비활성화면 켜기

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);            // 완전 정지/클리어
            ps.Clear(true);                                                            // 잔상 제거
            ps.Play(true);                                                             // 재생
        }
    }

    /// <summary>
    /// 모든 트레일을 정지하고 루트를 비활성화한다.
    /// </summary>
    private void StopAllTrail()
    {
        StopPS(shootBluePS);                                                           // 블루 파티클 정지
        StopPS(shootOrangePS);                                                         // 오렌지 파티클 정지

        if (shootBlueRoot) shootBlueRoot.gameObject.SetActive(false);                  // 블루 루트 끄기
        if (shootOrangeRoot) shootOrangeRoot.gameObject.SetActive(false);              // 오렌지 루트 끄기
    }

    /// <summary>
    /// 파티클 배열을 안전하게 정지/클리어한다.
    /// </summary>
    private static void StopPS(ParticleSystem[] arr)
    {
        if (arr == null) return;                                                       // null이면 종료
        for (int i = 0; i < arr.Length; i++)                                           // 순회
        {
            var ps = arr[i];                                                           // 파티클
            if (!ps) continue;                                                         // null이면 스킵
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);            // 정지+클리어
            ps.Clear(true);                                                            // 잔상 제거
        }
    }

    /// <summary>
    /// 탄환을 풀로 반환하거나(가능하면), 비활성화한다.
    /// </summary>
    private void Despawn()
    {
        StopAllTrail();                                                                // 트레일 정리

        rb.linearVelocity = Vector3.zero;                                              // 속도 제거
        rb.angularVelocity = Vector3.zero;                                             // 각속도 제거

        if (ObjectPoolManager.Instance != null)                                        // 풀 매니저가 있으면
            ObjectPoolManager.Instance.ReturnToPool(poolKey, gameObject);              // 풀로 반환
        else
            gameObject.SetActive(false);                                               // 없으면 그냥 비활성화
    }

    /// <summary>
    /// Transform 계층에서 이름이 일치하는 자식을 재귀적으로 찾는다.
    /// </summary>
    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (!parent) return null;                                                      // 부모 없으면 null
        for (int i = 0; i < parent.childCount; i++)                                    // 자식 순회
        {
            var c = parent.GetChild(i);                                                // i번째 자식
            if (c.name == name) return c;                                              // 이름 일치하면 반환
            var r = FindDeepChild(c, name);                                            // 재귀 탐색
            if (r) return r;                                                           // 찾았으면 반환
        }
        return null;                                                                   // 못 찾으면 null
    }
}
