using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TopHUDPanel : UIPanelBase
{
    [Header("Relic UI")]
    [SerializeField] private PlayerRelicUI playerRelicUI;

    public PlayerRelicUI PlayerRelicUI => playerRelicUI;

    [Header("Location Display")]
    [SerializeField] private TextMeshProUGUI locationFloorText;

    [Header("Currency TMP Texts")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI specialResourceText;

    [Header("Action Buttons")]
    [SerializeField] private Button mapButton;
    [SerializeField] private Button deckButton;
    [SerializeField] private Button pauseMenuButton;

    [Header("Tooltip Toggle")]
    [SerializeField] private Button tooltipToggleButton;
    [SerializeField] private TextMeshProUGUI tooltipToggleText;
    [SerializeField] private Image tooltipToggleBgImage;

    private static readonly Color TooltipOnBgColor = new Color(0.14f, 0.28f, 0.36f, 0.95f);
    private static readonly Color TooltipOnTextColor = new Color(0.72f, 0.96f, 1f, 1f);
    private static readonly Color TooltipOffBgColor = new Color(0.28f, 0.16f, 0.18f, 0.95f);
    private static readonly Color TooltipOffTextColor = new Color(0.86f, 0.65f, 0.65f, 1f);

    [Header("UI Texture")]
    [SerializeField] private Image currencyImage;
    [SerializeField] private Image specialResourceImage;
    [SerializeField] private Image mapButtonImage;
    [SerializeField] private Image deckButtonImage;

    private Player _boundPlayer;
    private CardDeckViewPanel _deckViewPanel;

    private void Awake()
    {
        blockWorldInput = false;

        if (playerRelicUI == null)
        {
            Debug.LogError("[TopHUDPanel] playerRelicUI가 바인딩되지 않았습니다! TopHUDPanel 프리팹 설정을 확인하세요.", this);
        }

        EnsureTierLayoutReferences();
    }

    private void OnEnable()
    {
        GameSceneManager.OnSceneLocationChanged += RefreshLocationAndNavigationState;
        RunManager.OnRunLocationChanged += RefreshLocationAndNavigationState;
        TooltipManager.OnTooltipEnabledChanged -= RefreshTooltipToggleVisual;
        TooltipManager.OnTooltipEnabledChanged += RefreshTooltipToggleVisual;
        EnsureTierLayoutReferences();
        RefreshLocationAndNavigationState();
        RefreshTooltipToggleVisual(TooltipManager.IsTooltipEnabled);
    }

    private void OnDisable()
    {
        GameSceneManager.OnSceneLocationChanged -= RefreshLocationAndNavigationState;
        RunManager.OnRunLocationChanged -= RefreshLocationAndNavigationState;
        TooltipManager.OnTooltipEnabledChanged -= RefreshTooltipToggleVisual;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        EnsureTierLayoutReferences();
        RefreshLocationAndNavigationState();
        RefreshTooltipToggleVisual(TooltipManager.IsTooltipEnabled);
    }

    /// <summary>
    /// 플레이어 인스턴스를 HUD에 바인딩하여 재화 및 유물 갱신 이벤트를 직접 수신합니다.
    /// </summary>
    public void BindPlayer(Player player)
    {
        if (_boundPlayer == player) return;

        UnbindPlayer();
        _boundPlayer = player;

        if (_boundPlayer != null && _boundPlayer.playerStat != null)
        {
            UpdateGold(_boundPlayer.playerStat.InGameCurrencyGold);
            UpdateSpecialResource(_boundPlayer.playerStat.InGameCurrencyMemorySharp);

            _boundPlayer.playerStat.OnGoldChanged += UpdateGold;
            _boundPlayer.playerStat.OnMemorySharpChanged += UpdateSpecialResource;

            if (playerRelicUI != null)
            {
                playerRelicUI.BindPlayer(_boundPlayer);
            }
        }

        // 기본 버튼 클릭 리스너 연결
        SetupHUD(ToggleExploreMap, ToggleDeckView);
        EnsureTierLayoutReferences();
        RefreshLocationAndNavigationState();
    }

    /// <summary>
    /// 등록된 플레이어 스탯 이벤트 구독을 해제합니다.
    /// </summary>
    public void UnbindPlayer()
    {
        if (_boundPlayer != null && _boundPlayer.playerStat != null)
        {
            _boundPlayer.playerStat.OnGoldChanged -= UpdateGold;
            _boundPlayer.playerStat.OnMemorySharpChanged -= UpdateSpecialResource;

            if (playerRelicUI != null)
            {
                playerRelicUI.BindPlayer(null);
            }
        }
        _boundPlayer = null;
    }

    private void OnDestroy()
    {
        UnbindPlayer();
    }

    /// <summary>
    /// 전체화면 덱 보기 창을 토글합니다.
    /// </summary>
    public void ToggleDeckView()
    {
        if (UIManager.instance == null) return;

        if (_deckViewPanel == null)
        {
            GameObject obj = UIManager.instance.OpenUI(UIConstants.PANEL_CARD_DECK, UILayerType.Normal, false);
            if (obj != null)
            {
                _deckViewPanel = obj.GetComponent<CardDeckViewPanel>();
            }
        }

        if (_deckViewPanel != null)
        {
            if (_deckViewPanel.gameObject.activeSelf)
            {
                _deckViewPanel.Close();
            }
            else
            {
                _deckViewPanel.uiLayerType = UILayerType.Normal;
                UIManager.instance.PushActiveUIPanel(_deckViewPanel);
                _deckViewPanel.OpenMasterDeck();
            }
        }
    }

    /// <summary>
    /// 탐색 맵(ExploreMap) 창을 토글합니다.
    /// </summary>
    public void ToggleExploreMap()
    {
        if (UIManager.instance == null) return;
        if (GameSceneManager.instance != null && !GameSceneManager.instance.isInDungeon) return;

        ExploreMapUI exploreMapUI = UIManager.instance.GetUI<ExploreMapUI>(UIConstants.PANEL_EXPLORE_MAP);
        if (exploreMapUI != null && exploreMapUI.gameObject.activeSelf)
        {
            exploreMapUI.Close();
            return;
        }

        if (RunManager.instance != null)
        {
            RunManager.instance.OpenOrShowExploreMap(15);
            return;
        }

        // RunManager가 존재하지 않는 예외 상황에서의 안전 폴백 (프리팹 기본 레이어 유지)
        if (exploreMapUI == null)
        {
            exploreMapUI = UIManager.instance.OpenUI<ExploreMapUI>(UIConstants.PANEL_EXPLORE_MAP, UILayerType.Normal, false);
        }

        if (exploreMapUI != null)
        {
            if (exploreMapUI.MapNodes == null || exploreMapUI.MapNodes.Count == 0)
            {
                exploreMapUI.CreateExploreMap(15);
            }
            UIManager.instance.PushActiveUIPanel(exploreMapUI);
        }
    }

    public void SetupHUD(Action onMapClicked, Action onDeckClicked)
    {
        if (mapButton != null)
        {
            mapButton.onClick.RemoveAllListeners();
            mapButton.onClick.AddListener(() => onMapClicked?.Invoke());
        }
        else
        {
            Debug.LogWarning("Map button is not assigned");
        }

        if (deckButton != null)
        {
            deckButton.onClick.RemoveAllListeners();
            deckButton.onClick.AddListener(() => onDeckClicked?.Invoke());
        }
        else
        {
            Debug.LogWarning("Deck button is not assigned");
        }

        BindPauseMenuButton();
        BindTooltipToggleButton();

        if (AssetCacheManager.instance != null)
        {
            UpdateUIImage();
        }
    }

    private void BindPauseMenuButton()
    {
        if (pauseMenuButton != null)
        {
            pauseMenuButton.onClick.RemoveListener(OpenPauseMenu);
            pauseMenuButton.onClick.AddListener(OpenPauseMenu);
        }
    }

    private void BindTooltipToggleButton()
    {
        if (tooltipToggleButton != null)
        {
            tooltipToggleButton.onClick.RemoveListener(HandleTooltipToggleClicked);
            tooltipToggleButton.onClick.AddListener(HandleTooltipToggleClicked);
        }
    }

    private void HandleTooltipToggleClicked()
    {
        TooltipManager.ToggleGlobalTooltipEnabled();
    }

    private void RefreshTooltipToggleVisual(bool isEnabled)
    {
        if (tooltipToggleText != null)
        {
            tooltipToggleText.text = isEnabled ? "[T] 툴팁: ON" : "[T] 툴팁: OFF";
            tooltipToggleText.color = isEnabled ? TooltipOnTextColor : TooltipOffTextColor;
        }

        if (tooltipToggleBgImage != null)
        {
            tooltipToggleBgImage.color = isEnabled ? TooltipOnBgColor : TooltipOffBgColor;
        }
    }

    /// <summary>
    /// 일시정지/설정 메뉴(PauseMenuPanel)를 토글하거나 엽니다.
    /// </summary>
    public void OpenPauseMenu()
    {
        if (UIManager.instance == null) return;

        PauseMenuPanel pausePanel = UIManager.instance.GetUI<PauseMenuPanel>(UIConstants.PANEL_PAUSE_MENU);
        if (pausePanel != null && pausePanel.gameObject.activeInHierarchy)
        {
            pausePanel.Close();
            return;
        }

        UIManager.instance.OpenPauseMenu();
    }

    /// <summary>
    /// 현재 구역(은신처 vs 던전/층)에 따라 중앙 위치 표시 텍스트와 지도 버튼 활성 상태를 갱신합니다.
    /// </summary>
    public void RefreshLocationAndNavigationState()
    {
        bool inDungeon = GameSceneManager.instance != null && GameSceneManager.instance.isInDungeon;

        if (mapButton != null)
        {
            mapButton.interactable = inDungeon;
        }

        if (locationFloorText == null)
        {
            return;
        }

        if (!inDungeon)
        {
            locationFloorText.text = "은신처 (Hideout)";
            return;
        }

        string stageRaw = null;
        int floor = 1;

        if (RunManager.instance != null)
        {
            stageRaw = RunManager.instance.currentMapName;
            floor = Mathf.Max(1, RunManager.instance.currentExploreMapFloor + 1);

            if (string.IsNullOrEmpty(stageRaw) &&
                RunManager.instance.currentStage != null &&
                RunManager.instance.currentStage.currentMapData != null)
            {
                stageRaw = RunManager.instance.currentStage.currentMapData.locationType.ToString();
            }
        }

        string stageDisplay = FormatStageDisplayName(stageRaw);
        locationFloorText.text = $"{stageDisplay} - {floor}F";
    }

    private static string FormatStageDisplayName(string rawStage)
    {
        if (string.IsNullOrEmpty(rawStage))
        {
            return "던전";
        }

        switch (rawStage)
        {
            case "Temple":
                return "사원 (Temple)";
            case "Dungeon":
                return "던전 (Dungeon)";
            case "Dock":
                return "항구 (Dock)";
            case "Hideout":
                return "은신처 (Hideout)";
            default:
                return rawStage;
        }
    }

    private void EnsureTierLayoutReferences()
    {
        Transform hudPanel = transform.Find("HUDPanel");
        if (hudPanel == null)
        {
            return;
        }

        if (locationFloorText == null)
        {
            Transform locTr = hudPanel.Find("LocationFloorText");
            if (locTr != null)
            {
                locationFloorText = locTr.GetComponent<TextMeshProUGUI>();
            }
        }

        if (pauseMenuButton == null)
        {
            Transform pauseTr = hudPanel.Find("PauseMenuButton");
            if (pauseTr != null)
            {
                pauseMenuButton = pauseTr.GetComponent<Button>();
            }
        }

        if (tooltipToggleButton == null)
        {
            Transform tooltipTr = hudPanel.Find("TooltipToggleButton");
            if (tooltipTr != null)
            {
                tooltipToggleButton = tooltipTr.GetComponent<Button>();
            }
        }

        if (tooltipToggleButton != null)
        {
            if (tooltipToggleText == null)
            {
                tooltipToggleText = tooltipToggleButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (tooltipToggleBgImage == null)
            {
                tooltipToggleBgImage = tooltipToggleButton.GetComponent<Image>();
            }
        }

        BindPauseMenuButton();
        BindTooltipToggleButton();
    }

    public void UpdateGold(int amount)
    {
        if (goldText != null)
        {
            goldText.text = amount.ToString();
        }
    }

    public void UpdateSpecialResource(int amount)
    {
        if (specialResourceText != null)
        {
            specialResourceText.text = amount.ToString();
        }
    }

    private void UpdateUIImage()
    {
        if (AssetCacheManager.instance == null) return;

        if (currencyImage != null)
        {
            if (AssetCacheManager.instance.TryGetTexture("Gold", out Sprite goldTexture))
            {
                currencyImage.sprite = goldTexture;
            }
            else
            {
                Debug.LogWarning("Failed to load Texture_UI_Gold");
            }
        }

        if (specialResourceImage != null)
        {
            if (AssetCacheManager.instance.TryGetTexture("MemoryShard", out Sprite shardTexture))
            {
                specialResourceImage.sprite = shardTexture;
            }
            else
            {
                Debug.LogWarning("Failed to load Texture_UI_MemoryShard");
            }
        }

        if (mapButtonImage != null)
        {
            if (AssetCacheManager.instance.TryGetTexture("MapButtonIcon", out Sprite mapButtonIconTexture))
            {
                mapButtonImage.sprite = mapButtonIconTexture;
            }
            else if (AssetCacheManager.instance.TryGetTexture("ExploreMapTexture", out Sprite exploreMapTexture))
            {
                mapButtonImage.sprite = exploreMapTexture;
            }
            else
            {
                Debug.LogWarning("Failed to load Texture_UI_MapButtonIcon");
            }
        }

        if (deckButtonImage != null)
        {
            if (AssetCacheManager.instance.TryGetTexture("DeckButtonIcon", out Sprite deckButtonIconTexture))
            {
                deckButtonImage.sprite = deckButtonIconTexture;
            }
            else
            {
                Debug.LogWarning("Failed to load Texture_UI_DeckButtonIcon");
            }
        }
    }
}
