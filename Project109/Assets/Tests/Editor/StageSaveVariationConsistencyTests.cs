using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class StageSaveVariationConsistencyTests
{
    private const int TestSaveSlot = 995;

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<StageManager> _createdStageManagers = new List<StageManager>();
    private readonly List<Player> _createdPlayers = new List<Player>();

    private Dictionary<string, BattleData> _backupBattles;
    private Dictionary<string, NPCUnitData> _backupNpcs;
    private Dictionary<string, InteractableData> _backupInteractables;
    private int _prevSelectedSaveSlot;
    private UnityEngine.Random.State _prevRandomState;

    [SetUp]
    public void SetUp()
    {
        _prevRandomState = UnityEngine.Random.state;
        _prevSelectedSaveSlot = GameSceneManager.selectedSaveSlot;

        _backupBattles = new Dictionary<string, BattleData>(ModLoader.Instance.BattleDatabase);
        _backupNpcs = new Dictionary<string, NPCUnitData>(ModLoader.Instance.NPCUnitDatabase);
        _backupInteractables = new Dictionary<string, InteractableData>(ModLoader.Instance.InteractableDatabase);

        SaveSystem.DeleteSaveFile(TestSaveSlot);
        SaveSystem.InvalidateAllCaches();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdStageManagers.Count - 1; i >= 0; i--)
        {
            if (_createdStageManagers[i] != null)
            {
                _createdStageManagers[i].Dispose();
            }
        }
        _createdStageManagers.Clear();

        for (int i = _createdPlayers.Count - 1; i >= 0; i--)
        {
            if (_createdPlayers[i] != null)
            {
                _createdPlayers[i].Dispose();
            }
        }
        _createdPlayers.Clear();

        SetStaticAutoProperty<RunManager>("instance", null);
        SetStaticAutoProperty<GameSceneManager>("instance", null);
        SetStaticAutoProperty<AssetCacheManager>("instance", null);
        SetStaticAutoProperty<GameMap>("current", null);

        GameSceneManager.selectedSaveSlot = _prevSelectedSaveSlot;
        SaveSystem.DeleteSaveFile(TestSaveSlot);
        SaveSystem.InvalidateAllCaches();

        ModLoader.Instance.BattleDatabase.Clear();
        foreach (var kvp in _backupBattles)
        {
            ModLoader.Instance.BattleDatabase[kvp.Key] = kvp.Value;
        }

        ModLoader.Instance.NPCUnitDatabase.Clear();
        foreach (var kvp in _backupNpcs)
        {
            ModLoader.Instance.NPCUnitDatabase[kvp.Key] = kvp.Value;
        }

        ModLoader.Instance.InteractableDatabase.Clear();
        foreach (var kvp in _backupInteractables)
        {
            ModLoader.Instance.InteractableDatabase[kvp.Key] = kvp.Value;
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

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
        field.SetValue(target, value);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} 메서드를 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private MapDataSO CreateBattleMapData(string mapName, LocationType locationType, EncountType encountType, Vector2Int[] enemySpawns, Vector2Int[] playerSpawns)
    {
        MapDataSO mapData = Track(ScriptableObject.CreateInstance<MapDataSO>());
        mapData.name = mapName;
        mapData.stageName = mapName;
        mapData.locationType = locationType;
        mapData.EncountType = encountType;
        mapData.width = 6;
        mapData.height = 5;
        mapData.cellSize = 1f;
        mapData.gridOffset = Vector3.zero;
        mapData.InitializeGrid();

        for (int i = 0; i < enemySpawns.Length; i++)
        {
            Vector2Int pos = enemySpawns[i];
            CellData cell = FindCell(mapData, pos);
            Assert.IsNotNull(cell, $"{mapName}에 적 스폰 셀 {pos}가 존재하지 않습니다.");
            cell.eventID = "EnemySpawn";
        }

        for (int i = 0; i < playerSpawns.Length; i++)
        {
            Vector2Int pos = playerSpawns[i];
            CellData cell = FindCell(mapData, pos);
            Assert.IsNotNull(cell, $"{mapName}에 플레이어 스폰 셀 {pos}가 존재하지 않습니다.");
            cell.eventID = "PlayerSpawn";
        }

        return mapData;
    }

    private static CellData FindCell(MapDataSO mapData, Vector2Int coord)
    {
        for (int i = 0; i < mapData.cells.Count; i++)
        {
            if (mapData.cells[i].position == coord)
            {
                return mapData.cells[i];
            }
        }
        return null;
    }

    private AssetCacheManager CreateSeededAssetCache(List<MapDataSO> maps, Dictionary<string, GameObject> models = null)
    {
        GameObject cacheGo = Track(new GameObject("TestAssetCacheManager"));
        AssetCacheManager cache = cacheGo.AddComponent<AssetCacheManager>();
        SetStaticAutoProperty<AssetCacheManager>("instance", cache);

        Dictionary<string, MapDataSO> mapDict = new Dictionary<string, MapDataSO>();
        Dictionary<string, StageMapDataBundle> stageMapDataDict = new Dictionary<string, StageMapDataBundle>();

        for (int i = 0; i < maps.Count; i++)
        {
            MapDataSO m = maps[i];
            if (m == null) continue;

            mapDict[m.ID] = m;

            string locKey = m.locationType.ToString();
            if (!stageMapDataDict.ContainsKey(locKey))
            {
                stageMapDataDict[locKey] = new StageMapDataBundle
                {
                    stageName = m.stageName,
                    startMapDataList = new List<MapDataSO>(),
                    battleMapDataList = new List<MapDataSO>(),
                    eliteMapDataList = new List<MapDataSO>(),
                    bossMapDataList = new List<MapDataSO>(),
                    secretMapDataList = new List<MapDataSO>(),
                    storeMapDataList = new List<MapDataSO>(),
                    restoreMapDataList = new List<MapDataSO>()
                };
            }

            switch (m.EncountType)
            {
                case EncountType.None:
                    stageMapDataDict[locKey].startMapDataList.Add(m);
                    break;
                case EncountType.Battle:
                    stageMapDataDict[locKey].battleMapDataList.Add(m);
                    break;
                case EncountType.Elite:
                    stageMapDataDict[locKey].eliteMapDataList.Add(m);
                    break;
                case EncountType.Boss:
                    stageMapDataDict[locKey].bossMapDataList.Add(m);
                    break;
                case EncountType.Secret:
                case EncountType.SecretBox:
                    stageMapDataDict[locKey].secretMapDataList.Add(m);
                    break;
                case EncountType.Store:
                    stageMapDataDict[locKey].storeMapDataList.Add(m);
                    break;
                case EncountType.Restore:
                    stageMapDataDict[locKey].restoreMapDataList.Add(m);
                    break;
            }
        }

        cache.mapList = new List<MapDataSO>(maps);
        SetPrivateField(cache, "mapDict", mapDict);
        SetPrivateField(cache, "stageMapDataDict", stageMapDataDict);

        if (models != null)
        {
            SetPrivateField(cache, "modelDict", models);
        }

        return cache;
    }

    private GameMap CreateTestGameMap()
    {
        GameObject mapGo = Track(new GameObject("TestGameMap"));
        GameMap gameMap = mapGo.AddComponent<GameMap>();
        InvokePrivateMethod(gameMap, "Awake");

        GameObject tilePrefab = Track(new GameObject("TilePrefab", typeof(Tile)));
        gameMap.prefabTile = tilePrefab;
        return gameMap;
    }

    private Character CreateTestPlayerCharacter()
    {
        GameObject charGo = Track(new GameObject("TestPlayerCharacter"));
        Character character = charGo.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = CharacterFaction.Player;
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 120f,
            staminaRegenPerSecond = 40f,
            moveRange = 3
        });
        return character;
    }

    private (GameSceneManager gsm, RunManager runManager, StageManager stageManager, Character playerChar) CreateTestSession()
    {
        Character playerChar = CreateTestPlayerCharacter();
        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        GameSceneManager.selectedSaveSlot = TestSaveSlot;
        GameObject gsmGo = Track(new GameObject("TestGameSceneManager"));
        GameSceneManager gsm = gsmGo.AddComponent<GameSceneManager>();
        gsm.player = player;
        SetStaticAutoProperty<GameSceneManager>("instance", gsm);

        PropertyInfo inDungeonProp = typeof(GameSceneManager).GetProperty("isInDungeon", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(inDungeonProp);
        inDungeonProp.SetValue(gsm, true);

        PropertyInfo saveSlotProp = typeof(GameSceneManager).GetProperty("currentSaveSlot", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(saveSlotProp);
        saveSlotProp.SetValue(gsm, TestSaveSlot);

        GameMap gameMap = CreateTestGameMap();

        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);

        StageManager stageManager = new StageManager();
        stageManager.Initialize(gameMap);
        _createdStageManagers.Add(stageManager);

        runManager.currentStage = stageManager;
        runManager.exploreMapManager = new ExploreMapManager();

        return (gsm, runManager, stageManager, playerChar);
    }

    private void RegisterTestBattleAndMonsters(string battleId, params string[] monsterIds)
    {
        BattleData battleData = new BattleData
        {
            battleId = battleId,
            battleDataName = "SaveConsistencyTestBattle",
            dataPath = battleId,
            battleLocation = "Temple",
            monsterNames = new List<string>(monsterIds)
        };
        ModLoader.Instance.BattleDatabase[battleData.ID] = battleData;

        for (int i = 0; i < monsterIds.Length; i++)
        {
            string monsterId = monsterIds[i];
            if (!ModLoader.Instance.NPCUnitDatabase.ContainsKey(monsterId))
            {
                ModLoader.Instance.NPCUnitDatabase[monsterId] = new NPCUnitData
                {
                    unitId = monsterId,
                    prefabAddress = monsterId,
                    displayName = monsterId,
                    characterStat = new CharacterStat
                    {
                        maxHealth = 40f,
                        maxStamina = 100f,
                        staminaRegenPerSecond = 20f,
                        moveRange = 2
                    }
                };
            }
        }
    }

    private Dictionary<string, GameObject> CreateTestMonsterPrefabs(params string[] monsterIds)
    {
        Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        for (int i = 0; i < monsterIds.Length; i++)
        {
            string monsterId = monsterIds[i];
            if (!models.ContainsKey(monsterId))
            {
                GameObject prefab = Track(new GameObject($"Prefab_{monsterId}", typeof(Character)));
                models[monsterId] = prefab;
            }
        }
        return models;
    }

    private static List<Vector2Int> ExtractPositions(IReadOnlyList<CellData> cells)
    {
        List<Vector2Int> positions = new List<Vector2Int>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            positions.Add(cells[i].position);
        }
        return positions;
    }

    private static List<Vector2Int> ExtractSpawnedEnemyCoords(StageManager stageManager, MapDataSO mapData)
    {
        List<Vector2Int> coords = new List<Vector2Int>(stageManager.currentSpawnEnemyList.Count);
        for (int i = 0; i < stageManager.currentSpawnEnemyList.Count; i++)
        {
            GameObject enemyObj = stageManager.currentSpawnEnemyList[i];
            Assert.IsNotNull(enemyObj);
            coords.Add(mapData.WorldToCell(enemyObj.transform.position));
        }
        return coords;
    }

    [Test]
    public void EncounterData_MapDataIdAndStageSeed_JsonRoundTripAndSaveSystem_PreservesValues()
    {
        var (_, runManager, _, _) = CreateTestSession();

        ExploreMapData mapData = new ExploreMapData();
        ExploreFloorData floor0 = new ExploreFloorData();
        ExploreNode battleNode = new ExploreNode(0, 0);
        battleNode.encounterData.locationType = LocationType.Temple;
        battleNode.encounterData.encountType = EncountType.Battle;
        battleNode.encounterData.battleDataId = "Battle_Save_Test";
        battleNode.encounterData.mapDataId = "Map_Temple_Battle_2";
        battleNode.encounterData.stageSeed = 789456123;
        floor0.nodes.Add(battleNode);
        mapData.floors.Add(floor0);

        runManager.currentMapName = "Temple";
        runManager.currentStageLevel = 1;
        runManager.currentExploreMapFloor = 0;
        runManager.exploreMapManager.RestoreMapData(mapData);
        runManager.currentExploreNode = battleNode;

        // 1. SaveSystem 디스크 저장 → 강제 리로드 → RestoreRunData 왕복 검증
        SaveSystem.SaveGame(runManager, TestSaveSlot);
        Assert.IsTrue(SaveSystem.TryLoadGameData(TestSaveSlot, out RunSaveData loadedData, forceReload: true));
        Assert.IsNotNull(loadedData);
        Assert.IsNotNull(loadedData.exploreMapData);

        string json = JsonUtility.ToJson(loadedData);
        StringAssert.Contains("\"mapDataId\":\"Map_Temple_Battle_2\"", json);
        StringAssert.Contains("\"stageSeed\":789456123", json);

        runManager.ResetRunProgress();
        SaveSystem.RestoreRunData(loadedData, runManager);

        Assert.IsNotNull(runManager.currentExploreNode);
        Assert.AreEqual("Map_Temple_Battle_2", runManager.currentExploreNode.encounterData.mapDataId);
        Assert.AreEqual(789456123, runManager.currentExploreNode.encounterData.stageSeed);
        Assert.AreEqual("Battle_Save_Test", runManager.currentExploreNode.encounterData.battleDataId);

        // 2. 구버전 세이브(stageSeed 필드 누락 JSON) 역직렬화 시 기본값 0으로 안전 복원 검증
        string legacyJson = "{\"floors\":[{\"nodes\":[{\"floor\":1,\"index\":2,\"encounterData\":{\"locationType\":2,\"encountType\":5,\"mapDataId\":\"\",\"battleDataId\":\"Battle_Legacy\"}}]}]}";
        ExploreMapData legacyDeserialized = JsonUtility.FromJson<ExploreMapData>(legacyJson);
        Assert.IsNotNull(legacyDeserialized);
        Assert.AreEqual(string.Empty, legacyDeserialized.floors[0].nodes[0].encounterData.mapDataId);
        Assert.AreEqual(0, legacyDeserialized.floors[0].nodes[0].encounterData.stageSeed);
    }

    [Test]
    public void CollectionExtensions_ShuffleWithSeed_IsDeterministicAndLeavesUnityRandomUntouched()
    {
        List<int> listA = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        List<int> listB = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        List<int> listC = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        UnityEngine.Random.InitState(11111);
        UnityEngine.Random.State beforeUnityState = UnityEngine.Random.state;

        listA.Shuffle(new System.Random(54321));
        UnityEngine.Random.State afterUnityState = UnityEngine.Random.state;
        Assert.AreEqual(beforeUnityState, afterUnityState, "결정론적 Shuffle(System.Random)은 전역 UnityEngine.Random 상태를 오염시키지 않아야 합니다.");

        listB.Shuffle(new System.Random(54321));
        listC.Shuffle(new System.Random(99999));

        CollectionAssert.AreEqual(listA, listB);
        CollectionAssert.AreNotEqual(listA, listC);
    }

    [Test]
    public void StageManager_GenerateStage_MultiMapPool_ConsistentMapAndSpawnPositionsAcrossReloads()
    {
        MapDataSO battleMap1 = CreateBattleMapData(
            "Map_Temple_Battle_1",
            LocationType.Temple,
            EncountType.Battle,
            new[]
            {
                new Vector2Int(3, 0), new Vector2Int(3, 1), new Vector2Int(3, 2),
                new Vector2Int(4, 0), new Vector2Int(4, 1), new Vector2Int(4, 2)
            },
            new[]
            {
                new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(0, 3)
            });

        MapDataSO battleMap2 = CreateBattleMapData(
            "Map_Temple_Battle_2",
            LocationType.Temple,
            EncountType.Battle,
            new[]
            {
                new Vector2Int(4, 2), new Vector2Int(4, 3), new Vector2Int(4, 4),
                new Vector2Int(5, 1), new Vector2Int(5, 2), new Vector2Int(5, 3)
            },
            new[]
            {
                new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(1, 3)
            });

        RegisterTestBattleAndMonsters("Battle_MultiMap_Test", "Goblin_A", "Goblin_B", "Goblin_C");
        Dictionary<string, GameObject> modelPrefabs = CreateTestMonsterPrefabs("Goblin_A", "Goblin_B", "Goblin_C");
        CreateSeededAssetCache(new List<MapDataSO> { battleMap1, battleMap2 }, modelPrefabs);

        // 복수 시드에서 두 맵 모두 선택 가능하며 서로 다른 스폰 순서가 생성됨을 사전 확인
        HashSet<string> observedMaps = new HashSet<string>();
        for (int s = 1; s <= 40; s++)
        {
            MapDataSO picked = StageManager.ResolveMapData("Temple", EncountType.Battle, preferredMapDataId: null, stageSeed: s * 7919);
            if (picked != null) observedMaps.Add(picked.ID);
        }
        Assert.AreEqual(2, observedMaps.Count, "복수 맵 풀에서 Map_Temple_Battle_1과 Map_Temple_Battle_2가 모두 선택 가능해야 합니다.");

        // 1차 진입: 맵 및 스폰 생성 후 JSON 직렬화
        var (_, runManager1, stageManager1, playerChar1) = CreateTestSession();
        ExploreNode initialNode = new ExploreNode(2, 1);
        initialNode.encounterData.locationType = LocationType.Temple;
        initialNode.encounterData.encountType = EncountType.Battle;
        initialNode.encounterData.battleDataId = "Battle_MultiMap_Test";
        initialNode.encounterData.mapDataId = "Map_Temple_Battle_2";
        initialNode.encounterData.stageSeed = 24681357;

        ExploreMapData initialMapData = new ExploreMapData();
        ExploreFloorData floorData = new ExploreFloorData();
        floorData.nodes.Add(initialNode);
        initialMapData.floors.Add(floorData);

        runManager1.currentMapName = "Temple";
        runManager1.exploreMapManager.RestoreMapData(initialMapData);
        runManager1.currentExploreNode = initialNode;

        UnityEngine.Random.InitState(101);
        stageManager1.GenerateStage(initialNode);

        string firstMapId = stageManager1.currentMapData.ID;
        Assert.AreEqual("Map_Temple_Battle_2", firstMapId);
        List<Vector2Int> firstEnemyCells = ExtractPositions(stageManager1.currentSpawnEnemyCells);
        List<Vector2Int> firstPlayerCells = ExtractPositions(stageManager1.currentSpawnPlayerCells);
        List<Vector2Int> firstSpawnedEnemies = ExtractSpawnedEnemyCoords(stageManager1, stageManager1.currentMapData);
        Vector2Int firstPlayerSpawnedCoord = stageManager1.currentMapData.WorldToCell(playerChar1.transform.position);
        Assert.AreEqual(3, firstSpawnedEnemies.Count);
        Assert.AreEqual(firstPlayerCells[0], firstPlayerSpawnedCoord);

        // 세이브 파일 저장 후 강제 종료/재로드 시뮬레이션 (전역 UnityEngine.Random 시드도 의도적으로 변경)
        SaveSystem.SaveGame(runManager1, TestSaveSlot);
        Assert.IsTrue(SaveSystem.TryLoadGameData(TestSaveSlot, out RunSaveData reloadedSave, forceReload: true));

        var (_, runManager2, stageManager2, playerChar2) = CreateTestSession();
        SaveSystem.RestoreRunData(reloadedSave, runManager2);
        UnityEngine.Random.InitState(987654);

        stageManager2.GenerateStage(runManager2.currentExploreNode);

        Assert.IsNotNull(stageManager2.currentMapData);
        Assert.AreEqual(firstMapId, stageManager2.currentMapData.ID, "재로드 후 MapDataSO.ID가 동일하게 복원되어야 합니다.");
        CollectionAssert.AreEqual(firstEnemyCells, ExtractPositions(stageManager2.currentSpawnEnemyCells), "재로드 후 적 스폰 셀 순서가 100% 일치해야 합니다.");
        CollectionAssert.AreEqual(firstPlayerCells, ExtractPositions(stageManager2.currentSpawnPlayerCells), "재로드 후 플레이어 스폰 셀 순서가 100% 일치해야 합니다.");
        CollectionAssert.AreEqual(firstSpawnedEnemies, ExtractSpawnedEnemyCoords(stageManager2, stageManager2.currentMapData), "재로드 후 실제 생성된 몬스터 좌표가 100% 일치해야 합니다.");
        Assert.AreEqual(firstPlayerSpawnedCoord, stageManager2.currentMapData.WorldToCell(playerChar2.transform.position), "재로드 후 플레이어 초기 스폰 좌표가 100% 일치해야 합니다.");

        // 동일 StageManager/GameMap 인스턴스에서 반복 호출 시에도 EditMode 안전하게 동일 결과 유지
        UnityEngine.Random.InitState(55555);
        stageManager2.GenerateStage(runManager2.currentExploreNode);
        Assert.AreEqual(firstMapId, stageManager2.currentMapData.ID);
        CollectionAssert.AreEqual(firstEnemyCells, ExtractPositions(stageManager2.currentSpawnEnemyCells));
        CollectionAssert.AreEqual(firstPlayerCells, ExtractPositions(stageManager2.currentSpawnPlayerCells));
    }

    [Test]
    public void StageManager_GenerateStage_InvalidMapDataId_FallsBackSafelyAndPersistsValidMapId()
    {
        MapDataSO battleMap1 = CreateBattleMapData(
            "Map_Temple_Battle_1",
            LocationType.Temple,
            EncountType.Battle,
            new[] { new Vector2Int(3, 0), new Vector2Int(3, 1), new Vector2Int(4, 0) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) });

        MapDataSO battleMap2 = CreateBattleMapData(
            "Map_Temple_Battle_2",
            LocationType.Temple,
            EncountType.Battle,
            new[] { new Vector2Int(4, 2), new Vector2Int(4, 3), new Vector2Int(5, 2) },
            new[] { new Vector2Int(1, 0), new Vector2Int(1, 1) });

        RegisterTestBattleAndMonsters("Battle_Fallback_Test", "Goblin_A");
        CreateSeededAssetCache(
            new List<MapDataSO> { battleMap1, battleMap2 },
            CreateTestMonsterPrefabs("Goblin_A"));

        var (_, _, stageManager, _) = CreateTestSession();

        ExploreNode node = new ExploreNode(1, 0);
        node.encounterData.locationType = LocationType.Temple;
        node.encounterData.encountType = EncountType.Battle;
        node.encounterData.battleDataId = "Battle_Fallback_Test";
        node.encounterData.mapDataId = "Deleted_Or_Invalid_Map_Id";
        node.encounterData.stageSeed = 424242;

        LogAssert.Expect(LogType.Warning, new Regex("Deleted_Or_Invalid_Map_Id"));
        stageManager.GenerateStage(node);

        Assert.IsNotNull(stageManager.currentMapData);
        string resolvedId = stageManager.currentMapData.ID;
        Assert.IsTrue(resolvedId == "Map_Temple_Battle_1" || resolvedId == "Map_Temple_Battle_2");
        Assert.AreEqual(resolvedId, node.encounterData.mapDataId, "폴백으로 선택된 유효한 맵 ID가 노드에 갱신 저장되어야 합니다.");

        List<Vector2Int> enemyCells = ExtractPositions(stageManager.currentSpawnEnemyCells);
        List<Vector2Int> playerCells = ExtractPositions(stageManager.currentSpawnPlayerCells);

        // 갱신된 후 재로드 시에는 경고 없이 동일한 폴백 맵과 배치가 유지되어야 함
        stageManager.GenerateStage(node);
        Assert.AreEqual(resolvedId, stageManager.currentMapData.ID);
        CollectionAssert.AreEqual(enemyCells, ExtractPositions(stageManager.currentSpawnEnemyCells));
        CollectionAssert.AreEqual(playerCells, ExtractPositions(stageManager.currentSpawnPlayerCells));
    }

    [Test]
    public void StageManager_GenerateStage_LegacyUnsavedNodeWithoutSeedOrMapId_RemainsDeterministicAcrossLoads()
    {
        MapDataSO battleMap1 = CreateBattleMapData(
            "Map_Temple_Battle_1",
            LocationType.Temple,
            EncountType.Battle,
            new[] { new Vector2Int(3, 0), new Vector2Int(3, 1), new Vector2Int(4, 0), new Vector2Int(4, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) });

        MapDataSO battleMap2 = CreateBattleMapData(
            "Map_Temple_Battle_2",
            LocationType.Temple,
            EncountType.Battle,
            new[] { new Vector2Int(4, 2), new Vector2Int(4, 3), new Vector2Int(5, 2), new Vector2Int(5, 3) },
            new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) });

        RegisterTestBattleAndMonsters("Battle_Legacy_Test", "Goblin_A", "Goblin_B");
        CreateSeededAssetCache(
            new List<MapDataSO> { battleMap1, battleMap2 },
            CreateTestMonsterPrefabs("Goblin_A", "Goblin_B"));

        var (_, _, stageManagerA, _) = CreateTestSession();
        var (_, _, stageManagerB, _) = CreateTestSession();

        // 시드와 mapDataId가 모두 비어있는 두 개의 독립된 구버전 세이브 노드 시뮬레이션
        ExploreNode legacyNodeA = new ExploreNode(3, 2);
        legacyNodeA.encounterData.locationType = LocationType.Temple;
        legacyNodeA.encounterData.encountType = EncountType.Battle;
        legacyNodeA.encounterData.battleDataId = "Battle_Legacy_Test";

        ExploreNode legacyNodeB = new ExploreNode(3, 2);
        legacyNodeB.encounterData.locationType = LocationType.Temple;
        legacyNodeB.encounterData.encountType = EncountType.Battle;
        legacyNodeB.encounterData.battleDataId = "Battle_Legacy_Test";

        UnityEngine.Random.InitState(111);
        stageManagerA.GenerateStage(legacyNodeA);

        UnityEngine.Random.InitState(999);
        stageManagerB.GenerateStage(legacyNodeB);

        Assert.AreNotEqual(0, legacyNodeA.encounterData.stageSeed);
        Assert.AreEqual(legacyNodeA.encounterData.stageSeed, legacyNodeB.encounterData.stageSeed);
        Assert.IsFalse(string.IsNullOrEmpty(legacyNodeA.encounterData.mapDataId));
        Assert.AreEqual(legacyNodeA.encounterData.mapDataId, legacyNodeB.encounterData.mapDataId);
        Assert.AreEqual(stageManagerA.currentMapData.ID, stageManagerB.currentMapData.ID);
        CollectionAssert.AreEqual(
            ExtractPositions(stageManagerA.currentSpawnEnemyCells),
            ExtractPositions(stageManagerB.currentSpawnEnemyCells));
        CollectionAssert.AreEqual(
            ExtractPositions(stageManagerA.currentSpawnPlayerCells),
            ExtractPositions(stageManagerB.currentSpawnPlayerCells));
    }

    [Test]
    public void ExploreMapManager_GenerateMap_PreAssignsStageSeedAndMapDataIdWhenCacheReady()
    {
        MapDataSO startMap = CreateBattleMapData("Map_Temple_Start_1", LocationType.Temple, EncountType.None, Array.Empty<Vector2Int>(), new[] { new Vector2Int(0, 0) });
        MapDataSO battleMap1 = CreateBattleMapData("Map_Temple_Battle_1", LocationType.Temple, EncountType.Battle, new[] { new Vector2Int(3, 0) }, new[] { new Vector2Int(0, 0) });
        MapDataSO battleMap2 = CreateBattleMapData("Map_Temple_Battle_2", LocationType.Temple, EncountType.Battle, new[] { new Vector2Int(4, 1) }, new[] { new Vector2Int(1, 0) });
        MapDataSO eliteMap = CreateBattleMapData("Map_Temple_Elite_1", LocationType.Temple, EncountType.Elite, new[] { new Vector2Int(3, 1) }, new[] { new Vector2Int(0, 0) });
        MapDataSO bossMap = CreateBattleMapData("Map_Temple_Boss_1", LocationType.Temple, EncountType.Boss, new[] { new Vector2Int(4, 2) }, new[] { new Vector2Int(0, 0) });
        MapDataSO secretMap = CreateBattleMapData("Map_Temple_Secret_1", LocationType.Temple, EncountType.Secret, Array.Empty<Vector2Int>(), new[] { new Vector2Int(0, 0) });
        MapDataSO storeMap = CreateBattleMapData("Map_Temple_Store_1", LocationType.Temple, EncountType.Store, Array.Empty<Vector2Int>(), new[] { new Vector2Int(0, 0) });
        MapDataSO restoreMap = CreateBattleMapData("Map_Temple_Restore_1", LocationType.Temple, EncountType.Restore, Array.Empty<Vector2Int>(), new[] { new Vector2Int(0, 0) });

        CreateSeededAssetCache(new List<MapDataSO>
        {
            startMap, battleMap1, battleMap2, eliteMap, bossMap, secretMap, storeMap, restoreMap
        });

        ExploreMapManager exploreMapManager = new ExploreMapManager();
        exploreMapManager.GenerateMap(6, 0, LocationType.Temple);

        Assert.IsNotNull(exploreMapManager.MapData);
        Assert.AreEqual(6, exploreMapManager.MapData.floors.Count);

        for (int f = 0; f < exploreMapManager.MapData.floors.Count; f++)
        {
            List<ExploreNode> nodes = exploreMapManager.MapData.floors[f].nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                EncounterData enc = nodes[i].encounterData;
                Assert.AreNotEqual(0, enc.stageSeed, $"노드 ({f},{i})에 stageSeed가 배정되어야 합니다.");
                Assert.IsFalse(string.IsNullOrEmpty(enc.mapDataId), $"노드 ({f},{i})에 mapDataId가 배정되어야 합니다.");

                if (enc.encountType == EncountType.Battle)
                {
                    Assert.IsTrue(
                        enc.mapDataId == "Map_Temple_Battle_1" || enc.mapDataId == "Map_Temple_Battle_2",
                        $"전투 노드의 mapDataId('{enc.mapDataId}')는 전투 맵 풀에서 선택되어야 합니다.");
                }
            }
        }
    }
}
