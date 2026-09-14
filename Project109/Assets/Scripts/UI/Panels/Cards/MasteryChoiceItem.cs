using GameItem.Types;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MasteryChoiceItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Card selectedCard;
    private string selectedMasteryPath;

    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private GameObject highlightImage;

    void Start()
    {

    }

    public void SetChoice(Card newCard, string masteryId)
    {
        selectedCard = newCard;
        selectedMasteryPath = masteryId;

        string displayName = masteryId;
        if (newCard != null && newCard.cardData != null)
        {
            if (newCard.cardData.masteryNames != null && newCard.cardData.masteryNames.TryGetValue(masteryId, out string nameValue))
            {
                displayName = nameValue;
            }
        }
        nameText.SetText(displayName);
        descriptionText.SetText(displayName);
    }

    public void StartMasteryUpgradeCard()
    {
        // 직접 호출 대신 PlayerDeck.ApplyMastery()를 경유해야 IOnCardMasteryUpgrade 이벤트가 발행됩니다.
        if (GameSceneManager.instance?.player?.deck != null)
        {
            GameSceneManager.instance.player.deck.ApplyMastery(selectedCard, selectedMasteryPath);
        }

        CardMasteryUpgradePanel parentPanel = GetComponentInParent<CardMasteryUpgradePanel>();
        if (parentPanel != null)
        {
            parentPanel.Close();
        }
        else
        {
            // 예외 처리: CardMasteryUpgradePanel을 찾지 못했다면 범용 UIPanelBase를 찾아 닫습니다.
            UIPanelBase genericPanel = GetComponentInParent<UIPanelBase>();
            if (genericPanel != null)
            {
                genericPanel.Close();
            }
            else
            {
                // UIPanelBase도 없다면 Canvas 컴포넌트를 만나기 직전의 최상위 UI 패널 루트를 찾아 파괴합니다.
                Transform rootUI = transform;
                while (rootUI.parent != null && rootUI.parent.GetComponent<Canvas>() == null)
                {
                    rootUI = rootUI.parent;
                }

                if (rootUI != transform)
                {
                    rootUI.gameObject.SetActive(false);
                    Destroy(rootUI.gameObject);
                }
                else
                {
                    // 최후의 폴백
                    transform.parent.parent.gameObject.SetActive(false);
                    Destroy(transform.parent.parent.gameObject);
                }
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        highlightImage.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        highlightImage.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        StartMasteryUpgradeCard();
    }
}
