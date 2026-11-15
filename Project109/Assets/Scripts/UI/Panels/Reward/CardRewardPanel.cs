using GameItem.Types;
using System.Collections.Generic;
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
        if (GameItemRewardManager.instance == null) return;

        List<CardData> selectedCards = GameItemRewardManager.instance.GetUniqueRandomCards(dropTableID, rewardCardCount);

        for (int i = 0; i < selectedCards.Count; i++)
        {
            CardData cardData = selectedCards[i];
            CardUI card = Instantiate(cardObjectPrefab, cardSpawnTransform).GetComponent<CardUI>();
            if (card == null) continue;

            card.UpdateCardData(cardData);
            card.bShowEffectAreaUI = true;
            card.OnCardClick.AddListener(() => GetCard(cardData));
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
