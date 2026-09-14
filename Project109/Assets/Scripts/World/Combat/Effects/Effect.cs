using System;
using System.Collections.Generic;
using XLua;

public enum EffectType
{
    Buff,
    Debuff
}

public class Effect : IDescribable
{
    // Lua 스크립트에서 접근할 수 있도록 getter를 public으로 개방
    public Character caster { get; protected set; }
    public Character target { get; protected set; }
    
    public const float tickInterval = 1f;
    public float elapsedSinceLastTick;
    public int currentStack;
    public float duration;
    public float currentDuration;

    public EffectData Data { get; private set; }
    private LuaTable luaTable;
    private List<object> activeProxies = new List<object>();

    // 데이터 기반(모딩) 이펙트용 단일 생성자
    public Effect(EffectData data, LuaTable luaLogic)
    {
        this.Data = data;
        this.luaTable = luaLogic;

        // 추가적인 Lua 자체 초기화 호출
        var luaOnInit = luaTable?.Get<Action<LuaTable, Effect>>("OnInit");
        luaOnInit?.Invoke(luaTable, this);
    }

    public void OnAdded(Character caster, Character target, int stack, float duration)
    {
        this.caster = caster;
        this.target = target;
        this.currentStack = stack;
        this.duration = duration;
        this.currentDuration = duration;

        if (luaTable != null)
        {
            // 1. Lua 측 OnAdded 실행 (Lua에서 초기값 덮어쓰기 가능)
            var luaOnAdded = luaTable.Get<Action<LuaTable, Effect, Character, Character, int, float>>("OnAdded");
            luaOnAdded?.Invoke(luaTable, this, caster, target, stack, duration);

            // 2. 캐릭터 이벤트 버스 자동 바인딩
            if (target != null && target.eventBus != null)
            {
                LuaEventBinder.BindCharacterEvents(luaTable, target.eventBus, activeProxies);
            }
        }
    }

    public void OnStacked(int stack, float duration) 
    {
        var luaOnStacked = luaTable?.Get<Action<LuaTable, Effect, int, float>>("OnStacked");
        if (luaOnStacked != null)
        {
            // Lua에서 스택 및 지속시간 합산 로직을 직접 제어
            luaOnStacked.Invoke(luaTable, this, stack, duration);
        }
        else
        {
            // Lua에 정의되지 않은 경우 기본 동작 (스택 증가, 지속시간 갱신)
            int maxStack = Data != null ? Data.maxStack : int.MaxValue;
            currentStack = System.Math.Min(currentStack + stack, maxStack);
            this.currentDuration = duration;
        }
    }

    public void OnTick() 
    { 
        var luaOnTick = luaTable?.Get<Action<LuaTable, Effect>>("OnTick");
        luaOnTick?.Invoke(luaTable, this);
    }

    public void OnTimeOut()
    {
        var luaOnTimeOut = luaTable?.Get<Action<LuaTable, Effect>>("OnTimeOut");
        if (luaOnTimeOut != null)
        {
            // 타임아웃 처리를 Lua에 완전 위임
            luaOnTimeOut.Invoke(luaTable, this);
        }
        else
        {
            // 기본 동작: 스택 1 감소, 지속시간 초기화
            currentStack--;
            if (currentStack > 0)   
            {
                currentDuration = duration;
            }
        }
    }

    public void OnRemoved()
    {
        if (luaTable != null)
        {
            // 1. Lua 측 OnRemoved 실행
            var luaOnRemoved = luaTable.Get<Action<LuaTable, Effect>>("OnRemoved");
            luaOnRemoved?.Invoke(luaTable, this);

            // 2. 이벤트 버스 구독 해제
            if (target != null && target.eventBus != null)
            {
                LuaEventBinder.UnbindCharacterEvents(target.eventBus, activeProxies);
            }
        }

        // 3. 리소스 정리
        activeProxies.Clear();
        luaTable?.Dispose();

        caster = null;
        target = null;
    }

    /// <summary>
    /// YAML description 템플릿을 Lua에 넘겨 토큰 치환을 위임합니다.
    /// Lua에 GetDescription이 없으면 템플릿 원문을 반환합니다.
    /// </summary>
    public string GetDescription()
    {
        string template = Data?.description ?? string.Empty;

        var luaFunc = luaTable?.Get<Func<LuaTable, Effect, string, string>>("GetDescription");
        if (luaFunc != null)
            return luaFunc(luaTable, this, template);

        // Lua GetDescription이 없으면 템플릿 그대로 반환
        // (동적 값이 없는 단순 효과는 YAML에 토큰 없이 작성하면 됨)
        return template;
    }
}
