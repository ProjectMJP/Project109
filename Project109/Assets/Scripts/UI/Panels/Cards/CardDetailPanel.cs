using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CardDetailPanel : UIPanelBase
{
    public CardUI cardUI;

    [SerializeField] private GameObject detailDescriptionUIPrefab;
    [SerializeField] private Transform detailDescriptionUITransform;
    [SerializeField] private Button closeBackgroundButton;

    private void Awake()
    {
        if (cardUI == null)
        {
            cardUI = GetComponentInChildren<CardUI>(true);
        }

        if (closeBackgroundButton == null)
        {
            var trigger = transform.Find("CloseTrigger");
            if (trigger != null)
            {
                closeBackgroundButton = trigger.GetComponent<Button>();
                if (closeBackgroundButton == null)
                {
                    closeBackgroundButton = trigger.gameObject.AddComponent<Button>();
                }
            }
        }

        if (closeBackgroundButton != null)
        {
            closeBackgroundButton.onClick.RemoveListener(Close);
            closeBackgroundButton.onClick.AddListener(Close);
        }
    }


    //클릭한 카드 데이터를 확인하고 화면 상에 보여줌 (CardData 오버로드)
    public void OnCardCheckUI(CardData newCardData)
    {
        if (cardUI == null)
        {
            cardUI = GetComponentInChildren<CardUI>(true);
        }
        if (cardUI == null)
            return;

        cardUI.UpdateCardData(newCardData);
        cardUI.bIsCardHighlight = false;

        Open();

        //이전에 있던 상세 설명 UI 삭제
        foreach (Transform child in detailDescriptionUITransform)
        {
            Destroy(child.gameObject);
        }

        //레이아웃 갱신
        LayoutRebuilder.ForceRebuildLayoutImmediate(detailDescriptionUITransform.GetComponent<RectTransform>());

        //카드 효과 범위 업데이트
        if(UIManager.instance != null)
        {
            UIManager.instance.UpdateEffectAreaUI(newCardData);
        }
    }

    //클릭한 카드 데이터를 확인하고 화면 상에 보여줌 (Card 오버로드)
    public void OnCardCheckUI(Card card)
    {
        if (cardUI == null)
        {
            cardUI = GetComponentInChildren<CardUI>(true);
        }
        if (card == null || cardUI == null)
            return;

        cardUI.UpdateCardInstance(card);
        cardUI.bIsCardHighlight = false;

        Open();

        //이전에 있던 상세 설명 UI 삭제
        foreach (Transform child in detailDescriptionUITransform)
        {
            Destroy(child.gameObject);
        }

        //카드 마스터리 업그레이드 정보 표시 (CardData.masteryNames에서 표시명 조회)
        Dictionary<string, int> masteryUpgrades = card.masteryUpgrades;

        if(masteryUpgrades != null && card.cardData != null)
        {
            foreach (var upgrade in masteryUpgrades)
            {
                string masteryId = upgrade.Key;
                int count = upgrade.Value;

                // CardData.masteryNames에서 표시명 조회, 없으면 masteryId 그대로 사용
                string displayName = masteryId;
                if (card.cardData.masteryNames != null &&
                    card.cardData.masteryNames.TryGetValue(masteryId, out string name))
                {
                    displayName = name;
                }

                GameObject detailDescriptionUIObj = Instantiate(detailDescriptionUIPrefab, detailDescriptionUITransform);
                MasteryUpgradeDescriptionUI detailDescriptionUI = detailDescriptionUIObj.GetComponent<MasteryUpgradeDescriptionUI>();
                detailDescriptionUI.SetDescriptionText($"{displayName} X {count}");
            }
        }

        //레이아웃 갱신
        LayoutRebuilder.ForceRebuildLayoutImmediate(detailDescriptionUITransform.GetComponent<RectTransform>());

        //카드 효과 범위 업데이트
        if(UIManager.instance != null)
        {
            UIManager.instance.UpdateEffectAreaUI(card);
        }
    }
}
