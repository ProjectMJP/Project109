using EventStructs;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 그리드 타일 위에 배치되는 데이터 설정형 전술 함정 컴포넌트입니다.
/// 즉시 발동(ImmediateOnEnter), N회 밟기(StepCountThreshold), 방향성 직선 발사(DirectionalShot)를 지원합니다.
/// </summary>
public class TrapObject : MonoBehaviour, ITileHazard
{
    [Header("Identity & Trigger")]
    [SerializeField] private string trapId = "Trap_Temple_Spike";
    [SerializeField] private TrapTriggerType triggerType = TrapTriggerType.ImmediateOnEnter;
    [SerializeField, Min(1)] private int requiredStepCount = 1;
    [SerializeField] private bool resetStepCountOnTrigger = true;
    [SerializeField] private bool rearmAfterTrigger = true;
    [SerializeField] private bool triggerOnlyInBattle = true;
    [SerializeField] private bool canHitDeadUnits = false;

    [Header("Payload")]
    [SerializeField, Min(0f)] private float damageAmount = 10f;
    [SerializeField] private bool ignoreShield = false;
    [SerializeField] private string statusEffectId = "Bleeding";
    [SerializeField, Min(0)] private int statusEffectStacks = 2;
    [SerializeField] private float statusEffectDuration = 999f;

    [Header("Directional Shot")]
    [SerializeField] private Vector2Int fireDirection = Vector2Int.right;
    [SerializeField, Min(1)] private int maxShotRange = 20;
    [SerializeField] private bool hitUnitOnOriginTile = false;

    [Header("Visual Feedback (Optional)")]
    [SerializeField] private Animator trapAnimator = null;
    [SerializeField] private string triggerAnimParam = "Trigger";
    [SerializeField] private Transform spikeVisualRoot = null;
    [SerializeField] private float spikePulseMultiplier = 1.6f;
    [SerializeField] private float spikePulseDuration = 0.25f;

    private Tile _occupiedTile;
    private bool _isActive = true;
    private bool _isDisarmed = false;
    private int _currentStepCount = 0;
    private Func<bool> _canTriggerCondition;

    private Coroutine _visualCoroutine;
    private Vector3 _baseVisualScale = Vector3.one;
    private bool _hasCachedVisualScale = false;

    public string TrapId => trapId;
    public TrapTriggerType TriggerType => triggerType;
    public int CurrentStepCount => _currentStepCount;
    public int RequiredStepCount => requiredStepCount;

    public Tile OccupiedTile => _occupiedTile;
    public bool IsActive => _isActive && !_isDisarmed;

    /// <summary>함정이 실제 발동되었을 때 발행되는 이벤트 (함정, 피격 대상)</summary>
    public event Action<TrapObject, Character> OnTrapTriggered;

    private void Awake()
    {
        CacheBaseVisualScale();
    }

    private void OnDisable()
    {
        if (_visualCoroutine != null)
        {
            StopCoroutine(_visualCoroutine);
            _visualCoroutine = null;
        }
        RestoreVisualScale();
    }

    private void OnDestroy()
    {
        if (_occupiedTile != null && ReferenceEquals(_occupiedTile.occupyingHazard, this))
        {
            _occupiedTile.occupyingHazard = null;
        }
        _occupiedTile = null;
        _canTriggerCondition = null;
        OnTrapTriggered = null;
    }

    /// <summary>
    /// 타일 위에서 함정을 초기화하고 점유 슬롯에 등록합니다.
    /// </summary>
    public void InitializeOnTile(Tile tile)
    {
        if (_occupiedTile != null && _occupiedTile != tile && ReferenceEquals(_occupiedTile.occupyingHazard, this))
        {
            _occupiedTile.occupyingHazard = null;
        }

        _occupiedTile = tile;
        _currentStepCount = 0;
        _isDisarmed = false;
        _isActive = true;

        if (_occupiedTile != null)
        {
            _occupiedTile.occupyingHazard = this;
        }

        CacheBaseVisualScale();
    }

    /// <summary>
    /// 상위 매니저(StageManager 등)에서 전투 상태 판별 조건을 주입합니다.
    /// L2 World 계층이 L3 Managers를 직접 참조하지 않도록 위임 패턴을 사용합니다.
    /// </summary>
    public void SetTriggerCondition(Func<bool> canTriggerCondition)
    {
        _canTriggerCondition = canTriggerCondition;
    }

