using UnityEngine;

public class PortalBullet : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private string poolKey = "PortalBullet";

    [Header("Life")]
    [SerializeField] private float maxLifeSeconds = 3f;

    [Header("Child FX Names (under PortalBullet prefab)")]
    [SerializeField] private string shootBlueChildName = "Shoot_B";
    [SerializeField] private string shootOrangeChildName = "Shoot_O";

    private Rigidbody rb;

    private PortalSystem.PortalType shotType;
    private PortalSystem portalSystem;

    private float lifeTimer;
    private float remainingDistance;

    private Transform shootBlueRoot;
    private Transform shootOrangeRoot;
    private ParticleSystem[] shootBluePS;
    private ParticleSystem[] shootOrangePS;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        CacheChildFX();
    }

    private void OnEnable()
    {
        StopAllTrail();
    }

    private void CacheChildFX()
    {
        shootBlueRoot = FindDeepChild(transform, shootBlueChildName);
        shootOrangeRoot = FindDeepChild(transform, shootOrangeChildName);

        shootBluePS = shootBlueRoot ? shootBlueRoot.GetComponentsInChildren<ParticleSystem>(true) : null;
        shootOrangePS = shootOrangeRoot ? shootOrangeRoot.GetComponentsInChildren<ParticleSystem>(true) : null;
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

        // 총알 비행 이펙트
        PlayTrail(type);
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

        // 포탈에 맞으면 그냥 사라짐
        if (collision.collider.CompareTag("Portal"))
        {
            Despawn();
            return;
        }

        ContactPoint cp = collision.contacts[0];

        bool placed = false;
        if (portalSystem != null)
            placed = portalSystem.TryPlacePortal(shotType, cp.point, cp.normal, collision.collider);

        // 포탈 생성 실패시에만 Impact 재생 + 풀 반환까지 처리
        if (!placed && PortalEffectManager.Instance != null)
            PortalEffectManager.Instance.PlayImpact(shotType, cp.point, cp.normal);

        Despawn();
    }

    private void PlayTrail(PortalSystem.PortalType type)
    {
        StopAllTrail();

        bool blue = (type == PortalSystem.PortalType.Blue);

        if (shootBlueRoot) shootBlueRoot.gameObject.SetActive(blue);
        if (shootOrangeRoot) shootOrangeRoot.gameObject.SetActive(!blue);

        var arr = blue ? shootBluePS : shootOrangePS;
        if (arr == null) return;

        for (int i = 0; i < arr.Length; i++)
        {
            var ps = arr[i];
            if (!ps) continue;

            if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private void StopAllTrail()
    {
        StopPS(shootBluePS);
        StopPS(shootOrangePS);

        if (shootBlueRoot) shootBlueRoot.gameObject.SetActive(false);
        if (shootOrangeRoot) shootOrangeRoot.gameObject.SetActive(false);
    }

    private static void StopPS(ParticleSystem[] arr)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
        {
            var ps = arr[i];
            if (!ps) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }
    }

    private void Despawn()
    {
        StopAllTrail();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.ReturnToPool(poolKey, gameObject);
        else
            gameObject.SetActive(false);
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (!parent) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;
            var r = FindDeepChild(c, name);
            if (r) return r;
        }
        return null;
    }
}
