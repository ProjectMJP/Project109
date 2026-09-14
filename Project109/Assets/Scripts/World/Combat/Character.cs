using EventStructs;
using System;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterState
{
    Idle,
    Move,
    Attack,
    Skill,
    Hit,
    Die
}


public enum CharacterFaction
{
    Player,
    Ally,
    Enemy,
    Neutral
}

public class Character : MonoBehaviour, IDamageable
{
    // 캐릭터 진영 소속 정보
    public CharacterFaction faction = CharacterFaction.Enemy;
    CharacterFaction IDamageable.faction => faction;

    /// <summary>
    /// 상대 캐릭터가 나와 적대적인 관계인지 판별합니다.
    /// </summary>
    public bool IsHostileTo(Character other)
    {
        if (other == null || other.isDead) return false;

        // 중립(Neutral) 진영은 누구와도 서로 적대하지 않음
        if (this.faction == CharacterFaction.Neutral || other.faction == CharacterFaction.Neutral)
            return false;

        // 플레이어(Player)와 아군 소환수(Ally)는 아군 진영이므로 서로 적대하지 않음
        if ((this.faction == CharacterFaction.Player || this.faction == CharacterFaction.Ally) &&
            (other.faction == CharacterFaction.Player || other.faction == CharacterFaction.Ally))
        {
            return false;
        }

        // 그 외 진영이 다를 경우 적대 관계로 판별
        return this.faction != other.faction;
    }

    /// <summary>
    /// 상대 캐릭터가 나와 동맹 관계인지 판별합니다.
    /// </summary>
    public bool IsFriendlyTo(Character other)
    {
        if (other == null) return false;
        return !IsHostileTo(other) && (this.faction != CharacterFaction.Neutral && other.faction != CharacterFaction.Neutral);
    }

    #region CharacterStat

    [SerializeField]
    private CharacterStat _characterStat;

    // 외부에 노출되는 런타임 현재 스탯
    public CharacterStat curCharacterStat => _characterStat;

    public EffectManager effectManager;
    public CharacterMove characterMove { get; private set; }

    private void Awake()
    {
        effectManager = new EffectManager(this);
        characterMove = new CharacterMove(this);
    }

    public void InitializeStat(CharacterStat characterStat)
    {
        if (characterStat != null)
        {
            _characterStat = characterStat;

            curHealth = characterStat.maxHealth;
            curStamina = 0f;

            curMoveCount = characterStat.maxMoveCount;
            curTilesPerMove = characterStat.maxTilesPerMove;
        }
    }

    #endregion

    #region CharacterEvents

    public EventBus<ICharacterEvent> eventBus = new EventBus<ICharacterEvent>();

    public event Action<Character> OnCharacterHealthChanged;
    public event Action<Character> OnCharacterStaminaChanged;
    public event Action<Character> OnCharacterShieldChanged;
    public event Action<Character> OnCharacterDied;

    public event Action<Character, CharacterState> OnCharacterStateChanged;

    #endregion

    #region CurrentStatValues

    [SerializeField]
    private int _curMoveCount;
    public int curMoveCount { get { return _curMoveCount; } set { _curMoveCount = value; } }

    [SerializeField]
    private int _curTilesPerMove;
    public int curTilesPerMove { get { return _curTilesPerMove; } set { _curTilesPerMove = value; } }

    [SerializeField]
    private CharacterState _currentState = CharacterState.Idle;
    public CharacterState currentState
    {
        get => _currentState;
        set
        {
            if (_currentState != value)
            {
                _currentState = value;
                OnCharacterStateChanged?.Invoke(this, _currentState);
            }
        }
    }


    public bool isDead = false;
    bool IDamageable.isDead => isDead;

    public float maxHealth => curCharacterStat != null ? curCharacterStat.maxHealth : 0f;

    [SerializeField]
    private float _curHealth;
    public float curHealth { get { return _curHealth; } set { _curHealth = value; OnCharacterHealthChanged?.Invoke(this); } }
    public float curHealthRate { get { return (_curHealth == 0) ? 0 : _curHealth / curCharacterStat.maxHealth; } }

    [SerializeField]
    private float _curStamina;
    public float curStamina { get { return _curStamina; } set { _curStamina = value; OnCharacterStaminaChanged?.Invoke(this); } }
    public float curStaminaRate { get { return (_curStamina == 0) ? 0 : _curStamina / curCharacterStat.maxStamina; } }

