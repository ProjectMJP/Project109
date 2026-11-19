using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatModifierTarget
{
    MaxStamina = 0,
    StaminaRegen = 1,
    MaxHealth = 2,
    MoveRange = 3,
}

public enum StatModifierOp
{
    Flat = 0,
    Percent = 1,
}

public enum StatModifierLifetime
{
    Permanent = 0,
    DurationSeconds = 1,
    UntilTurnEnd = 2,
    UntilNextTurnStart = 3,
}

/// <summary>
/// 캐릭터 스탯에 적용되는 수명형 보정치(Modifier) 데이터입니다.
/// 시간 제한 버프/디버프와 1턴성 유물 효과가 만료 시점을 스스로 보유하여
/// 타임라인 예측기가 부작용 없이 미래 구간의 스탯을 계산할 수 있습니다.
/// </summary>
[Serializable]
public struct StatModifier
{
    public string sourceId;
    public StatModifierTarget target;
    public StatModifierOp op;
    public float value;
    public StatModifierLifetime lifetime;
    public float remainingDuration;
    public bool isBattleScoped;

    public StatModifier(
        string sourceId,
        StatModifierTarget target,
        StatModifierOp op,
        float value,
        StatModifierLifetime lifetime = StatModifierLifetime.Permanent,
        float duration = 0f,
        bool isBattleScoped = true)
    {
        this.sourceId = sourceId ?? string.Empty;
        this.target = target;
        this.op = op;
        this.value = value;
        this.lifetime = lifetime;
        this.remainingDuration = (lifetime == StatModifierLifetime.DurationSeconds) ? Mathf.Max(0f, duration) : 0f;
        this.isBattleScoped = isBattleScoped;
    }

    public bool IsActiveAtFutureTime(float futureTime, bool excludeUntilTurnEnd)
    {
        switch (lifetime)
        {
            case StatModifierLifetime.Permanent:
            case StatModifierLifetime.UntilNextTurnStart:
                return true;
            case StatModifierLifetime.UntilTurnEnd:
                return !excludeUntilTurnEnd;
            case StatModifierLifetime.DurationSeconds:
                return remainingDuration > futureTime + 0.0001f;
            default:
                return true;
        }
    }
}

/// <summary>
/// 스탯 모디파이어 평가 및 시간 기반 ATB 도달 시간 구간 적분 유틸리티입니다.
/// 실제 BattleTick과 TimelinePredictor가 동일한 계산 공식을 사용하도록 단일화합니다.
/// </summary>
public static class StatModifierEvaluator
{
    private const float TimeEpsilon = 0.0001f;

    public static float EvaluateStat(
        float baseValue,
        StatModifierTarget target,
        IReadOnlyList<StatModifier> modifiers,
        float atFutureTime = -1f,
        bool excludeUntilTurnEnd = false,
        float extraFlat = 0f,
        float extraPercent = 0f,
        float minClamp = 0f)
    {
        float flatSum = extraFlat;
        float percentSum = extraPercent;

        if (modifiers != null)
        {
            bool filterFuture = atFutureTime >= 0f;
            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier mod = modifiers[i];
                if (mod.target != target) continue;
                if (filterFuture && !mod.IsActiveAtFutureTime(atFutureTime, excludeUntilTurnEnd)) continue;

                if (mod.op == StatModifierOp.Flat)
                    flatSum += mod.value;
                else
                    percentSum += mod.value;
            }
        }

