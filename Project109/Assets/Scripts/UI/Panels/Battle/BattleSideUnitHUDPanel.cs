using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 화면 좌측(플레이어 팀) 및 우측(전투 중인 모든 적 팀, 1-A안)에
/// 유닛 상세 상태 카드를 상시 표시하고, 타임라인·월드 캐릭터와 3방향 상호 하이라이트를 동기화하는 HUD 패널입니다.
/// </summary>
public class BattleSideUnitHUDPanel : UIPanelBase
{
    public static BattleSideUnitHUDPanel Instance { get; private set; }

    [Header("Team Container References")]
    [SerializeField] private RectTransform leftPlayerContainer;
    [SerializeField] private RectTransform rightEnemyContainer;
    [SerializeField] private BattleSideUnitCardUI unitCardPrefab;

    private readonly List<BattleSideUnitCardUI> _activeLeftCards = new List<BattleSideUnitCardUI>(4);
    private readonly List<BattleSideUnitCardUI> _activeRightCards = new List<BattleSideUnitCardUI>(8);
    private readonly List<BattleSideUnitCardUI> _cardPool = new List<BattleSideUnitCardUI>(8);
    private readonly List<BattleSideUnitCardUI> _nextCardsScratch = new List<BattleSideUnitCardUI>(8);

    private BattleManager _battleManager;
    private bool _isEventsSubscribed;
    private bool _isDirty;

    private BattleSideUnitCardUI _hoveredCard;
    private Character _externalHoveredCharacter;
    private Character _lastFallbackFocusedWorldChar;

    public override bool DestroyOnClose => false;
    public override bool CanCloseByCancel => false;

    public IReadOnlyList<BattleSideUnitCardUI> ActiveLeftCards => _activeLeftCards;
    public IReadOnlyList<BattleSideUnitCardUI> ActiveRightCards => _activeRightCards;

    private void Awake()
    {
        uiLayerType = UILayerType.Top;
        blockWorldInput = false;

        if (Instance == null || Instance == this)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (leftPlayerContainer == null)
        {
            Debug.LogError("[BattleSideUnitHUDPanel] leftPlayerContainer가 바인딩되지 않았습니다! BattleSideUnitHUDPanel 프리팹 설정을 확인하세요.", this);
        }
        if (rightEnemyContainer == null)
        {
            Debug.LogError("[BattleSideUnitHUDPanel] rightEnemyContainer가 바인딩되지 않았습니다! BattleSideUnitHUDPanel 프리팹 설정을 확인하세요.", this);
        }
        if (unitCardPrefab == null)
        {
            Debug.LogError("[BattleSideUnitHUDPanel] unitCardPrefab이 바인딩되지 않았습니다! BattleSideUnitHUDPanel 프리팹 설정을 확인하세요.", this);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_battleManager != null)
        {
            SubscribeBattleEvents();
            RebuildRoster();
        }
    }

    protected override void OnDisable()
    {
        ClearAllHoverState();
        UnsubscribeBattleEvents();
        base.OnDisable();
    }

    private void OnDestroy()
    {
        ClearAllHoverState();
        ReturnAllCardsToPool();
        UnsubscribeBattleEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (_battleManager != null)
        {
            SubscribeBattleEvents();
            RebuildRoster();
        }
    }

    public override void OnClose()
    {
        ClearAllHoverState();
        ReturnAllCardsToPool();
        UnsubscribeBattleEvents();
        base.OnClose();
    }

    /// <summary>
    /// 현재 전투의 BattleManager를 바인딩하고 팀 로스터 이벤트를 구독합니다.
    /// </summary>
    public void BindBattle(BattleManager bm)
    {
        if (ReferenceEquals(_battleManager, bm))
        {
            if (_battleManager != null && isActiveAndEnabled)
            {
                SubscribeBattleEvents();
                RebuildRoster();
            }
            return;
        }

        ClearAllHoverState();
        UnsubscribeBattleEvents();
        _battleManager = bm;

        if (_battleManager != null && isActiveAndEnabled)
        {
            SubscribeBattleEvents();
            RebuildRoster();
        }
        else if (_battleManager == null)
        {
            _isDirty = false;
            ReturnAllCardsToPool();
        }
    }

    /// <summary>
    /// 다음 LateUpdate에서 로스터를 재동기화하도록 Dirty 플래그를 설정합니다.
    /// </summary>
    public void MarkDirty()
    {
        _isDirty = true;
    }

