using UnityEngine;
using UnityEngine.EventSystems;

public enum BattleUIMode
{
    Hand,
    Move
}

/// <summary>
/// 전투 중 화면 하단에 표시되는 메인 전투 UI 컨테이너입니다.
/// 하위 UI로 BattleHandPanel(손패/덱)과 BattleMovePanel(이동 모드)을 소유하며,
/// 두 모드 간의 토글 전환 및 PlayerBattleController와의 조작 상태 동기화를 총괄합니다.
/// </summary>
public class BattleUIPanel : UIPanelBase
{
    [Header("Sub Panels")]
    [SerializeField] private BattleHandPanel battleHandPanel;
    [SerializeField] private BattleMovePanel battleMovePanel;

    [Header("Turn End")]
    [SerializeField] private UnityEngine.UI.Button endTurnButton;

    [Header("Hotkey Hint Settings")]
    [SerializeField] private bool showHotkeyHints = true;

    [Header("Targeting Preview")]
    [SerializeField] private GameObject targetingPreviewContainer;
    [SerializeField] private CardUI targetingPreviewCardUI;
    [SerializeField] private UnityEngine.UI.Button cancelTargetingButton;

    public BattleHandPanel HandPanel => battleHandPanel;
    public BattleMovePanel MovePanel => battleMovePanel;
    public UnityEngine.UI.Button EndTurnButton => endTurnButton;
    public GameObject TargetingPreviewContainer => targetingPreviewContainer;
    public CardUI TargetingPreviewCardUI => targetingPreviewCardUI;
    public UnityEngine.UI.Button CancelTargetingButton => cancelTargetingButton;
    public bool IsTargetingActive => _isTargetingActive;

    public BattleUIMode currentMode { get; private set; } = BattleUIMode.Hand;

    public override bool DestroyOnClose => false;
    public override bool CanCloseByCancel => false;

    private Player _boundPlayer;
    private PlayerBattleController _controller;
    private Character _boundCharacter;
    private BattlePointerHoverForwarder _endTurnHoverForwarder;
    private TMPro.TextMeshProUGUI _endTurnButtonText;
    private string _baseEndTurnButtonLabel;
    private bool _isTurnActive = false;
    private bool _isTargetingActive = false;

    /// <summary>
    /// 턴 종료 버튼의 포인터 진입/이탈을 감지하여 타임라인 패널에 호버 미리보기를 전달하는 경량 포워더입니다.
    /// </summary>
    private sealed class BattlePointerHoverForwarder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private UnityEngine.UI.Button _button;

        public void Bind(UnityEngine.UI.Button button)
        {
            _button = button;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button != null && _button.interactable && BattleTimelinePanel.Instance != null)
            {
                BattleTimelinePanel.Instance.SetEndTurnHoverPreview(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (BattleTimelinePanel.Instance != null)
            {
                BattleTimelinePanel.Instance.SetEndTurnHoverPreview(false);
            }
        }

        private void OnDisable()
        {
            if (BattleTimelinePanel.Instance != null)
            {
                BattleTimelinePanel.Instance.SetEndTurnHoverPreview(false);
            }
        }
    }

