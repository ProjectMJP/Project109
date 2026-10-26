using System.Collections.Generic;
using CardTypes;
using UnityEngine;

/// <summary>
/// 카드 시전 시 타깃팅 방식(단일 적, 지면 광역, 자신, 무작위 등)에 따른 검증 및 수집을 전담하는 전략 인터페이스입니다.
/// </summary>
public interface ITargetingStrategy
{
    /// <summary>
    /// 수동 타깃팅 모드(타일 클릭 대기)가 필요한 전략인지 여부. false인 경우 즉시 시전됩니다.
    /// </summary>
    bool IsManualSelectionRequired { get; }

    /// <summary>
    /// 현재 사거리 및 전장 상태에서 유효한 대상이 존재하는지 사전에 검증합니다.
    /// </summary>
    bool HasValidTargets(PlayerBattleController controller, Card card);

    /// <summary>
    /// 즉시 시전형(Self, AllEnemies, AreaSelf, Random 등) 카드의 대상을 즉시 수집합니다.
    /// </summary>
    List<Character> CollectImmediateTargets(PlayerBattleController controller, Card card, Vector2Int casterPos);

    /// <summary>
    /// 수동 지정형(SingleEnemy, AreaGround, MultiEnemy 등) 카드의 타일 클릭 시 유효성을 판별하고 타깃을 수집합니다.
    /// 시전 요건이 충족되면 true를 반환하고, 다중 타깃 누적 중이거나 무효 클릭인 경우 false를 반환합니다.
    /// </summary>
    bool TryCollectTargetsOnTileClicked(PlayerBattleController controller, Card card, Tile targetTile, out List<Character> targets, out Vector2Int targetPos);
}