    [SerializeField]
    private float _shield = 0f;
    public float shield
    {
        get => _shield;
        set
        {
            _shield = value;
            OnCharacterShieldChanged?.Invoke(this);
        }
    }

    [SerializeField]
    public int shieldDurationTurns = 1;

    [SerializeField]
    public int currentTurn;

    #endregion

    #region BattleTick

    /// <summary>
    /// BattleManager에서 매 프레임 호출. 배속을 적용해 dt를 받는다.
    /// </summary>
    public void BattleTick(float dt)
    {
        // 스태미나 회복
        curStamina += curCharacterStat.staminaRegenPerSecond * dt;

        // 이펙트 틱
        effectManager.Tick(dt);

        if (curStamina >= curCharacterStat.maxStamina)
        {
            RunManager.instance.battleManager.RequestTurnStart(this);
        }
    }

    /// <summary>
    /// 턴 종료 시 호출하여 스태미나 이월(절반 감축) 처리
    /// </summary>
    public void ResetStamina()
    {
        curStamina = curStamina * 0.5f;
    }

    /// <summary>
    /// 전투 개시 시 호출하여 스태미나를 0(또는 지정값)으로 명시적 초기화합니다.
    /// </summary>
    public void ResetStaminaForBattle(float initialStamina = 0f)
    {
        curStamina = initialStamina;
    }

    /// <summary>
    /// 전투 개시 또는 종료 시 캐릭터의 방어막, 스태미나, 버프/디버프 등을 완전히 초기화합니다.
    /// </summary>
    public void ResetCharacterForBattle(bool clearPermanentEffects = true)
    {
        curStamina = 0f;
        shield = 0f;
        shieldDurationTurns = 0;
        currentTurn = 0;

        if (effectManager != null)
        {
            effectManager.ClearAllEffects(clearPermanentEffects);
        }
    }

    /// <summary>
    /// 턴 시작 시 호출하여 이동 관련 스탯 초기화
    /// </summary>
    public void ResetMoveStat()
    {
        if (curCharacterStat != null)
        {
            curMoveCount = curCharacterStat.maxMoveCount;
        }
    }

    #endregion

    #region Damage and Heal Pipeline

    public void TakeDamage(DamageInfo info)
    {
        if (isDead) return;

        // 1. 공격자의 "공격 직전" 발동 (ex. 힘(Strength) 버프를 통해 baseDamageAmount 증가)
        if (!info.damageFlags.HasFlag(DamageFlag.NoCasterEvents) && info.caster != null)
            DispatchBeforeDealDamage(info.caster.eventBus, ref info);

        // 2. 피격자의 "방어 직전" 효과 발동 (ex. 데미지 경감, 회피 처리 등)
        if (!info.damageFlags.HasFlag(DamageFlag.NoTargetEvents))
            DispatchBeforeTakeDamage(this.eventBus, ref info);

        // 회피되었는지 체크 (도입 미정으로 주석 처리)
        // if (info.isDodged) return;

        float finalDamage = info.baseDamageAmount * info.damageMultiplier;
        info.finalDamageAmount = finalDamage;

        info.shieldDamageAmount = 0f;
        info.hpDamageAmount = 0f;

        // 3. 실제 데미지 적용
        if (!info.damageFlags.HasFlag(DamageFlag.IgnoreShield))
        {
            if (shield > 0f)
            {
                float damageAfterShield = finalDamage - shield;
                info.shieldDamageAmount = Mathf.Min(finalDamage, shield);
                shield -= finalDamage;
                if (shield <= 0f)
                {
                    shield = 0f;
                    shieldDurationTurns = 0; // 실드가 파괴되었으므로 지속 턴 수 리셋
                    info.isShieldBroken = true;
                    OnCharacterShieldChanged?.Invoke(this);

                    // 실드 파괴 즉시 이벤트 발생
                    if (!info.damageFlags.HasFlag(DamageFlag.NoCasterEvents) && info.caster != null)
                        DispatchBreakShield(info.caster.eventBus, info);

                    if (!info.damageFlags.HasFlag(DamageFlag.NoTargetEvents))
                        DispatchShieldBroken(this.eventBus, info);
                }
                finalDamage = Mathf.Max(0f, damageAfterShield);
            }
        }

        info.hpDamageAmount = finalDamage;
        info.isBlocked = (info.shieldDamageAmount > 0f && info.hpDamageAmount <= 0f);

        // 남은 데미지를 체력에 적용
        if (info.hpDamageAmount > 0f)
        {
            curHealth -= info.hpDamageAmount;
        }

        // 데미지 표시기 트리거
        if (DamageIndicatorManager.Instance != null)
        {
            DamageIndicatorManager.Instance.ShowIndicator(this.transform.position, info.hpDamageAmount, info.damageFlags);
        }

        // 4. 결과 기록 (사망 여부)
        if (curHealth <= 0f)
        {
            curHealth = 0f;
            info.isFatal = true;
        }

        // 5. 공격자의 "공격 직후" 발동 (ex. 흡혈, 대상 처치 시 추가 효과 등)
        if (!info.damageFlags.HasFlag(DamageFlag.NoCasterEvents) && info.caster != null)
        {
            DispatchAfterDealDamage(info.caster.eventBus, info);
            if (info.isFatal)
                DispatchKill(info.caster.eventBus, info);
        }

        // 6. 피격자의 "방어 직후" 발동 (ex. 가시 데미지 반사 등)
        if (!info.damageFlags.HasFlag(DamageFlag.NoTargetEvents))
        {
            DispatchAfterTakeDamage(this.eventBus, info);
        }

        // 7. 게임 내 사망 확정
        if (info.isFatal)
        {
            isDead = true;
            currentState = CharacterState.Die;
            OnCharacterDied?.Invoke(this);
        }
    }

