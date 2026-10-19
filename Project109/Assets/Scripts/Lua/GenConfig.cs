using UnityEngine;
using System;
using System.Collections.Generic;
using XLua;

public static class GenConfig
{
    // XLua가 Lua에서 C#을 호출할 때 필요한 바인딩 코드를 생성할 타입 목록
    [LuaCallCSharp]
    public static List<Type> LuaCallCSharp = new List<Type>()
    {
        // Core Game Types
        typeof(PlayerStat),
        typeof(CharacterStat),
        typeof(CardData),
        typeof(Card),
        typeof(RelicData),
        typeof(Relic),
        typeof(Player),
        typeof(Character),
        typeof(RelicManager),
        typeof(DialogueManager),

        // Yaml / Data Types
        typeof(ChoiceData),
        typeof(DialogueData),
        typeof(InteractableData),
        typeof(InteractableObject),
        typeof(ShopNPC),
        typeof(RestoreBonfire),
        typeof(RewardChest),
        typeof(RewardData),
        typeof(DropTableData),
        typeof(Effect),

        // Event Payloads & Flags
        typeof(EventStructs.DamageInfo),
        typeof(EventStructs.HealInfo),
        typeof(EventStructs.StaminaInfo),
        typeof(EventStructs.ShieldInfo),
        typeof(EventStructs.CardInfo),
        typeof(EventStructs.MoveInfo),
        typeof(EventStructs.EffectInfo),
        typeof(CardTag),
        typeof(EventStructs.DamageFlag),
        typeof(EventStructs.HealFlag),
        typeof(EventStructs.StaminaFlag),
        typeof(EventStructs.ShieldFlag),
        typeof(EventStructs.CardFlag),
        typeof(EventStructs.MoveFlag),
        typeof(EventStructs.EffectFlag),
        typeof(CharacterFaction),
        typeof(EventStructs.DefaultDamageTypeID),

        // Collections
        typeof(List<CardData>),
        typeof(List<Card>),
        typeof(List<RelicData>),
        typeof(List<Relic>),
        typeof(IReadOnlyList<Relic>),
        typeof(List<int>),
        typeof(List<float>),
        typeof(List<string>),
    };

    // XLua가 C#에서 Lua(인터페이스/델리게이트 프록시)를 호출할 때 필요한 코드를 생성할 타입 목록
    [CSharpCallLua]
    public static List<Type> CSharpCallLua = new List<Type>()
    {
        // Delegates
        typeof(CanSelectDialogueChoice),
        typeof(ExecuteDialogueChoice),
        typeof(GetDialogueChoiceDescription),
        typeof(IsDialogueChoiceVisible),
        typeof(Func<LuaTable, LuaTable>),
        typeof(Action<LuaTable, Card, Character>),
        typeof(Action<LuaTable, EventStructs.CardInfo>),
        typeof(Func<LuaTable, Card, Character, bool>),
        typeof(Func<LuaTable, Card, string, string>),
        typeof(Action<LuaTable, Effect>),
        typeof(Action<LuaTable, Effect, Character, Character, int, float>),
        typeof(Action<LuaTable, Effect, int, float>),
        typeof(Func<LuaTable, Effect, string, string>),
        typeof(Action<LuaTable, Relic, Player>),
        typeof(Func<LuaTable, Relic, string, string>),
        typeof(Action<LuaTable, CardTag, Card>),
        typeof(Action<LuaTable, CardTag>),
        typeof(Action<LuaTable, Character, NPCUnitController>),
        typeof(Func<LuaTable, Character, LuaTable>),
        typeof(Func<LuaTable, Character, string>),
        typeof(Func<LuaTable, string>),

        // Character Event Interfaces (Damage)
        typeof(IOnBeforeDealDamage),
        typeof(IOnBeforeTakeDamage),
        typeof(IOnAfterDealDamage),
        typeof(IOnAfterTakeDamage),
        typeof(IOnBreakShield),
        typeof(IOnShieldBroken),
        typeof(IOnKill),

        // Character Event Interfaces (Heal)
        typeof(IOnBeforeGiveHeal),
        typeof(IOnBeforeTakeHeal),
        typeof(IOnAfterGiveHeal),
        typeof(IOnAfterTakeHeal),

        // Character Event Interfaces (Shield)
        typeof(IOnBeforeGiveShield),
        typeof(IOnBeforeTakeShield),
        typeof(IOnAfterGiveShield),
        typeof(IOnAfterTakeShield),

        // Character Event Interfaces (Stamina)
        typeof(IOnBeforeSpendStamina),
        typeof(IOnAfterSpendStamina),
        typeof(IOnBeforeTakeStamina),
        typeof(IOnAfterTakeStamina),
        typeof(IOnBeforeGiveStamina),
        typeof(IOnAfterGiveStamina),

        // Character Event Interfaces (Move)
        typeof(IOnBeforeMove),
        typeof(IOnAfterMove),
        typeof(IOnBeforeForcedMove),
        typeof(IOnAfterForcedMove),

        // Character Event Interfaces (Battle/Turn)
        typeof(IOnBattleStart),
        typeof(IOnBattleEnd),
        typeof(IOnTurnStart),
        typeof(IOnTurnEnd),
        typeof(IOnDeath),

        // Character Event Interfaces (Card)
        typeof(IOnBeforeUseCard),
        typeof(IOnAfterUseCard),
        typeof(IOnTryUseCard),
        typeof(IOnDiscardCard),
        typeof(IOnDrawCard),
        typeof(IOnExhaustCard),
        typeof(IOnShuffleDeck),
        typeof(IOnEraseCard),

        // Character Event Interfaces (Effect)
        typeof(IOnBeforeGiveEffect),
        typeof(IOnBeforeTakeEffect),
        typeof(IOnAfterGiveEffect),
        typeof(IOnAfterTakeEffect),
        typeof(IOnBeforeRemoveEffect),
        typeof(IOnAfterRemoveEffect),

        // Player Event Interfaces
        typeof(IOnAddRelic),
        typeof(IOnRemoveRelic),
        typeof(IOnAddCard),
        typeof(IOnRemoveCard),
        typeof(IOnCardUpgrade),
        typeof(IOnCardMasteryUpgrade),
        typeof(IOnCardsRefreshed),
        typeof(IOnAddGold),
        typeof(IOnRemoveGold),
        typeof(IOnAddMemorySharp),
        typeof(IOnRemoveMemorySharp),
    };
}
