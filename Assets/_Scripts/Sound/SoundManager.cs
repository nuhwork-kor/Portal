using System;
using System.Collections.Generic;
using UnityEngine;

// 이 enum은 게임에서 사용할 SFX(효과음) ID 목록이다.
public enum SfxId
{
    None = 0,                  // 없음(미지정)
    StagePanel_LightOn,        // 스테이지 패널 라이트 켜짐
    StagePanel_Buzz,           // 스테이지 패널 버즈음
    PortalGun_Shoot,           // 포탈건 발사
    Player_Walk,               // 플레이어 걷기
    Player_Land,               // 플레이어 착지
    ObjImpact_Cube,            // 큐브 충돌
    ObjImpact_BulletHitCube,   // 탄환이 큐브에 맞는 충돌
    Portal_BlueEnter,          // 파란 포탈 진입
    Portal_OrangeEnter,        // 주황 포탈 진입
    StageTeleport,             // 스테이지 텔레포트/이동
    Button_Interact,           // 버튼 인터랙트
    Door_Interact,             // 문 인터랙트
}

// 이 enum은 게임에서 사용할 BGM(배경음) ID 목록이다.
public enum BgmId
{
    None = 0,                  // 없음(미지정)
    Main,                      // 메인 메뉴 BGM
    InGame,                    // 인게임 BGM
    Ending,                    // 엔딩 BGM
}

