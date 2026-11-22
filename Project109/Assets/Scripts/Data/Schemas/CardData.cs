using System;
using CardTypes;
using GameItem.Types;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLua;
using YamlDotNet.Serialization;

public class CardData : IModAssetResolver, IIdentifiable
{
    public const int DefaultCommonGoldValue = 50;
    public const int DefaultUncommonGoldValue = 75;
    public const int DefaultRareGoldValue = 120;
    public const int DefaultUniqueGoldValue = 150;
    public const float DefaultShopPriceVarianceRatio = 0.1f;

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

    // 적 AI 행동 예고 아이콘 분류 선택적 오버라이드 (예: Attack, Defend, Buff, Debuff, Special)
    public string intentType { get; set; }

    private string _targetType;
    public string targetType
    {
        get => _targetType;
        set
        {
            _targetType = value;
            parsedTargetType = ParseTargetType(value);
        }
    }

    // YAML 로드 시 파싱되어 캐싱되는 타깃팅 타입 (런타임 문자열 파싱 제거)
    [YamlIgnore]
    public TargetType parsedTargetType { get; set; } = TargetType.SingleEnemy;
    public int targetMinDistance { get; set; }
    public int targetMaxDistance { get; set; }
    public int targetCount { get; set; } = 1;
    public int minTargetCount { get; set; } = 1;
    public bool allowDuplicateTarget { get; set; } = true;
    public bool isPiercing { get; set; } = false;
    public bool ignoreObstacles { get; set; } = false;
    public List<EffectArea> additionalEffectAreaList { get; set; } = new();

    public CardRarity rarity { get; set; }
    public ItemObtainSource obtainSource { get; set; } = ItemObtainSource.Standard;
    public int stamina { get; set; }

    /// <summary>
    /// 카드 등급(Rarity)에 따른 기본 골드 가치입니다.
    /// </summary>
    [YamlIgnore]
    public int BaseGoldValue => GetBaseGoldValue(rarity);

    /// <summary>
    /// 카드 등급별 기준 골드 가치를 반환합니다.
    /// </summary>
    public static int GetBaseGoldValue(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => DefaultCommonGoldValue,
            CardRarity.Uncommon => DefaultUncommonGoldValue,
            CardRarity.Rare => DefaultRareGoldValue,
            CardRarity.Unique => DefaultUniqueGoldValue,
            _ => DefaultCommonGoldValue
        };
    }

    /// <summary>
    /// 카드 등급 기준가와 허용 변동폭 비율을 바탕으로 상점 진열 가격 최소/최대 범위를 계산합니다.
    /// </summary>
    public static void GetGoldPriceRange(CardRarity rarity, float varianceRatio, out int minPrice, out int maxPrice)
    {
        int baseValue = GetBaseGoldValue(rarity);
        float clampedVariance = Mathf.Clamp(varianceRatio, 0f, 0.5f);
        minPrice = Mathf.Max(1, Mathf.RoundToInt(baseValue * (1f - clampedVariance)));
        maxPrice = Mathf.Max(minPrice, Mathf.RoundToInt(baseValue * (1f + clampedVariance)));
    }

    /// <summary>
    /// 카드 등급 기준가와 변동폭 비율에 따라 상점 슬롯의 원가를 산출합니다.
    /// </summary>
    public static int RollShopBasePrice(
        CardRarity rarity,
        float varianceRatio = DefaultShopPriceVarianceRatio,
        Func<int, int, int> inclusiveRangeRoller = null)
    {
        GetGoldPriceRange(rarity, varianceRatio, out int minPrice, out int maxPrice);
        if (minPrice >= maxPrice)
        {
            return minPrice;
        }

        int rolled = inclusiveRangeRoller != null
            ? inclusiveRangeRoller(minPrice, maxPrice)
            : UnityEngine.Random.Range(minPrice, maxPrice + 1);
        return Mathf.Clamp(rolled, minPrice, maxPrice);
    }

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
        if (LuaManager.IsShuttingDown || LuaManager.IsDisposing)
        {
            return LocalizedDescription;
        }
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

    /// <summary>
    /// 캐싱된 설명용 더미 카드 인스턴스와 프로토타입 LuaTable 참조를 안전하게 해제합니다.
    /// </summary>
    public void DisposeLuaResources()
    {
        if (_dummyInstance != null)
        {
            _dummyInstance.Dispose();
            _dummyInstance = null;
        }

        if (luaPrototype != null)
        {
            if (LuaManager.IsLuaEnvAlive)
            {
                luaPrototype.Dispose();
            }
            luaPrototype = null;
        }
    }

    /// <summary>
    /// 카드 데이터의 문자열 형태 targetType을 TargetType 열거형으로 안전하게 변환합니다.
    /// YAML 내 레거시 표기("Enemy", "Single", "Target", "Area" 등)도 표준 열거형으로 정규화합니다.
    /// </summary>
    public static TargetType ParseTargetType(string targetTypeStr)
    {
        if (string.IsNullOrEmpty(targetTypeStr)) return TargetType.SingleEnemy;
        string lower = targetTypeStr.Trim().ToLowerInvariant();
        switch (lower)
        {
            case "self": return TargetType.Self;
            case "areaself": case "area": case "pbaoe": return TargetType.AreaSelf;
            case "areaground": case "ground": case "tile": return TargetType.AreaGround;
            case "randomenemy": case "random": return TargetType.RandomEnemy;
            case "multienemy": case "multi": return TargetType.MultiEnemy;
            case "allenemies": case "all": return TargetType.AllEnemies;
            case "singleally": case "ally": return TargetType.SingleAlly;
            case "allallies": return TargetType.AllAllies;
            case "directional": case "areadirectional": case "direction": case "front": return TargetType.Directional;
            case "singleenemy": case "enemy": case "single": case "target": default: return TargetType.SingleEnemy;
        }
    }

    public bool ResolveAndValidate(string modDirectory)
    {
        // 0. 타깃팅 타입 문자열 사전 파싱 및 캐싱
        this.parsedTargetType = ParseTargetType(this.targetType);

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
