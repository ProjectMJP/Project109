using System;
using TMPro;
using UnityEngine;

public enum BattleNoticeType
{
    BattleStart,
    PlayerTurn,
    EnemyTurn,
    TurnEnd,
    Victory,
    Defeat
}

public class UIBattleNotice : UIPanelBase
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI noticeText;
    [SerializeField] private Animator animator;

    [Header("Fallback Settings")]
    [SerializeField] private float defaultDuration = 2f;

    private Action _onComplete;
    private bool _isCompleteTriggered = false;

    /// <summary>
    /// 지정된 알림 유형에 맞춰 텍스트를 변경하고 연출 애니메이션을 실행합니다.
    /// </summary>
    public void ShowNotice(BattleNoticeType noticeType, Action onComplete = null)
    {
        _onComplete = onComplete;
        _isCompleteTriggered = false;

        // 텍스트 설정
        if (noticeText != null)
        {
            noticeText.text = GetNoticeText(noticeType);
        }

        // 애니메이터 설정
        if (animator != null)
        {
            string triggerName = GetTriggerName(noticeType);
            animator.SetTrigger(triggerName);
        }
        else
        {
            // 애니메이터가 없을 경우 딜레이 후 강제 완료
            Invoke(nameof(OnAnimationComplete), defaultDuration);
        }
    }

    public override bool CanCloseByCancel => false;
    public override bool DestroyOnClose => true;

    /// <summary>
    /// 애니메이션 완료 시 호출할 함수 (Animation Event 또는 타이머로 호출)
    /// </summary>
    public void OnAnimationComplete()
    {
        if (_isCompleteTriggered) return;
        _isCompleteTriggered = true;

        _onComplete?.Invoke();
        Close();
    }

    private string GetNoticeText(BattleNoticeType noticeType)
    {
        switch (noticeType)
        {
            case BattleNoticeType.BattleStart:
                return "BATTLE START";
            case BattleNoticeType.PlayerTurn:
                return "PLAYER TURN";
            case BattleNoticeType.EnemyTurn:
                return "ENEMY TURN";
            case BattleNoticeType.TurnEnd:
                return "TURN END";
            case BattleNoticeType.Victory:
                return "VICTORY";
            case BattleNoticeType.Defeat:
                return "DEFEAT";
            default:
                return string.Empty;
        }
    }

    private string GetTriggerName(BattleNoticeType noticeType)
    {
        switch (noticeType)
        {
            case BattleNoticeType.BattleStart:
                return "PlayBattleStart";
            case BattleNoticeType.PlayerTurn:
                return "PlayPlayerTurn";
            case BattleNoticeType.EnemyTurn:
                return "PlayEnemyTurn";
            case BattleNoticeType.TurnEnd:
                return "PlayTurnEnd";
            case BattleNoticeType.Victory:
                return "PlayVictory";
            case BattleNoticeType.Defeat:
                return "PlayDefeat";
            default:
                return "PlayDefault";
        }
    }
}
