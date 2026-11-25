using System.Collections.Generic;
using CardTypes;
using EventStructs;
using UnityEngine;

internal static class TargetingStrategyHelper
{
    public static void GetCasterPose(PlayerBattleController controller, out Vector2Int originCoord, out LookDirection facing)
    {
        originCoord = Vector2Int.zero;
        facing = LookDirection.East;

        if (controller != null && controller.controlledCharacter != null && controller.controlledCharacter.characterMove != null)
        {
            facing = controller.controlledCharacter.characterMove.facingDirection;
            Tile currentTile = controller.controlledCharacter.characterMove.GetCurrentTile();
            if (currentTile != null)
            {
                originCoord = currentTile.GetCoord();
            }
        }
    }

    public static void LookAtTargetTile(PlayerBattleController controller, Tile targetTile)
    {
        if (targetTile != null && controller != null && controller.controlledCharacter != null && controller.controlledCharacter.characterMove != null)
        {
            controller.controlledCharacter.characterMove.LookAtTile(targetTile);
        }
    }

    public static HashSet<Vector2Int> CollectAoECoords(Vector2Int originCoord, Vector2Int targetPos, Card card, LookDirection facing)
    {
        HashSet<Vector2Int> affectedCoords = new HashSet<Vector2Int>();
        if (GameMap.current != null && card != null && card.cardData != null)
        {
            var aoeList = GameMap.current.CalculateCardAoECoords(originCoord, targetPos, card.cardData, facing);
            if (aoeList != null && aoeList.Count > 0)
            {
                for (int i = 0; i < aoeList.Count; i++)
                {
                    affectedCoords.Add(aoeList[i]);
                }
            }
            else
            {
                affectedCoords.Add(targetPos);
            }
        }
        else
        {
            affectedCoords.Add(targetPos);
        }

        return affectedCoords;
    }
}

#region Immediate Casting Strategies (즉시 시전형 전략)

/// <summary>
/// 시전자 자신 대상 즉시 발동 전략
/// </summary>
public class SelfTargetingStrategy : ITargetingStrategy
{
    public bool IsManualSelectionRequired => false;

    public bool HasValidTargets(PlayerBattleController controller, Card card) => true;

    public List<Character> CollectImmediateTargets(PlayerBattleController controller, Card card, Vector2Int casterPos)
    {
        return CardInfo.EmptyTargets;
    }

    public bool TryCollectTargetsOnTileClicked(PlayerBattleController controller, Card card, Tile targetTile, out List<Character> targets, out Vector2Int targetPos)
    {
        targets = CardInfo.EmptyTargets;
        targetPos = targetTile != null ? targetTile.GetCoord() : Vector2Int.zero;
        return true;
    }
}

/// <summary>
/// 전장의 살아있는 모든 적 대상 즉시 발동 전략
/// </summary>
public class AllEnemiesTargetingStrategy : ITargetingStrategy
{
    public bool IsManualSelectionRequired => false;

    public bool HasValidTargets(PlayerBattleController controller, Card card)
    {
        return controller.HasAnyLivingEnemy();
    }

    public List<Character> CollectImmediateTargets(PlayerBattleController controller, Card card, Vector2Int casterPos)
    {
        return controller.GetAllLivingEnemies();
    }

    public bool TryCollectTargetsOnTileClicked(PlayerBattleController controller, Card card, Tile targetTile, out List<Character> targets, out Vector2Int targetPos)
    {
        targets = controller.GetAllLivingEnemies();
        targetPos = targetTile != null ? targetTile.GetCoord() : Vector2Int.zero;
        return true;
    }
}

/// <summary>
/// 시전자 중심 즉시 광역 발동 (PBAoE) 전략 (적 0명 허공 시전 허용)
/// </summary>
public class AreaSelfTargetingStrategy : ITargetingStrategy
{
    public bool IsManualSelectionRequired => false;

    public bool HasValidTargets(PlayerBattleController controller, Card card)
    {
        return card != null && card.cardData != null;
    }

    public List<Character> CollectImmediateTargets(PlayerBattleController controller, Card card, Vector2Int casterPos)
    {
        if (controller == null || card == null || card.cardData == null)
        {
            return CardInfo.EmptyTargets;
        }

        if (GameMap.current != null && card.cardData.additionalEffectAreaList != null && card.cardData.additionalEffectAreaList.Count > 0)
        {
            TargetingStrategyHelper.GetCasterPose(controller, out _, out LookDirection facing);
            var aoeCoords = GameMap.current.CalculateCardAoECoords(casterPos, casterPos, card.cardData, facing);
            HashSet<Vector2Int> set = new HashSet<Vector2Int>(aoeCoords) { casterPos };
            return controller.CollectCharactersInCoords(set);
        }

        return controller.GetEnemiesInRange(card.cardData.targetMinDistance, card.cardData.targetMaxDistance);
    }

