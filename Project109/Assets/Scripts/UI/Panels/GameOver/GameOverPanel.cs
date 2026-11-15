using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 패배 시 표시되는 게임 오버 및 런 정산 모달 패널입니다.
/// 고정 텍스트(타이틀, 안내문 등)는 프리팹에 정적으로 배치하고,
/// 코드에서는 동적 탐험 통계 데이터 바인딩 및 씬 전환 액션만 전담합니다.
/// </summary>
public class GameOverPanel : UIPanelBase
{
    [Header("Dynamic Statistics")]
    [SerializeField] private TextMeshProUGUI resultStatsText;

    [Header("Action Buttons")]
    [SerializeField] private Button returnToHideoutButton;
    [SerializeField] private Button returnToTitleButton;

    private bool _isActionProcessing = false;

    private void Awake()
    {
        uiLayerType = UILayerType.Popup;
        blockWorldInput = true;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _isActionProcessing = false;

        if (returnToHideoutButton != null)
        {
            returnToHideoutButton.onClick.RemoveAllListeners();
            returnToHideoutButton.onClick.AddListener(OnReturnToHideoutClicked);
        }

        if (returnToTitleButton != null)
        {
            returnToTitleButton.onClick.RemoveAllListeners();
            returnToTitleButton.onClick.AddListener(OnReturnToTitleClicked);
        }
    }

    public override bool CanCloseByCancel => false;
    public override bool DestroyOnClose => true;

    /// <summary>
    /// 동적 탐험 결과(도달 층수, 맵 이름, 골드)만 텍스트로 바인딩합니다.
    /// </summary>
    public void Setup(int currentFloor, string mapName)
    {
        int gold = (GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null)
            ? GameSceneManager.instance.player.playerStat.InGameCurrencyGold
            : 0;

        string mapDisplayName = string.IsNullOrEmpty(mapName) ? "알 수 없는 구역" : mapName;
        int displayFloor = Mathf.Max(1, currentFloor);

        if (resultStatsText != null)
        {
            resultStatsText.text = $"• 도달 층수: {displayFloor}F ({mapDisplayName})\n• 보유 골드: {gold:N0} G";
        }
    }

    private void RestorePlayerCharacterState()
    {
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.character != null)
        {
            Character character = GameSceneManager.instance.player.character;
            character.isDead = false;
            character.currentState = CharacterState.Idle;
            character.curHealth = character.maxHealth;
            character.ResetCharacterForBattle(true);
        }
    }

    private void OnReturnToHideoutClicked()
    {
        if (_isActionProcessing) return;
        _isActionProcessing = true;

        if (returnToHideoutButton != null) returnToHideoutButton.interactable = false;
        if (returnToTitleButton != null) returnToTitleButton.interactable = false;

        // 1. 캐릭터 사망 상태 복구 (체력 최대치 회복 및 전투 상태 클리어)
        RestorePlayerCharacterState();

        // 2. 던전 런 진행도 초기화
        if (RunManager.instance != null)
        {
            RunManager.instance.ResetRunProgress();
        }

        // 3. 팝업 닫기
        Close();

        // 4. 은신처 서브씬으로 복귀 전환
        if (GameSceneManager.instance != null)
        {
            GameSceneManager.instance.TransitionToHideout();
        }
        else
        {
            Debug.LogError("[GameOverPanel] GameSceneManager.instance is null!");
        }
    }

    private void OnReturnToTitleClicked()
    {
        if (_isActionProcessing) return;
        _isActionProcessing = true;

        if (returnToHideoutButton != null) returnToHideoutButton.interactable = false;
        if (returnToTitleButton != null) returnToTitleButton.interactable = false;

        // 1. 캐릭터 사망 상태 복구
        RestorePlayerCharacterState();

        // 2. 던전 런 진행도 초기화
        if (RunManager.instance != null)
        {
            RunManager.instance.ResetRunProgress();
        }

        Close();

        // 2. 타이틀 씬으로 이동
        if (SceneLoadManager.instance != null)
        {
            SceneLoadManager.instance.LoadScene("MainMenu");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }
}
