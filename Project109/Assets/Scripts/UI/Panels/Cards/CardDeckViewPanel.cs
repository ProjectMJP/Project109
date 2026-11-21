using System;
using System.Collections.Generic;
using GameItem.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 단일 통합 카드 목록 및 그리드 선택 UI 패널 (슬레이 더 스파이어의 GridCardSelectScreen 모델).
/// 마스터 덱, 뽑을 덱, 버린 덱, 소멸 덱 열람(ViewOnly) 및 카드 제거/선택(Single/MultiSelect), 강화(UpgradeSelect), 카드 도감을 통합 처리합니다.
/// </summary>
public class CardDeckViewPanel : UIPanelBase, IOnAddCard, IOnRemoveCard, IOnCardUpgrade, IOnCardsRefreshed, IOnCardMasteryUpgrade
{
    public enum ClassFilter
    {
        All = 0,
        Warrior = 1,
        Common = 2
    }

    public enum RarityFilter
    {
        All = 0,
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Unique = 4
    }

    public enum TypeFilter
    {
        All = 0,
        Attack = 1,
        Skill = 2
    }

    public enum ObtainSourceFilter
    {
        All = 0,
        Standard = 1,
        Starter = 2,
        EventOnly = 3
    }

    public enum SortCriteria
    {
        Rarity = 0,
        Cost = 1,
        Name = 2
    }

    [Header("UI References")]
    public Transform contentTransform;
    public TextMeshProUGUI titleText;
    public Button confirmButton;
    public TextMeshProUGUI confirmButtonText;
    public Button closeButton;

    [Header("Filter & Sort Controls")]
    [SerializeField] private GameObject filterSortBarRoot;
    [SerializeField] private Button classFilterButton;
    [SerializeField] private TextMeshProUGUI classFilterText;
    [SerializeField] private Button rarityFilterButton;
    [SerializeField] private TextMeshProUGUI rarityFilterText;
    [SerializeField] private Button typeFilterButton;
    [SerializeField] private TextMeshProUGUI typeFilterText;
    [SerializeField] private Button obtainSourceFilterButton;
    [SerializeField] private TextMeshProUGUI obtainSourceFilterText;
    [SerializeField] private Button sortCriteriaButton;
    [SerializeField] private TextMeshProUGUI sortCriteriaText;
    [SerializeField] private Button sortDirectionButton;
    [SerializeField] private TextMeshProUGUI sortDirectionText;

    [Header("Selection State")]
    public CardViewMode currentMode { get; private set; } = CardViewMode.ViewOnly;
    public int targetSelectCount { get; private set; } = 1;
    public readonly List<Card> selectedCards = new List<Card>();

    private readonly Dictionary<int, GameObject> activeCardUIs = new Dictionary<int, GameObject>();
    private readonly List<Card> _sourceCards = new List<Card>();
    private readonly List<Card> currentDisplayedCards = new List<Card>();

    private Action<List<Card>> _onConfirmCallback;
    private Action _onCancelCallback;
    private bool _isMasterDeckMode = false;
    private bool _isCompendiumMode = false;
    private bool _filterSortListenersBound = false;
    private CardDetailPanel _cardDetailHandler;

    private ClassFilter _classFilter = ClassFilter.All;
    private RarityFilter _rarityFilter = RarityFilter.All;
    private TypeFilter _typeFilter = TypeFilter.All;
    private ObtainSourceFilter _obtainSourceFilter = ObtainSourceFilter.All;
    private SortCriteria _sortCriteria = SortCriteria.Rarity;
    private bool _isSortAscending = true;

    public bool IsCompendiumMode => _isCompendiumMode;
    public ClassFilter CurrentClassFilter => _classFilter;
    public RarityFilter CurrentRarityFilter => _rarityFilter;
    public TypeFilter CurrentTypeFilter => _typeFilter;
    public ObtainSourceFilter CurrentObtainSourceFilter => _obtainSourceFilter;
    public SortCriteria CurrentSortCriteria => _sortCriteria;
    public bool IsSortAscending => _isSortAscending;
    public IReadOnlyList<Card> CurrentDisplayedCards => currentDisplayedCards;