    public void TakeHeal(HealInfo info)
    {
        if (isDead) return;

        // 힐량 증가/감소 등의 처리
        if (info.caster != null)
            DispatchBeforeGiveHeal(info.caster.eventBus, ref info);
        DispatchBeforeTakeHeal(this.eventBus, ref info);

        float healAmount = info.baseHealAmount; // 향후 healMultiplier 등 추가 가능

        if (info.healFlags.HasFlag(HealFlag.OverHeal))
        {
            curHealth += healAmount;
        }
        else
        {
            curHealth = Mathf.Min(curCharacterStat.maxHealth, curHealth + healAmount);
        }

        // 힐 표시기 트리거
        if (DamageIndicatorManager.Instance != null)
        {
            DamageIndicatorManager.Instance.ShowHealIndicator(this.transform.position, healAmount);
        }

        // 회복 직후 처리
        if (info.caster != null)
            DispatchAfterGiveHeal(info.caster.eventBus, info);
        DispatchAfterTakeHeal(this.eventBus, info);
    }

    public void TakeStamina(StaminaInfo info)
    {
        if (isDead) return;

        if (info.caster != null)
            DispatchBeforeGiveStamina(info.caster.eventBus, ref info);
        DispatchBeforeTakeStamina(this.eventBus, ref info);

        float gainAmount = info.baseStaminaAmount * info.staminaMultiplier;

        if (info.staminaFlags.HasFlag(StaminaFlag.OverStamina))
        {
            curStamina += gainAmount;
        }
        else
        {
            curStamina = Mathf.Min(curCharacterStat.maxStamina, curStamina + gainAmount);
        }

        if (info.caster != null)
            DispatchAfterGiveStamina(info.caster.eventBus, info);
        DispatchAfterTakeStamina(this.eventBus, info);
    }

    public void SpendStamina(StaminaInfo info)
    {
        if (isDead) return;

        DispatchBeforeSpendStamina(this.eventBus, ref info);

        float spendAmount = info.baseStaminaAmount * info.staminaMultiplier;
        curStamina = Mathf.Max(0f, curStamina - spendAmount);

        DispatchAfterSpendStamina(this.eventBus, info);
    }

    public void TakeShield(ShieldInfo info)
    {
        if (isDead) return;

        if (info.caster != null)
            DispatchBeforeGiveShield(info.caster.eventBus, ref info);
        DispatchBeforeTakeShield(this.eventBus, ref info);

        float shieldAmount = info.baseShieldAmount * info.shieldMultiplier;
        shield += shieldAmount;

        if (info.durationTurns > shieldDurationTurns)
        {
            shieldDurationTurns = info.durationTurns;
        }

        OnCharacterShieldChanged?.Invoke(this);

        if (info.caster != null)
            DispatchAfterGiveShield(info.caster.eventBus, info);
        DispatchAfterTakeShield(this.eventBus, info);
    }

    #region Event Dispatch Helpers (Zero-Alloc)

