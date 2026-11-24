using UnityEngine;
using System.Collections.Generic;
using GameItem.Types;

public class GameItemRewardManager : MonoBehaviour
{
    public static GameItemRewardManager instance { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(this.gameObject);
            }
        }
        else if (instance != this)
        {
            Destroy(this.gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // 초기화 시점 외부 주입 정적 데이터베이스 참조
    private IReadOnlyDictionary<string, CardData> _cardDatabase;
    private IReadOnlyDictionary<string, RelicData> _relicDatabase;
    private IReadOnlyDictionary<string, DropTableData> _dropTableDatabase;

    /// <summary>
    /// 외부 오케스트레이터로부터 데이터베이스가 주입되었는지 여부입니다.
    /// </summary>
    public bool IsDatabasesInitialized => _cardDatabase != null && _relicDatabase != null;

    // Cards
    private List<CardData> commonCardList = new List<CardData>();
    private List<CardData> uncommonCardList = new List<CardData>();
    private List<CardData> rareCardList = new List<CardData>();
    private List<CardData> uniqueCardList = new List<CardData>();

    // Relics
    private List<RelicData> commonRelicList = new List<RelicData>();
    private List<RelicData> rareRelicList = new List<RelicData>();
    private List<RelicData> uniqueRelicList = new List<RelicData>();
    private List<RelicData> bossRelicList = new List<RelicData>();

    // 드롭 테이블 필터링 시 힙 할당 제거용 재사용 버퍼 및 소유 유물 조회 캐시
    private readonly List<CardData> _filteredCommonCards = new List<CardData>();
    private readonly List<CardData> _filteredUncommonCards = new List<CardData>();
    private readonly List<CardData> _filteredRareCards = new List<CardData>();
    private readonly List<CardData> _filteredUniqueCards = new List<CardData>();

    private readonly List<RelicData> _filteredCommonRelics = new List<RelicData>();
    private readonly List<RelicData> _filteredRareRelics = new List<RelicData>();
    private readonly List<RelicData> _filteredUniqueRelics = new List<RelicData>();
    private readonly List<RelicData> _filteredBossRelics = new List<RelicData>();

    private readonly HashSet<string> _ownedRelicNameSet = new HashSet<string>();

    /// <summary>
    /// 일반 드롭 및 상점 가중치 추첨 풀에 등장 가능한 카드인지 판별합니다.
    /// 강화 카드(isUpgraded) 및 Starter/EventOnly/BossOnly/TestOnly 카드를 제외합니다.
    /// </summary>
    public static bool IsCardObtainableInRandomPool(CardData card)
    {
        return card != null && !card.isUpgraded && card.obtainSource == ItemObtainSource.Standard;
    }

    /// <summary>
    /// 일반 드롭 및 상점 가중치 추첨 풀에 등장 가능한 유물인지 판별합니다.
    /// Standard 유물과 Boss 등급의 BossOnly 유물만 허용하고 Starter/EventOnly/TestOnly 유물을 제외합니다.
    /// </summary>
    public static bool IsRelicObtainableInRandomPool(RelicData relic)
    {
        return relic != null &&
               (relic.obtainSource == ItemObtainSource.Standard ||
                (relic.obtainSource == ItemObtainSource.BossOnly && relic.rarity == RelicRarity.Boss));
    }

    /// <summary>
    /// 카드·유물·드롭테이블 정적 데이터베이스 참조를 외부에서 주입하고 등급별 추첨 풀을 동기화합니다.
    /// </summary>
    public void InitializeDatabases(
        IReadOnlyDictionary<string, CardData> cardDatabase,
        IReadOnlyDictionary<string, RelicData> relicDatabase,
        IReadOnlyDictionary<string, DropTableData> dropTableDatabase = null)
    {
        _cardDatabase = cardDatabase;
        _relicDatabase = relicDatabase;
        _dropTableDatabase = dropTableDatabase;
        UpdateItemList();
    }

    public void UpdateItemList()
    {
        // 이전 목록 비우기 (누적 증식 방지)
        commonCardList.Clear();
        uncommonCardList.Clear();
        rareCardList.Clear();
        uniqueCardList.Clear();

        commonRelicList.Clear();
        rareRelicList.Clear();
        uniqueRelicList.Clear();
        bossRelicList.Clear();

        if (_cardDatabase != null)
        {
            foreach (CardData data in _cardDatabase.Values)
            {
                if (!IsCardObtainableInRandomPool(data))
                {
                    continue; // 업그레이드 카드 및 일반 풀 제외 획득처 카드 배제
                }

                switch (data.rarity)
                {
                    case CardRarity.Common:
                        commonCardList.Add(data);
                        break;
                    case CardRarity.Uncommon:
                        uncommonCardList.Add(data);
                        break;
                    case CardRarity.Rare:
                        rareCardList.Add(data);
                        break;
                    case CardRarity.Unique:
                        uniqueCardList.Add(data);
                        break;
                }
            }
        }

        if (_relicDatabase != null)
        {
            foreach (RelicData data in _relicDatabase.Values)
            {
                if (!IsRelicObtainableInRandomPool(data))
                {
                    continue; // Starter/EventOnly/TestOnly 등 일반 풀 제외 획득처 유물 배제
                }

                switch (data.rarity)
                {
                    case GameItem.Types.RelicRarity.Common:
                        commonRelicList.Add(data);
                        break;
                    case GameItem.Types.RelicRarity.Rare:
                        rareRelicList.Add(data);
                        break;
                    case GameItem.Types.RelicRarity.Unique:
                        uniqueRelicList.Add(data);
                        break;
                    case GameItem.Types.RelicRarity.Boss:
                        bossRelicList.Add(data);
                        break;
                }
            }
        }
    }

    public CardData GetRandomCardDataByDropTable(string dropTableID)
    {
        if (TryResolveDropTableAndSpecific(dropTableID, _cardDatabase, out DropTableData dropTable, out CardData specificCard))
        {
            return specificCard;
        }

        _filteredCommonCards.Clear();
        _filteredUncommonCards.Clear();
        _filteredRareCards.Clear();
        _filteredUniqueCards.Clear();

        if (_cardDatabase != null)
        {
            foreach (var card in _cardDatabase.Values)
            {
                if (!IsCardObtainableInRandomPool(card)) continue;
                if (!PassesDropTableFilter(card.cardName, card.classTypes, dropTable)) continue;

                // 카드 타입 필터
                if (dropTable != null && dropTable.allowedCardTypes != null && dropTable.allowedCardTypes.Count > 0)
                {
                    if (string.IsNullOrEmpty(card.cardType) || !dropTable.allowedCardTypes.Contains(card.cardType))
                    {
                        continue;
                    }
                }

                switch (card.rarity)
                {
                    case CardRarity.Common:
                        _filteredCommonCards.Add(card);
                        break;
                    case CardRarity.Uncommon:
                        _filteredUncommonCards.Add(card);
                        break;
                    case CardRarity.Rare:
                        _filteredRareCards.Add(card);
                        break;
                    case CardRarity.Unique:
                        _filteredUniqueCards.Add(card);
                        break;
                }
            }
        }

        int commonRate = dropTable != null ? dropTable.commonWeight : 60;
        int uncommonRate = dropTable != null ? dropTable.uncommonWeight : 40;
        int rareRate = dropTable != null ? dropTable.rareWeight : 0;
        int uniqueRate = dropTable != null ? dropTable.uniqueWeight : 0;

        return PickWeightedByRarity(
            commonRate, uncommonRate, rareRate, uniqueRate,
            _filteredCommonCards, _filteredUncommonCards, _filteredRareCards, _filteredUniqueCards,
            commonCardList, uncommonCardList, rareCardList, uniqueCardList);
    }

    public RelicData GetRandomRelicDataByDropTable(string dropTableID)
    {
        if (TryResolveDropTableAndSpecific(dropTableID, _relicDatabase, out DropTableData dropTable, out RelicData specificRelic))
        {
            return specificRelic;
        }

        _filteredCommonRelics.Clear();
        _filteredRareRelics.Clear();
        _filteredUniqueRelics.Clear();
        _filteredBossRelics.Clear();
        RefreshOwnedRelicCache();

        if (_relicDatabase != null)
        {
            foreach (var relic in _relicDatabase.Values)
            {
                if (!IsRelicObtainableInRandomPool(relic)) continue;
                if (_ownedRelicNameSet.Contains(relic.relicName)) continue;
                if (!PassesDropTableFilter(relic.relicName, relic.classTypes, dropTable)) continue;

                switch (relic.rarity)
                {
                    case RelicRarity.Common:
                        _filteredCommonRelics.Add(relic);
                        break;
                    case RelicRarity.Rare:
                        _filteredRareRelics.Add(relic);
                        break;
                    case RelicRarity.Unique:
                        _filteredUniqueRelics.Add(relic);
                        break;
                    case RelicRarity.Boss:
                        _filteredBossRelics.Add(relic);
                        break;
                }
            }
        }

        int commonRate = dropTable != null ? dropTable.commonWeight : 70;
        int rareRate = dropTable != null ? dropTable.rareWeight : 30;
        int uniqueRate = dropTable != null ? dropTable.uniqueWeight : 0;
        int bossRate = dropTable != null ? dropTable.uncommonWeight : 0; // Relic의 경우 uncommonWeight를 Boss 가중치로 맵핑

        return PickWeightedByRarity(
            commonRate, rareRate, uniqueRate, bossRate,
            _filteredCommonRelics, _filteredRareRelics, _filteredUniqueRelics, _filteredBossRelics,
            commonRelicList, rareRelicList, uniqueRelicList, bossRelicList);
    }

    private void RefreshOwnedRelicCache()
    {
        _ownedRelicNameSet.Clear();
        if (GameSceneManager.instance == null || GameSceneManager.instance.player == null || GameSceneManager.instance.player.relicManager == null)
        {
            return;
        }

        var ownedRelics = GameSceneManager.instance.player.relicManager.GetRelics();
        for (int i = 0; i < ownedRelics.Count; i++)
        {
            var owned = ownedRelics[i];
            if (owned != null && owned.Data != null && !string.IsNullOrEmpty(owned.Data.relicName))
            {
                _ownedRelicNameSet.Add(owned.Data.relicName);
            }
        }
    }

    private bool TryResolveDropTableAndSpecific<T>(
        string dropTableID,
        IReadOnlyDictionary<string, T> database,
        out DropTableData dropTable,
        out T specificItem) where T : class
    {
        dropTable = null;
        specificItem = null;

        if (_dropTableDatabase != null && !string.IsNullOrEmpty(dropTableID))
        {
            _dropTableDatabase.TryGetValue(dropTableID, out dropTable);
        }

        if (dropTable != null && dropTable.specificItemIDs != null && dropTable.specificItemIDs.Count > 0 && database != null)
        {
            string randomName = dropTable.specificItemIDs[Random.Range(0, dropTable.specificItemIDs.Count)];
            if (database.TryGetValue(randomName, out specificItem) && specificItem != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool PassesDropTableFilter(string itemID, List<string> itemClasses, DropTableData dropTable)
    {
        if (dropTable == null) return true;

        // 1. 제외 아이템 필터
        if (dropTable.excludedItemIDs != null && dropTable.excludedItemIDs.Contains(itemID))
        {
            return false;
        }

        // 2. 직업 필터
        if (dropTable.allowedClassTypes != null && dropTable.allowedClassTypes.Count > 0)
        {
            if (itemClasses == null) return false;

            bool classMatched = false;
            for (int i = 0; i < itemClasses.Count; i++)
            {
                if (dropTable.allowedClassTypes.Contains(itemClasses[i]))
                {
                    classMatched = true;
                    break;
                }
            }
            if (!classMatched) return false;
        }

        return true;
    }

    /// <summary>
    /// 4단계 희귀도 가중치에 따라 아이템을 추첨하고, 해당 풀과 폴백 풀에서 순차적으로 대체 아이템을 선택합니다.
    /// </summary>
    private T PickWeightedByRarity<T>(
        int tier1Rate,
        int tier2Rate,
        int tier3Rate,
        int tier4Rate,
        List<T> tier1List,
        List<T> tier2List,
        List<T> tier3List,
        List<T> tier4List,
        List<T> fallback1List,
        List<T> fallback2List,
        List<T> fallback3List,
        List<T> fallback4List) where T : class
    {
        int totalWeight = tier1Rate + tier2Rate + tier3Rate + tier4Rate;
        if (totalWeight <= 0) totalWeight = 100;

        int pickNumber = Random.Range(1, totalWeight + 1);
        T picked = null;

        if (pickNumber <= tier4Rate)
        {
            picked = PickRandomFromList(tier4List)
                ?? PickRandomFromList(tier3List)
                ?? PickRandomFromList(tier2List)
                ?? PickRandomFromList(tier1List);
        }
        else if (pickNumber <= tier4Rate + tier3Rate)
        {
            picked = PickRandomFromList(tier3List)
                ?? PickRandomFromList(tier4List)
                ?? PickRandomFromList(tier2List)
                ?? PickRandomFromList(tier1List);
        }
        else if (pickNumber <= tier4Rate + tier3Rate + tier2Rate)
        {
            picked = PickRandomFromList(tier2List)
                ?? PickRandomFromList(tier1List)
                ?? PickRandomFromList(tier3List)
                ?? PickRandomFromList(tier4List);
        }
        else
        {
            picked = PickRandomFromList(tier1List)
                ?? PickRandomFromList(tier2List)
                ?? PickRandomFromList(tier3List)
                ?? PickRandomFromList(tier4List);
        }

        // 필터링 조건 때문에 아무 아이템도 뽑지 못했다면 전체 풀에서 폴백
        if (picked == null)
        {
            picked = PickRandomFromList(fallback1List)
                ?? PickRandomFromList(fallback2List)
                ?? PickRandomFromList(fallback3List)
                ?? PickRandomFromList(fallback4List);
        }

        return picked;
    }

    private T PickRandomFromList<T>(List<T> list) where T : class
    {
        if (list == null || list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    /// <summary>
    /// 카드 ID(cardName)로 주입된 CardDatabase에서 CardData를 조회합니다.
    /// </summary>
    public CardData GetCardDataByName(string cardName)
    {
        if (string.IsNullOrEmpty(cardName) || _cardDatabase == null)
        {
            return null;
        }

        _cardDatabase.TryGetValue(cardName, out CardData cardData);
        return cardData;
    }

    /// <summary>
    /// 유물 ID(relicName)로 주입된 RelicDatabase에서 RelicData를 조회합니다.
    /// </summary>
    public RelicData GetRelicDataByName(string relicName)
    {
        if (string.IsNullOrEmpty(relicName) || _relicDatabase == null)
        {
            return null;
        }

        _relicDatabase.TryGetValue(relicName, out RelicData relicData);
        return relicData;
    }

    /// <summary>
    /// 미수령 카드 보상 엔트리에 대해 기추첨 결과를 재사용하거나, 최초 추첨 후 엔트리에 고정 기록합니다.
    /// </summary>
    public List<CardData> ResolveOrRollCardsForEntry(PendingRewardEntry entry, int fallbackCount = 3)
    {
        List<CardData> result = new List<CardData>();
        if (entry == null)
        {
            return GetUniqueRandomCards("Default_Card_Table", Mathf.Max(1, fallbackCount));
        }

        if (entry.rewardType == RewardType.SpecificCard && !string.IsNullOrEmpty(entry.referenceID))
        {
            CardData specificCard = GetCardDataByName(entry.referenceID);
            if (specificCard != null)
            {
                if (entry.rolledCardNames == null)
                {
                    entry.rolledCardNames = new List<string>();
                }
                entry.rolledCardNames.Clear();
                entry.rolledCardNames.Add(specificCard.cardName);
                result.Add(specificCard);
                return result;
            }
        }

        if (entry.rolledCardNames != null && entry.rolledCardNames.Count > 0)
        {
            for (int i = 0; i < entry.rolledCardNames.Count; i++)
            {
                CardData cachedCard = GetCardDataByName(entry.rolledCardNames[i]);
                if (cachedCard != null)
                {
                    result.Add(cachedCard);
                }
            }

            if (result.Count > 0)
            {
                return result;
            }
        }

        int targetCount = entry.amount > 0 ? entry.amount : Mathf.Max(1, fallbackCount);
        string dropTableID = string.IsNullOrEmpty(entry.referenceID) ? "Default_Card_Table" : entry.referenceID;
        List<CardData> rolledCards = GetUniqueRandomCards(dropTableID, targetCount);

        if (entry.rolledCardNames == null)
        {
            entry.rolledCardNames = new List<string>();
        }
        entry.rolledCardNames.Clear();

        for (int i = 0; i < rolledCards.Count; i++)
        {
            if (rolledCards[i] != null && !string.IsNullOrEmpty(rolledCards[i].cardName))
            {
                entry.rolledCardNames.Add(rolledCards[i].cardName);
            }
        }

        return rolledCards;
    }

    /// <summary>
    /// 미수령 유물 보상 엔트리에 대해 기추첨 유물을 재사용하거나, 최초 추첨 후 엔트리에 고정 기록합니다.
    /// </summary>
    public RelicData ResolveOrRollRelicForEntry(PendingRewardEntry entry)
    {
        if (entry == null)
        {
            return GetRandomRelicDataByDropTable("Default_Relic_Table");
        }

        if (entry.rewardType == RewardType.SpecificRelic && !string.IsNullOrEmpty(entry.referenceID))
        {
            RelicData specificRelic = GetRelicDataByName(entry.referenceID);
            if (specificRelic != null)
            {
                entry.rolledRelicName = specificRelic.relicName;
                return specificRelic;
            }
        }

        if (!string.IsNullOrEmpty(entry.rolledRelicName))
        {
            RelicData cachedRelic = GetRelicDataByName(entry.rolledRelicName);
            if (cachedRelic != null)
            {
                return cachedRelic;
            }
        }

        string dropTableID = string.IsNullOrEmpty(entry.referenceID) ? "Default_Relic_Table" : entry.referenceID;
        RelicData rolledRelic = GetRandomRelicDataByDropTable(dropTableID);
        if (rolledRelic != null && !string.IsNullOrEmpty(rolledRelic.relicName))
        {
            entry.rolledRelicName = rolledRelic.relicName;
        }
        return rolledRelic;
    }

    /// <summary>
    /// 드롭 테이블을 기반으로 중복 없는 무작위 카드 목록을 지정한 개수만큼 추첨합니다.
    /// </summary>
    public List<CardData> GetUniqueRandomCards(string dropTableID, int count)
    {
        List<CardData> result = new List<CardData>();
        HashSet<string> pickedNames = new HashSet<string>();
        int attempts = 0;
        int maxAttempts = Mathf.Max(50, count * 20);

        while (result.Count < count && attempts < maxAttempts)
        {
            attempts++;
            var card = GetRandomCardDataByDropTable(dropTableID);
            if (card == null) break;
            if (pickedNames.Add(card.cardName))
            {
                result.Add(card);
            }
        }
        return result;
    }

    /// <summary>
    /// 드롭 테이블을 기반으로 중복 없는 무작위 유물 목록을 지정한 개수만큼 추첨합니다.
    /// </summary>
    public List<RelicData> GetUniqueRandomRelics(string dropTableID, int count)
    {
        List<RelicData> result = new List<RelicData>();
        HashSet<string> pickedNames = new HashSet<string>();
        int attempts = 0;
        int maxAttempts = Mathf.Max(50, count * 20);

        while (result.Count < count && attempts < maxAttempts)
        {
            attempts++;
            var relic = GetRandomRelicDataByDropTable(dropTableID);
            if (relic == null) break;
            if (pickedNames.Add(relic.relicName))
            {
                result.Add(relic);
            }
        }
        return result;
    }
}
