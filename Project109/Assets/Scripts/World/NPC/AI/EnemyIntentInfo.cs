/// <summary>
/// 적 유닛의 다음 행동 예고(Intent) 분류 열거형입니다.
/// </summary>
public enum EnemyIntentType
{
    None,
    Attack,
    AttackMove,
    Defend,
    Buff,
    Debuff,
    MoveOnly,
    Stunned,
    Special
}

/// <summary>
/// 적 AI가 예고한 다음 턴 행동의 종류, 대상, 이동 여부 및 예상 수치를 담는 불변 스냅샷 구조체입니다.
/// </summary>
public struct EnemyIntentInfo
{
    public EnemyIntentType intentType;
    public string cardId;
    public Card plannedCard;
    public Character targetCharacter;
    public Tile plannedMoveTile;
    public bool requiresMove;
    public bool canReachTarget;
    public int estimatedValue;
    public bool isValid;
}
