using CardTypes;
using System.Collections.Generic;
using UnityEngine;

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
    private float _cellSize = 1.0f;
    private Vector3 _gridOffset = Vector3.zero;

    private static readonly int[] _dirX = { 0, 0, 1, -1 };
    private static readonly int[] _dirY = { 1, -1, 0, 0 };

    private readonly Queue<Tile> _bfsCurrentQueue = new Queue<Tile>(32);
    private readonly Queue<Tile> _bfsNextQueue = new Queue<Tile>(32);
    private readonly HashSet<Tile> _bfsVisited = new HashSet<Tile>();

    /// <summary>
    /// 월드 좌표를 그리드 오프셋 및 셀 크기 기반 O(1) 산술 인덱싱으로 변환하여 타일을 반환합니다.
    /// 그리드 범위를 벗어나거나 타일이 null이면 null을 반환합니다.
    /// </summary>
    public Tile GetTileAtWorldPosition(Vector3 worldPos)
    {
        if (tileMap == null || tileMap.Count == 0 || _cellSize <= 0f) return null;

        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        Vector2Int coord = MapDataSO.WorldToCell(localPos, _gridOffset, _cellSize);

        if (!IsValidCoordinate(coord.x, coord.y)) return null;
        if (coord.x >= tileMap.Count || tileMap[coord.x] == null || coord.y >= tileMap[coord.x].Count) return null;

        return tileMap[coord.x][coord.y];
    }

    /// <summary>
    /// 카메라 중앙 시선 Ray가 맵 지면 평면과 전방 교차하며 유효한 카메라 패닝 허용 범위 내에 있는지 검사합니다.
    /// 물리 Raycast 없이 O(1) 평면 교차 및 그리드 영역 판정으로 동작합니다.
    /// </summary>
    public bool ValidateCameraPanRay(Ray centerRay)
    {
        Plane groundPlane = new Plane(Vector3.up, transform.position);
        if (!groundPlane.Raycast(centerRay, out float enter) || enter < 0f)
        {
            return false;
        }

        if (mapColumn <= 0 || mapRow <= 0 || _cellSize <= 0f)
        {
            return true;
        }

        Vector3 localHit = transform.InverseTransformPoint(centerRay.GetPoint(enter));
        float minX = _gridOffset.x - (_cellSize * 0.5f);
        float maxX = _gridOffset.x + ((mapColumn - 0.5f) * _cellSize);
        float minZ = _gridOffset.z - (_cellSize * 0.5f);
        float maxZ = _gridOffset.z + ((mapRow - 0.5f) * _cellSize);

        // 하단 손패 UI 시야 확보 및 가장자리 여유 패닝을 위한 마진 부여
        float margin = Mathf.Max(12f, Mathf.Max(mapColumn, mapRow) * _cellSize);
        return localHit.x >= (minX - margin) && localHit.x <= (maxX + margin) &&
               localHit.z >= (minZ - margin) && localHit.z <= (maxZ + margin);
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
    /// <param name="direction">밀려나는 방향 (상하좌우 단위 벡터)</param>
    /// <param name="distance">밀려나는 최대 타일 수</param>
    /// <param name="finalCoord">최종 도달하는 타일 좌표 (벽/장외/유닛 충돌 시 충돌 직전 좌표)</param>
    /// <param name="collidedUnit">밀려나는 경로에서 충돌한 다른 생존 유닛 (없으면 null)</param>
    /// <returns>벽, 장애물, 장외, 또는 다른 생존 유닛에 부딪혔다면 true, 안전하게 밀려났다면 false</returns>
    public bool CalculateKnockbackPosition(Vector2Int startCoord, Vector2Int direction, int distance, out Vector2Int finalCoord, out Character collidedUnit)
    {
        finalCoord = startCoord;
        collidedUnit = null;
        bool isCollided = false;

        // 방향 벡터를 상하좌우 1칸 단위 격자 벡터로 정규화 (대각선 및 초과 벡터 오작동 방지)
        Vector2Int stepDir = Vector2Int.zero;
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            stepDir = new Vector2Int(direction.x > 0 ? 1 : (direction.x < 0 ? -1 : 0), 0);
        else
            stepDir = new Vector2Int(0, direction.y > 0 ? 1 : (direction.y < 0 ? -1 : 0));

        if (stepDir == Vector2Int.zero || distance <= 0) return false;

        for (int i = 1; i <= distance; i++)
        {
            Vector2Int nextCoord = startCoord + stepDir * i;

            // 1. 벽, 장애물, 맵 바깥 충돌 검사
            if (IsWallOrOutside(nextCoord.x, nextCoord.y))
            {
                isCollided = true;
                break;
            }

            // 2. 다른 생존 유닛 점유 충돌 검사
            Tile nextTile = GetTile(nextCoord);
            if (nextTile != null && nextTile.occupyingCharacter != null && !nextTile.occupyingCharacter.isDead)
            {
                collidedUnit = nextTile.occupyingCharacter;
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
                        tile.ClearTile();
                        DestroyMapChildObject(tile.gameObject);
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
            DestroyMapChildObject(moveRangeIndicator.gameObject);
            moveRangeIndicator = null;
        }

        // 3. 자식 오브젝트 중 혹시 남아있는 타일이나 인디케이터가 있다면 안전하게 정리
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                DestroyMapChildObject(child.gameObject);
            }
        }

        // 4. 경로 탐색 인스턴스, BFS 버퍼 및 맵 크기/메트릭 초기화
        _routePathfinding = null;
        _bfsCurrentQueue.Clear();
        _bfsNextQueue.Clear();
        _bfsVisited.Clear();
        _cellSize = 1.0f;
        _gridOffset = Vector3.zero;
        mapColumn = 0;
        mapRow = 0;
    }

    private static void DestroyMapChildObject(GameObject obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(obj);
            return;
        }
