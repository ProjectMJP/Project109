using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// 다양한 기하학적 형태(직사각형, 원, 십자 등)의 타일 좌표 목록을 계산하는 순수 알고리즘 유틸리티 클래스입니다.
/// MonoBehaviour 오버헤드 없이 정적(static)으로 동작합니다.
/// </summary>
public static class EffectAreaShapeGenerator
{
    private static readonly Dictionary<string, Func<Vector2Int, int, int, int, int, List<Vector2Int>>> shapeAlgorithms =
        new Dictionary<string, Func<Vector2Int, int, int, int, int, List<Vector2Int>>>()
        {
            { "None",       GetDefaultShape },  // 기본값 (중심점만)
            { "SquareLine", GetSquareLine },    // 빈 네모
            { "SquareFill", GetSquareFill },    // 꽉 찬 네모
            { "CircleLine", GetCircleLine },    // 빈 원
            { "CircleFill", GetCircleFill },    // 꽉 찬 원
            { "Cross",           GetCross },           // 십자가 (+)
            { "XShape",          GetXShape },          // X 자
            { "TargetLine",      GetTargetLine },      // 타겟 지정 라인
            { "DirectionalLine", GetTargetLine },      // 방향성 직진 라인
            { "Line",            GetTargetLine },      // 직선 라인
            { "WholeMap",        GetWholeMapShape },   // 맵 전체
            { "All",             GetWholeMapShape }    // 맵 전체 (별칭)
        };

    public static List<Vector2Int> GetShapePositions(string shapeName, Vector2Int mapSize, int shapeLength, int targetX, int targetY, int radius)
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

    private static List<Vector2Int> GetDefaultShape(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        results.Add(new Vector2Int(targetX, targetY));
        return results;
    }

    private static List<Vector2Int> GetSquareLine(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int x = targetX - radius; x <= targetX + radius; x++)
        {
            for (int y = targetY - radius; y <= targetY + radius; y++)
            {
                int dx = Math.Abs(x - targetX);
                int dy = Math.Abs(y - targetY);

                // 둘 중 더 큰 값이 radius와 같으면 테두리 위치
                if (Mathf.Max(dx, dy) == radius)
                {
                    if (x >= 0 && x < mapSize.x && y >= 0 && y < mapSize.y)
                        results.Add(new Vector2Int(x, y));
                }
            }
        }

