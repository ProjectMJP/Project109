using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameItem.Types;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

[TestFixture]
public class CompendiumAndObtainSourceTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<Card> _createdCards = new List<Card>();

    private Dictionary<string, CardData> _backupCards;
    private Dictionary<string, RelicData> _backupRelics;
    private Dictionary<string, DropTableData> _backupDropTables;
    private float _backupTimeScale;

    [SetUp]
    public void SetUp()
    {
        _backupTimeScale = Time.timeScale;
        _backupCards = new Dictionary<string, CardData>(ModLoader.Instance.CardDatabase);
        _backupRelics = new Dictionary<string, RelicData>(ModLoader.Instance.RelicDatabase);
        _backupDropTables = new Dictionary<string, DropTableData>(ModLoader.Instance.DropTableDatabase);

        ModLoader.Instance.CardDatabase.Clear();
        ModLoader.Instance.RelicDatabase.Clear();
        ModLoader.Instance.DropTableDatabase.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = _backupTimeScale > 0f ? _backupTimeScale : 1f;

        for (int i = 0; i < _createdCards.Count; i++)
        {
            if (_createdCards[i] != null && !_createdCards[i].IsDisposed)
            {
                _createdCards[i].Dispose();
            }
        }
        _createdCards.Clear();

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();

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

    private Card CreateTestCard(
        string id,
        string displayName,
        CardRarity rarity,
        int stamina,
        string cardType,
        ItemObtainSource obtainSource,
        params string[] classTypes)
    {
        CardData data = new CardData
        {
            cardName = id,
            displayName = displayName,
            rarity = rarity,
            stamina = stamina,
            cardType = cardType,
            obtainSource = obtainSource,
            classTypes = classTypes != null ? new List<string>(classTypes) : new List<string>()
        };

        Card card = new Card(data, null, null, -(_createdCards.Count + 1));
        _createdCards.Add(card);
        return card;
    }

    private static RelicData CreateTestRelic(
        string id,
        string displayName,
        RelicRarity rarity,
        ItemObtainSource obtainSource)
    {
        return new RelicData
        {
            relicName = id,
            displayName = displayName,
            rarity = rarity,
            obtainSource = obtainSource,
            description = $"{displayName} 설명",
            classTypes = new List<string> { "All" }
        };
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"필드 '{fieldName}'을(를) 찾을 수 없습니다.");
        return (T)field.GetValue(target);
    }

    [Test]
    public void CardAndRelicYaml_ObtainSource_DeserializesExpectedDefaultsAndTags()
    {
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        string cardsDir = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "YAML", "Cards");
        string relicsDir = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "YAML", "Relics");

        string[] starterCardFiles = { "검격.yaml", "방어.yaml", "슬래시.yaml", "슬래시+.yaml" };
        for (int i = 0; i < starterCardFiles.Length; i++)
        {
            string yaml = File.ReadAllText(Path.Combine(cardsDir, starterCardFiles[i]));
            CardData card = deserializer.Deserialize<CardData>(yaml);
            Assert.AreEqual(ItemObtainSource.Starter, card.obtainSource, $"{starterCardFiles[i]}는 Starter여야 합니다.");
        }

        string[] testOnlyCardFiles =
        {
            "몬스터 강타.yaml",
            "테스트 곡사 포격.yaml",
            "테스트 광폭 관통.yaml",
            "테스트 다중 지정.yaml",
            "테스트 무작위 사격.yaml",
            "테스트 전방 부채꼴.yaml",
            "테스트 전장 전체.yaml",
            "테스트 주변 원형.yaml",
            "테스트 지정 저격.yaml",
            "테스트 직사 비관통.yaml"
        };
        for (int i = 0; i < testOnlyCardFiles.Length; i++)
        {
            string yaml = File.ReadAllText(Path.Combine(cardsDir, testOnlyCardFiles[i]));
            CardData card = deserializer.Deserialize<CardData>(yaml);
            Assert.AreEqual(ItemObtainSource.TestOnly, card.obtainSource, $"{testOnlyCardFiles[i]}는 TestOnly여야 합니다.");
        }

        // obtainSource 미기재 일반 카드/유물은 기본값 Standard로 역직렬화되어야 합니다.
        CardData standardCard = deserializer.Deserialize<CardData>(File.ReadAllText(Path.Combine(cardsDir, "땅울림.yaml")));
        Assert.AreEqual(ItemObtainSource.Standard, standardCard.obtainSource);

        RelicData standardRelic = deserializer.Deserialize<RelicData>(File.ReadAllText(Path.Combine(relicsDir, "가죽 장갑.yaml")));
        Assert.AreEqual(ItemObtainSource.Standard, standardRelic.obtainSource);
    }

    [Test]
    public void GameItemRewardManager_RandomPoolPredicates_FilterByObtainSourceAndUpgradedAndRarity()
    {
        Assert.IsFalse(GameItemRewardManager.IsCardObtainableInRandomPool(null));
        Assert.IsTrue(GameItemRewardManager.IsCardObtainableInRandomPool(new CardData { cardName = "c_std", isUpgraded = false, obtainSource = ItemObtainSource.Standard }));
        Assert.IsFalse(GameItemRewardManager.IsCardObtainableInRandomPool(new CardData { cardName = "c_upg", isUpgraded = true, obtainSource = ItemObtainSource.Standard }));
        Assert.IsFalse(GameItemRewardManager.IsCardObtainableInRandomPool(new CardData { cardName = "c_str", isUpgraded = false, obtainSource = ItemObtainSource.Starter }));
        Assert.IsFalse(GameItemRewardManager.IsCardObtainableInRandomPool(new CardData { cardName = "c_evt", isUpgraded = false, obtainSource = ItemObtainSource.EventOnly }));
        Assert.IsFalse(GameItemRewardManager.IsCardObtainableInRandomPool(new CardData { cardName = "c_bos", isUpgraded = false, obtainSource = ItemObtainSource.BossOnly }));
        Assert.IsFalse(GameItemRewardManager.IsCardObtainableInRandomPool(new CardData { cardName = "c_tst", isUpgraded = false, obtainSource = ItemObtainSource.TestOnly }));

        Assert.IsFalse(GameItemRewardManager.IsRelicObtainableInRandomPool(null));
        Assert.IsTrue(GameItemRewardManager.IsRelicObtainableInRandomPool(new RelicData { relicName = "r_std", rarity = RelicRarity.Common, obtainSource = ItemObtainSource.Standard }));
        Assert.IsTrue(GameItemRewardManager.IsRelicObtainableInRandomPool(new RelicData { relicName = "r_bos_ok", rarity = RelicRarity.Boss, obtainSource = ItemObtainSource.BossOnly }));
        Assert.IsFalse(GameItemRewardManager.IsRelicObtainableInRandomPool(new RelicData { relicName = "r_bos_bad", rarity = RelicRarity.Common, obtainSource = ItemObtainSource.BossOnly }));
        Assert.IsFalse(GameItemRewardManager.IsRelicObtainableInRandomPool(new RelicData { relicName = "r_str", rarity = RelicRarity.Common, obtainSource = ItemObtainSource.Starter }));
        Assert.IsFalse(GameItemRewardManager.IsRelicObtainableInRandomPool(new RelicData { relicName = "r_evt", rarity = RelicRarity.Rare, obtainSource = ItemObtainSource.EventOnly }));
        Assert.IsFalse(GameItemRewardManager.IsRelicObtainableInRandomPool(new RelicData { relicName = "r_tst", rarity = RelicRarity.Unique, obtainSource = ItemObtainSource.TestOnly }));
    }

    [Test]
    public void GameItemRewardManager_UpdateItemListAndDropTableQueries_ExcludeRestrictedObtainSourcesUnlessSpecific()
    {
        var rewardGo = Track(new GameObject("TestRewardManager"));
        var rewardManager = rewardGo.AddComponent<GameItemRewardManager>();

        CardData stdCard = new CardData { cardName = "일반카드", rarity = CardRarity.Common, obtainSource = ItemObtainSource.Standard, classTypes = new List<string> { "Warrior" } };
        CardData upgradedCard = new CardData { cardName = "일반카드+", rarity = CardRarity.Common, isUpgraded = true, obtainSource = ItemObtainSource.Standard, classTypes = new List<string> { "Warrior" } };
        CardData starterCard = new CardData { cardName = "기본카드", rarity = CardRarity.Common, obtainSource = ItemObtainSource.Starter, classTypes = new List<string> { "Warrior" } };
        CardData eventCard = new CardData { cardName = "이벤트카드", rarity = CardRarity.Common, obtainSource = ItemObtainSource.EventOnly, classTypes = new List<string> { "Warrior" } };
        CardData bossCard = new CardData { cardName = "보스카드", rarity = CardRarity.Common, obtainSource = ItemObtainSource.BossOnly, classTypes = new List<string> { "Warrior" } };
        CardData testCard = new CardData { cardName = "테스트카드", rarity = CardRarity.Common, obtainSource = ItemObtainSource.TestOnly, classTypes = new List<string> { "Warrior" } };

        ModLoader.Instance.CardDatabase[stdCard.cardName] = stdCard;
        ModLoader.Instance.CardDatabase[upgradedCard.cardName] = upgradedCard;
        ModLoader.Instance.CardDatabase[starterCard.cardName] = starterCard;
        ModLoader.Instance.CardDatabase[eventCard.cardName] = eventCard;
        ModLoader.Instance.CardDatabase[bossCard.cardName] = bossCard;
        ModLoader.Instance.CardDatabase[testCard.cardName] = testCard;

        RelicData stdRelic = CreateTestRelic("일반유물", "일반 유물", RelicRarity.Common, ItemObtainSource.Standard);
        RelicData starterRelic = CreateTestRelic("시작유물", "시작 유물", RelicRarity.Common, ItemObtainSource.Starter);
        RelicData eventRelic = CreateTestRelic("이벤트유물", "이벤트 유물", RelicRarity.Rare, ItemObtainSource.EventOnly);
        RelicData testRelic = CreateTestRelic("테스트유물", "테스트 유물", RelicRarity.Common, ItemObtainSource.TestOnly);
        RelicData invalidBossRelic = CreateTestRelic("비보스등급보스유물", "잘못된 보스 유물", RelicRarity.Common, ItemObtainSource.BossOnly);
        RelicData validBossRelic = CreateTestRelic("보스유물", "보스 유물", RelicRarity.Boss, ItemObtainSource.BossOnly);

        ModLoader.Instance.RelicDatabase[stdRelic.relicName] = stdRelic;
        ModLoader.Instance.RelicDatabase[starterRelic.relicName] = starterRelic;
        ModLoader.Instance.RelicDatabase[eventRelic.relicName] = eventRelic;
        ModLoader.Instance.RelicDatabase[testRelic.relicName] = testRelic;
        ModLoader.Instance.RelicDatabase[invalidBossRelic.relicName] = invalidBossRelic;
        ModLoader.Instance.RelicDatabase[validBossRelic.relicName] = validBossRelic;

        ModLoader.Instance.DropTableDatabase["Test_Random_Cards"] = new DropTableData
        {
            dropTableID = "Test_Random_Cards",
            commonWeight = 100
        };
        ModLoader.Instance.DropTableDatabase["Test_Specific_EventCard"] = new DropTableData
        {
            dropTableID = "Test_Specific_EventCard",
            specificItemIDs = new List<string> { eventCard.cardName }
        };
        ModLoader.Instance.DropTableDatabase["Test_Random_Relics"] = new DropTableData
        {
            dropTableID = "Test_Random_Relics",
            commonWeight = 70,
            rareWeight = 30,
            uncommonWeight = 0
        };
        ModLoader.Instance.DropTableDatabase["Test_Boss_Relics"] = new DropTableData
        {
            dropTableID = "Test_Boss_Relics",
            commonWeight = 0,
            rareWeight = 0,
            uniqueWeight = 0,
            uncommonWeight = 100
        };
        ModLoader.Instance.DropTableDatabase["Test_Specific_EventRelic"] = new DropTableData
        {
            dropTableID = "Test_Specific_EventRelic",
            specificItemIDs = new List<string> { eventRelic.relicName }
        };

        rewardManager.InitializeDatabases(
            ModLoader.Instance.CardDatabase,
            ModLoader.Instance.RelicDatabase,
            ModLoader.Instance.DropTableDatabase);

        var commonCardList = GetPrivateField<List<CardData>>(rewardManager, "commonCardList");
        Assert.AreEqual(1, commonCardList.Count);
        Assert.AreSame(stdCard, commonCardList[0]);

        var commonRelicList = GetPrivateField<List<RelicData>>(rewardManager, "commonRelicList");
        var rareRelicList = GetPrivateField<List<RelicData>>(rewardManager, "rareRelicList");
        var bossRelicList = GetPrivateField<List<RelicData>>(rewardManager, "bossRelicList");
        Assert.AreEqual(1, commonRelicList.Count);
        Assert.AreSame(stdRelic, commonRelicList[0]);
        Assert.AreEqual(0, rareRelicList.Count);
        Assert.AreEqual(1, bossRelicList.Count);
        Assert.AreSame(validBossRelic, bossRelicList[0]);

        // 일반 가중치 추첨에서는 Standard 외 카드가 절대 등장하지 않아야 합니다.
        for (int i = 0; i < 40; i++)
        {
            CardData pickedCard = rewardManager.GetRandomCardDataByDropTable("Test_Random_Cards");
            Assert.AreSame(stdCard, pickedCard);

            RelicData pickedRelic = rewardManager.GetRandomRelicDataByDropTable("Test_Random_Relics");
            Assert.AreSame(stdRelic, pickedRelic);
        }

        // Boss 가중치 테이블에서는 BossOnly + Boss 등급 유물이 반환되어야 합니다.
        RelicData pickedBossRelic = rewardManager.GetRandomRelicDataByDropTable("Test_Boss_Relics");
        Assert.AreSame(validBossRelic, pickedBossRelic);

        // specificItemIDs 직접 지정 시에는 EventOnly 항목도 반환되어야 합니다.
        Assert.AreSame(eventCard, rewardManager.GetRandomCardDataByDropTable("Test_Specific_EventCard"));
        Assert.AreSame(eventRelic, rewardManager.GetRandomRelicDataByDropTable("Test_Specific_EventRelic"));

        // Standard 카드가 없는 경우 일반 풀 및 폴백 모두 비어 null을 반환해야 합니다.
        ModLoader.Instance.CardDatabase.Remove(stdCard.cardName);
        rewardManager.InitializeDatabases(
            ModLoader.Instance.CardDatabase,
            ModLoader.Instance.RelicDatabase,
            ModLoader.Instance.DropTableDatabase);
        Assert.IsNull(rewardManager.GetRandomCardDataByDropTable("Test_Random_Cards"));
    }

    [Test]
    public void CardDeckViewPanel_FilterAndSortCards_FiltersAndSortsAccurately()
    {
        Card warriorCommonAttack = CreateTestCard("c1", "가나다", CardRarity.Common, 40, "Attack", ItemObtainSource.Standard, "Warrior");
        Card testerRareSkill = CreateTestCard("c2", "다라마", CardRarity.Rare, 20, "Skill", ItemObtainSource.Starter, "TesterWarrior");
        Card commonUncommonAttack = CreateTestCard("c3", "나다가", CardRarity.Uncommon, 60, "Attack", ItemObtainSource.EventOnly, "All");
        Card emptyClassUniqueSkill = CreateTestCard("c4", "라마다", CardRarity.Unique, 10, "Skill", ItemObtainSource.Standard);

        var source = new List<Card> { warriorCommonAttack, testerRareSkill, commonUncommonAttack, emptyClassUniqueSkill };

        // 클래스 필터: 전투광(Warrior/TesterWarrior) vs 공용(All/빈 목록)
        var warriorOnly = CardDeckViewPanel.FilterAndSortCards(
            source, true,
            CardDeckViewPanel.ClassFilter.Warrior,
            CardDeckViewPanel.RarityFilter.All,
            CardDeckViewPanel.TypeFilter.All,
            CardDeckViewPanel.ObtainSourceFilter.All,
            CardDeckViewPanel.SortCriteria.Rarity, true);
        CollectionAssert.AreEqual(new[] { warriorCommonAttack, testerRareSkill }, warriorOnly);

        var commonOnly = CardDeckViewPanel.FilterAndSortCards(
            source, true,
            CardDeckViewPanel.ClassFilter.Common,
            CardDeckViewPanel.RarityFilter.All,
            CardDeckViewPanel.TypeFilter.All,
            CardDeckViewPanel.ObtainSourceFilter.All,
            CardDeckViewPanel.SortCriteria.Rarity, true);
        CollectionAssert.AreEqual(new[] { commonUncommonAttack, emptyClassUniqueSkill }, commonOnly);

        // 타입 필터: Attack vs Skill
        var skillOnly = CardDeckViewPanel.FilterAndSortCards(
            source, true,
            CardDeckViewPanel.ClassFilter.All,
            CardDeckViewPanel.RarityFilter.All,
            CardDeckViewPanel.TypeFilter.Skill,
            CardDeckViewPanel.ObtainSourceFilter.All,
            CardDeckViewPanel.SortCriteria.Cost, true);
        CollectionAssert.AreEqual(new[] { emptyClassUniqueSkill, testerRareSkill }, skillOnly);

        // 등급 & 획득처 복합 필터
        var eventUncommon = CardDeckViewPanel.FilterAndSortCards(
            source, true,
            CardDeckViewPanel.ClassFilter.All,
            CardDeckViewPanel.RarityFilter.Uncommon,
            CardDeckViewPanel.TypeFilter.All,
            CardDeckViewPanel.ObtainSourceFilter.EventOnly,
            CardDeckViewPanel.SortCriteria.Rarity, true);
        CollectionAssert.AreEqual(new[] { commonUncommonAttack }, eventUncommon);

        // 정렬: 코스트 내림차순 & 이름 내림차순
        var byCostDesc = CardDeckViewPanel.FilterAndSortCards(
            source, false,
            CardDeckViewPanel.ClassFilter.Warrior, // applyCompendiumFilters == false 이므로 무시되어야 함
            CardDeckViewPanel.RarityFilter.Rare,
            CardDeckViewPanel.TypeFilter.Skill,
            CardDeckViewPanel.ObtainSourceFilter.Starter,
            CardDeckViewPanel.SortCriteria.Cost, false);
        CollectionAssert.AreEqual(new[] { commonUncommonAttack, warriorCommonAttack, testerRareSkill, emptyClassUniqueSkill }, byCostDesc);

        var byNameAsc = CardDeckViewPanel.FilterAndSortCards(
            source, true,
            CardDeckViewPanel.ClassFilter.All,
            CardDeckViewPanel.RarityFilter.All,
            CardDeckViewPanel.TypeFilter.All,
            CardDeckViewPanel.ObtainSourceFilter.All,
            CardDeckViewPanel.SortCriteria.Name, true);
        CollectionAssert.AreEqual(new[] { warriorCommonAttack, commonUncommonAttack, testerRareSkill, emptyClassUniqueSkill }, byNameAsc);
    }

    [Test]
    public void CardDeckViewPanel_CompendiumVsDeckMode_BranchesControlsAndResetsState()
    {
        GameObject panelGo = Track(new GameObject("CardDeckViewPanelTest", typeof(RectTransform)));
        GameObject contentGo = Track(new GameObject("Content", typeof(RectTransform)));
        contentGo.transform.SetParent(panelGo.transform, false);

        CardDeckViewPanel panel = panelGo.AddComponent<CardDeckViewPanel>();
        panel.contentTransform = contentGo.transform;

        Card c1 = CreateTestCard("c1", "가", CardRarity.Common, 40, "Attack", ItemObtainSource.Starter, "Warrior");
        Card c2 = CreateTestCard("c2", "나", CardRarity.Rare, 20, "Skill", ItemObtainSource.Standard, "All");
        var cards = new List<Card> { c1, c2 };

        // OpenCompendium 시 RefreshAllCardUIs에서 ObjectPoolManager 미초기화 오류가 1회 발생
        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        panel.OpenCompendium("카드 도감", cards);

        Assert.IsTrue(panel.IsCompendiumMode);
        Button classBtn = GetPrivateField<Button>(panel, "classFilterButton");
        Button rarityBtn = GetPrivateField<Button>(panel, "rarityFilterButton");
        Button typeBtn = GetPrivateField<Button>(panel, "typeFilterButton");
        Button obtainBtn = GetPrivateField<Button>(panel, "obtainSourceFilterButton");
        Button sortBtn = GetPrivateField<Button>(panel, "sortCriteriaButton");
        Button dirBtn = GetPrivateField<Button>(panel, "sortDirectionButton");

        Assert.IsTrue(classBtn.gameObject.activeSelf);
        Assert.IsTrue(rarityBtn.gameObject.activeSelf);
        Assert.IsTrue(typeBtn.gameObject.activeSelf);
        Assert.IsTrue(obtainBtn.gameObject.activeSelf);
        Assert.IsTrue(sortBtn.gameObject.activeSelf);
        Assert.IsTrue(dirBtn.gameObject.activeSelf);

        // 클래스 필터 순환: 전체 -> 전투광(c1) -> 공용(c2)
        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        classBtn.onClick.Invoke();
        Assert.AreEqual(CardDeckViewPanel.ClassFilter.Warrior, panel.CurrentClassFilter);
        Assert.AreEqual(1, panel.CurrentDisplayedCards.Count);
        Assert.AreSame(c1, panel.CurrentDisplayedCards[0]);

        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        classBtn.onClick.Invoke();
        Assert.AreEqual(CardDeckViewPanel.ClassFilter.Common, panel.CurrentClassFilter);
        Assert.AreEqual(1, panel.CurrentDisplayedCards.Count);
        Assert.AreSame(c2, panel.CurrentDisplayedCards[0]);

        // 일반 덱 열기로 전환 시 도감 필터 숨김, 공용 정렬 유지 및 상태 초기화 확인
        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        panel.Open("전체 덱", cards, CardViewMode.ViewOnly);

        Assert.IsFalse(panel.IsCompendiumMode);
        Assert.AreEqual(CardDeckViewPanel.ClassFilter.All, panel.CurrentClassFilter);
        Assert.AreEqual(CardDeckViewPanel.SortCriteria.Rarity, panel.CurrentSortCriteria);
        Assert.IsTrue(panel.IsSortAscending);
        Assert.IsFalse(classBtn.gameObject.activeSelf);
        Assert.IsFalse(rarityBtn.gameObject.activeSelf);
        Assert.IsFalse(typeBtn.gameObject.activeSelf);
        Assert.IsFalse(obtainBtn.gameObject.activeSelf);
        Assert.IsTrue(sortBtn.gameObject.activeSelf);
        Assert.IsTrue(dirBtn.gameObject.activeSelf);

        // 일반 덱에서도 정렬 기준 변경이 즉시 동작해야 합니다.
        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        sortBtn.onClick.Invoke();
        Assert.AreEqual(CardDeckViewPanel.SortCriteria.Cost, panel.CurrentSortCriteria);
        CollectionAssert.AreEqual(new[] { c2, c1 }, panel.CurrentDisplayedCards);
    }

    [Test]
    public void PauseMenuPanel_CompendiumExcludesTestOnlyAndFiltersRelics()
    {
        CardData stdCard = new CardData { cardName = "일반카드", displayName = "일반 카드", rarity = CardRarity.Common, obtainSource = ItemObtainSource.Standard };
        CardData starterCard = new CardData { cardName = "기본카드", displayName = "기본 카드", rarity = CardRarity.Common, obtainSource = ItemObtainSource.Starter };
        CardData testCard = new CardData { cardName = "테스트카드", displayName = "테스트 카드", rarity = CardRarity.Common, obtainSource = ItemObtainSource.TestOnly };
        ModLoader.Instance.CardDatabase[stdCard.cardName] = stdCard;
        ModLoader.Instance.CardDatabase[starterCard.cardName] = starterCard;
        ModLoader.Instance.CardDatabase[testCard.cardName] = testCard;

        RelicData rCommonStd = CreateTestRelic("r1", "가죽 장갑", RelicRarity.Common, ItemObtainSource.Standard);
        RelicData rRareEvt = CreateTestRelic("r2", "고대 회중시계", RelicRarity.Rare, ItemObtainSource.EventOnly);
        RelicData rBoss = CreateTestRelic("r3", "군주의 왕관", RelicRarity.Boss, ItemObtainSource.BossOnly);
        RelicData rTestOnly = CreateTestRelic("r4", "테스트 유물", RelicRarity.Unique, ItemObtainSource.TestOnly);

        ModLoader.Instance.RelicDatabase[rCommonStd.relicName] = rCommonStd;
        ModLoader.Instance.RelicDatabase[rRareEvt.relicName] = rRareEvt;
        ModLoader.Instance.RelicDatabase[rBoss.relicName] = rBoss;
        ModLoader.Instance.RelicDatabase[rTestOnly.relicName] = rTestOnly;

        // 순수 유물 필터/정렬 검증
        var filteredBoss = PauseMenuPanel.FilterAndSortRelics(
            ModLoader.Instance.RelicDatabase.Values,
            PauseMenuPanel.RelicRarityFilter.Boss,
            PauseMenuPanel.RelicObtainSourceFilter.BossOnly,
            PauseMenuPanel.RelicSortCriteria.Name,
            true);
        CollectionAssert.AreEqual(new[] { rBoss }, filteredBoss);

        var allWithoutTest = PauseMenuPanel.FilterAndSortRelics(
            ModLoader.Instance.RelicDatabase.Values,
            PauseMenuPanel.RelicRarityFilter.All,
            PauseMenuPanel.RelicObtainSourceFilter.All,
            PauseMenuPanel.RelicSortCriteria.Name,
            false);
        CollectionAssert.AreEqual(new[] { rBoss, rRareEvt, rCommonStd }, allWithoutTest);

        // 실제 PauseMenuPanel 폴백 계층 및 버튼 동작 검증
        GameObject pauseGo = Track(new GameObject("PauseMenuPanelTest"));
        PauseMenuPanel pausePanel = pauseGo.AddComponent<PauseMenuPanel>();
        pausePanel.OnOpen();

        pausePanel.EnsureCompendiumCardsBuilt();
        Assert.AreEqual(2, pausePanel.CachedCompendiumCards.Count);
        for (int i = 0; i < pausePanel.CachedCompendiumCards.Count; i++)
        {
            Assert.AreNotEqual(ItemObtainSource.TestOnly, pausePanel.CachedCompendiumCards[i].cardData.obtainSource);
        }

        Button relicCompendiumBtn = GetPrivateField<Button>(pausePanel, "relicCompendiumButton");
        TextMeshProUGUI relicCountText = GetPrivateField<TextMeshProUGUI>(pausePanel, "relicCountText");
        Button relicRarityBtn = GetPrivateField<Button>(pausePanel, "relicRarityFilterButton");
        Button relicSourceBtn = GetPrivateField<Button>(pausePanel, "relicObtainSourceFilterButton");
        Button relicSortBtn = GetPrivateField<Button>(pausePanel, "relicSortCriteriaButton");
        Button relicDirBtn = GetPrivateField<Button>(pausePanel, "relicSortDirectionButton");

        relicCompendiumBtn.onClick.Invoke();
        Assert.AreEqual(3, pausePanel.TotalCompendiumRelicCount);
        Assert.AreEqual(3, pausePanel.DisplayedRelicCount);
        Assert.AreEqual("표시 중인 유물: 3 / 전체 3종", relicCountText.text);

        // 등급 필터: All -> Common
        relicRarityBtn.onClick.Invoke();
        Assert.AreEqual(PauseMenuPanel.RelicRarityFilter.Common, pausePanel.CurrentRelicRarityFilter);
        Assert.AreEqual(1, pausePanel.DisplayedRelicCount);
        Assert.AreSame(rCommonStd, pausePanel.CurrentDisplayedRelics[0]);
        Assert.AreEqual("표시 중인 유물: 1 / 전체 3종", relicCountText.text);

        // 등급 필터 초기화 (Common -> Rare -> Unique -> Boss -> All)
        relicRarityBtn.onClick.Invoke();
        relicRarityBtn.onClick.Invoke();
        relicRarityBtn.onClick.Invoke();
        relicRarityBtn.onClick.Invoke();
        Assert.AreEqual(PauseMenuPanel.RelicRarityFilter.All, pausePanel.CurrentRelicRarityFilter);
        Assert.AreEqual(3, pausePanel.DisplayedRelicCount);

        // 획득처 필터: All -> Standard -> BossOnly -> EventOnly
        relicSourceBtn.onClick.Invoke();
        Assert.AreEqual(PauseMenuPanel.RelicObtainSourceFilter.Standard, pausePanel.CurrentRelicObtainSourceFilter);
        Assert.AreSame(rCommonStd, pausePanel.CurrentDisplayedRelics[0]);

        relicSourceBtn.onClick.Invoke();
        Assert.AreEqual(PauseMenuPanel.RelicObtainSourceFilter.BossOnly, pausePanel.CurrentRelicObtainSourceFilter);
        Assert.AreSame(rBoss, pausePanel.CurrentDisplayedRelics[0]);

        relicSourceBtn.onClick.Invoke();
        Assert.AreEqual(PauseMenuPanel.RelicObtainSourceFilter.EventOnly, pausePanel.CurrentRelicObtainSourceFilter);
        Assert.AreSame(rRareEvt, pausePanel.CurrentDisplayedRelics[0]);
        Assert.AreEqual("표시 중인 유물: 1 / 전체 3종", relicCountText.text);

        // 전체 복구 후 이름 내림차순 정렬 확인
        relicSourceBtn.onClick.Invoke();
        relicSortBtn.onClick.Invoke();
        relicDirBtn.onClick.Invoke();
        Assert.AreEqual(PauseMenuPanel.RelicSortCriteria.Name, pausePanel.CurrentRelicSortCriteria);
        Assert.IsFalse(pausePanel.IsRelicSortAscending);
        CollectionAssert.AreEqual(new[] { rBoss, rRareEvt, rCommonStd }, pausePanel.CurrentDisplayedRelics);
    }

    [Test]
    public void Prefabs_CardDeckCanvasAndPauseMenuPanel_AttachControlBarsWithoutLayoutOverlap()
    {
        GameObject deckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/CardDeckCanvas.prefab");
        Assert.IsNotNull(deckPrefab, "CardDeckCanvas.prefab을 찾을 수 없습니다.");

        GameObject deckGo = Track(UnityEngine.Object.Instantiate(deckPrefab));
        CardDeckViewPanel deckPanel = deckGo.GetComponent<CardDeckViewPanel>();
        Assert.IsNotNull(deckPanel);

        MethodInfo deckAwakeMethod = typeof(CardDeckViewPanel).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(deckAwakeMethod);
        deckAwakeMethod.Invoke(deckPanel, null);

        Card card = CreateTestCard("c_prefab", "테스트", CardRarity.Common, 30, "Attack", ItemObtainSource.Standard, "Warrior");
        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        deckPanel.OpenCompendium("카드 도감", new List<Card> { card });

        Transform cardBar = deckGo.transform.Find("FilterSortBar");
        Transform cardList = deckGo.transform.Find("CardList");
        Assert.IsNotNull(cardBar);
        Assert.IsNotNull(cardList);
        RectTransform cardBarRect = cardBar.GetComponent<RectTransform>();
        RectTransform cardListRect = cardList.GetComponent<RectTransform>();
        Assert.GreaterOrEqual(cardBarRect.anchorMin.y, cardListRect.anchorMax.y, "CardDeck FilterSortBar가 CardList 영역과 겹치면 안 됩니다.");

        GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/PauseMenuPanel.prefab");
        Assert.IsNotNull(pausePrefab, "PauseMenuPanel.prefab을 찾을 수 없습니다.");

        GameObject pauseGo = Track(UnityEngine.Object.Instantiate(pausePrefab));
        PauseMenuPanel pausePanel = pauseGo.GetComponent<PauseMenuPanel>();
        Assert.IsNotNull(pausePanel);

        RelicData r1 = CreateTestRelic("r_prefab", "프리팹 유물", RelicRarity.Common, ItemObtainSource.Standard);
        ModLoader.Instance.RelicDatabase[r1.relicName] = r1;

        pausePanel.OnOpen();
        Button relicCompendiumBtn = GetPrivateField<Button>(pausePanel, "relicCompendiumButton");
        relicCompendiumBtn.onClick.Invoke();

        Transform relicGroup = pauseGo.transform.Find("WindowFrame/RelicCompendiumGroup");
        Assert.IsNotNull(relicGroup);
        Transform relicBar = relicGroup.Find("RelicFilterSortBar");
        Transform relicCount = relicGroup.Find("RelicCountText");
        Transform relicScroll = relicGroup.Find("RelicScrollView");
        Assert.IsNotNull(relicBar);
        Assert.IsNotNull(relicCount);
        Assert.IsNotNull(relicScroll);

        RectTransform relicBarRect = relicBar.GetComponent<RectTransform>();
        RectTransform relicCountRect = relicCount.GetComponent<RectTransform>();
        RectTransform relicScrollRect = relicScroll.GetComponent<RectTransform>();
        float countBottomY = relicCountRect.anchoredPosition.y - relicCountRect.sizeDelta.y;
        float barTopY = relicBarRect.anchoredPosition.y;
        float barBottomY = barTopY - relicBarRect.sizeDelta.y;
        Assert.LessOrEqual(barTopY, countBottomY, "RelicFilterSortBar가 RelicCountText와 겹치면 안 됩니다.");
        Assert.LessOrEqual(relicScrollRect.offsetMax.y, barBottomY, "RelicScrollView 상단이 RelicFilterSortBar와 겹치면 안 됩니다.");
        Assert.AreEqual(1, pausePanel.DisplayedRelicCount);
    }

    [Test]
    public void PauseMenuPanel_RelicCompendium_ExpandsWindowToThreeColumnCardGrid_WithoutOverlapOn1080p()
    {
        GameObject canvasGo = Track(new GameObject("CompendiumLayoutTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);

        GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/PauseMenuPanel.prefab");
        Assert.IsNotNull(pausePrefab, "PauseMenuPanel.prefab을 찾을 수 없습니다.");

        GameObject pauseGo = Track(UnityEngine.Object.Instantiate(pausePrefab, canvasGo.transform, false));
        PauseMenuPanel pausePanel = pauseGo.GetComponent<PauseMenuPanel>();
        Assert.IsNotNull(pausePanel);

        for (int i = 0; i < 15; i++)
        {
            RelicRarity rarity = (RelicRarity)(i % 4);
            RelicData relic = CreateTestRelic($"grid_relic_{i}", $"테스트 유물 {i}", rarity, ItemObtainSource.Standard);
            ModLoader.Instance.RelicDatabase[relic.relicName] = relic;
        }

        pausePanel.OnOpen();
        RectTransform windowFrame = pauseGo.transform.Find("WindowFrame") as RectTransform;
        Assert.IsNotNull(windowFrame);
        Assert.AreEqual(new Vector2(460f, 520f), windowFrame.sizeDelta, "기본 일시정지 메뉴 크기는 460x520이어야 합니다.");

        Button relicCompendiumBtn = GetPrivateField<Button>(pausePanel, "relicCompendiumButton");
        Button relicBackBtn = GetPrivateField<Button>(pausePanel, "relicBackButton");
        Button settingsBtn = GetPrivateField<Button>(pausePanel, "settingsButton");
        Button settingsBackBtn = GetPrivateField<Button>(pausePanel, "settingsBackButton");

        // 1. 유물 도감 진입 시 대화면 확장 (1420x840) 및 3열 GridLayoutGroup 확인
        relicCompendiumBtn.onClick.Invoke();
        Assert.AreEqual(new Vector2(1420f, 840f), windowFrame.sizeDelta, "유물 도감 진입 시 WindowFrame이 1420x840으로 확장되어야 합니다.");
        Assert.AreEqual(15, pausePanel.DisplayedRelicCount);

        Transform relicGroup = windowFrame.Find("RelicCompendiumGroup");
        Transform contentTr = relicGroup.Find("RelicScrollView/Viewport/Content");
        Assert.IsNotNull(contentTr);
        Assert.IsNull(contentTr.GetComponent<VerticalLayoutGroup>(), "기존 VerticalLayoutGroup은 제거되어야 합니다.");

        GridLayoutGroup grid = contentTr.GetComponent<GridLayoutGroup>();
        Assert.IsNotNull(grid, "Content에 GridLayoutGroup이 부착되어야 합니다.");
        Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, grid.constraint);
        Assert.AreEqual(3, grid.constraintCount);

        // 2. 카드형 항목 구성 요소(Icon, NameText, RarityBadge, DescText) 검증
        Assert.AreEqual(15, contentTr.childCount);
        Transform firstCard = contentTr.GetChild(0);
        Assert.IsNotNull(firstCard.Find("Icon"));
        Assert.IsNotNull(firstCard.Find("NameText"));
        Assert.IsNotNull(firstCard.Find("RarityBadge"));
        Assert.IsNotNull(firstCard.Find("DescText"));

        // 3. 1920x1080 기준 상단 카운트 / 1줄 필터바 / 스크롤 영역 / 하단 뒤로 버튼 비겹침 검증
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(windowFrame);

        RectTransform countRect = relicGroup.Find("RelicCountText") as RectTransform;
        RectTransform barRect = relicGroup.Find("RelicFilterSortBar") as RectTransform;
        RectTransform scrollRect = relicGroup.Find("RelicScrollView") as RectTransform;
        RectTransform backRect = relicGroup.Find("RelicBackButton") as RectTransform;

        Assert.IsNotNull(barRect.GetComponent<HorizontalLayoutGroup>(), "RelicFilterSortBar는 1줄 HorizontalLayoutGroup이어야 합니다.");
        Assert.GreaterOrEqual(GetMinY(countRect), GetMaxY(barRect), "RelicCountText와 RelicFilterSortBar가 겹치면 안 됩니다.");
        Assert.GreaterOrEqual(GetMinY(barRect), GetMaxY(scrollRect), "RelicFilterSortBar와 RelicScrollView가 겹치면 안 됩니다.");
        Assert.GreaterOrEqual(GetMinY(scrollRect), GetMaxY(backRect), "RelicScrollView와 RelicBackButton이 겹치면 안 됩니다.");

        // 4. 뒤로/설정(460x640) 전환 및 닫기 시 기존 460x520 크기 원복 확인
        relicBackBtn.onClick.Invoke();
        Assert.AreEqual(new Vector2(460f, 520f), windowFrame.sizeDelta, "Main 메뉴 복귀 시 WindowFrame이 460x520으로 복원되어야 합니다.");

        settingsBtn.onClick.Invoke();
        Assert.AreEqual(new Vector2(460f, 640f), windowFrame.sizeDelta, "설정(볼륨 슬라이더 포함) 진입 시 WindowFrame이 460x640으로 확장되어야 합니다.");
        settingsBackBtn.onClick.Invoke();
        Assert.AreEqual(new Vector2(460f, 520f), windowFrame.sizeDelta);

        relicCompendiumBtn.onClick.Invoke();
        Assert.AreEqual(new Vector2(1420f, 840f), windowFrame.sizeDelta);
        pausePanel.OnClose();
        Assert.AreEqual(new Vector2(460f, 520f), windowFrame.sizeDelta, "패널 닫기 시 WindowFrame이 460x520으로 복원되어야 합니다.");
    }

    [Test]
    public void CardDeckCanvas_CloseButton_MatchesShopExitButtonPositionWithoutOverlap()
    {
        GameObject deckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/CardDeckCanvas.prefab");
        GameObject shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/NPC/ShopUI/ShopNPCUI.prefab");
        Assert.IsNotNull(deckPrefab, "CardDeckCanvas.prefab을 찾을 수 없습니다.");
        Assert.IsNotNull(shopPrefab, "ShopNPCUI.prefab을 찾을 수 없습니다.");

        Transform deckCloseTr = deckPrefab.transform.Find("CloseUI Button");
        Transform shopCloseTr = shopPrefab.transform.Find("CloseUIButton");
        Assert.IsNotNull(deckCloseTr, "CardDeckCanvas에서 'CloseUI Button'을 찾을 수 없습니다.");
        Assert.IsNotNull(shopCloseTr, "ShopNPCUI에서 'CloseUIButton'을 찾을 수 없습니다.");

        RectTransform deckCloseRectPrefab = deckCloseTr as RectTransform;
        RectTransform shopCloseRectPrefab = shopCloseTr as RectTransform;
        Assert.IsNotNull(deckCloseRectPrefab);
        Assert.IsNotNull(shopCloseRectPrefab);

        Assert.AreEqual(shopCloseRectPrefab.anchorMin, deckCloseRectPrefab.anchorMin);
        Assert.AreEqual(shopCloseRectPrefab.anchorMax, deckCloseRectPrefab.anchorMax);
        Assert.AreEqual(shopCloseRectPrefab.pivot, deckCloseRectPrefab.pivot);
        Assert.AreEqual(shopCloseRectPrefab.anchoredPosition, deckCloseRectPrefab.anchoredPosition);
        Assert.AreEqual(shopCloseRectPrefab.sizeDelta, deckCloseRectPrefab.sizeDelta);

        Button deckCloseButton = deckCloseTr.GetComponent<Button>();
        Assert.IsNotNull(deckCloseButton);
        Assert.AreEqual(0, deckCloseButton.onClick.GetPersistentEventCount(), "인스펙터 중복 Persistent onClick 리스너는 제거되어야 합니다.");

        TextMeshProUGUI deckCloseLabel = deckCloseTr.GetComponentInChildren<TextMeshProUGUI>(true);
        Assert.IsNotNull(deckCloseLabel);
        Assert.AreEqual("나가기", deckCloseLabel.text);

        Vector2[] resolutions = { new Vector2(1920f, 1080f), new Vector2(2560f, 1440f) };
        for (int i = 0; i < resolutions.Length; i++)
        {
            Vector2 res = resolutions[i];
            GameObject canvasGo = Track(new GameObject($"DeckExitLayoutCanvas_{res.x}x{res.y}", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = res;

            GameObject deckGo = Track(UnityEngine.Object.Instantiate(deckPrefab, canvasGo.transform, false));
            CardDeckViewPanel deckPanel = deckGo.GetComponent<CardDeckViewPanel>();
            Assert.IsNotNull(deckPanel);

            Card card = CreateTestCard($"c_exit_{i}", "테스트", CardRarity.Common, 30, "Attack", ItemObtainSource.Standard, "Warrior");
            LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
            deckPanel.Open("전체 덱", new List<Card> { card }, CardViewMode.ViewOnly);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);

            RectTransform closeRect = deckGo.transform.Find("CloseUI Button") as RectTransform;
            RectTransform barRect = deckGo.transform.Find("FilterSortBar") as RectTransform;
            RectTransform contentRect = deckGo.transform.Find("CardList/Viewport/Content") as RectTransform;
            Assert.IsNotNull(closeRect);
            Assert.IsNotNull(barRect);
            Assert.IsNotNull(contentRect);

            Assert.GreaterOrEqual(GetMinX(closeRect), 0f);
            Assert.LessOrEqual(GetMaxX(closeRect), res.x);
            Assert.GreaterOrEqual(GetMinY(closeRect), 0f);
            Assert.LessOrEqual(GetMaxY(closeRect), GetMinY(barRect), $"{res.x}x{res.y}에서 나가기 버튼이 FilterSortBar와 겹치면 안 됩니다.");
            Assert.GreaterOrEqual(GetMinX(closeRect), GetMaxX(contentRect), $"{res.x}x{res.y}에서 나가기 버튼이 카드 그리드 영역과 겹치면 안 됩니다.");
        }
    }

    [Test]
    public void CardDeckCanvas_ConfirmButton_IsPositionedAboveExitButtonAndWired()
    {
        GameObject deckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/CardDeckCanvas.prefab");
        Assert.IsNotNull(deckPrefab, "CardDeckCanvas.prefab을 찾을 수 없습니다.");

        CardDeckViewPanel prefabPanel = deckPrefab.GetComponent<CardDeckViewPanel>();
        Assert.IsNotNull(prefabPanel);
        Assert.IsNotNull(prefabPanel.confirmButton, "CardDeckViewPanel.confirmButton이 연결되어야 합니다.");
        Assert.IsNotNull(prefabPanel.confirmButtonText, "CardDeckViewPanel.confirmButtonText가 연결되어야 합니다.");

        Transform confirmTr = deckPrefab.transform.Find("ConfirmButton");
        Assert.IsNotNull(confirmTr, "CardDeckCanvas에서 'ConfirmButton'을 찾을 수 없습니다.");
        Assert.IsFalse(confirmTr.gameObject.activeSelf, "기본 프리팹 상태에서 ConfirmButton은 비활성 상태여야 합니다.");
        Assert.AreEqual(0, prefabPanel.confirmButton.onClick.GetPersistentEventCount());

        GameObject canvasGo = Track(new GameObject("DeckConfirmLayoutCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);

        GameObject deckGo = Track(UnityEngine.Object.Instantiate(deckPrefab, canvasGo.transform, false));
        CardDeckViewPanel deckPanel = deckGo.GetComponent<CardDeckViewPanel>();
        Card card = CreateTestCard("c_confirm_layout", "테스트", CardRarity.Common, 30, "Attack", ItemObtainSource.Standard, "Warrior");

        // ViewOnly 모드에서는 승인 버튼이 숨겨져야 합니다.
        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        deckPanel.Open("전체 덱", new List<Card> { card }, CardViewMode.ViewOnly);
        Assert.IsFalse(deckPanel.confirmButton.gameObject.activeSelf);
        Assert.IsFalse(deckPanel.confirmButton.interactable);

        // SingleSelect 모드에서는 승인 버튼이 나가기 버튼 상단에 노출되어야 합니다.
        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        deckPanel.Open("제거할 카드를 선택하세요", new List<Card> { card }, CardViewMode.SingleSelect, 1, null, null, "제거 승인");
        Assert.IsTrue(deckPanel.confirmButton.gameObject.activeSelf);
        Assert.IsFalse(deckPanel.confirmButton.interactable);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);

        RectTransform confirmRect = deckGo.transform.Find("ConfirmButton") as RectTransform;
        RectTransform closeRect = deckGo.transform.Find("CloseUI Button") as RectTransform;
        RectTransform barRect = deckGo.transform.Find("FilterSortBar") as RectTransform;
        RectTransform contentRect = deckGo.transform.Find("CardList/Viewport/Content") as RectTransform;
        Assert.IsNotNull(confirmRect);
        Assert.IsNotNull(closeRect);
        Assert.IsNotNull(barRect);
        Assert.IsNotNull(contentRect);

        Assert.Greater(GetMinY(confirmRect), GetMaxY(closeRect), "승인 버튼은 나가기 버튼 위쪽에 배치되어야 합니다.");
        Assert.AreEqual(GetMaxX(closeRect), GetMaxX(confirmRect), 0.1f, "승인 버튼과 나가기 버튼은 오른쪽 끝이 정렬되어야 합니다.");
        Assert.AreEqual(closeRect.rect.width, confirmRect.rect.width, 0.1f);
        Assert.AreEqual(closeRect.rect.height, confirmRect.rect.height, 0.1f);
        Assert.LessOrEqual(GetMaxY(confirmRect), GetMinY(barRect), "승인 버튼이 FilterSortBar와 겹치면 안 됩니다.");
        Assert.GreaterOrEqual(GetMinX(confirmRect), GetMaxX(contentRect), "승인 버튼이 카드 그리드 영역과 겹치면 안 됩니다.");
    }

    [Test]
    public void CardDeckViewPanel_CardRemovalFlow_SeparatesConfirmAndExitAndManagesButtonStates()
    {
        GameObject deckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/CardDeckCanvas.prefab");
        Assert.IsNotNull(deckPrefab);

        GameObject deckGo = Track(UnityEngine.Object.Instantiate(deckPrefab));
        CardDeckViewPanel deckPanel = deckGo.GetComponent<CardDeckViewPanel>();
        Assert.IsNotNull(deckPanel);

        MethodInfo deckAwakeMethod = typeof(CardDeckViewPanel).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(deckAwakeMethod);
        deckAwakeMethod.Invoke(deckPanel, null);

        Card cardA = CreateTestCard("c_rem_a", "카드A", CardRarity.Common, 30, "Attack", ItemObtainSource.Starter, "Warrior");
        Card cardB = CreateTestCard("c_rem_b", "카드B", CardRarity.Common, 30, "Skill", ItemObtainSource.Starter, "Warrior");
        var cards = new List<Card> { cardA, cardB };

        GameObject dummyCardUiGo = Track(new GameObject("DummyCardUI", typeof(RectTransform)));
        CardUI dummyCardUI = dummyCardUiGo.AddComponent<CardUI>();
        MethodInfo selectClickMethod = typeof(CardDeckViewPanel).GetMethod("HandleSingleSelectClick", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(selectClickMethod);

        // 1. ViewOnly 모드에서는 승인 버튼이 숨겨지고 비활성화됩니다.
        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        deckPanel.Open("전체 덱", cards, CardViewMode.ViewOnly);
        Assert.IsFalse(deckPanel.confirmButton.gameObject.activeSelf);
        Assert.IsFalse(deckPanel.confirmButton.interactable);

        // 2. SingleSelect(제거) 모드 진입: 미선택 시 승인 버튼 비활성화 및 클릭 무효
        int confirmCalls = 0;
        int cancelCalls = 0;
        List<Card> confirmedSelection = null;

        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        deckPanel.Open(
            "제거할 카드를 선택하세요",
            cards,
            CardViewMode.SingleSelect,
            1,
            onConfirm: sel => { confirmCalls++; confirmedSelection = sel; },
            onCancel: () => { cancelCalls++; },
            confirmLabel: "제거 승인");

        Assert.IsTrue(deckPanel.confirmButton.gameObject.activeSelf);
        Assert.IsFalse(deckPanel.confirmButton.interactable);
        Assert.AreEqual("선택 (0/1)", deckPanel.confirmButtonText.text);

        LogAssert.Expect(LogType.Warning, "[CardDeckViewPanel] 1장을 선택해야 합니다. (현재: 0)");
        deckPanel.confirmButton.onClick.Invoke();
        Assert.AreEqual(0, confirmCalls);
        Assert.AreEqual(0, cancelCalls);
        Assert.IsTrue(deckPanel.gameObject.activeSelf);

        // 3. 카드 선택 후 나가기 버튼 클릭: 제거가 확정되지 않고 취소 콜백만 실행되어야 함
        selectClickMethod.Invoke(deckPanel, new object[] { dummyCardUI, cardA });
        Assert.AreEqual(1, deckPanel.selectedCards.Count);
        Assert.IsTrue(deckPanel.confirmButton.interactable);
        Assert.AreEqual("제거 승인", deckPanel.confirmButtonText.text);

        deckPanel.closeButton.onClick.Invoke();
        Assert.AreEqual(0, confirmCalls, "나가기 버튼 클릭 시 제거 확정 콜백이 호출되면 안 됩니다.");
        Assert.AreEqual(1, cancelCalls, "나가기 버튼 클릭 시 취소 콜백이 호출되어야 합니다.");
        Assert.IsFalse(deckPanel.gameObject.activeSelf);
        Assert.AreEqual(0, deckPanel.selectedCards.Count);
        Assert.IsFalse(deckPanel.confirmButton.gameObject.activeSelf);

        // 4. 재진입 후 카드 선택 및 승인 버튼 클릭: 확정 콜백만 실행되고 취소 콜백은 미호출
        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        deckPanel.Open(
            "제거할 카드를 선택하세요",
            cards,
            CardViewMode.SingleSelect,
            1,
            onConfirm: sel => { confirmCalls++; confirmedSelection = sel; },
            onCancel: () => { cancelCalls++; },
            confirmLabel: "제거 승인");

        selectClickMethod.Invoke(deckPanel, new object[] { dummyCardUI, cardB });
        Assert.IsTrue(deckPanel.confirmButton.interactable);
        Assert.AreEqual("제거 승인", deckPanel.confirmButtonText.text);

        deckPanel.confirmButton.onClick.Invoke();
        Assert.AreEqual(1, confirmCalls, "승인 버튼 클릭 시 확정 콜백이 1회 호출되어야 합니다.");
        Assert.AreEqual(1, cancelCalls, "승인 완료 시 취소 콜백이 중복 호출되면 안 됩니다.");
        Assert.IsNotNull(confirmedSelection);
        CollectionAssert.AreEqual(new[] { cardB }, confirmedSelection);
        Assert.IsFalse(deckPanel.gameObject.activeSelf);
        Assert.IsFalse(deckPanel.confirmButton.gameObject.activeSelf);

        // 5. DialogueManager 일시정지 상태에서 MarkActiveInteractableUsed 지연 및 취소 시 소모 방지 검증
        GameObject dmGo = Track(new GameObject("TestDialogueManagerForErase"));
        DialogueManager dm = dmGo.AddComponent<DialogueManager>();
        MethodInfo dmAwake = typeof(DialogueManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(dmAwake);
        dmAwake.Invoke(dm, null);

        GameObject interactGo = Track(new GameObject("TestBonfireInteractable"));
        InteractableObject interactable = interactGo.AddComponent<InteractableObject>();
        FieldInfo activeInteractBacking = typeof(DialogueManager).GetField("<ActiveInteractable>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(activeInteractBacking);
        activeInteractBacking.SetValue(dm, interactable);

        try
        {
            dm.PauseDialogue();
            dm.MarkActiveInteractableUsed();
            Assert.IsFalse(interactable.IsUsed, "제거 UI가 열려 있는 동안에는 오브젝트가 즉시 소모 처리되면 안 됩니다.");

            dm.CancelPausedChoice();
            Assert.IsFalse(dm.IsDialoguePaused);
            Assert.IsFalse(interactable.IsUsed, "나가기(취소) 시에는 오브젝트 소모 상태가 확정되면 안 됩니다.");

            dm.PauseDialogue();
            dm.MarkActiveInteractableUsed();
            dm.ResumeDialogue();
            Assert.IsFalse(dm.IsDialoguePaused);
            Assert.IsTrue(interactable.IsUsed, "제거 승인 후 대화 재개 시에만 오브젝트가 사용 완료 처리되어야 합니다.");
        }
        finally
        {
            FieldInfo dmInstanceBacking = typeof(DialogueManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            dmInstanceBacking?.SetValue(null, null);
        }
    }

    private static float GetMinX(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
    }

    private static float GetMaxX(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
    }

    private static float GetMinY(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
    }

    private static float GetMaxY(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
    }
}
