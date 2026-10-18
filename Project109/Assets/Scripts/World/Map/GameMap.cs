using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using CardTypes;

public class GameMap : MonoBehaviour
{
    [SerializeField]
    private List<List<Tile>> tileMap;

    public GameObject prefabTile;
    public GameObject moveRangeIndicatorPrefab;
    private MapMoveRangeIndicator moveRangeIndicator;

    public int mapColumn;
    public int mapRow;

    /// <summary>
    /// 현재 활성화된 씬의 GameMap 인스턴스 (단일 접근 창구)
    /// </summary>
    public static GameMap current { get; private set; }

    /// <summary>
    /// 맵 내부 A* 경로 탐색 인스턴스
    /// </summary>
    private RoutePathfinding _routePathfinding;
    public RoutePathfinding routePathfinding
    {
        get
        {
            if (_routePathfinding == null)
            {
                _routePathfinding = new RoutePathfinding();
            }
            return _routePathfinding;
        }
    }

    private void Awake()
    {
        current = this;
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    public List<List<Tile>> GetTileMap()
    {
        return tileMap;
    }

    public MapMoveRangeIndicator GetMoveRangeIndicator()
    {
        return moveRangeIndicator;
    }

    /// <summary>
    /// 지정된 격자 좌표의 타일을 반환합니다. 유효하지 않은 범위일 경우 null을 반환합니다.
    /// </summary>
    public Tile GetTile(int col, int row)
    {
        if (!IsValidCoordinate(col, row) || tileMap == null) return null;
        return tileMap[col][row];
    }

    /// <summary>
    /// 지정된 격자 좌표의 타일을 반환합니다.
    /// </summary>
    public Tile GetTile(Vector2Int coord) => GetTile(coord.x, coord.y);

    /// <summary>
    /// 월드 3D 좌표에 가장 가까운 타일을 검색하여 반환합니다.
    /// </summary>
    public Tile GetTileAtWorldPosition(Vector3 worldPos)
    {
        if (tileMap == null || tileMap.Count == 0) return null;

        Tile closestTile = null;
        float minDistanceSqr = float.MaxValue;

        for (int col = 0; col < tileMap.Count; col++)
        {
            if (tileMap[col] == null) continue;
            for (int row = 0; row < tileMap[col].Count; row++)
            {
                Tile t = tileMap[col][row];
                if (t != null)
                {
                    float dx = t.transform.position.x - worldPos.x;
                    float dz = t.transform.position.z - worldPos.z;
                    float distSqr = dx * dx + dz * dz;

                    if (distSqr < minDistanceSqr)
                    {
                        minDistanceSqr = distSqr;
                        closestTile = t;
                    }
                }
            }
        }

        return closestTile;
    }

    /// <summary>
    /// 지정된 격자 좌표 타일의 상태(Empty, Obstacle 등)를 안전하게 변경합니다.
    /// </summary>
    public bool SetTileState(Vector2Int coord, TileState state)
    {
        Tile tile = GetTile(coord);
        if (tile == null) return false;
        tile.tileState = state;
        return true;
    }

    /// <summary>
    /// 지정된 타일 좌표가 맵 경계 내부의 유효한 격자 범위인지 확인합니다.
    /// </summary>
    public bool IsValidCoordinate(int col, int row)
    {
        return col >= 0 && col < mapColumn && row >= 0 && row < mapRow;
    }

    /// <summary>
    /// 지정된 타일 좌표가 벽(Full), 장애물(Obstacle)이거나 맵 바깥(장외)인지 판정합니다.
    /// 밀치기(넉백) 시 벽 충돌 여부를 검사할 때 공용으로 사용됩니다.
    /// </summary>
    public bool IsWallOrOutside(int col, int row)
    {
        // 맵 바깥인 경우 벽(장외) 판정
        if (!IsValidCoordinate(col, row))
        {
            return true;
        }

        Tile tile = tileMap[col][row];
        if (tile == null)
        {
            return true;
        }

        // 타일 상태가 Full(벽)이거나 Obstacle(장애물)이면 벽 판정
        return tile.tileState == TileState.Full || tile.tileState == TileState.Obstacle;
    }

    /// <summary>
    /// 특정 좌표에서 지정된 방향으로 밀려날 때, 최종 도달할 타일 좌표와 벽/장외 충돌 여부를 계산합니다.
    /// </summary>
    /// <param name="startCoord">밀치기가 시작되는 캐릭터의 타일 좌표</param>
    /// <param name="direction">밀려나는 방향 (상하좌우 단위 벡터. 예: Vector2Int.up, Vector2Int.down 등)</param>
    /// <param name="distance">밀려나는 최대 타일 수</param>
    /// <param name="finalCoord">최종 도달하는 타일 좌표 (벽/장외 충돌 시 충돌 직전 좌표)</param>
    /// <returns>벽이나 맵 바깥(장외)에 부딪혔다면 true(추가 데미지/기절 등 처리용), 안전하게 밀려났다면 false</returns>
    public bool CalculateKnockbackPosition(Vector2Int startCoord, Vector2Int direction, int distance, out Vector2Int finalCoord)
    {
        finalCoord = startCoord;
        bool isCollided = false;

        for (int i = 1; i <= distance; i++)
        {
            Vector2Int nextCoord = startCoord + direction * i;

            if (IsWallOrOutside(nextCoord.x, nextCoord.y))
            {
                isCollided = true;
                break;
            }

            finalCoord = nextCoord;
        }

        return isCollided;
    }

    /// <summary>
    /// 현재 맵에 생성된 모든 타일과 이동 인디케이터를 파괴하고 데이터를 초기화합니다.
    /// 스테이지 전환 시 GameMap 인스턴스를 재사용하기 위해 호출됩니다.
    /// </summary>
    public void ClearTiles()
    {
        // 1. 기존 타일 오브젝트 파괴
        if (tileMap != null)
        {
            for (int col = 0; col < tileMap.Count; col++)
            {
                if (tileMap[col] == null) continue;
                for (int row = 0; row < tileMap[col].Count; row++)
                {
                    Tile tile = tileMap[col][row];
                    if (tile != null)
                    {
                        Destroy(tile.gameObject);
                    }
                }
                tileMap[col].Clear();
            }
            tileMap.Clear();
            tileMap = null;
        }

        // 2. 이동 인디케이터 오브젝트 파괴
        if (moveRangeIndicator != null)
        {
            Destroy(moveRangeIndicator.gameObject);
            moveRangeIndicator = null;
        }

        // 3. 자식 오브젝트 중 혹시 남아있는 타일이나 인디케이터가 있다면 안전하게 정리
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

        // 4. 경로 탐색 인스턴스 및 맵 크기 초기화
        _routePathfinding = null;
        mapColumn = 0;
        mapRow = 0;
    }

    public void TileCreateByMapData(MapDataSO mapData)
    {
        ClearTiles();

        float startX = mapData.gridOffset.x;
        float startZ = mapData.gridOffset.z;

        tileMap = new List<List<Tile>>();
        for (int columnIndex = 0; columnIndex < mapData.width; columnIndex++)
        {
            tileMap.Add(new List<Tile>());
            for (int rowIndex = 0; rowIndex < mapData.height; rowIndex++)
            {
                Tile tile = Instantiate(prefabTile, transform).GetComponent<Tile>();
                tile.transform.localPosition = new Vector3(startX + (columnIndex) * mapData.cellSize,
                                                         0.01f,
                                                         startZ + (rowIndex) * mapData.cellSize);

                // 타일의 콜라이더 크기를 현재 맵의 cellSize에 맞춰 동기화 (콜라이더 중첩 방지)
                BoxCollider col = tile.GetComponent<BoxCollider>();
                if (col != null)
                {
                    col.size = new Vector3(mapData.cellSize, col.size.y, mapData.cellSize);
                }

                tile.SetCoord(columnIndex, rowIndex);
                tile.ownerMap = this;
                tileMap[columnIndex].Add(tile);
            }
        }

        //맵 크기 저장
        mapColumn = tileMap.Count;
        mapRow = tileMap[0].Count;

        Debug.Log($"MapManager Column: {mapColumn}, Row: {mapRow}");

        ProcessTileSetting(mapData);

        if (moveRangeIndicatorPrefab != null)
        {
            GameObject indicatorObj = Instantiate(moveRangeIndicatorPrefab, transform);
            moveRangeIndicator = indicatorObj.GetComponent<MapMoveRangeIndicator>();
            if (moveRangeIndicator != null)
            {
                moveRangeIndicator.Initialize(mapData.width, mapData.height, mapData.cellSize, mapData.gridOffset, tileMap);
            }
        }
        //맵의 외각선 생성
        //SetMapOutsideLine();
    }

    private void ProcessTileSetting(MapDataSO mapData)
    {
        //생성된 타일에 알맞은 정보 추가
        foreach (CellData cell in mapData.cells)
        {
            // eventID가 Trap이거나 objectID가 Trap으로 시작하는 경우 이동 가능(Trap)
            if (cell.eventID == "Trap" || cell.objectID.StartsWith("Trap"))
            {
                tileMap[cell.position.x][cell.position.y].tileState = TileState.Trap;
            }
            // 그 외에 objectID가 Empty가 아니거나, terrainID가 Empty이거나, eventID가 Block인 경우 이동 불가(Full)
            else if (cell.objectID != "Empty" || cell.terrainID == "Empty" || cell.eventID == "Block")
            {
                tileMap[cell.position.x][cell.position.y].tileState = TileState.Full;
            }
            // 그 외는 일반 이동 가능 타일
            else
            {
                tileMap[cell.position.x][cell.position.y].tileState = TileState.Empty;
            }
        }
    }

    #region Pathfinding & Spatial Queries (맵 공간 연산 단일 창구)

    /// <summary>
    /// 지정된 시작 타일에서 목표 타일까지의 경로를 A* 알고리즘으로 탐색합니다.
    /// </summary>
    public List<Tile> FindPath(Tile start, Tile target, MoverCapability capabilities = MoverCapability.None)
    {
        if (start == null || target == null || tileMap == null) return null;
        return routePathfinding.TilePathfinding(start, target, tileMap, capabilities);
    }

    /// <summary>
    /// 특정 타일에서 이동 능력치(거리, 이동 특성)에 따라 도달 가능한 모든 타일 목록을 BFS로 탐색합니다.
    /// </summary>
    public List<Tile> GetReachableTiles(Tile moveStart, int canMoveDistance, CharacterMove mover)
    {
        List<Tile> checkList = new List<Tile>();
        if (mover == null || moveStart == null || tileMap == null) return checkList;

        Queue<Tile> checkNextTiles = new Queue<Tile>();
        Queue<Tile> checkCurrentTiles = new Queue<Tile>();
        checkCurrentTiles.Enqueue(moveStart);

        int column = mapColumn;
        int row = mapRow;

        HashSet<Tile> visited = new HashSet<Tile>();
        visited.Add(moveStart);

        for (int currentDistance = 0; currentDistance < canMoveDistance; currentDistance++)
        {
            while (checkCurrentTiles.Count != 0)
            {
                Tile t = checkCurrentTiles.Dequeue();

                // 상,하,좌,우 순으로 탐색
                int[] dirX = { 0, 0, 1, -1 };
                int[] dirY = { 1, -1, 0, 0 };

                for (int i = 0; i < 4; i++)
                {
                    int x = t.GetCoord().x + dirX[i];
                    int y = t.GetCoord().y + dirY[i];

                    // 맵 범위 검사
                    if (!IsValidCoordinate(x, y)) continue;

                    Tile nextTile = tileMap[x][y];
                    if (nextTile == null || visited.Contains(nextTile)) continue;

                    // 시작 위치 제외
                    if (nextTile.GetCoord() == moveStart.GetCoord()) continue;

                    // 이동 및 전파 가능 여부 판단
                    if (nextTile.tileState == TileState.Full)
                    {
                        if (!mover.capabilities.HasFlag(MoverCapability.PassWalls)) continue;
                    }
                    else if (nextTile.tileState == TileState.Obstacle)
                    {
                        if (!mover.capabilities.HasFlag(MoverCapability.PassObstacles)) continue;
                    }

                    visited.Add(nextTile);
                    checkNextTiles.Enqueue(nextTile);

                    // 멈춰설 수 있는 타일만 최종 이동 범위에 추가하고 visual indicator 활성화
                    if (nextTile.CanEnter(mover))
                    {
                        nextTile.SetMoveIndicator(true);
                        checkList.Add(nextTile);
                    }
                }
            }

            checkCurrentTiles = new Queue<Tile>(checkNextTiles);
            checkNextTiles.Clear();
        }

        return checkList;
    }

    #endregion

    #region 카드 효과 범위 타일 제어 (Effect Area Tiles)

    [Header("Effect Area Tiles")]
    [SerializeField] private EffectAreaTile effectAreaTile;
    [SerializeField] private EffectAreaTile additionalEffectAreaTile;

    public EffectAreaTile MainEffectAreaTile => effectAreaTile;
    public EffectAreaTile AdditionalEffectAreaTile => additionalEffectAreaTile;

    public void RegisterEffectAreaTiles(EffectAreaTile mainTile, EffectAreaTile additionalTile)
    {
        effectAreaTile = mainTile;
        additionalEffectAreaTile = additionalTile;
    }

    /// <summary>
    /// 카드 데이터의 문자열 형태 targetType을 TargetType enum으로 안전하게 변환합니다.
    /// "Enemy", "Single", "Target" 등의 동의어를 TargetType.Target으로 정규화합니다.
    /// </summary>
    public static TargetType ParseTargetType(string targetTypeStr)
    {
        if (string.IsNullOrEmpty(targetTypeStr)) return TargetType.Target;
        string lower = targetTypeStr.Trim().ToLowerInvariant();
        switch (lower)
        {
            case "self":
                return TargetType.Self;
            case "area":
                return TargetType.Area;
            case "all":
                return TargetType.All;
            case "enemy":
            case "single":
            case "target":
            default:
                return TargetType.Target;
        }
    }

    /// <summary>
    /// 현재 활성 플레이어의 타일 그리드 좌표를 가져옵니다.
    /// </summary>
    public Vector2Int GetPlayerCoordinate()
    {
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            var pChar = GameSceneManager.instance.player.character;
            if (pChar != null)
            {
                if (pChar.characterMove != null && pChar.characterMove.GetCurrentTile() != null)
                {
                    return pChar.characterMove.GetCurrentTile().GetCoord();
                }

                // 폴백: 월드 좌표로부터 현재 타일 탐색
                Tile tileAtPos = GetTileAtWorldPosition(pChar.transform.position);
                if (tileAtPos != null)
                {
                    return tileAtPos.GetCoord();
                }
            }
        }
        return Vector2Int.zero;
    }

