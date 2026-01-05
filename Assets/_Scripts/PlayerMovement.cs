using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    Rigidbody rb;
    Vector3 gravityDir = Vector3.down;

    [Header("Setting")]
    public float moveSpeed = 6f;
    public float jumpVelocity = 6f;
    public float gravityPower = 20f;
    public LayerMask groundLayer;

    bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.freezeRotation = true;
    }

    private void OnEnable()
    {
        InputManager.OnJump += HandleJump;
    }

    private void OnDisable()
    {
        InputManager.OnJump -= HandleJump;
    }

    private void FixedUpdate()
    {
        CheckGround();
        Move();
        ApplyGravity();

        rb.angularVelocity = Vector3.zero;
    }

    void Move()
    {
        //input값 받아오기
        Vector2 input = InputManager.Input;

        //플레이어 바라보는 방향 기준으로 이동 방향 구성
        Vector3 dir = transform.forward * input.y +
            transform.right * input.x;

        //대각선 이동 속도 보정
        if(dir.sqrMagnitude > 1f) dir.Normalize();

        //현재 속도 가져오기
        Vector3 velocity = rb.linearVelocity;

        //수평 이동 세팅
        velocity.x = dir.x * moveSpeed;
        velocity.z = dir.z * moveSpeed;

        rb.linearVelocity = velocity;
    }

    void HandleJump()
    {
        if (!isGrounded) return;

        Vector3 velocity = rb.linearVelocity;

        //만약 바닥에서 아주 약간 하강 속도가 남아있으면 제거 후 점프
        if(velocity.y < 0f) velocity.y = 0f;

        velocity.y = jumpVelocity;
        rb.linearVelocity = velocity;

        isGrounded = false;
    }

    void ApplyGravity()
    {
        rb.AddForce(gravityDir * gravityPower, ForceMode.Acceleration);
    }

    void CheckGround()
    {
        isGrounded = Physics.Raycast(
            transform.position,
            gravityDir,
            1.1f,                       //캡슐 중심에서 아래로 쏴서 1보다 살짝 큰값으로 바닥 있는지 확인
            groundLayer
                );
    }
}