    /// <summary>
    /// 런타임 또는 테스트 코드에서 함정 발동 규칙과 피해량을 일괄 구성합니다.
    /// </summary>
    public void Configure(
        TrapTriggerType newTriggerType,
        float newDamageAmount,
        string newEffectId = "Bleeding",
        int newEffectStacks = 2,
        int newRequiredSteps = 1,
        bool newRearmAfterTrigger = true,
        Vector2Int? newFireDirection = null)
    {
        triggerType = newTriggerType;
        damageAmount = Mathf.Max(0f, newDamageAmount);
        statusEffectId = newEffectId;
        statusEffectStacks = Mathf.Max(0, newEffectStacks);
        requiredStepCount = Mathf.Max(1, newRequiredSteps);
        rearmAfterTrigger = newRearmAfterTrigger;
        if (newFireDirection.HasValue)
        {
            fireDirection = newFireDirection.Value;
        }
    }

    public void Disarm()
    {
        _isDisarmed = true;
        _isActive = false;
    }

    public void Rearm()
    {
        _isDisarmed = false;
        _isActive = true;
        _currentStepCount = 0;
    }

    public void OnUnitEnter(Character unit)
    {
        if (unit == null) return;
        if (!canHitDeadUnits && unit.isDead) return;
        if (!CanTriggerNow()) return;

        switch (triggerType)
        {
            case TrapTriggerType.ImmediateOnEnter:
                TriggerImmediate(unit);
                break;

            case TrapTriggerType.StepCountThreshold:
                TriggerByStepThreshold(unit);
                break;

            case TrapTriggerType.DirectionalShot:
                TriggerDirectionalShot(unit);
                break;
        }
    }

    public void OnUnitExit(Character unit)
    {
        // 압력판 해제 등 후속 확장 훅
    }

    private bool CanTriggerNow()
    {
        if (!IsActive) return false;

        if (triggerOnlyInBattle && _canTriggerCondition != null && !_canTriggerCondition.Invoke())
        {
            return false;
        }

        return true;
    }

    private void TriggerImmediate(Character unit)
    {
        ApplyTrapPayload(unit);
        CompleteTrigger(unit);
    }

    private void TriggerByStepThreshold(Character unit)
    {
        _currentStepCount++;
        int threshold = Mathf.Max(1, requiredStepCount);
        if (_currentStepCount < threshold)
        {
            return;
        }

        if (resetStepCountOnTrigger)
        {
            _currentStepCount = 0;
        }

        ApplyTrapPayload(unit);
        CompleteTrigger(unit);
    }

    private void TriggerDirectionalShot(Character triggerUnit)
    {
        Character hitTarget = null;
        GameMap map = null;
        if (_occupiedTile != null)
        {
            map = _occupiedTile.ownerMap != null ? _occupiedTile.ownerMap : GameMap.current;
        }
        else
        {
            map = GameMap.current;
        }

        if (_occupiedTile != null && map != null)
        {
            LookDirection shotDir = ResolveShotDirection(triggerUnit);
            Vector2Int originCoord = _occupiedTile.GetCoord();
            int startStep = hitUnitOnOriginTile ? 0 : 1;
            int range = Mathf.Max(1, maxShotRange);

            for (int step = startStep; step <= range; step++)
            {
                Vector2Int checkCoord = step == 0 ? originCoord : HexMath.Step(originCoord, shotDir, step);
                if (!map.IsValidCoordinate(checkCoord.x, checkCoord.y))
                {
                    break;
                }

                Tile checkTile = map.GetTile(checkCoord);
                if (checkTile == null)
                {
                    break;
                }

                // 발사 타일 이후 벽 또는 장애물에 부딪히면 직선 투사체 차단
                if (step > 0 && (checkTile.tileState == TileState.Full || checkTile.tileState == TileState.Obstacle))
                {
                    if (checkTile.occupyingDestructible != null && !checkTile.occupyingDestructible.isDead && damageAmount > 0f)
                    {
                        DamageInfo objDmg = new DamageInfo(null, null, damageAmount, BuildDamageFlag());
                        checkTile.occupyingDestructible.TakeDamage(objDmg);
                    }
                    break;
                }

                Character occupant = checkTile.occupyingCharacter;
                if (occupant != null && (canHitDeadUnits || !occupant.isDead))
                {
                    hitTarget = occupant;
                    ApplyTrapPayload(occupant);
                    break;
                }
            }
        }

        CompleteTrigger(hitTarget);
    }

