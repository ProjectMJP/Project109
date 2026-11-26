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
        Assert.IsNotNull(GetPrivateField<TextMeshProUGUI>(statusBar, "damagePreviewText"));
        Assert.IsNotNull(GetPrivateField<GameObject>(statusBar, "lethalIndicatorContainer"));
        Assert.IsNotNull(GetPrivateField<TextMeshProUGUI>(statusBar, "lethalIndicatorText"));
        Assert.IsNotNull(GetPrivateField<GameObject>(statusBar, "facingBadgeContainer"));
        Assert.IsNotNull(GetPrivateField<TextMeshProUGUI>(statusBar, "facingBadgeText"));

        Assert.IsNull(go.transform.Find("DamagePreviewContainer/DamagePreviewBackground"), "박스리스 개편 후 DamagePreviewBackground가 남아있지 않아야 합니다.");
        Assert.IsNull(go.transform.Find("FacingBadgeContainer/FacingBadgeBackground"), "박스리스 개편 후 FacingBadgeBackground가 남아있지 않아야 합니다.");

        GaugeUI healthGauge = GetPrivateField<GaugeUI>(statusBar, "healthGaugeUI");
        Assert.IsNotNull(healthGauge);
        Assert.IsNotNull(healthGauge.DamagePreviewOverlayImage, "HealthGauge에 DamagePreviewOverlayImage가 바인딩되어야 합니다.");
        Assert.IsFalse(healthGauge.DamagePreviewOverlayImage.gameObject.activeSelf);

        Assert.IsFalse(statusBar.IsDamagePreviewVisible);
        Assert.IsFalse(statusBar.IsLethalPreviewVisible);
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

        // 1. 정후면(플레이어 West -> East 보는 적) 타일 호버: -15 + BACK ATTACK 표시
        Vector2 enemyScreenPos = cam.WorldToScreenPoint(enemyTile.transform.position);
        InvokePrivateMethod(controller, "HandlePointerHover", enemyScreenPos);

        Assert.IsTrue(enemyStatusBar.IsDamagePreviewVisible);
        Assert.AreEqual("-15", enemyStatusBar.DamagePreviewDisplayText);
        Assert.IsTrue(enemyStatusBar.IsFacingBadgeVisible);
        Assert.AreEqual("BACK ATTACK", enemyStatusBar.FacingBadgeDisplayText);

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
        Assert.AreEqual("BACK ATTACK", enemyStatusBar.FacingBadgeDisplayText);

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
        Assert.AreEqual("BACK ATTACK", enemyStatusBar.FacingBadgeDisplayText);

        // 실제 전투 시전(투사체 진행 방향 기준)과 오차 없이 일치하는지 검증
        int sessionId = CombatFacing.BeginCastSession(playerChar, projCard.cardData, playerTile.GetCoord(), LookDirection.East, playerTile.GetCoord());
        enemyChar.TakeDamage(new DamageInfo(playerChar, enemyChar, 10f, DamageFlag.Normal));
        CombatFacing.CompleteCastSession(sessionId, applyRotations: true);
        Assert.AreEqual(73f, enemyChar.curHealth, 0.001f, "미리보기 예상 피해(27)와 실제 피격 결과(100 -> 73)가 일치해야 합니다.");

        controller.ClearImmediateCardHoverPreview();
        Assert.IsFalse(enemyStatusBar.IsDamagePreviewVisible);
        Assert.IsFalse(enemyStatusBar.IsFacingBadgeVisible);
    }

    [Test]
    public void GaugeUI_AndStatusBar_DamagePreviewGhostBar_ReflectsShieldAndLethalState()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterStatusBarUI.prefab");
        Assert.IsNotNull(prefab);

        Canvas canvas = CreateTestCanvas("GhostBarTestCanvas");
        GameObject go = Track(UnityEngine.Object.Instantiate(prefab, canvas.transform, false));
        CharacterStatusBarUI statusBar = go.GetComponent<CharacterStatusBarUI>();
        InvokePrivateMethod(statusBar, "Awake");

        GaugeUI healthGauge = GetPrivateField<GaugeUI>(statusBar, "healthGaugeUI");
        Assert.IsNotNull(healthGauge);

        // 1. 일반 체력 깎임 (100/100 에서 피해 30 -> [0.70, 1.00])
        statusBar.UpdateHealth(100f, 100f, 0f);
        statusBar.SetDamagePreview(new DamagePreviewInfo { isValid = true, expectedDamage = 30, facingRelation = FacingHitRelation.DirectFront, facingMultiplier = 1f });
        Assert.IsTrue(healthGauge.IsDamagePreviewActive);
        Assert.IsFalse(healthGauge.IsLethalPreview);
        Assert.IsFalse(statusBar.IsLethalPreviewVisible);
        Assert.AreEqual(0.70f, healthGauge.DamagePreviewStartRate, 0.001f);
        Assert.AreEqual(1.00f, healthGauge.DamagePreviewEndRate, 0.001f);
        Assert.IsTrue(healthGauge.DamagePreviewOverlayImage.gameObject.activeSelf);

        // 2. 실드 일부 흡수 (HP 60/100, Shield 10, 피해 25 -> HP 15만 깎임: [0.45, 0.60])
        statusBar.UpdateHealth(60f, 100f, 10f);
        statusBar.SetDamagePreview(new DamagePreviewInfo { isValid = true, expectedDamage = 25, facingRelation = FacingHitRelation.DirectFront, facingMultiplier = 1f });
        Assert.IsTrue(healthGauge.IsDamagePreviewActive);
        Assert.AreEqual(0.45f, healthGauge.DamagePreviewStartRate, 0.001f);
        Assert.AreEqual(0.60f, healthGauge.DamagePreviewEndRate, 0.001f);

        // 3. 실드 완전 흡수 (HP 60/100, Shield 30, 피해 20 -> HP 손실 0 -> 깎임 바 비활성)
        statusBar.UpdateHealth(60f, 100f, 30f);
        statusBar.SetDamagePreview(new DamagePreviewInfo { isValid = true, expectedDamage = 20, facingRelation = FacingHitRelation.DirectFront, facingMultiplier = 1f });
        Assert.IsFalse(healthGauge.IsDamagePreviewActive);
        Assert.IsFalse(healthGauge.DamagePreviewOverlayImage.gameObject.activeSelf);

        // 4. 처치 가능 (HP 20/100, Shield 5, 피해 25 -> HP 0 -> Lethal + KILL 표시)
        statusBar.UpdateHealth(20f, 100f, 5f);
        statusBar.SetDamagePreview(new DamagePreviewInfo { isValid = true, expectedDamage = 25, facingRelation = FacingHitRelation.DirectBack, facingMultiplier = 1.5f });
        Assert.IsTrue(healthGauge.IsDamagePreviewActive);
        Assert.IsTrue(healthGauge.IsLethalPreview);
        Assert.IsTrue(statusBar.IsLethalPreviewVisible);
        Assert.AreEqual("KILL", statusBar.LethalIndicatorDisplayText);
        Assert.IsTrue(statusBar.IsFacingBadgeVisible);
        Assert.AreEqual("BACK ATTACK", statusBar.FacingBadgeDisplayText);

        // 5. 초기화
        statusBar.ClearDamagePreview();
        Assert.IsFalse(healthGauge.IsDamagePreviewActive);
        Assert.IsFalse(statusBar.IsLethalPreviewVisible);
        Assert.IsFalse(healthGauge.DamagePreviewOverlayImage.gameObject.activeSelf);
    }

    [Test]
    public void BattleSideUnitCardUI_DamagePreview_UpdatesGhostBarTextAndBackAttackTag_AndClearsCleanly()
    {
        GameObject sideCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleSideUnitCardUI.prefab");
        Assert.IsNotNull(sideCardPrefab, "BattleSideUnitCardUI.prefab을 찾을 수 없습니다.");

        GameMap map = CreateTestGameMap(4, 4);
        Canvas canvas = CreateTestCanvas("SideCardPreviewCanvas");

        GameObject enemyGo = Track(new GameObject("PreviewSideEnemy"));
        Character enemyChar = enemyGo.AddComponent<Character>();
        InvokePrivateMethod(enemyChar, "Awake");
        enemyChar.faction = CharacterFaction.Enemy;
        enemyChar.InitializeStat(new CharacterStat { maxHealth = 100f, maxStamina = 100f, moveRange = 3 });
        enemyChar.curHealth = 45f;
        enemyChar.characterMove.SetCurrentTile(map.GetTile(1, 1), triggerTileHazards: false);
        _createdCharacters.Add(enemyChar);

        GameObject cardGo = Track(UnityEngine.Object.Instantiate(sideCardPrefab, canvas.transform, false));
        BattleSideUnitCardUI sideCard = cardGo.GetComponent<BattleSideUnitCardUI>();
        Assert.IsNotNull(sideCard);
        InvokePrivateMethod(sideCard, "Awake");

        sideCard.Bind(new StubEnemyController(enemyChar), isPlayerSide: false);
        Assert.AreEqual("45 / 100", sideCard.HealthDisplayText);
        Assert.IsFalse(sideCard.IsDamagePreviewActive);
        Assert.IsFalse(sideCard.IsBackAttackTagVisible);

        // 1. 정면 피해 15 -> "45 → 30 (-15)", Ghost Bar [0.30, 0.45], BACK ATTACK 태그 숨김
        enemyChar.UpdateDamagePreview(new DamagePreviewInfo
        {
            isValid = true,
            expectedDamage = 15,
            facingRelation = FacingHitRelation.DirectFront,
            facingMultiplier = 1.0f
        });
        Assert.IsTrue(sideCard.IsDamagePreviewActive);
        Assert.AreEqual("45 → 30 (-15)", sideCard.HealthDisplayText);
        Assert.IsFalse(sideCard.IsBackAttackTagVisible);

        GaugeUI healthGauge = GetPrivateField<GaugeUI>(sideCard, "healthGaugeUI");
        Assert.AreEqual(0.30f, healthGauge.DamagePreviewStartRate, 0.001f);
        Assert.AreEqual(0.45f, healthGauge.DamagePreviewEndRate, 0.001f);

        // 2. 백어택 피해 15 -> BACK ATTACK 태그 표시
        enemyChar.UpdateDamagePreview(new DamagePreviewInfo
        {
            isValid = true,
            expectedDamage = 15,
            facingRelation = FacingHitRelation.DirectBack,
            facingMultiplier = 1.5f
        });
        Assert.IsTrue(sideCard.IsBackAttackTagVisible);
        Assert.AreEqual("BACK ATTACK", sideCard.BackAttackTagDisplayText);

        // 3. 실드 10 보유 상태에서 피해 15 -> "(10→0) 45→40 (-15)"
        enemyChar.shield = 10f;
        Assert.AreEqual("(10→0) 45→40 (-15)", sideCard.HealthDisplayText);

        // 4. 미리보기 해제 -> 원래 수치 텍스트 및 태그 숨김 복구
        enemyChar.ClearDamagePreview();
        Assert.IsFalse(sideCard.IsDamagePreviewActive);
        Assert.IsFalse(sideCard.IsBackAttackTagVisible);
        Assert.AreEqual("(10) 45 / 100", sideCard.HealthDisplayText);
    }

    [Test]
    public void RendererPulseHighlighter_AndCharacterUIController_PulsesHostileRedAndFriendlyCyan_WithoutMaterialLeak()
    {
        GameMap map = CreateTestGameMap(4, 4);
        Canvas canvas = CreateTestCanvas("PulseHighlightCanvas");

        var (playerChar, playerCtrl, _) = CreateCharacterWithStatusBar("PulsePlayer", CharacterFaction.Player, map.GetTile(0, 0), LookDirection.East, canvas);
        var (enemyChar, enemyCtrl, _) = CreateCharacterWithStatusBar("PulseEnemy", CharacterFaction.Enemy, map.GetTile(1, 0), LookDirection.West, canvas);
        CreateBattleTurnContext(playerChar, enemyChar);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        Material sharedMat = Track(new Material(shader));

        GameObject enemyBody = Track(new GameObject("EnemyBody", typeof(MeshFilter), typeof(MeshRenderer)));
        enemyBody.transform.SetParent(enemyChar.transform, false);
        MeshRenderer enemyMesh = enemyBody.GetComponent<MeshRenderer>();
        enemyMesh.sharedMaterial = sharedMat;

        GameObject worldUiChild = Track(new GameObject("WorldUIChild", typeof(MeshFilter), typeof(MeshRenderer)));
        worldUiChild.transform.SetParent(enemyChar.transform, false);
        int worldUiLayer = LayerMask.NameToLayer("WorldUI");
        if (worldUiLayer >= 0) worldUiChild.layer = worldUiLayer;
        MeshRenderer worldUiMesh = worldUiChild.GetComponent<MeshRenderer>();
        worldUiMesh.sharedMaterial = sharedMat;

        GameObject playerBody = Track(new GameObject("PlayerBody", typeof(MeshFilter), typeof(MeshRenderer)));
        playerBody.transform.SetParent(playerChar.transform, false);
        MeshRenderer playerMesh = playerBody.GetComponent<MeshRenderer>();
        playerMesh.sharedMaterial = sharedMat;

        // 1. 적 타깃 지정 시 빨간색 계열 펄스 시작 및 시간 경과에 따른 색상 변화 검증
        enemyCtrl.SetTargetingHighlight(true);
        Assert.IsTrue(enemyCtrl.IsTargetingHighlighted);
        Assert.IsTrue(enemyCtrl.TargetingHighlighter.IsActive);

        MaterialPropertyBlock block0 = new MaterialPropertyBlock();
        enemyMesh.GetPropertyBlock(block0);
        Assert.IsFalse(block0.isEmpty);
        Color initialTint = enemyCtrl.TargetingHighlighter.LastAppliedTint;
        Color initialEmission = enemyCtrl.TargetingHighlighter.LastAppliedEmission;
        Assert.Greater(initialTint.r, initialTint.g);
        Assert.Greater(initialEmission.r, initialEmission.b);

        enemyCtrl.TickTargetingHighlight(0.22f);
        Color steppedTint = enemyCtrl.TargetingHighlighter.LastAppliedTint;
        Assert.AreNotEqual(initialTint, steppedTint, "시간이 경과하면 점멸 하이라이트 색상이 주기적으로 변화해야 합니다.");
        Assert.AreSame(sharedMat, enemyMesh.sharedMaterial, "MaterialPropertyBlock 사용 시 sharedMaterial 복제가 발생하지 않아야 합니다.");

        MaterialPropertyBlock worldUiBlock = new MaterialPropertyBlock();
        worldUiMesh.GetPropertyBlock(worldUiBlock);
        Assert.IsTrue(worldUiBlock.isEmpty, "WorldUI 레이어 렌더러에는 점멸 하이라이트가 적용되지 않아야 합니다.");

        // 2. 아군 타깃 지정 시 청록색 계열 펄스 검증
        playerCtrl.SetTargetingHighlight(true);
        Color allyTint = playerCtrl.TargetingHighlighter.LastAppliedTint;
        Assert.Greater(allyTint.g, allyTint.r);
        Assert.Greater(allyTint.b, allyTint.r);

        // 3. 해제 시 MaterialPropertyBlock 완전 정리
        enemyCtrl.SetTargetingHighlight(false);
        playerCtrl.SetTargetingHighlight(false);

        MaterialPropertyBlock clearedBlock = new MaterialPropertyBlock();
        enemyMesh.GetPropertyBlock(clearedBlock);
        Assert.IsTrue(clearedBlock.isEmpty);
        playerMesh.GetPropertyBlock(clearedBlock);
        Assert.IsTrue(clearedBlock.isEmpty);
    }
}
