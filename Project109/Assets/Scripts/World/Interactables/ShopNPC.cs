using GameItem.Types;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class ShopItemEntryBase
{
    public int basePrice;
    public bool isSold;
    public bool isOnSale;
    public float discountRate;
}

[Serializable]
public class ShopCardEntry : ShopItemEntryBase
{
    public CardData cardData;
}

[Serializable]
public class ShopRelicEntry : ShopItemEntryBase
{
    public RelicData relicData;
}

/// <summary>
/// 상점 슬롯의 원가, 최종 할인가 및 할인 출처 정보를 담는 불변 가격 구조체입니다.
/// </summary>
public readonly struct ShopPriceInfo
{
    public readonly int OriginalPrice;
    public readonly int FinalPrice;
    public readonly bool IsItemOnSale;
    public readonly float ItemDiscountRate;
    public readonly bool IsRelicDiscountApplied;

    public bool IsDiscounted => FinalPrice < OriginalPrice;

    public ShopPriceInfo(
        int originalPrice,
        int finalPrice,
        bool isItemOnSale,
        float itemDiscountRate,
        bool isRelicDiscountApplied)
    {
        OriginalPrice = originalPrice;
        FinalPrice = finalPrice;
        IsItemOnSale = isItemOnSale;
        ItemDiscountRate = itemDiscountRate;
        IsRelicDiscountApplied = isRelicDiscountApplied;
    }
}

public class ShopNPC : InteractableObject
{
    public const string DISCOUNT_RELIC_ID = "할인 카드";

    private ShopPanel shopUI;

    [Header("Shop Display Settings")]
    public int cardCount = 8;
    public int relicCount = 3;

    [Header("Reroll Settings")]
    public int baseRerollCost = 50;
    public int rerollCostIncrement = 25;
    public bool resetRerollCostPerShopVisit = false;

    [Header("Pricing Settings")]
    [Range(0f, 0.5f)]
    public float cardPriceVarianceRatio = CardData.DefaultShopPriceVarianceRatio;

    [Header("Random Sale Settings")]
    public int randomCardSaleCount = 1;
    public int randomRelicSaleCount = 0;
    [Range(0f, 0.9f)]
    public float randomSaleDiscountRate = 0.5f;

    public List<ShopCardEntry> cardEntries = new List<ShopCardEntry>();
    public List<ShopRelicEntry> relicEntries = new List<ShopRelicEntry>();

    private bool isInitialized = false;
    private int currentRerollCount = 0;
    private Player _boundPlayer;

    public int CurrentRerollCount => currentRerollCount;

    public override void SetInteractableData(InteractableData data)
    {
        base.SetInteractableData(data);
        InitializeShop();
    }

    public override void OnInteract()
    {
        if (interactableData != null && !string.IsNullOrEmpty(interactableData.targetDialogueID))
        {
            TriggerDialogue();
        }
        else
        {
            OpenShopDirectly();
        }
    }

    public void BindPlayer(Player player)
    {
        _boundPlayer = player;
    }

    private Player ResolvePlayer(Player player = null)
    {
        if (player != null) return player;
        if (_boundPlayer != null) return _boundPlayer;
        return GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
    }

    public int GetPlayerGold(Player player = null)
    {
        Player targetPlayer = ResolvePlayer(player);
        return (targetPlayer != null && targetPlayer.playerStat != null)
            ? targetPlayer.playerStat.InGameCurrencyGold
            : 0;
    }

    public bool IsCardSlotPurchased(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < cardEntries.Count && cardEntries[slotIndex] != null && cardEntries[slotIndex].isSold;
    }

    public bool IsRelicSlotPurchased(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < relicEntries.Count && relicEntries[slotIndex] != null && relicEntries[slotIndex].isSold;
    }

    public bool IsCardSlotOnSale(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < cardEntries.Count && cardEntries[slotIndex] != null &&
               cardEntries[slotIndex].isOnSale && cardEntries[slotIndex].discountRate > 0f;
    }

    public bool IsRelicSlotOnSale(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < relicEntries.Count && relicEntries[slotIndex] != null &&
               relicEntries[slotIndex].isOnSale && relicEntries[slotIndex].discountRate > 0f;
    }

