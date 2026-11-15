/// <summary>
/// 전투 이벤트 버스(EventBus&lt;ICharacterEvent&gt;) 및 플레이어 이벤트 버스(EventBus&lt;IPlayerEvent&gt;)에
/// 등록된 리스너들에게 무할당(Zero-Alloc) 역순 루프로 이벤트를 디스패치하는 정적 유틸리티 클래스입니다.
/// 실제 Dispatch 메서드들은 EventBinderSourceGenerator(Roslyn Source Generator)에 의해
/// 컴파일 타임에 자동 생성됩니다 (CombatEventDispatcher.g.cs, PlayerEventDispatcher.g.cs).
/// </summary>
public static partial class CombatEventDispatcher
{
}

public static partial class PlayerEventDispatcher
{
}
