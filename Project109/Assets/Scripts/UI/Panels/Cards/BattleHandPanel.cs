using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleHandPanel : UIPanelBase
{
    [Header("Layout Settings")]
    public Transform contentTransform;
    public float maxPanelWidth = 900f;
    public float defaultCardSpacing = 160f;

    [Header("Deck Pile UI")]
    [SerializeField] private Button drawPileButton;
    [SerializeField] private TextMeshProUGUI drawCountText;
    [SerializeField] private Button usedPileButton;
    [SerializeField] private TextMeshProUGUI usedCountText;

    [Header("Mode Switch Button")]
    [SerializeField] private Button switchToMoveModeButton;
    [SerializeField] private TextMeshProUGUI modeSwitchButtonText;

    public event Action OnMoveModeRequested;
    public event Action OnCardModeRequested;

    private readonly List<GameObject> activeCardUIs = new List<GameObject>();
    private BattleDeck _boundDeck;
    private bool _isMoveMode = false;

    private void Awake()
    {
        uiLayerType = UILayerType.Top;
        blockWorldInput = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (drawPileButton != null)
        {
            drawPileButton.onClick.RemoveAllListeners();
            drawPileButton.onClick.AddListener(OnDrawPileClicked);
        }

        if (usedPileButton != null)
        {
            usedPileButton.onClick.RemoveAllListeners();
            usedPileButton.onClick.AddListener(OnUsedPileClicked);
        }

        if (switchToMoveModeButton != null)
        {
            switchToMoveModeButton.onClick.RemoveAllListeners();
            switchToMoveModeButton.onClick.AddListener(HandleSwitchMode);
        }

        // 바인딩된 덱이 없으면 현재 플레이어의 전투 덱 자동 바인딩 시도
        if (_boundDeck == null && GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            BindBattleDeck(GameSceneManager.instance.player.battleDeck);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (switchToMoveModeButton != null)
        {
            switchToMoveModeButton.onClick.RemoveListener(HandleSwitchMode);
        }

        UnbindBattleDeck();
        ClearHandUI();
    }

    private void HandleSwitchMode()
    {
        if (_isMoveMode)
        {
            OnCardModeRequested?.Invoke();
        }
        else
        {
            OnMoveModeRequested?.Invoke();
        }
    }

    /// <summary>
    /// 이동 모드 상태와 덱 버튼의 MoveCount 표시를 상호 전환합니다.
    /// </summary>
    public void SetMoveMode(bool isMoveMode, int moveCount = 0)
    {
        _isMoveMode = isMoveMode;

        // 1. 손패 영역 표시/숨김
        if (contentTransform != null)
        {
            contentTransform.gameObject.SetActive(!isMoveMode);
        }

        // 2. 모드 전환 버튼 텍스트 변경
        if (modeSwitchButtonText != null)
        {
            modeSwitchButtonText.text = isMoveMode ? "전투" : "이동";
        }

        // 3. 덱 버튼 수치 전환 (이동 모드: 남은 이동 횟수, 카드 모드: 드로우 덱 수)
        if (isMoveMode)
        {
            if (drawCountText != null)
            {
                drawCountText.text = moveCount.ToString();
            }
        }
        else
        {
            RefreshDrawPileCount();
        }
    }

    /// <summary>
    /// 이동 모드 중 실시간으로 남은 이동 횟수를 갱신합니다.
    /// </summary>
    public void UpdateMoveCount(int moveCount)
    {
        if (_isMoveMode && drawCountText != null)
        {
            drawCountText.text = moveCount.ToString();
        }
    }

    /// <summary>
    /// 플레이어의 전투 덱을 바인딩하여 손패 변경(OnHandChanged) 이벤트를 수신합니다.
    /// </summary>
    public void BindPlayer(Player player)
    {
        BindBattleDeck(player != null ? player.battleDeck : null);
    }

    /// <summary>
    /// 전투 덱을 바인딩하고 손패 UI 및 덱 카운터를 즉시 갱신합니다.
    /// </summary>
    public void BindBattleDeck(BattleDeck deck)
    {
        if (_boundDeck == deck) return;

        UnbindBattleDeck();
        _boundDeck = deck;

        if (_boundDeck != null)
        {
            _boundDeck.OnHandChanged += HandleHandChanged;
            _boundDeck.OnDrawPileChanged += RefreshDrawPileCount;
            _boundDeck.OnDiscardPileChanged += RefreshDiscardPileCount;

            RefreshHand(_boundDeck.hand);
            RefreshDrawPileCount();
            RefreshDiscardPileCount();
        }
    }

    /// <summary>
    /// 등록된 전투 덱 이벤트 구독을 해제합니다.
    /// </summary>
    public void UnbindBattleDeck()
    {
        if (_boundDeck != null)
        {
            _boundDeck.OnHandChanged -= HandleHandChanged;
            _boundDeck.OnDrawPileChanged -= RefreshDrawPileCount;
            _boundDeck.OnDiscardPileChanged -= RefreshDiscardPileCount;
            _boundDeck = null;
        }
    }

    private void RefreshDrawPileCount()
    {
        if (_isMoveMode) return;

        if (drawCountText != null && _boundDeck != null)
        {
            drawCountText.text = _boundDeck.drawPile.Count.ToString();
        }
    }

    private void RefreshDiscardPileCount()
    {
        if (usedCountText != null && _boundDeck != null)
        {
            usedCountText.text = _boundDeck.discardPile.Count.ToString();
        }
    }

    private void OnDrawPileClicked()
    {
        if (_isMoveMode) return;

        if (_boundDeck == null || UIManager.instance == null) return;
        var deckPanel = UIManager.instance.OpenUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK, UILayerType.Normal, true);
        if (deckPanel != null)
        {
            deckPanel.OpenDrawPile(_boundDeck);
        }
    }

    private void OnUsedPileClicked()
    {
        if (_boundDeck == null || UIManager.instance == null) return;
        var deckPanel = UIManager.instance.OpenUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK, UILayerType.Normal, true);
        if (deckPanel != null)
        {
            deckPanel.OpenDiscardPile(_boundDeck);
        }
    }

    private void HandleHandChanged()
    {
        if (_boundDeck != null)
        {
            RefreshHand(_boundDeck.hand);
        }
    }

    /// <summary>
    /// 플레이어의 손패 카드를 UI에 반영하고 가로로 정렬합니다.
    /// </summary>
    public void RefreshHand(List<Card> handCards)
    {
        ClearHandUI();

        if (handCards == null || handCards.Count == 0) return;

        if (ObjectPoolManager.instance == null)
        {
            Debug.LogError("[BattleHandPanel] ObjectPoolManager.instance is null!");
            return;
        }

        int count = handCards.Count;
        for (int i = 0; i < count; i++)
        {
            Card card = handCards[i];
            if (card == null) continue;

            GameObject cardObj = ObjectPoolManager.instance.GetCardUI(contentTransform);
            if (cardObj != null)
            {
                CardUI cardUI = cardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.UpdateCardInstance(card);
                    cardUI.enableZoom = true;
                    cardUI.zoomScale = 1.1f;
                    cardUI.zoomYOffset = 90f;
                    cardUI.bIsCardHighlight = true;
                    cardUI.bShowEffectAreaUI = true;

                    // 카드 클릭 시 플레이어 컨트롤러의 카드 시전 시도 연동
                    cardUI.OnCardClick.RemoveAllListeners();
                    cardUI.OnCardClick.AddListener(() =>
                    {
                        if (RunManager.instance != null && RunManager.instance.playerBattleController != null)
                        {
                            RunManager.instance.playerBattleController.TryUseCard(card);
                        }
                    });
                }
                activeCardUIs.Add(cardObj);
            }
        }

        UpdateCardLayout();
    }

    /// <summary>
    /// 카드 수량과 가로 제약 폭에 맞춰 카드 배치를 갱신합니다.
    /// </summary>
    public void UpdateCardLayout()
    {
        int count = activeCardUIs.Count;
        if (count == 0) return;

        float spacing = defaultCardSpacing;
        if (count > 1)
        {
            float requiredWidth = (count - 1) * defaultCardSpacing;
            if (requiredWidth > maxPanelWidth)
            {
                // 최대 가로 폭을 초과할 경우 겹치도록 간격을 축소
                spacing = maxPanelWidth / (count - 1);
            }
        }

        float startX = -((count - 1) * spacing) / 2f;

        for (int i = 0; i < count; i++)
        {
            GameObject cardObj = activeCardUIs[i];
            if (cardObj == null) continue;

            RectTransform rect = cardObj.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(startX + (i * spacing), 0f);
                rect.localScale = Vector3.one;
            }
            cardObj.transform.SetSiblingIndex(i); // SiblingIndex를 순서대로 맞춰 겹침 렌더링 순서 보장
        }
    }

    private void ClearHandUI()
    {
        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            GameObject cardObj = activeCardUIs[i];
            if (cardObj != null)
            {
                CardUI cardUI = cardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.OffSelectHighlight();
                    cardUI.bIsCardHighlight = false;
                }

                if (ObjectPoolManager.instance != null)
                {
                    ObjectPoolManager.instance.ReturnCardUI(cardObj);
                }
                else
                {
                    Destroy(cardObj);
                }
            }
        }
        activeCardUIs.Clear();
    }

    private void OnDestroy()
    {
        UnbindBattleDeck();
        ClearHandUI();
    }
}
