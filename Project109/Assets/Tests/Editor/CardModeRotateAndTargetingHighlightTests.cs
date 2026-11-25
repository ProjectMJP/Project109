using System;
using System.Collections.Generic;
using System.Reflection;
using CardTypes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[TestFixture]
public class CardModeRotateAndTargetingHighlightTests
{
    private sealed class StubCharacterController : ICharacterController
    {
        public Character controlledCharacter { get; }

        public StubCharacterController(Character character)
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
    private readonly List<Camera> _disabledSceneMainCameras = new List<Camera>();

    [SetUp]
    public void SetUp()
    {
        TooltipManager.SetTooltipEnabled(true);
        BattleManager.SetGlobalBattleSpeedIndex(0, savePrefs: false);
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

        CharacterUIController.ClearAllTargetingHighlights();

        ActionQueueManager queueManager = UnityEngine.Object.FindFirstObjectByType<ActionQueueManager>();
        if (queueManager != null)
        {
            queueManager.ClearQueue();
            UnityEngine.Object.DestroyImmediate(queueManager.gameObject);
        }
        SetStaticField<ActionQueueManager>("_instance", null);

        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        SetStaticField<TooltipManager>("instance", null);
        TooltipManager.SetTooltipEnabled(true);

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

    private Camera CreateSlantedTestMainCamera(Vector3 position, Vector3 lookAtTarget)
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
        camGo.transform.position = position;
        camGo.transform.LookAt(lookAtTarget, Vector3.up);

        Camera cam = camGo.GetComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 45f;
        return cam;
    }

    private Character CreateTestCharacter(string name, CharacterFaction faction, Tile startTile = null)
    {
        GameObject charGo = Track(new GameObject(name));
        Character character = charGo.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = faction;
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 25f,
            moveRange = 3
        });
        character.currentState = CharacterState.Idle;
        character.curStamina = 80f;

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

    private (RunManager runManager, BattleManager battleManager) CreateTestRunAndBattleManager(
        PlayerBattleController playerController,
        IReadOnlyList<ICharacterController> additionalEnemies = null)
    {
        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);
        runManager.playerBattleController = playerController;

        BattleManager battleManager = new BattleManager();
        battleManager.Initialize();
        _createdBattleManagers.Add(battleManager);

        var players = new List<ICharacterController> { playerController };
        var enemies = new List<ICharacterController>();
        if (additionalEnemies != null)
        {
            for (int i = 0; i < additionalEnemies.Count; i++)
            {
                enemies.Add(additionalEnemies[i]);
            }
        }

        battleManager.InitBattle(players, enemies);
        battleManager.StartCombat();

        typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public).SetValue(runManager, battleManager);
        typeof(BattleManager).GetProperty("battleState", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, BattleState.TurnInProgress);
        typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, playerController);

        return (runManager, battleManager);
    }

    private Card CreateTestCard(
        string name,
        string targetType,
        int minDistance = 1,
        int maxDistance = 3,
        int targetCount = 1,
        bool allowDuplicateTarget = true,
        List<EffectArea> additionalAreas = null)
    {
        CardData data = new CardData
        {
            cardName = name,
            displayName = name,
            stamina = 1,
            targetType = targetType,
            targetMinDistance = minDistance,
            targetMaxDistance = maxDistance,
            targetCount = targetCount,
            allowDuplicateTarget = allowDuplicateTarget,
            additionalEffectAreaList = additionalAreas ?? new List<EffectArea>()
        };
        Card card = new Card(data, null, null, -(_createdCards.Count + 1));
        _createdCards.Add(card);
        return card;
    }

    private Canvas CreateTestCanvas(string name)
    {
        GameObject canvasGo = Track(new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1920f, 1080f);
        return canvas;
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

    private EventSystem CreateTestEventSystem(string name = "TestEventSystem")
    {
        GameObject esGo = Track(new GameObject(name, typeof(EventSystem)));
        return esGo.GetComponent<EventSystem>();
    }

    private static void BindCharacterWorldViews(
        CharacterUIController ctrl,
        CharacterStatusBarUI statusBar,
        CharacterEffectListUI effectList,
        CharacterIntentUI intentUI)
    {
        SetPrivateField(ctrl, "statusBarUI", statusBar);
        SetPrivateField(ctrl, "effectListUI", effectList);
        SetPrivateField(ctrl, "intentUI", intentUI);
        SetPrivateField(ctrl, "_isBattleActive", true);
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
        Assert.IsNotNull(field, $"정적 필드 '{fieldName}'을(를) {typeof(T).Name}에서 찾을 수 없습니다.");
        field.SetValue(null, value);
    }

    private static void SetStaticAutoProperty<T>(string propertyName, object value)
    {
        FieldInfo backingField = typeof(T).GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(backingField, $"정적 프로퍼티 '{propertyName}'의 백킹 필드를 {typeof(T).Name}에서 찾을 수 없습니다.");
        backingField.SetValue(null, value);
    }

    #region (a) Normal Mode Rotation & 3D Collider Priority

    [Test]
    public void TryRotatePlayerTowardCharacter_RotatesInAllSixDirectionsWithoutCost_AndEnforcesGuards()
    {
        GameMap map = CreateTestGameMap(5, 5);
        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, map.GetTile(2, 2));
        Character enemyEast = CreateTestCharacter("EnemyEast", CharacterFaction.Enemy, map.GetTile(4, 2));
        Character enemyNE = CreateTestCharacter("EnemyNE", CharacterFaction.Enemy, map.GetTile(3, 4));
        Character enemyNW = CreateTestCharacter("EnemyNW", CharacterFaction.Enemy, map.GetTile(1, 4));
        Character enemyWest = CreateTestCharacter("EnemyWest", CharacterFaction.Enemy, map.GetTile(0, 2));
        Character enemySW = CreateTestCharacter("EnemySW", CharacterFaction.Enemy, map.GetTile(1, 0));
        Character enemySE = CreateTestCharacter("EnemySE", CharacterFaction.Enemy, map.GetTile(3, 0));
        Character deadEnemy = CreateTestCharacter("DeadEnemy", CharacterFaction.Enemy, map.GetTile(4, 3));
        deadEnemy.isDead = true;

        var (_, controller) = CreateTestPlayerController(playerChar);
        var (_, battleManager) = CreateTestRunAndBattleManager(
            controller,
            new ICharacterController[] { new StubCharacterController(enemyEast), new StubCharacterController(enemyWest) });

        playerChar.curStamina = 75f;
        playerChar.curMoveRange = 3;

        Assert.IsTrue(controller.TryRotatePlayerTowardCharacter(enemyEast));
        Assert.AreEqual(LookDirection.East, playerChar.characterMove.facingDirection);

        Assert.IsTrue(controller.TryRotatePlayerTowardCharacter(enemyNE));
        Assert.AreEqual(LookDirection.NorthEast, playerChar.characterMove.facingDirection);

        Assert.IsTrue(controller.TryRotatePlayerTowardCharacter(enemyNW));
        Assert.AreEqual(LookDirection.NorthWest, playerChar.characterMove.facingDirection);

        Assert.IsTrue(controller.TryRotatePlayerTowardCharacter(enemyWest));
        Assert.AreEqual(LookDirection.West, playerChar.characterMove.facingDirection);

        Assert.IsTrue(controller.TryRotatePlayerTowardCharacter(enemySW));
        Assert.AreEqual(LookDirection.SouthWest, playerChar.characterMove.facingDirection);

        Assert.IsTrue(controller.TryRotatePlayerTowardCharacter(enemySE));
        Assert.AreEqual(LookDirection.SouthEast, playerChar.characterMove.facingDirection);

        Assert.AreEqual(75f, playerChar.curStamina, 0.001f, "회전은 스태미나를 소모하지 않아야 합니다.");
        Assert.AreEqual(3, playerChar.curMoveRange, "회전은 이동력을 소모하지 않아야 합니다.");

        // 자기 자신·사망·null 대상 거부
        Assert.IsFalse(controller.TryRotatePlayerTowardCharacter(playerChar));
        Assert.IsFalse(controller.TryRotatePlayerTowardCharacter(deadEnemy));
        Assert.IsFalse(controller.TryRotatePlayerTowardCharacter(null));

        // Move / TargetSelection 상태 거부
        SetPrivateField(controller, "<controlState>k__BackingField", PlayerControlState.Move);
        Assert.IsFalse(controller.TryRotatePlayerTowardCharacter(enemyEast));

        SetPrivateField(controller, "<controlState>k__BackingField", PlayerControlState.TargetSelection);
        Assert.IsFalse(controller.TryRotatePlayerTowardCharacter(enemyEast));

        SetPrivateField(controller, "<controlState>k__BackingField", PlayerControlState.Normal);

        // 비-Idle 상태 거부
        playerChar.currentState = CharacterState.Attack;
        Assert.IsFalse(controller.TryRotatePlayerTowardCharacter(enemyEast));
        playerChar.currentState = CharacterState.Idle;

        // 적 턴일 때 거부
        var enemyCtrl = new StubCharacterController(enemyEast);
        typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, enemyCtrl);
        Assert.IsFalse(controller.TryRotatePlayerTowardCharacter(enemyEast));
    }

    [Test]
    public void HandleGlobalClick_NormalMode_3DColliderClickRotatesTowardEnemyEvenWhenBehindTileUnderCursor()
    {
        GameMap map = CreateTestGameMap(6, 6);

        // 모든 타일에 Tile 레이어 박스 콜라이더를 부여하여 경사 카메라 레이캐스트 시 뒤쪽 타일이 히트되도록 구성
        int tileLayer = LayerMask.NameToLayer("Tile");
        for (int x = 0; x < 6; x++)
        {
            for (int y = 0; y < 6; y++)
            {
                Tile t = map.GetTile(x, y);
                if (tileLayer >= 0) t.gameObject.layer = tileLayer;
                BoxCollider tileCol = t.gameObject.AddComponent<BoxCollider>();
                tileCol.size = new Vector3(1f, 0.1f, 1f);
            }
        }

        // 플레이어 (2, 1), 적 (4, 1) -> 플레이어가 적을 바라보면 East여야 함
        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, map.GetTile(2, 1));
        playerChar.characterMove.SetFacingDirection(LookDirection.West, true);

        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, map.GetTile(4, 1));
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) enemyChar.gameObject.layer = enemyLayer;
        BoxCollider enemyCol = enemyChar.gameObject.AddComponent<BoxCollider>();
        enemyCol.center = new Vector3(0f, 1.2f, 0f);
        enemyCol.size = new Vector3(0.8f, 2.4f, 0.8f);

        // 35도 경사 카메라를 배치해 적 상단을 클릭할 때 뒤쪽 타일을 관통하도록 설정
        Camera cam = CreateSlantedTestMainCamera(new Vector3(2.5f, 4.5f, -3.8f), new Vector3(3.0f, 0f, 2.0f));
        Physics.SyncTransforms();

        var (_, controller) = CreateTestPlayerController(playerChar);
        CreateTestRunAndBattleManager(controller, new ICharacterController[] { new StubCharacterController(enemyChar) });

        Vector3 enemyHeadWorld = enemyChar.transform.position + new Vector3(0f, 1.6f, 0f);
        Vector2 clickScreenPos = cam.WorldToScreenPoint(enemyHeadWorld);

        // 뒤쪽 타일이 실제로 적 타일과 다른지 검증
        Tile rayTile = InvokePrivateMethodWithReturn<Tile>(controller, "GetTileFromScreenPosition", clickScreenPos);
        if (rayTile != null)
        {
            Assert.AreNotSame(map.GetTile(4, 1), rayTile, "경사 카메라에서 적 상단 클릭 시 뒤쪽 타일이 맞도록 구성되어야 합니다.");
        }

        InvokePrivateMethod(controller, "HandleGlobalClick", clickScreenPos);
        Assert.AreEqual(LookDirection.East, playerChar.characterMove.facingDirection, "3D 콜라이더 히트 시 뒤쪽 타일이 아닌 적 유닛 방향으로 회전해야 합니다.");
    }

    #endregion

    #region (b) UI Click Rotation (SideCard, TimelineSlot, CharacterIntentUI)

    [Test]
    public void BattleSideUnitCardUI_LeftClick_InvokesEventAndRotatesPlayer_IgnoresRightClick()
    {
        GameObject sideHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleSideUnitHUDPanel.prefab");
        Assert.IsNotNull(sideHudPrefab);

        GameMap map = CreateTestGameMap(5, 5);
        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, map.GetTile(2, 2));
        playerChar.characterMove.SetFacingDirection(LookDirection.SouthWest, true);
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, map.GetTile(3, 4));

        var (_, controller) = CreateTestPlayerController(playerChar);
        var enemyCtrl = new StubCharacterController(enemyChar);
        var (_, battleManager) = CreateTestRunAndBattleManager(controller, new ICharacterController[] { enemyCtrl });

        Canvas canvas = CreateTestCanvas("SideCardClickCanvas");
        EventSystem es = CreateTestEventSystem("SideCardClickES");
        GameObject panelGo = Track(UnityEngine.Object.Instantiate(sideHudPrefab, canvas.transform, false));
        BattleSideUnitHUDPanel sidePanel = panelGo.GetComponent<BattleSideUnitHUDPanel>();
        InvokePrivateMethod(sidePanel, "Awake");
        sidePanel.BindBattle(battleManager);

        List<Character> clickedUnits = new List<Character>();
        sidePanel.OnUnitCardClicked += c => clickedUnits.Add(c);

        BattleSideUnitCardUI enemyCard = sidePanel.ActiveRightCards[0];

        // 우클릭 무시
        enemyCard.OnPointerClick(new PointerEventData(es) { button = PointerEventData.InputButton.Right });
        Assert.AreEqual(0, clickedUnits.Count);
        Assert.AreEqual(LookDirection.SouthWest, playerChar.characterMove.facingDirection);

        // 좌클릭 시 콜백 발행 및 플레이어 회전
        enemyCard.OnPointerClick(new PointerEventData(es) { button = PointerEventData.InputButton.Left });
        Assert.AreEqual(1, clickedUnits.Count);
        Assert.AreSame(enemyChar, clickedUnits[0]);
        Assert.AreEqual(LookDirection.NorthEast, playerChar.characterMove.facingDirection);
    }

    [Test]
    public void BattleTimelineSlotUI_NonGhostLeftClick_RotatesPlayer_AndGhostIsIgnored()
    {
        GameObject timelinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleTimelinePanel.prefab");
        GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleTimelineSlotUI.prefab");
        Assert.IsNotNull(timelinePrefab);
        Assert.IsNotNull(slotPrefab);

        GameMap map = CreateTestGameMap(5, 5);
        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, map.GetTile(3, 2));
        playerChar.characterMove.SetFacingDirection(LookDirection.SouthWest, true);
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, map.GetTile(0, 2));

        var (_, controller) = CreateTestPlayerController(playerChar);
        var enemyCtrl = new StubCharacterController(enemyChar);
        var (_, battleManager) = CreateTestRunAndBattleManager(controller, new ICharacterController[] { enemyCtrl });

        Canvas canvas = CreateTestCanvas("TimelineClickCanvas");
        EventSystem es = CreateTestEventSystem("TimelineClickES");
        GameObject panelGo = Track(UnityEngine.Object.Instantiate(timelinePrefab, canvas.transform, false));
        BattleTimelinePanel timelinePanel = panelGo.GetComponent<BattleTimelinePanel>();
        InvokePrivateMethod(timelinePanel, "Awake");
        timelinePanel.BindBattle(battleManager);
        InvokePrivateMethod(timelinePanel, "LateUpdate");

        List<Character> clickedUnits = new List<Character>();
        timelinePanel.OnSlotClicked += c => clickedUnits.Add(c);

        BattleTimelineSlotUI enemySlot = FindTimelineSlotForCharacter(timelinePanel, enemyChar);
        Assert.IsNotNull(enemySlot);

        // 우클릭 무시
        enemySlot.OnPointerClick(new PointerEventData(es) { button = PointerEventData.InputButton.Right });
        Assert.AreEqual(0, clickedUnits.Count);
        Assert.AreEqual(LookDirection.SouthWest, playerChar.characterMove.facingDirection);

        // 고스트 슬롯 좌클릭 무시
        GameObject ghostGo = Track(UnityEngine.Object.Instantiate(slotPrefab, canvas.transform, false));
        BattleTimelineSlotUI ghostSlot = ghostGo.GetComponent<BattleTimelineSlotUI>();
        InvokePrivateMethod(ghostSlot, "Awake");
        TimelineEntry ghostEntry = enemySlot.BoundEntry;
        ghostSlot.Bind(in ghostEntry, isGhost: true);
        ghostSlot.OnPointerClick(new PointerEventData(es) { button = PointerEventData.InputButton.Left });
        Assert.AreEqual(0, clickedUnits.Count);
        Assert.AreEqual(LookDirection.SouthWest, playerChar.characterMove.facingDirection);

        // 비-고스트 슬롯 좌클릭 시 이벤트 발행 및 회전
        enemySlot.OnPointerClick(new PointerEventData(es) { button = PointerEventData.InputButton.Left });
        Assert.AreEqual(1, clickedUnits.Count);
        Assert.AreSame(enemyChar, clickedUnits[0]);
        Assert.AreEqual(LookDirection.West, playerChar.characterMove.facingDirection);
    }

    [Test]
    public void CharacterIntentUI_LeftClick_RotatesPlayerInNormalMode_AndPreservesHover()
    {
        GameObject intentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterIntentUI.prefab");
        Assert.IsNotNull(intentPrefab);

        GameMap map = CreateTestGameMap(5, 5);
        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, map.GetTile(2, 4));
        playerChar.characterMove.SetFacingDirection(LookDirection.NorthEast, true);
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, map.GetTile(0, 0));

        var (_, controller) = CreateTestPlayerController(playerChar);
        CreateTestRunAndBattleManager(controller, new ICharacterController[] { new StubCharacterController(enemyChar) });

        Canvas canvas = CreateTestCanvas("IntentClickCanvas");
        EventSystem es = CreateTestEventSystem("IntentClickES");
        CreateTestTooltipManager(canvas);
        CreateSlantedTestMainCamera(new Vector3(2f, 10f, -2f), new Vector3(2f, 0f, 2f));

        GameObject intentGo = Track(UnityEngine.Object.Instantiate(intentPrefab, canvas.transform, false));
        CharacterIntentUI intentUI = intentGo.GetComponent<CharacterIntentUI>();
        InvokePrivateMethod(intentUI, "Awake");
        intentUI.SetTarget(enemyChar.transform);
        intentUI.SetIntent(new EnemyIntentInfo
        {
            isValid = true,
            intentType = EnemyIntentType.Attack,
            estimatedValue = 12
        });

        // 호버 진입/이탈 정상 동작 확인
        PointerEventData pointerData = new PointerEventData(es) { button = PointerEventData.InputButton.Left };
        intentUI.OnPointerEnter(pointerData);
        Assert.IsTrue(GetPrivateField<bool>(intentUI, "_isPointerHovering"));
        intentUI.OnPointerExit(pointerData);
        Assert.IsFalse(GetPrivateField<bool>(intentUI, "_isPointerHovering"));

        // 우클릭 무시
        intentUI.OnPointerClick(new PointerEventData(es) { button = PointerEventData.InputButton.Right });
        Assert.AreEqual(LookDirection.NorthEast, playerChar.characterMove.facingDirection);

        // 좌클릭 시 회전
        intentUI.OnPointerClick(pointerData);
        Assert.AreEqual(LookDirection.SouthWest, playerChar.characterMove.facingDirection);
    }

    #endregion

    #region (c) Multi-Target Collection & Preview

    [Test]
    public void CollectTargetingPreviewTargets_AoE_MultiEnemy_AllEnemies_And_SingleAlly_WorkAccurately()
    {
        GameMap map = CreateTestGameMap(6, 6);
        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, map.GetTile(2, 0));
        Character enemyA = CreateTestCharacter("EnemyA", CharacterFaction.Enemy, map.GetTile(2, 2));
        Character enemyB = CreateTestCharacter("EnemyB", CharacterFaction.Enemy, map.GetTile(3, 2));
        Character deadEnemy = CreateTestCharacter("DeadEnemy", CharacterFaction.Enemy, map.GetTile(2, 3));
        Character allyChar = CreateTestCharacter("Ally", CharacterFaction.Ally, map.GetTile(1, 2));

        var (_, controller) = CreateTestPlayerController(playerChar);
        CreateTestRunAndBattleManager(
            controller,
            new ICharacterController[]
            {
                new StubCharacterController(enemyA),
                new StubCharacterController(enemyB),
                new StubCharacterController(deadEnemy),
                new StubCharacterController(allyChar)
            });

        // InitBattle -> ResetCharacterForBattle 이후 사망 상태 설정
        deadEnemy.isDead = true;
        playerChar.curStamina = 80f;

        List<Character> results = new List<Character>(8);

        // 1. AreaGround + CircleFill(r=1): 적 2명 동시 수집, 아군/사망/플레이어 제외
        Card aoeGroundCard = CreateTestCard(
            "Fireball",
            "AreaGround",
            minDistance: 1,
            maxDistance: 4,
            additionalAreas: new List<EffectArea> { new EffectArea { areaType = "CircleFill", distance = 1 } });

        controller.CollectTargetingPreviewTargets(aoeGroundCard, map.GetTile(2, 2), new Vector2Int(2, 2), isInRange: true, directHoveredChar: null, results);
        CollectionAssert.AreEquivalent(new[] { enemyA, enemyB }, results);

        // 사거리 밖일 때는 비어 있어야 함
        controller.CollectTargetingPreviewTargets(aoeGroundCard, map.GetTile(2, 2), new Vector2Int(2, 2), isInRange: false, directHoveredChar: null, results);
        Assert.AreEqual(0, results.Count);

        // 2. SingleEnemy + AoE(CircleFill r=1) + directHoveredChar 중복 없이 포함
        Card aoeSingleEnemyCard = CreateTestCard(
            "Cleave",
            "SingleEnemy",
            minDistance: 1,
            maxDistance: 4,
            additionalAreas: new List<EffectArea> { new EffectArea { areaType = "CircleFill", distance = 1 } });

        controller.CollectTargetingPreviewTargets(aoeSingleEnemyCard, map.GetTile(2, 2), new Vector2Int(2, 2), isInRange: true, directHoveredChar: enemyA, results);
        CollectionAssert.AreEquivalent(new[] { enemyA, enemyB }, results);

        // 3. MultiEnemy: 이미 확정된 _multiSelectedTargets + 현재 호버 대상 병합 (중복 방지 + 사거리 밖에도 확정 대상 유지)
        Card multiCard = CreateTestCard("TwinShot", "MultiEnemy", minDistance: 1, maxDistance: 4, targetCount: 2, allowDuplicateTarget: false);
        controller.MultiSelectedTargets.Clear();
        controller.MultiSelectedTargets.Add(enemyA);

        controller.CollectTargetingPreviewTargets(multiCard, map.GetTile(3, 2), new Vector2Int(3, 2), isInRange: true, directHoveredChar: enemyB, results);
        CollectionAssert.AreEquivalent(new[] { enemyA, enemyB }, results);

        // 이미 선택된 enemyA를 다시 호버해도 중복 추가되지 않아야 함
        controller.CollectTargetingPreviewTargets(multiCard, map.GetTile(2, 2), new Vector2Int(2, 2), isInRange: true, directHoveredChar: enemyA, results);
        Assert.AreEqual(1, results.Count);
        Assert.AreSame(enemyA, results[0]);

        // 사거리 밖 호버 시에도 확정된 enemyA는 유지되어야 함
        controller.CollectTargetingPreviewTargets(multiCard, map.GetTile(5, 5), new Vector2Int(5, 5), isInRange: false, directHoveredChar: null, results);
        Assert.AreEqual(1, results.Count);
        Assert.AreSame(enemyA, results[0]);
        controller.MultiSelectedTargets.Clear();

        // 4. AllEnemies: 살아있는 모든 적대 유닛만 수집
        Card allEnemiesCard = CreateTestCard("Whirlwind", "AllEnemies", minDistance: 0, maxDistance: 0);
        controller.CollectTargetingPreviewTargets(allEnemiesCard, map.GetTile(2, 0), new Vector2Int(2, 0), isInRange: true, directHoveredChar: null, results);
        CollectionAssert.AreEquivalent(new[] { enemyA, enemyB }, results);

        // 5. SingleAlly: 아군만 수집하고 적은 제외
        Card singleAllyCard = CreateTestCard("HealAlly", "SingleAlly", minDistance: 0, maxDistance: 4);
        controller.CollectTargetingPreviewTargets(singleAllyCard, map.GetTile(1, 2), new Vector2Int(1, 2), isInRange: true, directHoveredChar: allyChar, results);
        CollectionAssert.AreEquivalent(new[] { allyChar }, results);

        controller.CollectTargetingPreviewTargets(singleAllyCard, map.GetTile(2, 2), new Vector2Int(2, 2), isInRange: true, directHoveredChar: enemyA, results);
        Assert.AreEqual(0, results.Count);
    }

    #endregion

    #region (d) Simultaneous Multi-Target Highlight Sync & Cleanup

    [Test]
    public void MultiTargetHighlights_SyncSimultaneouslyAcrossTimelineSideHudAndWorld_AndClearCleanly()
    {
        GameObject timelinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleTimelinePanel.prefab");
        GameObject sideHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleSideUnitHUDPanel.prefab");
        GameObject statusBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterStatusBarUI.prefab");
        GameObject effectListPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterEffectListUI.prefab");
        GameObject intentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterIntentUI.prefab");

        Assert.IsNotNull(timelinePrefab);
        Assert.IsNotNull(sideHudPrefab);
        Assert.IsNotNull(statusBarPrefab);
        Assert.IsNotNull(effectListPrefab);
        Assert.IsNotNull(intentPrefab);

        GameMap map = CreateTestGameMap(6, 6);
        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, map.GetTile(2, 0));
        Character enemyA = CreateTestCharacter("EnemyA", CharacterFaction.Enemy, map.GetTile(2, 2));
        Character enemyB = CreateTestCharacter("EnemyB", CharacterFaction.Enemy, map.GetTile(3, 2));

        // 3D 모델 렌더러 + WorldUI 제외용 렌더러 + 바닥 링 오브젝트 구성
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (unlitShader == null)
        {
            unlitShader = Shader.Find("Standard");
        }
        Material sharedEnemyMat = Track(new Material(unlitShader));

        (CharacterUIController ctrlA, MeshRenderer meshA, MeshRenderer worldUiMeshA, GameObject ringA) = AttachTestCharacterVisuals(enemyA, sharedEnemyMat);
        (CharacterUIController ctrlB, MeshRenderer meshB, _, GameObject ringB) = AttachTestCharacterVisuals(enemyB, sharedEnemyMat);
        (CharacterUIController ctrlP, _, _, _) = AttachTestCharacterVisuals(playerChar, sharedEnemyMat);

        Canvas mainCanvas = CreateTestCanvas("HighlightMainCanvas");
        Canvas worldCanvas = CreateTestCanvas("HighlightWorldCanvas");

        CharacterEffectListUI effectsA = Track(UnityEngine.Object.Instantiate(effectListPrefab, worldCanvas.transform, false)).GetComponent<CharacterEffectListUI>();
        CharacterStatusBarUI statusA = Track(UnityEngine.Object.Instantiate(statusBarPrefab, worldCanvas.transform, false)).GetComponent<CharacterStatusBarUI>();
        CharacterIntentUI intentA = Track(UnityEngine.Object.Instantiate(intentPrefab, worldCanvas.transform, false)).GetComponent<CharacterIntentUI>();

        CharacterEffectListUI effectsB = Track(UnityEngine.Object.Instantiate(effectListPrefab, worldCanvas.transform, false)).GetComponent<CharacterEffectListUI>();
        CharacterStatusBarUI statusB = Track(UnityEngine.Object.Instantiate(statusBarPrefab, worldCanvas.transform, false)).GetComponent<CharacterStatusBarUI>();
        CharacterIntentUI intentB = Track(UnityEngine.Object.Instantiate(intentPrefab, worldCanvas.transform, false)).GetComponent<CharacterIntentUI>();

        CharacterEffectListUI effectsP = Track(UnityEngine.Object.Instantiate(effectListPrefab, worldCanvas.transform, false)).GetComponent<CharacterEffectListUI>();
        CharacterStatusBarUI statusP = Track(UnityEngine.Object.Instantiate(statusBarPrefab, worldCanvas.transform, false)).GetComponent<CharacterStatusBarUI>();

        BindCharacterWorldViews(ctrlA, statusA, effectsA, intentA);
        BindCharacterWorldViews(ctrlB, statusB, effectsB, intentB);
        BindCharacterWorldViews(ctrlP, statusP, effectsP, null);

        GameObject timelineGo = Track(UnityEngine.Object.Instantiate(timelinePrefab, mainCanvas.transform, false));
        BattleTimelinePanel timelinePanel = timelineGo.GetComponent<BattleTimelinePanel>();
        InvokePrivateMethod(timelinePanel, "Awake");

        GameObject sideHudGo = Track(UnityEngine.Object.Instantiate(sideHudPrefab, mainCanvas.transform, false));
        BattleSideUnitHUDPanel sidePanel = sideHudGo.GetComponent<BattleSideUnitHUDPanel>();
        InvokePrivateMethod(sidePanel, "Awake");

        var (_, controller) = CreateTestPlayerController(playerChar);
        var (_, battleManager) = CreateTestRunAndBattleManager(
            controller,
            new ICharacterController[] { new StubCharacterController(enemyA), new StubCharacterController(enemyB) });

        timelinePanel.BindBattle(battleManager);
        InvokePrivateMethod(timelinePanel, "LateUpdate");
        sidePanel.BindBattle(battleManager);

        Card aoeCard = CreateTestCard(
            "Meteor",
            "AreaGround",
            minDistance: 1,
            maxDistance: 4,
            additionalAreas: new List<EffectArea> { new EffectArea { areaType = "CircleFill", distance = 1 } });

        SetPrivateField(controller, "<controlState>k__BackingField", PlayerControlState.TargetSelection);
        SetPrivateField(controller, "activeCardForTargeting", aoeCard);

        // 적 A, B 동시 피격 하이라이트 적용
        var targets = new List<Character> { enemyA, enemyB };
        controller.ApplyTargetingHighlights(targets, aoeCard);

        BattleTimelineSlotUI slotA = FindTimelineSlotForCharacter(timelinePanel, enemyA);
        BattleTimelineSlotUI slotB = FindTimelineSlotForCharacter(timelinePanel, enemyB);
        BattleTimelineSlotUI slotP = FindTimelineSlotForCharacter(timelinePanel, playerChar);
        Assert.IsTrue(GetPrivateField<bool>(slotA, "_isHighlighted"));
        Assert.IsTrue(GetPrivateField<bool>(slotB, "_isHighlighted"));
        Assert.IsFalse(GetPrivateField<bool>(slotP, "_isHighlighted"));

        BattleSideUnitCardUI sideCardA = FindSideCardForCharacter(sidePanel.ActiveRightCards, enemyA);
        BattleSideUnitCardUI sideCardB = FindSideCardForCharacter(sidePanel.ActiveRightCards, enemyB);
        BattleSideUnitCardUI sideCardP = FindSideCardForCharacter(sidePanel.ActiveLeftCards, playerChar);
        Assert.IsTrue(sideCardA.IsHighlighted);
        Assert.IsTrue(sideCardB.IsHighlighted);
        Assert.IsFalse(sideCardP.IsHighlighted);

        Assert.IsTrue(ctrlA.IsTargetingHighlighted);
        Assert.IsTrue(ctrlB.IsTargetingHighlighted);
        Assert.IsFalse(ctrlP.IsTargetingHighlighted);
        Assert.IsTrue(ringA.activeSelf);
        Assert.IsTrue(ringB.activeSelf);
        Assert.AreEqual(new Vector3(1.12f, 1.12f, 1.12f), statusA.transform.localScale);
        Assert.AreEqual(new Vector3(1.12f, 1.12f, 1.12f), statusB.transform.localScale);

        // MaterialPropertyBlock 검증 + sharedMaterial 인스턴스 미생성 + WorldUI 렌더러 제외 검증
        MaterialPropertyBlock blockCheck = new MaterialPropertyBlock();
        meshA.GetPropertyBlock(blockCheck);
        Assert.IsFalse(blockCheck.isEmpty, "타깃팅 하이라이트 시 3D 렌더러 MaterialPropertyBlock이 적용되어야 합니다.");
        Assert.AreSame(sharedEnemyMat, meshA.sharedMaterial, "renderer.material 접근으로 머티리얼 복제가 발생하지 않아야 합니다.");

        MaterialPropertyBlock worldUiBlockCheck = new MaterialPropertyBlock();
        worldUiMeshA.GetPropertyBlock(worldUiBlockCheck);
        Assert.IsTrue(worldUiBlockCheck.isEmpty, "WorldUI 레이어 렌더러에는 MaterialPropertyBlock이 적용되지 않아야 합니다.");

        // 타임라인 호버 하이라이트와 타깃팅 하이라이트 결합 보존 검증
        CharacterUIController.SetTimelineHighlightFor(enemyA, true);
        CharacterUIController.SetTimelineHighlightFor(enemyA, false);
        Assert.IsTrue(ctrlA.IsTargetingHighlighted);
        Assert.IsTrue(ringA.activeSelf);
        Assert.AreEqual(new Vector3(1.12f, 1.12f, 1.12f), statusA.transform.localScale, "타임라인 호버 해제 시에도 타깃팅 하이라이트가 남아있으면 스케일이 유지되어야 합니다.");

        CharacterUIController.SetTimelineHighlightFor(enemyB, true);
        controller.CancelCurrentState();

        Assert.IsFalse(ctrlA.IsTargetingHighlighted);
        Assert.IsFalse(ctrlB.IsTargetingHighlighted);
        Assert.IsFalse(ringA.activeSelf);
        Assert.IsFalse(ringB.activeSelf);
        Assert.AreEqual(Vector3.one, statusA.transform.localScale);
        Assert.AreEqual(new Vector3(1.12f, 1.12f, 1.12f), statusB.transform.localScale, "타깃팅 해제 시에도 타임라인 호버가 유지 중이면 스케일이 보존되어야 합니다.");

        CharacterUIController.SetTimelineHighlightFor(enemyB, false);
        Assert.AreEqual(Vector3.one, statusB.transform.localScale);

        meshA.GetPropertyBlock(blockCheck);
        Assert.IsTrue(blockCheck.isEmpty, "CancelCurrentState 후 MaterialPropertyBlock이 완전히 초기화되어야 합니다.");
        Assert.IsFalse(GetPrivateField<bool>(slotA, "_isHighlighted"));
        Assert.IsFalse(GetPrivateField<bool>(slotB, "_isHighlighted"));
        Assert.IsFalse(sideCardA.IsHighlighted);
        Assert.IsFalse(sideCardB.IsHighlighted);

        // OnTurnEnd 및 OnBattleEnd 시에도 완전 정리되는지 검증
        SetPrivateField(controller, "<controlState>k__BackingField", PlayerControlState.TargetSelection);
        SetPrivateField(controller, "activeCardForTargeting", aoeCard);
        controller.ApplyTargetingHighlights(targets, aoeCard);
        Assert.IsTrue(ctrlA.IsTargetingHighlighted);

        controller.OnTurnEnd();
        Assert.IsFalse(ctrlA.IsTargetingHighlighted);
        Assert.IsFalse(sideCardA.IsHighlighted);
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);

        SetPrivateField(controller, "<controlState>k__BackingField", PlayerControlState.TargetSelection);
        SetPrivateField(controller, "activeCardForTargeting", aoeCard);
        controller.ApplyTargetingHighlights(targets, aoeCard);
        Assert.IsTrue(ctrlB.IsTargetingHighlighted);

        controller.OnBattleEnd();
        Assert.IsFalse(ctrlB.IsTargetingHighlighted);
        Assert.IsFalse(sideCardB.IsHighlighted);
    }

    [Test]
    public void HandleGlobalClick_TargetSelection_35DegCharacterSnapCastsAndClears_AndMultiEnemyKeepsLockedHighlight()
    {
        GameMap map = CreateTestGameMap(6, 6);
        int tileLayer = LayerMask.NameToLayer("Tile");
        for (int x = 0; x < 6; x++)
        {
            for (int y = 0; y < 6; y++)
            {
                Tile t = map.GetTile(x, y);
                if (tileLayer >= 0) t.gameObject.layer = tileLayer;
                BoxCollider tileCol = t.gameObject.AddComponent<BoxCollider>();
                tileCol.size = new Vector3(1f, 0.1f, 1f);
            }
        }

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, map.GetTile(2, 0));
        Character enemyA = CreateTestCharacter("EnemyA", CharacterFaction.Enemy, map.GetTile(2, 2));
        Character enemyB = CreateTestCharacter("EnemyB", CharacterFaction.Enemy, map.GetTile(4, 2));

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        foreach (Character enemy in new[] { enemyA, enemyB })
        {
            if (enemyLayer >= 0) enemy.gameObject.layer = enemyLayer;
            BoxCollider col = enemy.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.2f, 0f);
            col.size = new Vector3(0.8f, 2.4f, 0.8f);
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (unlitShader == null)
        {
            unlitShader = Shader.Find("Standard");
        }
        Material sharedMat = Track(new Material(unlitShader));
        (CharacterUIController ctrlA, _, _, _) = AttachTestCharacterVisuals(enemyA, sharedMat);
        (CharacterUIController ctrlB, _, _, _) = AttachTestCharacterVisuals(enemyB, sharedMat);

        Camera cam = CreateSlantedTestMainCamera(new Vector3(2.0f, 4.5f, -3.5f), new Vector3(2.5f, 0f, 2.5f));
        Physics.SyncTransforms();

        var (_, controller) = CreateTestPlayerController(playerChar);
        CreateTestRunAndBattleManager(
            controller,
            new ICharacterController[] { new StubCharacterController(enemyA), new StubCharacterController(enemyB) });
        playerChar.curStamina = 80f;

        // MultiEnemy (2명 선택) 진입 -> 호버 시 35도 스냅 하이라이트 + 1번째 적 클릭 시 _multiSelectedTargets 유지 + 하이라이트 유지
        Card multiCard = CreateTestCard("DualStrike", "MultiEnemy", minDistance: 1, maxDistance: 5, targetCount: 2, allowDuplicateTarget: false);
        controller.TryUseCard(multiCard);
        Assert.AreEqual(PlayerControlState.TargetSelection, controller.controlState);

        Vector2 hoverEnemyA = cam.WorldToScreenPoint(enemyA.transform.position + new Vector3(0f, 1.5f, 0f));
        InvokePrivateMethod(controller, "HandlePointerHover", hoverEnemyA);
        Assert.IsTrue(ctrlA.IsTargetingHighlighted, "타깃팅 호버 시 35도 스냅으로 적 하이라이트가 즉시 활성화되어야 합니다.");

        Vector2 clickEnemyA = cam.WorldToScreenPoint(enemyA.transform.position + new Vector3(0f, 1.5f, 0f));
        InvokePrivateMethod(controller, "HandleGlobalClick", clickEnemyA);

        Assert.AreEqual(PlayerControlState.TargetSelection, controller.controlState, "MultiEnemy 1/2 선택 시 타겟팅 모드가 유지되어야 합니다.");
        Assert.AreEqual(1, controller.MultiSelectedTargets.Count);
        Assert.AreSame(enemyA, controller.MultiSelectedTargets[0]);
        Assert.IsTrue(ctrlA.IsTargetingHighlighted, "확정된 첫 번째 다중 타깃은 하이라이트가 유지되어야 합니다.");

        // 2번째 적 클릭 시 카드 시전 완료 및 모든 타깃팅 하이라이트 해제
        Vector2 clickEnemyB = cam.WorldToScreenPoint(enemyB.transform.position + new Vector3(0f, 1.5f, 0f));
        InvokePrivateMethod(controller, "HandleGlobalClick", clickEnemyB);

        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.AreEqual(0, controller.MultiSelectedTargets.Count);
        Assert.IsFalse(ctrlA.IsTargetingHighlighted);
        Assert.IsFalse(ctrlB.IsTargetingHighlighted);
    }

    #endregion

    private (CharacterUIController ctrl, MeshRenderer bodyMesh, MeshRenderer worldUiMesh, GameObject ringObj) AttachTestCharacterVisuals(
        Character character,
        Material sharedMaterial)
    {
        GameObject bodyGo = Track(new GameObject("BodyModel", typeof(MeshFilter), typeof(MeshRenderer)));
        bodyGo.transform.SetParent(character.transform, false);
        MeshRenderer bodyMesh = bodyGo.GetComponent<MeshRenderer>();
        bodyMesh.sharedMaterial = sharedMaterial;

        GameObject worldUiGo = Track(new GameObject("WorldUIChild", typeof(MeshFilter), typeof(MeshRenderer)));
        worldUiGo.transform.SetParent(character.transform, false);
        int worldUiLayer = LayerMask.NameToLayer("WorldUI");
        if (worldUiLayer >= 0) worldUiGo.layer = worldUiLayer;
        MeshRenderer worldUiMesh = worldUiGo.GetComponent<MeshRenderer>();
        worldUiMesh.sharedMaterial = sharedMaterial;

        GameObject ringGo = Track(new GameObject("TargetingHighlightRing"));
        ringGo.transform.SetParent(character.transform, false);
        ringGo.SetActive(true);

        CharacterUIController ctrl = character.gameObject.AddComponent<CharacterUIController>();
        SetPrivateField(ctrl, "targetingHighlightRingObj", ringGo);
        InvokePrivateMethod(ctrl, "Awake");
        Assert.IsFalse(ringGo.activeSelf, "Awake 시 targetingHighlightRingObj는 비활성 초기화되어야 합니다.");

        return (ctrl, bodyMesh, worldUiMesh, ringGo);
    }

    private static BattleTimelineSlotUI FindTimelineSlotForCharacter(BattleTimelinePanel panel, Character target)
    {
        System.Collections.IList activeSlots = GetPrivateField<System.Collections.IList>(panel, "_activeSlots");
        for (int i = 0; i < activeSlots.Count; i++)
        {
            object slotInst = activeSlots[i];
            Character boundChar = slotInst.GetType().GetField("boundCharacter").GetValue(slotInst) as Character;
            bool isPredicted = (bool)slotInst.GetType().GetField("isPredictedNextTurn").GetValue(slotInst);
            if (ReferenceEquals(boundChar, target) && !isPredicted)
            {
                return slotInst.GetType().GetField("ui").GetValue(slotInst) as BattleTimelineSlotUI;
            }
        }
        return null;
    }

    private static BattleSideUnitCardUI FindSideCardForCharacter(IReadOnlyList<BattleSideUnitCardUI> cards, Character target)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && ReferenceEquals(cards[i].BoundCharacter, target))
            {
                return cards[i];
            }
        }
        return null;
    }

    private static T InvokePrivateMethodWithReturn<T>(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"메서드 '{methodName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        return (T)method.Invoke(target, args);
    }
}
