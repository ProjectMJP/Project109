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

    private void OnDestroy()
    {
        effectManager?.Dispose();
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

    // UI 계층과의 결합도를 낮추기 위한 정적 플로팅 인디케이터 이벤트
    public static event Action<Character, float, DamageFlag> OnCharacterDamagedIndicator;
    public static event Action<Character, float> OnCharacterHealedIndicator;

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

    /// <summary>
    /// 턴 도달 시 maxStamina를 초과했던 잉여 스태미나 (프레임 틱 오버플로우 보존용).
    /// 턴 중에는 maxStamina로 플레이하고, 턴 종료 시 다음 턴 충전으로 환원됩니다.
    /// </summary>
    public float overflowStamina { get; private set; } = 0f;

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
    public bool BattleTick(float dt)
    {
        float regen = curCharacterStat.staminaRegenPerSecond * dt;

        // 이펙트 틱
        effectManager.Tick(dt);

        if (curStamina >= curCharacterStat.maxStamina)
        {
            // 이미 완충되어 턴 대기 중인 상태: 잉여 스태미나 누적
            overflowStamina += regen;
            return true;
        }

        curStamina += regen;
        if (curStamina >= curCharacterStat.maxStamina)
        {
            // 이번 프레임에 maxStamina에 도달하여 초과한 잉여분을 overflowStamina에 누적하고 curStamina는 max로 고정
            overflowStamina += (curStamina - curCharacterStat.maxStamina);
            curStamina = curCharacterStat.maxStamina;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 턴 종료 시 호출하여 스태미나 이월(절반 감축) 처리 및 보존된 오버플로우 스태미나 환원
    /// </summary>
    public void ResetStamina()
    {
        curStamina = (curStamina * 0.5f) + overflowStamina;
        overflowStamina = 0f;
    }

    /// <summary>
    /// 전투 개시 시 호출하여 스태미나를 0(또는 지정값)으로 명시적 초기화합니다.
    /// </summary>
    public void ResetStaminaForBattle(float initialStamina = 0f)
    {
        curStamina = initialStamina;
        overflowStamina = 0f;
    }

    /// <summary>
    /// 전투 개시 또는 종료 시 캐릭터의 방어막, 스태미나, 버프/디버프 등을 완전히 초기화합니다.
    /// </summary>
    public void ResetCharacterForBattle(bool clearPermanentEffects = true)
    {
        curStamina = 0f;
        overflowStamina = 0f;
        shield = 0f;
        shieldDurationTurns = 0;
        currentTurn = 0;

        if (effectManager != null)
        {
            effectManager.ClearAllEffects(clearPermanentEffects);
        }

        characterMove?.StopMoving();

        currentState = CharacterState.Idle;
        isDead = false;
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

    private static int _damageRecursionDepth = 0;
    private const int MAX_DAMAGE_RECURSION_DEPTH = 10;

    public void TakeDamage(DamageInfo info)
    {
        if (isDead) return;

        if (_damageRecursionDepth >= MAX_DAMAGE_RECURSION_DEPTH)
        {
            Debug.LogError($"[Character] TakeDamage 연쇄/반사 데미지 최대 재귀 깊이({MAX_DAMAGE_RECURSION_DEPTH})를 초과하여 연쇄를 강제 차단합니다! (Target: {this.name})");
            return;
        }

        _damageRecursionDepth++;
        try
        {
            // 1. 공격자의 "공격 직전" 발동 (ex. 힘(Strength) 버프를 통해 baseDamageAmount 증가)
            if (!info.damageFlags.HasFlag(DamageFlag.NoCasterEvents) && info.caster != null)
                CombatEventDispatcher.DispatchBeforeDealDamage(info.caster.eventBus, ref info);

            // 2. 피격자의 "방어 직전" 효과 발동 (ex. 데미지 경감, 회피 처리 등)
            if (!info.damageFlags.HasFlag(DamageFlag.NoTargetEvents))
                CombatEventDispatcher.DispatchBeforeTakeDamage(this.eventBus, ref info);

            // 회피되었는지 체크 (도입 미정으로 주석 처리)
            // if (info.isDodged) return;

            float initialShield = shield;
            float initialHealth = curHealth;

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
                            CombatEventDispatcher.DispatchBreakShield(info.caster.eventBus, info);

                        if (!info.damageFlags.HasFlag(DamageFlag.NoTargetEvents))
                            CombatEventDispatcher.DispatchShieldBroken(this.eventBus, info);
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

            // 데미지 표시기 이벤트 트리거 (UI 계층 직접 의존 탈피)
            OnCharacterDamagedIndicator?.Invoke(this, info.hpDamageAmount, info.damageFlags);

            // 4. 결과 기록 및 즉시 사망 플래그 선반영 (후속 이벤트에서 죽은 유닛 재피격 방어)
            if (curHealth <= 0f)
            {
                curHealth = 0f;
                info.isFatal = true;
                isDead = true;
                currentState = CharacterState.Die;
                if (characterMove != null)
                {
                    Tile tile = characterMove.GetCurrentTile();
                    if (tile != null && tile.occupyingCharacter == this)
                    {
                        tile.occupyingCharacter = null;
                    }
                }
            }

            // 전투 파이프라인 디버그 감사 로그 (에디터 및 Development Build 전용)
            LogCombatDamageAudit(info, initialShield, initialHealth, shield, curHealth);

            // 5. 공격자의 "공격 직후" 발동 (ex. 흡혈, 대상 처치 시 추가 효과 등)
            if (!info.damageFlags.HasFlag(DamageFlag.NoCasterEvents) && info.caster != null)
            {
                CombatEventDispatcher.DispatchAfterDealDamage(info.caster.eventBus, info);
                if (info.isFatal)
                    CombatEventDispatcher.DispatchKill(info.caster.eventBus, info);
            }

            // 6. 피격자의 "방어 직후" 발동 (ex. 가시 데미지 반사 등)
            if (!info.damageFlags.HasFlag(DamageFlag.NoTargetEvents))
            {
                CombatEventDispatcher.DispatchAfterTakeDamage(this.eventBus, info);
            }

            // 7. 게임 내 사망 이벤트 발송 및 후처리
            if (info.isFatal)
            {
                CombatEventDispatcher.DispatchDeath(this.eventBus, info);
                OnCharacterDied?.Invoke(this);
            }
        }
        finally
        {
            _damageRecursionDepth--;
        }
    }

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void LogCombatDamageAudit(DamageInfo info, float initShield, float initHp, float finalShield, float finalHp)
    {
        string casterName = info.caster != null ? info.caster.name : "Environment/System";
        Debug.Log($"[CombatAudit] {casterName} -> {name} | Base: {info.baseDamageAmount} x {info.damageMultiplier} = {info.finalDamageAmount:F1} | Shield: {initShield:F1}->{finalShield:F1} (-{info.shieldDamageAmount:F1}) | HP: {initHp:F1}->{finalHp:F1} (-{info.hpDamageAmount:F1}) | Blocked: {info.isBlocked} | Fatal: {info.isFatal}");
    }

    public void TakeHeal(HealInfo info)
    {
        if (isDead) return;

        // 힐량 증가/감소 등의 처리
        if (info.caster != null)
            CombatEventDispatcher.DispatchBeforeGiveHeal(info.caster.eventBus, ref info);
        CombatEventDispatcher.DispatchBeforeTakeHeal(this.eventBus, ref info);

        float healAmount = info.baseHealAmount; // 향후 healMultiplier 등 추가 가능

        if (info.healFlags.HasFlag(HealFlag.OverHeal))
        {
            curHealth += healAmount;
        }
        else
        {
            curHealth = Mathf.Min(curCharacterStat.maxHealth, curHealth + healAmount);
        }

        // 힐 표시기 이벤트 트리거 (UI 계층 직접 의존 탈피)
        OnCharacterHealedIndicator?.Invoke(this, healAmount);

        // 회복 직후 처리
        if (info.caster != null)
            CombatEventDispatcher.DispatchAfterGiveHeal(info.caster.eventBus, info);
        CombatEventDispatcher.DispatchAfterTakeHeal(this.eventBus, info);
    }

    public void TakeStamina(StaminaInfo info)
    {
        if (isDead) return;

        if (info.caster != null)
            CombatEventDispatcher.DispatchBeforeGiveStamina(info.caster.eventBus, ref info);
        CombatEventDispatcher.DispatchBeforeTakeStamina(this.eventBus, ref info);

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
            CombatEventDispatcher.DispatchAfterGiveStamina(info.caster.eventBus, info);
        CombatEventDispatcher.DispatchAfterTakeStamina(this.eventBus, info);
    }

    public void SpendStamina(StaminaInfo info)
    {
        if (isDead) return;

        CombatEventDispatcher.DispatchBeforeSpendStamina(this.eventBus, ref info);

        float spendAmount = info.baseStaminaAmount * info.staminaMultiplier;
        curStamina = Mathf.Max(0f, curStamina - spendAmount);

        CombatEventDispatcher.DispatchAfterSpendStamina(this.eventBus, info);
    }

    public void TakeShield(ShieldInfo info)
    {
        if (isDead) return;

        if (info.caster != null)
            CombatEventDispatcher.DispatchBeforeGiveShield(info.caster.eventBus, ref info);
        CombatEventDispatcher.DispatchBeforeTakeShield(this.eventBus, ref info);

        float shieldAmount = info.baseShieldAmount * info.shieldMultiplier;
        shield += shieldAmount;

        if (info.durationTurns > shieldDurationTurns)
        {
            shieldDurationTurns = info.durationTurns;
        }

        OnCharacterShieldChanged?.Invoke(this);

        if (info.caster != null)
            CombatEventDispatcher.DispatchAfterGiveShield(info.caster.eventBus, info);
        CombatEventDispatcher.DispatchAfterTakeShield(this.eventBus, info);
    }

    #region Combat Pipeline Convenience Overloads

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
    /// 하위 호환성을 위해 남겨둔 TakeShield 오버로드 데코레이터입니다.
    /// </summary>
    public void TakeShield(ShieldInfo info, int durationTurns)
    {
        info.durationTurns = durationTurns;
        TakeShield(info);
    }

    /// <summary>
    /// 대상에게 간단하게 스태미나를 부여합니다. (편의성 오버로드)
    /// </summary>
    public void TakeStamina(Character caster, float baseStaminaAmount, StaminaFlag flags = StaminaFlag.Normal)
    {
        TakeStamina(new StaminaInfo(caster, this, baseStaminaAmount, flags));
    }

    /// <summary>
    /// 타겟에 의해 스태미나가 소모되거나 삭감됩니다. (편의성 오버로드)
    /// </summary>
    public void SpendStamina(Character caster, float baseStaminaAmount, StaminaFlag flags = StaminaFlag.Normal)
    {
        SpendStamina(new StaminaInfo(caster, this, baseStaminaAmount, flags));
    }

    #endregion

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

    #region Spatial & Forced Movement (Knockback, Dash)

    /// <summary>
    /// 현재 캐릭터가 서 있는 타일의 격자 좌표를 반환합니다.
    /// </summary>
    public Vector2Int GetGridPosition()
    {
        if (characterMove != null)
        {
            Tile tile = characterMove.GetCurrentTile();
            if (tile != null) return tile.GetCoord();
        }
        return Vector2Int.zero;
    }

    /// <summary>
    /// 대상을 시전자 반대 방향(또는 지정 방향)으로 distance칸만큼 강제로 밀쳐냅니다(넉백).
    /// 벽이나 다른 유닛에 가로막혀 충돌했는지 여부(bool)를 반환합니다.
    /// </summary>
    public bool KnockbackFrom(Character caster, int distance, float moveSpeedMultiplier = 2.5f)
    {
        if (isDead || characterMove == null || GameMap.current == null) return false;

        Vector2Int myCoord = GetGridPosition();
        Vector2Int casterCoord = caster != null ? caster.GetGridPosition() : myCoord;
        Vector2Int dir = myCoord - casterCoord;

        // 시전자와 동일 위치이거나 방향이 0인 경우 기본 상향 벡터 사용
        if (dir == Vector2Int.zero)
        {
            dir = Vector2Int.up;
        }

        return ForcedMoveInDirection(dir, distance, moveSpeedMultiplier);
    }

    /// <summary>
    /// 지정된 방향 벡터로 distance칸만큼 강제 이동(밀치기/당기기)을 수행합니다.
    /// 벽이나 다른 생존 유닛과 충돌했는지 여부(bool)를 반환합니다.
    /// </summary>
    public bool ForcedMoveInDirection(Vector2Int direction, int distance, float moveSpeedMultiplier = 2.5f)
    {
        if (isDead || characterMove == null || GameMap.current == null) return false;

        Tile startTile = characterMove.GetCurrentTile();
        if (startTile == null) return false;

        Vector2Int startCoord = startTile.GetCoord();
        Character collidedUnit = null;
        bool isCollided = GameMap.current.CalculateKnockbackPosition(
            startCoord, direction, distance, out Vector2Int finalCoord, out collidedUnit);

        if (finalCoord != startCoord)
        {
            Tile destTile = GameMap.current.GetTile(finalCoord);
            if (destTile != null)
            {
                List<Tile> directPath = new List<Tile> { destTile };
                characterMove.MoveAlongPath(directPath, destTile, null, MoveFlag.Forced, moveSpeedMultiplier);
            }
        }
        else
        {
            // 이미 벽이나 유닛에 막혀 0칸 이동한 경우에도 이벤트 발행
            MoveInfo zeroInfo = new MoveInfo(this, startCoord, startCoord, MoveFlag.Forced);
            zeroInfo.moveMultiplier = moveSpeedMultiplier;
            CombatEventDispatcher.DispatchBeforeForcedMove(this.eventBus, ref zeroInfo);
            CombatEventDispatcher.DispatchAfterForcedMove(this.eventBus, zeroInfo);
        }

        return isCollided;
    }

    /// <summary>
    /// 목표 타일로 신속하게 돌진 이동합니다. 이동 완료 후 콜백을 실행합니다.
    /// </summary>
    public bool DashToTile(Tile targetTile, Action onComplete = null)
    {
        if (isDead || characterMove == null || targetTile == null || GameMap.current == null) return false;

        Tile currentTile = characterMove.GetCurrentTile();
        if (currentTile == null || currentTile == targetTile)
        {
            onComplete?.Invoke();
            return false;
        }

        // 다른 생존 유닛이 점유 중인 타일로는 돌진 불가
        if (targetTile.occupyingCharacter != null && !targetTile.occupyingCharacter.isDead && targetTile.occupyingCharacter != this)
        {
            return false;
        }

        List<Tile> path = GameMap.current.FindPath(currentTile, targetTile, characterMove.capabilities);
        if (path == null || path.Count == 0)
        {
            path = new List<Tile> { targetTile };
        }

        characterMove.MoveAlongPath(path, targetTile, onComplete, MoveFlag.Normal, 2.5f);
        return true;
    }

    /// <summary>
    /// 대상 캐릭터와 인접한 빈 타일로 돌진하여 이동합니다.
    /// </summary>
    public bool DashTo(Character target, Action onComplete = null)
    {
        if (target == null || target.characterMove == null || GameMap.current == null) return false;

        Tile targetTile = target.characterMove.GetCurrentTile();
        Tile currentTile = (characterMove != null) ? characterMove.GetCurrentTile() : null;
        if (targetTile == null || currentTile == null) return false;

        List<List<Tile>> tileMap = GameMap.current.GetTileMap();
        if (tileMap == null) return false;

        Vector2Int tCoord = targetTile.GetCoord();
        Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };
        Tile bestTile = null;
        int minDistance = int.MaxValue;

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2Int c = tCoord + dirs[i];
            if (c.x >= 0 && c.x < tileMap.Count && c.y >= 0 && c.y < tileMap[0].Count)
            {
                Tile tile = tileMap[c.x][c.y];
                if (tile != null && tile.CanEnter(characterMove))
                {
                    int d = Tile.GetDistance(currentTile, tile);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        bestTile = tile;
                    }
                }
            }
        }

        if (bestTile != null)
        {
            return DashToTile(bestTile, onComplete);
        }

        return false;
    }

    #endregion
}
