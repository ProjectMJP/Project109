using UnityEngine;

public enum LookDirection
{
    Up,
    Down,
    Left,
    Right
}

namespace GameItem.Types
{
    public enum CardRarity
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Unique = 4
    }

    public enum RelicRarity
    {
        Common = 1,
        Rare = 2,
        Unique = 3,
        Boss = 4
    }

    public enum ItemObtainSource
    {
        Standard = 0,   // 일반 드롭 및 상점 등장 가능 (기본값)
        Starter = 1,    // 시작 덱/무장 전용 (일반 드롭/상점 추첨 제외)
        EventOnly = 2,  // 특수 이벤트/대화 보상 전용 (일반 드롭/상점 추첨 제외)
        BossOnly = 3,   // 보스 전용 (일반 드롭/상점 추첨 제외, 보스 풀 전용)
        TestOnly = 4    // 테스트 전용 (일반 드롭/상점 추첨 및 도감 기본 노출 제외)
    }

    public enum RewardType
    {
        Gold,
        MemorySharp,
        CardChoice,     // 카드 선택지 제공 (DropTable 참조)
        Relic,          // 유물 획득 (DropTable 참조)
        SpecificCard,   // 특정 카드 획득
        SpecificRelic,  // 특정 유물 획득
        Potion,         // 포션 획득
        Heal,           // 체력 회복

        // 가독성 및 UI/아이템 일원화를 위한 별칭
        Card = CardChoice
    }
}

namespace CardTypes
{
    public enum CardType  //효과 타입
    {
        None,
        Damage,
        Heal,
        Shield,
        TemporaryShield,
        Buff,
        Debuff,
        Move
    }

    public enum CardPlayFailReason
    {
        None = 0,
        NotPlayerTurn,
        CharacterBusy,
        InsufficientStamina,
        BlockedByEffect,
        ConditionNotMet,
        NoValidTargets,
        Unknown
    }

    public enum CardPlayabilityVisualState
    {
        Default = 0,
        Playable = 1,
        InsufficientStamina = 2,
        UnplayableCondition = 3
    }

    public enum TargetType  //공격 대상 및 타깃팅 방식 타입
    {
        Self,          // 시전자 자신 대상 즉시 발동
        SingleEnemy,   // 사거리 내 단일 적 수동 지정
        AreaSelf,      // 시전자 중심 즉시 광역 발동 (회전베기, 지진 등)
        AreaGround,    // 사거리 내 바닥 타일 수동 지정 광역
        RandomEnemy,   // 사거리 내 무작위 적 N명 자동 타격
        MultiEnemy,    // 사거리 내 적 N명 순차 수동 지정
        AllEnemies,    // 전장의 모든 적 즉시 타격
        SingleAlly,    // 사거리 내 아군 1명 수동 지정 (미래 확장용)
        AllAllies,     // 전장의 모든 아군 즉시 적용 (미래 확장용)
        Directional    // 시전자가 바라보는 방향(facingDirection) 기준 즉시 발동 (허공 시전 허용)
    }

    public enum CardMasteryType
    {
        UseCard,
        DealDamage,
        GuardDamage,
        KillEnemy,
        Heal,
        DrawCard,
        UpgradeCard
    }

    public enum InteractableType
    {
        Normal,         // 일반 대화 및 이벤트 인터랙터블
        Chest,          // 보상 상자
        Shop,           // 상점 NPC
        RestoreBonfire  // 휴식/회복 모닥불
    }
}


