using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PortalTraveller : MonoBehaviour
{
    [Header("Warp Rule")]
    [SerializeField] private float planeCrossEpsilon = 0.02f;
    [SerializeField] private float teleportCooldown = 0.05f;

    [Header("Tuning")]
    [SerializeField] private float exitOffset = 0.0f;

    private Rigidbody rb;
    private Collider col;
    private CapsuleCollider capsule;

    private Portal inPortal;
    private Portal outPortal;
    private Collider wallCollider;

    private int insideCount = 0;
    private int entrySideSign = +1;
    private float cooldownUntil = 0f;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    private PlayerMovement movement;
    private PlayerMouseLook mouseLook;
    private PortalCloneVisual cloneVisual;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        capsule = GetComponent<CapsuleCollider>();

        movement = GetComponent<PlayerMovement>();
        mouseLook = GetComponent<PlayerMouseLook>();

        cloneVisual = GetComponent<PortalCloneVisual>();
    }

    private void FixedUpdate()
    {
        if (insideCount <= 0) return;
        if (inPortal == null || outPortal == null) return;
        if (Time.time < cooldownUntil) return;

        if (HasCenterCrossedPlane(inPortal.Plane, entrySideSign))
            WarpNow();
    }

    public void EnterPortal(Portal inP, Portal outP, Collider inWall)
    {
        if (Time.time < cooldownUntil) return;
        if (!inP || !outP) return;

        // 다른 포탈로 들어온 경우: 기존 상태를 정리
        if (insideCount > 0 && inPortal != null && inPortal != inP)
        {
            ForceClearState();
        }

        if (insideCount == 0)
        {
            inPortal = inP;
            outPortal = outP;

            SetIgnoredWall(inWall);

            float d = SignedDistanceToPlane(inPortal.Plane, GetCenterWorld());
            entrySideSign = SignWithEps(d, +1);
        }
        else
        {
            if (inPortal == inP && inWall != null && inWall != wallCollider)
            {
                SetIgnoredWall(inWall);
            }

            outPortal = outP;
        }

        insideCount++;
    }

    public void NotifyTriggerExit(Portal exitedPortal)
    {
        if (exitedPortal != null && inPortal != null && exitedPortal != inPortal)
            return;

        ExitPortalInternal();
    }

    private void ExitPortalInternal()
    {
        if (insideCount <= 0) return;

        insideCount = Mathf.Max(insideCount - 1, 0);
        if (insideCount > 0) return;

        ForceClearState();
    }

    private void WarpNow()
    {
        Portal oldInPortal = inPortal;
        Portal oldOutPortal = outPortal;
        Collider oldInWall = wallCollider;

        Warp();

        Collider newWall = null;
        if (oldOutPortal != null)
            newWall = oldOutPortal.WallColliderCached;

        if (oldInWall) Physics.IgnoreCollision(col, oldInWall, false);

        wallCollider = null;
        SetIgnoredWall(newWall);

        cooldownUntil = Time.time + teleportCooldown;

        // 포탈쌍 스왑 유지
        inPortal = oldOutPortal;
        outPortal = oldInPortal;

        insideCount = Mathf.Max(insideCount, 1);

        if (inPortal != null)
        {
            float d = SignedDistanceToPlane(inPortal.Plane, GetCenterWorld());
            entrySideSign = SignWithEps(d, +1);
        }
        else
        {
            ForceClearState();
            return;
        }

        // ✅ 비주얼 클론도 “현재 포탈쌍”으로 갱신
        if (cloneVisual != null)
            cloneVisual.OnWarped(inPortal, outPortal);
    }

    private void ForceClearState()
    {
        if (wallCollider)
            Physics.IgnoreCollision(col, wallCollider, false);

        inPortal = null;
        outPortal = null;
        wallCollider = null;
        entrySideSign = +1;
        insideCount = 0;
    }

    private void SetIgnoredWall(Collider newWall)
    {
        if (wallCollider == newWall) return;

        if (wallCollider)
            Physics.IgnoreCollision(col, wallCollider, false);

        wallCollider = newWall;

        if (wallCollider)
            Physics.IgnoreCollision(col, wallCollider, true);
    }

    private Vector3 GetCenterWorld()
    {
        if (capsule) return transform.TransformPoint(capsule.center);
        if (col) return col.bounds.center;
        if (rb) return rb.worldCenterOfMass;
        return transform.position;
    }

    private static float SignedDistanceToPlane(Transform plane, Vector3 worldPoint)
    {
        return Vector3.Dot(plane.forward, worldPoint - plane.position);
    }

    private static int SignWithEps(float d, int defaultSign)
    {
        if (Mathf.Abs(d) < 1e-6f) return defaultSign;
        return (d > 0f) ? +1 : -1;
    }

    private bool HasCenterCrossedPlane(Transform plane, int entrySign)
    {
        if (!plane) return false;

        float d = SignedDistanceToPlane(plane, GetCenterWorld());
        return (d * entrySign) <= -planeCrossEpsilon;
    }

    private void Warp()
    {
        Transform inT = inPortal.Plane;
        Transform outT = outPortal.Plane;

        Vector3 center = GetCenterWorld();
        Vector3 pivotToCenter = center - transform.position;

        Vector3 relativePos = inT.InverseTransformPoint(center);
        relativePos = HalfTurn * relativePos;
        Vector3 newCenter = outT.TransformPoint(relativePos);

        if (exitOffset != 0f)
            newCenter += outT.forward * exitOffset;

        Vector3 newPos = newCenter - pivotToCenter;

        Quaternion relativeRot = Quaternion.Inverse(inT.rotation) * transform.rotation;
        relativeRot = HalfTurn * relativeRot;
        Quaternion newWorldRot = outT.rotation * relativeRot;

        Vector3 relativeVel = inT.InverseTransformDirection(rb.linearVelocity);
        relativeVel = HalfTurn * relativeVel;
        Vector3 newVel = outT.TransformDirection(relativeVel);

        rb.position = newPos;
        transform.position = newPos;

        Vector3 fwd = newWorldRot * Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 1e-6f)
        {
            Quaternion yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            if (mouseLook != null) mouseLook.ForceSetYaw(yaw);
            else transform.rotation = yaw;

            if (mouseLook != null) mouseLook.SyncPitchFromCamera();
        }

        if (movement != null) movement.SetVelocity(newVel);
        else rb.linearVelocity = newVel;
    }
}
