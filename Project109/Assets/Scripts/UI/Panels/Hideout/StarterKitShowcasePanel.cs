using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XLua;

/// <summary>
/// 은신처 무장 진열대 전체화면 오버레이 팝업 패널입니다.
/// 코어 및 모드 시작 무장을 좌우 스크롤 목록으로 전시하고, 장착·해금·시작 카드 미리보기·무기 역사 열람을 제공합니다.
/// </summary>
public class StarterKitShowcasePanel : UIPanelBase
{
    [Header("Panel Controls")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI shardBalanceText;
    [SerializeField] private TextMeshProUGUI statusMessageText;
    [SerializeField] private Button closeButton;

    [Header("Horizontal Scroll Showcase")]
    [SerializeField] private ScrollRect showcaseScrollRect;
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private StarterKitSlotUI slotTemplate;

    private readonly List<StarterKitSlotUI> _spawnedSlots = new List<StarterKitSlotUI>();
    private readonly List<StarterKitData> _sortedKits = new List<StarterKitData>();
    private readonly List<Card> _previewCards = new List<Card>();
    private Player _subscribedPlayer;
    private CardDeckViewPanel _openedDeckPanel;

    public override bool DestroyOnClose => false;
    public override bool CanCloseByCancel => true;

    private void Awake()
    {
        uiLayerType = UILayerType.Popup;
        blockWorldInput = true;

        EnsureHierarchyAndReferences();

        if (slotTemplate != null)
        {
            slotTemplate.gameObject.SetActive(false);
        }

        RegisterPanelListeners();
        LuaManager.OnBeforeLuaEnvDispose -= DisposePreviewCards;
        LuaManager.OnBeforeLuaEnvDispose += DisposePreviewCards;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        RegisterPanelListeners();
        RefreshShowcase();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnsubscribePlayerEvents();
    }

    public override void OnOpen()
    {
        ClearStatusMessage();
        RefreshShowcase();

        if (showcaseScrollRect != null)
        {
            showcaseScrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    public override void OnClose()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }

        for (int i = 0; i < _spawnedSlots.Count; i++)
        {
            if (_spawnedSlots[i] != null && _spawnedSlots[i].IsHistoryOpen)
            {
                _spawnedSlots[i].SetHistoryOpen(false, notifyLayout: false);
            }
        }

        // Start Cards 보기용으로 열어두었던 CardDeckViewPanel 참조를 정리합니다.
        if (_openedDeckPanel != null && !_openedDeckPanel.gameObject.activeSelf)
        {
            _openedDeckPanel = null;
        }
    }

    private void OnDestroy()
    {
        LuaManager.OnBeforeLuaEnvDispose -= DisposePreviewCards;
        UnsubscribePlayerEvents();
        DisposePreviewCards();
        ClearSpawnedSlots();
    }

    /// <summary>
    /// ModLoader의 시작 키트 데이터베이스를 코어 -> 모드 순으로 정렬하여 슬롯 목록을 갱신합니다.
    /// </summary>
    public void RefreshShowcase()
    {
        EnsureHierarchyAndReferences();

        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        BindPlayerEvents(player);
        UpdateShardBalanceText(player);

        if (titleText != null)
        {
            titleText.text = "무장 진열대";
        }

        CollectSortedStarterKits();
        RebuildSlots(player);
    }

    private void CollectSortedStarterKits()
    {
        _sortedKits.Clear();

        if (ModLoader.Instance == null || ModLoader.Instance.StarterKitDatabase == null)
        {
            return;
        }

        foreach (StarterKitData kit in ModLoader.Instance.StarterKitDatabase.Values)
        {
            if (kit != null)
            {
                _sortedKits.Add(kit);
            }
        }

        _sortedKits.Sort(CompareStarterKits);
    }

    private static int CompareStarterKits(StarterKitData a, StarterKitData b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        // 1. 바닐라(Core) 우선, 유저 모드 후순위 배치
        int coreCompare = b.isCoreMod.CompareTo(a.isCoreMod);
        if (coreCompare != 0) return coreCompare;

        // 2. 명시적 정렬 순서
        int orderCompare = a.sortOrder.CompareTo(b.sortOrder);
        if (orderCompare != 0) return orderCompare;

        // 3. 모드 로더 로딩 순서
        int loadCompare = a.loadOrder.CompareTo(b.loadOrder);
        if (loadCompare != 0) return loadCompare;

        return string.Compare(a.loadoutId, b.loadoutId, StringComparison.Ordinal);
    }

    private void RebuildSlots(Player player)
    {
        ClearSpawnedSlots();
        if (slotContainer == null || slotTemplate == null) return;

        TMP_FontAsset sharedFont = TMPFontUtility.ResolveDefaultFont(titleText, shardBalanceText, statusMessageText);

        for (int i = 0; i < _sortedKits.Count; i++)
        {
            StarterKitData kit = _sortedKits[i];
            StarterKitSlotUI slotInstance = Instantiate(slotTemplate, slotContainer, false);
            slotInstance.name = $"StarterKitSlot_{kit.loadoutId}";
            slotInstance.gameObject.SetActive(true);
            slotInstance.Bind(
                kit,
                player,
                sharedFont,
                HandleWeaponClicked,
                HandleViewStartCardsClicked,
                HandleSlotLayoutChanged);

            _spawnedSlots.Add(slotInstance);
        }

        RebuildScrollContainerLayout();
    }

    private void RefreshSlotStatesOnly(Player player)
    {
        for (int i = 0; i < _spawnedSlots.Count; i++)
        {
            if (_spawnedSlots[i] != null)
            {
                _spawnedSlots[i].RefreshState(player);
            }
        }
    }

    private void ClearSpawnedSlots()
    {
        for (int i = 0; i < _spawnedSlots.Count; i++)
        {
            if (_spawnedSlots[i] != null)
            {
                _spawnedSlots[i].Clear();
                Destroy(_spawnedSlots[i].gameObject);
            }
        }
        _spawnedSlots.Clear();
    }

    private void HandleWeaponClicked(StarterKitData kitData)
    {
        if (kitData == null) return;

        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player == null)
        {
            ShowStatusMessage("플레이어 데이터를 찾을 수 없습니다.", isError: true);
            return;
        }

        if (GameSceneManager.instance != null && GameSceneManager.instance.isInDungeon)
        {
            ShowStatusMessage("던전 탐색 중에는 시작 무장을 변경할 수 없습니다.", isError: true);
            return;
        }

        if (player.IsStarterKitUnlocked(kitData))
        {
            if (string.Equals(player.selectedStarterKitId, kitData.loadoutId, StringComparison.Ordinal))
            {
                ShowStatusMessage($"'{kitData.LocalizedDisplayName}' 무장이 이미 장착되어 있습니다.", isError: false);
                return;
            }

            if (SaveSystem.TryEquipStarterKit(player, kitData))
            {
                RefreshSlotStatesOnly(player);
                ShowStatusMessage($"'{kitData.LocalizedDisplayName}' 무장을 장착했습니다.", isError: false);
            }
            return;
        }

        if (SaveSystem.TryUnlockAndEquipStarterKit(player, kitData, out string failReason))
        {
            UpdateShardBalanceText(player);
            RefreshSlotStatesOnly(player);
            ShowStatusMessage($"'{kitData.LocalizedDisplayName}' 무장을 해금하고 장착했습니다!", isError: false);
        }
        else
        {
            ShowStatusMessage(failReason, isError: true);
        }
    }

