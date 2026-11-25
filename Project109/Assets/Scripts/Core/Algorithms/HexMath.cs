using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pointy-Top 육각 격자(Odd-R Offset 저장 ↔ Axial/Cube 연산)의 수학 유틸리티입니다.
/// 모든 공간 전술 계층(L0~L4, Editor)이 이 단일 규격(SSOT)을 공유합니다.
/// </summary>
public static class HexMath
{
    public const float Sqrt3 = 1.7320508075688772f;
    public const float HalfSqrt3 = 0.8660254037844386f;
    public const float InvSqrt3 = 0.5773502691896258f;
    public const int DirectionCount = 6;

    // LookDirection(0..5: East, NorthEast, NorthWest, West, SouthWest, SouthEast)에 대응하는 Axial 방향 벡터 (dq, dr)
    private static readonly int[] _axialDirQ = { 1, 0, -1, -1, 0, 1 };
    private static readonly int[] _axialDirR = { 0, 1, 1, 0, -1, -1 };

    // 6방향 단위 월드 벡터 (X-Z 평면, Y=0)
    private static readonly Vector3[] _worldDirVectors =
    {
        new Vector3(1f, 0f, 0f),                // East (0°)
        new Vector3(0.5f, 0f, HalfSqrt3),       // NorthEast (60°)
        new Vector3(-0.5f, 0f, HalfSqrt3),      // NorthWest (120°)
        new Vector3(-1f, 0f, 0f),               // West (180°)
        new Vector3(-0.5f, 0f, -HalfSqrt3),     // SouthWest (240°)
        new Vector3(0.5f, 0f, -HalfSqrt3),      // SouthEast (300°)
    };

    /// <summary>Pointy-Top 육각형의 인접 행(Z축) 간 중심 거리 (cellSize * √3 / 2).</summary>
    public static float RowSpacing(float cellSize) => cellSize * HalfSqrt3;

    /// <summary>Pointy-Top 육각형의 중심에서 꼭짓점까지의 외접원 반지름 (cellSize / √3).</summary>
    public static float OuterRadius(float cellSize) => cellSize * InvSqrt3;

    /// <summary>Odd-R Offset 좌표 (col, row)를 Axial 좌표 (q, r)로 변환합니다 (음수 행 포함 안전).</summary>
    public static void OddRToAxial(int col, int row, out int q, out int r)
    {
        q = col - ((row - (row & 1)) >> 1);
        r = row;
    }

    /// <summary>Odd-R Offset 좌표 (col, row)를 Axial 좌표 Vector2Int(q, r)로 변환합니다.</summary>
    public static Vector2Int OddRToAxial(Vector2Int coord)
    {
        OddRToAxial(coord.x, coord.y, out int q, out int r);
        return new Vector2Int(q, r);
    }

    /// <summary>Axial 좌표 (q, r)를 Odd-R Offset 좌표 (col, row)로 변환합니다.</summary>
    public static void AxialToOddR(int q, int r, out int col, out int row)
    {
        col = q + ((r - (r & 1)) >> 1);
        row = r;
    }

    /// <summary>Axial 좌표 (q, r)를 Odd-R Offset 좌표 Vector2Int(col, row)로 변환합니다.</summary>
    public static Vector2Int AxialToOddR(int q, int r)
    {
        AxialToOddR(q, r, out int col, out int row);
        return new Vector2Int(col, row);
    }

    /// <summary>방향 인덱스(0~5)에 대응하는 Axial 단위 방향 벡터를 반환합니다.</summary>
    public static void AxialDirection(int dirIndex, out int dq, out int dr)
    {
        int idx = ((dirIndex % DirectionCount) + DirectionCount) % DirectionCount;
        dq = _axialDirQ[idx];
        dr = _axialDirR[idx];
    }

    /// <summary>LookDirection에 대응하는 Axial 단위 방향 벡터를 반환합니다.</summary>
    public static void AxialDirection(LookDirection dir, out int dq, out int dr)
    {
        AxialDirection((int)dir, out dq, out dr);
    }

