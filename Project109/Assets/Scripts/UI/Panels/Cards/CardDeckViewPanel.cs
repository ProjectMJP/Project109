using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 단일 통합 카드 목록 및 그리드 선택 UI 패널 (슬레이 더 스파이어의 GridCardSelectScreen 모델).
/// 마스터 덱, 뽑을 덱, 버린 덱, 소멸 덱 열람(ViewOnly) 및 카드 제거/선택(Single/MultiSelect), 강화(UpgradeSelect)를 통합 처리합니다.
/// </summary>
public class CardDeckViewPanel : UIPanelBase, IOnAddCard, IOnRemoveCard, IOnCardUpgrade, IOnCardsRefreshed, IOnCardMasteryUpgrade
{
    [Header("UI References")]
    public Transform contentTransform;
    public TextMeshProUGUI titleText;
    public Button confirmButton;
    public TextMeshProUGUI confirmButtonText;
    public Button closeButton;

    [Header("Selection State")]
    public CardViewMode currentMode { get; private set; } = CardViewMode.ViewOnly;
    public int targetSelectCount { get; private set; } = 1;
    public readonly List<Card> selectedCards = new List<Card>();

    private readonly Dictionary<int, GameObject> activeCardUIs = new Dictionary<int, GameObject>();
    private readonly List<Card> currentDisplayedCards = new List<Card>();

    private Action<List<Card>> _onConfirmCallback;
    private Action _onCancelCallback;
    private bool _isMasterDeckMode = false;
    private CardDetailPanel _cardDetailHandler;

    private Player GetPlayer() => GameSceneManager.instance != null ? GameSceneManager.instance.player : null;

    private void Awake()
    {
        // 인스펙터 미할당 시 자식 오브젝트 자동 탐색 (Graceful Fallback)
        ResolveComponentReferences();

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        // 플레이어 이벤트 버스 등록
        RegisterPlayerEvents();

        gameObject.SetActive(false);
    }

