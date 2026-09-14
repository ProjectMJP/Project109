using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;
using CardTypes;

public class GridAreaSearch
{
    public List<Vector2Int> GetGridArea(TargetType cardTargetType, Vector2Int mapSize, int targetX, int targetY, int minDist, int maxDist)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        int startX = Mathf.Max(0, targetX - maxDist);
        int endX = Mathf.Min(mapSize.x - 1, targetX + maxDist);
        int startY = Mathf.Max(0, targetY - maxDist);
        int endY = Mathf.Min(mapSize.y - 1, targetY + maxDist);

        switch(cardTargetType)
        {
            case TargetType.Target:
                for (int x = startX; x <= endX; x++)
                {
                    if (x <= targetX - minDist || x >= targetX + minDist)
                    {
                        results.Add(new Vector2Int(x, targetY));
                    }
                }
                for (int y = startY; y <= endY; y++)
                {
                    if (y <= targetY - minDist || y >= targetY + minDist)
                    {
                        results.Add(new Vector2Int(targetX, y));
                    }
                }

                return results;
            case TargetType.Area:
                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        //상하좌우 이동 제한일 때 거리 계산 진행
                        int distance = Mathf.Abs(targetX - x) + Mathf.Abs(targetY - y);

                        if (distance >= minDist && distance <= maxDist)
                        {
                            results.Add(new Vector2Int(x, y));
                        }
                    }
                }

                return results;
            case TargetType.Self:
                results.Add(new Vector2Int(targetX, targetY));
                return results;
            case TargetType.All:
                for (int x = 0; x < mapSize.x; x++)
                {
                    for (int y = 0; y < mapSize.y; y++)
                    {
                        results.Add(new Vector2Int(x, y));
                    }
                }
                return results;
        }
 
        return results;
    }
}