    /// <summary>
    /// 카드 데이터와 시전 원점 좌표를 바탕으로 유효 사거리 타일 좌표 목록을 계산합니다.
    /// </summary>
    public List<Vector2Int> CalculateCardRangeCoords(Vector2Int originCoord, CardData cardData)
    {
        if (cardData == null) return new List<Vector2Int>();

        TargetType parsedTargetType = ParseTargetType(cardData.targetType);
        GridAreaSearch search = new GridAreaSearch();
        return search.GetGridArea(parsedTargetType, new Vector2Int(mapColumn, mapRow), originCoord.x, originCoord.y, cardData.targetMinDistance, cardData.targetMaxDistance);
    }

    /// <summary>
    /// 카드 데이터와 타깃 좌표를 바탕으로 폭발 범위(AoE) 타일 좌표 목록을 계산합니다.
    /// </summary>
    public List<Vector2Int> CalculateCardAoECoords(Vector2Int targetCoord, CardData cardData)
    {
        List<Vector2Int> aoeCoords = new List<Vector2Int>();
        if (cardData == null) return aoeCoords;

        if (cardData.additionalEffectAreaList != null && cardData.additionalEffectAreaList.Count > 0)
        {
            foreach (var area in cardData.additionalEffectAreaList)
            {
                if (area == null) continue;
                var shapePositions = EffectAreaShapeGenerator.GetShapePositions(
                    area.areaType,
                    new Vector2Int(mapColumn, mapRow),
                    1,
                    targetCoord.x,
                    targetCoord.y,
                    area.distance);

                if (shapePositions != null)
                {
                    aoeCoords.AddRange(shapePositions);
                }
            }
        }
        else
        {
            // 추가 효과 범위가 없으면 대상 타일(targetCoord) 자체를 단일 타깃으로 추가
            aoeCoords.Add(targetCoord);
        }

        return aoeCoords;
    }

