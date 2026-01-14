using UnityEngine;

[DisallowMultipleComponent]
public class PortalGunController : MonoBehaviour
{
    [Header("Shared Refs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Rigidbody playerRigidbody;

    [Tooltip("들고 있는 오브젝트를 끌어올 목표 위치(플레이어 앞).")]
    [SerializeField] private Transform holdPoint;

    [Header("Fire Refs")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private PortalSystem portalSystem;

    [Header("Portal Refs (권장: 인스펙터 할당)")]
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    public Camera PlayerCamera => playerCamera;
    public Rigidbody PlayerRigidbody => playerRigidbody;
    public Transform HoldPoint => holdPoint;

    public Transform Muzzle => muzzle;
    public PortalSystem PortalSystem => portalSystem;

    public Portal BluePortal => bluePortal;
    public Portal OrangePortal => orangePortal;

    private void Awake()
    {
        ResolveRefs();
    }

    private void ResolveRefs()
    {
        if (!playerCamera) playerCamera = Camera.main;

        if (!playerRigidbody)
        {
            playerRigidbody = GetComponentInParent<Rigidbody>();
            if (!playerRigidbody) playerRigidbody = GetComponent<Rigidbody>();
        }

        if (!portalSystem) portalSystem = FindAnyObjectByType<PortalSystem>();

        // 포탈 자동탐색(인스펙터 할당이 최우선)
        if (!bluePortal || !orangePortal)
        {
            var portals = Object.FindObjectsByType<Portal>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (var p in portals)
            {
                if (!p) continue;

                string n = p.name.ToLowerInvariant();
                if (!bluePortal && n.Contains("blue")) bluePortal = p;
                else if (!orangePortal && n.Contains("orange")) orangePortal = p;
            }
        }
    }
}
