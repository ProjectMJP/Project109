using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무장 진열대 패널 내 개별 시작 무장 카드 및 인접한 무기 역사 보드를 제어하는 슬롯 UI 컴포넌트입니다.
/// </summary>
public class StarterKitSlotUI : MonoBehaviour
{
    [Header("Main Weapon Card")]
    [SerializeField] private Image selectionOutlineImage;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private Button historyToggleButton;
    [SerializeField] private Button weaponSelectButton;
    [SerializeField] private Image weaponImage;
    [SerializeField] private TextMeshProUGUI weaponPlaceholderText;
    [SerializeField] private GameObject equippedBadgeRoot;

    [Header("Starting Relic Badge")]
    [SerializeField] private GameObject relicBadgeRoot;
    [SerializeField] private Image relicIconImage;
    [SerializeField] private TooltipTrigger relicTooltipTrigger;

    [Header("Lock Overlay & Bottom Controls")]
    [SerializeField] private GameObject lockOverlayRoot;
    [SerializeField] private TextMeshProUGUI lockCostText;
    [SerializeField] private TextMeshProUGUI statsSummaryText;
    [SerializeField] private Button viewStartCardsButton;

    [Header("Collapsible History Drawer")]
    [SerializeField] private GameObject historyBoardRoot;
    [SerializeField] private TextMeshProUGUI historyTitleText;
    [SerializeField] private Button historyCloseButton;
    [SerializeField] private RectTransform historyContentRoot;

    private StarterKitData _kitData;
    private Player _boundPlayer;
    private Action<StarterKitData> _onWeaponClicked;
    private Action<StarterKitData> _onViewCardsClicked;
    private Action<StarterKitSlotUI> _onLayoutChanged;
    private TMP_FontAsset _sharedFontAsset;
    private bool _isHistoryOpen;
    private readonly List<GameObject> _loreEntryObjects = new List<GameObject>();

    public string LoadoutId => _kitData != null ? _kitData.loadoutId : string.Empty;
    public bool IsHistoryOpen => _isHistoryOpen;

    /// <summary>
    /// 슬롯에 무장 데이터와 콜백을 바인딩하고 UI 상태를 초기화합니다.
    /// </summary>
    public void Bind(
        StarterKitData kitData,
        Player player,
        TMP_FontAsset sharedFontAsset,
        Action<StarterKitData> onWeaponClicked,
        Action<StarterKitData> onViewCardsClicked,
        Action<StarterKitSlotUI> onLayoutChanged)
    {
        _kitData = kitData;
        _boundPlayer = player;
        _onWeaponClicked = onWeaponClicked;
        _onViewCardsClicked = onViewCardsClicked;
        _onLayoutChanged = onLayoutChanged;

        if (sharedFontAsset != null)
        {
            _sharedFontAsset = sharedFontAsset;
        }

        EnsureHierarchyAndReferences();
        RegisterButtonListeners();
        SetHistoryOpen(false, notifyLayout: false);
        RefreshState(player);
    }

    /// <summary>
    /// 장착 여부, 해금 여부, 유물 툴팁, 스탯 요약 등 동적 상태를 갱신합니다.
    /// </summary>
    public void RefreshState(Player player)
    {
        _boundPlayer = player;
        if (_kitData == null) return;

        bool isUnlocked = player != null
            ? player.IsStarterKitUnlocked(_kitData)
            : (_kitData.isDefaultUnlocked || _kitData.unlockShardCost <= 0);
        bool isEquipped = player != null &&
            string.Equals(player.selectedStarterKitId, _kitData.loadoutId, StringComparison.Ordinal);

        if (selectionOutlineImage != null)
        {
            selectionOutlineImage.enabled = isEquipped;
        }

        if (equippedBadgeRoot != null)
        {
            equippedBadgeRoot.SetActive(isEquipped);
        }

        if (weaponNameText != null)
        {
            weaponNameText.text = _kitData.LocalizedDisplayName;
        }

        UpdateWeaponVisual(isUnlocked);
        UpdateRelicBadge();
        UpdateLockOverlay(player, isUnlocked);
        UpdateStatsSummary();

        bool hasLore = _kitData.loreEntries != null && _kitData.loreEntries.Count > 0;
        if (historyToggleButton != null)
        {
            historyToggleButton.gameObject.SetActive(hasLore);
        }

        if (!hasLore && _isHistoryOpen)
        {
            SetHistoryOpen(false, notifyLayout: true);
        }
        else if (_isHistoryOpen)
        {
            RebuildHistoryEntries();
        }
    }