    /// <summary>
    /// 원점 좌표를 기준으로 카드의 사거리를 월드 타일맵에 표시합니다.
    /// </summary>
    public void ShowCardRange(Vector2Int originCoord, CardData cardData)
    {
        if (cardData == null || moveRangeIndicator == null) return;
        if (!moveRangeIndicator.gameObject.activeSelf)
        {
            moveRangeIndicator.gameObject.SetActive(true);
        }
        List<Vector2Int> rangeCoords = CalculateCardRangeCoords(originCoord, cardData);
        moveRangeIndicator.ShowCardRangeTiles(rangeCoords, tileMap);
    }

    /// <summary>
    /// 카드의 사거리를 유지하면서, 마우스가 위치한 대상 타일 중심의 폭발 범위(AoE)를 월드 타일맵에 실시간 표시합니다.
    /// </summary>
    public void ShowCardAoE(Vector2Int originCoord, Vector2Int targetCoord, CardData cardData)
    {
        if (cardData == null || moveRangeIndicator == null) return;
        if (!moveRangeIndicator.gameObject.activeSelf)
        {
            moveRangeIndicator.gameObject.SetActive(true);
        }
        List<Vector2Int> rangeCoords = CalculateCardRangeCoords(originCoord, cardData);
        List<Vector2Int> aoeCoords = CalculateCardAoECoords(targetCoord, cardData);
        moveRangeIndicator.ShowCardAoETiles(rangeCoords, aoeCoords, tileMap);
    }

