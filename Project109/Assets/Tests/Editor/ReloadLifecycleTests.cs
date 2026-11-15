using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class ReloadLifecycleTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        SetStaticAutoProperty<GameSceneManager>("instance", null);
        SetStaticAutoProperty<RunManager>("instance", null);
        SetStaticAutoProperty<AssetCacheManager>("instance", null);

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
        if (obj != null) _createdObjects.Add(obj);
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

    private (GameSceneManager gsm, Player player, Character character) CreateMockGameScenePlayer()
    {
        GameObject gsmGo = Track(new GameObject("TestGameSceneManager"));
        GameSceneManager gsm = gsmGo.AddComponent<GameSceneManager>();
        SetStaticAutoProperty<GameSceneManager>("instance", gsm);

        GameObject charGo = Track(new GameObject("TestPlayerCharacter"));
        Character character = charGo.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 10f,
            staminaRegenPerSecond = 1f,
            moveRange = 3
        });

        Player player = new Player(character);
        gsm.player = player;
        return (gsm, player, character);
    }

    private RunManager CreateMockRunManager(bool callAwake)
    {
        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        if (callAwake)
        {
            InvokePrivateMethod(runManager, "Awake");
        }
        else
        {
            SetStaticAutoProperty<RunManager>("instance", runManager);
            runManager.exploreMapManager = new ExploreMapManager();
            runManager.currentStage = new StageManager();
            BattleManager bm = new BattleManager();
            bm.Initialize();
            typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public)?.SetValue(runManager, bm);
        }
        return runManager;
    }

    private static int GetTouchClickSubscriberCount(PlayerInputController inputCtrl)
    {
        FieldInfo field = typeof(PlayerInputController).GetField("OnTouchClickEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        Delegate del = field != null ? field.GetValue(inputCtrl) as Delegate : null;
        return del != null ? del.GetInvocationList().Length : 0;
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
    public void PlayerExploreController_Activate_IsIdempotent_AndDeactivateUnsubscribesCleanly()
    {
        PlayerInputController inputCtrl = CreateMockInputController();
        var (_, player, _) = CreateMockGameScenePlayer();
        PlayerExploreController exploreCtrl = new PlayerExploreController(player);

        Assert.AreEqual(0, GetTouchClickSubscriberCount(inputCtrl));

        exploreCtrl.Activate();
        exploreCtrl.Activate();
        Assert.AreEqual(1, GetTouchClickSubscriberCount(inputCtrl));

        exploreCtrl.Deactivate();
        Assert.AreEqual(0, GetTouchClickSubscriberCount(inputCtrl));
    }

    [Test]
    public void RunManager_PreStart_ExploreNodeResume_ActivatesExploreControllerAndSurvivesStart()
    {
        PlayerInputController inputCtrl = CreateMockInputController();
        CreateMockGameScenePlayer();
        RunManager runManager = CreateMockRunManager(callAwake: false);

        // 미클리어 탐색(상점) 노드가 복원된 직후 Start() 이전 시점 시뮬레이션
        ExploreMapData mapData = new ExploreMapData();
        ExploreFloorData floor0 = new ExploreFloorData();
        ExploreNode storeNode = new ExploreNode(0, 0)
        {
            isRevealed = true,
            isCleared = false
        };
        storeNode.encounterData.encountType = EncountType.Store;
        floor0.nodes.Add(storeNode);
        mapData.floors.Add(floor0);

        RunSaveData saveData = new RunSaveData
        {
            isInDungeon = true,
            currentMapName = "Temple",
            currentStageLevel = 1,
            currentExploreMapFloor = 0,
            currentExploreNodeFloor = 0,
            currentExploreNodeIndex = 0,
            exploreMapData = mapData
        };

        SaveSystem.RestoreRunData(saveData, runManager);
        Assert.AreSame(storeNode, runManager.currentExploreNode);

        // Start() 이전 OnMapStateChanged(None) 호출 시 선초기화 + 탐색 입력 활성화 보장
        runManager.OnMapStateChanged(MapState.None);

        Assert.IsNotNull(runManager.playerExploreController);
        Assert.IsNotNull(runManager.playerBattleController);
        Assert.AreSame(runManager.playerExploreController, runManager.activePlayerController);
        Assert.AreEqual(1, GetTouchClickSubscriberCount(inputCtrl));

        PlayerExploreController createdExplore = runManager.playerExploreController;
        PlayerBattleController createdBattle = runManager.playerBattleController;

        // 다음 프레임 Start()가 실행되더라도 컨트롤러를 재생성하거나 중복 구독하지 않음
        InvokePrivateMethod(runManager, "Start");

        Assert.AreSame(createdExplore, runManager.playerExploreController);
        Assert.AreSame(createdBattle, runManager.playerBattleController);
        Assert.AreSame(createdExplore, runManager.activePlayerController);
        Assert.AreEqual(1, GetTouchClickSubscriberCount(inputCtrl));
    }

    [Test]
    public void RunManager_PreStart_BattleStateTransition_DoesNotGetOverwrittenByStart()
    {
        PlayerInputController inputCtrl = CreateMockInputController();
        CreateMockGameScenePlayer();
        RunManager runManager = CreateMockRunManager(callAwake: false);
        runManager.currentStage.currentMapState = MapState.Battle;

        // Start() 이전 시점에 전투 상태로 진입
        runManager.OnMapStateChanged(MapState.Battle);

        Assert.IsNotNull(runManager.playerBattleController);
        Assert.IsNotNull(runManager.playerExploreController);
        Assert.AreSame(runManager.playerBattleController, runManager.activePlayerController);
        Assert.AreEqual(0, GetTouchClickSubscriberCount(inputCtrl));

        PlayerBattleController createdBattle = runManager.playerBattleController;

        // 다음 프레임 Start()가 실행되어도 Battle 활성 컨트롤러와 상태를 유지해야 함
        InvokePrivateMethod(runManager, "Start");

        Assert.AreSame(createdBattle, runManager.playerBattleController);
        Assert.AreSame(createdBattle, runManager.activePlayerController);
        Assert.AreEqual(0, GetTouchClickSubscriberCount(inputCtrl));
    }

    [Test]
    public void BattleManager_InitBattle_WithNullCombatants_FailsFastWithoutException()
    {
        BattleManager battleManager = new BattleManager();
        battleManager.Initialize();

        LogAssert.Expect(LogType.Error, "[BattleManager] InitBattle: playerTeam에 null 컨트롤러 또는 controlledCharacter가 null인 요소가 전달되었습니다.");
        LogAssert.Expect(LogType.Error, "[BattleManager] InitBattle: enemyTeam에 null 컨트롤러 또는 controlledCharacter가 null인 요소가 전달되었습니다.");
        LogAssert.Expect(LogType.Error, "[BattleManager] InitBattle: 유효한 플레이어 전투원이 없어 전투 초기화를 중단합니다.");

        Assert.DoesNotThrow(() =>
        {
            battleManager.InitBattle(
                new List<ICharacterController> { null },
                new List<ICharacterController> { null });
        });
        Assert.AreEqual(BattleState.None, battleManager.battleState);

        var (_, player, _) = CreateMockGameScenePlayer();
        PlayerBattleController validPlayerCtrl = new PlayerBattleController(player);

        LogAssert.Expect(LogType.Error, "[BattleManager] InitBattle: enemyTeam에 null 컨트롤러 또는 controlledCharacter가 null인 요소가 전달되었습니다.");
        Assert.DoesNotThrow(() =>
        {
            battleManager.InitBattle(
                new List<ICharacterController> { validPlayerCtrl },
                new List<ICharacterController> { null });
        });
        Assert.AreEqual(BattleState.Combat, battleManager.battleState);

        battleManager.Dispose();
    }

    [Test]
    public void CharacterUIController_JoinedBattleBeforeStart_PreservesActiveStateAndSingleEventBusEntry()
    {
        GameObject enemyGo = Track(new GameObject("TestEnemyCharacter"));
        Character enemyChar = enemyGo.AddComponent<Character>();
        CharacterUIController uiCtrl = enemyGo.AddComponent<CharacterUIController>();

        InvokePrivateMethod(enemyChar, "Awake");
        InvokePrivateMethod(uiCtrl, "Awake");

        // 동적 스폰 프레임: Start() 실행 전 전투 합류 이벤트 발생
        enemyChar.NotifyJoinedBattle();
        Assert.IsTrue(GetPrivateField<bool>(uiCtrl, "_isBattleActive"));

        // 다음 프레임 Start() 실행 시 _isBattleActive가 HideUI()로 덮어써지지 않아야 함
        InvokePrivateMethod(uiCtrl, "Start");
        Assert.IsTrue(GetPrivateField<bool>(uiCtrl, "_isBattleActive"));
        Assert.IsTrue(GetPrivateField<bool>(uiCtrl, "_isEventBusRegistered"));

        // EventBus 중복 등록 없이 1회 DispatchBattleEnd로 정상 종료 처리 확인
        CombatEventDispatcher.DispatchBattleEnd(enemyChar.eventBus);
        Assert.IsFalse(GetPrivateField<bool>(uiCtrl, "_isBattleActive"));
    }

    [Test]
    public void ExploreMapManager_ConstructedBeforeBattleListLoaded_RefreshesPickerOnGenerateMap()
    {
        var battleDb = ModLoader.Instance.BattleDatabase;
        var backupBattles = new Dictionary<string, BattleData>(battleDb);

        try
        {
            battleDb.Clear();

            // 생성 시점에는 BattleDatabase가 아직 비어있음
            ExploreMapManager manager = new ExploreMapManager();

            BattleData testBattle = new BattleData
            {
                battleId = "Battle_Reload_Test_01",
                dataPath = "Battle_Reload_Test_01",
                battleDataName = "ReloadTestBattle"
            };
            battleDb[testBattle.ID] = testBattle;

            manager.GenerateMap(5, 0, LocationType.Temple);

            bool foundAssignedCombatId = false;
            foreach (ExploreFloorData floor in manager.MapData.floors)
            {
                foreach (ExploreNode node in floor.nodes)
                {
                    if (node.encounterData.encountType.IsCombat() && node.encounterData.battleDataId == "Battle_Reload_Test_01")
                    {
                        foundAssignedCombatId = true;
                        break;
                    }
                }
                if (foundAssignedCombatId) break;
            }

            Assert.IsTrue(foundAssignedCombatId, "초기화 지연 후 로드된 ModLoader.BattleDatabase가 전투 노드에 반영되어야 합니다.");
        }
        finally
        {
            battleDb.Clear();
            foreach (var kvp in backupBattles)
            {
                battleDb[kvp.Key] = kvp.Value;
            }
        }
    }
}
