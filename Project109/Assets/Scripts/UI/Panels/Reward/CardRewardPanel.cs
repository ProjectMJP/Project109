using GameItem.Types;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CardRewardPanel : UIPanelBase
{
    [SerializeField] private List<CardUI> cardList;

    public GameObject rootObject;
    public GameObject cardObjectPrefab;
    [SerializeField] private Transform cardSpawnTransform;

    void Start()
    {

    }

    public void SettingCards(string dropTableID, int rewardCardCount)
    {
        GameItemRewardManager.instance.ResetCardLists();

        List<CardData> selectedCards = new List<CardData>();

        for (int count = 0; count < rewardCardCount; count++)
        {
            CardUI card = Instantiate(cardObjectPrefab, cardSpawnTransform).GetComponent<CardUI>();

            if (card == null)
                continue;

            CardData cardData = null;
            int attempts = 0;
            const int maxAttempts = 50;

            while (attempts < maxAttempts)
            {
                cardData = GameItemRewardManager.instance.GetRandomCardDataByDropTable(dropTableID);
                if (cardData == null) break;

                bool isDuplicate = false;
                foreach (var selectedCard in selectedCards)
                {
                    if (selectedCard.cardName == cardData.cardName)
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

            if (cardData != null)
            {
                selectedCards.Add(cardData);
                card.UpdateCardData(cardData);
                card.bShowEffectAreaUI = true;
                card.OnCardClick.AddListener(() => GetCard(cardData));
            }
            else
            {
                Destroy(card.gameObject);
            }
        }
    }


    public override bool CanCloseByCancel => false;
    public override bool DestroyOnClose => true;

    void GetCard(CardData newCardData)
    {
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            GameSceneManager.instance.player.deck.AddCard(newCardData);
        }

        Close();

        //이 카드 선택지를 제공한 NPC오브젝트 제거 및 캔버스 제거
        if (rootObject != null)
        {
            Destroy(rootObject);
        }
    }
}
