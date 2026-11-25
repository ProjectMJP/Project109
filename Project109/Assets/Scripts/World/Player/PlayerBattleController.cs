using CardTypes;
using EventStructs;
using GameItem.Types;
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
public class PlayerBattleController : ICharacterController, IOnBattleEnd, System.IDisposable
{
    private BattleUIPanel _battleUI;
    private BattleTimelinePanel _timelineUI;
    private BattleSideUnitHUDPanel _sideUnitHUD;
    public BattleUIPanel battleUI => _battleUI;
    public BattleHandPanel handUI => _battleUI != null ? _battleUI.HandPanel : null;

    /// <summary>
    /// 카드 시전 시도가 사전 조건 또는 사거리 검증에서 거부되었을 때 발행되는 이벤트입니다.
    /// </summary>
    public event System.Action<Card, CardPlayFailReason, string> OnCardPlayRejected;

    /// <summary>
    /// 카드 타깃팅 모드 진입 또는 해제 시 현재 타깃팅 중인 카드를 발행하는 이벤트입니다. (해제 시 null)
    /// </summary>
    public event System.Action<Card> OnTargetingStateChanged;

    /// <summary>
    /// 키보드 단축키(1~9)로 손패 카드가 선택되거나 선택 해제되었을 때 발행되는 이벤트입니다.
    /// </summary>
    public event System.Action<Card> OnKeyboardSelectedCardChanged;

    private Card _lastNotifiedTargetingCard;
    private Card _keyboardSelectedCard;

    /// <summary>
    /// 현재 타깃팅 선택 중인 카드 인스턴스를 반환합니다. 타깃팅 중이 아니면 null입니다.
    /// </summary>
    public Card ActiveTargetingCard => controlState == PlayerControlState.TargetSelection ? activeCardForTargeting : null;

    /// <summary>
    /// 현재 키보드 단축키로 1차 선택된 손패 카드를 반환합니다.
    /// </summary>
    public Card KeyboardSelectedCard => _keyboardSelectedCard;

    public void Dispose()
    {
        OnBattleEnd();
        OnCardPlayRejected = null;
        OnTargetingStateChanged = null;
        OnKeyboardSelectedCardChanged = null;
    }

    private void SetKeyboardSelectedCard(Card card)
    {
        if (ReferenceEquals(_keyboardSelectedCard, card))
        {
            return;
        }

        _keyboardSelectedCard = card;
        OnKeyboardSelectedCardChanged?.Invoke(_keyboardSelectedCard);
    }

    private void NotifyTargetingStateChanged(Card targetingCard)
    {
        if (ReferenceEquals(_lastNotifiedTargetingCard, targetingCard))
        {
            return;
        }

        _lastNotifiedTargetingCard = targetingCard;
        OnTargetingStateChanged?.Invoke(targetingCard);
    }

    /// <summary>
    /// PlayerInputController가 살아있는 경우에만 전투 입력 이벤트 구독을 안전하게 해제합니다.
    /// </summary>
    private void UnsubscribeInputEvents()
    {
        if (PlayerInputController.HasInstance)
        {
            PlayerInputController.instance.OnTouchClickEvent -= HandleGlobalClick;
            PlayerInputController.instance.OnPointerHoverEvent -= HandlePointerHover;
            PlayerInputController.instance.OnCancelEvent -= HandleCancelInput;
            PlayerInputController.instance.OnCardHotkeyEvent -= HandleCardHotkey;
            PlayerInputController.instance.OnEndTurnHotkeyEvent -= HandleEndTurnHotkey;
            PlayerInputController.instance.OnToggleMoveModeHotkeyEvent -= HandleToggleMoveModeHotkey;
        }
    }

    #region ICharacterController

    private Character _controlledCharacter;
    public Character controlledCharacter => _controlledCharacter;

    #endregion

    // 현재 플레이어 조작 상태
    public PlayerControlState controlState { get; private set; } = PlayerControlState.Normal;
    private Card activeCardForTargeting;
    private readonly List<Character> _multiSelectedTargets = new List<Character>();
    public List<Character> MultiSelectedTargets => _multiSelectedTargets;
    private Vector2Int _lastHoveredCoord = new Vector2Int(-999, -999);
    private Character _lastHoveredTargetingChar;
    private readonly List<Character> _targetingPreviewBuffer = new List<Character>(8);
    private readonly HashSet<Vector2Int> _targetingAoeCoordBuffer = new HashSet<Vector2Int>();
    private readonly List<Character> _appliedWorldTargetingHighlights = new List<Character>(8);
    private readonly List<Character> _appliedDamagePreviewTargets = new List<Character>(8);
    private readonly List<Character> _immediateHoverPreviewBuffer = new List<Character>(8);
    private readonly RaycastHit[] _characterRaycastHits = new RaycastHit[16];
    private int _moveRequestToken = 0;

    #region Targeting Strategies

    private static readonly Dictionary<TargetType, ITargetingStrategy> _strategies = new()
    {
        { TargetType.Self, new SelfTargetingStrategy() },
        { TargetType.AllEnemies, new AllEnemiesTargetingStrategy() },
        { TargetType.AreaSelf, new AreaSelfTargetingStrategy() },
        { TargetType.RandomEnemy, new RandomEnemyTargetingStrategy() },
        { TargetType.SingleEnemy, new SingleEnemyTargetingStrategy() },
        { TargetType.AreaGround, new AreaGroundTargetingStrategy() },
        { TargetType.MultiEnemy, new MultiEnemyTargetingStrategy() },
        { TargetType.Directional, new DirectionalTargetingStrategy() }
    };

    public ITargetingStrategy GetTargetingStrategy(TargetType type)
    {
        return _strategies.TryGetValue(type, out var strategy) ? strategy : _strategies[TargetType.Self];
    }