    /// <summary>두 Odd-R Offset 좌표 간의 육각 격자 거리(Cube 거리)를 무할당으로 계산합니다.</summary>
    public static int Distance(int colA, int rowA, int colB, int rowB)
    {
        OddRToAxial(colA, rowA, out int q1, out int r1);
        OddRToAxial(colB, rowB, out int q2, out int r2);
        int dq = q1 - q2;
        int dr = r1 - r2;
        int ds = -dq - dr;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) >> 1;
    }

    /// <summary>두 Odd-R Offset 좌표 간의 육각 격자 거리를 계산합니다.</summary>
    public static int Distance(Vector2Int a, Vector2Int b)
    {
        return Distance(a.x, a.y, b.x, b.y);
    }

    /// <summary>Odd-R Offset 좌표에서 지정된 방향(0~5)의 인접 이웃 좌표를 무할당으로 반환합니다.</summary>
    public static void GetOddRNeighbor(int col, int row, int dirIndex, out int nCol, out int nRow)
    {
        OddRToAxial(col, row, out int q, out int r);
        AxialDirection(dirIndex, out int dq, out int dr);
        AxialToOddR(q + dq, r + dr, out nCol, out nRow);
    }

    /// <summary>Odd-R Offset 좌표에서 지정된 LookDirection 방향의 인접 이웃 좌표를 반환합니다.</summary>
    public static Vector2Int GetOddRNeighbor(Vector2Int coord, LookDirection dir)
    {
        GetOddRNeighbor(coord.x, coord.y, (int)dir, out int nCol, out int nRow);
        return new Vector2Int(nCol, nRow);
    }

    /// <summary>Odd-R Offset 좌표에서 지정된 방향(dir)으로 steps칸 직진한 좌표를 반환합니다.</summary>
    public static Vector2Int Step(Vector2Int origin, LookDirection dir, int steps)
    {
        if (steps == 0) return origin;
        OddRToAxial(origin.x, origin.y, out int q, out int r);
        AxialDirection(dir, out int dq, out int dr);
        return AxialToOddR(q + dq * steps, r + dr * steps);
    }

    /// <summary>
    /// Axial 상대 오프셋 (q, r)을 반시계 방향(CCW)으로 60° * steps60CCW 만큼 회전합니다.
    /// (East=0° 기준 오프셋을 목표 LookDirection 방향으로 회전할 때 사용)
    /// </summary>
    public static void RotateAxial(int q, int r, int steps60CCW, out int outQ, out int outR)
    {
        int steps = ((steps60CCW % DirectionCount) + DirectionCount) % DirectionCount;
        int cq = q;
        int cr = r;
        int cs = -q - r;

        for (int i = 0; i < steps; i++)
        {
            int nextQ = -cr;
            int nextR = -cs;
            int nextS = -cq;
            cq = nextQ;
            cr = nextR;
            cs = nextS;
        }

        outQ = cq;
        outR = cr;
    }

    /// <summary>East(0°) 기준 Axial 상대 오프셋을 지정된 시선 방향(facing)으로 회전합니다.</summary>
    public static Vector2Int RotateAxial(Vector2Int axialOffset, LookDirection facing)
    {
        RotateAxial(axialOffset.x, axialOffset.y, (int)facing, out int outQ, out int outR);
        return new Vector2Int(outQ, outR);
    }

    /// <summary>LookDirection을 반시계 방향으로 60° * steps60CCW 만큼 회전한 방향을 반환합니다.</summary>
    public static LookDirection RotateDirection(LookDirection dir, int steps60CCW)
    {
        int idx = (((int)dir + steps60CCW) % DirectionCount + DirectionCount) % DirectionCount;
        return (LookDirection)idx;
    }

    /// <summary>지정된 방향의 정반대(180°) 방향을 반환합니다.</summary>
    public static LookDirection GetOpposite(LookDirection dir)
    {
        return RotateDirection(dir, 3);
    }

    /// <summary>시선(facing) 기준으로 targetDir이 반시계 몇 스텝(0~5) 떨어져 있는지 반환합니다.</summary>
    public static int GetRelativeDirectionStep(LookDirection facing, LookDirection targetDir)
    {
        return (((int)targetDir - (int)facing) % DirectionCount + DirectionCount) % DirectionCount;
    }

    /// <summary>LookDirection에 대응하는 3D X-Z 평면 단위 방향 벡터를 반환합니다.</summary>
    public static Vector3 DirectionToWorldVector(LookDirection dir)
    {
        int idx = (((int)dir % DirectionCount) + DirectionCount) % DirectionCount;
        return _worldDirVectors[idx];
    }

    /// <summary>X-Z 평면 방향 벡터를 가장 가까운 6방향 LookDirection으로 스냅합니다.</summary>
    public static LookDirection WorldVectorToDirection(float x, float z, LookDirection fallback = LookDirection.East)
    {
        if (x * x + z * z <= 1e-8f)
        {
            return fallback;
        }

        float angleDeg = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
        if (angleDeg < 0f)
        {
            angleDeg += 360f;
        }

        int sector = Mathf.FloorToInt((angleDeg + 30f) / 60f) % DirectionCount;
        return (LookDirection)sector;
    }

    /// <summary>3D 벡터(X-Z 평면)를 가장 가까운 6방향 LookDirection으로 스냅합니다.</summary>
    public static LookDirection WorldVectorToDirection(Vector3 direction, LookDirection fallback = LookDirection.East)
    {
        return WorldVectorToDirection(direction.x, direction.z, fallback);
    }

    /// <summary>Odd-R 시작 좌표에서 목표 좌표를 향하는 가장 가까운 6방향 LookDirection을 계산합니다.</summary>
    public static LookDirection DirectionFromCoords(Vector2Int origin, Vector2Int target, LookDirection fallback = LookDirection.East)
    {
        if (origin == target) return fallback;

        OddRToAxial(origin.x, origin.y, out int q1, out int r1);
        OddRToAxial(target.x, target.y, out int q2, out int r2);
        int dq = q2 - q1;
        int dr = r2 - r1;

        float wx = dq + dr * 0.5f;
        float wz = dr * HalfSqrt3;
        return WorldVectorToDirection(wx, wz, fallback);
    }

    /// <summary>target이 origin의 시선 정면 1방향(0°)에 위치하는지 판정합니다.</summary>
    public static bool IsInDirectFront(Vector2Int origin, LookDirection facing, Vector2Int target, bool requireAdjacent = false)
    {
        if (origin == target) return false;
        if (requireAdjacent && Distance(origin, target) != 1) return false;

        LookDirection targetDir = DirectionFromCoords(origin, target, facing);
        return GetRelativeDirectionStep(facing, targetDir) == 0;
    }

    /// <summary>target이 origin의 전방 3방향(0°, ±60°) 부채꼴에 위치하는지 판정합니다.</summary>
    public static bool IsInFrontArc(Vector2Int origin, LookDirection facing, Vector2Int target, bool requireAdjacent = false)
    {
        if (origin == target) return false;
        if (requireAdjacent && Distance(origin, target) != 1) return false;

        LookDirection targetDir = DirectionFromCoords(origin, target, facing);
        int diff = GetRelativeDirectionStep(facing, targetDir);
        return diff == 0 || diff == 1 || diff == 5;
    }

    /// <summary>target이 origin의 정후면 1방향(180°)에 위치하는지 판정합니다.</summary>
    public static bool IsInDirectBack(Vector2Int origin, LookDirection facing, Vector2Int target, bool requireAdjacent = false)
    {
        if (origin == target) return false;
        if (requireAdjacent && Distance(origin, target) != 1) return false;

        LookDirection targetDir = DirectionFromCoords(origin, target, facing);
        return GetRelativeDirectionStep(facing, targetDir) == 3;
    }

    /// <summary>target이 origin의 후방 3방향(±120°, 180°) 부채꼴에 위치하는지 판정합니다.</summary>
    public static bool IsInBackArc(Vector2Int origin, LookDirection facing, Vector2Int target, bool requireAdjacent = false)
    {
        if (origin == target) return false;
        if (requireAdjacent && Distance(origin, target) != 1) return false;

        LookDirection targetDir = DirectionFromCoords(origin, target, facing);
        int diff = GetRelativeDirectionStep(facing, targetDir);
        return diff == 2 || diff == 3 || diff == 4;
    }

    /// <summary>투사체 등의 진행 방향(travelDirection)으로부터 피격자 입장에서의 공격 원점 방향(정반대)을 계산합니다.</summary>
    public static LookDirection GetAttackSourceDirectionFromTravel(LookDirection travelDirection)
    {
        return GetOpposite(travelDirection);
    }

    /// <summary>피격자 시선(targetFacing)과 피격자→공격 원점 방향(sourceDirection) 간의 방향 관계를 판정합니다.</summary>
    public static FacingHitRelation EvaluateFacingRelation(LookDirection targetFacing, LookDirection sourceDirection)
    {
        int diff = GetRelativeDirectionStep(targetFacing, sourceDirection);
        switch (diff)
        {
            case 0:
                return FacingHitRelation.DirectFront;
            case 1:
            case 5:
                return FacingHitRelation.FrontSide;
            case 2:
            case 4:
                return FacingHitRelation.BackSide;
            case 3:
                return FacingHitRelation.DirectBack;
            default:
                return FacingHitRelation.None;
        }
    }

    /// <summary>피격자 좌표/시선과 공격 원점 좌표(공격자 위치 또는 AoE 중심점) 간의 방향 관계를 판정합니다.</summary>
    public static FacingHitRelation EvaluateFacingRelationFromCoords(Vector2Int targetCoord, LookDirection targetFacing, Vector2Int originCoord)
    {
        if (targetCoord == originCoord)
        {
            return FacingHitRelation.None;
        }

        LookDirection sourceDir = DirectionFromCoords(targetCoord, originCoord, targetFacing);
        return EvaluateFacingRelation(targetFacing, sourceDir);
    }

    /// <summary>피격자 시선과 투사체 진행(입사) 방향 간의 방향 관계를 판정합니다.</summary>
    public static FacingHitRelation EvaluateFacingRelationFromTravel(LookDirection targetFacing, LookDirection travelDirection)
    {
        return EvaluateFacingRelation(targetFacing, GetAttackSourceDirectionFromTravel(travelDirection));
    }

    /// <summary>방향 관계가 지정된 4종 판정 구역(전면1/후면1/전방3/후방3)에 포함되는지 반환합니다.</summary>
    public static bool IsFacingZoneMatch(FacingHitRelation relation, FacingZone zone)
    {
        switch (zone)
        {
            case FacingZone.DirectFront:
                return relation == FacingHitRelation.DirectFront;
            case FacingZone.DirectBack:
                return relation == FacingHitRelation.DirectBack;
            case FacingZone.FrontArc:
                return relation == FacingHitRelation.DirectFront || relation == FacingHitRelation.FrontSide;
            case FacingZone.BackArc:
                return relation == FacingHitRelation.DirectBack || relation == FacingHitRelation.BackSide;
            default:
                return false;
        }
    }

    /// <summary>시선(facing) 기준 공격 원점 방향(sourceDirection)이 정전면 1방향(0°)인지 판정합니다.</summary>
    public static bool IsInDirectFront(LookDirection facing, LookDirection sourceDirection)
    {
        return EvaluateFacingRelation(facing, sourceDirection) == FacingHitRelation.DirectFront;
    }

    /// <summary>시선(facing) 기준 공격 원점 방향(sourceDirection)이 전방 3방향(0°, ±60°)인지 판정합니다.</summary>
    public static bool IsInFrontArc(LookDirection facing, LookDirection sourceDirection)
    {
        return IsFacingZoneMatch(EvaluateFacingRelation(facing, sourceDirection), FacingZone.FrontArc);
    }

    /// <summary>시선(facing) 기준 공격 원점 방향(sourceDirection)이 정후면 1방향(180°)인지 판정합니다.</summary>
    public static bool IsInDirectBack(LookDirection facing, LookDirection sourceDirection)
    {
        return EvaluateFacingRelation(facing, sourceDirection) == FacingHitRelation.DirectBack;
    }

    /// <summary>시선(facing) 기준 공격 원점 방향(sourceDirection)이 후방 3방향(±120°, 180°)인지 판정합니다.</summary>
    public static bool IsInBackArc(LookDirection facing, LookDirection sourceDirection)
    {
        return IsFacingZoneMatch(EvaluateFacingRelation(facing, sourceDirection), FacingZone.BackArc);
    }

    /// <summary>투사체 진행 방향(travelDirection) 기준으로 피격자가 정전면(1방향) 피격인지 판정합니다.</summary>
    public static bool IsTravelDirectFront(LookDirection targetFacing, LookDirection travelDirection)
    {
        return IsFacingZoneMatch(EvaluateFacingRelationFromTravel(targetFacing, travelDirection), FacingZone.DirectFront);
    }

    /// <summary>투사체 진행 방향(travelDirection) 기준으로 피격자가 전방(3방향) 피격인지 판정합니다.</summary>
    public static bool IsTravelFrontArc(LookDirection targetFacing, LookDirection travelDirection)
    {
        return IsFacingZoneMatch(EvaluateFacingRelationFromTravel(targetFacing, travelDirection), FacingZone.FrontArc);
    }

    /// <summary>투사체 진행 방향(travelDirection) 기준으로 피격자가 정후면(1방향) 피격인지 판정합니다.</summary>
    public static bool IsTravelDirectBack(LookDirection targetFacing, LookDirection travelDirection)
    {
        return IsFacingZoneMatch(EvaluateFacingRelationFromTravel(targetFacing, travelDirection), FacingZone.DirectBack);
    }

    /// <summary>투사체 진행 방향(travelDirection) 기준으로 피격자가 후방(3방향) 피격인지 판정합니다.</summary>
    public static bool IsTravelBackArc(LookDirection targetFacing, LookDirection travelDirection)
    {
        return IsFacingZoneMatch(EvaluateFacingRelationFromTravel(targetFacing, travelDirection), FacingZone.BackArc);
    }

    /// <summary>Pointy-Top Odd-R 셀 좌표를 3D 월드(또는 로컬) 좌표로 변환합니다.</summary>
    public static Vector3 CellToWorld(int col, int row, Vector3 gridOffset, float cellSize)
    {
        float oddShift = (row & 1) != 0 ? 0.5f : 0f;
        return new Vector3(
            gridOffset.x + (col + oddShift) * cellSize,
            gridOffset.y,
            gridOffset.z + row * (cellSize * HalfSqrt3)
        );
    }

    /// <summary>3D 월드(또는 로컬) 좌표를 가장 가까운 Pointy-Top Odd-R 셀 좌표로 변환합니다.</summary>
    public static Vector2Int WorldToCell(Vector3 worldPos, Vector3 gridOffset, float cellSize)
    {
        if (cellSize <= 0f) return Vector2Int.zero;

        float dx = worldPos.x - gridOffset.x;
        float dz = worldPos.z - gridOffset.z;

        float rf = dz / (cellSize * HalfSqrt3);
        float qf = dx / cellSize - rf * 0.5f;
        float sf = -qf - rf;

        CubeRound(qf, rf, sf, out int q, out int r);
        return AxialToOddR(q, r);
    }

    /// <summary>부동소수점 Cube 좌표를 가장 가까운 정수 Axial 좌표로 반올림합니다.</summary>
    public static void CubeRound(float qf, float rf, float sf, out int q, out int r)
    {
        int rq = Mathf.RoundToInt(qf);
        int rr = Mathf.RoundToInt(rf);
        int rs = Mathf.RoundToInt(sf);

        float qDiff = Mathf.Abs(rq - qf);
        float rDiff = Mathf.Abs(rr - rf);
        float sDiff = Mathf.Abs(rs - sf);

        if (qDiff > rDiff && qDiff > sDiff)
        {
            rq = -rr - rs;
        }
        else if (rDiff > sDiff)
        {
            rr = -rq - rs;
        }

        q = rq;
        r = rr;
    }

    /// <summary>Cube 선형 보간(Lerp + ε nudge)으로 origin에서 target까지의 육각 직선 좌표 경로를 반환합니다.</summary>
    public static List<Vector2Int> GetLine(Vector2Int origin, Vector2Int target, bool includeOrigin = false)
    {
        List<Vector2Int> coords = new List<Vector2Int>();
        if (origin == target)
        {
            if (includeOrigin)
            {
                coords.Add(origin);
            }
            return coords;
        }

        OddRToAxial(origin.x, origin.y, out int q0, out int r0);
        OddRToAxial(target.x, target.y, out int q1, out int r1);
        int s0 = -q0 - r0;
        int s1 = -q1 - r1;

        int dist = Distance(origin, target);
        if (dist <= 0)
        {
            if (includeOrigin) coords.Add(origin);
            return coords;
        }

        // 경계선 정중앙 통과 시 일관된 타이브레이킹을 위한 미세 편향
        float aq = q0 + 1e-6f;
        float ar = r0 + 2e-6f;
        float a_s = s0 - 3e-6f;

        float bq = q1 + 1e-6f;
        float br = r1 + 2e-6f;
        float bs = s1 - 3e-6f;

        int startStep = includeOrigin ? 0 : 1;
        Vector2Int prev = new Vector2Int(int.MinValue, int.MinValue);

        for (int i = startStep; i <= dist; i++)
        {
            float t = (float)i / dist;
            float qf = aq + (bq - aq) * t;
            float rf = ar + (br - ar) * t;
            float sf = a_s + (bs - a_s) * t;

            CubeRound(qf, rf, sf, out int q, out int r);
            Vector2Int cell = AxialToOddR(q, r);
            if (cell != prev)
            {
                coords.Add(cell);
                prev = cell;
            }
        }

        return coords;
    }

    /// <summary>Pointy-Top 육각형 중심으로부터 cornerIndex(0~5) 꼭짓점까지의 로컬 오프셋을 반환합니다.</summary>
    public static Vector3 GetHexCornerOffset(int cornerIndex, float cellSize)
    {
        float radius = OuterRadius(cellSize);
        float angleRad = (30f + 60f * cornerIndex) * Mathf.Deg2Rad;
        return new Vector3(radius * Mathf.Cos(angleRad), 0f, radius * Mathf.Sin(angleRad));
    }
}
