using System;
using GameItem.Types;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLua;
using YamlDotNet.Serialization;

public class CardData : IModAssetResolver, IIdentifiable
{
    private static readonly string[] _luaFallbackGlobals = { "card" };

    // cardName이 고유 식별자(ID) 역할을 겸합니다.
    public string cardName { get; set; }

    // 다국어 미사용 시 표시될 선택적 표시 이름 (미지정 시 cardName 사용)
    public string displayName { get; set; }

    public string ID => cardName;

    [YamlIgnore]
    public string LocalizedName => LocalizationManager.GetOrDefault($"{ID}.name", !string.IsNullOrEmpty(displayName) ? displayName : cardName);

    [YamlIgnore]
    public string LocalizedDescription => LocalizationManager.GetOrDefault($"{ID}.desc", description);

    // 카드가 속한 영웅 클래스 (공용 카드일 경우 비어있거나 "All")
    public List<string> classTypes { get; set; } = new List<string>();

    public string cardType { get; set; }
    public string targetType { get; set; }
    public int targetMinDistance { get; set; }
    public int targetMaxDistance { get; set; }
    public int targetCount { get; set; } = 1;
    public int minTargetCount { get; set; } = 1;
    public bool allowDuplicateTarget { get; set; } = true;
    public List<EffectArea> additionalEffectAreaList { get; set; } = new();

    public CardRarity rarity { get; set; }
    public int stamina { get; set; }

    // 마스터리 최대 포인트
    public int maxMasteryPoint { get; set; }

    // 업그레이드 가능한 카드인지 여부
    public bool isUpgradable { get; set; }

    // 업그레이드된 카드인지 여부
    public bool isUpgraded { get; set; }

    // 업그레이드 시 변환될 카드의 이름(ID)
    public string upgradedCardName { get; set; }

    // 카드 효과 템플릿 설명 (동적 토큰 지원 가능)
    public string description { get; set; }

    // 기본 수치 및 마스터리 업그레이드 수치 정의
    // masteryUpgrades: masteryId → { valueKey → 강화량 }
    public Dictionary<string, float> baseValues { get; set; } = new();
    public Dictionary<string, Dictionary<string, float>> masteryUpgrades { get; set; } = new();

    // 각 마스터리 항목의 최대 선택 횟수 (0 또는 키 없음 = 무제한)
    // 예: { "power_up": 3 }  → "power_up" 마스터리는 최대 3번만 선택 가능
    public Dictionary<string, int> masteryMaxUpgrades { get; set; } = new();

    // 마스터리 획득 시 카드에 추가되는 태그 목록
    // 예: { "preserve_mastery": ["Preserve"] } → 해당 마스터리 획득 시 Preserve 태그 부착
    public Dictionary<string, List<string>> masteryTags { get; set; } = new();

    // 마스터리 항목별 표시 이름 (CardDescription SO에서 편입)
    // 예: { "power_up": "파워 업", "cost_reduce": "코스트 감소" }
    public Dictionary<string, string> masteryNames { get; set; } = new();

    // 런타임에 cardName을 통해 자동으로 로드되는 리소스들 (YAML 파싱에서 제외)
    [YamlIgnore]
    public Sprite cardSprite { get; set; }

    [YamlIgnore]
    public LuaTable luaPrototype { get; set; }

    [YamlIgnore]
    private Card _dummyInstance;

    public string GetDescription()
    {
        if (string.IsNullOrEmpty(LocalizedDescription)) return string.Empty;
        if (_dummyInstance == null)
        {
            LuaTable dummyLua = null;
            if (luaPrototype != null && LuaManager.Instance != null)
            {
                dummyLua = LuaManager.Instance.NewInstance(luaPrototype);
            }
            _dummyInstance = new Card(this, null, dummyLua, -1);
        }
        return _dummyInstance.GetDescription();
    }

    public bool ResolveAndValidate(string modDirectory)
    {
        // 1. 카드 일러스트/아이콘 로드 (Default.png 폴백 지원)
        ModAssetLoader.TryLoadIconWithFallback(modDirectory, "Cards", cardName, 256, out Sprite sprite);
        this.cardSprite = sprite;

        // 2. Lua 스크립트 검증 및 사전 로딩 (cardName으로 경로 자동 추론)
        string expectedScriptPath = Path.Combine(modDirectory, "Scripts", "Cards", cardName + ".lua");
        if (ModAssetLoader.TryLoadLuaPrototype(expectedScriptPath, cardName, "CardData", _luaFallbackGlobals, null, out LuaTable proto))
        {
            this.luaPrototype = proto;
            return true;
        }

        return false;
    }
}
