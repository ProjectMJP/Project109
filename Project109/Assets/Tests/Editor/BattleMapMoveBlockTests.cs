using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using GameItem.Types;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using XLua;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

[TestFixture]
public class BattleMapMoveBlockTests
{
    private const int TestSaveSlot = 996;

    private sealed class StubBattleAction : BattleAction
    {
        public bool WasCanceled { get; private set; }

        public StubBattleAction(Character caster) : base(caster) { }

        public override IEnumerator ExecuteRoutine()
        {
            while (true)
            {
                yield return null;
            }
        }

        public override void OnCanceled()
        {
            WasCanceled = true;
        }
    }

    private sealed class StubEnemyController : ICharacterController
    {
        public Character controlledCharacter { get; }

        public StubEnemyController(Character character)
        {
            controlledCharacter = character;
        }

        public void OnTurnStart() { }
        public void OnTurnEnd() { }
    }

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<Character> _createdCharacters = new List<Character>();
    private readonly List<PlayerBattleController> _createdControllers = new List<PlayerBattleController>();
    private readonly List<Player> _createdPlayers = new List<Player>();
    private readonly List<BattleManager> _createdBattleManagers = new List<BattleManager>();
    private readonly List<Card> _createdCards = new List<Card>();
    private LuaManager _createdLuaManager;

    private Dictionary<string, CardData> _backupCards;
    private Dictionary<string, RelicData> _backupRelics;
    private Dictionary<string, StarterKitData> _backupStarterKits;
    private Dictionary<string, InteractableData> _backupInteractables;
    private Dictionary<string, DropTableData> _backupDropTables;
    private int _prevSelectedSaveSlot;

    [SetUp]
    public void SetUp()
    {
        _backupCards = new Dictionary<string, CardData>(ModLoader.Instance.CardDatabase);
        _backupRelics = new Dictionary<string, RelicData>(ModLoader.Instance.RelicDatabase);
        _backupStarterKits = new Dictionary<string, StarterKitData>(ModLoader.Instance.StarterKitDatabase);
        _backupInteractables = new Dictionary<string, InteractableData>(ModLoader.Instance.InteractableDatabase);
        _backupDropTables = new Dictionary<string, DropTableData>(ModLoader.Instance.DropTableDatabase);
        _prevSelectedSaveSlot = GameSceneManager.selectedSaveSlot;

        TooltipManager.SetTooltipEnabled(true);
        SaveSystem.DeleteSaveFile(TestSaveSlot);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _createdCharacters.Count; i++)
        {
            Character c = _createdCharacters[i];
            if (c != null && c.characterMove != null)
            {
                c.characterMove.StopMoving();
            }
        }
        _createdCharacters.Clear();

        for (int i = _createdBattleManagers.Count - 1; i >= 0; i--)
        {
            if (_createdBattleManagers[i] != null)
            {
                _createdBattleManagers[i].Dispose();
            }
        }
        _createdBattleManagers.Clear();

        for (int i = _createdControllers.Count - 1; i >= 0; i--)
        {
            if (_createdControllers[i] != null)
            {
                _createdControllers[i].Dispose();
            }
        }
        _createdControllers.Clear();

        for (int i = _createdCards.Count - 1; i >= 0; i--)
        {
            if (_createdCards[i] != null && !_createdCards[i].IsDisposed)
            {
                _createdCards[i].Dispose();
            }
        }
        _createdCards.Clear();

        for (int i = _createdPlayers.Count - 1; i >= 0; i--)
        {
            if (_createdPlayers[i] != null)
            {
                _createdPlayers[i].Dispose();
            }
        }
        _createdPlayers.Clear();

        if (_createdLuaManager != null)
        {
            _createdLuaManager.Dispose();
            _createdLuaManager = null;
        }

        ActionQueueManager queueManager = UnityEngine.Object.FindFirstObjectByType<ActionQueueManager>();
        if (queueManager != null)
        {
            queueManager.ClearQueue();
            UnityEngine.Object.DestroyImmediate(queueManager.gameObject);
        }
        SetStaticField<ActionQueueManager>("_instance", null);

        SetStaticField<LuaManager>("_instance", null);
        SetStaticField<LuaManager>("_isShuttingDown", false);
        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        SetStaticField<TooltipManager>("instance", null);
        TooltipManager.SetTooltipEnabled(true);

        SetStaticAutoProperty<RunManager>("instance", null);
        SetStaticAutoProperty<GameSceneManager>("instance", null);
        SetStaticAutoProperty<UIManager>("instance", null);
        SetStaticAutoProperty<AssetCacheManager>("instance", null);
        SetStaticAutoProperty<DialogueManager>("Instance", null);
        SetStaticAutoProperty<BattleTimelinePanel>("Instance", null);
        SetStaticAutoProperty<BattleSideUnitHUDPanel>("Instance", null);
        SetStaticAutoProperty<GameMap>("current", null);

        GameSceneManager.selectedSaveSlot = _prevSelectedSaveSlot;
        SaveSystem.DeleteSaveFile(TestSaveSlot);

        ModLoader.Instance.CardDatabase.Clear();
        foreach (var kvp in _backupCards) ModLoader.Instance.CardDatabase[kvp.Key] = kvp.Value;

        ModLoader.Instance.RelicDatabase.Clear();
        foreach (var kvp in _backupRelics) ModLoader.Instance.RelicDatabase[kvp.Key] = kvp.Value;

        ModLoader.Instance.StarterKitDatabase.Clear();
        foreach (var kvp in _backupStarterKits) ModLoader.Instance.StarterKitDatabase[kvp.Key] = kvp.Value;

        ModLoader.Instance.InteractableDatabase.Clear();
        foreach (var kvp in _backupInteractables) ModLoader.Instance.InteractableDatabase[kvp.Key] = kvp.Value;

