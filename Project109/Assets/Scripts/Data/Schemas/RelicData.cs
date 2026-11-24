using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLua;
using YamlDotNet.Serialization;

public class RelicData : IModAssetResolver, IIdentifiable
{
    private static readonly string[] _luaFallbackGlobals = { "relic" };

    // relicName이 고유 식별자(ID) 역할을 겸합니다.
    public string relicName { get; set; }

    public string ID => relicName;

    public string displayName { get; set; }

    [YamlIgnore]
    public string LocalizedName => LocalizationManager.GetOrDefault($"{ID}.name", !string.IsNullOrEmpty(displayName) ? displayName : relicName);

    [YamlIgnore]
    public string LocalizedDescription => LocalizationManager.GetOrDefault($"{ID}.desc", description);

    [YamlIgnore]
    public string LocalizedFlavorText => LocalizationManager.GetOrDefault($"{ID}.flavor", flavorText);

    // 등장 가능한 캐릭터 클래스 목록.
    // 비어있거나 "All" 또는 "None" 포함 시 공용 유물로 취급됩니다.
    // YAML 예시 (전사/마법사 전용): classTypes:\n  - Warrior\n  - Wizard
    public List<string> classTypes { get; set; } = new List<string>();

    public GameItem.Types.RelicRarity rarity { get; set; }
    public GameItem.Types.ItemObtainSource obtainSource { get; set; } = GameItem.Types.ItemObtainSource.Standard;

    /// <summary>
    /// 유물 등급(Rarity)에 따른 기본 골드 가치입니다.
    /// </summary>
    [YamlIgnore]
    public int BaseGoldValue => GetBaseGoldValue(rarity);

    /// <summary>
    /// 유물 등급별 기준 골드 가치를 반환합니다.
    /// </summary>
    public static int GetBaseGoldValue(GameItem.Types.RelicRarity rarity)
    {
        return rarity switch
        {
            GameItem.Types.RelicRarity.Common => 150,
            GameItem.Types.RelicRarity.Rare => 250,
            GameItem.Types.RelicRarity.Unique => 300,
            GameItem.Types.RelicRarity.Boss => 400,
            _ => 150
        };
    }

    // 업그레이드 시 변환될 유물의 ID (null이거나 비어있으면 업그레이드 불가)
    public string upgradedRelicId { get; set; }

    // 효과에 대한 기계적 설명 (예: "매 턴 시작 시 힘 +1")
    // {placeholder} 문법으로 동적 수치 표현 가능 (예: "공격력 {damage}만큼 피해")
    public string description { get; set; }

    // 분위기/세계관 텍스트. 효과 설명과 분리된 풍미 문구입니다.
    public string flavorText { get; set; }

    /// <summary>
    /// 플레이어의 클래스에 이 유물이 등장할 수 있는지 체크합니다.
    /// </summary>
    public bool CanAppearForClass(string playerClass)
    {
        if (classTypes == null || classTypes.Count == 0 || classTypes.Contains("None") || classTypes.Contains("All"))
        {
            return true;
        }
        return classTypes.Contains(playerClass);
    }

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
        ModAssetLoader.TryLoadIconWithFallback(modDirectory, "Relics", relicName, 128, out Sprite sprite);
        this.iconSprite = sprite;

        // 2. 루아 스크립트 검증 및 사전 로딩 (relicName으로 경로 자동 추론)
        string expectedScriptPath = Path.Combine(modDirectory, "Scripts", "Relics", relicName + ".lua");
        if (ModAssetLoader.TryLoadLuaPrototype(expectedScriptPath, relicName, "RelicData", _luaFallbackGlobals, "OnInit", luaEnv, out LuaTable proto))
        {
            this.luaPrototype = proto;
            return true;
        }

        return false;
    }
}