    #endregion

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
                _battleUI.SetTurnActive(false);
            }

            _timelineUI = UIManager.instance.GetOrSpawnBattleTimelineUI();
            if (_timelineUI != null)
            {
                _timelineUI.BindBattle(RunManager.instance != null ? RunManager.instance.battleManager : null);
                _timelineUI.Open();
            }

            _sideUnitHUD = UIManager.instance.GetOrSpawnBattleSideUnitHUD();
            if (_sideUnitHUD != null)
            {
                _sideUnitHUD.BindBattle(RunManager.instance != null ? RunManager.instance.battleManager : null);
                _sideUnitHUD.Open();
            }
        }

        if (_controlledCharacter != null)
        {
            _controlledCharacter.eventBus.Add<IOnBattleEnd>(this);
            _controlledCharacter.OnCharacterStateChanged -= HandleControlledCharacterStateChanged;
            _controlledCharacter.OnCharacterStateChanged += HandleControlledCharacterStateChanged;
        }

        ActionQueueManager.OnGlobalBusyStateChanged -= HandleActionQueueBusyChanged;
        ActionQueueManager.OnGlobalBusyStateChanged += HandleActionQueueBusyChanged;
    }

    public void OnBattleEnd()
    {
        CancelCurrentState();
        ClearTargetingHighlights();
        _moveRequestToken++;
        if (playerMove != null)
        {
            playerMove.ClearCanMoveTiles();
        }
        controlState = PlayerControlState.Normal;
        _lastHoveredCoord = new Vector2Int(-999, -999);
        bonusDrawThisTurn = 0;
        SetKeyboardSelectedCard(null);
        NotifyTargetingStateChanged(null);

        UnsubscribeInputEvents();
        ActionQueueManager.OnGlobalBusyStateChanged -= HandleActionQueueBusyChanged;

        if (_controlledCharacter != null)
        {
            _controlledCharacter.eventBus.Remove<IOnBattleEnd>(this);
            _controlledCharacter.OnCharacterStateChanged -= HandleControlledCharacterStateChanged;
        }

        if (_battleUI != null)
        {
            _battleUI.SetTurnActive(false);
            _battleUI.Close();
            _battleUI = null;
        }

        if (_timelineUI != null)
        {
            _timelineUI.BindBattle(null);
            _timelineUI.Close();
            _timelineUI = null;
        }

        if (_sideUnitHUD != null)
        {
            _sideUnitHUD.BindBattle(null);
            _sideUnitHUD.Close();
            _sideUnitHUD = null;
        }

        if (player != null)
        {
            player.DisposeBattleDeck(suppressEvents: true);
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
        int baseCount = (player != null && player.playerStat != null) ? player.playerStat.BaseDrawCount : 5;
        return Mathf.Max(0, baseCount + bonusDrawThisTurn);
    }

    public void OnTurnStart()
    {
        _moveRequestToken++;

        // 1. 이벤트 버스 — 등록된 IOnTurnStart들 호출 (버프, 유물 등에서 AddBonusDrawThisTurn 호출 가능)
        CombatEventDispatcher.DispatchTurnStart(controlledCharacter.eventBus);

        // 기절(Stun) 등 턴 시작 이벤트로 턴이 즉시 종료되었다면 드로우를 진행하지 않고 중단
        if (RunManager.instance != null && RunManager.instance.battleManager != null
            && !ReferenceEquals(RunManager.instance.battleManager.currentTurnController, this))
        {
            if (_battleUI != null)
            {
                _battleUI.SetTurnActive(false);
            }
            return;
        }

        // 2. 카드 드로우 (BattleDeck.OnHandChanged를 통해 BattleHandPanel 자동 갱신)
        battleDeck.DrawCards(GetTurnDrawCount());

        // 3. 이번 턴 보정치 리셋 (다음 턴에는 기본값으로 자동 원복)
        bonusDrawThisTurn = 0;

        // 4. 전역 클릭, 취소 및 전투 단축키 이벤트 구독
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent -= HandleGlobalClick;
            PlayerInputController.instance.OnTouchClickEvent += HandleGlobalClick;
            PlayerInputController.instance.OnPointerHoverEvent -= HandlePointerHover;
            PlayerInputController.instance.OnPointerHoverEvent += HandlePointerHover;
            PlayerInputController.instance.OnCancelEvent -= HandleCancelInput;
            PlayerInputController.instance.OnCancelEvent += HandleCancelInput;
            PlayerInputController.instance.OnCardHotkeyEvent -= HandleCardHotkey;
            PlayerInputController.instance.OnCardHotkeyEvent += HandleCardHotkey;
            PlayerInputController.instance.OnEndTurnHotkeyEvent -= HandleEndTurnHotkey;
            PlayerInputController.instance.OnEndTurnHotkeyEvent += HandleEndTurnHotkey;
            PlayerInputController.instance.OnToggleMoveModeHotkeyEvent -= HandleToggleMoveModeHotkey;
            PlayerInputController.instance.OnToggleMoveModeHotkeyEvent += HandleToggleMoveModeHotkey;
        }
        if (playerMove != null)
        {
            playerMove.ClearCanMoveTiles();
        }
        controlState = PlayerControlState.Normal;
        _lastHoveredCoord = new Vector2Int(-999, -999);
        SetKeyboardSelectedCard(null);
        NotifyTargetingStateChanged(null);
        if (_battleUI != null)
        {
            _battleUI.SwitchToHandMode();
            _battleUI.SetTurnActive(true);
            _battleUI.RefreshMoveStats();
        }
    }

    /// <summary>
    /// 현재 전투 상태 및 턴 소유자가 이 플레이어 컨트롤러인지 반환합니다.
    /// </summary>
    public bool IsPlayerTurnActive()
    {
        if (RunManager.instance == null || RunManager.instance.battleManager == null)
        {
            return false;
        }

        BattleManager battleManager = RunManager.instance.battleManager;
        return battleManager.battleState == BattleState.TurnInProgress &&
               ReferenceEquals(battleManager.currentTurnController, this);
    }

    /// <summary>
    /// 현재 플레이어가 이동모드 전환 또는 턴 종료 등 턴 내 조작을 수행할 수 있는 상태인지 반환합니다.
    /// </summary>
    public bool CanPerformPlayerTurnAction()
    {
        if (controlledCharacter == null || controlledCharacter.isDead)
        {
            return false;
        }

        if (controlledCharacter.currentState != CharacterState.Idle)
        {
            return false;
        }

        if (ActionQueueManager.HasInstance && ActionQueueManager.Instance.IsBusy)
        {
            return false;
        }

        return IsPlayerTurnActive();
    }

    private void HandleControlledCharacterStateChanged(Character character, CharacterState state)
    {
        if (_battleUI != null)
        {
            _battleUI.RefreshActionInteractableState();
        }
    }

    private void HandleActionQueueBusyChanged(bool isBusy)
    {
        if (_battleUI != null)
        {
            _battleUI.RefreshActionInteractableState();
        }
    }

    public void OnTurnEnd()
    {
        _moveRequestToken++;

        // 1. 이벤트 버스 — 등록된 IOnTurnEnd들 호출
        CombatEventDispatcher.DispatchTurnEnd(controlledCharacter.eventBus);

        // 2. 손패 → 묘지 이동 (BattleDeck.OnHandChanged를 통해 BattleHandPanel 자동 갱신)
        battleDeck.DiscardHand();

        // 3. 이동 비활성화 및 칸 정리
        if (playerMove != null)
        {
            playerMove.ClearCanMoveTiles();
        }

        // 4. 이벤트 구독 해제
        UnsubscribeInputEvents();
        if (controlState == PlayerControlState.TargetSelection)
        {
            _multiSelectedTargets.Clear();
            activeCardForTargeting = null;
            if (UIManager.instance != null)
            {
                UIManager.instance.ClearEffectAreaTiles();
            }
            else if (GameMap.current != null)
            {
                GameMap.current.ClearCardRange();
            }
        }
        controlState = PlayerControlState.Normal;
        _lastHoveredCoord = new Vector2Int(-999, -999);
        bonusDrawThisTurn = 0;

        ClearTargetingHighlights();
        SetKeyboardSelectedCard(null);
        NotifyTargetingStateChanged(null);

        if (_battleUI != null)
        {
            _battleUI.SetTurnActive(false);
        }
    }

    /// <summary>
    /// 플레이어의 턴 종료를 요청합니다.
    /// </summary>
    public void RequestEndTurn()
    {
        if (!CanPerformPlayerTurnAction())
        {
            Debug.LogWarning("[PlayerBattleController] 현재 플레이어가 턴을 종료할 수 있는 상태가 아닙니다.");
            return;
        }

        _moveRequestToken++;

        if (controlState == PlayerControlState.TargetSelection)
        {
            _multiSelectedTargets.Clear();
            activeCardForTargeting = null;
            if (UIManager.instance != null)
            {
                UIManager.instance.ClearEffectAreaTiles();
            }
            else if (GameMap.current != null)
            {
                GameMap.current.ClearCardRange();
            }
            ClearTargetingHighlights();
            NotifyTargetingStateChanged(null);
        }

        // 턴 종료 요청 즉시 이동 가능 타일 인디케이터 및 상태 정리, UI 버튼 비활성화 (반응성 및 중복 클릭 방지)
        SetKeyboardSelectedCard(null);
        if (playerMove != null)
        {
            playerMove.ClearCanMoveTiles();
        }
        controlState = PlayerControlState.Normal;

        if (_battleUI != null)
        {
            _battleUI.SetTurnActive(false);
        }

        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            RunManager.instance.battleManager.EndTurn(this);
        }
    }

    #region Card Operations

    /// <summary>
    /// 카드를 실제로 사용하는 파이프라인
    /// </summary>
    public void UseCard(Card card, List<Character> targets, Vector2Int targetPosition)
    {
        ClearDamagePreviews();
        SetKeyboardSelectedCard(null);
        // 즉시 실행하지 않고 액션 큐에 위임
        CardPlayAction playAction = new CardPlayAction(controlledCharacter, card, targets, targetPosition);
        ActionQueueManager.Instance.EnqueueAction(playAction);
    }

    /// <summary>
    /// 카드 시전 시도 (사전 유효성 검사 후 지정형 카드는 타겟팅 모드로 진입, 비지정형은 즉시 시전)
    /// </summary>
    public void TryUseCard(Card card)
    {
        if (card == null) return;

        // 0. 플레이어 턴 여부 검사
        if (RunManager.instance != null &&
            RunManager.instance.battleManager != null &&
            RunManager.instance.battleManager.currentTurnController != null &&
            !ReferenceEquals(RunManager.instance.battleManager.currentTurnController, this))
        {
            Debug.LogWarning("[PlayerBattleController] 현재 플레이어의 턴이 아니므로 카드를 사용할 수 없습니다.");
            OnCardPlayRejected?.Invoke(card, CardPlayFailReason.NotPlayerTurn, "현재 플레이어의 턴이 아닙니다.");
            return;
        }

        if (controlledCharacter == null || controlledCharacter.currentState != CharacterState.Idle)
        {
            Debug.LogWarning("[PlayerBattleController] 캐릭터가 대기(Idle) 상태가 아니므로 카드를 사용할 수 없습니다.");
            OnCardPlayRejected?.Invoke(card, CardPlayFailReason.CharacterBusy, "현재 행동 중입니다.");
            return;
        }

        // 1. 액션 큐 실행 중 조작 방지 잠금
        if (ActionQueueManager.Instance.IsBusy)
        {
            Debug.LogWarning("[PlayerBattleController] 이전 행동을 수행 중입니다. 카드 사용이 무시됩니다.");
            OnCardPlayRejected?.Invoke(card, CardPlayFailReason.CharacterBusy, "현재 행동 중입니다.");
            return;
        }

        // 2. 실시간 다중 비용 및 시전 가능 조건 검사
        if (!card.TryCheckPlayable(controlledCharacter, out CardPlayFailReason failReason, out string failMsg))
        {
            Debug.LogWarning($"[PlayerBattleController] 시전 요구조건이 충족되지 않아 '{card.cardName}' 카드를 사용할 수 없습니다.");
            OnCardPlayRejected?.Invoke(card, failReason, failMsg);
            return;
        }

        TargetType parsedTargetType = card.cardData != null ? card.cardData.parsedTargetType : TargetType.SingleEnemy;

        // 3. [★ 핵심] 사거리 내 유효 대상 존재 여부 사전 검증 (Fail-Fast)
        if (!HasValidTargetsInRange(card, parsedTargetType))
        {
            Debug.LogWarning($"[PlayerBattleController] '{card.cardName}' 시전 실패: 사거리 내에 유효한 대상이 없습니다.");
            if (controlState == PlayerControlState.TargetSelection)
            {
                CancelCurrentState();
            }
            OnCardPlayRejected?.Invoke(card, CardPlayFailReason.NoValidTargets, "사거리 내에 유효한 대상이 없습니다.");
            return;
        }

        // 이미 다른 카드로 타겟팅 중이었다면 이전 타겟팅 상태 안전하게 리셋
        if (controlState == PlayerControlState.TargetSelection)
        {
            CancelCurrentState();
        }

        Vector2Int casterPos = Vector2Int.zero;
        if (controlledCharacter.characterMove != null && controlledCharacter.characterMove.GetCurrentTile() != null)
        {
            casterPos = controlledCharacter.characterMove.GetCurrentTile().GetCoord();
        }

        // 4. 타깃팅 전략 위임 처리
        ITargetingStrategy strategy = GetTargetingStrategy(parsedTargetType);

        if (strategy.IsManualSelectionRequired)
        {
            // 수동 지정형: 타겟팅 모드로 진입
            ClearTargetingHighlights();
            _multiSelectedTargets.Clear();
            controlState = PlayerControlState.TargetSelection;
            activeCardForTargeting = card;

            if (UIManager.instance != null)
            {
                UIManager.instance.UpdateEffectAreaUI(casterPos, card);
            }

            ApplyTargetingHighlights(_targetingPreviewBuffer, activeCardForTargeting);
            NotifyTargetingStateChanged(activeCardForTargeting);

            Debug.Log($"[TargetSelection] {card.cardData?.cardName} 카드의 대상을 지정해 주세요. (TargetType: {parsedTargetType})");
        }
        else
        {
            // 즉시 시전형: 전략을 통해 대상을 즉시 수집 후 시전
            List<Character> immediateTargets = strategy.CollectImmediateTargets(this, card, casterPos);
            UseCard(card, immediateTargets, casterPos);

            if (UIManager.instance != null)
            {
                UIManager.instance.ClearEffectAreaTiles();
            }
            else if (GameMap.current != null)
            {
                GameMap.current.ClearEffectAreaTiles();
            }
        }
    }

    /// <summary>
    /// 카드의 타깃팅 유형과 사거리를 기반으로 현재 유효한 대상이 존재하는지 사전에 검증합니다.
    /// </summary>
    public bool HasValidTargetsInRange(Card card, TargetType targetType)
    {
        if (card == null || card.cardData == null) return false;
        return GetTargetingStrategy(targetType).HasValidTargets(this, card);
    }

    /// <summary>
    /// 플레이어 기준 맨해튼 사거리 내에 살아있는 적 캐릭터가 1명 이상 존재하는지 GC 할당 없이 검사합니다.
    /// </summary>
    public bool HasEnemiesInRange(int minDistance, int maxDistance)
    {
        Tile playerTile = (controlledCharacter != null && controlledCharacter.characterMove != null)
            ? controlledCharacter.characterMove.GetCurrentTile()
            : null;

        if (playerTile == null || RunManager.instance == null || RunManager.instance.battleManager == null)
        {
            return false;
        }

        IReadOnlyList<ICharacterController> combatants = RunManager.instance.battleManager.GetAllCombatants();
        for (int i = 0; i < combatants.Count; i++)
        {
            ICharacterController combatant = combatants[i];
            if (combatant == null || combatant.controlledCharacter == null) continue;
            if (ReferenceEquals(combatant.controlledCharacter, controlledCharacter)) continue;
            if (combatant.controlledCharacter.isDead) continue;
            if (controlledCharacter != null && !controlledCharacter.IsHostileTo(combatant.controlledCharacter)) continue;

            CharacterMove enemyMove = combatant.controlledCharacter.characterMove;
            if (enemyMove != null && enemyMove.GetCurrentTile() != null)
            {
                int dist = Tile.GetDistance(playerTile, enemyMove.GetCurrentTile());
                if (dist >= minDistance && dist <= maxDistance)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 전장에 살아있는 적 캐릭터가 1명 이상 존재하는지 GC 할당 없이 검사합니다.
    /// </summary>
    public bool HasAnyLivingEnemy()
    {
        if (RunManager.instance == null || RunManager.instance.battleManager == null)
        {
            return false;
        }

        IReadOnlyList<ICharacterController> combatants = RunManager.instance.battleManager.GetAllCombatants();
        for (int i = 0; i < combatants.Count; i++)
        {
            ICharacterController combatant = combatants[i];
            if (combatant == null || combatant.controlledCharacter == null) continue;
            if (ReferenceEquals(combatant.controlledCharacter, controlledCharacter)) continue;
            if (combatant.controlledCharacter.isDead) continue;
            if (controlledCharacter != null && !controlledCharacter.IsHostileTo(combatant.controlledCharacter)) continue;

            return true;
        }
        return false;
    }

    /// <summary>
    /// 손패 UI의 실시간 시각 피드백(글로우/딤/코스트 색상)을 위해 카드의 현재 시전 가능 상태를 평가합니다.
    /// </summary>
    public CardPlayabilityVisualState EvaluateCardPlayabilityVisual(Card card, out bool isCostInsufficient)
    {
        isCostInsufficient = false;
        if (card == null || card.IsDisposed || controlledCharacter == null)
        {
            return CardPlayabilityVisualState.UnplayableCondition;
        }

        isCostInsufficient = controlledCharacter.curStamina < card.currentCost;

        bool hasBattleManager = RunManager.instance != null && RunManager.instance.battleManager != null;
        if ((hasBattleManager && !IsPlayerTurnActive()) ||
            controlledCharacter.isDead ||
            controlledCharacter.currentState != CharacterState.Idle ||
            (ActionQueueManager.HasInstance && ActionQueueManager.Instance.IsBusy))
        {
            return isCostInsufficient
                ? CardPlayabilityVisualState.InsufficientStamina
                : CardPlayabilityVisualState.UnplayableCondition;
        }

        if (!card.TryCheckPlayable(controlledCharacter, out CardPlayFailReason failReason, out _))
        {
            if (failReason == CardPlayFailReason.InsufficientStamina)
            {
                isCostInsufficient = true;
                return CardPlayabilityVisualState.InsufficientStamina;
            }
            return CardPlayabilityVisualState.UnplayableCondition;
        }

        TargetType targetType = card.cardData != null ? card.cardData.parsedTargetType : TargetType.SingleEnemy;
        if (!HasValidTargetsInRange(card, targetType))
        {
            return CardPlayabilityVisualState.UnplayableCondition;
        }

        return CardPlayabilityVisualState.Playable;
    }

    /// <summary>
    /// 플레이어 기준 맨해튼 사거리 내에 위치한 살아있는 모든 적 캐릭터 목록을 가져옵니다.
    /// </summary>
    public List<Character> GetEnemiesInRange(int minDistance, int maxDistance)
    {
        List<Character> enemies = new List<Character>();
        Tile playerTile = (controlledCharacter != null && controlledCharacter.characterMove != null)
            ? controlledCharacter.characterMove.GetCurrentTile()
            : null;

        if (playerTile == null || RunManager.instance == null || RunManager.instance.battleManager == null)
        {
            return enemies;
        }

        var combatants = RunManager.instance.battleManager.GetAllCombatants();
        for (int i = 0; i < combatants.Count; i++)
        {
            var combatant = combatants[i];
            if (combatant == null || combatant.controlledCharacter == null) continue;
            if (ReferenceEquals(combatant.controlledCharacter, controlledCharacter)) continue;
            if (combatant.controlledCharacter.isDead) continue;
            if (controlledCharacter != null && !controlledCharacter.IsHostileTo(combatant.controlledCharacter)) continue;

            var enemyMove = combatant.controlledCharacter.characterMove;
            if (enemyMove != null && enemyMove.GetCurrentTile() != null)
            {
                int dist = Tile.GetDistance(playerTile, enemyMove.GetCurrentTile());
                if (dist >= minDistance && dist <= maxDistance)
                {
                    enemies.Add(combatant.controlledCharacter);
                }
            }
        }
        return enemies;
    }

    /// <summary>
    /// 플레이어 기준 맨해튼 사거리 내에 위치한 파괴 가능한 오브젝트(DestructibleObject)가 존재하는지 확인합니다.
    /// </summary>
    public bool HasDestructiblesInRange(int minDistance, int maxDistance)
    {
        Tile playerTile = (controlledCharacter != null && controlledCharacter.characterMove != null)
            ? controlledCharacter.characterMove.GetCurrentTile()
            : null;
        if (playerTile == null || GameMap.current == null) return false;

        GameMap map = GameMap.current;
        for (int c = 0; c < map.mapColumn; c++)
        {
            for (int r = 0; r < map.mapRow; r++)
            {
                Tile tile = map.GetTile(c, r);
                if (tile != null && tile.occupyingDestructible != null && !tile.occupyingDestructible.isDead)
                {
                    int dist = Tile.GetDistance(playerTile, tile);
                    if (dist >= minDistance && dist <= maxDistance)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 전장에 살아있는 모든 적 캐릭터 목록을 반환합니다.
    /// </summary>
    public List<Character> GetAllLivingEnemies()
    {
        List<Character> enemies = new List<Character>();
        if (RunManager.instance == null || RunManager.instance.battleManager == null) return enemies;

        var combatants = RunManager.instance.battleManager.GetAllCombatants();
        for (int i = 0; i < combatants.Count; i++)
        {
            var combatant = combatants[i];
            if (combatant == null || combatant.controlledCharacter == null) continue;
            if (ReferenceEquals(combatant.controlledCharacter, controlledCharacter)) continue;
            if (combatant.controlledCharacter.isDead) continue;
            if (controlledCharacter != null && !controlledCharacter.IsHostileTo(combatant.controlledCharacter)) continue;

            enemies.Add(combatant.controlledCharacter);
        }
        return enemies;
    }

    /// <summary>
    /// 후보 리스트 중 지정된 수량만큼 대상을 무작위 추첨합니다. (중복 허용 여부 옵션 반영)
    /// </summary>
    public List<Character> PickRandomTargets(List<Character> candidates, int count, bool allowDuplicate)
    {
        List<Character> result = new List<Character>();
        if (candidates == null || candidates.Count == 0 || count <= 0) return result;

        if (allowDuplicate)
        {
            for (int i = 0; i < count; i++)
            {
                int idx = UnityEngine.Random.Range(0, candidates.Count);
                result.Add(candidates[idx]);
            }
        }
        else
        {
            List<Character> pool = new List<Character>(candidates);
            int pickCount = Mathf.Min(count, pool.Count);
            for (int i = 0; i < pickCount; i++)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
        }
        return result;
    }

    private int _lastBattleCancelFrame = -1;

    /// <summary>
    /// 현재 배틀 컨트롤러가 이동, 타깃팅 또는 키보드 카드 선택 등 ESC/우클릭으로 취소 가능한 하위 조작 상태인지 반환합니다.
    /// </summary>
    public bool HasActiveBattleCancelTarget => controlState != PlayerControlState.Normal || _keyboardSelectedCard != null;

    /// <summary>
    /// 현재 프레임에 배틀 타깃팅/이동 취소가 이미 소비되었는지 반환합니다.
    /// </summary>
    public bool IsBattleCancelConsumedThisFrame => _lastBattleCancelFrame == Time.frameCount;

    /// <summary>
    /// 모달 팝업·대화·드래그 가드 등으로 전투 단축키 입력이 차단되어야 하는지 검사합니다.
    /// </summary>
    private bool IsBattleHotkeyBlocked()
    {
        if (PlayerInputController.HasInstance && PlayerInputController.instance.IsWorldInputBlocked)
        {
            return true;
        }

        if (UIManager.instance != null &&
            (UIManager.instance.IsWorldInputBlocked || UIManager.instance.HasActivePopup))
        {
            return true;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            return true;
        }

        if (UIDragInputGuard.IsAnyUIDragging)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 숫자 단축키(1~9, 0-based 인덱스) 입력 처리:
    /// 수동 지정형 카드는 즉시 타깃팅 진입, 즉시 시전형 카드는 1차 입력 시 선택·하이라이트 후 재입력 시 시전합니다.
    /// </summary>
    public void HandleCardHotkey(int handIndex)
    {
        if (IsBattleHotkeyBlocked() || battleDeck == null || battleDeck.hand == null)
        {
            return;
        }

        if (handIndex < 0 || handIndex >= battleDeck.hand.Count)
        {
            return;
        }

        if (controlState == PlayerControlState.Move)
        {
            CancelCurrentState();
        }

        Card card = battleDeck.hand[handIndex];
        if (card == null)
        {
            return;
        }

        TargetType targetType = card.cardData != null ? card.cardData.parsedTargetType : TargetType.SingleEnemy;
        ITargetingStrategy strategy = GetTargetingStrategy(targetType);

        if (strategy.IsManualSelectionRequired)
        {
            SetKeyboardSelectedCard(null);
            TryUseCard(card);
            return;
        }

        if (ReferenceEquals(_keyboardSelectedCard, card))
        {
            SetKeyboardSelectedCard(null);
            TryUseCard(card);
        }
        else
        {
            if (controlState == PlayerControlState.TargetSelection)
            {
                CancelCurrentState();
            }
            SetKeyboardSelectedCard(card);
        }
    }

    /// <summary>
    /// 턴 종료 단축키(E / Space) 입력 처리입니다.
    /// </summary>
    public void HandleEndTurnHotkey()
    {
        if (IsBattleHotkeyBlocked() || controlState == PlayerControlState.TargetSelection)
        {
            return;
        }

        RequestEndTurn();
    }

    /// <summary>
    /// 카드 모드 ↔ 이동 모드 토글 단축키(Tab / Q / M) 입력 처리입니다.
    /// </summary>
    public void HandleToggleMoveModeHotkey()
    {
        if (IsBattleHotkeyBlocked() || controlState == PlayerControlState.TargetSelection)
        {
            return;
        }

        if (!CanPerformPlayerTurnAction())
        {
            return;
        }

        SetKeyboardSelectedCard(null);

        if (_battleUI != null)
        {
            _battleUI.ToggleMode();
        }
        else if (controlState == PlayerControlState.Move)
        {
            CancelCurrentState();
        }
        else
        {
            EnablePlayerMove();
        }
    }

    /// <summary>
    /// 입력 컨트롤러의 취소(ESC/우클릭) 이벤트 수신 시 호출됩니다.
    /// 모달 팝업이 열려 있거나 동일 프레임에 팝업 닫기가 먼저 소비된 경우 타깃팅/이동 취소가 중복 실행되지 않도록 방어합니다.
    /// </summary>
    private void HandleCancelInput()
    {
        if (PlayerInputController.instance != null &&
            (PlayerInputController.instance.IsWorldInputBlocked || PlayerInputController.instance.WasWorldInputBlockedOnCancel))
        {
            return;
        }

        if (UIManager.instance != null &&
            (UIManager.instance.IsWorldInputBlocked || UIManager.instance.HasActivePopup || UIManager.instance.IsCancelConsumedThisFrame))
        {
            return;
        }

        CancelCurrentState();
    }

    /// <summary>
    /// 현재 조작 상태(이동 모드, 타겟 모드, 키보드 선택 상태) 취소
    /// </summary>
    public void CancelCurrentState()
    {
        _moveRequestToken++;
        if (controlState == PlayerControlState.Normal)
        {
            if (_keyboardSelectedCard != null)
            {
                _lastBattleCancelFrame = Time.frameCount;
                SetKeyboardSelectedCard(null);
            }
            NotifyTargetingStateChanged(null);
            return;
        }
        _lastBattleCancelFrame = Time.frameCount;
        SetKeyboardSelectedCard(null);

        Debug.Log($"[PlayerBattleController] 조작 취소. 이전 상태: {controlState}");

        if (controlState == PlayerControlState.TargetSelection)
        {
            _multiSelectedTargets.Clear();
            activeCardForTargeting = null;
            if (UIManager.instance != null)
            {
                UIManager.instance.ClearEffectAreaTiles();
            }
            else if (GameMap.current != null)
            {
                GameMap.current.ClearCardRange();
            }

            ClearTargetingHighlights();
        }
        else if (controlState == PlayerControlState.Move)
        {
            if (playerMove != null)
            {
                playerMove.ClearCanMoveTiles();
            }
        }

        controlState = PlayerControlState.Normal;
        NotifyTargetingStateChanged(null);
        if (_battleUI != null && _battleUI.currentMode != BattleUIMode.Hand)
        {
            _battleUI.SwitchToHandMode();
        }
        if (_battleUI != null)
        {
            _battleUI.RefreshCardPlayability();
        }
    }

    /// <summary>
    /// 이동 모드를 활성화하기 위한 상태 전이 메서드. 플레이어 턴이 아니거나 행동 불가 시 false를 반환하고 차단합니다.
    /// </summary>
    public bool EnablePlayerMove()
    {
        if (!CanPerformPlayerTurnAction())
        {
            Debug.LogWarning("[PlayerBattleController] 현재 플레이어가 행동 가능한 턴 상태가 아니므로 이동 모드로 전환할 수 없습니다.");
            if (_battleUI != null)
            {
                if (_battleUI.currentMode != BattleUIMode.Hand)
                {
                    _battleUI.SwitchToHandMode();
                }
                _battleUI.RefreshActionInteractableState();
            }
            return false;
        }

        if (playerMove == null)
        {
            return false;
        }

        if (controlState == PlayerControlState.TargetSelection)
        {
            CancelCurrentState();
        }

        _moveRequestToken++;
        SetKeyboardSelectedCard(null);
        playerMove.CheckCanMoveTiles();
        controlState = PlayerControlState.Move;

        if (_battleUI != null && _battleUI.currentMode != BattleUIMode.Move)
        {
            _battleUI.SwitchToMoveMode();
        }
        return true;
    }

    /// <summary>
    /// 전투 이동 완료 시 호출되어 잔여 이동력과 도달 가능 타일 유무에 따라 이동 모드를 유지하거나 손패 모드로 복귀합니다.
    /// 취소·턴 종료·전투 종료 등으로 토큰이 무효화된 지연 콜백은 즉시 무시합니다.
    /// </summary>
    public void HandleBattleMoveCompleted(int moveToken)
    {
        if (moveToken != _moveRequestToken || controlState != PlayerControlState.Move)
        {
            return;
        }

        if (_battleUI != null)
        {
            _battleUI.RefreshMoveStats();
        }

        bool isBattleActiveAndPlayerTurn = true;
        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            BattleManager battleManager = RunManager.instance.battleManager;
            isBattleActiveAndPlayerTurn =
                battleManager.battleState != BattleState.None &&
                battleManager.battleState != BattleState.BattleEnd &&
                (battleManager.currentTurnController == null || ReferenceEquals(battleManager.currentTurnController, this));
        }

        bool isMoveEligible =
            playerMove != null &&
            controlledCharacter != null &&
            !controlledCharacter.isDead &&
            isBattleActiveAndPlayerTurn &&
            controlledCharacter.curMoveRange > 0;

        if (isMoveEligible)
        {
            playerMove.CheckCanMoveTiles();
            if (playerMove.canMoveTiles != null && playerMove.canMoveTiles.Count > 0)
            {
                if (_battleUI != null)
                {
                    _battleUI.RefreshMoveStats();
                }
                return;
            }
        }

        if (playerMove != null)
        {
            playerMove.ClearCanMoveTiles();
        }
        controlState = PlayerControlState.Normal;
        if (_battleUI != null && _battleUI.currentMode != BattleUIMode.Hand)
        {
            _battleUI.SwitchToHandMode();
        }
        if (_battleUI != null)
        {
            _battleUI.RefreshCardPlayability();
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

        // 1. Tile 레이어 우선 레이캐스트 (히트 지점 기준 육각 Voronoi 피킹 우선)
        int tileMask = LayerMask.GetMask("Tile");
        if (tileMask != 0 && Physics.Raycast(ray, out RaycastHit tileHit, 10000.0f, tileMask))
        {
            if (GameMap.current != null)
            {
                targetTile = GameMap.current.GetTileAtWorldPosition(tileHit.point);
            }
            if (targetTile == null)
            {
                targetTile = tileHit.collider.GetComponentInParent<Tile>();
            }
        }

        // 2. Tile 레이어 히트 실패 시 일반 레이캐스트 (장애물 레이어 제외)
        int obstacleLayerMask = LayerMask.GetMask("Obstacle");
        int generalMask = Physics.DefaultRaycastLayers & ~obstacleLayerMask;
        if (targetTile == null && Physics.Raycast(ray, out RaycastHit generalHit, 10000.0f, generalMask))
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
    /// 지정된 타깃팅 타입 기준으로 캐릭터가 레이캐스트/스냅 후보에 부합하는지 판정합니다.
    /// </summary>
    private bool IsCharacterEligibleForScreenSnap(Character candidate, TargetType? filterTargetType)
    {
        if (candidate == null || candidate.isDead)
        {
            return false;
        }

        if (!filterTargetType.HasValue)
        {
            return true;
        }

        switch (filterTargetType.Value)
        {
            case TargetType.SingleEnemy:
            case TargetType.MultiEnemy:
                return IsCharacterMatchingTargetType(candidate, isFriendlyTarget: false);

            case TargetType.SingleAlly:
                return IsCharacterMatchingTargetType(candidate, isFriendlyTarget: true);

            default:
                return true;
        }
    }

    /// <summary>
    /// 화면 스크린 좌표로부터 캐릭터(플레이어/적) 콜라이더를 우선 레이캐스트하고, 미스 시 타일 점유 캐릭터로 폴백 감지합니다.
    /// 적 지정형 타깃팅 중 카메라 앞쪽의 플레이어/아군 콜라이더가 뒤편 적을 가리는 경우를 방지하기 위해 RaycastNonAlloc으로 유효 대상만 필터링합니다.
    /// </summary>
    private Character TryGetCharacterFromScreenPosition(Vector2 pos, Tile fallbackTile, TargetType? filterTargetType = null)
    {
        if (Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(pos);
            int charMask;
            if (filterTargetType == TargetType.SingleEnemy || filterTargetType == TargetType.MultiEnemy)
            {
                charMask = LayerMask.GetMask("Enemy");
                if (charMask == 0)
                {
                    charMask = LayerMask.GetMask("Player", "Enemy");
                }
            }
            else
            {
                charMask = LayerMask.GetMask("Player", "Enemy");
            }

            if (charMask != 0)
            {
                int hitCount = Physics.RaycastNonAlloc(ray, _characterRaycastHits, 10000f, charMask, QueryTriggerInteraction.Collide);
                Character closestChar = null;
                float closestDistance = float.MaxValue;

                for (int i = 0; i < hitCount; i++)
                {
                    Collider hitCollider = _characterRaycastHits[i].collider;
                    if (hitCollider == null) continue;

                    Character hitChar = hitCollider.GetComponentInParent<Character>();
                    if (!IsCharacterEligibleForScreenSnap(hitChar, filterTargetType)) continue;

                    float dist = _characterRaycastHits[i].distance;
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestChar = hitChar;
                    }
                }

                if (closestChar != null)
                {
                    return closestChar;
                }
            }
        }

        if (fallbackTile != null && IsCharacterEligibleForScreenSnap(fallbackTile.occupyingCharacter, filterTargetType))
        {
            return fallbackTile.occupyingCharacter;
        }

        return null;
    }

    /// <summary>
    /// 일반 전투 모드(Normal)에서 스태미나나 턴 행동을 소모하지 않고 플레이어 캐릭터를 지정된 대상 방향으로 회전시킵니다.
    /// </summary>
    public bool TryRotatePlayerTowardCharacter(Character target)
    {
        if (controlState != PlayerControlState.Normal) return false;
        if (controlledCharacter == null || controlledCharacter.isDead || controlledCharacter.currentState != CharacterState.Idle || controlledCharacter.characterMove == null)
        {
            return false;
        }
        if (target == null || target.isDead || ReferenceEquals(target, controlledCharacter))
        {
            return false;
        }
        if (ActionQueueManager.HasInstance && ActionQueueManager.Instance.IsBusy)
        {
            return false;
        }
        if (RunManager.instance != null &&
            RunManager.instance.battleManager != null &&
            RunManager.instance.battleManager.currentTurnController != null &&
            !ReferenceEquals(RunManager.instance.battleManager.currentTurnController, this))
        {
            return false;
        }

        Tile targetTile = target.characterMove != null ? target.characterMove.GetCurrentTile() : null;
        if (targetTile != null)
        {
            Vector3 tileDiff = targetTile.transform.position - controlledCharacter.transform.position;
            if (tileDiff != Vector3.zero)
            {
                controlledCharacter.characterMove.LookAtTile(targetTile);
                RefreshKeyboardSelectedImmediatePreview();
                return true;
            }
        }

        Vector3 diff = target.transform.position - controlledCharacter.transform.position;
        if (diff != Vector3.zero)
        {
            controlledCharacter.characterMove.SetFacingDirection(CharacterMove.GetDirectionFromVector(diff), true);
            RefreshKeyboardSelectedImmediatePreview();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 타깃팅 모드 중 35도 경사 카메라에서 3D 캐릭터 콜라이더를 우선 감지하고, 단일/아군 지정형 카드의 경우 해당 캐릭터 타일로 좌표를 보정합니다.
    /// </summary>
    private Tile ResolveTargetingTileWithCharacterSnap(Vector2 screenPos, Tile rayTile, TargetType targetType, out Character directChar)
    {
        bool shouldFilterSnap = targetType == TargetType.SingleEnemy
            || targetType == TargetType.MultiEnemy
            || targetType == TargetType.SingleAlly;
        directChar = TryGetCharacterFromScreenPosition(screenPos, rayTile, shouldFilterSnap ? targetType : (TargetType?)null);
        if (shouldFilterSnap
            && directChar != null
            && !directChar.isDead
            && directChar.characterMove != null)
        {
            Tile charTile = directChar.characterMove.GetCurrentTile();
            if (charTile != null)
            {
                return charTile;
            }
        }
        return rayTile;
    }

    /// <summary>
    /// 타깃팅 모드 중 마우스 커서 이동에 따라 실시간으로 폭발 범위(AoE) 타일과 피격 예정 유닛 하이라이트를 갱신합니다.
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

        Tile rayTile = GetTileFromScreenPosition(screenPos);
        TargetType targetType = activeCardForTargeting.cardData != null
            ? activeCardForTargeting.cardData.parsedTargetType
            : TargetType.SingleEnemy;
        Tile hoveredTile = ResolveTargetingTileWithCharacterSnap(screenPos, rayTile, targetType, out Character directHoveredChar);
        Vector2Int hoveredCoord = hoveredTile != null ? hoveredTile.GetCoord() : new Vector2Int(-999, -999);

        // 타일 좌표와 직접 호버 캐릭터가 모두 동일할 때만 중복 갱신 스킵
        if (hoveredCoord == _lastHoveredCoord && ReferenceEquals(directHoveredChar, _lastHoveredTargetingChar))
        {
            return;
        }
        _lastHoveredCoord = hoveredCoord;
        _lastHoveredTargetingChar = directHoveredChar;

        Tile playerTile = controlledCharacter != null && controlledCharacter.characterMove != null
            ? controlledCharacter.characterMove.GetCurrentTile()
            : null;
        Vector2Int playerCoord = playerTile != null ? playerTile.GetCoord() : Vector2Int.zero;

        bool isInRange = false;
        if (hoveredTile != null && playerTile != null && activeCardForTargeting.cardData != null)
        {
            int distance = Tile.GetDistance(playerTile, hoveredTile);
            int minDistance = activeCardForTargeting.cardData.targetMinDistance;
            int maxDistance = activeCardForTargeting.cardData.targetMaxDistance;
            isInRange = distance >= minDistance && distance <= maxDistance;
        }

        if (isInRange && hoveredTile != null && GameMap.current != null)
        {
            // 유효 사거리 내 타일이면 사거리(R) 유지 + 해당 타일 중심 폭발 범위(G) 실시간 표시
            LookDirection facing = (controlledCharacter != null && controlledCharacter.characterMove != null)
                ? controlledCharacter.characterMove.facingDirection
                : LookDirection.East;
            GameMap.current.ShowCardAoE(playerCoord, hoveredCoord, activeCardForTargeting.cardData, facing);
        }
        else if (GameMap.current != null)
        {
            // 사거리 밖이거나 타일이 없으면 사거리만 유지 표시
            GameMap.current.ShowCardRange(playerCoord, activeCardForTargeting.cardData);
        }

        CollectTargetingPreviewTargets(
            activeCardForTargeting,
            hoveredTile,
            hoveredCoord,
            isInRange,
            directHoveredChar,
            _targetingPreviewBuffer);
        ApplyTargetingHighlights(_targetingPreviewBuffer, activeCardForTargeting);
        UpdateDamagePreviews(activeCardForTargeting, _targetingPreviewBuffer, hoveredCoord);
    }

    /// <summary>
    /// 지정된 타일 좌표 집합에 위치한 유효 대상 캐릭터들을 GC 할당 없이 results 리스트에 중복 없이 추가합니다.
    /// </summary>
    public void CollectCharactersInCoordsNonAlloc(HashSet<Vector2Int> targetCoords, TargetType targetType, List<Character> results)
    {
        if (targetCoords == null || targetCoords.Count == 0 || results == null) return;

        bool isFriendlyTarget = targetType == TargetType.SingleAlly
            || targetType == TargetType.AllAllies
            || targetType == TargetType.Self;

        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            IReadOnlyList<ICharacterController> combatants = RunManager.instance.battleManager.GetAllCombatants();
            for (int i = 0; i < combatants.Count; i++)
            {
                ICharacterController combatant = combatants[i];
                if (combatant == null || combatant.controlledCharacter == null) continue;

                Character candidate = combatant.controlledCharacter;
                if (!IsCharacterMatchingTargetType(candidate, isFriendlyTarget)) continue;

                CharacterMove cMove = candidate.characterMove;
                Tile currentTile = cMove != null ? cMove.GetCurrentTile() : null;
                if (currentTile != null && targetCoords.Contains(currentTile.GetCoord()) && !ContainsCharacter(results, candidate))
                {
                    results.Add(candidate);
                }
            }
            return;
        }

        if (GameMap.current != null)
        {
            foreach (Vector2Int coord in targetCoords)
            {
                Tile tile = GameMap.current.GetTile(coord);
                if (tile == null || tile.occupyingCharacter == null) continue;

                Character candidate = tile.occupyingCharacter;
                if (!IsCharacterMatchingTargetType(candidate, isFriendlyTarget)) continue;

                if (!ContainsCharacter(results, candidate))
                {
                    results.Add(candidate);
                }
            }
        }
    }

    /// <summary>
    /// 현재 호버 상태와 다중 선택 확정 목록(_multiSelectedTargets)을 종합해 피격·적용 예정 캐릭터 목록을 GC 없이 수집합니다.
    /// </summary>
    public void CollectTargetingPreviewTargets(
        Card card,
        Tile hoveredTile,
        Vector2Int hoveredCoord,
        bool isInRange,
        Character directHoveredChar,
        List<Character> results)
    {
        if (results == null) return;
        results.Clear();

        for (int i = 0; i < _multiSelectedTargets.Count; i++)
        {
            Character selected = _multiSelectedTargets[i];
            if (selected != null && !selected.isDead && !ContainsCharacter(results, selected))
            {
                results.Add(selected);
            }
        }

        if (!isInRange || hoveredTile == null || card == null || card.cardData == null)
        {
            return;
        }

        TargetType targetType = card.cardData.parsedTargetType;
        switch (targetType)
        {
            case TargetType.AllEnemies:
                CollectAllMatchingCombatants(isFriendlyTarget: false, results);
                break;

            case TargetType.AllAllies:
                CollectAllMatchingCombatants(isFriendlyTarget: true, results);
                break;

            case TargetType.Self:
                if (controlledCharacter != null && !controlledCharacter.isDead && !ContainsCharacter(results, controlledCharacter))
                {
                    results.Add(controlledCharacter);
                }
                break;

            case TargetType.MultiEnemy:
                {
                    Character candidate = directHoveredChar != null ? directHoveredChar : hoveredTile.occupyingCharacter;
                    if (IsCharacterMatchingTargetType(candidate, isFriendlyTarget: false) && !ContainsCharacter(results, candidate))
                    {
                        results.Add(candidate);
                    }
                    break;
                }

            case TargetType.SingleAlly:
                {
                    Character candidate = directHoveredChar != null ? directHoveredChar : hoveredTile.occupyingCharacter;
                    if (IsCharacterMatchingTargetType(candidate, isFriendlyTarget: true) && !ContainsCharacter(results, candidate))
                    {
                        results.Add(candidate);
                    }
                    break;
                }

            default:
                {
                    _targetingAoeCoordBuffer.Clear();
                    Tile playerTile = (controlledCharacter != null && controlledCharacter.characterMove != null)
                        ? controlledCharacter.characterMove.GetCurrentTile()
                        : null;
                    Vector2Int playerCoord = playerTile != null ? playerTile.GetCoord() : Vector2Int.zero;
                    LookDirection facing = (controlledCharacter != null && controlledCharacter.characterMove != null)
                        ? controlledCharacter.characterMove.facingDirection
                        : LookDirection.East;

                    if (GameMap.current != null)
                    {
                        List<Vector2Int> aoeList = GameMap.current.CalculateCardAoECoords(playerCoord, hoveredCoord, card.cardData, facing);
                        if (aoeList != null && aoeList.Count > 0)
                        {
                            for (int i = 0; i < aoeList.Count; i++)
                            {
                                _targetingAoeCoordBuffer.Add(aoeList[i]);
                            }
                        }
                        else
                        {
                            _targetingAoeCoordBuffer.Add(hoveredCoord);
                        }
                    }
                    else
                    {
                        _targetingAoeCoordBuffer.Add(hoveredCoord);
                    }

                    CollectCharactersInCoordsNonAlloc(_targetingAoeCoordBuffer, targetType, results);

                    if (hoveredTile.occupyingCharacter != null && _targetingAoeCoordBuffer.Contains(hoveredCoord))
                    {
                        Character tileChar = hoveredTile.occupyingCharacter;
                        if (IsCharacterMatchingTargetType(tileChar, isFriendlyTarget: false) && !ContainsCharacter(results, tileChar))
                        {
                            results.Add(tileChar);
                        }
                    }

                    if (targetType == TargetType.SingleEnemy &&
                        IsCharacterMatchingTargetType(directHoveredChar, isFriendlyTarget: false) &&
                        !ContainsCharacter(results, directHoveredChar))
                    {
                        Tile directTile = directHoveredChar.characterMove != null ? directHoveredChar.characterMove.GetCurrentTile() : null;
                        bool matchesAoe = directTile == null
                            ? _targetingAoeCoordBuffer.Contains(hoveredCoord)
                            : _targetingAoeCoordBuffer.Contains(directTile.GetCoord());

                        if (matchesAoe)
                        {
                            bool withinRange = true;
                            if (playerTile != null && directTile != null)
                            {
                                int dist = Tile.GetDistance(playerTile, directTile);
                                withinRange = dist >= card.cardData.targetMinDistance && dist <= card.cardData.targetMaxDistance;
                            }
                            if (withinRange)
                            {
                                results.Add(directHoveredChar);
                            }
                        }
                    }
                    break;
                }
        }
    }

    /// <summary>
    /// 수집된 피격·적용 예정 대상 목록을 타임라인, 사이드 HUD 및 월드 캐릭터 하이라이트에 동기화합니다.
    /// </summary>
    public void ApplyTargetingHighlights(IReadOnlyList<Character> targets, Card card)
    {
        if (BattleTimelinePanel.Instance != null)
        {
            ClearFallbackWorldTargetingHighlights();
            BattleTimelinePanel.Instance.SetTargetingHoverCharacters(targets, card);
            return;
        }

        if (BattleSideUnitHUDPanel.Instance != null)
        {
            BattleSideUnitHUDPanel.Instance.SetExternalTargetingCharacters(targets);
        }

        for (int i = _appliedWorldTargetingHighlights.Count - 1; i >= 0; i--)
        {
            Character prev = _appliedWorldTargetingHighlights[i];
            if (prev == null || !ContainsCharacter(targets, prev))
            {
                if (prev != null)
                {
                    CharacterUIController.SetTargetingHighlightFor(prev, false);
                }
                _appliedWorldTargetingHighlights.RemoveAt(i);
            }
        }

        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Character next = targets[i];
                if (next != null && !next.isDead && !ContainsCharacter(_appliedWorldTargetingHighlights, next))
                {
                    CharacterUIController.SetTargetingHighlightFor(next, true);
                    _appliedWorldTargetingHighlights.Add(next);
                }
            }
        }
    }

    /// <summary>
    /// 모든 타깃팅 피격 예정 하이라이트(타임라인, 사이드 HUD, 3D 월드/WorldUI)와 호버 캐시를 초기화합니다.
    /// </summary>
    public void ClearTargetingHighlights()
    {
        _targetingPreviewBuffer.Clear();
        _targetingAoeCoordBuffer.Clear();
        _lastHoveredCoord = new Vector2Int(-999, -999);
        _lastHoveredTargetingChar = null;

        ClearDamagePreviews();
        ClearFallbackWorldTargetingHighlights();

        if (BattleTimelinePanel.Instance != null)
        {
            BattleTimelinePanel.Instance.ClearTargetingHoverPreview();
        }

        if (BattleSideUnitHUDPanel.Instance != null)
        {
            BattleSideUnitHUDPanel.Instance.SetExternalTargetingCharacters(null);
        }

        CharacterUIController.ClearAllTargetingHighlights();
    }

    /// <summary>
    /// 지정된 대상 목록에 대해 방향 보정 및 버프/디버프가 반영된 예상 피해 미리보기를 갱신합니다.
    /// </summary>
    public void UpdateDamagePreviews(Card card, IReadOnlyList<Character> targets, Vector2Int targetPosition)
    {
        for (int i = _appliedDamagePreviewTargets.Count - 1; i >= 0; i--)
        {
            Character prev = _appliedDamagePreviewTargets[i];
            if (prev == null || !ContainsCharacter(targets, prev))
            {
                if (prev != null)
                {
                    prev.ClearDamagePreview();
                }
                _appliedDamagePreviewTargets.RemoveAt(i);
            }
        }

        if (card == null || card.cardData == null || controlledCharacter == null || controlledCharacter.isDead || targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            Character target = targets[i];
            if (target == null || target.isDead)
            {
                continue;
            }

            if (CombatFacing.TrySimulateCardDamagePreview(controlledCharacter, target, card, targetPosition, out DamagePreviewInfo preview))
            {
                target.UpdateDamagePreview(in preview);
                if (!ContainsCharacter(_appliedDamagePreviewTargets, target))
                {
                    _appliedDamagePreviewTargets.Add(target);
                }
            }
            else
            {
                target.ClearDamagePreview();
                _appliedDamagePreviewTargets.Remove(target);
            }
        }
    }

    /// <summary>
    /// 현재 표시 중인 모든 대상의 예상 피해 미리보기를 초기화합니다.
    /// </summary>
    public void ClearDamagePreviews()
    {
        _immediateHoverPreviewBuffer.Clear();

        for (int i = _appliedDamagePreviewTargets.Count - 1; i >= 0; i--)
        {
            Character c = _appliedDamagePreviewTargets[i];
            if (c != null)
            {
                c.ClearDamagePreview();
            }
        }
        _appliedDamagePreviewTargets.Clear();
    }

    /// <summary>
    /// 즉시 시전형(Directional/AreaSelf/AllEnemies/RandomEnemy) 카드 호버·선택 시 예상 피해 미리보기를 표시합니다.
    /// </summary>
    public void PreviewImmediateCardHover(Card card)
    {
        if (controlState != PlayerControlState.Normal)
        {
            return;
        }

        if (card == null || card.cardData == null || controlledCharacter == null || controlledCharacter.isDead)
        {
            ClearDamagePreviews();
            return;
        }

        TargetType targetType = card.cardData.parsedTargetType;
        if (GetTargetingStrategy(targetType).IsManualSelectionRequired || targetType == TargetType.Self || targetType == TargetType.SingleAlly || targetType == TargetType.AllAllies)
        {
            ClearDamagePreviews();
            return;
        }

        _immediateHoverPreviewBuffer.Clear();
        Tile casterTile = controlledCharacter.characterMove != null ? controlledCharacter.characterMove.GetCurrentTile() : null;
        Vector2Int casterCoord = casterTile != null ? casterTile.GetCoord() : controlledCharacter.GetGridPosition();
        LookDirection casterFacing = controlledCharacter.characterMove != null ? controlledCharacter.characterMove.facingDirection : LookDirection.East;

        if (targetType == TargetType.AllEnemies)
        {
            CollectAllMatchingCombatants(isFriendlyTarget: false, _immediateHoverPreviewBuffer);
        }
        else if (targetType == TargetType.RandomEnemy)
        {
            CollectEnemiesInRangeNonAlloc(casterTile, card.cardData.targetMinDistance, card.cardData.targetMaxDistance, _immediateHoverPreviewBuffer);
        }
        else if (targetType == TargetType.AreaSelf)
        {
            if (GameMap.current != null && card.cardData.additionalEffectAreaList != null && card.cardData.additionalEffectAreaList.Count > 0)
            {
                _targetingAoeCoordBuffer.Clear();
                List<Vector2Int> aoeCoords = GameMap.current.CalculateCardAoECoords(casterCoord, casterCoord, card.cardData, casterFacing);
                if (aoeCoords != null)
                {
                    for (int i = 0; i < aoeCoords.Count; i++)
                    {
                        _targetingAoeCoordBuffer.Add(aoeCoords[i]);
                    }
                }
                _targetingAoeCoordBuffer.Add(casterCoord);
                CollectCharactersInCoordsNonAlloc(_targetingAoeCoordBuffer, TargetType.SingleEnemy, _immediateHoverPreviewBuffer);
            }
            else
            {
                CollectEnemiesInRangeNonAlloc(casterTile, card.cardData.targetMinDistance, card.cardData.targetMaxDistance, _immediateHoverPreviewBuffer);
            }
        }
        else if (targetType == TargetType.Directional)
        {
            if (GameMap.current != null)
            {
                _targetingAoeCoordBuffer.Clear();
                List<Vector2Int> aoeCoords = GameMap.current.CalculateCardAoECoords(casterCoord, casterCoord, card.cardData, casterFacing);
                if (aoeCoords != null && aoeCoords.Count > 0)
                {
                    for (int i = 0; i < aoeCoords.Count; i++)
                    {
                        _targetingAoeCoordBuffer.Add(aoeCoords[i]);
                    }
                    CollectCharactersInCoordsNonAlloc(_targetingAoeCoordBuffer, TargetType.SingleEnemy, _immediateHoverPreviewBuffer);
                }
            }
        }

        UpdateDamagePreviews(card, _immediateHoverPreviewBuffer, casterCoord);
    }

    /// <summary>
    /// 즉시 시전형 카드 호버 종료 시 표시된 예상 피해 미리보기를 숨깁니다.
    /// </summary>
    public void ClearImmediateCardHoverPreview()
    {
        if (controlState == PlayerControlState.TargetSelection)
        {
            return;
        }

        ClearDamagePreviews();
    }

    private void RefreshKeyboardSelectedImmediatePreview()
    {
        if (controlState == PlayerControlState.Normal && _keyboardSelectedCard != null)
        {
            PreviewImmediateCardHover(_keyboardSelectedCard);
        }
    }

    private void CollectEnemiesInRangeNonAlloc(Tile originTile, int minDistance, int maxDistance, List<Character> results)
    {
        if (originTile == null || results == null) return;

        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            IReadOnlyList<ICharacterController> combatants = RunManager.instance.battleManager.GetAllCombatants();
            for (int i = 0; i < combatants.Count; i++)
            {
                Character candidate = combatants[i]?.controlledCharacter;
                if (!IsCharacterMatchingTargetType(candidate, isFriendlyTarget: false)) continue;

                Tile candidateTile = candidate.characterMove != null ? candidate.characterMove.GetCurrentTile() : null;
                if (candidateTile == null) continue;

                int dist = Tile.GetDistance(originTile, candidateTile);
                if (dist >= minDistance && dist <= maxDistance && !ContainsCharacter(results, candidate))
                {
                    results.Add(candidate);
                }
            }
            return;
        }

        if (GameMap.current != null)
        {
            for (int c = 0; c < GameMap.current.mapColumn; c++)
            {
                for (int r = 0; r < GameMap.current.mapRow; r++)
                {
                    Tile tile = GameMap.current.GetTile(c, r);
                    Character candidate = tile != null ? tile.occupyingCharacter : null;
                    if (!IsCharacterMatchingTargetType(candidate, isFriendlyTarget: false)) continue;

                    int dist = Tile.GetDistance(originTile, tile);
                    if (dist >= minDistance && dist <= maxDistance && !ContainsCharacter(results, candidate))
                    {
                        results.Add(candidate);
                    }
                }
            }
        }
    }

    private void ClearFallbackWorldTargetingHighlights()
    {
        for (int i = 0; i < _appliedWorldTargetingHighlights.Count; i++)
        {
            Character c = _appliedWorldTargetingHighlights[i];
            if (c != null)
            {
                CharacterUIController.SetTargetingHighlightFor(c, false);
            }
        }
        _appliedWorldTargetingHighlights.Clear();
    }

    private void CollectAllMatchingCombatants(bool isFriendlyTarget, List<Character> results)
    {
        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            IReadOnlyList<ICharacterController> combatants = RunManager.instance.battleManager.GetAllCombatants();
            for (int i = 0; i < combatants.Count; i++)
            {
                ICharacterController combatant = combatants[i];
                Character candidate = combatant != null ? combatant.controlledCharacter : null;
                if (IsCharacterMatchingTargetType(candidate, isFriendlyTarget) && !ContainsCharacter(results, candidate))
                {
                    results.Add(candidate);
                }
            }
            return;
        }

        if (GameMap.current != null)
        {
            for (int c = 0; c < GameMap.current.mapColumn; c++)
            {
                for (int r = 0; r < GameMap.current.mapRow; r++)
                {
                    Tile tile = GameMap.current.GetTile(c, r);
                    Character candidate = tile != null ? tile.occupyingCharacter : null;
                    if (IsCharacterMatchingTargetType(candidate, isFriendlyTarget) && !ContainsCharacter(results, candidate))
                    {
                        results.Add(candidate);
                    }
                }
            }
        }
    }

    private bool IsCharacterMatchingTargetType(Character candidate, bool isFriendlyTarget)
    {
        if (candidate == null || candidate.isDead) return false;

        if (isFriendlyTarget)
        {
            return controlledCharacter == null || controlledCharacter.IsFriendlyTo(candidate);
        }

        if (ReferenceEquals(candidate, controlledCharacter)) return false;
        return controlledCharacter == null || controlledCharacter.IsHostileTo(candidate);
    }

    private static bool ContainsCharacter(IReadOnlyList<Character> list, Character target)
    {
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], target))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 지정된 타일 좌표 목록에 위치한 모든 적 캐릭터를 수집합니다.
    /// </summary>
    public List<Character> CollectCharactersInCoords(HashSet<Vector2Int> targetCoords)
    {
        List<Character> foundCharacters = new List<Character>();
        CollectCharactersInCoordsNonAlloc(targetCoords, TargetType.SingleEnemy, foundCharacters);
        return foundCharacters;
    }

    private void HandleGlobalClick(Vector2 pos)
    {
        // UI 요소 클릭 시 월드 조작 무시
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (controlledCharacter == null || controlledCharacter.isDead || controlledCharacter.currentState != CharacterState.Idle || (ActionQueueManager.HasInstance && ActionQueueManager.Instance.IsBusy))
        {
            return;
        }

        Tile targetTile = GetTileFromScreenPosition(pos);
        string hitObjectName = targetTile != null ? targetTile.name : "None";
        Vector3 hitPoint = targetTile != null ? targetTile.transform.position : Vector3.zero;

        // 상세 디버그 로그 출력
        Tile playerTile = (controlledCharacter != null && controlledCharacter.characterMove != null)
            ? controlledCharacter.characterMove.GetCurrentTile()
            : null;
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

        if (controlState == PlayerControlState.Normal)
        {
            Character clickedChar = TryGetCharacterFromScreenPosition(pos, targetTile);
            if (clickedChar != null && !ReferenceEquals(clickedChar, controlledCharacter))
            {
                TryRotatePlayerTowardCharacter(clickedChar);
                return;
            }

            if (targetTile != null)
            {
                // 일반 모드: 클릭 시 해당 타일 방향으로 회전
                Vector3 diff = targetTile.transform.position - controlledCharacter.transform.position;
                LookDirection targetDir = CharacterMove.GetDirectionFromVector(diff);
                if (controlledCharacter != null && controlledCharacter.characterMove != null)
                {
                    controlledCharacter.characterMove.LookAtTile(targetTile);
                    RefreshKeyboardSelectedImmediatePreview();
                }
                Debug.Log($"[PlayerBattleController] 바라보기 회전: {targetDir} (diff: ({diff.x:F2}, {diff.z:F2}))");
            }
            return;
        }

        if (controlState == PlayerControlState.TargetSelection && activeCardForTargeting != null)
        {
            TargetType snapTargetType = activeCardForTargeting.cardData != null
                ? activeCardForTargeting.cardData.parsedTargetType
                : TargetType.SingleEnemy;
            targetTile = ResolveTargetingTileWithCharacterSnap(pos, targetTile, snapTargetType, out _);
        }

        if (targetTile != null)
        {
            switch (controlState)
            {
                case PlayerControlState.Move:
                    // 이동 모드: 이동 시작 직후 기존 이동 범위 인디케이터를 정리하고, 이동 완료 시점에 연속 이동 유지 여부를 판정
                    if (playerMove != null && controlledCharacter != null && !controlledCharacter.isDead && controlledCharacter.currentState == CharacterState.Idle)
                    {
                        int moveToken = ++_moveRequestToken;
                        bool postStartReady = false;
                        bool pendingSyncCompletion = false;

                        if (playerMove.ExecuteMoveToTile(targetTile, () =>
                        {
                            if (postStartReady)
                            {
                                HandleBattleMoveCompleted(moveToken);
                            }
                            else
                            {
                                pendingSyncCompletion = true;
                            }
                        }))
                        {
                            playerMove.ClearCanMoveTiles();
                            if (_battleUI != null)
                            {
                                _battleUI.RefreshMoveStats();
                            }

                            postStartReady = true;
                            if (pendingSyncCompletion)
                            {
                                HandleBattleMoveCompleted(moveToken);
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[PlayerBattleController] 유효하지 않은 이동 타일이거나 이동할 수 없습니다. (이동 모드 유지)");
                        }
                    }
                    break;

                case PlayerControlState.TargetSelection:
                    // 타겟팅 모드: 사거리 유효성 검사 후 최종 시전
                    if (activeCardForTargeting != null)
                    {
                        Tile currentTile = (controlledCharacter != null && controlledCharacter.characterMove != null)
                            ? controlledCharacter.characterMove.GetCurrentTile()
                            : null;
                        if (currentTile != null)
                        {
                            int distance = Tile.GetDistance(currentTile, targetTile);
                            int minDistance = activeCardForTargeting.cardData.targetMinDistance;
                            int maxDistance = activeCardForTargeting.cardData.targetMaxDistance;

                            if (distance >= minDistance && distance <= maxDistance)
                            {
                                TargetType targetType = activeCardForTargeting.cardData != null ? activeCardForTargeting.cardData.parsedTargetType : TargetType.SingleEnemy;
                                ITargetingStrategy strategy = GetTargetingStrategy(targetType);

                                if (strategy.TryCollectTargetsOnTileClicked(this, activeCardForTargeting, targetTile, out List<Character> targets, out Vector2Int targetPos))
                                {
                                    UseCard(activeCardForTargeting, targets, targetPos);

                                    activeCardForTargeting = null;

                                    if (UIManager.instance != null)
                                    {
                                        UIManager.instance.ClearEffectAreaTiles();
                                    }
                                    else if (GameMap.current != null)
                                    {
                                        GameMap.current.ClearCardRange();
                                    }
                                    controlState = PlayerControlState.Normal;
                                    ClearTargetingHighlights();
                                    NotifyTargetingStateChanged(null);
                                }
                                else if (targetType == TargetType.MultiEnemy && _multiSelectedTargets.Count > 0)
                                {
                                    ApplyTargetingHighlights(_multiSelectedTargets, activeCardForTargeting);
                                    UpdateDamagePreviews(activeCardForTargeting, _multiSelectedTargets, targetTile.GetCoord());
                                }
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

