using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EventStructs;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XLua;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

[TestFixture]
public class FacingDamagePreviewUITests
{
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
    private readonly List<LuaTable> _createdLuaTables = new List<LuaTable>();
    private readonly List<Camera> _disabledSceneMainCameras = new List<Camera>();
    private LuaEnv _luaEnv;
    private IDeserializer _yamlDeserializer;

    [SetUp]
    public void SetUp()
    {
        CombatFacing.ClearActiveCastSession();
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _luaEnv = new LuaEnv();
        _luaEnv.DoString(@"
            EventStructs = {
                DamageInfo = CS.EventStructs.DamageInfo,
                HealInfo = CS.EventStructs.HealInfo,
                StaminaInfo = CS.EventStructs.StaminaInfo,
                TurnEndStaminaInfo = CS.EventStructs.TurnEndStaminaInfo,
                ShieldInfo = CS.EventStructs.ShieldInfo,
                CardInfo = CS.EventStructs.CardInfo,
                MoveInfo = CS.EventStructs.MoveInfo,
                EffectInfo = CS.EventStructs.EffectInfo,
                DamageFlag = CS.EventStructs.DamageFlag,
                HealFlag = CS.EventStructs.HealFlag,
                StaminaFlag = CS.EventStructs.StaminaFlag,
                ShieldFlag = CS.EventStructs.ShieldFlag,
                CardFlag = CS.EventStructs.CardFlag,
                MoveFlag = CS.EventStructs.MoveFlag,
                EffectFlag = CS.EventStructs.EffectFlag,
                StatModifierTarget = CS.StatModifierTarget,
                StatModifierOp = CS.StatModifierOp,
                StatModifierLifetime = CS.StatModifierLifetime,
                FacingZone = CS.FacingZone,
                FacingHitRelation = CS.FacingHitRelation,
                FacingOriginMode = CS.FacingOriginMode,
            }
            UnityEngine = {
                Debug = CS.UnityEngine.Debug,
                Mathf = CS.UnityEngine.Mathf,
                Random = CS.UnityEngine.Random,
                Vector3 = CS.UnityEngine.Vector3,
                Vector2 = CS.UnityEngine.Vector2,
                Vector2Int = CS.UnityEngine.Vector2Int,
            }
            LookDirection = CS.LookDirection
            HexMath = CS.HexMath
            CombatFacing = CS.CombatFacing
            CS = nil
        ");
    }

    [TearDown]
    public void TearDown()
    {
        CombatFacing.ClearActiveCastSession();

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

        ActionQueueManager queueManager = UnityEngine.Object.FindFirstObjectByType<ActionQueueManager>();
        if (queueManager != null)
        {
            queueManager.ClearQueue(invokeCanceled: false);
            UnityEngine.Object.DestroyImmediate(queueManager.gameObject);
        }
        SetStaticField<ActionQueueManager>("_instance", null);
        SetStaticField<ActionQueueManager>("_isShuttingDown", false);

        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        SetStaticAutoProperty<BattleTimelinePanel>("Instance", null);
        SetStaticAutoProperty<BattleSideUnitHUDPanel>("Instance", null);
        SetStaticAutoProperty<RunManager>("instance", null);
        SetStaticAutoProperty<GameSceneManager>("instance", null);
        SetStaticAutoProperty<UIManager>("instance", null);
        SetStaticAutoProperty<GameMap>("current", null);

        for (int i = _createdLuaTables.Count - 1; i >= 0; i--)
        {
            if (_createdLuaTables[i] != null)
            {
                _createdLuaTables[i].Dispose();
            }
        }
        _createdLuaTables.Clear();

        if (_luaEnv != null)
        {
            _luaEnv.FullGc();
            _luaEnv.Dispose();
            _luaEnv = null;
        }

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

    private Canvas CreateTestCanvas(string name = "FacingPreviewTestCanvas")
    {
        Track(new GameObject(name + "_EventSystem", typeof(EventSystem)));

        GameObject canvasGo = Track(new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        return canvas;
    }

    private GameMap CreateTestGameMap(int width = 8, int height = 8)
    {
        GameObject mapGo = Track(new GameObject("FacingPreviewGameMap"));
        GameMap gameMap = mapGo.AddComponent<GameMap>();
        InvokePrivateMethod(gameMap, "Awake");

        GameObject tilePrefab = Track(new GameObject("FacingPreviewTilePrefab", typeof(Tile)));
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
        camGo.transform.position = lookCenter + new Vector3(0f, 12f, 0f);
        camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        Camera cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        return cam;
    }

    private (Character character, CharacterUIController uiCtrl, CharacterStatusBarUI statusBar) CreateCharacterWithStatusBar(
        string name,
        CharacterFaction faction,
        Tile tile,
        LookDirection facing,
        Canvas canvas)
    {
        GameObject charGo = Track(new GameObject(name));
        Character character = charGo.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = faction;
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 0f,
            moveRange = 3
        });
        character.curStamina = 100f;
        character.currentState = CharacterState.Idle;
        if (tile != null)
        {
            charGo.transform.position = tile.transform.position;
            character.characterMove.SetCurrentTile(tile, triggerTileHazards: false);
        }
        character.characterMove.SetFacingDirection(facing, true);
        _createdCharacters.Add(character);

        GameObject statusBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterStatusBarUI.prefab");
        Assert.IsNotNull(statusBarPrefab, "CharacterStatusBarUI.prefab을 찾을 수 없습니다.");

        GameObject statusGo = Track(UnityEngine.Object.Instantiate(statusBarPrefab, canvas.transform, false));
        CharacterStatusBarUI statusBar = statusGo.GetComponent<CharacterStatusBarUI>();
        Assert.IsNotNull(statusBar);
        InvokePrivateMethod(statusBar, "Awake");

        CharacterUIController uiCtrl = charGo.AddComponent<CharacterUIController>();
        SetPrivateField(uiCtrl, "character", character);
        SetPrivateField(uiCtrl, "statusBarUI", statusBar);
        InvokePrivateMethod(uiCtrl, "Awake");
        uiCtrl.ShowUI();

        return (character, uiCtrl, statusBar);
    }

    private (PlayerBattleController controller, BattleManager battleManager) CreateBattleTurnContext(Character playerChar, Character enemyChar)
    {
        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);

        GameObject runGo = Track(new GameObject("FacingPreviewRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);
        runManager.playerBattleController = controller;

        BattleManager battleManager = new BattleManager();
        battleManager.Initialize();
        _createdBattleManagers.Add(battleManager);
        typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public).SetValue(runManager, battleManager);

        battleManager.InitBattle(
            new List<ICharacterController> { controller },
            new List<ICharacterController> { new StubEnemyController(enemyChar) });
        battleManager.StartCombat();
        typeof(BattleManager).GetProperty("battleState", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, BattleState.TurnInProgress);
        typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, controller);

        return (controller, battleManager);
    }

