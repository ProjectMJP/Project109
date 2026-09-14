using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TopHUDPanel : UIPanelBase
{
    [Header("Relic UI")]
    [SerializeField] private PlayerRelicUI playerRelicUI;

    public PlayerRelicUI PlayerRelicUI => playerRelicUI;

    private void Awake()
    {
        blockWorldInput = false;

        if (playerRelicUI == null)
        {
            Transform spawnTransform = transform.Find("HUDPanel/Relic/SpawnTransform");
            if (spawnTransform != null)
            {
                playerRelicUI = spawnTransform.gameObject.GetComponent<PlayerRelicUI>();
                if (playerRelicUI == null)
                {
                    playerRelicUI = spawnTransform.gameObject.AddComponent<PlayerRelicUI>();
                }
            }
            else
            {
                Debug.LogWarning("[TopHUDPanel] Relic/SpawnTransform not found in hierarchy!");
            }
        }
    }

    [Header("Currency TMP Texts")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI specialResourceText;

    [Header("Action Buttons")]
    [SerializeField] private Button mapButton;
    [SerializeField] private Button deckButton;

    [Header("UI Texture")]
    [SerializeField] private Image currencyImage;
    [SerializeField] private Image specialResourceImage;
    [SerializeField] private Image mapButtonImage;
    [SerializeField] private Image deckButtonImage;

    private Player _boundPlayer;
    private CardDeckViewPanel _deckViewPanel;

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
                UIManager.instance.RemoveActiveUIFromStack(_deckViewPanel.gameObject);
                _deckViewPanel.gameObject.SetActive(false);
            }
            else
            {
                UIManager.instance.PushActiveUIPanel(_deckViewPanel.gameObject, UILayerType.Normal);
            }
        }
    }

    /// <summary>
    /// 탐색 맵(ExploreMap) 창을 토글합니다.
    /// </summary>
    public void ToggleExploreMap()
    {
        if (RunManager.instance == null || UIManager.instance == null) return;

        ExploreUI exploreUI = RunManager.instance.currentExploreUI;
        if (exploreUI == null)
        {
            GameObject exploreObj = UIManager.instance.OpenUI(UIConstants.PANEL_EXPLORE_MAP, UILayerType.Top, false);
            if (exploreObj != null)
            {
                exploreUI = exploreObj.GetComponent<ExploreUI>();
                if (exploreUI != null)
                {
                    exploreUI.CreateExploreMap(15);
                }
            }
        }

        if (exploreUI != null)
        {
            if (exploreUI.gameObject.activeSelf)
            {
                exploreUI.Close();
            }
            else
            {
                UIManager.instance.PushActiveUIPanel(exploreUI.gameObject, UILayerType.Top);
            }
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

        if (AssetCacheManager.instance != null)
            UpdateUIImage();
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
        if (AssetCacheManager.instance.TryGetTexture("Gold", out Sprite goldTexture))
        {
            currencyImage.sprite = goldTexture;
        }
        else
        {
            Debug.LogWarning("Failed to load Texture_UI_Gold");
        }

        if (AssetCacheManager.instance.TryGetTexture("MemoryShard", out Sprite shardTexture))
        {
            specialResourceImage.sprite = shardTexture;
        }
        else
        {
            Debug.LogWarning("Failed to load Texture_UI_MemoryShard");
        }

        if (AssetCacheManager.instance.TryGetTexture("MapButtonIcon", out Sprite mapButtonIconTexture))
        {
            mapButtonImage.sprite = mapButtonIconTexture;
        }
        else
        {
            Debug.LogWarning("Failed to load Texture_UI_MapButtonIcon");
        }

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

