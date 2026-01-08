using System;
using UnityEngine;

public class PortalBullet : MonoBehaviour
{
    Rigidbody rb;
    Collider col;

    PortalGunController.PortalShotType shotType;
    float lifeTimer;
    float maxLifeSeconds = 3.0f;
    float remainingDistance;

    PortalSystem portalSystem;

    [Header("Pool")]
    [SerializeField] string poolKey = "PortalBullet";

    [Header("FX")]
    [SerializeField] ParticleSystem onHitFx;
    [SerializeField] TrailRenderer trail;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = rb.GetComponent<Collider>();

        //탄환은 빠르게 날아가니까 CCD
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    /// <summary>
    /// PortalGun에서 호출
    /// </summary>
    public void Launch(
        PortalGunController.PortalShotType type,
        Vector3 dir,
        float speed,
        float maxDistance,
        PortalSystem system
        )
    {
        shotType = type;
        portalSystem = system;

        //상태 리셋
        lifeTimer = 0f;
        remainingDistance = maxDistance;

        //물리 리셋
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        //trail/particle 리셋
        if (onHitFx) onHitFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (trail) trail.Clear();

        //발사
        rb.linearVelocity = dir.normalized * speed;
        transform.forward = dir.normalized;
    }

    private void Update()
    {
        //수명 관리
        lifeTimer += Time.deltaTime;

        //distance 기반 제한
        remainingDistance -= rb.linearVelocity.magnitude * Time.deltaTime;

        if (lifeTimer >= maxLifeSeconds || remainingDistance <= 0f)
        {
            Despawn();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        //첫 접촉점 기준
        ContactPoint cp = collision.contacts[0];

        //포탈을 배치할 수 있는 표면인지 검사
        if(portalSystem != null)
        {
            bool placed = portalSystem.PlacePortal(shotType, cp.point, cp.normal, collision.collider);
        }

        Despawn();
    }

    private void Despawn()
    {
        //물리 정지
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        //풀 반환
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(poolKey, gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
