using System.Collections.Generic;
using UnityEngine;

// 이 스크립트는 여러 StageRoot(스테이지 묶음)를 stageIndex로 관리하고,
// 특정 스테이지만 활성화하거나(OnlyActive), 개별 스테이지를 On/Off 하며,
// 현재 스테이지 인덱스(CurrentStageIndex)와 클리어 상태를 기록하는 매니저다.
[DisallowMultipleComponent]
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; } // StageManager 싱글톤 인스턴스

    [Header("Stages (0~3)")]
    [Tooltip("StageRoot가 들어 있어야(0~3). stageIndex가 중복되면 안 됨.")]
    [SerializeField] private StageRoot[] stages; // 인스펙터에서 등록하는 StageRoot 배열(각 스테이지 루트)

    [Min(0)]
    [SerializeField] private int startStageIndex = 0; // 게임 시작 시 활성화할 스테이지 인덱스

    public int CurrentStageIndex { get; private set; } = 0; // 현재(논리적으로) 플레이 중인 스테이지 인덱스

    private readonly Dictionary<int, StageRoot> stageMap = new Dictionary<int, StageRoot>(8); // stageIndex -> StageRoot 매핑 딕셔너리
    private readonly HashSet<int> clearedStages = new HashSet<int>(); // 클리어한 스테이지 인덱스 집합

    /// <summary>
    /// 싱글톤을 초기화하고, StageRoot들을 stageIndex로 맵핑한 뒤,
    /// 시작 시 모든 스테이지를 비활성화하고 startStageIndex만 활성화한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)                             // 이미 다른 인스턴스가 있으면
        {
            Destroy(gameObject);                                              // 중복 제거
            return;                                                           // 종료
        }
        Instance = this;                                                      // 싱글톤 등록

        BuildMap();                                                           // StageRoot -> stageIndex 맵 구성

        foreach (var kv in stageMap)                                          // 모든 스테이지를
        {
            if (kv.Value) kv.Value.gameObject.SetActive(false);               // 일단 전부 비활성화
        }

        SetOnlyActiveStage(startStageIndex);                                  // 시작 스테이지만 활성화
    }

    /// <summary>
    /// stages 배열을 읽어서 stageIndex 기준으로 stageMap을 만든다.
    /// stageIndex가 중복되면 에러를 출력하고 해당 항목은 스킵한다.
    /// </summary>
    private void BuildMap()
    {
        stageMap.Clear();                                                     // 기존 맵 초기화

        if (stages == null) return;                                           // stages가 없으면 종료

        for (int i = 0; i < stages.Length; i++)                               // stages를 순회하며
        {
            var s = stages[i];                                                // i번째 StageRoot 가져오기
            if (!s) continue;                                                 // null이면 스킵

            int idx = s.StageIndex;                                           // StageRoot의 stageIndex 읽기
            if (stageMap.ContainsKey(idx))                                    // 중복이면
            {
                Debug.LogError($"[StageManager] stageIndex 중복: {idx}. StageRoot를 확인."); // 에러 출력
                continue;                                                     // 스킵
            }

            stageMap.Add(idx, s);                                             // 맵에 등록
        }
    }

    /// <summary>
    /// stageIndex에 해당하는 StageRoot가 존재하고 null이 아니면 true를 반환한다.
    /// </summary>
    /// <param name="stageIndex">찾을 스테이지 인덱스</param>
    /// <param name="root">찾은 StageRoot</param>
    /// <returns>존재하면 true</returns>
    public bool TryGetStage(int stageIndex, out StageRoot root)               // out으로 StageRoot 반환 시도
        => stageMap.TryGetValue(stageIndex, out root) && root != null;        // 딕셔너리 성공 + root null 아님

    /// <summary>
    /// stageIndex로 StageRoot를 가져온다. 없으면 null을 반환한다.
    /// </summary>
    /// <param name="stageIndex">가져올 스테이지 인덱스</param>
    /// <returns>StageRoot 또는 null</returns>
    public StageRoot GetStage(int stageIndex)
    {
        stageMap.TryGetValue(stageIndex, out var r);                          // stageMap에서 검색
        return r;                                                             // 결과 반환(없으면 null)
    }

    /// <summary>
    /// 특정 스테이지만 활성화하고, 나머지 모든 스테이지는 비활성화한다.
    /// CurrentStageIndex도 해당 stageIndex로 갱신한다.
    /// </summary>
    /// <param name="stageIndex">활성화할 스테이지 인덱스</param>
    public void SetOnlyActiveStage(int stageIndex)
    {
        if (!TryGetStage(stageIndex, out var _))                              // 해당 스테이지가 없으면
        {
            Debug.LogError($"[StageManager] 없는 스테이지 index: {stageIndex}"); // 에러 출력
            return;                                                           // 종료
        }

        foreach (var kv in stageMap)                                          // 모든 스테이지를 순회하며
        {
            if (!kv.Value) continue;                                          // null이면 스킵
            kv.Value.gameObject.SetActive(kv.Key == stageIndex);              // 목표 스테이지만 true, 나머지 false
        }

        CurrentStageIndex = stageIndex;                                       // 현재 스테이지 인덱스 갱신
    }

    /// <summary>
    /// 특정 스테이지를 개별적으로 On/Off 한다.
    /// (전환 중 2개 스테이지를 동시에 켜야 하는 경우 등에 사용)
    /// </summary>
    /// <param name="stageIndex">대상 스테이지 인덱스</param>
    /// <param name="active">활성화 여부</param>
    public void SetStageActive(int stageIndex, bool active)
    {
        if (!TryGetStage(stageIndex, out var root)) return;                   // 스테이지가 없으면 종료
        root.gameObject.SetActive(active);                                    // 해당 스테이지 활성/비활성
    }

    /// <summary>
    /// 트리거나 외부 로직에서 "현재 스테이지 인덱스"만 갱신하고 싶을 때 사용한다.
    /// (실제 GameObject 활성화는 건드리지 않는다.)
    /// </summary>
    /// <param name="stageIndex">새 CurrentStageIndex</param>
    public void SetCurrentStage(int stageIndex)
    {
        CurrentStageIndex = stageIndex;                                       // 현재 스테이지 인덱스만 세팅
    }

    /// <summary>
    /// 스테이지를 클리어 상태로 기록한다.
    /// </summary>
    /// <param name="stageIndex">클리어 처리할 스테이지 인덱스</param>
    public void MarkCleared(int stageIndex)
    {
        clearedStages.Add(stageIndex);                                        // HashSet에 추가(중복은 자동 무시)
    }

    /// <summary>
    /// 해당 스테이지가 클리어 되었는지 반환한다.
    /// </summary>
    /// <param name="stageIndex">확인할 스테이지 인덱스</param>
    /// <returns>클리어면 true</returns>
    public bool IsCleared(int stageIndex)
    {
        return clearedStages.Contains(stageIndex);                            // HashSet 포함 여부 반환
    }
}
