using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// 육각 격자(Pointy-Top Odd-R) 기반의 다양한 기하학적 효과 범위(원, 링, 4대각, Y자, 부채꼴, 직선, Axial 패턴)
/// 타일 좌표 목록을 계산하는 순수 알고리즘 유틸리티 클래스입니다.
/// MonoBehaviour 오버헤드 없이 정적(static)으로 동작합니다.
/// </summary>
public static class EffectAreaShapeGenerator
{
    private static readonly Dictionary<string, Func<Vector2Int, int, int, int, int, LookDirection, List<Vector2Int>>> shapeAlgorithms =
        new Dictionary<string, Func<Vector2Int, int, int, int, int, LookDirection, List<Vector2Int>>>()
        {
            { "None",            GetDefaultShape },     // 기본값 (중심점만)
            { "SquareLine",      GetHexRing },          // 육각 링 테두리
            { "SquareFill",      GetHexFill },          // 육각 반경 채우기
            { "CircleLine",      GetHexRing },          // 육각 링 테두리
            { "CircleFill",      GetHexFill },          // 육각 반경 채우기
            { "Cross",           GetHexDiagonalCross }, // 시선 기준 4대각 방향 (±60°, ±120°)
            { "XShape",          GetHexDiagonalCross }, // 시선 기준 4대각 방향 (±60°, ±120°)
            { "HexTri",          GetHexTri },           // 시선 기준 3방향 Y자 (0°, ±120°)
            { "HexCone",         GetHexCone },          // 시선 기준 전방 부채꼴 (0°, ±60°)
            { "TargetLine",      GetTargetLine },       // 타겟 지정 6방향 직선
            { "DirectionalLine", GetTargetLine },       // 방향성 6방향 직선
            { "Line",            GetTargetLine },       // 직선 라인
            { "WholeMap",        GetWholeMapShape },    // 맵 전체
            { "All",             GetWholeMapShape }     // 맵 전체 (별칭)
        };

    public static List<Vector2Int> GetShapePositions(
        string shapeName,
        Vector2Int mapSize,
        int shapeLength,
        int targetX,
        int targetY,
        int radius,
        LookDirection facing = LookDirection.East)
    {
        if (shapeAlgorithms.TryGetValue(shapeName, out var algorithm))
        {
            return algorithm(mapSize, targetX, targetY, shapeLength, radius, facing);
        }

        Debug.LogWarning($"Shape algorithm '{shapeName}' not found.");
        return new List<Vector2Int>();
    }

    private static bool IsInsideMap(int col, int row, Vector2Int mapSize)
    {
        return col >= 0 && col < mapSize.x && row >= 0 && row < mapSize.y;
    }