        ModLoader.Instance.DropTableDatabase.Clear();
        foreach (var kvp in _backupDropTables) ModLoader.Instance.DropTableDatabase[kvp.Key] = kvp.Value;

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private GameMap CreateTestGameMap(int width = 4, int height = 4, MapDataSO existingMapData = null)
    {
        GameObject mapGo = Track(new GameObject("TestGameMap"));
        GameMap gameMap = mapGo.AddComponent<GameMap>();
        InvokePrivateMethod(gameMap, "Awake");

        GameObject tilePrefab = Track(new GameObject("TilePrefab", typeof(Tile)));
        gameMap.prefabTile = tilePrefab;

        MapDataSO mapData = existingMapData;
        if (mapData == null)
        {
            mapData = Track(ScriptableObject.CreateInstance<MapDataSO>());
            mapData.stageName = "TestMap";
            mapData.locationType = LocationType.Temple;
            mapData.width = width;
            mapData.height = height;
            mapData.cellSize = 1f;
            mapData.gridOffset = Vector3.zero;
            mapData.InitializeGrid();

            for (int i = 0; i < mapData.cells.Count; i++)
            {
                mapData.cells[i].terrainID = "Floor";
            }
            mapData.cells[0].eventID = "PlayerSpawn";
        }

        gameMap.TileCreateByMapData(mapData);
        return gameMap;
    }