    private void ResolveComponentReferences()
    {
        if (contentTransform == null)
        {
            Transform content = transform.Find("CardList/Viewport/Content");
            if (content != null)
            {
                contentTransform = content;
            }
        }

        if (titleText == null)
        {
            Transform titleTr = transform.Find("TitleText");
            if (titleTr != null)
            {
                titleText = titleTr.GetComponent<TextMeshProUGUI>();
            }
        }

        if (confirmButton == null)
        {
            Transform confirmTr = transform.Find("ConfirmButton");
            if (confirmTr != null)
            {
                confirmButton = confirmTr.GetComponent<Button>();
            }
        }

        if (confirmButton != null && confirmButtonText == null)
        {
            confirmButtonText = confirmButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (closeButton == null)
        {
            Transform closeTr = transform.Find("CloseUI Button");
            if (closeTr != null)
            {
                closeButton = closeTr.GetComponent<Button>();
            }
        }
    }

    private void RegisterPlayerEvents()
    {
        var p = GetPlayer();
        if (p != null && p.eventBus != null)
        {
            p.eventBus.Add<IOnAddCard>(this);
            p.eventBus.Add<IOnRemoveCard>(this);
            p.eventBus.Add<IOnCardUpgrade>(this);
            p.eventBus.Add<IOnCardsRefreshed>(this);
            p.eventBus.Add<IOnCardMasteryUpgrade>(this);
        }
    }

    private void UnregisterPlayerEvents()
    {
        var p = GetPlayer();
        if (p != null && p.eventBus != null)
        {
            p.eventBus.Remove<IOnAddCard>(this);
            p.eventBus.Remove<IOnRemoveCard>(this);
            p.eventBus.Remove<IOnCardUpgrade>(this);
            p.eventBus.Remove<IOnCardsRefreshed>(this);
            p.eventBus.Remove<IOnCardMasteryUpgrade>(this);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    private void OnDestroy()
    {
        UnregisterPlayerEvents();
        ClearAllCardUIs();
    }

    #region Public Open APIs

    /// <summary>
    /// 임의의 카드 목록을 전달받아 통합 카드 목록/선택 UI를 엽니다.
    /// </summary>
    /// <param name="title">상단 제목 문구 (예: "뽑을 카드 더미", "제거할 카드 선택")</param>
    /// <param name="cards">표시할 카드 목록</param>
    /// <param name="mode">열람/선택 모드</param>
    /// <param name="targetSelectCount">선택해야 하는 목표 카드 수량 (SingleSelect는 1)</param>
    /// <param name="onConfirm">확인 시 호출될 콜백</param>
    /// <param name="onCancel">취소 시 호출될 콜백</param>
    public void Open(
        string title,
        IReadOnlyList<Card> cards,
        CardViewMode mode = CardViewMode.ViewOnly,
        int targetSelectCount = 1,
        Action<List<Card>> onConfirm = null,
        Action onCancel = null)
    {
        _isMasterDeckMode = false;
        currentMode = mode;
        this.targetSelectCount = Mathf.Max(1, targetSelectCount);
        _onConfirmCallback = onConfirm;
        _onCancelCallback = onCancel;

        selectedCards.Clear();

        if (titleText != null)
        {
            titleText.text = title;
        }

        UpdateConfirmButtonUI();

        currentDisplayedCards.Clear();
        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    currentDisplayedCards.Add(cards[i]);
                }
            }
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        RefreshAllCardUIs();
    }

    /// <summary>
    /// 플레이어의 마스터 덱을 기본 ViewOnly 모드로 엽니다.
    /// 마스터 덱 카드 추가/제거/강화 이벤트를 실시간 반영합니다.
    /// </summary>
    public void OpenMasterDeck()
    {
        _isMasterDeckMode = true;
        var p = GetPlayer();
        var cards = (p != null && p.deck != null) ? p.deck.GetCards() : new List<Card>();
        Open("전체 덱", cards, CardViewMode.ViewOnly);
        _isMasterDeckMode = true; // Open() 내부에서 false로 초기화되므로 다시 true 설정
    }

    /// <summary>
    /// 전투 중 뽑을 카드 더미(Draw Pile)를 열람합니다.
    /// </summary>
    public void OpenDrawPile(BattleDeck battleDeck)
    {
        if (battleDeck == null) return;
        Open($"뽑을 카드 ({battleDeck.drawPileList.Count})", battleDeck.drawPileList, CardViewMode.ViewOnly);
    }

    /// <summary>
    /// 전투 중 버린 카드 더미(Discard Pile)를 열람합니다.
    /// </summary>
    public void OpenDiscardPile(BattleDeck battleDeck)
    {
        if (battleDeck == null) return;
        Open($"버린 카드 ({battleDeck.discardPileList.Count})", battleDeck.discardPileList, CardViewMode.ViewOnly);
    }

    /// <summary>
    /// 전투 중 소멸된 카드 더미(Exhaust Pile)를 열람합니다.
    /// </summary>
    public void OpenExhaustPile(BattleDeck battleDeck)
    {
        if (battleDeck == null) return;
        Open($"소멸된 카드 ({battleDeck.exhaustPileList.Count})", battleDeck.exhaustPileList, CardViewMode.ViewOnly);
    }

    #endregion

    #region Card UI Lifecycle & Pooling

    private void ClearAllCardUIs()
    {
        foreach (GameObject cardUIObject in activeCardUIs.Values)
        {
            if (cardUIObject != null)
            {
                CardUI cardUI = cardUIObject.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.OffSelectHighlight();
                    cardUI.bIsCardHighlight = false;
                }

                if (ObjectPoolManager.instance != null)
                {
                    ObjectPoolManager.instance.ReturnCardUI(cardUIObject);
                }
                else
                {
                    Destroy(cardUIObject);
                }
            }
        }
        activeCardUIs.Clear();
    }

    private void RefreshAllCardUIs()
    {
        ClearAllCardUIs();

        if (contentTransform == null)
        {
            Debug.LogError("[CardDeckViewPanel] contentTransform is null!");
            return;
        }

        for (int i = 0; i < currentDisplayedCards.Count; i++)
        {
            CreateCardUI(currentDisplayedCards[i]);
        }
    }