    private static void DispatchBeforeDealDamage(EventBus<ICharacterEvent> bus, ref DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeDealDamage>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeDealDamage(ref info);
        }
    }

    private static void DispatchBeforeTakeDamage(EventBus<ICharacterEvent> bus, ref DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeTakeDamage>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeTakeDamage(ref info);
        }
    }

    private static void DispatchAfterDealDamage(EventBus<ICharacterEvent> bus, DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterDealDamage>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterDealDamage(info);
        }
    }

    private static void DispatchAfterTakeDamage(EventBus<ICharacterEvent> bus, DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterTakeDamage>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterTakeDamage(info);
        }
    }

    private static void DispatchKill(EventBus<ICharacterEvent> bus, DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnKill>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnKill(info);
        }
    }

    private static void DispatchBreakShield(EventBus<ICharacterEvent> bus, DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBreakShield>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBreakShield(info);
        }
    }

    private static void DispatchShieldBroken(EventBus<ICharacterEvent> bus, DamageInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnShieldBroken>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnShieldBroken(info);
        }
    }

    private static void DispatchBeforeGiveHeal(EventBus<ICharacterEvent> bus, ref HealInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeGiveHeal>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeGiveHeal(ref info);
        }
    }

    private static void DispatchBeforeTakeHeal(EventBus<ICharacterEvent> bus, ref HealInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeTakeHeal>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeTakeHeal(ref info);
        }
    }

    private static void DispatchAfterGiveHeal(EventBus<ICharacterEvent> bus, HealInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterGiveHeal>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterGiveHeal(info);
        }
    }

    private static void DispatchAfterTakeHeal(EventBus<ICharacterEvent> bus, HealInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterTakeHeal>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterTakeHeal(info);
        }
    }

    private static void DispatchBeforeGiveStamina(EventBus<ICharacterEvent> bus, ref StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeGiveStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeGiveStamina(ref info);
        }
    }

    private static void DispatchBeforeTakeStamina(EventBus<ICharacterEvent> bus, ref StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeTakeStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeTakeStamina(ref info);
        }
    }

    private static void DispatchAfterGiveStamina(EventBus<ICharacterEvent> bus, StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterGiveStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterGiveStamina(info);
        }
    }

    private static void DispatchAfterTakeStamina(EventBus<ICharacterEvent> bus, StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterTakeStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterTakeStamina(info);
        }
    }

    private static void DispatchBeforeSpendStamina(EventBus<ICharacterEvent> bus, ref StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeSpendStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeSpendStamina(ref info);
        }
    }

    private static void DispatchAfterSpendStamina(EventBus<ICharacterEvent> bus, StaminaInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterSpendStamina>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterSpendStamina(info);
        }
    }

    private static void DispatchBeforeGiveShield(EventBus<ICharacterEvent> bus, ref ShieldInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeGiveShield>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeGiveShield(ref info);
        }
    }

    private static void DispatchBeforeTakeShield(EventBus<ICharacterEvent> bus, ref ShieldInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnBeforeTakeShield>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnBeforeTakeShield(ref info);
        }
    }

    private static void DispatchAfterGiveShield(EventBus<ICharacterEvent> bus, ShieldInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterGiveShield>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterGiveShield(info);
        }
    }

    private static void DispatchAfterTakeShield(EventBus<ICharacterEvent> bus, ShieldInfo info)
    {
        if (bus != null && bus.TryGetListeners<IOnAfterTakeShield>(out var listeners))
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i].OnAfterTakeShield(info);
        }
    }

    #endregion

    /// <summary>
    /// 하위 호환성을 위해 남겨둔 TakeShield 오버로드 데코레이터입니다.
    /// </summary>
    public void TakeShield(ShieldInfo info, int durationTurns)
    {
        info.durationTurns = durationTurns;
        TakeShield(info);
    }

    /// <summary>
    /// 대상에게 간단하게 데미지를 적용합니다. (편의성 오버로드)
    /// </summary>
    public void TakeDamage(Character caster, float baseDamageAmount, DamageFlag flags = DamageFlag.Normal)
    {
        TakeDamage(new DamageInfo(caster, this, baseDamageAmount, flags));
    }

    /// <summary>
    /// 대상에게 간단하게 회복을 적용합니다. (편의성 오버로드)
    /// </summary>
    public void TakeHeal(Character caster, float baseHealAmount, HealFlag flags = HealFlag.Normal)
    {
        TakeHeal(new HealInfo(caster, this, baseHealAmount, flags));
    }

    /// <summary>
    /// 대상에게 간단하게 보호막을 적용합니다. (편의성 오버로드)
    /// </summary>
    public void TakeShield(Character caster, float baseShieldAmount, int durationTurns = 1, ShieldFlag flags = ShieldFlag.Normal)
    {
        TakeShield(new ShieldInfo(caster, this, baseShieldAmount, durationTurns, flags));
    }

    /// <summary>
    /// 대상에게 간단하게 스태미나를 부여합니다. (편의성 오버로드)
    /// </summary>
    public void TakeStamina(Character caster, float baseStaminaAmount, StaminaFlag flags = StaminaFlag.Normal)
    {
        TakeStamina(new StaminaInfo(caster, this, baseStaminaAmount, flags));
    }

    /// <summary>
    /// 본인의 스태미나를 스스로 소모하거나 적에 의해 차감됩니다. (편의성 오버로드)
    /// </summary>
    public void SpendStamina(float baseStaminaAmount, StaminaFlag flags = StaminaFlag.Normal)
    {
        SpendStamina(new StaminaInfo(null, this, baseStaminaAmount, flags));
    }

    /// <summary>
    /// 타겟에 의해 스태미나가 소모되거나 삭감됩니다. (편의성 오버로드)
    /// </summary>
    public void SpendStamina(Character caster, float baseStaminaAmount, StaminaFlag flags = StaminaFlag.Normal)
    {
        SpendStamina(new StaminaInfo(caster, this, baseStaminaAmount, flags));
    }

    /// <summary>
    /// 턴 종료 시 호출하여 실드 지속 시간을 1 감소시킵니다.
    /// 0 이하가 되면 실드 값을 0으로 만들고 지속 시간을 0으로 리셋합니다.
    /// </summary>
    public void UpdateShieldDuration()
    {
        if (shield > 0f)
        {
            shieldDurationTurns--;
            if (shieldDurationTurns <= 0)
            {
                shield = 0f;
                shieldDurationTurns = 0; // 실드가 만료되었으므로 지속 턴 수 리셋
                OnCharacterShieldChanged?.Invoke(this);
            }
        }
    }

    public void TakeEffect(EffectInfo info)
    {
        if (isDead) return;

        // 1. 시전자(Caster)의 "내가 부여하기 직전" 유물/버프 발동 (ex. 독 부여 시 +1스택)
        if (info.caster != null)
            info.caster.eventBus?.Invoke<IOnBeforeGiveEffect>(l => l.OnBeforeGiveEffect(ref info));

        // 2. 피격자(Target)의 "내가 받기 직전" 유물/버프 발동 (ex. 인공물: 디버프 무효화)
        this.eventBus?.Invoke<IOnBeforeTakeEffect>(l => l.OnBeforeTakeEffect(ref info));

        // 3. 무효화(Cancel) 판정 검사
        if (info.effectFlags.HasFlag(EffectFlag.Cancel))
            return; // 인공물 등에 의해 막혔으므로 종료

        // 4. 무사히 통과했으므로 진짜로 버프 추가 (이때 Added, Stacked 등의 UI 이벤트가 터짐)
        this.effectManager.AddEffect(info.caster, info.effect, info.FinalStack, info.FinalDuration);
        // 5. 부여 직후 파이프라인 (ex. 취약 부여 성공 시 약화도 부여)
        if (info.caster != null)
            info.caster.eventBus?.Invoke<IOnAfterGiveEffect>(l => l.OnAfterGiveEffect(info));
        this.eventBus?.Invoke<IOnAfterTakeEffect>(l => l.OnAfterTakeEffect(info));
    }

    /// <summary>
    /// 이펙트 ID와 스택, 지속 시간을 기반으로 이펙트를 대상에게 부여합니다.
    /// </summary>
    public void TakeEffect(Character caster, string effectId, int stack, float duration)
    {
        Effect effect = ModObjectFactory.CreateEffect(effectId);
        if (effect != null)
        {
            TakeEffect(new EffectInfo(caster, this, effect, stack, duration));
        }
        else
        {
            Debug.LogError($"[Character] Failed to take effect: Effect ID '{effectId}' not found in ModObjectFactory. Target: {gameObject.name}");
        }
    }

    #endregion
}
