using Unity.VisualScripting;
using UnityEngine;

public class PortalGunController : MonoBehaviour
{
    public enum PortalShotType { Blue, Orange }

    [Header("Refs")]
    [SerializeField] Camera playerCamera;
    [SerializeField] Transform muzzle;
    [SerializeField] PortalSystem portalSystem;

    [Header("Pooling")]
    [SerializeField] string bulletPoolKey = "PortalBullet";

    [Header("Shoot")]
    [SerializeField] float shootSpeed = 35f;
    [SerializeField] float maxDistance = 100f;

    private void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;
        if (!portalSystem) portalSystem = FindAnyObjectByType<PortalSystem>();
    }

    private void OnEnable()
    {
        InputManager.OnFireBlue += FireBlue;
        InputManager.OnFireOrange += FireOrange;
    }

    private void OnDisable()
    {
        InputManager.OnFireBlue -= FireBlue;
        InputManager.OnFireOrange -= FireOrange;
    }
    void FireBlue() => Fire(PortalShotType.Blue);
    void FireOrange() => Fire(PortalShotType.Orange);

    void Fire(PortalShotType type)
    {
        if (!playerCamera) return;

        Vector3 origin = muzzle ? muzzle.position : playerCamera.transform.position + playerCamera.transform.forward * 0.3f;
        Vector3 dir  = playerCamera.transform.forward;

        // 풀에서 꺼내기
        GameObject obj = ObjectPoolManager.Instance.SpawnFromPool(bulletPoolKey, origin, Quaternion.LookRotation(dir, Vector3.up));
        if(!obj) return;

        PortalBullet bullet = obj.GetComponent<PortalBullet>();
        if(!bullet) return;

        bullet.Launch(
            type,
            dir,
            shootSpeed,
            maxDistance,
            portalSystem
            );
    }
}