    public ShopCardEntry AddCardEntry(CardData card, int? basePriceOverride = null, bool isOnSale = false, float discountRate = 0f)
    {
        int resolvedBasePrice = basePriceOverride.HasValue && basePriceOverride.Value > 0
            ? basePriceOverride.Value
            : (card != null ? card.BaseGoldValue : CardData.DefaultCommonGoldValue);

        float clampedRate = isOnSale ? Mathf.Clamp(discountRate, 0f, 0.9f) : 0f;
        ShopCardEntry entry = new ShopCardEntry
        {
            cardData = card,
            basePrice = resolvedBasePrice,
            isSold = false,
            isOnSale = isOnSale && clampedRate > 0f,
            discountRate = clampedRate
        };
        cardEntries.Add(entry);
        return entry;
    }

    public ShopRelicEntry AddRelicEntry(RelicData relic, int? basePriceOverride = null, bool isOnSale = false, float discountRate = 0f)
    {
        int resolvedBasePrice = basePriceOverride.HasValue && basePriceOverride.Value > 0
            ? basePriceOverride.Value
            : (relic != null ? relic.BaseGoldValue : 150);

        float clampedRate = isOnSale ? Mathf.Clamp(discountRate, 0f, 0.9f) : 0f;
        ShopRelicEntry entry = new ShopRelicEntry
        {
            relicData = relic,
            basePrice = resolvedBasePrice,
            isSold = false,
            isOnSale = isOnSale && clampedRate > 0f,
            discountRate = clampedRate
        };
        relicEntries.Add(entry);
        return entry;
    }

    public int GetEffectiveCardCount(Player player = null)
    {
        Player targetPlayer = ResolvePlayer(player);
        int bonus = (targetPlayer != null && targetPlayer.playerStat != null)
            ? targetPlayer.playerStat.BonusShopCardCount
            : 0;
        return Mathf.Max(1, cardCount + bonus);
    }

    public int GetEffectiveRelicCount(Player player = null)
    {
        Player targetPlayer = ResolvePlayer(player);
        int bonus = (targetPlayer != null && targetPlayer.playerStat != null)
            ? targetPlayer.playerStat.BonusShopRelicCount
            : 0;
        return Mathf.Max(0, relicCount + bonus);
    }

    public static bool HasDiscountRelic(Player player)
    {
        return player != null &&
               player.relicManager != null &&
               player.relicManager.GetSpecificRelic(DISCOUNT_RELIC_ID) != null;
    }

    public bool IsDiscountActive(Player player = null)
    {
        return HasDiscountRelic(ResolvePlayer(player));
    }

    public static int CalculateCardPrice(CardData card, Player player = null)
    {
        return CalculateCardPrice(card, HasDiscountRelic(player));
    }

    public static int CalculateCardPrice(CardData card, bool applyDiscount)
    {
        int basePrice = card != null ? card.BaseGoldValue : CardData.DefaultCommonGoldValue;
        return applyDiscount ? Mathf.RoundToInt(basePrice * 0.7f) : basePrice;
    }

    public static int CalculateRelicPrice(RelicData relic, Player player = null)
    {
        return CalculateRelicPrice(relic, HasDiscountRelic(player));
    }

    public static int CalculateRelicPrice(RelicData relic, bool applyDiscount)
    {
        int basePrice = relic != null ? relic.BaseGoldValue : 150;
        return applyDiscount ? Mathf.RoundToInt(basePrice * 0.7f) : basePrice;
    }

    public int GetCardPrice(CardData card, Player player = null)
    {
        return CalculateCardPrice(card, ResolvePlayer(player));
    }

    public int GetCardSlotBasePrice(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < cardEntries.Count && cardEntries[slotIndex] != null)
        {
            ShopCardEntry entry = cardEntries[slotIndex];
            if (entry.basePrice > 0)
            {
                return entry.basePrice;
            }
            if (entry.cardData != null)
            {
                return entry.cardData.BaseGoldValue;
            }
        }