    private Character CreateTestCharacter(string name, CharacterFaction faction, Tile startTile = null, int moveRange = 3)
    {
        GameObject charGo = Track(new GameObject(name));
        Character character = charGo.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = faction;
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 120f,
            staminaRegenPerSecond = 40f,
            moveRange = moveRange
        });
        character.currentState = CharacterState.Idle;
        if (startTile != null && character.characterMove != null)
        {
            character.characterMove.SetCurrentTile(startTile);
            charGo.transform.position = startTile.transform.position;
        }
        _createdCharacters.Add(character);
        return character;
    }

    private Player CreateTestPlayer(Character character = null)
    {
        if (character == null)
        {
            character = CreateTestCharacter("TestPlayer", CharacterFaction.Player);
        }
        Player player = new Player(character);
        player.InitBattleDeck();
        _createdPlayers.Add(player);
        return player;
    }

    private GameSceneManager CreateTestGameSceneManager(Player player, bool isInDungeon = true)
    {
        GameSceneManager.selectedSaveSlot = TestSaveSlot;
        GameObject gsmGo = Track(new GameObject("TestGameSceneManager"));
        GameSceneManager gsm = gsmGo.AddComponent<GameSceneManager>();
        gsm.player = player;
        SetStaticAutoProperty<GameSceneManager>("instance", gsm);

        PropertyInfo inDungeonProp = typeof(GameSceneManager).GetProperty("isInDungeon", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(inDungeonProp);
        inDungeonProp.SetValue(gsm, isInDungeon);

        PropertyInfo saveSlotProp = typeof(GameSceneManager).GetProperty("currentSaveSlot", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(saveSlotProp);
        saveSlotProp.SetValue(gsm, TestSaveSlot);

        return gsm;
    }

    private (RunManager runManager, BattleManager battleManager, StageManager stageManager) CreateTestRunContext(Player player, GameMap gameMap = null)
    {
        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);

        BattleManager battleManager = new BattleManager();
        battleManager.Initialize();
        _createdBattleManagers.Add(battleManager);

        StageManager stageManager = new StageManager();
        stageManager.Initialize(gameMap);

        typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public).SetValue(runManager, battleManager);
        runManager.currentStage = stageManager;
        runManager.exploreMapManager = new ExploreMapManager();

        if (player != null)
        {
            runManager.playerBattleController = new PlayerBattleController(player);
            _createdControllers.Add(runManager.playerBattleController);
            runManager.playerExploreController = new PlayerExploreController(player);
            SetPrivateField(runManager, "_activePlayerController", runManager.playerBattleController);
        }

        return (runManager, battleManager, stageManager);
    }

    private PlayerInputController CreateMockInputController()
    {
        GameObject inputGo = Track(new GameObject("TestPlayerInputController"));
        PlayerInputController inputCtrl = inputGo.AddComponent<PlayerInputController>();
        SetStaticField<PlayerInputController>("_instance", inputCtrl);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        return inputCtrl;
    }

    private LuaManager CreateIsolatedLuaManager()
    {
        GameObject luaGo = Track(new GameObject("TestLuaManager"));
        LuaManager luaManager = luaGo.AddComponent<LuaManager>();
        SetStaticField<LuaManager>("_instance", luaManager);
        SetStaticField<LuaManager>("_isShuttingDown", false);
        InvokePrivateMethod(luaManager, "InitLuaEnv");
        _createdLuaManager = luaManager;
        return luaManager;
    }

    private BattleUIPanel CreateTestBattleUI(Player player, PlayerBattleController controller)
    {
        GameObject canvasGo = Track(new GameObject("TestBattleUICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject battleUIGo = Track(new GameObject("BattleUIPanel", typeof(RectTransform)));
        battleUIGo.transform.SetParent(canvasGo.transform, false);
        BattleUIPanel battleUI = battleUIGo.AddComponent<BattleUIPanel>();

        GameObject handGo = Track(new GameObject("BattleHandPanel", typeof(RectTransform)));
        handGo.transform.SetParent(battleUIGo.transform, false);
        BattleHandPanel handPanel = handGo.AddComponent<BattleHandPanel>();
        GameObject handContentGo = Track(new GameObject("HandContent", typeof(RectTransform)));
        handContentGo.transform.SetParent(handGo.transform, false);
        handPanel.contentTransform = handContentGo.transform;

        GameObject moveGo = Track(new GameObject("BattleMovePanel", typeof(RectTransform)));
        moveGo.transform.SetParent(battleUIGo.transform, false);
        BattleMovePanel movePanel = moveGo.AddComponent<BattleMovePanel>();

        GameObject endTurnGo = Track(new GameObject("EndTurnButton", typeof(RectTransform), typeof(Image), typeof(Button)));
        endTurnGo.transform.SetParent(battleUIGo.transform, false);

        SetPrivateField(battleUI, "battleHandPanel", handPanel);
        SetPrivateField(battleUI, "battleMovePanel", movePanel);
        SetPrivateField(battleUI, "endTurnButton", endTurnGo.GetComponent<Button>());

        InvokePrivateMethod(handPanel, "Awake");
        InvokePrivateMethod(movePanel, "Awake");
        InvokePrivateMethod(battleUI, "Awake");
        InvokePrivateMethod(battleUI, "OnEnable");
        InvokePrivateMethod(handPanel, "OnEnable");
        InvokePrivateMethod(movePanel, "OnEnable");

        SetPrivateField(controller, "_battleUI", battleUI);
        battleUI.BindPlayer(player, controller);
        battleUIGo.SetActive(true);
        return battleUI;
    }

    private (BattleTimelinePanel timelineUI, BattleSideUnitHUDPanel sideHud) CreateTestAuxBattleUIs(BattleManager battleManager, PlayerBattleController controller)
    {
        GameObject canvasGo = Track(new GameObject("TestAuxBattleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject timelinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleTimelinePanel.prefab");
        GameObject sideHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleSideUnitHUDPanel.prefab");
        Assert.IsNotNull(timelinePrefab, "BattleTimelinePanel.prefab을 찾을 수 없습니다.");
        Assert.IsNotNull(sideHudPrefab, "BattleSideUnitHUDPanel.prefab을 찾을 수 없습니다.");

        GameObject timelineGo = Track(UnityEngine.Object.Instantiate(timelinePrefab, canvasGo.transform, false));
        BattleTimelinePanel timelineUI = timelineGo.GetComponent<BattleTimelinePanel>();
        InvokePrivateMethod(timelineUI, "Awake");
        timelineUI.BindBattle(battleManager);
        timelineUI.Open();

        GameObject sideHudGo = Track(UnityEngine.Object.Instantiate(sideHudPrefab, canvasGo.transform, false));
        BattleSideUnitHUDPanel sideHud = sideHudGo.GetComponent<BattleSideUnitHUDPanel>();
        InvokePrivateMethod(sideHud, "Awake");
        sideHud.BindBattle(battleManager);
        sideHud.Open();

        SetPrivateField(controller, "_timelineUI", timelineUI);
        SetPrivateField(controller, "_sideUnitHUD", sideHud);

        return (timelineUI, sideHud);
    }

    private TooltipManager CreateTestTooltipManager(Canvas canvas)
    {
        GameObject ttGo = Track(new GameObject("TooltipManager", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)));
        ttGo.transform.SetParent(canvas.transform, false);

        ContentSizeFitter fitter = ttGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/TooltipPanel.prefab");
        Assert.IsNotNull(templatePrefab, "TooltipPanel.prefab을 찾을 수 없습니다.");

        TooltipManager manager = ttGo.AddComponent<TooltipManager>();
        SetPrivateField(manager, "panelTemplatePrefab", templatePrefab.GetComponent<TooltipPanel>());
        InvokePrivateMethod(manager, "Awake");
        return manager;
    }

    private (ExploreMapUI mapUI, TextMeshProUGUI noticeText) CreateTestExploreMapUI(Canvas canvas, ExploreMapData customMapData)
    {
        GameObject mapUiGo = Track(new GameObject("TestExploreMapUI", typeof(RectTransform)));
        mapUiGo.transform.SetParent(canvas.transform, false);

        GameObject viewLayoutGo = Track(new GameObject("ViewLayout", typeof(RectTransform)));
        viewLayoutGo.transform.SetParent(mapUiGo.transform, false);

        GameObject noticeGo = Track(new GameObject("MoveBlockedNoticeText", typeof(RectTransform), typeof(TextMeshProUGUI)));
        noticeGo.transform.SetParent(mapUiGo.transform, false);
        TextMeshProUGUI noticeText = noticeGo.GetComponent<TextMeshProUGUI>();

        GameObject verticalPrefab = Track(new GameObject("TestVerticalLayoutPrefab", typeof(RectTransform), typeof(VerticalLayoutGroup)));
        GameObject nodePrefab = Track(new GameObject("TestExploreNodePrefab", typeof(RectTransform), typeof(Image), typeof(ExploreNodeUI)));

        GameObject coverObj = new GameObject("Cover", typeof(RectTransform));
        coverObj.transform.SetParent(nodePrefab.transform, false);
        GameObject hoverRingObj = new GameObject("HoverRing", typeof(RectTransform));
        hoverRingObj.transform.SetParent(nodePrefab.transform, false);
        GameObject visitedRingObj = new GameObject("VisitedRing", typeof(RectTransform));
        visitedRingObj.transform.SetParent(nodePrefab.transform, false);

        ExploreNodeUI prefabNodeUi = nodePrefab.GetComponent<ExploreNodeUI>();
        prefabNodeUi.ExploreNodeUICoverObject = coverObj;
        prefabNodeUi.ExploreNodeUIHighlightCircleObject = hoverRingObj;
        prefabNodeUi.ExploreNodeUICurrentHighlightCircleObject = visitedRingObj;

        ExploreMapUI mapUI = mapUiGo.AddComponent<ExploreMapUI>();
        SetPrivateField(mapUI, "ViewLayout", viewLayoutGo);
        SetPrivateField(mapUI, "ExploreMapVerticalLayoutPrefab", verticalPrefab);
        SetPrivateField(mapUI, "NodePrefab", nodePrefab);
        SetPrivateField(mapUI, "moveBlockedNoticeText", noticeText);

        RunManager.instance.exploreMapManager.RestoreMapData(customMapData);
        RunManager.instance.currentExploreNode = customMapData.floors[0].nodes[0];
        RunManager.instance.currentExploreMapFloor = 0;

        mapUI.CreateExploreMap(customMapData.floors.Count);
        return (mapUI, noticeText);
    }

    private static ExploreMapData CreateTwoFloorMapData()
    {
        ExploreMapData mapData = new ExploreMapData();

        ExploreFloorData floor0 = new ExploreFloorData();
        ExploreNode startNode = new ExploreNode(0, 0)
        {
            isRevealed = true,
            isVisited = true
        };
        startNode.encounterData.locationType = LocationType.Temple;
        startNode.encounterData.encountType = EncountType.Battle;
        startNode.nextNodeIndices.Add(0);
        floor0.nodes.Add(startNode);

        ExploreFloorData floor1 = new ExploreFloorData();
        ExploreNode connectedNext = new ExploreNode(1, 0)
        {
            isRevealed = true
        };
        connectedNext.encounterData.locationType = LocationType.Temple;
        connectedNext.encounterData.encountType = EncountType.None;

        ExploreNode unconnectedNode = new ExploreNode(1, 1)
        {
            isRevealed = true
        };
        unconnectedNode.encounterData.locationType = LocationType.Temple;
        unconnectedNode.encounterData.encountType = EncountType.None;

        floor1.nodes.Add(connectedNext);
        floor1.nodes.Add(unconnectedNode);

        mapData.floors.Add(floor0);
        mapData.floors.Add(floor1);
        return mapData;
    }

    private static void SetBattleState(BattleManager battleManager, BattleState state)
    {
        typeof(BattleManager).GetProperty("battleState", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, state);
    }

    private static int GetEventSubscriberCount(object target, string eventFieldName)
    {
        FieldInfo field = target.GetType().GetField(eventFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"이벤트 백킹 필드 '{eventFieldName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        Delegate del = field.GetValue(target) as Delegate;
        return del != null ? del.GetInvocationList().Length : 0;
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"메서드 '{methodName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"필드 '{fieldName}'을(를) {target.GetType().Name}에서 찾을 수 없습니다.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"필드 '{fieldName}'을(를) {target.GetType().Name}에서 찾을 수 없습니다.");
        field.SetValue(target, value);
    }

    private static void SetStaticField<T>(string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"정적 필드 '{fieldName}'을(를) {typeof(T).Name}에서 찾을 수 없습니다.");
        field.SetValue(null, value);
    }

    private static void SetStaticAutoProperty<T>(string propName, object value)
    {
        FieldInfo field = typeof(T).GetField($"<{propName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"정적 백킹 필드 '<{propName}>k__BackingField'를 {typeof(T).Name}에서 찾을 수 없습니다.");
        field.SetValue(null, value);
    }

    [Test]
    public void CanMoveToNextNode_BlocksNormalPlayerDuringActiveBattle_AndBlocksMoveToNodeCall()
    {
        Player player = CreateTestPlayer();
        CreateTestGameSceneManager(player);
        var (runManager, battleManager, stageManager) = CreateTestRunContext(player);

        Assert.IsFalse(player.playerStat.CanBypassBattleMapLock);

        // 1. MapState.Battle 상태만으로 차단
        stageManager.currentMapState = MapState.Battle;
        SetBattleState(battleManager, BattleState.None);
        Assert.IsFalse(runManager.CanMoveToNextNode(out string reasonByMapState));
        Assert.AreEqual("전투를 완료해야 다음 노드로 이동할 수 있습니다.", reasonByMapState);

        // 2. BattleState.Intro / Combat / TurnInProgress 상태에서 차단
        stageManager.currentMapState = MapState.None;
        foreach (BattleState activeState in new[] { BattleState.Intro, BattleState.Combat, BattleState.TurnInProgress })
        {
            SetBattleState(battleManager, activeState);
            Assert.IsFalse(runManager.CanMoveToNextNode(out string reasonByBattleState), $"BattleState.{activeState}에서는 이동이 차단되어야 합니다.");
            Assert.AreEqual("전투를 완료해야 다음 노드로 이동할 수 있습니다.", reasonByBattleState);
        }

        // 3. UI 우회로 RunManager.MoveToNode 직접 호출 시에도 차단 및 현재 노드/층 유지 검증
        stageManager.currentMapState = MapState.Battle;
        SetBattleState(battleManager, BattleState.TurnInProgress);

        ExploreMapData mapData = CreateTwoFloorMapData();
        runManager.exploreMapManager.RestoreMapData(mapData);
        runManager.currentExploreNode = mapData.floors[0].nodes[0];
        runManager.currentExploreMapFloor = 0;

        GameObject nextNodeGo = Track(new GameObject("BlockedTargetNode", typeof(RectTransform), typeof(Image), typeof(ExploreNodeUI)));
        ExploreNodeUI nextNodeUI = nextNodeGo.GetComponent<ExploreNodeUI>();
        nextNodeUI.InitExploreNodeUI(mapData.floors[1].nodes[0]);

        LogAssert.Expect(LogType.Warning, new Regex("노드 이동이 차단되었습니다"));
        runManager.MoveToNode(nextNodeUI);

        Assert.AreSame(mapData.floors[0].nodes[0], runManager.currentExploreNode);
        Assert.AreEqual(0, runManager.currentExploreMapFloor);
    }

    [Test]
    public void CanMoveToNextNode_BlocksWhenDialogueActive_EvenWithBypassRelic()
    {
        Player player = CreateTestPlayer();
        player.playerStat.BattleMapLockBypassCount = 1;
        CreateTestGameSceneManager(player);
        var (runManager, battleManager, stageManager) = CreateTestRunContext(player);

        stageManager.currentMapState = MapState.Battle;
        SetBattleState(battleManager, BattleState.Combat);

        // 1. 주입 인자 기반 대화 중 차단 검증 (우회 유물이 있어도 대화 중에는 차단)
        Assert.IsFalse(runManager.CanMoveToNextNode(player, isDialogueActive: true, out string injectedReason));
        Assert.AreEqual("NPC 대화 중에는 다음 노드로 이동할 수 없습니다.", injectedReason);

        // 2. 실제 DialogueManager.Instance.IsDialogueActive 연동 검증
        GameObject dmGo = Track(new GameObject("TestDialogueManager"));
        DialogueManager dm = dmGo.AddComponent<DialogueManager>();
        SetStaticAutoProperty<DialogueManager>("Instance", dm);
        SetPrivateField(dm, "activeDialogue", new DialogueData { dialogueID = "test_dlg" });
        Assert.IsTrue(dm.IsDialogueActive);

        Assert.IsFalse(runManager.CanMoveToNextNode(out string liveReason));
        Assert.AreEqual("NPC 대화 중에는 다음 노드로 이동할 수 없습니다.", liveReason);
    }

    [Test]
    public void CanMoveToNextNode_AllowsMoveWhenBattleEnded()
    {
        Player player = CreateTestPlayer();
        CreateTestGameSceneManager(player);
        var (runManager, battleManager, stageManager) = CreateTestRunContext(player);

        stageManager.currentMapState = MapState.None;
        SetBattleState(battleManager, BattleState.None);
        Assert.IsTrue(runManager.CanMoveToNextNode(out string noneReason));
        Assert.IsEmpty(noneReason);

        SetBattleState(battleManager, BattleState.BattleEnd);
        Assert.IsTrue(runManager.CanMoveToNextNode(out string endReason));
        Assert.IsEmpty(endReason);
    }

    [Test]
    public void TestBypassRelic_LuaScriptAndSaveSystemReset_ControlLockBypassWithoutCountDrift()
    {
        Player player = CreateTestPlayer();
        CreateTestGameSceneManager(player);
        var (runManager, battleManager, stageManager) = CreateTestRunContext(player);

        stageManager.currentMapState = MapState.Battle;
        SetBattleState(battleManager, BattleState.TurnInProgress);

        // 1. 실제 "테스트 통행증.lua"를 XLua 환경에서 컴파일하여 OnInit / OnRemoved 동작 검증
        LuaManager luaManager = CreateIsolatedLuaManager();
        string luaPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Relics", "테스트 통행증.lua");
        Assert.IsTrue(File.Exists(luaPath), $"유물 Lua 스크립트를 찾을 수 없습니다: {luaPath}");

        object[] results = luaManager.luaEnv.DoString(File.ReadAllBytes(luaPath), "테스트 통행증");
        Assert.IsNotNull(results);
        Assert.Greater(results.Length, 0);
        LuaTable proto = results[0] as LuaTable;
        Assert.IsNotNull(proto);

        RelicData relicData = new RelicData
        {
            relicName = "테스트 통행증",
            displayName = "테스트 통행증",
            obtainSource = ItemObtainSource.TestOnly,
            rarity = RelicRarity.Common,
            classTypes = new List<string> { "TesterWarrior" },
            luaPrototype = proto
        };

        Assert.AreEqual(0, player.playerStat.BattleMapLockBypassCount);
        Assert.IsFalse(runManager.CanMoveToNextNode(out _));

        LuaTable relicInstance = luaManager.NewInstance(proto);
        Relic activeRelic = new Relic(relicData, player, relicInstance);

        Assert.AreEqual(1, player.playerStat.BattleMapLockBypassCount);
        Assert.IsTrue(player.playerStat.CanBypassBattleMapLock);
        Assert.IsTrue(runManager.HasBattleMapLockBypass());
        Assert.IsTrue(runManager.CanMoveToNextNode(out string allowedReason));
        Assert.IsEmpty(allowedReason);

        // 유물 제거(OnRemoved) 시 카운트 감소 및 이동 제한 즉시 복구
        activeRelic.Dispose();
        Assert.AreEqual(0, player.playerStat.BattleMapLockBypassCount);
        Assert.IsFalse(player.playerStat.CanBypassBattleMapLock);
        Assert.IsFalse(runManager.CanMoveToNextNode(out string restoredBlockReason));
        Assert.AreEqual("전투를 완료해야 다음 노드로 이동할 수 있습니다.", restoredBlockReason);

        // 이미 0인 상태에서 OnRemoved가 재호출되어도 음수로 내려가지 않아야 함 (math.max(0, ...))
        LuaTable extraInst = luaManager.NewInstance(proto);
        var onRemovedFunc = extraInst.Get<Action<LuaTable, Relic, Player>>("OnRemoved");
        Assert.IsNotNull(onRemovedFunc);
        onRemovedFunc(extraInst, null, player);
        extraInst.Dispose();
        Assert.AreEqual(0, player.playerStat.BattleMapLockBypassCount);

        // 2. SaveSystem.ApplyStarterKit() / RestorePlayerData()가 ClearRelics() 후 BattleMapLockBypassCount를 0으로 리셋하는지 검증
        player.playerStat.BattleMapLockBypassCount = 3; // 누적 드리프트 시뮬레이션
        StarterKitData emptyKit = new StarterKitData
        {
            loadoutId = "warrior_starter",
            startGold = 100,
            baseDrawCount = 5,
            startRelics = new List<string>()
        };
        Assert.IsTrue(SaveSystem.ApplyStarterKit(player, emptyKit));
        Assert.AreEqual(0, player.playerStat.BattleMapLockBypassCount, "ApplyStarterKit은 유물 재장착 전 BattleMapLockBypassCount를 0으로 초기화해야 합니다.");

        player.playerStat.BattleMapLockBypassCount = 2;
        RunSaveData emptySave = new RunSaveData
        {
            playerGold = 50,
            playerRelicNames = new List<string>()
        };
        SaveSystem.RestorePlayerData(emptySave, player);
        Assert.AreEqual(0, player.playerStat.BattleMapLockBypassCount, "RestorePlayerData는 유물 복원 전 BattleMapLockBypassCount를 0으로 초기화해야 합니다.");
    }

    [Test]
    public void CleanupActiveBattleForForcedMove_AndForcedMoveToNode_CleansUpBattleQueueControllerAndUI()
    {
        PlayerInputController inputCtrl = CreateMockInputController();
        GameMap gameMap = CreateTestGameMap(4, 4);
        Tile playerTile = gameMap.GetTile(1, 1);
        Tile enemyTile = gameMap.GetTile(2, 1);

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, playerTile, moveRange: 3);
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, enemyTile, moveRange: 2);
        Player player = CreateTestPlayer(playerChar);
        player.playerStat.BattleMapLockBypassCount = 1;
        CreateTestGameSceneManager(player, isInDungeon: true);

        // StageManager.GenerateStage가 에러 없이 새 노드 맵을 조립할 수 있도록 AssetCacheManager 시드
        GameObject cacheGo = Track(new GameObject("TestAssetCacheManager"));
        AssetCacheManager cache = cacheGo.AddComponent<AssetCacheManager>();
        SetStaticAutoProperty<AssetCacheManager>("instance", cache);
        MapDataSO nextStageMapData = Track(ScriptableObject.CreateInstance<MapDataSO>());
        nextStageMapData.stageName = "NextStageTestMap";
        nextStageMapData.locationType = LocationType.Temple;
        nextStageMapData.width = 3;
        nextStageMapData.height = 3;
        nextStageMapData.cellSize = 1f;
        nextStageMapData.InitializeGrid();
        for (int i = 0; i < nextStageMapData.cells.Count; i++)
        {
            nextStageMapData.cells[i].terrainID = "Floor";
        }
        nextStageMapData.cells[0].eventID = "PlayerSpawn";
        cache.mapList = new List<MapDataSO> { nextStageMapData };

        var (runManager, battleManager, stageManager) = CreateTestRunContext(player, gameMap);
        PlayerBattleController battleController = runManager.playerBattleController;

        BattleUIPanel battleUI = CreateTestBattleUI(player, battleController);
        var (timelineUI, sideHud) = CreateTestAuxBattleUIs(battleManager, battleController);

        // 1. 전투 시작 및 플레이어 턴 진입 -> TargetSelection 진입 -> 액션 큐 적재 상태 구성
        StubEnemyController enemyController = new StubEnemyController(enemyChar);
        battleManager.InitBattle(
            new List<ICharacterController> { battleController },
            new List<ICharacterController> { enemyController });
        battleController.OnBattleStart();
        SetPrivateField(battleController, "_battleUI", battleUI);
        SetPrivateField(battleController, "_timelineUI", timelineUI);
        SetPrivateField(battleController, "_sideUnitHUD", sideHud);

        stageManager.currentMapState = MapState.Battle;
        battleManager.RequestTurnStart(playerChar);

        Assert.AreEqual(BattleState.TurnInProgress, battleManager.battleState);
        Assert.AreEqual(1, GetEventSubscriberCount(inputCtrl, "OnTouchClickEvent"));
        Assert.AreEqual(1, GetEventSubscriberCount(inputCtrl, "OnPointerHoverEvent"));
        Assert.AreEqual(1, GetEventSubscriberCount(inputCtrl, "OnCancelEvent"));

        CardData targetCardData = new CardData
        {
            cardName = "TestTargetCard",
            displayName = "테스트 타깃 카드",
            stamina = 0,
            targetType = "SingleEnemy",
            targetMinDistance = 1,
            targetMaxDistance = 3
        };
        Card targetCard = new Card(targetCardData, null, null, -1);
        _createdCards.Add(targetCard);

        battleController.TryUseCard(targetCard);
        Assert.AreEqual(PlayerControlState.TargetSelection, battleController.controlState);
        Assert.AreSame(targetCard, GetPrivateField<Card>(battleController, "activeCardForTargeting"));

        StubBattleAction runningAction = new StubBattleAction(playerChar);
        StubBattleAction queuedAction = new StubBattleAction(playerChar);
        Assert.IsTrue(ActionQueueManager.Instance.EnqueueAction(runningAction));
        Assert.IsTrue(ActionQueueManager.Instance.EnqueueAction(queuedAction));
        Assert.IsTrue(ActionQueueManager.HasInstance);
        Assert.IsTrue(ActionQueueManager.Instance.IsBusy);

        // 2. CleanupActiveBattleForForcedMove() 호출 검증
        runManager.CleanupActiveBattleForForcedMove();

        Assert.AreEqual(BattleState.None, battleManager.battleState);
        Assert.AreEqual(MapState.None, stageManager.currentMapState);
        Assert.IsFalse(ActionQueueManager.Instance.IsBusy);
        Assert.IsTrue(runningAction.WasCanceled);
        Assert.IsTrue(queuedAction.WasCanceled);
        Assert.AreEqual(PlayerControlState.Normal, battleController.controlState);
        Assert.IsNull(GetPrivateField<Card>(battleController, "activeCardForTargeting"));
        Assert.AreEqual(0, battleController.MultiSelectedTargets.Count);
        Assert.IsFalse(battleUI.gameObject.activeSelf);
        Assert.IsFalse(timelineUI.gameObject.activeSelf);
        Assert.IsFalse(sideHud.gameObject.activeSelf);
        Assert.AreSame(runManager.playerExploreController, runManager.activePlayerController);

        // PlayerExploreController.Activate()가 OnTouchClickEvent 1개만 등록하고, PlayerBattleController 핸들러는 전부 해제되어야 함
        Assert.AreEqual(1, GetEventSubscriberCount(inputCtrl, "OnTouchClickEvent"));
        Assert.AreEqual(0, GetEventSubscriberCount(inputCtrl, "OnPointerHoverEvent"));
        Assert.AreEqual(0, GetEventSubscriberCount(inputCtrl, "OnCancelEvent"));

        // 3. OnTurnEnd() 없이 PlayerBattleController.OnBattleEnd()만 직접 호출해도 Move 상태 및 입력 이벤트가 안전 해제되는지 검증
        battleManager.InitBattle(
            new List<ICharacterController> { battleController },
            new List<ICharacterController> { new StubEnemyController(enemyChar) });
        battleController.OnBattleStart();
        SetPrivateField(battleController, "_battleUI", battleUI);
        SetPrivateField(battleController, "_timelineUI", timelineUI);
        SetPrivateField(battleController, "_sideUnitHUD", sideHud);
        battleUI.gameObject.SetActive(true);
        timelineUI.gameObject.SetActive(true);
        sideHud.gameObject.SetActive(true);
        stageManager.currentMapState = MapState.Battle;

        battleManager.RequestTurnStart(playerChar);
        battleController.EnablePlayerMove();
        PlayerBattleMove playerMove = GetPrivateField<PlayerBattleMove>(battleController, "playerMove");
        Assert.AreEqual(PlayerControlState.Move, battleController.controlState);
        Assert.Greater(playerMove.canMoveTiles.Count, 0);
        Assert.AreEqual(2, GetEventSubscriberCount(inputCtrl, "OnTouchClickEvent"));
        Assert.AreEqual(1, GetEventSubscriberCount(inputCtrl, "OnPointerHoverEvent"));
        Assert.AreEqual(1, GetEventSubscriberCount(inputCtrl, "OnCancelEvent"));

        battleController.OnBattleEnd();
        Assert.AreEqual(PlayerControlState.Normal, battleController.controlState);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);
        Assert.AreEqual(1, GetEventSubscriberCount(inputCtrl, "OnTouchClickEvent"));
        Assert.AreEqual(0, GetEventSubscriberCount(inputCtrl, "OnPointerHoverEvent"));
        Assert.AreEqual(0, GetEventSubscriberCount(inputCtrl, "OnCancelEvent"));
        Assert.IsFalse(battleUI.gameObject.activeSelf);
        Assert.IsFalse(timelineUI.gameObject.activeSelf);
        Assert.IsFalse(sideHud.gameObject.activeSelf);

        // 4. 실제 전투 중 우회 유물로 MoveToNode() 호출 시 전투 정리 + 다음 노드 전환 동시 수행 검증
        battleManager.InitBattle(
            new List<ICharacterController> { battleController },
            new List<ICharacterController> { new StubEnemyController(enemyChar) });
        battleController.OnBattleStart();
        SetPrivateField(battleController, "_battleUI", battleUI);
        battleUI.gameObject.SetActive(true);
        stageManager.currentMapState = MapState.Battle;
        battleManager.RequestTurnStart(playerChar);
        battleController.EnablePlayerMove();

        ExploreMapData mapData = CreateTwoFloorMapData();
        runManager.exploreMapManager.RestoreMapData(mapData);
        runManager.currentExploreNode = mapData.floors[0].nodes[0];
        runManager.currentExploreMapFloor = 0;

        GameObject nextNodeGo = Track(new GameObject("ForcedMoveTargetNode", typeof(RectTransform), typeof(Image), typeof(ExploreNodeUI)));
        ExploreNodeUI nextNodeUI = nextNodeGo.GetComponent<ExploreNodeUI>();
        nextNodeUI.InitExploreNodeUI(mapData.floors[1].nodes[0]);

        runManager.MoveToNode(nextNodeUI);

        Assert.AreSame(mapData.floors[1].nodes[0], runManager.currentExploreNode);
        Assert.AreEqual(1, runManager.currentExploreMapFloor);
        Assert.AreEqual(BattleState.None, battleManager.battleState);
        Assert.AreEqual(MapState.None, stageManager.currentMapState);
        Assert.AreEqual(PlayerControlState.Normal, battleController.controlState);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);
        Assert.IsFalse(battleUI.gameObject.activeSelf);
        Assert.AreSame(runManager.playerExploreController, runManager.activePlayerController);
    }

    [Test]
    public void ExploreMapUI_And_ExploreNodeUI_VisualDimmingHoverSuppressionAndTooltipFeedback()
    {
        Player player = CreateTestPlayer();
        CreateTestGameSceneManager(player);
        var (runManager, battleManager, stageManager) = CreateTestRunContext(player);

        GameObject canvasGo = Track(new GameObject("ExploreMapTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        TooltipManager tooltipManager = CreateTestTooltipManager(canvas);

        ExploreMapData mapData = CreateTwoFloorMapData();
        var (mapUI, noticeText) = CreateTestExploreMapUI(canvas, mapData);

        ExploreNodeUI currentNodeUI = mapUI.MapNodes[0][0];
        ExploreNodeUI connectedNextUI = mapUI.MapNodes[1][0];
        ExploreNodeUI unconnectedNextUI = mapUI.MapNodes[1][1];

        Assert.AreSame(mapUI, currentNodeUI.ExploreMapUI);
        Assert.AreSame(mapUI, connectedNextUI.ExploreMapUI);

        // 1. 전투 중(우회 없음): 연결된 다음 노드만 어둡게 표시 + 호버 링 억제 + 안내 문구 표시
        stageManager.currentMapState = MapState.Battle;
        SetBattleState(battleManager, BattleState.TurnInProgress);
        mapUI.RefreshMapVisuals();

        Assert.IsTrue(noticeText.gameObject.activeSelf);
        Assert.AreEqual("전투를 완료해야 다음 노드로 이동할 수 있습니다.", noticeText.text);

        Assert.IsTrue(connectedNextUI.IsMoveBlocked);
        Assert.AreEqual(new Color(0.55f, 0.55f, 0.55f, 0.8f), connectedNextUI.GetComponent<Image>().color);
        Assert.IsFalse(unconnectedNextUI.IsMoveBlocked);
        Assert.AreEqual(Color.white, unconnectedNextUI.GetComponent<Image>().color);
        Assert.IsFalse(currentNodeUI.IsMoveBlocked);

        // 차단 상태에서 호버 시 강조 링 비활성 유지
        connectedNextUI.OnPointerEnter(null);
        Assert.IsFalse(connectedNextUI.ExploreNodeUIHighlightCircleObject.activeSelf);

        // 차단 상태에서 클릭 시 이동 거부 + TooltipManager("이동 불가", ...) 즉시 노출
        connectedNextUI.LoadMapDataFromExploreNodeUI();
        Assert.AreSame(mapData.floors[0].nodes[0], runManager.currentExploreNode);
        Assert.IsTrue(tooltipManager.gameObject.activeSelf);

        var activePanels = GetPrivateField<List<TooltipPanel>>(tooltipManager, "activePanels");
        Assert.AreEqual(1, activePanels.Count);
        Assert.AreEqual("이동 불가", GetPrivateField<TextMeshProUGUI>(activePanels[0], "_headerText").text);
        Assert.AreEqual("전투를 완료해야 다음 노드로 이동할 수 있습니다.", GetPrivateField<TextMeshProUGUI>(activePanels[0], "_bodyText").text);

        // 포인터 이탈 시 툴팁 안전 숨김
        connectedNextUI.OnPointerExit(null);
        Assert.IsFalse(tooltipManager.gameObject.activeSelf);

        // 다시 클릭 후 OnDisable 호출 시에도 툴팁 안전 숨김
        connectedNextUI.LoadMapDataFromExploreNodeUI();
        Assert.IsTrue(tooltipManager.gameObject.activeSelf);
        InvokePrivateMethod(connectedNextUI, "OnDisable");
        Assert.IsFalse(tooltipManager.gameObject.activeSelf);

        // 2. 우회 유물 활성화 후 RefreshMapVisuals() 호출 시 차단 시각화 및 안내 문구 해제 + 호버 강조 복원
        player.playerStat.BattleMapLockBypassCount = 1;
        mapUI.RefreshMapVisuals();

        Assert.IsFalse(noticeText.gameObject.activeSelf);
        Assert.IsFalse(connectedNextUI.IsMoveBlocked);
        Assert.AreEqual(Color.white, connectedNextUI.GetComponent<Image>().color);
        connectedNextUI.OnPointerEnter(null);
        Assert.IsTrue(connectedNextUI.ExploreNodeUIHighlightCircleObject.activeSelf);
    }

    [Test]
    public void RelicAndStarterKitAndDropTables_YamlValidation()
    {
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        string coreRoot = Path.Combine(Application.streamingAssetsPath, "Mods", "Core");

        // 1. "테스트 통행증.yaml" 스키마 검증
        string relicYamlPath = Path.Combine(coreRoot, "YAML", "Relics", "테스트 통행증.yaml");
        Assert.IsTrue(File.Exists(relicYamlPath), $"유물 YAML을 찾을 수 없습니다: {relicYamlPath}");
        RelicData passRelic = deserializer.Deserialize<RelicData>(File.ReadAllText(relicYamlPath, System.Text.Encoding.UTF8));

        Assert.IsNotNull(passRelic);
        Assert.AreEqual("테스트 통행증", passRelic.relicName);
        Assert.AreEqual("테스트 통행증", passRelic.displayName);
        Assert.AreEqual(ItemObtainSource.TestOnly, passRelic.obtainSource);
        Assert.AreEqual(RelicRarity.Common, passRelic.rarity);
        CollectionAssert.Contains(passRelic.classTypes, "TesterWarrior");
        Assert.IsTrue(passRelic.CanAppearForClass("TesterWarrior"));
        Assert.IsFalse(passRelic.CanAppearForClass("Warrior"));

        // 일반 보상 풀 및 도감 필터에서 TestOnly 제외 확인
        Assert.IsFalse(GameItemRewardManager.IsRelicObtainableInRandomPool(passRelic));
        var compendiumRelics = PauseMenuPanel.FilterAndSortRelics(
            new[] { passRelic },
            PauseMenuPanel.RelicRarityFilter.All,
            PauseMenuPanel.RelicObtainSourceFilter.All,
            PauseMenuPanel.RelicSortCriteria.Name,
            true);
        Assert.AreEqual(0, compendiumRelics.Count);

        // 2. "테스트 통행증.lua" 존재 및 XLua 샌드박스 위반 없음 검증
        string relicLuaPath = Path.Combine(coreRoot, "Scripts", "Relics", "테스트 통행증.lua");
        Assert.IsTrue(File.Exists(relicLuaPath), $"유물 Lua를 찾을 수 없습니다: {relicLuaPath}");
        string[] luaLines = File.ReadAllLines(relicLuaPath, System.Text.Encoding.UTF8);
        for (int i = 0; i < luaLines.Length; i++)
        {
            string codePart = luaLines[i].Split(new[] { "--" }, StringSplitOptions.None)[0];
            Assert.IsFalse(Regex.IsMatch(codePart, @"\b(CS|io|os|debug)\."), $"XLua 샌드박스 금지 키워드가 감지되었습니다 ({i + 1}행): {luaLines[i]}");
        }

        // 3. "tester_warrior_starter.yaml" 시작 유물 검증
        string starterKitPath = Path.Combine(coreRoot, "YAML", "StarterKits", "tester_warrior_starter.yaml");
        Assert.IsTrue(File.Exists(starterKitPath), $"시작 키트 YAML을 찾을 수 없습니다: {starterKitPath}");
        StarterKitData testerKit = deserializer.Deserialize<StarterKitData>(File.ReadAllText(starterKitPath, System.Text.Encoding.UTF8));

        Assert.IsNotNull(testerKit);
        Assert.AreEqual("TesterWarrior", testerKit.classType);
        CollectionAssert.Contains(testerKit.startRelics, "가죽 장갑");
        CollectionAssert.Contains(testerKit.startRelics, "테스트 통행증");

        // 4. DropTables/*.yaml 전수 검증 ("테스트 통행증" 미포함)
        string dropTablesDir = Path.Combine(coreRoot, "YAML", "DropTables");
        string[] dropTableFiles = Directory.GetFiles(dropTablesDir, "*.yaml", SearchOption.AllDirectories);
        Assert.Greater(dropTableFiles.Length, 0);

        foreach (string dtFile in dropTableFiles)
        {
            string rawYaml = File.ReadAllText(dtFile, System.Text.Encoding.UTF8);
            StringAssert.DoesNotContain("테스트 통행증", rawYaml, $"드롭 테이블({Path.GetFileName(dtFile)})에 '테스트 통행증'이 포함되면 안 됩니다.");

            DropTableData dt = deserializer.Deserialize<DropTableData>(rawYaml);
            if (dt != null && dt.specificItemIDs != null)
            {
                CollectionAssert.DoesNotContain(dt.specificItemIDs, "테스트 통행증", $"DropTable '{dt.dropTableID}'의 specificItemIDs에 '테스트 통행증'이 포함되면 안 됩니다.");
            }
        }
    }
}
