using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class EffectAreaCheckButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform effectAreaSpawnTransform;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(UIManager.instance.effectAreaManager == null)
        {
            return;
        }

        //UIManager.instance.OnCardEffectAreaBackground(effectAreaSpawnTransform.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (UIManager.instance.effectAreaManager == null)
        {
            return;
        }

        //UIManager.instance.OffCardEffectAreaBackground();
    }
}
