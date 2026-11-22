using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

[TestFixture]
public class BattleTurnInputGuardTests
{
    private sealed class StubBattleAction : BattleAction
    {
        public StubBattleAction(Character caster) : base(caster) { }

        public override IEnumerator ExecuteRoutine()
        {
            while (true)
            {
                yield return null;
            }
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
            queueManager.ClearQueue(invokeCanceled: false);
            UnityEngine.Object.DestroyImmediate(queueManager.gameObject);
        }
        SetStaticField<ActionQueueManager>("_instance", null);
        SetStaticField<ActionQueueManager>("OnGlobalBusyStateChanged", null);

        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        SetStaticAutoProperty<BattleTimelinePanel>("Instance", null);
        SetStaticAutoProperty<BattleSideUnitHUDPanel>("Instance", null);
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
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private PlayerInputController CreateMockInputController()
    {
        GameObject inputGo = Track(new GameObject("TestPlayerInputController"));
        PlayerInputController inputCtrl = inputGo.AddComponent<PlayerInputController>();
        SetStaticField<PlayerInputController>("_instance", inputCtrl);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        return inputCtrl;
    }

    private GameMap CreateTestGameMap(int width = 5, int height = 5)
    {
        GameObject mapGo = Track(new GameObject("TestGameMap"));
        GameMap gameMap = mapGo.AddComponent<GameMap>();
        InvokePrivateMethod(gameMap, "Awake");

        GameObject tilePrefab = Track(new GameObject("TilePrefab", typeof(Tile)));
        gameMap.prefabTile = tilePrefab;

        GameObject indicatorPrefab = Track(new GameObject("MoveRangeIndicatorPrefab", typeof(MapMoveRangeIndicator)));
        gameMap.moveRangeIndicatorPrefab = indicatorPrefab;

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
        character.curStamina = 0f;
        character.currentState = CharacterState.Idle;
        if (startTile != null)
        {
            character.characterMove.SetCurrentTile(startTile);
            charGo.transform.position = startTile.transform.position;
        }
        _createdCharacters.Add(character);
        return character;
    }

    private (RunManager runManager, BattleManager battleManager, Player player, PlayerBattleController controller, BattleUIPanel battleUI, Button switchToMoveBtn, Button switchToCardBtn)
        CreateBattleContext(GameMap gameMap, Character playerChar, Character enemyChar, bool holdIntro = false)
    {
        CreateMockInputController();

        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);

        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);
        runManager.playerBattleController = controller;

