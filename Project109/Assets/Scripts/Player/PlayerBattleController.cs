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
/// 전투 시작 ~ 전투 종료까지 유지된다.
/// </summary>
public class PlayerBattleController : ICharacterController, System.IDisposable
{
    public void Dispose()
    {
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent -= HandleGlobalClick;
            PlayerInputController.instance.OnCancelEvent -= CancelCurrentState;
        }
        if (battleDeck != null)
        {
            battleDeck.OnHandChanged -= UpdateHandUI;
        }

        if (battleHandPanel != null)
        {
            if (UIManager.instance != null)
            {
                UIManager.instance.RemoveActiveUIFromStack(battleHandPanel.gameObject);
            }
            UnityEngine.Object.Destroy(battleHandPanel.gameObject);
            battleHandPanel = null;
        }
    }

    public BattleHandPanel battleHandPanel { get; private set; }

    private BattleHandPanel GetOrSpawnBattleHand()
    {
        if (battleHandPanel != null) return battleHandPanel;

        if (UIManager.instance != null)
        {
            GameObject obj = UIManager.instance.OpenUI(UIConstants.PANEL_BATTLE_HAND, UILayerType.Normal, true);
            if (obj != null)
            {
                battleHandPanel = obj.GetComponent<BattleHandPanel>();
            }
        }
        return battleHandPanel;
    }

    private void UpdateHandUI()
    {
        if (battleHandPanel != null)
        {
            battleHandPanel.RefreshHand(battleDeck.hand);
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

    [SerializeField]
    private SOCharacterStatData playerStatDataSO;

    private readonly PlayerMove playerMove;

    #endregion

    #region Battle Deck (전투 중 카드 덱 관리)

    public BattleDeck battleDeck { get; private set; }

    [SerializeField] private int drawCount = 5;

    #endregion

    public PlayerBattleController(Player player)
    {
        this.player = player;
        this._controlledCharacter = player?.character;

        this.battleDeck = new BattleDeck(this._controlledCharacter);

        // PlayerMove 초기화
        if (this._controlledCharacter != null)
        {
            CharacterMove charMove = this._controlledCharacter.characterMove;
            if (charMove != null)
            {
                playerMove = new PlayerMove(charMove);
            }
            else
            {
                Debug.LogWarning("PlayerBattleController: CharacterMove 또는 InputController가 없습니다.");
            }
        }
    }

    public void OnBattleStart()
    {
        // BattleDeck.InitDeck 내부에서 전투 전용으로 1회 복제(Clone)하여 drawPile 구성
        battleDeck.InitDeck(player.masterDeck);

        var handUI = GetOrSpawnBattleHand();
        if (handUI != null)
        {
            battleDeck.OnHandChanged -= UpdateHandUI;
            battleDeck.OnHandChanged += UpdateHandUI;
            handUI.RefreshHand(battleDeck.hand);
        }
    }

    public void OnTurnStart()
    {
        // 1. 이벤트 버스 — 등록된 IOnTurnStart들 호출 (버프, 유물 등)
        controlledCharacter.eventBus.Invoke<IOnTurnStart>(a => a.OnTurnStart());

        // 2. 카드 드로우
        battleDeck.DrawCards(drawCount);

        // 3. UI 갱신
        var handUI = GetOrSpawnBattleHand();
        if (handUI != null)
        {
            handUI.gameObject.SetActive(true);
            handUI.RefreshHand(battleDeck.hand);
        }

        // 4. 이동 활성화 (이동 상태는 필요할 때 켬)

        // 5. 전역 클릭 및 취소 이벤트 구독
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent += HandleGlobalClick;
            PlayerInputController.instance.OnCancelEvent += CancelCurrentState;
        }
        controlState = PlayerControlState.Normal;
    }

    public void OnTurnEnd()
    {
        // 1. 이벤트 버스 — 등록된 IOnTurnEnd들 호출
        controlledCharacter.eventBus.Invoke<IOnTurnEnd>(a => a.OnTurnEnd());

        // 2. 손패 → 묘지 이동
        battleDeck.DiscardHand();

        // 3. 이동 비활성화
        playerMove?.ClearCanMoveTiles();

        // 4. UI 정리
        if (battleHandPanel != null)
        {
            battleHandPanel.RefreshHand(battleDeck.hand);
        }

        // 5. 이벤트 구독 해제
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent -= HandleGlobalClick;
            PlayerInputController.instance.OnCancelEvent -= CancelCurrentState;
        }
        controlState = PlayerControlState.Normal;
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
        CardInfo tryInfo = new CardInfo(controlledCharacter, CardInfo.EmptyTargets, Vector2Int.zero, card, CardFlag.Normal);
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
        }
    }

    private void HandleGlobalClick(Vector2 pos)
    {
        if (controlledCharacter == null || controlledCharacter.currentState != CharacterState.Idle || ActionQueueManager.Instance.IsBusy)
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(pos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (!hit.transform.TryGetComponent<Tile>(out var targetTile))
            {
                targetTile = hit.transform.root.GetComponentInChildren<Tile>();
            }

            if (targetTile != null)
            {
                switch (controlState)
                {
                    case PlayerControlState.Normal:
                        // 일반 모드: 클릭 시 해당 타일 방향으로 회전
                        controlledCharacter.characterMove?.LookAtTile(targetTile);
                        Debug.Log("바라보기 회전: " + controlledCharacter.characterMove?.facingDirection);
                        break;

                    case PlayerControlState.Move:
                        // 이동 모드: playerMove에게 이동 지시
                        if (playerMove != null)
                        {
                            playerMove.ExecuteMoveToTile(targetTile);
                            playerMove.ClearCanMoveTiles();
                        }
                        controlState = PlayerControlState.Normal;
                        break;

                    case PlayerControlState.TargetSelection:
                        // 타겟팅 모드: 사거리 유효성 검사 후 최종 시전
                        if (activeCardForTargeting != null)
                        {
                            Tile currentTile = controlledCharacter.characterMove?.GetCurrentTile();
                            if (currentTile != null)
                            {
                                int distance = GetDistanceBetweenTiles(currentTile, targetTile);
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
    }

    private int GetDistanceBetweenTiles(Tile t1, Tile t2)
    {
        if (t1 == null || t2 == null) return int.MaxValue;
        var coord1 = t1.GetCoord();
        var coord2 = t2.GetCoord();
        return Mathf.Abs(coord1.x - coord2.x) + Mathf.Abs(coord1.y - coord2.y);
    }

    #endregion
}

