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

    public enum TargetType  //공격 대상 타입
    {
        Target,
        Area,
        Self,
        All
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

