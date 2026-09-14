using System.Collections.Generic;
using UnityEngine;
using GameItem.Types;

[System.Serializable]
public class RewardItemConfig
{
    public RewardType rewardType;

    [Header("수량 및 수량 범위")]
    public int minValue;
    public int maxValue;

    [Header("드롭테이블 ID 또는 특정 아이템 ID")]
    public string referenceID;
}

[System.Serializable]
public class RewardData
{
    public List<RewardItemConfig> rewards = new List<RewardItemConfig>();
}
