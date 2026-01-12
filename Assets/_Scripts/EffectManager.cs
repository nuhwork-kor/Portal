using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    public List<EffectData> effectDatas;

    Dictionary<Enums.EffectType, EffectData> effectDict;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }
        else Destroy(gameObject);
    }

    void Init()
    {
        effectDict = new Dictionary<Enums.EffectType, EffectData>();

        foreach (var data in effectDatas)
        {
            if (!effectDict.ContainsKey(data.effectType))
            {
                effectDict.Add(data.effectType, data);
            }
        }
    }

    public void PlayEffect(
        Enums.EffectType type,
        Vector3 pos,
        Quaternion rot,
        Transform followTarget = null)
    {
        if (!effectDict.TryGetValue(type, out var data))
        {
            Debug.LogWarning($"Effect Type {type}이 등록되지 않았습니다");
            return;
        }

        GameObject fx = Instantiate(data.effectPrefab, pos, rot);

        if (data.followTarget && followTarget != null)
        {
            fx.transform.SetParent(followTarget, true);
        }

        var ps = fx.GetComponentInChildren<ParticleSystem>(true);
        if (ps == null)
        {
            Debug.LogError($"[{type}] ParticleSystem 없음");
            Destroy(fx);
            return;
        }

        ps.Clear(true);
        ps.Play(true);

        float life = GetParticleTotalTime(ps);
        StartCoroutine(DestroyAfterRealtime(fx, life));
    }

    float GetParticleTotalTime(ParticleSystem ps)
    {
        var main = ps.main;

        float duration = main.duration;

        float lifetime = 0f;
        if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
            lifetime = main.startLifetime.constant;
        else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
            lifetime = main.startLifetime.constantMax;
        else
            lifetime = 1f;

        return duration + lifetime;
    }

    IEnumerator DestroyAfterRealtime(GameObject fx, float time)
    {
        yield return new WaitForSecondsRealtime(time);
        Destroy(fx);
    }
}
