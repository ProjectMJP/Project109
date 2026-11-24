using System.Collections.Generic;
using UnityEngine;
using XLua;

/// <summary>
/// ModLoader를 통해 메모리에 캐싱된 Data Class(YAML)를 바탕으로
/// 실제 인게임에서 동작하는 런타임 도메인 Object(Card, Effect, Relic)를 생성하고
/// 정적 카드 데이터 설명용 미리보기 인스턴스 수명주기를 관리하는 팩토리 클래스입니다.
/// </summary>
public static class ModObjectFactory
{
    private static readonly Dictionary<CardData, Card> _descriptionPreviewCards = new Dictionary<CardData, Card>();
    private static bool _isShutdownHookRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _descriptionPreviewCards.Clear();
        _isShutdownHookRegistered = false;
    }

    private static void EnsureShutdownHookRegistered()
    {
        if (_isShutdownHookRegistered) return;
        LuaManager.OnBeforeLuaEnvDispose -= DisposeCachedDescriptions;
        LuaManager.OnBeforeLuaEnvDispose += DisposeCachedDescriptions;
        _isShutdownHookRegistered = true;
    }

    /// <summary>
    /// 카드 데이터로부터 Lua 인스턴스가 바인딩된 런타임 Card 객체를 생성합니다.
    /// </summary>
    public static Card CreateCard(
        CardData cardData,
        Character owner = null,
        int runtimeId = -1,
        bool fallbackToPrototypeWithoutLuaManager = false)
    {
        if (cardData == null)
        {
            return null;
        }

        LuaTable luaInstance = null;
        if (cardData.luaPrototype != null)
        {
            if (LuaManager.Instance != null)
            {
                luaInstance = LuaManager.Instance.NewInstance(cardData.luaPrototype);
            }
            else if (fallbackToPrototypeWithoutLuaManager)
            {
                luaInstance = cardData.luaPrototype;
            }
        }

        return new Card(cardData, owner, luaInstance, runtimeId);
    }

    /// <summary>
    /// 카드 ID를 기반으로 런타임 Card 객체를 생성합니다.
    /// </summary>
    public static Card CreateCard(string cardId, Character owner = null, int runtimeId = -1)
    {
        if (string.IsNullOrEmpty(cardId) ||
            ModLoader.Instance == null ||
            ModLoader.Instance.CardDatabase == null ||
            !ModLoader.Instance.CardDatabase.TryGetValue(cardId, out CardData cardData))
        {
            Debug.LogError($"[ModObjectFactory] '{cardId}' 카드 데이터를 찾을 수 없습니다.");
            return null;
        }

        return CreateCard(cardData, owner, runtimeId);
    }

    /// <summary>
    /// 소유자 없는 정적 CardData의 동적 토큰 치환 설명 문자열을 반환합니다.
    /// 내부 미리보기 Card 인스턴스를 캐싱하여 반복 할당을 방지합니다.
    /// </summary>
    public static string GetCardDescription(CardData cardData)
    {
        if (cardData == null || string.IsNullOrEmpty(cardData.LocalizedDescription))
        {
            return string.Empty;
        }

        if (LuaManager.IsShuttingDown || LuaManager.IsDisposing)
        {
            return cardData.LocalizedDescription;
        }

        EnsureShutdownHookRegistered();

        if (!_descriptionPreviewCards.TryGetValue(cardData, out Card previewCard) ||
            previewCard == null ||
            previewCard.IsDisposed)
        {
            previewCard = CreateCard(cardData, null, -1);
            if (previewCard == null)
            {
                return cardData.LocalizedDescription;
            }
            _descriptionPreviewCards[cardData] = previewCard;
        }

        return previewCard.GetDescription();
    }

    /// <summary>
    /// 특정 CardData에 대해 캐싱된 설명용 미리보기 Card 인스턴스를 해제합니다.
    /// </summary>
    public static void DisposeCachedCardDescription(CardData cardData)
    {
        if (cardData == null) return;

        if (_descriptionPreviewCards.TryGetValue(cardData, out Card previewCard))
        {
            _descriptionPreviewCards.Remove(cardData);
            if (previewCard != null && !previewCard.IsDisposed)
            {
                previewCard.Dispose();
            }
        }
    }

    /// <summary>
    /// 캐싱된 모든 카드 설명용 미리보기 인스턴스를 일괄 해제합니다.
    /// </summary>
    public static void DisposeCachedDescriptions()
    {
        if (_descriptionPreviewCards.Count == 0) return;

        foreach (var pair in _descriptionPreviewCards)
        {
            if (pair.Value != null && !pair.Value.IsDisposed)
            {
                pair.Value.Dispose();
            }
        }
        _descriptionPreviewCards.Clear();
    }

    /// <summary>
    /// EffectData로부터 Effect 인스턴스를 생성합니다.
    /// </summary>
    public static Effect CreateEffect(EffectData data)
    {
        if (data == null)
        {
            return null;
        }

        LuaTable luaInstance = null;
        if (data.luaPrototype != null && LuaManager.Instance != null)
        {
            luaInstance = LuaManager.Instance.NewInstance(data.luaPrototype);
        }

        return new Effect(data, luaInstance);
    }

    /// <summary>
    /// 이펙트 ID를 기반으로 Effect 인스턴스를 생성합니다.
    /// </summary>
    public static Effect CreateEffect(string effectId)
    {
        if (string.IsNullOrEmpty(effectId) ||
            ModLoader.Instance == null ||
            ModLoader.Instance.EffectDatabase == null ||
            !ModLoader.Instance.EffectDatabase.TryGetValue(effectId, out EffectData data))
        {
            Debug.LogError($"[ModObjectFactory] '{effectId}' 이펙트 데이터를 찾을 수 없습니다.");
            return null;
        }

        return CreateEffect(data);
    }

    /// <summary>
    /// RelicData로부터 Relic 인스턴스를 생성합니다.
    /// </summary>
    public static Relic CreateRelic(RelicData data, Player owner)
    {
        if (data == null)
        {
            return null;
        }

        LuaTable luaInstance = null;
        if (data.luaPrototype != null && LuaManager.Instance != null)
        {
            luaInstance = LuaManager.Instance.NewInstance(data.luaPrototype);
        }

        return new Relic(data, owner, luaInstance);
    }

    /// <summary>
    /// 유물 ID를 기반으로 Relic 인스턴스를 생성합니다.
    /// </summary>
    public static Relic CreateRelic(string relicId, Player owner)
    {
        if (string.IsNullOrEmpty(relicId) ||
            ModLoader.Instance == null ||
            ModLoader.Instance.RelicDatabase == null ||
            !ModLoader.Instance.RelicDatabase.TryGetValue(relicId, out RelicData data))
        {
            Debug.LogError($"[ModObjectFactory] '{relicId}' 유물 데이터를 찾을 수 없습니다.");
            return null;
        }

        return CreateRelic(data, owner);
    }
}
