using CardTypes;
using LitMotion;
using LitMotion.Extensions;
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

    [Header("Animation Settings")]
    [SerializeField] private float drawDuration = 0.28f;
    [SerializeField] private float drawStaggerInterval = 0.05f;
    [SerializeField] private float drawStartScale = 0.2f;
    [SerializeField] private float drawStartRotationZ = 15f;
    [SerializeField] private float layoutDuration = 0.2f;
    [SerializeField] private float discardDuration = 0.22f;
    [SerializeField] private float discardStaggerInterval = 0.03f;
    [SerializeField] private float maxDiscardStagger = 0.15f;
    [SerializeField] private float discardEndScale = 0.2f;
    [SerializeField] private float discardEndRotationZ = -15f;
    [SerializeField] private Vector2 fallbackDrawOffset = new Vector2(-450f, -120f);
    [SerializeField] private Vector2 fallbackDiscardOffset = new Vector2(450f, -120f);

    [Header("Deck Pile UI")]
    [SerializeField] private Button drawPileButton;
    [SerializeField] private TextMeshProUGUI drawCountText;
    [SerializeField] private Button usedPileButton;
    [SerializeField] private TextMeshProUGUI usedCountText;

    [Header("Mode Switch Button")]
    [SerializeField] private Button switchToMoveModeButton;
    [SerializeField] private TextMeshProUGUI modeSwitchButtonText;

    [Header("Bottom Peek & Hover Reveal Settings")]
    [SerializeField] private float restingCardYOffset = -105f;
    [SerializeField] private float hoverRevealScale = 1.1f;
    [SerializeField] private float hoverRevealYOffset = 140f;
    [SerializeField] private float handPlayDragThresholdY = 250f;

    [Header("Targeting Layout Settings")]
    [SerializeField] private float targetingSlideDownOffset = 175f;
    [SerializeField] private float targetingSelectedCardExtraOffset = 90f;
    [SerializeField] private float targetingSlideDuration = 0.18f;

    [Header("Unplayable Feedback UI")]
    [SerializeField] private TextMeshProUGUI unplayableFeedbackText;
    [SerializeField] private CanvasGroup unplayableFeedbackCanvasGroup;
    [SerializeField] private RectTransform unplayableFeedbackRect;

    [Header("Hotkey Hint Settings")]
    [SerializeField] private bool showHotkeyHints = true;

    public bool ShowHotkeyHints => showHotkeyHints;

    public float RestingCardYOffset => restingCardYOffset;
    public float HoverRevealYOffset => hoverRevealYOffset;

    public event Action OnMoveModeRequested;
    public event Action OnCardModeRequested;

    private sealed class HandCardEntry
    {
        public Card card;
        public GameObject cardObj;
        public CardUI cardUI;
        public RectTransform rectTransform;
        public MotionHandle posHandle;
        public MotionHandle scaleHandle;
        public MotionHandle rotHandle;
        public bool isDiscarding;
        public bool isReturnedToPool;
    }

    private readonly List<HandCardEntry> activeCardUIs = new List<HandCardEntry>();
    private readonly Dictionary<Card, HandCardEntry> _activeEntryMap = new Dictionary<Card, HandCardEntry>();
    private readonly List<HandCardEntry> _discardingEntries = new List<HandCardEntry>();
    private readonly Stack<HandCardEntry> _entryPool = new Stack<HandCardEntry>();

    // 손패 차분 동기화용 재사용 스크래치 컬렉션
    private readonly HashSet<Card> _newHandSet = new HashSet<Card>();
    private readonly HashSet<Card> _seenNewHandCards = new HashSet<Card>();
    private readonly List<HandCardEntry> _scratchOrderedEntries = new List<HandCardEntry>();
    private readonly List<HandCardEntry> _scratchRemovedEntries = new List<HandCardEntry>();
    private readonly HashSet<HandCardEntry> _scratchNewlyDrawnSet = new HashSet<HandCardEntry>();

    private const float FeedbackDefaultY = 210f;
    private const float FeedbackStartY = 195f;
    private const float FeedbackTargetY = 230f;
    private const float FeedbackMoveDuration = 0.25f;
    private const float FeedbackHoldDuration = 0.60f;
    private const float FeedbackFadeDuration = 0.35f;

    private BattleDeck _boundDeck;
    private PlayerBattleController _boundController;
    private Character _boundCharacter;
    private BattleManager _boundBattleManager;
    private Card _targetingCard;
    private MotionHandle _feedbackMoveHandle;
    private MotionHandle _feedbackFadeHandle;
    private Action _onFeedbackFadeComplete;
    private bool _isMoveMode = false;
    private int _lastEvaluatedStaminaInt = int.MinValue;
    private bool _lastEvaluatedTurnActive = false;

    public bool IsTargetingMode => _targetingCard != null;
    public Card TargetingCard => _targetingCard;

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

        RefreshAllCardPlayability();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (switchToMoveModeButton != null)
        {
            switchToMoveModeButton.onClick.RemoveListener(HandleSwitchMode);
        }

        CancelFeedbackMotions();
        if (unplayableFeedbackRect != null)
        {
            unplayableFeedbackRect.gameObject.SetActive(false);
        }

        UnbindBattleController();
        UnbindBattleDeck();
        _targetingCard = null;
        ClearHandUI();
    }

    private void HandleSwitchMode()
    {
        AudioServiceLocator.PlaySFX(AudioCueKeys.SFX_UI_CLICK);
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
    /// 이동 모드 상태와 덱 버튼의 남은 이동력 표시를 상호 전환합니다.
    /// </summary>
    public void SetMoveMode(bool isMoveMode, int remainingTiles = 0)
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
            if (showHotkeyHints)
            {
                modeSwitchButtonText.text = isMoveMode ? "전투 모드 [Q]" : "이동 모드 [Q]";
            }
            else
            {
                modeSwitchButtonText.text = isMoveMode ? "전투 모드" : "이동 모드";
            }
        }

        // 3. 덱 버튼 수치 전환 (이동 모드: 남은 이동 가능 칸수, 카드 모드: 드로우 덱 수)
        if (isMoveMode)
        {
            if (drawCountText != null)
            {
                drawCountText.text = remainingTiles.ToString();
            }
        }
        else
        {
            RefreshDrawPileCount();
            RefreshAllCardPlayability();
        }
    }

    /// <summary>
    /// 손패 및 모드 전환 버튼의 단축키 힌트 표시 여부를 설정하고 즉시 반영합니다.
    /// </summary>
    public void SetShowHotkeyHints(bool show)
    {
        showHotkeyHints = show;
        SetMoveMode(_isMoveMode, (_boundCharacter != null) ? _boundCharacter.curMoveRange : 0);

        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            HandCardEntry entry = activeCardUIs[i];
            if (entry != null && !entry.isDiscarding && entry.cardUI != null)
            {
                entry.cardUI.SetHotkeyHint(showHotkeyHints && i < 9 ? (i + 1).ToString() : null);
            }
        }
    }

    /// <summary>
    /// 지정한 손패 슬롯 인덱스의 CardUI 컴포넌트를 반환합니다.
    /// </summary>
    public CardUI GetHandCardUI(int index)
    {
        if (index < 0 || index >= activeCardUIs.Count)
        {
            return null;
        }
        HandCardEntry entry = activeCardUIs[index];
        return entry != null ? entry.cardUI : null;
    }

    /// <summary>
    /// 이동 모드 중 실시간으로 남은 이동 가능 칸수를 갱신합니다.
    /// </summary>
    public void UpdateMoveRange(int remainingTiles)
    {
        if (_isMoveMode && drawCountText != null)
        {
            drawCountText.text = remainingTiles.ToString();
        }
    }

    /// <summary>
    /// 모드 전환 버튼(이동/카드)의 상호작용 가능 여부를 설정합니다.
    /// 플레이어 턴이 아닐 때는 비활성화됩니다.
    /// </summary>
    public void SetSwitchModeInteractable(bool interactable)
    {
        if (switchToMoveModeButton != null)
        {
            switchToMoveModeButton.interactable = interactable;
        }
    }

    /// <summary>
    /// 카드 타깃팅 모드 진입·해제에 맞춰 손패 카드를 하단으로 내려 시야를 확보하거나 원래 위치로 복귀시킵니다.
    /// </summary>
    public void SetTargetingState(Card targetingCard)
    {
        if (ReferenceEquals(_targetingCard, targetingCard))
        {
            return;
        }

        _targetingCard = targetingCard;
        bool isTargeting = _targetingCard != null;

        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            HandCardEntry entry = activeCardUIs[i];
            if (entry == null || entry.isDiscarding || entry.cardUI == null)
            {
                continue;
            }

            entry.cardUI.ForceExitHoverZoom(immediate: true);
            entry.cardUI.CancelHoverMotions();
            entry.cardUI.SetInteractable(!isTargeting);
        }

        ApplyCardLayout(null);
    }

    private float GetTargetCardY(HandCardEntry entry)
    {
        if (_targetingCard == null)
        {
            return restingCardYOffset;
        }

        if (entry != null && ReferenceEquals(entry.card, _targetingCard))
        {
            return restingCardYOffset - (targetingSlideDownOffset + targetingSelectedCardExtraOffset);
        }

        return restingCardYOffset - targetingSlideDownOffset;
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

    /// <summary>
    /// 전투 컨트롤러를 바인딩하여 카드 시전 실패, 단축키 선택, 스태미나·전장 상태 변화에 따른 사용 가능 여부를 실시간 구독합니다.
    /// </summary>
    public void BindBattleController(PlayerBattleController controller)
    {
        if (_boundController == controller)
        {
            RefreshBattleControllerDynamicSubscriptions();
            RefreshAllCardPlayability();
            return;
        }

        UnbindBattleController();
        _boundController = controller;

        if (_boundController != null)
        {
            _boundController.OnCardPlayRejected += ShowUnplayableFeedback;
            _boundController.OnKeyboardSelectedCardChanged += HandleKeyboardSelectedCardChanged;
            RefreshBattleControllerDynamicSubscriptions();
        }

        RefreshAllCardPlayability();
    }

    private void RefreshBattleControllerDynamicSubscriptions()
    {
        Character targetChar = _boundController != null ? _boundController.controlledCharacter : null;
        if (!ReferenceEquals(_boundCharacter, targetChar))
        {
            if (_boundCharacter != null)
            {
                _boundCharacter.OnCharacterStaminaChanged -= HandleCharacterStaminaChanged;
                if (_boundCharacter.characterMove != null)
                {
                    _boundCharacter.characterMove.OnTileChanged -= HandleCharacterTileChanged;
                }
            }

            _boundCharacter = targetChar;
            if (_boundCharacter != null)
            {
                _boundCharacter.OnCharacterStaminaChanged -= HandleCharacterStaminaChanged;
                _boundCharacter.OnCharacterStaminaChanged += HandleCharacterStaminaChanged;
                if (_boundCharacter.characterMove != null)
                {
                    _boundCharacter.characterMove.OnTileChanged -= HandleCharacterTileChanged;
                    _boundCharacter.characterMove.OnTileChanged += HandleCharacterTileChanged;
                }
            }
        }

        BattleManager targetBm = RunManager.instance != null ? RunManager.instance.battleManager : null;
        if (!ReferenceEquals(_boundBattleManager, targetBm))
        {
            if (_boundBattleManager != null)
            {
                _boundBattleManager.OnBattlefieldStateChanged -= HandleBattlefieldStateChanged;
            }

            _boundBattleManager = targetBm;
            if (_boundBattleManager != null)
            {
                _boundBattleManager.OnBattlefieldStateChanged -= HandleBattlefieldStateChanged;
                _boundBattleManager.OnBattlefieldStateChanged += HandleBattlefieldStateChanged;
            }
        }
    }

    /// <summary>
    /// 등록된 전투 컨트롤러 및 상태 변화 이벤트 구독을 해제합니다.
    /// </summary>
    public void UnbindBattleController()
    {
        if (_boundBattleManager != null)
        {
            _boundBattleManager.OnBattlefieldStateChanged -= HandleBattlefieldStateChanged;
            _boundBattleManager = null;
        }

        if (_boundCharacter != null)
        {
            _boundCharacter.OnCharacterStaminaChanged -= HandleCharacterStaminaChanged;
            if (_boundCharacter.characterMove != null)
            {
                _boundCharacter.characterMove.OnTileChanged -= HandleCharacterTileChanged;
            }
            _boundCharacter = null;
        }

        if (_boundController != null)
        {
            _boundController.OnCardPlayRejected -= ShowUnplayableFeedback;
            _boundController.OnKeyboardSelectedCardChanged -= HandleKeyboardSelectedCardChanged;
            _boundController = null;
        }

        _lastEvaluatedStaminaInt = int.MinValue;
        _lastEvaluatedTurnActive = false;
    }

    private void HandleCharacterStaminaChanged(Character character)
    {
        if (activeCardUIs.Count == 0)
        {
            return;
        }

        bool isTurnActive = _boundController != null && _boundController.IsPlayerTurnActive();
        int staminaInt = character != null ? Mathf.FloorToInt(character.curStamina) : 0;

        // ATB 충전 중 턴 비활성 상태에서 정수 스태미나가 변하지 않았다면 불필요한 재평가 생략
        if (!isTurnActive && !_lastEvaluatedTurnActive && staminaInt == _lastEvaluatedStaminaInt)
        {
            return;
        }

        RefreshAllCardPlayability();
    }

    private void HandleCharacterTileChanged(Character character, Tile previousTile, Tile newTile)
    {
        RefreshAllCardPlayability();
    }

    private void HandleBattlefieldStateChanged()
    {
        RefreshAllCardPlayability();
    }

    private void HandleKeyboardSelectedCardChanged(Card selectedCard)
    {
        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            HandCardEntry entry = activeCardUIs[i];
            if (entry == null || entry.isDiscarding || entry.cardUI == null)
            {
                continue;
            }

            entry.cardUI.SetKeyboardSelected(selectedCard != null && ReferenceEquals(entry.card, selectedCard));
        }

        if (selectedCard != null)
        {
            if (UIManager.instance != null)
            {
                UIManager.instance.UpdateEffectAreaUI(selectedCard);
            }
            if (BattleTimelinePanel.Instance != null)
            {
                BattleTimelinePanel.Instance.SetCardHoverPreview(selectedCard);
            }
        }
        else if (!IsTargetingMode)
        {
            if (UIManager.instance != null)
            {
                UIManager.instance.ClearEffectAreaTiles();
            }
            if (BattleTimelinePanel.Instance != null)
            {
                BattleTimelinePanel.Instance.ClearCardHoverPreview();
            }
        }
    }

    /// <summary>
    /// 현재 손패의 모든 카드에 대해 스태미나·사거리·조건 기반 사용 가능 시각 상태(글로우/딤/코스트 색상)를 일괄 갱신합니다.
    /// </summary>
    public void RefreshAllCardPlayability()
    {
        if (_boundController != null)
        {
            RefreshBattleControllerDynamicSubscriptions();
        }

        _lastEvaluatedTurnActive = _boundController != null && _boundController.IsPlayerTurnActive();
        _lastEvaluatedStaminaInt = _boundCharacter != null ? Mathf.FloorToInt(_boundCharacter.curStamina) : int.MinValue;

        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            HandCardEntry entry = activeCardUIs[i];
            if (entry == null || entry.isDiscarding || entry.cardUI == null || entry.card == null)
            {
                continue;
            }

            if (_boundController != null)
            {
                CardPlayabilityVisualState state = _boundController.EvaluateCardPlayabilityVisual(entry.card, out bool isCostInsufficient);
                entry.cardUI.SetPlayabilityVisualState(state, isCostInsufficient);
            }
            else
            {
                entry.cardUI.SetPlayabilityVisualState(CardPlayabilityVisualState.Default);
            }
        }
    }

    /// <summary>
    /// 카드 사용 불가 사유 텍스트를 손패 위 중앙에 플로팅 경고 배지로 표시합니다.
    /// </summary>
    public void ShowUnplayableFeedback(Card card, CardPlayFailReason reason, string message)
    {
        AudioServiceLocator.PlaySFX(AudioCueKeys.SFX_CARD_UNPLAYABLE, 0.03f);
        string resolvedMessage = !string.IsNullOrEmpty(message) ? message : GetDefaultFeedbackMessage(reason);
        if (string.IsNullOrEmpty(resolvedMessage))
        {
            return;
        }

        CancelFeedbackMotions();
        EnsureUnplayableFeedbackUI();

        if (unplayableFeedbackText == null || unplayableFeedbackRect == null || unplayableFeedbackCanvasGroup == null)
        {
            return;
        }

        unplayableFeedbackText.text = resolvedMessage;
        unplayableFeedbackRect.gameObject.SetActive(true);
        unplayableFeedbackRect.SetAsLastSibling();

        float posX = unplayableFeedbackRect.anchoredPosition.x;
        Vector2 startPos = new Vector2(posX, FeedbackStartY);
        Vector2 targetPos = new Vector2(posX, FeedbackTargetY);

        unplayableFeedbackRect.anchoredPosition = startPos;
        unplayableFeedbackCanvasGroup.alpha = 1f;

        if (!Application.isPlaying || !gameObject.activeInHierarchy || !unplayableFeedbackRect.gameObject.activeInHierarchy)
        {
            unplayableFeedbackRect.anchoredPosition = targetPos;
            return;
        }

        if (_onFeedbackFadeComplete == null)
        {
            _onFeedbackFadeComplete = HandleFeedbackFadeComplete;
        }

        _feedbackMoveHandle = LMotion.Create(startPos, targetPos, FeedbackMoveDuration)
            .WithEase(Ease.OutCubic)
            .BindToAnchoredPosition(unplayableFeedbackRect);

        _feedbackFadeHandle = LMotion.Create(1f, 0f, FeedbackFadeDuration)
            .WithDelay(FeedbackHoldDuration, DelayType.FirstLoop, skipValuesDuringDelay: false)
            .WithEase(Ease.InQuad)
            .WithOnComplete(_onFeedbackFadeComplete)
            .BindToAlpha(unplayableFeedbackCanvasGroup);
    }

    private void EnsureUnplayableFeedbackUI()
    {
        if (unplayableFeedbackRect != null && unplayableFeedbackText != null && unplayableFeedbackCanvasGroup != null)
        {
            return;
        }

        if (unplayableFeedbackRect == null && unplayableFeedbackText != null)
        {
            unplayableFeedbackRect = unplayableFeedbackText.rectTransform;
        }

        if (unplayableFeedbackRect == null && unplayableFeedbackCanvasGroup != null)
        {
            unplayableFeedbackRect = unplayableFeedbackCanvasGroup.transform as RectTransform;
        }

        if (unplayableFeedbackRect == null)
        {
            Transform existing = transform.Find("UnplayableFeedback");
            if (existing != null)
            {
                unplayableFeedbackRect = existing as RectTransform;
                if (unplayableFeedbackRect == null)
                {
                    unplayableFeedbackRect = existing.gameObject.AddComponent<RectTransform>();
                }
            }
        }

        if (unplayableFeedbackRect == null)
        {
            GameObject rootObj = new GameObject("UnplayableFeedback", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            rootObj.layer = gameObject.layer != 0 ? gameObject.layer : 5;
            rootObj.transform.SetParent(transform, false);

            unplayableFeedbackRect = rootObj.GetComponent<RectTransform>();
            unplayableFeedbackRect.anchorMin = new Vector2(0.5f, 0f);
            unplayableFeedbackRect.anchorMax = new Vector2(0.5f, 0f);
            unplayableFeedbackRect.pivot = new Vector2(0.5f, 0.5f);
            unplayableFeedbackRect.anchoredPosition = new Vector2(0f, FeedbackDefaultY);
            unplayableFeedbackRect.sizeDelta = new Vector2(540f, 52f);

            Image badgeImage = rootObj.GetComponent<Image>();
            badgeImage.color = new Color(0.08f, 0.08f, 0.12f, 0.86f);
            badgeImage.raycastTarget = false;

            unplayableFeedbackCanvasGroup = rootObj.GetComponent<CanvasGroup>();
            unplayableFeedbackCanvasGroup.interactable = false;
            unplayableFeedbackCanvasGroup.blocksRaycasts = false;
            unplayableFeedbackCanvasGroup.alpha = 0f;

            unplayableFeedbackText = CreateFeedbackTextChild(unplayableFeedbackRect);
            rootObj.SetActive(false);
            return;
        }

        if (unplayableFeedbackCanvasGroup == null)
        {
            unplayableFeedbackCanvasGroup = unplayableFeedbackRect.GetComponent<CanvasGroup>();
            if (unplayableFeedbackCanvasGroup == null)
            {
                unplayableFeedbackCanvasGroup = unplayableFeedbackRect.gameObject.AddComponent<CanvasGroup>();
            }
        }
        unplayableFeedbackCanvasGroup.interactable = false;
        unplayableFeedbackCanvasGroup.blocksRaycasts = false;

        if (unplayableFeedbackText == null)
        {
            unplayableFeedbackText = unplayableFeedbackRect.GetComponentInChildren<TextMeshProUGUI>(true);
            if (unplayableFeedbackText == null)
            {
                unplayableFeedbackText = CreateFeedbackTextChild(unplayableFeedbackRect);
            }
        }
        unplayableFeedbackText.raycastTarget = false;
    }

    private TextMeshProUGUI CreateFeedbackTextChild(RectTransform parentRect)
    {
        GameObject textObj = new GameObject("FeedbackText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.layer = parentRect.gameObject.layer;
        textObj.transform.SetParent(parentRect, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 6f);
        textRect.offsetMax = new Vector2(-18f, -6f);

        TextMeshProUGUI textComp = textObj.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = TMPFontUtility.ResolveDefaultFont(modeSwitchButtonText, drawCountText);
        if (fontAsset != null)
        {
            textComp.font = fontAsset;
        }
        textComp.fontSize = 22f;
        textComp.enableAutoSizing = true;
        textComp.fontSizeMin = 16f;
        textComp.fontSizeMax = 24f;
        textComp.fontStyle = FontStyles.Bold;
        textComp.alignment = TextAlignmentOptions.Center;
        textComp.color = new Color(1f, 0.42f, 0.36f, 1f);
        textComp.raycastTarget = false;
        return textComp;
    }

    private static string GetDefaultFeedbackMessage(CardPlayFailReason reason)
    {
        switch (reason)
        {
            case CardPlayFailReason.NotPlayerTurn:
                return "현재 플레이어의 턴이 아닙니다.";
            case CardPlayFailReason.CharacterBusy:
                return "현재 행동 중입니다.";
            case CardPlayFailReason.InsufficientStamina:
                return "스태미나가 부족합니다.";
            case CardPlayFailReason.BlockedByEffect:
                return "상태이상 효과로 카드를 사용할 수 없습니다.";
            case CardPlayFailReason.ConditionNotMet:
                return "카드 사용 조건을 충족하지 못했습니다.";
            case CardPlayFailReason.NoValidTargets:
                return "사거리 내에 유효한 대상이 없습니다.";
            case CardPlayFailReason.Unknown:
                return "카드를 사용할 수 없습니다.";
            default:
                return string.Empty;
        }
    }

    private void CancelFeedbackMotions()
    {
        if (_feedbackMoveHandle.IsActive()) _feedbackMoveHandle.TryCancel();
        if (_feedbackFadeHandle.IsActive()) _feedbackFadeHandle.TryCancel();
        _feedbackMoveHandle = MotionHandle.None;
        _feedbackFadeHandle = MotionHandle.None;
    }

    private void HandleFeedbackFadeComplete()
    {
        _feedbackFadeHandle = MotionHandle.None;
        if (unplayableFeedbackRect != null)
        {
            unplayableFeedbackRect.gameObject.SetActive(false);
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
        AudioServiceLocator.PlaySFX(AudioCueKeys.SFX_UI_CLICK);
        var deckPanel = UIManager.instance.OpenUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK, UILayerType.Popup, true);
        if (deckPanel != null)
        {
            deckPanel.OpenDrawPile(_boundDeck);
        }
    }

    private void OnUsedPileClicked()
    {
        if (_boundDeck == null || UIManager.instance == null) return;
        AudioServiceLocator.PlaySFX(AudioCueKeys.SFX_UI_CLICK);
        var deckPanel = UIManager.instance.OpenUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK, UILayerType.Popup, true);
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
    /// 플레이어의 손패 카드를 diff 기반으로 동기화하여 생성·배치·버리기 애니메이션을 수행합니다.
    /// </summary>
    public void RefreshHand(List<Card> handCards)
    {
        _newHandSet.Clear();
        _seenNewHandCards.Clear();
        _scratchOrderedEntries.Clear();
        _scratchRemovedEntries.Clear();
        _scratchNewlyDrawnSet.Clear();

        if (handCards != null)
        {
            for (int i = 0; i < handCards.Count; i++)
            {
                Card card = handCards[i];
                if (card != null)
                {
                    _newHandSet.Add(card);
                }
            }
        }

        // 기존 손패 중 새 손패에서 사라진 카드는 버리기/사용 애니메이션으로 전환
        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            HandCardEntry entry = activeCardUIs[i];
            if (entry == null) continue;

            if (entry.cardObj == null || entry.card == null)
            {
                CancelEntryHandles(entry);
                if (entry.card != null)
                {
                    _activeEntryMap.Remove(entry.card);
                }
                RecycleEntry(entry);
                continue;
            }

            if (!_newHandSet.Contains(entry.card))
            {
                _activeEntryMap.Remove(entry.card);
                _scratchRemovedEntries.Add(entry);
            }
        }

        for (int i = 0; i < _scratchRemovedEntries.Count; i++)
        {
            StartDiscardAnimation(_scratchRemovedEntries[i], i);
        }
        _scratchRemovedEntries.Clear();

        if (handCards == null || handCards.Count == 0)
        {
            activeCardUIs.Clear();
            _newHandSet.Clear();
            return;
        }

        bool hasPoolManager = gameObject.scene.isLoaded && ObjectPoolManager.instance != null;
        bool loggedMissingPool = false;

        // 새 손패 순서대로 기존 카드는 유지하고 신규 드로우 카드는 풀에서 생성
        for (int i = 0; i < handCards.Count; i++)
        {
            Card card = handCards[i];
            if (card == null || !_seenNewHandCards.Add(card)) continue;

            if (_activeEntryMap.TryGetValue(card, out HandCardEntry existingEntry) &&
                existingEntry != null &&
                existingEntry.cardObj != null)
            {
                if (existingEntry.cardUI != null)
                {
                    existingEntry.cardUI.UpdateCardInstance(card);
                    ConfigureHandCardVisuals(existingEntry.cardUI);
                }
                _scratchOrderedEntries.Add(existingEntry);
            }
            else
            {
                if (!hasPoolManager)
                {
                    if (!loggedMissingPool)
                    {
                        Debug.LogError("[BattleHandPanel] ObjectPoolManager.instance is null!");
                        loggedMissingPool = true;
                    }
                    continue;
                }

                HandCardEntry drawnEntry = CreateDrawnCardEntry(card);
                if (drawnEntry != null)
                {
                    _activeEntryMap[card] = drawnEntry;
                    _scratchOrderedEntries.Add(drawnEntry);
                    _scratchNewlyDrawnSet.Add(drawnEntry);
                }
            }
        }

        activeCardUIs.Clear();
        for (int i = 0; i < _scratchOrderedEntries.Count; i++)
        {
            activeCardUIs.Add(_scratchOrderedEntries[i]);
        }

        _scratchOrderedEntries.Clear();
        _newHandSet.Clear();
        _seenNewHandCards.Clear();

        ApplyCardLayout(_scratchNewlyDrawnSet);
        _scratchNewlyDrawnSet.Clear();
        RefreshAllCardPlayability();
    }

    /// <summary>
    /// 카드 수량과 가로 제약 폭에 맞춰 카드 배치를 갱신합니다.
    /// </summary>
    public void UpdateCardLayout()
    {
        ApplyCardLayout(null);
    }

    private void ApplyCardLayout(HashSet<HandCardEntry> newlyDrawnSet)
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
        int drawStaggerIndex = 0;
        HandCardEntry zoomedEntry = null;
        HandCardEntry draggingEntry = null;
        bool isPanelActive = gameObject.activeInHierarchy;

        for (int i = 0; i < count; i++)
        {
            HandCardEntry entry = activeCardUIs[i];
            if (entry == null || entry.cardObj == null || entry.isDiscarding) continue;

            Vector2 targetPos = new Vector2(startX + (i * spacing), GetTargetCardY(entry));
            RectTransform rect = entry.rectTransform;

            if (entry.cardUI != null)
            {
                entry.cardUI.SetHandSlot(targetPos, Vector3.one, i);
                entry.cardUI.SetHotkeyHint(showHotkeyHints && i < 9 ? (i + 1).ToString() : null);
            }

            bool isDragging = entry.cardUI != null && entry.cardUI.IsDragging;
            if (isDragging)
            {
                draggingEntry = entry;
                CancelEntryHandles(entry);
                continue;
            }

            // SiblingIndex를 순서대로 맞춰 겹침 렌더링 순서 보장 (좌상단 코스트·단축키 가시성 확보)
            entry.cardObj.transform.SetSiblingIndex(i);

            bool isZoomed = entry.cardUI != null && entry.cardUI.IsZoomed;
            if (isZoomed)
            {
                zoomedEntry = entry;
            }

            bool isNewlyDrawn = newlyDrawnSet != null && newlyDrawnSet.Contains(entry);
            bool isEntering = entry.cardUI != null && entry.cardUI.IsEntering;

            if (rect == null)
            {
                if (isNewlyDrawn && entry.cardUI != null)
                {
                    entry.cardUI.SetEntering(false);
                }
                continue;
            }

            if (isNewlyDrawn)
            {
                CancelEntryHandles(entry);

                if (!Application.isPlaying || !isPanelActive || drawDuration <= 0f)
                {
                    rect.anchoredPosition = targetPos;
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                    CompleteDrawEntry(entry);
                    continue;
                }

                float delay = drawStaggerIndex * drawStaggerInterval;
                drawStaggerIndex++;

                HandCardEntry capturedEntry = entry;
                Vector2 fromPos = rect.anchoredPosition;
                Vector3 fromScale = rect.localScale;
                Quaternion fromRot = rect.localRotation;

                var posBuilder = LMotion.Create(fromPos, targetPos, drawDuration)
                    .WithEase(Ease.OutCubic);
                var scaleBuilder = LMotion.Create(fromScale, Vector3.one, drawDuration)
                    .WithEase(Ease.OutBack);
                var rotBuilder = LMotion.Create(fromRot, Quaternion.identity, drawDuration)
                    .WithEase(Ease.OutCubic);

                if (delay > 0f)
                {
                    posBuilder = posBuilder.WithDelay(delay, DelayType.FirstLoop, skipValuesDuringDelay: false);
                    scaleBuilder = scaleBuilder.WithDelay(delay, DelayType.FirstLoop, skipValuesDuringDelay: false);
                    rotBuilder = rotBuilder.WithDelay(delay, DelayType.FirstLoop, skipValuesDuringDelay: false);
                }

                entry.posHandle = posBuilder
                    .WithOnComplete(() => CompleteDrawEntry(capturedEntry))
                    .BindToAnchoredPosition(rect);
                entry.scaleHandle = scaleBuilder.BindToLocalScale(rect);
                entry.rotHandle = rotBuilder.BindToLocalRotation(rect);
            }
            else if (isZoomed)
            {
                // 현재 호버 확대 중인 카드는 CardUI가 수평 슬라이드 및 확대 상태를 유지하도록 간섭하지 않음
                CancelEntryHandles(entry);
            }
            else
            {
                CancelEntryHandles(entry);
                if (entry.cardUI != null)
                {
                    // OnPointerExit 복귀 트윈과 레이아웃 슬라이드 트윈이 동시에 위치를 덮어쓰지 않도록 차단
                    entry.cardUI.CancelHoverMotions();
                }

                bool needsMove = (rect.anchoredPosition - targetPos).sqrMagnitude > 0.25f;
                bool needsScale = (rect.localScale - Vector3.one).sqrMagnitude > 0.0001f;
                bool needsRot = Quaternion.Angle(rect.localRotation, Quaternion.identity) > 0.1f;

                if (!needsMove && !needsScale && !needsRot)
                {
                    rect.anchoredPosition = targetPos;
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                    if (isEntering)
                    {
                        CompleteDrawEntry(entry);
                    }
                    continue;
                }

                float moveDuration = isEntering ? drawDuration : (IsTargetingMode ? targetingSlideDuration : layoutDuration);
                if (!Application.isPlaying || !isPanelActive || moveDuration <= 0f)
                {
                    rect.anchoredPosition = targetPos;
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                    if (isEntering)
                    {
                        CompleteDrawEntry(entry);
                    }
                    continue;
                }

                if (needsMove || isEntering)
                {
                    var posBuilder = LMotion.Create(rect.anchoredPosition, targetPos, moveDuration)
                        .WithEase(Ease.OutCubic);

                    if (isEntering)
                    {
                        HandCardEntry capturedEntry = entry;
                        entry.posHandle = posBuilder
                            .WithOnComplete(() => CompleteDrawEntry(capturedEntry))
                            .BindToAnchoredPosition(rect);
                    }
                    else
                    {
                        entry.posHandle = posBuilder.BindToAnchoredPosition(rect);
                    }
                }

                if (needsScale)
                {
                    entry.scaleHandle = LMotion.Create(rect.localScale, Vector3.one, moveDuration)
                        .WithEase(Ease.OutCubic)
                        .BindToLocalScale(rect);
                }

                if (needsRot)
                {
                    entry.rotHandle = LMotion.Create(rect.localRotation, Quaternion.identity, moveDuration)
                        .WithEase(Ease.OutCubic)
                        .BindToLocalRotation(rect);
                }
            }
        }

        // 호버 중인 카드가 있다면 배치 정렬 후 최상단 렌더링 순서 재보장
        if (zoomedEntry != null && zoomedEntry.cardObj != null)
        {
            zoomedEntry.cardObj.transform.SetAsLastSibling();
        }

        // 드래그 중인 카드가 있다면 최상위 렌더링 순서 유지
        if (draggingEntry != null && draggingEntry.cardObj != null)
        {
            draggingEntry.cardObj.transform.SetAsLastSibling();
        }
    }

    private HandCardEntry CreateDrawnCardEntry(Card card)
    {
        GameObject cardObj = ObjectPoolManager.instance.GetCardUI(contentTransform);
        if (cardObj == null) return null;

        HandCardEntry entry = RentEntry();
        entry.card = card;
        entry.cardObj = cardObj;
        entry.cardUI = cardObj.GetComponent<CardUI>();

        if (entry.cardUI != null)
        {
            entry.rectTransform = entry.cardUI.CachedRectTransform;
            entry.cardUI.ResetForPool();
            entry.cardUI.UpdateCardInstance(card);
            ConfigureHandCardVisuals(entry.cardUI);
            entry.cardUI.SetInteractable(!IsTargetingMode);
            entry.cardUI.SetEntering(true);
            entry.cardUI.OnHoverZoomStarted -= HandleCardHoverZoomStarted;
            entry.cardUI.OnHoverZoomStarted += HandleCardHoverZoomStarted;
            entry.cardUI.OnHoverZoomEnded -= HandleCardHoverZoomEnded;
            entry.cardUI.OnHoverZoomEnded += HandleCardHoverZoomEnded;

            // 카드 클릭 시 플레이어 컨트롤러의 카드 시전 시도 연동
            entry.cardUI.OnCardClick.RemoveAllListeners();
            Card capturedCard = card;
            entry.cardUI.OnCardClick.AddListener(() =>
            {
                PlayerBattleController controller = _boundController != null
                    ? _boundController
                    : (RunManager.instance != null ? RunManager.instance.playerBattleController : null);
                if (controller != null)
                {
                    controller.TryUseCard(capturedCard);
                }
            });
        }
        else
        {
            entry.rectTransform = cardObj.GetComponent<RectTransform>();
        }

        if (entry.rectTransform != null)
        {
            Vector2 centerAnchor = new Vector2(0.5f, 0.5f);
            entry.rectTransform.anchorMin = centerAnchor;
            entry.rectTransform.anchorMax = centerAnchor;
            entry.rectTransform.pivot = centerAnchor;
            entry.rectTransform.anchoredPosition = GetPileAnchoredPosition(drawPileButton, entry.rectTransform, fallbackDrawOffset);
            entry.rectTransform.localScale = new Vector3(drawStartScale, drawStartScale, 1f);
            entry.rectTransform.localRotation = Quaternion.Euler(0f, 0f, drawStartRotationZ);
        }

        return entry;
    }

    private void ConfigureHandCardVisuals(CardUI cardUI)
    {
        cardUI.enableZoom = true;
        cardUI.zoomScale = hoverRevealScale;
        cardUI.zoomYOffset = hoverRevealYOffset;
        cardUI.playDragThresholdY = handPlayDragThresholdY;
        cardUI.bIsCardHighlight = true;
        cardUI.bShowEffectAreaUI = true;
        cardUI.usePlayGesture = true;
    }

    private void StartDiscardAnimation(HandCardEntry entry, int discardIndex)
    {
        if (entry == null) return;

        if (entry.cardObj == null)
        {
            RecycleEntry(entry);
            return;
        }

        CancelEntryHandles(entry);
        entry.isDiscarding = true;
        entry.isReturnedToPool = false;
        _discardingEntries.Add(entry);

        if (entry.cardUI != null)
        {
            entry.cardUI.OnHoverZoomStarted -= HandleCardHoverZoomStarted;
            entry.cardUI.OnHoverZoomEnded -= HandleCardHoverZoomEnded;
            entry.cardUI.OnCardClick.RemoveAllListeners();
            entry.cardUI.OffSelectHighlight();
            entry.cardUI.bIsCardHighlight = false;
            entry.cardUI.bShowEffectAreaUI = false;
            entry.cardUI.SetDiscarding(true);
        }

        entry.cardObj.transform.SetAsLastSibling();

        RectTransform rect = entry.rectTransform;
        if (rect == null || !Application.isPlaying || !gameObject.activeInHierarchy || discardDuration <= 0f)
        {
            CompleteDiscardEntry(entry);
            return;
        }

        Vector2 targetPos = GetPileAnchoredPosition(usedPileButton, rect, fallbackDiscardOffset);
        Vector3 targetScale = new Vector3(discardEndScale, discardEndScale, 1f);
        Quaternion targetRot = Quaternion.Euler(0f, 0f, discardEndRotationZ);
        float delay = Mathf.Min(discardIndex * discardStaggerInterval, maxDiscardStagger);

        HandCardEntry capturedEntry = entry;
        var posBuilder = LMotion.Create(rect.anchoredPosition, targetPos, discardDuration)
            .WithEase(Ease.OutCubic);
        var scaleBuilder = LMotion.Create(rect.localScale, targetScale, discardDuration)
            .WithEase(Ease.OutCubic);
        var rotBuilder = LMotion.Create(rect.localRotation, targetRot, discardDuration)
            .WithEase(Ease.OutCubic);

        if (delay > 0f)
        {
            posBuilder = posBuilder.WithDelay(delay, DelayType.FirstLoop, skipValuesDuringDelay: false);
            scaleBuilder = scaleBuilder.WithDelay(delay, DelayType.FirstLoop, skipValuesDuringDelay: false);
            rotBuilder = rotBuilder.WithDelay(delay, DelayType.FirstLoop, skipValuesDuringDelay: false);
        }

        entry.posHandle = posBuilder
            .WithOnComplete(() => CompleteDiscardEntry(capturedEntry))
            .BindToAnchoredPosition(rect);
        entry.scaleHandle = scaleBuilder.BindToLocalScale(rect);
        entry.rotHandle = rotBuilder.BindToLocalRotation(rect);
    }

    private void CompleteDrawEntry(HandCardEntry entry)
    {
        if (entry == null || entry.isDiscarding || entry.isReturnedToPool) return;

        entry.posHandle = MotionHandle.None;
        entry.scaleHandle = MotionHandle.None;
        entry.rotHandle = MotionHandle.None;

        if (entry.cardUI != null)
        {
            entry.cardUI.SetEntering(false);
        }
    }

    private void CompleteDiscardEntry(HandCardEntry entry)
    {
        if (entry == null) return;

        _discardingEntries.Remove(entry);
        entry.posHandle = MotionHandle.None;
        CancelEntryHandles(entry);

        if (!entry.isReturnedToPool && entry.cardObj != null)
        {
            entry.isReturnedToPool = true;
            ReturnCardObjectToPool(entry.cardObj, entry.cardUI);
        }

        RecycleEntry(entry);
    }

    private void HandleCardHoverZoomStarted(CardUI cardUI)
    {
        if (cardUI == null) return;

        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            HandCardEntry entry = activeCardUIs[i];
            if (entry == null || entry.isDiscarding || entry.cardUI == null)
            {
                continue;
            }

            if (entry.cardUI == cardUI)
            {
                CancelEntryHandles(entry);
                if (entry.rectTransform != null)
                {
                    entry.rectTransform.localRotation = Quaternion.identity;
                }
            }
            else if (entry.cardUI.IsZoomed)
            {
                entry.cardUI.ForceExitHoverZoom(immediate: false);
            }
        }

        NormalizeSiblingOrder(cardUI);
    }

    private void HandleCardHoverZoomEnded(CardUI cardUI)
    {
        NormalizeSiblingOrder(null);
    }

    private void NormalizeSiblingOrder(CardUI priorityTopCard)
    {
        CardUI topCard = priorityTopCard;

        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            HandCardEntry entry = activeCardUIs[i];
            if (entry == null || entry.cardObj == null || entry.isDiscarding)
            {
                continue;
            }

            entry.cardObj.transform.SetSiblingIndex(i);

            if (topCard == null && entry.cardUI != null && (entry.cardUI.IsZoomed || entry.cardUI.IsDragging))
            {
                topCard = entry.cardUI;
            }
        }

        if (topCard != null)
        {
            topCard.transform.SetAsLastSibling();
        }
    }

    private Vector2 GetPileAnchoredPosition(Button pileButton, RectTransform cardRect, Vector2 fallbackOffset)
    {
        if (contentTransform == null)
        {
            return fallbackOffset;
        }

        RectTransform contentRect = contentTransform as RectTransform;
        if (contentRect == null || pileButton == null)
        {
            return fallbackOffset;
        }

        RectTransform pileRect = pileButton.transform as RectTransform;
        if (pileRect == null)
        {
            return fallbackOffset;
        }

        Camera pileCamera = ResolveCanvasEventCamera(pileRect);
        Camera contentCamera = ResolveCanvasEventCamera(contentRect);

        Vector3 worldCenter = pileRect.TransformPoint(pileRect.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(pileCamera, worldCenter);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRect, screenPoint, contentCamera, out Vector2 localPoint))
        {
            Vector3 invLocal = contentRect.InverseTransformPoint(worldCenter);
            localPoint = new Vector2(invLocal.x, invLocal.y);
        }

        // contentTransform의 pivot과 cardRect의 anchor 기준 좌표 차이를 보정
        if (cardRect != null)
        {
            Rect parentBounds = contentRect.rect;
            float anchorCenterX = Mathf.Lerp(parentBounds.xMin, parentBounds.xMax, (cardRect.anchorMin.x + cardRect.anchorMax.x) * 0.5f);
            float anchorCenterY = Mathf.Lerp(parentBounds.yMin, parentBounds.yMax, (cardRect.anchorMin.y + cardRect.anchorMax.y) * 0.5f);
            localPoint.x -= anchorCenterX;
            localPoint.y -= anchorCenterY;
        }

        return localPoint;
    }

    private Camera ResolveCanvasEventCamera(RectTransform targetRect)
    {
        if (targetRect == null) return null;

        Canvas parentCanvas = targetRect.GetComponentInParent<Canvas>();
        if (parentCanvas == null) return null;

        Canvas rootCanvas = parentCanvas.rootCanvas;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return rootCanvas.worldCamera;
        }

        return null;
    }

    private void ClearHandUI()
    {
        _targetingCard = null;

        // OnComplete를 통한 중복 풀 반환을 방지하기 위해 먼저 모든 핸들을 취소한 뒤 풀에 반환
        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            HandCardEntry entry = activeCardUIs[i];
            if (entry == null) continue;

            CancelEntryHandles(entry);
            if (!entry.isReturnedToPool && entry.cardObj != null)
            {
                entry.isReturnedToPool = true;
                ReturnCardObjectToPool(entry.cardObj, entry.cardUI);
            }
            RecycleEntry(entry);
        }
        activeCardUIs.Clear();
        _activeEntryMap.Clear();

        for (int i = 0; i < _discardingEntries.Count; i++)
        {
            HandCardEntry entry = _discardingEntries[i];
            if (entry == null) continue;

            CancelEntryHandles(entry);
            if (!entry.isReturnedToPool && entry.cardObj != null)
            {
                entry.isReturnedToPool = true;
                ReturnCardObjectToPool(entry.cardObj, entry.cardUI);
            }
            RecycleEntry(entry);
        }
        _discardingEntries.Clear();

        _newHandSet.Clear();
        _seenNewHandCards.Clear();
        _scratchOrderedEntries.Clear();
        _scratchRemovedEntries.Clear();
        _scratchNewlyDrawnSet.Clear();
    }

    private void ReturnCardObjectToPool(GameObject cardObj, CardUI cardUI)
    {
        if (cardUI != null)
        {
            cardUI.OnHoverZoomStarted -= HandleCardHoverZoomStarted;
            cardUI.OnHoverZoomEnded -= HandleCardHoverZoomEnded;
            cardUI.OnCardClick.RemoveAllListeners();
            cardUI.OffSelectHighlight();
            cardUI.enableZoom = false;
            cardUI.bIsCardHighlight = false;
            cardUI.bShowEffectAreaUI = false;
            cardUI.usePlayGesture = false;
            cardUI.ResetForPool();
        }

        if (ObjectPoolManager.HasInstance && gameObject.scene.isLoaded)
        {
            ObjectPoolManager.instance.ReturnCardUI(cardObj);
        }
        else if (cardObj != null)
        {
            if (Application.isPlaying)
            {
                Destroy(cardObj);
            }
            else
            {
                DestroyImmediate(cardObj);
            }
        }
    }

    private void CancelEntryHandles(HandCardEntry entry)
    {
        if (entry == null) return;

        if (entry.posHandle.IsActive()) entry.posHandle.TryCancel();
        if (entry.scaleHandle.IsActive()) entry.scaleHandle.TryCancel();
        if (entry.rotHandle.IsActive()) entry.rotHandle.TryCancel();

        entry.posHandle = MotionHandle.None;
        entry.scaleHandle = MotionHandle.None;
        entry.rotHandle = MotionHandle.None;
    }

    private HandCardEntry RentEntry()
    {
        HandCardEntry entry = _entryPool.Count > 0 ? _entryPool.Pop() : new HandCardEntry();
        entry.card = null;
        entry.cardObj = null;
        entry.cardUI = null;
        entry.rectTransform = null;
        entry.posHandle = MotionHandle.None;
        entry.scaleHandle = MotionHandle.None;
        entry.rotHandle = MotionHandle.None;
        entry.isDiscarding = false;
        entry.isReturnedToPool = false;
        return entry;
    }

    private void RecycleEntry(HandCardEntry entry)
    {
        if (entry == null) return;

        entry.card = null;
        entry.cardObj = null;
        entry.cardUI = null;
        entry.rectTransform = null;
        entry.posHandle = MotionHandle.None;
        entry.scaleHandle = MotionHandle.None;
        entry.rotHandle = MotionHandle.None;
        entry.isDiscarding = false;
        entry.isReturnedToPool = false;
        _entryPool.Push(entry);
    }

    private void OnDestroy()
    {
        CancelFeedbackMotions();
        UnbindBattleController();
        UnbindBattleDeck();
        _targetingCard = null;
        ClearHandUI();
    }
}