    private void HandleViewStartCardsClicked(StarterKitData kitData)
    {
        if (kitData == null || UIManager.instance == null) return;

        BuildPreviewCards(kitData);

        CardDeckViewPanel deckPanel = UIManager.instance.OpenUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK, UILayerType.Popup, false);
        if (deckPanel == null)
        {
            ShowStatusMessage("시작 카드 보기 패널을 열 수 없습니다.", isError: true);
            return;
        }

        _openedDeckPanel = deckPanel;
        deckPanel.uiLayerType = UILayerType.Popup;
        UIManager.instance.PushActiveUIPanel(deckPanel);

        string panelTitle = $"{kitData.LocalizedDisplayName} 시작 카드";
        deckPanel.Open(panelTitle, _previewCards, CardViewMode.ViewOnly);
    }

    private void BuildPreviewCards(StarterKitData kitData)
    {
        DisposePreviewCards();

        if (kitData == null || kitData.startCards == null ||
            ModLoader.Instance == null || ModLoader.Instance.CardDatabase == null)
        {
            return;
        }

        for (int i = 0; i < kitData.startCards.Count; i++)
        {
            string cardId = kitData.startCards[i];
            if (string.IsNullOrEmpty(cardId)) continue;

            if (!ModLoader.Instance.CardDatabase.TryGetValue(cardId, out CardData cardData) || cardData == null)
            {
                Debug.LogWarning($"[StarterKitShowcasePanel] 시작 카드 '{cardId}'를 CardDatabase에서 찾을 수 없습니다.");
                continue;
            }

            LuaTable luaLogic = null;
            if (cardData.luaPrototype != null && LuaManager.Instance != null)
            {
                luaLogic = LuaManager.Instance.NewInstance(cardData.luaPrototype);
            }

            // 미리보기 전용 임시 카드이므로 owner는 null, 음수 runtimeID를 부여해 실제 덱 카드와 ID 충돌 방지
            Card previewCard = new Card(cardData, null, luaLogic, -30000 - i);
            _previewCards.Add(previewCard);
        }
    }

    private void DisposePreviewCards()
    {
        for (int i = 0; i < _previewCards.Count; i++)
        {
            if (_previewCards[i] != null)
            {
                _previewCards[i].Dispose();
            }
        }
        _previewCards.Clear();
    }

    private void HandleSlotLayoutChanged(StarterKitSlotUI changedSlot)
    {
        RebuildScrollContainerLayout();
    }

    private void RebuildScrollContainerLayout()
    {
        if (slotContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer);
        }
    }

    private void BindPlayerEvents(Player player)
    {
        if (ReferenceEquals(_subscribedPlayer, player)) return;

        UnsubscribePlayerEvents();
        _subscribedPlayer = player;

        if (_subscribedPlayer != null && _subscribedPlayer.playerStat != null)
        {
            _subscribedPlayer.playerStat.OnMemorySharpChanged += HandleMemorySharpChanged;
        }
    }

    private void UnsubscribePlayerEvents()
    {
        if (_subscribedPlayer != null && _subscribedPlayer.playerStat != null)
        {
            _subscribedPlayer.playerStat.OnMemorySharpChanged -= HandleMemorySharpChanged;
        }
        _subscribedPlayer = null;
    }

    private void HandleMemorySharpChanged(int currentShards)
    {
        UpdateShardBalanceText(_subscribedPlayer);
        RefreshSlotStatesOnly(_subscribedPlayer);
    }

    private void UpdateShardBalanceText(Player player)
    {
        if (shardBalanceText == null) return;

        int shards = (player != null && player.playerStat != null)
            ? player.playerStat.InGameCurrencyMemorySharp
            : 0;
        shardBalanceText.text = $"보유 기억의 조각: <color=#FFE066>{shards}</color>";
    }

    private void ShowStatusMessage(string message, bool isError)
    {
        if (statusMessageText == null) return;

        statusMessageText.text = message;
        statusMessageText.color = isError
            ? new Color(1f, 0.56f, 0.52f, 1f)
            : new Color(0.62f, 0.90f, 1f, 1f);
    }

    private void ClearStatusMessage()
    {
        if (statusMessageText != null)
        {
            statusMessageText.text = string.Empty;
        }
    }

    private void RegisterPanelListeners()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(HandleCloseClicked);
        }
    }

    private void HandleCloseClicked()
    {
        Close();
    }

    private void EnsureHierarchyAndReferences()
    {
        if (titleText == null) titleText = FindComponent<TextMeshProUGUI>("MainContainer/Header/TitleText");
        if (statusMessageText == null) statusMessageText = FindComponent<TextMeshProUGUI>("MainContainer/Header/StatusMessageText");
        if (shardBalanceText == null) shardBalanceText = FindComponent<TextMeshProUGUI>("MainContainer/Header/ShardBalanceText");
        if (closeButton == null) closeButton = FindComponent<Button>("MainContainer/Header/CloseButton");
        if (showcaseScrollRect == null) showcaseScrollRect = FindComponent<ScrollRect>("MainContainer/ShowcaseScrollView");
        if (slotContainer == null) slotContainer = FindComponent<RectTransform>("MainContainer/ShowcaseScrollView/Viewport/Content");
        if (slotTemplate == null) slotTemplate = FindComponent<StarterKitSlotUI>("MainContainer/ShowcaseScrollView/Viewport/Content/StarterKitSlotTemplate");

        if (closeButton != null && showcaseScrollRect != null && slotContainer != null && slotTemplate != null)
        {
            return;
        }

        BuildFallbackHierarchy();
    }

    private T FindComponent<T>(string childPath) where T : Component
    {
        Transform found = transform.Find(childPath);
        return found != null ? found.GetComponent<T>() : null;
    }

    private void BuildFallbackHierarchy()
    {
        RectTransform rootRect = transform as RectTransform;
        StretchFull(rootRect);

        TMP_FontAsset font = TMPFontUtility.ResolveDefaultFont(titleText, shardBalanceText, statusMessageText);

        // 1. 전체화면 반투명 딤 배경
        GameObject dimObj = CreateChild("DimBackground", transform);
        StretchFull(dimObj.GetComponent<RectTransform>());
        Image dimImg = dimObj.AddComponent<Image>();
        dimImg.color = new Color(0.04f, 0.05f, 0.08f, 0.86f);
        dimImg.raycastTarget = true;

        // 2. 중앙 메인 컨테이너
        GameObject containerObj = CreateChild("MainContainer", transform);
        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.04f, 0.05f);
        containerRect.anchorMax = new Vector2(0.96f, 0.95f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = Vector2.zero;
        Image containerBg = containerObj.AddComponent<Image>();
        containerBg.color = new Color(0.10f, 0.12f, 0.17f, 0.97f);

        // 2-1. 상단 헤더
        GameObject headerObj = CreateChild("Header", containerObj.transform);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 92f);

        GameObject titleObj = CreateChild("TitleText", headerObj.transform);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(0.28f, 1f);
        titleRect.offsetMin = new Vector2(32f, 0f);
        titleRect.offsetMax = Vector2.zero;
        titleText = CreateText(titleObj, "무장 진열대", 34f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft, font);

        GameObject statusObj = CreateChild("StatusMessageText", headerObj.transform);
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.28f, 0f);
        statusRect.anchorMax = new Vector2(0.64f, 1f);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;
        statusMessageText = CreateText(statusObj, string.Empty, 21f, FontStyles.Normal, new Color(0.62f, 0.90f, 1f, 1f), TextAlignmentOptions.Center, font);

        GameObject shardObj = CreateChild("ShardBalanceText", headerObj.transform);
        RectTransform shardRect = shardObj.GetComponent<RectTransform>();
        shardRect.anchorMin = new Vector2(0.64f, 0f);
        shardRect.anchorMax = new Vector2(0.86f, 1f);
        shardRect.offsetMin = Vector2.zero;
        shardRect.offsetMax = new Vector2(-16f, 0f);
        shardBalanceText = CreateText(shardObj, "보유 기억의 조각: 0", 23f, FontStyles.Bold, new Color(0.88f, 0.92f, 0.98f, 1f), TextAlignmentOptions.MidlineRight, font);

        GameObject closeObj = CreateChild("CloseButton", headerObj.transform);
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-28f, 0f);
        closeRect.sizeDelta = new Vector2(180f, 52f);
        Image closeImg = closeObj.AddComponent<Image>();
        closeImg.color = new Color(0.58f, 0.20f, 0.22f, 1f);
        closeButton = closeObj.AddComponent<Button>();
        GameObject closeTextObj = CreateChild("Text (TMP)", closeObj.transform);
        StretchFull(closeTextObj.GetComponent<RectTransform>());
        TextMeshProUGUI closeLabel = CreateText(closeTextObj, "진열대 닫기", 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, font);
        closeLabel.raycastTarget = false;

        // 2-2. 가로 스크롤 영역
        GameObject scrollObj = CreateChild("ShowcaseScrollView", containerObj.transform);
        RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
        StretchFull(scrollRect);
        scrollRect.offsetMin = new Vector2(24f, 24f);
        scrollRect.offsetMax = new Vector2(-24f, -100f);

        showcaseScrollRect = scrollObj.AddComponent<ScrollRect>();
        showcaseScrollRect.horizontal = true;
        showcaseScrollRect.vertical = false;
        showcaseScrollRect.movementType = ScrollRect.MovementType.Elastic;
        showcaseScrollRect.elasticity = 0.1f;
        showcaseScrollRect.scrollSensitivity = 35f;

        GameObject viewportObj = CreateChild("Viewport", scrollObj.transform);
        RectTransform vpRect = viewportObj.GetComponent<RectTransform>();
        StretchFull(vpRect);
        Image vpImg = viewportObj.AddComponent<Image>();
        vpImg.color = Color.white;
        Mask vpMask = viewportObj.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        showcaseScrollRect.viewport = vpRect;

        GameObject contentObj = CreateChild("Content", viewportObj.transform);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup contentLayout = contentObj.AddComponent<HorizontalLayoutGroup>();
        contentLayout.padding = new RectOffset(20, 20, 12, 12);
        contentLayout.spacing = 26f;
        contentLayout.childAlignment = TextAnchor.MiddleLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = true;

        ContentSizeFitter contentFitter = contentObj.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        slotContainer = contentRect;
        showcaseScrollRect.content = contentRect;

        // 2-3. 템플릿 슬롯 (프리팹 미연결 시 폴백용)
        GameObject templateObj = CreateChild("StarterKitSlotTemplate", contentObj.transform);
        slotTemplate = templateObj.AddComponent<StarterKitSlotUI>();
        templateObj.SetActive(false);
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void StretchFull(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static TextMeshProUGUI CreateText(
        GameObject target,
        string content,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment,
        TMP_FontAsset fontAsset)
    {
        TextMeshProUGUI tmp = target.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null)
        {
            tmp.font = fontAsset;
        }
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }
}
