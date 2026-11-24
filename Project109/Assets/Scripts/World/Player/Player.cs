using System.Collections.Generic;
using System;

/// <summary>
/// 플레이어의 영속 데이터를 관리하는 클래스.
/// 던전 진입부터 게임 오버까지 유지된다.
/// </summary>
public class Player : IDisposable
{
    public Character character;
    public PlayerStat playerStat;

    public EventBus<IPlayerEvent> eventBus = new EventBus<IPlayerEvent>();

    // 카드 덱 (런 전체에서 유지되는 전체 카드 풀)
    public PlayerDeck deck { get; private set; }
    public IReadOnlyList<Card> masterDeck => deck.GetCards();

    // 전투 전용 카드 덱 (드로우더미, 손패, 버림패, 소멸패)
    public BattleDeck battleDeck { get; private set; }

    public void InitBattleDeck()
    {
        if (battleDeck == null)
        {
            battleDeck = new BattleDeck(character);
        }
        battleDeck.InitDeck(masterDeck);
    }

    public void DisposeBattleDeck(bool suppressEvents = false)
    {
        if (battleDeck != null)
        {
            battleDeck.DisposeDeck(suppressEvents);
        }
    }

    // 유물 관리자
    public RelicManager relicManager;

    // 선택된 시작 키트(무기/로드아웃)
    public string selectedStarterKitId { get; set; } = "warrior_starter";

    // 해금된 시작 키트 ID 및 무기 역사 스토리 키 집합 (스토리 키 형식: "{loadoutId}#{loreIndex}")
    public HashSet<string> unlockedStarterKitIds { get; private set; } = new HashSet<string>();
    public HashSet<string> unlockedLoreKeys { get; private set; } = new HashSet<string>();

    public bool IsStarterKitUnlocked(StarterKitData kit)
    {
        if (kit == null) return false;
        if (kit.isDefaultUnlocked || kit.unlockShardCost <= 0) return true;
        return unlockedStarterKitIds.Contains(kit.loadoutId);
    }

    public void UnlockStarterKit(string loadoutId)
    {
        if (!string.IsNullOrEmpty(loadoutId))
        {
            unlockedStarterKitIds.Add(loadoutId);
        }
    }

    public static string GetLoreUnlockKey(string loadoutId, int loreIndex)
    {
        return $"{loadoutId}#{loreIndex}";
    }

    public bool IsLoreUnlocked(string loadoutId, int loreIndex, WeaponLoreEntry lore)
    {
        if (lore == null) return false;
        if (lore.isUnlockedByDefault) return true;
        return unlockedLoreKeys.Contains(GetLoreUnlockKey(loadoutId, loreIndex));
    }

    public void UnlockLore(string loadoutId, int loreIndex)
    {
        if (!string.IsNullOrEmpty(loadoutId) && loreIndex >= 0)
        {
            unlockedLoreKeys.Add(GetLoreUnlockKey(loadoutId, loreIndex));
        }
    }

    public Player(Character character)
    {
        this.character = character;
        this.playerStat = new PlayerStat();
        this.playerStat.player = this;
        this.deck = new PlayerDeck(this);

        // 초기 스탯 세팅
        this.playerStat.ResetSessionDefaults();
        this.playerStat.InGameCurrencyGold = 0;

        this.relicManager = new RelicManager(this);
    }

    public void AddCardToDeck(Card card)
    {
        deck.AddCard(card);
    }

    public void RemoveCardFromDeck(Card card)
    {
        deck.RemoveCard(card);
    }

    // 유물 관련 기능들은 RelicManager로 위임
    public Relic AddRelic(string relicId, bool isRestoredFromSave = false, int restoredCounter = 0)
    {
        return relicManager.AddRelic(relicId, isRestoredFromSave, restoredCounter);
    }

    public void RemoveRelic(string relicId)
    {
        relicManager.RemoveRelic(relicId);
    }

    public Relic GetRandomRelic()
    {
        return relicManager.GetRandomRelic();
    }

    public Relic GetSpecificRelic(string relicId)
    {
        return relicManager.GetSpecificRelic(relicId);
    }

    public void Dispose()
    {
        battleDeck?.DisposeDeck(suppressEvents: true);
        deck?.Dispose();
        relicManager?.Dispose();
        eventBus?.Clear();
    }
}
