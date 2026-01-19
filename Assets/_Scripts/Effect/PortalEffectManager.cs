using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 이 스크립트는 "포탈 발사/충돌 이펙트"를 관리한다.
// - Muzzle FX: 씬에 고정 배치된 ParticleSystem(블루/오렌지)을 즉시 재생(자식 포함 전체 재생)
// - Impact FX: 풀에서 스폰해서 재생 후 수명만큼 기다렸다가 풀로 반환
// - playToken으로 "같은 오브젝트가 재사용되는 동안 이전 코루틴이 오동작"하는 것을 방지한다.
public class PortalEffectManager : MonoBehaviour
{
    public static PortalEffectManager Instance { get; private set; }                   // 싱글톤 인스턴스

    [Header("Muzzle FX (Scene Fixed ParticleSystems)")]
    [SerializeField] private ParticleSystem muzzleBlue;                                // 블루 발사 이펙트(씬 고정)
    [SerializeField] private ParticleSystem muzzleOrange;                              // 오렌지 발사 이펙트(씬 고정)

    [Header("Impact FX (Pooled)")]
    [SerializeField] private string impactBluePoolKey = "ImpactEffect_B";              // 블루 임팩트 풀 키
    [SerializeField] private string impactOrangePoolKey = "ImpactEffect_O";            // 오렌지 임팩트 풀 키

    [Tooltip("Impact 파티클이 loop이거나 수명 계산이 애매할 때 강제 반환 시간(초)")]
    [SerializeField] private float fallbackImpactLife = 3.0f;                          // 수명 추정 실패 시 fallback

    // 캐시(매번 GetComponentsInChildren 호출 방지)
    private ParticleSystem[] muzzleBlueAll;                                             // 블루 머즐 하위 포함 파티클 배열
    private ParticleSystem[] muzzleOrangeAll;                                           // 오렌지 머즐 하위 포함 파티클 배열

    // 코루틴 안전 토큰(재사용 시 이전 코루틴 무효화)
    private readonly Dictionary<GameObject, int> playToken = new();                     // fx 오브젝트별 토큰

    /// <summary>
    /// 유니티 생명주기: 싱글톤 초기화 + 머즐 파티클 배열 캐시.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }     // 중복 싱글톤 방지
        Instance = this;                                                               // Instance 설정

