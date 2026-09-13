using EventStructs;
using System.Collections.Generic;

public interface ICharacterEvent { }

#region 데미지 관련 인터페이스 (Damage Interfaces)

    public interface IOnBeforeDealDamage : ICharacterEvent { void OnBeforeDealDamage(ref DamageInfo info); }
    public interface IOnBeforeTakeDamage : ICharacterEvent { void OnBeforeTakeDamage(ref DamageInfo info); }
    public interface IOnAfterDealDamage : ICharacterEvent { void OnAfterDealDamage(DamageInfo info); }
    public interface IOnAfterTakeDamage : ICharacterEvent { void OnAfterTakeDamage(DamageInfo info); }
    public interface IOnBreakShield : ICharacterEvent { void OnBreakShield(DamageInfo info); }
    public interface IOnShieldBroken : ICharacterEvent { void OnShieldBroken(DamageInfo info); }
    public interface IOnKill : ICharacterEvent { void OnKill(DamageInfo info); }

#endregion

#region 회복 관련 인터페이스 (Heal Interfaces)

    public interface IOnBeforeGiveHeal : ICharacterEvent { void OnBeforeGiveHeal(ref HealInfo info); }
    public interface IOnBeforeTakeHeal : ICharacterEvent { void OnBeforeTakeHeal(ref HealInfo info); }
    public interface IOnAfterGiveHeal : ICharacterEvent  { void OnAfterGiveHeal(HealInfo info); }
    public interface IOnAfterTakeHeal : ICharacterEvent  { void OnAfterTakeHeal(HealInfo info); }

#endregion

#region 방어도 관련 인터페이스 (Shield/Armor Interfaces)

    public interface IOnBeforeGiveShield : ICharacterEvent { void OnBeforeGiveShield(ref ShieldInfo info); }
    public interface IOnBeforeTakeShield : ICharacterEvent { void OnBeforeTakeShield(ref ShieldInfo info); }
    public interface IOnAfterGiveShield : ICharacterEvent  { void OnAfterGiveShield(ShieldInfo info); }
    public interface IOnAfterTakeShield : ICharacterEvent  { void OnAfterTakeShield(ShieldInfo info); }

#endregion

#region 스태미나 관련 인터페이스 (Stamina Interfaces)

    public interface IOnBeforeSpendStamina : ICharacterEvent { void OnBeforeSpendStamina(ref StaminaInfo info); }
    public interface IOnAfterSpendStamina : ICharacterEvent  { void OnAfterSpendStamina(StaminaInfo info); }
    public interface IOnBeforeTakeStamina : ICharacterEvent  { void OnBeforeTakeStamina(ref StaminaInfo info); }
    public interface IOnAfterTakeStamina : ICharacterEvent   { void OnAfterTakeStamina(StaminaInfo info); }
    public interface IOnBeforeGiveStamina : ICharacterEvent  { void OnBeforeGiveStamina(ref StaminaInfo info); }
    public interface IOnAfterGiveStamina : ICharacterEvent   { void OnAfterGiveStamina(StaminaInfo info); }

#endregion

#region 이동 관련 인터페이스 (Movement Interfaces)

    public interface IOnBeforeMove : ICharacterEvent { void OnBeforeMove(MoveInfo info); }
    public interface IOnAfterMove : ICharacterEvent  { void OnAfterMove(MoveInfo info); }
    public interface IOnBeforeForcedMove : ICharacterEvent { void OnBeforeForcedMove(MoveInfo info); }
    public interface IOnAfterForcedMove : ICharacterEvent { void OnAfterForcedMove(MoveInfo info); }

#endregion

#region 전투 관련 인터페이스 (Core State Interfaces)

    public interface IOnBattleStart : ICharacterEvent { void OnBattleStart(); }
    public interface IOnBattleEnd : ICharacterEvent   { void OnBattleEnd(); }
    public interface IOnTurnStart : ICharacterEvent { void OnTurnStart(); }
    public interface IOnTurnEnd : ICharacterEvent   { void OnTurnEnd(); }
    public interface IOnDeath : ICharacterEvent     { void OnDeath(DamageInfo fatalDamageInfo); }

#endregion

#region 카드 관련 인터페이스 (Card Interfaces)

    public interface IOnBeforeUseCard : ICharacterEvent { void OnBeforeUseCard(CardInfo info); }
    public interface IOnAfterUseCard : ICharacterEvent  { void OnAfterUseCard(CardInfo info); }
    public interface IOnTryUseCard : ICharacterEvent { void OnTryUseCard(CardInfo info); }
    public interface IOnDiscardCard : ICharacterEvent { void OnDiscardCard(CardInfo info); }
    public interface IOnDrawCard : ICharacterEvent { void OnDrawCard(Card cardData); }
    public interface IOnExhaustCard : ICharacterEvent { void OnExhaustCard(CardInfo info); }
    public interface IOnShuffleDeck : ICharacterEvent { void OnShuffleDeck(List<Card> deck); }
    public interface IOnEraseCard : ICharacterEvent { void OnEraseCard(CardInfo info); }

#endregion

#region 이펙트/버프/디버프 관련 인터페이스 (Effect Interfaces)

    public interface IOnBeforeGiveEffect : ICharacterEvent { void OnBeforeGiveEffect(ref EffectInfo info); }
    public interface IOnBeforeTakeEffect : ICharacterEvent { void OnBeforeTakeEffect(ref EffectInfo info); }
    public interface IOnAfterGiveEffect : ICharacterEvent  { void OnAfterGiveEffect(EffectInfo info); }
    public interface IOnAfterTakeEffect : ICharacterEvent  { void OnAfterTakeEffect(EffectInfo info); }
    public interface IOnBeforeRemoveEffect : ICharacterEvent { void OnBeforeRemoveEffect(Effect effect); }
    public interface IOnAfterRemoveEffect : ICharacterEvent  { void OnAfterRemoveEffect(Effect effect); }

#endregion

#region 피격/생명체 인터페이스 (Damageable Interface)

/// <summary>
/// 생명체(Character) 및 파괴 가능한 사물(DestructibleObject)이 공통으로 피격을 받을 수 있도록 규정하는 인터페이스.
/// </summary>
public interface IDamageable
{
    float curHealth { get; }
    float maxHealth { get; }
    bool isDead { get; }
    CharacterFaction faction { get; }

    void TakeDamage(DamageInfo info);
}

#endregion
