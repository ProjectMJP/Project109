using System;
using System.Collections.Generic;
using System.Linq;
using GameItem.Types;
using XLua;
using EventStructs;

public class Card : IDescribable
{
    // 임시 복제 카드 인스턴스들의 고유 ID 충돌 방지를 위한 전역 시퀀스 카운터 (음수 영역)
    private static int nextTemporaryRuntimeID = -1;

    // 정적 카드 데이터 템플릿
    public CardData cardData { get; private set; }

    // 이 카드를 소유한 캐릭터

    public Character owner { get; private set; }

    #region Runtime State Fields (런타임 상태 데이터)

    // 런타임 고유 식별 ID
    public int runtimeID { get; private set; }

    // 전투 중 실시간 변동이 반영되는 카드 비용 (기본값: Data.stamina)
    private int? _customCost;
    public int currentCost
    {
        get
        {
            if (_customCost.HasValue)
            {
                return _customCost.Value;
            }

            int baseCost = cardData != null ? cardData.stamina : 0;
            float costMod = GetEffectiveValue("cost");
            return UnityEngine.Mathf.Max(0, baseCost + (int)costMod);
        }
        set
        {
            int baseCost = cardData != null ? cardData.stamina : 0;
            float costMod = GetEffectiveValue("cost");
            int expectedNormalCost = UnityEngine.Mathf.Max(0, baseCost + (int)costMod);

            if (value == expectedNormalCost)
            {
                _customCost = null;
            }
            else
            {
                _customCost = value;
            }
        }
    }

    // 하위 호환성 및 Lua 모딩용 프록시 프로퍼티
    public string cardName => cardData != null ? cardData.cardName : string.Empty;
    public string path => cardData != null ? cardData.cardName : string.Empty;

    // 전투 중 임시로 생성된 카드인지 여부
    public bool isTemporary { get; set; }

    // 숙련도 상태를 공유 및 관리하기 위한 참조형 데이터 클래스
    public CardMasteryState masteryState { get; set; }

    public int masteryLevel 
    { 
        get => masteryState != null ? masteryState.masteryLevel : 0; 
        set { if (masteryState != null) masteryState.masteryLevel = value; } 
    }
    public float currentMasteryXP 
    { 
        get => masteryState != null ? masteryState.currentMasteryXP : 0f; 
        set { if (masteryState != null) masteryState.currentMasteryXP = value; } 
    }
    public float maxMasteryXP 
    { 
        get => masteryState != null ? masteryState.maxMasteryXP : 0f; 
        private set { if (masteryState != null) masteryState.maxMasteryXP = value; } 
    }
    public float masteryXPIncreasePerLevel 
    { 
        get => masteryState != null ? masteryState.masteryXPIncreasePerLevel : 0f; 
        private set { if (masteryState != null) masteryState.masteryXPIncreasePerLevel = value; } 
    }

    // 마스터리 시스템이 활성화된 카드인지 여부 (maxMasteryXP > 0)
    public bool hasMastery => maxMasteryXP > 0;

    // 이 카드 인스턴스가 획득한 세부 마스터리 업그레이드 현황 (마스터리ID -> 강화횟수)
    public Dictionary<string, int> masteryUpgrades 
    { 
        get => masteryState?.masteryUpgrades; 
        private set { if (masteryState != null) masteryState.masteryUpgrades = value; } 
    }

    // 이 인스턴스에 붙은 태그 이름 목록 (예: "Preserve", "Vanguard")
    public HashSet<string> tagNames 
    { 
        get => masteryState?.tagNames; 
        private set { if (masteryState != null) masteryState.tagNames = value; } 
    }

    // 이 카드 인스턴스에 장착된 태그(동작 포함) 목록
    public List<CardTag> attachedTags { get; private set; } = new List<CardTag>();


    #endregion

    public readonly LuaTable luaTable;
    private readonly List<object> activeProxies = new List<object>();

    public Card(CardData data, Character owner, LuaTable luaLogic, int runtimeID)
    {
        this.cardData = data;
        this.owner = owner;
        this.luaTable = luaLogic;
        this.runtimeID = runtimeID;

        // 초기 런타임 비용 설정

        this.currentCost = data != null ? data.stamina : 0;

        // 초기 마스터리 스탯 설정 (기본 독립 상태 생성)
        this.masteryState = new CardMasteryState();
        if (data != null && data.maxMasteryPoint > 0)
        {
            this.masteryState.masteryLevel = 0;
            this.masteryState.maxMasteryXP = data.maxMasteryPoint;
            this.masteryState.currentMasteryXP = 0;
            this.masteryState.masteryXPIncreasePerLevel = data.maxMasteryPoint / 2.0f;
        }

        // 1. Lua 측 OnInit 함수 호출 (초기화)
        var luaOnInit = luaTable?.Get<Action<LuaTable, Card, Character>>("OnInit");
        luaOnInit?.Invoke(luaTable, this, owner);

        // 2. 캐릭터 이벤트 자동 바인딩
        if (owner != null && owner.eventBus != null)
        {
            LuaEventBinder.BindCharacterEvents(luaTable, owner.eventBus, activeProxies);
        }
    }