    /// <summary>
    /// 무기 역사 보드의 열림/닫힘 상태를 전환합니다.
    /// </summary>
    public void SetHistoryOpen(bool open, bool notifyLayout = true)
    {
        bool hasLore = _kitData != null && _kitData.loreEntries != null && _kitData.loreEntries.Count > 0;
        _isHistoryOpen = open && hasLore;

        if (historyBoardRoot != null)
        {
            historyBoardRoot.SetActive(_isHistoryOpen);
        }

        if (_isHistoryOpen)
        {
            if (historyTitleText != null)
            {
                historyTitleText.text = "무기 역사";
            }

            RebuildHistoryEntries();

            if (historyContentRoot != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(historyContentRoot);
            }

            if (historyBoardRoot != null)
            {
                RectTransform boardRect = historyBoardRoot.GetComponent<RectTransform>();
                if (boardRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(boardRect);
                }
            }
        }

        RectTransform selfRect = transform as RectTransform;
        if (selfRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(selfRect);
        }

        if (notifyLayout)
        {
            _onLayoutChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// 슬롯 반환/제거 전 동적 리스너와 자식 오브젝트를 정리합니다.
    /// </summary>
    public void Clear()
    {
        if (weaponSelectButton != null) weaponSelectButton.onClick.RemoveAllListeners();
        if (historyToggleButton != null) historyToggleButton.onClick.RemoveAllListeners();
        if (historyCloseButton != null) historyCloseButton.onClick.RemoveAllListeners();
        if (viewStartCardsButton != null) viewStartCardsButton.onClick.RemoveAllListeners();

        ClearLoreEntries();
        _kitData = null;
        _boundPlayer = null;
        _onWeaponClicked = null;
        _onViewCardsClicked = null;
        _onLayoutChanged = null;
    }

    private void RegisterButtonListeners()
    {
        if (weaponSelectButton != null)
        {
            weaponSelectButton.onClick.RemoveAllListeners();
            weaponSelectButton.onClick.AddListener(HandleWeaponClicked);
        }

        if (historyToggleButton != null)
        {
            historyToggleButton.onClick.RemoveAllListeners();
            historyToggleButton.onClick.AddListener(HandleHistoryToggleClicked);
        }

        if (historyCloseButton != null)
        {
            historyCloseButton.onClick.RemoveAllListeners();
            historyCloseButton.onClick.AddListener(HandleHistoryCloseClicked);
        }

        if (viewStartCardsButton != null)
        {
            viewStartCardsButton.onClick.RemoveAllListeners();
            viewStartCardsButton.onClick.AddListener(HandleViewStartCardsClicked);
        }
    }

    private void HandleWeaponClicked()
    {
        if (_kitData != null)
        {
            _onWeaponClicked?.Invoke(_kitData);
        }
    }

    private void HandleHistoryToggleClicked()
    {
        SetHistoryOpen(!_isHistoryOpen, notifyLayout: true);
    }

    private void HandleHistoryCloseClicked()
    {
        SetHistoryOpen(false, notifyLayout: true);
    }

    private void HandleViewStartCardsClicked()
    {
        if (_kitData != null)
        {
            _onViewCardsClicked?.Invoke(_kitData);
        }
    }

    private void UpdateWeaponVisual(bool isUnlocked)
    {
        if (weaponImage == null) return;

        if (_kitData.weaponSprite != null)
        {
            weaponImage.sprite = _kitData.weaponSprite;
            weaponImage.preserveAspect = true;
            weaponImage.color = isUnlocked ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);

            if (weaponPlaceholderText != null)
            {
                weaponPlaceholderText.gameObject.SetActive(false);
            }
        }
        else
        {
            weaponImage.sprite = null;
            weaponImage.color = isUnlocked
                ? new Color(0.18f, 0.22f, 0.30f, 1f)
                : new Color(0.11f, 0.12f, 0.16f, 1f);

            if (weaponPlaceholderText != null)
            {
                weaponPlaceholderText.gameObject.SetActive(true);
                string classLabel = !string.IsNullOrEmpty(_kitData.classType) ? $"\n<size=70%><color=#9FB2C9>[{_kitData.classType}]</color></size>" : string.Empty;
                weaponPlaceholderText.text = $"{_kitData.LocalizedDisplayName}{classLabel}";
            }
        }
    }

    private void UpdateRelicBadge()
    {
        if (relicBadgeRoot == null) return;

        if (_kitData.startRelics == null || _kitData.startRelics.Count == 0 ||
            ModLoader.Instance == null || ModLoader.Instance.RelicDatabase == null)
        {
            relicBadgeRoot.SetActive(false);
            return;
        }

        string primaryRelicId = _kitData.startRelics[0];
        if (string.IsNullOrEmpty(primaryRelicId) ||
            !ModLoader.Instance.RelicDatabase.TryGetValue(primaryRelicId, out RelicData primaryRelic) ||
            primaryRelic == null)
        {
            relicBadgeRoot.SetActive(false);
            return;
        }

        relicBadgeRoot.SetActive(true);

        if (relicIconImage != null)
        {
            relicIconImage.sprite = primaryRelic.iconSprite;
            relicIconImage.color = primaryRelic.iconSprite != null
                ? Color.white
                : new Color(0.75f, 0.65f, 0.35f, 1f);
        }

        if (relicTooltipTrigger != null)
        {
            string body = primaryRelic.LocalizedDescription;
            if (_kitData.startRelics.Count > 1)
            {
                body = $"{body}\n\n<color=#8FB8FF>추가 시작 유물:</color>";
                for (int i = 1; i < _kitData.startRelics.Count; i++)
                {
                    string extraId = _kitData.startRelics[i];
                    if (ModLoader.Instance.RelicDatabase.TryGetValue(extraId, out RelicData extraRelic) && extraRelic != null)
                    {
                        body += $"\n• {extraRelic.LocalizedName}: {extraRelic.LocalizedDescription}";
                    }
                    else if (!string.IsNullOrEmpty(extraId))
                    {
                        body += $"\n• {extraId}";
                    }
                }
            }

            relicTooltipTrigger.SetStaticContent(primaryRelic.LocalizedName, body);
        }
    }

    private void UpdateLockOverlay(Player player, bool isUnlocked)
    {
        if (lockOverlayRoot != null)
        {
            lockOverlayRoot.SetActive(!isUnlocked);
        }

        if (isUnlocked || lockCostText == null) return;

        int cost = Mathf.Max(0, _kitData.unlockShardCost);
        int playerShards = (player != null && player.playerStat != null)
            ? player.playerStat.InGameCurrencyMemorySharp
            : 0;
        bool canAfford = playerShards >= cost;

        string colorHex = canAfford ? "#FFE066" : "#FF7A7A";
        string actionHint = canAfford ? "클릭하여 해금 및 장착" : "기억의 조각 부족";
        lockCostText.text = $"[잠금 상태]\n<color={colorHex}>기억의 조각 {cost}개 필요</color>\n<size=78%><color=#D0D7E2>{actionHint}</color></size>";
    }

    private void UpdateStatsSummary()
    {
        if (statsSummaryText == null) return;

        CharacterStat stat = _kitData.characterStat;
        int maxHp = stat != null ? Mathf.RoundToInt(stat.maxHealth) : 100;
        int maxStamina = stat != null ? Mathf.RoundToInt(stat.maxStamina) : 120;
        int moveRange = stat != null ? stat.moveRange : 3;
        int drawCount = _kitData.baseDrawCount > 0 ? _kitData.baseDrawCount : 5;

        statsSummaryText.text = $"체력 {maxHp}  |  스태미나 {maxStamina}  |  이동력 {moveRange}\n기본 드로우 {drawCount}장  |  시작 골드 {_kitData.startGold}G";
    }

    private void RebuildHistoryEntries()
    {
        ClearLoreEntries();
        if (historyContentRoot == null || _kitData == null || _kitData.loreEntries == null) return;

        TMP_FontAsset fontToUse = ResolveFontAsset();

        for (int i = 0; i < _kitData.loreEntries.Count; i++)
        {
            WeaponLoreEntry entry = _kitData.loreEntries[i];
            if (entry == null) continue;

            bool isLoreUnlocked = _boundPlayer != null
                ? _boundPlayer.IsLoreUnlocked(_kitData.loadoutId, i, entry)
                : entry.isUnlockedByDefault;

            GameObject itemObj = new GameObject($"LoreEntry_{i + 1}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            itemObj.transform.SetParent(historyContentRoot, false);

            Image bg = itemObj.GetComponent<Image>();
            bg.color = isLoreUnlocked
                ? new Color(0.17f, 0.20f, 0.28f, 0.95f)
                : new Color(0.10f, 0.11f, 0.15f, 0.90f);
            bg.raycastTarget = false;

            VerticalLayoutGroup layout = itemObj.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = itemObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            string titleDisplay = isLoreUnlocked
                ? entry.LocalizedTitle
                : $"{entry.LocalizedTitle} (잠금)";
            CreateLoreText(
                itemObj.transform,
                "Title",
                titleDisplay,
                20f,
                FontStyles.Bold,
                isLoreUnlocked ? new Color(0.98f, 0.85f, 0.45f, 1f) : new Color(0.62f, 0.66f, 0.74f, 1f),
                fontToUse);

            string bodyDisplay = isLoreUnlocked
                ? entry.LocalizedDescription
                : (!string.IsNullOrEmpty(entry.LocalizedUnlockHint)
                    ? $"해금 조건: {entry.LocalizedUnlockHint}"
                    : "해금 조건을 달성하면 열람할 수 있습니다.");
            CreateLoreText(
                itemObj.transform,
                "Body",
                bodyDisplay,
                17f,
                FontStyles.Normal,
                isLoreUnlocked ? new Color(0.90f, 0.93f, 0.98f, 1f) : new Color(0.58f, 0.62f, 0.68f, 1f),
                fontToUse);

            _loreEntryObjects.Add(itemObj);
        }
    }

    private void CreateLoreText(
        Transform parent,
        string objectName,
        string content,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TMP_FontAsset fontAsset)
    {
        GameObject textObj = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null)
        {
            tmp.font = fontAsset;
        }
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.raycastTarget = false;
    }

    private void ClearLoreEntries()
    {
        for (int i = 0; i < _loreEntryObjects.Count; i++)
        {
            if (_loreEntryObjects[i] != null)
            {
                Destroy(_loreEntryObjects[i]);
            }
        }
        _loreEntryObjects.Clear();
    }

    private TMP_FontAsset ResolveFontAsset()
    {
        if (_sharedFontAsset != null) return _sharedFontAsset;
        return TMPFontUtility.ResolveDefaultFont(weaponNameText, historyTitleText, statsSummaryText);
    }

    private void EnsureHierarchyAndReferences()
    {
        if (selectionOutlineImage == null) selectionOutlineImage = FindComponent<Image>("WeaponCardContainer/SelectionOutline");
        if (weaponNameText == null) weaponNameText = FindComponent<TextMeshProUGUI>("WeaponCardContainer/CardBody/CardHeader/WeaponNameText");
        if (historyToggleButton == null) historyToggleButton = FindComponent<Button>("WeaponCardContainer/CardBody/CardHeader/HistoryToggleButton");
        if (weaponSelectButton == null) weaponSelectButton = FindComponent<Button>("WeaponCardContainer/CardBody/WeaponImageButton");
        if (weaponImage == null) weaponImage = FindComponent<Image>("WeaponCardContainer/CardBody/WeaponImageButton");
        if (weaponPlaceholderText == null) weaponPlaceholderText = FindComponent<TextMeshProUGUI>("WeaponCardContainer/CardBody/WeaponImageButton/WeaponPlaceholderText");
        if (equippedBadgeRoot == null) equippedBadgeRoot = FindGameObject("WeaponCardContainer/CardBody/WeaponImageButton/EquippedBadge");
        if (relicBadgeRoot == null) relicBadgeRoot = FindGameObject("WeaponCardContainer/CardBody/WeaponImageButton/RelicBadge");
        if (relicIconImage == null) relicIconImage = FindComponent<Image>("WeaponCardContainer/CardBody/WeaponImageButton/RelicBadge/RelicIcon");
        if (relicTooltipTrigger == null) relicTooltipTrigger = FindComponent<TooltipTrigger>("WeaponCardContainer/CardBody/WeaponImageButton/RelicBadge");
        if (lockOverlayRoot == null) lockOverlayRoot = FindGameObject("WeaponCardContainer/CardBody/WeaponImageButton/LockOverlay");
        if (lockCostText == null) lockCostText = FindComponent<TextMeshProUGUI>("WeaponCardContainer/CardBody/WeaponImageButton/LockOverlay/LockCostText");
        if (statsSummaryText == null) statsSummaryText = FindComponent<TextMeshProUGUI>("WeaponCardContainer/CardBody/StatsSummaryText");
        if (viewStartCardsButton == null) viewStartCardsButton = FindComponent<Button>("WeaponCardContainer/CardBody/ViewStartCardsButton");
        if (historyBoardRoot == null) historyBoardRoot = FindGameObject("HistoryBoard");
        if (historyTitleText == null) historyTitleText = FindComponent<TextMeshProUGUI>("HistoryBoard/HistoryHeader/HistoryTitleText");
        if (historyCloseButton == null) historyCloseButton = FindComponent<Button>("HistoryBoard/HistoryHeader/HistoryCloseButton");
        if (historyContentRoot == null) historyContentRoot = FindComponent<RectTransform>("HistoryBoard/HistoryScrollView/HistoryViewport/HistoryContent");

        if (weaponSelectButton != null && statsSummaryText != null && historyBoardRoot != null)
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

    private GameObject FindGameObject(string childPath)
    {
        Transform found = transform.Find(childPath);
        return found != null ? found.gameObject : null;
    }

    private void BuildFallbackHierarchy()
    {
        TMP_FontAsset font = ResolveFontAsset();

        HorizontalLayoutGroup rootLayout = GetComponent<HorizontalLayoutGroup>();
        if (rootLayout == null) rootLayout = gameObject.AddComponent<HorizontalLayoutGroup>();
        rootLayout.spacing = 12f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = false;
        rootLayout.childForceExpandHeight = true;

        ContentSizeFitter rootFitter = GetComponent<ContentSizeFitter>();
        if (rootFitter == null) rootFitter = gameObject.AddComponent<ContentSizeFitter>();
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 1. 메인 무기 카드 컨테이너
        GameObject cardContainer = CreateChild("WeaponCardContainer", transform);
        LayoutElement cardLayoutElem = cardContainer.AddComponent<LayoutElement>();
        cardLayoutElem.minWidth = 380f;
        cardLayoutElem.preferredWidth = 380f;

        GameObject outlineObj = CreateChild("SelectionOutline", cardContainer.transform);
        StretchFull(outlineObj.GetComponent<RectTransform>());
        selectionOutlineImage = outlineObj.AddComponent<Image>();
        selectionOutlineImage.color = new Color(0.20f, 0.58f, 1f, 1f);
        selectionOutlineImage.raycastTarget = false;
        selectionOutlineImage.enabled = false;

        GameObject cardBody = CreateChild("CardBody", cardContainer.transform);
        RectTransform cardBodyRect = cardBody.GetComponent<RectTransform>();
        StretchFull(cardBodyRect);
        cardBodyRect.offsetMin = new Vector2(5f, 5f);
        cardBodyRect.offsetMax = new Vector2(-5f, -5f);
        Image bodyImage = cardBody.AddComponent<Image>();
        bodyImage.color = new Color(0.15f, 0.17f, 0.23f, 1f);

        VerticalLayoutGroup cardBodyLayout = cardBody.AddComponent<VerticalLayoutGroup>();
        cardBodyLayout.padding = new RectOffset(16, 16, 14, 14);
        cardBodyLayout.spacing = 12f;
        cardBodyLayout.childControlWidth = true;
        cardBodyLayout.childControlHeight = true;
        cardBodyLayout.childForceExpandWidth = true;
        cardBodyLayout.childForceExpandHeight = false;

        // 1-1. 카드 헤더 (무기 이름 + 우측 상단 역사 버튼)
        GameObject cardHeader = CreateChild("CardHeader", cardBody.transform);
        LayoutElement headerLayoutElem = cardHeader.AddComponent<LayoutElement>();
        headerLayoutElem.minHeight = 46f;
        headerLayoutElem.preferredHeight = 46f;
        headerLayoutElem.flexibleHeight = 0f;
        HorizontalLayoutGroup headerLayout = cardHeader.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 8f;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;

        GameObject weaponNameObj = CreateChild("WeaponNameText", cardHeader.transform);
        LayoutElement nameLayoutElem = weaponNameObj.AddComponent<LayoutElement>();
        nameLayoutElem.flexibleWidth = 1f;
        weaponNameText = CreateText(weaponNameObj, "무기 이름", 24f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft, font);

        GameObject historyBtnObj = CreateButtonObject("HistoryToggleButton", cardHeader.transform, "역사", 18f, new Color(0.24f, 0.29f, 0.39f, 1f), font, out historyToggleButton);
        LayoutElement historyBtnElem = historyBtnObj.AddComponent<LayoutElement>();
        historyBtnElem.minWidth = 74f;
        historyBtnElem.preferredWidth = 74f;

        // 1-2. 중앙 무기 이미지 버튼 (+ 좌하단 유물 아이콘 + 잠금/장착 오버레이)
        GameObject weaponBtnObj = CreateChild("WeaponImageButton", cardBody.transform);
        LayoutElement weaponBtnElem = weaponBtnObj.AddComponent<LayoutElement>();
        weaponBtnElem.flexibleHeight = 1f;
        weaponBtnElem.minHeight = 280f;
        weaponImage = weaponBtnObj.AddComponent<Image>();
        weaponImage.color = new Color(0.18f, 0.22f, 0.30f, 1f);
        weaponSelectButton = weaponBtnObj.AddComponent<Button>();

        GameObject placeholderObj = CreateChild("WeaponPlaceholderText", weaponBtnObj.transform);
        StretchFull(placeholderObj.GetComponent<RectTransform>());
        weaponPlaceholderText = CreateText(placeholderObj, string.Empty, 28f, FontStyles.Bold, new Color(0.88f, 0.92f, 0.98f, 1f), TextAlignmentOptions.Center, font);
        weaponPlaceholderText.raycastTarget = false;

        GameObject equippedObj = CreateChild("EquippedBadge", weaponBtnObj.transform);
        RectTransform equippedRect = equippedObj.GetComponent<RectTransform>();
        equippedRect.anchorMin = new Vector2(0f, 1f);
        equippedRect.anchorMax = new Vector2(0f, 1f);
        equippedRect.pivot = new Vector2(0f, 1f);
        equippedRect.anchoredPosition = new Vector2(12f, -12f);
        equippedRect.sizeDelta = new Vector2(104f, 34f);
        Image equippedBg = equippedObj.AddComponent<Image>();
        equippedBg.color = new Color(0.18f, 0.55f, 0.98f, 0.95f);
        equippedBg.raycastTarget = false;
        GameObject equippedLabelObj = CreateChild("Label", equippedObj.transform);
        StretchFull(equippedLabelObj.GetComponent<RectTransform>());
        TextMeshProUGUI equippedLabel = CreateText(equippedLabelObj, "장착 중", 17f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, font);
        equippedLabel.raycastTarget = false;
        equippedBadgeRoot = equippedObj;

        GameObject lockObj = CreateChild("LockOverlay", weaponBtnObj.transform);
        StretchFull(lockObj.GetComponent<RectTransform>());
        Image lockBg = lockObj.AddComponent<Image>();
        lockBg.color = new Color(0.03f, 0.03f, 0.05f, 0.76f);
        lockBg.raycastTarget = false;
        GameObject lockTextObj = CreateChild("LockCostText", lockObj.transform);
        StretchFull(lockTextObj.GetComponent<RectTransform>());
        lockCostText = CreateText(lockTextObj, "[잠금]", 21f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, font);
        lockCostText.raycastTarget = false;
        lockOverlayRoot = lockObj;

        GameObject relicObj = CreateChild("RelicBadge", weaponBtnObj.transform);
        RectTransform relicRect = relicObj.GetComponent<RectTransform>();
        relicRect.anchorMin = new Vector2(0f, 0f);
        relicRect.anchorMax = new Vector2(0f, 0f);
        relicRect.pivot = new Vector2(0f, 0f);
        relicRect.anchoredPosition = new Vector2(12f, 12f);
        relicRect.sizeDelta = new Vector2(68f, 68f);
        Image relicBg = relicObj.AddComponent<Image>();
        relicBg.color = new Color(0.08f, 0.09f, 0.13f, 0.92f);
        relicTooltipTrigger = relicObj.AddComponent<TooltipTrigger>();
        GameObject relicIconObj = CreateChild("RelicIcon", relicObj.transform);
        RectTransform relicIconRect = relicIconObj.GetComponent<RectTransform>();
        StretchFull(relicIconRect);
        relicIconRect.offsetMin = new Vector2(6f, 6f);
        relicIconRect.offsetMax = new Vector2(-6f, -6f);
        relicIconImage = relicIconObj.AddComponent<Image>();
        relicIconImage.preserveAspect = true;
        relicIconImage.raycastTarget = false;
        relicBadgeRoot = relicObj;

        // 1-3. 하단 스탯 요약 & 시작 카드 보기 버튼
        GameObject statsObj = CreateChild("StatsSummaryText", cardBody.transform);
        LayoutElement statsElem = statsObj.AddComponent<LayoutElement>();
        statsElem.minHeight = 84f;
        statsElem.preferredHeight = 84f;
        statsSummaryText = CreateText(statsObj, string.Empty, 19f, FontStyles.Normal, new Color(0.84f, 0.89f, 0.95f, 1f), TextAlignmentOptions.Center, font);
        statsSummaryText.raycastTarget = false;

        GameObject viewCardsBtnObj = CreateButtonObject("ViewStartCardsButton", cardBody.transform, "시작 카드 보기", 20f, new Color(0.24f, 0.34f, 0.48f, 1f), font, out viewStartCardsButton);
        LayoutElement viewCardsElem = viewCardsBtnObj.AddComponent<LayoutElement>();
        viewCardsElem.minHeight = 50f;
        viewCardsElem.preferredHeight = 50f;

        // 2. 인접 접이식 무기 역사 보드
        historyBoardRoot = CreateChild("HistoryBoard", transform);
        LayoutElement boardElem = historyBoardRoot.AddComponent<LayoutElement>();
        boardElem.minWidth = 360f;
        boardElem.preferredWidth = 360f;
        Image boardBg = historyBoardRoot.AddComponent<Image>();
        boardBg.color = new Color(0.12f, 0.14f, 0.20f, 0.98f);

        VerticalLayoutGroup boardLayout = historyBoardRoot.AddComponent<VerticalLayoutGroup>();
        boardLayout.padding = new RectOffset(16, 16, 14, 14);
        boardLayout.spacing = 10f;
        boardLayout.childControlWidth = true;
        boardLayout.childControlHeight = true;
        boardLayout.childForceExpandWidth = true;
        boardLayout.childForceExpandHeight = false;

        GameObject historyHeader = CreateChild("HistoryHeader", historyBoardRoot.transform);
        LayoutElement hhElem = historyHeader.AddComponent<LayoutElement>();
        hhElem.minHeight = 46f;
        hhElem.preferredHeight = 46f;
        hhElem.flexibleHeight = 0f;
        HorizontalLayoutGroup hhLayout = historyHeader.AddComponent<HorizontalLayoutGroup>();
        hhLayout.spacing = 8f;
        hhLayout.childControlWidth = true;
        hhLayout.childControlHeight = true;
        hhLayout.childForceExpandWidth = false;
        hhLayout.childForceExpandHeight = true;

        GameObject historyTitleObj = CreateChild("HistoryTitleText", historyHeader.transform);
        LayoutElement htElem = historyTitleObj.AddComponent<LayoutElement>();
        htElem.flexibleWidth = 1f;
        historyTitleText = CreateText(historyTitleObj, "무기 역사", 24f, FontStyles.Bold, new Color(0.98f, 0.88f, 0.56f, 1f), TextAlignmentOptions.MidlineLeft, font);

        GameObject historyCloseObj = CreateButtonObject("HistoryCloseButton", historyHeader.transform, "X", 20f, new Color(0.48f, 0.20f, 0.22f, 1f), font, out historyCloseButton);
        LayoutElement hcElem = historyCloseObj.AddComponent<LayoutElement>();
        hcElem.minWidth = 40f;
        hcElem.preferredWidth = 40f;

        GameObject historyScrollObj = CreateChild("HistoryScrollView", historyBoardRoot.transform);
        LayoutElement hsElem = historyScrollObj.AddComponent<LayoutElement>();
        hsElem.flexibleHeight = 1f;
        ScrollRect historyScroll = historyScrollObj.AddComponent<ScrollRect>();
        historyScroll.horizontal = false;
        historyScroll.vertical = true;
        historyScroll.scrollSensitivity = 28f;

        GameObject historyViewportObj = CreateChild("HistoryViewport", historyScrollObj.transform);
        StretchFull(historyViewportObj.GetComponent<RectTransform>());
        Image vpImg = historyViewportObj.AddComponent<Image>();
        vpImg.color = Color.white;
        Mask vpMask = historyViewportObj.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        historyScroll.viewport = historyViewportObj.GetComponent<RectTransform>();

        GameObject historyContentObj = CreateChild("HistoryContent", historyViewportObj.transform);
        RectTransform hcRect = historyContentObj.GetComponent<RectTransform>();
        hcRect.anchorMin = new Vector2(0f, 1f);
        hcRect.anchorMax = new Vector2(1f, 1f);
        hcRect.pivot = new Vector2(0.5f, 1f);
        hcRect.anchoredPosition = Vector2.zero;
        hcRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = historyContentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 12f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = historyContentObj.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        historyContentRoot = hcRect;
        historyScroll.content = hcRect;
        historyBoardRoot.SetActive(false);
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

    private static GameObject CreateButtonObject(
        string name,
        Transform parent,
        string label,
        float fontSize,
        Color bgColor,
        TMP_FontAsset fontAsset,
        out Button button)
    {
        GameObject btnObj = CreateChild(name, parent);
        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;
        button = btnObj.AddComponent<Button>();

        GameObject textObj = CreateChild("Text (TMP)", btnObj.transform);
        StretchFull(textObj.GetComponent<RectTransform>());
        TextMeshProUGUI tmp = CreateText(textObj, label, fontSize, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, fontAsset);
        tmp.raycastTarget = false;

        return btnObj;
    }
}
