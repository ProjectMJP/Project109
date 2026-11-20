using System;
using System.Collections.Generic;
using GameItem.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XLua;

/// <summary>
/// ESC 또는 TopHUD 일시정지 버튼으로 호출되는 공용 시스템 메뉴 패널입니다.
/// 게임 복귀, 카드/유물 도감 열람, 게임 설정(볼륨/화면 모드), 안전 저장 후 타이틀 귀환을 제공합니다.
/// </summary>
public class PauseMenuPanel : UIPanelBase
{
    private const string PREF_MASTER_VOLUME = "Settings_MasterVolume";
    private const string PREF_FULLSCREEN = "Settings_FullScreen";

    private enum ViewMode
    {
        Main,
        Settings,
        RelicCompendium
    }

    public enum RelicRarityFilter
    {
        All = 0,
        Common = 1,
        Rare = 2,
        Unique = 3,
        Boss = 4
    }

    public enum RelicObtainSourceFilter
    {
        All = 0,
        Standard = 1,
        BossOnly = 2,
        EventOnly = 3
    }

    public enum RelicSortCriteria
    {
        Rarity = 0,
        Name = 1
    }

    [Header("Sub-View Containers")]
    [SerializeField] private GameObject mainMenuGroup;
    [SerializeField] private GameObject settingsGroup;
    [SerializeField] private GameObject relicCompendiumGroup;

    [Header("Header & Feedback")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statusMessageText;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button compendiumButton;
    [SerializeField] private Button relicCompendiumButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button returnToTitleButton;

    [Header("Settings Controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeValueText;
    [SerializeField] private Toggle fullScreenToggle;
    [SerializeField] private TextMeshProUGUI fullScreenStatusText;
    [SerializeField] private Toggle tooltipToggle;
    [SerializeField] private TextMeshProUGUI tooltipStatusText;
    [SerializeField] private Button settingsBackButton;

    [Header("Relic Compendium Controls")]
    [SerializeField] private Transform relicListContent;
    [SerializeField] private TextMeshProUGUI relicCountText;
    [SerializeField] private Button relicBackButton;

    [Header("Relic Filter & Sort Controls")]
    [SerializeField] private GameObject relicFilterSortBarRoot;
    [SerializeField] private Button relicRarityFilterButton;
    [SerializeField] private TextMeshProUGUI relicRarityFilterText;
    [SerializeField] private Button relicObtainSourceFilterButton;
    [SerializeField] private TextMeshProUGUI relicObtainSourceFilterText;
    [SerializeField] private Button relicSortCriteriaButton;
    [SerializeField] private TextMeshProUGUI relicSortCriteriaText;
    [SerializeField] private Button relicSortDirectionButton;
    [SerializeField] private TextMeshProUGUI relicSortDirectionText;

    private readonly List<Card> _cachedCompendiumCards = new List<Card>();
    private readonly List<RelicData> _allCompendiumRelics = new List<RelicData>();
    private readonly List<RelicData> _filteredCompendiumRelics = new List<RelicData>();
    private readonly List<GameObject> _spawnedRelicRows = new List<GameObject>();

    private CardDeckViewPanel _openedDeckPanel;
    private bool _listenersBound;
    private bool _relicFilterListenersBound;
    private bool _isTransitioning;
    private bool _isSyncingSettingsUI;
    private bool _relicListBuilt;

    private RelicRarityFilter _relicRarityFilter = RelicRarityFilter.All;
    private RelicObtainSourceFilter _relicObtainSourceFilter = RelicObtainSourceFilter.All;
    private RelicSortCriteria _relicSortCriteria = RelicSortCriteria.Rarity;
    private bool _isRelicSortAscending = true;

    public RelicRarityFilter CurrentRelicRarityFilter => _relicRarityFilter;
    public RelicObtainSourceFilter CurrentRelicObtainSourceFilter => _relicObtainSourceFilter;
    public RelicSortCriteria CurrentRelicSortCriteria => _relicSortCriteria;
    public bool IsRelicSortAscending => _isRelicSortAscending;
    public int TotalCompendiumRelicCount => _allCompendiumRelics.Count;
    public int DisplayedRelicCount => _filteredCompendiumRelics.Count;
    public IReadOnlyList<RelicData> CurrentDisplayedRelics => _filteredCompendiumRelics;
    public IReadOnlyList<Card> CachedCompendiumCards => _cachedCompendiumCards;

    public override bool CanCloseByCancel => true;
    public override bool DestroyOnClose => false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedSettingsOnStartup()
    {
        if (PlayerPrefs.HasKey(PREF_MASTER_VOLUME))
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, 1f));
        }

        if (PlayerPrefs.HasKey(PREF_FULLSCREEN))
        {
            bool fullScreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
            if (Screen.fullScreen != fullScreen)
            {
                Screen.fullScreen = fullScreen;
            }
        }
    }

    private void Awake()
    {
        uiLayerType = UILayerType.Popup;
        blockWorldInput = true;

        ResolveComponentReferences();
        EnsureTooltipSettingRow();
        EnsureRelicFilterSortBar();
        BindListeners();
        BindRelicFilterSortListeners();
        LuaManager.OnBeforeLuaEnvDispose -= DisposeCompendiumCards;
        LuaManager.OnBeforeLuaEnvDispose += DisposeCompendiumCards;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        TooltipManager.OnTooltipEnabledChanged -= HandleTooltipEnabledChanged;
        TooltipManager.OnTooltipEnabledChanged += HandleTooltipEnabledChanged;
        SyncTooltipSettingUI(TooltipManager.IsTooltipEnabled);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        TooltipManager.OnTooltipEnabledChanged -= HandleTooltipEnabledChanged;
    }

    private void OnDestroy()
    {
        LuaManager.OnBeforeLuaEnvDispose -= DisposeCompendiumCards;
        TooltipManager.OnTooltipEnabledChanged -= HandleTooltipEnabledChanged;
        UnbindListeners();
        UnbindRelicFilterSortListeners();
        DisposeCompendiumCards();
        ClearSpawnedRelicRows();
        _allCompendiumRelics.Clear();
        _filteredCompendiumRelics.Clear();
        _relicListBuilt = false;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        uiLayerType = UILayerType.Popup;
        blockWorldInput = true;
        _isTransitioning = false;

        ResolveComponentReferences();
        EnsureTooltipSettingRow();
        EnsureRelicFilterSortBar();
        BindListeners();
        BindRelicFilterSortListeners();

        if (returnToTitleButton != null)
        {
            returnToTitleButton.interactable = true;
        }

        ClearStatusMessage();
        RefreshSettingsUI();
        SwitchViewMode(ViewMode.Main);
    }

    public override void OnClose()
    {
        base.OnClose();

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }

        RestoreOpenedDeckPanelIfClosed();
        ClearStatusMessage();
        SwitchViewMode(ViewMode.Main);