    /// <summary>
    /// 플레이어 팀(좌측)과 살아있는 적 팀(우측)의 상태 카드 목록을 즉시 재구성합니다.
    /// 기존 생존 유닛의 카드 인스턴스를 재사용하여 깜빡임 및 호버 이탈을 방지합니다.
    /// </summary>
    public void RebuildRoster()
    {
        _isDirty = false;

        if (_battleManager == null)
        {
            ReturnAllCardsToPool();
            return;
        }

        RebuildTeamCards(_battleManager.PlayerTeam, leftPlayerContainer, _activeLeftCards, isPlayerSide: true);
        RebuildTeamCards(_battleManager.EnemyTeam, rightEnemyContainer, _activeRightCards, isPlayerSide: false);
        RefreshCardHighlights();
    }

    /// <summary>
    /// 사이드 유닛 카드에 마우스 포인터가 진입했을 때 3방향 하이라이트를 전파합니다.
    /// </summary>
    public void NotifyCardPointerEnter(BattleSideUnitCardUI card)
    {
        _hoveredCard = card;
        RefreshCardHighlights();

        Character hoveredChar = card != null ? card.BoundCharacter : null;
        PushHoverToExternalSystems(hoveredChar);
    }

    /// <summary>
    /// 사이드 유닛 카드에서 마우스 포인터가 이탈했을 때 3방향 하이라이트를 갱신합니다.
    /// </summary>
    public void NotifyCardPointerExit(BattleSideUnitCardUI card)
    {
        if (_hoveredCard == card)
        {
            _hoveredCard = null;
            RefreshCardHighlights();
            PushHoverToExternalSystems(null);
        }
    }

    /// <summary>
    /// 타임라인 슬롯이나 월드 캐릭터 등 외부 호버 대상을 사이드 카드 하이라이트에 반영합니다.
    /// </summary>
    public void SetExternalHoverCharacter(Character target)
    {
        if (_externalHoveredCharacter == target) return;

        _externalHoveredCharacter = target;
        RefreshCardHighlights();
    }

    private void LateUpdate()
    {
        if (_isDirty && _battleManager != null)
        {
            RebuildRoster();
        }

        RefreshEffectDurations();
    }

    private void RefreshEffectDurations()
    {
        for (int i = 0; i < _activeLeftCards.Count; i++)
        {
            if (_activeLeftCards[i] != null)
            {
                _activeLeftCards[i].RefreshEffectDurations();
            }
        }

        for (int i = 0; i < _activeRightCards.Count; i++)
        {
            if (_activeRightCards[i] != null)
            {
                _activeRightCards[i].RefreshEffectDurations();
            }
        }
    }

    private void RebuildTeamCards(
        IReadOnlyList<ICharacterController> team,
        RectTransform container,
        List<BattleSideUnitCardUI> activeCards,
        bool isPlayerSide)
    {
        if (container == null) return;

        _nextCardsScratch.Clear();
        ICharacterController currentTurn = _battleManager != null ? _battleManager.currentTurnController : null;

        if (team != null)
        {
            for (int i = 0; i < team.Count; i++)
            {
                ICharacterController controller = team[i];
                if (controller == null || controller.controlledCharacter == null || controller.controlledCharacter.isDead)
                {
                    continue;
                }

                BattleSideUnitCardUI card = TakeMatchingActiveCard(activeCards, controller.controlledCharacter);
                if (card == null)
                {
                    card = AcquireCardFromPool(container);
                }

                if (card == null) continue;

                if (card.transform.parent != container)
                {
                    card.transform.SetParent(container, false);
                }
                card.gameObject.SetActive(true);
                card.Bind(controller, isPlayerSide);
                card.transform.SetSiblingIndex(_nextCardsScratch.Count);
                card.SetCurrentTurn(ReferenceEquals(currentTurn, controller));

                _nextCardsScratch.Add(card);
            }
        }

        // 사망/제거된 유닛 카드는 풀에 반납
        for (int i = 0; i < activeCards.Count; i++)
        {
            BattleSideUnitCardUI leftover = activeCards[i];
            if (ReferenceEquals(_hoveredCard, leftover))
            {
                _hoveredCard = null;
            }
            ReturnCardToPool(leftover);
        }

        activeCards.Clear();
        activeCards.AddRange(_nextCardsScratch);
        _nextCardsScratch.Clear();
    }

    private static BattleSideUnitCardUI TakeMatchingActiveCard(List<BattleSideUnitCardUI> activeCards, Character targetChar)
    {
        for (int i = 0; i < activeCards.Count; i++)
        {
            BattleSideUnitCardUI candidate = activeCards[i];
            if (candidate != null && candidate.BoundCharacter == targetChar)
            {
                activeCards.RemoveAt(i);
                return candidate;
            }
        }
        return null;
    }