    public bool TryCollectTargetsOnTileClicked(PlayerBattleController controller, Card card, Tile targetTile, out List<Character> targets, out Vector2Int targetPos)
    {
        targetPos = targetTile != null ? targetTile.GetCoord() : Vector2Int.zero;
        targets = CollectImmediateTargets(controller, card, targetPos);
        return true;
    }
}

/// <summary>
/// 시전자가 바라보는 방향(facingDirection) 기준 즉시 발동 전략 (적 0명 허공 시전 허용)
/// </summary>
public class DirectionalTargetingStrategy : ITargetingStrategy
{
    public bool IsManualSelectionRequired => false;

    public bool HasValidTargets(PlayerBattleController controller, Card card)
    {
        return card != null && card.cardData != null;
    }

    public List<Character> CollectImmediateTargets(PlayerBattleController controller, Card card, Vector2Int casterPos)
    {
        if (controller == null || card == null || card.cardData == null)
        {
            return CardInfo.EmptyTargets;
        }

        if (GameMap.current != null)
        {
            TargetingStrategyHelper.GetCasterPose(controller, out _, out LookDirection facing);
            var aoeCoords = GameMap.current.CalculateCardAoECoords(casterPos, casterPos, card.cardData, facing);
            if (aoeCoords != null && aoeCoords.Count > 0)
            {
                return controller.CollectCharactersInCoords(new HashSet<Vector2Int>(aoeCoords));
            }
        }

        return CardInfo.EmptyTargets;
    }

    public bool TryCollectTargetsOnTileClicked(PlayerBattleController controller, Card card, Tile targetTile, out List<Character> targets, out Vector2Int targetPos)
    {
        TargetingStrategyHelper.GetCasterPose(controller, out Vector2Int casterPos, out _);
        targetPos = targetTile != null ? targetTile.GetCoord() : casterPos;
        targets = CollectImmediateTargets(controller, card, casterPos);
        return true;
    }
}

/// <summary>
/// 사거리 내 무작위 적 자동 추첨 발동 전략
/// </summary>
public class RandomEnemyTargetingStrategy : ITargetingStrategy
{
    public bool IsManualSelectionRequired => false;

    public bool HasValidTargets(PlayerBattleController controller, Card card)
    {
        if (card == null || card.cardData == null) return false;
        return controller.HasEnemiesInRange(card.cardData.targetMinDistance, card.cardData.targetMaxDistance);
    }

    public List<Character> CollectImmediateTargets(PlayerBattleController controller, Card card, Vector2Int casterPos)
    {
        List<Character> candidates = controller.GetEnemiesInRange(card.cardData.targetMinDistance, card.cardData.targetMaxDistance);
        return controller.PickRandomTargets(candidates, card.cardData.targetCount, card.cardData.allowDuplicateTarget);
    }

    public bool TryCollectTargetsOnTileClicked(PlayerBattleController controller, Card card, Tile targetTile, out List<Character> targets, out Vector2Int targetPos)
    {
        targetPos = targetTile != null ? targetTile.GetCoord() : Vector2Int.zero;
        targets = CollectImmediateTargets(controller, card, targetPos);
        return true;
    }
}

#endregion

#region Manual Selection Strategies (수동 지정형 전략)

/// <summary>
/// 사거리 내 단일 적 또는 파괴 가능 오브젝트 수동 지정 전략
/// </summary>
public class SingleEnemyTargetingStrategy : ITargetingStrategy
{
    public bool IsManualSelectionRequired => true;

    public bool HasValidTargets(PlayerBattleController controller, Card card)
    {
        if (card == null || card.cardData == null) return false;
        return controller.HasEnemiesInRange(card.cardData.targetMinDistance, card.cardData.targetMaxDistance)
            || controller.HasDestructiblesInRange(card.cardData.targetMinDistance, card.cardData.targetMaxDistance);
    }

    public List<Character> CollectImmediateTargets(PlayerBattleController controller, Card card, Vector2Int casterPos)
    {
        return CardInfo.EmptyTargets;
    }

    public bool TryCollectTargetsOnTileClicked(PlayerBattleController controller, Card card, Tile targetTile, out List<Character> targets, out Vector2Int targetPos)
    {
        targets = new List<Character>();
        targetPos = targetTile != null ? targetTile.GetCoord() : Vector2Int.zero;

        if (targetTile == null) return false;

        TargetingStrategyHelper.GetCasterPose(controller, out Vector2Int originCoord, out LookDirection facing);
        HashSet<Vector2Int> affectedCoords = TargetingStrategyHelper.CollectAoECoords(originCoord, targetPos, card, facing);

        targets = controller.CollectCharactersInCoords(affectedCoords);
        bool hasDestructible = targetTile.occupyingDestructible != null && !targetTile.occupyingDestructible.isDead;
        if (!hasDestructible && GameMap.current != null)
        {
            foreach (Vector2Int coord in affectedCoords)
            {
                Tile coordTile = GameMap.current.GetTile(coord);
                if (coordTile != null && coordTile.occupyingDestructible != null && !coordTile.occupyingDestructible.isDead)
                {
                    hasDestructible = true;
                    break;
                }
            }
        }

        if (targets.Count == 0 && !hasDestructible)
        {
            Debug.LogWarning("[SingleEnemyTargetingStrategy] 적 유닛 또는 파괴 가능한 오브젝트가 위치한 타일을 선택해야 합니다.");
            return false;
        }

        TargetingStrategyHelper.LookAtTargetTile(controller, targetTile);
        return true;
    }
}

