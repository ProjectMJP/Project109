using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Script Execution Order를 통해 CardDeckManager를 이 클래스보다 먼저 실행되도록 변경됨
/// </summary>
public class CardDeckViewPanel : UIPanelBase, IOnAddCard, IOnRemoveCard, IOnCardUpgrade, IOnCardsRefreshed, IOnCardMasteryUpgrade
{
    public Transform contentTransform;

    private Dictionary<int, GameObject> activeCardUIs = new Dictionary<int, GameObject>();

    protected override void OnEnable()
    {
        base.OnEnable();
        if (RunManager.instance != null && RunManager.instance.player != null)
        {
            RunManager.instance.player.deck.RequestAllCardRefresh();
        }
    }

    private void Awake()
    {
        if (RunManager.instance != null && RunManager.instance.player != null)
        {
            RunManager.instance.player.eventBus.Add<IOnAddCard>(this);
            RunManager.instance.player.eventBus.Add<IOnRemoveCard>(this);
            RunManager.instance.player.eventBus.Add<IOnCardUpgrade>(this);
            RunManager.instance.player.eventBus.Add<IOnCardsRefreshed>(this);
            RunManager.instance.player.eventBus.Add<IOnCardMasteryUpgrade>(this);

            gameObject.SetActive(false);

            Debug.Log("Awake is Done!");
        }
        else
        {
            Debug.LogWarning("Player event bus is null!");
        }
    }

    private void OnDestroy()
    {
        if (RunManager.instance != null && RunManager.instance.player != null)
        {
            RunManager.instance.player.eventBus.Remove<IOnAddCard>(this);
            RunManager.instance.player.eventBus.Remove<IOnRemoveCard>(this);
            RunManager.instance.player.eventBus.Remove<IOnCardUpgrade>(this);
            RunManager.instance.player.eventBus.Remove<IOnCardsRefreshed>(this);
            RunManager.instance.player.eventBus.Remove<IOnCardMasteryUpgrade>(this);
        }

        foreach(GameObject uiObject in activeCardUIs.Values)
        {
            if(ObjectPoolManager.instance != null)
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

    public void OnAddCard(Card card)
    {
        HandleCardAdded(card);
    }

    public void OnRemoveCard(Card card)
    {
        HandleCardRemoved(card.runtimeID);
    }

    public void OnCardUpgrade(Card card)
    {
        HandleCardUpgrade(card.runtimeID);
    }

    public void OnCardsRefreshed()
    {
        RefreshAllCardUIs();
    }

    public void OnCardMasteryUpgrade(Card card, string masteryId)
    {
        RefreshAllCardUIs();
    }

    private void HandleCardAdded(Card card)
    {
        if(ObjectPoolManager.instance == null)
        {
            Debug.LogError("ObjectPoolManager is not initialized. Check ObjewctPoolManager In Hierarchy");
            return;
        }

        CardUI cardUI = ObjectPoolManager.instance.GetCardUI(contentTransform).GetComponent<CardUI>();
        
        if(cardUI != null && contentTransform != null)
        {
            cardUI.UpdateCardInstance(card);
            AddCardClickEvent(cardUI);

            activeCardUIs.Add(card.runtimeID, cardUI.gameObject);

            Debug.Log("Add card is success!");
        }
        else
        {
            if (cardUI != null)
            {
                ObjectPoolManager.instance.ReturnCardUI(cardUI.gameObject);
            }
        }

        Debug.Log("HandleCardAdded is end");
    }

    private void HandleCardRemoved(int runtimeID)
    {
        if(activeCardUIs.TryGetValue(runtimeID, out GameObject cardUIObject))
        {
            if (ObjectPoolManager.instance != null)
            {
                ObjectPoolManager.instance.ReturnCardUI(cardUIObject);
            }
            else
            {
                Destroy(cardUIObject);
            }
            activeCardUIs.Remove(runtimeID);      
        }
    }

    private void HandleCardUpgrade(int runtimeID)
    {
        if (activeCardUIs.TryGetValue(runtimeID, out GameObject cardUIObject))
        {
            CardUI cardUI = cardUIObject.GetComponent<CardUI>();
            if(cardUI != null)
            {
                cardUI.UpgradeCard();
            }
        }
    }

    private void RefreshAllCardUIs()
    {
        foreach(GameObject cardUIObject in activeCardUIs.Values)
        {
            if(ObjectPoolManager.instance != null)
            {
                ObjectPoolManager.instance.ReturnCardUI(cardUIObject);
            }
            else
            {
                Destroy(cardUIObject);
            }
        }
        activeCardUIs.Clear();

        if (RunManager.instance != null && RunManager.instance.player != null)
        {
            foreach (Card card in RunManager.instance.player.deck.GetCards())
            {
                HandleCardAdded(card);
            }
        }
    }

    void AddCardClickEvent(CardUI cardUI)
    {
        //이전에 등록했던 클릭 이벤트 제거
        cardUI.OnCardClick.RemoveAllListeners();

        //카드가 눌리면 카드 데이터를 전달과 동시에 함수 실행
        Card cardInstance = cardUI.GetCardInstance();
        cardUI.OnCardClick.AddListener(() => CardCheck(cardInstance));
    }

    void CardCheck(Card card)
    {
        if (card != null)
        {
            bool canUpgrade = card.hasMastery && 
                              (card.currentMasteryXP >= card.maxMasteryXP) &&
                              (card.GetRandomMasteryOption(1).Count > 0);

            if (canUpgrade)
            {
                if (CardMasteryManager.instance != null)
                {
                    CardMasteryManager.instance.ProcessMasteryUpgrade(card);
                }
                else
                {
                    Debug.LogError("[CardDeckViewPanel] CardMasteryManager instance is null!");
                }
            }
            else
            {
                if (_cardDetailHandler == null)
                {
                    CardDetailPanel[] panels = Resources.FindObjectsOfTypeAll<CardDetailPanel>();
                    if (panels != null && panels.Length > 0)
                    {
                        _cardDetailHandler = panels[0];
                    }
                    if (_cardDetailHandler == null && UIManager.instance != null)
                    {
                        GameObject uiObj = UIManager.instance.OpenUI(UIConstants.PANEL_CARD_DETAIL, UILayerType.Normal, false);
                        if (uiObj != null)
                        {
                            _cardDetailHandler = uiObj.GetComponent<CardDetailPanel>();
                        }
                    }
                }
                if (_cardDetailHandler != null)
                {
                    _cardDetailHandler.OnCardCheckUI(card);
                }
                else
                {
                    Debug.LogError("[CardDeckViewPanel] cardDetailHandler is null and could not be resolved.");
                }
            }
        }
    }

    private CardDetailPanel _cardDetailHandler;
}
