using UnityEngine;

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

    public enum RewardItemType
    {
        Card,
        Relic
    }

    public enum ItemRewardUIType
    {
        Gold,
        MemorySharp,
        Card,
        Relic
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
        Heal            // 체력 회복
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

        // 레거시 하위 호환 별칭
        Target = SingleEnemy,
        Area = AreaSelf,
        All = AllEnemies
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
}

