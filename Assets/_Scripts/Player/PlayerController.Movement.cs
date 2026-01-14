using UnityEngine;

public partial class PlayerController
{
    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float airControlMultiplier = 0.4f;

    [Header("Jump/Gravity")]
    [SerializeField] private float jumpVelocity = 6f;
    [SerializeField] private float gravityPower = 20f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private float groundCheckRadius = 0.25f;

    private bool isGrounded;
    private bool jumpQueued;

    private void InitMove()
    {
        // nothing now
    }

    private void QueueJump()
    {
        jumpQueued = true;
    }

    private void TickMove(Vector2 move)
    {
        CheckGround();

        ApplyMove(move);

        if (jumpQueued)
        {
            jumpQueued = false;
            ApplyJump();
        }

        ApplyGravity();

        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyMove(Vector2 move)
    {
        Vector3 v = rb.linearVelocity;

        if (move.sqrMagnitude < 0.0001f)
        {
            if (isGrounded)
            {
                v.x = 0f;
                v.z = 0f;
                rb.linearVelocity = v;
            }
            return;
        }

        Vector3 forward = moveBasis.forward;
        Vector3 right = moveBasis.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 dir = forward * move.y + right * move.x;
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        float control = isGrounded ? 1f : airControlMultiplier;

        v.x = dir.x * moveSpeed * control;
        v.z = dir.z * moveSpeed * control;

        rb.linearVelocity = v;
    }

    private void ApplyJump()
    {
        if (!isGrounded) return;

        Vector3 v = rb.linearVelocity;
        if (v.y < 0f) v.y = 0f;
        v.y = jumpVelocity;
        rb.linearVelocity = v;

        isGrounded = false;
    }

    private void ApplyGravity()
    {
        rb.AddForce(Vector3.down * gravityPower, ForceMode.Acceleration);
    }

    private void CheckGround()
    {
        if (!capsule)
        {
            isGrounded = false;
            return;
        }

        Transform t = capsule.transform;

        // ���� center
        Vector3 center = t.TransformPoint(capsule.center);

        float scaleXZ = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.z));
        float scaleY = Mathf.Abs(t.lossyScale.y);

        float capsuleRadius = capsule.radius * scaleXZ;
        float halfHeight = Mathf.Max((capsule.height * 0.5f) * scaleY, capsuleRadius);

        float castRadius = Mathf.Min(groundCheckRadius, capsuleRadius) * 0.95f;
        float castDistance = (halfHeight - castRadius) + groundCheckDistance;

        isGrounded = Physics.SphereCast(
            center,
            castRadius,
            Vector3.down,
            out _,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    // ��Ż �ڷ���Ʈ �� �ӵ� ���ÿ�
    public void SetVelocity(Vector3 newVel)
    {
        rb.linearVelocity = newVel;
    }

    public bool IsGrounded => isGrounded;
    public Rigidbody RB => rb;
}