        float evaluated = (baseValue + flatSum) * Mathf.Max(0f, 1f + percentSum);
        return Mathf.Max(minClamp, evaluated);
    }

    /// <summary>
    /// 시간 제한 버프/디버프의 만료 시점을 경계로 구간을 나누어 다음 턴 도달 예상 시간(초)을 계산합니다.
    /// 호출자가 전달한 breakpointBuffer를 재사용하여 GC 할당을 방지합니다.
    /// </summary>
    public static float CalculateTimeUntilTurn(
        CharacterStat baseStat,
        IReadOnlyList<StatModifier> modifiers,
        float currentStamina,
        float currentOverflow,
        bool excludeUntilTurnEnd,
        List<float> breakpointBuffer,
        out float effectiveOverflow,
        out float arrivalMaxStamina,
        float previewRegenFlat = 0f,
        float previewRegenPercent = 0f,
        float previewRegenDuration = 0f)
    {
        if (baseStat == null || breakpointBuffer == null)
        {
            effectiveOverflow = 0f;
            arrivalMaxStamina = 1f;
            return float.PositiveInfinity;
        }

        // 1. t = 0 기준 즉시 턴 획득 여부 점검
        float maxAtZero = EvaluateStat(
            baseStat.maxStamina,
            StatModifierTarget.MaxStamina,
            modifiers,
            atFutureTime: 0f,
            excludeUntilTurnEnd: excludeUntilTurnEnd,
            minClamp: 1f);

        if (currentStamina >= maxAtZero)
        {
            effectiveOverflow = currentOverflow + Mathf.Max(0f, currentStamina - maxAtZero);
            arrivalMaxStamina = maxAtZero;
            return 0f;
        }

        // 2. 시간 제한 모디파이어(MaxStamina, StaminaRegen) 및 프리뷰 버프 만료 경계 수집
        breakpointBuffer.Clear();
        if (modifiers != null)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier mod = modifiers[i];
                if (mod.lifetime != StatModifierLifetime.DurationSeconds) continue;
                if (mod.target != StatModifierTarget.MaxStamina && mod.target != StatModifierTarget.StaminaRegen) continue;
                if (mod.remainingDuration > TimeEpsilon)
                {
                    AddUniqueBreakpoint(breakpointBuffer, mod.remainingDuration);
                }
            }
        }

        bool hasTimedPreviewRegen = previewRegenDuration > TimeEpsilon &&
            (Mathf.Abs(previewRegenFlat) > TimeEpsilon || Mathf.Abs(previewRegenPercent) > TimeEpsilon);
        bool hasUntimedPreviewRegen = !hasTimedPreviewRegen && previewRegenDuration <= 0f &&
            (Mathf.Abs(previewRegenFlat) > TimeEpsilon || Mathf.Abs(previewRegenPercent) > TimeEpsilon);

        if (hasTimedPreviewRegen)
        {
            AddUniqueBreakpoint(breakpointBuffer, previewRegenDuration);
        }

        SortBreakpoints(breakpointBuffer);

        // 3. 각 시간 구간 [tStart, tEnd]에서 선형 충전 적분
        float tStart = 0f;
        float staminaAcc = currentStamina;
        int bpCount = breakpointBuffer.Count;

        for (int idx = 0; idx <= bpCount; idx++)
        {
            float tEnd = (idx < bpCount) ? breakpointBuffer[idx] : float.PositiveInfinity;

            float maxInInterval = EvaluateStat(
                baseStat.maxStamina,
                StatModifierTarget.MaxStamina,
                modifiers,
                atFutureTime: tStart,
                excludeUntilTurnEnd: excludeUntilTurnEnd,
                minClamp: 1f);

            if (staminaAcc >= maxInInterval)
            {
                effectiveOverflow = staminaAcc - maxInInterval;
                arrivalMaxStamina = maxInInterval;
                return tStart;
            }

            bool applyPreview = hasUntimedPreviewRegen || (hasTimedPreviewRegen && previewRegenDuration > tStart + TimeEpsilon);
            float regenInInterval = EvaluateStat(
                baseStat.staminaRegenPerSecond,
                StatModifierTarget.StaminaRegen,
                modifiers,
                atFutureTime: tStart,
                excludeUntilTurnEnd: excludeUntilTurnEnd,
                extraFlat: applyPreview ? previewRegenFlat : 0f,
                extraPercent: applyPreview ? previewRegenPercent : 0f,
                minClamp: 0f);

            float needed = maxInInterval - staminaAcc;
            if (regenInInterval > TimeEpsilon)
            {
                float dtNeeded = needed / regenInInterval;
                float dtInterval = float.IsPositiveInfinity(tEnd) ? float.PositiveInfinity : (tEnd - tStart);

                if (dtNeeded <= dtInterval + TimeEpsilon)
                {
                    effectiveOverflow = 0f;
                    arrivalMaxStamina = maxInInterval;
                    return tStart + dtNeeded;
                }

                staminaAcc += regenInInterval * dtInterval;
            }

            tStart = tEnd;
        }

        effectiveOverflow = 0f;
        arrivalMaxStamina = EvaluateStat(
            baseStat.maxStamina,
            StatModifierTarget.MaxStamina,
            modifiers,
            atFutureTime: tStart,
            excludeUntilTurnEnd: excludeUntilTurnEnd,
            minClamp: 1f);
        return float.PositiveInfinity;
    }

    private static void AddUniqueBreakpoint(List<float> buffer, float value)
    {
        for (int i = 0; i < buffer.Count; i++)
        {
            if (Mathf.Abs(buffer[i] - value) <= TimeEpsilon)
                return;
        }
        buffer.Add(value);
    }

    private static void SortBreakpoints(List<float> buffer)
    {
        for (int i = 1; i < buffer.Count; i++)
        {
            float key = buffer[i];
            int j = i - 1;
            while (j >= 0 && buffer[j] > key)
            {
                buffer[j + 1] = buffer[j];
                j--;
            }
            buffer[j + 1] = key;
        }
    }
}
