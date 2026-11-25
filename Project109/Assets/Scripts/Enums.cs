using UnityEngine;

public enum LookDirection
{
    East = 0,       //   0° (+X)
    NorthEast = 1,  //  60° (+X, +Z)
    NorthWest = 2,  // 120° (-X, +Z)
    West = 3,       // 180° (-X)
    SouthWest = 4,  // 240° (-X, -Z)
    SouthEast = 5   // 300° (+X, -Z)
}

/// <summary>
/// 6방향 육각 격자 기준 4종 방향 판정 구역입니다.
/// </summary>
public enum FacingZone
{
    DirectFront = 0, // 정전면 1방향 (0°)
    DirectBack = 1,  // 정후면 1방향 (180°)
    FrontArc = 2,    // 전방 3방향 (0°, ±60°)
    BackArc = 3      // 후방 3방향 (±120°, 180°)
}

/// <summary>
/// 피격자 시선 기준 공격 입사 방향의 세부 관계입니다.
/// </summary>
public enum FacingHitRelation
{
    None = 0,
    DirectFront = 1, // 정전면 (0°)
    FrontSide = 2,   // 전측면 (±60°)
    BackSide = 3,    // 후측면 (±120°)
    DirectBack = 4   // 정후면 (180°)
}

/// <summary>
/// 공격 피해의 방향 판정 원점/입사 벡터 결정 방식입니다.
/// </summary>
public enum FacingOriginMode
{
    Auto = 0,            // 활성 시전 컨텍스트가 있으면 사용, 없으면 공격자 위치 기준
    CasterPosition = 1,  // 공격자 현재 타일 기준 (근접/단일 타깃)
    OriginCoord = 2,     // 지정 원점 좌표 기준 (광역 AoE 중심점)
    TravelDirection = 3, // 지정 진행 벡터 기준 (직선 투사체/관통 사선)
    IgnoreFacing = 4     // 방향 판정 및 피격 회전 미적용 (지속피해/반사 등)
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


