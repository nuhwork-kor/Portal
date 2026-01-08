using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    PlayerInput playerInput;

    public static Vector2 Move { get; private set; }
    public static Vector2 Look { get; private set; }

    public static event Action OnJump;

    public static event Action OnFireBlue;
    public static event Action OnFireOrange;

    InputAction moveAction;
    InputAction lookAction;
    InputAction jumpAction;
    InputAction fireBlueAction;
    InputAction fireOrangeAction;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        //Action 설정
        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        jumpAction = playerInput.actions["Jump"];
        fireBlueAction = playerInput.actions["LeftFire"];
        fireOrangeAction = playerInput.actions["RightFire"];
    }

    private void OnEnable()
    {
        if(moveAction != null)
        {
            moveAction.performed += OnMovePerformed;
            moveAction.canceled += OnMoveCanceled;
            moveAction.Enable();
        }

        if(lookAction != null)
        {
            lookAction.performed += OnLookPerformed;
            lookAction.canceled += OnLookCanceled;
            lookAction.Enable();
        }

        if(jumpAction != null)
        {
            jumpAction.performed += OnJumpPerformed;
            jumpAction.Enable();
        }

        if(fireBlueAction != null)
        {
            fireBlueAction.performed += OnFireBluePerformed;
            fireBlueAction.Enable();
        }
        
        if(fireOrangeAction != null)
        {
            fireOrangeAction.performed += OnFireOrangePerformed;
            fireOrangeAction.Enable();
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
            jumpAction.performed -= OnJumpPerformed;
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
    }

    static void OnMovePerformed(InputAction.CallbackContext ctx) => Move = ctx.ReadValue<Vector2>();
    static void OnMoveCanceled(InputAction.CallbackContext ctx) => Move = Vector2.zero;
    
    static void OnLookPerformed(InputAction.CallbackContext ctx) => Look = ctx.ReadValue<Vector2>();
    static void OnLookCanceled(InputAction.CallbackContext ctx) => Look = Vector2.zero;

    static void OnJumpPerformed(InputAction.CallbackContext ctx) => OnJump?.Invoke();
    static void OnFireBluePerformed(InputAction.CallbackContext ctx) => OnFireBlue?.Invoke();
    static void OnFireOrangePerformed(InputAction.CallbackContext ctx) => OnFireOrange?.Invoke();

}
