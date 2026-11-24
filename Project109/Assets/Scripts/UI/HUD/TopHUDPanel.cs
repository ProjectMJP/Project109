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

    [Header("Node Reopen Button")]
    [SerializeField] private Button nodeReopenButton;
    [SerializeField] private TextMeshProUGUI nodeReopenButtonText;

    [Header("Battle Speed Toggle")]
    [SerializeField] private Button speedToggleButton;
    [SerializeField] private TextMeshProUGUI speedToggleText;
    [SerializeField] private Image speedToggleBgImage;

    private static readonly Color Speed1xBgColor = new Color(0.18f, 0.23f, 0.30f, 0.95f);
    private static readonly Color Speed1xTextColor = new Color(0.82f, 0.88f, 0.94f, 1f);
    private static readonly Color Speed1_5xBgColor = new Color(0.12f, 0.32f, 0.38f, 0.95f);
    private static readonly Color Speed1_5xTextColor = new Color(0.55f, 0.96f, 1f, 1f);
    private static readonly Color Speed2xBgColor = new Color(0.38f, 0.27f, 0.10f, 0.95f);
    private static readonly Color Speed2xTextColor = new Color(1f, 0.86f, 0.38f, 1f);

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

    protected override void OnEnable()
    {
        base.OnEnable();
        GameSceneManager.OnSceneLocationChanged -= RefreshLocationAndNavigationState;
        GameSceneManager.OnSceneLocationChanged += RefreshLocationAndNavigationState;
        RunManager.OnRunLocationChanged -= RefreshLocationAndNavigationState;
        RunManager.OnRunLocationChanged += RefreshLocationAndNavigationState;
        RunManager.OnNodeContextChanged -= RefreshNodeReopenButton;
        RunManager.OnNodeContextChanged += RefreshNodeReopenButton;
        BattleManager.OnBattleSpeedChanged -= RefreshSpeedToggleVisual;
        BattleManager.OnBattleSpeedChanged += RefreshSpeedToggleVisual;
        EnsureTierLayoutReferences();
        EnsureNodeReopenButtonBound();
        RefreshLocationAndNavigationState();
        RefreshNodeReopenButton();
        RefreshSpeedToggleVisual(BattleManager.GlobalBattleSpeed);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        GameSceneManager.OnSceneLocationChanged -= RefreshLocationAndNavigationState;
        RunManager.OnRunLocationChanged -= RefreshLocationAndNavigationState;
        RunManager.OnNodeContextChanged -= RefreshNodeReopenButton;
        BattleManager.OnBattleSpeedChanged -= RefreshSpeedToggleVisual;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        RunManager.OnNodeContextChanged -= RefreshNodeReopenButton;
        RunManager.OnNodeContextChanged += RefreshNodeReopenButton;
        EnsureTierLayoutReferences();
        EnsureNodeReopenButtonBound();
        RefreshLocationAndNavigationState();
        RefreshNodeReopenButton();
        RefreshSpeedToggleVisual(BattleManager.GlobalBattleSpeed);
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
        EnsureNodeReopenButtonBound();
        RefreshLocationAndNavigationState();
        RefreshNodeReopenButton();
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
        GameSceneManager.OnSceneLocationChanged -= RefreshLocationAndNavigationState;
        RunManager.OnRunLocationChanged -= RefreshLocationAndNavigationState;
        RunManager.OnNodeContextChanged -= RefreshNodeReopenButton;
        BattleManager.OnBattleSpeedChanged -= RefreshSpeedToggleVisual;

        if (nodeReopenButton != null)
        {
            nodeReopenButton.onClick.RemoveListener(HandleNodeReopenClicked);
        }
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
            GameObject obj = UIManager.instance.OpenUI(UIConstants.PANEL_CARD_DECK, UILayerType.Popup, false);
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
                _deckViewPanel.uiLayerType = UILayerType.Popup;
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

        // RunManager가 존재하지 않는 예외 상황에서의 안전 폴백 (Popup 레이어로 전투 UI 상위에 표시)
        if (exploreMapUI == null)
        {
            exploreMapUI = UIManager.instance.OpenUI<ExploreMapUI>(UIConstants.PANEL_EXPLORE_MAP, UILayerType.Popup, false);
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
        BindSpeedToggleButton();

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

    private void BindSpeedToggleButton()
    {
        if (speedToggleButton != null)
        {
            speedToggleButton.onClick.RemoveListener(HandleSpeedToggleClicked);
            speedToggleButton.onClick.AddListener(HandleSpeedToggleClicked);
        }
    }

    private void HandleSpeedToggleClicked()
    {
        BattleManager.CycleGlobalBattleSpeed();
    }

    private void RefreshSpeedToggleVisual(float speed)
    {
        if (this == null)
        {
            BattleManager.OnBattleSpeedChanged -= RefreshSpeedToggleVisual;
            return;
        }

        string speedLabel;
        Color bgColor;
        Color textColor;

        if (Mathf.Approximately(speed, 2.0f) || speed > 1.75f)
        {
            speedLabel = "배속: 2x";
            bgColor = Speed2xBgColor;
            textColor = Speed2xTextColor;
        }
        else if (Mathf.Approximately(speed, 1.5f) || speed > 1.25f)
        {
            speedLabel = "배속: 1.5x";
            bgColor = Speed1_5xBgColor;
            textColor = Speed1_5xTextColor;
        }
        else
        {
            speedLabel = "배속: 1x";
            bgColor = Speed1xBgColor;
            textColor = Speed1xTextColor;
        }

        if (speedToggleText != null)
        {
            speedToggleText.text = speedLabel;
            speedToggleText.color = textColor;
        }

        if (speedToggleBgImage != null)
        {
            speedToggleBgImage.color = bgColor;
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
        if (this == null)
        {
            GameSceneManager.OnSceneLocationChanged -= RefreshLocationAndNavigationState;
            RunManager.OnRunLocationChanged -= RefreshLocationAndNavigationState;
            return;
        }

        bool inDungeon = GameSceneManager.instance != null && GameSceneManager.instance.isInDungeon;

        if (mapButton != null)
        {
            mapButton.interactable = inDungeon;
        }

        RefreshNodeReopenButton();

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

    private void EnsureNodeReopenButtonBound()
    {
        if (nodeReopenButton == null)
        {
            Transform btnTransform = transform.Find("NodeReopenButton");
            if (btnTransform != null)
            {
                nodeReopenButton = btnTransform.GetComponent<Button>();
            }
        }

        if (nodeReopenButton != null && nodeReopenButtonText == null)
        {
            Transform labelTransform = nodeReopenButton.transform.Find("Label");
            if (labelTransform != null)
            {
                nodeReopenButtonText = labelTransform.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                nodeReopenButtonText = nodeReopenButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (nodeReopenButton != null)
        {
            nodeReopenButton.onClick.RemoveListener(HandleNodeReopenClicked);
            nodeReopenButton.onClick.AddListener(HandleNodeReopenClicked);
        }
    }

    public void RefreshNodeReopenButton()
    {
        if (this == null)
        {
            RunManager.OnNodeContextChanged -= RefreshNodeReopenButton;
            return;
        }

        EnsureNodeReopenButtonBound();
        if (nodeReopenButton == null)
        {
            return;
        }

        bool inDungeon = GameSceneManager.instance == null || GameSceneManager.instance.isInDungeon;
        bool inBattle = RunManager.instance != null &&
                        RunManager.instance.currentStage != null &&
                        RunManager.instance.currentStage.currentMapState == MapState.Battle;

        if (!inBattle && inDungeon && RunManager.instance != null &&
            RunManager.instance.HasPendingRewards && !IsRewardListOpen())
        {
            if (!nodeReopenButton.gameObject.activeSelf)
            {
                nodeReopenButton.gameObject.SetActive(true);
            }

            if (nodeReopenButtonText != null)
            {
                nodeReopenButtonText.text = $"보상 확인({RunManager.instance.PendingRewardCount})";
            }
            return;
        }

        if (!inBattle && inDungeon && RunManager.instance != null &&
            RunManager.instance.CanReopenCurrentNodeShop && !IsShopPanelOpen())
        {
            if (!nodeReopenButton.gameObject.activeSelf)
            {
                nodeReopenButton.gameObject.SetActive(true);
            }

            if (nodeReopenButtonText != null)
            {
                nodeReopenButtonText.text = "재입장";
            }
            return;
        }

        if (nodeReopenButton.gameObject.activeSelf)
        {
            nodeReopenButton.gameObject.SetActive(false);
        }
    }

    private static bool IsRewardListOpen()
    {
        if (UIManager.instance == null)
        {
            return false;
        }

        RewardListUIHandler panel = UIManager.instance.GetUI<RewardListUIHandler>(UIConstants.PANEL_REWARD_LIST);
        return panel != null && panel.IsPanelOpen && panel.gameObject.activeInHierarchy;
    }

    private static bool IsShopPanelOpen()
    {
        if (UIManager.instance == null)
        {
            return false;
        }

        ShopPanel panel = UIManager.instance.GetUI<ShopPanel>(UIConstants.PANEL_SHOP_NPC);
        return panel != null && panel.IsPanelOpen && panel.gameObject.activeInHierarchy;
    }

    private void HandleNodeReopenClicked()
    {
        if (RunManager.instance == null)
        {
            return;
        }

        if (RunManager.instance.HasPendingRewards)
        {
            RunManager.instance.OpenPendingRewardUI();
        }
        else if (RunManager.instance.CanReopenCurrentNodeShop)
        {
            RunManager.instance.ReopenCurrentNodeShop();
        }
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

        if (speedToggleButton == null)
        {
            Transform speedTr = hudPanel.Find("SpeedToggleButton");
            if (speedTr == null)
            {
                speedTr = hudPanel.Find("TooltipToggleButton");
            }
            if (speedTr != null)
            {
                speedToggleButton = speedTr.GetComponent<Button>();
            }
        }

        if (speedToggleButton != null)
        {
            if (speedToggleText == null)
            {
                speedToggleText = speedToggleButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (speedToggleBgImage == null)
            {
                speedToggleBgImage = speedToggleButton.GetComponent<Image>();
            }
        }

        BindPauseMenuButton();
        BindSpeedToggleButton();
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