    private void CompleteTrigger(Character hitTarget)
    {
        PlayTriggerVisual();
        OnTrapTriggered?.Invoke(this, hitTarget);

        if (!rearmAfterTrigger)
        {
            Disarm();
        }
    }

    private void ApplyTrapPayload(Character target)
    {
        if (target == null) return;
        if (!canHitDeadUnits && target.isDead) return;

        if (damageAmount > 0f)
        {
            DamageInfo dmgInfo = new DamageInfo(null, target, damageAmount, BuildDamageFlag());
            target.TakeDamage(dmgInfo);
        }

        if (!string.IsNullOrEmpty(statusEffectId) && statusEffectStacks > 0 && !target.isDead)
        {
            // EffectManager.Tick이 0 이하 duration을 즉시 만료로 처리하므로 0 이하일 경우 999f로 보정
            float resolvedDuration = statusEffectDuration > 0f ? statusEffectDuration : 999f;
            target.TakeEffect(null, statusEffectId, statusEffectStacks, resolvedDuration);
        }
    }

    private DamageFlag BuildDamageFlag()
    {
        return ignoreShield ? DamageFlag.IgnoreShield : DamageFlag.Normal;
    }

    private LookDirection ResolveShotDirection(Character triggerUnit)
    {
        if (fireDirection != Vector2Int.zero)
        {
            return HexMath.WorldVectorToDirection(fireDirection.x, fireDirection.y, LookDirection.East);
        }

        if (triggerUnit != null && triggerUnit.characterMove != null)
        {
            return triggerUnit.characterMove.facingDirection;
        }

        return LookDirection.East;
    }

    private void PlayTriggerVisual()
    {
        if (trapAnimator != null && trapAnimator.isActiveAndEnabled && trapAnimator.runtimeAnimatorController != null)
        {
            if (!string.IsNullOrEmpty(triggerAnimParam) && HasAnimatorTrigger(trapAnimator, triggerAnimParam))
            {
                trapAnimator.SetTrigger(triggerAnimParam);
                return;
            }
        }

        Transform visualTarget = GetVisualTarget();
        if (visualTarget != null && gameObject.activeInHierarchy && spikePulseDuration > 0f)
        {
            if (_visualCoroutine != null)
            {
                StopCoroutine(_visualCoroutine);
                RestoreVisualScale();
            }
            _visualCoroutine = StartCoroutine(PulseSpikeRoutine(visualTarget));
        }
    }

    private IEnumerator PulseSpikeRoutine(Transform visualTarget)
    {
        CacheBaseVisualScale();

        float halfDuration = Mathf.Max(0.02f, spikePulseDuration * 0.5f);
        float peakMultiplier = Mathf.Max(1f, spikePulseMultiplier);

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            if (visualTarget == null)
            {
                _visualCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float scaleY = Mathf.Lerp(_baseVisualScale.y, _baseVisualScale.y * peakMultiplier, t);
            visualTarget.localScale = new Vector3(_baseVisualScale.x, scaleY, _baseVisualScale.z);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            if (visualTarget == null)
            {
                _visualCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float scaleY = Mathf.Lerp(_baseVisualScale.y * peakMultiplier, _baseVisualScale.y, t);
            visualTarget.localScale = new Vector3(_baseVisualScale.x, scaleY, _baseVisualScale.z);
            yield return null;
        }

        RestoreVisualScale();
        _visualCoroutine = null;
    }

    private Transform GetVisualTarget()
    {
        if (spikeVisualRoot != null) return spikeVisualRoot;
        return transform;
    }

    private void CacheBaseVisualScale()
    {
        if (_hasCachedVisualScale) return;
        Transform visualTarget = GetVisualTarget();
        if (visualTarget != null)
        {
            _baseVisualScale = visualTarget.localScale;
            _hasCachedVisualScale = true;
        }
    }

    private void RestoreVisualScale()
    {
        if (!_hasCachedVisualScale) return;
        Transform visualTarget = GetVisualTarget();
        if (visualTarget != null)
        {
            visualTarget.localScale = _baseVisualScale;
        }
    }

    private static bool HasAnimatorTrigger(Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == paramName)
            {
                return true;
            }
        }
        return false;
    }
}
