using UnityEngine;
using System.Collections.Generic;
using GameItem.Types;

public class RewardChest : InteractableObject
{
    public GameObject rewardListUIPrefab;
    private RewardListUIHandler rewardListUI;
    private RewardData activeRewardData;

    public override void SetInteractableData(InteractableData data)
    {
        base.SetInteractableData(data);
        if (data != null)
        {
            activeRewardData = data.rewardData;
        }
    }

    public override void OnInteract()
    {
        if (interactableData != null && !string.IsNullOrEmpty(interactableData.targetDialogueID))
        {
            TriggerDialogue();
        }
        else
        {
            OpenChestDirectly();
        }
    }

    public void SetRewardData(RewardData data)
    {
        activeRewardData = data;
        SetupRewardUI();
    }

    public void SetRewardItemList()
    {
        SetupRewardUI();
    }

    private void SetupRewardUI()
    {
        if (rewardListUI != null) return; // 중복 생성 방지

        GameObject spawned = null;
        if (UIManager.instance != null)
        {
            spawned = UIManager.instance.OpenUI("RewardListCanvas", UILayerType.Normal, false);
        }

        if (spawned == null)
        {
            if (rewardListUIPrefab != null)
            {
                spawned = Instantiate(rewardListUIPrefab);
            }
            else
            {
                Debug.LogError("[RewardChest] RewardListCanvas 프리팹을 찾을 수 없습니다.");
                return;
            }
        }

        rewardListUI = spawned.GetComponent<RewardListUIHandler>();
        if (rewardListUI == null && spawned.transform.childCount > 0)
        {
            rewardListUI = spawned.transform.GetChild(0).GetComponent<RewardListUIHandler>();
        }

        if (rewardListUI != null && activeRewardData != null)
        {
            foreach (var rewardConfig in activeRewardData.rewards)
            {
                int value = 0;
                if (rewardConfig.minValue > 0 || rewardConfig.maxValue > 0)
                {
                    value = Random.Range(rewardConfig.minValue, rewardConfig.maxValue + 1);
                }

                // 각 보상 타입에 따른 UI 연동
                switch (rewardConfig.rewardType)
                {
                    case RewardType.Gold:
                        if (RunManager.instance != null && RunManager.instance.battleManager != null)
                        {
                            value += RunManager.instance.battleManager.accumulativeGoldReward;
                            RunManager.instance.battleManager.accumulativeGoldReward = 0; // 중복 합산 방지
                        }
                        if (value > 0)
                        {
                            rewardListUI.AddRewardItem(ItemRewardUIType.Gold, rewardConfig.referenceID, value);
                        }
                        break;

                    case RewardType.MemorySharp:
                        if (value > 0)
                        {
                            rewardListUI.AddRewardItem(ItemRewardUIType.MemorySharp, rewardConfig.referenceID, value);
                        }
                        break;

                    case RewardType.CardChoice:
                        // value는 선택지 카드의 개수
                        rewardListUI.AddRewardItem(ItemRewardUIType.Card, rewardConfig.referenceID, value);
                        break;

                    case RewardType.Relic:
                        // value는 획득할 유물의 개수
                        for (int i = 0; i < value; i++)
                        {
                            rewardListUI.AddRewardItem(ItemRewardUIType.Relic, rewardConfig.referenceID, 1);
                        }
                        break;
                }
            }
        }
        else if (rewardListUI != null && activeRewardData == null && RunManager.instance != null && RunManager.instance.currentIncountNode != null)
        {
            // Fallback: 기존 하드코딩 전투 노드 보상 정보
            int totalGold = 0;
            int baseMemorySharp = 0;
            int baseCardCount = 0;
            int baseRelicCount = 0;

            switch (RunManager.instance.currentIncountNode.incountType)
            {
                case IncountType.Battle:
                    totalGold = 60;
                    baseMemorySharp = 2;
                    baseCardCount = 1;
                    if (RunManager.instance.currentIncountNode.battleExtraRewardType == BattleExtraRewardType.Card)
                    {
                        rewardListUI.AddRewardItem(ItemRewardUIType.Card, "Rare_Card_Table", 1);
                    }
                    else if (RunManager.instance.currentIncountNode.battleExtraRewardType == BattleExtraRewardType.Relic)
                    {
                        rewardListUI.AddRewardItem(ItemRewardUIType.Relic, "Default_Relic_Table", 1);
                    }
                    else if (RunManager.instance.currentIncountNode.battleExtraRewardType == BattleExtraRewardType.Gold)
                    {
                        totalGold += UnityEngine.Random.Range(100, 151);
                    }
                    break;
                case IncountType.Elite:
                    totalGold = 80;
                    baseMemorySharp = 5;
                    baseCardCount = 1;
                    baseRelicCount = 1;
                    break;
                case IncountType.Boss:
                    totalGold = 150;
                    baseMemorySharp = 10;
                    baseCardCount = 1;
                    break;
            }

            if (RunManager.instance.battleManager != null)
            {
                totalGold += RunManager.instance.battleManager.accumulativeGoldReward;
                RunManager.instance.battleManager.accumulativeGoldReward = 0;
            }

            if (totalGold > 0)
                rewardListUI.AddRewardItem(ItemRewardUIType.Gold, "", totalGold);
            if (baseMemorySharp > 0)
                rewardListUI.AddRewardItem(ItemRewardUIType.MemorySharp, "", baseMemorySharp);
            if (baseCardCount > 0)
                rewardListUI.AddRewardItem(ItemRewardUIType.Card, "Default_Card_Table", baseCardCount);
            if (baseRelicCount > 0)
                rewardListUI.AddRewardItem(ItemRewardUIType.Relic, "Default_Relic_Table", baseRelicCount);
        }

        if (rewardListUI != null)
        {
            rewardListUI.gameObject.SetActive(false);
        }
    }

    public GameObject GetRewardListUI()
    {
        if (rewardListUI == null) SetupRewardUI();
        return rewardListUI != null ? rewardListUI.gameObject : null;
    }

    public void OpenChestDirectly()
    {
        if (rewardListUI == null) SetupRewardUI();
        if (rewardListUI != null)
        {
            rewardListUI.Open();

            if (RunManager.instance != null && RunManager.instance.currentMap != null)
            {
                if (!RunManager.instance.currentMap.currentSpawnUIList.Contains(rewardListUI.gameObject))
                {
                    RunManager.instance.currentMap.currentSpawnUIList.Add(rewardListUI.gameObject);
                }
            }
        }
    }

    public void CloseChestUI()
    {
        if (rewardListUI != null)
        {
            rewardListUI.Close();
        }
    }
}
