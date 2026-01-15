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

    private Rigidbody rb;
    private Collider col;
    private CapsuleCollider capsule;

    private Portal inPortal;
    private Portal outPortal;

    // 벽(포탈 설치된 실제 표면) 콜라이더
    private Collider wallCollider;

    // 포탈 “면” 콜라이더(PortalSurface) 충돌도 무시
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

            // outPortal 갱신될 수 있으니 surface ignore도 갱신
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

        Warped?.Invoke(oldInPortal, oldOutPortal);

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

        // 스왑 이후에도 surface ignore 갱신
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

        // ✅ (플레이어면) 워프 전 시야를 먼저 읽어둔다
        Quaternion viewBefore = Quaternion.identity;
        Vector3 stableYawBefore = Vector3.forward;

        if (playerController != null)
        {
            viewBefore = playerController.GetViewWorldRotation();
            stableYawBefore = playerController.GetStableYawForward();
        }

        // ===== 위치 변환 =====
        Vector3 center = GetCenterWorld();
        Vector3 pivotToCenter = center - transform.position;

        Vector3 relativePos = inT.InverseTransformPoint(center);
        relativePos = HalfTurn * relativePos;
        Vector3 newCenter = outT.TransformPoint(relativePos);

        if (exitOffset != 0f)
            newCenter += outT.forward * exitOffset;

        Vector3 newPos = newCenter - pivotToCenter;

        // ===== 속도 변환 =====
        Vector3 relativeVel = inT.InverseTransformDirection(rb.linearVelocity);
        relativeVel = HalfTurn * relativeVel;
        Vector3 newVel = outT.TransformDirection(relativeVel);

        rb.position = newPos;
        transform.position = newPos;

        if (playerController != null)
        {
            // ===== Portal1처럼 시야 "딸려가기" =====
            // viewAfter = out * halfTurn * inv(in) * viewBefore
            Quaternion viewAfter =
                outT.rotation *
                HalfTurn *
                Quaternion.Inverse(inT.rotation) *
                viewBefore;

            // 특이점 fallback도 포탈 변환해서 넘기기(안 그러면 수직 시선에서 yaw가 튐)
            Vector3 stableYawAfter = TransformDirectionThroughPortal(stableYawBefore, inT, outT);

            // ✅ 카메라 직접 제어 X : playerBody yaw + cameraRoot pitch만 적용
            playerController.ApplyViewAfterUpright(viewAfter, stableYawAfter);

            // 속도는 포탈 변환 유지(낙하 가속/런치 기믹)
            playerController.SetVelocity(newVel);

            // 혹시 남는 각속도 제거(너는 freezeRotation이라 거의 0이겠지만 안전빵)
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            // ===== 일반 오브젝트: 회전도 포탈 변환 =====
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

    // ✅ 포탈 방향 변환(정규화 유지)
    private static Vector3 TransformDirectionThroughPortal(Vector3 worldDir, Transform inPlane, Transform outPlane)
    {
        Vector3 local = inPlane.InverseTransformDirection(worldDir);
        local = HalfTurn * local;
        return outPlane.TransformDirection(local).normalized;
    }
}
