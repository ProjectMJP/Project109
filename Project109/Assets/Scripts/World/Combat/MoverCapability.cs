using System;

/// <summary>
/// 캐릭터의 이동 및 지형 관통 능력을 나타내는 비트 플래그 Enum입니다.
/// </summary>
[Flags]
public enum MoverCapability
{
    None = 0,
    PassObstacles = 1 << 0, // 장애물(Obstacle) 통과 및 위에 올라서기 가능
    PassWalls = 1 << 1,     // 벽(Full) 통과 가능
    IgnoreTraps = 1 << 2    // 함정(Trap) 무시 가능 (비행 등)
}
