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
                    player.eventBus.Invoke<IOnAddGold>(a => a.OnAddGold(diff));
                else if (diff < 0)
                    player.eventBus.Invoke<IOnRemoveGold>(a => a.OnRemoveGold(-diff));
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
                    player.eventBus.Invoke<IOnAddMemorySharp>(a => a.OnAddMemorySharp(diff));
                else if (diff < 0)
                    player.eventBus.Invoke<IOnRemoveMemorySharp>(a => a.OnRemoveMemorySharp(-diff));
            }
        } 
    }
    public int MapFloorCheckStartLength { get; set; }
    public int MapFloorCheckLength { get; set; }
    public int MapRevealRandomCount { get; set; }
    public int RewardCardCount {  get; set; }
    public int RewardRelicCount { get; set; }
    public int MasteryChoiceCount { get; set; }
    public int UpgradeMasteryPointValue { get; set; }
    public int BaseDrawCount { get; set; } = 5;
}