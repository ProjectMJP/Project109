using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 비 전투(탐색/이벤트) 상황에서 플레이어 캐릭터의 움직임과 상호작용을 제어하는 컨트롤러.
/// </summary>
public class PlayerExploreController : ICharacterController, System.IDisposable
{
    public void Dispose()
    {
        Deactivate();
    }

    private readonly Player player;
    private readonly CharacterMove characterMove;

    public Character controlledCharacter => player?.character;

    public PlayerExploreController(Player player)
    {
        this.player = player;
        this.characterMove = player?.character?.characterMove;
    }

    public void OnTurnStart() { }
    public void OnTurnEnd() { }

    /// <summary>
    /// 탐색 컨트롤러가 활성화될 때 입력을 바인딩합니다.
    /// </summary>
    public void Activate()
    {
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent += HandleExploreClick;
        }
    }

    /// <summary>
    /// 탐색 컨트롤러가 비활성화될 때 입력을 해제합니다.
    /// </summary>
    public void Deactivate()
    {
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent -= HandleExploreClick;
        }
    }

    private void HandleExploreClick(Vector2 screenPos)
    {
        // 마우스 포인터가 UI 위에 있는 경우 인풋 관통 방지를 위해 조작 무시
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        // 현재 전투 맵 상태가 Battle인 경우 탐색 조작은 동작하지 않음
        if (RunManager.instance == null || RunManager.instance.currentMap == null || RunManager.instance.currentMap.currentMapState == MapState.Battle) return;

        // 플레이어 캐릭터 상태가 Idle 또는 Move(이동 중 경로 변경 허용)가 아니면 조작 무시
        if (controlledCharacter == null || (controlledCharacter.currentState != CharacterState.Idle && controlledCharacter.currentState != CharacterState.Move)) return;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        // 1차 레이캐스트: 캐릭터 및 상호작용 가능한 대상(Player, Enemy, NPC) 우선 스캔
        int interactionMask = LayerMask.GetMask("Player", "Enemy", "NPC");
        if (Physics.Raycast(ray, out RaycastHit interactHit, 10000.0f, interactionMask))
        {
            IInteractable interactable = interactHit.collider.GetComponent<IInteractable>();
            if (interactable == null)
            {
                interactable = interactHit.collider.transform.root.GetComponent<IInteractable>();
            }

            if (interactable != null)
            {
                HandleInteractionClick(interactable);
                return;
            }
        }

        // 2차 레이캐스트: 1차 검출 실패 시, 오직 바닥 타일(Tile) 레이어만 필터링하여 스캔
        // 이를 통해 벽(Map 레이어)이나 기타 콜라이더를 관통하여 순수한 바닥 타일만 피격합니다.
        int tileMask = LayerMask.GetMask("Tile");
        if (Physics.Raycast(ray, out RaycastHit tileHit, 10000.0f, tileMask))
        {
            Tile targetTile = tileHit.collider.GetComponent<Tile>();
            if (targetTile == null)
            {
                targetTile = tileHit.collider.transform.root.GetComponentInChildren<Tile>();
            }

            if (targetTile != null)
            {
                HandleTileMovementClick(targetTile);
            }
        }
    }

    /// <summary>
    /// 일반 타일 클릭 시 제한 없는 이동을 처리합니다.
    /// </summary>
    private void HandleTileMovementClick(Tile targetTile)
    {
        if (targetTile == null || characterMove == null) return;

        // 이 타일에 캐릭터가 진입하여 대기할 수 있는지 체크 (벽 관통 유무에 따른 차등 판단)
        if (!targetTile.CanEnter(characterMove)) return;

        Tile startTile = characterMove.GetCurrentTile();
        if (startTile == null || startTile == targetTile) return;

        var tileMap = RunManager.instance.currentMap?.currentGameMap?.GetTileMap();
        if (tileMap == null) return;

        // 자유 길찾기 수행
        List<Tile> movePath = FindPathFree(startTile, targetTile, tileMap);
        if (movePath != null && movePath.Count > 0)
        {
            characterMove.MoveAlongPath(movePath, targetTile);
        }
    }

    /// <summary>
    /// 상호작용 대상 클릭 시 대상의 인접 타일로 이동 후 상호작용을 실행합니다.
    /// </summary>
    private void HandleInteractionClick(IInteractable interactable)
    {
        if (interactable == null || characterMove == null) return;

        Component npcComp = interactable as Component;
        if (npcComp == null)
        {
            // 인터페이스 단독 구현체인 경우 원거리 상호작용을 폴백으로 즉시 실행
            interactable.OnInteract();
            return;
        }

        Tile startTile = characterMove.GetCurrentTile();
        if (startTile == null) return;

        var tileMap = RunManager.instance.currentMap?.currentGameMap?.GetTileMap();
        if (tileMap == null) return;

        // 상호작용 대상과 가장 가까운 타일을 NPC가 위치한 타일로 간주
        Tile npcTile = FindTileNearPosition(npcComp.transform.position, tileMap);
        if (npcTile == null)
        {
            interactable.OnInteract();
            return;
        }

        // 이미 플레이어가 NPC에 인접해 있다면 제자리 회전 후 즉시 상호작용
        if (IsAdjacent(startTile, npcTile))
        {
            characterMove.LookAtTile(npcTile);
            interactable.OnInteract();
            return;
        }

        // NPC 타일 주변 4방향 인접 타일 탐색
        List<Tile> adjacentWalkableTiles = GetAdjacentWalkableTiles(npcTile, tileMap);
        if (adjacentWalkableTiles.Count == 0)
        {
            Debug.LogWarning("NPC 주변에 서 있을 수 있는 빈 타일이 없습니다.");
            return;
        }

        // 가장 가까운 인접 타일과 경로 탐색
        Tile bestTargetTile = null;
        List<Tile> shortestPath = null;

        foreach (var adjTile in adjacentWalkableTiles)
        {
            List<Tile> path = FindPathFree(startTile, adjTile, tileMap);
            if (path != null && path.Count > 0)
            {
                if (shortestPath == null || path.Count < shortestPath.Count)
                {
                    shortestPath = path;
                    bestTargetTile = adjTile;
                }
            }
        }

        if (shortestPath != null && bestTargetTile != null)
        {
            // 이동 완료 후 NPC를 바라보고 상호작용 트리거
            characterMove.MoveAlongPath(shortestPath, bestTargetTile, () =>
            {
                if (characterMove != null)
                {
                    characterMove.LookAtTile(npcTile);
                }

                if (interactable.RequiresCameraFocus && CameraController.instance != null)
                {
                    CameraController.instance.CameraFocusToTarget(npcComp.transform.position);
                }
                interactable.OnInteract();
            });
        }
        else
        {
            // 경로가 완전히 막힌(Unreachable) 경우, 멀리서 대화가 가능하도록 원거리 즉시 상호작용 폴백 실행
            characterMove.LookAtTile(npcTile);
            if (interactable.RequiresCameraFocus && CameraController.instance != null)
            {
                CameraController.instance.CameraFocusToTarget(npcComp.transform.position);
            }
            interactable.OnInteract();
        }
    }

    private List<Tile> FindPathFree(Tile startTile, Tile targetTile, List<List<Tile>> tileMap)
    {
        if (startTile == null || targetTile == null || tileMap == null) return null;

        RoutePathfinding pathfinder = (RunManager.instance != null && RunManager.instance.currentMap != null) ? RunManager.instance.currentMap.routePathfinding : null;
        if (pathfinder != null)
        {
            MoverCapability caps = characterMove != null ? characterMove.capabilities : MoverCapability.None;
            return pathfinder.TilePathfinding(startTile, targetTile, tileMap, caps);
        }

        return null;
    }

    private Tile FindTileNearPosition(Vector3 position, List<List<Tile>> tileMap)
    {
        Tile closestTile = null;
        float minDistance = float.MaxValue;

        foreach (var col in tileMap)
        {
            foreach (var tile in col)
            {
                float dist = Vector3.Distance(tile.transform.position, position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestTile = tile;
                }
            }
        }
        return closestTile;
    }

    private List<Tile> GetAdjacentWalkableTiles(Tile centerTile, List<List<Tile>> tileMap)
    {
        List<Tile> adjacentTiles = new List<Tile>();
        if (centerTile == null || tileMap == null) return adjacentTiles;

        int col = centerTile.GetCoord().x;
        int row = centerTile.GetCoord().y;

        int[] dirX = { 0, 0, 1, -1 };
        int[] dirY = { 1, -1, 0, 0 };

        for (int i = 0; i < 4; i++)
        {
            int x = col + dirX[i];
            int y = row + dirY[i];

            if (x >= 0 && x < tileMap.Count && y >= 0 && y < tileMap[0].Count)
            {
                Tile tile = tileMap[x][y];
                if (tile != null && (tile.tileState == TileState.Empty || tile.tileState == TileState.Trap))
                {
                    adjacentTiles.Add(tile);
                }
            }
        }
        return adjacentTiles;
    }

    private bool IsAdjacent(Tile t1, Tile t2)
    {
        if (t1 == null || t2 == null) return false;
        int dist = Mathf.Abs(t1.GetCoord().x - t2.GetCoord().x) +
                   Mathf.Abs(t1.GetCoord().y - t2.GetCoord().y);
        return dist == 1;
    }
}
