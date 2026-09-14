using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum ShopItems
{
    Card,
    Relic,
    Potion
}

public class ShopItemTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ShopItems ShopItemType;

    public CardUI cardHandler;
    public RelicUI relicHandler;
    //포션 데이터 나중에 추가

    void Start()
    {
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"{gameObject.name} is enter!");

        switch (ShopItemType)
        {
            case ShopItems.Card:
                if(cardHandler != null)
                {
                    cardHandler.OnSelectHighlight();
                }
                break;
            case ShopItems.Relic:
                //유물 설명 출력
                if (relicHandler != null && relicHandler.relicData != null)
                {
                    if (TooltipManager.Instance != null)
                    {
                        TooltipManager.Instance.ShowTooltip(relicHandler.relicData.relicName, relicHandler.relicData.description, transform as RectTransform);
                    }
                }
                break;
            case ShopItems.Potion:
                //포션 설명 출력
                break;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"{gameObject.name} is Exit!");

        switch (ShopItemType)
        {
            case ShopItems.Card:
                if (cardHandler != null)
                {
                    cardHandler.OffSelectHighlight();
                }
                break;
            case ShopItems.Relic:
                if (relicHandler != null)
                {
                    if (TooltipManager.Instance != null)
                    {
                        TooltipManager.Instance.HideTooltip();
                    }
                }
                break;
            case ShopItems.Potion:
                //포션 설명 출력
                break;
        }
    }
}
