using XLua;
using System.Collections.Generic;

public static partial class LuaEventBinder
{
    // =========================================================
    // 1. Character Events (전투 및 캐릭터 상태 관련)
    // =========================================================
    public static partial void BindCharacterEvents(LuaTable luaTable, EventBus<ICharacterEvent> eventBus, List<object> activeProxies);
    public static partial void UnbindCharacterEvents(EventBus<ICharacterEvent> eventBus, List<object> activeProxies);

    // =========================================================
    // 2. Player Events (게임 진행, 덱, 재화 관련)
    // =========================================================
    public static partial void BindPlayerEvents(LuaTable luaTable, EventBus<IPlayerEvent> eventBus, List<object> activeProxies);
    public static partial void UnbindPlayerEvents(EventBus<IPlayerEvent> eventBus, List<object> activeProxies);


    // =========================================================
    // 3. Helper Methods (내부 동작용 — 컴파일 타임 상수 문자열 주입으로 GC 무할당)
    // =========================================================

    private static void BindCharacter<T>(LuaTable luaTable, string methodName, EventBus<ICharacterEvent> eventBus, List<object> activeProxies) where T : class, ICharacterEvent
    {
        if (luaTable.ContainsKey(methodName))
        {
            var proxy = luaTable.Cast<T>();
            eventBus.Add<T>(proxy);
            activeProxies.Add(proxy);
        }
    }

    private static void BindCharacter<T>(LuaTable luaTable, EventBus<ICharacterEvent> eventBus, List<object> activeProxies) where T : class, ICharacterEvent
    {
        BindCharacter<T>(luaTable, typeof(T).Name.Substring(1), eventBus, activeProxies);
    }

    private static void BindPlayer<T>(LuaTable luaTable, string methodName, EventBus<IPlayerEvent> eventBus, List<object> activeProxies) where T : class, IPlayerEvent
    {
        if (luaTable.ContainsKey(methodName))
        {
            var proxy = luaTable.Cast<T>();
            eventBus.Add<T>(proxy);
            activeProxies.Add(proxy);
        }
    }

    private static void BindPlayer<T>(LuaTable luaTable, EventBus<IPlayerEvent> eventBus, List<object> activeProxies) where T : class, IPlayerEvent
    {
        BindPlayer<T>(luaTable, typeof(T).Name.Substring(1), eventBus, activeProxies);
    }
}