#endif
        Destroy(obj);
    }

    public void TileCreateByMapData(MapDataSO mapData)
    {
        ClearTiles();
        if (mapData == null) return;

        _cellSize = mapData.cellSize;
        _gridOffset = mapData.gridOffset;

        tileMap = new List<List<Tile>>();
        for (int columnIndex = 0; columnIndex < mapData.width; columnIndex++)
        {
            tileMap.Add(new List<Tile>());
            for (int rowIndex = 0; rowIndex < mapData.height; rowIndex++)
            {
                Tile tile = Instantiate(prefabTile, transform).GetComponent<Tile>();
                Vector3 tileLocalPos = mapData.CellToWorld(columnIndex, rowIndex);
                tileLocalPos.y = 0.01f;
                tile.transform.localPosition = tileLocalPos;

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
        mapRow = mapColumn > 0 ? tileMap[0].Count : 0;

        Debug.Log($"GameMap Column: {mapColumn}, Row: {mapRow}");

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
        GetReachableTiles(moveStart, canMoveDistance, mover, checkList);
        return checkList;
    }

    /// <summary>
    /// 이동 가능한 타일 목록을 힙 할당 없이 전달받은 리스트에 채웁니다.
    /// (비주얼 인디케이터 표시는 MapMoveRangeIndicator가 별도로 처리합니다.)
    /// </summary>
    public void GetReachableTiles(Tile moveStart, int canMoveDistance, CharacterMove mover, List<Tile> results)
    {
        if (results == null) return;
        results.Clear();
        if (mover == null || moveStart == null || tileMap == null || canMoveDistance <= 0) return;

        Queue<Tile> currentQueue = _bfsCurrentQueue;
        Queue<Tile> nextQueue = _bfsNextQueue;
        currentQueue.Clear();
        nextQueue.Clear();
        _bfsVisited.Clear();

        try
        {
            currentQueue.Enqueue(moveStart);
            _bfsVisited.Add(moveStart);

            for (int currentDistance = 0; currentDistance < canMoveDistance; currentDistance++)
            {
                while (currentQueue.Count != 0)
                {
                    Tile t = currentQueue.Dequeue();
                    Vector2Int tCoord = t.GetCoord();

                    // 상,하,좌,우 순으로 탐색
                    for (int i = 0; i < 4; i++)
                    {
                        int x = tCoord.x + _dirX[i];
                        int y = tCoord.y + _dirY[i];

                        // 맵 범위 검사
                        if (!IsValidCoordinate(x, y)) continue;
                        if (x >= tileMap.Count || tileMap[x] == null || y >= tileMap[x].Count) continue;

                        Tile nextTile = tileMap[x][y];
                        if (nextTile == null || _bfsVisited.Contains(nextTile)) continue;

                        // 이동 및 전파 가능 여부 판단 (HasFlag 박싱 회피용 비트 연산)
                        if (nextTile.tileState == TileState.Full)
                        {
                            if ((mover.capabilities & MoverCapability.PassWalls) == 0) continue;
                        }
                        else if (nextTile.tileState == TileState.Obstacle)
                        {
                            if ((mover.capabilities & MoverCapability.PassObstacles) == 0) continue;
                        }

                        // 다른 살아있는 유닛이 점유 중인 타일은 통과 및 BFS 전파 모두 차단 (우회 거리 정확도 보장)
                        if (nextTile.occupyingCharacter != null && !nextTile.occupyingCharacter.isDead &&
                            nextTile.occupyingCharacter != mover.character)
                        {
                            continue;
                        }

                        _bfsVisited.Add(nextTile);
                        nextQueue.Enqueue(nextTile);

                        // 멈춰설 수 있는 타일만 최종 이동 범위에 추가 (비주얼 표시는 MapMoveRangeIndicator 전담)
                        if (nextTile.CanEnter(mover))
                        {
                            results.Add(nextTile);
                        }
                    }
                }

                // 큐 참조 스왑으로 힙 할당 없이 다음 깊이 탐색 준비
                Queue<Tile> temp = currentQueue;
                currentQueue = nextQueue;
                nextQueue = temp;
                nextQueue.Clear();
            }
        }
        finally
        {
            _bfsCurrentQueue.Clear();
            _bfsNextQueue.Clear();
            _bfsVisited.Clear();
        }
    }

    #endregion

    #region 카드 효과 범위 타일 제어 (Effect Area Tiles)

    [Header("Effect Area Tiles")]
    [SerializeField] private EffectAreaTile effectAreaTile;
    [SerializeField] private EffectAreaTile additionalEffectAreaTile;

    /// <summary>
    /// 지정된 캐릭터의 현재 타일 그리드 좌표를 가져옵니다.
    /// </summary>
    public Vector2Int GetCharacterCoordinate(Character character)
    {
        if (character != null)
        {
            if (character.characterMove != null && character.characterMove.GetCurrentTile() != null)
            {
                return character.characterMove.GetCurrentTile().GetCoord();
            }

            // 폴백: 월드 좌표로부터 현재 타일 탐색
            Tile tileAtPos = GetTileAtWorldPosition(character.transform.position);
            if (tileAtPos != null)
            {
                return tileAtPos.GetCoord();
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

        TargetType parsedTargetType = cardData.parsedTargetType;
        GridAreaSearch search = new GridAreaSearch();
        return search.GetGridArea(parsedTargetType, new Vector2Int(mapColumn, mapRow), originCoord.x, originCoord.y, cardData.targetMinDistance, cardData.targetMaxDistance);
    }

    /// <summary>
    /// Bresenham 정수 직선 알고리즘으로 originCoord에서 targetCoord까지의 격자 좌표 경로를 순서대로 반환합니다.
    /// </summary>
    public static List<Vector2Int> GetLineCoords(Vector2Int originCoord, Vector2Int targetCoord, bool includeOrigin = false)
    {
        List<Vector2Int> coords = new List<Vector2Int>();
        int x0 = originCoord.x;
        int y0 = originCoord.y;
        int x1 = targetCoord.x;
        int y1 = targetCoord.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (includeOrigin || x0 != originCoord.x || y0 != originCoord.y)
            {
                coords.Add(new Vector2Int(x0, y0));
            }

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return coords;
    }

    private bool IsTileBlockingProjectile(Tile tile)
    {
        if (tile == null) return true;
        if (tile.isBlocked) return true;
        if (tile.occupyingCharacter != null && !tile.occupyingCharacter.isDead) return true;
        if (tile.occupyingDestructible != null && !tile.occupyingDestructible.isDead) return true;
        return false;
    }

    /// <summary>
    /// 시전자 좌표, 타깃 좌표, 시선 방향 및 카드 데이터를 바탕으로 효과 범위(AoE) 및 투사체 시선(LoS) 타일 좌표 목록을 계산합니다.
    /// </summary>
    public List<Vector2Int> CalculateCardAoECoords(Vector2Int originCoord, Vector2Int targetCoord, CardData cardData, LookDirection facing = LookDirection.Down)
    {
        List<Vector2Int> aoeCoords = new List<Vector2Int>();
        if (cardData == null) return aoeCoords;

        LookDirection effectiveFacing = EffectAreaShapeGenerator.GetDirectionFromCoords(originCoord, targetCoord, facing);
        Vector2Int mapSize = new Vector2Int(mapColumn, mapRow);

        if (cardData.additionalEffectAreaList != null && cardData.additionalEffectAreaList.Count > 0)
        {
            foreach (var area in cardData.additionalEffectAreaList)
            {
                if (area == null) continue;

                bool isPiercing = cardData.isPiercing || area.isPiercing;
                bool ignoreObstacles = cardData.ignoreObstacles || area.ignoreObstacles;
                string areaType = area.areaType ?? string.Empty;

                if (areaType == "WholeMap" || areaType == "All")
                {
                    aoeCoords.AddRange(EffectAreaShapeGenerator.GetWholeMap(mapSize));
                }
                else if (areaType == "DirectionalLine" || areaType == "TargetLine" || areaType == "Line")
                {
                    int minDist = cardData.targetMinDistance > 0 ? cardData.targetMinDistance : 1;
                    int maxDist = cardData.targetMaxDistance > 0 ? Mathf.Max(cardData.targetMaxDistance, area.distance) : Mathf.Max(1, area.distance);
                    int width = area.width > 0 ? area.width : 1;

                    if (isPiercing || ignoreObstacles)
                    {
                        aoeCoords.AddRange(EffectAreaShapeGenerator.GetDirectionalLine(mapSize, originCoord, effectiveFacing, minDist, maxDist, width));
                    }
                    else
                    {
                        int halfWidth = Mathf.Max(0, (width - 1) / 2);
                        for (int step = minDist; step <= maxDist; step++)
                        {
                            bool blockedAtStep = false;
                            for (int lat = -halfWidth; lat <= halfWidth; lat++)
                            {
                                Vector2Int coord = originCoord + EffectAreaShapeGenerator.RotateOffset(lat, step, effectiveFacing);
                                if (!IsValidCoordinate(coord.x, coord.y)) continue;

                                aoeCoords.Add(coord);
                                if (IsTileBlockingProjectile(GetTile(coord)))
                                {
                                    blockedAtStep = true;
                                }
                            }

                            if (blockedAtStep)
                            {
                                break;
                            }
                        }
                    }
                }
                else if (areaType == "Pattern" || areaType == "PatternWithRotation")
                {
                    Vector2Int anchor = (cardData.parsedTargetType == TargetType.AreaGround) ? targetCoord : originCoord;
                    var patternPositions = EffectAreaShapeGenerator.GetPatternWithRotation(mapSize, anchor, area.pattern, effectiveFacing);
                    if (patternPositions != null)
                    {
                        aoeCoords.AddRange(patternPositions);
                    }
                }
                else
                {
                    Vector2Int center = (cardData.parsedTargetType == TargetType.AreaSelf ||
                                         cardData.parsedTargetType == TargetType.Directional ||
                                         cardData.parsedTargetType == TargetType.Self)
                        ? originCoord
                        : targetCoord;
                    int shapeLen = Mathf.Max(1, Mathf.Abs(cardData.targetMaxDistance - cardData.targetMinDistance));
                    var shapePositions = EffectAreaShapeGenerator.GetShapePositions(
                        areaType,
                        mapSize,
                        shapeLen,
                        center.x,
                        center.y,
                        area.distance);

                    if (shapePositions != null)
                    {
                        aoeCoords.AddRange(shapePositions);
                    }
                }
            }
        }
        else
        {
            if (cardData.parsedTargetType == TargetType.Directional && originCoord == targetCoord)
            {
                int minDist = cardData.targetMinDistance > 0 ? cardData.targetMinDistance : 1;
                int maxDist = cardData.targetMaxDistance > 0 ? cardData.targetMaxDistance : minDist;
                for (int step = minDist; step <= maxDist; step++)
                {
                    Vector2Int coord = originCoord + EffectAreaShapeGenerator.RotateOffset(0, step, effectiveFacing);
                    if (!IsValidCoordinate(coord.x, coord.y)) continue;

                    aoeCoords.Add(coord);
                    if (!cardData.isPiercing && !cardData.ignoreObstacles && IsTileBlockingProjectile(GetTile(coord)))
                    {
                        break;
                    }
                }
            }
            else if (originCoord != targetCoord && !cardData.ignoreObstacles)
            {
                List<Vector2Int> rayCoords = GetLineCoords(originCoord, targetCoord, false);
                for (int i = 0; i < rayCoords.Count; i++)
                {
                    Vector2Int coord = rayCoords[i];
                    if (!IsValidCoordinate(coord.x, coord.y)) continue;

                    aoeCoords.Add(coord);
                    if (!cardData.isPiercing && IsTileBlockingProjectile(GetTile(coord)))
                    {
                        break;
                    }
                }
            }
            else if (IsValidCoordinate(targetCoord.x, targetCoord.y))
            {
                aoeCoords.Add(targetCoord);
            }
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
    public void ShowCardAoE(Vector2Int originCoord, Vector2Int targetCoord, CardData cardData, LookDirection facing = LookDirection.Down)
    {
        if (cardData == null || moveRangeIndicator == null) return;
        if (!moveRangeIndicator.gameObject.activeSelf)
        {
            moveRangeIndicator.gameObject.SetActive(true);
        }
        bool skipRange = cardData.parsedTargetType == TargetType.AreaSelf || cardData.parsedTargetType == TargetType.Directional;
        List<Vector2Int> rangeCoords = skipRange ? null : CalculateCardRangeCoords(originCoord, cardData);
        List<Vector2Int> aoeCoords = CalculateCardAoECoords(originCoord, targetCoord, cardData, facing);
        moveRangeIndicator.ShowCardAoETiles(rangeCoords, aoeCoords, tileMap);
    }

    private LookDirection GetPlayerFacingDirection()
    {
        if (GameSceneManager.instance != null &&
            GameSceneManager.instance.player != null &&
            GameSceneManager.instance.player.character != null &&
            GameSceneManager.instance.player.character.characterMove != null)
        {
            return GameSceneManager.instance.player.character.characterMove.facingDirection;
        }
        return LookDirection.Down;
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
    /// 지정된 시전자 원점 좌표를 기준으로 카드 데이터의 사거리 및 효과 범위를 월드 타일맵과 프리뷰에 시각화합니다.
    /// </summary>
    public void UpdateEffectArea(Vector2Int originCoord, CardData newCardData)
    {
        if (newCardData == null) return;

        // 1. 실제 월드 타일맵에 하이라이트 표시 (즉발 광역/방향형은 즉시 AoE G 채널 표시)
        if (newCardData.parsedTargetType == TargetType.AreaSelf || newCardData.parsedTargetType == TargetType.Directional)
        {
            ShowCardAoE(originCoord, originCoord, newCardData, GetPlayerFacingDirection());
        }
        else
        {
            ShowCardRange(originCoord, newCardData);
        }

        // 2. 레거시 3D 스튜디오(CardDetailUI 호환용) 업데이트 유지
        if (effectAreaTile != null)
        {
            effectAreaTile.ClearAllTiles();
            SetEffectAreaFromTargetDistance(newCardData.parsedTargetType,
                                            newCardData.targetMinDistance,
                                            newCardData.targetMaxDistance,
                                            AreaIndicatorChannel.TargetTile);
        }

        if (newCardData.additionalEffectAreaList != null && additionalEffectAreaTile != null)
        {
            additionalEffectAreaTile.ClearAllTiles();
            foreach (EffectArea additionalEffectArea in newCardData.additionalEffectAreaList)
            {
                if (additionalEffectArea == null ||
                    additionalEffectArea.areaType == "Pattern" ||
                    additionalEffectArea.areaType == "PatternWithRotation")
                {
                    continue;
                }
                SetEffectAreaFromShapeGenerator(additionalEffectArea.areaType,
                                                Mathf.Abs(newCardData.targetMaxDistance - newCardData.targetMinDistance),
                                                additionalEffectArea.distance,
                                                AreaIndicatorChannel.AdditionalEffectTile);
            }
        }
    }

    /// <summary>
    /// 지정된 시전자 원점 좌표를 기준으로 카드 객체로부터 효과 범위를 월드 타일맵에 시각화합니다.
    /// </summary>
    public void UpdateEffectArea(Vector2Int originCoord, Card card)
    {
        if (card != null && card.cardData != null)
        {
            UpdateEffectArea(originCoord, card.cardData);
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

    public void SetEffectAreaFromTargetDistance(TargetType parsedTargetType, int minDistance, int maxDistance, AreaIndicatorChannel type)
    {
        if (effectAreaTile == null) return;

        effectAreaTile.SetTileFromTargetDistance(parsedTargetType, minDistance, maxDistance, type);
    }

    public void SetEffectAreaFromShapeGenerator(string shapeName, int shapeLength, int radius, AreaIndicatorChannel type)
    {
        if (additionalEffectAreaTile == null) return;

        additionalEffectAreaTile.SetTileFromShapeGenerator(shapeName, shapeLength, radius, type);
    }

    #endregion
}
