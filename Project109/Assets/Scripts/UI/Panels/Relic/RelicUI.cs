using System.Collections.Generic;
using TMPro;
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

    private bool _isInteractable = true;

    public bool IsInteractable => _isInteractable;

    /// <summary>
    /// 유물 슬롯의 포인터 클릭 및 호버 툴팁 상호작용 가능 여부를 설정합니다.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        _isInteractable = interactable;

        if (relicImage != null)
        {
            relicImage.raycastTarget = interactable;
        }

        if (!interactable && TooltipManager.HasInstance)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

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
        if (!_isInteractable) return;
        OnRelicClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isInteractable || relicData == null) return;

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
        if (TooltipManager.HasInstance)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

    private void OnDisable()
    {
        if (TooltipManager.HasInstance)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}
