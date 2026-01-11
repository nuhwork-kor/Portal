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

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        capsule = GetComponent<CapsuleCollider>();

        movement = GetComponent<PlayerMovement>();
        mouseLook = GetComponent<PlayerMouseLook>();
    }

    private void FixedUpdate()
    {
        if (insideCount <= 0) return;
        if (inPortal == null || outPortal == null) return;
        if (Time.time < cooldownUntil) return;

        if (HasCenterCrossedPlane(inPortal.Plane, entrySideSign))
            WarpNow();
    }

    // 쿨다운 중이어도 "상태/Ignore 갱신"은 해야 한다.
    public void EnterPortal(Portal inP, Portal outP, Collider inWall)
    {
        if (!inP || !outP) return;

        // 1) "다른 포탈"에 들어온 거면: 이전 상태를 강제로 정리하고 새로 시작
        if (insideCount > 0 && inPortal != null && inPortal != inP)
        {
            ForceClearState(); // 이전 wall ignore 풀고 상태 리셋
        }

        // 2) 첫 진입(또는 강제 리셋 직후)이라면 정상 세팅
        if (insideCount == 0)
        {
            inPortal = inP;
            outPortal = outP;

            // wallCollider 갱신
            SetIgnoredWall(inWall);

            float d = SignedDistanceToPlane(inPortal.Plane, GetCenterWorld());
            entrySideSign = SignWithEps(d, +1);
        }
        else
        {
            // 3) 같은 포탈인데 "벽 콜라이더가 바뀐 경우"(포탈 재설치/다른 조각 벽 등)
            //    -> 이전 ignore 풀고 새 벽 ignore로 교체
            if (inPortal == inP && inWall != null && inWall != wallCollider)
            {
                SetIgnoredWall(inWall);
            }

            // outPortal도 최신으로 맞춰둠(포탈 페어가 바뀌는 상황 대비)
            outPortal = outP;
        }

        insideCount++;
    }

    public void NotifyTriggerExit(Portal exitedPortal)
    {
        // 다른 포탈 exit 이벤트면 무시(기존 로직 유지)
        if (exitedPortal != null && inPortal != null && exitedPortal != inPortal)
            return;

        ExitPortalInternal();
    }

    private void ExitPortalInternal()
    {
        if (insideCount <= 0) return;

        insideCount = Mathf.Max(insideCount - 1, 0);
        if (insideCount > 0) return;

        // 워프 없이 그냥 빠져나간 경우 정리
        ForceClearState();
    }

    private void WarpNow()
    {
        Portal oldInPortal = inPortal;
        Portal oldOutPortal = outPortal;
        Collider oldInWall = wallCollider;

        Warp();

        // 출구쪽 벽 콜라이더(네 Portal이 캐시해두는 값 사용)
        Collider newWall = null;
        if (oldOutPortal != null)
            newWall = oldOutPortal.WallColliderCached;

        // 원래 들어가던 벽 ignore 해제
        if (oldInWall) Physics.IgnoreCollision(col, oldInWall, false);

        // 새로 나온 쪽 벽 ignore 설정(트리거 완전히 나갈 때까지 유지)
        if (newWall) Physics.IgnoreCollision(col, newWall, true);

        cooldownUntil = Time.time + teleportCooldown;

        // 출구 포탈 기준으로 상태 스왑 유지
        inPortal = oldOutPortal;
        outPortal = oldInPortal;
        wallCollider = newWall;

        insideCount = Mathf.Max(insideCount, 1);

        if (inPortal != null)
        {
            float d = SignedDistanceToPlane(inPortal.Plane, GetCenterWorld());
            entrySideSign = SignWithEps(d, +1);
        }
        else
        {
            ForceClearState();
        }
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
        if (rb) return rb.worldCenterOfMass;
        if (col) return col.bounds.center;
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
