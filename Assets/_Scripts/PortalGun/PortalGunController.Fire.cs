using UnityEngine;

public partial class PortalGunController
{
    [Header("Pooling")]
    [SerializeField] private string bulletPoolKey = "PortalBullet";

    [Header("Shoot")]
    [SerializeField] private float shootSpeed = 100f;
    [SerializeField] private float maxDistance = 120f;

    private void InitFire()
    {
        // nothing now
    }

    private void BindFireInput(bool bind)
    {
        if (bind)
        {
            InputManager.OnFireBlue += FireBlue;
            InputManager.OnFireOrange += FireOrange;
        }
        else
        {
            InputManager.OnFireBlue -= FireBlue;
            InputManager.OnFireOrange -= FireOrange;
        }
    }

    private void FireBlue() => FirePortal(PortalSystem.PortalType.Blue);
    private void FireOrange() => FirePortal(PortalSystem.PortalType.Orange);

    private void FirePortal(PortalSystem.PortalType type)
    {
        if (!playerCamera || !portalSystem || ObjectPoolManager.Instance == null) return;
        if (IsHolding) return; // µé°í ÀÖÀ¸¸é Æ÷Å» ¸ø ½ô

        // ÃÑ±¸ ÀÌÆåÆ®
        if (PortalEffectManager.Instance != null)
            PortalEffectManager.Instance.PlayMuzzle(type);

        Vector3 origin = muzzle
            ? muzzle.position
            : (playerCamera.transform.position + playerCamera.transform.forward * 0.3f);

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
