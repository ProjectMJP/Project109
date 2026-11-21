using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardRewardPanel : UIPanelBase
{
    [SerializeField] private List<CardUI> cardList = new List<CardUI>();

    public GameObject cardObjectPrefab;
    [SerializeField] private Transform cardSpawnTransform;
    [SerializeField] private Button closeButton;

    private PendingRewardEntry _boundEntry;
    private RewardListUIHandler _ownerRewardListPanel;

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
        EnsureCloseButtonBound();
    }

    public override void OnClose()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
        base.OnClose();
    }

    public void BindPendingEntry(PendingRewardEntry entry, RewardListUIHandler ownerRewardListPanel = null)
    {
        _boundEntry = entry;
        _ownerRewardListPanel = ownerRewardListPanel;
        ClearSpawnedCards();

        if (GameItemRewardManager.instance == null || cardObjectPrefab == null || cardSpawnTransform == null)
        {
            return;
        }

        int fallbackCount = RunManager.instance != null ? RunManager.instance.GetDefaultCardChoiceCount() : 3;
        List<CardData> selectedCards = GameItemRewardManager.instance.ResolveOrRollCardsForEntry(entry, fallbackCount);
        SpawnCardOptions(selectedCards);
    }

    public void SettingCards(string dropTableID, int rewardCardCount)
    {
        ClearSpawnedCards();
        if (GameItemRewardManager.instance == null || cardObjectPrefab == null || cardSpawnTransform == null)
        {
            return;
        }

        List<CardData> selectedCards = GameItemRewardManager.instance.GetUniqueRandomCards(dropTableID, rewardCardCount);
        SpawnCardOptions(selectedCards);
    }

    private void SpawnCardOptions(List<CardData> selectedCards)
    {
        if (selectedCards == null) return;

        for (int i = 0; i < selectedCards.Count; i++)
        {
            CardData cardData = selectedCards[i];
            if (cardData == null) continue;

            GameObject cardGo = Instantiate(cardObjectPrefab, cardSpawnTransform, false);
            CardUI card = cardGo != null ? cardGo.GetComponent<CardUI>() : null;
            if (card == null)
            {
                if (cardGo != null)
                {
                    DestroyObjectSafely(cardGo);
                }
                continue;
            }

            card.UpdateCardData(cardData);
            card.bShowEffectAreaUI = true;
            card.OnCardClick.RemoveAllListeners();
            card.OnCardClick.AddListener(() => SelectCard(cardData));
            cardList.Add(card);
        }
    }

    private void SelectCard(CardData newCardData)
    {
        if (newCardData == null) return;

        bool claimed = false;
        if (RunManager.instance != null && _boundEntry != null)
        {
            claimed = RunManager.instance.TryClaimPendingReward(_boundEntry.entryId, newCardData);
        }
        else if (GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.deck != null)
        {
            GameSceneManager.instance.player.deck.AddCard(newCardData);
            claimed = true;
        }

        RewardListUIHandler ownerPanel = _ownerRewardListPanel;
        _boundEntry = null;
        _ownerRewardListPanel = null;

        Close();

        if (claimed && ownerPanel != null && ownerPanel.IsPanelOpen)
        {
            ownerPanel.NotifyRewardClaimed();
        }
    }

    public void ClearSpawnedCards()
    {
        if (cardList != null)
        {
            for (int i = 0; i < cardList.Count; i++)
            {
                CardUI card = cardList[i];
                if (card != null && card.gameObject != null)
                {
                    DestroyObjectSafely(card.gameObject);
                }
            }
            cardList.Clear();
        }

        if (cardSpawnTransform != null)
        {
            for (int i = cardSpawnTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = cardSpawnTransform.GetChild(i);
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
