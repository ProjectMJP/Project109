using UnityEngine;
using System;

[System.Serializable]
public class PlayerStat
{
    public event Action<int> OnGoldChanged;

    private int _inGameCurrencyGold;
    public int InGameCurrencyGold 
    { 
        get => _inGameCurrencyGold; 
        set 
        { 
            _inGameCurrencyGold = value; 
            OnGoldChanged?.Invoke(_inGameCurrencyGold); 
        } 
    }
    public event Action<int> OnMemorySharpChanged;

    private int _inGameCurrencyMemorySharp;
    public int InGameCurrencyMemorySharp 
    { 
        get => _inGameCurrencyMemorySharp; 
        set 
        { 
            _inGameCurrencyMemorySharp = value; 
            OnMemorySharpChanged?.Invoke(_inGameCurrencyMemorySharp); 
        } 
    }
    public int MapFloorCheckStartLength { get; set; }
    public int MapFloorCheckLength { get; set; }
    public int MapRevealRandomCount { get; set; }
    public int RewardCardCount {  get; set; }
    public int RewardRelicCount { get; set; }
    public int MasteryChoiceCount { get; set; }
    public int UpgradeMasteryPointValue { get; set; }
}