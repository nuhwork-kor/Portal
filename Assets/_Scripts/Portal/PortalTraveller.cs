using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PortalTraveller : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float exitOffset = 0.2f;

    private Rigidbody rb;
    private Collider col;

    private Portal inPortal;
    private Portal outPortal;
    private Collider wallCollider;

    private bool isInPortal;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    private PlayerMovement movement;      // 유지 스크립트
    private PlayerMouseLook mouseLook;    // 유지 스크립트

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        movement = GetComponent<PlayerMovement>();
        mouseLook = GetComponent<PlayerMouseLook>();
    }

    private void Update()
    {
        if (!isInPortal || inPortal == null || outPortal == null) return;

        // 포탈 평면 넘어갔는지 체크 (forward 기준)
        Vector3 localPos = inPortal.Plane.InverseTransformPoint(transform.position);

        // forward가 “플레이어 쪽을 보는 방향”으로 잡혔으니까,
        // 넘어가면 local z가 음수로 들어갈 가능성이 큼.
        // 애매하면 애매할수도있는데, 메쉬/기준축에 따라 부호가 반대일 수 있음.
        if (localPos.z < 0f)
        {
            Warp();
        }
    }

    public void EnterPortal(Portal inP, Portal outP, Collider wall)
    {
        inPortal = inP;
        outPortal = outP;
        wallCollider = wall;

        isInPortal = true;

        if (wallCollider)
            Physics.IgnoreCollision(col, wallCollider, true);
    }

    public void ExitPortal(Collider wall)
    {
        if (wall)
            Physics.IgnoreCollision(col, wall, false);

        isInPortal = false;
        inPortal = null;
        outPortal = null;
        wallCollider = null;
    }

    private void Warp()
    {
        Transform inT = inPortal.Plane;
        Transform outT = outPortal.Plane;

        // 위치
        Vector3 relativePos = inT.InverseTransformPoint(transform.position);
        relativePos = HalfTurn * relativePos;
        Vector3 newPos = outT.TransformPoint(relativePos);

        // 회전(전체 회전은 위험할 수 있어서, 일단 yaw만 맞추는 전략)
        Quaternion relativeRot = Quaternion.Inverse(inT.rotation) * transform.rotation;
        relativeRot = HalfTurn * relativeRot;
        Quaternion newWorldRot = outT.rotation * relativeRot;

        // 속도
        Vector3 relativeVel = inT.InverseTransformDirection(rb.linearVelocity);
        relativeVel = HalfTurn * relativeVel;
        Vector3 newVel = outT.TransformDirection(relativeVel);

        // 출구에서 살짝 앞으로
        newPos += outT.forward * exitOffset;

        // 적용
        rb.position = newPos;
        transform.position = newPos;

        // yaw만 맞추기(Portal1 느낌 유지)
        Vector3 fwd = newWorldRot * Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 1e-6f)
        {
            Quaternion yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            transform.rotation = yaw;
            // mouseLook 내부 pitch 변수 튐 방지
            if (mouseLook) mouseLook.SyncPitchFromCamera();
        }

        if (movement != null) movement.SetVelocity(newVel);
        else rb.linearVelocity = newVel;

        // 포탈 스왑 (되돌아가기 방지)
        (inPortal, outPortal) = (outPortal, inPortal);
    }
}
