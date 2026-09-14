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
        isWalkable = nisWalkable;
        gridX = nGridX;
        gridY = nGridY;
    }

    public int fCost
    {
        get { return gCost + hCost; }
    }

    public int CompareTo(TileNode other)
    {
        return fCost.CompareTo(other.fCost);
    }
}

public class RoutePathfinding
{
    TileNode[,] tileNodeMap;

    public List<Tile> TilePathfinding(Tile start, Tile target, List<List<Tile>> map, MoverCapability capabilities = MoverCapability.None)
    {
        PriorityQueue<TileNode> TileList = new PriorityQueue<TileNode>();
        HashSet<TileNode> openList = new HashSet<TileNode>();
        HashSet<TileNode> closeList = new HashSet<TileNode>();

        //노드 맵 생성
        MakeTileNodeMap(map, capabilities);

        TileNode startNode = tileNodeMap[start.GetCoord().x, start.GetCoord().y];
        TileNode targetNode = tileNodeMap[target.GetCoord().x, target.GetCoord().y];

        List<Tile> resultPath = new List<Tile>();

        TileList.Enqueue(startNode);
        openList.Add(startNode);

        while (TileList.Count > 0)
        {
            TileNode currentTile = TileList.Dequeue();

            closeList.Add(currentTile);

            if (currentTile == targetNode)
            {
                //현재까지 탐색한 방향 반대로 트래킹 시작
                resultPath = RetracePath(startNode, targetNode, map);

                return resultPath;
            }

            //현재 타일의 상,하,좌,우 탐색(대각선은 탐색하지 않음)
            int[] dirX = { 0, 0, 1, -1 };
            int[] dirY = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int x = currentTile.gridX + dirX[i];
                int y = currentTile.gridY + dirY[i];

                //맵을 넘어가거나 비어있지 않을 경우 제외
                if (x >= map.Count || y >= map[0].Count || x < 0 || y < 0 || !tileNodeMap[x, y].isWalkable || closeList.Contains(tileNodeMap[x, y]))
                    continue;

                //현재 탐색중인 타일이 이전에 계산된 코스트보다 더 적을 경우 교환
                //또는 탐색된 적이 없는 경우 코스트 계산
                int newCurrentTileCost = currentTile.gCost + GetDistanceToTileNode(currentTile, tileNodeMap[x, y]);
                if(newCurrentTileCost < tileNodeMap[x, y].gCost || !openList.Contains(tileNodeMap[x, y]))
                {
                    tileNodeMap[x, y].gCost = newCurrentTileCost;
                    tileNodeMap[x, y].hCost = GetDistanceToTileNode(tileNodeMap[x, y], targetNode);
                    tileNodeMap[x, y].parentNode = currentTile;

                    TileList.Enqueue(tileNodeMap[x, y]);
                    if(!openList.Contains(tileNodeMap[x, y]))
                    {
                        openList.Add(tileNodeMap[x, y]);
                    }
                }
            }
        }
        return resultPath;
    }

    List<Tile> RetracePath(TileNode startNode, TileNode endNode, List<List<Tile>> map)
    {
        List<Tile> path = new List<Tile>();
        TileNode currentTileNode = endNode;

        while (currentTileNode != startNode) 
        {
            path.Add(map[currentTileNode.gridX][currentTileNode.gridY]);
            currentTileNode = currentTileNode.parentNode;
        }
        path.Reverse();

        return path;
    }

    int GetDistanceToTileNode(TileNode startNode, TileNode endNode)
    {
        int distX = Mathf.Abs(startNode.gridX - endNode.gridX);
        int distY = Mathf.Abs(startNode.gridY - endNode.gridY);

        //가장 빠른 경로일 경우의 두 노드간의 거리 계산
        //각 칸의 가중치는 10으로 계산함
        return 10 * (distX + distY);
    }

    //탐색을 위한 타일 맵 생성
    void MakeTileNodeMap(List<List<Tile>> map, MoverCapability capabilities)
    {
        int column = map.Count;
        int row = map[0].Count;

        tileNodeMap = new TileNode[column, row];

        for (int columnIndex = 0; columnIndex < column; columnIndex++)
        {
            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                TileState state = map[columnIndex][rowIndex].tileState;
                bool isWalkable = false;

                if (state == TileState.Full)
                {
                    // 벽(Full)을 관통하려면 PassWalls 플래그가 있어야 합니다.
                    isWalkable = capabilities.HasFlag(MoverCapability.PassWalls);
                }
                else if (state == TileState.Obstacle)
                {
                    // 장애물(Obstacle)을 관통하려면 PassObstacles 플래그가 있어야 합니다.
                    isWalkable = capabilities.HasFlag(MoverCapability.PassObstacles);
                }
                else
                {
                    // 일반 타일(Empty, Trap)은 항상 통과 가능
                    isWalkable = true;
                }

                tileNodeMap[columnIndex, rowIndex] = new TileNode(isWalkable, columnIndex, rowIndex);
                tileNodeMap[columnIndex, rowIndex].gCost = 0;
            }
        }
    }
}
