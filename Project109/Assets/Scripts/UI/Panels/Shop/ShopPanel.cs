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
        upgradeCardButton.onClick.AddListener(OpenUpgradeCardUI);   //버튼 등록
        closeUIButton.onClick.AddListener(CloseUI);     //버튼 등록
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

    public void UpdateCardList(List<CardData> cardData)
    {
        int cardCount = Mathf.Clamp(cardData.Count, 0, cardList.Count); //상점의 카드 수만큼 데이터를 불러와 등록

        for (int i = 0; i < cardCount; i++)
        {
            CardData currentCardData = cardData[i];
            cardList[i].UpdateCardData(currentCardData);
            cardList[i].OnCardClick.AddListener(() => PurchaseCard(currentCardData));
        }
    }

    public void UpdateRelicList(List<RelicData> relicData)
    {
        int relicCount = Mathf.Clamp(relicData.Count, 0, relicList.Count); //상점의 유물 수만큼 데이터를 불러와 등록

        for (int i = 0; i < relicCount; i++)
        {
            RelicData currentRelicData = relicData[i];
            relicList[i].UpdateRelicData(currentRelicData);
            relicList[i].OnRelicClick.AddListener(() => PurchaseRelic(currentRelicData));
        }
    }

    void PurchaseCard(CardData cardData)
    {
        Debug.Log($"{cardData.cardName} 행동 카드를 구매합니다.");

        //덱에 카드 추가하는 로직 구현
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            GameSceneManager.instance.player.deck.AddCard(cardData);
        }
    }

    void PurchaseRelic(RelicData relicData)
    {
        Debug.Log($"{relicData.relicName} 유물을 구매합니다.");

        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            GameSceneManager.instance.player.AddRelic(relicData.relicName);
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
