using UnityEngine;
using System.Collections.Generic;
using System;

public class EffectAreaShapeGenerator : MonoBehaviour
{
    public static EffectAreaShapeGenerator instance { get; private set; }

    private Dictionary<string, Func<Vector2Int, int, int, int, int, List<Vector2Int>>> shapeAlgorithms;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;

            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }

        shapeAlgorithms = new Dictionary<string, Func<Vector2Int, int, int, int, int, List<Vector2Int>>>()
        {
            { "None",       GetDefaultShape },  // 기본값 (중심점만)
            { "SquareLine", GetSquareLine },    // 빈 네모
            { "SquareFill", GetSquareFill },    // 꽉 찬 네모
            { "CircleLine", GetCircleLine },    // 빈 원
            { "CircleFill", GetCircleFill },    // 꽉 찬 원
            { "Cross",      GetCross },         // 십자가 (+)
            { "XShape",     GetXShape },        // X 자
            { "TargetLine", GetTargetLine }     // 타겟 지정 라인
        };
    }

    public List<Vector2Int> GetShapePositions(string shapeName, Vector2Int mapSize, int shapeLength, int targetX, int targetY, int radius)
    {
        if (shapeAlgorithms.TryGetValue(shapeName, out var algorithm))
        {
            return algorithm(mapSize, targetX, targetY, shapeLength, radius);
        }
        else
        {
            Debug.LogWarning($"Shape algorithm '{shapeName}' not found.");
            return new List<Vector2Int>();
        }
    }

    private List<Vector2Int> GetDefaultShape(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

       results.Add(new Vector2Int(targetX, targetY));

        return results;
    }

    private List<Vector2Int> GetSquareLine(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int x = targetX - radius; x <= targetX + radius; x++)
        {
           for (int y = targetY - radius; y <= targetY + radius; y++)
           {
                int dx = Math.Abs(x - targetX);
                int dy = Math.Abs(y - targetY);

                //둘 중 더 큰 값이 radius와 같으면 테두리 위치
                if (Mathf.Max(dx, dy) == radius)
                {
                    if(x >= 0 && x < mapSize.x && y >= 0 && y < mapSize.y)
                        results.Add(new Vector2Int(x, y));
                }
            }
        }

        return results;
    }

    private List<Vector2Int> GetSquareFill(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int x = targetX - radius; x <= targetX + radius; x++)
        {
            for (int y = targetY - radius; y <= targetY + radius; y++)
            {
                //범위 안이라면 모두 추가
                if (x >= 0 && x < mapSize.x && y >= 0 && y < mapSize.y)
                    results.Add(new Vector2Int(x, y));
            }
        }

        return results;
    }

    private List<Vector2Int> GetCircleLine(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int x = targetX - radius; x <= targetX + radius; x++)
        {
            for (int y = targetY - radius; y <= targetY + radius; y++)
            {
                float dist = Vector2.Distance(new Vector2(targetX, targetY), new Vector2(x, y));

                //값이 radius +- 0.5f 범위 내에 있다면 테두리 위치
                if (dist >= radius - 0.5f && dist <= radius + 0.5f)
                {
                    if (x >= 0 && x < mapSize.x && y >= 0 && y < mapSize.y)
                        results.Add(new Vector2Int(x, y));
                }
            }
        }

        return results;
    }

    private List<Vector2Int> GetCircleFill(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int x = targetX - radius; x <= targetX + radius; x++)
        {
            for (int y = targetY - radius; y <= targetY + radius; y++)
            {
                float dist = Vector2.Distance(new Vector2(targetX, targetY), new Vector2(x, y));

                //값이 radius + 0.5f 범위 내에 있다면 모두 추가
                if (dist <= radius + 0.5f)
                {
                    if (x >= 0 && x < mapSize.x && y >= 0 && y < mapSize.y)
                        results.Add(new Vector2Int(x, y));
                }
            }
        }

        return results;
    }

    private List<Vector2Int> GetCross(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int x = targetX - radius; x <= targetX + radius; x++)
            if (x >= 0 && x < mapSize.x)
                results.Add(new Vector2Int(x, targetY));
        for (int y = targetY - radius; y <= targetY + radius; y++)
            if (y >= 0 && y < mapSize.y)
                if (y != targetY) results.Add(new Vector2Int(targetX, y));

        return results;
    }

    private List<Vector2Int> GetXShape(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int i = -radius; i <= radius; i++)
        {
            if (targetX + i >= 0 && targetX + i < mapSize.x && targetY + i >= 0 && targetY + i < mapSize.y && targetY - i >= 0 && targetY - i < mapSize.y)
            {
                results.Add(new Vector2Int(targetX + i, targetY + i));
                if (i != 0) results.Add(new Vector2Int(targetX + i, targetY - i));
            }
        }

        return results;
    }

    private List<Vector2Int> GetTargetLine(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int x = targetX - (radius / 2); x <= targetX + (radius / 2); x++)
        {
            for (int y = targetY; y <= targetY + shapeLength; y++)
            {
                //범위 안이라면 모두 추가
                if (x >= 0 && x < mapSize.x && y >= 0 && y < mapSize.y)
                    results.Add(new Vector2Int(x, y));
            }
        }

        return results;
    }
}
