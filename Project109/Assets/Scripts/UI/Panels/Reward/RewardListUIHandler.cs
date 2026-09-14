using UnityEngine;
using GameItem.Types;

public class RewardListUIHandler : UIPanelBase
{
    public GameObject rewardItemPrefab;
    public Transform rewardItemSpawnTransform;

    void Start()
    {

    }

    public void AddRewardItem(ItemRewardUIType rewardType, string referenceID, int value)
    {
        ItemRewardUIHandler item = Instantiate(rewardItemPrefab, rewardItemSpawnTransform).GetComponent<ItemRewardUIHandler>();

        if(item != null)
        {
            item.SetReward(rewardType, referenceID, value);
        }
    }
}
