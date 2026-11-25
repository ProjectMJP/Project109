using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XLua;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

[TestFixture]
public class EnemyIntentTests
{
    private sealed class StubPlayerController : ICharacterController
    {
        public Character controlledCharacter { get; }

        public StubPlayerController(Character character)
        {
            controlledCharacter = character;
        }

        public void OnTurnStart()
        {
            if (controlledCharacter != null && controlledCharacter.eventBus != null)
            {
                CombatEventDispatcher.DispatchTurnStart(controlledCharacter.eventBus);
            }
        }

        public void OnTurnEnd()
        {
            if (controlledCharacter != null && controlledCharacter.eventBus != null)
            {
                CombatEventDispatcher.DispatchTurnEnd(controlledCharacter.eventBus);
            }
        }
    }

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<LuaTable> _createdLuaTables = new List<LuaTable>();
    private readonly List<NPCUnitController> _createdNpcControllers = new List<NPCUnitController>();
    private readonly Dictionary<string, CardData> _cardDatabase = new Dictionary<string, CardData>();
    private LuaEnv _luaEnv;
    private IDeserializer _yamlDeserializer;
    private BattleManager _battleManager;

    [SetUp]
    public void SetUp()
    {
        _cardDatabase.Clear();
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
                DefaultDamageTypeID = CS.EventStructs.DefaultDamageTypeID,
            }
            UnityEngine = {
                Debug = CS.UnityEngine.Debug,
                Mathf = CS.UnityEngine.Mathf,
                Random = CS.UnityEngine.Random,
                Vector3 = CS.UnityEngine.Vector3,
                Vector2 = CS.UnityEngine.Vector2,
                Vector2Int = CS.UnityEngine.Vector2Int,
            }
            RunManager = CS.RunManager
            CS = nil
        ");

