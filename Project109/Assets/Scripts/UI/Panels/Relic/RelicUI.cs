using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RelicUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public RelicData relicData;
    public Relic relicInstance;

    public Image relicImage;

    public UnityEvent OnRelicClick; //클릭 시 호출될 이벤트

    public void UpdateRelicData(RelicData newRelicData)
    {
        relicData = newRelicData;
        relicInstance = null;

        if (relicData != null && relicData.iconSprite != null && relicImage != null)
        {
            relicImage.sprite = relicData.iconSprite;
        }
    }

    public void UpdateRelicData(Relic instance)
    {
        relicInstance = instance;
        if (instance != null)
        {
            relicData = instance.Data;
        }

        if (relicData != null && relicData.iconSprite != null && relicImage != null)
        {
            relicImage.sprite = relicData.iconSprite;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnRelicClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (relicData == null) return;

        string descriptionText = relicData.description;
        if (relicInstance != null)
        {
            descriptionText = relicInstance.GetDescription();
        }

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.ShowTooltip(relicData.relicName, descriptionText, transform as RectTransform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

    private void OnDisable()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}