    public void Dispose()
    {
        // 1. 장착된 모든 카드 태그 정리
        if (attachedTags != null)
        {
            foreach (var tag in attachedTags)
            {
                tag.OnDetached();
            }
            attachedTags.Clear();
        }

        // 2. Lua 측 OnRemoved 함수 호출
        var luaOnRemoved = luaTable?.Get<Action<LuaTable, Card, Character>>("OnRemoved");
        luaOnRemoved?.Invoke(luaTable, this, owner);

        // 3. 캐릭터 이벤트 버스 구독 해제
        if (owner != null && owner.eventBus != null)
        {
            LuaEventBinder.UnbindCharacterEvents(owner.eventBus, activeProxies);
        }

        // 4. 리소스 정리
        activeProxies.Clear();
        luaTable?.Dispose();
    }

    /// <summary>
    /// 카드 인스턴스를 마스터리 및 태그 상태를 포함하여 깊은 복사(Deep Copy)합니다.
    /// </summary>
    public Card Clone(Character newOwner = null, int? newRuntimeID = null)
    {
        // 1. 새로운 Lua 인스턴스 생성
        LuaTable luaInstance = null;
        if (cardData != null && cardData.luaPrototype != null)
        {
            try
            {
                var newInstanceFunc = LuaManager.Instance?.luaEnv.Global.Get<Func<LuaTable, LuaTable>>("NewInstance");
                if (newInstanceFunc != null)
                {
                    luaInstance = newInstanceFunc(cardData.luaPrototype);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Card.Clone] '{cardData.cardName}' Lua 인스턴스 생성 실패:\n{e.Message}");
            }
        }

        // 2. Card 인스턴스 생성 (식별자 충돌 방지를 위해 newRuntimeID가 지정되지 않으면 음수 전역 고유 시퀀스 ID 자동 순차 발급)
        int id = newRuntimeID ?? System.Threading.Interlocked.Decrement(ref nextTemporaryRuntimeID);
        Card clonedCard = new Card(cardData, newOwner ?? this.owner, luaInstance, id);

        // 3. 마스터리 공유 상태 참조 복사
        clonedCard.masteryState = this.masteryState;

        // 5. 붙어 있던 태그 복사 (AddTag를 통해 C# 및 Lua 로직 바인딩)
        if (this.tagNames != null)
        {
            foreach (var tagName in this.tagNames)
            {
                clonedCard.AddTag(tagName);
            }
        }

        // 6. 비용(currentCost) 복원
        float costMod = clonedCard.GetEffectiveValue("cost");
        clonedCard.currentCost = UnityEngine.Mathf.Max(0, (cardData != null ? cardData.stamina : 0) + (int)costMod);

        // 7. 기타 플래그 복사
        clonedCard.isTemporary = this.isTemporary;

        return clonedCard;
    }

    /// <summary>
    /// 마스터 덱에 안전하게 안착시킬 때 runtimeID를 고유 번호로 갱신하기 위한 메서드입니다.
    /// </summary>
    public void UpdateRuntimeID(int newID)
    {
        this.runtimeID = newID;
    }

    /// <summary>
    /// 카드의 기본 수치(Base Value)를 반환합니다.
    /// </summary>
    public float GetBaseValue(string valueKey)
    {
        if (cardData != null && cardData.baseValues != null && cardData.baseValues.TryGetValue(valueKey, out float val))
        {
            return val;
        }
        return 0f;
    }

    /// <summary>
    /// 카드의 실시간 최종 수치(Effective Value)를 소수점 이하 불필요한 0을 제거하여 포맷팅된 문자열로 반환합니다.
    /// 예: 2.0 -> "2", 2.5304 -> "2.5304"
    /// </summary>
    public string GetFormattedValue(string valueKey)
    {
        float val = GetEffectiveValue(valueKey);
        double rounded = Math.Round((double)val, 5);
        return rounded.ToString("0.#####");
    }

    /// <summary>
    /// 카드의 기본 수치(Base Value)를 소수점 이하 불필요한 0을 제거하여 포맷팅된 문자열로 반환합니다.
    /// 예: 2.0 -> "2", 2.5304 -> "2.5304"
    /// </summary>
    public string GetBaseFormattedValue(string valueKey)
    {
        float val = GetBaseValue(valueKey);
        double rounded = Math.Round((double)val, 5);
        return rounded.ToString("0.#####");
    }

