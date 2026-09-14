using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XInput;

public class PlayerMove
{
    private CharacterMove characterMove;

    public List<Tile> canMoveTiles;

    public MapManager battleMap;
    public RoutePathfinding routePathfinding;

    public PlayerMove(CharacterMove characterMove)
    {
        this.characterMove = characterMove;
        this.canMoveTiles = new List<Tile>();

        this.battleMap = RunManager.instance?.currentMap;

        if (this.battleMap != null)
        {
            this.routePathfinding = this.battleMap.routePathfinding;
        }

        // 입력 이벤트 바인딩 제거 (PlayerBattleController에서 중앙 관리)
    }

    public void CheckCanMoveTiles()
    {
        canMoveTiles = battleMap.CheckPlayerMoveTiles(characterMove.GetCurrentTile(), characterMove.character.curCharacterStat.maxTilesPerMove, characterMove);

        if (battleMap != null && battleMap.currentGameMap != null && battleMap.currentGameMap.GetMoveRangeIndicator() != null)
        {
            battleMap.currentGameMap.GetMoveRangeIndicator().gameObject.SetActive(true);
            battleMap.currentGameMap.GetMoveRangeIndicator().ShowWalkableTiles(canMoveTiles, battleMap.currentGameMap.GetTileMap());
        }
    }

    /// <summary>
    /// 이전에 검색하여 얻은 플레이어가 움직일 수 있는 타일 정보들을 초기화
    /// </summary>
    public void ClearCanMoveTiles()
    {
        for (int index = 0; index < canMoveTiles.Count; index++)
        {
            canMoveTiles[index].SetMoveIndicator(false);
        }
        canMoveTiles.Clear();

        if (battleMap != null && battleMap.currentGameMap != null && battleMap.currentGameMap.GetMoveRangeIndicator() != null)
        {
            battleMap.currentGameMap.GetMoveRangeIndicator().ClearAllTiles(battleMap.currentGameMap.GetTileMap());
            battleMap.currentGameMap.GetMoveRangeIndicator().gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 외부(PlayerBattleController)에서 이동 모드일 때 클릭된 타일을 전달받아 이동을 실행합니다.
    /// </summary>
    public void ExecuteMoveToTile(Tile targetTile)
    {
        // 캐릭터가 Idle 상태가 아닌 경우 무시
        if (characterMove.character.currentState != CharacterState.Idle)
        {
            return;
        }

        if (targetTile != null)
        {
            if (canMoveTiles.Contains(targetTile))
            {
                if (characterMove.character.curMoveCount <= 0)
                {
                    Debug.Log("이동 횟수가 부족하여 이동할 수 없습니다.");
                    return;
                }

                Debug.Log("이동 목표 타일: " + targetTile.GetCoordToString());

                // 경로 탐색 및 실제 이동 명령 (capabilities 적용)
                MoverCapability caps = characterMove != null ? characterMove.capabilities : MoverCapability.None;
                List<Tile> movePath = routePathfinding.TilePathfinding(characterMove.GetCurrentTile(), targetTile, battleMap.currentGameMap.GetTileMap(), caps);
                characterMove.MoveAlongPath(movePath, targetTile);
            }
        }
    }
}
