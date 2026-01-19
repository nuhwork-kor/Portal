using UnityEngine;

// 이 스크립트는 "포탈건 관련 공용 참조(컨텍스트)"를 한곳에 모아 제공한다.
// - PlayerCamera / PlayerRigidbody / HoldPoint / Muzzle / PortalManager / Blue/Orange Portal 참조를 들고 있다.
// - 인스펙터 할당이 최우선이며, 일부는 자동 탐색(예: Camera.main, FindAnyObjectByType, 이름에 blue/orange 포함)으로 보완한다.
[DisallowMultipleComponent]
public class PortalGunController : MonoBehaviour
{
    [Header("Shared Refs")]
    [SerializeField] private Camera playerCamera;                                      // 플레이어 카메라(시점, 발사 방향)
    [SerializeField] private Rigidbody playerRigidbody;                                // 플레이어 리짓바디(속도, traveller 등)

    [Tooltip("들고 있는 오브젝트를 끌어올 목표 위치(플레이어 앞).")]
    [SerializeField] private Transform holdPoint;                                      // 홀드 목표 위치(플레이어 앞)

    [Header("Fire Refs")]
    [SerializeField] private Transform muzzle;                                         // 발사 시작점(총구)
    [SerializeField] private PortalManager portalManager;                              // 포탈 배치 관리 매니저

    [Header("Portal Refs (권장: 인스펙터 할당)")]
    [SerializeField] private Portal bluePortal;                                       // 블루 포탈 참조(권장: 직접 할당)
    [SerializeField] private Portal orangePortal;                                     // 오렌지 포탈 참조(권장: 직접 할당)

    public Camera PlayerCamera => playerCamera;                                       // 외부 제공: 카메라
    public Rigidbody PlayerRigidbody => playerRigidbody;                              // 외부 제공: 플레이어 RB
    public Transform HoldPoint => holdPoint;                                          // 외부 제공: 홀드포인트

    public Transform Muzzle => muzzle;                                                // 외부 제공: 총구
    public PortalManager PortalManager => portalManager;                              // 외부 제공: 포탈 매니저

    public Portal BluePortal => bluePortal;                                           // 외부 제공: 블루 포탈
    public Portal OrangePortal => orangePortal;                                       // 외부 제공: 오렌지 포탈

    /// <summary>
    /// 유니티 생명주기: 자동 참조 보완을 수행한다.
    /// </summary>
    private void Awake()
    {
        ResolveRefs();                                                                // 참조 확보
    }

    /// <summary>
    /// 인스펙터에서 할당되지 않은 참조를 자동으로 탐색해서 채운다.
    /// - 카메라: Camera.main
    /// - 플레이어 RB: 부모/자기에서 검색
    /// - PortalManager: FindAnyObjectByType
    /// - 포탈: 씬에서 Portal 전부 찾고 이름에 "blue/orange" 포함 여부로 매칭(인스펙터 우선)
    /// </summary>
    private void ResolveRefs()
    {
        if (!playerCamera) playerCamera = Camera.main;                                // 카메라 자동 연결

        if (!playerRigidbody)                                                         // 플레이어 RB가 없으면
        {
            playerRigidbody = GetComponentInParent<Rigidbody>();                      // 부모에서 먼저 찾기
            if (!playerRigidbody) playerRigidbody = GetComponent<Rigidbody>();        // 없으면 자기 자신에서 찾기
        }

        if (!portalManager) portalManager = FindAnyObjectByType<PortalManager>();     // 포탈 매니저 자동 탐색

        // 포탈 자동탐색(인스펙터 할당이 최우선)
        if (!bluePortal || !orangePortal)                                              // 둘 중 하나라도 없으면
        {
            var portals = Object.FindObjectsByType<Portal>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );                                                                         // 씬의 모든 Portal 검색

            foreach (var p in portals)                                                 // 포탈 순회
            {
                if (!p) continue;                                                      // null이면 스킵

                string n = p.name.ToLowerInvariant();                                  // 이름 소문자화
                if (!bluePortal && n.Contains("blue")) bluePortal = p;                 // blue 미할당이면 이름으로 매칭
                else if (!orangePortal && n.Contains("orange")) orangePortal = p;      // orange 미할당이면 이름으로 매칭
            }
        }
    }
}
