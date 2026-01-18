using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("Stages (0~3)")]
    [Tooltip("StageRoot를 전부 넣어라(0~3). stageIndex가 중복되면 안 됨.")]
    [SerializeField] private StageRoot[] stages;

    [Min(0)]
    [SerializeField] private int startStageIndex = 0;

    public int CurrentStageIndex { get; private set; } = 0;

    private readonly Dictionary<int, StageRoot> stageMap = new Dictionary<int, StageRoot>(8);
    private readonly HashSet<int> clearedStages = new HashSet<int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildMap();

        // 시작 시: 전부 꺼두고 시작 스테이지만 켠다
        foreach (var kv in stageMap)
        {
            if (kv.Value) kv.Value.gameObject.SetActive(false);
        }

        SetOnlyActiveStage(startStageIndex);
    }

    private void BuildMap()
    {
        stageMap.Clear();

        if (stages == null) return;

        for (int i = 0; i < stages.Length; i++)
        {
            var s = stages[i];
            if (!s) continue;

            int idx = s.StageIndex;
            if (stageMap.ContainsKey(idx))
            {
                Debug.LogError($"[StageManager] stageIndex 중복: {idx}. StageRoot를 확인해.");
                continue;
            }

            stageMap.Add(idx, s);
        }
    }

    public bool TryGetStage(int stageIndex, out StageRoot root)
        => stageMap.TryGetValue(stageIndex, out root) && root != null;

    public StageRoot GetStage(int stageIndex)
    {
        stageMap.TryGetValue(stageIndex, out var r);
        return r;
    }

    /// <summary>
    /// 특정 스테이지만 활성(나머지는 전부 비활성)
    /// </summary>
    public void SetOnlyActiveStage(int stageIndex)
    {
        if (!TryGetStage(stageIndex, out var _))
        {
            Debug.LogError($"[StageManager] 없는 스테이지 index: {stageIndex}");
            return;
        }

        foreach (var kv in stageMap)
        {
            if (!kv.Value) continue;
            kv.Value.gameObject.SetActive(kv.Key == stageIndex);
        }

        CurrentStageIndex = stageIndex;
    }

    /// <summary>
    /// 스테이지 개별 On/Off (전환 중에 2개 켜야 할 때 사용)
    /// </summary>
    public void SetStageActive(int stageIndex, bool active)
    {
        if (!TryGetStage(stageIndex, out var root)) return;
        root.gameObject.SetActive(active);
    }

    /// <summary>
    /// 전환 트리거에서 "현재 스테이지" 인덱스를 확정할 때 사용
    /// </summary>
    public void SetCurrentStage(int stageIndex)
    {
        CurrentStageIndex = stageIndex;
    }

    public void MarkCleared(int stageIndex)
    {
        clearedStages.Add(stageIndex);
    }

    public bool IsCleared(int stageIndex)
    {
        return clearedStages.Contains(stageIndex);
    }

    public Transform GetRespawnPoint(int stageIndex)
    {
        var s = GetStage(stageIndex);
        return s ? s.RespawnPoint : null;
    }
}
