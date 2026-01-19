using UnityEngine;

// 이 스크립트는 "포탈건 발사"를 담당한다.
// - InputManager 이벤트(OnFireBlue/OnFireOrange)를 받아서 포탈 탄환(PortalBullet)을 풀에서 꺼내 발사한다.
// - 발사 시 Muzzle FX / SFX를 재생한다.
// - 선택 옵션: 물체를 들고 있을 때 발사를 막을지 여부(blockFireWhileHolding).
[DisallowMultipleComponent]
[RequireComponent(typeof(PortalGunController))]
public class PortalGunShooter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PortalGunController ctx;                                  // 발사에 필요한 카메라/총구/매니저 참조

    [Header("Pooling")]
    [SerializeField] private string bulletPoolKey = "PortalBullet";                    // ObjectPoolManager에서 사용할 키

    [Header("Shoot")]
    [SerializeField] private float shootSpeed = 100f;                                  // 탄환 속도
    [SerializeField] private float maxDistance = 120f;                                 // 최대 비행 거리(PortalBullet에서 소진)

    [Header("Option")]
    [Tooltip("원하면 '들고 있을 때도 발사 가능'하게 false로 두면 됨.")]
    [SerializeField] private bool blockFireWhileHolding = false;                       // 홀드 중 발사 차단 여부

    private HeldObjectController holder;                                               // 현재 홀드 중인지 확인용(있으면 참조)

    /// <summary>
    /// 유니티 생명주기: 참조 자동 확보.
    /// </summary>
    private void Awake()
    {
        if (!ctx) ctx = GetComponent<PortalGunController>();                            // 컨텍스트 자동 연결
        holder = GetComponent<HeldObjectController>();                                  // 홀더는 같은 오브젝트에 있을 수도 있음
    }

    /// <summary>
    /// 유니티 생명주기: 입력 이벤트 구독.
    /// </summary>
    private void OnEnable()
    {
        InputManager.OnFireBlue += FireBlue;                                           // 블루 발사 입력 구독
        InputManager.OnFireOrange += FireOrange;                                       // 오렌지 발사 입력 구독
    }

    /// <summary>
    /// 유니티 생명주기: 입력 이벤트 해제.
    /// </summary>
    private void OnDisable()
    {
        InputManager.OnFireBlue -= FireBlue;                                           // 블루 발사 입력 해제
        InputManager.OnFireOrange -= FireOrange;                                       // 오렌지 발사 입력 해제
    }

    /// <summary>
    /// 블루 포탈 발사 요청 핸들러.
    /// </summary>
    private void FireBlue() => FirePortal(PortalManager.PortalType.Blue);              // 블루 타입으로 발사

    /// <summary>
    /// 오렌지 포탈 발사 요청 핸들러.
    /// </summary>
    private void FireOrange() => FirePortal(PortalManager.PortalType.Orange);          // 오렌지 타입으로 발사

    /// <summary>
    /// 실제 발사 처리:
    /// - 참조/풀 체크
    /// - 홀드 중 차단 옵션 체크
    /// - Muzzle FX/SFX 재생
    /// - 풀에서 탄환 스폰 후 Launch
    /// </summary>
    /// <param name="type">발사할 포탈 타입</param>
    private void FirePortal(PortalManager.PortalType type)
    {
        if (ctx == null) return;                                                       // 컨텍스트 없으면 종료
        if (!ctx.PlayerCamera || !ctx.PortalManager) return;                           // 필수 참조 없으면 종료
        if (ObjectPoolManager.Instance == null) return;                                // 풀 매니저 없으면 종료

        if (blockFireWhileHolding && holder != null && holder.IsHolding)               // 홀드 중 발사 차단 옵션이 켜져있고 실제 홀드 중이면
            return;                                                                    // 발사하지 않음

        // muzzle effect
        if (PortalEffectManager.Instance != null)                                      // 이펙트 매니저가 있으면
            PortalEffectManager.Instance.PlayMuzzle(type);                             // 머즐 이펙트 재생

        Vector3 origin = ctx.Muzzle
            ? ctx.Muzzle.position                                                      // 총구가 있으면 총구 위치
            : (ctx.PlayerCamera.transform.position + ctx.PlayerCamera.transform.forward * 0.3f); // 없으면 카메라 앞쪽 임시 위치

        Vector3 dir = ctx.PlayerCamera.transform.forward;                              // 발사 방향은 카메라 전방

        GameObject obj = ObjectPoolManager.Instance.SpawnFromPool(
            bulletPoolKey,
            origin,
            Quaternion.LookRotation(dir, Vector3.up)
        );                                                                             // 풀에서 탄환 스폰

        if (!obj) return;                                                              // 스폰 실패면 종료

        PortalBullet bullet = obj.GetComponent<PortalBullet>();                        // 탄환 컴포넌트 얻기
        if (!bullet) return;                                                           // 탄환 컴포넌트 없으면 종료

        // ✅ SFX: 포탈건 발사
        SoundManager.PlaySFX(SfxId.PortalGun_Shoot);                                   // 발사 사운드 재생

        bullet.Launch(type, dir, shootSpeed, maxDistance, ctx.PortalManager);          // 탄환 발사 초기화
    }
}
