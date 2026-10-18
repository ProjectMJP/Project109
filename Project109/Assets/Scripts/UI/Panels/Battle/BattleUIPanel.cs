using UnityEngine;

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

    public BattleHandPanel HandPanel => battleHandPanel;
    public BattleMovePanel MovePanel => battleMovePanel;
    public UnityEngine.UI.Button EndTurnButton => endTurnButton;

    public BattleUIMode currentMode { get; private set; } = BattleUIMode.Hand;

    private Player _boundPlayer;
    private PlayerBattleController _controller;
    private Character _boundCharacter;

    private void Awake()
    {
        uiLayerType = UILayerType.Top;
        blockWorldInput = false;

        // 직렬화 필드가 비어있을 경우 하위 자식 계층에서 자동 탐색 (폴백)
        if (battleHandPanel == null)
        {
            battleHandPanel = GetComponentInChildren<BattleHandPanel>(true);
        }

        if (battleMovePanel == null)
        {
            battleMovePanel = GetComponentInChildren<BattleMovePanel>(true);
        }

        if (endTurnButton == null)
        {
            Transform btnTrans = transform.Find("EndTurnButton");
            if (btnTrans != null)
            {
                endTurnButton = btnTrans.GetComponent<UnityEngine.UI.Button>();
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (battleHandPanel != null)
        {
            battleHandPanel.OnMoveModeRequested -= SwitchToMoveMode;
            battleHandPanel.OnMoveModeRequested += SwitchToMoveMode;
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
        }

        // 패널 활성화 시 기본 손패 모드로 시작
        SwitchToHandMode();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (battleHandPanel != null)
        {
            battleHandPanel.OnMoveModeRequested -= SwitchToMoveMode;
        }

        if (battleMovePanel != null)
        {
            battleMovePanel.OnCardModeRequested -= SwitchToHandMode;
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
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
    }

    private void HandleEndTurnClicked()
    {
        if (_controller != null)
        {
            _controller.RequestEndTurn();
        }
        else if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            RunManager.instance.battleManager.EndTurn();
        }
    }

    /// <summary>
    /// 플레이어 인스턴스와 전투 컨트롤러를 바인딩합니다.
    /// </summary>
    public void BindPlayer(Player player, PlayerBattleController controller)
    {
        _boundPlayer = player;
        _controller = controller;
        _boundCharacter = player != null ? player.character : null;

        if (battleHandPanel != null && player != null)
        {
            battleHandPanel.BindBattleDeck(player.battleDeck);
        }

        if (battleMovePanel != null && _boundCharacter != null)
        {
            battleMovePanel.RefreshMoveInfo(_boundCharacter);
        }
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
    /// 이동 모드로 전환합니다.
    /// </summary>
    public void SwitchToMoveMode()
    {
        currentMode = BattleUIMode.Move;

        if (battleHandPanel != null)
        {
            battleHandPanel.gameObject.SetActive(false);
        }

        if (battleMovePanel != null)
        {
            battleMovePanel.gameObject.SetActive(true);
            if (_boundCharacter != null)
            {
                battleMovePanel.RefreshMoveInfo(_boundCharacter);
            }
        }

        // 플레이어 전투 컨트롤러에 이동 모드 활성화 지시
        if (_controller != null && _controller.controlState != PlayerControlState.Move)
        {
            _controller.EnablePlayerMove();
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
    /// 캐릭터의 최신 이동 수치를 이동 패널에 반영합니다.
    /// </summary>
    public void RefreshMoveStats()
    {
        if (battleMovePanel != null && _boundCharacter != null)
        {
            battleMovePanel.RefreshMoveInfo(_boundCharacter);
        }
    }
}
