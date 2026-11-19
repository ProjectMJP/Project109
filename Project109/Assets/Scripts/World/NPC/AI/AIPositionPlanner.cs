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

        Tile casterTile = (caster.characterMove != null) ? caster.characterMove.GetCurrentTile() : null;
        Tile targetTile = (target.characterMove != null) ? target.characterMove.GetCurrentTile() : null;
        if (casterTile == null || targetTile == null) return null;

        int minRange = card.cardData != null ? card.cardData.targetMinDistance : 0;
        int maxRange = card.cardData != null ? card.cardData.targetMaxDistance : 99;

        // 1. 이미 현재 위치에서 사거리가 만족되는지 확인
        int currentDist = Tile.GetDistance(casterTile, targetTile);
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
        CharacterMove mover = caster != null ? caster.characterMove : null;

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                Tile tile = tileMap[c][r];
                if (tile == null) continue;

                // 자기 타일이거나 진입 가능한 타일만 후보군
                if (tile != casterTile && !tile.CanEnter(mover))
                {
                    continue;
                }

                int dist = Tile.GetDistance(tile, targetTile);
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

        // 3. 시전자로부터의 맨해튼 거리 기준으로 오름차순 정렬 (가까운 타일부터 우선 탐색하여 프루닝 극대화)
        candidateTiles.Sort((a, b) => Tile.GetDistance(casterTile, a).CompareTo(Tile.GetDistance(casterTile, b)));

        Tile bestTile = null;
        int shortestPathLength = int.MaxValue;
        RoutePathfinding pathfinder = RoutePathfinding.Shared;
        MoverCapability caps = mover != null ? mover.capabilities : MoverCapability.None;

        foreach (var candidate in candidateTiles)
        {
            // 맨해튼 거리가 현재 발견된 최단 경로 이상이면 실제 유효 경로는 절대 더 짧을 수 없으므로 탐색 스킵 (Pruning)
            if (Tile.GetDistance(casterTile, candidate) >= shortestPathLength)
            {
                continue;
            }

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

        // 4. 경로를 찾지 못했더라도 대상에게 가장 가까워지는 후보 타일로 보정 (일관된 그리드 맨해튼 거리 적용)
        if (bestTile == null)
        {
            int minDistanceToTarget = int.MaxValue;
            foreach (var candidate in candidateTiles)
            {
                int d = Tile.GetDistance(candidate, targetTile);
                if (d < minDistanceToTarget)
                {
                    minDistanceToTarget = d;
                    bestTile = candidate;
                }
            }
        }

        return bestTile;
    }

    /// <summary>
    /// 적의 최대 이동력 범위에 맞춰 실제 이동할 수 있는 타일을 중간 지점으로 축소/선정합니다.
    /// moveRangeOverride가 지정되면 현재 이동력 대신 해당 예산을 기준으로 계산합니다.
    /// </summary>
    public static Tile GetClampedMoveTarget(Character caster, Tile finalDestination, GameMap gameMap, int? moveRangeOverride = null)
    {
        if (caster == null || finalDestination == null || gameMap == null) return null;

        Tile currentTile = (caster.characterMove != null) ? caster.characterMove.GetCurrentTile() : null;
        if (currentTile == null || currentTile == finalDestination) return currentTile;

        // 이번 턴 남은 이동 가능 칸수 또는 지정 예산 확인 (0 이하이면 이동 불가)
        int maxMoveRange = Mathf.Max(0, moveRangeOverride ?? caster.curMoveRange);
        if (maxMoveRange <= 0) return currentTile;

        List<List<Tile>> tileMap = gameMap.GetTileMap();
        RoutePathfinding pathfinder = RoutePathfinding.Shared;
        
        MoverCapability caps = (caster != null && caster.characterMove != null) ? caster.characterMove.capabilities : MoverCapability.None;
        List<Tile> path = pathfinder.TilePathfinding(currentTile, finalDestination, tileMap, caps);
        if (path == null || path.Count == 0) return currentTile;

        // 이동 사거리 한도로 경로 자르기
        int targetIndex = Mathf.Min(path.Count - 1, maxMoveRange - 1);

        // 잘린 도착 지점이 다른 살아있는 유닛에 의해 점유되어 있다면, 역순으로 거슬러 올라가며 비어있는 타일 선택
        while (targetIndex >= 0)
        {
            Tile candidate = path[targetIndex];
            if (candidate == currentTile || candidate.occupyingCharacter == null || candidate.occupyingCharacter.isDead || candidate.occupyingCharacter == caster)
            {
                return candidate;
            }
            targetIndex--;
        }

        return currentTile;
    }
}
