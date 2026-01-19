// - Unity Input System(PlayerInput) 기반으로 입력을 읽어 캐싱하고, 필요한 액션은 이벤트로 브로드캐스트한다.
// - Move/Look은 매 프레임 값(프로퍼티)로 제공하고, Jump/Fire/Interact는 이벤트로 제공한다.
using System;                                       // Action 사용
using UnityEngine;                                  // MonoBehaviour, Debug 등
using UnityEngine.InputSystem;                      // PlayerInput, InputAction

[DisallowMultipleComponent]                          // 같은 오브젝트에 중복 추가 방지
[RequireComponent(typeof(PlayerInput))]             // PlayerInput 없으면 자동 추가/필수
public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;                // PlayerInput 컴포넌트 참조

    public static Vector2 Move { get; private set; } // 이동 입력(Vector2) 캐시 (WASD/스틱)
    public static Vector2 Look { get; private set; } // 시야 입력(Vector2) 캐시 (마우스/스틱)

    public static event Action OnJump;              // 점프 입력 이벤트
    public static event Action OnFireBlue;          // 블루 포탈 발사 이벤트
    public static event Action OnFireOrange;        // 오렌지 포탈 발사 이벤트
    public static event Action OnInteract;          // 상호작용(E) 입력 이벤트

    private InputAction moveAction;                 // "Move" 액션 참조
    private InputAction lookAction;                 // "Look" 액션 참조
    private InputAction jumpAction;                 // "Jump" 액션 참조
    private InputAction fireBlueAction;             // "FireBlue" 액션 참조
    private InputAction fireOrangeAction;           // "FireOrange" 액션 참조
    private InputAction interactAction;             // "Interact" 액션 참조

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();  // PlayerInput 가져오기

        // 안전하게 FindAction 사용 (이름 틀리면 null)                                   // 액션 이름은 InputActions에 정의된 문자열과 일치해야 함
        moveAction = playerInput.actions.FindAction("Move", false);                      // 이동 액션 찾기
        lookAction = playerInput.actions.FindAction("Look", false);                      // 시야 액션 찾기
        jumpAction = playerInput.actions.FindAction("Jump", false);                      // 점프 액션 찾기
        fireBlueAction = playerInput.actions.FindAction("FireBlue", false);              // 블루 발사 액션 찾기
        fireOrangeAction = playerInput.actions.FindAction("FireOrange", false);          // 오렌지 발사 액션 찾기
        interactAction = playerInput.actions.FindAction("Interact", false);              // 상호작용 액션 찾기

        if (moveAction == null) Debug.LogWarning("[InputManager] Move 액션을 못 찾음");          // 액션 누락 경고
        if (lookAction == null) Debug.LogWarning("[InputManager] Look 액션을 못 찾음");          // 액션 누락 경고
        if (jumpAction == null) Debug.LogWarning("[InputManager] Jump 액션을 못 찾음");          // 액션 누락 경고
        if (fireBlueAction == null) Debug.LogWarning("[InputManager] FireBlue 액션을 못 찾음");  // 액션 누락 경고
        if (fireOrangeAction == null) Debug.LogWarning("[InputManager] FireOrange 액션을 못 찾음"); // 액션 누락 경고
    }

    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.performed += OnMovePerformed;        // 이동 입력 들어올 때 캐시 갱신
            moveAction.canceled += OnMoveCanceled;          // 이동 입력 해제 시 0으로
            moveAction.Enable();                            // 액션 활성화
        }

        if (lookAction != null)
        {
            lookAction.performed += OnLookPerformed;        // 시야 입력 들어올 때 캐시 갱신
            lookAction.canceled += OnLookCanceled;          // 시야 입력 해제 시 0으로
            lookAction.Enable();                            // 액션 활성화
        }

        if (jumpAction != null)
        {
            jumpAction.started += OnJumpStarted;            // 점프 시작(버튼 다운) 시 이벤트
            jumpAction.Enable();                            // 액션 활성화
        }

        if (fireBlueAction != null)
        {
            fireBlueAction.performed += OnFireBluePerformed; // 블루 발사 트리거 시 이벤트
            fireBlueAction.Enable();                         // 액션 활성화
        }

        if (fireOrangeAction != null)
        {
            fireOrangeAction.performed += OnFireOrangePerformed; // 오렌지 발사 트리거 시 이벤트
            fireOrangeAction.Enable();                            // 액션 활성화
        }

        if (interactAction != null)
        {
            interactAction.started += OnInteractStarted;      // 상호작용 시작(버튼 다운) 시 이벤트
            interactAction.Enable();                          // 액션 활성화
        }
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.performed -= OnMovePerformed;         // 이벤트 해제
            moveAction.canceled -= OnMoveCanceled;           // 이벤트 해제
            moveAction.Disable();                            // 액션 비활성화
        }

        if (lookAction != null)
        {
            lookAction.performed -= OnLookPerformed;         // 이벤트 해제
            lookAction.canceled -= OnLookCanceled;           // 이벤트 해제
            lookAction.Disable();                            // 액션 비활성화
        }

        if (jumpAction != null)
        {
            jumpAction.started -= OnJumpStarted;             // 이벤트 해제
            jumpAction.Disable();                            // 액션 비활성화
        }

        if (fireBlueAction != null)
        {
            fireBlueAction.performed -= OnFireBluePerformed; // 이벤트 해제
            fireBlueAction.Disable();                        // 액션 비활성화
        }

        if (fireOrangeAction != null)
        {
            fireOrangeAction.performed -= OnFireOrangePerformed; // 이벤트 해제
            fireOrangeAction.Disable();                           // 액션 비활성화
        }

        if (interactAction != null)
        {
            interactAction.started -= OnInteractStarted;      // 이벤트 해제
            interactAction.Disable();                         // 액션 비활성화
        }

        // 끌 때 입력 값 리셋(선택이지만 안전)
        Move = Vector2.zero;                                  // 이동 캐시 초기화
        Look = Vector2.zero;                                  // 시야 캐시 초기화
    }

    /// <summary>
    /// "Move" 액션 performed 시 호출된다.
    /// Vector2 이동 입력 값을 읽어서 Move 프로퍼티에 캐싱한다.
    /// </summary>
    /// <param name="ctx">InputAction 콜백 컨텍스트(입력 값 포함)</param>
    private static void OnMovePerformed(InputAction.CallbackContext ctx) => Move = ctx.ReadValue<Vector2>(); // 이동 입력 캐시

    /// <summary>
    /// "Move" 액션 canceled 시 호출된다.
    /// 이동 입력을 0으로 리셋한다.
    /// </summary>
    /// <param name="ctx">InputAction 콜백 컨텍스트</param>
    private static void OnMoveCanceled(InputAction.CallbackContext ctx) => Move = Vector2.zero;              // 이동 입력 리셋

    /// <summary>
    /// "Look" 액션 performed 시 호출된다.
    /// Vector2 시야 입력 값을 읽어서 Look 프로퍼티에 캐싱한다.
    /// </summary>
    /// <param name="ctx">InputAction 콜백 컨텍스트(입력 값 포함)</param>
    private static void OnLookPerformed(InputAction.CallbackContext ctx) => Look = ctx.ReadValue<Vector2>(); // 시야 입력 캐시

    /// <summary>
    /// "Look" 액션 canceled 시 호출된다.
    /// 시야 입력을 0으로 리셋한다.
    /// </summary>
    /// <param name="ctx">InputAction 콜백 컨텍스트</param>
    private static void OnLookCanceled(InputAction.CallbackContext ctx) => Look = Vector2.zero;              // 시야 입력 리셋

    /// <summary>
    /// "Jump" 액션 started 시 호출된다.
    /// 점프 이벤트(OnJump)를 발행한다.
    /// </summary>
    /// <param name="ctx">InputAction 콜백 컨텍스트</param>
    private static void OnJumpStarted(InputAction.CallbackContext ctx) => OnJump?.Invoke();                  // 점프 이벤트 발행

    /// <summary>
    /// "FireBlue" 액션 performed 시 호출된다.
    /// 블루 발사 이벤트(OnFireBlue)를 발행한다.
    /// </summary>
    /// <param name="ctx">InputAction 콜백 컨텍스트</param>
    private static void OnFireBluePerformed(InputAction.CallbackContext ctx) => OnFireBlue?.Invoke();        // 블루 발사 이벤트 발행

    /// <summary>
    /// "FireOrange" 액션 performed 시 호출된다.
    /// 오렌지 발사 이벤트(OnFireOrange)를 발행한다.
    /// </summary>
    /// <param name="ctx">InputAction 콜백 컨텍스트</param>
    private static void OnFireOrangePerformed(InputAction.CallbackContext ctx) => OnFireOrange?.Invoke();    // 오렌지 발사 이벤트 발행

    /// <summary>
    /// "Interact" 액션 started 시 호출된다.
    /// 상호작용 이벤트(OnInteract)를 발행한다.
    /// </summary>
    /// <param name="ctx">InputAction 콜백 컨텍스트</param>
    private static void OnInteractStarted(InputAction.CallbackContext ctx) => OnInteract?.Invoke();          // 상호작용 이벤트 발행
}
