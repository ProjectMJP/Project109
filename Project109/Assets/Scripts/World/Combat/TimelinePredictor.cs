using System;
using System.Collections.Generic;
using EventStructs;
using UnityEngine;

/// <summary>
/// 전투 타임라인에 표시될 단일 행동 슬롯 정보입니다.
/// </summary>
public struct TimelineEntry
{
    public ICharacterController controller;
    public Character character;
    public bool isPlayerTeam;
    public bool isCurrentTurn;
    public bool isPredictedNextTurn;
    public bool isStunned;
    public float timeUntilTurn;
    public float overflowStamina;
    public float predictedStartStamina;
}

/// <summary>
/// 카드 또는 턴 종료 호버 미리보기 시 타임라인 예측기에 전달하는 가상 변화 요청입니다.
/// 실제 게임 상태를 수정하지 않고 가상 스냅샷 위에서 결과를 산출합니다.
/// </summary>
public struct TimelinePreviewRequest
{
    public bool hasPreview;
    public float casterStaminaSpent;
    public Character targetCharacter;
    public float targetStaminaDelta;
    public float targetRegenFlatDelta;
    public float targetRegenPercentDelta;
    public float targetRegenDuration;
    public bool targetGrantsStun;

    public static readonly TimelinePreviewRequest None = default;
}

/// <summary>
/// 살아있는 전투원의 다음 1회 행동 순서와 플레이어 턴 기준 다음 차례("내 다음 차례")를
/// 수명형 StatModifier 및 IOnCalculateTurnEndStamina 선계산 훅을 반영하여 GC 무할당으로 예측합니다.
/// </summary>
public class TimelinePredictor
{
    private const float TimeTieEpsilon = 0.0001f;
    private readonly List<float> _breakpointScratch = new List<float>(8);

    /// <summary>
    /// 현재 전투 상태(및 선택적 호버 미리보기)를 기준으로 타임라인 엔트리 목록을 계산하여 results에 채웁니다.
    /// </summary>
    public void Predict(
        IReadOnlyList<ICharacterController> playerTeam,
        IReadOnlyList<ICharacterController> enemyTeam,
        ICharacterController currentTurnController,
        List<TimelineEntry> results,
        in TimelinePreviewRequest preview = default)
    {
        if (results == null) return;
        results.Clear();

        bool hasCurrentTurn = currentTurnController != null
            && currentTurnController.controlledCharacter != null
            && !currentTurnController.controlledCharacter.isDead;

        // 1. 현재 턴 소유자를 0번 슬롯(하이라이트)으로 배치
        if (hasCurrentTurn)
        {
            Character currentChar = currentTurnController.controlledCharacter;
            bool isCurrentPlayerTeam = IsInTeam(playerTeam, currentTurnController)
                || currentChar.faction == CharacterFaction.Player
                || currentChar.faction == CharacterFaction.Ally;

            results.Add(new TimelineEntry
            {
                controller = currentTurnController,
                character = currentChar,
                isPlayerTeam = isCurrentPlayerTeam,
                isCurrentTurn = true,
                isPredictedNextTurn = false,
                isStunned = IsUnitStunned(currentChar, in preview),
                timeUntilTurn = 0f,
                overflowStamina = currentChar.overflowStamina,
                predictedStartStamina = currentChar.curStamina,
            });
        }

        int sortStartIndex = results.Count;

        // 2. 나머지 살아있는 아군/적군 전투원의 다음 1회 도달 시간 산출
        CollectTeamEntries(playerTeam, isPlayerTeam: true, currentTurnController, results, in preview);
        CollectTeamEntries(enemyTeam, isPlayerTeam: false, currentTurnController, results, in preview);

        // 3. 현재 턴 소유자가 플레이어 팀인 경우에만 "내 다음 차례" 이월 예측 슬롯 추가
        // (적 턴 중에는 적의 미확정 카드/이월 변수를 추측하지 않음)
        if (hasCurrentTurn && results[0].isPlayerTeam)
        {
            TimelineEntry predictedSelf = BuildPredictedNextPlayerTurn(currentTurnController, in preview);
            results.Add(predictedSelf);
        }

        // 4. sortStartIndex 이후 엔트리를 도달 시간·오버플로우·팀 우선순위로 무할당 정렬
        SortEntries(results, sortStartIndex);
    }

    private void CollectTeamEntries(
        IReadOnlyList<ICharacterController> team,
        bool isPlayerTeam,
        ICharacterController currentTurnController,
        List<TimelineEntry> results,
        in TimelinePreviewRequest preview)
    {
        if (team == null) return;

        for (int i = 0; i < team.Count; i++)
        {
            ICharacterController controller = team[i];
            if (controller == null || ReferenceEquals(controller, currentTurnController)) continue;

            Character c = controller.controlledCharacter;
            if (c == null || c.isDead) continue;

            CharacterStat baseStat = c.baseCharacterStat ?? c.curCharacterStat;
            if (baseStat == null) continue;

            float previewStamina = c.curStamina;
            float previewOverflow = c.overflowStamina;
            float regenFlat = 0f;
            float regenPercent = 0f;
            float regenDuration = 0f;

            if (preview.hasPreview && preview.targetCharacter == c)
            {
                previewStamina = Mathf.Max(0f, previewStamina + preview.targetStaminaDelta);
                if (preview.targetStaminaDelta < 0f && c.curCharacterStat != null && previewStamina < c.curCharacterStat.maxStamina)
                {
                    // ATB 삭감 카드 등으로 최대치 미만으로 떨어지면 대기 중이던 오버플로우도 0으로 간주
                    previewOverflow = 0f;
                }
                regenFlat = preview.targetRegenFlatDelta;
                regenPercent = preview.targetRegenPercentDelta;
                regenDuration = preview.targetRegenDuration;
            }

            float timeUntilTurn = StatModifierEvaluator.CalculateTimeUntilTurn(
                baseStat,
                c.StatModifiers,
                previewStamina,
                previewOverflow,
                excludeUntilTurnEnd: false,
                _breakpointScratch,
                out float effectiveOverflow,
                out _,
                previewRegenFlat: regenFlat,
                previewRegenPercent: regenPercent,
                previewRegenDuration: regenDuration);

            results.Add(new TimelineEntry
            {
                controller = controller,
                character = c,
                isPlayerTeam = isPlayerTeam,
                isCurrentTurn = false,
                isPredictedNextTurn = false,
                isStunned = IsUnitStunned(c, in preview),
                timeUntilTurn = timeUntilTurn,
                overflowStamina = effectiveOverflow,
                predictedStartStamina = previewStamina,
            });
        }
    }

