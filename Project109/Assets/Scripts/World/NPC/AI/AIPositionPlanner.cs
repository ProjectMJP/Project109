using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 격자 맵 상에서 적군 유닛이 시전하려는 카드의 사거리를 만족시키기 위해 
/// 이동해야 할 최적의 타일 위치를 계산하는 플래너 클래스입니다.
/// </summary>
public static class AIPositionPlanner
{
    /// <summary>
    /// 시전자가 특정 카드를 타겟에게 사용하기 위해 이동해야 할 최적의 타일을 계산합니다.
    /// </summary>
    public static Tile PlanOptimalPosition(Character caster, Character target, Card card, GameMap gameMap)
    {
        if (caster == null || target == null || card == null || gameMap == null) return null;

        Tile casterTile = caster.characterMove?.GetCurrentTile();
        Tile targetTile = target.characterMove?.GetCurrentTile();
        if (casterTile == null || targetTile == null) return null;

        int minRange = card.cardData != null ? card.cardData.targetMinDistance : 0;
        int maxRange = card.cardData != null ? card.cardData.targetMaxDistance : 99;

        // 1. 이미 현재 위치에서 사거리가 만족되는지 확인
        int currentDist = GetDistance(casterTile, targetTile);
        if (currentDist >= minRange && currentDist <= maxRange)
        {
            return casterTile; // 이동 필요 없음
        }

        // 2. 맵 전체 타일 중 타겟과의 거리가 카드의 사거리 내에 해당하는 빈 타일 탐색
        List<List<Tile>> tileMap = gameMap.GetTileMap();
        if (tileMap == null || tileMap.Count == 0) return null;

        List<Tile> candidateTiles = new List<Tile>();
        int cols = tileMap.Count;
        int rows = tileMap[0].Count;

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                Tile tile = tileMap[c][r];
                if (tile == null) continue;

                // 자기 타일이거나 비어있는 타일만 후보군
                if (tile != casterTile && tile.tileState != TileState.Empty && tile.tileState != TileState.Trap)
                {
                    // 장애물이 있거나 다른 유닛이 점유한 타일 제외
                    continue;
                }

                int dist = GetDistance(tile, targetTile);
                if (dist >= minRange && dist <= maxRange)
                {
                    candidateTiles.Add(tile);
                }
            }
        }

        if (candidateTiles.Count == 0)
        {
            return null; // 사거리를 만족할 수 있는 타일이 아예 없음
        }

        // 3. 후보 타일들 중 시전자의 현재 타일로부터 경로 길이가 가장 짧은 타일을 선택
        Tile bestTile = null;
        int shortestPathLength = int.MaxValue;
        
        // 경로 탐색을 위해 임시 RoutePathfinding 인스턴스 사용
        RoutePathfinding pathfinder = new RoutePathfinding();

        foreach (var candidate in candidateTiles)
        {
            // 경로 계산 (A* 또는 BFS 연동, capabilities 반영)
            MoverCapability caps = (caster != null && caster.characterMove != null) ? caster.characterMove.capabilities : MoverCapability.None;
            List<Tile> path = pathfinder.TilePathfinding(casterTile, candidate, tileMap, caps);
            if (path != null && path.Count > 0)
            {
                if (path.Count < shortestPathLength)
                {
                    shortestPathLength = path.Count;
                    bestTile = candidate;
                }
            }
        }

        // 4. 경로를 찾지 못했더라도 플레이어에게 가장 가까워지는 방향으로 보정
        if (bestTile == null)
        {
            float minDistanceToPlayer = float.MaxValue;
            foreach (var candidate in candidateTiles)
            {
                float d = Vector3.Distance(candidate.transform.position, targetTile.transform.position);
                if (d < minDistanceToPlayer)
                {
                    minDistanceToPlayer = d;
                    bestTile = candidate;
                }
            }
        }

        return bestTile;
    }

    /// <summary>
    /// 적의 최대 이동력 범위에 맞춰 실제 이동할 수 있는 타일을 중간 지점으로 축소/선정합니다.
    /// </summary>
    public static Tile GetClampedMoveTarget(Character caster, Tile finalDestination, GameMap gameMap)
    {
        if (caster == null || finalDestination == null || gameMap == null) return null;

        Tile currentTile = caster.characterMove?.GetCurrentTile();
        if (currentTile == null || currentTile == finalDestination) return currentTile;

        // 최대 이동 횟수 * 1회당 이동 제한
        int maxMoveRange = 3; 
        if (caster.curCharacterStat != null)
        {
            maxMoveRange = caster.curCharacterStat.maxMoveCount * caster.curCharacterStat.maxTilesPerMove;
        }

        List<List<Tile>> tileMap = gameMap.GetTileMap();
        RoutePathfinding pathfinder = new RoutePathfinding();
        
        MoverCapability caps = (caster != null && caster.characterMove != null) ? caster.characterMove.capabilities : MoverCapability.None;
        List<Tile> path = pathfinder.TilePathfinding(currentTile, finalDestination, tileMap, caps);
        if (path == null || path.Count == 0) return currentTile;

        // 이동 사거리 한도로 경로 자르기
        int targetIndex = Mathf.Min(path.Count - 1, maxMoveRange - 1);
        if (targetIndex >= 0 && targetIndex < path.Count)
        {
            return path[targetIndex];
        }

        return currentTile;
    }

    private static int GetDistance(Tile t1, Tile t2)
    {
        if (t1 == null || t2 == null) return int.MaxValue;
        var coord1 = t1.GetCoord();
        var coord2 = t2.GetCoord();
        return Mathf.Abs(coord1.x - coord2.x) + Mathf.Abs(coord1.y - coord2.y);
    }
}
