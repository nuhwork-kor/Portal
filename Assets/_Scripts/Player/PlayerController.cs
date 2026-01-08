using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] PlayerMovement movement;
    [SerializeField] PlayerMouseLook mouseLook;

    private void Awake()
    {
        if(!movement) movement = GetComponent<PlayerMovement>();
        if(!mouseLook) mouseLook = GetComponent<PlayerMouseLook>();
    }

    private void OnEnable()
    {
        InputManager.OnJump += HandleJump;
    }
    private void OnDisable()
    {
        InputManager.OnJump -= HandleJump;
    }

    private void Update()
    {
        movement.SetMoveInput(InputManager.Move);
        mouseLook.SetLookInput(InputManager.Look);
    }

    void HandleJump()
    {
        movement.Jump();
    }
}