        return CardData.DefaultCommonGoldValue;
    }

    /// <summary>
    /// 개별 품목 세일(discountRate)과 할인 카드 유물(30%)을 곱연산으로 결합해 슬롯 가격 정보를 산출합니다.
    /// </summary>
    public static ShopPriceInfo CalculateEntryPriceInfo(ShopItemEntryBase entry, int fallbackBasePrice, bool hasDiscountRelic)
    {
        int originalPrice = (entry != null && entry.basePrice > 0) ? entry.basePrice : fallbackBasePrice;
        bool isItemOnSale = entry != null && entry.isOnSale && entry.discountRate > 0f;
        float clampedRate = isItemOnSale ? Mathf.Clamp(entry.discountRate, 0f, 0.9f) : 0f;
        float multiplier = (1f - clampedRate) * (hasDiscountRelic ? 0.7f : 1f);
        int finalPrice = originalPrice > 0 ? Mathf.Max(1, Mathf.RoundToInt(originalPrice * multiplier)) : 0;
        return new ShopPriceInfo(originalPrice, finalPrice, isItemOnSale, clampedRate, hasDiscountRelic);
    }

    public ShopPriceInfo GetCardSlotPriceInfo(int slotIndex, Player player = null)
    {
        ShopCardEntry entry = (slotIndex >= 0 && slotIndex < cardEntries.Count) ? cardEntries[slotIndex] : null;
        int fallbackBasePrice = GetCardSlotBasePrice(slotIndex);
        bool hasRelicDiscount = HasDiscountRelic(ResolvePlayer(player));
        return CalculateEntryPriceInfo(entry, fallbackBasePrice, hasRelicDiscount);
    }

    public int GetCardSlotPrice(int slotIndex, Player player = null)
    {
        return GetCardSlotPriceInfo(slotIndex, player).FinalPrice;
    }

    public int GetRelicPrice(RelicData relic, Player player = null)
    {
        return CalculateRelicPrice(relic, ResolvePlayer(player));
    }

    public int GetRelicSlotBasePrice(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < relicEntries.Count && relicEntries[slotIndex] != null)
        {
            ShopRelicEntry entry = relicEntries[slotIndex];
            if (entry.basePrice > 0)
            {
                return entry.basePrice;
            }
            if (entry.relicData != null)
            {
                return entry.relicData.BaseGoldValue;
            }
        }

        return 150;
    }

    public ShopPriceInfo GetRelicSlotPriceInfo(int slotIndex, Player player = null)
    {
        ShopRelicEntry entry = (slotIndex >= 0 && slotIndex < relicEntries.Count) ? relicEntries[slotIndex] : null;
        int fallbackBasePrice = GetRelicSlotBasePrice(slotIndex);
        bool hasRelicDiscount = HasDiscountRelic(ResolvePlayer(player));
        return CalculateEntryPriceInfo(entry, fallbackBasePrice, hasRelicDiscount);
    }

    public int GetRelicSlotPrice(int slotIndex, Player player = null)
    {
        return GetRelicSlotPriceInfo(slotIndex, player).FinalPrice;
    }

    public int GetCurrentRerollCost(Player player = null)
    {
        Player targetPlayer = ResolvePlayer(player);
        int rerolls = resetRerollCostPerShopVisit
            ? currentRerollCount
            : ((targetPlayer != null && targetPlayer.playerStat != null)
                ? targetPlayer.playerStat.ShopRerollCount
                : currentRerollCount);

        int rawCost = Mathf.Max(0, baseRerollCost + (Mathf.Max(0, rerolls) * rerollCostIncrement));
        return HasDiscountRelic(targetPlayer) ? Mathf.RoundToInt(rawCost * 0.7f) : rawCost;
    }

    public bool TryPurchaseCard(int slotIndex, out string failMessage)
    {
        if (slotIndex < 0 || slotIndex >= cardEntries.Count || cardEntries[slotIndex] == null || cardEntries[slotIndex].cardData == null)
        {
            failMessage = "유효하지 않은 카드 슬롯입니다.";
            return false;
        }

        ShopCardEntry entry = cardEntries[slotIndex];
        if (entry.isSold)
        {
            failMessage = "이미 구매한 상품입니다.";
            return false;
        }

        Player targetPlayer = ResolvePlayer();
        if (targetPlayer == null || targetPlayer.playerStat == null || targetPlayer.deck == null)
        {
            failMessage = "플레이어 정보를 찾을 수 없습니다.";
            return false;
        }

        CardData cardData = entry.cardData;
        int price = GetCardSlotPrice(slotIndex, targetPlayer);
        int gold = targetPlayer.playerStat.InGameCurrencyGold;
        if (gold < price)
        {
            failMessage = $"골드가 부족합니다! (필요: {price}G, 보유: {gold}G)";
            return false;
        }

        targetPlayer.playerStat.InGameCurrencyGold -= price;
        targetPlayer.deck.AddCard(cardData);
        entry.isSold = true;

        failMessage = string.Empty;
        Debug.Log($"[ShopNPC] {cardData.cardName} 카드를 {price}골드에 구매했습니다.");
        return true;
    }

    public bool TryPurchaseRelic(int slotIndex, out string failMessage)
    {
        if (slotIndex < 0 || slotIndex >= relicEntries.Count || relicEntries[slotIndex] == null || relicEntries[slotIndex].relicData == null)
        {
            failMessage = "유효하지 않은 유물 슬롯입니다.";
            return false;
        }

        ShopRelicEntry entry = relicEntries[slotIndex];
        if (entry.isSold)
        {
            failMessage = "이미 구매한 상품입니다.";
            return false;
        }

        Player targetPlayer = ResolvePlayer();
        if (targetPlayer == null || targetPlayer.playerStat == null || targetPlayer.relicManager == null)
        {
            failMessage = "플레이어 정보를 찾을 수 없습니다.";
            return false;
        }

        RelicData relicData = entry.relicData;
        int price = GetRelicSlotPrice(slotIndex, targetPlayer);
        int gold = targetPlayer.playerStat.InGameCurrencyGold;
        if (gold < price)
        {
            failMessage = $"골드가 부족합니다! (필요: {price}G, 보유: {gold}G)";
            return false;
        }

        targetPlayer.playerStat.InGameCurrencyGold -= price;
        targetPlayer.AddRelic(relicData.relicName);
        entry.isSold = true;

        failMessage = string.Empty;
        Debug.Log($"[ShopNPC] {relicData.relicName} 유물을 {price}골드에 구매했습니다.");
        return true;
    }

    public bool TryReroll(out string failMessage)
    {
        Player targetPlayer = ResolvePlayer();
        if (targetPlayer == null || targetPlayer.playerStat == null)
        {
            failMessage = "플레이어 정보를 찾을 수 없습니다.";
            return false;
        }

        int rerollCost = GetCurrentRerollCost(targetPlayer);
        int gold = targetPlayer.playerStat.InGameCurrencyGold;
        if (gold < rerollCost)
        {
            failMessage = $"골드가 부족합니다! (필요: {rerollCost}G, 보유: {gold}G)";
            return false;
        }

        targetPlayer.playerStat.InGameCurrencyGold -= rerollCost;
        currentRerollCount++;
        targetPlayer.playerStat.ShopRerollCount++;

        AddRandomItems();

        if (shopUI != null)
        {
            shopUI.BindShop(this);
            shopUI.CreateStoreItemCollections(cardEntries.Count, relicEntries.Count, 0);
            shopUI.RefreshShopDisplay();
        }

        failMessage = string.Empty;
        Debug.Log($"[ShopNPC] 상점 품목을 {rerollCost}골드에 리롤했습니다. (방문 리롤: {currentRerollCount}, 누적 리롤: {targetPlayer.playerStat.ShopRerollCount})");
        return true;
    }

    public void InitializeShop()
    {
        if (isInitialized) return;

        currentRerollCount = 0;
        AddRandomItems();
        UpdateShopItems();
        isInitialized = true;
    }

    public void AddRandomItems()
    {
        cardEntries.Clear();
        relicEntries.Clear();

        if (GameItemRewardManager.instance == null) return;

        Player targetPlayer = ResolvePlayer();
        int targetCardCount = GetEffectiveCardCount(targetPlayer);
        int targetRelicCount = GetEffectiveRelicCount(targetPlayer);

        // 1. 카드 추첨 (중복 방지) 및 등급별 변동 원가 확정
        List<CardData> pickedCards = GameItemRewardManager.instance.GetUniqueRandomCards("Shop_Card_Table", targetCardCount);
        for (int i = 0; i < pickedCards.Count; i++)
        {
            CardData drawnCard = pickedCards[i];
            if (drawnCard == null) continue;
            int slotPrice = CardData.RollShopBasePrice(drawnCard.rarity, cardPriceVarianceRatio);
            AddCardEntry(drawnCard, slotPrice);
        }

        // 2. 유물 추첨 (중복 및 보유 유물 제외)
        if (targetRelicCount > 0)
        {
            List<RelicData> pickedRelics = GameItemRewardManager.instance.GetUniqueRandomRelics("Shop_Relic_Table", targetRelicCount);
            HashSet<string> addedRelicNames = new HashSet<string>();

            for (int i = 0; i < pickedRelics.Count; i++)
            {
                RelicData candidate = pickedRelics[i];
                if (candidate == null) continue;
                if (targetPlayer != null && targetPlayer.relicManager != null &&
                    targetPlayer.relicManager.GetSpecificRelic(candidate.relicName) != null)
                {
                    continue;
                }

                if (addedRelicNames.Add(candidate.relicName))
                {
                    AddRelicEntry(candidate);
                }
            }

            // _boundPlayer 테스트 환경 등에서 초기 추첨에 보유 유물이 섞여 부족한 경우 보충 추첨
            int extraAttempts = 0;
            int maxExtraAttempts = targetRelicCount * 10;
            while (relicEntries.Count < targetRelicCount && extraAttempts < maxExtraAttempts)
            {
                extraAttempts++;
                RelicData extra = GameItemRewardManager.instance.GetRandomRelicDataByDropTable("Shop_Relic_Table");
                if (extra == null) break;
                if (targetPlayer != null && targetPlayer.relicManager != null &&
                    targetPlayer.relicManager.GetSpecificRelic(extra.relicName) != null)
                {
                    continue;
                }

                if (addedRelicNames.Add(extra.relicName))
                {
                    AddRelicEntry(extra);
                }
            }
        }

        // 3. 랜덤 품목 할인 추첨 및 적용 (카드/유물 공통 엔트리 구조 사용)
        ApplyRandomSalesToEntries(cardEntries, randomCardSaleCount, randomSaleDiscountRate);
        ApplyRandomSalesToEntries(relicEntries, randomRelicSaleCount, randomSaleDiscountRate);
    }

    private static void ApplyRandomSalesToEntries<T>(List<T> entries, int saleCount, float discountRate) where T : ShopItemEntryBase
    {
        if (entries == null || entries.Count == 0) return;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
            {
                entries[i].isOnSale = false;
                entries[i].discountRate = 0f;
            }
        }

        float clampedRate = Mathf.Clamp(discountRate, 0f, 0.9f);
        if (saleCount <= 0 || clampedRate <= 0f) return;

        int targetCount = Mathf.Min(saleCount, entries.Count);
        int[] indices = new int[entries.Count];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        for (int i = 0; i < targetCount; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, indices.Length);
            int pickedIndex = indices[swapIndex];
            indices[swapIndex] = indices[i];
            indices[i] = pickedIndex;

            T targetEntry = entries[pickedIndex];
            if (targetEntry != null)
            {
                targetEntry.isOnSale = true;
                targetEntry.discountRate = clampedRate;
            }
        }
    }

    public void UpdateShopItems() // 상점 UI 생성 후 아이템 진열
    {
        bool wasUnbound = shopUI == null;
        if (shopUI == null)
        {
            if (UIManager.instance == null)
            {
                return;
            }

            GameObject spawned = UIManager.instance.OpenUI(UIConstants.PANEL_SHOP_NPC, UILayerType.Normal, false);
            if (spawned == null)
            {
                Debug.LogError("[ShopNPC] ShopNPCUI 프리팹을 찾을 수 없습니다.");
                return;
            }

            shopUI = spawned.GetComponentInChildren<ShopPanel>(true);
        }

        if (shopUI != null)
        {
            shopUI.BindShop(this);
            shopUI.CreateStoreItemCollections(cardEntries.Count, relicEntries.Count, 0);
            shopUI.RefreshShopDisplay();

            if (!isInitialized && wasUnbound)
            {
                shopUI.gameObject.SetActive(false);
            }
        }
    }

    public ShopPanel GetShopUI()
    {
        return shopUI;
    }

    public void OpenShopDirectly()
    {
        InitializeShop();

        if (shopUI == null && UIManager.instance != null)
        {
            UpdateShopItems();
        }

        if (shopUI != null)
        {
            if (RunManager.instance != null)
            {
                RunManager.instance.SetActiveNodeShop(this);
            }
            shopUI.BindShop(this);
            shopUI.CreateStoreItemCollections(cardEntries.Count, relicEntries.Count, 0);
            shopUI.RefreshShopDisplay();
            shopUI.Open();
        }
        else
        {
            Debug.LogWarning("[ShopNPC] ShopUI가 로드되지 않았습니다.");
        }
    }

    public void CloseShopUI()
    {
        if (shopUI != null)
        {
            shopUI.Close();
        }
    }
}
