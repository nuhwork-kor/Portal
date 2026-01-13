using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;

    public static Vector2 Move { get; private set; }
    public static Vector2 Look { get; private set; }

    public static event Action OnJump;
    public static event Action OnFireBlue;
    public static event Action OnFireOrange;
    public static event Action OnInteract;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction fireBlueAction;
    private InputAction fireOrangeAction;
    private InputAction interactAction;


    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        // 안전하게 FindAction 사용 (이름 틀리면 null)
        moveAction = playerInput.actions.FindAction("Move", false);
        lookAction = playerInput.actions.FindAction("Look", false);
        jumpAction = playerInput.actions.FindAction("Jump", false);
        fireBlueAction = playerInput.actions.FindAction("FireBlue", false);
        fireOrangeAction = playerInput.actions.FindAction("FireOrange", false);
        interactAction = playerInput.actions.FindAction("Interact", false);


        if (moveAction == null) Debug.LogWarning("[InputManager] Move 액션을 못 찾음");
        if (lookAction == null) Debug.LogWarning("[InputManager] Look 액션을 못 찾음");
        if (jumpAction == null) Debug.LogWarning("[InputManager] Jump 액션을 못 찾음");
        if (fireBlueAction == null) Debug.LogWarning("[InputManager] FireBlue 액션을 못 찾음");
        if (fireOrangeAction == null) Debug.LogWarning("[InputManager] FireOrange 액션을 못 찾음");
    }

    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.performed += OnMovePerformed;
            moveAction.canceled += OnMoveCanceled;
            moveAction.Enable();
        }

        if (lookAction != null)
        {
            lookAction.performed += OnLookPerformed;
            lookAction.canceled += OnLookCanceled;
            lookAction.Enable();
        }

        if (jumpAction != null)
        {
            jumpAction.started += OnJumpStarted;
            jumpAction.Enable();
        }

        if (fireBlueAction != null)
        {
            fireBlueAction.performed += OnFireBluePerformed;
            fireBlueAction.Enable();
        }

        if (fireOrangeAction != null)
        {
            fireOrangeAction.performed += OnFireOrangePerformed;
            fireOrangeAction.Enable();
        }

        if (interactAction != null)
        {
            interactAction.started += OnInteractStarted;
            interactAction.Enable();
        }

    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.performed -= OnMovePerformed;
            moveAction.canceled -= OnMoveCanceled;
            moveAction.Disable();
        }

        if (lookAction != null)
        {
            lookAction.performed -= OnLookPerformed;
            lookAction.canceled -= OnLookCanceled;
            lookAction.Disable();
        }

        if (jumpAction != null)
        {
            jumpAction.started -= OnJumpStarted;
            jumpAction.Disable();
        }

        if (fireBlueAction != null)
        {
            fireBlueAction.performed -= OnFireBluePerformed;
            fireBlueAction.Disable();
        }

        if (fireOrangeAction != null)
        {
            fireOrangeAction.performed -= OnFireOrangePerformed;
            fireOrangeAction.Disable();
        }

        if (interactAction != null)
        {
            interactAction.started -= OnInteractStarted;
            interactAction.Disable();
        }


        // 끌 때 입력 값 리셋(선택이지만 안전)
        Move = Vector2.zero;
        Look = Vector2.zero;
    }

    private static void OnMovePerformed(InputAction.CallbackContext ctx) => Move = ctx.ReadValue<Vector2>();
    private static void OnMoveCanceled(InputAction.CallbackContext ctx) => Move = Vector2.zero;

    private static void OnLookPerformed(InputAction.CallbackContext ctx) => Look = ctx.ReadValue<Vector2>();
    private static void OnLookCanceled(InputAction.CallbackContext ctx) => Look = Vector2.zero;

    private static void OnJumpStarted(InputAction.CallbackContext ctx) => OnJump?.Invoke();
    private static void OnFireBluePerformed(InputAction.CallbackContext ctx) => OnFireBlue?.Invoke();
    private static void OnFireOrangePerformed(InputAction.CallbackContext ctx) => OnFireOrange?.Invoke();
    static void OnInteractStarted(InputAction.CallbackContext ctx) => OnInteract?.Invoke();

}