    private static List<Vector2Int> GetDefaultShape(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius, LookDirection facing)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        if (IsInsideMap(targetX, targetY, mapSize))
        {
            results.Add(new Vector2Int(targetX, targetY));
        }
        return results;
    }

    private static List<Vector2Int> GetHexRing(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius, LookDirection facing)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        if (radius < 0) return results;

        if (radius == 0)
        {
            if (IsInsideMap(targetX, targetY, mapSize))
            {
                results.Add(new Vector2Int(targetX, targetY));
            }
            return results;
        }

        HexMath.OddRToAxial(targetX, targetY, out int cq, out int cr);
        // 링 시작점: SouthWest(4) 방향으로 radius칸 이동 후 6변을 반시계 순회
        HexMath.AxialDirection(4, out int sq, out int sr);
        int curQ = cq + sq * radius;
        int curR = cr + sr * radius;

        for (int side = 0; side < HexMath.DirectionCount; side++)
        {
            HexMath.AxialDirection(side, out int dq, out int dr);
            for (int step = 0; step < radius; step++)
            {
                HexMath.AxialToOddR(curQ, curR, out int col, out int row);
                if (IsInsideMap(col, row, mapSize))
                {
                    results.Add(new Vector2Int(col, row));
                }
                curQ += dq;
                curR += dr;
            }
        }

        return results;
    }

    private static List<Vector2Int> GetHexFill(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius, LookDirection facing)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        if (radius < 0) return results;

        HexMath.OddRToAxial(targetX, targetY, out int cq, out int cr);

        for (int dq = -radius; dq <= radius; dq++)
        {
            int rMin = Mathf.Max(-radius, -dq - radius);
            int rMax = Mathf.Min(radius, -dq + radius);
            for (int dr = rMin; dr <= rMax; dr++)
            {
                HexMath.AxialToOddR(cq + dq, cr + dr, out int col, out int row);
                if (IsInsideMap(col, row, mapSize))
                {
                    results.Add(new Vector2Int(col, row));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 시선(facing) 기준 전면(0°)과 후면(180°)을 제외한 4개 대각 방향(±60°, ±120°)으로 radius칸만큼 뻗는 X자 범위입니다.
    /// </summary>
    private static List<Vector2Int> GetHexDiagonalCross(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius, LookDirection facing)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        Vector2Int origin = new Vector2Int(targetX, targetY);
        if (IsInsideMap(origin.x, origin.y, mapSize))
        {
            results.Add(origin);
        }

        if (radius <= 0) return results;

        int[] relativeSteps = { 1, 2, 4, 5 };
        for (int d = 0; d < relativeSteps.Length; d++)
        {
            LookDirection rayDir = HexMath.RotateDirection(facing, relativeSteps[d]);
            for (int step = 1; step <= radius; step++)
            {
                Vector2Int coord = HexMath.Step(origin, rayDir, step);
                if (IsInsideMap(coord.x, coord.y, mapSize))
                {
                    results.Add(coord);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 시선(facing) 기준 3방향 Y자 패턴(0°, +120°, -120°)으로 radius칸만큼 뻗는 범위입니다.
    /// </summary>
    private static List<Vector2Int> GetHexTri(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius, LookDirection facing)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        Vector2Int origin = new Vector2Int(targetX, targetY);
        if (IsInsideMap(origin.x, origin.y, mapSize))
        {
            results.Add(origin);
        }

        if (radius <= 0) return results;

        int[] relativeSteps = { 0, 2, 4 };
        for (int d = 0; d < relativeSteps.Length; d++)
        {
            LookDirection rayDir = HexMath.RotateDirection(facing, relativeSteps[d]);
            for (int step = 1; step <= radius; step++)
            {
                Vector2Int coord = HexMath.Step(origin, rayDir, step);
                if (IsInsideMap(coord.x, coord.y, mapSize))
                {
                    results.Add(coord);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 시선(facing) 기준 전방 부채꼴(0°, ±60°, 거리 1..radius) 범위를 정수 Axial 대칭 공식으로 계산합니다.
    /// 거리 r에서 r=1이면 3칸, r=2이면 총 8칸(3+5)을 6방향 모두에 대해 대칭으로 포함합니다.
    /// </summary>
    private static List<Vector2Int> GetHexCone(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius, LookDirection facing)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        if (radius <= 0) return results;

        HexMath.OddRToAxial(targetX, targetY, out int oq, out int or);
        HexMath.AxialDirection(facing, out int fq, out int fr);
        HexMath.AxialDirection(HexMath.RotateDirection(facing, 1), out int lq, out int lr);
        HexMath.AxialDirection(HexMath.RotateDirection(facing, 5), out int rq, out int rr);

        for (int dist = 1; dist <= radius; dist++)
        {
            for (int k = 0; k <= dist; k++)
            {
                HexMath.AxialToOddR(oq + fq * (dist - k) + lq * k, or + fr * (dist - k) + lr * k, out int col, out int row);
                if (IsInsideMap(col, row, mapSize))
                {
                    results.Add(new Vector2Int(col, row));
                }
            }

            for (int k = 1; k <= dist; k++)
            {
                HexMath.AxialToOddR(oq + fq * (dist - k) + rq * k, or + fr * (dist - k) + rr * k, out int col, out int row);
                if (IsInsideMap(col, row, mapSize))
                {
                    results.Add(new Vector2Int(col, row));
                }
            }
        }

        return results;
    }

    private static List<Vector2Int> GetTargetLine(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius, LookDirection facing)
    {
        int maxDist = Mathf.Max(1, Mathf.Max(shapeLength, radius));
        return GetDirectionalLine(mapSize, new Vector2Int(targetX, targetY), facing, 1, maxDist, 1);
    }

    private static List<Vector2Int> GetWholeMapShape(Vector2Int mapSize, int targetX, int targetY, int shapeLength, int radius, LookDirection facing)
    {
        return GetWholeMap(mapSize);
    }

    /// <summary>
    /// East(0°) 기준 Axial 상대 오프셋 (dq, dr)을 지정된 시선 방향(facing)으로 60° 단위 회전합니다.
    /// </summary>
    public static Vector2Int RotateAxialOffset(int dq, int dr, LookDirection facing)
    {
        HexMath.RotateAxial(dq, dr, (int)facing, out int outQ, out int outR);
        return new Vector2Int(outQ, outR);
    }

    /// <summary>
    /// 시전자 좌표에서 목표 좌표를 향하는 방향을 가장 가까운 6방향 LookDirection으로 스냅하여 반환합니다.
    /// 두 좌표가 동일하면 fallback 방향을 반환합니다.
    /// </summary>
    public static LookDirection GetDirectionFromCoords(Vector2Int originCoord, Vector2Int targetCoord, LookDirection fallback = LookDirection.East)
    {
        return HexMath.DirectionFromCoords(originCoord, targetCoord, fallback);
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
    /// 6방향 직선의 특정 step 거리에서 폭(width)을 이루는 Odd-R 좌표들을 outBuffer에 채웁니다.
    /// 좌우 확장은 진행 방향에 수직(±90°)이 되도록 +120°/+60° 및 -120°/-60°를 교대 누적합니다.
    /// </summary>
    public static void GetDirectionalLineStepCoords(Vector2Int originCoord, LookDirection facing, int step, int width, List<Vector2Int> outBuffer)
    {
        if (outBuffer == null) return;
        outBuffer.Clear();

        HexMath.OddRToAxial(originCoord.x, originCoord.y, out int oq, out int or);
        HexMath.AxialDirection(facing, out int fq, out int fr);

        int centerQ = oq + fq * step;
        int centerR = or + fr * step;
        outBuffer.Add(HexMath.AxialToOddR(centerQ, centerR));

        int halfWidth = Mathf.Max(0, (width - 1) / 2);
        if (halfWidth == 0) return;

        // 좌측 수직 확장: 홀수 스텝(+120°), 짝수 스텝(+60°)
        HexMath.AxialDirection(HexMath.RotateDirection(facing, 2), out int l1q, out int l1r);
        HexMath.AxialDirection(HexMath.RotateDirection(facing, 1), out int l2q, out int l2r);
        int lq = centerQ;
        int lr = centerR;
        for (int k = 1; k <= halfWidth; k++)
        {
            if ((k & 1) != 0)
            {
                lq += l1q;
                lr += l1r;
            }
            else
            {
                lq += l2q;
                lr += l2r;
            }
            outBuffer.Add(HexMath.AxialToOddR(lq, lr));
        }

        // 우측 수직 확장: 홀수 스텝(-120° / +4), 짝수 스텝(-60° / +5)
        HexMath.AxialDirection(HexMath.RotateDirection(facing, 4), out int r1q, out int r1r);
        HexMath.AxialDirection(HexMath.RotateDirection(facing, 5), out int r2q, out int r2r);
        int rq = centerQ;
        int rr = centerR;
        for (int k = 1; k <= halfWidth; k++)
        {
            if ((k & 1) != 0)
            {
                rq += r1q;
                rr += r1r;
            }
            else
            {
                rq += r2q;
                rr += r2r;
            }
            outBuffer.Add(HexMath.AxialToOddR(rq, rr));
        }
    }

    /// <summary>
    /// 시전자 좌표에서 시선 방향(facing)으로 minDistance~maxDistance 범위의 6방향 직선 타일 좌표를 생성합니다.
    /// </summary>
    public static List<Vector2Int> GetDirectionalLine(Vector2Int mapSize, Vector2Int originCoord, LookDirection facing, int minDistance, int maxDistance, int width = 1)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        int startStep = Mathf.Max(1, minDistance);
        int endStep = Mathf.Max(startStep, maxDistance);
        List<Vector2Int> stepCoords = new List<Vector2Int>(Mathf.Max(1, width));

        for (int step = startStep; step <= endStep; step++)
        {
            GetDirectionalLineStepCoords(originCoord, facing, step, width, stepCoords);
            for (int i = 0; i < stepCoords.Count; i++)
            {
                Vector2Int coord = stepCoords[i];
                if (IsInsideMap(coord.x, coord.y, mapSize))
                {
                    results.Add(coord);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// East(0°) 기준 Axial 상대 오프셋 패턴을 파싱하고 시선 방향(facing)으로 60° 단위 6방향 회전하여 맵 내 유효 Odd-R 타일 좌표를 반환합니다.
    /// </summary>
    public static List<Vector2Int> GetPatternWithRotation(Vector2Int mapSize, Vector2Int anchorCoord, List<string> pattern, LookDirection facing)
    {
        List<Vector2Int> results = new List<Vector2Int>();
        if (pattern == null || pattern.Count == 0) return results;

        List<Vector2Int> axialOffsets = ParsePatternOffsets(pattern);
        HexMath.OddRToAxial(anchorCoord.x, anchorCoord.y, out int anchorQ, out int anchorR);
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();

        for (int i = 0; i < axialOffsets.Count; i++)
        {
            Vector2Int rotated = RotateAxialOffset(axialOffsets[i].x, axialOffsets[i].y, facing);
            Vector2Int oddRCoord = HexMath.AxialToOddR(anchorQ + rotated.x, anchorR + rotated.y);
            if (IsInsideMap(oddRCoord.x, oddRCoord.y, mapSize))
            {
                if (seen.Add(oddRCoord))
                {
                    results.Add(oddRCoord);
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
                    int.TryParse(parts[0].Trim(), out int dq) &&
                    int.TryParse(parts[1].Trim(), out int dr))
                {
                    offsets.Add(new Vector2Int(dq, dr));
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
                    int dq = c - originCol;
                    int dr = originRow - r;
                    offsets.Add(new Vector2Int(dq, dr));
                }
            }
        }

        return offsets;
    }
}
