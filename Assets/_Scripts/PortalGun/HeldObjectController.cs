// HeldObjectController.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PortalGunController))]
[RequireComponent(typeof(PortalRaycaster))]
[RequireComponent(typeof(HeldObjectMotorSpring))]
public class HeldObjectController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PortalGunController ctx;
    [SerializeField] private PortalRaycaster raycaster;
    [SerializeField] private HeldObjectMotorSpring motor;

    [Header("Hold Rotation (Portal1-like)")]
    [SerializeField, Range(1f, 45f)] private float snapAngleDeg = 15f;
    [SerializeField] private bool snapFaceTowardPlayer = true;
    [SerializeField] private bool useYawOnlyFrame = true;

    [Header("Collision")]
    [SerializeField] private bool ignoreCollisionWithPlayerWhileHolding = true;

    [Header("Micro Snap (너가 태그한 해결방안)")]
    [Tooltip("타겟에 거의 붙었을 때 미세 오차를 스냅으로 제거(Portal1 느낌)")]
    [SerializeField] private bool enableMicroSnap = true;

    [Tooltip("COM 기준 거리 오차(m) 이하면 스냅 후보. 0.01~0.02 추천")]
    [SerializeField] private float snapPosError = 0.015f;

    [Tooltip("속도 오차(m/s) 이하면 스냅 후보. 0.15~0.35 추천")]
    [SerializeField] private float snapVelError = 0.25f;

    [Tooltip("스냅 시도 전에 충돌 검사(SweepTest)로 안전장치")]
    [SerializeField] private bool safeSweepBeforeSnap = true;

    public bool IsHolding => heldRb != null;

    private Rigidbody heldRb;
    private PortalTraveller heldTraveller;
    private PortalTraveller playerTraveller;

    private readonly List<Collider> heldCols = new();
    private readonly List<Collider> playerCols = new();

    // ===== ThroughPortal 상태 =====
    private bool holdingThroughPortal;
    private Portal holdingInPortal;   // 플레이어(holdPoint) 쪽
    private Portal holdingOutPortal;  // 오브젝트 쪽

    // ===== 현재 어느 공간인지 추적 =====
    private Portal playerSidePortal;
    private Portal objectSidePortal;

    private Quaternion holdRotOffset = Quaternion.identity;

    // FixedUpdate에서만 쓸 캐시
    private bool hasCachedTarget;
    private Vector3 cachedTargetPos;
    private Quaternion cachedTargetRot;
    private Vector3 cachedTargetVel;
    private Vector3 cachedTargetAngVel;

    // 잡는 동안 튜닝 백업/복원
    private float prevMaxAngularVel;
    private int prevSolverIter;
    private int prevSolverVelIter;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    private void Awake()
    {
        if (!ctx) ctx = GetComponent<PortalGunController>();
        if (!raycaster) raycaster = GetComponent<PortalRaycaster>();
        if (!motor) motor = GetComponent<HeldObjectMotorSpring>();

        CachePlayerColliders();
        CachePlayerTraveller();
    }

    private void OnEnable()
    {
        InputManager.OnInteract += ToggleHold;
    }

    private void OnDisable()
    {
        InputManager.OnInteract -= ToggleHold;
        UnbindHeldTraveller();
        UnbindPlayerTraveller();
    }

    public void ToggleHold()
    {
        if (IsHolding) Drop();
        else TryPickup();
    }

    private void TryPickup()
    {
        if (!ctx || !ctx.PlayerCamera || !ctx.HoldPoint) return;
        if (!raycaster.TryGetInteractHit(out var hit)) return;
        Pickup(hit);
    }

    private void Pickup(PortalRaycaster.InteractHit hit)
    {
        if (!hit.hit || hit.rb == null) return;

        heldRb = hit.rb;

        // 물리 튜닝(회전 떨림 완화)
        prevMaxAngularVel = heldRb.maxAngularVelocity;
        prevSolverIter = heldRb.solverIterations;
        prevSolverVelIter = heldRb.solverVelocityIterations;

        heldRb.maxAngularVelocity = 50f;
        heldRb.solverIterations = 12;
        heldRb.solverVelocityIterations = 12;

        heldCols.Clear();
        heldRb.GetComponentsInChildren(true, heldCols);

        if (ignoreCollisionWithPlayerWhileHolding)
            SetIgnorePlayerCollision(true);

        SetupGrabRotation(hit.rbHit);

        BindHeldTraveller();
        BindPlayerTraveller();

        // 공간 추적 초기화
        playerSidePortal = null;
        objectSidePortal = null;

        if (hit.throughPortal && hit.inPortal && hit.outPortal)
        {
            playerSidePortal = hit.inPortal;
            objectSidePortal = hit.outPortal;
        }

        RefreshThroughPortalState(force: true);

        ResetTargetCache();
        ForceCacheNow();
    }

    public void Drop()
    {
        if (!heldRb) return;

        if (ignoreCollisionWithPlayerWhileHolding)
            SetIgnorePlayerCollision(false);

        UnbindHeldTraveller();
        UnbindPlayerTraveller();

        heldRb.maxAngularVelocity = prevMaxAngularVel;
        heldRb.solverIterations = prevSolverIter;
        heldRb.solverVelocityIterations = prevSolverVelIter;

        heldRb = null;
        heldCols.Clear();

        holdingThroughPortal = false;
        holdingInPortal = null;
        holdingOutPortal = null;

        playerSidePortal = null;
        objectSidePortal = null;

        holdRotOffset = Quaternion.identity;

        ResetTargetCache();
    }

    private void FixedUpdate()
    {
        if (!IsHolding) return;

        RefreshThroughPortalState();

        // ThroughPortal인데 포탈이 깨지면 Drop
        if (holdingThroughPortal)
        {
            if (!holdingInPortal || !holdingOutPortal || !holdingInPortal.IsPlaced || !holdingOutPortal.IsPlaced)
            {
                Drop();
                return;
            }
        }

        if (!hasCachedTarget)
            ForceCacheNow();

        // ✅ (너가 태그한 해결방안) 거의 붙었으면 미세 스냅으로 떨림 제거
        if (enableMicroSnap && TryMicroSnapToTarget())
        {
            // 스냅 성공하면 이번 Fixed에서는 모터 보정 생략해도 됨(더 안정적)
            return;
        }

        motor.Apply(heldRb, cachedTargetPos, cachedTargetRot, cachedTargetVel, cachedTargetAngVel);
    }

    private void LateUpdate()
    {
        if (!IsHolding) return;

        RefreshThroughPortalState();
        CacheTargetFromTransforms();
    }

    // =============================
    // Micro Snap (Tagged fix)
    // =============================
    private bool TryMicroSnapToTarget()
    {
        if (!heldRb) return false;

        Vector3 com = heldRb.worldCenterOfMass;
        Vector3 posError = cachedTargetPos - com;

        float posErrSqr = posError.sqrMagnitude;
        float posThreshSqr = snapPosError * snapPosError;
        if (posErrSqr > posThreshSqr) return false;

        Vector3 velError = cachedTargetVel - heldRb.linearVelocity;
        float velErrSqr = velError.sqrMagnitude;
        float velThreshSqr = snapVelError * snapVelError;
        if (velErrSqr > velThreshSqr) return false;

        // 충돌 안전장치(작은 델타라도 벽 안으로 파고들 수 있으니)
        if (safeSweepBeforeSnap && posErrSqr > 1e-12f)
        {
            Vector3 dir = posError.normalized;
            float dist = Mathf.Sqrt(posErrSqr);

            // 경로에 뭔가 있으면 스냅 금지(모터로 부드럽게 해결)
            if (heldRb.SweepTest(dir, out _, dist, QueryTriggerInteraction.Ignore))
                return false;
        }

        // ✅ COM이 타겟에 딱 붙도록 rb.position을 같은 델타만큼 이동
        heldRb.position += posError;

        // 스냅 직후 속도도 타겟에 맞춰 흔들림 종료
        heldRb.linearVelocity = cachedTargetVel;
        heldRb.angularVelocity = Vector3.zero;

        // 다음 프레임 캐시 갱신
        ResetTargetCache();
        ForceCacheNow();
        return true;
    }

    // =============================
    // ThroughPortal 상태 갱신
    // =============================
    private void RefreshThroughPortalState(bool force = false)
    {
        bool wasThrough = holdingThroughPortal;

        if (playerSidePortal != null && objectSidePortal != null && playerSidePortal != objectSidePortal)
        {
            holdingThroughPortal = true;
            holdingInPortal = playerSidePortal;
            holdingOutPortal = objectSidePortal;
        }
        else
        {
            holdingThroughPortal = false;
            holdingInPortal = null;
            holdingOutPortal = null;
        }

        // split -> merged 순간 튐/빙글 방지
        if ((force || wasThrough) && !holdingThroughPortal)
        {
            if (playerSidePortal != null && objectSidePortal != null && playerSidePortal == objectSidePortal)
            {
                RebaseHoldRotationToCurrent();
                SnapHeldToHoldPoint();
            }
        }
    }

    private void RebaseHoldRotationToCurrent()
    {
        if (!heldRb) return;
        Quaternion frameRot = GetHoldFrameRotation();
        holdRotOffset = Quaternion.Inverse(frameRot) * heldRb.rotation;
    }

    private void SnapHeldToHoldPoint()
    {
        if (!heldRb || !ctx || !ctx.HoldPoint) return;

        Vector3 snapPos = ctx.HoldPoint.position;
        Vector3 baseVel = (ctx.PlayerRigidbody != null) ? ctx.PlayerRigidbody.linearVelocity : Vector3.zero;
        Vector3 snapVel = holdingThroughPortal ? TransformDirThroughPortal(baseVel, holdingInPortal.Plane, holdingOutPortal.Plane) : baseVel;

        heldRb.position = snapPos;
        heldRb.linearVelocity = snapVel;
        heldRb.angularVelocity = Vector3.zero;

        ResetTargetCache();
        ForceCacheNow();
    }

    // =============================
    // 캐시(중요: ThroughPortal이면 Velocity도 변환!)
    // =============================
    private void ResetTargetCache()
    {
        hasCachedTarget = false;
        cachedTargetPos = Vector3.zero;
        cachedTargetRot = Quaternion.identity;
        cachedTargetVel = Vector3.zero;
        cachedTargetAngVel = Vector3.zero;
    }

    private void ForceCacheNow()
    {
        CacheTargetFromTransforms(forceNoVelocity: true);
    }

    private void CacheTargetFromTransforms(bool forceNoVelocity = false)
    {
        if (!ctx || !ctx.HoldPoint) return;

        Vector3 targetPos = ctx.HoldPoint.position;

        Quaternion frameRot = GetHoldFrameRotation();
        Quaternion targetRot = frameRot * holdRotOffset;

        // 기본 targetVel: 플레이어 RB 속도
        Vector3 baseVel = (ctx.PlayerRigidbody != null) ? ctx.PlayerRigidbody.linearVelocity : Vector3.zero;
        Vector3 targetVel = baseVel;

        if (holdingThroughPortal)
        {
            targetPos = PortalMath.TransformPoint(targetPos, holdingInPortal.Plane, holdingOutPortal.Plane);
            targetRot = PortalMath.TransformRotation(targetRot, holdingInPortal.Plane, holdingOutPortal.Plane);

            // ✅ 핵심: 속도도 포탈 변환해야 떨림이 크게 줄어듦
            targetVel = TransformDirThroughPortal(baseVel, holdingInPortal.Plane, holdingOutPortal.Plane);
        }

        cachedTargetPos = targetPos;
        cachedTargetRot = targetRot;

        cachedTargetVel = forceNoVelocity ? Vector3.zero : targetVel;
        cachedTargetAngVel = Vector3.zero; // 안정 우선(필요하면 나중에만 추가)

        hasCachedTarget = true;
    }

    // in->out 방향 벡터 변환(PortalTraveller의 HalfTurn 규칙과 동일)
    private static Vector3 TransformDirThroughPortal(Vector3 dirWorld, Transform inT, Transform outT)
    {
        Vector3 local = inT.InverseTransformDirection(dirWorld);
        local = HalfTurn * local;
        return outT.TransformDirection(local);
    }

    // =============================
    // 워프 이벤트
    // =============================
    private void CachePlayerColliders()
    {
        playerCols.Clear();
        Rigidbody prb = ctx ? ctx.PlayerRigidbody : null;
        if (prb != null)
            prb.GetComponentsInChildren(true, playerCols);
    }

    private void CachePlayerTraveller()
    {
        if (ctx != null && ctx.PlayerRigidbody != null)
            playerTraveller = ctx.PlayerRigidbody.GetComponent<PortalTraveller>();
    }

    private void BindPlayerTraveller()
    {
        UnbindPlayerTraveller();
        CachePlayerTraveller();
        if (playerTraveller != null)
            playerTraveller.Warped += OnPlayerWarped;
    }

    private void UnbindPlayerTraveller()
    {
        if (playerTraveller != null)
            playerTraveller.Warped -= OnPlayerWarped;
    }

    private void BindHeldTraveller()
    {
        UnbindHeldTraveller();
        if (!heldRb) return;

        heldTraveller = heldRb.GetComponent<PortalTraveller>();
        if (heldTraveller != null)
            heldTraveller.Warped += OnHeldWarped;
    }

    private void UnbindHeldTraveller()
    {
        if (heldTraveller != null)
            heldTraveller.Warped -= OnHeldWarped;
        heldTraveller = null;
    }

    private void OnHeldWarped(Portal from, Portal to)
    {
        if (!IsHolding) return;

        objectSidePortal = to;
        if (playerSidePortal == null) playerSidePortal = from;

        RefreshThroughPortalState();
        ResetTargetCache();
        ForceCacheNow();
    }

    private void OnPlayerWarped(Portal from, Portal to)
    {
        if (!IsHolding) return;

        playerSidePortal = to;
        if (objectSidePortal == null) objectSidePortal = from;

        RefreshThroughPortalState();
        ResetTargetCache();
        ForceCacheNow();
    }

    // =============================
    // 회전 오프셋(Pickup 시)
    // =============================
    private void SetupGrabRotation(RaycastHit rbHit)
    {
        Quaternion frameRot = GetHoldFrameRotation();

        Vector3 localN = heldRb.transform.InverseTransformDirection(rbHit.normal).normalized;
        float cos = Mathf.Cos(snapAngleDeg * Mathf.Deg2Rad);

        float ax = Mathf.Abs(localN.x);
        float ay = Mathf.Abs(localN.y);
        float az = Mathf.Abs(localN.z);
        float m = Mathf.Max(ax, Mathf.Max(ay, az));

        bool snapped = (m >= cos);

        if (!snapped || !snapFaceTowardPlayer)
        {
            holdRotOffset = Quaternion.Inverse(frameRot) * heldRb.rotation;
            return;
        }

        Vector3 localAxis;
        if (m == ax) localAxis = (localN.x >= 0f) ? Vector3.right : Vector3.left;
        else if (m == ay) localAxis = (localN.y >= 0f) ? Vector3.up : Vector3.down;
        else localAxis = (localN.z >= 0f) ? Vector3.forward : Vector3.back;

        Vector3 holdForward = frameRot * Vector3.forward;

        Quaternion B = Quaternion.LookRotation(-holdForward, Vector3.up);
        Quaternion M = Quaternion.FromToRotation(localAxis, Vector3.forward);
        Quaternion desiredWorld = B * M;

        holdRotOffset = Quaternion.Inverse(frameRot) * desiredWorld;
    }

    private Quaternion GetHoldFrameRotation()
    {
        if (!ctx || !ctx.PlayerCamera) return transform.rotation;

        Transform camT = ctx.PlayerCamera.transform;

        if (!useYawOnlyFrame)
            return camT.rotation;

        Vector3 f = Vector3.ProjectOnPlane(camT.forward, Vector3.up);
        if (f.sqrMagnitude < 1e-6f) f = transform.forward;
        f.Normalize();

        return Quaternion.LookRotation(f, Vector3.up);
    }

    // =============================
    // 충돌 무시
    // =============================
    private void SetIgnorePlayerCollision(bool ignore)
    {
        if (heldCols.Count == 0 || playerCols.Count == 0) return;

        for (int i = 0; i < heldCols.Count; i++)
        {
            var hc = heldCols[i];
            if (!hc) continue;

            for (int j = 0; j < playerCols.Count; j++)
            {
                var pc = playerCols[j];
                if (!pc) continue;

                Physics.IgnoreCollision(hc, pc, ignore);
            }
        }
    }
}