    /// <summary>
    /// 카드 인스턴스 자체에 기록된 마스터리 업그레이드 횟수를 기반으로 최종 수치를 계산합니다.
    /// </summary>
    public float GetEffectiveValue(string valueKey)
    {
        if (cardData == null) return 0f;

        float val = 0f;
        if (cardData.baseValues != null && cardData.baseValues.TryGetValue(valueKey, out float baseVal))
        {
            val = baseVal;
        }

        if (masteryUpgrades != null)
        {
            foreach (var upgrade in masteryUpgrades)
            {
                string masteryId = upgrade.Key;
                int level = upgrade.Value;

                if (cardData.masteryUpgrades != null && cardData.masteryUpgrades.TryGetValue(masteryId, out var modifierDict))
                {
                    if (modifierDict != null && modifierDict.TryGetValue(valueKey, out float modifier))
                    {
                        val += modifier * level;
                    }
                }
            }
        }
        return val;
    }

    /// <summary>
    /// 이 카드 인스턴스가 특정 마스터리 업그레이드를 몇 번 획득했는지 조회합니다.
    /// </summary>
    public int GetMasteryLevel(string masteryId)
    {
        if (masteryUpgrades != null && masteryUpgrades.TryGetValue(masteryId, out int level))
        {
            return level;
        }
        return 0;
    }

    /// <summary>
    /// YAML description 템플릿을 Lua에 넘겨 토큰 치환을 위임합니다.
    /// </summary>
    public string GetDescription()
    {
        string template = cardData?.description ?? string.Empty;

        var luaFunc = luaTable?.Get<Func<LuaTable, Card, string, string>>("GetDescription");
        if (luaFunc != null)
            return luaFunc(luaTable, this, template);

        return template;
    }

    /// <summary>
    /// 카드의 루아 실행 함수를 호출합니다.
    /// </summary>
    public void Execute(CardInfo info)
    {
        var luaFunc = luaTable?.Get<Action<LuaTable, CardInfo>>("Execute");
        luaFunc?.Invoke(luaTable, info);
    }