        if (Time.timeScale <= 0f)
        {
            Time.timeScale = 1f;
        }
    }

    private void BindListeners()
    {
        if (_listenersBound)
        {
            return;
        }

        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
        if (compendiumButton != null) compendiumButton.onClick.AddListener(OnCompendiumClicked);
        if (relicCompendiumButton != null) relicCompendiumButton.onClick.AddListener(OnRelicCompendiumClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (returnToTitleButton != null) returnToTitleButton.onClick.AddListener(OnReturnToTitleClicked);
        if (settingsBackButton != null) settingsBackButton.onClick.AddListener(OnBackToMainClicked);
        if (relicBackButton != null) relicBackButton.onClick.AddListener(OnBackToMainClicked);

        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (fullScreenToggle != null) fullScreenToggle.onValueChanged.AddListener(OnFullScreenToggleChanged);
        if (tooltipToggle != null)
        {
            tooltipToggle.onValueChanged.RemoveListener(OnTooltipToggleChanged);
            tooltipToggle.onValueChanged.AddListener(OnTooltipToggleChanged);
        }

        _listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!_listenersBound)
        {
            return;
        }

        if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeClicked);
        if (compendiumButton != null) compendiumButton.onClick.RemoveListener(OnCompendiumClicked);
        if (relicCompendiumButton != null) relicCompendiumButton.onClick.RemoveListener(OnRelicCompendiumClicked);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
        if (returnToTitleButton != null) returnToTitleButton.onClick.RemoveListener(OnReturnToTitleClicked);
        if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(OnBackToMainClicked);
        if (relicBackButton != null) relicBackButton.onClick.RemoveListener(OnBackToMainClicked);

        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (fullScreenToggle != null) fullScreenToggle.onValueChanged.RemoveListener(OnFullScreenToggleChanged);
        if (tooltipToggle != null) tooltipToggle.onValueChanged.RemoveListener(OnTooltipToggleChanged);

        _listenersBound = false;
    }

    private void SwitchViewMode(ViewMode mode)
    {
        if (mainMenuGroup != null) mainMenuGroup.SetActive(mode == ViewMode.Main);
        if (settingsGroup != null) settingsGroup.SetActive(mode == ViewMode.Settings);
        if (relicCompendiumGroup != null) relicCompendiumGroup.SetActive(mode == ViewMode.RelicCompendium);

        if (titleText != null)
        {
            switch (mode)
            {
                case ViewMode.Settings:
                    titleText.text = "게임 설정";
                    break;
                case ViewMode.RelicCompendium:
                    titleText.text = "유물 도감";
                    break;
                default:
                    titleText.text = "일시정지 메뉴";
                    break;
            }
        }
    }

    private void OnResumeClicked()
    {
        Close();
    }

    private void OnCompendiumClicked()
    {
        ClearStatusMessage();

        if (UIManager.instance == null)
        {
            ShowStatusMessage("UIManager를 찾을 수 없어 도감을 열 수 없습니다.");
            return;
        }

        bool hasDeckPanel = UIManager.instance.GetUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK) != null ||
                            (AssetCacheManager.instance != null &&
                             AssetCacheManager.instance.TryGetUI(UIConstants.PANEL_CARD_DECK, out _));

        if (!hasDeckPanel)
        {
            ShowStatusMessage("카드 도감 UI 프리팹이 등록되지 않았습니다.");
            return;
        }

        EnsureCompendiumCardsBuilt();
        if (_cachedCompendiumCards.Count == 0)
        {
            ShowStatusMessage("표시할 카드 도감 데이터가 없습니다.");
            return;
        }

        CardDeckViewPanel deckPanel = UIManager.instance.OpenUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK, UILayerType.Popup, false);
        if (deckPanel == null)
        {
            ShowStatusMessage("카드 도감 패널을 열 수 없습니다.");
            return;
        }

        _openedDeckPanel = deckPanel;
        deckPanel.uiLayerType = UILayerType.Popup;

        if (!deckPanel.gameObject.activeSelf)
        {
            UIManager.instance.PushActiveUIPanel(deckPanel);
        }

        deckPanel.OpenCompendium("카드 도감", _cachedCompendiumCards, onCancel: RestoreOpenedDeckPanelIfClosed);
    }

    private void OnRelicCompendiumClicked()
    {
        ClearStatusMessage();
        ResetRelicFilterAndSortState();
        EnsureRelicFilterSortBar();
        BindRelicFilterSortListeners();
        EnsureRelicCompendiumBuilt();
        UpdateRelicFilterSortControlBarUI();
        SwitchViewMode(ViewMode.RelicCompendium);
    }

    private void OnSettingsClicked()
    {
        ClearStatusMessage();
        RefreshSettingsUI();
        SwitchViewMode(ViewMode.Settings);
    }

    private void OnBackToMainClicked()
    {
        ClearStatusMessage();
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
        SwitchViewMode(ViewMode.Main);
    }

    private void OnReturnToTitleClicked()
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        if (returnToTitleButton != null)
        {
            returnToTitleButton.interactable = false;
        }

        // 1. 진행 상황 안전 저장 (사망 직후 상태는 덮어쓰지 않음)
        if (GameSceneManager.instance != null)
        {
            bool isPlayerDead = GameSceneManager.instance.player != null &&
                                GameSceneManager.instance.player.character != null &&
                                GameSceneManager.instance.player.character.isDead;

            if (!isPlayerDead)
            {
                int saveSlot = GameSceneManager.instance.currentSaveSlot;
                RunManager activeRun = GameSceneManager.instance.isInDungeon ? RunManager.instance : null;
                SaveSystem.SaveGame(activeRun, saveSlot);
            }
        }

        // 2. 시간 배율 복원 및 팝업 닫기
        Time.timeScale = 1f;
        RestoreOpenedDeckPanelIfClosed();
        Close();

        // 3. 타이틀 씬(MainMenu)으로 전환 (GameOverPanel.OnReturnToTitleClicked 규약과 동일)
        if (SceneLoadManager.instance != null)
        {
            SceneLoadManager.instance.LoadScene("MainMenu");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    private void RefreshSettingsUI()
    {
        _isSyncingSettingsUI = true;

        float savedVolume = PlayerPrefs.HasKey(PREF_MASTER_VOLUME)
            ? Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, 1f))
            : Mathf.Clamp01(AudioListener.volume);

        AudioListener.volume = savedVolume;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.SetValueWithoutNotify(savedVolume);
        }

        UpdateMasterVolumeText(savedVolume);

        bool isFullScreen = PlayerPrefs.HasKey(PREF_FULLSCREEN)
            ? PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1
            : Screen.fullScreen;

        if (fullScreenToggle != null)
        {
            fullScreenToggle.SetIsOnWithoutNotify(isFullScreen);
        }

        UpdateFullScreenStatusText(isFullScreen);

        bool isTooltipEnabled = TooltipManager.IsTooltipEnabled;
        if (tooltipToggle != null)
        {
            tooltipToggle.SetIsOnWithoutNotify(isTooltipEnabled);
        }

        UpdateTooltipStatusText(isTooltipEnabled);
        _isSyncingSettingsUI = false;
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (_isSyncingSettingsUI)
        {
            return;
        }

        float clamped = Mathf.Clamp01(value);
        AudioListener.volume = clamped;
        PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, clamped);
        PlayerPrefs.Save();

        UpdateMasterVolumeText(clamped);
    }

    private void OnFullScreenToggleChanged(bool isFullScreen)
    {
        if (_isSyncingSettingsUI)
        {
            return;
        }

        Screen.fullScreen = isFullScreen;
        PlayerPrefs.SetInt(PREF_FULLSCREEN, isFullScreen ? 1 : 0);
        PlayerPrefs.Save();

        UpdateFullScreenStatusText(isFullScreen);
    }

    private void OnTooltipToggleChanged(bool isEnabled)
    {
        if (_isSyncingSettingsUI)
        {
            return;
        }

        TooltipManager.SetTooltipEnabled(isEnabled);
        UpdateTooltipStatusText(isEnabled);
    }

    private void HandleTooltipEnabledChanged(bool isEnabled)
    {
        SyncTooltipSettingUI(isEnabled);
    }

    private void SyncTooltipSettingUI(bool isEnabled)
    {
        bool prevSync = _isSyncingSettingsUI;
        _isSyncingSettingsUI = true;
        try
        {
            if (tooltipToggle != null)
            {
                tooltipToggle.SetIsOnWithoutNotify(isEnabled);
            }
            UpdateTooltipStatusText(isEnabled);
        }
        finally
        {
            _isSyncingSettingsUI = prevSync;
        }
    }

    private void UpdateMasterVolumeText(float volume)
    {
        if (masterVolumeValueText != null)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f);
            masterVolumeValueText.text = $"{percent}%";
        }
    }

    private void UpdateFullScreenStatusText(bool isFullScreen)
    {
        if (fullScreenStatusText != null)
        {
            fullScreenStatusText.text = isFullScreen ? "전체화면" : "창모드";
        }
    }

    private void UpdateTooltipStatusText(bool isEnabled)
    {
        if (tooltipStatusText != null)
        {
            tooltipStatusText.text = isEnabled ? "ON" : "OFF";
        }
    }

    private void RestoreOpenedDeckPanelIfClosed()
    {
        if (_openedDeckPanel != null)
        {
            _openedDeckPanel.uiLayerType = UILayerType.Normal;
            _openedDeckPanel = null;
        }
    }

    public void EnsureCompendiumCardsBuilt()
    {
        if (_cachedCompendiumCards.Count > 0)
        {
            return;
        }

        if (ModLoader.Instance == null || ModLoader.Instance.CardDatabase == null)
        {
            Debug.LogWarning("[PauseMenuPanel] ModLoader.CardDatabase가 로드되지 않아 카드 도감을 구성할 수 없습니다.");
            return;
        }

        List<CardData> sortedCards = new List<CardData>(ModLoader.Instance.CardDatabase.Count);
        foreach (CardData cardData in ModLoader.Instance.CardDatabase.Values)
        {
            if (cardData != null && cardData.obtainSource != ItemObtainSource.TestOnly)
            {
                sortedCards.Add(cardData);
            }
        }

        sortedCards.Sort(CompareCardDataForCompendium);

        for (int i = 0; i < sortedCards.Count; i++)
        {
            CardData cardData = sortedCards[i];
            LuaTable luaLogic = null;
            if (cardData.luaPrototype != null && LuaManager.Instance != null)
            {
                luaLogic = LuaManager.Instance.NewInstance(cardData.luaPrototype);
            }

            Card tempCard = new Card(cardData, null, luaLogic, -(i + 1));
            _cachedCompendiumCards.Add(tempCard);
        }
    }

    private void DisposeCompendiumCards()
    {
        for (int i = 0; i < _cachedCompendiumCards.Count; i++)
        {
            if (_cachedCompendiumCards[i] != null)
            {
                _cachedCompendiumCards[i].Dispose();
            }
        }
        _cachedCompendiumCards.Clear();
    }

    public void EnsureRelicCompendiumBuilt()
    {
        if (!_relicListBuilt)
        {
            _allCompendiumRelics.Clear();

            if (ModLoader.Instance != null && ModLoader.Instance.RelicDatabase != null)
            {
                foreach (RelicData relicData in ModLoader.Instance.RelicDatabase.Values)
                {
                    if (relicData != null && relicData.obtainSource != ItemObtainSource.TestOnly)
                    {
                        _allCompendiumRelics.Add(relicData);
                    }
                }
            }

            _relicListBuilt = true;
        }

        RefreshRelicCompendiumList();
    }

    public void CycleRelicRarityFilter()
    {
        _relicRarityFilter = (RelicRarityFilter)(((int)_relicRarityFilter + 1) % 5);
        UpdateRelicFilterSortControlBarUI();
        RefreshRelicCompendiumList();
    }

    public void CycleRelicObtainSourceFilter()
    {
        _relicObtainSourceFilter = (RelicObtainSourceFilter)(((int)_relicObtainSourceFilter + 1) % 4);
        UpdateRelicFilterSortControlBarUI();
        RefreshRelicCompendiumList();
    }

    public void CycleRelicSortCriteria()
    {
        _relicSortCriteria = (RelicSortCriteria)(((int)_relicSortCriteria + 1) % 2);
        UpdateRelicFilterSortControlBarUI();
        RefreshRelicCompendiumList();
    }

    public void ToggleRelicSortDirection()
    {
        _isRelicSortAscending = !_isRelicSortAscending;
        UpdateRelicFilterSortControlBarUI();
        RefreshRelicCompendiumList();
    }

    /// <summary>
    /// 유물 목록에 도감 필터(TestOnly 기본 제외 포함)와 정렬을 순수하게 적용한 새 리스트를 반환합니다.
    /// </summary>
    public static List<RelicData> FilterAndSortRelics(
        IEnumerable<RelicData> sourceRelics,
        RelicRarityFilter rarityFilter,
        RelicObtainSourceFilter obtainSourceFilter,
        RelicSortCriteria sortCriteria,
        bool isAscending)
    {
        List<RelicData> result = new List<RelicData>();
        FilterAndSortRelicsInto(sourceRelics, result, rarityFilter, obtainSourceFilter, sortCriteria, isAscending);
        return result;
    }

    /// <summary>
    /// 유물 목록에 도감 필터와 정렬을 적용하여 지정된 출력 리스트에 채웁니다.
    /// </summary>
    public static void FilterAndSortRelicsInto(
        IEnumerable<RelicData> sourceRelics,
        List<RelicData> outputRelics,
        RelicRarityFilter rarityFilter,
        RelicObtainSourceFilter obtainSourceFilter,
        RelicSortCriteria sortCriteria,
        bool isAscending)
    {
        if (outputRelics == null) return;
        outputRelics.Clear();

        if (sourceRelics == null) return;

        foreach (RelicData relic in sourceRelics)
        {
            if (MatchesRelicCompendiumFilters(relic, rarityFilter, obtainSourceFilter))
            {
                outputRelics.Add(relic);
            }
        }

        outputRelics.Sort((left, right) => CompareRelics(left, right, sortCriteria, isAscending));
    }

    public static bool MatchesRelicCompendiumFilters(
        RelicData relicData,
        RelicRarityFilter rarityFilter,
        RelicObtainSourceFilter obtainSourceFilter)
    {
        if (relicData == null) return false;
        if (relicData.obtainSource == ItemObtainSource.TestOnly) return false;

        switch (rarityFilter)
        {
            case RelicRarityFilter.Common:
                if (relicData.rarity != RelicRarity.Common) return false;
                break;
            case RelicRarityFilter.Rare:
                if (relicData.rarity != RelicRarity.Rare) return false;
                break;
            case RelicRarityFilter.Unique:
                if (relicData.rarity != RelicRarity.Unique) return false;
                break;
            case RelicRarityFilter.Boss:
                if (relicData.rarity != RelicRarity.Boss) return false;
                break;
        }

        switch (obtainSourceFilter)
        {
            case RelicObtainSourceFilter.Standard:
                if (relicData.obtainSource != ItemObtainSource.Standard) return false;
                break;
            case RelicObtainSourceFilter.BossOnly:
                if (relicData.obtainSource != ItemObtainSource.BossOnly) return false;
                break;
            case RelicObtainSourceFilter.EventOnly:
                if (relicData.obtainSource != ItemObtainSource.EventOnly) return false;
                break;
        }

        return true;
    }

    public static int CompareRelics(RelicData left, RelicData right, RelicSortCriteria sortCriteria, bool isAscending)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return -1;
        if (right == null) return 1;

        string leftName = !string.IsNullOrEmpty(left.LocalizedName) ? left.LocalizedName : left.relicName;
        string rightName = !string.IsNullOrEmpty(right.LocalizedName) ? right.LocalizedName : right.relicName;

        int primaryCompare;
        if (sortCriteria == RelicSortCriteria.Name)
        {
            primaryCompare = string.Compare(leftName, rightName, StringComparison.Ordinal);
        }
        else
        {
            primaryCompare = left.rarity.CompareTo(right.rarity);
        }

        if (primaryCompare != 0)
        {
            return isAscending ? primaryCompare : -primaryCompare;
        }

        int rarityTie = left.rarity.CompareTo(right.rarity);
        if (rarityTie != 0) return rarityTie;

        int nameTie = string.Compare(leftName, rightName, StringComparison.Ordinal);
        if (nameTie != 0) return nameTie;

        return string.Compare(left.relicName, right.relicName, StringComparison.Ordinal);
    }

    private void RefreshRelicCompendiumList()
    {
        ClearSpawnedRelicRows();

        FilterAndSortRelicsInto(
            _allCompendiumRelics,
            _filteredCompendiumRelics,
            _relicRarityFilter,
            _relicObtainSourceFilter,
            _relicSortCriteria,
            _isRelicSortAscending);

        if (relicCountText != null)
        {
            relicCountText.text = $"표시 중인 유물: {_filteredCompendiumRelics.Count} / 전체 {_allCompendiumRelics.Count}종";
        }

        if (relicListContent == null)
        {
            return;
        }

        TMP_FontAsset fontAsset = ResolveDefaultFont();
        for (int i = 0; i < _filteredCompendiumRelics.Count; i++)
        {
            GameObject rowObj = CreateRelicEntryRow(relicListContent, _filteredCompendiumRelics[i], fontAsset);
            if (rowObj != null)
            {
                _spawnedRelicRows.Add(rowObj);
            }
        }
    }

    private void ClearSpawnedRelicRows()
    {
        for (int i = 0; i < _spawnedRelicRows.Count; i++)
        {
            if (_spawnedRelicRows[i] != null)
            {
                DestroyOrImmediate(_spawnedRelicRows[i]);
            }
        }
        _spawnedRelicRows.Clear();
    }

    private void ResetRelicFilterAndSortState()
    {
        _relicRarityFilter = RelicRarityFilter.All;
        _relicObtainSourceFilter = RelicObtainSourceFilter.All;
        _relicSortCriteria = RelicSortCriteria.Rarity;
        _isRelicSortAscending = true;
    }

    private void EnsureRelicFilterSortBar()
    {
        if (relicCompendiumGroup == null)
        {
            return;
        }

        if (relicCountText != null)
        {
            RectTransform countRect = relicCountText.GetComponent<RectTransform>();
            if (countRect != null)
            {
                countRect.anchorMin = new Vector2(0f, 1f);
                countRect.anchorMax = new Vector2(1f, 1f);
                countRect.pivot = new Vector2(0.5f, 1f);
                countRect.anchoredPosition = Vector2.zero;
                countRect.sizeDelta = new Vector2(0f, 26f);
            }
        }

        Transform scrollTr = relicCompendiumGroup.transform.Find("RelicScrollView");
        if (scrollTr == null && relicListContent != null && relicListContent.parent != null)
        {
            scrollTr = relicListContent.parent.parent;
        }
        if (scrollTr != null)
        {
            RectTransform scrollRect = scrollTr.GetComponent<RectTransform>();
            if (scrollRect != null)
            {
                scrollRect.anchorMin = Vector2.zero;
                scrollRect.anchorMax = Vector2.one;
                scrollRect.offsetMin = new Vector2(0f, 56f);
                scrollRect.offsetMax = new Vector2(0f, -100f);
            }
        }

        if (relicFilterSortBarRoot != null)
        {
            ResolveRelicFilterSortChildReferences(relicFilterSortBarRoot.transform);
            return;
        }

        Transform existingBar = relicCompendiumGroup.transform.Find("RelicFilterSortBar");
        if (existingBar != null)
        {
            relicFilterSortBarRoot = existingBar.gameObject;
            ResolveRelicFilterSortChildReferences(existingBar);
            return;
        }

        TMP_FontAsset fontAsset = ResolveDefaultFont();

        GameObject barObj = CreateUIObject("RelicFilterSortBar", relicCompendiumGroup.transform);
        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -28f);
        barRect.sizeDelta = new Vector2(0f, 66f);

        VerticalLayoutGroup vLayout = barObj.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 4f;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = true;

        GameObject filterRow = CreateUIObject("FilterRow", barObj.transform);
        HorizontalLayoutGroup filterRowLayout = filterRow.AddComponent<HorizontalLayoutGroup>();
        filterRowLayout.spacing = 6f;
        filterRowLayout.childControlWidth = true;
        filterRowLayout.childControlHeight = true;
        filterRowLayout.childForceExpandWidth = true;
        filterRowLayout.childForceExpandHeight = true;

        GameObject sortRow = CreateUIObject("SortRow", barObj.transform);
        HorizontalLayoutGroup sortRowLayout = sortRow.AddComponent<HorizontalLayoutGroup>();
        sortRowLayout.spacing = 6f;
        sortRowLayout.childControlWidth = true;
        sortRowLayout.childControlHeight = true;
        sortRowLayout.childForceExpandWidth = true;
        sortRowLayout.childForceExpandHeight = true;

        relicFilterSortBarRoot = barObj;

        Color filterBg = new Color(0.20f, 0.27f, 0.38f, 1f);
        Color sortBg = new Color(0.22f, 0.33f, 0.28f, 1f);

        relicRarityFilterButton = CreateCompactFilterButton("RelicRarityFilterButton", filterRow.transform, "등급: 전체", filterBg, fontAsset, out relicRarityFilterText);
        relicObtainSourceFilterButton = CreateCompactFilterButton("RelicObtainSourceFilterButton", filterRow.transform, "획득처: 전체", filterBg, fontAsset, out relicObtainSourceFilterText);
        relicSortCriteriaButton = CreateCompactFilterButton("RelicSortCriteriaButton", sortRow.transform, "정렬: 등급순", sortBg, fontAsset, out relicSortCriteriaText);
        relicSortDirectionButton = CreateCompactFilterButton("RelicSortDirectionButton", sortRow.transform, "▲ 오름차순", sortBg, fontAsset, out relicSortDirectionText);
    }

    private void ResolveRelicFilterSortChildReferences(Transform barTransform)
    {
        if (barTransform == null) return;

        ResolveCompactButton(barTransform, "FilterRow/RelicRarityFilterButton", "RelicRarityFilterButton", ref relicRarityFilterButton, ref relicRarityFilterText);
        ResolveCompactButton(barTransform, "FilterRow/RelicObtainSourceFilterButton", "RelicObtainSourceFilterButton", ref relicObtainSourceFilterButton, ref relicObtainSourceFilterText);
        ResolveCompactButton(barTransform, "SortRow/RelicSortCriteriaButton", "RelicSortCriteriaButton", ref relicSortCriteriaButton, ref relicSortCriteriaText);
        ResolveCompactButton(barTransform, "SortRow/RelicSortDirectionButton", "RelicSortDirectionButton", ref relicSortDirectionButton, ref relicSortDirectionText);
    }

    private static void ResolveCompactButton(
        Transform parent,
        string path,
        string fallbackName,
        ref Button button,
        ref TextMeshProUGUI label)
    {
        if (button == null)
        {
            Transform target = parent.Find(path);
            if (target == null)
            {
                target = parent.Find(fallbackName);
            }
            if (target != null)
            {
                button = target.GetComponent<Button>();
            }
        }

        if (button != null && label == null)
        {
            label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void BindRelicFilterSortListeners()
    {
        if (_relicFilterListenersBound) return;

        if (relicRarityFilterButton != null) relicRarityFilterButton.onClick.AddListener(CycleRelicRarityFilter);
        if (relicObtainSourceFilterButton != null) relicObtainSourceFilterButton.onClick.AddListener(CycleRelicObtainSourceFilter);
        if (relicSortCriteriaButton != null) relicSortCriteriaButton.onClick.AddListener(CycleRelicSortCriteria);
        if (relicSortDirectionButton != null) relicSortDirectionButton.onClick.AddListener(ToggleRelicSortDirection);

        _relicFilterListenersBound = true;
    }

    private void UnbindRelicFilterSortListeners()
    {
        if (!_relicFilterListenersBound) return;

        if (relicRarityFilterButton != null) relicRarityFilterButton.onClick.RemoveListener(CycleRelicRarityFilter);
        if (relicObtainSourceFilterButton != null) relicObtainSourceFilterButton.onClick.RemoveListener(CycleRelicObtainSourceFilter);
        if (relicSortCriteriaButton != null) relicSortCriteriaButton.onClick.RemoveListener(CycleRelicSortCriteria);
        if (relicSortDirectionButton != null) relicSortDirectionButton.onClick.RemoveListener(ToggleRelicSortDirection);

        _relicFilterListenersBound = false;
    }

    private void UpdateRelicFilterSortControlBarUI()
    {
        if (relicRarityFilterText != null) relicRarityFilterText.text = GetRelicRarityFilterLabel(_relicRarityFilter);
        if (relicObtainSourceFilterText != null) relicObtainSourceFilterText.text = GetRelicObtainSourceFilterLabel(_relicObtainSourceFilter);
        if (relicSortCriteriaText != null) relicSortCriteriaText.text = GetRelicSortCriteriaLabel(_relicSortCriteria);
        if (relicSortDirectionText != null) relicSortDirectionText.text = _isRelicSortAscending ? "▲ 오름차순" : "▼ 내림차순";
    }

    private static string GetRelicRarityFilterLabel(RelicRarityFilter filter)
    {
        switch (filter)
        {
            case RelicRarityFilter.Common: return "등급: Common";
            case RelicRarityFilter.Rare: return "등급: Rare";
            case RelicRarityFilter.Unique: return "등급: Unique";
            case RelicRarityFilter.Boss: return "등급: Boss";
            default: return "등급: 전체";
        }
    }

    private static string GetRelicObtainSourceFilterLabel(RelicObtainSourceFilter filter)
    {
        switch (filter)
        {
            case RelicObtainSourceFilter.Standard: return "획득처: 일반(Standard)";
            case RelicObtainSourceFilter.BossOnly: return "획득처: 보스(BossOnly)";
            case RelicObtainSourceFilter.EventOnly: return "획득처: 이벤트(EventOnly)";
            default: return "획득처: 전체";
        }
    }

    private static string GetRelicSortCriteriaLabel(RelicSortCriteria criteria)
    {
        return criteria == RelicSortCriteria.Name ? "정렬: 이름순" : "정렬: 등급순";
    }

    private static Button CreateCompactFilterButton(
        string name,
        Transform parent,
        string initialText,
        Color bgColor,
        TMP_FontAsset fontAsset,
        out TextMeshProUGUI labelText)
    {
        GameObject btnObj = CreateUIObject(name, parent);
        LayoutElement layoutElem = btnObj.AddComponent<LayoutElement>();
        layoutElem.minHeight = 28f;
        layoutElem.flexibleWidth = 1f;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        labelText = CreateTMPText("Label", btnObj.transform, initialText, 14, FontStyles.Bold, TextAlignmentOptions.Center, fontAsset);
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 10;
        labelText.fontSizeMax = 14;
        RectTransform txtRect = labelText.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(6f, 2f);
        txtRect.offsetMax = new Vector2(-6f, -2f);

        return btn;
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

    private static int CompareCardDataForCompendium(CardData left, CardData right)
    {
        int rarityCompare = left.rarity.CompareTo(right.rarity);
        if (rarityCompare != 0)
        {
            return rarityCompare;
        }

        int costCompare = left.stamina.CompareTo(right.stamina);
        if (costCompare != 0)
        {
            return costCompare;
        }

        int nameCompare = string.Compare(left.LocalizedName, right.LocalizedName, StringComparison.Ordinal);
        if (nameCompare != 0)
        {
            return nameCompare;
        }

        return string.Compare(left.cardName, right.cardName, StringComparison.Ordinal);
    }

    private void ShowStatusMessage(string message)
    {
        if (statusMessageText != null)
        {
            statusMessageText.text = message;
            statusMessageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
        else
        {
            Debug.LogWarning($"[PauseMenuPanel] {message}");
        }
    }

    private void ClearStatusMessage()
    {
        if (statusMessageText != null)
        {
            statusMessageText.text = string.Empty;
            statusMessageText.gameObject.SetActive(false);
        }
    }

    private void ResolveComponentReferences()
    {
        if (mainMenuGroup != null &&
            settingsGroup != null &&
            resumeButton != null &&
            compendiumButton != null &&
            settingsButton != null &&
            returnToTitleButton != null &&
            masterVolumeSlider != null &&
            fullScreenToggle != null &&
            tooltipToggle != null &&
            settingsBackButton != null)
        {
            return;
        }

        Transform panelRoot = transform.Find("WindowFrame");
        if (panelRoot == null)
        {
            BuildFallbackHierarchy();
            return;
        }

        if (titleText == null)
        {
            Transform titleTr = panelRoot.Find("HeaderTitleText");
            if (titleTr != null) titleText = titleTr.GetComponent<TextMeshProUGUI>();
        }

        if (statusMessageText == null)
        {
            Transform statusTr = panelRoot.Find("StatusMessageText");
            if (statusTr != null) statusMessageText = statusTr.GetComponent<TextMeshProUGUI>();
        }

        Transform mainTr = panelRoot.Find("MainMenuGroup");
        if (mainMenuGroup == null && mainTr != null) mainMenuGroup = mainTr.gameObject;
        if (mainTr != null)
        {
            if (resumeButton == null) resumeButton = FindChildComponent<Button>(mainTr, "ResumeButton");
            if (compendiumButton == null) compendiumButton = FindChildComponent<Button>(mainTr, "CompendiumButton");
            if (relicCompendiumButton == null) relicCompendiumButton = FindChildComponent<Button>(mainTr, "RelicCompendiumButton");
            if (settingsButton == null) settingsButton = FindChildComponent<Button>(mainTr, "SettingsButton");
            if (returnToTitleButton == null) returnToTitleButton = FindChildComponent<Button>(mainTr, "ReturnToTitleButton");
        }

        Transform settingsTr = panelRoot.Find("SettingsGroup");
        if (settingsGroup == null && settingsTr != null) settingsGroup = settingsTr.gameObject;
        if (settingsTr != null)
        {
            if (masterVolumeSlider == null) masterVolumeSlider = FindChildComponent<Slider>(settingsTr, "MasterVolumeRow/MasterVolumeSlider");
            if (masterVolumeValueText == null) masterVolumeValueText = FindChildComponent<TextMeshProUGUI>(settingsTr, "MasterVolumeRow/MasterVolumeValueText");
            if (fullScreenToggle == null) fullScreenToggle = FindChildComponent<Toggle>(settingsTr, "FullScreenRow/FullScreenToggle");
            if (fullScreenStatusText == null) fullScreenStatusText = FindChildComponent<TextMeshProUGUI>(settingsTr, "FullScreenRow/FullScreenStatusText");
            if (tooltipToggle == null) tooltipToggle = FindChildComponent<Toggle>(settingsTr, "TooltipRow/TooltipToggle");
            if (tooltipStatusText == null) tooltipStatusText = FindChildComponent<TextMeshProUGUI>(settingsTr, "TooltipRow/TooltipStatusText");
            if (settingsBackButton == null) settingsBackButton = FindChildComponent<Button>(settingsTr, "SettingsBackButton");
        }

        Transform relicTr = panelRoot.Find("RelicCompendiumGroup");
        if (relicCompendiumGroup == null && relicTr != null) relicCompendiumGroup = relicTr.gameObject;
        if (relicTr != null)
        {
            if (relicCountText == null) relicCountText = FindChildComponent<TextMeshProUGUI>(relicTr, "RelicCountText");
            if (relicListContent == null) relicListContent = relicTr.Find("RelicScrollView/Viewport/Content");
            if (relicBackButton == null) relicBackButton = FindChildComponent<Button>(relicTr, "RelicBackButton");
        }
    }

    private void EnsureTooltipSettingRow()
    {
        if (settingsGroup == null)
        {
            return;
        }

        Transform rowTr = settingsGroup.transform.Find("TooltipRow");
        if (rowTr == null)
        {
            TMP_FontAsset fontAsset = (fullScreenStatusText != null && fullScreenStatusText.font != null)
                ? fullScreenStatusText.font
                : ResolveDefaultFont();
            rowTr = CreateTooltipSettingRow(settingsGroup.transform, fontAsset);
        }
        else
        {
            if (tooltipToggle == null) tooltipToggle = FindChildComponent<Toggle>(rowTr, "TooltipToggle");
            if (tooltipStatusText == null) tooltipStatusText = FindChildComponent<TextMeshProUGUI>(rowTr, "TooltipStatusText");
        }

        if (rowTr != null && settingsBackButton != null && settingsBackButton.transform.parent == settingsGroup.transform)
        {
            int backIndex = settingsBackButton.transform.GetSiblingIndex();
            if (rowTr.GetSiblingIndex() > backIndex)
            {
                rowTr.SetSiblingIndex(backIndex);
            }
        }

        if (_listenersBound && tooltipToggle != null)
        {
            tooltipToggle.onValueChanged.RemoveListener(OnTooltipToggleChanged);
            tooltipToggle.onValueChanged.AddListener(OnTooltipToggleChanged);
        }
    }

    private Transform CreateTooltipSettingRow(Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject tooltipRow = CreateUIObject("TooltipRow", parent);
        LayoutElement rowLayout = tooltipRow.AddComponent<LayoutElement>();
        rowLayout.minHeight = 64f;
        rowLayout.preferredHeight = 64f;

        TextMeshProUGUI tooltipLabel = CreateTMPText("Label", tooltipRow.transform, "툴팁 표시 ([T])", 20, FontStyles.Bold, TextAlignmentOptions.Left, fontAsset);
        RectTransform tooltipLabelRect = tooltipLabel.GetComponent<RectTransform>();
        tooltipLabelRect.anchorMin = new Vector2(0f, 0.5f);
        tooltipLabelRect.anchorMax = new Vector2(0.55f, 0.5f);
        tooltipLabelRect.pivot = new Vector2(0f, 0.5f);
        tooltipLabelRect.anchoredPosition = Vector2.zero;
        tooltipLabelRect.sizeDelta = new Vector2(0f, 32f);

        tooltipStatusText = CreateTMPText("TooltipStatusText", tooltipRow.transform, TooltipManager.IsTooltipEnabled ? "ON" : "OFF", 18, FontStyles.Normal, TextAlignmentOptions.Right, fontAsset);
        RectTransform tooltipStatusRect = tooltipStatusText.GetComponent<RectTransform>();
        tooltipStatusRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipStatusRect.anchorMax = new Vector2(0.84f, 0.5f);
        tooltipStatusRect.pivot = new Vector2(1f, 0.5f);
        tooltipStatusRect.anchoredPosition = Vector2.zero;
        tooltipStatusRect.sizeDelta = new Vector2(0f, 30f);

        tooltipToggle = CreateToggle("TooltipToggle", tooltipRow.transform);
        tooltipToggle.SetIsOnWithoutNotify(TooltipManager.IsTooltipEnabled);

        return tooltipRow.transform;
    }

    private static T FindChildComponent<T>(Transform parent, string relativePath) where T : Component
    {
        if (parent == null) return null;
        Transform target = parent.Find(relativePath);
        return target != null ? target.GetComponent<T>() : null;
    }

    private void BuildFallbackHierarchy()
    {
        TMP_FontAsset fontAsset = ResolveDefaultFont();

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image backdrop = GetComponent<Image>();
        if (backdrop == null)
        {
            backdrop = gameObject.AddComponent<Image>();
        }
        backdrop.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);
        backdrop.raycastTarget = true;

        GameObject windowObj = CreateUIObject("WindowFrame", transform);
        RectTransform windowRect = windowObj.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(460f, 520f);
        Image windowBg = windowObj.AddComponent<Image>();
        windowBg.color = new Color(0.11f, 0.13f, 0.18f, 0.97f);

        titleText = CreateTMPText("HeaderTitleText", windowObj.transform, "일시정지 메뉴", 30, FontStyles.Bold, TextAlignmentOptions.Center, fontAsset);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(-40f, 44f);

        statusMessageText = CreateTMPText("StatusMessageText", windowObj.transform, string.Empty, 16, FontStyles.Normal, TextAlignmentOptions.Center, fontAsset);
        statusMessageText.color = new Color(1f, 0.76f, 0.42f, 1f);
        RectTransform statusRect = statusMessageText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 14f);
        statusRect.sizeDelta = new Vector2(-40f, 28f);
        statusMessageText.gameObject.SetActive(false);

        // Main Menu Group
        mainMenuGroup = CreateUIObject("MainMenuGroup", windowObj.transform);
        RectTransform mainRect = mainMenuGroup.GetComponent<RectTransform>();
        mainRect.anchorMin = Vector2.zero;
        mainRect.anchorMax = Vector2.one;
        mainRect.offsetMin = new Vector2(40f, 52f);
        mainRect.offsetMax = new Vector2(-40f, -84f);

        VerticalLayoutGroup mainLayout = mainMenuGroup.AddComponent<VerticalLayoutGroup>();
        mainLayout.spacing = 14f;
        mainLayout.childAlignment = TextAnchor.MiddleCenter;
        mainLayout.childControlWidth = true;
        mainLayout.childControlHeight = true;
        mainLayout.childForceExpandWidth = true;
        mainLayout.childForceExpandHeight = false;

        resumeButton = CreateActionButton("ResumeButton", mainMenuGroup.transform, "게임 복귀", new Color(0.20f, 0.44f, 0.34f, 1f), fontAsset);
        compendiumButton = CreateActionButton("CompendiumButton", mainMenuGroup.transform, "도감 열람", new Color(0.21f, 0.30f, 0.44f, 1f), fontAsset);
        relicCompendiumButton = CreateActionButton("RelicCompendiumButton", mainMenuGroup.transform, "유물 도감", new Color(0.24f, 0.28f, 0.38f, 1f), fontAsset);
        settingsButton = CreateActionButton("SettingsButton", mainMenuGroup.transform, "게임 설정", new Color(0.25f, 0.28f, 0.36f, 1f), fontAsset);
        returnToTitleButton = CreateActionButton("ReturnToTitleButton", mainMenuGroup.transform, "타이틀로 돌아가기", new Color(0.45f, 0.22f, 0.24f, 1f), fontAsset);

        // Settings Group
        settingsGroup = CreateUIObject("SettingsGroup", windowObj.transform);
        RectTransform settingsRect = settingsGroup.GetComponent<RectTransform>();
        settingsRect.anchorMin = Vector2.zero;
        settingsRect.anchorMax = Vector2.one;
        settingsRect.offsetMin = new Vector2(36f, 52f);
        settingsRect.offsetMax = new Vector2(-36f, -84f);

        VerticalLayoutGroup settingsLayout = settingsGroup.AddComponent<VerticalLayoutGroup>();
        settingsLayout.spacing = 20f;
        settingsLayout.childAlignment = TextAnchor.UpperCenter;
        settingsLayout.childControlWidth = true;
        settingsLayout.childControlHeight = true;
        settingsLayout.childForceExpandWidth = true;
        settingsLayout.childForceExpandHeight = false;

        BuildSettingsRows(settingsGroup.transform, fontAsset);
        settingsBackButton = CreateActionButton("SettingsBackButton", settingsGroup.transform, "뒤로", new Color(0.26f, 0.30f, 0.38f, 1f), fontAsset);
        settingsGroup.SetActive(false);

        // Relic Compendium Group
        relicCompendiumGroup = CreateUIObject("RelicCompendiumGroup", windowObj.transform);
        RectTransform relicGroupRect = relicCompendiumGroup.GetComponent<RectTransform>();
        relicGroupRect.anchorMin = Vector2.zero;
        relicGroupRect.anchorMax = Vector2.one;
        relicGroupRect.offsetMin = new Vector2(28f, 48f);
        relicGroupRect.offsetMax = new Vector2(-28f, -80f);

        relicCountText = CreateTMPText("RelicCountText", relicCompendiumGroup.transform, "등록된 유물: 0종", 18, FontStyles.Normal, TextAlignmentOptions.Left, fontAsset);
        RectTransform countRect = relicCountText.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0f, 1f);
        countRect.anchorMax = new Vector2(1f, 1f);
        countRect.pivot = new Vector2(0.5f, 1f);
        countRect.anchoredPosition = Vector2.zero;
        countRect.sizeDelta = new Vector2(0f, 28f);

        relicListContent = CreateScrollListContent(relicCompendiumGroup.transform);
        relicBackButton = CreateActionButton("RelicBackButton", relicCompendiumGroup.transform, "뒤로", new Color(0.26f, 0.30f, 0.38f, 1f), fontAsset);
        RectTransform relicBackRect = relicBackButton.GetComponent<RectTransform>();
        relicBackRect.anchorMin = new Vector2(0.2f, 0f);
        relicBackRect.anchorMax = new Vector2(0.8f, 0f);
        relicBackRect.pivot = new Vector2(0.5f, 0f);
        relicBackRect.anchoredPosition = Vector2.zero;
        relicBackRect.sizeDelta = new Vector2(0f, 48f);

        relicCompendiumGroup.SetActive(false);
    }

    private void BuildSettingsRows(Transform parent, TMP_FontAsset fontAsset)
    {
        // 1. 마스터 볼륨 슬라이더 행
        GameObject volumeRow = CreateUIObject("MasterVolumeRow", parent);
        LayoutElement volumeLayout = volumeRow.AddComponent<LayoutElement>();
        volumeLayout.preferredHeight = 76f;

        TextMeshProUGUI volLabel = CreateTMPText("Label", volumeRow.transform, "전체 음량 (Master Volume)", 20, FontStyles.Bold, TextAlignmentOptions.Left, fontAsset);
        RectTransform volLabelRect = volLabel.GetComponent<RectTransform>();
        volLabelRect.anchorMin = new Vector2(0f, 1f);
        volLabelRect.anchorMax = new Vector2(0.75f, 1f);
        volLabelRect.pivot = new Vector2(0f, 1f);
        volLabelRect.anchoredPosition = Vector2.zero;
        volLabelRect.sizeDelta = new Vector2(0f, 30f);

        masterVolumeValueText = CreateTMPText("MasterVolumeValueText", volumeRow.transform, "100%", 20, FontStyles.Normal, TextAlignmentOptions.Right, fontAsset);
        RectTransform volValRect = masterVolumeValueText.GetComponent<RectTransform>();
        volValRect.anchorMin = new Vector2(0.75f, 1f);
        volValRect.anchorMax = new Vector2(1f, 1f);
        volValRect.pivot = new Vector2(1f, 1f);
        volValRect.anchoredPosition = Vector2.zero;
        volValRect.sizeDelta = new Vector2(0f, 30f);

        masterVolumeSlider = CreateSlider("MasterVolumeSlider", volumeRow.transform);

        // 2. 화면 모드 토글 행
        GameObject screenRow = CreateUIObject("FullScreenRow", parent);
        LayoutElement screenLayout = screenRow.AddComponent<LayoutElement>();
        screenLayout.preferredHeight = 64f;

        TextMeshProUGUI screenLabel = CreateTMPText("Label", screenRow.transform, "화면 모드", 20, FontStyles.Bold, TextAlignmentOptions.Left, fontAsset);
        RectTransform screenLabelRect = screenLabel.GetComponent<RectTransform>();
        screenLabelRect.anchorMin = new Vector2(0f, 0.5f);
        screenLabelRect.anchorMax = new Vector2(0.55f, 0.5f);
        screenLabelRect.pivot = new Vector2(0f, 0.5f);
        screenLabelRect.anchoredPosition = Vector2.zero;
        screenLabelRect.sizeDelta = new Vector2(0f, 32f);

        fullScreenStatusText = CreateTMPText("FullScreenStatusText", screenRow.transform, "전체화면", 18, FontStyles.Normal, TextAlignmentOptions.Right, fontAsset);
        RectTransform screenStatusRect = fullScreenStatusText.GetComponent<RectTransform>();
        screenStatusRect.anchorMin = new Vector2(0.5f, 0.5f);
        screenStatusRect.anchorMax = new Vector2(0.84f, 0.5f);
        screenStatusRect.pivot = new Vector2(1f, 0.5f);
        screenStatusRect.anchoredPosition = Vector2.zero;
        screenStatusRect.sizeDelta = new Vector2(0f, 30f);

        fullScreenToggle = CreateToggle("FullScreenToggle", screenRow.transform);

        // 3. 툴팁 표시 토글 행
        CreateTooltipSettingRow(parent, fontAsset);
    }

    private static Slider CreateSlider(string name, Transform parent)
    {
        GameObject sliderObj = CreateUIObject(name, parent);
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 8f);
        sliderRect.sizeDelta = new Vector2(0f, 28f);

        GameObject bgObj = CreateUIObject("Background", sliderObj.transform);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.22f, 0.25f, 0.32f, 1f);

        GameObject fillArea = CreateUIObject("Fill Area", sliderObj.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5f, 0f);
        fillAreaRect.offsetMax = new Vector2(-5f, 0f);

        GameObject fillObj = CreateUIObject("Fill", fillArea.transform);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.35f, 0.68f, 0.92f, 1f);

        GameObject handleArea = CreateUIObject("Handle Slide Area", sliderObj.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handleObj = CreateUIObject("Handle", handleArea.transform);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(22f, 28f);
        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.color = new Color(0.95f, 0.96f, 0.99f, 1f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }

    private static Toggle CreateToggle(string name, Transform parent)
    {
        GameObject toggleObj = CreateUIObject(name, parent);
        RectTransform toggleRect = toggleObj.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1f, 0.5f);
        toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.anchoredPosition = Vector2.zero;
        toggleRect.sizeDelta = new Vector2(36f, 36f);

        GameObject bgObj = CreateUIObject("Background", toggleObj.transform);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.22f, 0.25f, 0.32f, 1f);

        GameObject checkObj = CreateUIObject("Checkmark", bgObj.transform);
        RectTransform checkRect = checkObj.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.18f, 0.18f);
        checkRect.anchorMax = new Vector2(0.82f, 0.82f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        Image checkImg = checkObj.AddComponent<Image>();
        checkImg.color = new Color(0.36f, 0.82f, 0.56f, 1f);

        Toggle toggle = toggleObj.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        return toggle;
    }

    private static Transform CreateScrollListContent(Transform parent)
    {
        GameObject scrollObj = CreateUIObject("RelicScrollView", parent);
        RectTransform scrollRectTransform = scrollObj.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(0f, 56f);
        scrollRectTransform.offsetMax = new Vector2(0f, -100f);

        Image scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0.07f, 0.08f, 0.12f, 0.9f);

        GameObject viewport = CreateUIObject("Viewport", scrollObj.transform);
        RectTransform vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = new Vector2(6f, 6f);
        vpRect.offsetMax = new Vector2(-6f, -6f);
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.01f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = CreateUIObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vLayout = content.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 8f;
        vLayout.padding = new RectOffset(4, 4, 4, 4);
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.viewport = vpRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 25f;

        return content.transform;
    }

    private static GameObject CreateRelicEntryRow(Transform parent, RelicData relicData, TMP_FontAsset fontAsset)
    {
        GameObject rowObj = CreateUIObject($"Relic_{relicData.relicName}", parent);
        LayoutElement rowLayout = rowObj.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 72f;
        rowLayout.minHeight = 72f;

        Image rowBg = rowObj.AddComponent<Image>();
        rowBg.color = new Color(0.15f, 0.17f, 0.24f, 0.95f);

        GameObject iconObj = CreateUIObject("Icon", rowObj.transform);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(10f, 0f);
        iconRect.sizeDelta = new Vector2(52f, 52f);
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        if (relicData.iconSprite != null)
        {
            iconImg.sprite = relicData.iconSprite;
            iconImg.color = Color.white;
        }
        else
        {
            iconImg.color = new Color(0.35f, 0.35f, 0.42f, 0.8f);
        }

        string title = !string.IsNullOrEmpty(relicData.LocalizedName) ? relicData.LocalizedName : relicData.relicName;
        TextMeshProUGUI nameText = CreateTMPText("NameText", rowObj.transform, title, 18, FontStyles.Bold, TextAlignmentOptions.Left, fontAsset);
        RectTransform nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.offsetMin = new Vector2(72f, -28f);
        nameRect.offsetMax = new Vector2(-10f, -6f);

        string desc = !string.IsNullOrEmpty(relicData.LocalizedDescription) ? relicData.LocalizedDescription : string.Empty;
        TextMeshProUGUI descText = CreateTMPText("DescText", rowObj.transform, desc, 14, FontStyles.Normal, TextAlignmentOptions.TopLeft, fontAsset);
        descText.color = new Color(0.80f, 0.84f, 0.90f, 1f);
        descText.textWrappingMode = TextWrappingModes.Normal;
        RectTransform descRect = descText.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.offsetMin = new Vector2(72f, 6f);
        descRect.offsetMax = new Vector2(-10f, -28f);

        return rowObj;
    }

    private static Button CreateActionButton(string name, Transform parent, string label, Color bgColor, TMP_FontAsset fontAsset)
    {
        GameObject btnObj = CreateUIObject(name, parent);
        LayoutElement layout = btnObj.AddComponent<LayoutElement>();
        layout.preferredHeight = 52f;
        layout.minHeight = 52f;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        TextMeshProUGUI txt = CreateTMPText("Label", btnObj.transform, label, 22, FontStyles.Bold, TextAlignmentOptions.Center, fontAsset);
        RectTransform txtRect = txt.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(10f, 4f);
        txtRect.offsetMax = new Vector2(-10f, -4f);

        return btn;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI Layer
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateTMPText(
        string name,
        Transform parent,
        string content,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        TMP_FontAsset fontAsset)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (fontAsset != null)
        {
            tmp.font = fontAsset;
        }
        return tmp;
    }

    private static TMP_FontAsset ResolveDefaultFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        if (loadedFonts != null && loadedFonts.Length > 0)
        {
            return loadedFonts[0];
        }

        return null;
    }
}
