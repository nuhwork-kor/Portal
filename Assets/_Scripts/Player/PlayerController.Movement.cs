// - 플레이어 이동/점프/중력/접지 체크 및 발자국 SFX를 담당하는 PlayerController의 Partial.
// - Update에서 큐잉된 점프(jumpQueued)를 FixedUpdate(TickMove)에서 처리한다.
using UnityEngine;                                                  // Rigidbody, Physics, LayerMask 등

public partial class PlayerController
{
    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 10f;                 // 기본 이동 속도
    [SerializeField] private float airControlMultiplier = 0.4f;     // 공중 제어(지상 대비 비율)

    [Header("Jump/Gravity")]
    [SerializeField] private float jumpVelocity = 6f;               // 점프 순간 y 속도
    [SerializeField] private float gravityPower = 20f;              // 커스텀 중력 가속도 크기

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;                  // 접지 판정 레이어(바닥/천장 포함 Ground 레이어 기준)
    [SerializeField] private float groundCheckDistance = 0.15f;     // 추가 접지 거리(여유)
    [SerializeField] private float groundCheckRadius = 0.25f;       // 접지 스피어 반경(캡슐 반경보다 작게 추천)

    [Header("Footstep SFX")]
    [SerializeField] private float footstepInterval = 0.42f;        // 발자국 재생 간격(초)
    [SerializeField] private float footstepMinSpeed = 0.25f;        // 너무 느릴 때 발자국 방지 최소 속도

    private bool isGrounded;                                        // 현재 접지 여부
    private bool jumpQueued;                                        // 점프 입력이 들어왔는지(큐잉)

    private bool _wasGrounded;                                      // 이전 Fixed 프레임 접지 여부(착지 감지용)
    private float _footstepT;                                       // 발자국 타이머

    /// <summary>
    /// Movement 파트 초기화.
    /// 착지 상태/발자국 타이머를 리셋한다.
    /// </summary>
    private void InitMove()
    {
        _wasGrounded = false;                                       // 이전 접지 상태 초기화
        _footstepT = 0f;                                            // 발자국 타이머 초기화
    }

    /// <summary>
    /// Jump 입력 이벤트에서 호출된다.
    /// FixedUpdate에서 처리하도록 jumpQueued를 true로 설정한다.
    /// </summary>
    private void QueueJump()
    {
        jumpQueued = true;                                          // 점프 큐잉
    }

    /// <summary>
    /// FixedUpdate에서 호출되는 이동/점프/중력 메인 루프.
    /// </summary>
    /// <param name="move">이동 입력(Vector2)</param>
    private void TickMove(Vector2 move)
    {
        _wasGrounded = isGrounded;                                  // 이전 접지 상태 저장

        CheckGround();                                              // 현재 접지 체크

        if (!_wasGrounded && isGrounded)                            // 공중 -> 지상 전환(착지)
            SoundManager.PlaySFX(SfxId.Player_Land);                 // 착지 SFX

        ApplyMove(move);                                            // 이동 적용(수평 속도)

        TickFootstepSfx(move);                                      // 발자국 SFX(지상+이동 중)

        if (jumpQueued)                                             // 점프 입력이 큐에 있으면
        {
            jumpQueued = false;                                     // 큐 소모
            ApplyJump();                                            // 점프 적용
        }

        ApplyGravity();                                             // 커스텀 중력 적용

        rb.angularVelocity = Vector3.zero;                          // 물리 각속도 강제 0(뒤집힘/회전 방지)
    }

    /// <summary>
    /// 발자국 SFX를 재생한다.
    /// 지상에서 실제로 이동 중이고, 일정 간격이 지나면 SFX를 재생한다.
    /// </summary>
    /// <param name="move">이동 입력(Vector2)</param>
    private void TickFootstepSfx(Vector2 move)
    {
        if (!isGrounded) { _footstepT = 0f; return; }               // 공중이면 타이머 리셋

        Vector3 v = rb.linearVelocity;                              // 현재 속도
        float planarSpeed = new Vector2(v.x, v.z).magnitude;         // 수평 속도 크기

        if (planarSpeed < footstepMinSpeed || move.sqrMagnitude < 0.0001f) // 너무 느리거나 입력 없음
        {
            _footstepT = 0f;                                        // 타이머 리셋
            return;                                                 // 발자국 재생 안 함
        }

        _footstepT += Time.deltaTime;                               // 타이머 증가(프레임 시간)
        if (_footstepT >= footstepInterval)                         // 간격 도달
        {
            _footstepT = 0f;                                        // 타이머 리셋
            SoundManager.PlaySFX(SfxId.Player_Walk);                 // 발자국 SFX
        }
    }

