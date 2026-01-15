using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public partial class PlayerController : MonoBehaviour
{
    [Header("Refs (Look)")]
    [SerializeField] private Transform playerBody;   // yaw
    [SerializeField] private Transform cameraRoot;   // pitch

    [Header("Refs (Move)")]
    [SerializeField] private Transform moveBasis;    // �̵� ����(���� playerBody)

    // runtime
    private Rigidbody rb;
    private CapsuleCollider capsule;

    // input cache
    private Vector2 moveInput;
    private Vector2 lookInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        // �̵��� Ŀ���� �߷� �����ϱ� �⺻ �߷� off ��õ
        rb.freezeRotation = true;
        rb.useGravity = false;

        if (!moveBasis) moveBasis = playerBody ? playerBody : transform;

        InitLook();   // partial
        InitMove();   // partial
    }

    private void OnEnable()
    {
        // ������ �̺�Ʈ�� �޴°� ������(������ ������ ��ġ�°� ����)
        InputManager.OnJump += QueueJump;
    }

    private void OnDisable()
    {
        InputManager.OnJump -= QueueJump;
    }

    private void Update()
    {
        // ���� �Է��� ������Ƽ�� �о ĳ��
        moveInput = InputManager.Move;
        lookInput = InputManager.Look;

        TickLook(lookInput);
    }

    private void FixedUpdate()
    {
        TickMove(moveInput);
    }
}
