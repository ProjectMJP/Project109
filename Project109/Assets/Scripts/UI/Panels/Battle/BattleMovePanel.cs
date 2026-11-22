using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 중 이동 모드 시 남은 이동 횟수 및 1회 최대 이동 가능 거리를 표시하고,
/// 카드(손패) 모드로 복귀할 수 있는 토글 UI 패널입니다.
/// </summary>
public class BattleMovePanel : UIPanelBase
{
    [Header("Move Info Displays")]
    [SerializeField] private TextMeshProUGUI moveCountText;
    [SerializeField] private TextMeshProUGUI maxTilesText;

    [Header("Mode Switch Button")]
    [SerializeField] private Button switchToCardModeButton;

    public event Action OnCardModeRequested;

    private void Awake()
    {
        uiLayerType = UILayerType.Top;
        blockWorldInput = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (switchToCardModeButton != null)
        {
            switchToCardModeButton.onClick.RemoveAllListeners();
            switchToCardModeButton.onClick.AddListener(HandleSwitchToCardMode);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (switchToCardModeButton != null)
        {
            switchToCardModeButton.onClick.RemoveListener(HandleSwitchToCardMode);
        }
    }

    private void HandleSwitchToCardMode()
    {
        OnCardModeRequested?.Invoke();
    }

    /// <summary>
    /// 카드 모드로 복귀 버튼의 상호작용 가능 여부를 설정합니다.
    /// </summary>
    public void SetSwitchToCardInteractable(bool interactable)
    {
        if (switchToCardModeButton != null)
        {
            switchToCardModeButton.interactable = interactable;
        }
    }

    /// <summary>
    /// 캐릭터의 현재 남은 이동 가능 칸수 및 최대 이동 거리를 받아 텍스트를 갱신합니다.
    /// </summary>
    public void RefreshMoveInfo(Character character)
    {
        if (character == null) return;

        int remainingTiles = character.curMoveRange;
        int maxTiles = (character.curCharacterStat != null)
            ? character.curCharacterStat.moveRange
            : character.curMoveRange;

        RefreshMoveInfo(remainingTiles, maxTiles);
    }

    /// <summary>
    /// 남은 이동 칸수와 최대 타일 수를 직접 전달받아 UI를 갱신합니다.
    /// </summary>
    public void RefreshMoveInfo(int remainingTiles, int maxTiles)
    {
        if (moveCountText != null)
        {
            moveCountText.text = remainingTiles.ToString();
        }

        if (maxTilesText != null)
        {
            maxTilesText.text = maxTiles.ToString();
        }
    }
}
