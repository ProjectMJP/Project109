using GameItem.Types;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private CardData cardData;
    private Card cardInstance;

    //private CardEffect currentCardEffect;   //현재 카드효과
    [SerializeField] private CardDescriptionHandler cardDescriptionHandler;

    public TextMeshProUGUI cardName;
    public TextMeshProUGUI cardDescription;
    public TextMeshProUGUI useStamina;

    public Image cardImage;  //카드 데이터에 맞는 이미지

    public GameObject selectHighlightObject;    //선택을 알려주는 하이라이트 UI
    [SerializeField] private GameObject masteryUpgradeAvailableUI;    // 마스터리 업그레이드 가능 알림 UI

    public EffectAreaCheckButton effectAreaCheckButton; //공격 범위 확인용 버튼

    public UnityEvent OnCardClick;  //클릭 시 호출될 이벤트(다양한 변수들도 쉽게 호출하기 위해 UnityEvent 사용)

    public bool bIsCardHighlight;
    public bool bShowEffectAreaUI;

    [Header("Zoom Settings")]
    public bool enableZoom = false;
    public float zoomScale = 1.1f;
    public float zoomYOffset = 90f;
    private Vector3 originalScale = Vector3.one;
    private Vector3 originalPosition;
    private int originalSiblingIndex;
    private bool isZoomed = false;

    void Awake()
    {
        selectHighlightObject.SetActive(false);
        if (masteryUpgradeAvailableUI != null)
        {
            masteryUpgradeAvailableUI.SetActive(false);
        }
    }

    public void UpdateCardData(CardData newCardData)
    {
        if (newCardData == null)
        {
            Debug.LogWarning("New Card Data is null!");
            return;
        }
        cardData = newCardData;
        cardInstance = null;

        cardName.text = cardData.cardName;
        if (useStamina != null)
        {
            useStamina.text = cardData.stamina.ToString();
        }

        UpdateCardDescription();

        if (cardData.cardSprite != null)
        {
            cardImage.sprite = cardData.cardSprite;
        }

        if (masteryUpgradeAvailableUI != null)
        {
            masteryUpgradeAvailableUI.SetActive(false);
        }
    }

    public void UpdateCardInstance(Card newCardInstance)
    {
        if (newCardInstance == null)
        {
            Debug.LogWarning("New Card Instance is null!");
            return;
        }
        cardInstance = newCardInstance;
        cardData = cardInstance.cardData;

        // masteryLevel > 0이면 ★ 접두사 표시
        string displayName = cardData.cardName;
        if (cardInstance.masteryLevel > 0)
            displayName = "\u2605" + displayName;
        cardName.text = displayName;
        if (useStamina != null)
        {
            useStamina.text = cardInstance.currentCost.ToString();
        }

        UpdateCardDescription();

        if (cardData.cardSprite != null)
        {
            cardImage.sprite = cardData.cardSprite;
        }

        if (masteryUpgradeAvailableUI != null)
        {
            bool canUpgrade = cardInstance.hasMastery && 
                              (cardInstance.currentMasteryXP >= cardInstance.maxMasteryXP) &&
                              (cardInstance.GetRandomMasteryOption(1).Count > 0);
            masteryUpgradeAvailableUI.SetActive(canUpgrade);
        }
    }

    public void UpgradeCard()
    {
        if (cardData == null)
            return;

        string upgradeName = cardData.upgradedCardName;
        if (!string.IsNullOrEmpty(upgradeName) && ModLoader.Instance.CardDatabase.TryGetValue(upgradeName, out CardData upgradeCardData))
        {
            cardData = upgradeCardData;
            cardName.text = cardData.cardName;
            if (useStamina != null)
            {
                useStamina.text = cardData.stamina.ToString();
            }
            UpdateCardDescription();

            if (cardData.cardSprite != null)
            {
                cardImage.sprite = cardData.cardSprite;
            }
        }
        else
        {
            Debug.LogWarning("Failed to Upgrade Card!");
        }
    }

    public void UpdateCardDescription()
    {
        string rawDescription = string.Empty;
        if (cardInstance != null)
        {
            rawDescription = cardInstance.GetDescription();
        }
        else if (cardData != null)
        {
            rawDescription = cardData.GetDescription();
        }
        string formattedDescription = TooltipManager.ReplaceKeywordsForDisplay(rawDescription);
        cardDescription.SetText(formattedDescription);
    }

    public CardData GetCardData()
    {
        return cardData;
    }

    public Card GetCardInstance()
    {
        return cardInstance;
    }

    public void OnSelectHighlight()
    {
        selectHighlightObject.SetActive(true);
    }

    public void OffSelectHighlight()
    {
        selectHighlightObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnCardClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (bIsCardHighlight)
            OnSelectHighlight();       //카드 하이라이트on

        //카드 범위 세팅 진행
        if (bShowEffectAreaUI && cardData != null)
        {
            Debug.Log("Show Effect Area UI");
            UIManager.instance.UpdateEffectAreaUI(cardData);
        }

        if (enableZoom)
        {
            if (!isZoomed)
            {
                isZoomed = true;
                originalScale = transform.localScale;
                originalPosition = transform.localPosition;
                originalSiblingIndex = transform.GetSiblingIndex();
            }
            transform.localScale = Vector3.one * zoomScale;
            transform.localPosition = originalPosition + new Vector3(0, zoomYOffset, 0);
            transform.SetAsLastSibling();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (bIsCardHighlight)
            OffSelectHighlight();       //카드 하이라이트off

        if (bShowEffectAreaUI)
        {
            UIManager.instance.ClearEffectAreaTiles();
        }

        if (enableZoom && isZoomed)
        {
            isZoomed = false;
            transform.localScale = originalScale;
            transform.localPosition = originalPosition;
            transform.SetSiblingIndex(originalSiblingIndex);
        }
    }
}