        return results;
    }

    private static List<Vector2Int> GetSquareFill(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int x = targetX - radius; x <= targetX + radius; x++)
        {
            for (int y = targetY - radius; y <= targetY + radius; y++)
            {
                // 범위 안이라면 모두 추가
                if (x >= 0 && x < mapSize.x && y >= 0 && y < mapSize.y)
                    results.Add(new Vector2Int(x, y));
            }
        }

        return results;
    }

    private static List<Vector2Int> GetCircleLine(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int x = targetX - radius; x <= targetX + radius; x++)
        {
            for (int y = targetY - radius; y <= targetY + radius; y++)
            {
                float dist = Vector2.Distance(new Vector2(targetX, targetY), new Vector2(x, y));

                // 값이 radius +- 0.5f 범위 내에 있다면 테두리 위치
                if (dist >= radius - 0.5f && dist <= radius + 0.5f)
                {
                    if (x >= 0 && x < mapSize.x && y >= 0 && y < mapSize.y)
                        results.Add(new Vector2Int(x, y));
                }
            }
        }

        return results;
    }

    private static List<Vector2Int> GetCircleFill(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();

        for (int x = targetX - radius; x <= targetX + radius; x++)
        {
            for (int y = targetY - radius; y <= targetY + radius; y++)
            {
                float dist = Vector2.Distance(new Vector2(targetX, targetY), new Vector2(x, y));

                // 값이 radius + 0.5f 범위 내에 있다면 모두 추가
                if (dist <= radius + 0.5f)
                {
                    if (x >= 0 && x < mapSize.x && y >= 0 && y < mapSize.y)
                        results.Add(new Vector2Int(x, y));
                }
            }
        }

        return results;
    }

    private static List<Vector2Int> GetCross(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
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

    private static List<Vector2Int> GetXShape(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
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

    private static List<Vector2Int> GetTargetLine(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        int safeLength = Mathf.Max(0, shapeLength);
        int halfWidth = Mathf.Max(0, radius / 2);

        for (int x = targetX - halfWidth; x <= targetX + halfWidth; x++)
        {
            for (int y = targetY; y <= targetY + safeLength; y++)
            {
                // 범위 안이라면 모두 추가
                if (x >= 0 && x < mapSize.x && y >= 0 && y < mapSize.y)
                    results.Add(new Vector2Int(x, y));
            }
        }

        return results;
    }

    private static List<Vector2Int> GetWholeMapShape(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius)
    {
        return GetWholeMap(mapSize);
    }

    /// <summary>
    /// 북쪽(Up, 0도) 기준 상대 오프셋 (dx, dy)를 지정된 시선 방향(facing)으로 90도 단위 정수 회전 변환합니다.
    /// </summary>
    public static Vector2Int RotateOffset(int dx, int dy, LookDirection facing)
    {
        switch (facing)
        {
            case LookDirection.Up:    return new Vector2Int(dx, dy);
            case LookDirection.Right: return new Vector2Int(dy, -dx);
            case LookDirection.Down:  return new Vector2Int(-dx, -dy);
            case LookDirection.Left:  return new Vector2Int(-dy, dx);
            default:                  return new Vector2Int(dx, dy);
        }
    }

    /// <summary>
    /// 시전자 좌표에서 목표 좌표를 향하는 방향을 가장 가까운 상하좌우 LookDirection으로 스냅하여 반환합니다.
    /// 두 좌표가 동일하면 fallback 방향을 반환합니다.
    /// </summary>
    public static LookDirection GetDirectionFromCoords(Vector2Int originCoord, Vector2Int targetCoord, LookDirection fallback = LookDirection.Down)
    {
        if (originCoord == targetCoord) return fallback;

        int dx = targetCoord.x - originCoord.x;
        int dy = targetCoord.y - originCoord.y;

        if (Mathf.Abs(dx) > Mathf.Abs(dy))
        {
            return dx > 0 ? LookDirection.Right : LookDirection.Left;
        }
        else
        {
            return dy > 0 ? LookDirection.Up : LookDirection.Down;
        }
    }

    /// <summary>
    /// 맵 전체의 모든 유효 타일 좌표를 반환합니다.
    /// </summary>
    public static List<Vector2Int> GetWholeMap(Vector2Int mapSize)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                results.Add(new Vector2Int(x, y));
            }
        }
        return results;
    }

    /// <summary>
    /// 시전자 좌표에서 시선 방향(facing)으로 minDistance~maxDistance 범위의 직선 타일 좌표를 생성합니다.
    /// </summary>
    public static List<Vector2Int> GetDirectionalLine(Vector2Int mapSize, Vector2Int originCoord, LookDirection facing, int minDistance, int maxDistance, int width = 1)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        int startStep = Mathf.Max(1, minDistance);
        int endStep = Mathf.Max(startStep, maxDistance);
        int halfWidth = Mathf.Max(0, (width - 1) / 2);

        for (int step = startStep; step <= endStep; step++)
        {
            for (int lat = -halfWidth; lat <= halfWidth; lat++)
            {
                Vector2Int coord = originCoord + RotateOffset(lat, step, facing);
                if (coord.x >= 0 && coord.x < mapSize.x && coord.y >= 0 && coord.y < mapSize.y)
                {
                    results.Add(coord);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 상대 오프셋 텍스트 패턴을 파싱하고 시선 방향(facing)으로 4방향 회전하여 맵 내 유효 타일 좌표를 반환합니다.
    /// </summary>
    public static List<Vector2Int> GetPatternWithRotation(Vector2Int mapSize, Vector2Int anchorCoord, List<string> pattern, LookDirection facing)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        if (pattern == null || pattern.Count == 0) return results;

        List<Vector2Int> offsets = ParsePatternOffsets(pattern);
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();

        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int rotated = RotateOffset(offsets[i].x, offsets[i].y, facing);
            Vector2Int worldCoord = anchorCoord + rotated;
            if (worldCoord.x >= 0 && worldCoord.x < mapSize.x && worldCoord.y >= 0 && worldCoord.y < mapSize.y)
            {
                if (seen.Add(worldCoord))
                {
                    results.Add(worldCoord);
                }
            }
        }

        return results;
    }

    private static List<Vector2Int> ParsePatternOffsets(List<string> pattern)
    {
        List<Vector2Int> offsets = new List<Vector2Int>();
        if (pattern == null || pattern.Count == 0) return offsets;

        bool isCoordList = false;
        for (int i = 0; i < pattern.Count; i++)
        {
            if (!string.IsNullOrEmpty(pattern[i]) && pattern[i].Contains(","))
            {
                isCoordList = true;
                break;
            }
        }

        if (isCoordList)
        {
            for (int i = 0; i < pattern.Count; i++)
            {
                string entry = pattern[i];
                if (string.IsNullOrWhiteSpace(entry)) continue;
                string[] parts = entry.Split(',');
                if (parts.Length >= 2 &&
                    int.TryParse(parts[0].Trim(), out int dx) &&
                    int.TryParse(parts[1].Trim(), out int dy))
                {
                    offsets.Add(new Vector2Int(dx, dy));
                }
            }
            return offsets;
        }

        int rows = pattern.Count;
        int maxCols = 0;
        for (int r = 0; r < rows; r++)
        {
            if (pattern[r] != null && pattern[r].Length > maxCols)
            {
                maxCols = pattern[r].Length;
            }
        }

        int originCol = maxCols / 2;
        int originRow = rows / 2;
        bool foundOrigin = false;

        for (int r = 0; r < rows && !foundOrigin; r++)
        {
            string rowStr = pattern[r];
            if (string.IsNullOrEmpty(rowStr)) continue;
            for (int c = 0; c < rowStr.Length; c++)
            {
                char ch = rowStr[c];
                if (ch == 'C' || ch == 'c' || ch == 'O' || ch == 'o' || ch == '@')
                {
                    originCol = c;
                    originRow = r;
                    foundOrigin = true;
                    break;
                }
            }
        }

        for (int r = 0; r < rows; r++)
        {
            string rowStr = pattern[r];
            if (string.IsNullOrEmpty(rowStr)) continue;
            for (int c = 0; c < rowStr.Length; c++)
            {
                char ch = rowStr[c];
                if (ch == 'X' || ch == 'x' || ch == '1' || ch == '#' || ch == '*' || ch == '@')
                {
                    int dx = c - originCol;
                    int dy = originRow - r;
                    offsets.Add(new Vector2Int(dx, dy));
                }
            }
        }

        return offsets;
    }
}
