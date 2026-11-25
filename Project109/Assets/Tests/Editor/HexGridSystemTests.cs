using System;
using System.Collections.Generic;
using CardTypes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class HexGridSystemTests
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

    private (GameMap map, MapDataSO data) CreateTestGameMap(int width, int height, float cellSize = 1.0f, Vector3 gridOffset = default)
    {
        var mapGo = Track(new GameObject("HexTestGameMap"));
        var gameMap = mapGo.AddComponent<GameMap>();

        var tilePrefab = Track(new GameObject("HexTilePrefab", typeof(Tile), typeof(BoxCollider)));
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

    private Character CreateTestCharacter(string name, Tile tile)
    {
        var go = Track(new GameObject(name));
        var ch = go.AddComponent<Character>();
        typeof(Character).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(ch, null);
        ch.InitializeStat(new CharacterStat { maxHealth = 100f, maxStamina = 100f, moveRange = 3 });
        if (tile != null)
        {
            ch.transform.position = tile.transform.position;
            ch.characterMove.SetCurrentTile(tile, triggerTileHazards: false);
        }
        return ch;
    }

    [Test]
    public void HexMath_OddRAndAxialRoundTrip_WorksAcrossNegativeAndPositiveCoords()
    {
        for (int col = -10; col <= 10; col++)
        {
            for (int row = -10; row <= 10; row++)
            {
                HexMath.OddRToAxial(col, row, out int q, out int r);
                HexMath.AxialToOddR(q, r, out int outCol, out int outRow);
                Assert.AreEqual(col, outCol, $"col={col}, row={row}");
                Assert.AreEqual(row, outRow, $"col={col}, row={row}");
            }
        }
    }

    [Test]
    public void HexMath_CellAndWorldRoundTrip_HandlesOffsetScaleAndIncircleJitter()
    {
        Vector3 offset = new Vector3(-4f, 2f, 7f);
        float cellSize = 2.0f;

        for (int col = 0; col < 8; col++)
        {
            for (int row = 0; row < 8; row++)
            {
                Vector3 center = HexMath.CellToWorld(col, row, offset, cellSize);
                Assert.AreEqual(new Vector2Int(col, row), HexMath.WorldToCell(center, offset, cellSize));

                // 내접원 반경(0.5 * cellSize = 1.0) 내부 지터는 동일 셀 유지
                Vector3 jittered = center + new Vector3(0.45f, 0f, -0.45f);
                Assert.AreEqual(new Vector2Int(col, row), HexMath.WorldToCell(jittered, offset, cellSize));
            }
        }
    }

    [Test]
    public void HexMath_Distance_IsSymmetricSatisfiesTriangleInequalityAndMatchesSixNeighbors()
    {
        Vector2Int evenCenter = new Vector2Int(4, 4);
        Vector2Int oddCenter = new Vector2Int(4, 5);

        for (int dir = 0; dir < HexMath.DirectionCount; dir++)
        {
            HexMath.GetOddRNeighbor(evenCenter.x, evenCenter.y, dir, out int ec, out int er);
            Assert.AreEqual(1, HexMath.Distance(evenCenter, new Vector2Int(ec, er)));

            HexMath.GetOddRNeighbor(oddCenter.x, oddCenter.y, dir, out int oc, out int or);
            Assert.AreEqual(1, HexMath.Distance(oddCenter, new Vector2Int(oc, or)));
        }

        Vector2Int a = new Vector2Int(1, 2);
        Vector2Int b = new Vector2Int(5, 7);
        Vector2Int c = new Vector2Int(3, 4);

        Assert.AreEqual(0, HexMath.Distance(a, a));
        Assert.AreEqual(HexMath.Distance(a, b), HexMath.Distance(b, a));
        Assert.LessOrEqual(HexMath.Distance(a, b), HexMath.Distance(a, c) + HexMath.Distance(c, b));
    }

    [Test]
    public void HexMath_RotateAxial_SixStepsReturnsIdentity()
    {
        int q = 2;
        int r = -1;
        HexMath.RotateAxial(q, r, 6, out int outQ, out int outR);
        Assert.AreEqual(q, outQ);
        Assert.AreEqual(r, outR);

        HexMath.RotateAxial(1, 0, 1, out int q1, out int r1);
        Assert.AreEqual((0, 1), (q1, r1)); // East -> NorthEast
    }

    [Test]
    public void HexMath_FacingJudgements_FourModesWorkAndReturnFalseOnOrigin()
    {
        Vector2Int origin = new Vector2Int(4, 4);
        LookDirection facing = LookDirection.East;

        // 동일 좌표는 4종 모두 false
        Assert.IsFalse(HexMath.IsInDirectFront(origin, facing, origin));
        Assert.IsFalse(HexMath.IsInFrontArc(origin, facing, origin));
        Assert.IsFalse(HexMath.IsInDirectBack(origin, facing, origin));
        Assert.IsFalse(HexMath.IsInBackArc(origin, facing, origin));

        Vector2Int east = HexMath.GetOddRNeighbor(origin, LookDirection.East);
        Vector2Int ne = HexMath.GetOddRNeighbor(origin, LookDirection.NorthEast);
        Vector2Int nw = HexMath.GetOddRNeighbor(origin, LookDirection.NorthWest);
        Vector2Int west = HexMath.GetOddRNeighbor(origin, LookDirection.West);
        Vector2Int sw = HexMath.GetOddRNeighbor(origin, LookDirection.SouthWest);
        Vector2Int se = HexMath.GetOddRNeighbor(origin, LookDirection.SouthEast);

        // 전면 1칸 (0°)
        Assert.IsTrue(HexMath.IsInDirectFront(origin, facing, east));
        Assert.IsFalse(HexMath.IsInDirectFront(origin, facing, ne));
        Assert.IsFalse(HexMath.IsInDirectFront(origin, facing, west));

        // 전방 3칸 (0°, ±60°)
        Assert.IsTrue(HexMath.IsInFrontArc(origin, facing, east));
        Assert.IsTrue(HexMath.IsInFrontArc(origin, facing, ne));
        Assert.IsTrue(HexMath.IsInFrontArc(origin, facing, se));
        Assert.IsFalse(HexMath.IsInFrontArc(origin, facing, nw));
        Assert.IsFalse(HexMath.IsInFrontArc(origin, facing, west));

        // 후면 1칸 (180°)
        Assert.IsTrue(HexMath.IsInDirectBack(origin, facing, west));
        Assert.IsFalse(HexMath.IsInDirectBack(origin, facing, nw));
        Assert.IsFalse(HexMath.IsInDirectBack(origin, facing, east));

        // 후방 3칸 (±120°, 180°)
        Assert.IsTrue(HexMath.IsInBackArc(origin, facing, nw));
        Assert.IsTrue(HexMath.IsInBackArc(origin, facing, west));
        Assert.IsTrue(HexMath.IsInBackArc(origin, facing, sw));
        Assert.IsFalse(HexMath.IsInBackArc(origin, facing, ne));
        Assert.IsFalse(HexMath.IsInBackArc(origin, facing, east));
    }

    [Test]
    public void HexMath_GetLine_IsContinuousWithStepDistanceOne()
    {
        Vector2Int start = new Vector2Int(1, 1);
        Vector2Int end = new Vector2Int(7, 6);

        List<Vector2Int> line = HexMath.GetLine(start, end, includeOrigin: true);
        Assert.AreEqual(HexMath.Distance(start, end) + 1, line.Count);
        Assert.AreEqual(start, line[0]);
        Assert.AreEqual(end, line[line.Count - 1]);

        for (int i = 1; i < line.Count; i++)
        {
            Assert.AreEqual(1, HexMath.Distance(line[i - 1], line[i]));
        }
    }

    [Test]
    public void RoutePathfinding_FindsHexPathAndDetoursAroundWalls()
    {
        var (gameMap, _) = CreateTestGameMap(7, 7);
        var tileMap = gameMap.GetTileMap();
        tileMap[2][2].tileState = TileState.Full;
        tileMap[2][3].tileState = TileState.Full;

        Tile start = tileMap[1][2];
        Tile target = tileMap[3][2];
        List<Tile> path = RoutePathfinding.Shared.TilePathfinding(start, target, tileMap);

        Assert.IsNotNull(path);
        Assert.Greater(path.Count, 0);
        Assert.AreSame(target, path[path.Count - 1]);
        Assert.IsFalse(path.Contains(tileMap[2][2]));
        Assert.IsFalse(path.Contains(tileMap[2][3]));

        Tile prev = start;
        for (int i = 0; i < path.Count; i++)
        {
            Assert.AreEqual(1, Tile.GetDistance(prev, path[i]));
            prev = path[i];
        }
    }

    [Test]
    public void GridAreaSearch_ComputesHexRangeAndDirectionalSixRays()
    {
        var search = new GridAreaSearch();
        Vector2Int mapSize = new Vector2Int(11, 11);

        // 반경 2 육각 링(1..2): 6 + 12 = 18칸
        List<Vector2Int> area = search.GetGridArea(TargetType.AreaGround, mapSize, 5, 5, 1, 2);
        Assert.AreEqual(18, area.Count);
        for (int i = 0; i < area.Count; i++)
        {
            int d = HexMath.Distance(new Vector2Int(5, 5), area[i]);
            Assert.GreaterOrEqual(d, 1);
            Assert.LessOrEqual(d, 2);
        }

        // Directional NorthEast 3칸
        List<Vector2Int> neRay = search.GetGridArea(TargetType.Directional, mapSize, 5, 5, 1, 3, LookDirection.NorthEast);
        Assert.AreEqual(3, neRay.Count);
        Assert.AreEqual(HexMath.Step(new Vector2Int(5, 5), LookDirection.NorthEast, 3), neRay[2]);
    }

    [Test]
    public void EffectAreaShapeGenerator_AllHexShapesAndRotations_MatchSpec()
    {
        Vector2Int mapSize = new Vector2Int(15, 15);
        Vector2Int center = new Vector2Int(7, 7);

        // CircleFill: r=0 -> 1, r=1 -> 7, r=2 -> 19
        Assert.AreEqual(1, EffectAreaShapeGenerator.GetShapePositions("CircleFill", mapSize, 0, center.x, center.y, 0).Count);
        Assert.AreEqual(7, EffectAreaShapeGenerator.GetShapePositions("CircleFill", mapSize, 0, center.x, center.y, 1).Count);
        Assert.AreEqual(19, EffectAreaShapeGenerator.GetShapePositions("CircleFill", mapSize, 0, center.x, center.y, 2).Count);

        // CircleLine: r=1 -> 6, r=2 -> 12
        Assert.AreEqual(6, EffectAreaShapeGenerator.GetShapePositions("CircleLine", mapSize, 0, center.x, center.y, 1).Count);
        Assert.AreEqual(12, EffectAreaShapeGenerator.GetShapePositions("CircleLine", mapSize, 0, center.x, center.y, 2).Count);

        // Cross / XShape: 중심 + 4대각(±60°, ±120°) 각 r칸 (East 기준 East/West 제외)
        List<Vector2Int> cross = EffectAreaShapeGenerator.GetShapePositions("Cross", mapSize, 0, center.x, center.y, 1, LookDirection.East);
        Assert.AreEqual(5, cross.Count);
        Assert.IsTrue(cross.Contains(center));
        Assert.IsFalse(cross.Contains(HexMath.GetOddRNeighbor(center, LookDirection.East)));
        Assert.IsFalse(cross.Contains(HexMath.GetOddRNeighbor(center, LookDirection.West)));
        Assert.IsTrue(cross.Contains(HexMath.GetOddRNeighbor(center, LookDirection.NorthEast)));
        Assert.IsTrue(cross.Contains(HexMath.GetOddRNeighbor(center, LookDirection.SouthWest)));

        // HexTri: 중심 + 3방향 Y자(0°, ±120°)
        List<Vector2Int> tri = EffectAreaShapeGenerator.GetShapePositions("HexTri", mapSize, 0, center.x, center.y, 1, LookDirection.East);
        Assert.AreEqual(4, tri.Count);
        Assert.IsTrue(tri.Contains(HexMath.GetOddRNeighbor(center, LookDirection.East)));
        Assert.IsTrue(tri.Contains(HexMath.GetOddRNeighbor(center, LookDirection.NorthWest)));
        Assert.IsTrue(tri.Contains(HexMath.GetOddRNeighbor(center, LookDirection.SouthWest)));

        // HexCone: 6방향 모두 r=1 -> 3, r=2 -> 8
        for (int d = 0; d < HexMath.DirectionCount; d++)
        {
            LookDirection dir = (LookDirection)d;
            Assert.AreEqual(3, EffectAreaShapeGenerator.GetShapePositions("HexCone", mapSize, 0, center.x, center.y, 1, dir).Count, $"dir={dir}, r=1");
            Assert.AreEqual(8, EffectAreaShapeGenerator.GetShapePositions("HexCone", mapSize, 0, center.x, center.y, 2, dir).Count, $"dir={dir}, r=2");
        }

        // DirectionalLine width=3, distance=4 -> 12칸
        List<Vector2Int> wideLine = EffectAreaShapeGenerator.GetDirectionalLine(mapSize, center, LookDirection.NorthWest, 1, 4, width: 3);
        Assert.AreEqual(12, wideLine.Count);

        // Axial Pattern 6방향 회전 각도별 유일성 확인
        List<string> pattern = new List<string> { "1, 0", "2, 0", "1, 1" };
        List<Vector2Int> eastPattern = EffectAreaShapeGenerator.GetPatternWithRotation(mapSize, center, pattern, LookDirection.East);
        List<Vector2Int> nePattern = EffectAreaShapeGenerator.GetPatternWithRotation(mapSize, center, pattern, LookDirection.NorthEast);
        Assert.AreEqual(3, eastPattern.Count);
        Assert.AreEqual(3, nePattern.Count);
        CollectionAssert.AreNotEquivalent(eastPattern, nePattern);
    }

    [Test]
    public void GameMap_KnockbackAndAoEBlocking_WorkAcrossOddEvenRows()
    {
        var (gameMap, _) = CreateTestGameMap(10, 10);
        gameMap.SetTileState(new Vector2Int(5, 6), TileState.Full);

        // (4, 4)에서 NorthEast 3칸 밀치기 -> (4, 5) -> (5, 6) 벽 충돌 직전 (4, 5) 정지
        bool hitWall = gameMap.CalculateKnockbackPosition(new Vector2Int(4, 4), LookDirection.NorthEast, 3, out Vector2Int finalCoord, out Character collidedUnit);
        Assert.IsTrue(hitWall);
        Assert.IsNull(collidedUnit);
        Assert.AreEqual(new Vector2Int(4, 5), finalCoord);

        // (4, 5)에 생존 적 배치 후 (4, 4)에서 NorthEast 밀치기 -> 제자리 (4, 4) 정지 및 유닛 충돌 보고
        Character blocker = CreateTestCharacter("Blocker", gameMap.GetTile(4, 5));
        bool hitUnit = gameMap.CalculateKnockbackPosition(new Vector2Int(4, 4), LookDirection.NorthEast, 2, out Vector2Int unitStopCoord, out Character hitChar);
        Assert.IsTrue(hitUnit);
        Assert.AreSame(blocker, hitChar);
        Assert.AreEqual(new Vector2Int(4, 4), unitStopCoord);

        // 비관통 DirectionalLine AoE는 첫 번째 차단 타일에서 멈춰야 함
        var nonPiercingCard = new CardData
        {
            cardName = "TestHexRay",
            targetType = "Directional",
            targetMinDistance = 1,
            targetMaxDistance = 4,
            isPiercing = false,
            additionalEffectAreaList = new List<EffectArea>
            {
                new EffectArea { areaType = "DirectionalLine", distance = 4, width = 1 }
            }
        };

        List<Vector2Int> aoe = gameMap.CalculateCardAoECoords(new Vector2Int(4, 4), new Vector2Int(4, 4), nonPiercingCard, LookDirection.NorthEast);
        Assert.AreEqual(1, aoe.Count);
        Assert.AreEqual(new Vector2Int(4, 5), aoe[0]);
    }

    [Test]
    public void MapMoveRangeIndicator_AlignsQuadScaleAndCenterToPointyTopHexGrid()
    {
        var (gameMap, _) = CreateTestGameMap(14, 14, cellSize: 1.5f, gridOffset: new Vector3(2f, 0f, -3f));
        var indicatorGo = Track(new GameObject("HexIndicator", typeof(MapMoveRangeIndicator)));
        var indicator = indicatorGo.GetComponent<MapMoveRangeIndicator>();
        indicator.tileRenderer = null;

        indicator.Initialize(14, 14, 1.5f, new Vector3(2f, 0f, -3f), gameMap.GetTileMap());

        float expectedWidth = (14f + 0.5f) * 1.5f;
        float expectedHeight = (13f * HexMath.HalfSqrt3 + 2f * HexMath.InvSqrt3) * 1.5f;
        float expectedCenterX = 2f + (13.5f * 0.5f) * 1.5f;
        float expectedCenterZ = -3f + (13f * HexMath.HalfSqrt3 * 0.5f) * 1.5f;

        Assert.AreEqual(expectedWidth, indicator.transform.localScale.x, 0.001f);
        Assert.AreEqual(expectedHeight, indicator.transform.localScale.y, 0.001f);
        Assert.AreEqual(expectedCenterX, indicator.transform.localPosition.x, 0.001f);
        Assert.AreEqual(expectedCenterZ, indicator.transform.localPosition.z, 0.001f);
    }

    [Test]
    public void AllExistingMapAssets_LoadAndRoundTripHexCoordinates()
    {
        string[] mapGuids = AssetDatabase.FindAssets("t:MapDataSO", new[] { "Assets/SO/Maps" });
        Assert.AreEqual(10, mapGuids.Length, "기존 MapDataSO 에셋 10개가 모두 존재해야 합니다.");

        var mapGo = Track(new GameObject("AssetValidationGameMap"));
        var gameMap = mapGo.AddComponent<GameMap>();
        var tilePrefab = Track(new GameObject("ValidationTilePrefab", typeof(Tile), typeof(BoxCollider)));
        gameMap.prefabTile = tilePrefab;

        for (int i = 0; i < mapGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(mapGuids[i]);
            MapDataSO mapData = AssetDatabase.LoadAssetAtPath<MapDataSO>(path);
            Assert.IsNotNull(mapData, $"MapDataSO 로드 실패: {path}");
            Assert.Greater(mapData.width, 0, path);
            Assert.Greater(mapData.height, 0, path);

            gameMap.TileCreateByMapData(mapData);
            var tileMap = gameMap.GetTileMap();
            Assert.AreEqual(mapData.width, tileMap.Count, path);

            for (int col = 0; col < mapData.width; col++)
            {
                Assert.AreEqual(mapData.height, tileMap[col].Count, $"{path} col={col}");
                for (int row = 0; row < mapData.height; row++)
                {
                    Tile tile = tileMap[col][row];
                    Assert.IsNotNull(tile, $"{path} ({col},{row})");
                    Vector2Int expected = new Vector2Int(col, row);
                    Assert.AreEqual(expected, tile.GetCoord(), $"{path} ({col},{row})");
                    Assert.AreEqual(expected, mapData.WorldToCell(tile.transform.localPosition), $"{path} WorldToCell ({col},{row})");
                    Assert.AreSame(tile, gameMap.GetTileAtWorldPosition(tile.transform.position), $"{path} GetTileAtWorldPosition ({col},{row})");
                }
            }
        }
    }
}
