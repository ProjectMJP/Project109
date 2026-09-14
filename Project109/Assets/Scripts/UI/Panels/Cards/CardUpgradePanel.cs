using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardUpgradePanel : UIPanelBase
{
    public CardUpgradeDetailView upgradeCardCheckHandler;
    public CardMasteryUpgradeDetailView upgradeMasteryCardCheckHandler;

    public Transform contentTransform;

    private Dictionary<int, GameObject> activeCardUIs = new Dictionary<int, GameObject>();

    private void Awake()
    {
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            RefreshAllCardUIs();
        }
        else
        {
            Debug.LogWarning("Player is null!");
        }
    }

    private void OnDestroy()
    {
        foreach (GameObject uiObject in activeCardUIs.Values)
        {
            if (ObjectPoolManager.instance != null)
            {
                ObjectPoolManager.instance.ReturnCardUI(uiObject);
            }
            else
            {
                Destroy(uiObject);
            }
        }
        activeCardUIs.Clear();
    }

    private void HandleCardAdded(Card card)
    {
        if (ObjectPoolManager.instance == null)
        {
            Debug.LogError("ObjectPoolManager is not initialized. Check ObjewctPoolManager In Hierarchy");
            return;
        }

        CardUI cardUI = ObjectPoolManager.instance.GetCardUI(contentTransform).GetComponent<CardUI>();

        if (cardUI != null)
        {
            cardUI.UpdateCardInstance(card);
            AddCardClickEvent(cardUI);

            activeCardUIs.Add(card.runtimeID, cardUI.gameObject);
        }
    }

    private void RefreshAllCardUIs()
    {
        foreach (GameObject cardUIObject in activeCardUIs.Values)
        {
            if (ObjectPoolManager.instance != null)
            {
                ObjectPoolManager.instance.ReturnCardUI(cardUIObject);
            }
            else
            {
                Destroy(cardUIObject);
            }
        }
        activeCardUIs.Clear();

        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            HashSet<string> addedMasteryCardNames = new HashSet<string>();
            foreach (Card card in GameSceneManager.instance.player.deck.GetCards())
            {
                if (card.cardData != null && !card.cardData.isUpgraded)
                {
                    bool isMasteryCard = card.cardData.maxMasteryPoint > 0;
                    bool isUpgradable = card.cardData.isUpgradable;

                    if (isMasteryCard || isUpgradable)
                    {
                        if (isMasteryCard)
                        {
                            if (addedMasteryCardNames.Contains(card.cardData.cardName))
                            {
                                continue;
                            }
                            addedMasteryCardNames.Add(card.cardData.cardName);
                        }

                        HandleCardAdded(card);
                    }
                }
            }
        }

        Debug.Log("현재 가진 카드들 등록 완료");
    }

    void AddCardClickEvent(CardUI cardUI)
    {
        //이전에 등록했던 클릭 이벤트 제거
        cardUI.OnCardClick.RemoveAllListeners();

        Debug.Log($"AddCardClickEvent {cardUI.GetCardInstance().cardData.cardName}");

        //카드가 눌리면 카드 데이터를 전달과 동시에 함수 실행
        Card cardInstance = cardUI.GetCardInstance();
        cardUI.OnCardClick.AddListener(() => CheckUpgradeCard(cardInstance));
    }

    void CheckUpgradeCard(Card card)
    {
        if (card == null || card.cardData == null)
        {
            Debug.LogWarning("CheckUpgradeCard: card or cardData is null");
            return;
        }

        if (card.cardData.maxMasteryPoint > 0)
        {
            if (upgradeMasteryCardCheckHandler == null)
            {
                Debug.LogWarning("CheckUpgradeCard: upgradeMasteryCardCheckHandler is null");
                return;
            }
            upgradeMasteryCardCheckHandler.OnCardCheckUI(card);
        }
        else
        {
            if (upgradeCardCheckHandler == null)
            {
                Debug.LogWarning("CheckUpgradeCard: upgradeCardCheckHandler is null");
                return;
            }
            upgradeCardCheckHandler.OnCardCheckUI(card);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive && DialogueManager.Instance.IsDialoguePaused)
        {
            DialogueManager.Instance.ResumeDialogue();
        }
    }
}
