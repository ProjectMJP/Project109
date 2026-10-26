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
        GameMap gameMap = null;
        if (TargetTile != null && TargetTile.ownerMap != null) gameMap = TargetTile.ownerMap;
        else if (startTile != null && startTile.ownerMap != null) gameMap = startTile.ownerMap;
        else gameMap = GameMap.current;
        if (gameMap != null)
        {
            MoverCapability caps = caster.characterMove.capabilities;
            List<Tile> movePath = gameMap.FindPath(startTile, TargetTile, caps);

            if (movePath != null && movePath.Count > 0)
            {
                caster.curMoveCount--;
                bool isMoveCompleted = false;

                caster.characterMove.MoveAlongPath(movePath, TargetTile, onComplete: () =>
                {
                    isMoveCompleted = true;
                });
                
                // 3. 이동 완료 콜백 대기 (소프트락 방지 최대 3초 타임아웃 가드 포함)
                float elapsed = 0f;
                while (!isMoveCompleted && elapsed < 3.0f && caster != null && caster.currentState != CharacterState.Die)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // 이동 완료 후 짧은 대기 (자연스러운 딜레이)
        yield return _postMoveDelay;
        if (caster != null && caster.currentState != CharacterState.Die)
        {
            caster.currentState = CharacterState.Idle;
        }
    }

    public override void OnCanceled()
    {
        base.OnCanceled();
        if (caster != null)
        {
            if (caster.characterMove != null)
            {
                caster.characterMove.StopMoving();
            }
            if (caster.currentState != CharacterState.Die)
            {
                caster.currentState = CharacterState.Idle;
            }
        }
    }
}
