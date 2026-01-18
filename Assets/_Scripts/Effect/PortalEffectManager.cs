using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalEffectManager : MonoBehaviour
{
    public static PortalEffectManager Instance { get; private set; }

    [Header("Muzzle FX (Scene Fixed ParticleSystems)")]
    [SerializeField] private ParticleSystem muzzleBlue;   // ShootEffect_B (루트 ParticleSystem)
    [SerializeField] private ParticleSystem muzzleOrange; // ShootEffect_O (루트 ParticleSystem)

    [Header("Impact FX (Pooled)")]
    [SerializeField] private string impactBluePoolKey = "ImpactEffect_B";
    [SerializeField] private string impactOrangePoolKey = "ImpactEffect_O";

    [Tooltip("Impact 프리팹 수명이 애매하거나 loop면 이 시간으로 반환(초)")]
    [SerializeField] private float fallbackImpactLife = 3.0f;

    // 캐시(매 클릭마다 GetComponentsInChildren 안 돌리려고)
    private ParticleSystem[] muzzleBlueAll;
    private ParticleSystem[] muzzleOrangeAll;

    // 코루틴 꼬임 방지 토큰
    private readonly Dictionary<GameObject, int> playToken = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 총구 이펙트는 “루트 1개만” Play하면 안 되는 경우가 많아서 전부 캐시
        muzzleBlueAll = muzzleBlue ? muzzleBlue.GetComponentsInChildren<ParticleSystem>(true) : null;
        muzzleOrangeAll = muzzleOrange ? muzzleOrange.GetComponentsInChildren<ParticleSystem>(true) : null;
    }

    public void PlayMuzzle(PortalManager.PortalType type)
    {
        var arr = (type == PortalManager.PortalType.Blue) ? muzzleBlueAll : muzzleOrangeAll;
        if (arr == null || arr.Length == 0) return;

        // 매번 확실하게 “시간 0 리셋 + 클리어 + 재생”
        for (int i = 0; i < arr.Length; i++)
        {
            var ps = arr[i];
            if (!ps) continue;

            if (!ps.gameObject.activeInHierarchy)
                ps.gameObject.SetActive(true);

            // Stop + Clear만으로 안 잡히는 케이스가 있어서 Simulate(0)까지 같이
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);

            //  완전 리셋
            ps.Simulate(0f, true, true, true);

            ps.Play(true);
        }
    }

    public void PlayImpact(PortalManager.PortalType type, Vector3 pos, Vector3 normal)
    {
        if (ObjectPoolManager.Instance == null) return;

        string key = (type == PortalManager.PortalType.Blue) ? impactBluePoolKey : impactOrangePoolKey;
        if (string.IsNullOrEmpty(key)) return;

        Quaternion rot = Quaternion.LookRotation(normal, Vector3.up);
        GameObject fx = ObjectPoolManager.Instance.SpawnFromPool(key, pos, rot);
        if (!fx) return;

        fx.transform.SetParent(null, true);
        fx.transform.SetPositionAndRotation(pos, rot);

        var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
        float life = (systems != null && systems.Length > 0)
            ? GetMaxParticleLife(systems, fallbackImpactLife)
            : fallbackImpactLife;

        if (systems != null)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (!ps) continue;

                if (!ps.gameObject.activeInHierarchy)
                    ps.gameObject.SetActive(true);

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
                ps.Simulate(0f, true, true, true);
                ps.Play(true);
            }
        }

        int token = 1;
        if (playToken.TryGetValue(fx, out int cur)) token = cur + 1;
        playToken[fx] = token;

        StartCoroutine(ReturnImpactAfter(key, fx, token, life));
    }

    private float GetMaxParticleLife(ParticleSystem[] systems, float fallback)
    {
        float max = 0f;

        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (!ps) continue;

            var main = ps.main;

            if (main.loop) return fallback;

            float delay = 0f;
            if (main.startDelay.mode == ParticleSystemCurveMode.Constant) delay = main.startDelay.constant;
            else if (main.startDelay.mode == ParticleSystemCurveMode.TwoConstants) delay = main.startDelay.constantMax;

            float duration = main.duration;

            float lifetime = 0f;
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant) lifetime = main.startLifetime.constant;
            else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants) lifetime = main.startLifetime.constantMax;
            else lifetime = 0.5f;

            max = Mathf.Max(max, delay + duration + lifetime);
        }

        return Mathf.Max(max, 0.05f);
    }

    private IEnumerator ReturnImpactAfter(string key, GameObject fx, int token, float time)
    {
        yield return new WaitForSeconds(time);

        if (!fx) yield break;

        if (!playToken.TryGetValue(fx, out int curToken) || curToken != token)
            yield break;

        if (!fx.activeSelf) yield break;

        var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
        if (systems != null)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i])
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        ObjectPoolManager.Instance.ReturnToPool(key, fx);
    }
}
