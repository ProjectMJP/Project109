using System;
using System.Collections.Generic;
using XLua;

public class CardTag
{
    // 이 태그가 부착된 대상 카드
    public Card targetCard { get; private set; }

    // 카드를 소유한 캐릭터
    public Character owner => targetCard?.owner;

    // 태그 고유 명칭 (예: "Preserve", "DiscardReduce")
    public string tagName { get; private set; }

    private readonly LuaTable luaTable;
    private readonly List<object> activeProxies = new List<object>();

    public CardTag(string tagName, LuaTable luaLogic)
    {
        this.tagName = tagName;
        this.luaTable = luaLogic;
    }

    /// <summary>
    /// 카드 인스턴스에 태그가 장착될 때 호출됩니다.
    /// Lua 측의 OnInit을 실행하고 캐릭터 이벤트 버스에 바인딩합니다.
    /// </summary>
    public void OnAttached(Card card)
    {
        this.targetCard = card;

        if (luaTable != null)
        {
            // 1. Lua 측 OnInit 실행 (태그 자체 초기화)
            var luaOnInit = luaTable.Get<Action<LuaTable, CardTag, Card>>("OnInit");
            luaOnInit?.Invoke(luaTable, this, card);

            // 2. 캐릭터 이벤트 버스 자동 바인딩
            if (owner != null && owner.eventBus != null)
            {
                LuaEventBinder.BindCharacterEvents(luaTable, owner.eventBus, activeProxies);
            }
        }
    }

    /// <summary>
    /// 카드 인스턴스에서 태그가 해제되거나 카드가 소멸할 때 호출됩니다.
    /// 캐릭터 이벤트 버스 구독을 완전히 해제하고 리소스를 정리합니다.
    /// </summary>
    public void OnDetached()
    {
        if (luaTable != null)
        {
            // 1. Lua 측 OnRemoved 실행
            var luaOnRemoved = luaTable.Get<Action<LuaTable, CardTag>>("OnRemoved");
            luaOnRemoved?.Invoke(luaTable, this);

            // 2. 캐릭터 이벤트 버스 구독 해제
            if (owner != null && owner.eventBus != null)
            {
                LuaEventBinder.UnbindCharacterEvents(owner.eventBus, activeProxies);
            }
        }

        // 3. 리소스 정리
        activeProxies.Clear();
        luaTable?.Dispose();

        targetCard = null;
    }

    /// <summary>
    /// 카드 태그의 화면 표시용 이름을 반환합니다. Lua에 구현되어 있지 않으면 tagName을 폴백으로 씁니다.
    /// </summary>
    public string GetDisplayName()
    {
        if (luaTable != null)
        {
            var luaFunc = luaTable.Get<LuaFunction>("GetDisplayName");
            if (luaFunc != null)
            {
                object[] results = luaFunc.Call(luaTable);
                if (results != null && results.Length > 0 && results[0] is string str)
                {
                    return str;
                }
            }
        }
        return tagName;
    }

    /// <summary>
    /// 카드 태그의 상세 설명을 반환합니다. Lua에 구현되어 있지 않으면 빈 값을 반환합니다.
    /// </summary>
    public string GetDescription()
    {
        if (luaTable != null)
        {
            var luaFunc = luaTable.Get<LuaFunction>("GetDescription");
            if (luaFunc != null)
            {
                object[] results = luaFunc.Call(luaTable);
                if (results != null && results.Length > 0 && results[0] is string str)
                {
                    return str;
                }
            }
        }
        return string.Empty;
    }
}