    private TimelineEntry BuildPredictedNextPlayerTurn(
        ICharacterController playerController,
        in TimelinePreviewRequest preview)
    {
        Character playerChar = playerController.controlledCharacter;
        CharacterStat baseStat = playerChar.baseCharacterStat ?? playerChar.curCharacterStat;

        float spent = preview.hasPreview ? Mathf.Max(0f, preview.casterStaminaSpent) : 0f;
        float selfTargetDelta = (preview.hasPreview && preview.targetCharacter == playerChar)
            ? preview.targetStaminaDelta
            : 0f;
        float projectedRemaining = Mathf.Max(0f, playerChar.curStamina - spent + selfTargetDelta);

        TurnEndStaminaInfo endInfo = playerChar.CalculateTurnEndStaminaForRemaining(projectedRemaining);
        float nextTurnStartStamina = Character.ComputeCarriedStamina(projectedRemaining, playerChar.overflowStamina, in endInfo);

        float regenFlat = (preview.hasPreview && preview.targetCharacter == playerChar) ? preview.targetRegenFlatDelta : 0f;
        float regenPercent = (preview.hasPreview && preview.targetCharacter == playerChar) ? preview.targetRegenPercentDelta : 0f;
        float regenDuration = (preview.hasPreview && preview.targetCharacter == playerChar) ? preview.targetRegenDuration : 0f;

        float timeUntilNextTurn = StatModifierEvaluator.CalculateTimeUntilTurn(
            baseStat,
            playerChar.StatModifiers,
            nextTurnStartStamina,
            currentOverflow: 0f,
            excludeUntilTurnEnd: true,
            _breakpointScratch,
            out float effectiveOverflow,
            out _,
            previewRegenFlat: regenFlat,
            previewRegenPercent: regenPercent,
            previewRegenDuration: regenDuration);

        return new TimelineEntry
        {
            controller = playerController,
            character = playerChar,
            isPlayerTeam = true,
            isCurrentTurn = false,
            isPredictedNextTurn = true,
            isStunned = IsUnitStunned(playerChar, in preview),
            timeUntilTurn = timeUntilNextTurn,
            overflowStamina = effectiveOverflow,
            predictedStartStamina = nextTurnStartStamina,
        };
    }

    private static bool IsUnitStunned(Character c, in TimelinePreviewRequest preview)
    {
        if (c == null) return false;
        if (preview.hasPreview && preview.targetGrantsStun && preview.targetCharacter == c)
            return true;

        return c.effectManager != null && c.effectManager.HasEffect("Stun");
    }

    private static bool IsInTeam(IReadOnlyList<ICharacterController> team, ICharacterController target)
    {
        if (team == null || target == null) return false;
        for (int i = 0; i < team.Count; i++)
        {
            if (ReferenceEquals(team[i], target))
                return true;
        }
        return false;
    }

    private static void SortEntries(List<TimelineEntry> entries, int startIndex)
    {
        for (int i = startIndex + 1; i < entries.Count; i++)
        {
            TimelineEntry key = entries[i];
            int j = i - 1;
            while (j >= startIndex && ShouldComeBefore(key, entries[j]))
            {
                entries[j + 1] = entries[j];
                j--;
            }
            entries[j + 1] = key;
        }
    }

    private static bool ShouldComeBefore(in TimelineEntry candidate, in TimelineEntry existing)
    {
        float timeDiff = candidate.timeUntilTurn - existing.timeUntilTurn;
        if (timeDiff < -TimeTieEpsilon) return true;
        if (timeDiff > TimeTieEpsilon) return false;

        // 동일 도달 시간일 때: 오버플로우 스태미나가 큰 유닛 우선
        float overflowDiff = candidate.overflowStamina - existing.overflowStamina;
        if (overflowDiff > TimeTieEpsilon) return true;
        if (overflowDiff < -TimeTieEpsilon) return false;

        // 오버플로우도 동일할 때: 플레이어 팀 우선 (BattleManager.TickTeamCombatants와 동일)
        if (candidate.isPlayerTeam != existing.isPlayerTeam)
            return candidate.isPlayerTeam;

        // 동일 플레이어 팀 내에서는 대기 중인 실제 유닛을 예측 슬롯보다 우선
        if (candidate.isPredictedNextTurn != existing.isPredictedNextTurn)
            return !candidate.isPredictedNextTurn;

        return false;
    }
}
