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

    public void Dispose()
    {
        OnBattleEnd();

        if (PlayerInputController.HasInstance)
        {
            PlayerInputController.instance.OnTouchClickEvent -= HandleGlobalClick;
            PlayerInputController.instance.OnPointerHoverEvent -= HandlePointerHover;
            PlayerInputController.instance.OnCancelEvent -= HandleCancelInput;
        }

        OnCardPlayRejected = null;
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
        }
    }

    public void OnBattleEnd()
    {
        _moveRequestToken++;
        if (playerMove != null)
        {
            playerMove.ClearCanMoveTiles();
        }
        controlState = PlayerControlState.Normal;

        if (_controlledCharacter != null)
        {
            _controlledCharacter.eventBus.Remove<IOnBattleEnd>(this);
        }

        if (_battleUI != null)
        {
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
            return;
        }

        // 2. 카드 드로우 (BattleDeck.OnHandChanged를 통해 BattleHandPanel 자동 갱신)
        battleDeck.DrawCards(GetTurnDrawCount());

        // 3. 이번 턴 보정치 리셋 (다음 턴에는 기본값으로 자동 원복)
        bonusDrawThisTurn = 0;

        // 4. 전역 클릭 및 취소 이벤트 구독
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent += HandleGlobalClick;
            PlayerInputController.instance.OnPointerHoverEvent += HandlePointerHover;
            PlayerInputController.instance.OnCancelEvent += HandleCancelInput;
        }
        controlState = PlayerControlState.Normal;
        _lastHoveredCoord = new Vector2Int(-999, -999);
        if (_battleUI != null)
        {
            _battleUI.SetTurnActive(true);
            _battleUI.RefreshMoveStats();
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
        if (PlayerInputController.HasInstance)
        {
            PlayerInputController.instance.OnTouchClickEvent -= HandleGlobalClick;
            PlayerInputController.instance.OnPointerHoverEvent -= HandlePointerHover;
            PlayerInputController.instance.OnCancelEvent -= HandleCancelInput;
        }
        controlState = PlayerControlState.Normal;
        _lastHoveredCoord = new Vector2Int(-999, -999);
        bonusDrawThisTurn = 0;

        if (BattleTimelinePanel.Instance != null)
        {
            BattleTimelinePanel.Instance.ClearTargetingHoverPreview();
        }

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
        if (controlledCharacter == null || controlledCharacter.currentState != CharacterState.Idle)
        {
            return;
        }

        _moveRequestToken++;

        // 턴 종료 요청 즉시 이동 가능 타일 인디케이터 클리어 및 UI 버튼 비활성화 (반응성 및 중복 클릭 방지)
        if (playerMove != null)
        {
            playerMove.ClearCanMoveTiles();
        }
        if (_battleUI != null)
        {
            _battleUI.SetTurnActive(false);
        }

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
            _multiSelectedTargets.Clear();
            controlState = PlayerControlState.TargetSelection;
            activeCardForTargeting = card;

            if (UIManager.instance != null)
            {
                UIManager.instance.UpdateEffectAreaUI(casterPos, card);
            }

            if (BattleTimelinePanel.Instance != null)
            {
                BattleTimelinePanel.Instance.SetTargetingHoverCharacter(null, activeCardForTargeting);
            }

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
    /// 현재 배틀 컨트롤러가 이동 또는 타깃팅 선택 등 ESC/우클릭으로 취소 가능한 하위 조작 상태인지 반환합니다.
    /// </summary>
    public bool HasActiveBattleCancelTarget => controlState != PlayerControlState.Normal;

    /// <summary>
    /// 현재 프레임에 배틀 타깃팅/이동 취소가 이미 소비되었는지 반환합니다.
    /// </summary>
    public bool IsBattleCancelConsumedThisFrame => _lastBattleCancelFrame == Time.frameCount;

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
    /// 현재 조작 상태(이동 모드, 타겟 모드) 취소
    /// </summary>
    public void CancelCurrentState()
    {
        _moveRequestToken++;
        if (controlState == PlayerControlState.Normal)
        {
            return;
        }
        _lastBattleCancelFrame = Time.frameCount;

        Debug.Log($"[PlayerBattleController] 조작 취소. 이전 상태: {controlState}");

        if (controlState == PlayerControlState.TargetSelection)
        {
            _multiSelectedTargets.Clear();
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

            if (BattleTimelinePanel.Instance != null)
            {
                BattleTimelinePanel.Instance.ClearTargetingHoverPreview();
            }
        }
        else if (controlState == PlayerControlState.Move)
        {
            if (playerMove != null)
            {
                playerMove.ClearCanMoveTiles();
            }
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
        _moveRequestToken++;
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
    /// 화면 스크린 좌표로부터 캐릭터(플레이어/적) 콜라이더를 우선 레이캐스트하고, 미스 시 타일 점유 캐릭터로 폴백 감지합니다.
    /// </summary>
    private Character TryGetCharacterFromScreenPosition(Vector2 pos, Tile fallbackTile)
    {
        if (Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(pos);
            int charMask = LayerMask.GetMask("Player", "Enemy");
            if (charMask != 0 && Physics.Raycast(ray, out RaycastHit hit, 10000f, charMask, QueryTriggerInteraction.Collide))
            {
                Character hitChar = hit.collider.GetComponentInParent<Character>();
                if (hitChar != null && !hitChar.isDead)
                {
                    return hitChar;
                }
            }
        }

        if (fallbackTile != null && fallbackTile.occupyingCharacter != null && !fallbackTile.occupyingCharacter.isDead)
        {
            return fallbackTile.occupyingCharacter;
        }

        return null;
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
                LookDirection facing = (controlledCharacter != null && controlledCharacter.characterMove != null)
                    ? controlledCharacter.characterMove.facingDirection
                    : LookDirection.Down;
                GameMap.current.ShowCardAoE(playerCoord, hoveredCoord, activeCardForTargeting.cardData, facing);

                if (BattleTimelinePanel.Instance != null)
                {
                    Character hoveredChar = TryGetCharacterFromScreenPosition(screenPos, hoveredTile);
                    Character validTarget = null;
                    if (hoveredChar != null && !hoveredChar.isDead && controlledCharacter != null)
                    {
                        TargetType targetType = activeCardForTargeting.cardData.parsedTargetType;
                        if (targetType == TargetType.SingleAlly || targetType == TargetType.Self)
                        {
                            if (controlledCharacter.IsFriendlyTo(hoveredChar))
                            {
                                validTarget = hoveredChar;
                            }
                        }
                        else if (controlledCharacter.IsHostileTo(hoveredChar))
                        {
                            validTarget = hoveredChar;
                        }
                    }
                    BattleTimelinePanel.Instance.SetTargetingHoverCharacter(validTarget, activeCardForTargeting);
                }
                return;
            }
        }

        // 사거리 밖이거나 타일이 없으면 사거리만 유지 표시
        if (GameMap.current != null)
        {
            GameMap.current.ShowCardRange(playerCoord, activeCardForTargeting.cardData);
        }
        if (BattleTimelinePanel.Instance != null)
        {
            BattleTimelinePanel.Instance.SetTargetingHoverCharacter(null, activeCardForTargeting);
        }
    }

    /// <summary>
    /// 지정된 타일 좌표 목록에 위치한 모든 적 캐릭터를 수집합니다.
    /// </summary>
    public List<Character> CollectCharactersInCoords(HashSet<Vector2Int> targetCoords)
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

                // 시전자 자신 및 사망한 대상은 타깃에서 제외, 적대 관계인 캐릭터만 수집
                if (ReferenceEquals(combatant.controlledCharacter, controlledCharacter)) continue;
                if (combatant.controlledCharacter.isDead) continue;
                if (controlledCharacter != null && !controlledCharacter.IsHostileTo(combatant.controlledCharacter)) continue;

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

        if (controlledCharacter == null || controlledCharacter.isDead || controlledCharacter.currentState != CharacterState.Idle || ActionQueueManager.Instance.IsBusy)
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
            if (targetTile != null)
            {
                // 일반 모드: 클릭 시 해당 타일 방향으로 회전
                Vector3 diff = targetTile.transform.position - controlledCharacter.transform.position;
                LookDirection targetDir = CharacterMove.GetDirectionFromVector(diff);
                if (controlledCharacter != null && controlledCharacter.characterMove != null)
                {
                    controlledCharacter.characterMove.LookAtTile(targetTile);
                }
                Debug.Log($"[PlayerBattleController] 바라보기 회전: {targetDir} (diff: ({diff.x:F2}, {diff.z:F2}))");
            }
            return;
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
                                    if (BattleTimelinePanel.Instance != null)
                                    {
                                        BattleTimelinePanel.Instance.ClearTargetingHoverPreview();
                                    }
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

