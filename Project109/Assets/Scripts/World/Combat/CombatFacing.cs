using System.Collections.Generic;
using CardTypes;
using EventStructs;
using UnityEngine;

/// <summary>
/// 전투 중 방향(Facing) 판정, 정후면 추가 피해, 공격 유형별 원점/투사체/AoE 판정 및
/// 카드/스킬 1회 시전 완료 시점의 일괄 피격 회전을 조율하는 정적 코어입니다.
/// </summary>
public static class CombatFacing
{
    /// <summary>정후면(1방향) 피격 시 적용되는 전역 기본 피해 배율(+50%).</summary>
    public const float DirectBackDamageMultiplier = 1.5f;

    private struct PendingHitRotation
    {
        public Character target;
        public LookDirection turnDirection;
    }

    private static int _nextSessionId = 1;
    private static int _activeSessionId = 0;
    private static Character _activeSessionCaster;
    private static FacingOriginMode _activeSessionOriginMode = FacingOriginMode.CasterPosition;
    private static Vector2Int _activeSessionOriginCoord;
    private static LookDirection _activeSessionTravelDirection = LookDirection.East;
    private static readonly List<PendingHitRotation> _pendingHitRotations = new List<PendingHitRotation>(8);
    private static readonly List<PendingHitRotation> _flushRotationBuffer = new List<PendingHitRotation>(8);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _nextSessionId = 1;
        ClearActiveCastSession();
    }

    public static bool HasActiveCastSession => _activeSessionId != 0;

    /// <summary>현재 활성 시전 세션이 지정된 시전자와 일치하는지 확인합니다.</summary>
    public static bool HasMatchingActiveCastSession(Character caster)
    {
        return _activeSessionId != 0 && caster != null && ReferenceEquals(_activeSessionCaster, caster);
    }

    /// <summary>
    /// 카드/스킬 1회 시전 동안의 방향 판정 기준과 지연 피격 회전 세션을 시작합니다.
    /// 다단히트 동안 피격 대상의 시선을 유지하다가 시전 종료 시 일괄 회전시킵니다.
    /// </summary>
    public static int BeginCastSession(
        Character caster,
        CardData cardData,
        Vector2Int casterCoord,
        LookDirection casterFacing,
        Vector2Int targetPosition)
    {
        ResolveCardFacingOrigin(
            cardData,
            casterCoord,
            casterFacing,
            targetPosition,
            out FacingOriginMode mode,
            out Vector2Int originCoord,
            out LookDirection travelDirection);

        _activeSessionId = _nextSessionId++;
        if (_activeSessionId == 0)
        {
            _activeSessionId = _nextSessionId++;
        }

        _activeSessionCaster = caster;
        _activeSessionOriginMode = mode;
        _activeSessionOriginCoord = originCoord;
        _activeSessionTravelDirection = travelDirection;
        _pendingHitRotations.Clear();
        return _activeSessionId;
    }

    /// <summary>
    /// 진행 중인 시전 세션 내 피격 대상의 회전 예정 방향을 기록합니다.
    /// 동일 대상 다단히트 시 최초 피격 원점 방향을 보존합니다.
    /// </summary>
    public static void RecordPendingHitRotation(Character target, LookDirection turnDirection)
    {
        if (!HasActiveCastSession || !CanRotateOnHit(target))
        {
            return;
        }

        for (int i = 0; i < _pendingHitRotations.Count; i++)
        {
            if (ReferenceEquals(_pendingHitRotations[i].target, target))
            {
                return;
            }
        }

        _pendingHitRotations.Add(new PendingHitRotation
        {
            target = target,
            turnDirection = turnDirection
        });
    }

    /// <summary>
    /// 카드/스킬 시전 세션을 종료하고, 유효 시전 완료 시 기록된 피격 대상들을 공격 원점 방향으로 일괄 회전시킵니다.
    /// </summary>
    public static void CompleteCastSession(int sessionId, bool applyRotations = true)
    {
        if (sessionId == 0 || sessionId != _activeSessionId)
        {
            return;
        }

        _flushRotationBuffer.Clear();
        if (applyRotations && _pendingHitRotations.Count > 0)
        {
            _flushRotationBuffer.AddRange(_pendingHitRotations);
        }

        ClearActiveCastSession();

        if (!applyRotations || _flushRotationBuffer.Count == 0)
        {
            _flushRotationBuffer.Clear();
            return;
        }

        for (int i = 0; i < _flushRotationBuffer.Count; i++)
        {
            PendingHitRotation entry = _flushRotationBuffer[i];
            if (entry.target != null)
            {
                entry.target.TryRotateOnHit(entry.turnDirection);
            }
        }

        _flushRotationBuffer.Clear();
    }

    /// <summary>활성 시전 세션과 대기 회전 목록을 즉시 초기화합니다. (전투 종료/테스트 정리용)</summary>
    public static void ClearActiveCastSession()
    {
        _activeSessionId = 0;
        _activeSessionCaster = null;
        _activeSessionOriginMode = FacingOriginMode.CasterPosition;
        _activeSessionOriginCoord = Vector2Int.zero;
        _activeSessionTravelDirection = LookDirection.East;
        _pendingHitRotations.Clear();
    }

    /// <summary>
    /// 카드 데이터의 타깃팅 유형과 효과 범위를 바탕으로 방향 판정 기준(근접·투사체 진행방향·광역 중심점)을 결정합니다.
    /// </summary>
    public static void ResolveCardFacingOrigin(
        CardData cardData,
        Vector2Int casterCoord,
        LookDirection casterFacing,
        Vector2Int targetPosition,
        out FacingOriginMode mode,
        out Vector2Int originCoord,
        out LookDirection travelDirection)
    {
        if (HasDirectionalLineArea(cardData))
        {
            mode = FacingOriginMode.TravelDirection;
            travelDirection = HexMath.DirectionFromCoords(casterCoord, targetPosition, casterFacing);
            originCoord = casterCoord;
            return;
        }

        if (cardData != null && cardData.parsedTargetType == TargetType.AreaGround)
        {
            mode = FacingOriginMode.OriginCoord;
            originCoord = targetPosition;
            travelDirection = casterFacing;
            return;
        }

        if (cardData != null && cardData.parsedTargetType == TargetType.AreaSelf)
        {
            mode = FacingOriginMode.OriginCoord;
            originCoord = casterCoord;
            travelDirection = casterFacing;
            return;
        }

        mode = FacingOriginMode.CasterPosition;
        originCoord = casterCoord;
        travelDirection = casterFacing;
    }

    /// <summary>해당 피해가 방향 보정 및 피격 회전 규칙의 적용 대상인지 판별합니다.</summary>
    public static bool ShouldApplyFacing(Character target, in DamageInfo info)
    {
        if (target == null || target.characterMove == null)
        {
            return false;
        }

        if (info.facingOriginMode == FacingOriginMode.IgnoreFacing)
        {
            return false;
        }

        if (info.damageFlags.HasFlag(DamageFlag.HPLoss) || info.damageFlags.HasFlag(DamageFlag.Reflected))
        {
            return false;
        }

        if (info.facingOriginMode == FacingOriginMode.Auto
            && (info.caster == null || ReferenceEquals(info.caster, target))
            && !HasMatchingActiveCastSession(info.caster))
        {
            return false;
        }

        return true;
    }

    /// <summary>피격자가 행동 불가(기절 등)나 사망 상태가 아니어서 피격 후 회전이 가능한지 확인합니다.</summary>
    public static bool CanRotateOnHit(Character target)
    {
        return target != null
            && !target.isDead
            && target.currentState != CharacterState.Die
            && target.characterMove != null
            && !target.IsIncapacitated();
    }

    /// <summary>피격자와 피해 컨텍스트를 기반으로 방향 관계 및 돌아볼 방향을 계산합니다.</summary>
    public static bool EvaluateDamageFacing(
        Character target,
        in DamageInfo info,
        out FacingHitRelation relation,
        out LookDirection sourceDirection,
        out LookDirection turnDirection,
        out bool hasTurnDirection)
    {
        relation = FacingHitRelation.None;
        sourceDirection = LookDirection.East;
        turnDirection = LookDirection.East;
        hasTurnDirection = false;

        if (!ShouldApplyFacing(target, in info))
        {
            return false;
        }

        FacingOriginMode effectiveMode = info.facingOriginMode;
        Vector2Int effectiveOriginCoord = info.attackOriginCoord;
        LookDirection effectiveTravelDir = info.incomingTravelDirection;

        if (effectiveMode == FacingOriginMode.Auto)
        {
            if (HasMatchingActiveCastSession(info.caster))
            {
                effectiveMode = _activeSessionOriginMode;
                effectiveOriginCoord = _activeSessionOriginCoord;
                effectiveTravelDir = _activeSessionTravelDirection;
            }
            else
            {
                effectiveMode = FacingOriginMode.CasterPosition;
            }
        }

        LookDirection targetFacing = target.characterMove.facingDirection;
        Vector2Int targetCoord = target.GetGridPosition();

        if (effectiveMode == FacingOriginMode.TravelDirection)
        {
            sourceDirection = HexMath.GetAttackSourceDirectionFromTravel(effectiveTravelDir);
            relation = HexMath.EvaluateFacingRelation(targetFacing, sourceDirection);
            turnDirection = sourceDirection;
            hasTurnDirection = true;
            return true;
        }

        Vector2Int originCoord;
        if (effectiveMode == FacingOriginMode.OriginCoord)
        {
            originCoord = effectiveOriginCoord;
        }
        else
        {
            if (info.caster == null || ReferenceEquals(info.caster, target) || info.caster.characterMove == null)
            {
                return false;
            }

            originCoord = info.caster.GetGridPosition();
        }

        if (targetCoord != originCoord)
        {
            sourceDirection = HexMath.DirectionFromCoords(targetCoord, originCoord, targetFacing);
            relation = HexMath.EvaluateFacingRelation(targetFacing, sourceDirection);
            turnDirection = sourceDirection;
            hasTurnDirection = true;
            return true;
        }

        // AoE 중심점과 동일 타일 피격 시: 방향 보정은 None(1.0배)이지만, 외부 공격자가 다른 타일에 있으면 돌아보기 방향은 공격자 기준
        if (info.caster != null && !ReferenceEquals(info.caster, target) && info.caster.characterMove != null)
        {
            Vector2Int casterCoord = info.caster.GetGridPosition();
            if (casterCoord != targetCoord)
            {
                turnDirection = HexMath.DirectionFromCoords(targetCoord, casterCoord, targetFacing);
                hasTurnDirection = true;
            }
        }

        return false;
    }

    /// <summary>DamageInfo에 방향 판정 결과와 정후면 1.5배 보정을 반영합니다.</summary>
    public static void ApplyFacingToDamageInfo(
        Character target,
        ref DamageInfo info,
        out LookDirection turnDirection,
        out bool hasTurnDirection)
    {
        if (!EvaluateDamageFacing(target, in info, out FacingHitRelation relation, out LookDirection sourceDir, out turnDirection, out hasTurnDirection))
        {
            info.facingHitRelation = FacingHitRelation.None;
            info.hasAttackSourceDirection = false;
            info.isBackstab = false;
            info.facingDamageMultiplier = 1f;
            return;
        }

        info.facingHitRelation = relation;
        info.resolvedSourceDirection = sourceDir;
        info.hasAttackSourceDirection = true;
        info.isBackstab = relation == FacingHitRelation.DirectBack;
        info.facingDamageMultiplier = info.isBackstab ? DirectBackDamageMultiplier : 1f;

        if (info.isBackstab)
        {
            info.damageMultiplier *= DirectBackDamageMultiplier;
        }
    }

    /// <summary>
    /// 실제 전투 데미지 파이프라인과 동일한 방향 보정·버프·취약/약화 배율로 카드 예상 피해를 부작용 없이 시뮬레이션합니다.
    /// </summary>
    public static bool TrySimulateCardDamagePreview(
        Character caster,
        Character target,
        Card card,
        Vector2Int targetPosition,
        out DamagePreviewInfo preview)
    {
        preview = default;
        if (caster == null || target == null || target.isDead || card == null || card.cardData == null)
        {
            return false;
        }

        float baseDamage = card.GetEffectiveValue("damage");
        if (baseDamage <= 0f)
        {
            return false;
        }

        Vector2Int casterCoord = caster.GetGridPosition();
        LookDirection casterFacing = caster.characterMove != null ? caster.characterMove.facingDirection : LookDirection.East;

        ResolveCardFacingOrigin(
            card.cardData,
            casterCoord,
            casterFacing,
            targetPosition,
            out FacingOriginMode mode,
            out Vector2Int originCoord,
            out LookDirection travelDir);

        DamageInfo simInfo = new DamageInfo(caster, target, baseDamage, DamageFlag.Normal);
        if (mode == FacingOriginMode.TravelDirection)
        {
            simInfo.SetIncomingTravelDirection(travelDir);
        }
        else if (mode == FacingOriginMode.OriginCoord)
        {
            simInfo.SetOriginCoord(originCoord);
        }
        else
        {
            simInfo.facingOriginMode = FacingOriginMode.CasterPosition;
        }

        ApplyFacingToDamageInfo(target, ref simInfo, out _, out _);

        if (caster.eventBus != null)
        {
            CombatEventDispatcher.DispatchBeforeDealDamage(caster.eventBus, ref simInfo);
        }

        if (target.eventBus != null)
        {
            CombatEventDispatcher.DispatchBeforeTakeDamage(target.eventBus, ref simInfo);
        }

        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(simInfo.baseDamageAmount * simInfo.damageMultiplier));
        preview = new DamagePreviewInfo
        {
            isValid = true,
            expectedDamage = finalDamage,
            facingRelation = simInfo.facingHitRelation,
            facingMultiplier = simInfo.facingDamageMultiplier
        };
        return true;
    }

    private static bool HasDirectionalLineArea(CardData cardData)
    {
        if (cardData == null || cardData.additionalEffectAreaList == null)
        {
            return false;
        }

        for (int i = 0; i < cardData.additionalEffectAreaList.Count; i++)
        {
            EffectArea area = cardData.additionalEffectAreaList[i];
            if (area == null || string.IsNullOrEmpty(area.areaType))
            {
                continue;
            }

            if (area.areaType == "DirectionalLine" || area.areaType == "TargetLine" || area.areaType == "Line")
            {
                return true;
            }
        }

        return false;
    }
}
