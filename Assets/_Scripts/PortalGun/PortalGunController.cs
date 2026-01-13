using UnityEngine;

[DisallowMultipleComponent]
public partial class PortalGunController : MonoBehaviour
{
    [Header("Shared Refs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Rigidbody playerRigidbody;

    [Tooltip("집었을 때 오브젝트가 고정될 위치(빈 오브젝트).")]
    [SerializeField] private Transform holdPoint;

    [Header("Fire Refs")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private PortalSystem portalSystem;

    public Camera PlayerCamera => playerCamera;
    public Rigidbody PlayerRigidbody => playerRigidbody;
    public Transform HoldPoint => holdPoint;

    public bool IsHolding => heldRb != null;

    private void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;
        if (!playerRigidbody) playerRigidbody = GetComponentInParent<Rigidbody>();
        if (!portalSystem) portalSystem = FindAnyObjectByType<PortalSystem>();

        InitFire();
        InitInteraction();
    }

    private void OnEnable()
    {
        BindFireInput(true);
        BindInteractionInput(true);
    }

    private void OnDisable()
    {
        BindFireInput(false);
        BindInteractionInput(false);
    }
}