    private void Awake()
    {
        uiLayerType = UILayerType.Top;
        blockWorldInput = false;

        // 프리팹 직렬화 필드 유효성 검증 (Fail-Fast)
        if (battleHandPanel == null)
        {
            Debug.LogError("[BattleUIPanel] battleHandPanel이 바인딩되지 않았습니다! BattleUIPanel 프리팹 설정을 확인하세요.", this);
        }

        if (battleMovePanel == null)
        {
            Debug.LogError("[BattleUIPanel] battleMovePanel이 바인딩되지 않았습니다! BattleUIPanel 프리팹 설정을 확인하세요.", this);
        }

        if (endTurnButton == null)
        {
            Debug.LogError("[BattleUIPanel] endTurnButton이 바인딩되지 않았습니다! BattleUIPanel 프리팹 설정을 확인하세요.", this);
        }
        else
        {
            RefreshEndTurnHotkeyHint();
        }

        if (targetingPreviewContainer != null)
        {
            targetingPreviewContainer.SetActive(false);

            if (targetingPreviewCardUI == null)
            {
                Debug.LogError("[BattleUIPanel] targetingPreviewCardUI가 바인딩되지 않았습니다! BattleUIPanel 프리팹 설정을 확인하세요.", this);
            }

            if (cancelTargetingButton == null)
            {
                Debug.LogError("[BattleUIPanel] cancelTargetingButton이 바인딩되지 않았습니다! BattleUIPanel 프리팹 설정을 확인하세요.", this);
            }
        }
        else
        {
            Debug.LogWarning("[BattleUIPanel] targetingPreviewContainer가 바인딩되지 않았습니다.", this);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (battleHandPanel != null)
        {
            battleHandPanel.OnMoveModeRequested -= SwitchToMoveMode;
            battleHandPanel.OnMoveModeRequested += SwitchToMoveMode;
            battleHandPanel.OnCardModeRequested -= SwitchToHandMode;
            battleHandPanel.OnCardModeRequested += SwitchToHandMode;

            if (_controller != null)
            {
                battleHandPanel.BindBattleController(_controller);
            }
        }

        if (battleMovePanel != null)
        {
            battleMovePanel.OnCardModeRequested -= SwitchToHandMode;
            battleMovePanel.OnCardModeRequested += SwitchToHandMode;
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(HandleEndTurnClicked);

            if (_endTurnHoverForwarder == null)
            {
                _endTurnHoverForwarder = endTurnButton.GetComponent<BattlePointerHoverForwarder>();
                if (_endTurnHoverForwarder == null)
                {
                    _endTurnHoverForwarder = endTurnButton.gameObject.AddComponent<BattlePointerHoverForwarder>();
                }
            }
            _endTurnHoverForwarder.Bind(endTurnButton);
        }

        if (cancelTargetingButton != null)
        {
            cancelTargetingButton.onClick.RemoveAllListeners();
            cancelTargetingButton.onClick.AddListener(HandleCancelTargetingClicked);
        }

        if (_controller != null)
        {
            _controller.OnTargetingStateChanged -= HandleTargetingStateChanged;
            _controller.OnTargetingStateChanged += HandleTargetingStateChanged;
        }

        // 패널 활성화 시 기본 손패 모드로 시작 및 현재 턴·행동 가능 여부에 따라 버튼 상태 동기화
        _isTurnActive = _controller != null && _controller.IsPlayerTurnActive();
        SwitchToHandMode();
        HandleTargetingStateChanged(_controller != null ? _controller.ActiveTargetingCard : null);
        RefreshEndTurnHotkeyHint();
        RefreshActionInteractableState();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (battleHandPanel != null)
        {
            battleHandPanel.OnMoveModeRequested -= SwitchToMoveMode;
            battleHandPanel.OnCardModeRequested -= SwitchToHandMode;
        }

        if (battleMovePanel != null)
        {
            battleMovePanel.OnCardModeRequested -= SwitchToHandMode;
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
        }

        if (cancelTargetingButton != null)
        {
            cancelTargetingButton.onClick.RemoveListener(HandleCancelTargetingClicked);
        }

        if (_controller != null)
        {
            _controller.OnTargetingStateChanged -= HandleTargetingStateChanged;
        }

        HandleTargetingStateChanged(null);

        if (BattleTimelinePanel.Instance != null)
        {
            BattleTimelinePanel.Instance.SetEndTurnHoverPreview(false);
        }
    }

    private void OnDestroy()
    {
        if (_controller != null)
        {
            _controller.OnTargetingStateChanged -= HandleTargetingStateChanged;
        }
    }

    /// <summary>
    /// 턴 종료 버튼의 활성화/비활성화 상태를 설정합니다.
    /// </summary>
    public void SetEndTurnInteractable(bool interactable)
    {
        if (endTurnButton != null)
        {
            endTurnButton.interactable = interactable;
        }

        if (!interactable && BattleTimelinePanel.Instance != null)
        {
            BattleTimelinePanel.Instance.SetEndTurnHoverPreview(false);
        }
    }

    /// <summary>
    /// 플레이어의 턴 활성화 여부에 따라 턴 종료 버튼 및 모드 전환 버튼을 활성화/비활성화하고,
    /// 턴이 끝났을 경우 즉시 카드(손패) 모드로 강제 복구합니다.
    /// </summary>
    public void SetTurnActive(bool isActive)
    {
        _isTurnActive = isActive;

        if (!isActive && _isTargetingActive)
        {
            HandleTargetingStateChanged(null);
        }

        RefreshActionInteractableState();

        if (!isActive)
        {
            SwitchToHandMode();
        }
    }

    private void HandleEndTurnClicked()
    {
        if (_controller != null)
        {
            _controller.RequestEndTurn();
        }
        else if (RunManager.instance != null &&
                 RunManager.instance.battleManager != null &&
                 RunManager.instance.battleManager.battleState == BattleState.TurnInProgress &&
                 RunManager.instance.battleManager.currentTurnController is PlayerBattleController activePlayerController)
        {
            activePlayerController.RequestEndTurn();
        }
    }

    private void HandleCancelTargetingClicked()
    {
        if (_controller != null)
        {
            _controller.CancelCurrentState();
        }
    }

    private void HandleTargetingStateChanged(Card targetingCard)
    {
        _isTargetingActive = targetingCard != null;

        if (targetingPreviewContainer != null)
        {
            targetingPreviewContainer.SetActive(_isTargetingActive);

            if (_isTargetingActive && targetingPreviewCardUI != null)
            {
                targetingPreviewCardUI.UpdateCardInstance(targetingCard);
                ConfigureTargetingPreviewCard(targetingPreviewCardUI);
            }
        }

        if (battleHandPanel != null)
        {
            battleHandPanel.SetTargetingState(targetingCard);
        }

        RefreshActionInteractableState();
    }

    private static void ConfigureTargetingPreviewCard(CardUI previewCardUI)
    {
        if (previewCardUI == null) return;

        previewCardUI.enableZoom = false;
        previewCardUI.usePlayGesture = false;
        previewCardUI.bIsCardHighlight = false;
        previewCardUI.bShowEffectAreaUI = false;
        previewCardUI.OffSelectHighlight();
        previewCardUI.SetInteractable(false);
    }

    /// <summary>
    /// 현재 턴 소유, 타깃팅 여부, 캐릭터 대기 상태 및 액션 큐 실행 상태에 맞춰 전투 액션 버튼 활성화 여부를 동기화합니다.
    /// </summary>
    public void RefreshActionInteractableState()
    {
        bool canAct = _isTurnActive && !_isTargetingActive;
        if (_controller != null)
        {
            canAct = canAct && _controller.CanPerformPlayerTurnAction();
        }

        SetEndTurnInteractable(canAct);

        if (battleHandPanel != null)
        {
            battleHandPanel.SetSwitchModeInteractable(canAct);
        }

        if (battleMovePanel != null)
        {
            battleMovePanel.SetSwitchToCardInteractable(canAct);
        }

        RefreshCardPlayability();
    }

    /// <summary>
    /// 턴 종료 버튼의 단축키 힌트([E]) 표시를 갱신합니다.
    /// </summary>
    public void RefreshEndTurnHotkeyHint()
    {
        if (endTurnButton == null)
        {
            return;
        }

        if (_endTurnButtonText == null)
        {
            _endTurnButtonText = endTurnButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        }

        if (_endTurnButtonText == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_baseEndTurnButtonLabel))
        {
            string raw = _endTurnButtonText.text != null ? _endTurnButtonText.text.Trim() : string.Empty;
            if (raw.EndsWith(" [E]"))
            {
                raw = raw.Substring(0, raw.Length - 4);
            }
            _baseEndTurnButtonLabel = string.IsNullOrEmpty(raw) ? "턴 종료" : raw;
        }

        _endTurnButtonText.text = showHotkeyHints ? $"{_baseEndTurnButtonLabel} [E]" : _baseEndTurnButtonLabel;
    }

