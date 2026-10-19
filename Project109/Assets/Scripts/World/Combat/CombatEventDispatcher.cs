using EventStructs;
using System.Collections.Generic;

/// <summary>
/// 전투 이벤트 버스(EventBus)에 등록된 리스너들에게 무할당(Zero-Alloc) 역순 루프로 
/// 이벤트를 디스패치하는 정적 유틸리티 클래스입니다.
/// </summary>
public static class CombatEventDispatcher
{
    #region Damage Dispatchers

    public static void DispatchBeforeDealDamage(EventBus<ICharacterEvent> bus, ref DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeDealDamage>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeDealDamage(ref info);
        }
    }

    public static void DispatchBeforeTakeDamage(EventBus<ICharacterEvent> bus, ref DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeTakeDamage>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeTakeDamage(ref info);
        }
    }

    public static void DispatchAfterDealDamage(EventBus<ICharacterEvent> bus, DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterDealDamage>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterDealDamage(info);
        }
    }

    public static void DispatchAfterTakeDamage(EventBus<ICharacterEvent> bus, DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterTakeDamage>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterTakeDamage(info);
        }
    }

    public static void DispatchKill(EventBus<ICharacterEvent> bus, DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnKill>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnKill(info);
        }
    }

    public static void DispatchDeath(EventBus<ICharacterEvent> bus, DamageInfo fatalDamageInfo)
    {
        if (bus != null && bus.TryGetListeners<IOnDeath>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnDeath(fatalDamageInfo);
        }
    }

    public static void DispatchBreakShield(EventBus<ICharacterEvent> bus, DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBreakShield>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBreakShield(info);
        }
    }

    public static void DispatchShieldBroken(EventBus<ICharacterEvent> bus, DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnShieldBroken>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnShieldBroken(info);
        }
    }

    #endregion

    #region Heal Dispatchers

    public static void DispatchBeforeGiveHeal(EventBus<ICharacterEvent> bus, ref HealInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeGiveHeal>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeGiveHeal(ref info);
        }
    }

    public static void DispatchBeforeTakeHeal(EventBus<ICharacterEvent> bus, ref HealInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeTakeHeal>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeTakeHeal(ref info);
        }
    }

    public static void DispatchAfterGiveHeal(EventBus<ICharacterEvent> bus, HealInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterGiveHeal>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterGiveHeal(info);
        }
    }

    public static void DispatchAfterTakeHeal(EventBus<ICharacterEvent> bus, HealInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterTakeHeal>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterTakeHeal(info);
        }
    }

    #endregion

    #region Stamina Dispatchers

    public static void DispatchBeforeGiveStamina(EventBus<ICharacterEvent> bus, ref StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeGiveStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeGiveStamina(ref info);
        }
    }

    public static void DispatchBeforeTakeStamina(EventBus<ICharacterEvent> bus, ref StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeTakeStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeTakeStamina(ref info);
        }
    }

    public static void DispatchAfterGiveStamina(EventBus<ICharacterEvent> bus, StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterGiveStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterGiveStamina(info);
        }
    }

    public static void DispatchAfterTakeStamina(EventBus<ICharacterEvent> bus, StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterTakeStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterTakeStamina(info);
        }
    }

    public static void DispatchBeforeSpendStamina(EventBus<ICharacterEvent> bus, ref StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeSpendStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeSpendStamina(ref info);
        }
    }

    public static void DispatchAfterSpendStamina(EventBus<ICharacterEvent> bus, StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterSpendStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterSpendStamina(info);
        }
    }

    #endregion

    #region Shield Dispatchers

    public static void DispatchBeforeGiveShield(EventBus<ICharacterEvent> bus, ref ShieldInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeGiveShield>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeGiveShield(ref info);
        }
    }

    public static void DispatchBeforeTakeShield(EventBus<ICharacterEvent> bus, ref ShieldInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeTakeShield>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeTakeShield(ref info);
        }
    }

    public static void DispatchAfterGiveShield(EventBus<ICharacterEvent> bus, ShieldInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterGiveShield>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterGiveShield(info);
        }
    }

    public static void DispatchAfterTakeShield(EventBus<ICharacterEvent> bus, ShieldInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterTakeShield>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterTakeShield(info);
        }
    }

    #endregion

    #region Movement Dispatchers

    public static void DispatchBeforeMove(EventBus<ICharacterEvent> bus, ref MoveInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeMove>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeMove(ref info);
        }
    }

    public static void DispatchAfterMove(EventBus<ICharacterEvent> bus, MoveInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterMove>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterMove(info);
        }
    }

    public static void DispatchBeforeForcedMove(EventBus<ICharacterEvent> bus, ref MoveInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeForcedMove>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeForcedMove(ref info);
        }
    }

    public static void DispatchAfterForcedMove(EventBus<ICharacterEvent> bus, MoveInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterForcedMove>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterForcedMove(info);
        }
    }

    #endregion
}