/// <summary>
/// 사거리 내 바닥 타일 수동 지정 광역 전략 (빈 땅이어도 발동 가능)
/// </summary>
public class AreaGroundTargetingStrategy : ITargetingStrategy
{
    public bool IsManualSelectionRequired => true;

    public bool HasValidTargets(PlayerBattleController controller, Card card)
    {
        return true; // 바닥 지정형은 사거리 내 타일만 존재하면 시전 가능
    }

    public List<Character> CollectImmediateTargets(PlayerBattleController controller, Card card, Vector2Int casterPos)
    {
        return CardInfo.EmptyTargets;
    }

    public bool TryCollectTargetsOnTileClicked(PlayerBattleController controller, Card card, Tile targetTile, out List<Character> targets, out Vector2Int targetPos)
    {
        targets = new List<Character>();
        targetPos = targetTile != null ? targetTile.GetCoord() : Vector2Int.zero;

        if (targetTile == null) return false;

        TargetingStrategyHelper.GetCasterPose(controller, out Vector2Int originCoord, out LookDirection facing);
        HashSet<Vector2Int> affectedCoords = TargetingStrategyHelper.CollectAoECoords(originCoord, targetPos, card, facing);

        targets = controller.CollectCharactersInCoords(affectedCoords);
        TargetingStrategyHelper.LookAtTargetTile(controller, targetTile);
        return true;
    }
}

/// <summary>
/// 사거리 내 적 N명 순차 수동 지정 전략
/// </summary>
public class MultiEnemyTargetingStrategy : ITargetingStrategy
{
    public bool IsManualSelectionRequired => true;

    public bool HasValidTargets(PlayerBattleController controller, Card card)
    {
        if (card == null || card.cardData == null) return false;
        return controller.HasEnemiesInRange(card.cardData.targetMinDistance, card.cardData.targetMaxDistance)
            || controller.HasDestructiblesInRange(card.cardData.targetMinDistance, card.cardData.targetMaxDistance);
    }

    public List<Character> CollectImmediateTargets(PlayerBattleController controller, Card card, Vector2Int casterPos)
    {
        return CardInfo.EmptyTargets;
    }

    public bool TryCollectTargetsOnTileClicked(PlayerBattleController controller, Card card, Tile targetTile, out List<Character> targets, out Vector2Int targetPos)
    {
        targets = new List<Character>();
        targetPos = targetTile != null ? targetTile.GetCoord() : Vector2Int.zero;

        if (targetTile == null) return false;

        HashSet<Vector2Int> affectedCoords = new HashSet<Vector2Int> { targetPos };
        List<Character> foundTargets = controller.CollectCharactersInCoords(affectedCoords);
        Character enemyTarget = foundTargets.Find(t => t != null && !t.isDead && (controller.controlledCharacter == null || controller.controlledCharacter.IsHostileTo(t)));

        if (enemyTarget == null)
        {
            Debug.LogWarning("[MultiEnemyTargetingStrategy] 적대 유닛이 위치한 타일을 선택해야 합니다.");
            return false;
        }

        int requiredCount = Mathf.Max(1, card.cardData != null ? card.cardData.targetCount : 1);
        bool allowDuplicate = card.cardData != null && card.cardData.allowDuplicateTarget;

        if (!allowDuplicate && controller.MultiSelectedTargets.Contains(enemyTarget))
        {
            Debug.LogWarning("[MultiEnemyTargetingStrategy] 이미 선택된 대상입니다.");
            return false;
        }

        controller.MultiSelectedTargets.Add(enemyTarget);
        int totalLivingEnemies = controller.GetEnemiesInRange(card.cardData.targetMinDistance, card.cardData.targetMaxDistance).Count;
        int targetCap = allowDuplicate ? requiredCount : Mathf.Min(requiredCount, totalLivingEnemies);

        Debug.Log($"[MultiEnemyTargetingStrategy] 다중 대상 선택됨 ({controller.MultiSelectedTargets.Count}/{targetCap})");

        if (controller.MultiSelectedTargets.Count < targetCap)
        {
            return false; // 아직 선택할 대상이 남아있음
        }

        targets = new List<Character>(controller.MultiSelectedTargets);
        controller.MultiSelectedTargets.Clear();
        TargetingStrategyHelper.LookAtTargetTile(controller, targetTile);
        return true;
    }
}

#endregion
