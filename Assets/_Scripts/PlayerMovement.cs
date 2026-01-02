using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    Rigidbody rb;
    Vector3 gravityDir = Vector3.down;

    [Header("Setting")]
    public float moveSpeed = 6f;
    public float jumpPower = 6f;
    public float gravityPower = 20f;
    public LayerMask groundLayer;

    bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        InputManager.OnJump += Jump;
    }

    private void OnDisable()
    {
        InputManager.OnJump -= Jump;
    }

    private void FixedUpdate()
    {
        Move();
        CheckGround();
        Jump();
    }

    void Move()
    {
        Vector2 input = InputManager.Input;

        Vector3 dir = transform.forward * input.y +
            transform.right * input.x;

        Vector3 velocity = rb.linearVelocity;

        // 중력 방향을 고려한 이동
        Vector3 lateral = Vector3.ProjectOnPlane(dir, gravityDir);
        velocity = lateral.normalized * moveSpeed + Vector3.Project(velocity, gravityDir);

        rb.linearVelocity = velocity;
    }

    void Jump()
    {
        if (!isGrounded) return;

        rb.AddForce(-gravityDir * jumpPower, ForceMode.VelocityChange);
    }

    void CheckGround()
    {
        isGrounded = Physics.Raycast(
            transform.position,
            gravityDir,
            1.1f,
            groundLayer
                );
    }
}
