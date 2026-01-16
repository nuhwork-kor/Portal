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

    [Header("Camera Rotation Feed-Forward")]
    [SerializeField] private bool useCameraRotationVelocity = true;
    [SerializeField] private float maxCameraOmega = 60f; // rad/s clamp

    public bool IsHolding => heldRb != null;

    private Rigidbody heldRb;
    private PortalTraveller heldTraveller;
    private PortalTraveller playerTraveller;

    private readonly List<Collider> heldCols = new();
    private readonly List<Collider> playerCols = new();

    // Through-portal holding state
    private bool holdingThroughPortal;
    private Portal holdingInPortal;
    private Portal holdingOutPortal;
    private Portal playerSidePortal;
    private Portal objectSidePortal;

    private Quaternion holdRotOffset = Quaternion.identity;

    // Restore rb params
    private float prevMaxAngularVel;
    private int prevSolverIter;
    private int prevSolverVelIter;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    // ===== Camera omega cached per frame (Update) =====
    private Vector3 cachedCamOmega;                 // world rad/s
    private Quaternion prevCamRotFrame;
    private bool hasPrevCamRotFrame;

    private void Awake()
    {
        if (!ctx) ctx = GetComponent<PortalGunController>();
        if (!raycaster) raycaster = GetComponent<PortalRaycaster>();
        if (!motor) motor = GetComponent<HeldObjectMotorSpring>();

        CachePlayerColliders();
        CachePlayerTraveller();
    }

    private void OnEnable() => InputManager.OnInteract += ToggleHold;

    private void OnDisable()
    {
        InputManager.OnInteract -= ToggleHold;
        UnbindHeldTraveller();
        UnbindPlayerTraveller();
    }

    private void Update()
    {
        // ✅ omega는 프레임(Update)에서만 계산해서 Fixed에서 튀는 현상 제거
        if (!IsHolding || !useCameraRotationVelocity || ctx == null || ctx.PlayerCamera == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            cachedCamOmega = Vector3.zero;
            return;
        }

        Quaternion now = ctx.PlayerCamera.transform.rotation;

        if (!hasPrevCamRotFrame)
        {
            prevCamRotFrame = now;
            hasPrevCamRotFrame = true;
            cachedCamOmega = Vector3.zero;
            return;
        }

        Quaternion dq = now * Quaternion.Inverse(prevCamRotFrame);
        prevCamRotFrame = now;

        dq.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (axis.sqrMagnitude < 1e-8f)
        {
            cachedCamOmega = Vector3.zero;
            return;
        }

        if (angleDeg > 180f) angleDeg -= 360f;

        float angleRad = angleDeg * Mathf.Deg2Rad;
        axis.Normalize();

        Vector3 omega = axis * (angleRad / Mathf.Max(1e-6f, dt)); // rad/s

        float mag = omega.magnitude;
        if (mag > maxCameraOmega)
            omega *= (maxCameraOmega / mag);

        cachedCamOmega = omega;
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

        // 안정화 세팅
        prevMaxAngularVel = heldRb.maxAngularVelocity;
        prevSolverIter = heldRb.solverIterations;
        prevSolverVelIter = heldRb.solverVelocityIterations;

        heldRb.maxAngularVelocity = 50f;
        heldRb.solverIterations = 12;
        heldRb.solverVelocityIterations = 12;
        heldRb.interpolation = RigidbodyInterpolation.Interpolate;

        heldCols.Clear();
        heldRb.GetComponentsInChildren(true, heldCols);

        if (ignoreCollisionWithPlayerWhileHolding)
            SetIgnorePlayerCollision(true);

        SetupGrabRotation(hit.rbHit);

        BindHeldTraveller();
        BindPlayerTraveller();

        // split 상태 초기화
        playerSidePortal = null;
        objectSidePortal = null;

        if (hit.throughPortal && hit.inPortal && hit.outPortal)
        {
            playerSidePortal = hit.inPortal;
            objectSidePortal = hit.outPortal;
        }

        RefreshThroughPortalState(force: true);

        PrimeCameraOmegaHistory();
        SnapHeldToHoldPoint();
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

        hasPrevCamRotFrame = false;
        cachedCamOmega = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (!IsHolding) return;
        if (!ctx || !ctx.HoldPoint) { Drop(); return; }

        RefreshThroughPortalState();

        if (holdingThroughPortal)
        {
            if (!holdingInPortal || !holdingOutPortal || !holdingInPortal.IsPlaced || !holdingOutPortal.IsPlaced)
            {
                Drop();
                return;
            }
        }

        ComputeTargetFixed(out var targetPos, out var targetRot, out var targetVel, out var targetAngVel);
        motor.Apply(heldRb, targetPos, targetRot, targetVel, targetAngVel);
    }

    // =============================
    // ThroughPortal state
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

        // split -> merged 순간: 기준 재정렬 + 스파이크 방지 + 스냅
        if ((force || wasThrough) && !holdingThroughPortal)
        {
            if (playerSidePortal != null && objectSidePortal != null && playerSidePortal == objectSidePortal)
            {
                RebaseHoldRotationToCurrent();
                PrimeCameraOmegaHistory();
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

        Vector3 targetPos = ctx.HoldPoint.position;
        Quaternion targetRot = GetHoldFrameRotation() * holdRotOffset;

        Vector3 targetVel = (ctx.PlayerRigidbody != null) ? ctx.PlayerRigidbody.linearVelocity : Vector3.zero;

        PrimeCameraOmegaHistory();

        if (holdingThroughPortal && holdingInPortal && holdingOutPortal)
        {
            targetPos = PortalMath.TransformPoint(targetPos, holdingInPortal.Plane, holdingOutPortal.Plane);
            targetRot = PortalMath.TransformRotation(targetRot, holdingInPortal.Plane, holdingOutPortal.Plane);
            targetVel = TransformDirection(targetVel, holdingInPortal.Plane, holdingOutPortal.Plane);
        }

        motor.Snap(heldRb, targetPos, targetRot, targetVel);
    }

    private void ComputeTargetFixed(out Vector3 targetPos, out Quaternion targetRot, out Vector3 targetVel, out Vector3 targetAngVel)
    {
        Vector3 basePos = ctx.HoldPoint.position;

        Quaternion frameRot = GetHoldFrameRotation();
        Quaternion baseRot = frameRot * holdRotOffset;

        Vector3 baseVel = (ctx.PlayerRigidbody != null) ? ctx.PlayerRigidbody.linearVelocity : Vector3.zero;

        // ✅ 프레임에서 캐시된 omega로 회전 유도 속도(ω×r) 추가
        Vector3 velFromRot = Vector3.zero;
        if (useCameraRotationVelocity && ctx.PlayerCamera != null)
        {
            Transform camT = ctx.PlayerCamera.transform;
            Vector3 r = basePos - camT.position;
            velFromRot = Vector3.Cross(cachedCamOmega, r);
        }

        Vector3 baseTargetVel = baseVel + velFromRot;

        targetPos = basePos;
        targetRot = baseRot;
        targetVel = baseTargetVel;
        targetAngVel = Vector3.zero; // 안정 우선

        if (holdingThroughPortal && holdingInPortal && holdingOutPortal)
        {
            targetPos = PortalMath.TransformPoint(basePos, holdingInPortal.Plane, holdingOutPortal.Plane);
            targetRot = PortalMath.TransformRotation(baseRot, holdingInPortal.Plane, holdingOutPortal.Plane);
            targetVel = TransformDirection(baseTargetVel, holdingInPortal.Plane, holdingOutPortal.Plane);
        }
    }

    private void PrimeCameraOmegaHistory()
    {
        if (ctx != null && ctx.PlayerCamera != null)
        {
            prevCamRotFrame = ctx.PlayerCamera.transform.rotation;
            hasPrevCamRotFrame = true;
            cachedCamOmega = Vector3.zero;
        }
        else
        {
            hasPrevCamRotFrame = false;
            cachedCamOmega = Vector3.zero;
        }
    }

    private static Vector3 TransformDirection(Vector3 dir, Transform inPlane, Transform outPlane)
    {
        Vector3 rel = inPlane.InverseTransformDirection(dir);
        rel = HalfTurn * rel;
        return outPlane.TransformDirection(rel);
    }

    // =============================
    // Warp events
    // =============================
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

        RefreshThroughPortalState(force: true);
        PrimeCameraOmegaHistory();
        SnapHeldToHoldPoint();
    }

    private void OnPlayerWarped(Portal from, Portal to)
    {
        if (!IsHolding) return;

        playerSidePortal = to;
        if (objectSidePortal == null) objectSidePortal = from;

        RefreshThroughPortalState(force: true);
        PrimeCameraOmegaHistory();
        SnapHeldToHoldPoint();
    }

    // =============================
    // Rotation snap at pickup
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
    // Collision ignore
    // =============================
    private void CachePlayerColliders()
    {
        playerCols.Clear();
        Rigidbody prb = ctx ? ctx.PlayerRigidbody : null;
        if (prb != null)
            prb.GetComponentsInChildren(true, playerCols);
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
