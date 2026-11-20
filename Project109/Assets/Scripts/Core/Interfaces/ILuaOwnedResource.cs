/// <summary>
/// XLua LuaTable·DelegateBridge 등 네이티브 Lua 수명에 종속된 리소스를 소유하는 도메인 객체의 해제 계약입니다.
/// LuaEnv.Dispose() 직전 중앙 종료 코디네이터가 잔존 참조를 결정론적으로 회수할 때 호출됩니다.
/// </summary>
public interface ILuaOwnedResource
{
    void ReleaseLuaResources();
}
