/// <summary>
/// 전술 타일 함정의 발동 규칙 유형을 정의합니다.
/// </summary>
public enum TrapTriggerType
{
    /// <summary>유닛이 함정 타일에 진입하는 즉시 발동</summary>
    ImmediateOnEnter = 0,

    /// <summary>유닛 진입 횟수(StepCount)가 임계치에 도달할 때 발동</summary>
    StepCountThreshold = 1,

    /// <summary>진입 시 지정된 격자 방향으로 직선 투사체/선형 판정을 발사</summary>
    DirectionalShot = 2
}
