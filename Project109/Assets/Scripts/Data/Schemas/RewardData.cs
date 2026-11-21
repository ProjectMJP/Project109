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

[System.Serializable]
public class PendingRewardEntry
{
    public string entryId;
    public RewardType rewardType;
    public string referenceID;
    public int amount = 1;
    public List<string> rolledCardNames = new List<string>();
    public string rolledRelicName;

    public PendingRewardEntry Clone()
    {
        PendingRewardEntry copy = new PendingRewardEntry();
        copy.entryId = entryId;
        copy.rewardType = rewardType;
        copy.referenceID = referenceID;
        copy.amount = amount;
        if (rolledCardNames != null)
        {
            copy.rolledCardNames = new List<string>(rolledCardNames);
        }
        copy.rolledRelicName = rolledRelicName;
        return copy;
    }
}

[System.Serializable]
public class PendingRewardSession
{
    public List<PendingRewardEntry> entries = new List<PendingRewardEntry>();

    public int Count => entries != null ? entries.Count : 0;
    public bool HasPendingRewards => entries != null && entries.Count > 0;

    public void Clear()
    {
        if (entries == null)
        {
            entries = new List<PendingRewardEntry>();
            return;
        }
        entries.Clear();
    }

    public void AddGoldReward(int amount)
    {
        if (amount <= 0) return;
        EnsureList();

        for (int i = 0; i < entries.Count; i++)
        {
            PendingRewardEntry existing = entries[i];
            if (existing != null && existing.rewardType == RewardType.Gold)
            {
                existing.amount += amount;
                return;
            }
        }

        AddEntry(RewardType.Gold, string.Empty, amount);
    }

    public void AddMemoryShardReward(int amount)
    {
        if (amount <= 0) return;
        EnsureList();

        for (int i = 0; i < entries.Count; i++)
        {
            PendingRewardEntry existing = entries[i];
            if (existing != null && existing.rewardType == RewardType.MemorySharp)
            {
                existing.amount += amount;
                return;
            }
        }

        AddEntry(RewardType.MemorySharp, string.Empty, amount);
    }

    public PendingRewardEntry AddCardChoiceReward(string dropTableID, int optionCount = 3)
    {
        int clampedCount = optionCount > 0 ? optionCount : 3;
        string tableId = string.IsNullOrEmpty(dropTableID) ? "Default_Card_Table" : dropTableID;
        return AddEntry(RewardType.CardChoice, tableId, clampedCount);
    }

    public PendingRewardEntry AddRelicReward(string dropTableID)
    {
        string tableId = string.IsNullOrEmpty(dropTableID) ? "Default_Relic_Table" : dropTableID;
        return AddEntry(RewardType.Relic, tableId, 1);
    }

    public PendingRewardEntry AddEntry(RewardType rewardType, string referenceID, int amount)
    {
        EnsureList();
        PendingRewardEntry entry = new PendingRewardEntry();
        entry.entryId = System.Guid.NewGuid().ToString("N");
        entry.rewardType = rewardType == RewardType.Card ? RewardType.CardChoice : rewardType;
        entry.referenceID = referenceID ?? string.Empty;
        entry.amount = amount > 0 ? amount : 1;
        entries.Add(entry);
        return entry;
    }

    public PendingRewardEntry FindEntry(string entryId)
    {
        if (entries == null || string.IsNullOrEmpty(entryId)) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            PendingRewardEntry entry = entries[i];
            if (entry != null && entry.entryId == entryId)
            {
                return entry;
            }
        }
        return null;
    }

    public bool RemoveEntry(string entryId)
    {
        if (entries == null || string.IsNullOrEmpty(entryId)) return false;
        for (int i = 0; i < entries.Count; i++)
        {
            PendingRewardEntry entry = entries[i];
            if (entry != null && entry.entryId == entryId)
            {
                entries.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public List<PendingRewardEntry> ExportEntriesCopy()
    {
        List<PendingRewardEntry> copy = new List<PendingRewardEntry>();
        if (entries == null) return copy;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
            {
                copy.Add(entries[i].Clone());
            }
        }
        return copy;
    }

    public void ReplaceEntries(List<PendingRewardEntry> sourceEntries)
    {
        EnsureList();
        entries.Clear();
        if (sourceEntries == null) return;
        for (int i = 0; i < sourceEntries.Count; i++)
        {
            PendingRewardEntry item = sourceEntries[i];
            if (item != null)
            {
                PendingRewardEntry copy = item.Clone();
                if (string.IsNullOrEmpty(copy.entryId))
                {
                    copy.entryId = System.Guid.NewGuid().ToString("N");
                }
                entries.Add(copy);
            }
        }
    }

    private void EnsureList()
    {
        if (entries == null)
        {
            entries = new List<PendingRewardEntry>();
        }
    }
}
