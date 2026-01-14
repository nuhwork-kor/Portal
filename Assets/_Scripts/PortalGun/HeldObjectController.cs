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

    public bool IsHolding => heldRb != null;

    private Rigidbody heldRb;
    private PortalTraveller heldTraveller;

    // ✅ 추가: 플레이어 워프도 감지해야 "같은 공간으로 합쳐질 때" ThroughPortal을 끌 수 있음
    private PortalTraveller playerTraveller;

    private readonly List<Collider> heldCols = new();
    private readonly List<Collider> playerCols = new();

    // ======== ThroughPortal 상태 ========
    private bool holdingThroughPortal;
    private Portal holdingInPortal;   // "플레이어(holdPoint)가 있는 쪽" 포탈
    private Portal holdingOutPortal;  // "오브젝트가 있는 쪽" 포탈

    // ✅ 추가: 플레이어/오브젝트가 현재 어느 포탈쪽 공간인지 추적
    // - 같으면: 같은 공간(=ThroughPortal 필요 없음)
    // - 다르면: split 상태(=ThroughPortal로 타겟 변환 필요)
    private Portal playerSidePortal;
    private Portal objectSidePortal;

    private Quaternion holdRotOffset = Quaternion.identity;

    // ✅ Update/LateUpdate 캐시 (이 값만 FixedUpdate에서 사용)
    private bool hasCachedTarget;
    private Vector3 cachedTargetPos;
    private Quaternion cachedTargetRot;

    private Vector3 cachedTargetVel;
    private Vector3 cachedTargetAngVel;

    private Vector3 prevTargetPos;
    private Quaternion prevTargetRot;
    private float prevTargetTime;

    // HeldObjectController.cs 상단 멤버에 추가
    private float prevMaxAngularVel;
    private int prevSolverIter;
    private int prevSolverVelIter;

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
        CachePlayerTraveller();
        if (playerTraveller != null)
            playerTraveller.Warped += OnPlayerWarped;
    }

    private void UnbindPlayerTraveller()
    {
        if (playerTraveller != null)
            playerTraveller.Warped -= OnPlayerWarped;
    }

    public void ToggleHold()
    {
        if (IsHolding) Drop();
        else TryPickup();
    }

    private void TryPickup()
    {
        if (!ctx || !ctx.PlayerCamera || !ctx.HoldPoint) return;

        if (!raycaster.TryGetInteractHit(out var hit))
            return;

        Pickup(hit);
    }

    private void Pickup(PortalRaycaster.InteractHit hit)
    {
        if (!hit.hit || hit.rb == null) return;

        heldRb = hit.rb;

        prevMaxAngularVel = heldRb.maxAngularVelocity;
        prevSolverIter = heldRb.solverIterations;
        prevSolverVelIter = heldRb.solverVelocityIterations;

        // Portal류 홀드에서는 기본값(7rad/s)이 너무 낮아 떨림 원인이 됨
        heldRb.maxAngularVelocity = 50f;

        // 인스펙터에 안 보여도 여기서 올릴 수 있음
        heldRb.solverIterations = 12;
        heldRb.solverVelocityIterations = 12;

        // 콜라이더 캐시
        heldCols.Clear();
        heldRb.GetComponentsInChildren(true, heldCols);

        if (ignoreCollisionWithPlayerWhileHolding)
            SetIgnorePlayerCollision(true);

        SetupGrabRotation(hit.rbHit);

        BindHeldTraveller();
        BindPlayerTraveller();

        // ✅ pickup 시 "현재 split 상태" 초기화
        playerSidePortal = null;
        objectSidePortal = null;

        if (hit.throughPortal && hit.inPortal && hit.outPortal)
        {
            // 플레이어는 inPortal 쪽, 오브젝트는 outPortal 쪽에 있음
            playerSidePortal = hit.inPortal;
            objectSidePortal = hit.outPortal;
        }

        RefreshThroughPortalState(force: true);

        ResetTargetCache();
        motor.ResetTargetHistory();
        ForceCacheNow(); // 첫 프레임 튐 방지
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
        motor.ResetTargetHistory();
    }

    private void FixedUpdate()
    {
        if (!IsHolding) return;

        // 최신 상태 반영
        RefreshThroughPortalState();

        // ThroughPortal이면 링크 깨지면 Drop
        if (holdingThroughPortal)
        {
            if (!holdingInPortal || !holdingOutPortal || !holdingInPortal.IsPlaced || !holdingOutPortal.IsPlaced)
            {
                Drop();
                return;
            }
        }

        // ✅ Fixed는 캐시만 사용
        if (!hasCachedTarget)
            ForceCacheNow();

        motor.Apply(heldRb, cachedTargetPos, cachedTargetRot, cachedTargetVel, cachedTargetAngVel);
    }

    private void LateUpdate()
    {
        if (!IsHolding) return;

        RefreshThroughPortalState();
        CacheTargetFromTransforms();
    }

    // =============================
    // ✅ ThroughPortal 상태 갱신
    // =============================
    private void RefreshThroughPortalState(bool force = false)
    {
        // force는 pickup 직후처럼 "무조건 한번 정렬"이 필요할 때만 사용
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

        // ✅ split -> merged 로 바뀌는 순간(=같은 공간이 되는 순간) 튐/빙글빙글 방지 처리
        if ((force || wasThrough) && !holdingThroughPortal)
        {
            // 둘 다 값이 있고 같으면 "합쳐짐"으로 판단
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

        Vector3 snapVel = Vector3.zero;
        if (ctx.PlayerRigidbody != null)
            snapVel = ctx.PlayerRigidbody.linearVelocity;

        // 위치는 확정적으로 붙여버림 (Portal1 느낌)
        heldRb.position = snapPos;

        // 속도/각속도도 정리 (원형 이동/떨림/오차 누적 방지)
        heldRb.linearVelocity = snapVel;
        heldRb.angularVelocity = Vector3.zero;

        // 모터/캐시 히스토리 리셋
        motor.ResetTargetHistory();
        ResetTargetCache();
        ForceCacheNow();
    }

    // =============================
    // ✅ 핵심: LateUpdate 캐시 로직
    // =============================
    private void ResetTargetCache()
    {
        hasCachedTarget = false;
        cachedTargetPos = Vector3.zero;
        cachedTargetRot = Quaternion.identity;
        cachedTargetVel = Vector3.zero;
        cachedTargetAngVel = Vector3.zero;

        prevTargetTime = 0f;
        prevTargetPos = Vector3.zero;
        prevTargetRot = Quaternion.identity;
    }

    private void ForceCacheNow()
    {
        CacheTargetFromTransforms(forceNoVelocity: true);
    }

    private void CacheTargetFromTransforms(bool forceNoVelocity = false)
    {
        if (!ctx || !ctx.HoldPoint) return;

        // 1) 기본 타겟(플레이어 앞 HoldPoint)
        Vector3 targetPos = ctx.HoldPoint.position;

        Quaternion frameRot = GetHoldFrameRotation();
        Quaternion targetRot = frameRot * holdRotOffset;

        // 2) ThroughPortal이면 타겟을 포탈 변환한 위치로
        if (holdingThroughPortal)
        {
            targetPos = PortalMath.TransformPoint(targetPos, holdingInPortal.Plane, holdingOutPortal.Plane);
            targetRot = PortalMath.TransformRotation(targetRot, holdingInPortal.Plane, holdingOutPortal.Plane);
        }

        float t = Time.time;

        if (forceNoVelocity || prevTargetTime <= 0f)
        {
            cachedTargetVel = Vector3.zero;
            cachedTargetAngVel = Vector3.zero;

            prevTargetPos = targetPos;
            prevTargetRot = targetRot;
            prevTargetTime = t;
        }
        else
        {
            float dt = t - prevTargetTime;
            if (dt < 1e-6f) dt = 1e-6f;

            cachedTargetVel = (targetPos - prevTargetPos) / dt;
            cachedTargetAngVel = CalcAngularVelocity(prevTargetRot, targetRot, dt);

            prevTargetPos = targetPos;
            prevTargetRot = targetRot;
            prevTargetTime = t;
        }

        cachedTargetPos = targetPos;
        cachedTargetRot = targetRot;
        hasCachedTarget = true;
    }

    private static Vector3 CalcAngularVelocity(Quaternion prev, Quaternion cur, float dt)
    {
        Quaternion dq = cur * Quaternion.Inverse(prev);
        dq.ToAngleAxis(out float angleDeg, out Vector3 axis);

        if (axis.sqrMagnitude < 1e-8f) return Vector3.zero;
        if (angleDeg > 180f) angleDeg -= 360f;

        axis.Normalize();
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return axis * (angleRad / Mathf.Max(1e-6f, dt));
    }

    // =============================
    // ✅ Held 오브젝트 워프 대응
    // =============================
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

        // object는 to 쪽 공간으로 이동
        objectSidePortal = to;

        // playerSide가 아직 미정이면, object가 떠난 쪽(from)을 playerSide로 가정
        if (playerSidePortal == null)
            playerSidePortal = from;

        // 상태 갱신 + 캐시 리셋
        bool wasThrough = holdingThroughPortal;
        RefreshThroughPortalState();

        ResetTargetCache();
        motor.ResetTargetHistory();
        ForceCacheNow();
    }

    // =============================
    // ✅ Player 워프 대응 (여기가 이번 문제의 핵심)
    // =============================
    private void OnPlayerWarped(Portal from, Portal to)
    {
        if (!IsHolding) return;

        // player는 to 쪽 공간으로 이동
        playerSidePortal = to;

        // objectSide가 아직 미정이면, player가 떠난 쪽(from)을 objectSide로 가정
        if (objectSidePortal == null)
            objectSidePortal = from;

        // ✅ 여기서 merged가 되면 즉시 SnapHeldToHoldPoint()가 걸려서
        // "포탈 한바퀴 돌고 붙는" 현상이 사라짐
        RefreshThroughPortalState();

        ResetTargetCache();
        motor.ResetTargetHistory();
        ForceCacheNow();
    }

    // =============================
    // Rotation: pickup-time snap/keep
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
