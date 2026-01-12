using UnityEngine;

public class Enums
{
    // 아이템 등급
    public enum ItemRank
    {
        Common,
        Rare,
        Unique,
        Legendary
    }

    // 아이템 시너지
    // 임시 시너지 3개
    public enum ItemSynergy
    {
        None,
        Fire,
        Ice,
        Wind,
        Dark,
        Light
    }

    // 아이템 추가 효과
    // 이건 정해진게 없음. 예시일 뿐임
    public enum ItemEffect
    {
        None,
        AutoAttack,
        AutoBuff,
        LifeSteal
    }

    // 몬스터 등급(종류)
    public enum MonsterRank
    {
        Normal,
        Elite,
        Boss
    }

    // 방(노드) 종류
    public enum RoomType
    {
        None,
        Normal,
        Elite,
        Shop,
        Rest,
        Forge,
        Boss
    }

    //던전 클리어 형태
    public enum DungeonClearResult
    {
        NormalClear,        // 일반 방 클리어
        StageBossClear,     // 중간 스테이지 보스 클리어
        FinalBossClear      // 마지막 스테이지 보스 클리어
    }


    // 던전 테마
    public enum DungeonTheme
    {
        None,
        Desert,
        //Grass,
        //Lava,
        Snow
    }

    // 클래스(직업) 종류
    public enum ClassType
    {
        Warrior,
        Mage,
        Archer
    }

    // 몬스터 상태 종류
    // 텔레포트, 도망, 달리기(?), 패트롤 등 추가적으로 생각
    public enum MonsterState
    {
        Idle,
        Move,
        Attack,
        Die
    }
    public enum JumpPhase
    {
        None,
        Windup,
        Air,
        LandRecovery
    }

    // 인벤토리 모드
    public enum InventoryMode
    {
        None,               // 기본 상태
        Swap,               // 아이템 위치 교환 모드
        RankUp,             // 아이템 등급 상승 모드
        RanKUpWithSynergy   // 시너지 유지 아이템 등급 상승 모드
    }

    // 오브젝트 풀링 오브젝트 타입
    public enum PoolType
    {
        DescriptionSynergy,
        SynergyEffects,
        SynergyStatText,
        ArrowPool,
        MonsterProjectile,
        MonsterArcProjectile,
        BossBullet
    }

    // 플레이어 애니메이션
    public enum PlayerAnimState
    {
        Idle,
        Move,
        Attack
    }

    //플레이어 현재 장소
    public enum currentPlayerPlace
    {
        dungeonOut,
        dungeonIn
    }


    //스킬 카드 타입
    public enum SkillCardType
    {
        NewSkill,
        LevelUpSkill
    }

    //게임 상태
    public enum GamePlayState
    {
        Playing,                    //정상 플레이
        LevelUpUI,                  //레벨업 UI 중 정지 
        Paused                      //일시 정지 (환경설정 같은거 누를때 용)
    }

    // 특성 종류
    public enum TraitType
    {
        Attack,
        Health,
        Luck,
        Intelligence,
        speed
    }

    // 무적 이유
    public enum InvincibleReason
    {
        Hit,
        Dash
    }

    //이펙트 타입(이름)
    public enum EffectType
    {
        BaseAttack,
        Skill_MultiSlash,
        Skill_Heal,
        Skill_DamageBuff,
        Dash,
        LevelUp
    }

    // 로딩 씬 타입
    public enum LoadingType
    {
        None,                   // 아무 연출없음
        Logo,                   // 로고 나오는 연출
        CircleInBlack,          // 원이 퍼지고 작아지는 연출 (로딩이 짧은 씬 전환)
        ProgressBar             // 프로그레스 바 (로딩이 긴 씬 전환)
    }
}
