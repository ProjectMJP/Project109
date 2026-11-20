using System;
using System.Collections.Generic;
using XLua;

public class CardTag : ILuaOwnedResource
{
    // 이 태그가 부착된 대상 카드
    public Card targetCard { get; private set; }

    // 카드를 소유한 캐릭터
    public Character owner => targetCard?.owner;

    // 태그 고유 명칭 (예: "Preserve", "DiscardReduce")
    public string tagName { get; private set; }

    private LuaTable luaTable;
    private readonly List<object> activeProxies = new List<object>();
    private bool _isDisposed = false;

    public CardTag(string tagName, LuaTable luaLogic)
    {
        this.tagName = tagName;
        this.luaTable = luaLogic;
        if (this.luaTable != null)
        {
            LuaManager.RegisterResource(this);
        }
    }

    public void ReleaseLuaResources()
    {
        OnDetached();
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
        if (_isDisposed) return;
        _isDisposed = true;

        bool canInvokeLuaCallback = LuaManager.IsLuaEnvValid;
        bool canDisposeLuaNative = LuaManager.IsLuaEnvAlive;

        if (luaTable != null)
        {
            // 1. Lua 측 OnRemoved 실행 (일반 런타임 해제 시에만 호출)
            if (canInvokeLuaCallback)
            {
                var luaOnRemoved = luaTable.Get<Action<LuaTable, CardTag>>("OnRemoved");
                luaOnRemoved?.Invoke(luaTable, this);
            }

            // 2. 캐릭터 이벤트 버스 구독 해제
            if (owner != null && owner.eventBus != null)
            {
                LuaEventBinder.UnbindCharacterEvents(owner.eventBus, activeProxies);
            }
        }

        // 3. 리소스 정리 (InterfaceBridge 프록시 및 LuaTable 네이티브 참조 해제)
        LuaEventBinder.DisposeProxies(activeProxies);
        if (luaTable != null)
        {
            if (canDisposeLuaNative)
            {
                luaTable.Dispose();
            }
            luaTable = null;
        }

        targetCard = null;
    }

    /// <summary>
    /// 카드 태그의 화면 표시용 이름을 반환합니다. Lua에 구현되어 있지 않으면 tagName을 폴백으로 씁니다.
    /// </summary>
    public string GetDisplayName()
    {
        if (luaTable != null && !LuaManager.IsShuttingDown && !LuaManager.IsDisposing)
        {
            var luaFunc = luaTable.Get<System.Func<LuaTable, string>>("GetDisplayName");
            string result = luaFunc?.Invoke(luaTable);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }
        return tagName;
    }

    /// <summary>
    /// 카드 태그의 상세 설명을 반환합니다. Lua에 구현되어 있지 않으면 빈 값을 반환합니다.
    /// </summary>
    public string GetDescription()
    {
        if (luaTable != null && !LuaManager.IsShuttingDown && !LuaManager.IsDisposing)
        {
            var luaFunc = luaTable.Get<System.Func<LuaTable, string>>("GetDescription");
            string result = luaFunc?.Invoke(luaTable);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }
        return string.Empty;
    }
}
