using System.IO;
using UnityEngine;
using XLua;
using YamlDotNet.Serialization;

/// <summary>
/// 상태이상이 만료·차감되는 시간/진행 축을 정의합니다.
/// </summary>
public enum EffectClockType
{
    Time,   // 월드 시간(초) 기반 틱 및 감쇠
    Turn,   // 전투 턴 경과 기반 차감/만료
    Event   // 특정 이벤트 조건 전까지 영구 유지
}

public class EffectData : IModAssetResolver, IIdentifiable
{
    private static readonly string[] _luaFallbackGlobals = { "effect" };

    // effectName이 고유 식별자(ID) 역할을 겸합니다.
    public string effectName { get; set; }

    public string ID => effectName;

    [YamlIgnore]
    public string LocalizedName => LocalizationManager.GetOrDefault($"{ID}.name", !string.IsNullOrEmpty(displayName) ? displayName : effectName);

    [YamlIgnore]
    public string LocalizedDescription => LocalizationManager.GetOrDefault($"{ID}.desc", description);

    public string displayName { get; set; }
    // 효과에 대한 기계적 설명 (예: "적에게 {stacks}의 피해를 입힙니다.")
    // {stacks} 플레이스홀더로 현재 스택 수치를 동적으로 표현할 수 있습니다.
    public string description { get; set; }
    public EffectType effectType { get; set; }
    public EffectClockType clockType { get; set; } = EffectClockType.Time;
    public bool isPermanent { get; set; }
    public bool isIndependent { get; set; } // true일 경우, 중첩(Stack)되지 않고 새로운 인스턴스로 각각 존재합니다.
    public int maxStack { get; set; } = int.MaxValue;

    // 런타임에 imagePath를 통해 디스크에서 직접 읽어온 스프라이트 
    // (YAML 파싱 대상에서 제외하기 위해 [YamlIgnore] 사용)
    [YamlIgnore]
    public Sprite iconSprite { get; set; }

    [YamlIgnore]
    public LuaTable luaPrototype { get; set; }

    /// <summary>
    /// 프로토타입 LuaTable 네이티브 참조를 안전하게 해제합니다.
    /// </summary>
    public void DisposeLuaResources(bool canDisposeLuaNative = true)
    {
        if (luaPrototype != null)
        {
            if (canDisposeLuaNative)
            {
                luaPrototype.Dispose();
            }
            luaPrototype = null;
        }
    }

    public bool ResolveAndValidate(string modDirectory, LuaEnv luaEnv = null)
    {
        // 1. 아이콘 스프라이트 링크 (Default.png 폴백 지원)
        ModAssetLoader.TryLoadIconWithFallback(modDirectory, "Effects", effectName, 128, out Sprite sprite);
        this.iconSprite = sprite;

        // 2. 루아 스크립트 검증 및 사전 로딩 (effectName으로 경로 자동 추론)
        string expectedScriptPath = Path.Combine(modDirectory, "Scripts", "Effects", effectName + ".lua");
        if (ModAssetLoader.TryLoadLuaPrototype(expectedScriptPath, effectName, "EffectData", _luaFallbackGlobals, "OnInit", luaEnv, out LuaTable proto))
        {
            this.luaPrototype = proto;
            return true;
        }

        return false;
    }
}
