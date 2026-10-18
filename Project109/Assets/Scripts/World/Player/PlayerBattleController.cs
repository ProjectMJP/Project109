using EventStructs;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerControlState
{
    Normal,
    Move,
    TargetSelection
}

/// <summary>
/// 전투 중 플레이어 캐릭터의 턴을 제어하는 컨트롤러.
/// <summary>
/// 전투 중 플레이어 캐릭터의 턴을 제어하는 컨트롤러.
/// 전투 시작 ~ 전투 종료까지 유지된다.
/// </summary>
public class PlayerBattleController : ICharacterController, IOnBattleEnd, System.IDisposable
{
    private BattleUIPanel _battleUI;
    public BattleUIPanel battleUI => _battleUI;
    public BattleHandPanel handUI => _battleUI != null ? _battleUI.HandPanel : null;

    public void Dispose()
    {
        OnBattleEnd();

        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent -= HandleGlobalClick;
            PlayerInputController.instance.OnPointerHoverEvent -= HandlePointerHover;
            PlayerInputController.instance.OnCancelEvent -= CancelCurrentState;
        }
    }

    #region ICharacterController

    [SerializeField]
    private Character _controlledCharacter;
    public Character controlledCharacter => _controlledCharacter;

    #endregion

    // 현재 플레이어 조작 상태
    public PlayerControlState controlState { get; private set; } = PlayerControlState.Normal;
    private Card activeCardForTargeting;
    private Vector2Int _lastHoveredCoord = new Vector2Int(-999, -999);

    #region References

    private readonly Player player;

    private readonly PlayerBattleMove playerMove;

    #endregion

    #region Battle Deck (전투 중 카드 덱 관리)

    public BattleDeck battleDeck => player != null ? player.battleDeck : null;

    #endregion

    public PlayerBattleController(Player player)
    {
        this.player = player;
        this._controlledCharacter = player != null ? player.character : null;

        // PlayerBattleMove 초기화
        if (this._controlledCharacter != null)
        {
            CharacterMove charMove = this._controlledCharacter.characterMove;
            if (charMove != null)
            {
                playerMove = new PlayerBattleMove(charMove);
            }
            else
            {
                Debug.LogWarning("PlayerBattleController: CharacterMove 또는 InputController가 없습니다.");
            }
        }
    }

    public void OnBattleStart()
    {
        if (player != null)
        {
            player.InitBattleDeck();
        }

        if (UIManager.instance != null)
        {
            _battleUI = UIManager.instance.GetOrSpawnBattleUI();
            if (_battleUI != null)
            {
                _battleUI.BindPlayer(player, this);
                _battleUI.SwitchToHandMode();
            }
        }

        if (_controlledCharacter != null)
        {
            _controlledCharacter.eventBus.Add<IOnBattleEnd>(this);
        }
    }

    public void OnBattleEnd()
    {
        if (_controlledCharacter != null)
        {
            _controlledCharacter.eventBus.Remove<IOnBattleEnd>(this);
        }

        if (_battleUI != null)
        {
            _battleUI.Close();
            _battleUI = null;
        }
    }

    /// <summary>
    /// 이번 턴에만 임시로 적용되는 드로우 보너스/패널티 (버프, 유물 등이 설정 가능)
    /// </summary>
    public int bonusDrawThisTurn { get; set; } = 0;

    /// <summary>
    /// 이번 턴 드로우 보너스/패널티를 가산합니다.
    /// </summary>
    public void AddBonusDrawThisTurn(int count)
    {
        bonusDrawThisTurn += count;
    }

    /// <summary>
    /// 이번 턴에 드로우할 카드 장수를 계산하여 반환합니다.
    /// (PlayerStat의 BaseDrawCount 기본값 + 이번 턴 임시 보너스 합산)
    /// </summary>
    public int GetTurnDrawCount()
    {
        int baseCount = player?.playerStat != null ? player.playerStat.BaseDrawCount : 5;
        return Mathf.Max(0, baseCount + bonusDrawThisTurn);
    }

    public void OnTurnStart()
    {
        // 1. 이벤트 버스 — 등록된 IOnTurnStart들 호출 (버프, 유물 등에서 AddBonusDrawThisTurn 호출 가능)
        controlledCharacter.eventBus.Invoke<IOnTurnStart>(a => a.OnTurnStart());

        // 2. 카드 드로우 (BattleDeck.OnHandChanged를 통해 BattleHandPanel 자동 갱신)
        battleDeck.DrawCards(GetTurnDrawCount());

        // 3. 이번 턴 보정치 리셋 (다음 턴에는 기본값으로 자동 원복)
        bonusDrawThisTurn = 0;

        // 4. 전역 클릭 및 취소 이벤트 구독
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent += HandleGlobalClick;
            PlayerInputController.instance.OnPointerHoverEvent += HandlePointerHover;
            PlayerInputController.instance.OnCancelEvent += CancelCurrentState;
        }
        controlState = PlayerControlState.Normal;
        _lastHoveredCoord = new Vector2Int(-999, -999);
        if (_battleUI != null)
        {
            _battleUI.SwitchToHandMode();
            _battleUI.RefreshMoveStats();
            _battleUI.SetEndTurnInteractable(true);
        }
    }

    public void OnTurnEnd()
    {
        // 1. 이벤트 버스 — 등록된 IOnTurnEnd들 호출
        controlledCharacter.eventBus.Invoke<IOnTurnEnd>(a => a.OnTurnEnd());

        // 2. 손패 → 묘지 이동 (BattleDeck.OnHandChanged를 통해 BattleHandPanel 자동 갱신)
        battleDeck.DiscardHand();

        // 3. 이동 비활성화
        if (playerMove != null)
        {
            playerMove.ClearCanMoveTiles();
        }

        // 4. 이벤트 구독 해제
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent -= HandleGlobalClick;
            PlayerInputController.instance.OnPointerHoverEvent -= HandlePointerHover;
            PlayerInputController.instance.OnCancelEvent -= CancelCurrentState;
        }
        controlState = PlayerControlState.Normal;
        _lastHoveredCoord = new Vector2Int(-999, -999);
        bonusDrawThisTurn = 0;

        if (_battleUI != null)
        {
            _battleUI.SetEndTurnInteractable(false);
        }
    }

    /// <summary>
    /// 플레이어의 턴 종료를 요청합니다.
    /// </summary>
    public void RequestEndTurn()
    {
        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            if (ReferenceEquals(RunManager.instance.battleManager.currentTurnController, this))
            {
                RunManager.instance.battleManager.EndTurn();
            }
        }
    }

    #region Card Operations

    /// <summary>
    /// 카드를 실제로 사용하는 파이프라인
    /// </summary>
    public void UseCard(Card card, List<Character> targets, Vector2Int targetPosition)
    {
        // 즉시 실행하지 않고 액션 큐에 위임
        CardPlayAction playAction = new CardPlayAction(controlledCharacter, card, targets, targetPosition);
        ActionQueueManager.Instance.EnqueueAction(playAction);
    }

    /// <summary>
    /// 카드 시전 시도 (지정형 카드는 타겟팅 모드로 진입)
    /// </summary>
    public void TryUseCard(Card card)
    {
        if (card == null) return;

        // 액션 큐 실행 중 조작 방지 잠금
        if (ActionQueueManager.Instance.IsBusy)
        {
            Debug.LogWarning("[PlayerBattleController] 캐릭터가 행동 중입니다. 카드 사용이 무시됩니다.");
            return;
        }

        // 실시간 다중 비용 및 시전 가능 조건 검사
        if (!card.CanPlay(controlledCharacter))
        {
            Debug.LogWarning($"[PlayerBattleController] 시전 요구조건이 충족되지 않아 '{card.cardName}' 카드를 사용할 수 없습니다.");
            return;
        }

        // 사용 시도 이벤트 발동 (IOnTryUseCard)
        CardInfo tryInfo = new CardInfo(controlledCharacter, card, CardFlag.Normal);
        controlledCharacter.eventBus.Invoke<IOnTryUseCard>(c => c.OnTryUseCard(tryInfo));

        string typeLower = card.cardData?.targetType?.ToLower() ?? "";
        if (typeLower == "target" || typeLower == "area" || typeLower == "tile")
        {
            controlState = PlayerControlState.TargetSelection;
            activeCardForTargeting = card;

            if (UIManager.instance != null)
            {
                UIManager.instance.UpdateEffectAreaUI(card);
            }

            Debug.Log($"[TargetSelection] {card.cardData?.cardName} 카드의 대상을 지정해 주세요.");
        }
        else
        {
            // 즉시 시전 카드 (Self, All 등)
            Vector2Int casterPos = Vector2Int.zero;

            if (controlledCharacter.characterMove != null && controlledCharacter.characterMove.GetCurrentTile() != null)
            {
                casterPos = controlledCharacter.characterMove.GetCurrentTile().GetCoord();
            }

            UseCard(card, CardInfo.EmptyTargets, casterPos);
        }
    }

    /// <summary>
    /// 현재 조작 상태(이동 모드 또는 타겟 모드) 취소
    /// </summary>
    public void CancelCurrentState()
    {
        if (controlState == PlayerControlState.Normal) return;

        Debug.Log($"[PlayerBattleController] 조작 취소. 이전 상태: {controlState}");

        if (controlState == PlayerControlState.TargetSelection)
        {
            activeCardForTargeting = null;
            _lastHoveredCoord = new Vector2Int(-999, -999);
            if (UIManager.instance != null)
            {
                UIManager.instance.ClearEffectAreaTiles();
            }
            else if (GameMap.current != null)
            {
                GameMap.current.ClearCardRange();
            }
        }
        else if (controlState == PlayerControlState.Move)
        {
            playerMove?.ClearCanMoveTiles();
        }

        controlState = PlayerControlState.Normal;
        if (_battleUI != null && _battleUI.currentMode != BattleUIMode.Hand)
        {
            _battleUI.SwitchToHandMode();
        }
    }

    /// <summary>
    /// 이동/타겟팅 모드를 활성화하기 위한 상태 전이 메서드
    /// </summary>
    public void EnablePlayerMove()
    {
        if (playerMove != null)
        {
            playerMove.CheckCanMoveTiles();
            controlState = PlayerControlState.Move;

            if (_battleUI != null && _battleUI.currentMode != BattleUIMode.Move)
            {
                _battleUI.SwitchToMoveMode();
            }
        }
    }

    /// <summary>
    /// 화면 스크린 좌표로부터 해당 위치의 인게임 타일을 감지합니다.
    /// </summary>
    private Tile GetTileFromScreenPosition(Vector2 pos)
    {
        if (Camera.main == null) return null;

        Ray ray = Camera.main.ScreenPointToRay(pos);
        Tile targetTile = null;

        // 1. Tile 레이어 우선 레이캐스트
        int tileMask = LayerMask.GetMask("Tile");
        if (tileMask != 0 && Physics.Raycast(ray, out RaycastHit tileHit, 10000.0f, tileMask))
        {
            targetTile = tileHit.collider.GetComponentInParent<Tile>();
        }

        // 2. Tile 레이어 히트 실패 시 일반 레이캐스트
        if (targetTile == null && Physics.Raycast(ray, out RaycastHit generalHit, 10000.0f))
        {
            targetTile = generalHit.collider.GetComponentInParent<Tile>();
            if (targetTile == null && GameMap.current != null)
            {
                targetTile = GameMap.current.GetTileAtWorldPosition(generalHit.point);
            }
        }

        // 3. 지면 평면(Y=0)과의 교차점 탐색
        if (targetTile == null)
        {
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                if (GameMap.current != null)
                {
                    targetTile = GameMap.current.GetTileAtWorldPosition(ray.GetPoint(enter));
                }
            }
        }

        return targetTile;
    }

    /// <summary>
    /// 타겟팅 모드 중 마우스 커서 이동에 따라 실시간으로 폭발 범위(AoE) 타일을 갱신합니다.
    /// </summary>
    private void HandlePointerHover(Vector2 screenPos)
    {
        if (controlState != PlayerControlState.TargetSelection || activeCardForTargeting == null)
        {
            return;
        }

        // UI 위를 가리키고 있을 경우 월드 호버 갱신 무시
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Tile hoveredTile = GetTileFromScreenPosition(screenPos);
        Vector2Int hoveredCoord = hoveredTile != null ? hoveredTile.GetCoord() : new Vector2Int(-999, -999);

        // 동일한 타일 위라면 중복 렌더링 스킵
        if (hoveredCoord == _lastHoveredCoord)
        {
            return;
        }
        _lastHoveredCoord = hoveredCoord;

        Tile playerTile = controlledCharacter != null && controlledCharacter.characterMove != null 
            ? controlledCharacter.characterMove.GetCurrentTile() 
            : null;
        Vector2Int playerCoord = playerTile != null ? playerTile.GetCoord() : Vector2Int.zero;

        if (hoveredTile != null && playerTile != null && GameMap.current != null)
        {
            int distance = Tile.GetDistance(playerTile, hoveredTile);
            int minDistance = activeCardForTargeting.cardData.targetMinDistance;
            int maxDistance = activeCardForTargeting.cardData.targetMaxDistance;

            if (distance >= minDistance && distance <= maxDistance)
            {
                // 유효 사거리 내 타일이면 사거리(R) 유지 + 해당 타일 중심 폭발 범위(G) 실시간 표시
                GameMap.current.ShowCardAoE(playerCoord, hoveredCoord, activeCardForTargeting.cardData);
                return;
            }
        }

        // 사거리 밖이거나 타일이 없으면 사거리만 유지 표시
        if (GameMap.current != null)
        {
            GameMap.current.ShowCardRange(playerCoord, activeCardForTargeting.cardData);
        }
    }

    /// <summary>
    /// 지정된 타일 좌표 목록에 위치한 모든 적 캐릭터를 수집합니다.
    /// </summary>
    private List<Character> CollectCharactersInCoords(HashSet<Vector2Int> targetCoords)
    {
        List<Character> foundCharacters = new List<Character>();
        if (targetCoords == null || targetCoords.Count == 0) return foundCharacters;

        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            var combatants = RunManager.instance.battleManager.GetAllCombatants();
            for (int i = 0; i < combatants.Count; i++)
            {
                var combatant = combatants[i];
                if (combatant == null || combatant.controlledCharacter == null) continue;

                // 시전자 자신은 타깃에서 제외
                if (ReferenceEquals(combatant.controlledCharacter, controlledCharacter)) continue;

                var cMove = combatant.controlledCharacter.characterMove;
                if (cMove != null && cMove.GetCurrentTile() != null)
                {
                    if (targetCoords.Contains(cMove.GetCurrentTile().GetCoord()))
                    {
                        foundCharacters.Add(combatant.controlledCharacter);
                    }
                }
            }
        }
        return foundCharacters;
    }

    private void HandleGlobalClick(Vector2 pos)
    {
        // UI 요소 클릭 시 월드 조작 무시
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (controlledCharacter == null || controlledCharacter.currentState != CharacterState.Idle || ActionQueueManager.Instance.IsBusy)
        {
            return;
        }

        Tile targetTile = GetTileFromScreenPosition(pos);
        string hitObjectName = targetTile != null ? targetTile.name : "None";
        Vector3 hitPoint = targetTile != null ? targetTile.transform.position : Vector3.zero;

        // 상세 디버그 로그 출력
        Tile playerTile = controlledCharacter.characterMove?.GetCurrentTile();
        Vector2Int playerCoord = playerTile != null ? playerTile.GetCoord() : new Vector2Int(-1, -1);
        Vector2Int targetCoord = targetTile != null ? targetTile.GetCoord() : new Vector2Int(-1, -1);
        Vector3 playerWorldPos = controlledCharacter.transform.position;
        Vector3 targetWorldPos = targetTile != null ? targetTile.transform.position : Vector3.zero;

        Debug.Log($"[PlayerBattleController] 화면 클릭 감지!\n" +
                  $"▶ 스크린 좌표: ({pos.x:F1}, {pos.y:F1})\n" +
                  $"▶ 히트 오브젝트: '{hitObjectName}', 히트 월드 좌표: ({hitPoint.x:F2}, {hitPoint.y:F2}, {hitPoint.z:F2})\n" +
                  $"▶ 플레이어 현재 위치: {playerCoord} ({playerWorldPos.x:F2}, {playerWorldPos.y:F2}, {playerWorldPos.z:F2})\n" +
                  $"▶ 클릭된 대상 타일: {targetCoord} ({targetWorldPos.x:F2}, {targetWorldPos.y:F2}, {targetWorldPos.z:F2})\n" +
                  $"▶ 현재 조작 상태: {controlState}");

        if (targetTile != null)
        {
            switch (controlState)
            {
                case PlayerControlState.Normal:
                    // 일반 모드: 클릭 시 해당 타일 방향으로 회전
                    Vector3 diff = targetTile.transform.position - controlledCharacter.transform.position;
                    LookDirection targetDir = CharacterMove.GetDirectionFromVector(diff);
                    controlledCharacter.characterMove?.LookAtTile(targetTile);
                    Debug.Log($"[PlayerBattleController] 바라보기 회전: {targetDir} (diff: ({diff.x:F2}, {diff.z:F2}))");
                    break;

                    case PlayerControlState.Move:
                        // 이동 모드: playerMove에게 이동 지시
                        if (playerMove != null)
                        {
                            playerMove.ExecuteMoveToTile(targetTile);
                            playerMove.ClearCanMoveTiles();
                        }
                        controlState = PlayerControlState.Normal;
                        if (_battleUI != null)
                        {
                            _battleUI.SwitchToHandMode();
                        }
                        break;

                    case PlayerControlState.TargetSelection:
                        // 타겟팅 모드: 사거리 유효성 검사 후 최종 시전
                        if (activeCardForTargeting != null)
                        {
                            Tile currentTile = controlledCharacter.characterMove?.GetCurrentTile();
                            if (currentTile != null)
                            {
                                int distance = Tile.GetDistance(currentTile, targetTile);
                                int minDistance = activeCardForTargeting.cardData.targetMinDistance;
                                int maxDistance = activeCardForTargeting.cardData.targetMaxDistance;

                                if (distance >= minDistance && distance <= maxDistance)
                                {
                                    Vector2Int targetPos = targetTile.GetCoord();

                                    // 폭발 범위(AoE) 좌표 계산 및 대상 캐릭터 수집
                                    HashSet<Vector2Int> affectedCoords = new HashSet<Vector2Int>();
                                    affectedCoords.Add(targetPos);

                                    if (GameMap.current != null)
                                    {
                                        var aoeList = GameMap.current.CalculateCardAoECoords(targetPos, activeCardForTargeting.cardData);
                                        for (int i = 0; i < aoeList.Count; i++)
                                        {
                                            affectedCoords.Add(aoeList[i]);
                                        }
                                    }

                                    List<Character> targets = CollectCharactersInCoords(affectedCoords);

                                    UseCard(activeCardForTargeting, targets, targetPos);

                                    activeCardForTargeting = null;
                                    _lastHoveredCoord = new Vector2Int(-999, -999);

                                    if (UIManager.instance != null)
                                    {
                                        UIManager.instance.ClearEffectAreaTiles();
                                    }
                                    else if (GameMap.current != null)
                                    {
                                        GameMap.current.ClearCardRange();
                                    }
                                    controlState = PlayerControlState.Normal;
                                }
                                else
                                {
                                    Debug.LogWarning("사거리를 벗어난 타겟 타일입니다.");
                                }
                            }
                        }
                        break;
                }
        }
    }

    #endregion
}

