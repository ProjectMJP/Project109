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
using UnityEngine.UI;

public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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

    public UnityEvent OnCardClick;  //클릭 시 호출될 이벤트(다양한 변수들도 쉽게 호출하기 위해 UnityEvent 사용)

    public bool bIsCardHighlight;
    public bool bShowEffectAreaUI;

    [Header("Zoom Settings")]
    public bool enableZoom = false;
    public float zoomScale = 1.1f;
    public float zoomYOffset = 90f;
    [SerializeField] private float hoverMotionDuration = 0.12f;
    [SerializeField] private Ease hoverMotionEase = Ease.OutCubic;

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

    private static readonly Vector2 DefaultCardSize = new Vector2(185f, 325f);
    private static readonly Vector2 CenterAnchorPivot = new Vector2(0.5f, 0.5f);

    /// <summary>
    /// 카드 호버 줌이 시작될 때 외부 레이아웃 트윈과의 충돌을 막기 위해 호출되는 이벤트입니다.
    /// </summary>
    public event Action<CardUI> OnHoverZoomStarted;

    public bool IsInteractable => _isInteractable;
    public bool IsZoomed => isZoomed;
    public bool IsEntering => _isEntering;
    public bool IsDiscarding => _isDiscarding;

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
        HideCardKeywordTooltips();
        bool hadActiveHoverMotion = _hoverPosHandle.IsActive() || _hoverScaleHandle.IsActive();
        CancelHoverMotions();

        if (bIsCardHighlight)
        {
            OffSelectHighlight();
        }

        if (bShowEffectAreaUI && UIManager.instance != null)
        {
            bool isTargeting = RunManager.instance != null &&
                               RunManager.instance.playerBattleController != null &&
                               RunManager.instance.playerBattleController.controlState == PlayerControlState.TargetSelection;

            if (!isTargeting)
            {
                UIManager.instance.ClearEffectAreaTiles();
            }
        }

        if (!_isDiscarding && (isZoomed || hadActiveHoverMotion))
        {
            isZoomed = false;
            RestoreRestingTransformImmediate();
        }
        else
        {
            isZoomed = false;
        }
    }

    private void OnDestroy()
    {
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
            HideCardKeywordTooltips();
            CancelHoverMotions();

            if (bIsCardHighlight)
            {
                OffSelectHighlight();
            }

            if (isZoomed)
            {
                if (bShowEffectAreaUI && UIManager.instance != null)
                {
                    bool isTargeting = RunManager.instance != null &&
                                       RunManager.instance.playerBattleController != null &&
                                       RunManager.instance.playerBattleController.controlState == PlayerControlState.TargetSelection;

                    if (!isTargeting)
                    {
                        UIManager.instance.ClearEffectAreaTiles();
                    }
                }

                isZoomed = false;
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

        if (isZoomed && enableZoom && _isInteractable && !_isDiscarding)
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
        CancelHoverMotions();

        cardInstance = null;
        cardData = null;

        isZoomed = false;
        _isEntering = false;
        _isDiscarding = false;
        _hasHandSlot = false;

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

        if (!gameObject.activeInHierarchy || hoverMotionDuration <= 0f)
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
                .BindToAnchoredPosition(_rectTransform);
        }

        _hoverScaleHandle = LMotion.Create(transform.localScale, targetScale, hoverMotionDuration)
            .WithEase(hoverMotionEase)
            .BindToLocalScale(transform);
    }

    private void RestoreRestingTransformImmediate()
    {
        EnsureCachedComponents();

        if (_hasHandSlot)
        {
            transform.localScale = _baseScale;
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _baseAnchoredPosition;
            }
            RestoreSiblingIndexSafely(_baseSiblingIndex);
        }
        else if (enableZoom)
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isInteractable || _isDiscarding) return;
        HideCardKeywordTooltips();
        OnCardClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isInteractable || _isEntering || _isDiscarding)
        {
            return;
        }

        ShowCardKeywordTooltips();

        if (bIsCardHighlight)
            OnSelectHighlight();       //카드 하이라이트on

        // 타깃팅 모드(TargetSelection) 진행 중이 아닐 때만 호버 범위 세팅 진행
        bool isTargeting = RunManager.instance != null &&
                           RunManager.instance.playerBattleController != null &&
                           RunManager.instance.playerBattleController.controlState == PlayerControlState.TargetSelection;

        if (!isTargeting && bShowEffectAreaUI && cardData != null && UIManager.instance != null)
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
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideCardKeywordTooltips();

        if (bIsCardHighlight)
            OffSelectHighlight();       //카드 하이라이트off

        if (!_isInteractable || _isDiscarding)
        {
            return;
        }

        if (bShowEffectAreaUI)
        {
            // 타깃팅 모드(TargetSelection) 진행 중일 때는 마우스가 손패를 벗어나도 사거리/AoE 프리뷰 유지
            bool isTargeting = RunManager.instance != null &&
                               RunManager.instance.playerBattleController != null &&
                               RunManager.instance.playerBattleController.controlState == PlayerControlState.TargetSelection;

            if (!isTargeting && UIManager.instance != null)
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
