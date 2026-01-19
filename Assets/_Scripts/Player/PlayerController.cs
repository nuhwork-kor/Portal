// - 플레이어의 기본 본체(Partial) 스크립트.
// - 입력 캐싱(Update) + 시야 처리(Update) + 이동/물리(FixedUpdate)를 분리해서 호출한다.
using UnityEngine;                                                  // MonoBehaviour, Rigidbody, CapsuleCollider 등

[DisallowMultipleComponent]                                         // 중복 추가 방지
[RequireComponent(typeof(Rigidbody))]                               // Rigidbody 필수
[RequireComponent(typeof(CapsuleCollider))]                         // CapsuleCollider 필수
public partial class PlayerController : MonoBehaviour
{
    [Header("Refs (Look)")]
    [SerializeField] private Transform playerBody;                  // yaw 회전 축(플레이어 바디)
    [SerializeField] private Transform cameraRoot;                  // pitch 회전 축(카메라 루트)

    [Header("Refs (Move)")]
    [SerializeField] private Transform moveBasis;                   // 이동 기준(보통 playerBody)              

    private Rigidbody rb;                                           // 플레이어 Rigidbody 런타임 캐시
    private CapsuleCollider capsule;                                // 플레이어 CapsuleCollider 런타임 캐시

    private Vector2 moveInput;                                      // 이동 입력 캐시(Update에서 읽음)
    private Vector2 lookInput;                                      // 시야 입력 캐시(Update에서 읽음)

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();                             // Rigidbody 캐싱
        capsule = GetComponent<CapsuleCollider>();                  // CapsuleCollider 캐싱

        rb.freezeRotation = true;                                   // 물리 회전은 막고(시야/워프는 직접 제어)
        rb.useGravity = false;                                      // 중력은 커스텀 적용(ApplyGravity)

        if (!moveBasis) moveBasis = playerBody ? playerBody : transform; // 이동 기준 미설정이면 playerBody 우선

        InitLook();                                                 // partial: MouseLook 초기화
        InitMove();                                                 // partial: Movement 초기화
    }

    private void OnEnable()
    {
        InputManager.OnJump += QueueJump;                            // Jump 이벤트를 Movement 파트에 전달
    }

    private void OnDisable()
    {
        InputManager.OnJump -= QueueJump;                            // Jump 이벤트 해제
    }

    private void Update()
    {
        moveInput = InputManager.Move;                               // 이동 입력 캐싱
        lookInput = InputManager.Look;                               // 시야 입력 캐싱

        TickLook(lookInput);                                         // partial: 시야 업데이트
    }

    private void FixedUpdate()
    {
        TickMove(moveInput);                                         // partial: 이동/점프/중력 처리

    }
}
