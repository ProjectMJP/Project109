using UnityEngine;
using XLua;

/// <summary>
/// ModLoader를 통해 메모리에 캐싱된 Data Class(YAML)를 바탕으로
/// 실제 인게임에서 동작하는 런타임 Object(인스턴스)를 생성하는 팩토리 클래스입니다.
/// </summary>
public static class ModObjectFactory
{
    /// <summary>
    /// 이펙트 ID를 기반으로 Effect 인스턴스를 생성합니다.
    /// </summary>
    public static Effect CreateEffect(string effectId)
    {
        // 1. 원본 데이터 검색
        if (!ModLoader.Instance.EffectDatabase.TryGetValue(effectId, out EffectData data))
        {
            Debug.LogError($"[ModObjectFactory] '{effectId}' 이펙트 데이터를 찾을 수 없습니다.");
            return null;
        }

        // 2. Lua 로직 테이블 인스턴스화 (NewInstance 사용)
        LuaTable luaInstance = null;
        if (data.luaPrototype != null)
        {
            try
            {
                var newInstanceFunc = LuaManager.Instance.luaEnv.Global.Get<System.Func<LuaTable, LuaTable>>("NewInstance");
                if (newInstanceFunc != null)
                {
                    luaInstance = newInstanceFunc(data.luaPrototype);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ModObjectFactory] '{data.effectName}' Lua 인스턴스 생성 실패:\n{e.Message}");
            }
        }

        // 3. 인스턴스 조립 및 반환
        return new Effect(data, luaInstance);
    }

    /// <summary>
    /// 유물 ID를 기반으로 Relic 인스턴스를 생성합니다.
    /// (추후 ModLoader에 RelicDatabase가 추가되었다고 가정)
    /// </summary>
    public static Relic CreateRelic(string relicId, Player owner)
    {
        // 1. 원본 데이터 검색
        if (!ModLoader.Instance.RelicDatabase.TryGetValue(relicId, out RelicData data))
        {
            Debug.LogError($"[ModObjectFactory] '{relicId}' 유물 데이터를 찾을 수 없습니다.");
            return null;
        }

        // 2. Lua 로직 테이블 인스턴스화 (NewInstance 사용)
        LuaTable luaInstance = null;
        if (data.luaPrototype != null)
        {
            try
            {
                var newInstanceFunc = LuaManager.Instance.luaEnv.Global.Get<System.Func<LuaTable, LuaTable>>("NewInstance");
                if (newInstanceFunc != null)
                {
                    luaInstance = newInstanceFunc(data.luaPrototype);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ModObjectFactory] '{data.relicName}' Lua 인스턴스 생성 실패:\n{e.Message}");
            }
        }

        // 3. 인스턴스 조립 및 반환 (Relic는 owner를 받음)
        return new Relic(data, owner, luaInstance);
    }
}