    private Card CreateAttackCard(string name, string targetType, float damage, int minDist, int maxDist, List<EffectArea> areas = null)
    {
        CardData data = new CardData
        {
            cardName = name,
            displayName = name,
            cardType = "Attack",
            stamina = 0,
            targetType = targetType,
            targetMinDistance = minDist,
            targetMaxDistance = maxDist,
            baseValues = new Dictionary<string, float> { { "damage", damage } },
            additionalEffectAreaList = areas ?? new List<EffectArea>()
        };
        Card card = new Card(data, null, null, -(_createdCards.Count + 1));
        _createdCards.Add(card);
        return card;
    }

    private Effect AttachCoreEffect(Character target, string effectId, int stack, float duration)
    {
        string yamlPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "YAML", "Effects", effectId + ".yaml");
        string luaPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Effects", effectId + ".lua");

        EffectData data = _yamlDeserializer.Deserialize<EffectData>(File.ReadAllText(yamlPath));
        object[] res = _luaEnv.DoString(File.ReadAllText(luaPath), effectId);
        LuaTable table = res[0] as LuaTable;
        _createdLuaTables.Add(table);

        Effect effect = new Effect(data, table);
        target.effectManager.AddEffect(target, effect, stack, duration);
        return effect;
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
        field?.SetValue(null, value);
    }

    private static void SetStaticAutoProperty<T>(string propName, object value)
    {
        PropertyInfo prop = typeof(T).GetProperty(propName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        prop?.SetValue(null, value);
    }

    [Test]
    public void CharacterStatusBarUI_PrefabHasDamagePreviewBindings_AndStaysInactiveByDefault()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterStatusBarUI.prefab");
        Assert.IsNotNull(prefab, "CharacterStatusBarUI.prefab을 찾을 수 없습니다.");

        Canvas canvas = CreateTestCanvas("StatusBarPrefabBindingsCanvas");
        GameObject go = Track(UnityEngine.Object.Instantiate(prefab, canvas.transform, false));
        CharacterStatusBarUI statusBar = go.GetComponent<CharacterStatusBarUI>();
        Assert.IsNotNull(statusBar);
        InvokePrivateMethod(statusBar, "Awake");

        Assert.AreEqual(0, go.GetComponentsInChildren<Canvas>(true).Length, "CharacterStatusBarUI 프리팹 내부에 중첩 Canvas가 없어야 합니다.");
        Assert.AreEqual(0, go.GetComponentsInChildren<CanvasScaler>(true).Length, "CharacterStatusBarUI 프리팹 내부에 CanvasScaler가 없어야 합니다.");

        RectTransform rt = go.GetComponent<RectTransform>();
        Assert.AreEqual(64f, rt.sizeDelta.x, 0.01f);
        Assert.AreEqual(14f, rt.sizeDelta.y, 0.01f);
        Assert.IsFalse(statusBar.ShowValueText);

        Assert.IsNotNull(GetPrivateField<GameObject>(statusBar, "damagePreviewContainer"));
        Assert.IsNotNull(GetPrivateField<Image>(statusBar, "damagePreviewBackground"));
        Assert.IsNotNull(GetPrivateField<TextMeshProUGUI>(statusBar, "damagePreviewText"));
        Assert.IsNotNull(GetPrivateField<GameObject>(statusBar, "facingBadgeContainer"));
        Assert.IsNotNull(GetPrivateField<Image>(statusBar, "facingBadgeBackground"));
        Assert.IsNotNull(GetPrivateField<TextMeshProUGUI>(statusBar, "facingBadgeText"));

        Assert.IsFalse(statusBar.IsDamagePreviewVisible);
        Assert.IsFalse(statusBar.IsFacingBadgeVisible);
    }

    [Test]
    public void SingleEnemyTargeting_HoverShowsBackstabAndNormalDamagePreview_AndClearsOnCancel()
    {
        GameMap map = CreateTestGameMap(6, 6);
        Camera cam = CreateTestMainCamera(new Vector3(2f, 0f, 2f));
        Canvas canvas = CreateTestCanvas("SingleEnemyPreviewCanvas");

        Tile playerTile = map.GetTile(1, 2);
        Tile enemyTile = map.GetTile(2, 2);
        Tile emptyTile = map.GetTile(1, 3);

        var (playerChar, _, _) = CreateCharacterWithStatusBar("Player", CharacterFaction.Player, playerTile, LookDirection.East, canvas);
        var (enemyChar, _, enemyStatusBar) = CreateCharacterWithStatusBar("Enemy", CharacterFaction.Enemy, enemyTile, LookDirection.East, canvas);

        var (controller, _) = CreateBattleTurnContext(playerChar, enemyChar);
        Card card = CreateAttackCard("단일 타격", "SingleEnemy", damage: 10f, minDist: 1, maxDist: 3);

        controller.TryUseCard(card);
        Assert.AreEqual(PlayerControlState.TargetSelection, controller.controlState);

        // 1. 정후면(플레이어 West -> East 보는 적) 타일 호버: -15 + 정후면 1.5x 배지 표시
        Vector2 enemyScreenPos = cam.WorldToScreenPoint(enemyTile.transform.position);
        InvokePrivateMethod(controller, "HandlePointerHover", enemyScreenPos);

        Assert.IsTrue(enemyStatusBar.IsDamagePreviewVisible);
        Assert.AreEqual("-15", enemyStatusBar.DamagePreviewDisplayText);
        Assert.IsTrue(enemyStatusBar.IsFacingBadgeVisible);
        Assert.AreEqual("정후면 1.5x", enemyStatusBar.FacingBadgeDisplayText);

        // 2. 빈 타일로 마우스 이탈 시 미리보기 및 배지 깔끔하게 숨김
        Vector2 emptyScreenPos = cam.WorldToScreenPoint(emptyTile.transform.position);
        InvokePrivateMethod(controller, "HandlePointerHover", emptyScreenPos);
        Assert.IsFalse(enemyStatusBar.IsDamagePreviewVisible);
        Assert.IsFalse(enemyStatusBar.IsFacingBadgeVisible);

        // 3. 적 시선을 West(정전면)로 돌리고 다시 호버 시 -10 표시 + 배지 숨김
        enemyChar.characterMove.SetFacingDirection(LookDirection.West, true);
        InvokePrivateMethod(controller, "HandlePointerHover", enemyScreenPos);

        Assert.IsTrue(enemyStatusBar.IsDamagePreviewVisible);
        Assert.AreEqual("-10", enemyStatusBar.DamagePreviewDisplayText);
        Assert.IsFalse(enemyStatusBar.IsFacingBadgeVisible);

        // 4. 타깃팅 취소 시 미리보기 초기화
        controller.CancelCurrentState();
        Assert.IsFalse(enemyStatusBar.IsDamagePreviewVisible);
        Assert.IsFalse(enemyStatusBar.IsFacingBadgeVisible);
    }

    [Test]
    public void AreaGroundTargeting_UsesAoECenterForBackstabPreview_AndClearsOnUse()
    {
        GameMap map = CreateTestGameMap(7, 6);
        Camera cam = CreateTestMainCamera(new Vector3(3f, 0f, 2f));
        Canvas canvas = CreateTestCanvas("AoEPreviewCanvas");

        // 플레이어는 적의 앞쪽(4,2)에 있으나, AoE 중심점을 적(2,2, East 시선)의 등 뒤(1,2)에 지정
        Tile playerTile = map.GetTile(4, 2);
        Tile enemyTile = map.GetTile(2, 2);
        Tile behindAoETile = map.GetTile(1, 2);
        Tile missAoETile = map.GetTile(5, 4);

        var (playerChar, _, _) = CreateCharacterWithStatusBar("Player", CharacterFaction.Player, playerTile, LookDirection.West, canvas);
        var (enemyChar, _, enemyStatusBar) = CreateCharacterWithStatusBar("Enemy", CharacterFaction.Enemy, enemyTile, LookDirection.East, canvas);

        var (controller, _) = CreateBattleTurnContext(playerChar, enemyChar);
        Card aoeCard = CreateAttackCard(
            "곡사 포격",
            "AreaGround",
            damage: 10f,
            minDist: 1,
            maxDist: 4,
            areas: new List<EffectArea> { new EffectArea { areaType = "CircleFill", distance = 1 } });

        controller.TryUseCard(aoeCard);

        // AoE 중심이 적 등 뒤(1,2) -> 적에게는 정후면 1.5x(-15) 판정
        Vector2 behindScreenPos = cam.WorldToScreenPoint(behindAoETile.transform.position);
        InvokePrivateMethod(controller, "HandlePointerHover", behindScreenPos);

        Assert.IsTrue(enemyStatusBar.IsDamagePreviewVisible);
        Assert.AreEqual("-15", enemyStatusBar.DamagePreviewDisplayText);
        Assert.IsTrue(enemyStatusBar.IsFacingBadgeVisible);
        Assert.AreEqual("정후면 1.5x", enemyStatusBar.FacingBadgeDisplayText);

        // 대상이 범위를 벗어난 타일로 이동 호버 시 미리보기 숨김
        Vector2 missScreenPos = cam.WorldToScreenPoint(missAoETile.transform.position);
        InvokePrivateMethod(controller, "HandlePointerHover", missScreenPos);
        Assert.IsFalse(enemyStatusBar.IsDamagePreviewVisible);
        Assert.IsFalse(enemyStatusBar.IsFacingBadgeVisible);

        // 다시 유효 타일 호버 후 클릭하여 카드 사용 확정 시 미리보기 즉시 정리
        InvokePrivateMethod(controller, "HandlePointerHover", behindScreenPos);
        Assert.IsTrue(enemyStatusBar.IsDamagePreviewVisible);

        InvokePrivateMethod(controller, "HandleGlobalClick", behindScreenPos);
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.IsFalse(enemyStatusBar.IsDamagePreviewVisible);
        Assert.IsFalse(enemyStatusBar.IsFacingBadgeVisible);
    }

    [Test]
    public void ImmediateProjectileHover_ShowsBackstabWithModifiers_AndMatchesActualTakeDamage()
    {
        GameMap map = CreateTestGameMap(7, 6);
        Canvas canvas = CreateTestCanvas("ProjectilePreviewCanvas");

        Tile playerTile = map.GetTile(1, 2);
        Tile enemyTile = map.GetTile(3, 3);

        var (playerChar, _, _) = CreateCharacterWithStatusBar("Player", CharacterFaction.Player, playerTile, LookDirection.East, canvas);
        var (enemyChar, _, enemyStatusBar) = CreateCharacterWithStatusBar("Enemy", CharacterFaction.Enemy, enemyTile, LookDirection.East, canvas);

        var (controller, _) = CreateBattleTurnContext(playerChar, enemyChar);
        AttachCoreEffect(playerChar, "Strength", 2, 0f);
        AttachCoreEffect(enemyChar, "Vulnerable", 1, 5f);
        Card projCard = CreateAttackCard(
            "광폭 투사체",
            "Directional",
            damage: 10f,
            minDist: 1,
            maxDist: 4,
            areas: new List<EffectArea> { new EffectArea { areaType = "DirectionalLine", distance = 4, width = 3 } });

        // 즉시 시전형 투사체 카드 호버: (10 + 2) * 1.5(정후면) * 1.5(취약) = 27
        controller.PreviewImmediateCardHover(projCard);

        Assert.IsTrue(enemyStatusBar.IsDamagePreviewVisible);
        Assert.AreEqual("-27", enemyStatusBar.DamagePreviewDisplayText);
        Assert.IsTrue(enemyStatusBar.IsFacingBadgeVisible);
        Assert.AreEqual("정후면 1.5x", enemyStatusBar.FacingBadgeDisplayText);

        // 실제 전투 시전(투사체 진행 방향 기준)과 오차 없이 일치하는지 검증
        int sessionId = CombatFacing.BeginCastSession(playerChar, projCard.cardData, playerTile.GetCoord(), LookDirection.East, playerTile.GetCoord());
        enemyChar.TakeDamage(new DamageInfo(playerChar, enemyChar, 10f, DamageFlag.Normal));
        CombatFacing.CompleteCastSession(sessionId, applyRotations: true);
        Assert.AreEqual(73f, enemyChar.curHealth, 0.001f, "미리보기 예상 피해(27)와 실제 피격 결과(100 -> 73)가 일치해야 합니다.");

        controller.ClearImmediateCardHoverPreview();
        Assert.IsFalse(enemyStatusBar.IsDamagePreviewVisible);
        Assert.IsFalse(enemyStatusBar.IsFacingBadgeVisible);
    }
}
