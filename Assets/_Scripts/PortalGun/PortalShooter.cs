using UnityEngine;

public class PortalShooter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform muzzle;
    [SerializeField] private PortalSystem portalSystem;

    [Header("Pooling")]
    [SerializeField] private string bulletPoolKey = "PortalBullet";

    [Header("Shoot")]
    [SerializeField] private float shootSpeed = 35f;
    [SerializeField] private float maxDistance = 120f;

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

    private void FireBlue() => Fire(PortalSystem.PortalType.Blue);
    private void FireOrange() => Fire(PortalSystem.PortalType.Orange);

    private void Fire(PortalSystem.PortalType type)
    {
        if (!playerCamera || !portalSystem || ObjectPoolManager.Instance == null) return;

        Vector3 origin = muzzle ? muzzle.position : (playerCamera.transform.position + playerCamera.transform.forward * 0.3f);
        Vector3 dir = playerCamera.transform.forward;

        GameObject obj = ObjectPoolManager.Instance.SpawnFromPool(
            bulletPoolKey,
            origin,
            Quaternion.LookRotation(dir, Vector3.up)
        );

        if (!obj) return;

        PortalBullet bullet = obj.GetComponent<PortalBullet>();
        if (!bullet) return;

        bullet.Launch(type, dir, shootSpeed, maxDistance, portalSystem);
    }
}
