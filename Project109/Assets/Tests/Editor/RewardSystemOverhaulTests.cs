using System;
using System.Collections.Generic;
using System.Reflection;
using GameItem.Types;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[TestFixture]
public class RewardSystemOverhaulTests
{
    private const int TestSaveSlot = 994;

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<Player> _createdPlayers = new List<Player>();
    private readonly List<StageManager> _createdStages = new List<StageManager>();

    private Dictionary<string, CardData> _backupCards;
    private Dictionary<string, RelicData> _backupRelics;
    private Dictionary<string, DropTableData> _backupDropTables;
    private int _prevSelectedSaveSlot;
    private UnityEngine.Random.State _prevRandomState;

    [SetUp]
    public void SetUp()
    {
        _prevRandomState = UnityEngine.Random.state;
        _prevSelectedSaveSlot = GameSceneManager.selectedSaveSlot;

        _backupCards = new Dictionary<string, CardData>(ModLoader.Instance.CardDatabase);
        _backupRelics = new Dictionary<string, RelicData>(ModLoader.Instance.RelicDatabase);
        _backupDropTables = new Dictionary<string, DropTableData>(ModLoader.Instance.DropTableDatabase);

        SaveSystem.DeleteSaveFile(TestSaveSlot);
        SaveSystem.InvalidateAllCaches();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdStages.Count - 1; i >= 0; i--)
        {
            if (_createdStages[i] != null)
            {
                _createdStages[i].Dispose();
            }
        }
        _createdStages.Clear();

        for (int i = _createdPlayers.Count - 1; i >= 0; i--)
        {
            if (_createdPlayers[i] != null)
            {
                _createdPlayers[i].Dispose();
            }
        }
        _createdPlayers.Clear();