    private Player GetPlayer() => GameSceneManager.instance != null ? GameSceneManager.instance.player : null;

    private void Awake()
    {
        // 인스펙터 미할당 시 자식 오브젝트 자동 탐색 (Graceful Fallback)
        ResolveComponentReferences();
        EnsureFilterSortBar();
        BindFilterSortListeners();

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
        UnbindFilterSortListeners();
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
        OpenInternal(title, cards, mode, targetSelectCount, onConfirm, onCancel, isMasterDeck: false, isCompendium: false);
    }

    /// <summary>
    /// 카드 도감 전용 모드로 패널을 엽니다.
    /// 클래스·등급·타입·획득처 필터와 공용 정렬 컨트롤을 모두 노출하며 상태를 초기화합니다.
    /// </summary>
    public void OpenCompendium(
        string title,
        IReadOnlyList<Card> cards,
        Action onCancel = null)
    {
        OpenInternal(title, cards, CardViewMode.ViewOnly, 1, null, onCancel, isMasterDeck: false, isCompendium: true);
    }

    private void OpenInternal(
        string title,
        IReadOnlyList<Card> cards,
        CardViewMode mode,
        int targetSelectCount,
        Action<List<Card>> onConfirm,
        Action onCancel,
        bool isMasterDeck,
        bool isCompendium)
    {
        _isMasterDeckMode = isMasterDeck;
        _isCompendiumMode = isCompendium;
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

        _sourceCards.Clear();
        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    _sourceCards.Add(cards[i]);
                }
            }
        }

        EnsureFilterSortBar();
        BindFilterSortListeners();
        ResetFilterAndSortState();
        UpdateFilterSortControlBarUI();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        ApplyFilterSortAndRefreshUI();
    }

    /// <summary>
    /// 플레이어의 마스터 덱을 기본 ViewOnly 모드로 엽니다.
    /// 마스터 덱 카드 추가/제거/강화 이벤트를 실시간 반영합니다.
    /// </summary>
    public void OpenMasterDeck()
    {
        var p = GetPlayer();
        var cards = (p != null && p.deck != null) ? p.deck.GetCards() : new List<Card>();
        OpenInternal("전체 덱", cards, CardViewMode.ViewOnly, 1, null, null, isMasterDeck: true, isCompendium: false);
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

    #region Filter & Sort Logic and Controls

    public void CycleClassFilter()
    {
        _classFilter = (ClassFilter)(((int)_classFilter + 1) % 3);
        UpdateFilterSortControlBarUI();
        ApplyFilterSortAndRefreshUI();
    }

    public void CycleRarityFilter()
    {
        _rarityFilter = (RarityFilter)(((int)_rarityFilter + 1) % 5);
        UpdateFilterSortControlBarUI();
        ApplyFilterSortAndRefreshUI();
    }

    public void CycleTypeFilter()
    {
        _typeFilter = (TypeFilter)(((int)_typeFilter + 1) % 3);
        UpdateFilterSortControlBarUI();
        ApplyFilterSortAndRefreshUI();
    }

    public void CycleObtainSourceFilter()
    {
        _obtainSourceFilter = (ObtainSourceFilter)(((int)_obtainSourceFilter + 1) % 4);
        UpdateFilterSortControlBarUI();
        ApplyFilterSortAndRefreshUI();
    }

    public void CycleSortCriteria()
    {
        _sortCriteria = (SortCriteria)(((int)_sortCriteria + 1) % 3);
        UpdateFilterSortControlBarUI();
        ApplyFilterSortAndRefreshUI();
    }

    public void ToggleSortDirection()
    {
        _isSortAscending = !_isSortAscending;
        UpdateFilterSortControlBarUI();
        ApplyFilterSortAndRefreshUI();
    }

    /// <summary>
    /// 카드 목록에 필터링(도감 모드 시) 및 정렬을 순수하게 적용한 새 리스트를 반환합니다.
    /// </summary>
    public static List<Card> FilterAndSortCards(
        IReadOnlyList<Card> sourceCards,
        bool applyCompendiumFilters,
        ClassFilter classFilter,
        RarityFilter rarityFilter,
        TypeFilter typeFilter,
        ObtainSourceFilter obtainSourceFilter,
        SortCriteria sortCriteria,
        bool isAscending)
    {
        List<Card> result = new List<Card>();
        FilterAndSortCardsInto(
            sourceCards,
            result,
            applyCompendiumFilters,
            classFilter,
            rarityFilter,
            typeFilter,
            obtainSourceFilter,
            sortCriteria,
            isAscending);
        return result;
    }

    /// <summary>
    /// 카드 목록에 필터링 및 정렬을 적용하여 지정된 출력 리스트에 채웁니다.
    /// </summary>
    public static void FilterAndSortCardsInto(
        IReadOnlyList<Card> sourceCards,
        List<Card> outputCards,
        bool applyCompendiumFilters,
        ClassFilter classFilter,
        RarityFilter rarityFilter,
        TypeFilter typeFilter,
        ObtainSourceFilter obtainSourceFilter,
        SortCriteria sortCriteria,
        bool isAscending)
    {
        if (outputCards == null) return;
        outputCards.Clear();

        if (sourceCards == null) return;

        for (int i = 0; i < sourceCards.Count; i++)
        {
            Card card = sourceCards[i];
            if (card == null) continue;

            if (applyCompendiumFilters &&
                !MatchesCardCompendiumFilters(card.cardData, classFilter, rarityFilter, typeFilter, obtainSourceFilter))
            {
                continue;
            }

            outputCards.Add(card);
        }

        outputCards.Sort((left, right) => CompareCards(left, right, sortCriteria, isAscending));
    }

    public static bool MatchesCardCompendiumFilters(
        CardData cardData,
        ClassFilter classFilter,
        RarityFilter rarityFilter,
        TypeFilter typeFilter,
        ObtainSourceFilter obtainSourceFilter)
    {
        if (cardData == null) return false;

        if (!MatchesClassFilter(cardData.classTypes, classFilter))
        {
            return false;
        }

        switch (rarityFilter)
        {
            case RarityFilter.Common:
                if (cardData.rarity != CardRarity.Common) return false;
                break;
            case RarityFilter.Uncommon:
                if (cardData.rarity != CardRarity.Uncommon) return false;
                break;
            case RarityFilter.Rare:
                if (cardData.rarity != CardRarity.Rare) return false;
                break;
            case RarityFilter.Unique:
                if (cardData.rarity != CardRarity.Unique) return false;
                break;
        }

        switch (typeFilter)
        {
            case TypeFilter.Attack:
                if (!string.Equals(cardData.cardType, "Attack", StringComparison.OrdinalIgnoreCase)) return false;
                break;
            case TypeFilter.Skill:
                if (!string.Equals(cardData.cardType, "Skill", StringComparison.OrdinalIgnoreCase)) return false;
                break;
        }

        switch (obtainSourceFilter)
        {
            case ObtainSourceFilter.Standard:
                if (cardData.obtainSource != ItemObtainSource.Standard) return false;
                break;
            case ObtainSourceFilter.Starter:
                if (cardData.obtainSource != ItemObtainSource.Starter) return false;
                break;
            case ObtainSourceFilter.EventOnly:
                if (cardData.obtainSource != ItemObtainSource.EventOnly) return false;
                break;
        }

        return true;
    }

    private static bool MatchesClassFilter(List<string> classTypes, ClassFilter classFilter)
    {
        switch (classFilter)
        {
            case ClassFilter.All:
                return true;

            case ClassFilter.Warrior:
                if (classTypes == null) return false;
                for (int i = 0; i < classTypes.Count; i++)
                {
                    string cls = classTypes[i];
                    if (string.Equals(cls, "Warrior", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(cls, "TesterWarrior", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(cls, "전투광", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;

            case ClassFilter.Common:
                if (classTypes == null || classTypes.Count == 0) return true;
                bool hasNonEmptyEntry = false;
                for (int i = 0; i < classTypes.Count; i++)
                {
                    string cls = classTypes[i];
                    if (string.IsNullOrWhiteSpace(cls)) continue;
                    hasNonEmptyEntry = true;
                    if (string.Equals(cls, "All", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(cls, "None", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(cls, "공용", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return !hasNonEmptyEntry;

            default:
                return true;
        }
    }

    public static int CompareCards(Card left, Card right, SortCriteria sortCriteria, bool isAscending)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return -1;
        if (right == null) return 1;

        CardData leftData = left.cardData;
        CardData rightData = right.cardData;

        CardRarity leftRarity = leftData != null ? leftData.rarity : CardRarity.Common;
        CardRarity rightRarity = rightData != null ? rightData.rarity : CardRarity.Common;

        int leftCost = left.currentCost;
        int rightCost = right.currentCost;

        string leftName = leftData != null && !string.IsNullOrEmpty(leftData.LocalizedName) ? leftData.LocalizedName : left.cardName;
        string rightName = rightData != null && !string.IsNullOrEmpty(rightData.LocalizedName) ? rightData.LocalizedName : right.cardName;

        int primaryCompare;
        switch (sortCriteria)
        {
            case SortCriteria.Cost:
                primaryCompare = leftCost.CompareTo(rightCost);
                break;
            case SortCriteria.Name:
                primaryCompare = string.Compare(leftName, rightName, StringComparison.Ordinal);
                break;
            default:
                primaryCompare = leftRarity.CompareTo(rightRarity);
                break;
        }

        if (primaryCompare != 0)
        {
            return isAscending ? primaryCompare : -primaryCompare;
        }

        int rarityTie = leftRarity.CompareTo(rightRarity);
        if (rarityTie != 0) return rarityTie;

        int costTie = leftCost.CompareTo(rightCost);
        if (costTie != 0) return costTie;

        int nameTie = string.Compare(leftName, rightName, StringComparison.Ordinal);
        if (nameTie != 0) return nameTie;

        int idTie = string.Compare(left.cardName, right.cardName, StringComparison.Ordinal);
        if (idTie != 0) return idTie;

        return left.runtimeID.CompareTo(right.runtimeID);
    }

    private void ResetFilterAndSortState()
    {
        _classFilter = ClassFilter.All;
        _rarityFilter = RarityFilter.All;
        _typeFilter = TypeFilter.All;
        _obtainSourceFilter = ObtainSourceFilter.All;
        _sortCriteria = SortCriteria.Rarity;
        _isSortAscending = true;
    }

    private void ApplyFilterSortAndRefreshUI()
    {
        FilterAndSortCardsInto(
            _sourceCards,
            currentDisplayedCards,
            _isCompendiumMode,
            _classFilter,
            _rarityFilter,
            _typeFilter,
            _obtainSourceFilter,
            _sortCriteria,
            _isSortAscending);

        RefreshAllCardUIs();
    }

    private void EnsureFilterSortBar()
    {
        if (filterSortBarRoot != null)
        {
            ResolveFilterSortChildReferences(filterSortBarRoot.transform);
            return;
        }

        Transform existingBar = transform.Find("FilterSortBar");
        if (existingBar != null)
        {
            filterSortBarRoot = existingBar.gameObject;
            ResolveFilterSortChildReferences(existingBar);
            return;
        }

        TMP_FontAsset fontAsset = TMPFontUtility.ResolveDefaultFont();

        GameObject barObj = CreateUIObject("FilterSortBar", transform);
        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.05f, 0.86f);
        barRect.anchorMax = new Vector2(0.95f, 0.935f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;

        Image barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0.08f, 0.10f, 0.15f, 0.88f);

        HorizontalLayoutGroup layout = barObj.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 6, 6);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        filterSortBarRoot = barObj;

        Color filterBgColor = new Color(0.20f, 0.27f, 0.38f, 1f);
        Color sortBgColor = new Color(0.22f, 0.33f, 0.28f, 1f);

        classFilterButton = CreateControlButton("ClassFilterButton", barObj.transform, "클래스: 전체", filterBgColor, fontAsset, out classFilterText);
        rarityFilterButton = CreateControlButton("RarityFilterButton", barObj.transform, "등급: 전체", filterBgColor, fontAsset, out rarityFilterText);
        typeFilterButton = CreateControlButton("TypeFilterButton", barObj.transform, "타입: 전체", filterBgColor, fontAsset, out typeFilterText);
        obtainSourceFilterButton = CreateControlButton("ObtainSourceFilterButton", barObj.transform, "획득처: 전체", filterBgColor, fontAsset, out obtainSourceFilterText);
        sortCriteriaButton = CreateControlButton("SortCriteriaButton", barObj.transform, "정렬: 등급순", sortBgColor, fontAsset, out sortCriteriaText);
        sortDirectionButton = CreateControlButton("SortDirectionButton", barObj.transform, "▲ 오름차순", sortBgColor, fontAsset, out sortDirectionText);
    }

    private void ResolveFilterSortChildReferences(Transform barTransform)
    {
        if (barTransform == null) return;

        ResolveControlButton(barTransform, "ClassFilterButton", ref classFilterButton, ref classFilterText);
        ResolveControlButton(barTransform, "RarityFilterButton", ref rarityFilterButton, ref rarityFilterText);
        ResolveControlButton(barTransform, "TypeFilterButton", ref typeFilterButton, ref typeFilterText);
        ResolveControlButton(barTransform, "ObtainSourceFilterButton", ref obtainSourceFilterButton, ref obtainSourceFilterText);
        ResolveControlButton(barTransform, "SortCriteriaButton", ref sortCriteriaButton, ref sortCriteriaText);
        ResolveControlButton(barTransform, "SortDirectionButton", ref sortDirectionButton, ref sortDirectionText);
    }

    private static void ResolveControlButton(Transform barTransform, string childName, ref Button button, ref TextMeshProUGUI label)
    {
        if (button == null)
        {
            Transform child = barTransform.Find(childName);
            if (child != null)
            {
                button = child.GetComponent<Button>();
            }
        }

        if (button != null && label == null)
        {
            label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void BindFilterSortListeners()
    {
        if (_filterSortListenersBound) return;

        if (classFilterButton != null) classFilterButton.onClick.AddListener(CycleClassFilter);
        if (rarityFilterButton != null) rarityFilterButton.onClick.AddListener(CycleRarityFilter);
        if (typeFilterButton != null) typeFilterButton.onClick.AddListener(CycleTypeFilter);
        if (obtainSourceFilterButton != null) obtainSourceFilterButton.onClick.AddListener(CycleObtainSourceFilter);
        if (sortCriteriaButton != null) sortCriteriaButton.onClick.AddListener(CycleSortCriteria);
        if (sortDirectionButton != null) sortDirectionButton.onClick.AddListener(ToggleSortDirection);

        _filterSortListenersBound = true;
    }

    private void UnbindFilterSortListeners()
    {
        if (!_filterSortListenersBound) return;

        if (classFilterButton != null) classFilterButton.onClick.RemoveListener(CycleClassFilter);
        if (rarityFilterButton != null) rarityFilterButton.onClick.RemoveListener(CycleRarityFilter);
        if (typeFilterButton != null) typeFilterButton.onClick.RemoveListener(CycleTypeFilter);
        if (obtainSourceFilterButton != null) obtainSourceFilterButton.onClick.RemoveListener(CycleObtainSourceFilter);
        if (sortCriteriaButton != null) sortCriteriaButton.onClick.RemoveListener(CycleSortCriteria);
        if (sortDirectionButton != null) sortDirectionButton.onClick.RemoveListener(ToggleSortDirection);

        _filterSortListenersBound = false;
    }

    private void UpdateFilterSortControlBarUI()
    {
        if (filterSortBarRoot != null)
        {
            filterSortBarRoot.SetActive(true);
        }

        // 도감 전용 필터 버튼은 도감 모드에서만 노출
        if (classFilterButton != null) classFilterButton.gameObject.SetActive(_isCompendiumMode);
        if (rarityFilterButton != null) rarityFilterButton.gameObject.SetActive(_isCompendiumMode);
        if (typeFilterButton != null) typeFilterButton.gameObject.SetActive(_isCompendiumMode);
        if (obtainSourceFilterButton != null) obtainSourceFilterButton.gameObject.SetActive(_isCompendiumMode);

        // 공용 정렬 버튼은 도감 및 일반 덱 보기 모두 노출
        if (sortCriteriaButton != null) sortCriteriaButton.gameObject.SetActive(true);
        if (sortDirectionButton != null) sortDirectionButton.gameObject.SetActive(true);

        if (classFilterText != null) classFilterText.text = GetClassFilterLabel(_classFilter);
        if (rarityFilterText != null) rarityFilterText.text = GetRarityFilterLabel(_rarityFilter);
        if (typeFilterText != null) typeFilterText.text = GetTypeFilterLabel(_typeFilter);
        if (obtainSourceFilterText != null) obtainSourceFilterText.text = GetObtainSourceFilterLabel(_obtainSourceFilter);
        if (sortCriteriaText != null) sortCriteriaText.text = GetSortCriteriaLabel(_sortCriteria);
        if (sortDirectionText != null) sortDirectionText.text = _isSortAscending ? "▲ 오름차순" : "▼ 내림차순";
    }

    private static string GetClassFilterLabel(ClassFilter filter)
    {
        switch (filter)
        {
            case ClassFilter.Warrior: return "클래스: 전투광";
            case ClassFilter.Common: return "클래스: 공용";
            default: return "클래스: 전체";
        }
    }

    private static string GetRarityFilterLabel(RarityFilter filter)
    {
        switch (filter)
        {
            case RarityFilter.Common: return "등급: Common";
            case RarityFilter.Uncommon: return "등급: Uncommon";
            case RarityFilter.Rare: return "등급: Rare";
            case RarityFilter.Unique: return "등급: Unique";
            default: return "등급: 전체";
        }
    }

    private static string GetTypeFilterLabel(TypeFilter filter)
    {
        switch (filter)
        {
            case TypeFilter.Attack: return "타입: 공격(Attack)";
            case TypeFilter.Skill: return "타입: 스킬(Skill)";
            default: return "타입: 전체";
        }
    }

    private static string GetObtainSourceFilterLabel(ObtainSourceFilter filter)
    {
        switch (filter)
        {
            case ObtainSourceFilter.Standard: return "획득처: 일반(Standard)";
            case ObtainSourceFilter.Starter: return "획득처: 기본(Starter)";
            case ObtainSourceFilter.EventOnly: return "획득처: 이벤트(EventOnly)";
            default: return "획득처: 전체";
        }
    }

    private static string GetSortCriteriaLabel(SortCriteria criteria)
    {
        switch (criteria)
        {
            case SortCriteria.Cost: return "정렬: 코스트순";
            case SortCriteria.Name: return "정렬: 이름순";
            default: return "정렬: 등급순";
        }
    }

    private static Button CreateControlButton(
        string name,
        Transform parent,
        string initialText,
        Color bgColor,
        TMP_FontAsset fontAsset,
        out TextMeshProUGUI labelText)
    {
        GameObject btnObj = CreateUIObject(name, parent);
        LayoutElement layoutElem = btnObj.AddComponent<LayoutElement>();
        layoutElem.minWidth = 100f;
        layoutElem.flexibleWidth = 1f;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject textObj = CreateUIObject("Label", btnObj.transform);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 2f);
        textRect.offsetMax = new Vector2(-6f, -2f);

        labelText = textObj.AddComponent<TextMeshProUGUI>();
        labelText.text = initialText;
        labelText.fontSize = 16;
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 11;
        labelText.fontSizeMax = 17;
        labelText.fontStyle = FontStyles.Bold;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.raycastTarget = false;
        if (fontAsset != null)
        {
            labelText.font = fontAsset;
        }

        return btn;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void DestroyOrImmediate(GameObject target)
    {
        if (target == null) return;
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
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

                if (ObjectPoolManager.HasInstance && gameObject.scene.isLoaded)
                {
                    ObjectPoolManager.instance.ReturnCardUI(cardUIObject);
                }
                else
                {
                    DestroyOrImmediate(cardUIObject);
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

        if (currentDisplayedCards.Count > 0 && ObjectPoolManager.instance == null)
        {
            Debug.LogError("[CardDeckViewPanel] ObjectPoolManager is not initialized.");
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
            GameObject uiObj = UIManager.instance.OpenUI(UIConstants.PANEL_CARD_DETAIL, this.uiLayerType, false);
            if (uiObj != null)
            {
                _cardDetailHandler = uiObj.GetComponent<CardDetailPanel>();
            }
        }

        if (_cardDetailHandler != null)
        {
            _cardDetailHandler.uiLayerType = this.uiLayerType;
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
        var confirmCallback = _onConfirmCallback;
        _onConfirmCallback = null;
        _onCancelCallback = null;
        confirmCallback?.Invoke(result);

        Close();
    }

    public override void Close()
    {
        _onCancelCallback?.Invoke();
        _onConfirmCallback = null;
        _onCancelCallback = null;
        selectedCards.Clear();
        _sourceCards.Clear();
        currentDisplayedCards.Clear();
        _isMasterDeckMode = false;
        _isCompendiumMode = false;

        ClearAllCardUIs();

        base.Close();
    }

    #endregion

    #region Master Deck Event Handlers (IOnAddCard, IOnRemoveCard, etc.)

    public void OnAddCard(Card card)
    {
        if (!_isMasterDeckMode) return;
        if (card == null) return;

        _sourceCards.Add(card);
        ApplyFilterSortAndRefreshUI();
    }

    public void OnRemoveCard(Card card)
    {
        if (!_isMasterDeckMode) return;
        if (card == null) return;

        _sourceCards.Remove(card);
        currentDisplayedCards.Remove(card);
        if (activeCardUIs.TryGetValue(card.runtimeID, out GameObject cardUIObject))
        {
            if (cardUIObject != null)
            {
                if (ObjectPoolManager.HasInstance && gameObject.scene.isLoaded)
                {
                    ObjectPoolManager.instance.ReturnCardUI(cardUIObject);
                }
                else
                {
                    DestroyOrImmediate(cardUIObject);
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
            _sourceCards.Clear();
            var deckCards = p.deck.GetCards();
            if (deckCards != null)
            {
                for (int i = 0; i < deckCards.Count; i++)
                {
                    if (deckCards[i] != null)
                    {
                        _sourceCards.Add(deckCards[i]);
                    }
                }
            }
            ApplyFilterSortAndRefreshUI();
        }
    }

    public void OnCardMasteryUpgrade(Card card, string masteryId)
    {
        if (!_isMasterDeckMode) return;
        OnCardsRefreshed();
    }

    #endregion
}
