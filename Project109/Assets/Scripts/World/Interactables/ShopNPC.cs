using GameItem.Types;
using System.Collections.Generic;
using UnityEngine;

public class ShopNPC : InteractableObject
{
    public GameObject shopUICanvasPrefab;
    private ShopPanel shopUI;

    public int cardCount = 6;
    public int relicCount = 3;

    public List<CardData> actionCards = new List<CardData>();
    public List<RelicData> relics = new List<RelicData>();

    private bool isInitialized = false;

    public override void OnInteract()
    {
        if (interactableData != null && !string.IsNullOrEmpty(interactableData.targetDialogueID))
        {
            TriggerDialogue();
        }
        else
        {
            OpenShopDirectly();
        }
    }

    public void InitializeShop()
    {
        if (isInitialized) return;

        AddRandomItems();
        UpdateShopItems();
        isInitialized = true;
    }

    public void AddRandomItems()
    {
        actionCards.Clear();
        relics.Clear();

        if (GameItemRewardManager.instance == null) return;

        // 1. 카드 추첨 (중복 방지)
        actionCards.AddRange(GameItemRewardManager.instance.GetUniqueRandomCards("Shop_Card_Table", cardCount));

        // 2. 유물 추첨 (중복 방지)
        relics.AddRange(GameItemRewardManager.instance.GetUniqueRandomRelics("Shop_Relic_Table", relicCount));
    }

    public void UpdateShopItems() //상점UI 생성 후 아이템 진열
    {
        if (shopUI != null)
        {
            return;
        }

        GameObject spawned = null;
        if (UIManager.instance != null)
        {
            spawned = UIManager.instance.OpenUI("ShopNPCUI", UILayerType.Normal, false);
        }

        if (spawned == null)
        {
            Debug.LogError("[ShopNPC] ShopNPCUI 프리팹을 찾을 수 없습니다.");
            return;
        }

        shopUI = spawned.GetComponentInChildren<ShopPanel>(true);

        if (shopUI != null)
        {
            shopUI.CreateStoreItemCollections(actionCards.Count, relics.Count, 3);
            shopUI.UpdateCardList(actionCards);
            shopUI.UpdateRelicList(relics);
            shopUI.gameObject.SetActive(false);
        }
    }

    public ShopPanel GetShopUI()
    {
        return shopUI;
    }

    public void OpenShopDirectly()
    {
        InitializeShop();

        if (shopUI != null)
        {
            shopUI.Open();
        }
        else
        {
            Debug.LogWarning("[ShopNPC] ShopUI가 로드되지 않았습니다.");
        }
    }

    public void CloseShopUI()
    {
        if (shopUI != null)
        {
            shopUI.Close();
        }
    }
}