        SetStaticAutoProperty<RunManager>("instance", null);
        SetStaticField<RunManager>("OnNodeContextChanged", null);
        SetStaticAutoProperty<GameSceneManager>("instance", null);
        SetStaticAutoProperty<AssetCacheManager>("instance", null);
        SetStaticAutoProperty<UIManager>("instance", null);
        SetStaticAutoProperty<GameItemRewardManager>("instance", null);
        SetStaticAutoProperty<DialogueManager>("Instance", null);
        SetStaticAutoProperty<GameMap>("current", null);
        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);

        GameSceneManager.selectedSaveSlot = _prevSelectedSaveSlot;
        SaveSystem.DeleteSaveFile(TestSaveSlot);
        SaveSystem.InvalidateAllCaches();

        ModLoader.Instance.CardDatabase.Clear();
        foreach (KeyValuePair<string, CardData> kvp in _backupCards)
        {
            ModLoader.Instance.CardDatabase[kvp.Key] = kvp.Value;
        }

        ModLoader.Instance.RelicDatabase.Clear();
        foreach (KeyValuePair<string, RelicData> kvp in _backupRelics)
        {
            ModLoader.Instance.RelicDatabase[kvp.Key] = kvp.Value;
        }

        ModLoader.Instance.DropTableDatabase.Clear();
        foreach (KeyValuePair<string, DropTableData> kvp in _backupDropTables)
        {
            ModLoader.Instance.DropTableDatabase[kvp.Key] = kvp.Value;
        }

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();

        UnityEngine.Random.state = _prevRandomState;
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private static void SetStaticAutoProperty<TTarget>(string propName, object value)
    {
        FieldInfo backing = typeof(TTarget).GetField($"<{propName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(backing, $"{typeof(TTarget).Name}.{propName} 자동 프로퍼티 백킹 필드를 찾을 수 없습니다.");
        backing.SetValue(null, value);
    }

    private static void SetStaticField<TTarget>(string fieldName, object value)
    {
        FieldInfo field = typeof(TTarget).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{typeof(TTarget).Name}.{fieldName} 정적 필드를 찾을 수 없습니다.");
        field.SetValue(null, value);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
        return (T)field.GetValue(target);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} 메서드를 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private void SeedRewardCatalog()
    {
        ModLoader.Instance.CardDatabase.Clear();
        for (int i = 1; i <= 5; i++)
        {
            string id = $"Test_Reward_Card_{i}";
            ModLoader.Instance.CardDatabase[id] = new CardData
            {
                cardName = id,
                displayName = $"테스트 카드 {i}",
                description = "테스트 효과",
                rarity = CardRarity.Common,
                obtainSource = ItemObtainSource.Standard,
                stamina = 1
            };
        }

        ModLoader.Instance.RelicDatabase.Clear();
        ModLoader.Instance.RelicDatabase["Test_Reward_Relic_1"] = new RelicData
        {
            relicName = "Test_Reward_Relic_1",
            displayName = "테스트 유물",
            description = "유물 설명",
            rarity = RelicRarity.Common,
            obtainSource = ItemObtainSource.Standard
        };

        ModLoader.Instance.DropTableDatabase.Clear();
        ModLoader.Instance.DropTableDatabase["Default_Card_Table"] = new DropTableData
        {
            dropTableID = "Default_Card_Table",
            commonWeight = 100
        };
        ModLoader.Instance.DropTableDatabase["Default_Relic_Table"] = new DropTableData
        {
            dropTableID = "Default_Relic_Table",
            commonWeight = 100
        };
    }

    private (RunManager runManager, Player player, UIManager uiManager, TopHUDPanel topHud, GameItemRewardManager rewardManager) CreateFullRewardHarness(
        EncountType encountType = EncountType.Battle,
        BattleExtraRewardType extraRewardType = BattleExtraRewardType.None)
    {
        SeedRewardCatalog();

        GameObject inputGo = Track(new GameObject("TestPlayerInputController"));
        PlayerInputController inputCtrl = inputGo.AddComponent<PlayerInputController>();
        SetStaticField<PlayerInputController>("_instance", inputCtrl);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);

        GameObject rewardListPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Reward/RewardListCanvas.prefab");
        GameObject cardRewardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/CardRewardCanvas.prefab");
        GameObject topHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/TopHUDPanel.prefab");
        GameObject uiManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/UIManager.prefab");

        Assert.IsNotNull(rewardListPrefab);
        Assert.IsNotNull(cardRewardPrefab);
        Assert.IsNotNull(topHudPrefab);
        Assert.IsNotNull(uiManagerPrefab);

        GameObject cacheGo = Track(new GameObject("TestAssetCacheManager"));
        AssetCacheManager cache = cacheGo.AddComponent<AssetCacheManager>();
        SetStaticAutoProperty<AssetCacheManager>("instance", cache);

        MapDataSO testMap = Track(ScriptableObject.CreateInstance<MapDataSO>());
        testMap.name = "Temple_SecretBox_Test";
        testMap.stageName = "Temple";
        testMap.locationType = LocationType.Temple;
        testMap.EncountType = EncountType.SecretBox;
        testMap.width = 4;
        testMap.height = 4;
        testMap.cellSize = 1f;
        testMap.InitializeGrid();
        testMap.cells[0].eventID = "PlayerSpawn";

        SetPrivateField(cache, "mapDict", new Dictionary<string, MapDataSO> { { testMap.ID, testMap } });
        SetPrivateField(cache, "stageMapDataDict", new Dictionary<string, StageMapDataBundle>
        {
            {
                LocationType.Temple.ToString(),
                new StageMapDataBundle
                {
                    stageName = "Temple",
                    secretMapDataList = new List<MapDataSO> { testMap },
                    battleMapDataList = new List<MapDataSO> { testMap },
                    eliteMapDataList = new List<MapDataSO> { testMap },
                    bossMapDataList = new List<MapDataSO> { testMap },
                    startMapDataList = new List<MapDataSO> { testMap },
                    storeMapDataList = new List<MapDataSO> { testMap },
                    restoreMapDataList = new List<MapDataSO> { testMap }
                }
            }
        });

        SetPrivateField(cache, "uiDict", new Dictionary<string, GameObject>
        {
            { UIConstants.PANEL_REWARD_LIST, rewardListPrefab },
            { UIConstants.PANEL_CARD_REWARD, cardRewardPrefab },
            { UIConstants.PANEL_TOP_HUD, topHudPrefab }
        });

        GameObject uiManagerGo = Track(UnityEngine.Object.Instantiate(uiManagerPrefab));
        UIManager uiManager = uiManagerGo.GetComponent<UIManager>();
        InvokePrivateMethod(uiManager, "Awake");

        DialogueManager dm = uiManagerGo.GetComponent<DialogueManager>();
        if (dm != null)
        {
            InvokePrivateMethod(dm, "Awake");
        }

        GameObject charGo = Track(new GameObject("TestPlayerChar"));
        Character playerChar = charGo.AddComponent<Character>();
        InvokePrivateMethod(playerChar, "Awake");
        playerChar.faction = CharacterFaction.Player;
        playerChar.InitializeStat(new CharacterStat { maxHealth = 100f, maxStamina = 100f, staminaRegenPerSecond = 20f, moveRange = 3 });
        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        GameSceneManager.selectedSaveSlot = TestSaveSlot;
        GameObject gsmGo = Track(new GameObject("TestGameSceneManager"));
        GameSceneManager gsm = gsmGo.AddComponent<GameSceneManager>();
        gsm.player = player;
        SetStaticAutoProperty<GameSceneManager>("instance", gsm);
        typeof(GameSceneManager).GetProperty("isInDungeon", BindingFlags.Instance | BindingFlags.Public).SetValue(gsm, true);
        typeof(GameSceneManager).GetProperty("currentSaveSlot", BindingFlags.Instance | BindingFlags.Public).SetValue(gsm, TestSaveSlot);

        GameObject rewardMgrGo = Track(new GameObject("TestGameItemRewardManager"));
        GameItemRewardManager rewardManager = rewardMgrGo.AddComponent<GameItemRewardManager>();
        SetStaticAutoProperty<GameItemRewardManager>("instance", rewardManager);
        rewardManager.InitializeDatabases(
            ModLoader.Instance.CardDatabase,
            ModLoader.Instance.RelicDatabase,
            ModLoader.Instance.DropTableDatabase);

        GameObject mapGo = Track(new GameObject("TestGameMap"));
        GameMap gameMap = mapGo.AddComponent<GameMap>();
        InvokePrivateMethod(gameMap, "Awake");
        GameObject tilePrefab = Track(new GameObject("TilePrefab", typeof(Tile)));
        gameMap.prefabTile = tilePrefab;

        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        InvokePrivateMethod(runManager, "Awake");

        StageManager stageManager = new StageManager();
        stageManager.Initialize(gameMap);
        _createdStages.Add(stageManager);
        runManager.currentStage = stageManager;
        runManager.exploreMapManager = new ExploreMapManager();

        ExploreNode currentNode = new ExploreNode
        {
            floor = 0,
            index = 0,
            encounterData = new EncounterData
            {
                encountType = encountType,
                locationType = LocationType.Temple,
                battleExtraRewardType = extraRewardType,
                mapDataId = testMap.ID
            }
        };
        runManager.currentExploreNode = currentNode;

        TopHUDPanel topHud = uiManager.OpenUI<TopHUDPanel>(UIConstants.PANEL_TOP_HUD, UILayerType.Top);
        Assert.IsNotNull(topHud);
        topHud.BindPlayer(player);

        return (runManager, player, uiManager, topHud, rewardManager);
    }

    [Test]
    public void ReleaseSceneAndCleanCut_MeetArchitectureSpec()
    {
        Assembly csharpAssembly = typeof(RewardType).Assembly;
        Assert.IsNull(csharpAssembly.GetType("GameItem.Types.RewardItemType"), "레거시 RewardItemType enum은 제거되어야 합니다.");
        Assert.IsNull(csharpAssembly.GetType("GameItem.Types.ItemRewardUIType"), "레거시 ItemRewardUIType enum은 제거되어야 합니다.");

        BindingFlags allInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.IsNull(typeof(GameItemRewardManager).GetField("rewardObjectPrefab", allInstance));
        Assert.IsNull(typeof(GameItemRewardManager).GetField("rewardBoxPrefab", allInstance));
        Assert.IsNull(typeof(GameItemRewardManager).GetMethod("InstantiateItemReward", allInstance));
        Assert.IsNull(typeof(GameItemRewardManager).GetMethod("SpawnRewardBox", allInstance));
        Assert.IsNull(typeof(ShopNPC).GetField("shopUICanvasPrefab", allInstance));
        Assert.IsNull(typeof(RewardChest).GetField("rewardListUIPrefab", allInstance));

        const string releaseScenePath = "Assets/Scenes/Release/GameScene.unity";
        Scene loadedScene = SceneManager.GetSceneByPath(releaseScenePath);
        bool openedByTest = !loadedScene.isLoaded;
        if (openedByTest)
        {
            loadedScene = EditorSceneManager.OpenScene(releaseScenePath, OpenSceneMode.Additive);
        }

        try
        {
            Assert.IsTrue(loadedScene.isLoaded, "릴리스 GameScene을 로드할 수 있어야 합니다.");
            GameItemRewardManager foundManager = null;
            GameObject[] roots = loadedScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "[Managers]")
                {
                    foundManager = roots[i].GetComponentInChildren<GameItemRewardManager>(true);
                    break;
                }
            }
            Assert.IsNotNull(foundManager, "GameScene.unity의 [Managers] 하위에 GameItemRewardManager가 정적 배치되어 있어야 합니다.");
        }
        finally
        {
            if (openedByTest && loadedScene.isLoaded)
            {
                EditorSceneManager.CloseScene(loadedScene, true);
            }
        }

        GameObject rewardListPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Reward/RewardListCanvas.prefab");
        GameObject cardRewardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/CardRewardCanvas.prefab");
        GameObject topHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/TopHUDPanel.prefab");

        Assert.AreEqual(0, rewardListPrefab.transform.Find("CloseUI Button").GetComponent<Button>().onClick.GetPersistentEventCount());
        Assert.AreEqual(0, cardRewardPrefab.transform.Find("CloseUI Button").GetComponent<Button>().onClick.GetPersistentEventCount());
        Assert.IsNotNull(topHudPrefab.transform.Find("NodeReopenButton"), "TopHUDPanel 프리팹에 NodeReopenButton이 배치되어야 합니다.");
    }

    [Test]
    public void BattleVictory_PopulatesPendingSession_SupportsCloseReopenAndCardCancelPreservation()
    {
        var (runManager, player, uiManager, topHud, _) = CreateFullRewardHarness(EncountType.Battle, BattleExtraRewardType.Relic);
        int initialGold = player.playerStat.InGameCurrencyGold;
        int initialShard = player.playerStat.InGameCurrencyMemorySharp;
        runManager.battleManager.accumulativeGoldReward = 15;

        InvokePrivateMethod(runManager, "HandleBattleWon");

        Assert.IsTrue(runManager.currentExploreNode.isCleared);
        Assert.IsTrue(runManager.HasPendingRewards);
        Assert.AreEqual(4, runManager.PendingRewardCount);

        RewardListUIHandler rewardPanel = uiManager.GetUI<RewardListUIHandler>(UIConstants.PANEL_REWARD_LIST);
        Assert.IsNotNull(rewardPanel);
        Assert.IsTrue(rewardPanel.IsPanelOpen);
        Assert.IsTrue(rewardPanel.gameObject.activeSelf);

        Button reopenButton = GetPrivateField<Button>(topHud, "nodeReopenButton");
        TMP_Text reopenText = GetPrivateField<TMP_Text>(topHud, "nodeReopenButtonText");
        Assert.IsFalse(reopenButton.gameObject.activeSelf, "보상 패널이 열려 있는 동안에는 재확인 버튼이 숨겨져야 합니다.");

        // 1. 닫기 시 남은 미수령 보상 개수만큼 재확인 버튼 노출
        rewardPanel.Close();
        Assert.IsFalse(rewardPanel.gameObject.activeSelf);
        Assert.IsTrue(reopenButton.gameObject.activeSelf);
        Assert.AreEqual("보상 확인(4)", reopenText.text);

        // 2. 재확인 버튼 클릭 시 보상 UI 재오픈
        reopenButton.onClick.Invoke();
        Assert.IsTrue(rewardPanel.gameObject.activeSelf);
        Assert.IsTrue(rewardPanel.IsPanelOpen);
        Assert.IsFalse(reopenButton.gameObject.activeSelf);

        // 3. 골드만 수령 후 창 닫기 -> 미수령 3개만 남아 재확인 버튼에 3 표시
        List<ItemRewardUIHandler> items = GetPrivateField<List<ItemRewardUIHandler>>(rewardPanel, "_spawnedItems");
        Assert.AreEqual(4, items.Count);
        ItemRewardUIHandler goldItem = items.Find(i => i != null && i.BoundEntry != null && i.BoundEntry.rewardType == RewardType.Gold);
        Assert.IsNotNull(goldItem);
        goldItem.OnPointerClick(new PointerEventData(EventSystem.current));

        Assert.AreEqual(initialGold + 75, player.playerStat.InGameCurrencyGold);
        Assert.AreEqual(3, runManager.PendingRewardCount);

        // 4. 카드 선택지 열었다가 고르지 않고 닫으면 추첨 결과와 미수령 엔트리가 유지되어야 함
        items = GetPrivateField<List<ItemRewardUIHandler>>(rewardPanel, "_spawnedItems");
        ItemRewardUIHandler cardItem = items.Find(i => i != null && i.BoundEntry != null && i.BoundEntry.rewardType == RewardType.CardChoice);
        Assert.IsNotNull(cardItem);
        List<string> rolledBeforeCancel = new List<string>(cardItem.BoundEntry.rolledCardNames);
        Assert.AreEqual(3, rolledBeforeCancel.Count);

        cardItem.OnPointerClick(new PointerEventData(EventSystem.current));
        CardRewardPanel cardPanel = uiManager.GetUI<CardRewardPanel>(UIConstants.PANEL_CARD_REWARD);
        Assert.IsNotNull(cardPanel);
        Assert.IsTrue(cardPanel.gameObject.activeSelf);

        Button cardCloseBtn = GetPrivateField<Button>(cardPanel, "closeButton");
        cardCloseBtn.onClick.Invoke();
        Assert.IsFalse(cardPanel.gameObject.activeSelf);
        Assert.AreEqual(3, runManager.PendingRewardCount);
        CollectionAssert.AreEqual(rolledBeforeCancel, cardItem.BoundEntry.rolledCardNames);

        // 5. 기억의 파편, 카드 1장 선택, 유물 순차 수령 시 마지막 수령 후 보상 창 자동 종료 및 재확인 버튼 숨김
        items = GetPrivateField<List<ItemRewardUIHandler>>(rewardPanel, "_spawnedItems");
        ItemRewardUIHandler shardItem = items.Find(i => i != null && i.BoundEntry != null && i.BoundEntry.rewardType == RewardType.MemorySharp);
        Assert.IsNotNull(shardItem);
        shardItem.OnPointerClick(new PointerEventData(EventSystem.current));
        Assert.AreEqual(initialShard + 2, player.playerStat.InGameCurrencyMemorySharp);

        items = GetPrivateField<List<ItemRewardUIHandler>>(rewardPanel, "_spawnedItems");
        cardItem = items.Find(i => i != null && i.BoundEntry != null && i.BoundEntry.rewardType == RewardType.CardChoice);
        cardItem.OnPointerClick(new PointerEventData(EventSystem.current));
        List<CardUI> spawnedCards = GetPrivateField<List<CardUI>>(cardPanel, "cardList");
        Assert.AreEqual(3, spawnedCards.Count);
        spawnedCards[0].OnCardClick.Invoke();

        Assert.AreEqual(1, player.deck.GetCards().Count);
        Assert.AreEqual(1, runManager.PendingRewardCount);
        Assert.IsTrue(rewardPanel.gameObject.activeSelf);

        items = GetPrivateField<List<ItemRewardUIHandler>>(rewardPanel, "_spawnedItems");
        Assert.AreEqual(1, items.Count);
        items[0].OnPointerClick(new PointerEventData(EventSystem.current));

        Assert.AreEqual(1, player.relicManager.GetRelics().Count);
        Assert.IsFalse(runManager.HasPendingRewards);
        Assert.IsFalse(rewardPanel.gameObject.activeSelf, "마지막 보상을 수령하면 보상 UI가 자동으로 닫혀야 합니다.");
        Assert.IsFalse(reopenButton.gameObject.activeSelf, "보상을 모두 수령하면 재확인 버튼이 숨겨져야 합니다.");
    }

    [Test]
    public void DialogueActionExecutor_RewardChest_And_MoveToNode_LifecycleWorkCorrectly()
    {
        var (runManager, player, uiManager, topHud, _) = CreateFullRewardHarness(EncountType.SecretBox, BattleExtraRewardType.None);
        DialogueActionExecutor executor = new DialogueActionExecutor();

        executor.SpawnReward("card", "common", Vector3.zero);
        Assert.AreEqual(1, runManager.PendingRewardCount);

        RewardListUIHandler rewardPanel = uiManager.GetUI<RewardListUIHandler>(UIConstants.PANEL_REWARD_LIST);
        Assert.IsNotNull(rewardPanel);
        Assert.IsTrue(rewardPanel.gameObject.activeSelf);
        rewardPanel.Close();

        GameObject chestGo = Track(new GameObject("TestRewardChest"));
        RewardChest chest = chestGo.AddComponent<RewardChest>();
        chest.OpenChestDirectly();
        int afterChestFirstOpen = runManager.PendingRewardCount;
        Assert.Greater(afterChestFirstOpen, 1);

        chest.OpenChestDirectly();
        Assert.AreEqual(afterChestFirstOpen, runManager.PendingRewardCount, "같은 보물 상자를 다시 열어도 보상이 중복 추가되면 안 됩니다.");

        rewardPanel.Close();
        Button reopenButton = GetPrivateField<Button>(topHud, "nodeReopenButton");
        Assert.IsTrue(reopenButton.gameObject.activeSelf);

        // 다음 노드로 이동하면 잔여 보상과 재확인 버튼이 모두 정리되어야 함
        GameObject nextNodeGo = Track(new GameObject("NextExploreNode", typeof(Image)));
        ExploreNodeUI nextNodeUI = nextNodeGo.AddComponent<ExploreNodeUI>();
        nextNodeUI.InitExploreNodeUI(new ExploreNode
        {
            floor = 1,
            index = 0,
            encounterData = new EncounterData
            {
                encountType = EncountType.SecretBox,
                locationType = LocationType.Temple,
                mapDataId = "Temple_SecretBox_Test"
            }
        });

        runManager.MoveToNode(nextNodeUI);

        Assert.IsFalse(runManager.HasPendingRewards, "다음 노드로 이동하면 미수령 보상 세션이 초기화되어야 합니다.");
        Assert.IsFalse(reopenButton.gameObject.activeSelf, "다음 노드로 이동하면 재확인 버튼이 숨겨져야 합니다.");
    }

    [Test]
    public void SaveSystem_PreservesPendingRewardsAcrossSaveAndRestore()
    {
        var (runManager, _, _, _, _) = CreateFullRewardHarness(EncountType.Battle, BattleExtraRewardType.Relic);
        runManager.PopulateBattleVictoryRewards(bonusGold: 10);
        Assert.AreEqual(4, runManager.PendingRewardCount);

        PendingRewardEntry goldEntry = runManager.pendingRewardSession.entries.Find(e => e.rewardType == RewardType.Gold);
        Assert.IsNotNull(goldEntry);
        Assert.IsTrue(runManager.TryClaimPendingReward(goldEntry.entryId));
        Assert.AreEqual(3, runManager.PendingRewardCount);

        PendingRewardEntry cardEntry = runManager.pendingRewardSession.entries.Find(e => e.rewardType == RewardType.CardChoice);
        Assert.IsNotNull(cardEntry);
        List<string> savedRolledCards = new List<string>(cardEntry.rolledCardNames);

        SaveSystem.SaveGame(runManager, TestSaveSlot);
        Assert.IsTrue(SaveSystem.TryLoadGameData(TestSaveSlot, out RunSaveData loaded, forceReload: true));
        Assert.IsNotNull(loaded.pendingRewards);
        Assert.AreEqual(3, loaded.pendingRewards.Count);

        runManager.ClearPendingRewards();
        Assert.IsFalse(runManager.HasPendingRewards);

        SaveSystem.RestoreRunData(loaded, runManager);
        Assert.AreEqual(3, runManager.PendingRewardCount);
        PendingRewardEntry restoredCardEntry = runManager.pendingRewardSession.entries.Find(e => e.rewardType == RewardType.CardChoice);
        Assert.IsNotNull(restoredCardEntry);
        CollectionAssert.AreEqual(savedRolledCards, restoredCardEntry.rolledCardNames);
    }
}