        LoadCardIntoLookup("몬스터 강타");
        LoadCardIntoLookup("방어");
    }

    [TearDown]
    public void TearDown()
    {
        ActionQueueManager aqm = UnityEngine.Object.FindFirstObjectByType<ActionQueueManager>();
        if (aqm != null)
        {
            aqm.ClearQueue();
            UnityEngine.Object.DestroyImmediate(aqm.gameObject);
        }
        SetStaticField<ActionQueueManager>("_instance", null);

        if (_battleManager != null)
        {
            _battleManager.Dispose();
            _battleManager = null;
        }

        for (int i = _createdNpcControllers.Count - 1; i >= 0; i--)
        {
            _createdNpcControllers[i]?.Dispose();
        }
        _createdNpcControllers.Clear();

        SetStaticAutoProperty<RunManager>("instance", null);
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
        _cardDatabase.Clear();
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null) _createdObjects.Add(obj);
        return obj;
    }

    private CardData LoadCardIntoLookup(string cardId)
    {
        string yamlPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "YAML", "Cards", cardId + ".yaml");
        string luaPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Cards", cardId + ".lua");

        CardData cardData = _yamlDeserializer.Deserialize<CardData>(File.ReadAllText(yamlPath));
        cardData.parsedTargetType = CardData.ParseTargetType(cardData.targetType);

        object[] res = _luaEnv.DoString(File.ReadAllText(luaPath), cardId);
        LuaTable table = res[0] as LuaTable;
        _createdLuaTables.Add(table);
        cardData.luaPrototype = table;

        _cardDatabase[cardId] = cardData;
        return cardData;
    }

    private NPCUnitData LoadEnemyData(string unitId)
    {
        string yamlPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "YAML", "Enemies", unitId + ".yaml");
        string luaPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Enemies", unitId + ".lua");

        NPCUnitData unitData = _yamlDeserializer.Deserialize<NPCUnitData>(File.ReadAllText(yamlPath));
        object[] res = _luaEnv.DoString(File.ReadAllText(luaPath), unitId);
        LuaTable table = res[0] as LuaTable;
        _createdLuaTables.Add(table);
        unitData.luaPrototype = table;

        return unitData;
    }

    private Effect AttachEffectFromCore(Character target, string effectId, int stack, float duration)
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

    private (GameMap map, RunManager runManager, BattleManager battleManager) CreateBattleEnvironment(int width = 6, int height = 5)
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

        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);

        StageManager stageManager = new StageManager();
        stageManager.Initialize(gameMap);
        runManager.currentStage = stageManager;

        _battleManager = new BattleManager();
        _battleManager.Initialize();
        typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public)?.SetValue(runManager, _battleManager);

        return (gameMap, runManager, _battleManager);
    }

    private Character CreateCombatCharacter(string name, CharacterFaction faction, CharacterStat stat, Tile startTile)
    {
        GameObject go = Track(new GameObject(name));
        Character character = go.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = faction;
        character.InitializeStat(stat);
        if (startTile != null)
        {
            character.characterMove.SetCurrentTile(startTile);
        }
        return character;
    }

    private CardData LookupCard(string cardId)
    {
        return _cardDatabase.TryGetValue(cardId, out CardData data) ? data : null;
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"메서드 '{methodName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        method.Invoke(target, null);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"필드 '{fieldName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        return (T)field.GetValue(target);
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
    public void SkeletonGuard_Turn1Strike_Turn2Defend_IntentMatchesLockedExecution()
    {
        var (gameMap, _, battleManager) = CreateBattleEnvironment();
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        NPCUnitData guardData = LoadEnemyData("SkeletonGuard");
        Character player = CreateCombatCharacter("Player", CharacterFaction.Player, new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 20f,
            moveRange = 3
        }, tileMap[2][2]);

        Character guard = CreateCombatCharacter("SkeletonGuard", CharacterFaction.Enemy, guardData.characterStat, tileMap[1][2]);

        var playerCtrl = new StubPlayerController(player);
        var guardCtrl = new NPCUnitController(guard, guardData, LookupCard);
        _createdNpcControllers.Add(guardCtrl);

        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { guardCtrl });
        battleManager.StartCombat();
        guardCtrl.EvaluateNextIntent();

        // 1턴 예고 (currentTurn == 0): "몬스터 강타" 고정
        Assert.IsTrue(guard.CurrentIntent.isValid);
        Assert.AreEqual("몬스터 강타", guard.CurrentIntent.cardId);
        Assert.AreEqual(EnemyIntentType.Attack, guard.CurrentIntent.intentType);
        Assert.AreEqual(6, guard.CurrentIntent.estimatedValue);

        // 1턴 시작: currentTurn이 1로 증가해도 사전 고정된 "몬스터 강타"를 실행해야 함
        guard.curStamina = guard.curCharacterStat.maxStamina;
        battleManager.RequestTurnStart(guard);
        Assert.AreEqual(1, guard.currentTurn);

        ActionQueueManager aqm = ActionQueueManager.Instance;
        BattleAction currentAction = GetPrivateField<BattleAction>(aqm, "_currentAction");
        CardPlayAction turn1Play = currentAction as CardPlayAction;
        Assert.IsNotNull(turn1Play, "1턴 시작 시 CardPlayAction이 실행 중이어야 합니다.");
        Assert.AreEqual("몬스터 강타", turn1Play.card.cardName);

        // 1턴 종료 콜백(EndNPCTurn) 실행 -> currentTurn == 1이므로 다음 2턴 예고는 "방어"로 고정
        aqm.ClearQueue();
        Assert.AreEqual(BattleState.Combat, battleManager.battleState);
        Assert.IsTrue(guard.CurrentIntent.isValid);
        Assert.AreEqual("방어", guard.CurrentIntent.cardId);
        Assert.AreEqual(EnemyIntentType.Defend, guard.CurrentIntent.intentType);
        Assert.AreEqual(8, guard.CurrentIntent.estimatedValue);

        // 2턴 시작: currentTurn이 2로 증가해도 고정된 "방어"가 실행되어야 함
        guard.curStamina = guard.curCharacterStat.maxStamina;
        battleManager.RequestTurnStart(guard);
        Assert.AreEqual(2, guard.currentTurn);

        BattleAction turn2Action = GetPrivateField<BattleAction>(aqm, "_currentAction");
        CardPlayAction turn2Play = turn2Action as CardPlayAction;
        Assert.IsNotNull(turn2Play, "2턴 시작 시 CardPlayAction이 실행 중이어야 합니다.");
        Assert.AreEqual("방어", turn2Play.card.cardName);

        // 2턴 종료 -> currentTurn == 2이므로 3턴 예고는 다시 "몬스터 강타"
        aqm.ClearQueue();
        Assert.AreEqual("몬스터 강타", guard.CurrentIntent.cardId);
        Assert.AreEqual(EnemyIntentType.Attack, guard.CurrentIntent.intentType);
    }

    [Test]
    public void DamageModifiers_StrengthWeaknessVulnerable_UpdateEstimatedValueInRealTime()
    {
        var (gameMap, _, battleManager) = CreateBattleEnvironment();
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        NPCUnitData guardData = LoadEnemyData("SkeletonGuard");
        Character player = CreateCombatCharacter("Player", CharacterFaction.Player, new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 20f,
            moveRange = 3
        }, tileMap[2][2]);

        Character guard = CreateCombatCharacter("SkeletonGuard", CharacterFaction.Enemy, guardData.characterStat, tileMap[1][2]);

        var playerCtrl = new StubPlayerController(player);
        var guardCtrl = new NPCUnitController(guard, guardData, LookupCard);
        _createdNpcControllers.Add(guardCtrl);

        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { guardCtrl });
        battleManager.StartCombat();
        guardCtrl.EvaluateNextIntent();

        int intentEventCount = 0;
        guard.OnIntentChanged += _ => intentEventCount++;

        Assert.AreEqual(6, guard.CurrentIntent.estimatedValue);

        // 1. 적에게 힘(Strength) +2 부여 -> 6 + 2 = 8
        AttachEffectFromCore(guard, "Strength", 2, 0f);
        Assert.AreEqual(8, guard.CurrentIntent.estimatedValue);
        Assert.Greater(intentEventCount, 0);

        // 2. 적에게 약화(Weakness) 부여 -> (6 + 2) * 0.75 = 6
        Effect weakness = AttachEffectFromCore(guard, "Weakness", 1, 5f);
        Assert.AreEqual(6, guard.CurrentIntent.estimatedValue);

        // 3. 플레이어에게 취약(Vulnerable) 부여 -> (6 + 2) * 0.75 * 1.5 = 9
        AttachEffectFromCore(player, "Vulnerable", 1, 5f);
        Assert.AreEqual(9, guard.CurrentIntent.estimatedValue);

        // 4. 적 약화 제거 -> (6 + 2) * 1.5 = 12
        guard.effectManager.RemoveEffect(weakness);
        Assert.AreEqual(12, guard.CurrentIntent.estimatedValue);
    }

    [Test]
    public void StunEffect_TransitionsToStunned_AndRestoresLockedIntentWhenRemoved()
    {
        var (gameMap, _, battleManager) = CreateBattleEnvironment();
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        NPCUnitData guardData = LoadEnemyData("SkeletonGuard");
        Character player = CreateCombatCharacter("Player", CharacterFaction.Player, new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 20f,
            moveRange = 3
        }, tileMap[2][2]);

        Character guard = CreateCombatCharacter("SkeletonGuard", CharacterFaction.Enemy, guardData.characterStat, tileMap[1][2]);

        var playerCtrl = new StubPlayerController(player);
        var guardCtrl = new NPCUnitController(guard, guardData, LookupCard);
        _createdNpcControllers.Add(guardCtrl);

        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { guardCtrl });
        battleManager.StartCombat();
        guardCtrl.EvaluateNextIntent();

        Assert.AreEqual(EnemyIntentType.Attack, guard.CurrentIntent.intentType);
        Assert.AreEqual("몬스터 강타", guard.CurrentIntent.cardId);

        // 기절 부여 시 Stunned 전환, 기존 고정 카드 ID 유지
        Effect stun = AttachEffectFromCore(guard, "Stun", 1, 1f);
        Assert.AreEqual(EnemyIntentType.Stunned, guard.CurrentIntent.intentType);
        Assert.AreEqual(0, guard.CurrentIntent.estimatedValue);
        Assert.AreEqual("몬스터 강타", guard.CurrentIntent.cardId);

        // 기절 해제 시 기존 고정 의도("몬스터 강타", Attack 6)로 복원
        guard.effectManager.RemoveEffect(stun);
        Assert.AreEqual(EnemyIntentType.Attack, guard.CurrentIntent.intentType);
        Assert.AreEqual("몬스터 강타", guard.CurrentIntent.cardId);
        Assert.AreEqual(6, guard.CurrentIntent.estimatedValue);
    }

    [Test]
    public void PlayerMovesOutOfRange_ReevaluatesAttackToMoveOnly_AndExecutesMoveWithoutCardPlay()
    {
        var (gameMap, _, battleManager) = CreateBattleEnvironment(width: 6, height: 5);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        NPCUnitData guardData = LoadEnemyData("SkeletonGuard"); // moveRange = 2
        Character player = CreateCombatCharacter("Player", CharacterFaction.Player, new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 20f,
            moveRange = 3
        }, tileMap[1][2]);

        Character guard = CreateCombatCharacter("SkeletonGuard", CharacterFaction.Enemy, guardData.characterStat, tileMap[0][2]);

        var playerCtrl = new StubPlayerController(player);
        var guardCtrl = new NPCUnitController(guard, guardData, LookupCard);
        _createdNpcControllers.Add(guardCtrl);

        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { guardCtrl });
        battleManager.StartCombat();
        guardCtrl.EvaluateNextIntent();

        // 초기 거리 1 -> 제자리 공격 예고
        Assert.AreEqual(EnemyIntentType.Attack, guard.CurrentIntent.intentType);
        Assert.IsFalse(guard.CurrentIntent.requiresMove);
        Assert.IsTrue(guard.CurrentIntent.canReachTarget);

        // 플레이어가 (3, 2)로 이동 -> 적 이동력 2로 (2, 2)까지 접근 후 공격 가능 (AttackMove)
        player.characterMove.SetCurrentTile(tileMap[3][2]);
        Assert.AreEqual(EnemyIntentType.AttackMove, guard.CurrentIntent.intentType);
        Assert.IsTrue(guard.CurrentIntent.requiresMove);
        Assert.IsTrue(guard.CurrentIntent.canReachTarget);
        Assert.AreEqual(tileMap[2][2], guard.CurrentIntent.plannedMoveTile);
        Assert.AreEqual(6, guard.CurrentIntent.estimatedValue);

        // 플레이어가 (4, 2)로 추가 이탈 -> 적 이동력 2로 (2, 2)까지만 이동 가능하므로 사거리 미달 (MoveOnly)
        player.characterMove.SetCurrentTile(tileMap[4][2]);
        Assert.AreEqual(EnemyIntentType.MoveOnly, guard.CurrentIntent.intentType);
        Assert.IsTrue(guard.CurrentIntent.requiresMove);
        Assert.IsFalse(guard.CurrentIntent.canReachTarget);
        Assert.AreEqual(tileMap[2][2], guard.CurrentIntent.plannedMoveTile);
        Assert.AreEqual(0, guard.CurrentIntent.estimatedValue);

        // 적 턴 시작 시 이동 액션만 적재되고 CardPlayAction은 적재되지 않아야 함
        guard.curStamina = guard.curCharacterStat.maxStamina;
        battleManager.RequestTurnStart(guard);

        ActionQueueManager aqm = ActionQueueManager.Instance;
        BattleAction currentAction = GetPrivateField<BattleAction>(aqm, "_currentAction");
        EnemyMoveAction moveAction = currentAction as EnemyMoveAction;
        Assert.IsNotNull(moveAction, "사거리 미달 시 EnemyMoveAction만 실행되어야 합니다.");
        Assert.AreEqual(tileMap[2][2], moveAction.TargetTile);

        Queue<BattleAction> remainingQueue = GetPrivateField<Queue<BattleAction>>(aqm, "_actionQueue");
        foreach (BattleAction queued in remainingQueue)
        {
            Assert.IsFalse(queued is CardPlayAction, "MoveOnly 상태에서는 CardPlayAction이 큐에 적재되면 안 됩니다.");
        }

        Assert.AreEqual(100f, player.curHealth, 0.001f);
    }

    [Test]
    public void DisarmAndIntentTypeOverride_HandledConsistentWithStaminaAndYamlSpec()
    {
        var (gameMap, _, battleManager) = CreateBattleEnvironment(width: 6, height: 5);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        NPCUnitData guardData = LoadEnemyData("SkeletonGuard");
        Character player = CreateCombatCharacter("Player", CharacterFaction.Player, new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 20f,
            moveRange = 3
        }, tileMap[1][2]);

        Character guard = CreateCombatCharacter("SkeletonGuard", CharacterFaction.Enemy, guardData.characterStat, tileMap[0][2]);

        var playerCtrl = new StubPlayerController(player);
        var guardCtrl = new NPCUnitController(guard, guardData, LookupCard);
        _createdNpcControllers.Add(guardCtrl);

        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { guardCtrl });
        battleManager.StartCombat();
        guardCtrl.EvaluateNextIntent();

        // 근접 상태(이동 불필요)에서 무장해제(Disarm) 적용 시 Attack 불가 -> Stunned 예고
        Effect disarm = AttachEffectFromCore(guard, "Disarm", 1, 1f);
        Assert.AreEqual(EnemyIntentType.Stunned, guard.CurrentIntent.intentType);

        // 플레이어가 거리를 벌리면 이동은 가능하므로 MoveOnly로 전환
        player.characterMove.SetCurrentTile(tileMap[3][2]);
        Assert.AreEqual(EnemyIntentType.MoveOnly, guard.CurrentIntent.intentType);
        Assert.IsTrue(guard.CurrentIntent.requiresMove);

        // 무장해제 제거 시 AttackMove 복원
        guard.effectManager.RemoveEffect(disarm);
        Assert.AreEqual(EnemyIntentType.AttackMove, guard.CurrentIntent.intentType);

        // CardData.intentType YAML 오버라이드 우선 적용 확인
        CardData strikeData = LookupCard("몬스터 강타");
        strikeData.intentType = "Special";
        guardCtrl.RefreshIntentState();
        Assert.AreEqual(EnemyIntentType.Special, guard.CurrentIntent.intentType);
    }

    [Test]
    public void UnreachableWithZeroMoveOrNoHostile_ReturnsStunnedOrNone_WithoutExecutingCardPlay()
    {
        var (gameMap, _, battleManager) = CreateBattleEnvironment(width: 6, height: 5);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        NPCUnitData guardData = LoadEnemyData("SkeletonGuard");
        guardData.characterStat.moveRange = 0;

        Character player = CreateCombatCharacter("Player", CharacterFaction.Player, new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 20f,
            moveRange = 3
        }, tileMap[3][2]);

        Character guard = CreateCombatCharacter("SkeletonGuard", CharacterFaction.Enemy, guardData.characterStat, tileMap[0][2]);

        var playerCtrl = new StubPlayerController(player);
        var guardCtrl = new NPCUnitController(guard, guardData, LookupCard);
        _createdNpcControllers.Add(guardCtrl);

        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { guardCtrl });
        battleManager.StartCombat();
        guardCtrl.EvaluateNextIntent();

        // 1. 이동력 0 + 사거리 밖 -> Special이 아닌 Stunned(행동 불가 대기)로 예고되어야 함
        Assert.AreEqual(EnemyIntentType.Stunned, guard.CurrentIntent.intentType);
        Assert.IsTrue(guard.CurrentIntent.isValid);
        Assert.IsFalse(guard.CurrentIntent.requiresMove);
        Assert.IsFalse(guard.CurrentIntent.canReachTarget);
        Assert.AreEqual(0, guard.CurrentIntent.estimatedValue);

        // 2. 적대 대상이 없을 때(hostileTarget == null, 타깃 필요 카드 "몬스터 강타" 고정 상태) -> None (isValid = false) 반환
        player.isDead = true;
        guardCtrl.RefreshIntentState();
        Assert.AreEqual(EnemyIntentType.None, guard.CurrentIntent.intentType);
        Assert.IsFalse(guard.CurrentIntent.isValid);
        Assert.AreEqual(0, guard.CurrentIntent.estimatedValue);

        // 대상 복구 후 실제 턴 시작 시 CardPlayAction/EnemyMoveAction을 적재하지 않고 즉시 턴을 종료해야 함
        player.isDead = false;
        guardCtrl.RefreshIntentState();
        Assert.AreEqual(EnemyIntentType.Stunned, guard.CurrentIntent.intentType);

        guard.curStamina = guard.curCharacterStat.maxStamina;
        battleManager.RequestTurnStart(guard);
        Assert.AreEqual(BattleState.Combat, battleManager.battleState);
        Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<ActionQueueManager>(), "이동·공격 불가 상태에서는 ActionQueueManager에 액션이 적재되면 안 됩니다.");
        Assert.AreEqual(100f, player.curHealth, 0.001f);
    }

    [Test]
    public void DisposeAndCleanupCombatants_PreventsCardPoolLeakAndResubscriptionAfterDispose()
    {
        var (gameMap, _, battleManager) = CreateBattleEnvironment(width: 6, height: 5);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        NPCUnitData guardData = LoadEnemyData("SkeletonGuard");
        Character player = CreateCombatCharacter("Player", CharacterFaction.Player, new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 20f,
            moveRange = 3
        }, tileMap[2][2]);

        Character guard = CreateCombatCharacter("SkeletonGuard", CharacterFaction.Enemy, guardData.characterStat, tileMap[1][2]);

        var playerCtrl = new StubPlayerController(player);
        var guardCtrl = new NPCUnitController(guard, guardData, LookupCard);
        _createdNpcControllers.Add(guardCtrl);

        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { guardCtrl });
        battleManager.StartCombat();
        guardCtrl.EvaluateNextIntent();

        AttachEffectFromCore(guard, "Strength", 2, 0f);
        Assert.AreEqual(8, guard.CurrentIntent.estimatedValue);

        Dictionary<string, Card> cardPool = GetPrivateField<Dictionary<string, Card>>(guardCtrl, "_cardPool");
        Assert.Greater(cardPool.Count, 0);

        // 생존 상태에서 선행 Dispose 후 BattleManager.ClearBattle()이 실행되어도 카드 풀 재생성 누수가 없어야 함
        guardCtrl.Dispose();
        Assert.AreEqual(0, cardPool.Count);

        guardCtrl.RefreshIntentState();
        Assert.AreEqual(0, cardPool.Count);
        Assert.AreEqual(EnemyIntentType.None, guardCtrl.CurrentIntent.intentType);
        Assert.IsFalse(guardCtrl.CurrentIntent.isValid);

        battleManager.ClearBattle();
        Assert.AreEqual(0, cardPool.Count);
        Assert.AreEqual(0, guard.effectManager.GetEffects().Count);
    }

    [Test]
    public void MoveOnlyEarlyTurnEnd_ReturnsHalfStaminaCarryover_NoMoveBonus_AndReevaluatesNextIntent()
    {
        var (gameMap, _, battleManager) = CreateBattleEnvironment(width: 6, height: 5);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        NPCUnitData guardData = LoadEnemyData("SkeletonGuard"); // moveRange = 2, maxStamina = 100
        Character player = CreateCombatCharacter("Player", CharacterFaction.Player, new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 20f,
            moveRange = 3
        }, tileMap[4][2]);

        Character guard = CreateCombatCharacter("SkeletonGuard", CharacterFaction.Enemy, guardData.characterStat, tileMap[0][2]);

        var playerCtrl = new StubPlayerController(player);
        var guardCtrl = new NPCUnitController(guard, guardData, LookupCard);
        _createdNpcControllers.Add(guardCtrl);

        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { guardCtrl });
        battleManager.StartCombat();
        guardCtrl.EvaluateNextIntent();

        Assert.AreEqual(EnemyIntentType.MoveOnly, guard.CurrentIntent.intentType);

        // 1턴 시작: 사거리 미달(MoveOnly) -> 이동만 적재하고 턴 조기 종료
        guard.curStamina = guard.curCharacterStat.maxStamina;
        battleManager.RequestTurnStart(guard);
        Assert.AreEqual(1, guard.currentTurn);

        ActionQueueManager aqm = ActionQueueManager.Instance;
        BattleAction currentAction = GetPrivateField<BattleAction>(aqm, "_currentAction");
        Assert.IsInstanceOf<EnemyMoveAction>(currentAction);

        // 이동 완료 후 조기 종료 콜백 실행
        aqm.ClearQueue(invokeCanceled: true);

        Assert.AreEqual(BattleState.Combat, battleManager.battleState);
        Assert.AreEqual(guard.curCharacterStat.maxStamina * 0.5f, guard.curStamina, 0.001f,
            "조기 턴 종료 시 플레이어와 동일하게 스태미나 50%가 이월되어야 합니다.");
        Assert.AreEqual(100f, player.curHealth, 0.001f);
        Assert.AreEqual("방어", guard.CurrentIntent.cardId,
            "조기 종료 후 다음 턴 의도가 강제 고정되지 않고 현재 턴 규칙에 맞게 재결정되어야 합니다.");
        Assert.AreEqual(EnemyIntentType.Defend, guard.CurrentIntent.intentType);

        // 다음 턴 시작 시 이동력 이월 보너스 없이 기본 moveRange로 초기화되는지 검증
        guard.curMoveRange = 0;
        guard.curStamina = guard.curCharacterStat.maxStamina;
        battleManager.RequestTurnStart(guard);
        Assert.AreEqual(guardData.characterStat.moveRange, guard.curMoveRange,
            "적 AI 조기 종료 시 추가 이동력 이월 보너스는 부여되지 않아야 합니다.");
    }

    [Test]
    public void PostMoveInterruptedOutOfRange_SkipsCardPlay_AppliesHalfStaminaCarryover_AndReevaluatesIntent()
    {
        var (gameMap, _, battleManager) = CreateBattleEnvironment(width: 6, height: 5);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        NPCUnitData guardData = LoadEnemyData("SkeletonGuard"); // moveRange = 2
        Character player = CreateCombatCharacter("Player", CharacterFaction.Player, new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 20f,
            moveRange = 3
        }, tileMap[3][2]);

        Character guard = CreateCombatCharacter("SkeletonGuard", CharacterFaction.Enemy, guardData.characterStat, tileMap[0][2]);

        var playerCtrl = new StubPlayerController(player);
        var guardCtrl = new NPCUnitController(guard, guardData, LookupCard);
        _createdNpcControllers.Add(guardCtrl);

        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { guardCtrl });
        battleManager.StartCombat();
        guardCtrl.EvaluateNextIntent();

        // 초기 거리 3 -> (2, 2)로 이동 후 공격 예정 (AttackMove)
        Assert.AreEqual(EnemyIntentType.AttackMove, guard.CurrentIntent.intentType);

        guard.curStamina = guard.curCharacterStat.maxStamina;
        battleManager.RequestTurnStart(guard);
        Assert.AreEqual(1, guard.currentTurn);

        ActionQueueManager aqm = ActionQueueManager.Instance;
        BattleAction currentAction = GetPrivateField<BattleAction>(aqm, "_currentAction");
        Assert.IsInstanceOf<EnemyMoveAction>(currentAction, "이동이 필요한 AttackMove는 EnemyMoveAction을 먼저 실행해야 합니다.");

        Queue<BattleAction> queuedActions = GetPrivateField<Queue<BattleAction>>(aqm, "_actionQueue");
        Assert.AreEqual(1, queuedActions.Count, "이동 완료 전에는 CardPlayAction 대신 사후 판정 CustomCallbackAction만 대기해야 합니다.");
        Assert.IsInstanceOf<CustomCallbackAction>(queuedActions.Peek());

        // 이동이 중간 차단되어 제자리 (0, 2)에 남았다고 가정하고 사후 판정 호출
        Card plannedCard = guard.CurrentIntent.plannedCard;
        aqm.ClearQueue(invokeCanceled: false);
        MethodInfo postMoveMethod = typeof(NPCUnitController).GetMethod("HandlePostMoveDecision", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(postMoveMethod);
        postMoveMethod.Invoke(guardCtrl, new object[] { plannedCard });

        Assert.AreEqual(BattleState.Combat, battleManager.battleState);
        Assert.IsFalse(aqm.IsBusy, "이동 후 사거리 미달 시 CardPlayAction을 적재하지 않고 즉시 조기 종료해야 합니다.");
        Assert.AreEqual(100f, player.curHealth, 0.001f);
        Assert.AreEqual(guard.curCharacterStat.maxStamina * 0.5f, guard.curStamina, 0.001f);
        Assert.AreEqual("방어", guard.CurrentIntent.cardId);
    }

    [Test]
    public void AreaSelfAttackCard_WhenOutOfRange_ResolvesToMoveOnlyIntent()
    {
        var (gameMap, _, battleManager) = CreateBattleEnvironment(width: 6, height: 5);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        CardData areaAttack = new CardData
        {
            cardName = "테스트 주변 공격",
            cardType = "Attack",
            targetType = "AreaSelf",
            targetMinDistance = 1,
            targetMaxDistance = 1,
            stamina = 30,
            baseValues = new Dictionary<string, float> { { "damage", 8f } }
        };
        _cardDatabase[areaAttack.cardName] = areaAttack;

        NPCUnitData guardData = LoadEnemyData("SkeletonGuard"); // moveRange = 2
        LuaTable customAi = _luaEnv.DoString(@"
            local ai = {}
            function ai:ChooseCard(character) return '테스트 주변 공격' end
            return ai
        ")[0] as LuaTable;
        _createdLuaTables.Add(customAi);
        guardData.luaPrototype = customAi;

        Character player = CreateCombatCharacter("Player", CharacterFaction.Player, new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 20f,
            moveRange = 3
        }, tileMap[4][2]);

        Character guard = CreateCombatCharacter("SkeletonGuard", CharacterFaction.Enemy, guardData.characterStat, tileMap[0][2]);

        var playerCtrl = new StubPlayerController(player);
        var guardCtrl = new NPCUnitController(guard, guardData, LookupCard);
        _createdNpcControllers.Add(guardCtrl);

        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { guardCtrl });
        battleManager.StartCombat();
        guardCtrl.EvaluateNextIntent();

        Assert.AreEqual(EnemyIntentType.MoveOnly, guard.CurrentIntent.intentType,
            "공격형 AreaSelf 카드도 이동 후 사거리 내 적이 없으면 MoveOnly로 판정되어 조기 종료해야 합니다.");
        Assert.IsFalse(guard.CurrentIntent.canReachTarget);
    }
}
