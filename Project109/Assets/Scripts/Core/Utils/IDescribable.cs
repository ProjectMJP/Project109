/// <summary>
/// 인게임에서 설명 텍스트를 제공할 수 있는 객체를 나타내는 인터페이스.
/// Relic, Effect, Card 등이 구현합니다.
/// </summary>
public interface IDescribable
{
    /// <summary>
    /// 현재 상태를 반영한 효과 설명 텍스트를 반환합니다.
    /// Lua에 GetDescription 함수가 정의되어 있으면 Lua 결과를,
    /// 없으면 YAML description 필드(플레이스홀더 치환 포함)를 반환합니다.
    /// </summary>
    string GetDescription();
}
