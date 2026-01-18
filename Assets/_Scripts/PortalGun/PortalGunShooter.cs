// PortalGunShooter.cs
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PortalGunController))]
public class PortalGunShooter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PortalGunController ctx;

    [Header("Pooling")]
    [SerializeField] private string bulletPoolKey = "PortalBullet";

    [Header("Shoot")]
    [SerializeField] private float shootSpeed = 100f;
    [SerializeField] private float maxDistance = 120f;

    [Header("Option")]
    [Tooltip("원하면 '들고 있을 때도 발사 가능'하게 false로 두면 됨.")]
    [SerializeField] private bool blockFireWhileHolding = false;

    private HeldObjectController holder;

    private void Awake()
    {
        if (!ctx) ctx = GetComponent<PortalGunController>();
        holder = GetComponent<HeldObjectController>();
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

    private void FireBlue() => FirePortal(PortalManager.PortalType.Blue);
    private void FireOrange() => FirePortal(PortalManager.PortalType.Orange);

    private void FirePortal(PortalManager.PortalType type)
    {
        if (ctx == null) return;
        if (!ctx.PlayerCamera || !ctx.PortalManager) return;
        if (ObjectPoolManager.Instance == null) return;

        if (blockFireWhileHolding && holder != null && holder.IsHolding)
            return;

        // muzzle effect
        if (PortalEffectManager.Instance != null)
            PortalEffectManager.Instance.PlayMuzzle(type);

        Vector3 origin = ctx.Muzzle
            ? ctx.Muzzle.position
            : (ctx.PlayerCamera.transform.position + ctx.PlayerCamera.transform.forward * 0.3f);

        Vector3 dir = ctx.PlayerCamera.transform.forward;

        GameObject obj = ObjectPoolManager.Instance.SpawnFromPool(
            bulletPoolKey,
            origin,
            Quaternion.LookRotation(dir, Vector3.up)
        );

        if (!obj) return;

        PortalBullet bullet = obj.GetComponent<PortalBullet>();
        if (!bullet) return;

        // ✅ SFX: 포탈건 발사
        SoundManager.PlaySFX(SfxId.PortalGun_Shoot);

        bullet.Launch(type, dir, shootSpeed, maxDistance, ctx.PortalManager);
    }
}
