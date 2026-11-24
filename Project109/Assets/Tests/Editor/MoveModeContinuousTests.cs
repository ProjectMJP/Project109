using System;
using System.Collections.Generic;
using System.Reflection;
using EventStructs;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class MoveModeContinuousTests
{
    private sealed class SyncMoveCanceler : IOnBeforeMove
    {
        public bool ShouldCancel { get; set; }
        public int CallCount { get; private set; }

        public void OnBeforeMove(ref MoveInfo info)
        {
            CallCount++;
            if (ShouldCancel)
            {
                info.isCanceled = true;
            }
        }
    }

    private sealed class StubTurnController : ICharacterController
    {
        public Character controlledCharacter { get; }

        public StubTurnController(Character character)
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
    private readonly List<Camera> _disabledSceneMainCameras = new List<Camera>();

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

        for (int i = _createdPlayers.Count - 1; i >= 0; i--)
        {
            if (_createdPlayers[i] != null)
            {
                _createdPlayers[i].Dispose();
            }
        }
        _createdPlayers.Clear();

        ActionQueueManager queueManager = UnityEngine.Object.FindFirstObjectByType<ActionQueueManager>();
        if (queueManager != null)
        {
            queueManager.ClearQueue();
            UnityEngine.Object.DestroyImmediate(queueManager.gameObject);
        }
        SetStaticField<ActionQueueManager>("_instance", null);

        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        SetStaticAutoProperty<RunManager>("instance", null);
        SetStaticAutoProperty<GameSceneManager>("instance", null);
        SetStaticAutoProperty<UIManager>("instance", null);
        SetStaticAutoProperty<GameMap>("current", null);

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();

        for (int i = 0; i < _disabledSceneMainCameras.Count; i++)
        {
            Camera cam = _disabledSceneMainCameras[i];
            if (cam != null)
            {
                cam.enabled = true;
            }
        }
        _disabledSceneMainCameras.Clear();
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private GameMap CreateTestGameMap(int width = 5, int height = 5)
    {
        GameObject mapGo = Track(new GameObject("TestGameMap"));
        GameMap gameMap = mapGo.AddComponent<GameMap>();
        InvokePrivateMethod(gameMap, "Awake");

        GameObject tilePrefab = Track(new GameObject("TilePrefab", typeof(Tile)));
        gameMap.prefabTile = tilePrefab;

        MapDataSO mapData = Track(ScriptableObject.CreateInstance<MapDataSO>());
        mapData.width = width;
        mapData.height = height;
        mapData.cellSize = 1f;
        mapData.gridOffset = Vector3.zero;
        mapData.InitializeGrid();

        for (int i = 0; i < mapData.cells.Count; i++)
        {
            mapData.cells[i].terrainID = "Floor";
        }

        gameMap.TileCreateByMapData(mapData);
        return gameMap;
    }

    private Camera CreateTestMainCamera(Vector3 lookCenter)
    {
        Camera[] existingCameras = Camera.allCameras;
        for (int i = 0; i < existingCameras.Length; i++)
        {
            Camera existing = existingCameras[i];
            if (existing != null && existing.enabled && existing.CompareTag("MainCamera"))
            {
                existing.enabled = false;
                _disabledSceneMainCameras.Add(existing);
            }
        }

        GameObject camGo = Track(new GameObject("TestMainCamera", typeof(Camera)));
        camGo.tag = "MainCamera";
        camGo.transform.position = lookCenter + new Vector3(0f, 10f, 0f);
        camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        Camera cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        return cam;
    }

    private Character CreateTestCharacter(string name, CharacterFaction faction, Tile startTile, int moveRange = 3)
    {
        GameObject charGo = Track(new GameObject(name));
        Character character = charGo.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = faction;
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 10f,
            staminaRegenPerSecond = 0f,
            moveRange = moveRange
        });
        character.characterMove.moveSpeed = 0.001f;
        character.currentState = CharacterState.Idle;
        if (startTile != null)
        {
            character.characterMove.SetCurrentTile(startTile);
            charGo.transform.position = startTile.transform.position;
        }
        _createdCharacters.Add(character);
        return character;
    }

    private (Player player, PlayerBattleController controller) CreateTestPlayerController(Character character)
    {
        Player player = new Player(character);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);
        return (player, controller);
    }

    private (BattleUIPanel battleUI, BattleHandPanel handPanel, BattleMovePanel movePanel, TextMeshProUGUI drawCountText, TextMeshProUGUI moveCountText)
        CreateAndBindBattleUI(Player player, PlayerBattleController controller)
    {
        GameObject canvasGo = Track(new GameObject("TestBattleUICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject battleUIGo = Track(new GameObject("BattleUIPanel", typeof(RectTransform)));
        battleUIGo.transform.SetParent(canvasGo.transform, false);
        battleUIGo.SetActive(false);

        BattleUIPanel battleUI = battleUIGo.AddComponent<BattleUIPanel>();

        GameObject handGo = Track(new GameObject("BattleHandPanel", typeof(RectTransform)));
        handGo.transform.SetParent(battleUIGo.transform, false);
        BattleHandPanel handPanel = handGo.AddComponent<BattleHandPanel>();

        GameObject handContentGo = Track(new GameObject("HandContent", typeof(RectTransform)));
        handContentGo.transform.SetParent(handGo.transform, false);
        handPanel.contentTransform = handContentGo.transform;

        GameObject drawCountGo = Track(new GameObject("DrawCountText", typeof(RectTransform), typeof(TextMeshProUGUI)));
        drawCountGo.transform.SetParent(handGo.transform, false);
        TextMeshProUGUI drawCountText = drawCountGo.GetComponent<TextMeshProUGUI>();
        SetPrivateField(handPanel, "drawCountText", drawCountText);

        GameObject modeSwitchTextGo = Track(new GameObject("ModeSwitchText", typeof(RectTransform), typeof(TextMeshProUGUI)));
        modeSwitchTextGo.transform.SetParent(handGo.transform, false);
        SetPrivateField(handPanel, "modeSwitchButtonText", modeSwitchTextGo.GetComponent<TextMeshProUGUI>());

        GameObject moveGo = Track(new GameObject("BattleMovePanel", typeof(RectTransform)));
        moveGo.transform.SetParent(battleUIGo.transform, false);
        BattleMovePanel movePanel = moveGo.AddComponent<BattleMovePanel>();

        GameObject moveCountGo = Track(new GameObject("MoveCountText", typeof(RectTransform), typeof(TextMeshProUGUI)));
        moveCountGo.transform.SetParent(moveGo.transform, false);
        TextMeshProUGUI moveCountText = moveCountGo.GetComponent<TextMeshProUGUI>();
        SetPrivateField(movePanel, "moveCountText", moveCountText);

        GameObject maxTilesGo = Track(new GameObject("MaxTilesText", typeof(RectTransform), typeof(TextMeshProUGUI)));
        maxTilesGo.transform.SetParent(moveGo.transform, false);
        SetPrivateField(movePanel, "maxTilesText", maxTilesGo.GetComponent<TextMeshProUGUI>());

        GameObject endTurnGo = Track(new GameObject("EndTurnButton", typeof(RectTransform), typeof(Image), typeof(Button)));
        endTurnGo.transform.SetParent(battleUIGo.transform, false);
        Button endTurnButton = endTurnGo.GetComponent<Button>();

        SetPrivateField(battleUI, "battleHandPanel", handPanel);
        SetPrivateField(battleUI, "battleMovePanel", movePanel);
        SetPrivateField(battleUI, "endTurnButton", endTurnButton);

        battleUIGo.SetActive(true);
        InvokePrivateMethod(handPanel, "Awake");
        InvokePrivateMethod(movePanel, "Awake");
        InvokePrivateMethod(battleUI, "Awake");
        InvokePrivateMethod(battleUI, "OnEnable");
        InvokePrivateMethod(handPanel, "OnEnable");
        InvokePrivateMethod(movePanel, "OnEnable");

        SetPrivateField(controller, "_battleUI", battleUI);
        battleUI.BindPlayer(player, controller);

        return (battleUI, handPanel, movePanel, drawCountText, moveCountText);
    }

    private (RunManager runManager, BattleManager battleManager) CreateTestBattleManager(ICharacterController activeTurnController, BattleState state = BattleState.TurnInProgress)
    {
        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);

        BattleManager battleManager = new BattleManager();
        battleManager.Initialize();
        _createdBattleManagers.Add(battleManager);

        typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public).SetValue(runManager, battleManager);
        typeof(BattleManager).GetProperty("battleState", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, state);
        typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, activeTurnController);

        return (runManager, battleManager);
    }

    private PlayerInputController CreateMockInputController()
    {
        GameObject inputGo = Track(new GameObject("TestPlayerInputController"));
        PlayerInputController inputCtrl = inputGo.AddComponent<PlayerInputController>();
        SetStaticField<PlayerInputController>("_instance", inputCtrl);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        return inputCtrl;
    }

    private static void ClickTile(PlayerBattleController controller, Camera cam, Tile tile)
    {
        Vector2 screenPos = cam.WorldToScreenPoint(tile.transform.position);
        InvokePrivateMethod(controller, "HandleGlobalClick", screenPos);
    }

    private static void CompleteSimulatedMove(Character character, Tile destinationTile)
    {
        character.characterMove.StopMoving();
        character.characterMove.SetCurrentTile(destinationTile);
        character.transform.position = destinationTile.transform.position;
        character.currentState = CharacterState.Idle;
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
        if (field != null)
        {
            field.SetValue(null, value);
        }
    }

    private static void SetStaticAutoProperty<T>(string propName, object value)
    {
        PropertyInfo prop = typeof(T).GetProperty(propName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null)
        {
            prop.SetValue(null, value);
        }
    }

    [Test]
    public void MoveMode_WithRemainingMoveRange_KeepsMoveModeAndRecomputesReachableTiles()
    {
        GameMap gameMap = CreateTestGameMap(5, 5);
        Camera cam = CreateTestMainCamera(new Vector3(2f, 0f, 2f));
        Tile startTile = gameMap.GetTile(1, 1);
        Tile firstDest = gameMap.GetTile(2, 1);
        Tile secondDest = gameMap.GetTile(3, 1);

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, startTile, moveRange: 3);
        var (player, controller) = CreateTestPlayerController(playerChar);
        var (battleUI, handPanel, _, drawCountText, moveCountText) = CreateAndBindBattleUI(player, controller);
        CreateTestBattleManager(controller, BattleState.TurnInProgress);

        PlayerBattleMove playerMove = GetPrivateField<PlayerBattleMove>(controller, "playerMove");

        battleUI.SwitchToMoveMode();
        Assert.AreEqual(PlayerControlState.Move, controller.controlState);
        Assert.AreEqual(BattleUIMode.Move, battleUI.currentMode);
        Assert.IsTrue(playerMove.canMoveTiles.Contains(firstDest));
        Assert.IsTrue(playerMove.canMoveTiles.Contains(gameMap.GetTile(0, 0)), "초기 위치 (1,1)에서 거리 2인 (0,0)은 이동 가능해야 합니다.");
        Assert.IsFalse(playerMove.canMoveTiles.Contains(startTile));

        // 1칸 이동 (1,1) -> (2,1), 남은 이동력 2
        ClickTile(controller, cam, firstDest);
        int tokenAfterFirstMove = GetPrivateField<int>(controller, "_moveRequestToken");

        // 이동 중에는 기존 타일 하이라이트만 정리되고 이동 모드와 UI는 유지되어야 함
        Assert.AreEqual(2, playerChar.curMoveRange);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);
        Assert.AreEqual(PlayerControlState.Move, controller.controlState);
        Assert.AreEqual(BattleUIMode.Move, battleUI.currentMode);
        Assert.AreEqual("2", drawCountText.text);
        Assert.AreEqual("2", moveCountText.text);

        // (2,1) 도착 및 이동 완료 콜백 실행
        CompleteSimulatedMove(playerChar, firstDest);
        controller.HandleBattleMoveCompleted(tokenAfterFirstMove);

        Assert.AreEqual(PlayerControlState.Move, controller.controlState);
        Assert.AreEqual(BattleUIMode.Move, battleUI.currentMode);
        Assert.IsFalse(handPanel.contentTransform.gameObject.activeSelf);
        Assert.AreEqual(2, playerChar.curMoveRange);
        Assert.AreEqual("2", drawCountText.text);
        Assert.AreEqual("2", moveCountText.text);

        // 새 타일 (2,1) 기준으로 거리 2 이내 타일이 재계산되어야 함
        Assert.IsTrue(playerMove.canMoveTiles.Contains(startTile), "새 위치 (2,1)에서 (1,1)은 거리 1로 이동 가능해야 합니다.");
        Assert.IsTrue(playerMove.canMoveTiles.Contains(gameMap.GetTile(4, 1)), "새 위치 (2,1)에서 (4,1)은 거리 2로 이동 가능해야 합니다.");
        Assert.IsFalse(playerMove.canMoveTiles.Contains(firstDest), "현재 서 있는 (2,1)은 이동 가능 목록에서 제외되어야 합니다.");
        Assert.IsFalse(playerMove.canMoveTiles.Contains(gameMap.GetTile(0, 0)), "새 위치 (2,1)에서 (0,0)은 거리 3이므로 범위 2로는 도달 불가여야 합니다.");

        // 연속으로 두 번째 이동 (2,1) -> (3,1), 남은 이동력 1
        ClickTile(controller, cam, secondDest);
        int tokenAfterSecondMove = GetPrivateField<int>(controller, "_moveRequestToken");
        CompleteSimulatedMove(playerChar, secondDest);
        controller.HandleBattleMoveCompleted(tokenAfterSecondMove);

        Assert.AreEqual(PlayerControlState.Move, controller.controlState);
        Assert.AreEqual(BattleUIMode.Move, battleUI.currentMode);
        Assert.AreEqual(1, playerChar.curMoveRange);
        Assert.IsTrue(playerMove.canMoveTiles.Contains(gameMap.GetTile(4, 1)));
        Assert.IsFalse(playerMove.canMoveTiles.Contains(startTile));
    }

    [Test]
    public void MoveMode_WhenMoveExhaustsRange_AutoReturnsToNormalAndHandMode()
    {
        GameMap gameMap = CreateTestGameMap(5, 5);
        Camera cam = CreateTestMainCamera(new Vector3(2f, 0f, 2f));
        Tile startTile = gameMap.GetTile(1, 1);
        Tile destTile = gameMap.GetTile(4, 1);

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, startTile, moveRange: 3);
        var (player, controller) = CreateTestPlayerController(playerChar);
        var (battleUI, handPanel, _, drawCountText, _) = CreateAndBindBattleUI(player, controller);
        CreateTestBattleManager(controller, BattleState.TurnInProgress);

        PlayerBattleMove playerMove = GetPrivateField<PlayerBattleMove>(controller, "playerMove");

        battleUI.SwitchToMoveMode();
        Assert.IsTrue(playerMove.canMoveTiles.Contains(destTile));

        // 3칸 전부 소모하는 이동 시작
        ClickTile(controller, cam, destTile);
        int moveToken = GetPrivateField<int>(controller, "_moveRequestToken");
        Assert.AreEqual(0, playerChar.curMoveRange);
        Assert.AreEqual(PlayerControlState.Move, controller.controlState);
        Assert.AreEqual("0", drawCountText.text);

        // 도착 완료 후 잔여 이동력 0 -> 자동으로 Normal 및 Hand 모드 복귀
        CompleteSimulatedMove(playerChar, destTile);
        controller.HandleBattleMoveCompleted(moveToken);

        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(BattleUIMode.Hand, battleUI.currentMode);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);
        Assert.IsTrue(handPanel.contentTransform.gameObject.activeSelf);
    }

    [Test]
    public void MoveMode_WhenRemainingRangePositiveButNoReachableTiles_AutoReturnsToNormalAndHandMode()
    {
        GameMap gameMap = CreateTestGameMap(5, 5);
        Camera cam = CreateTestMainCamera(new Vector3(1f, 0f, 1f));
        Tile startTile = gameMap.GetTile(1, 0);
        Tile cornerDest = gameMap.GetTile(0, 0);
        gameMap.GetTile(0, 1).tileState = TileState.Full;

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, startTile, moveRange: 3);
        var (player, controller) = CreateTestPlayerController(playerChar);
        var (battleUI, handPanel, _, _, _) = CreateAndBindBattleUI(player, controller);
        CreateTestBattleManager(controller, BattleState.TurnInProgress);

        PlayerBattleMove playerMove = GetPrivateField<PlayerBattleMove>(controller, "playerMove");

        battleUI.SwitchToMoveMode();
        Assert.IsTrue(playerMove.canMoveTiles.Contains(cornerDest));

        // 모서리 (0,0)으로 1칸 이동 (잔여 이동력 2)
        ClickTile(controller, cam, cornerDest);
        int moveToken = GetPrivateField<int>(controller, "_moveRequestToken");
        Assert.AreEqual(2, playerChar.curMoveRange);

        // 도착 시점 직전 (1,0) 타일도 벽/유닛으로 막혀 (0,0)이 완전히 고립된 상황 구성
        CompleteSimulatedMove(playerChar, cornerDest);
        startTile.tileState = TileState.Full;

        controller.HandleBattleMoveCompleted(moveToken);

        Assert.AreEqual(2, playerChar.curMoveRange, "이동력은 남아 있어야 합니다.");
        Assert.AreEqual(0, playerMove.canMoveTiles.Count, "도달 가능한 타일이 0개여야 합니다.");
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(BattleUIMode.Hand, battleUI.currentMode);
        Assert.IsTrue(handPanel.contentTransform.gameObject.activeSelf);
    }

    [Test]
    public void MoveMode_WhenCanceledMidMove_IgnoresStaleCompletionCallback()
    {
        GameMap gameMap = CreateTestGameMap(5, 5);
        Camera cam = CreateTestMainCamera(new Vector3(2f, 0f, 2f));
        Tile startTile = gameMap.GetTile(1, 1);
        Tile destTile = gameMap.GetTile(2, 1);

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, startTile, moveRange: 3);
        var (player, controller) = CreateTestPlayerController(playerChar);
        var (battleUI, _, _, _, _) = CreateAndBindBattleUI(player, controller);
        CreateTestBattleManager(controller, BattleState.TurnInProgress);

        PlayerBattleMove playerMove = GetPrivateField<PlayerBattleMove>(controller, "playerMove");

        battleUI.SwitchToMoveMode();
        ClickTile(controller, cam, destTile);
        int staleToken = GetPrivateField<int>(controller, "_moveRequestToken");

        // 이동 도중 취소
        controller.CancelCurrentState();
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(BattleUIMode.Hand, battleUI.currentMode);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);

        // 지연된 이전 이동 완료 콜백 도착 시 무시되어야 함
        CompleteSimulatedMove(playerChar, destTile);
        controller.HandleBattleMoveCompleted(staleToken);

        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(BattleUIMode.Hand, battleUI.currentMode);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);

        // 이동 모드를 다시 켠 상태에서도 이전 토큰(staleToken)은 거부되어야 함
        controller.EnablePlayerMove();
        playerMove.ClearCanMoveTiles();
        controller.HandleBattleMoveCompleted(staleToken);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count, "재진입 후에도 이전 이동의 stale 콜백은 무시되어야 합니다.");
    }

    [Test]
    public void MoveMode_WhenTurnOrBattleInterruptedMidMove_IgnoresStaleCompletionCallback()
    {
        CreateMockInputController();
        GameMap gameMap = CreateTestGameMap(5, 5);
        Camera cam = CreateTestMainCamera(new Vector3(2f, 0f, 2f));
        Tile startTile = gameMap.GetTile(1, 1);
        Tile destTile = gameMap.GetTile(2, 1);

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, startTile, moveRange: 3);
        var (player, controller) = CreateTestPlayerController(playerChar);
        var (battleUI, _, _, _, _) = CreateAndBindBattleUI(player, controller);
        var (_, battleManager) = CreateTestBattleManager(controller, BattleState.TurnInProgress);

        PlayerBattleMove playerMove = GetPrivateField<PlayerBattleMove>(controller, "playerMove");

        // 1. OnTurnEnd() 중단 시 stale 콜백 무시
        controller.OnTurnStart();
        battleUI.SwitchToMoveMode();
        ClickTile(controller, cam, destTile);
        int turnEndStaleToken = GetPrivateField<int>(controller, "_moveRequestToken");

        controller.OnTurnEnd();
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(BattleUIMode.Hand, battleUI.currentMode);

        CompleteSimulatedMove(playerChar, destTile);
        controller.HandleBattleMoveCompleted(turnEndStaleToken);
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(BattleUIMode.Hand, battleUI.currentMode);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);

        // 2. RequestEndTurn() 중단 시 stale 콜백 무시 (Idle 상태에서 턴 종료 요청)
        playerChar.curMoveRange = 3;
        controller.OnTurnStart();
        battleUI.SwitchToMoveMode();
        int requestEndTurnStaleToken = GetPrivateField<int>(controller, "_moveRequestToken");
        controller.RequestEndTurn();
        controller.HandleBattleMoveCompleted(requestEndTurnStaleToken);
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);

        // 3. OnBattleEnd() 중단 시 stale 콜백 무시
        playerChar.curMoveRange = 3;
        typeof(BattleManager).GetProperty("battleState", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, BattleState.TurnInProgress);
        typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, controller);
        controller.OnTurnStart();
        battleUI.SwitchToMoveMode();
        ClickTile(controller, cam, startTile);
        int battleEndStaleToken = GetPrivateField<int>(controller, "_moveRequestToken");

        controller.OnBattleEnd();
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        CompleteSimulatedMove(playerChar, startTile);
        controller.HandleBattleMoveCompleted(battleEndStaleToken);
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);

        // 4. 토큰이 유효해도 턴 소유자가 타 컨트롤러로 바뀐 경우 이동 모드를 자동 종료해야 함
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, gameMap.GetTile(4, 4), moveRange: 2);
        StubTurnController enemyTurnController = new StubTurnController(enemyChar);
        Character isolatedChar = CreateTestCharacter("TurnCheckPlayer", CharacterFaction.Player, gameMap.GetTile(0, 4), moveRange: 3);
        var (isolatedPlayer, isolatedCtrl) = CreateTestPlayerController(isolatedChar);
        var (isolatedUI, _, _, _, _) = CreateAndBindBattleUI(isolatedPlayer, isolatedCtrl);

        typeof(BattleManager).GetProperty("battleState", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, BattleState.TurnInProgress);
        typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, isolatedCtrl);
        isolatedUI.SwitchToMoveMode();
        int turnCheckToken = GetPrivateField<int>(isolatedCtrl, "_moveRequestToken");

        typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, enemyTurnController);
        isolatedCtrl.HandleBattleMoveCompleted(turnCheckToken);
        Assert.AreEqual(PlayerControlState.Normal, isolatedCtrl.controlState);
        Assert.AreEqual(BattleUIMode.Hand, isolatedUI.currentMode);
    }

    [Test]
    public void MoveMode_WhenPlayerDiesMidMove_NeverRestoresMoveMode()
    {
        CreateMockInputController();
        GameMap gameMap = CreateTestGameMap(5, 5);
        Camera cam = CreateTestMainCamera(new Vector3(2f, 0f, 2f));
        Tile startTile = gameMap.GetTile(1, 1);
        Tile destTile = gameMap.GetTile(2, 1);
        Tile enemyTile = gameMap.GetTile(4, 4);

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, startTile, moveRange: 3);
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, enemyTile, moveRange: 2);
        var (player, controller) = CreateTestPlayerController(playerChar);
        var (battleUI, _, _, _, _) = CreateAndBindBattleUI(player, controller);

        GameObject runGo = Track(new GameObject("DeathTestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);
        BattleManager battleManager = new BattleManager();
        battleManager.Initialize();
        _createdBattleManagers.Add(battleManager);
        typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public).SetValue(runManager, battleManager);

        StubTurnController enemyController = new StubTurnController(enemyChar);
        battleManager.InitBattle(
            new List<ICharacterController> { controller },
            new List<ICharacterController> { enemyController });
        battleManager.RequestTurnStart(playerChar);

        PlayerBattleMove playerMove = GetPrivateField<PlayerBattleMove>(controller, "playerMove");

        battleUI.SwitchToMoveMode();
        ClickTile(controller, cam, destTile);
        int staleToken = GetPrivateField<int>(controller, "_moveRequestToken");

        // 1. 전투 중 피격 사망 -> BattleManager가 턴/전투 종료 및 토큰 무효화 -> 지연 콜백 무시 검증
        playerChar.TakeDamage(enemyChar, 999f);
        Assert.IsTrue(playerChar.isDead);
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);

        controller.HandleBattleMoveCompleted(staleToken);
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);

        // 2. 외부 전투 이벤트 없이 isDead만 true인 상태에서 HandleBattleMoveCompleted가 호출되어도 Move 모드를 종료해야 함
        Character isolatedChar = CreateTestCharacter("IsolatedDeadPlayer", CharacterFaction.Player, gameMap.GetTile(0, 4), moveRange: 3);
        var (isolatedPlayer, isolatedCtrl) = CreateTestPlayerController(isolatedChar);
        var (isolatedUI, _, _, _, _) = CreateAndBindBattleUI(isolatedPlayer, isolatedCtrl);
        typeof(BattleManager).GetProperty("battleState", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, BattleState.TurnInProgress);
        typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, isolatedCtrl);
        isolatedUI.SetTurnActive(true);
        isolatedUI.SwitchToMoveMode();
        int currentToken = GetPrivateField<int>(isolatedCtrl, "_moveRequestToken");
        isolatedChar.isDead = true;

        isolatedCtrl.HandleBattleMoveCompleted(currentToken);
        Assert.AreEqual(PlayerControlState.Normal, isolatedCtrl.controlState);
        Assert.AreEqual(BattleUIMode.Hand, isolatedUI.currentMode);
    }

    [Test]
    public void ExecuteMoveToTile_ForwardsOnMoveCompleted_AndHandlesSynchronousCompletionSafely()
    {
        GameMap gameMap = CreateTestGameMap(5, 5);
        Camera cam = CreateTestMainCamera(new Vector3(2f, 0f, 2f));
        Tile startTile = gameMap.GetTile(1, 1);
        Tile destTile = gameMap.GetTile(2, 1);

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, startTile, moveRange: 3);
        var (player, controller) = CreateTestPlayerController(playerChar);
        var (battleUI, _, _, _, _) = CreateAndBindBattleUI(player, controller);
        CreateTestBattleManager(controller, BattleState.TurnInProgress);

        PlayerBattleMove playerMove = GetPrivateField<PlayerBattleMove>(controller, "playerMove");
        SyncMoveCanceler syncCanceler = new SyncMoveCanceler { ShouldCancel = true };
        playerChar.eventBus.Add<IOnBeforeMove>(syncCanceler);

        try
        {
            // 1. PlayerBattleMove.ExecuteMoveToTile이 onMoveCompleted를 CharacterMove.MoveAlongPath에 직접 전달하는지 검증
            playerMove.CheckCanMoveTiles();
            bool directCallbackInvoked = false;
            bool startedDirect = playerMove.ExecuteMoveToTile(destTile, () => directCallbackInvoked = true);

            Assert.IsTrue(startedDirect);
            Assert.AreEqual(1, syncCanceler.CallCount);
            Assert.IsTrue(directCallbackInvoked, "ExecuteMoveToTile의 onMoveCompleted가 동기 완료 시에도 전달되어야 합니다.");

            // 2. HandleGlobalClick 경로에서 동기 완료 발생 시 pendingSyncCompletion이 ClearCanMoveTiles 이후에 처리되어야 함
            playerChar.curMoveRange = 3;
            playerChar.currentState = CharacterState.Idle;
            battleUI.SwitchToMoveMode();

            ClickTile(controller, cam, destTile);

            // curMoveRange가 2로 남았고 동기 완료 후 CheckCanMoveTiles가 ClearCanMoveTiles 이후 실행되므로 타일이 살아 있어야 함
            Assert.AreEqual(2, playerChar.curMoveRange);
            Assert.AreEqual(PlayerControlState.Move, controller.controlState);
            Assert.AreEqual(BattleUIMode.Move, battleUI.currentMode);
            Assert.IsTrue(playerMove.canMoveTiles.Count > 0, "동기 완료 콜백이 ClearCanMoveTiles() 이후 실행되어 새 이동 가능 타일이 유지되어야 합니다.");

            // 3. 남은 이동력 0이 되는 동기 완료 시에는 자동으로 Normal / Hand 복귀해야 함
            playerChar.curMoveRange = 1;
            ClickTile(controller, cam, destTile);

            Assert.AreEqual(0, playerChar.curMoveRange);
            Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
            Assert.AreEqual(BattleUIMode.Hand, battleUI.currentMode);
            Assert.AreEqual(0, playerMove.canMoveTiles.Count);
        }
        finally
        {
            playerChar.eventBus.Remove<IOnBeforeMove>(syncCanceler);
        }
    }

    [Test]
    public void GetReachableTiles_BlockedByLivingEnemy_ExcludesDetourExceedingRangeAndIncludesWithinRange()
    {
        GameMap gameMap = CreateTestGameMap(5, 5);
        Tile startTile = gameMap.GetTile(0, 2);
        Tile enemyTile = gameMap.GetTile(1, 2);

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, startTile, moveRange: 3);
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, enemyTile, moveRange: 2);

        List<Tile> reachableTiles = new List<Tile>(32);
        gameMap.GetReachableTiles(startTile, 3, playerChar.characterMove, reachableTiles);

        Assert.IsFalse(reachableTiles.Contains(enemyTile), "살아있는 적이 점유한 타일은 이동 가능 범위에 포함되면 안 됩니다.");
        Assert.IsFalse(reachableTiles.Contains(gameMap.GetTile(2, 2)), "우회 거리 4인 (2,2)는 이동력 3으로 도달 불가여야 합니다.");
        Assert.IsFalse(reachableTiles.Contains(gameMap.GetTile(3, 2)), "우회 거리 5인 (3,2)는 이동력 3으로 도달 불가여야 합니다.");
        Assert.IsTrue(reachableTiles.Contains(gameMap.GetTile(2, 3)), "우회 거리 3인 (2,3)은 이동 가능해야 합니다.");
        Assert.IsTrue(reachableTiles.Contains(gameMap.GetTile(2, 1)), "우회 거리 3인 (2,1)은 이동 가능해야 합니다.");

        enemyChar.isDead = true;
        gameMap.GetReachableTiles(startTile, 3, playerChar.characterMove, reachableTiles);

        Assert.IsTrue(reachableTiles.Contains(gameMap.GetTile(2, 2)), "사망한 적 타일은 통과 가능하므로 (2,2)에 도달 가능해야 합니다.");
        Assert.IsTrue(reachableTiles.Contains(gameMap.GetTile(3, 2)), "사망한 적 타일은 통과 가능하므로 (3,2)에 도달 가능해야 합니다.");
    }

    [Test]
    public void ExecuteMoveToTile_WhenDetourExceedsMoveRangeOrTargetBlocked_RejectsMoveAndPreservesState()
    {
        GameMap gameMap = CreateTestGameMap(5, 5);
        Tile startTile = gameMap.GetTile(0, 2);
        Tile blockerTile = gameMap.GetTile(1, 2);
        Tile detourTargetTile = gameMap.GetTile(3, 2);
        Tile occupiedTargetTile = gameMap.GetTile(0, 3);

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, startTile, moveRange: 3);
        PlayerBattleMove playerMove = new PlayerBattleMove(playerChar.characterMove);

        // 장애물/적이 없는 상태에서 이동 가능 타일 목록을 먼저 캐시 (detourTargetTile, occupiedTargetTile 포함)
        playerMove.CheckCanMoveTiles();
        Assert.IsTrue(playerMove.canMoveTiles.Contains(detourTargetTile));
        Assert.IsTrue(playerMove.canMoveTiles.Contains(occupiedTargetTile));

        // 직선 경로를 막는 적 배치 -> 실제 우회 경로 길이는 5로 curMoveRange(3) 초과
        CreateTestCharacter("BlockerEnemy", CharacterFaction.Enemy, blockerTile, moveRange: 2);
        bool detourStarted = playerMove.ExecuteMoveToTile(detourTargetTile);

        Assert.IsFalse(detourStarted, "실제 우회 경로 길이가 curMoveRange를 초과하면 이동이 거부되어야 합니다.");
        Assert.AreEqual(3, playerChar.curMoveRange);
        Assert.AreEqual(CharacterState.Idle, playerChar.currentState);
        Assert.AreSame(startTile, playerChar.characterMove.GetCurrentTile());

        // 캐시된 목표 타일에 다른 생존 유닛이 들어온 경우에도 진입 거부 검증
        CreateTestCharacter("TargetOccupantEnemy", CharacterFaction.Enemy, occupiedTargetTile, moveRange: 2);
        bool occupiedStarted = playerMove.ExecuteMoveToTile(occupiedTargetTile);

        Assert.IsFalse(occupiedStarted, "목표 타일이 생존 유닛에 의해 점유된 경우 이동이 거부되어야 합니다.");
        Assert.AreEqual(3, playerChar.curMoveRange);
        Assert.AreEqual(CharacterState.Idle, playerChar.currentState);
        Assert.AreSame(startTile, playerChar.characterMove.GetCurrentTile());
    }
}