    private void CreateCardUI(Card card)
    {
        if (card == null) return;

        if (ObjectPoolManager.instance == null)
        {
            Debug.LogError("[CardDeckViewPanel] ObjectPoolManager is not initialized.");
            return;
        }

        GameObject cardUIObject = ObjectPoolManager.instance.GetCardUI(contentTransform);
        if (cardUIObject == null) return;

        CardUI cardUI = cardUIObject.GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.UpdateCardInstance(card);
            cardUI.bIsCardHighlight = (currentMode == CardViewMode.ViewOnly);
            cardUI.enableZoom = false;

            // 이미 선택 목록에 있는 카드면 하이라이트 복원
            if (selectedCards.Contains(card))
            {
                cardUI.OnSelectHighlight();
            }
            else
            {
                cardUI.OffSelectHighlight();
            }

            // 클릭 이벤트 바인딩
            cardUI.OnCardClick.RemoveAllListeners();
            cardUI.OnCardClick.AddListener(() => OnCardClicked(cardUI, card));

            activeCardUIs[card.runtimeID] = cardUIObject;
        }
        else
        {
            ObjectPoolManager.instance.ReturnCardUI(cardUIObject);
        }
    }

    #endregion

    #region Card Click & Selection Logic

    private void OnCardClicked(CardUI cardUI, Card card)
    {
        if (card == null || cardUI == null) return;

        switch (currentMode)
        {
            case CardViewMode.ViewOnly:
                HandleViewOnlyClick(card);
                break;

            case CardViewMode.SingleSelect:
                HandleSingleSelectClick(cardUI, card);
                break;

            case CardViewMode.MultiSelect:
                HandleMultiSelectClick(cardUI, card);
                break;

            case CardViewMode.UpgradeSelect:
                HandleUpgradeSelectClick(cardUI, card);
                break;
        }
    }

    private void HandleViewOnlyClick(Card card)
    {
        bool canUpgrade = card.hasMastery &&
                          (card.currentMasteryXP >= card.maxMasteryXP) &&
                          (card.GetRandomMasteryOption(1).Count > 0);

        if (canUpgrade)
        {
            if (CardMasteryManager.instance != null)
            {
                CardMasteryManager.instance.ProcessMasteryUpgrade(card);
            }
            else
            {
                Debug.LogError("[CardDeckViewPanel] CardMasteryManager instance is null!");
            }
        }
        else
        {
            ShowCardDetail(card);
        }
    }

    private void HandleSingleSelectClick(CardUI cardUI, Card card)
    {
        if (selectedCards.Contains(card))
        {
            // 이미 선택된 카드를 다시 누르면 선택 해제
            selectedCards.Remove(card);
            cardUI.OffSelectHighlight();
        }
        else
        {
            // 다른 카드가 이미 선택되어 있으면 기존 선택 해제
            if (selectedCards.Count > 0)
            {
                Card prevCard = selectedCards[0];
                if (activeCardUIs.TryGetValue(prevCard.runtimeID, out GameObject prevObj))
                {
                    if (prevObj != null)
                    {
                        CardUI prevUI = prevObj.GetComponent<CardUI>();
                        if (prevUI != null)
                        {
                            prevUI.OffSelectHighlight();
                        }
                    }
                }
                selectedCards.Clear();
            }

            selectedCards.Add(card);
            cardUI.OnSelectHighlight();
        }

        UpdateConfirmButtonUI();
    }

    private void HandleMultiSelectClick(CardUI cardUI, Card card)
    {
        if (selectedCards.Contains(card))
        {
            selectedCards.Remove(card);
            cardUI.OffSelectHighlight();
        }
        else
        {
            if (selectedCards.Count < targetSelectCount)
            {
                selectedCards.Add(card);
                cardUI.OnSelectHighlight();
            }
            else
            {
                Debug.LogWarning($"[CardDeckViewPanel] 최대 {targetSelectCount}장까지만 선택할 수 있습니다.");
            }
        }

        UpdateConfirmButtonUI();
    }

    private void HandleUpgradeSelectClick(CardUI cardUI, Card card)
    {
        // 1장 단일 선택 처리 후 상세 정보 또는 강화 프리뷰 열기
        HandleSingleSelectClick(cardUI, card);
    }

    private void ShowCardDetail(Card card)
    {
        if (_cardDetailHandler == null && UIManager.instance != null)
        {
            GameObject uiObj = UIManager.instance.OpenUI(UIConstants.PANEL_CARD_DETAIL, UILayerType.Normal, false);
            if (uiObj != null)
            {
                _cardDetailHandler = uiObj.GetComponent<CardDetailPanel>();
            }
        }

        if (_cardDetailHandler != null)
        {
            _cardDetailHandler.OnCardCheckUI(card);
        }
        else
        {
            Debug.LogError("[CardDeckViewPanel] CardDetailPanel could not be resolved via UIManager.");
        }
    }

    #endregion

    #region Confirm & Close Handling

    private void UpdateConfirmButtonUI()
    {
        if (confirmButton == null) return;

        if (currentMode == CardViewMode.ViewOnly)
        {
            confirmButton.gameObject.SetActive(false);
            return;
        }

        confirmButton.gameObject.SetActive(true);

        bool isReady = selectedCards.Count == targetSelectCount;
        confirmButton.interactable = isReady;

        if (confirmButtonText != null)
        {
            confirmButtonText.text = isReady
                ? "확인"
                : $"선택 ({selectedCards.Count}/{targetSelectCount})";
        }
    }

    public void OnConfirmClicked()
    {
        if (selectedCards.Count != targetSelectCount)
        {
            Debug.LogWarning($"[CardDeckViewPanel] {targetSelectCount}장을 선택해야 합니다. (현재: {selectedCards.Count})");
            return;
        }

        var result = new List<Card>(selectedCards);
        _onConfirmCallback?.Invoke(result);

        Close();
    }

    public override void Close()
    {
        _onCancelCallback?.Invoke();
        _onConfirmCallback = null;
        _onCancelCallback = null;
        selectedCards.Clear();
        currentDisplayedCards.Clear();
        _isMasterDeckMode = false;

        ClearAllCardUIs();

        base.Close();
    }

    #endregion

    #region Master Deck Event Handlers (IOnAddCard, IOnRemoveCard, etc.)

    public void OnAddCard(Card card)
    {
        if (!_isMasterDeckMode) return;
        if (card == null) return;

        currentDisplayedCards.Add(card);
        CreateCardUI(card);
    }

    public void OnRemoveCard(Card card)
    {
        if (!_isMasterDeckMode) return;
        if (card == null) return;

        currentDisplayedCards.Remove(card);
        if (activeCardUIs.TryGetValue(card.runtimeID, out GameObject cardUIObject))
        {
            if (cardUIObject != null)
            {
                if (ObjectPoolManager.instance != null)
                {
                    ObjectPoolManager.instance.ReturnCardUI(cardUIObject);
                }
                else
                {
                    Destroy(cardUIObject);
                }
            }
            activeCardUIs.Remove(card.runtimeID);
        }
    }

    public void OnCardUpgrade(Card card)
    {
        if (!_isMasterDeckMode) return;
        if (card == null) return;

        if (activeCardUIs.TryGetValue(card.runtimeID, out GameObject cardUIObject))
        {
            if (cardUIObject != null)
            {
                CardUI cardUI = cardUIObject.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.UpgradeCard();
                }
            }
        }
    }

    public void OnCardsRefreshed()
    {
        if (!_isMasterDeckMode) return;

        var p = GetPlayer();
        if (p != null && p.deck != null)
        {
            currentDisplayedCards.Clear();
            currentDisplayedCards.AddRange(p.deck.GetCards());
            RefreshAllCardUIs();
        }
    }

    public void OnCardMasteryUpgrade(Card card, string masteryId)
    {
        if (!_isMasterDeckMode) return;
        OnCardsRefreshed();
    }

    #endregion
}
