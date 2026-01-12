using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Effect/EffectData")]
public class EffectData : ScriptableObject
{
    public Enums.EffectType effectType;     // 이펙트 타입
    public GameObject effectPrefab;         // 이펙트 프리팹
    public float lifeTime;                  // 이펙트 지속 시간
    public bool followTarget;               // 따라갈건지 설정
}