    /// <summary>
    /// 이동 입력에 따라 Rigidbody의 수평 속도를 설정한다.
    /// 지상에서는 즉시 반응, 공중에서는 airControlMultiplier만큼 제어를 약화한다.
    /// </summary>
    /// <param name="move">이동 입력(Vector2)</param>
    private void ApplyMove(Vector2 move)
    {
        Vector3 v = rb.linearVelocity;                              // 현재 속도

        if (move.sqrMagnitude < 0.0001f)                            // 입력이 거의 없으면
        {
            if (isGrounded)
            {
                v.x = 0f;                                           // 지상에서 x 속도 정지
                v.z = 0f;                                           // 지상에서 z 속도 정지
                rb.linearVelocity = v;                              // 속도 적용
            }
            return;                                                 // 이동 처리 종료
        }

        Vector3 forward = moveBasis.forward;                        // 이동 기준 forward
        Vector3 right = moveBasis.right;                            // 이동 기준 right

        forward.y = 0f;                                             // 수평 이동만
        right.y = 0f;                                               // 수평 이동만
        forward.Normalize();                                        // 정규화
        right.Normalize();                                          // 정규화

        Vector3 dir = forward * move.y + right * move.x;            // 입력 기반 이동 방향
        if (dir.sqrMagnitude > 1f) dir.Normalize();                 // 대각선 정규화

        float control = isGrounded ? 1f : airControlMultiplier;     // 접지/공중 제어 계수

        v.x = dir.x * moveSpeed * control;                          // 수평 x 속도 설정
        v.z = dir.z * moveSpeed * control;                          // 수평 z 속도 설정

        rb.linearVelocity = v;                                      // 속도 적용
    }

    /// <summary>
    /// 접지 상태에서만 점프 속도를 부여한다.
    /// </summary>
    private void ApplyJump()
    {
        if (!isGrounded) return;                                    // 공중이면 점프 불가

        Vector3 v = rb.linearVelocity;                              // 현재 속도
        if (v.y < 0f) v.y = 0f;                                      // 하강 중이면 y 속도 초기화
        v.y = jumpVelocity;                                         // 점프 y 속도 부여
        rb.linearVelocity = v;                                      // 속도 적용

        isGrounded = false;                                         // 즉시 공중 상태로 전환
    }

    /// <summary>
    /// 커스텀 중력을 Acceleration으로 적용한다.
    /// </summary>
    private void ApplyGravity()
    {
        rb.AddForce(Vector3.down * gravityPower, ForceMode.Acceleration); // 아래 방향 가속도 적용
    }

    /// <summary>
    /// 캡슐 콜라이더를 기준으로 아래 방향 SphereCast를 수행해 접지 여부를 갱신한다.
    /// </summary>
    private void CheckGround()
    {
        if (!capsule)
        {
            isGrounded = false;                                     // 캡슐 없으면 접지 불가
            return;
        }

        Transform t = capsule.transform;                            // 캡슐 트랜스폼
        Vector3 center = t.TransformPoint(capsule.center);          // 월드 중심점

        float scaleXZ = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.z)); // xz 스케일
        float scaleY = Mathf.Abs(t.lossyScale.y);                   // y 스케일

        float capsuleRadius = capsule.radius * scaleXZ;             // 월드 반경
        float halfHeight = Mathf.Max((capsule.height * 0.5f) * scaleY, capsuleRadius); // 반높이(반경 이상 보장)

        float castRadius = Mathf.Min(groundCheckRadius, capsuleRadius) * 0.95f;        // 캐스트 반경(여유)
        float castDistance = (halfHeight - castRadius) + groundCheckDistance;          // 중심에서 아래로 쏠 거리

        isGrounded = Physics.SphereCast(
            center,                                                 // 시작점
            castRadius,                                             // 반경
            Vector3.down,                                           // 방향
            out _,                                                  // 히트 정보(여기선 필요 없음)
            castDistance,                                           // 거리
            groundMask,                                             // 바닥 마스크
            QueryTriggerInteraction.Ignore                           // 트리거 무시
        );
    }

    /// <summary>
    /// Rigidbody의 속도를 외부에서 강제로 설정한다.
    /// (예: 포탈 워프 직후 속도 세팅)
    /// </summary>
    /// <param name="newVel">설정할 새 속도(월드)</param>
    public void SetVelocity(Vector3 newVel)
    {
        rb.linearVelocity = newVel;                                 // 속도 강제 적용
    }

    public bool IsGrounded => isGrounded;                           // 현재 접지 여부 읽기 프로퍼티
    public Rigidbody RB => rb;                                      // Rigidbody 외부 접근 프로퍼티
}
