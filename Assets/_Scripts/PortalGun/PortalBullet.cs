using UnityEngine;

public class PortalBullet : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private string poolKey = "PortalBullet";

    [Header("Life")]
    [SerializeField] private float maxLifeSeconds = 3f;

    [Header("Portal Pass-Through")]
    [SerializeField] private float portalExitOffset = 0.08f;
    [SerializeField] private float portalRehitCooldown = 0.05f;

    private Rigidbody rb;

    private PortalSystem.PortalType shotType;
    private PortalSystem portalSystem;

    private float lifeTimer;
    private float remainingDistance;
    private float portalCooldownTimer;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Launch(PortalSystem.PortalType type, Vector3 dir, float speed, float maxDistance, PortalSystem system)
    {
        shotType = type;
        portalSystem = system;

        lifeTimer = 0f;
        portalCooldownTimer = 0f;
        remainingDistance = maxDistance;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 v = dir.normalized * speed;
        rb.linearVelocity = v;
        transform.rotation = Quaternion.LookRotation(v.normalized, Vector3.up);
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (portalCooldownTimer > 0f) portalCooldownTimer -= Time.deltaTime;

        remainingDistance -= rb.linearVelocity.magnitude * Time.deltaTime;

        if (lifeTimer >= maxLifeSeconds || remainingDistance <= 0f)
            Despawn();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0) { Despawn(); return; }
        if (portalCooldownTimer > 0f) return;

        ContactPoint cp = collision.contacts[0];

        // 1) 포탈 표면이면 통과 시도
        if (collision.collider.CompareTag("Portal"))
        {
            if (TryWarpThroughPortal(collision.collider, cp.point))
            {
                portalCooldownTimer = portalRehitCooldown;
                return;
            }

            Despawn();
            return;
        }

        // 2) 일반 표면이면 포탈 배치
        if (portalSystem != null)
        {
            portalSystem.TryPlacePortal(shotType, cp.point, cp.normal, collision.collider);
        }

        Despawn();
    }

    private bool TryWarpThroughPortal(Collider portalCollider, Vector3 hitPoint)
    {
        Portal inPortal = portalCollider.GetComponentInParent<Portal>();
        if (!inPortal || !inPortal.IsPlaced) return false;

        Portal outPortal = inPortal.OtherPortal;
        if (!outPortal || !outPortal.IsPlaced) return false;

        Transform inT = inPortal.Plane;
        Transform outT = outPortal.Plane;

        Vector3 dir = rb.linearVelocity.sqrMagnitude > 1e-6f ? rb.linearVelocity.normalized : transform.forward;

        Vector3 relativePos = inT.InverseTransformPoint(hitPoint + dir * 0.05f);
        relativePos = HalfTurn * relativePos;
        Vector3 newPos = outT.TransformPoint(relativePos);

        Vector3 relativeVel = inT.InverseTransformDirection(rb.linearVelocity);
        relativeVel = HalfTurn * relativeVel;
        Vector3 newVel = outT.TransformDirection(relativeVel);

        // 출구에서 살짝 앞으로
        newPos += outT.forward * portalExitOffset;

        rb.position = newPos;
        rb.linearVelocity = newVel;

        if (newVel.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(newVel.normalized, Vector3.up);

        return true;
    }

    private void Despawn()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.ReturnToPool(poolKey, gameObject);
        else
            gameObject.SetActive(false);
    }
}
