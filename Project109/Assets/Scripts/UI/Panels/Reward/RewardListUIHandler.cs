using System.Collections.Generic;
using GameItem.Types;
using UnityEngine;
using UnityEngine.UI;

public class RewardListUIHandler : UIPanelBase
{
    public GameObject rewardItemPrefab;
    public Transform rewardItemSpawnTransform;
    [SerializeField] private Button closeButton;

    private readonly List<ItemRewardUIHandler> _spawnedItems = new List<ItemRewardUIHandler>();

    public bool IsPanelOpen { get; private set; }
    public override bool CanCloseByCancel => true;
    public override bool DestroyOnClose => false;

    private void Awake()
    {
        EnsureCloseButtonBound();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        IsPanelOpen = true;
        EnsureCloseButtonBound();
        RefreshFromSession();

        if (RunManager.instance != null)
        {
            RunManager.instance.NotifyNodeContextChanged();
        }
    }

    public override void OnClose()
    {
        IsPanelOpen = false;
        if (TooltipManager.HasInstance)
        {
            TooltipManager.Instance.HideTooltip();
        }
        base.OnClose();

        if (RunManager.instance != null)
        {
            RunManager.instance.NotifyNodeContextChanged();
        }
    }

    public void RefreshFromSession()
    {
        ClearSpawnedItems();

        if (RunManager.instance == null || RunManager.instance.pendingRewardSession == null)
        {
            return;
        }

        List<PendingRewardEntry> entries = RunManager.instance.pendingRewardSession.entries;
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            PendingRewardEntry entry = entries[i];
            if (entry == null) continue;
            AddPendingRewardItem(entry);
        }
    }

    public ItemRewardUIHandler AddPendingRewardItem(PendingRewardEntry entry)
    {
        if (entry == null || rewardItemPrefab == null || rewardItemSpawnTransform == null)
        {
            return null;
        }

        GameObject itemGo = Instantiate(rewardItemPrefab, rewardItemSpawnTransform, false);
        ItemRewardUIHandler item = itemGo != null ? itemGo.GetComponent<ItemRewardUIHandler>() : null;
        if (item == null)
        {
            if (itemGo != null)
            {
                DestroyObjectSafely(itemGo);
            }
            return null;
        }

        item.BindPendingEntry(entry, this);
        _spawnedItems.Add(item);
        return item;
    }

    public void NotifyRewardClaimed()
    {
        RefreshFromSession();

        if (RunManager.instance == null || !RunManager.instance.HasPendingRewards)
        {
            Close();
        }
    }

    public void ClearSpawnedItems()
    {
        for (int i = 0; i < _spawnedItems.Count; i++)
        {
            ItemRewardUIHandler item = _spawnedItems[i];
            if (item != null && item.gameObject != null)
            {
                DestroyObjectSafely(item.gameObject);
            }
        }
        _spawnedItems.Clear();

        if (rewardItemSpawnTransform != null)
        {
            for (int i = rewardItemSpawnTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = rewardItemSpawnTransform.GetChild(i);
                if (child != null && child.gameObject != null)
                {
                    DestroyObjectSafely(child.gameObject);
                }
            }
        }
    }

    private void EnsureCloseButtonBound()
    {
        if (closeButton == null)
        {
            Transform closeTransform = transform.Find("CloseUI Button");
            if (closeTransform != null)
            {
                closeButton = closeTransform.GetComponent<Button>();
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private static void DestroyObjectSafely(GameObject target)
    {
        if (target == null) return;
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