    /// <summary>
    /// 이 카드를 현재 시점에서 시전할 수 있는지 실시간으로 검사합니다.
    /// C#의 Stamina 비용 검사와 Lua의 CanPlay 재정의 함수를 평가합니다.
    /// </summary>
    public bool CanPlay(Character caster)
    {
        if (caster == null) return false;

        // 1. 스태미나 기본 비용 유효성 검증
        if (caster.curStamina < currentCost)
        {
            return false;
        }

        // 2. Lua 스크립트에 CanPlay 함수가 정의되어 있다면 호출하여 최종 검증 진행
        if (luaTable != null)
        {
            var luaCanPlay = luaTable.Get<Func<LuaTable, Card, Character, bool>>("CanPlay");
            if (luaCanPlay != null)
            {
                try
                {
                    return luaCanPlay.Invoke(luaTable, this, caster);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[Card.CanPlay] '{cardName}' Lua CanPlay 실행 실패:\n{e.Message}");
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 카드 시전에 소요되는 기본 비용(Stamina) 및 Lua의 커스텀 비용(HP, Gold 등)을 차감합니다.
    /// </summary>
    public void SpendCosts(Character caster)
    {
        if (caster == null) return;

        // 1. 기본 스태미나 소모 차감
        if (currentCost > 0)
        {
            caster.SpendStamina(new StaminaInfo(caster, caster, currentCost, StaminaFlag.Normal));
        }

        // 2. Lua 스크립트에 SpendCosts 함수가 정의되어 있다면 호출하여 커스텀 비용 소모 진행
        if (luaTable != null)
        {
            var luaSpendCosts = luaTable.Get<Action<LuaTable, Card, Character>>("SpendCosts");
            if (luaSpendCosts != null)
            {
                try
                {
                    luaSpendCosts.Invoke(luaTable, this, caster);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[Card.SpendCosts] '{cardName}' Lua SpendCosts 실행 실패:\n{e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 카드 인스턴스에 숙련도 경험치(포인트)를 추가하고 레벨업 여부를 판단합니다.
    /// </summary>
    public void AddMasteryPoint(float amount)
    {
        if (!hasMastery) return;

        currentMasteryXP += amount;
        UnityEngine.Debug.Log($"Added {amount} mastery points to card {cardData?.cardName}. Current: {currentMasteryXP}/{maxMasteryXP}");

        // 테스트를 위해 마스터리 업그레이드 UI 트리거를 일시적으로 비활성화합니다.
        // if (currentMasteryXP >= maxMasteryXP)
        // {
        //     TriggerMasteryUpgradeUI();
        // }
    }

    private void TriggerMasteryUpgradeUI()
    {
        if (CardMasteryManager.instance != null)
        {
            CardMasteryManager.instance.ProcessMasteryUpgrade(this);
        }
    }

    /// <summary>
    /// 특정 마스터리 업그레이드를 인스턴스에 추가하고 스탯을 갱신합니다.
    /// </summary>
    public void AddMastery(string masteryID)
    {
        if (masteryUpgrades == null)
        {
            masteryUpgrades = new Dictionary<string, int>();
        }

        if (!masteryUpgrades.ContainsKey(masteryID))
        {
            masteryUpgrades.Add(masteryID, 1);
        }
        else
        {
            masteryUpgrades[masteryID]++;
        }

        if (hasMastery)
        {
            masteryLevel++;
            currentMasteryXP = UnityEngine.Mathf.Max(0f, currentMasteryXP - maxMasteryXP);
            maxMasteryXP += masteryXPIncreasePerLevel;
        }

        // 1. 코스트 재계산: masteryUpgrades에 "cost" 키가 있으면 currentCost를 갱신
        //    YAML 예시: mastery_cost_reduce: { cost: -1 }  → 선택 시마다 코스트 1 감소
        float costMod = GetEffectiveValue("cost");
        if (costMod != 0 && cardData != null)
        {
            currentCost = UnityEngine.Mathf.Max(0, cardData.stamina + (int)costMod);
        }

        // 2. 태그 적용: masteryTags에 정의된 태그를 기반으로 동적 태그를 부착
        //    YAML 예시: mastery_preserve: tags: ["Preserve"] -> Preserve.lua 태그 부착
        if (cardData?.masteryTags != null &&
            cardData.masteryTags.TryGetValue(masteryID, out var tags))
        {
            foreach (var tag in tags)
            {
                AddTag(tag);
            }
        }
    }

    /// <summary>
    /// 이 카드 인스턴스에 특정 태그가 부착되어 있는지 조회합니다.
    /// Lua에서도 card:HasTag("Preserve") 형태로 호출 가능합니다.
    /// </summary>
    public bool HasTag(string tagName)
    {
        return tagNames != null && tagNames.Contains(tagName);
    }

    /// <summary>
    /// 카드 인스턴스에 새로운 태그를 부착합니다.
    /// </summary>
    public void AddTag(string tagName)
    {
        if (LuaManager.Instance == null) return;

        // LuaManager를 통해 온디맨드로 프로토타입 획득
        var proto = LuaManager.Instance.GetCardTagPrototype(tagName);
        if (proto == null) return;

        // NewInstance 헬퍼를 통해 인스턴스화
        var newInstanceFunc = LuaManager.Instance.luaEnv.Global.Get<Func<LuaTable, LuaTable>>("NewInstance");
        LuaTable luaInstance = newInstanceFunc?.Invoke(proto);

        if (luaInstance != null)
        {
            CardTag cardTag = new CardTag(tagName, luaInstance);
            attachedTags.Add(cardTag);
            cardTag.OnAttached(this);

            // 빠른 조회를 위해 tagNames 해시셋에도 이름 저장
            tagNames.Add(tagName);
        }
    }

    /// <summary>
    /// 카드 인스턴스에서 태그를 제거합니다.
    /// </summary>
    public void RemoveTag(CardTag cardTag)
    {
        if (cardTag == null) return;

        if (attachedTags.Remove(cardTag))
        {
            tagNames.Remove(cardTag.tagName);
            cardTag.OnDetached();
        }
    }

    /// <summary>
    /// 인스턴스의 기존 마스터리 레벨을 고려하여 선택 가능한 무작위 마스터리 ID 목록을 반환합니다.
    /// CardData.masteryUpgrades에 정의된 마스터리 중 최대 횟수에 도달하지 않은 것만 반환합니다.
    /// </summary>
    public List<string> GetRandomMasteryOption(int optionCount)
    {
        List<string> available = new List<string>();

        if (cardData?.masteryUpgrades == null) return available;

        foreach (var masteryId in cardData.masteryUpgrades.Keys)
        {
            int current = GetMasteryLevel(masteryId);

            // masteryMaxUpgrades에 항목이 없거나 0이면 무제한
            bool hasLimit = cardData.masteryMaxUpgrades != null &&
                            cardData.masteryMaxUpgrades.TryGetValue(masteryId, out int maxCount) &&
                            maxCount > 0;

            if (!hasLimit || current < cardData.masteryMaxUpgrades[masteryId])
            {
                available.Add(masteryId);
            }
        }

        return available.OrderBy(x => UnityEngine.Random.value).Take(optionCount).ToList();
    }
}

public class CardMasteryState
{
    public int masteryLevel = 0;
    public float currentMasteryXP = 0f;
    public float maxMasteryXP = 0f;
    public float masteryXPIncreasePerLevel = 0f;
    public Dictionary<string, int> masteryUpgrades = new Dictionary<string, int>();
    public HashSet<string> tagNames = new HashSet<string>();
}