    /// <summary>
    /// 손패 패널의 카드별 사용 가능 시각 피드백을 즉시 재평가합니다.
    /// </summary>
    public void RefreshCardPlayability()
    {
        if (battleHandPanel != null)
        {
            battleHandPanel.RefreshAllCardPlayability();
        }
    }

    /// <summary>
    /// 플레이어 인스턴스와 전투 컨트롤러를 바인딩합니다.
    /// </summary>
    public void BindPlayer(Player player, PlayerBattleController controller)
    {
        if (_controller != null)
        {
            _controller.OnTargetingStateChanged -= HandleTargetingStateChanged;
        }

        _boundPlayer = player;
        _controller = controller;
        _boundCharacter = player != null ? player.character : null;
        _isTurnActive = _controller != null && _controller.IsPlayerTurnActive();

        if (_controller != null)
        {
            _controller.OnTargetingStateChanged -= HandleTargetingStateChanged;
            _controller.OnTargetingStateChanged += HandleTargetingStateChanged;
        }

        if (battleHandPanel != null)
        {
            battleHandPanel.BindBattleDeck(player != null ? player.battleDeck : null);
            battleHandPanel.BindBattleController(controller);
        }

        if (battleMovePanel != null && _boundCharacter != null)
        {
            battleMovePanel.RefreshMoveInfo(_boundCharacter);
        }

        HandleTargetingStateChanged(_controller != null ? _controller.ActiveTargetingCard : null);
        RefreshActionInteractableState();
    }

