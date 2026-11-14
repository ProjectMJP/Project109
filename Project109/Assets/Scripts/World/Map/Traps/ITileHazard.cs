/// <summary>
/// 타일 위에 배치되어 유닛의 진입/이탈에 반응하는 전술 위험 요소(함정, 장판 등)의 공통 계약입니다.
/// </summary>
public interface ITileHazard
{
    /// <summary>이 위험 요소가 점유/바인딩된 타일</summary>
    Tile OccupiedTile { get; }

    /// <summary>현재 발동 가능한 활성 상태인지 여부</summary>
    bool IsActive { get; }

    /// <summary>특정 타일에 함정을 바인딩하고 초기 상태를 설정합니다.</summary>
    void InitializeOnTile(Tile tile);

    /// <summary>유닛이 해당 타일에 진입했을 때 호출됩니다.</summary>
    void OnUnitEnter(Character unit);

    /// <summary>유닛이 해당 타일에서 이탈했을 때 호출됩니다.</summary>
    void OnUnitExit(Character unit);

    /// <summary>함정을 비활성화(해제)합니다.</summary>
    void Disarm();

    /// <summary>함정을 다시 발동 가능한 상태로 재장전합니다.</summary>
    void Rearm();
}