        BattleManager battleManager = new BattleManager();
        battleManager.Initialize();
        if (holdIntro)
        {
            battleManager.OnBattleIntroRequested += _ => { };
        }
        _createdBattleManagers.Add(battleManager);
        typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public).SetValue(runManager, battleManager);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleUIPanel.prefab");
        Assert.IsNotNull(prefab, "BattleUIPanel.prefab을 찾을 수 없습니다.");

        GameObject canvasGo = Track(new GameObject("TurnGuardCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject panelGo = Track(UnityEngine.Object.Instantiate(prefab, canvasGo.transform, false));
        BattleUIPanel battleUI = panelGo.GetComponent<BattleUIPanel>();
        InvokePrivateMethod(battleUI.HandPanel, "Awake");
        InvokePrivateMethod(battleUI.MovePanel, "Awake");
        InvokePrivateMethod(battleUI, "Awake");
        InvokePrivateMethod(battleUI, "OnEnable");
        InvokePrivateMethod(battleUI.HandPanel, "OnEnable");
        InvokePrivateMethod(battleUI.MovePanel, "OnEnable");

        battleManager.InitBattle(
            new List<ICharacterController> { controller },
            new List<ICharacterController> { new StubEnemyController(enemyChar) });

        // OnBattleStart()의 실제 이벤트 구독(CharacterStateChanged, ActionQueueBusy) 연결 후 UI 바인딩
        controller.OnBattleStart();
        SetPrivateField(controller, "_battleUI", battleUI);
        battleUI.BindPlayer(player, controller);
        battleUI.SwitchToHandMode();
        battleUI.SetTurnActive(false);

        Button switchToMoveBtn = GetPrivateField<Button>(battleUI.HandPanel, "switchToMoveModeButton");
        Button switchToCardBtn = GetPrivateField<Button>(battleUI.MovePanel, "switchToCardModeButton");

        return (runManager, battleManager, player, controller, battleUI, switchToMoveBtn, switchToCardBtn);
    }

    private static int GetStaticEventSubscriberCount<T>(string eventFieldName)
    {
        FieldInfo field = typeof(T).GetField(eventFieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"정적 이벤트 백킹 필드 '{eventFieldName}'를 {typeof(T).Name}에서 찾을 수 없습니다.");
        Delegate del = field.GetValue(null) as Delegate;
        return del != null ? del.GetInvocationList().Length : 0;
    }

    private static int GetInstanceEventSubscriberCount(object target, string eventFieldName)
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
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"필드 '{fieldName}'을(를) {target.GetType().Name}에서 찾을 수 없습니다.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
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
    public void OutOfPlayerTurn_DisablesButtonsAndBlocksMoveModeAndEndTurn()
    {
        GameMap gameMap = CreateTestGameMap(5, 5);
        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, gameMap.GetTile(1, 1));
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, gameMap.GetTile(3, 3));

        var (_, battleManager, _, controller, battleUI, switchToMoveBtn, _) = CreateBattleContext(gameMap, playerChar, enemyChar, holdIntro: true);
        PlayerBattleMove playerMove = GetPrivateField<PlayerBattleMove>(controller, "playerMove");

        // 1. Intro 상태 (턴 시작 전): 버튼 비활성화 및 도메인 진입 차단
        Assert.AreEqual(BattleState.Intro, battleManager.battleState);
        Assert.IsFalse(controller.IsPlayerTurnActive());
        Assert.IsFalse(controller.CanPerformPlayerTurnAction());
        Assert.IsFalse(battleUI.EndTurnButton.interactable);
        Assert.IsFalse(switchToMoveBtn.interactable);

        LogAssert.Expect(LogType.Warning, new Regex("이동 모드로 전환할 수 없습니다"));
        Assert.IsFalse(controller.EnablePlayerMove());
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(BattleUIMode.Hand, battleUI.currentMode);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);

        battleUI.SwitchToMoveMode();
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(BattleUIMode.Hand, battleUI.currentMode);

        // 2. Combat 상태 (스태미나 충전 중, 턴 미획득): 여전히 비활성 및 차단
        battleManager.StartCombat();
        Assert.AreEqual(BattleState.Combat, battleManager.battleState);
        Assert.IsNull(battleManager.currentTurnController);
        Assert.IsFalse(battleUI.EndTurnButton.interactable);
        Assert.IsFalse(switchToMoveBtn.interactable);

        LogAssert.Expect(LogType.Warning, new Regex("이동 모드로 전환할 수 없습니다"));
        Assert.IsFalse(controller.EnablePlayerMove());

        LogAssert.Expect(LogType.Warning, new Regex("턴을 종료할 수 있는 상태가 아닙니다"));
        controller.RequestEndTurn();
        Assert.AreEqual(BattleState.Combat, battleManager.battleState);

        // 3. 적 턴 진행 중: 플레이어 이동모드 전환 및 적 턴 조기 종료 모두 차단
        battleManager.RequestTurnStart(enemyChar);
        Assert.AreEqual(BattleState.TurnInProgress, battleManager.battleState);
        Assert.AreNotSame(controller, battleManager.currentTurnController);
        Assert.IsFalse(battleUI.EndTurnButton.interactable);
        Assert.IsFalse(switchToMoveBtn.interactable);

        LogAssert.Expect(LogType.Warning, new Regex("이동 모드로 전환할 수 없습니다"));
        Assert.IsFalse(controller.EnablePlayerMove());
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);

        LogAssert.Expect(LogType.Warning, new Regex("턴을 종료할 수 있는 상태가 아닙니다"));
        controller.RequestEndTurn();
        Assert.AreEqual(BattleState.TurnInProgress, battleManager.battleState);
        Assert.AreSame(enemyChar, battleManager.currentTurnController.controlledCharacter);
    }

    [Test]
    public void PlayerTurn_SynchronizesButtonsWithBusyState_AndRestoresWhenIdle()
    {
        GameMap gameMap = CreateTestGameMap(5, 5);
        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, gameMap.GetTile(1, 1));
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, gameMap.GetTile(3, 3));

        var (_, battleManager, _, controller, battleUI, switchToMoveBtn, switchToCardBtn) = CreateBattleContext(gameMap, playerChar, enemyChar);

        battleManager.StartCombat();
        battleManager.RequestTurnStart(playerChar);

        Assert.AreEqual(BattleState.TurnInProgress, battleManager.battleState);
        Assert.AreSame(controller, battleManager.currentTurnController);
        Assert.IsTrue(controller.IsPlayerTurnActive());
        Assert.IsTrue(controller.CanPerformPlayerTurnAction());
        Assert.IsTrue(battleUI.EndTurnButton.interactable);
        Assert.IsTrue(switchToMoveBtn.interactable);

        // 1. 액션 큐 실행 중 즉시 비활성화 및 차단 -> 큐 완료(정리) 시 복구
        ActionQueueManager.Instance.EnqueueAction(new StubBattleAction(playerChar));
        Assert.IsTrue(ActionQueueManager.Instance.IsBusy);
        Assert.IsFalse(controller.CanPerformPlayerTurnAction());
        Assert.IsFalse(battleUI.EndTurnButton.interactable);
        Assert.IsFalse(switchToMoveBtn.interactable);

        LogAssert.Expect(LogType.Warning, new Regex("이동 모드로 전환할 수 없습니다"));
        Assert.IsFalse(controller.EnablePlayerMove());

        LogAssert.Expect(LogType.Warning, new Regex("턴을 종료할 수 있는 상태가 아닙니다"));
        controller.RequestEndTurn();
        Assert.AreEqual(BattleState.TurnInProgress, battleManager.battleState);

        ActionQueueManager.Instance.ClearQueue(invokeCanceled: false);
        Assert.IsTrue(controller.CanPerformPlayerTurnAction());
        Assert.IsTrue(battleUI.EndTurnButton.interactable);
        Assert.IsTrue(switchToMoveBtn.interactable);

        // 2. 캐릭터 이동/스킬 상태 중 즉시 비활성화 및 차단 -> Idle 복귀 시 재활성화
        battleUI.SwitchToMoveMode();
        Assert.AreEqual(PlayerControlState.Move, controller.controlState);
        Assert.IsTrue(switchToCardBtn.interactable);

        playerChar.currentState = CharacterState.Move;
        Assert.IsFalse(battleUI.EndTurnButton.interactable);
        Assert.IsFalse(switchToMoveBtn.interactable);
        Assert.IsFalse(switchToCardBtn.interactable);

        LogAssert.Expect(LogType.Warning, new Regex("턴을 종료할 수 있는 상태가 아닙니다"));
        controller.RequestEndTurn();
        Assert.AreEqual(BattleState.TurnInProgress, battleManager.battleState);

        playerChar.currentState = CharacterState.Idle;
        Assert.IsTrue(battleUI.EndTurnButton.interactable);
        Assert.IsTrue(switchToMoveBtn.interactable);
        Assert.IsTrue(switchToCardBtn.interactable);
    }

    [Test]
    public void RequestEndTurn_WhileInMoveMode_ClearsMoveStateIndicatorAndDisablesButtons()
    {
        GameMap gameMap = CreateTestGameMap(5, 5);
        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, gameMap.GetTile(1, 1), moveRange: 3);
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, gameMap.GetTile(4, 4));

        var (_, battleManager, _, controller, battleUI, switchToMoveBtn, _) = CreateBattleContext(gameMap, playerChar, enemyChar, holdIntro: true);
        PlayerBattleMove playerMove = GetPrivateField<PlayerBattleMove>(controller, "playerMove");
        MapMoveRangeIndicator indicator = gameMap.GetMoveRangeIndicator();
        Assert.IsNotNull(indicator);

        battleManager.StartCombat();
        battleManager.RequestTurnStart(playerChar);

        // 이동모드 진입 확인
        Assert.IsTrue(controller.EnablePlayerMove());
        Assert.AreEqual(PlayerControlState.Move, controller.controlState);
        Assert.AreEqual(BattleUIMode.Move, battleUI.currentMode);
        Assert.Greater(playerMove.canMoveTiles.Count, 0);
        Assert.IsTrue(indicator.gameObject.activeSelf);

        // 턴 종료 버튼 클릭 -> 이동모드·이동범위 인디케이터 자동 정리 및 턴 종료 후 버튼 비활성화
        battleUI.EndTurnButton.onClick.Invoke();

        Assert.AreEqual(BattleState.Combat, battleManager.battleState);
        Assert.IsNull(battleManager.currentTurnController);
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(BattleUIMode.Hand, battleUI.currentMode);
        Assert.AreEqual(0, playerMove.canMoveTiles.Count);
        Assert.IsFalse(indicator.gameObject.activeSelf);
        Assert.IsFalse(battleUI.EndTurnButton.interactable);
        Assert.IsFalse(switchToMoveBtn.interactable);

        // OnBattleEnd() 시 상태 이벤트 구독이 모두 깔끔하게 해제되는지 검증
        Assert.AreEqual(1, GetStaticEventSubscriberCount<ActionQueueManager>("OnGlobalBusyStateChanged"));
        Assert.AreEqual(1, GetInstanceEventSubscriberCount(playerChar, "OnCharacterStateChanged"));
        controller.OnBattleEnd();
        Assert.AreEqual(0, GetStaticEventSubscriberCount<ActionQueueManager>("OnGlobalBusyStateChanged"));
        Assert.AreEqual(0, GetInstanceEventSubscriberCount(playerChar, "OnCharacterStateChanged"));
    }
}