    /// <summary>
    /// 카드(손패) 조작 모드로 전환합니다.
    /// </summary>
    public void SwitchToHandMode()
    {
        currentMode = BattleUIMode.Hand;

        if (battleHandPanel != null)
        {
            battleHandPanel.gameObject.SetActive(true);
            battleHandPanel.SetMoveMode(false);
        }

        if (battleMovePanel != null)
        {
            battleMovePanel.gameObject.SetActive(false);
        }

        // 이동 모드 조작 상태였을 경우 취소
        if (_controller != null && _controller.controlState == PlayerControlState.Move)
        {
            _controller.CancelCurrentState();
        }
    }

    /// <summary>
    /// 이동 모드로 전환합니다. (손패 숨김, 덱 버튼을 MoveCount 표시로 전환, 버튼은 전투 모드 복귀로 변경)
    /// </summary>
    public void SwitchToMoveMode()
    {
        if (_controller != null && !_controller.CanPerformPlayerTurnAction())
        {
            SwitchToHandMode();
            RefreshActionInteractableState();
            return;
        }

        currentMode = BattleUIMode.Move;

        int remainingTiles = _boundCharacter != null ? _boundCharacter.curMoveRange : 0;

        if (battleHandPanel != null)
        {
            battleHandPanel.gameObject.SetActive(true);
            battleHandPanel.SetMoveMode(true, remainingTiles);
        }

        if (battleMovePanel != null)
        {
            battleMovePanel.gameObject.SetActive(false);
        }

        // 플레이어 전투 컨트롤러에 이동 모드 활성화 지시
        if (_controller != null && _controller.controlState != PlayerControlState.Move)
        {
            if (!_controller.EnablePlayerMove())
            {
                SwitchToHandMode();
                RefreshActionInteractableState();
                return;
            }
        }
    }

    /// <summary>
    /// 손패 모드와 이동 모드를 상호 토글합니다.
    /// </summary>
    public void ToggleMode()
    {
        if (currentMode == BattleUIMode.Hand)
        {
            SwitchToMoveMode();
        }
        else
        {
            SwitchToHandMode();
        }
    }

    /// <summary>
    /// 캐릭터의 최신 이동 수치를 덱 카운터 및 이동 패널에 반영합니다.
    /// </summary>
    public void RefreshMoveStats()
    {
        if (_boundCharacter != null)
        {
            if (battleMovePanel != null)
            {
                battleMovePanel.RefreshMoveInfo(_boundCharacter);
            }

            if (battleHandPanel != null)
            {
                battleHandPanel.UpdateMoveRange(_boundCharacter.curMoveRange);
            }
        }
    }
}
