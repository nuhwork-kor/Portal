using System;
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

    [Header("Impact SFX (optional)")]
    [Tooltip("충돌 속도가 이 이상이면 ObjImpact_Cube 재생")]
    [SerializeField] private float impactMinSpeed = 1.5f;

    private Rigidbody rb;
    private Collider col;
    private CapsuleCollider capsule;

    private Portal inPortal;
    private Portal outPortal;

    private Collider wallCollider;
    private Collider inSurfaceCol;
    private Collider outSurfaceCol;

    private int insideCount = 0;
    private int entrySideSign = +1;
    private float cooldownUntil = 0f;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    private PlayerController playerController;
    private PortalCloneVisual cloneVisual;

    public event Action<Portal, Portal> Warped;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        capsule = GetComponent<CapsuleCollider>();

        playerController = GetComponent<PlayerController>();
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

        if (insideCount > 0 && inPortal != null && inPortal != inP)
        {
            ForceClearState();
        }

        if (insideCount == 0)
        {
            inPortal = inP;
            outPortal = outP;

            SetIgnoredWall(inWall);
            SetIgnoredPortalSurfaces(inPortal, outPortal);

            float d = SignedDistanceToPlane(inPortal.Plane, GetCenterWorld());
            entrySideSign = SignWithEps(d, +1);
        }
        else
        {
            if (inPortal == inP && inWall != null && inWall != wallCollider)
                SetIgnoredWall(inWall);

            outPortal = outP;
            SetIgnoredPortalSurfaces(inPortal, outPortal);
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

        // ✅ SFX: 포탈 진입(워프 발생 순간)
        if (oldInPortal != null)
        {
            // Portal에 색/타입 값이 있다면 그걸 쓰는 게 베스트.
            // 지금은 이름 기반으로 안전하게 처리(애매하면 애매할수도있는데, 네 네이밍이 바뀌면 여기 수정 필요)
            string n = oldInPortal.name.ToLowerInvariant();
            if (n.Contains("blue"))
                SoundManager.PlaySFX(SfxId.Portal_BlueEnter);
            else
                SoundManager.PlaySFX(SfxId.Portal_OrangeEnter);
        }

        Warped?.Invoke(oldInPortal, oldOutPortal);

        Collider newWall = null;
        if (oldOutPortal != null)
            newWall = oldOutPortal.WallColliderCached;

        if (oldInWall) Physics.IgnoreCollision(col, oldInWall, false);

        wallCollider = null;
        SetIgnoredWall(newWall);

        cooldownUntil = Time.time + teleportCooldown;

        inPortal = oldOutPortal;
        outPortal = oldInPortal;

        SetIgnoredPortalSurfaces(inPortal, outPortal);

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

        if (cloneVisual != null)
            cloneVisual.OnWarped(inPortal, outPortal);
    }

    private void ForceClearState()
    {
        if (wallCollider)
            Physics.IgnoreCollision(col, wallCollider, false);

        if (inSurfaceCol) Physics.IgnoreCollision(col, inSurfaceCol, false);
        if (outSurfaceCol) Physics.IgnoreCollision(col, outSurfaceCol, false);
        inSurfaceCol = null;
        outSurfaceCol = null;

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

    private void SetIgnoredPortalSurfaces(Portal inP, Portal outP)
    {
        Collider newIn = (inP != null) ? inP.SurfaceCollider : null;
        Collider newOut = (outP != null) ? outP.SurfaceCollider : null;

        if (inSurfaceCol != newIn)
        {
            if (inSurfaceCol) Physics.IgnoreCollision(col, inSurfaceCol, false);
            inSurfaceCol = newIn;
            if (inSurfaceCol) Physics.IgnoreCollision(col, inSurfaceCol, true);
        }

        if (outSurfaceCol != newOut)
        {
            if (outSurfaceCol) Physics.IgnoreCollision(col, outSurfaceCol, false);
            outSurfaceCol = newOut;
            if (outSurfaceCol) Physics.IgnoreCollision(col, outSurfaceCol, true);
        }
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

        Quaternion viewBefore = Quaternion.identity;
        Vector3 stableYawBefore = Vector3.forward;

        if (playerController != null)
        {
            viewBefore = playerController.GetViewWorldRotation();
            stableYawBefore = playerController.GetStableYawForward();
        }

        Vector3 center = GetCenterWorld();
        Vector3 pivotToCenter = center - transform.position;

        Vector3 relativePos = inT.InverseTransformPoint(center);
        relativePos = HalfTurn * relativePos;
        Vector3 newCenter = outT.TransformPoint(relativePos);

        if (exitOffset != 0f)
            newCenter += outT.forward * exitOffset;

        Vector3 newPos = newCenter - pivotToCenter;

        Vector3 relativeVel = inT.InverseTransformDirection(rb.linearVelocity);
        relativeVel = HalfTurn * relativeVel;
        Vector3 newVel = outT.TransformDirection(relativeVel);

        rb.position = newPos;
        transform.position = newPos;

        if (playerController != null)
        {
            Quaternion viewAfter =
                outT.rotation *
                HalfTurn *
                Quaternion.Inverse(inT.rotation) *
                viewBefore;

            Vector3 stableYawAfter = TransformDirectionThroughPortal(stableYawBefore, inT, outT);

            playerController.ApplyViewAfterUpright(viewAfter, stableYawAfter);
            playerController.SetVelocity(newVel);
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            Quaternion relativeRot = Quaternion.Inverse(inT.rotation) * transform.rotation;
            relativeRot = HalfTurn * relativeRot;
            Quaternion newWorldRot = outT.rotation * relativeRot;
            transform.rotation = newWorldRot;

            Vector3 relAng = inT.InverseTransformDirection(rb.angularVelocity);
            relAng = HalfTurn * relAng;
            rb.angularVelocity = outT.TransformDirection(relAng);

            rb.linearVelocity = newVel;
        }
    }

    private static Vector3 TransformDirectionThroughPortal(Vector3 worldDir, Transform inPlane, Transform outPlane)
    {
        Vector3 local = inPlane.InverseTransformDirection(worldDir);
        local = HalfTurn * local;
        return outPlane.TransformDirection(local).normalized;
    }

    // 큐브/오브젝트 충돌 SFX
    private void OnCollisionEnter(Collision collision)
    {
        if (rb == null) return;
        if (collision == null || collision.contactCount == 0) return;

        if (!(CompareTag("Cube") || CompareTag("Turret")))
            return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < impactMinSpeed) return;

        Vector3 p = collision.GetContact(0).point;
        SoundManager.PlaySFX(SfxId.ObjImpact_Cube, worldPos: p);
    }

}
