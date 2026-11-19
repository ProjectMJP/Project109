using GameItem.Types;
using System.Collections.Generic;
using UnityEngine;

public class ShopNPC : InteractableObject
{
    public const string DISCOUNT_RELIC_ID = "할인 카드";

    public GameObject shopUICanvasPrefab;
    private ShopPanel shopUI;

    [Header("Shop Display Settings")]
    public int cardCount = 8;
    public int relicCount = 3;

    [Header("Reroll Settings")]
    public int baseRerollCost = 50;
    public int rerollCostIncrement = 25;
    public bool resetRerollCostPerShopVisit = false;

    public List<CardData> actionCards = new List<CardData>();
    public List<RelicData> relics = new List<RelicData>();

    private bool isInitialized = false;
    private int currentRerollCount = 0;
    private readonly HashSet<int> purchasedCardSlots = new HashSet<int>();
    private readonly HashSet<int> purchasedRelicSlots = new HashSet<int>();
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

    public bool IsCardSlotPurchased(int slotIndex)
    {
        return purchasedCardSlots.Contains(slotIndex);
    }

    public bool IsRelicSlotPurchased(int slotIndex)
    {
        return purchasedRelicSlots.Contains(slotIndex);
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
        int basePrice = 50;
        if (card != null)
        {
            basePrice = card.rarity switch
            {
                CardRarity.Common => 50,
                CardRarity.Uncommon => 75,
                CardRarity.Rare => 120,
                CardRarity.Unique => 150,
                _ => 50
            };
        }

        return applyDiscount ? Mathf.RoundToInt(basePrice * 0.7f) : basePrice;
    }

    public static int CalculateRelicPrice(RelicData relic, Player player = null)
    {
        return CalculateRelicPrice(relic, HasDiscountRelic(player));
    }

    public static int CalculateRelicPrice(RelicData relic, bool applyDiscount)
    {
        int basePrice = 150;
        if (relic != null)
        {
            basePrice = relic.rarity switch
            {
                RelicRarity.Common => 150,
                RelicRarity.Rare => 250,
                RelicRarity.Unique => 300,
                RelicRarity.Boss => 400,
                _ => 150
            };
        }

        return applyDiscount ? Mathf.RoundToInt(basePrice * 0.7f) : basePrice;
    }

    public int GetCardPrice(CardData card, Player player = null)
    {
        return CalculateCardPrice(card, ResolvePlayer(player));
    }

    public int GetRelicPrice(RelicData relic, Player player = null)
    {
        return CalculateRelicPrice(relic, ResolvePlayer(player));
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
        if (slotIndex < 0 || slotIndex >= actionCards.Count || actionCards[slotIndex] == null)
        {
            failMessage = "유효하지 않은 카드 슬롯입니다.";
            return false;
        }

        if (purchasedCardSlots.Contains(slotIndex))
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

        CardData cardData = actionCards[slotIndex];
        int price = GetCardPrice(cardData, targetPlayer);
        int gold = targetPlayer.playerStat.InGameCurrencyGold;
        if (gold < price)
        {
            failMessage = $"골드가 부족합니다! (필요: {price}G, 보유: {gold}G)";
            return false;
        }

        targetPlayer.playerStat.InGameCurrencyGold -= price;
        targetPlayer.deck.AddCard(cardData);
        purchasedCardSlots.Add(slotIndex);

        failMessage = string.Empty;
        Debug.Log($"[ShopNPC] {cardData.cardName} 카드를 {price}골드에 구매했습니다.");
        return true;
    }

    public bool TryPurchaseRelic(int slotIndex, out string failMessage)
    {
        if (slotIndex < 0 || slotIndex >= relics.Count || relics[slotIndex] == null)
        {
            failMessage = "유효하지 않은 유물 슬롯입니다.";
            return false;
        }

        if (purchasedRelicSlots.Contains(slotIndex))
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

        RelicData relicData = relics[slotIndex];
        int price = GetRelicPrice(relicData, targetPlayer);
        int gold = targetPlayer.playerStat.InGameCurrencyGold;
        if (gold < price)
        {
            failMessage = $"골드가 부족합니다! (필요: {price}G, 보유: {gold}G)";
            return false;
        }

        targetPlayer.playerStat.InGameCurrencyGold -= price;
        targetPlayer.AddRelic(relicData.relicName);
        purchasedRelicSlots.Add(slotIndex);

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
            shopUI.CreateStoreItemCollections(actionCards.Count, relics.Count, 0);
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
        actionCards.Clear();
        relics.Clear();
        purchasedCardSlots.Clear();
        purchasedRelicSlots.Clear();

        if (GameItemRewardManager.instance == null) return;

        Player targetPlayer = ResolvePlayer();
        int targetCardCount = GetEffectiveCardCount(targetPlayer);
        int targetRelicCount = GetEffectiveRelicCount(targetPlayer);

        // 1. 카드 추첨 (중복 방지)
        actionCards.AddRange(GameItemRewardManager.instance.GetUniqueRandomCards("Shop_Card_Table", targetCardCount));

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
                    relics.Add(candidate);
                }
            }

            // _boundPlayer 테스트 환경 등에서 초기 추첨에 보유 유물이 섞여 부족한 경우 보충 추첨
            int extraAttempts = 0;
            int maxExtraAttempts = targetRelicCount * 10;
            while (relics.Count < targetRelicCount && extraAttempts < maxExtraAttempts)
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
                    relics.Add(extra);
                }
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
            shopUI.CreateStoreItemCollections(actionCards.Count, relics.Count, 0);
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
            shopUI.BindShop(this);
            shopUI.CreateStoreItemCollections(actionCards.Count, relics.Count, 0);
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
