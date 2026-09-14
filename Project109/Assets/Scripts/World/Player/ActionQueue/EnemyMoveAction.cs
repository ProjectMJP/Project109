using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 유닛이 턴 동안 격자 맵 상에서 비동기식으로 이동 애니메이션 및 경로 추적을 수행하는 배틀 액션입니다.
/// </summary>
public class EnemyMoveAction : BattleAction
{
    private static readonly WaitForSeconds _postMoveDelay = new WaitForSeconds(0.2f);

    public Tile TargetTile { get; private set; }

    public EnemyMoveAction(Character caster, Tile targetTile) : base(caster)
    {
        this.TargetTile = targetTile;
    }

    public override IEnumerator ExecuteRoutine()
    {
        if (caster == null || TargetTile == null || caster.characterMove == null) yield break;

        // 이미 같은 위치면 생략
        if (caster.characterMove.GetCurrentTile() == TargetTile) yield break;

        // 1. 캐릭터 상태 변경
        caster.currentState = CharacterState.Move;

        // 2. 경로 탐색 및 이동 실행
        Tile startTile = caster.characterMove.GetCurrentTile();
        GameMap gameMap = TargetTile.ownerMap ?? startTile?.ownerMap ?? GameMap.current;
        if (gameMap != null)
        {
            MoverCapability caps = caster.characterMove.capabilities;
            List<Tile> movePath = gameMap.FindPath(startTile, TargetTile, caps);

            if (movePath != null && movePath.Count > 0)
            {
                caster.curMoveCount--;
                caster.characterMove.MoveAlongPath(movePath, TargetTile);
                
                // 3. 이동 완료(캐릭터 상태가 Idle로 복구)될 때까지 대기
                // MoveAlongPath 내부 코루틴이나 연출 시간에 상응하여 대기
                while (caster.currentState == CharacterState.Move)
                {
                    yield return null;
                }
            }
        }

        // 이동 완료 후 짧은 대기 (자연스러운 딜레이)
        yield return _postMoveDelay;
        caster.currentState = CharacterState.Idle;
    }
}