    /// <summary>
    /// 월드 타일맵에 표시 중인 카드 사거리 및 폭발 범위 하이라이트를 제거합니다.
    /// </summary>
    public void ClearCardRange()
    {
        if (moveRangeIndicator != null)
        {
            moveRangeIndicator.ClearCardRange(tileMap);
        }
    }

    /// <summary>
    /// 카드 데이터의 사거리 및 효과 범위를 월드 타일맵과 프리뷰에 시각화합니다.
    /// </summary>
    public void UpdateEffectArea(CardData newCardData)
    {
        if (newCardData == null) return;

        // 1. 실제 월드 타일맵에 사거리 하이라이트 표시
        Vector2Int playerCoord = GetPlayerCoordinate();
        ShowCardRange(playerCoord, newCardData);

        // 2. 레거시 3D 스튜디오(CardDetailUI 호환용) 업데이트 유지
        if (effectAreaTile != null)
        {
            effectAreaTile.ClearAllTiles();
            SetEffectAreaFromTargetDistance(newCardData.targetType,
                                            newCardData.targetMinDistance,
                                            newCardData.targetMaxDistance,
                                            TileType.TargetTile);
        }

        if (newCardData.additionalEffectAreaList != null && additionalEffectAreaTile != null)
        {
            additionalEffectAreaTile.ClearAllTiles();
            foreach (EffectArea additionalEffectArea in newCardData.additionalEffectAreaList)
            {
                SetEffectAreaFromShapeGenerator(additionalEffectArea.areaType,
                                                Mathf.Abs(newCardData.targetMaxDistance - newCardData.targetMinDistance),
                                                additionalEffectArea.distance,
                                                TileType.AdditionalEffectTile);
            }
        }
    }

