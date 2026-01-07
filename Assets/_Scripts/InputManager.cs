using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputManager : MonoBehaviour
{
    PlayerInput playerinput;

    //외부 공개용 인터페이스, 옵저버들
    public static Vector2 Move {  get; private set; }
    public static Vector2 Look {  get; private set; }
    public static event Action OnJump;

    //Input System의 액션들
    InputAction moveAction;
    InputAction lookAction;
    InputAction jumpAction;

    //콜백 함수들을 저장할 변수들
    Action<InputAction.CallbackContext> onMovePerformed;
    Action<InputAction.CallbackContext> onMoveCanceled;
    Action<InputAction.CallbackContext> onLookPerformed;
    Action<InputAction.CallbackContext> onLookCanceled;
    Action<InputAction.CallbackContext> onJumpPerformed;


    private void OnEnable()
    {
        //PlayerInput 컴포넌트 초기화
        playerinput = GetComponent<PlayerInput>();

        playerinput.defaultActionMap = "Player";
        playerinput.defaultControlScheme = "Keyboard&Mouse";
        playerinput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

        //액션 찾기
        moveAction = playerinput.actions.FindAction("Move");
        lookAction = playerinput.actions.FindAction("Look");
        jumpAction = playerinput.actions.FindAction("Jump");

        //콜백 등록
        if (moveAction != null)
        {
            onMovePerformed = ctx => Move = ctx.ReadValue<Vector2>();
            onMoveCanceled = ctx => Move = Vector2.zero;
            moveAction.performed += onMovePerformed;
            moveAction.canceled += onMoveCanceled;
        }

        if (lookAction != null)
        {
            onLookPerformed = ctx => Look = ctx.ReadValue<Vector2>();
            onLookCanceled = ctx => Look = Vector2.zero;
            lookAction.performed += onLookPerformed;
            lookAction.canceled += onLookCanceled;
        }

        if (jumpAction != null)
        {
            onJumpPerformed = ctx => OnJump?.Invoke();
            jumpAction.performed += onJumpPerformed;
        }
    }

    private void OnDestroy()
    {
        if (moveAction != null)
        {
            if (onMovePerformed != null)
            {
                moveAction.performed -= onMovePerformed;
                onMovePerformed = null;
            }
            if (onMoveCanceled != null)
            {
                moveAction.canceled -= onMoveCanceled;
                onMoveCanceled = null;
            }
        }

        if(lookAction != null)
        {
            if(onLookPerformed != null)
            {
                lookAction.performed -= onLookPerformed;
                onLookPerformed = null;
            }
            if(onLookCanceled != null)
            {
                lookAction.canceled -= onLookCanceled;
                onLookCanceled = null;
            }
        }

        if (jumpAction != null && onJumpPerformed != null)
        {
            jumpAction.performed -= onJumpPerformed;
            onJumpPerformed = null;

        }
    }
}
