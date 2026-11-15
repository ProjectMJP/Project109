using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopPanel : UIPanelBase
{
    public GameObject cardCollection;
    public GameObject relicCollection;
    public GameObject potionCollection;

    public List<CardUI> cardList;
    public List<RelicUI> relicList;
    public List<GameObject> potionList;

    public Button upgradeCardButton;
    public Button closeUIButton;

    void Start()
    {
        if (upgradeCardButton != null)
        {
            upgradeCardButton.onClick.RemoveAllListeners();
            upgradeCardButton.onClick.AddListener(OpenUpgradeCardUI);
        }
        if (closeUIButton != null)
        {
            closeUIButton.onClick.RemoveAllListeners();
            closeUIButton.onClick.AddListener(CloseUI);
        }
    }

    /// <summary>
    /// 상점에 생성될 카드, 유물, 포션의 갯수를 정해진 수만큼 빈 칸을 생성시킴.
    /// 기본적으로 카드는 최대 6개, 유물은 3개, 포션은 3개임
    /// </summary>
    public void CreateStoreItemCollections(int cardNumber, int relicNumber, int potionNumber)
    {
        if (AssetCacheManager.instance == null)
        {
            Debug.LogError("[ShopPanel] AssetCacheManager is null!");
            return;
        }

        // 1. 카드 생성 (ActionCard)
        if (AssetCacheManager.instance.TryGetUI("ActionCard", out GameObject cardPrefabObj))
        {
            cardNumber = Mathf.Clamp(cardNumber, 0, 6);
            for (int i = 0; i < cardNumber; i++)
            {
                cardList.Add(GameObject.Instantiate(cardPrefabObj, cardCollection.transform).GetComponent<CardUI>());
            }
        }
        else
        {
            Debug.LogError("[ShopPanel] Failed to load ActionCard prefab from AssetCacheManager!");
        }

        // 2. 유물 생성 (Relic)
        if (AssetCacheManager.instance.TryGetUI("Relic", out GameObject relicPrefabObj))
        {
            relicNumber = Mathf.Clamp(relicNumber, 0, 3);
            for (int i = 0; i < relicNumber; i++)
            {
                relicList.Add(GameObject.Instantiate(relicPrefabObj, relicCollection.transform).GetComponent<RelicUI>());
            }
        }
        else
        {
            Debug.LogError("[ShopPanel] Failed to load Relic prefab from AssetCacheManager!");
        }

        // 3. 포션 생성 (Potion)
        if (AssetCacheManager.instance.TryGetUI("Potion", out GameObject potionPrefabObj))
        {
            potionNumber = Mathf.Clamp(potionNumber, 0, 3);
            for (int i = 0; i < potionNumber; i++)
            {
                potionList.Add(GameObject.Instantiate(potionPrefabObj, potionCollection.transform));
            }
        }
        else
        {
            Debug.LogError("[ShopPanel] Failed to load Potion prefab from AssetCacheManager!");
        }
    }

    public static int GetCardPrice(CardData card)
    {
        if (card == null) return 50;
        return card.rarity switch
        {
            GameItem.Types.CardRarity.Common => 50,
            GameItem.Types.CardRarity.Uncommon => 75,
            GameItem.Types.CardRarity.Rare => 120,
            GameItem.Types.CardRarity.Unique => 150,
            _ => 50
        };
    }

    public static int GetRelicPrice(RelicData relic)
    {
        if (relic == null) return 150;
        return relic.rarity switch
        {
            GameItem.Types.RelicRarity.Common => 150,
            GameItem.Types.RelicRarity.Rare => 250,
            GameItem.Types.RelicRarity.Unique => 300,
            GameItem.Types.RelicRarity.Boss => 400,
            _ => 150
        };
    }

    public void UpdateCardList(List<CardData> cardData)
    {
        int cardCount = cardData != null ? cardData.Count : 0;

        for (int i = 0; i < cardList.Count; i++)
        {
            if (cardList[i] == null) continue;

            cardList[i].OnCardClick.RemoveAllListeners();

            if (i < cardCount)
            {
                CardData currentCardData = cardData[i];
                cardList[i].gameObject.SetActive(true);
                cardList[i].UpdateCardData(currentCardData);
                int index = i;
                cardList[i].OnCardClick.AddListener(() => PurchaseCard(currentCardData, index));
            }
            else
            {
                cardList[i].gameObject.SetActive(false);
            }
        }
    }

    public void UpdateRelicList(List<RelicData> relicData)
    {
        int relicCount = relicData != null ? relicData.Count : 0;

        for (int i = 0; i < relicList.Count; i++)
        {
            if (relicList[i] == null) continue;

            relicList[i].OnRelicClick.RemoveAllListeners();

            if (i < relicCount)
            {
                RelicData currentRelicData = relicData[i];
                relicList[i].gameObject.SetActive(true);
                relicList[i].UpdateRelicData(currentRelicData);
                int index = i;
                relicList[i].OnRelicClick.AddListener(() => PurchaseRelic(currentRelicData, index));
            }
            else
            {
                relicList[i].gameObject.SetActive(false);
            }
        }
    }

    void PurchaseCard(CardData cardData, int slotIndex)
    {
        if (cardData == null) return;

        var player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player == null || player.playerStat == null) return;

        int price = GetCardPrice(cardData);
        if (player.playerStat.InGameCurrencyGold < price)
        {
            Debug.LogWarning($"[ShopPanel] 골드가 부족합니다! (필요: {price}, 보유: {player.playerStat.InGameCurrencyGold})");
            return;
        }

        player.playerStat.InGameCurrencyGold -= price;
        player.deck.AddCard(cardData);
        Debug.Log($"[ShopPanel] {cardData.cardName} 카드를 {price}골드에 구매했습니다.");

        if (slotIndex >= 0 && slotIndex < cardList.Count && cardList[slotIndex] != null)
        {
            cardList[slotIndex].OnCardClick.RemoveAllListeners();
            cardList[slotIndex].gameObject.SetActive(false);
        }
    }

    void PurchaseRelic(RelicData relicData, int slotIndex)
    {
        if (relicData == null) return;

        var player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player == null || player.playerStat == null) return;

        int price = GetRelicPrice(relicData);
        if (player.playerStat.InGameCurrencyGold < price)
        {
            Debug.LogWarning($"[ShopPanel] 골드가 부족합니다! (필요: {price}, 보유: {player.playerStat.InGameCurrencyGold})");
            return;
        }

        player.playerStat.InGameCurrencyGold -= price;
        player.AddRelic(relicData.relicName);
        Debug.Log($"[ShopPanel] {relicData.relicName} 유물을 {price}골드에 구매했습니다.");

        if (slotIndex >= 0 && slotIndex < relicList.Count && relicList[slotIndex] != null)
        {
            relicList[slotIndex].OnRelicClick.RemoveAllListeners();
            relicList[slotIndex].gameObject.SetActive(false);
        }
    }

    public void OpenUpgradeCardUI()
    {
        if (UIManager.instance != null)
        {
            UIManager.instance.OpenUI("UpgradeCardDeckCanvas", UILayerType.Normal, true);
        }
        Debug.Log("Card Upgrade is Process!");
    }

    public void CloseUI()
    {
        Close();
    }
}