    private BattleSideUnitCardUI AcquireCardFromPool(RectTransform container)
    {
        for (int i = _cardPool.Count - 1; i >= 0; i--)
        {
            BattleSideUnitCardUI pooled = _cardPool[i];
            _cardPool.RemoveAt(i);
            if (pooled != null)
            {
                if (pooled.transform.parent != container)
                {
                    pooled.transform.SetParent(container, false);
                }
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        if (unitCardPrefab == null || container == null)
        {
            return null;
        }

        BattleSideUnitCardUI inst = Instantiate(unitCardPrefab, container, false);
        UIManager.SetLayerRecursively(inst.gameObject, gameObject.layer);
        inst.gameObject.SetActive(true);
        return inst;
    }

    private void ReturnCardToPool(BattleSideUnitCardUI card)
    {
        if (card == null) return;
        card.Unbind();
        if (card.gameObject != null)
        {
            card.gameObject.SetActive(false);
        }
        _cardPool.Add(card);
    }

    private void ReturnAllCardsToPool()
    {
        for (int i = 0; i < _activeLeftCards.Count; i++)
        {
            ReturnCardToPool(_activeLeftCards[i]);
        }
        _activeLeftCards.Clear();

        for (int i = 0; i < _activeRightCards.Count; i++)
        {
            ReturnCardToPool(_activeRightCards[i]);
        }
        _activeRightCards.Clear();
    }

    private void RefreshCardHighlights()
    {
        Character hoveredCardChar = _hoveredCard != null ? _hoveredCard.BoundCharacter : null;

        RefreshTeamCardHighlights(_activeLeftCards, hoveredCardChar);
        RefreshTeamCardHighlights(_activeRightCards, hoveredCardChar);
    }

    private void RefreshTeamCardHighlights(List<BattleSideUnitCardUI> cards, Character hoveredCardChar)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            BattleSideUnitCardUI card = cards[i];
            if (card == null) continue;

            Character c = card.BoundCharacter;
            bool shouldHighlight = (card == _hoveredCard)
                || (hoveredCardChar != null && c == hoveredCardChar)
                || (_externalHoveredCharacter != null && c == _externalHoveredCharacter);

            card.SetHighlighted(shouldHighlight);
        }
    }

    private void PushHoverToExternalSystems(Character hoveredChar)
    {
        if (BattleTimelinePanel.Instance != null)
        {
            BattleTimelinePanel.Instance.SetExternalHoverCharacter(hoveredChar);
            return;
        }

        // 타임라인 패널이 없는 단독 테스트/비활성 환경에서는 월드 UI 포커스를 직접 동기화
        if (_lastFallbackFocusedWorldChar != hoveredChar)
        {
            if (_lastFallbackFocusedWorldChar != null)
            {
                CharacterUIController.SetTimelineHighlightFor(_lastFallbackFocusedWorldChar, false);
            }
            if (hoveredChar != null)
            {
                CharacterUIController.SetTimelineHighlightFor(hoveredChar, true);
            }
            _lastFallbackFocusedWorldChar = hoveredChar;
        }
    }

    private void ClearAllHoverState()
    {
        bool hadCardHover = _hoveredCard != null;
        _hoveredCard = null;
        _externalHoveredCharacter = null;

        if (_lastFallbackFocusedWorldChar != null)
        {
            CharacterUIController.SetTimelineHighlightFor(_lastFallbackFocusedWorldChar, false);
            _lastFallbackFocusedWorldChar = null;
        }

        if (hadCardHover && BattleTimelinePanel.Instance != null)
        {
            BattleTimelinePanel.Instance.SetExternalHoverCharacter(null);
        }

        RefreshCardHighlights();
    }

    private void SubscribeBattleEvents()
    {
        if (_battleManager == null || _isEventsSubscribed) return;

        _battleManager.OnTimelineDirty -= HandleTimelineDirty;
        _battleManager.OnTimelineDirty += HandleTimelineDirty;
        _isEventsSubscribed = true;
    }

    private void UnsubscribeBattleEvents()
    {
        if (_battleManager != null && _isEventsSubscribed)
        {
            _battleManager.OnTimelineDirty -= HandleTimelineDirty;
        }
        _isEventsSubscribed = false;
    }

    private void HandleTimelineDirty()
    {
        MarkDirty();
    }
}