    /// <summary>
    /// 카드 객체로부터 효과 범위를 월드 타일맵에 시각화합니다.
    /// </summary>
    public void UpdateEffectArea(Card card)
    {
        if (card != null && card.cardData != null)
        {
            UpdateEffectArea(card.cardData);
        }
    }

    /// <summary>
    /// 표시 중인 모든 카드 효과 범위 타일을 초기화(제거)합니다.
    /// </summary>
    public void ClearEffectAreaTiles()
    {
        // 1. 월드 맵 하이라이트 클리어
        ClearCardRange();

        // 2. 레거시 3D 스튜디오 타일 클리어
        if (effectAreaTile != null)
        {
            effectAreaTile.ClearAllTiles();
        }

        if (additionalEffectAreaTile != null)
        {
            additionalEffectAreaTile.ClearAllTiles();
        }
    }

    public void SetEffectAreaFromTargetDistance(string cardTargetType, int minDistance, int maxDistance, TileType type)
    {
        if (effectAreaTile == null) return;

        TargetType parsedTargetType = ParseTargetType(cardTargetType);
        effectAreaTile.SetTileFromTargetDistance(parsedTargetType, minDistance, maxDistance, type);
    }

    public void SetEffectAreaFromShapeGenerator(string shapeName, int shapeLength, int radius, TileType type)
    {
        if (additionalEffectAreaTile == null) return;

        additionalEffectAreaTile.SetTileFromShapeGenerator(shapeName, shapeLength, radius, type);
    }

    #endregion
}
