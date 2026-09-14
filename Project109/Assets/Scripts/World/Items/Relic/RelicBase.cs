using EventStructs;
using System;
using System.Collections.Generic;
using XLua;

public class Relic : IDescribable
{
    public RelicData Data { get; private set; }
    public int Counter { get; set; }

    private readonly Player owner;
    private readonly LuaTable luaLogic;

    // 이벤트 구독 해제를 위해 만들어진 프록시 객체들을 담아둘 리스트

    private readonly List<object> activeProxies = new();

    public Relic(RelicData data, Player owner, LuaTable luaLogic)
    {
        this.Data = data;
        this.owner = owner;
        this.luaLogic = luaLogic;

        // 1. Lua 측 OnInit 함수 호출 (초기화, 카운터 세팅 등)
        var luaOnInit = luaLogic.Get<Action<LuaTable, Relic, Player>>("OnInit");
        luaOnInit?.Invoke(luaLogic, this, owner);

        // 2. 캐릭터 이벤트 (전투, 체력, 이동 등) 자동 바인딩
        if (owner.character != null)
        {
            LuaEventBinder.BindCharacterEvents(luaLogic, owner.character.eventBus, activeProxies);
        }

        // 3. 플레이어 이벤트 (골드, 카드, 유물 획득 등) 자동 바인딩
        if (owner.eventBus != null)
        {
            LuaEventBinder.BindPlayerEvents(luaLogic, owner.eventBus, activeProxies);
        }
    }

    public void Dispose()
    {
        // 1. Lua 측 OnRemoved 함수 호출 (유물이 파괴되거나 게임오버 시)
        var luaOnRemoved = luaLogic.Get<Action<LuaTable, Relic, Player>>("OnRemoved");
        luaOnRemoved?.Invoke(luaLogic, this, owner);

        // 2. 이벤트 버스 구독 해제
        if (owner.character != null)
        {
            LuaEventBinder.UnbindCharacterEvents(owner.character.eventBus, activeProxies);
        }


        if (owner.eventBus != null)
        {
            LuaEventBinder.UnbindPlayerEvents(owner.eventBus, activeProxies);
        }

        // 3. 리소스 정리
        activeProxies.Clear();
        luaLogic?.Dispose();
    }

    /// <summary>
    /// YAML description 템플릿을 Lua에 넘겨 토큰 치환을 위임합니다.
    /// Lua에 GetDescription이 없으면 템플릿 원문을 반환합니다.
    /// </summary>
    public string GetDescription()
    {
        string template = Data?.description ?? string.Empty;

        var luaFunc = luaLogic?.Get<Func<LuaTable, Relic, string, string>>("GetDescription");
        if (luaFunc != null)
            return luaFunc(luaLogic, this, template);

        return template;
    }

    /// <summary>
    /// 유물의 flavorText(lore/풍미 텍스트)를 반환합니다.
    /// </summary>
    public string GetFlavorText() => Data?.flavorText ?? string.Empty;
}
