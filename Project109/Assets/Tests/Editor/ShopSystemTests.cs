using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameItem.Types;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class ShopSystemTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<Player> _createdPlayers = new List<Player>();

    private Dictionary<string, CardData> _backupCards;
    private Dictionary<string, RelicData> _backupRelics;
    private Dictionary<string, DropTableData> _backupDropTables;

    private GameSceneManager _prevGameSceneManager;
    private GameItemRewardManager _prevRewardManager;
    private AssetCacheManager _prevAssetCacheManager;
    private RunManager _prevRunManager;

    [SetUp]
    public void SetUp()
    {
        _backupCards = new Dictionary<string, CardData>(ModLoader.Instance.CardDatabase);
        _backupRelics = new Dictionary<string, RelicData>(ModLoader.Instance.RelicDatabase);
        _backupDropTables = new Dictionary<string, DropTableData>(ModLoader.Instance.DropTableDatabase);

        _prevGameSceneManager = GameSceneManager.instance;
        _prevRewardManager = GameItemRewardManager.instance;
        _prevAssetCacheManager = AssetCacheManager.instance;
        _prevRunManager = RunManager.instance;

        ModLoader.Instance.CardDatabase.Clear();
        ModLoader.Instance.RelicDatabase.Clear();
        ModLoader.Instance.DropTableDatabase.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _createdPlayers.Count; i++)
        {
            if (_createdPlayers[i] != null)
            {
                _createdPlayers[i].Dispose();
            }
        }
        _createdPlayers.Clear();

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();

        SetStaticBackingField<GameSceneManager>("instance", _prevGameSceneManager);
        SetStaticBackingField<GameItemRewardManager>("instance", _prevRewardManager);
        SetStaticBackingField<AssetCacheManager>("instance", _prevAssetCacheManager);
        SetStaticBackingField<RunManager>("instance", _prevRunManager);

        ModLoader.Instance.CardDatabase.Clear();
        foreach (var kvp in _backupCards)
        {
            ModLoader.Instance.CardDatabase[kvp.Key] = kvp.Value;
        }

        ModLoader.Instance.RelicDatabase.Clear();
        foreach (var kvp in _backupRelics)
        {
            ModLoader.Instance.RelicDatabase[kvp.Key] = kvp.Value;
        }

        ModLoader.Instance.DropTableDatabase.Clear();
        foreach (var kvp in _backupDropTables)
        {
            ModLoader.Instance.DropTableDatabase[kvp.Key] = kvp.Value;
        }
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private static void SetStaticBackingField<TOwner>(string propertyName, object value)
    {
        FieldInfo field = typeof(TOwner).GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"{typeof(TOwner).Name}.{propertyName} 백킹 필드를 찾을 수 없습니다.");
        field.SetValue(null, value);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
        field.SetValue(target, value);
    }

    private Player CreateTestPlayer(int initialGold = 0)
    {
        GameObject charGo = Track(new GameObject("TestShopPlayerCharacter"));
        Character character = charGo.AddComponent<Character>();
        character.faction = CharacterFaction.Player;
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 120f,
            staminaRegenPerSecond = 40f,
            moveRange = 3
        });

        Player player = new Player(character);
        player.playerStat.InGameCurrencyGold = initialGold;
        _createdPlayers.Add(player);
        return player;
    }

    private GameSceneManager CreateTestGameSceneManager(Player player, bool isInDungeon = true)
    {
        GameObject gsmGo = Track(new GameObject("TestGameSceneManager"));
        GameSceneManager gsm = gsmGo.AddComponent<GameSceneManager>();
        gsm.player = player;
        SetStaticBackingField<GameSceneManager>("instance", gsm);

        PropertyInfo prop = typeof(GameSceneManager).GetProperty("isInDungeon", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(prop);
        prop.SetValue(gsm, isInDungeon);
        return gsm;
    }

    private GameItemRewardManager SeedShopDatabasesAndRewardManager(int cardCount = 14, int relicCount = 8)
    {
        for (int i = 0; i < cardCount; i++)
        {
            CardRarity rarity = (i % 4) switch
            {
                0 => CardRarity.Common,
                1 => CardRarity.Uncommon,
                2 => CardRarity.Rare,
                _ => CardRarity.Unique
            };
            CardData card = new CardData
            {
                cardName = $"ShopTestCard_{i}",
                displayName = $"테스트 카드 {i}",
                rarity = rarity,
                stamina = 30,
                cardType = "Attack",
                obtainSource = ItemObtainSource.Standard,
                classTypes = new List<string> { "Warrior", "All" }
            };
            ModLoader.Instance.CardDatabase[card.cardName] = card;
        }

        for (int i = 0; i < relicCount; i++)
        {
            RelicRarity rarity = (i % 3) switch
            {
                0 => RelicRarity.Common,
                1 => RelicRarity.Rare,
                _ => RelicRarity.Unique
            };
            RelicData relic = new RelicData
            {
                relicName = $"ShopTestRelic_{i}",
                displayName = $"테스트 유물 {i}",
                rarity = rarity,
                obtainSource = ItemObtainSource.Standard,
                description = $"테스트 유물 설명 {i}",
                classTypes = new List<string> { "All" }
            };
            ModLoader.Instance.RelicDatabase[relic.relicName] = relic;
        }

        ModLoader.Instance.DropTableDatabase["Shop_Card_Table"] = new DropTableData
        {
            dropTableID = "Shop_Card_Table",
            commonWeight = 50,
            uncommonWeight = 30,
            rareWeight = 15,
            uniqueWeight = 5,
            allowedClassTypes = new List<string> { "Warrior", "All" }
        };

        ModLoader.Instance.DropTableDatabase["Shop_Relic_Table"] = new DropTableData
        {
            dropTableID = "Shop_Relic_Table",
            commonWeight = 60,
            rareWeight = 30,
            uniqueWeight = 10,
            uncommonWeight = 0
        };

        GameObject rewardGo = Track(new GameObject("TestGameItemRewardManager"));
        GameItemRewardManager rewardManager = rewardGo.AddComponent<GameItemRewardManager>();
        SetStaticBackingField<GameItemRewardManager>("instance", rewardManager);
        rewardManager.UpdateItemList();
        return rewardManager;
    }

    private AssetCacheManager SetupTestAssetCacheWithShopPrefabs()
    {
        GameObject cacheGo = Track(new GameObject("TestAssetCacheManager"));
        AssetCacheManager cache = cacheGo.AddComponent<AssetCacheManager>();
        SetStaticBackingField<AssetCacheManager>("instance", cache);

        GameObject actionCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/ActionCard.prefab");
        GameObject relicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Relic/Relic.prefab");
        GameObject potionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Potion/Potion.prefab");

        Assert.IsNotNull(actionCardPrefab, "ActionCard.prefab을 찾을 수 없습니다.");
        Assert.IsNotNull(relicPrefab, "Relic.prefab을 찾을 수 없습니다.");
        Assert.IsNotNull(potionPrefab, "Potion.prefab을 찾을 수 없습니다.");

        var uiDict = new Dictionary<string, GameObject>
        {
            { "ActionCard", actionCardPrefab },
            { "Relic", relicPrefab },
            { "Potion", potionPrefab }
        };
        SetPrivateField(cache, "uiDict", uiDict);
        return cache;
    }

    private ShopPanel InstantiateShopPanelFromPrefab()
    {
        GameObject shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/NPC/ShopUI/ShopNPCUI.prefab");
        Assert.IsNotNull(shopPrefab, "ShopNPCUI.prefab을 찾을 수 없습니다.");

        GameObject panelGo = Track(UnityEngine.Object.Instantiate(shopPrefab));
        ShopPanel panel = panelGo.GetComponent<ShopPanel>();
        Assert.IsNotNull(panel);
        return panel;
    }

    private static TextMeshProUGUI FindPriceTextOnSlot(Component slot)
    {
        if (slot == null) return null;
        TextMeshProUGUI[] tmps = slot.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            if (tmps[i] != null && tmps[i].gameObject.name == "PriceText")
            {
                return tmps[i];
            }
        }
        return null;
    }

    [Test]
    public void ShopNPC_DefaultAndBonusCounts_And_ShopPanel_DynamicSlotReuse()
    {
        Player player = CreateTestPlayer(500);
        CreateTestGameSceneManager(player);
        SeedShopDatabasesAndRewardManager(cardCount: 14, relicCount: 8);
        SetupTestAssetCacheWithShopPrefabs();

        GameObject npcGo = Track(new GameObject("TestShopNPC"));
        ShopNPC shopNpc = npcGo.AddComponent<ShopNPC>();

        // 1. 기본 진열 수(카드 8장, 유물 3개) 확인
        Assert.AreEqual(8, shopNpc.cardCount);
        Assert.AreEqual(3, shopNpc.relicCount);
        Assert.AreEqual(8, shopNpc.GetEffectiveCardCount(player));
        Assert.AreEqual(3, shopNpc.GetEffectiveRelicCount(player));

        shopNpc.AddRandomItems();
        Assert.AreEqual(8, shopNpc.actionCards.Count);
        Assert.AreEqual(3, shopNpc.relics.Count);

        // 2. BonusShopCardCount / BonusShopRelicCount 보정 반영 확인
        player.playerStat.BonusShopCardCount = 2;
        player.playerStat.BonusShopRelicCount = 1;
        Assert.AreEqual(10, shopNpc.GetEffectiveCardCount(player));
        Assert.AreEqual(4, shopNpc.GetEffectiveRelicCount(player));

        shopNpc.AddRandomItems();
        Assert.AreEqual(10, shopNpc.actionCards.Count);
        Assert.AreEqual(4, shopNpc.relics.Count);

        // 음수 보정 시 하한 클램프 (카드 최소 1, 유물 최소 0)
        player.playerStat.BonusShopCardCount = -20;
        player.playerStat.BonusShopRelicCount = -20;
        Assert.AreEqual(1, shopNpc.GetEffectiveCardCount(player));
        Assert.AreEqual(0, shopNpc.GetEffectiveRelicCount(player));

        // 3. ShopPanel.CreateStoreItemCollections 8장 이상 동적 생성 및 슬롯 재사용 검증
        ShopPanel panel = InstantiateShopPanelFromPrefab();
        panel.CreateStoreItemCollections(8, 3, 0);
        Assert.AreEqual(8, panel.cardList.Count);
        Assert.AreEqual(3, panel.relicList.Count);
        for (int i = 0; i < 8; i++)
        {
            Assert.IsTrue(panel.cardList[i].gameObject.activeSelf);
        }

        CardUI firstCardSlotRef = panel.cardList[0];
        RelicUI firstRelicSlotRef = panel.relicList[0];

        // 동일 수량 재호출 시 무한 증식 방지 (기존 슬롯 재사용)
        panel.CreateStoreItemCollections(8, 3, 0);
        Assert.AreEqual(8, panel.cardList.Count);
        Assert.AreEqual(3, panel.relicList.Count);
        Assert.AreSame(firstCardSlotRef, panel.cardList[0]);
        Assert.AreSame(firstRelicSlotRef, panel.relicList[0]);

        // 10장, 4개로 확장 시 부족한 만큼만 추가 생성
        panel.CreateStoreItemCollections(10, 4, 0);
        Assert.AreEqual(10, panel.cardList.Count);
        Assert.AreEqual(4, panel.relicList.Count);
        Assert.AreSame(firstCardSlotRef, panel.cardList[0]);
        Assert.AreSame(firstRelicSlotRef, panel.relicList[0]);
        for (int i = 0; i < 10; i++)
        {
            Assert.IsTrue(panel.cardList[i].gameObject.activeSelf);
        }

        // 축소 호출 시 남는 슬롯 SetActive(false)
        panel.CreateStoreItemCollections(5, 2, 0);
        Assert.AreEqual(10, panel.cardList.Count);
        Assert.AreEqual(4, panel.relicList.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.AreEqual(i < 5, panel.cardList[i].gameObject.activeSelf);
        }
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(i < 2, panel.relicList[i].gameObject.activeSelf);
        }
    }

    [Test]
    public void ShopNPC_PricingAndDiscountRelic_CalculatesExpectedPrices()
    {
        Player player = CreateTestPlayer(500);

        RelicData discountRelicData = new RelicData
        {
            relicName = "할인 카드",
            displayName = "할인 카드",
            rarity = RelicRarity.Common,
            obtainSource = ItemObtainSource.Standard,
            description = "상인의 물건 가격이 30% 감소합니다.",
            classTypes = new List<string> { "All" }
        };
        ModLoader.Instance.RelicDatabase["할인 카드"] = discountRelicData;

        GameObject npcGo = Track(new GameObject("TestShopNPC"));
        ShopNPC shopNpc = npcGo.AddComponent<ShopNPC>();
        shopNpc.BindPlayer(player);

        CardData commonCard = new CardData { cardName = "c_common", rarity = CardRarity.Common };
        CardData uncommonCard = new CardData { cardName = "c_uncommon", rarity = CardRarity.Uncommon };
        CardData rareCard = new CardData { cardName = "c_rare", rarity = CardRarity.Rare };
        CardData uniqueCard = new CardData { cardName = "c_unique", rarity = CardRarity.Unique };

        RelicData commonRelic = new RelicData { relicName = "r_common", rarity = RelicRarity.Common };
        RelicData rareRelic = new RelicData { relicName = "r_rare", rarity = RelicRarity.Rare };
        RelicData uniqueRelic = new RelicData { relicName = "r_unique", rarity = RelicRarity.Unique };
        RelicData bossRelic = new RelicData { relicName = "r_boss", rarity = RelicRarity.Boss };

        // 할인 미보유 기본가 검증 (ShopNPC 및 ShopPanel 위임 일치 확인)
        Assert.AreEqual(50, shopNpc.GetCardPrice(commonCard, player));
        Assert.AreEqual(75, shopNpc.GetCardPrice(uncommonCard, player));
        Assert.AreEqual(120, shopNpc.GetCardPrice(rareCard, player));
        Assert.AreEqual(150, shopNpc.GetCardPrice(uniqueCard, player));
        Assert.AreEqual(50, ShopPanel.GetCardPrice(commonCard));
        Assert.AreEqual(75, ShopPanel.GetCardPrice(uncommonCard));
        Assert.AreEqual(120, ShopPanel.GetCardPrice(rareCard));
        Assert.AreEqual(150, ShopPanel.GetCardPrice(uniqueCard));

        Assert.AreEqual(150, shopNpc.GetRelicPrice(commonRelic, player));
        Assert.AreEqual(250, shopNpc.GetRelicPrice(rareRelic, player));
        Assert.AreEqual(300, shopNpc.GetRelicPrice(uniqueRelic, player));
        Assert.AreEqual(400, shopNpc.GetRelicPrice(bossRelic, player));
        Assert.AreEqual(150, ShopPanel.GetRelicPrice(commonRelic));
        Assert.AreEqual(250, ShopPanel.GetRelicPrice(rareRelic));
        Assert.AreEqual(300, ShopPanel.GetRelicPrice(uniqueRelic));
        Assert.AreEqual(400, ShopPanel.GetRelicPrice(bossRelic));
        Assert.AreEqual(50, shopNpc.GetCurrentRerollCost(player));

        // "할인 카드" 유물 보유 시 30% 할인 (Mathf.RoundToInt(base * 0.7f)) 검증
        player.AddRelic("할인 카드");
        Assert.AreEqual(Mathf.RoundToInt(50 * 0.7f), shopNpc.GetCardPrice(commonCard, player));
        Assert.AreEqual(Mathf.RoundToInt(75 * 0.7f), shopNpc.GetCardPrice(uncommonCard, player));
        Assert.AreEqual(Mathf.RoundToInt(120 * 0.7f), shopNpc.GetCardPrice(rareCard, player));
        Assert.AreEqual(Mathf.RoundToInt(150 * 0.7f), shopNpc.GetCardPrice(uniqueCard, player));
        Assert.AreEqual(Mathf.RoundToInt(50 * 0.7f), ShopPanel.GetCardPrice(commonCard, player));

        Assert.AreEqual(Mathf.RoundToInt(150 * 0.7f), shopNpc.GetRelicPrice(commonRelic, player));
        Assert.AreEqual(Mathf.RoundToInt(250 * 0.7f), shopNpc.GetRelicPrice(rareRelic, player));
        Assert.AreEqual(Mathf.RoundToInt(300 * 0.7f), shopNpc.GetRelicPrice(uniqueRelic, player));
        Assert.AreEqual(Mathf.RoundToInt(400 * 0.7f), shopNpc.GetRelicPrice(bossRelic, player));
        Assert.AreEqual(Mathf.RoundToInt(150 * 0.7f), ShopPanel.GetRelicPrice(commonRelic, player));
        Assert.AreEqual(Mathf.RoundToInt(50 * 0.7f), shopNpc.GetCurrentRerollCost(player));
    }

    [Test]
    public void ShopNPC_TryPurchaseCardAndRelic_DeductsGoldAndRejectsInsufficientGold()
    {
        Player player = CreateTestPlayer(10);

        CardData cardCommon = new CardData { cardName = "ShopBuyCard", rarity = CardRarity.Common, obtainSource = ItemObtainSource.Standard };
        RelicData relicCommon = new RelicData { relicName = "ShopBuyRelic", rarity = RelicRarity.Common, obtainSource = ItemObtainSource.Standard };
        RelicData discountRelic = new RelicData { relicName = "할인 카드", rarity = RelicRarity.Common, obtainSource = ItemObtainSource.Standard };

        ModLoader.Instance.CardDatabase[cardCommon.cardName] = cardCommon;
        ModLoader.Instance.RelicDatabase[relicCommon.relicName] = relicCommon;
        ModLoader.Instance.RelicDatabase[discountRelic.relicName] = discountRelic;

        GameObject npcGo = Track(new GameObject("TestShopNPC"));
        ShopNPC shopNpc = npcGo.AddComponent<ShopNPC>();
        shopNpc.BindPlayer(player);
        shopNpc.actionCards.Add(cardCommon);
        shopNpc.relics.Add(relicCommon);
        shopNpc.relics.Add(discountRelic);

        // 1. 골드 부족 시 카드 구매 실패
        bool cardBuyFail = shopNpc.TryPurchaseCard(0, out string failMessage);
        Assert.IsFalse(cardBuyFail);
        Assert.AreEqual("골드가 부족합니다! (필요: 50G, 보유: 10G)", failMessage);
        Assert.AreEqual(10, player.playerStat.InGameCurrencyGold);
        Assert.AreEqual(0, player.deck.CardCount);
        Assert.IsFalse(shopNpc.IsCardSlotPurchased(0));

        // 2. 골드 부족 시 유물 구매 실패
        bool relicBuyFail = shopNpc.TryPurchaseRelic(0, out failMessage);
        Assert.IsFalse(relicBuyFail);
        Assert.AreEqual("골드가 부족합니다! (필요: 150G, 보유: 10G)", failMessage);
        Assert.AreEqual(10, player.playerStat.InGameCurrencyGold);
        Assert.IsFalse(shopNpc.IsRelicSlotPurchased(0));

        // 3. UI 연동 상태에서 골드 부족 클릭 시 feedbackText 노출 및 성공 구매 시 가격 갱신 확인
        SetupTestAssetCacheWithShopPrefabs();
        ShopPanel panel = InstantiateShopPanelFromPrefab();
        panel.BindShop(shopNpc);
        panel.CreateStoreItemCollections(shopNpc.actionCards.Count, shopNpc.relics.Count, 0);
        panel.OnOpen();

        TextMeshProUGUI cardPriceBefore = FindPriceTextOnSlot(panel.cardList[0]);
        Assert.IsNotNull(cardPriceBefore);
        Assert.AreEqual("50G", cardPriceBefore.text);

        // 골드 부족 상태에서 UI 카드 클릭
        panel.cardList[0].OnCardClick.Invoke();
        Assert.IsTrue(panel.feedbackText.gameObject.activeSelf);
        StringAssert.Contains("골드가 부족합니다", panel.feedbackText.text);

        // 골드 지급 후 "할인 카드"(유물 슬롯 1, 150G) 구매 -> 잔여 상품 가격 30% 즉시 할인 반영
        player.playerStat.InGameCurrencyGold = 500;
        panel.relicList[1].OnRelicClick.Invoke();
        Assert.IsTrue(shopNpc.IsRelicSlotPurchased(1));
        Assert.IsFalse(panel.relicList[1].gameObject.activeSelf);
        Assert.IsNotNull(player.GetSpecificRelic("할인 카드"));
        Assert.AreEqual(350, player.playerStat.InGameCurrencyGold);
        Assert.IsFalse(panel.feedbackText.gameObject.activeSelf);

        // 할인 카드 구매로 인해 남은 카드 슬롯(0)과 유물 슬롯(0) 및 리롤 버튼이 30% 할인 가격으로 갱신되어야 함
        Assert.AreEqual("35G", FindPriceTextOnSlot(panel.cardList[0]).text);
        Assert.AreEqual("105G", FindPriceTextOnSlot(panel.relicList[0]).text);
        Assert.AreEqual("리롤 (35G)", panel.rerollButtonText.text);

        // 카드 슬롯 0 구매 성공 (할인가 35G 차감)
        Assert.IsTrue(shopNpc.TryPurchaseCard(0, out failMessage));
        Assert.IsTrue(string.IsNullOrEmpty(failMessage));
        Assert.AreEqual(315, player.playerStat.InGameCurrencyGold);
        Assert.AreEqual(1, player.deck.CardCount);
        Assert.IsTrue(shopNpc.IsCardSlotPurchased(0));

        // 이미 구매한 슬롯 재구매 방지
        Assert.IsFalse(shopNpc.TryPurchaseCard(0, out failMessage));
        Assert.AreEqual(315, player.playerStat.InGameCurrencyGold);

        // 유물 슬롯 0 구매 성공 (할인가 105G 차감)
        Assert.IsTrue(shopNpc.TryPurchaseRelic(0, out failMessage));
        Assert.IsTrue(string.IsNullOrEmpty(failMessage));
        Assert.AreEqual(210, player.playerStat.InGameCurrencyGold);
        Assert.IsNotNull(player.GetSpecificRelic(relicCommon.relicName));
        Assert.IsTrue(shopNpc.IsRelicSlotPurchased(0));
    }

    [Test]
    public void ShopNPC_TryReroll_EscalatesCostRedrawsItemsAndPersistsSaveData()
    {
        Player player = CreateTestPlayer(500);
        CreateTestGameSceneManager(player, isInDungeon: true);
        SeedShopDatabasesAndRewardManager(cardCount: 16, relicCount: 8);

        GameObject npcGo = Track(new GameObject("TestShopNPC"));
        ShopNPC shopNpc = npcGo.AddComponent<ShopNPC>();
        shopNpc.BindPlayer(player);
        shopNpc.AddRandomItems();

        Assert.AreEqual(8, shopNpc.actionCards.Count);
        Assert.AreEqual(3, shopNpc.relics.Count);
        Assert.IsFalse(shopNpc.resetRerollCostPerShopVisit, "기본 리롤 정책은 런 누적(방문별 미초기화)이어야 합니다.");
        Assert.AreEqual(50, shopNpc.GetCurrentRerollCost(player));

        // 첫 번째 카드/유물 슬롯 구매 후 리롤 시 구매 상태가 초기화되어야 함
        Assert.IsTrue(shopNpc.TryPurchaseCard(0, out _));
        Assert.IsTrue(shopNpc.IsCardSlotPurchased(0));
        int goldBeforeReroll = player.playerStat.InGameCurrencyGold;

        // 1차 리롤(상점 A): 50G 소모, 다음 비용 75G
        Assert.IsTrue(shopNpc.TryReroll(out string failMessage));
        Assert.IsTrue(string.IsNullOrEmpty(failMessage));
        Assert.AreEqual(goldBeforeReroll - 50, player.playerStat.InGameCurrencyGold);
        Assert.AreEqual(1, shopNpc.CurrentRerollCount);
        Assert.AreEqual(1, player.playerStat.ShopRerollCount);
        Assert.IsFalse(shopNpc.IsCardSlotPurchased(0));
        Assert.AreEqual(8, shopNpc.actionCards.Count);
        Assert.AreEqual(3, shopNpc.relics.Count);
        Assert.AreEqual(75, shopNpc.GetCurrentRerollCost(player));

        // 서로 다른 상점(상점 B) 방문 시에도 기본값(resetRerollCostPerShopVisit = false)으로 누적 리롤 비용 75G 승계 및 2차 리롤 시 100G 상승
        GameObject secondNpcGo = Track(new GameObject("TestSecondShopNPC"));
        ShopNPC secondShop = secondNpcGo.AddComponent<ShopNPC>();
        Assert.IsFalse(secondShop.resetRerollCostPerShopVisit);
        secondShop.BindPlayer(player);
        secondShop.AddRandomItems();
        Assert.AreEqual(75, secondShop.GetCurrentRerollCost(player));

        goldBeforeReroll = player.playerStat.InGameCurrencyGold;
        Assert.IsTrue(secondShop.TryReroll(out failMessage));
        Assert.IsTrue(string.IsNullOrEmpty(failMessage));
        Assert.AreEqual(goldBeforeReroll - 75, player.playerStat.InGameCurrencyGold);
        Assert.AreEqual(1, secondShop.CurrentRerollCount);
        Assert.AreEqual(2, player.playerStat.ShopRerollCount);
        Assert.AreEqual(100, secondShop.GetCurrentRerollCost(player));
        Assert.AreEqual(100, shopNpc.GetCurrentRerollCost(player));

        // 골드 부족 시 리롤 실패 확인
        player.playerStat.InGameCurrencyGold = 40;
        Assert.IsFalse(secondShop.TryReroll(out failMessage));
        Assert.AreEqual("골드가 부족합니다! (필요: 100G, 보유: 40G)", failMessage);
        Assert.AreEqual(40, player.playerStat.InGameCurrencyGold);
        Assert.AreEqual(1, secondShop.CurrentRerollCount);
        Assert.AreEqual(2, player.playerStat.ShopRerollCount);

        // 새 지역 탐험 맵 생성(ExploreMapManager.GenerateMap) 시 ShopRerollCount가 0으로 초기화되는지 검증
        ExploreMapManager exploreMapManager = new ExploreMapManager();
        exploreMapManager.GenerateMap(15, 1, LocationType.Temple);
        Assert.AreEqual(0, player.playerStat.ShopRerollCount);
        Assert.AreEqual(50, secondShop.GetCurrentRerollCost(player));

        // 테스트용 리롤 카운트 재설정 후 SaveSystem 저장 및 복원(JSON 라운드트립 포함) 검증
        player.playerStat.ShopRerollCount = 2;
        const int testSlot = 997;
        GameObject rmGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = rmGo.AddComponent<RunManager>();
        SetStaticBackingField<RunManager>("instance", runManager);
        runManager.currentMapName = "Temple";
        runManager.currentStageLevel = 1;
        runManager.currentExploreMapFloor = 3;

        try
        {
            SaveSystem.DeleteSaveFile(testSlot);
            SaveSystem.SaveGame(runManager, testSlot);

            RunSaveData loaded = SaveSystem.LoadGameData(testSlot, forceReload: true);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.playerShopRerollCount);

            Player restoredPlayer = CreateTestPlayer(0);
            SaveSystem.RestorePlayerData(loaded, restoredPlayer);
            Assert.AreEqual(2, restoredPlayer.playerStat.ShopRerollCount);

            // 런 리셋 시 ShopRerollCount 0 초기화 확인
            runManager.ResetRunProgress();
            Assert.AreEqual(0, player.playerStat.ShopRerollCount);

            // 은신처(!isInDungeon) 상태 저장 시 playerShopRerollCount 0 유지 확인
            player.playerStat.ShopRerollCount = 5;
            PropertyInfo prop = typeof(GameSceneManager).GetProperty("isInDungeon", BindingFlags.Instance | BindingFlags.Public);
            prop.SetValue(GameSceneManager.instance, false);
            SaveSystem.SaveGame(null, testSlot);

            RunSaveData hideoutLoaded = SaveSystem.LoadGameData(testSlot, forceReload: true);
            Assert.IsNotNull(hideoutLoaded);
            Assert.AreEqual(0, hideoutLoaded.playerShopRerollCount);
        }
        finally
        {
            SaveSystem.DeleteSaveFile(testSlot);
        }
    }

    [Test]
    public void ExploreMapManager_GenerateMap15_Floor7IsSingleRestoreNode()
    {
        ExploreMapManager manager = new ExploreMapManager();
        manager.GenerateMap(15, 0, LocationType.Temple);

        Assert.IsNotNull(manager.MapData);
        Assert.AreEqual(15, manager.MapData.floors.Count);

        ExploreFloorData floor7 = manager.MapData.floors[7];
        Assert.IsNotNull(floor7);
        Assert.IsNotNull(floor7.nodes);
        Assert.AreEqual(1, floor7.nodes.Count, "7층 중간 거점은 단일 노드만 생성되어야 합니다.");

        ExploreNode midNode = floor7.nodes[0];
        Assert.IsNotNull(midNode);
        Assert.AreEqual(EncountType.Restore, midNode.encounterData.encountType);
        Assert.AreEqual("Restore_Bonfire_Data", midNode.encounterData.interactableDataId);
    }

    [Test]
    public void ShopPanel_RerollButtonAndNullShopDefense_BehaveSafely()
    {
        Player player = CreateTestPlayer(200);
        CreateTestGameSceneManager(player, isInDungeon: true);
        SeedShopDatabasesAndRewardManager(cardCount: 14, relicCount: 8);
        SetupTestAssetCacheWithShopPrefabs();

        GameObject npcGo = Track(new GameObject("TestShopNPC"));
        ShopNPC shopNpc = npcGo.AddComponent<ShopNPC>();
        shopNpc.AddRandomItems();

        ShopPanel panel = InstantiateShopPanelFromPrefab();
        panel.BindShop(shopNpc);
        panel.CreateStoreItemCollections(shopNpc.actionCards.Count, shopNpc.relics.Count, 0);
        panel.OnOpen();

        Assert.AreEqual("리롤 (50G)", panel.rerollButtonText.text);

        // 리롤 버튼 클릭 성공 시 비용 상승 및 피드백 비표시 확인
        panel.rerollButton.onClick.Invoke();
        Assert.AreEqual(150, player.playerStat.InGameCurrencyGold);
        Assert.AreEqual("리롤 (75G)", panel.rerollButtonText.text);
        Assert.IsFalse(panel.feedbackText.gameObject.activeSelf);

        // 골드 부족 상태에서 리롤 버튼 클릭 시 피드백 표시 확인
        player.playerStat.InGameCurrencyGold = 20;
        panel.rerollButton.onClick.Invoke();
        Assert.IsTrue(panel.feedbackText.gameObject.activeSelf);
        StringAssert.Contains("골드가 부족합니다", panel.feedbackText.text);

        // _boundShop == null 방어 동작 확인
        panel.BindShop(null);
        panel.OnRerollClicked();
        Assert.IsTrue(panel.feedbackText.gameObject.activeSelf);
    }

    [Test]
    public void ShopNPCUIPrefab_RebuiltLayoutAndBindings_MeetSpecAndUseKoreanFont()
    {
        const string prefabPath = "Assets/Prefab/UI/NPC/ShopUI/ShopNPCUI.prefab";
        Assert.AreEqual("39a18a397335fd44da0eea7c1d25f5c0", AssetDatabase.AssetPathToGUID(prefabPath));

        GameObject shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(shopPrefab, "ShopNPCUI.prefab을 찾을 수 없습니다.");

        Player player = CreateTestPlayer(500);
        CreateTestGameSceneManager(player, isInDungeon: true);
        SeedShopDatabasesAndRewardManager(cardCount: 14, relicCount: 8);
        SetupTestAssetCacheWithShopPrefabs();

        // 1920x1080 기준 가상 캔버스 하위에 인스턴스화해 레이아웃 및 4x2 배치 검증
        GameObject canvasGo = Track(new GameObject("TestShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)));
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject instanceGo = Track(UnityEngine.Object.Instantiate(shopPrefab, canvasGo.transform, false));
        ShopPanel panel = instanceGo.GetComponent<ShopPanel>();
        Assert.IsNotNull(panel);

        // 1. 중첩 Canvas / CanvasScaler 금지 규칙 검증
        Assert.AreEqual(0, instanceGo.GetComponentsInChildren<Canvas>(true).Length, "ShopNPCUI 프리팹 내부에는 Canvas가 없어야 합니다.");
        Assert.AreEqual(0, instanceGo.GetComponentsInChildren<CanvasScaler>(true).Length, "ShopNPCUI 프리팹 내부에는 CanvasScaler가 없어야 합니다.");

        // 2. 직렬화 필드 전부 비-null 바인딩 및 potionCollection null 확인
        Assert.IsNotNull(panel.cardCollection);
        Assert.IsNotNull(panel.relicCollection);
        Assert.IsNull(panel.potionCollection);
        Assert.IsNotNull(panel.upgradeCardButton);
        Assert.IsNotNull(panel.closeUIButton);
        Assert.IsNotNull(panel.headerCloseButton);
        Assert.IsNotNull(panel.rerollButton);
        Assert.IsNotNull(panel.rerollButtonText);
        Assert.IsNotNull(panel.feedbackText);
        Assert.IsFalse(panel.feedbackText.gameObject.activeSelf, "FeedbackText는 초기 비활성 상태여야 합니다.");

        // 3. 레거시 오브젝트(PotionCollection, CardCollectionTrigger, RelicCollectionTrigger) 제거 검증
        Assert.IsNull(FindTransformByName(instanceGo.transform, "PotionCollection"));
        Assert.IsNull(FindTransformByName(instanceGo.transform, "CardCollectionTrigger"));
        Assert.IsNull(FindTransformByName(instanceGo.transform, "RelicCollectionTrigger"));

        // 4. 프리팹 내 모든 TextMeshProUGUI에 NEXON 한글 폰트 적용 및 LiberationSans 미사용 검증
        TextMeshProUGUI[] allTexts = instanceGo.GetComponentsInChildren<TextMeshProUGUI>(true);
        Assert.GreaterOrEqual(allTexts.Length, 6);
        for (int i = 0; i < allTexts.Length; i++)
        {
            TextMeshProUGUI tmp = allTexts[i];
            Assert.IsNotNull(tmp.font, $"{tmp.name}의 TMP_FontAsset이 null입니다.");
            StringAssert.Contains("NEXON", tmp.font.name, $"{tmp.name}에 NEXON 한글 폰트가 적용되어야 합니다.");
            StringAssert.DoesNotContain("LiberationSans", tmp.font.name, $"{tmp.name}에 LiberationSans가 남아있으면 안 됩니다.");
        }

        // 5. ScrollRect + GridLayoutGroup(4열) + ContentSizeFitter 구조 검증
        ScrollRect scrollRect = instanceGo.GetComponentInChildren<ScrollRect>(true);
        Assert.IsNotNull(scrollRect);
        Assert.IsTrue(scrollRect.vertical);
        Assert.IsFalse(scrollRect.horizontal);
        Assert.IsNotNull(scrollRect.viewport);
        Assert.IsNotNull(scrollRect.viewport.GetComponent<RectMask2D>());
        Assert.AreSame(panel.cardCollection.transform, scrollRect.content);

        GridLayoutGroup cardGrid = panel.cardCollection.GetComponent<GridLayoutGroup>();
        Assert.IsNotNull(cardGrid);
        Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, cardGrid.constraint);
        Assert.AreEqual(4, cardGrid.constraintCount);
        Assert.GreaterOrEqual(cardGrid.spacing.x, 24f);
        Assert.GreaterOrEqual(cardGrid.spacing.y, 42f);

        ContentSizeFitter cardFitter = panel.cardCollection.GetComponent<ContentSizeFitter>();
        Assert.IsNotNull(cardFitter);
        Assert.AreEqual(ContentSizeFitter.FitMode.PreferredSize, cardFitter.verticalFit);

        GridLayoutGroup relicGrid = panel.relicCollection.GetComponent<GridLayoutGroup>();
        Assert.IsNotNull(relicGrid);
        Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, relicGrid.constraint);
        Assert.AreEqual(3, relicGrid.constraintCount);

        // 6. 8장 카드 및 3개 유물 바인딩 후 4x2 그리드 레이아웃 확인
        GameObject npcGo = Track(new GameObject("TestShopNPC"));
        ShopNPC shopNpc = npcGo.AddComponent<ShopNPC>();
        shopNpc.AddRandomItems();

        panel.BindShop(shopNpc);
        panel.CreateStoreItemCollections(shopNpc.actionCards.Count, shopNpc.relics.Count, 0);
        panel.OnOpen();

        Assert.AreEqual(8, panel.cardList.Count);
        Assert.AreEqual(3, panel.relicList.Count);
        Assert.AreEqual("리롤 (50G)", panel.rerollButtonText.text);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel.cardCollection.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel.relicCollection.GetComponent<RectTransform>());

        // 0..3번 카드는 1행(동일 Y), 4..7번 카드는 2행(더 낮은 Y)에 배치되어야 함
        float row0Y = panel.cardList[0].CachedRectTransform.anchoredPosition.y;
        float row1Y = panel.cardList[4].CachedRectTransform.anchoredPosition.y;
        Assert.Greater(row0Y, row1Y + 300f, "4x2 그리드 2행 카드가 1행 아래에 충분한 가격 배지 간격을 두고 배치되어야 합니다.");

        for (int col = 0; col < 4; col++)
        {
            Assert.AreEqual(row0Y, panel.cardList[col].CachedRectTransform.anchoredPosition.y, 0.5f);
            Assert.AreEqual(row1Y, panel.cardList[col + 4].CachedRectTransform.anchoredPosition.y, 0.5f);
            Assert.AreEqual(
                panel.cardList[col].CachedRectTransform.anchoredPosition.x,
                panel.cardList[col + 4].CachedRectTransform.anchoredPosition.x,
                0.5f);
        }

        // 헤더 닫기 버튼 클릭 시 패널 닫힘 확인
        panel.headerCloseButton.onClick.Invoke();
        Assert.IsFalse(panel.gameObject.activeSelf);
    }

    private static Transform FindTransformByName(Transform root, string targetName)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;
            if (child.name == targetName) return child;
            Transform nested = FindTransformByName(child, targetName);
            if (nested != null) return nested;
        }
        return null;
    }
}