        muzzleBlueAll = muzzleBlue ? muzzleBlue.GetComponentsInChildren<ParticleSystem>(true) : null;       // 블루 머즐 파티클 캐시
        muzzleOrangeAll = muzzleOrange ? muzzleOrange.GetComponentsInChildren<ParticleSystem>(true) : null; // 오렌지 머즐 파티클 캐시
    }

    /// <summary>
    /// 발사 머즐 이펙트를 재생한다(블루/오렌지).
    /// - Stop/Clear/Simulate(0)/Play로 "항상 같은 프레임부터" 재생되게 강제한다.
    /// </summary>
    public void PlayMuzzle(PortalManager.PortalType type)
    {
        var arr = (type == PortalManager.PortalType.Blue) ? muzzleBlueAll : muzzleOrangeAll; // 타입에 맞는 배열 선택
        if (arr == null || arr.Length == 0) return;                                      // 없으면 종료

        for (int i = 0; i < arr.Length; i++)                                             // 파티클 순회
        {
            var ps = arr[i];                                                             // 파티클
            if (!ps) continue;                                                           // null이면 스킵

            if (!ps.gameObject.activeInHierarchy)                                        // 비활성화면
                ps.gameObject.SetActive(true);                                           // 활성화

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);              // 정지+클리어
            ps.Clear(true);                                                              // 잔상 제거
            ps.Simulate(0f, true, true, true);                                           // 시뮬 0프레임으로 리셋
            ps.Play(true);                                                               // 재생
        }
    }

    /// <summary>
    /// 포탈 배치 실패 등의 상황에서 임팩트 이펙트를 재생한다(풀 사용).
    /// - 파티클 수명을 추정해 해당 시간 뒤 풀로 반환한다.
    /// - playToken으로 재사용 중 코루틴 충돌을 방지한다.
    /// </summary>
    public void PlayImpact(PortalManager.PortalType type, Vector3 pos, Vector3 normal)
    {
        if (ObjectPoolManager.Instance == null) return;                                  // 풀 매니저 없으면 종료

        string key = (type == PortalManager.PortalType.Blue) ? impactBluePoolKey : impactOrangePoolKey; // 풀 키 선택
        if (string.IsNullOrEmpty(key)) return;                                           // 키가 비면 종료

        Quaternion rot = Quaternion.LookRotation(normal, Vector3.up);                    // 표면 노멀 기준 회전
        GameObject fx = ObjectPoolManager.Instance.SpawnFromPool(key, pos, rot);         // 풀에서 스폰
        if (!fx) return;                                                                 // 실패면 종료

        fx.transform.SetParent(null, true);                                              // 월드에 두기
        fx.transform.SetPositionAndRotation(pos, rot);                                   // 위치/회전 세팅

        var systems = fx.GetComponentsInChildren<ParticleSystem>(true);                  // 파티클 시스템 배열
        float life = (systems != null && systems.Length > 0)
            ? GetMaxParticleLife(systems, fallbackImpactLife)                            // 수명 추정
            : fallbackImpactLife;                                                        // 실패 시 fallback

        if (systems != null)                                                             // 파티클이 있으면
        {
            for (int i = 0; i < systems.Length; i++)                                     // 순회
            {
                var ps = systems[i];                                                     // 파티클
                if (!ps) continue;                                                       // null 스킵

                if (!ps.gameObject.activeInHierarchy)                                    // 비활성화면
                    ps.gameObject.SetActive(true);                                       // 활성화

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);          // 정지+클리어
                ps.Clear(true);                                                          // 잔상 제거
                ps.Simulate(0f, true, true, true);                                       // 리셋
                ps.Play(true);                                                           // 재생
            }
        }

        int token = 1;                                                                   // 토큰 기본값
        if (playToken.TryGetValue(fx, out int cur)) token = cur + 1;                    // 기존 토큰이 있으면 증가
        playToken[fx] = token;                                                           // 토큰 저장

        StartCoroutine(ReturnImpactAfter(key, fx, token, life));                        // life 후 반환 코루틴 시작
    }

    /// <summary>
    /// 파티클 시스템 배열에서 "최대 재생 시간"을 추정한다.
    /// - loop면 정확한 종료가 없으므로 fallback을 반환한다.
    /// </summary>
    private float GetMaxParticleLife(ParticleSystem[] systems, float fallback)
    {
        float max = 0f;                                                                  // 최대값

        for (int i = 0; i < systems.Length; i++)                                        // 순회
        {
            var ps = systems[i];                                                        // 파티클
            if (!ps) continue;                                                          // null 스킵

            var main = ps.main;                                                         // main 모듈

            if (main.loop) return fallback;                                             // 루프면 fallback

            float delay = 0f;                                                           // startDelay 추정
            if (main.startDelay.mode == ParticleSystemCurveMode.Constant) delay = main.startDelay.constant;
            else if (main.startDelay.mode == ParticleSystemCurveMode.TwoConstants) delay = main.startDelay.constantMax;

            float duration = main.duration;                                             // duration

            float lifetime = 0f;                                                        // startLifetime 추정
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant) lifetime = main.startLifetime.constant;
            else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants) lifetime = main.startLifetime.constantMax;
            else lifetime = 0.5f;                                                       // 기타 모드는 보수적으로 0.5

            max = Mathf.Max(max, delay + duration + lifetime);                          // 최대 시간 갱신
        }

        return Mathf.Max(max, 0.05f);                                                   // 너무 작지 않게 보정
    }

    /// <summary>
    /// 일정 시간 뒤 임팩트 FX를 풀로 반환한다.
    /// - playToken이 현재 토큰과 다르면(재사용됨) 반환하지 않는다.
    /// </summary>
    private IEnumerator ReturnImpactAfter(string key, GameObject fx, int token, float time)
    {
        yield return new WaitForSeconds(time);                                          // 수명만큼 대기

        if (!fx) yield break;                                                           // 오브젝트가 파괴되었으면 종료

        if (!playToken.TryGetValue(fx, out int curToken) || curToken != token)          // 토큰이 다르면(재사용)
            yield break;                                                                // 이 코루틴은 무효

        if (!fx.activeSelf) yield break;                                                // 이미 비활성화면 종료

        var systems = fx.GetComponentsInChildren<ParticleSystem>(true);                 // 파티클 배열
        if (systems != null)
        {
            for (int i = 0; i < systems.Length; i++)                                    // 순회
            {
                if (systems[i])
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 정지+클리어
            }
        }

        ObjectPoolManager.Instance.ReturnToPool(key, fx);                               // 풀로 반환
    }
}
