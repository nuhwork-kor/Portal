// TurretBullet.cs
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class TurretBullet : MonoBehaviour
{
    [Header("Pool (Return Key)")]
    [SerializeField] private string poolKey = "TurretBullet";

    [Header("Life")]
    [SerializeField] private float maxLifeSeconds = 3f;
    [SerializeField] private float maxDistance = 60f;

    [Header("Hit Filter")]
    [Tooltip("Player는 레이어로 판정. 여기 Player 레이어(또는 마스크) 넣어라.")]
    [SerializeField] private LayerMask playerMask;

    private Rigidbody rb;

    private float lifeTimer;
    private float remainingDistance;

    // 자기 터렛에 맞는거 무시용
    private Transform ownerRoot;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (playerMask.value == 0)
        {
            int layer = LayerMask.NameToLayer("Player");
            if (layer >= 0) playerMask = (1 << layer);
        }
    }

    private void OnEnable()
    {
        lifeTimer = 0f;
        remainingDistance = maxDistance;

        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// TurretController가 스폰 직후 호출
    /// </summary>
    public void Launch(
        Vector3 dir,
        float speed,
        float maxDist = 60f,
        UnityEngine.Object owner = null,
        string returnPoolKey = null
    )
    {
        if (!string.IsNullOrEmpty(returnPoolKey))
            poolKey = returnPoolKey;

        maxDistance = Mathf.Max(1f, maxDist);
        lifeTimer = 0f;
        remainingDistance = maxDistance;

        ownerRoot = GetOwnerRoot(owner);

        if (!rb) rb = GetComponent<Rigidbody>();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 v = dir.normalized * speed;
        rb.linearVelocity = v;
        transform.rotation = Quaternion.LookRotation(v.normalized, Vector3.up);
    }

    private void Update()
    {
        if (!rb) return;

        lifeTimer += Time.deltaTime;
        remainingDistance -= rb.linearVelocity.magnitude * Time.deltaTime;

        if (lifeTimer >= maxLifeSeconds || remainingDistance <= 0f)
            Despawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other) return;

        // 자기 터렛/자기 프리팹에 맞는 것 무시
        if (ownerRoot != null)
        {
            Transform hitT = other.transform;
            if (hitT == ownerRoot || hitT.IsChildOf(ownerRoot))
                return;
        }

        // ===== Player Hit : 레이어 마스크로 판정 =====
        if (IsInMask(other.gameObject.layer, playerMask))
        {
            other.transform.root.SendMessage("OnTurretHit", SendMessageOptions.DontRequireReceiver);
            Despawn();
            return;
        }

        // ===== Cube Hit : Tag로 판정 =====
        if (other.transform.root.CompareTag("Cube"))
        {
            SoundManager.PlaySFX(SfxId.ObjImpact_BulletHitCube, worldPos: transform.position);
            Despawn();
            return;
        }

        // 나머지(벽/바닥 등)
        Despawn();
    }

    private void Despawn()
    {
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.ReturnToPool(poolKey, gameObject);
        else
            gameObject.SetActive(false);
    }

    private static bool IsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private static Transform GetOwnerRoot(UnityEngine.Object owner)
    {
        if (owner == null) return null;

        if (owner is Component c) return c.transform.root;
        if (owner is GameObject go) return go.transform.root;
        if (owner is Transform t) return t.root;

        return null;
    }
}
