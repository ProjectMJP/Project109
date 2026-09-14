using UnityEngine;

public class PlayerUIController : MonoBehaviour
{
    [Header("UI Views")]
    public PlayerRelicUI playerRelicUI;
    public PlayerCurrencyUI playerCurrencyUI;

    private Player _player;

    private void Start()
    {
        // 런 매니저 생성 및 플레이어 생성 대기 후 연결
        StartCoroutine(WaitAndSubscribe());
    }

    private System.Collections.IEnumerator WaitAndSubscribe()
    {
        while (GameSceneManager.instance == null || GameSceneManager.instance.player == null)
        {
            yield return null;
        }

        Init(GameSceneManager.instance.player);
    }

    public void Init(Player player)
    {
        _player = player;

        // 1. 유물 UI 이벤트 연결
        if (playerRelicUI != null && _player.relicManager != null)
        {
            _player.relicManager.OnRelicAddedEvent += playerRelicUI.AddRelicUI;
            _player.relicManager.OnRelicRemovedEvent += playerRelicUI.RemoveRelicUI;
            
            // 기존에 가지고 있던 유물 그리기 (로드 등)
            foreach (var relic in _player.relicManager.GetRelics())
            {
                playerRelicUI.AddRelicUI(relic);
            }
        }

        // 2. 재화 UI 이벤트 연결
        if (playerCurrencyUI != null)
        {
            _player.playerStat.OnGoldChanged += playerCurrencyUI.UpdateGoldText;
            _player.playerStat.OnMemorySharpChanged += playerCurrencyUI.UpdateMemorySharpText;
            
            // 초기 재화 그리기
            playerCurrencyUI.UpdateGoldText(_player.playerStat.InGameCurrencyGold);
            playerCurrencyUI.UpdateMemorySharpText(_player.playerStat.InGameCurrencyMemorySharp);
        }
    }

    private void OnDestroy()
    {
        if (_player != null)
        {
            if (playerRelicUI != null && _player.relicManager != null)
            {
                _player.relicManager.OnRelicAddedEvent -= playerRelicUI.AddRelicUI;
                _player.relicManager.OnRelicRemovedEvent -= playerRelicUI.RemoveRelicUI;
            }

            if (playerCurrencyUI != null)
            {
                _player.playerStat.OnGoldChanged -= playerCurrencyUI.UpdateGoldText;
                _player.playerStat.OnMemorySharpChanged -= playerCurrencyUI.UpdateMemorySharpText;
            }
        }
    }
}
