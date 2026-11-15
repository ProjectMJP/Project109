using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TileNode : IComparable<TileNode>
{
    public bool isWalkable;
    public int gridX;
    public int gridY;

    public int gCost;
    public int hCost;
    public TileNode parentNode;

    public TileNode(bool nisWalkable, int nGridX, int nGridY)
    {
        Reset(nisWalkable, nGridX, nGridY);
    }

    public void Reset(bool nisWalkable, int nGridX, int nGridY)
    {
        isWalkable = nisWalkable;
        gridX = nGridX;
        gridY = nGridY;
        gCost = 0;
        hCost = 0;
        parentNode = null;
    }

    public int fCost => gCost + hCost;

    public int CompareTo(TileNode other)
    {
        if (other == null) return 1;
        return fCost.CompareTo(other.fCost);
    }
}

public class RoutePathfinding
{
    /// <summary>
    /// 메인 스레드에서 반복 할당 없이 재사용할 수 있는 공유 길찾기 인스턴스입니다.
    /// </summary>
    public static readonly RoutePathfinding Shared = new RoutePathfinding();

    private readonly PriorityQueue<TileNode> _tileQueue = new PriorityQueue<TileNode>(64);
    private readonly HashSet<TileNode> _openList = new HashSet<TileNode>();
    private readonly HashSet<TileNode> _closeList = new HashSet<TileNode>();

    private TileNode[,] _tileNodeMap;
    private int _allocatedCols = 0;
    private int _allocatedRows = 0;

    public List<Tile> TilePathfinding(Tile start, Tile target, List<List<Tile>> map, MoverCapability capabilities = MoverCapability.None)
    {
        if (start == null || target == null || map == null || map.Count == 0 || map[0].Count == 0)
        {
            return new List<Tile>();
        }

        _tileQueue.Clear();
        _openList.Clear();
        _closeList.Clear();

        // 노드 맵 캐시 갱신 (무할당 재활용, 다른 유닛 점유 타일 차단)
        MakeTileNodeMap(map, capabilities, start, target);

        var startCoord = start.GetCoord();
        var targetCoord = target.GetCoord();

        if (startCoord.x < 0 || startCoord.x >= map.Count || startCoord.y < 0 || startCoord.y >= map[0].Count ||
            targetCoord.x < 0 || targetCoord.x >= map.Count || targetCoord.y < 0 || targetCoord.y >= map[0].Count)
        {
            return new List<Tile>();
        }

        TileNode startNode = _tileNodeMap[startCoord.x, startCoord.y];
        TileNode targetNode = _tileNodeMap[targetCoord.x, targetCoord.y];

        List<Tile> resultPath = new List<Tile>();

        _tileQueue.Enqueue(startNode);
        _openList.Add(startNode);

        int[] dirX = { 0, 0, 1, -1 };
        int[] dirY = { 1, -1, 0, 0 };

        while (_tileQueue.Count > 0)
        {
            TileNode currentTile = _tileQueue.Dequeue();

            _closeList.Add(currentTile);

            if (currentTile == targetNode)
            {
                // 현재까지 탐색한 방향 반대로 트래킹 시작
                resultPath = RetracePath(startNode, targetNode, map);
                return resultPath;
            }

            // 현재 타일의 상,하,좌,우 탐색(대각선은 탐색하지 않음)
            for (int i = 0; i < 4; i++)
            {
                int x = currentTile.gridX + dirX[i];
                int y = currentTile.gridY + dirY[i];

                // 맵을 넘어가거나 통과 불가능하거나 닫힌 목록에 있으면 제외
                if (x >= map.Count || y >= map[0].Count || x < 0 || y < 0)
                    continue;

                TileNode neighbor = _tileNodeMap[x, y];
                if (neighbor == null || !neighbor.isWalkable || _closeList.Contains(neighbor))
                    continue;

                // 현재 탐색중인 타일이 이전에 계산된 코스트보다 더 적거나 열린 목록에 없는 경우
                int newCurrentTileCost = currentTile.gCost + GetDistanceToTileNode(currentTile, neighbor);
                if (newCurrentTileCost < neighbor.gCost || !_openList.Contains(neighbor))
                {
                    neighbor.gCost = newCurrentTileCost;
                    neighbor.hCost = GetDistanceToTileNode(neighbor, targetNode);
                    neighbor.parentNode = currentTile;

                    _tileQueue.Enqueue(neighbor);
                    if (!_openList.Contains(neighbor))
                    {
                        _openList.Add(neighbor);
                    }
                }
            }
        }
        return resultPath;
    }

    private List<Tile> RetracePath(TileNode startNode, TileNode endNode, List<List<Tile>> map)
    {
        List<Tile> path = new List<Tile>();
        TileNode currentTileNode = endNode;

        while (currentTileNode != null && currentTileNode != startNode)
        {
            path.Add(map[currentTileNode.gridX][currentTileNode.gridY]);
            currentTileNode = currentTileNode.parentNode;
        }
        path.Reverse();

        return path;
    }

    private static int GetDistanceToTileNode(TileNode startNode, TileNode endNode)
    {
        int distX = Mathf.Abs(startNode.gridX - endNode.gridX);
        int distY = Mathf.Abs(startNode.gridY - endNode.gridY);

        // 가장 빠른 경로일 경우의 두 노드간의 거리 계산 (각 칸 가중치 10)
        return 10 * (distX + distY);
    }

    // 탐색을 위한 타일 맵 생성 및 캐시 재활용
    private void MakeTileNodeMap(List<List<Tile>> map, MoverCapability capabilities, Tile start = null, Tile target = null)
    {
        int column = map.Count;
        int row = map[0].Count;

        // 크기가 커졌을 때만 신규 배열 할당
        if (_tileNodeMap == null || column > _allocatedCols || row > _allocatedRows)
        {
            _allocatedCols = Mathf.Max(column, _allocatedCols);
            _allocatedRows = Mathf.Max(row, _allocatedRows);
            _tileNodeMap = new TileNode[_allocatedCols, _allocatedRows];

            for (int c = 0; c < _allocatedCols; c++)
            {
                for (int r = 0; r < _allocatedRows; r++)
                {
                    _tileNodeMap[c, r] = new TileNode(false, c, r);
                }
            }
        }

        // 기존 인스턴스에 값만 재설정 (GC Alloc 없음)
        for (int columnIndex = 0; columnIndex < column; columnIndex++)
        {
            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                Tile tile = map[columnIndex][rowIndex];
                TileState state = tile != null ? tile.tileState : TileState.Full;
                bool isWalkable;

                if (state == TileState.Full)
                {
                    isWalkable = capabilities.HasFlag(MoverCapability.PassWalls);
                }
                else if (state == TileState.Obstacle)
                {
                    isWalkable = capabilities.HasFlag(MoverCapability.PassObstacles);
                }
                else
                {
                    isWalkable = true;
                }

                // 시작 타일과 목표 타일을 제외한, 다른 살아있는 유닛이 점유 중인 타일은 통과 불가 처리
                if (isWalkable && tile != null && tile != start && tile != target)
                {
                    if (tile.occupyingCharacter != null && !tile.occupyingCharacter.isDead)
                    {
                        isWalkable = false;
                    }
                }

                _tileNodeMap[columnIndex, rowIndex].Reset(isWalkable, columnIndex, rowIndex);
            }
        }
    }
}