// 이 스크립트는 게임 전체의 사운드(SFX/BGM)를 관리하는 싱글톤 매니저다.
// - SFX: 원샷(2D/3D) 재생 + 루프 SFX(owner+id 기반) 생성/정지 지원
// - BGM: 2D 루프 AudioSource로 재생/정지/전환 지원
[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    [Serializable]
    public class SfxEntry
    {
        public SfxId id;                                            // 이 엔트리의 SFX ID
        public AudioClip[] clips;                                   // 랜덤으로 선택될 오디오 클립 배열

        [Range(0f, 1f)] public float volume = 1f;                    // 개별 SFX 기본 볼륨(0~1)
        [Tooltip("피치 랜덤 범위 (1,1이면 고정)")]
        public Vector2 pitchRange = new Vector2(1f, 1f);             // 피치 랜덤 범위(min,max)

        [Header("3D")]
        public bool spatial3D = true;                                // true면 3D 사운드(거리 감쇠)
        [Tooltip("3D일 때 거리 감쇠")]
        public float minDistance = 1f;                               // 3D 최소 거리(감쇠 시작)
        public float maxDistance = 25f;                              // 3D 최대 거리(감쇠 종료)
    }

    [Serializable]
    public class BgmEntry
    {
        public BgmId id;                                             // 이 엔트리의 BGM ID
        public AudioClip clip;                                       // 재생할 BGM 클립
        [Range(0f, 1f)] public float volume = 1f;                    // 개별 BGM 기본 볼륨(0~1)
    }

    public static SoundManager I { get; private set; }               // SoundManager 싱글톤 인스턴스

    [Header("Library")]
    [SerializeField] private List<SfxEntry> sfx = new();             // SFX 라이브러리(인스펙터에서 등록)
    [SerializeField] private List<BgmEntry> bgm = new();             // BGM 라이브러리(인스펙터에서 등록)

    [Header("Mixer/Volumes (optional)")]
    [Range(0f, 1f)][SerializeField] private float masterSfx = 1f;    // 전체 SFX 마스터 볼륨(0~1)
    [Range(0f, 1f)][SerializeField] private float masterBgm = 1f;    // 전체 BGM 마스터 볼륨(0~1)

    [Header("Sources")]
    [SerializeField] private AudioSource bgmSource;                  // BGM 재생용 2D AudioSource(루프)
    [SerializeField] private AudioSource sfx2DSource;                // 2D 원샷 SFX 재생용 AudioSource

    // 루프 SFX는 owner+id 기준으로 AudioSource를 생성/재사용하기 위한 구조체다.
    private struct LoopInst
    {
        public AudioSource src;                                      // 루프 재생 AudioSource
        public Transform follow;                                     // 따라갈 대상(3D일 때 위치 동기화)
        public bool spatial3D;                                       // 이 루프가 3D로 동작하는지 여부
    }

    private readonly Dictionary<SfxId, SfxEntry> sfxMap = new();      // SfxId -> SfxEntry 빠른 조회 맵
    private readonly Dictionary<BgmId, BgmEntry> bgmMap = new();      // BgmId -> BgmEntry 빠른 조회 맵
    private readonly Dictionary<int, LoopInst> loops = new();         // (owner+id key) -> 루프 인스턴스 맵

    /// <summary>
    /// 유니티 생명주기: 싱글톤 초기화, AudioSource 준비, 라이브러리 맵 구성, DontDestroyOnLoad를 적용한다.
    /// </summary>
    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }  // 중복 인스턴스 제거
        I = this;                                                     // 싱글톤 등록

        if (!bgmSource)                                               // bgmSource가 비어있으면
        {
            bgmSource = gameObject.AddComponent<AudioSource>();       // 동적으로 생성
            bgmSource.playOnAwake = false;                            // 자동 재생 방지
            bgmSource.loop = true;                                    // BGM은 루프
            bgmSource.spatialBlend = 0f;                              // BGM은 2D
        }

        if (!sfx2DSource)                                             // sfx2DSource가 비어있으면
        {
            sfx2DSource = gameObject.AddComponent<AudioSource>();     // 동적으로 생성
            sfx2DSource.playOnAwake = false;                          // 자동 재생 방지
            sfx2DSource.loop = false;                                 // 원샷이므로 루프 아님
            sfx2DSource.spatialBlend = 0f;                            // 2D 원샷
        }

        BuildMaps();                                                  // 라이브러리 -> 맵 구성
        DontDestroyOnLoad(gameObject);                                // 씬이 바뀌어도 유지
    }

    /// <summary>
    /// 유니티 생명주기: 루프 SFX가 follow 대상이 있으면 매 프레임 위치를 따라가게 동기화한다.
    /// </summary>
    private void LateUpdate()
    {
        if (loops.Count == 0) return;                                 // 루프가 없으면 종료

        foreach (var kv in loops)                                     // 루프들 순회
        {
            var inst = kv.Value;                                      // 루프 인스턴스
            if (inst.src == null) continue;                           // 소스가 없으면 스킵
            if (inst.follow == null) continue;                        // follow 대상이 없으면 스킵
            if (!inst.spatial3D) continue;                            // 3D가 아니면 위치 동기화 불필요

            inst.src.transform.position = inst.follow.position;       // AudioSource 위치를 follow 위치로 갱신
        }
    }

    /// <summary>
    /// 인스펙터에 등록된 SFX/BGM 리스트를 빠른 조회용 Dictionary로 변환한다.
    /// </summary>
    private void BuildMaps()
    {
        sfxMap.Clear();                                               // 기존 SFX 맵 초기화
        for (int i = 0; i < sfx.Count; i++)                           // SFX 리스트 순회
        {
            var e = sfx[i];                                           // i번째 엔트리
            if (e == null) continue;                                  // null 방지
            if (e.id == SfxId.None) continue;                         // None은 등록하지 않음
            sfxMap[e.id] = e;                                         // 맵에 등록(중복이면 덮어씀)
        }

        bgmMap.Clear();                                               // 기존 BGM 맵 초기화
        for (int i = 0; i < bgm.Count; i++)                           // BGM 리스트 순회
        {
            var e = bgm[i];                                           // i번째 엔트리
            if (e == null) continue;                                  // null 방지
            if (e.id == BgmId.None) continue;                         // None은 등록하지 않음
            bgmMap[e.id] = e;                                         // 맵에 등록(중복이면 덮어씀)
        }
    }

    // =========================================================
    // ✅ 요구사항: PlaySFX "하나"로 재생/정지/루프 처리
    // =========================================================

    /// <summary>
    /// 효과음(SFX)을 재생한다.
    /// - stop=true면 루프 SFX만 정지한다(owner+id 기준).
    /// - loop=true면 owner+id 기준으로 루프 AudioSource를 만들거나 재사용한다.
    /// - worldPos가 있고 entry.spatial3D가 true면 3D 원샷/루프를 처리한다.
    /// </summary>
    /// <param name="id">재생할 SFX ID</param>
    /// <param name="worldPos">3D 원샷 재생 위치(옵션)</param>
    /// <param name="owner">루프 SFX 식별용 owner(옵션, loop/stop에서 중요)</param>
    /// <param name="follow">루프 SFX가 따라갈 Transform(옵션)</param>
    /// <param name="loop">true면 루프 SFX로 재생</param>
    /// <param name="stop">true면 해당 owner+id 루프를 정지</param>
    /// <param name="volumeMul">추가 볼륨 배수(최종 볼륨에 곱해짐)</param>
    public static void PlaySFX(
        SfxId id,
        Vector3? worldPos = null,
        UnityEngine.Object owner = null,
        Transform follow = null,
        bool loop = false,
        bool stop = false,
        float volumeMul = 1f
    )
    {
        if (I == null) return;                                       // SoundManager가 없으면 종료

        if (stop)                                                    // stop 요청이면
        {
            I.StopLoop(owner, id);                                   // 루프만 정지 처리
            return;                                                  // 종료
        }

        if (!I.sfxMap.TryGetValue(id, out var entry) || entry.clips == null || entry.clips.Length == 0)
            return;                                                  // 엔트리/클립이 없으면 재생 불가

        AudioClip clip = Pick(entry.clips);                          // 클립 랜덤 선택
        if (!clip) return;                                           // 선택 실패 시 종료

        float vol = Mathf.Clamp01(I.masterSfx * entry.volume * volumeMul);                // 최종 볼륨 계산(마스터*개별*배수)
        float pitch = UnityEngine.Random.Range(entry.pitchRange.x, entry.pitchRange.y);   // 피치 랜덤 계산

        if (loop)                                                    // 루프 재생이면
        {
            I.PlayLoop(owner, id, clip, vol, pitch, follow, entry, worldPos);            // 루프 처리
        }
        else                                                         // 원샷 재생이면
        {
            I.PlayOneShot(clip, vol, pitch, entry, worldPos);        // 원샷 처리
        }
    }

    /// <summary>
    /// 배경음(BGM)을 재생한다.
    /// - restart=false면 같은 클립이 이미 재생 중일 때는 유지한다.
    /// </summary>
    /// <param name="id">재생할 BGM ID</param>
    /// <param name="restart">true면 같은 곡이어도 처음부터 재생</param>
    public static void PlayBGM(BgmId id, bool restart = false)
    {
        if (I == null) return;                                       // SoundManager가 없으면 종료
        if (!I.bgmMap.TryGetValue(id, out var e) || !e.clip) return;  // 엔트리/클립이 없으면 종료

        if (!restart && I.bgmSource.isPlaying && I.bgmSource.clip == e.clip)
            return;                                                  // 같은 곡 재생 중 + restart=false면 유지

        I.bgmSource.clip = e.clip;                                   // BGM 클립 지정
        I.bgmSource.volume = Mathf.Clamp01(I.masterBgm * e.volume);   // BGM 볼륨 지정
        I.bgmSource.Play();                                          // 재생
    }

    /// <summary>
    /// 현재 재생 중인 BGM을 정지한다.
    /// </summary>
    public static void StopBGM()
    {
        if (I == null) return;                                       // SoundManager가 없으면 종료
        I.bgmSource.Stop();                                          // 정지
        I.bgmSource.clip = null;                                     // 클립 해제
    }

    // ---------------- internal ----------------

    /// <summary>
    /// 원샷 SFX를 재생한다.
    /// - 2D면 sfx2DSource.PlayOneShot 사용
    /// - 3D면 임시 GameObject + AudioSource를 만들어 재생 후 자동 파괴
    /// </summary>
    /// <param name="clip">재생할 오디오 클립</param>
    /// <param name="vol">볼륨</param>
    /// <param name="pitch">피치</param>
    /// <param name="entry">SFX 설정 엔트리(3D 설정 포함)</param>
    /// <param name="worldPos">3D 재생 위치</param>
    private void PlayOneShot(AudioClip clip, float vol, float pitch, SfxEntry entry, Vector3? worldPos)
    {
        bool use3D = entry.spatial3D && worldPos.HasValue;            // 3D로 재생할지 여부(설정+위치 유무)

        if (!use3D)                                                   // 2D 원샷이면
        {
            sfx2DSource.pitch = pitch;                                // 피치 적용
            sfx2DSource.PlayOneShot(clip, vol);                       // 2D 원샷 재생
            return;                                                   // 종료
        }

        var go = new GameObject($"SFX_{clip.name}");                  // 임시 오브젝트 생성
        go.transform.position = worldPos.Value;                       // 위치 지정

        var a = go.AddComponent<AudioSource>();                       // 임시 AudioSource 추가
        a.playOnAwake = false;                                        // 자동 재생 방지
        a.loop = false;                                               // 원샷
        a.clip = clip;                                                // 클립 지정
        a.volume = vol;                                               // 볼륨 지정
        a.pitch = pitch;                                              // 피치 지정
        a.spatialBlend = 1f;                                          // 3D
        a.minDistance = entry.minDistance;                            // 3D minDistance
        a.maxDistance = entry.maxDistance;                            // 3D maxDistance

        a.Play();                                                     // 재생
        Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);     // 재생 길이 기반으로 오브젝트 파괴 예약
    }

    /// <summary>
    /// 루프 SFX를 재생한다(owner+id 키로 생성/재사용).
    /// - follow가 있으면 위치를 따라가도록 LateUpdate에서 동기화된다.
    /// </summary>
    /// <param name="owner">루프 식별용 owner</param>
    /// <param name="id">루프 SFX ID</param>
    /// <param name="clip">재생할 클립</param>
    /// <param name="vol">볼륨</param>
    /// <param name="pitch">피치</param>
    /// <param name="follow">따라갈 Transform</param>
    /// <param name="entry">SFX 엔트리(3D 설정 포함)</param>
    /// <param name="worldPos">초기 위치(옵션)</param>
    private void PlayLoop(UnityEngine.Object owner, SfxId id, AudioClip clip, float vol, float pitch, Transform follow, SfxEntry entry, Vector3? worldPos)
    {
        int key = MakeKey(owner, id);                                 // owner+id 기반 키 생성

        if (!loops.TryGetValue(key, out var inst) || inst.src == null) // 기존 루프가 없으면 생성
        {
            var go = new GameObject($"SFXLoop_{id}");                 // 루프 오브젝트 생성
            if (worldPos.HasValue) go.transform.position = worldPos.Value;  // 위치 지정(옵션)
            else if (follow) go.transform.position = follow.position;       // follow 위치로 초기화

            var a = go.AddComponent<AudioSource>();                   // AudioSource 추가
            a.playOnAwake = false;                                    // 자동 재생 방지

            inst = new LoopInst
            {
                src = a,                                              // AudioSource 저장
                follow = follow,                                      // follow 저장
                spatial3D = entry.spatial3D && (follow != null || worldPos.HasValue) // 3D 여부(조건 충족 시)
            };
            loops[key] = inst;                                        // 딕셔너리에 등록
        }

        var src = inst.src;                                           // 실제 AudioSource
        src.clip = clip;                                              // 클립 지정
        src.loop = true;                                              // 루프
        src.volume = vol;                                             // 볼륨
        src.pitch = pitch;                                            // 피치

        if (inst.spatial3D)                                           // 3D 루프면
        {
            src.spatialBlend = 1f;                                    // 3D
            src.minDistance = entry.minDistance;                      // 거리 감쇠 시작
            src.maxDistance = entry.maxDistance;                      // 거리 감쇠 끝

            if (follow) src.transform.position = follow.position;     // follow 위치 적용
            else if (worldPos.HasValue) src.transform.position = worldPos.Value; // worldPos 적용
        }
        else                                                          // 2D 루프면
        {
            src.spatialBlend = 0f;                                    // 2D
        }

        inst.follow = follow;                                         // follow 갱신
        loops[key] = inst;                                            // 딕셔너리 갱신

        if (!src.isPlaying) src.Play();                               // 재생 중이 아니면 재생 시작
    }

    /// <summary>
    /// owner+id 기반으로 생성된 루프 SFX를 정지하고 오브젝트를 파괴한다.
    /// </summary>
    /// <param name="owner">루프 식별용 owner</param>
    /// <param name="id">루프 SFX ID</param>
    private void StopLoop(UnityEngine.Object owner, SfxId id)
    {
        int key = MakeKey(owner, id);                                 // 키 생성
        if (!loops.TryGetValue(key, out var inst) || inst.src == null) return; // 없으면 종료

        inst.src.Stop();                                              // 재생 정지
        Destroy(inst.src.gameObject);                                 // 오브젝트 파괴
        loops.Remove(key);                                            // 딕셔너리에서 제거
    }

    /// <summary>
    /// owner와 SfxId로 루프 딕셔너리 키를 생성한다.
    /// </summary>
    /// <param name="owner">루프 식별용 owner(없으면 0)</param>
    /// <param name="id">SFX ID</param>
    /// <returns>딕셔너리 키(int)</returns>
    private static int MakeKey(UnityEngine.Object owner, SfxId id)
    {
        int o = owner ? owner.GetInstanceID() : 0;                     // owner InstanceID(없으면 0)
        return (o * 397) ^ (int)id;                                    // 해시 조합(간단 XOR)
    }

    /// <summary>
    /// 클립 배열에서 랜덤으로 하나를 선택한다.
    /// </summary>
    /// <param name="clips">선택할 AudioClip 배열</param>
    /// <returns>선택된 AudioClip(없으면 null)</returns>
    private static AudioClip Pick(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;           // 비어있으면 null
        int idx = UnityEngine.Random.Range(0, clips.Length);           // 랜덤 인덱스
        return clips[idx];                                             // 선택 반환
    }
}
