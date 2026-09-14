using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class EraseCardDeckPanel : UIPanelBase
{
    public Transform contentTransform;

    private int eraseCardCount;
    public List<int> eraseCardIDs;

    public Button eraseCardButton;

    private Dictionary<int, GameObject> activeCardUIs = new Dictionary<int, GameObject>();

    private Player GetPlayer() => GameSceneManager.instance != null ? GameSceneManager.instance.player : null;

    private void Awake()
    {
        if (GetPlayer() != null)
        {
            Open();
            RefreshAllCardUIs();
            eraseCardButton.onClick.AddListener(StartEraseCards);
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

        if (cardUI != null && contentTransform != null)
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

        var p = GetPlayer();
        if (p?.deck != null)
        {
            foreach (Card card in p.deck.GetCards())
            {
                HandleCardAdded(card);
            }
        }
    }

    public void SetEraseCardCount(int count)
    {
        eraseCardCount = count;
    }

    void AddCardClickEvent(CardUI cardUI)
    {
        //이전에 등록했던 클릭 이벤트 제거
        cardUI.OnCardClick.RemoveAllListeners();

        //카드가 눌리면 카드 데이터를 전달과 동시에 함수 실행
        cardUI.OnCardClick.AddListener(() => AddEraseCard(cardUI));
    }

    void AddEraseCard(CardUI newCardUI)
    {
        int cardRuntimeID = newCardUI.GetCardInstance().runtimeID;

        //이미 선택된 카드가 한번 더 선택되었을 경우 지울 카드 리스트에서 제거
        if (eraseCardIDs.Contains(cardRuntimeID))
        {
            eraseCardIDs.Remove(cardRuntimeID);
            newCardUI.bIsCardHighlight = true;
            newCardUI.OffSelectHighlight(); //선택이 해제되었음을 알리기 위한 하이라이트 비활성화
        }
        else if(eraseCardIDs.Count < eraseCardCount)
        {
            eraseCardIDs.Add(cardRuntimeID);   //지울 카드를 리스트에 저장
            newCardUI.bIsCardHighlight = false;
            newCardUI.OnSelectHighlight(); //선택됬음을 알리기 위한 하이라이트 활성화
        }
    }

    public void StartEraseCards()
    {
        var p = GetPlayer();
        if (eraseCardIDs.Count == eraseCardCount && p?.deck != null) 
        {
            Debug.Log($"선택된 카드를 제거합니다.");
            foreach (int cardID in eraseCardIDs)
            {
                p.deck.RemoveCard(cardID);
            }

            //카드 제거 후 UI 제거
            Close();
        }
        else
        {
            Debug.LogWarning($"{eraseCardCount}만큼 카드를 선택해야 합니다.");
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
