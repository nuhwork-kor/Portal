// SoundManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum SfxId
{
    None = 0,
    StagePanel_LightOn,
    StagePanel_Buzz,
    PortalGun_Shoot,
    Player_Walk,
    Player_Land,
    ObjImpact_Cube,
    ObjImpact_BulletHitCube,
    Portal_BlueEnter,
    Portal_OrangeEnter,
    StageTeleport,
    Button_Interact,
    Door_Interact,
    Turret_LockOn,
    Turret_ReadyForShoot,
    Turret_Shoot
}

public enum BgmId
{
    None = 0,
    Main,
    InGame,
    Ending,
}

[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    [Serializable]
    public class SfxEntry
    {
        public SfxId id;
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("피치 랜덤 범위 (1,1이면 고정)")] public Vector2 pitchRange = new Vector2(1f, 1f);

        [Header("3D")]
        public bool spatial3D = true;
        [Tooltip("3D일 때 거리 감쇠")] public float minDistance = 1f;
        public float maxDistance = 25f;
    }

    [Serializable]
    public class BgmEntry
    {
        public BgmId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public static SoundManager I { get; private set; }

    [Header("Library")]
    [SerializeField] private List<SfxEntry> sfx = new();
    [SerializeField] private List<BgmEntry> bgm = new();

    [Header("Mixer/Volumes (optional)")]
    [Range(0f, 1f)][SerializeField] private float masterSfx = 1f;
    [Range(0f, 1f)][SerializeField] private float masterBgm = 1f;

    [Header("Sources")]
    [SerializeField] private AudioSource bgmSource;     // 2D
    [SerializeField] private AudioSource sfx2DSource;   // 2D oneshot

    // 루프 SFX는 owner+id 기준으로 AudioSource를 생성/재사용
    private struct LoopInst
    {
        public AudioSource src;
        public Transform follow;
        public bool spatial3D;
    }

    private readonly Dictionary<SfxId, SfxEntry> sfxMap = new();
    private readonly Dictionary<BgmId, BgmEntry> bgmMap = new();
    private readonly Dictionary<int, LoopInst> loops = new();

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        if (!bgmSource)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
        }

        if (!sfx2DSource)
        {
            sfx2DSource = gameObject.AddComponent<AudioSource>();
            sfx2DSource.playOnAwake = false;
            sfx2DSource.loop = false;
            sfx2DSource.spatialBlend = 0f;
        }

        BuildMaps();
        DontDestroyOnLoad(gameObject);
    }

    private void LateUpdate()
    {
        // follow 루프들은 매 프레임 위치 따라가게
        if (loops.Count == 0) return;

        foreach (var kv in loops)
        {
            var inst = kv.Value;
            if (inst.src == null) continue;
            if (inst.follow == null) continue;
            if (!inst.spatial3D) continue;

            inst.src.transform.position = inst.follow.position;
        }
    }

    private void BuildMaps()
    {
        sfxMap.Clear();
        for (int i = 0; i < sfx.Count; i++)
        {
            var e = sfx[i];
            if (e == null) continue;
            if (e.id == SfxId.None) continue;
            sfxMap[e.id] = e;
        }

        bgmMap.Clear();
        for (int i = 0; i < bgm.Count; i++)
        {
            var e = bgm[i];
            if (e == null) continue;
            if (e.id == BgmId.None) continue;
            bgmMap[e.id] = e;
        }
    }

    // =========================================================
    // ✅ 요구사항: PlaySFX "하나"로 재생/정지/루프 처리
    // =========================================================
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
        if (I == null) return;

        // stop 요청이면 루프만 정지
        if (stop)
        {
            I.StopLoop(owner, id);
            return;
        }

        if (!I.sfxMap.TryGetValue(id, out var entry) || entry.clips == null || entry.clips.Length == 0)
            return;

        AudioClip clip = Pick(entry.clips);
        if (!clip) return;

        float vol = Mathf.Clamp01(I.masterSfx * entry.volume * volumeMul);
        float pitch = UnityEngine.Random.Range(entry.pitchRange.x, entry.pitchRange.y);

        if (loop)
        {
            I.PlayLoop(owner, id, clip, vol, pitch, follow, entry, worldPos);
        }
        else
        {
            I.PlayOneShot(clip, vol, pitch, entry, worldPos);
        }
    }

    public static void PlayBGM(BgmId id, bool restart = false)
    {
        if (I == null) return;
        if (!I.bgmMap.TryGetValue(id, out var e) || !e.clip) return;

        if (!restart && I.bgmSource.isPlaying && I.bgmSource.clip == e.clip)
            return;

        I.bgmSource.clip = e.clip;
        I.bgmSource.volume = Mathf.Clamp01(I.masterBgm * e.volume);
        I.bgmSource.Play();
    }

    public static void StopBGM()
    {
        if (I == null) return;
        I.bgmSource.Stop();
        I.bgmSource.clip = null;
    }

    // ---------------- internal ----------------

    private void PlayOneShot(AudioClip clip, float vol, float pitch, SfxEntry entry, Vector3? worldPos)
    {
        // 3D 원샷이면 임시 오브젝트 만들어서 재생 후 파괴
        bool use3D = entry.spatial3D && worldPos.HasValue;

        if (!use3D)
        {
            sfx2DSource.pitch = pitch;
            sfx2DSource.PlayOneShot(clip, vol);
            return;
        }

        var go = new GameObject($"SFX_{clip.name}");
        go.transform.position = worldPos.Value;

        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.clip = clip;
        a.volume = vol;
        a.pitch = pitch;
        a.spatialBlend = 1f;
        a.minDistance = entry.minDistance;
        a.maxDistance = entry.maxDistance;

        a.Play();
        Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
    }

    private void PlayLoop(UnityEngine.Object owner, SfxId id, AudioClip clip, float vol, float pitch, Transform follow, SfxEntry entry, Vector3? worldPos)
    {
        int key = MakeKey(owner, id);

        if (!loops.TryGetValue(key, out var inst) || inst.src == null)
        {
            var go = new GameObject($"SFXLoop_{id}");
            if (worldPos.HasValue) go.transform.position = worldPos.Value;
            else if (follow) go.transform.position = follow.position;

            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;

            inst = new LoopInst
            {
                src = a,
                follow = follow,
                spatial3D = entry.spatial3D && (follow != null || worldPos.HasValue)
            };
            loops[key] = inst;
        }

        // 설정 업데이트
        var src = inst.src;
        src.clip = clip;
        src.loop = true;
        src.volume = vol;
        src.pitch = pitch;

        if (inst.spatial3D)
        {
            src.spatialBlend = 1f;
            src.minDistance = entry.minDistance;
            src.maxDistance = entry.maxDistance;

            if (follow) src.transform.position = follow.position;
            else if (worldPos.HasValue) src.transform.position = worldPos.Value;
        }
        else
        {
            src.spatialBlend = 0f;
        }

        // follow 갱신
        inst.follow = follow;
        loops[key] = inst;

        if (!src.isPlaying) src.Play();
    }

    private void StopLoop(UnityEngine.Object owner, SfxId id)
    {
        int key = MakeKey(owner, id);
        if (!loops.TryGetValue(key, out var inst) || inst.src == null) return;

        inst.src.Stop();
        Destroy(inst.src.gameObject);
        loops.Remove(key);
    }

    private static int MakeKey(UnityEngine.Object owner, SfxId id)
    {
        int o = owner ? owner.GetInstanceID() : 0;
        return (o * 397) ^ (int)id;
    }

    private static AudioClip Pick(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        int idx = UnityEngine.Random.Range(0, clips.Length);
        return clips[idx];
    }
}
