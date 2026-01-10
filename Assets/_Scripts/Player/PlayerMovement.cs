using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PlayerMovement : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] float moveSpeed = 10f;
    [SerializeField] float jumpVelocity = 6f;
    [SerializeField] float gravityPower = 20f;

    [Header("Ground Check")]
    [SerializeField] LayerMask groundMask;
    [SerializeField] float groundCheckDistance = 0.15f;

    [Header("Refs")]
    [SerializeField] Transform moveBasis;
    [SerializeField] float airControlMultiplier = 0.4f;

    Rigidbody rb;
    Vector2 moveInput;
    bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (!moveBasis) moveBasis = transform;
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }

    private void FixedUpdate()
    {
        CheckGround();
        ApplyMove();
        ApplyGravity();

        rb.angularVelocity = Vector3.zero;
    }

    void ApplyMove()
    {
        if (moveInput.sqrMagnitude < 0.0001f) return;

        Vector3 forward = moveBasis.forward;
        Vector3 right = moveBasis.right;

        //수평면 고정
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 dir = forward * moveInput.y + right * moveInput.x;
        if(dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 v = rb.linearVelocity;
        float control = isGrounded ? 1f : airControlMultiplier;

        v.x = dir.x * moveSpeed * control;
        v.z = dir.z * moveSpeed * control;
        rb.linearVelocity = v;
    }

    public void Jump()
    {
        if (!isGrounded) return;

        Vector3 v = rb.linearVelocity;
        if (v.y < 0f) v.y = 0f;
        v.y = jumpVelocity;
        rb.linearVelocity = v;

        isGrounded = false;
    }

    void ApplyGravity()
    {
        rb.AddForce(Vector3.down * gravityPower, ForceMode.Acceleration);
    }

    void CheckGround()
    {
        //플레이어 중심 살짝 아래에서 sphereCast
        float radius = 0.25f;
        Vector3 origin = transform.position + Vector3.up * 0.1f;

        isGrounded = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
            );
    }

    /// <summary>
    /// 텔레포트 후 외부에서 속도 세팅할 용도
    /// </summary>
    /// <param name="newVel"></param>
    public void SetVelocity(Vector3 newVel)
    {
        rb.linearVelocity = newVel;
    }

    public Rigidbody RB => rb;
}
