using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 중 플레이어 캐릭터의 이동 범위 계산, 비주얼 인디케이터 표시, 이동 명령 실행을 전담하는 클래스.
/// PlayerBattleController에 의해 소유 및 관리됩니다.
/// </summary>
public class PlayerBattleMove
{
    private readonly CharacterMove characterMove;
    public List<Tile> canMoveTiles { get; private set; }

    public PlayerBattleMove(CharacterMove characterMove)
    {
        this.characterMove = characterMove;
        this.canMoveTiles = new List<Tile>();
    }

    /// <summary>
    /// 현재 타일 기준으로 캐릭터의 이동 가능 범위 타일들을 계산하고 인디케이터를 활성화합니다.
    /// </summary>
    public void CheckCanMoveTiles()
    {
        if (characterMove == null) return;

        Tile currentTile = characterMove.GetCurrentTile();
        if (currentTile == null) return;

        GameMap gameMap = currentTile.ownerMap ?? GameMap.current;
        if (gameMap == null) return;

        int maxDistance = characterMove.character?.curCharacterStat?.maxTilesPerMove ?? 0;
        canMoveTiles = gameMap.GetReachableTiles(currentTile, maxDistance, characterMove);

        var indicator = gameMap.GetMoveRangeIndicator();
        if (indicator != null)
        {
            indicator.gameObject.SetActive(true);
            indicator.ShowWalkableTiles(canMoveTiles, gameMap.GetTileMap());
        }
    }

    /// <summary>
    /// 이전에 검색하여 얻은 플레이어가 움직일 수 있는 타일 정보들을 초기화합니다.
    /// </summary>
    public void ClearCanMoveTiles()
    {
        for (int index = 0; index < canMoveTiles.Count; index++)
        {
            if (canMoveTiles[index] != null)
            {
                canMoveTiles[index].SetMoveIndicator(false);
            }
        }
        canMoveTiles.Clear();

        Tile currentTile = characterMove?.GetCurrentTile();
        GameMap gameMap = currentTile?.ownerMap ?? GameMap.current;
        var indicator = gameMap?.GetMoveRangeIndicator();
        if (indicator != null)
        {
            indicator.ClearAllTiles(gameMap.GetTileMap());
            indicator.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 외부(PlayerBattleController)에서 이동 모드일 때 클릭된 타일을 전달받아 이동을 실행합니다.
    /// </summary>
    public void ExecuteMoveToTile(Tile targetTile)
    {
        if (characterMove == null || targetTile == null) return;

        // 캐릭터가 Idle 상태가 아닌 경우 무시
        if (characterMove.character.currentState != CharacterState.Idle)
        {
            return;
        }

        if (canMoveTiles.Contains(targetTile))
        {
            if (characterMove.character.curMoveCount <= 0)
            {
                Debug.LogWarning("[PlayerBattleMove] 이동 횟수가 부족하여 이동할 수 없습니다.");
                return;
            }

            Debug.Log("[PlayerBattleMove] 이동 목표 타일: " + targetTile.GetCoordToString());

            // 1. 전투 이동 횟수 차감 (턴제 규칙 적용)
            characterMove.character.curMoveCount--;

            // 2. 경로 탐색 및 실제 이동 명령
            Tile startTile = characterMove.GetCurrentTile();
            GameMap gameMap = targetTile.ownerMap ?? startTile?.ownerMap ?? GameMap.current;
            if (gameMap != null)
            {
                MoverCapability caps = characterMove.capabilities;
                List<Tile> movePath = gameMap.FindPath(startTile, targetTile, caps);
                if (movePath != null && movePath.Count > 0)
                {
                    characterMove.MoveAlongPath(movePath, targetTile);
                }
            }
        }
    }
}
