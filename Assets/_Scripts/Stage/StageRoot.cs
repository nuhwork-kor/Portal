using UnityEngine;

// StageRoot: 스테이지 루트 오브젝트에 붙는 "스테이지 식별자" 컴포넌트(StageManager가 stageIndex로 스테이지를 찾을 때 사용)
[DisallowMultipleComponent] // 같은 오브젝트에 StageRoot가 2개 이상 붙는 걸 방지
public class StageRoot : MonoBehaviour
{
    [Header("Identity")]                    // 인스펙터에서 스테이지 식별 관련 필드 그룹 라벨
    [Min(0)]                                // stageIndex가 0 미만으로 내려가지 않게 제한(Inspector)
    [SerializeField] private int stageIndex = 0; // 스테이지 고유 인덱스(0~N). StageManager에서 키로 사용

    /// <summary>
    /// 이 StageRoot가 담당하는 스테이지 인덱스를 반환합니다. (읽기 전용)
    /// </summary>
    public int StageIndex => stageIndex;    // 외부에서 stageIndex를 안전하게 조회하기 위한 프로퍼티
}
