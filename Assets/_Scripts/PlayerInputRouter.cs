using UnityEngine;

/// <summary>
/// 입력 > 전달
/// </summary>
public class PlayerInputRouter : MonoBehaviour
{
    PlayerMovement move;

    private void Awake()
    {
        move = GetComponent<PlayerMovement>();
    }

    private void OnEnable()
    {
        InputManager.OnJump += HandleJump;
    }

    private void OnDisable()
    {
        InputManager.OnJump -= HandleJump;
    }

    void HandleJump()
    {
        move.Jump();
    }
}
