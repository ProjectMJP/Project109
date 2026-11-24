using UnityEngine;
using System;

[System.Serializable]
public class PlayerStat
{
    [System.NonSerialized] public Player player;

    public event Action<int> OnGoldChanged;

    private int _inGameCurrencyGold;
    public int InGameCurrencyGold 
    { 
        get => _inGameCurrencyGold; 
        set 
        { 
            int diff = value - _inGameCurrencyGold;
            _inGameCurrencyGold = value; 
            OnGoldChanged?.Invoke(_inGameCurrencyGold); 
            if (player != null && player.eventBus != null)
            {
                if (diff > 0)
                    PlayerEventDispatcher.DispatchAddGold(player.eventBus, diff);
                else if (diff < 0)
                    PlayerEventDispatcher.DispatchRemoveGold(player.eventBus, -diff);
            }
        } 
    }
    public event Action<int> OnMemorySharpChanged;

    private int _inGameCurrencyMemorySharp;
    public int InGameCurrencyMemorySharp 
    { 
        get => _inGameCurrencyMemorySharp; 
        set 
        { 
            int diff = value - _inGameCurrencyMemorySharp;
            _inGameCurrencyMemorySharp = value; 
            OnMemorySharpChanged?.Invoke(_inGameCurrencyMemorySharp); 
            if (player != null && player.eventBus != null)
            {
                if (diff > 0)
                    PlayerEventDispatcher.DispatchAddMemorySharp(player.eventBus, diff);
                else if (diff < 0)
                    PlayerEventDispatcher.DispatchRemoveMemorySharp(player.eventBus, -diff);
            }
        } 
    }
    public int MapFloorCheckStartLength { get; set; }
    public int MapFloorCheckLength { get; set; } = 3;
    public int MapRevealRandomCount { get; set; }
    public int RewardCardCount { get; set; } = 3;
    public int RewardRelicCount { get; set; } = 3;
    public int MasteryChoiceCount { get; set; } = 3;
    public int UpgradeMasteryPointValue { get; set; } = 500;
    public int BaseDrawCount { get; set; } = 5;
    public int BonusShopCardCount { get; set; } = 0;
    public int BonusShopRelicCount { get; set; } = 0;
    public int ShopRerollCount { get; set; } = 0;

    /// <summary>
    /// 전투 중 탐험 지도를 통한 다음 노드 강제 이동을 허용하는 우회 카운트입니다.
    /// </summary>
    public int BattleMapLockBypassCount { get; set; } = 0;

    /// <summary>
    /// 전투 중 탐험 지도 이동 제한을 우회할 수 있는지 여부입니다.
    /// </summary>
    public bool CanBypassBattleMapLock => BattleMapLockBypassCount > 0;

    /// <summary>
    /// 유물 장착 복원 또는 신규 런 초기화 전에 파생 세션 스탯 수치를 표준 기본값으로 리셋합니다.
    /// </summary>
    public void ResetSessionDefaults()
    {
        MapFloorCheckStartLength = 0;
        MapFloorCheckLength = 3;
        MapRevealRandomCount = 0;
        RewardCardCount = 3;
        RewardRelicCount = 3;
        MasteryChoiceCount = 3;
        UpgradeMasteryPointValue = 500;
        BaseDrawCount = 5;
        BonusShopCardCount = 0;
        BonusShopRelicCount = 0;
        ShopRerollCount = 0;
        BattleMapLockBypassCount = 0;
    }
}