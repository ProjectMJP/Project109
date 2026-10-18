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
            PlayerInputController.instance.OnCancelEvent += CancelCurrentState;
        }
        controlState = PlayerControlState.Normal;
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
            PlayerInputController.instance.OnCancelEvent -= CancelCurrentState;
        }
        controlState = PlayerControlState.Normal;
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
            if (UIManager.instance != null)
            {
                UIManager.instance.ClearEffectAreaTiles();
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

        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(pos);
        Tile targetTile = null;
        string hitObjectName = "None";
        Vector3 hitPoint = Vector3.zero;

        // 1. Tile 레이어 우선 레이캐스트
        int tileMask = LayerMask.GetMask("Tile");
        if (tileMask != 0 && Physics.Raycast(ray, out RaycastHit tileHit, 10000.0f, tileMask))
        {
            hitObjectName = tileHit.collider.gameObject.name;
            hitPoint = tileHit.point;
            targetTile = tileHit.collider.GetComponentInParent<Tile>();
        }

        // 2. Tile 레이어 히트 실패 시, 전체 레이캐스트 수행 후 히트 지점 월드 좌표로 가장 가까운 타일 탐색
        if (targetTile == null && Physics.Raycast(ray, out RaycastHit generalHit, 10000.0f))
        {
            hitObjectName = generalHit.collider.gameObject.name;
            hitPoint = generalHit.point;
            targetTile = generalHit.collider.GetComponentInParent<Tile>();

            if (targetTile == null && GameMap.current != null)
            {
                targetTile = GameMap.current.GetTileAtWorldPosition(hitPoint);
            }
        }

        // 3. 레이캐스트가 아무 콜라이더에도 닿지 않았을 경우, 지면 평면(Y=0)과의 교차점으로 타일 탐색
        if (targetTile == null)
        {
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                hitPoint = ray.GetPoint(enter);
                hitObjectName = "GroundPlane(Y=0)";
                if (GameMap.current != null)
                {
                    targetTile = GameMap.current.GetTileAtWorldPosition(hitPoint);
                }
            }
        }

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
                                    List<Character> targets = new();
                                    Vector2Int targetPos = targetTile.GetCoord();

                                    UseCard(activeCardForTargeting, targets, targetPos);

                                    activeCardForTargeting = null;
                                    if (UIManager.instance != null)
                                    {
                                        UIManager.instance.ClearEffectAreaTiles();
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

