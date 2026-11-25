using System.Collections.Generic;
using UnityEngine;
using CardTypes;

public class GridAreaSearch
{
    public List<Vector2Int> GetGridArea(
        TargetType cardTargetType,
        Vector2Int mapSize,
        int targetX,
        int targetY,
        int minDist,
        int maxDist,
        LookDirection facing = LookDirection.East)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        switch (cardTargetType)
        {
            case TargetType.SingleEnemy:
            case TargetType.MultiEnemy:
            case TargetType.RandomEnemy:
            case TargetType.SingleAlly:
            case TargetType.AreaSelf:
            case TargetType.AreaGround:
                CollectHexRange(mapSize, targetX, targetY, minDist, maxDist, results);
                return results;

            case TargetType.Self:
                if (targetX >= 0 && targetX < mapSize.x && targetY >= 0 && targetY < mapSize.y)
                {
                    results.Add(new Vector2Int(targetX, targetY));
                }
                return results;

            case TargetType.AllEnemies:
            case TargetType.AllAllies:
                for (int x = 0; x < mapSize.x; x++)
                {
                    for (int y = 0; y < mapSize.y; y++)
                    {
                        results.Add(new Vector2Int(x, y));
                    }
                }
                return results;

            case TargetType.Directional:
                Vector2Int origin = new Vector2Int(targetX, targetY);
                if (minDist == 0 && maxDist == 0)
                {
                    if (origin.x >= 0 && origin.x < mapSize.x && origin.y >= 0 && origin.y < mapSize.y)
                    {
                        results.Add(origin);
                    }
                }
                else
                {
                    int startStep = Mathf.Max(0, minDist);
                    int endStep = Mathf.Max(startStep, maxDist);
                    for (int d = startStep; d <= endStep; d++)
                    {
                        Vector2Int coord = HexMath.Step(origin, facing, d);
                        if (coord.x >= 0 && coord.x < mapSize.x && coord.y >= 0 && coord.y < mapSize.y)
                        {
                            results.Add(coord);
                        }
                    }
                }
                return results;
        }

        return results;
    }

    private static void CollectHexRange(
        Vector2Int mapSize,
        int centerCol,
        int centerRow,
        int minDist,
        int maxDist,
        List<Vector2Int> results)
    {
        if (maxDist < 0 || maxDist < minDist) return;

        HexMath.OddRToAxial(centerCol, centerRow, out int cq, out int cr);
        int clampedMin = Mathf.Max(0, minDist);

        for (int dq = -maxDist; dq <= maxDist; dq++)
        {
            int rMin = Mathf.Max(-maxDist, -dq - maxDist);
            int rMax = Mathf.Min(maxDist, -dq + maxDist);
            for (int dr = rMin; dr <= rMax; dr++)
            {
                int ds = -dq - dr;
                int dist = (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) >> 1;
                if (dist < clampedMin || dist > maxDist) continue;

                HexMath.AxialToOddR(cq + dq, cr + dr, out int col, out int row);
                if (col >= 0 && col < mapSize.x && row >= 0 && row < mapSize.y)
                {
                    results.Add(new Vector2Int(col, row));
                }
            }
        }
    }
}
