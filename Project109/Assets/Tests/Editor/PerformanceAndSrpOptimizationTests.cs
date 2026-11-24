using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CardTypes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XLua;

[TestFixture]
public class PerformanceAndSrpOptimizationTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
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
        _createdObjects.Add(obj);
        return obj;
    }

    private (GameMap map, MapDataSO data) CreateTestGameMap(int width, int height, float cellSize, Vector3 gridOffset, Vector3 mapWorldPos)
    {
        var mapGo = Track(new GameObject("TestGameMap"));
        mapGo.transform.position = mapWorldPos;
        var gameMap = mapGo.AddComponent<GameMap>();

        var tilePrefab = Track(new GameObject("TilePrefab", typeof(Tile)));
        gameMap.prefabTile = tilePrefab;

        var mapData = Track(ScriptableObject.CreateInstance<MapDataSO>());
        mapData.width = width;
        mapData.height = height;
        mapData.cellSize = cellSize;
        mapData.gridOffset = gridOffset;
        mapData.InitializeGrid();

        for (int i = 0; i < mapData.cells.Count; i++)
        {
            mapData.cells[i].terrainID = "Floor";
        }

        gameMap.TileCreateByMapData(mapData);
        return (gameMap, mapData);
    }

    [Test]
    public void CardData_TargetTypeAssignmentAndResolve_CachesParsedEnum()
    {
        var data = new CardData { cardName = "nonexistent_test_card", targetType = "Area" };
        Assert.AreEqual(TargetType.AreaSelf, data.parsedTargetType);

        data.targetType = "Ground";
        Assert.AreEqual(TargetType.AreaGround, data.parsedTargetType);

        // 파일이 없더라도 ResolveAndValidate 호출 시 parsedTargetType 캐시가 갱신되어야 합니다.
        LogAssert.Expect(LogType.Error, new Regex("연동할 루아 스크립트 파일을 찾을 수 없습니다"));
        bool resolved = data.ResolveAndValidate(Path.Combine(Path.GetTempPath(), "missing_mod_dir_test"));
        Assert.IsFalse(resolved);
        Assert.AreEqual(TargetType.AreaGround, data.parsedTargetType);
    }

    [Test]
    public void MapDataSO_CellAndWorldConversions_RoundTripWithOffsetAndScale()
    {
        var mapData = Track(ScriptableObject.CreateInstance<MapDataSO>());
        mapData.cellSize = 2.5f;
        mapData.gridOffset = new Vector3(-5f, 1.5f, 10f);

        Vector2Int original = new Vector2Int(3, 4);
        Vector3 world = mapData.CellToWorld(original);

        Assert.AreEqual(new Vector3(2.5f, 1.5f, 20f), world);
        Assert.AreEqual(original, mapData.WorldToCell(world));

        // 셀 경계 내부 미세 오프셋(±0.49 * cellSize)도 동일 셀로 스냅되어야 합니다.
        Vector3 jittered = world + new Vector3(1.1f, 0f, -1.1f);
        Assert.AreEqual(original, mapData.WorldToCell(jittered));

        // cellSize가 0 이하일 때는 안전하게 (0, 0)을 반환해야 합니다.
        Assert.AreEqual(Vector2Int.zero, MapDataSO.WorldToCell(world, mapData.gridOffset, 0f));
    }

    [Test]
    public void EncountType_IsCombat_IdentifiesCombatTypesOnly()
    {
        Assert.IsTrue(EncountType.Battle.IsCombat());
        Assert.IsTrue(EncountType.Elite.IsCombat());
        Assert.IsTrue(EncountType.Boss.IsCombat());

        Assert.IsFalse(EncountType.None.IsCombat());
        Assert.IsFalse(EncountType.Store.IsCombat());
        Assert.IsFalse(EncountType.Restore.IsCombat());
        Assert.IsFalse(EncountType.SecretBox.IsCombat());
        Assert.IsFalse(EncountType.Secret.IsCombat());
    }

    [Test]
    public void GameMap_GetTileAtWorldPosition_UsesO1ArithmeticIndexingAndBoundsChecks()
    {
        var (gameMap, mapData) = CreateTestGameMap(
            width: 5,
            height: 4,
            cellSize: 2.0f,
            gridOffset: new Vector3(10f, 0f, -4f),
            mapWorldPos: new Vector3(3f, 0f, 5f)
        );

        Vector3 targetWorld = gameMap.transform.TransformPoint(mapData.CellToWorld(2, 3)) + new Vector3(0.4f, 0f, -0.4f);
        Tile hit = gameMap.GetTileAtWorldPosition(targetWorld);
        Assert.IsNotNull(hit);
        Assert.AreEqual(new Vector2Int(2, 3), hit.GetCoord());

        // 경계 밖 좌표는 null을 반환해야 합니다.
        Assert.IsNull(gameMap.GetTileAtWorldPosition(gameMap.transform.TransformPoint(mapData.CellToWorld(-1, 0))));
        Assert.IsNull(gameMap.GetTileAtWorldPosition(gameMap.transform.TransformPoint(mapData.CellToWorld(5, 0))));
        Assert.IsNull(gameMap.GetTileAtWorldPosition(gameMap.transform.TransformPoint(mapData.CellToWorld(0, -1))));
        Assert.IsNull(gameMap.GetTileAtWorldPosition(gameMap.transform.TransformPoint(mapData.CellToWorld(0, 4))));

        // null 타일 슬롯이 있어도 예외 없이 null을 반환해야 합니다.
        gameMap.GetTileMap()[1][1] = null;
        Assert.IsNull(gameMap.GetTileAtWorldPosition(gameMap.transform.TransformPoint(mapData.CellToWorld(1, 1))));
    }

    [Test]
    public void GameMap_GetReachableTiles_RespectsObstaclesAndAllocatesZeroBytesAfterWarmup()
    {
        var (gameMap, _) = CreateTestGameMap(
            width: 5,
            height: 5,
            cellSize: 1.0f,
            gridOffset: Vector3.zero,
            mapWorldPos: Vector3.zero
        );

        var tileMap = gameMap.GetTileMap();
        tileMap[3][2].tileState = TileState.Full;     // 벽: 통과/정지 불가
        tileMap[2][3].tileState = TileState.Obstacle; // 장애물: 기본 통과 불가

        var moverGo = Track(new GameObject("TestMover"));
        var moverCharacter = moverGo.AddComponent<Character>();
        var mover = new CharacterMove(moverCharacter);
        Tile startTile = tileMap[2][2];

        List<Tile> results = new List<Tile>(32);

        // 워밍업 (내부 큐/해시셋 버퍼 용량 확보)
        gameMap.GetReachableTiles(startTile, 2, mover, results);
        Assert.IsFalse(results.Contains(startTile));
        Assert.IsFalse(results.Contains(tileMap[3][2]));
        Assert.IsFalse(results.Contains(tileMap[2][3]));
        Assert.IsTrue(results.Contains(tileMap[1][2]));
        Assert.IsTrue(results.Contains(tileMap[0][2]));
        Assert.IsTrue(results.Contains(tileMap[2][1]));

        // PassObstacles 권한 부여 시 장애물 타일 진입 허용 검증
        mover.capabilities = MoverCapability.PassObstacles;
        gameMap.GetReachableTiles(startTile, 2, mover, results);
        Assert.IsTrue(results.Contains(tileMap[2][3]));

        // 워밍업 완료 후 재호출 시 힙 할당이 0바이트인지 검증
        long before = GC.GetAllocatedBytesForCurrentThread();
        gameMap.GetReachableTiles(startTile, 2, mover, results);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(0L, allocated);
    }

    [Test]
    public void SaveSystem_CacheHitAndInvalidation_TracksSlotChangesCorrectly()
    {
        const int slotA = 998;
        const int slotB = 999;

        string pathA = Path.Combine(Application.persistentDataPath, $"save_{slotA}.json");
        string pathB = Path.Combine(Application.persistentDataPath, $"save_{slotB}.json");

        try
        {
            SaveSystem.DeleteSaveFile(slotA);
            SaveSystem.DeleteSaveFile(slotB);

            var dataA1 = new RunSaveData { currentStageLevel = 3, isInDungeon = true, currentMapName = "Temple" };
            File.WriteAllText(pathA, JsonUtility.ToJson(dataA1), System.Text.Encoding.UTF8);

            var dataB = new RunSaveData { currentStageLevel = 12, isInDungeon = false, currentMapName = "Hideout" };
            File.WriteAllText(pathB, JsonUtility.ToJson(dataB), System.Text.Encoding.UTF8);

            Assert.IsTrue(SaveSystem.TryLoadGameData(slotA, out RunSaveData firstLoaded));
            Assert.AreEqual(3, firstLoaded.currentStageLevel);

            // 디스크 파일을 덮어써도 JSON 텍스트 캐시가 살아있으면 디스크 I/O는 생략하되 공유 참조 오염 방지를 위해 독립 인스턴스를 반환
            var dataA2 = new RunSaveData { currentStageLevel = 7, isInDungeon = true, currentMapName = "Temple" };
            File.WriteAllText(pathA, JsonUtility.ToJson(dataA2), System.Text.Encoding.UTF8);

            Assert.IsTrue(SaveSystem.TryLoadGameData(slotA, out RunSaveData cachedLoaded));
            Assert.AreNotSame(firstLoaded, cachedLoaded);
            Assert.AreEqual(3, cachedLoaded.currentStageLevel);

            // forceReload 또는 InvalidateCache 호출 시 디스크에서 최신 데이터를 다시 읽어야 함
            RunSaveData forced = SaveSystem.LoadGameData(slotA, forceReload: true);
            Assert.AreEqual(7, forced.currentStageLevel);

            var dataA3 = new RunSaveData { currentStageLevel = 9, isInDungeon = false, currentMapName = "Temple" };
            File.WriteAllText(pathA, JsonUtility.ToJson(dataA3), System.Text.Encoding.UTF8);
            SaveSystem.InvalidateCache(slotA);

            Assert.IsTrue(SaveSystem.TryLoadGameData(slotA, out RunSaveData afterInvalidate));
            Assert.AreEqual(9, afterInvalidate.currentStageLevel);

            // 슬롯 B는 영향을 받지 않고 독립적으로 캐시/로드되어야 함
            Assert.IsTrue(SaveSystem.TryLoadGameData(slotB, out RunSaveData loadedB));
            Assert.AreEqual(12, loadedB.currentStageLevel);

            // 슬롯 A 삭제 시 슬롯 A 캐시만 즉시 무효화되어야 함
            SaveSystem.DeleteSaveFile(slotA);
            Assert.IsFalse(SaveSystem.HasSaveData(slotA));
            Assert.IsFalse(SaveSystem.TryLoadGameData(slotA, out _));
            Assert.IsTrue(SaveSystem.TryLoadGameData(slotB, out _));
        }
        finally
        {
            SaveSystem.DeleteSaveFile(slotA);
            SaveSystem.DeleteSaveFile(slotB);
        }
    }

    [Test]
    public void UIManager_CreateWorldUI_ImplementsFollowInterfaceAndFailsFastSafely()
    {
        Assert.IsTrue(typeof(IWorldFollowUI).IsAssignableFrom(typeof(CharacterStatusBarUI)));
        Assert.IsTrue(typeof(IWorldFollowUI).IsAssignableFrom(typeof(CharacterEffectListUI)));

        var uiGo = Track(new GameObject("TestUIManager"));
        var uiManager = uiGo.AddComponent<UIManager>();

        // UGUI 컴포넌트 Awake 부재를 고려한 안전한 Fail-Fast 동작 확인
        LogAssert.Expect(LogType.Error, new Regex("prefabKey가 비어 있습니다"));
        Assert.IsNull(uiManager.CreateWorldUI<CharacterStatusBarUI>(string.Empty, null, Vector3.zero));

        LogAssert.Expect(LogType.Error, new Regex("필수 프리팹\\(MissingTestPrefabKey\\)"));
        Assert.IsNull(uiManager.CreateWorldUI<CharacterStatusBarUI>("MissingTestPrefabKey", null, Vector3.zero));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RunCardLuaDelegateLifecycle(LuaEnv luaEnv, Character caster)
    {
        LuaTable luaTable = null;
        Card card = null;
        try
        {
            object[] results = luaEnv.DoString(@"
                return {
                    canPlayCalls = 0,
                    spendCostCalls = 0,
                    CanPlay = function(self, card, caster)
                        self.canPlayCalls = self.canPlayCalls + 1
                        return self.canPlayCalls <= 2
                    end,
                    SpendCosts = function(self, card, caster)
                        self.spendCostCalls = self.spendCostCalls + 1
                    end
                }
            ");

            luaTable = results[0] as LuaTable;
            Assert.IsNotNull(luaTable);

            var data = new CardData { cardName = "LuaDelegateCacheTest", stamina = 1 };
            card = new Card(data, caster, luaTable, runtimeID: 42);

            Assert.IsTrue(card.CanPlay(caster));
            Assert.IsTrue(card.CanPlay(caster));
            Assert.IsFalse(card.CanPlay(caster));
            Assert.AreEqual(3, luaTable.Get<int>("canPlayCalls"));

            card.SpendCosts(caster);
            card.SpendCosts(caster);
            Assert.AreEqual(2, luaTable.Get<int>("spendCostCalls"));
            Assert.AreEqual(8f, caster.curStamina);

            card.Dispose();
            Assert.IsTrue(card.IsDisposed);
            Assert.IsNull(card.luaTable);
            Assert.IsFalse(card.CanPlay(caster));

            // Dispose 이후 내부 캐싱 델리게이트 필드까지 null 해제되었는지 검증
            var canPlayField = typeof(Card).GetField("_luaCanPlay", BindingFlags.Instance | BindingFlags.NonPublic);
            var spendCostsField = typeof(Card).GetField("_luaSpendCosts", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(canPlayField);
            Assert.IsNotNull(spendCostsField);
            Assert.IsNull(canPlayField.GetValue(card));
            Assert.IsNull(spendCostsField.GetValue(card));

            // 멱등 Dispose 호출 시 예외가 없어야 함
            card.Dispose();
        }
        finally
        {
            if (card != null && !card.IsDisposed)
            {
                card.Dispose();
            }
            if (luaTable != null)
            {
                luaTable.Dispose();
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ScrubMonoStack()
    {
        // Mono 보수적 GC가 이전 호출 프레임의 죽은 레지스터/스택 슬롯을 참조로 오인하지 않도록 스택 영역을 덮어씁니다.
        Span<byte> scrub = stackalloc byte[256];
        scrub.Clear();
    }

    [Test]
    public void Card_CachedLuaDelegates_InvokeWithoutRelookupAndClearOnDispose()
    {
        var casterGo = Track(new GameObject("TestCaster"));
        var caster = casterGo.AddComponent<Character>();
        caster.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 10f,
            staminaRegenPerSecond = 0f,
            moveRange = 2
        });
        caster.curStamina = 10f;

        LuaEnv luaEnv = new LuaEnv();
        try
        {
            RunCardLuaDelegateLifecycle(luaEnv, caster);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(casterGo);
            caster = null;
            casterGo = null;
            ScrubMonoStack();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            luaEnv.Tick();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            luaEnv.Tick();
            luaEnv.Dispose();
        }
    }

    [Test]
    public void GameItemRewardManager_DropTableQueries_HandleMissingLoaderSafely()
    {
        var rewardGo = Track(new GameObject("TestRewardManager"));
        var rewardManager = rewardGo.AddComponent<GameItemRewardManager>();

        Assert.IsNull(rewardManager.GetRandomCardDataByDropTable("nonexistent"));
        Assert.IsNull(rewardManager.GetRandomRelicDataByDropTable("nonexistent"));
    }
}
