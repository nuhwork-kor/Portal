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
    [SerializeField] private Transform moveBasis;    // 이동 기준(보통 playerBody)

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

        // 이동이 커스텀 중력 구조니까 기본 중력 off 추천
        rb.freezeRotation = true;
        rb.useGravity = false;

        if (!moveBasis) moveBasis = playerBody ? playerBody : transform;

        InitLook();   // partial
        InitMove();   // partial
    }

    private void OnEnable()
    {
        // 점프는 이벤트로 받는게 안정적(누르는 프레임 놓치는거 방지)
        InputManager.OnJump += QueueJump;
    }

    private void OnDisable()
    {
        InputManager.OnJump -= QueueJump;
    }

    private void Update()
    {
        // 연속 입력은 프로퍼티로 읽어서 캐싱
        moveInput = InputManager.Move;
        lookInput = InputManager.Look;

        TickLook(lookInput);
    }

    private void FixedUpdate()
    {
        TickMove(moveInput);
    }
}
