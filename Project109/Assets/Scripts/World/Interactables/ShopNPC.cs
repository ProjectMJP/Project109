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

        // 1. 카드 중복 방지 저장
        for (int i = 0; i < cardCount; i++)
        {
            CardData card = null;
            int attempts = 0;
            const int maxAttempts = 50;

            while (attempts < maxAttempts)
            {
                card = GameItemRewardManager.instance.GetRandomCardDataByDropTable("Shop_Card_Table");
                if (card == null) break;

                bool isDuplicate = false;
                foreach (var activeCard in actionCards)
                {
                    if (activeCard.cardName == card.cardName)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    break;
                }
                attempts++;
            }

            if (card != null)
            {
                actionCards.Add(card);
            }
        }

        // 2. 유물 중복 방지 저장
        for (int i = 0; i < relicCount; i++)
        {
            RelicData relic = null;
            int attempts = 0;
            const int maxAttempts = 50;

            while (attempts < maxAttempts)
            {
                relic = GameItemRewardManager.instance.GetRandomRelicDataByDropTable("Shop_Relic_Table");
                if (relic == null) break;

                bool isDuplicate = false;
                foreach (var activeRelic in relics)
                {
                    if (activeRelic.relicName == relic.relicName)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    break;
                }
                attempts++;
            }

            if (relic != null)
            {
                relics.Add(relic);
            }
        }
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

        shopUI = spawned.GetComponent<ShopPanel>();
        if (shopUI == null && spawned.transform.childCount > 0)
        {
            shopUI = spawned.transform.GetChild(0).GetComponent<ShopPanel>();
        }

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

            if (RunManager.instance != null && RunManager.instance.currentMap != null)
            {
                if (!RunManager.instance.currentMap.currentSpawnUIList.Contains(shopUI.gameObject))
                {
                    RunManager.instance.currentMap.currentSpawnUIList.Add(shopUI.gameObject);
                }
            }
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
