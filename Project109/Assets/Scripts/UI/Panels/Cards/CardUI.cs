using GameItem.Types;
using LitMotion;
using LitMotion.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private CardData cardData;
    private Card cardInstance;

    public TextMeshProUGUI cardName;
    public TextMeshProUGUI cardDescription;
    public TextMeshProUGUI useStamina;

    public Image cardImage;  //카드 데이터에 맞는 이미지

    public GameObject selectHighlightObject;    //선택을 알려주는 하이라이트 UI
    [SerializeField] private GameObject masteryUpgradeAvailableUI;    // 마스터리 업그레이드 가능 알림 UI

    public EffectAreaCheckButton effectAreaCheckButton; //공격 범위 확인용 버튼

    public UnityEvent OnCardClick = new UnityEvent();  //클릭 시 호출될 이벤트(다양한 변수들도 쉽게 호출하기 위해 UnityEvent 사용)

    public bool bIsCardHighlight;
    public bool bShowEffectAreaUI;

    [Header("Zoom Settings")]
    public bool enableZoom = false;
    public float zoomScale = 1.1f;
    public float zoomYOffset = 90f;
    [SerializeField] private float hoverMotionDuration = 0.12f;
    [SerializeField] private Ease hoverMotionEase = Ease.OutCubic;

    [Header("Play Gesture Settings")]
    public bool usePlayGesture = false;
    public float playDragThresholdY = 140f;
    public float doubleClickInterval = 0.32f;

    private Vector3 originalScale = Vector3.one;
    private Vector3 originalPosition;
    private int originalSiblingIndex;
    private bool isZoomed = false;

    // 손패 레이아웃 및 상태 동기화 필드
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private readonly List<Graphic> _raycastGraphics = new List<Graphic>();
    private bool _componentsCached = false;

    private bool _isInteractable = true;
    private bool _isEntering = false;
    private bool _isDiscarding = false;
    private bool _hasHandSlot = false;
    private Vector2 _baseAnchoredPosition;
    private Vector3 _baseScale = Vector3.one;
    private int _baseSiblingIndex = 0;

    private MotionHandle _hoverPosHandle;
    private MotionHandle _hoverScaleHandle;
    private bool _isShowingTooltip = false;
    private bool _isShowingTimelinePreview = false;

    private bool _isDragging = false;
    private bool _dragCanceled = false;
    private float _lastClickUnscaledTime = -10f;
    private Canvas _cachedRootCanvas;

    private static readonly Vector2 DefaultCardSize = new Vector2(185f, 325f);
    private static readonly Vector2 CenterAnchorPivot = new Vector2(0.5f, 0.5f);
    private static readonly List<CardUI> _activeDraggingCards = new List<CardUI>();
    private static int _activeCardDragCount = 0;

    /// <summary>
    /// 현재 드래그 중인 카드 UI가 하나 이상 존재하는지 여부입니다.
    /// </summary>
    public static bool IsAnyCardDragging
    {
        get
        {
            PruneDeadDraggingCards();
            return _activeCardDragCount > 0;
        }
    }

    /// <summary>
    /// 카드 호버 줌이 시작될 때 외부 레이아웃 트윈과의 충돌을 막기 위해 호출되는 이벤트입니다.
    /// </summary>
    public event Action<CardUI> OnHoverZoomStarted;

    public bool IsInteractable => _isInteractable;
    public bool IsZoomed => isZoomed;
    public bool IsEntering => _isEntering;
    public bool IsDiscarding => _isDiscarding;
    public bool IsDragging => _isDragging;

    private static void PruneDeadDraggingCards()
    {
        if (_activeDraggingCards.Count == 0)
        {
            _activeCardDragCount = 0;
            return;
        }

        for (int i = _activeDraggingCards.Count - 1; i >= 0; i--)
        {
            CardUI card = _activeDraggingCards[i];
            if (card == null)
            {
                if (!ReferenceEquals(card, null))
                {
                    UIDragInputGuard.EndDrag(card);
                }
                _activeDraggingCards.RemoveAt(i);
            }
        }

        _activeCardDragCount = _activeDraggingCards.Count;
    }

    /// <summary>
    /// 카드의 드래그 상태와 전역 UI 드래그 가드 카운터를 멱등적으로 동기화합니다.
    /// </summary>
    private void SetDraggingState(bool dragging)
    {
        if (dragging)
        {
            if (this == null)
            {
                return;
            }

            _isDragging = true;
            PruneDeadDraggingCards();
            if (!_activeDraggingCards.Contains(this))
            {
                _activeDraggingCards.Add(this);
            }
            _activeCardDragCount = _activeDraggingCards.Count;
            UIDragInputGuard.BeginDrag(this);
            return;
        }

        _isDragging = false;
        if (this == null)
        {
            PruneDeadDraggingCards();
            UIDragInputGuard.EndDrag(this);
            return;
        }

        _activeDraggingCards.Remove(this);
        PruneDeadDraggingCards();
        _activeCardDragCount = _activeDraggingCards.Count;
        UIDragInputGuard.EndDrag(this);
    }

    public RectTransform CachedRectTransform
    {
        get
        {
            EnsureCachedComponents();
            return _rectTransform;
        }
    }

    void Awake()
    {
        EnsureCachedComponents();

        if (selectHighlightObject != null)
        {
            selectHighlightObject.SetActive(false);
        }
        if (masteryUpgradeAvailableUI != null)
        {
            masteryUpgradeAvailableUI.SetActive(false);
        }
    }

    private void OnEnable()
    {
        EnsureCachedComponents();

        if (!_isDiscarding)
        {
            ApplyRaycastInteractable(_isInteractable);
        }
    }

    private void OnDisable()
    {
        bool wasDragging = _isDragging;
        SetDraggingState(false);
        _dragCanceled = false;
        _lastClickUnscaledTime = -10f;

        HideCardKeywordTooltips();
        ClearTimelineCardPreview();
        bool hadActiveHoverMotion = _hoverPosHandle.IsActive() || _hoverScaleHandle.IsActive();
        CancelHoverMotions();

        if (bIsCardHighlight)
        {
            OffSelectHighlight();
        }

        if (bShowEffectAreaUI && UIManager.instance != null && !IsPlayerTargetSelectionActive())
        {
            UIManager.instance.ClearEffectAreaTiles();
        }

        if (!_isDiscarding && (isZoomed || wasDragging || hadActiveHoverMotion))
        {
            isZoomed = false;
            RestoreRestingTransformImmediate();
        }
        else
        {
            isZoomed = false;
        }
    }

    private void Update()
    {
        if (!_isDragging || _dragCanceled) return;

        bool cancelPressed =
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

        if (cancelPressed)
        {
            CancelDrag();
        }
    }

    private void OnDestroy()
    {
        SetDraggingState(false);
        CancelHoverMotions();
    }

    private void EnsureCachedComponents()
    {
        if (_componentsCached) return;
        _componentsCached = true;

        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();

        if (_canvasGroup == null)
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null && graphics[i].raycastTarget)
                {
                    _raycastGraphics.Add(graphics[i]);
                }
            }
        }
    }

    private void ApplyRaycastInteractable(bool interactable)
    {
        EnsureCachedComponents();

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = interactable;
        }
        else
        {
            for (int i = 0; i < _raycastGraphics.Count; i++)
            {
                if (_raycastGraphics[i] != null)
                {
                    _raycastGraphics[i].raycastTarget = interactable;
                }
            }
        }
    }

    /// <summary>
    /// 카드의 포인터 입력/레이캐스트 상호작용 가능 여부를 설정합니다.
    /// CanvasGroup이 없을 경우 초기 RaycastTarget 그래픽 목록으로 안전하게 폴백합니다.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        _isInteractable = interactable;
        ApplyRaycastInteractable(interactable);

        if (!interactable)
        {
            bool wasDragging = _isDragging;
            SetDraggingState(false);
            _dragCanceled = false;
            _lastClickUnscaledTime = -10f;

            HideCardKeywordTooltips();
            ClearTimelineCardPreview();
            CancelHoverMotions();

            if (bIsCardHighlight)
            {
                OffSelectHighlight();
            }

            if (isZoomed || wasDragging)
            {
                if (bShowEffectAreaUI && UIManager.instance != null && !IsPlayerTargetSelectionActive())
                {
                    UIManager.instance.ClearEffectAreaTiles();
                }

                isZoomed = false;
                if (wasDragging && !_isDiscarding)
                {
                    RestoreRestingTransformImmediate();
                }
            }
        }
    }

    /// <summary>
    /// 드로우 진입 애니메이션 중 여부를 설정합니다. 진입 중에는 호버 줌이 무시됩니다.
    /// </summary>
    public void SetEntering(bool entering)
    {
        _isEntering = entering;
        if (entering)
        {
            CancelHoverMotions();
            isZoomed = false;
            SetDraggingState(false);
            _dragCanceled = false;
            _lastClickUnscaledTime = -10f;
        }
    }

    /// <summary>
    /// 버림/사용 연출 중 여부를 설정합니다. 버림 상태에서는 즉시 상호작용과 호버가 차단됩니다.
    /// </summary>
    public void SetDiscarding(bool discarding)
    {
        _isDiscarding = discarding;
        if (discarding)
        {
            _isEntering = false;
            SetDraggingState(false);
            _dragCanceled = false;
            _lastClickUnscaledTime = -10f;
            SetInteractable(false);
        }
    }

    /// <summary>
    /// 손패 레이아웃에서의 기준 슬롯 위치, 스케일, 형제 순서를 동기화합니다.
    /// 호버 중일 경우 새로운 슬롯 기준점에 맞춰 호버 목표 위치를 부드럽게 보정합니다.
    /// </summary>
    public void SetHandSlot(Vector2 baseAnchoredPosition, Vector3 baseScale, int baseSiblingIndex)
    {
        _hasHandSlot = true;
        _baseAnchoredPosition = baseAnchoredPosition;
        _baseScale = baseScale;
        _baseSiblingIndex = baseSiblingIndex;

        originalPosition = baseAnchoredPosition;
        originalScale = baseScale;
        originalSiblingIndex = baseSiblingIndex;

        if (!_isDragging && isZoomed && enableZoom && _isInteractable && !_isDiscarding)
        {
            PlayHoverZoomMotion(true);
        }
    }

    /// <summary>
    /// 오브젝트 풀 재사용 또는 반납 전 카드 UI의 트윈 핸들, 상호작용, 변형 상태를 초기화합니다.
    /// </summary>
    public void ResetForPool()
    {
        HideCardKeywordTooltips();
        ClearTimelineCardPreview();
        CancelHoverMotions();

        cardInstance = null;
        cardData = null;

        usePlayGesture = false;
        isZoomed = false;
        _isEntering = false;
        _isDiscarding = false;
        _hasHandSlot = false;
        SetDraggingState(false);
        _dragCanceled = false;
        _lastClickUnscaledTime = -10f;
        _cachedRootCanvas = null;

        _baseAnchoredPosition = Vector2.zero;
        _baseScale = Vector3.one;
        _baseSiblingIndex = 0;

        originalPosition = Vector3.zero;
        originalScale = Vector3.one;
        originalSiblingIndex = 0;

        OffSelectHighlight();
        SetInteractable(true);

        EnsureCachedComponents();
        if (_rectTransform != null)
        {
            // 도감/보상 등의 LayoutGroup이 변경한 좌상단 앵커를 중앙 앵커 규격으로 복원
            _rectTransform.anchorMin = CenterAnchorPivot;
            _rectTransform.anchorMax = CenterAnchorPivot;
            _rectTransform.pivot = CenterAnchorPivot;
            _rectTransform.sizeDelta = DefaultCardSize;
            _rectTransform.anchoredPosition = Vector2.zero;
        }

        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 진행 중인 호버 확대/복귀 트윈 핸들을 즉시 취소합니다.
    /// </summary>
    public void CancelHoverMotions()
    {
        if (_hoverPosHandle.IsActive())
        {
            _hoverPosHandle.TryCancel();
        }
        if (_hoverScaleHandle.IsActive())
        {
            _hoverScaleHandle.TryCancel();
        }
        _hoverPosHandle = MotionHandle.None;
        _hoverScaleHandle = MotionHandle.None;
    }

    private void PlayHoverZoomMotion(bool isZoomingIn)
    {
        EnsureCachedComponents();
        CancelHoverMotions();

        Vector2 basePos = _hasHandSlot ? _baseAnchoredPosition : (Vector2)originalPosition;
        Vector3 baseScale = _hasHandSlot ? _baseScale : originalScale;

        Vector2 targetPos = isZoomingIn ? basePos + new Vector2(0f, zoomYOffset) : basePos;
        Vector3 targetScale = isZoomingIn ? baseScale * zoomScale : baseScale;

        if (!Application.isPlaying || !gameObject.activeInHierarchy || hoverMotionDuration <= 0f)
        {
            transform.localScale = targetScale;
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = targetPos;
            }
            else
            {
                transform.localPosition = targetPos;
            }
            return;
        }

        if (_rectTransform != null)
        {
            _hoverPosHandle = LMotion.Create(_rectTransform.anchoredPosition, targetPos, hoverMotionDuration)
                .WithEase(hoverMotionEase)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAnchoredPosition(_rectTransform);
        }

        _hoverScaleHandle = LMotion.Create(transform.localScale, targetScale, hoverMotionDuration)
            .WithEase(hoverMotionEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToLocalScale(transform);
    }

    private void RestoreRestingTransformImmediate()
    {
        EnsureCachedComponents();
        transform.localRotation = Quaternion.identity;

        if (_hasHandSlot)
        {
            transform.localScale = _baseScale;
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _baseAnchoredPosition;
            }
            RestoreSiblingIndexSafely(_baseSiblingIndex);
        }
        else if (enableZoom || usePlayGesture)
        {
            transform.localScale = originalScale;
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = originalPosition;
            }
            else
            {
                transform.localPosition = originalPosition;
            }
            RestoreSiblingIndexSafely(originalSiblingIndex);
        }
    }

    private void RestoreSiblingIndexSafely(int targetIndex)
    {
        if (transform.parent == null) return;
        int maxIndex = transform.parent.childCount - 1;
        if (maxIndex < 0) return;
        transform.SetSiblingIndex(Mathf.Clamp(targetIndex, 0, maxIndex));
    }

    public void UpdateCardData(CardData newCardData)
    {
        if (newCardData == null)
        {
            Debug.LogWarning("New Card Data is null!");
            return;
        }

        if (cardData != newCardData || cardInstance != null)
        {
            CancelHoverMotions();
            isZoomed = false;
            _isEntering = false;
            _isDiscarding = false;
            SetDraggingState(false);
            _dragCanceled = false;
            _lastClickUnscaledTime = -10f;
            SetInteractable(true);
        }

        cardData = newCardData;
        cardInstance = null;

        if (cardName != null)
        {
            cardName.text = cardData.cardName;
        }
        if (useStamina != null)
        {
            useStamina.text = cardData.stamina.ToString();
        }

        UpdateCardDescription();

        if (cardImage != null && cardData.cardSprite != null)
        {
            cardImage.sprite = cardData.cardSprite;
        }

        if (masteryUpgradeAvailableUI != null)
        {
            masteryUpgradeAvailableUI.SetActive(false);
        }
    }

    public void UpdateCardInstance(Card newCardInstance)
    {
        if (newCardInstance == null)
        {
            Debug.LogWarning("New Card Instance is null!");
            return;
        }

        // 동일한 카드 인스턴스의 데이터만 갱신(손패 유지 카드)하는 경우가 아니라면 호버/상호작용 상태 초기화
        if (!ReferenceEquals(cardInstance, newCardInstance))
        {
            CancelHoverMotions();
            isZoomed = false;
            _isEntering = false;
            _isDiscarding = false;
            SetDraggingState(false);
            _dragCanceled = false;
            _lastClickUnscaledTime = -10f;
            SetInteractable(true);
        }

        cardInstance = newCardInstance;
        cardData = cardInstance.cardData;

        // masteryLevel > 0이면 ★ 접두사 표시
        string displayName = cardData != null ? cardData.cardName : string.Empty;
        if (cardInstance.masteryLevel > 0)
            displayName = "\u2605" + displayName;
        if (cardName != null)
        {
            cardName.text = displayName;
        }
        if (useStamina != null)
        {
            useStamina.text = cardInstance.currentCost.ToString();
        }

        UpdateCardDescription();

        if (cardImage != null && cardData != null && cardData.cardSprite != null)
        {
            cardImage.sprite = cardData.cardSprite;
        }

        if (masteryUpgradeAvailableUI != null)
        {
            bool canUpgrade = cardInstance.hasMastery &&
                              (cardInstance.currentMasteryXP >= cardInstance.maxMasteryXP) &&
                              (cardInstance.GetRandomMasteryOption(1).Count > 0);
            masteryUpgradeAvailableUI.SetActive(canUpgrade);
        }
    }

    public void UpgradeCard()
    {
        if (cardData == null)
            return;

        string upgradeName = cardData.upgradedCardName;
        if (!string.IsNullOrEmpty(upgradeName) && ModLoader.Instance.CardDatabase.TryGetValue(upgradeName, out CardData upgradeCardData))
        {
            cardData = upgradeCardData;
            if (cardName != null)
            {
                cardName.text = cardData.cardName;
            }
            if (useStamina != null)
            {
                useStamina.text = cardData.stamina.ToString();
            }
            UpdateCardDescription();

            if (cardImage != null && cardData.cardSprite != null)
            {
                cardImage.sprite = cardData.cardSprite;
            }
        }
        else
        {
            Debug.LogWarning("Failed to Upgrade Card!");
        }
    }

    public void UpdateCardDescription()
    {
        string rawDescription = string.Empty;
        if (cardInstance != null)
        {
            rawDescription = cardInstance.GetDescription();
        }
        else if (cardData != null)
        {
            rawDescription = cardData.GetDescription();
        }
        string formattedDescription = TooltipManager.ReplaceKeywordsForDisplay(rawDescription);
        if (cardDescription != null)
        {
            cardDescription.SetText(formattedDescription);
        }
    }

    public CardData GetCardData()
    {
        return cardData;
    }

    public Card GetCardInstance()
    {
        return cardInstance;
    }

    public void OnSelectHighlight()
    {
        if (selectHighlightObject != null)
        {
            selectHighlightObject.SetActive(true);
        }
    }

    public void OffSelectHighlight()
    {
        if (selectHighlightObject != null)
        {
            selectHighlightObject.SetActive(false);
        }
    }

    private void ShowCardKeywordTooltips()
    {
        if (TooltipManager.Instance == null) return;

        string rawDescription = string.Empty;
        if (cardInstance != null)
        {
            rawDescription = cardInstance.GetDescription();
        }
        else if (cardData != null)
        {
            rawDescription = cardData.GetDescription();
        }

        if (!string.IsNullOrEmpty(rawDescription))
        {
            EnsureCachedComponents();
            TooltipManager.Instance.ShowKeywordTooltips(rawDescription, _rectTransform);
            _isShowingTooltip = TooltipManager.Instance.gameObject.activeSelf;
        }
    }

    private void HideCardKeywordTooltips()
    {
        if (!_isShowingTooltip) return;
        _isShowingTooltip = false;

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

    private void ShowTimelineCardPreview()
    {
        if (!bShowEffectAreaUI || cardInstance == null || BattleTimelinePanel.Instance == null)
        {
            return;
        }

        _isShowingTimelinePreview = true;
        BattleTimelinePanel.Instance.SetCardHoverPreview(cardInstance);
    }

    private void ClearTimelineCardPreview()
    {
        if (!_isShowingTimelinePreview) return;
        _isShowingTimelinePreview = false;

        if (BattleTimelinePanel.Instance != null)
        {
            BattleTimelinePanel.Instance.ClearCardHoverPreview();
        }
    }

    private bool IsPlayerTargetSelectionActive()
    {
        return RunManager.instance != null &&
               RunManager.instance.playerBattleController != null &&
               RunManager.instance.playerBattleController.controlState == PlayerControlState.TargetSelection;
    }

    private void RefreshCachedRootCanvas()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        _cachedRootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
    }

    private Camera ResolveDragEventCamera(PointerEventData eventData)
    {
        if (_cachedRootCanvas == null || !_cachedRootCanvas.isActiveAndEnabled)
        {
            RefreshCachedRootCanvas();
        }

        if (_cachedRootCanvas != null)
        {
            if (_cachedRootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (_cachedRootCanvas.worldCamera != null)
            {
                return _cachedRootCanvas.worldCamera;
            }
        }

        return eventData != null ? eventData.pressEventCamera : null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isInteractable || _isDiscarding || _isDragging) return;
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

        if (!usePlayGesture)
        {
            HideCardKeywordTooltips();
            ClearTimelineCardPreview();
            OnCardClick?.Invoke();
            return;
        }

        bool isDoubleClick =
            (eventData != null && eventData.clickCount >= 2) ||
            (Time.unscaledTime - _lastClickUnscaledTime) <= doubleClickInterval;

        if (isDoubleClick)
        {
            _lastClickUnscaledTime = -10f;
            HideCardKeywordTooltips();
            ClearTimelineCardPreview();
            OnCardClick?.Invoke();
        }
        else
        {
            _lastClickUnscaledTime = Time.unscaledTime;
            OnSelectHighlight();

            if (!IsPlayerTargetSelectionActive() && bShowEffectAreaUI && cardData != null && UIManager.instance != null)
            {
                UIManager.instance.UpdateEffectAreaUI(cardData);
            }
            ShowTimelineCardPreview();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!usePlayGesture || !_isInteractable || _isEntering || _isDiscarding) return;
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

        SetDraggingState(true);
        _dragCanceled = false;
        _lastClickUnscaledTime = -10f;

        EnsureCachedComponents();
        RefreshCachedRootCanvas();

        if (!_hasHandSlot && !isZoomed)
        {
            originalScale = transform.localScale;
            originalPosition = _rectTransform != null ? (Vector3)_rectTransform.anchoredPosition : transform.localPosition;
            originalSiblingIndex = transform.GetSiblingIndex();
        }

        OnHoverZoomStarted?.Invoke(this);
        CancelHoverMotions();
        HideCardKeywordTooltips();

        transform.SetAsLastSibling();
        transform.localRotation = Quaternion.identity;

        if (bShowEffectAreaUI && cardData != null && UIManager.instance != null)
        {
            UIManager.instance.UpdateEffectAreaUI(cardData);
        }
        ShowTimelineCardPreview();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || _dragCanceled) return;

        EnsureCachedComponents();
        if (_rectTransform != null && eventData != null)
        {
            RectTransform parentRect = _rectTransform.parent as RectTransform;
            if (parentRect != null)
            {
                Camera eventCamera = ResolveDragEventCamera(eventData);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventCamera, out Vector2 localPoint))
                {
                    Rect parentBounds = parentRect.rect;
                    float anchorCenterX = Mathf.Lerp(parentBounds.xMin, parentBounds.xMax, (_rectTransform.anchorMin.x + _rectTransform.anchorMax.x) * 0.5f);
                    float anchorCenterY = Mathf.Lerp(parentBounds.yMin, parentBounds.yMax, (_rectTransform.anchorMin.y + _rectTransform.anchorMax.y) * 0.5f);
                    localPoint.x -= anchorCenterX;
                    localPoint.y -= anchorCenterY;
                    _rectTransform.anchoredPosition = localPoint;
                }
                else
                {
                    _rectTransform.anchoredPosition = eventData.position;
                }
            }
            else
            {
                _rectTransform.anchoredPosition = eventData.position;
            }
        }

        Vector2 basePos = _hasHandSlot ? _baseAnchoredPosition : (Vector2)originalPosition;
        Vector3 baseScale = _hasHandSlot ? _baseScale : originalScale;
        float dragDeltaY = _rectTransform != null ? _rectTransform.anchoredPosition.y - basePos.y : 0f;

        if (dragDeltaY >= playDragThresholdY)
        {
            OnSelectHighlight();
            transform.localScale = baseScale * (zoomScale * 1.05f);
        }
        else
        {
            if (!bIsCardHighlight)
            {
                OffSelectHighlight();
            }
            transform.localScale = baseScale * zoomScale;
        }
    }

    /// <summary>
    /// 진행 중인 카드 드래그를 취소하고 원래 손패 슬롯으로 되돌립니다.
    /// </summary>
    public void CancelDrag()
    {
        SetDraggingState(false);
        _dragCanceled = true;
        isZoomed = false;

        HideCardKeywordTooltips();
        ClearTimelineCardPreview();
        OffSelectHighlight();

        if (bShowEffectAreaUI && UIManager.instance != null && !IsPlayerTargetSelectionActive())
        {
            UIManager.instance.ClearEffectAreaTiles();
        }

        ReturnToHandSlotAfterDrag();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        SetDraggingState(false);
        isZoomed = false;

        Vector2 basePos = _hasHandSlot ? _baseAnchoredPosition : (Vector2)originalPosition;
        float dragDeltaY = _rectTransform != null ? _rectTransform.anchoredPosition.y - basePos.y : 0f;
        bool shouldPlayCard = !_dragCanceled && dragDeltaY >= playDragThresholdY;

        HideCardKeywordTooltips();
        ClearTimelineCardPreview();
        OffSelectHighlight();
        ReturnToHandSlotAfterDrag();

        if (shouldPlayCard)
        {
            OnCardClick?.Invoke();
        }

        if (bShowEffectAreaUI && UIManager.instance != null && !IsPlayerTargetSelectionActive())
        {
            UIManager.instance.ClearEffectAreaTiles();
        }
    }

    private void ReturnToHandSlotAfterDrag()
    {
        EnsureCachedComponents();
        CancelHoverMotions();

        Vector2 targetPos = _hasHandSlot ? _baseAnchoredPosition : (Vector2)originalPosition;
        Vector3 targetScale = _hasHandSlot ? _baseScale : originalScale;
        int targetSiblingIndex = _hasHandSlot ? _baseSiblingIndex : originalSiblingIndex;

        transform.localRotation = Quaternion.identity;
        RestoreSiblingIndexSafely(targetSiblingIndex);

        if (!Application.isPlaying || !gameObject.activeInHierarchy || hoverMotionDuration <= 0f)
        {
            transform.localScale = targetScale;
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = targetPos;
            }
            else
            {
                transform.localPosition = targetPos;
            }
            return;
        }

        if (_rectTransform != null)
        {
            _hoverPosHandle = LMotion.Create(_rectTransform.anchoredPosition, targetPos, hoverMotionDuration)
                .WithEase(hoverMotionEase)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAnchoredPosition(_rectTransform);
        }

        _hoverScaleHandle = LMotion.Create(transform.localScale, targetScale, hoverMotionDuration)
            .WithEase(hoverMotionEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToLocalScale(transform);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isInteractable || _isEntering || _isDiscarding || _isDragging)
        {
            return;
        }

        if (bIsCardHighlight)
            OnSelectHighlight();       //카드 하이라이트on

        // 타깃팅 모드(TargetSelection) 진행 중이 아닐 때만 호버 범위 세팅 진행
        if (!IsPlayerTargetSelectionActive() && bShowEffectAreaUI && cardData != null && UIManager.instance != null)
        {
            UIManager.instance.UpdateEffectAreaUI(cardData);
        }

        if (enableZoom)
        {
            EnsureCachedComponents();
            OnHoverZoomStarted?.Invoke(this);

            if (!isZoomed)
            {
                isZoomed = true;
                if (_hasHandSlot)
                {
                    originalScale = _baseScale;
                    originalPosition = _baseAnchoredPosition;
                    originalSiblingIndex = _baseSiblingIndex;
                }
                else
                {
                    originalScale = transform.localScale;
                    originalPosition = _rectTransform != null ? (Vector3)_rectTransform.anchoredPosition : transform.localPosition;
                    originalSiblingIndex = transform.GetSiblingIndex();
                }
            }

            transform.SetAsLastSibling();
            PlayHoverZoomMotion(true);
        }

        // 호버 확대 상태(회전 초기화, 형제 순서, 줌 모션 시작)가 반영된 직후 툴팁 기준 사분면/경계를 잡도록 후속 배치
        ShowCardKeywordTooltips();
        ShowTimelineCardPreview();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_isDragging)
        {
            return;
        }

        HideCardKeywordTooltips();
        ClearTimelineCardPreview();

        if (bIsCardHighlight)
            OffSelectHighlight();       //카드 하이라이트off

        if (!_isInteractable || _isDiscarding)
        {
            return;
        }

        if (bShowEffectAreaUI)
        {
            // 타깃팅 모드(TargetSelection) 진행 중일 때는 마우스가 손패를 벗어나도 사거리/AoE 프리뷰 유지
            if (!IsPlayerTargetSelectionActive() && UIManager.instance != null)
            {
                UIManager.instance.ClearEffectAreaTiles();
            }
        }

        if (enableZoom && isZoomed)
        {
            isZoomed = false;
            RestoreSiblingIndexSafely(_hasHandSlot ? _baseSiblingIndex : originalSiblingIndex);
            PlayHoverZoomMotion(false);
        }
    }
}
