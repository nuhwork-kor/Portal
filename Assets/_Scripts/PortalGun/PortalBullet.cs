using UnityEngine;

public class PortalBullet : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private string poolKey = "PortalBullet";

    [Header("Life")]
    [SerializeField] private float maxLifeSeconds = 3f;

    private Rigidbody rb;

    private PortalSystem.PortalType shotType;
    private PortalSystem portalSystem;

    private float lifeTimer;
    private float remainingDistance;

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
        remainingDistance -= rb.linearVelocity.magnitude * Time.deltaTime;

        if (lifeTimer >= maxLifeSeconds || remainingDistance <= 0f)
            Despawn();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0) { Despawn(); return; }

        // 포탈에 맞으면: 워프/포탈생성 없이 그냥 사라짐
        if (collision.collider.CompareTag("Portal"))
        {
            Despawn();
            return;
        }

        // 일반 표면이면 포탈 배치
        if (portalSystem != null)
        {
            ContactPoint cp = collision.contacts[0];
            portalSystem.TryPlacePortal(shotType, cp.point, cp.normal, collision.collider);
        }

        Despawn();
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
