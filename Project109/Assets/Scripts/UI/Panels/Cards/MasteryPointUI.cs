using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MasteryPointUI : MonoBehaviour
{
    [SerializeField] private Image masteryPointBar;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    private int maxMasteryPoint;


    public void SetMasteryDescription(CardData cardData, int maxPoint)
    {
        valueText.SetText("0 / " + maxPoint);
        descriptionText.SetText(SetMasteryDescription(cardData.cardType));
        masteryPointBar.fillAmount = 0f;
    }

    public void SetMasteryDescription(Card card, int maxPoint)
    {
        if (card == null || card.cardData == null) return;
        valueText.SetText("0 / " + maxPoint);
        descriptionText.SetText(SetMasteryDescription(card.cardData.cardType));
        masteryPointBar.fillAmount = 0f;

        if (card.hasMastery)
        {
            valueText.SetText(card.currentMasteryXP + " / " + card.maxMasteryXP);
            float fillAmount = card.maxMasteryXP > 0 ? card.currentMasteryXP / card.maxMasteryXP : 0f;
            masteryPointBar.fillAmount = Mathf.Clamp01(fillAmount);
        }
    }

    public void UpdateMasteryPointUI(Card card)
    {
        if (card == null || !card.hasMastery) return;

        valueText.SetText(card.currentMasteryXP + " / " + card.maxMasteryXP);
        float fillAmount = card.maxMasteryXP > 0
            ? card.currentMasteryXP / card.maxMasteryXP : 0f;
        masteryPointBar.fillAmount = Mathf.Clamp01(fillAmount);
    }

    public void PreviewUpgradeMasteryPointUI(Card card, int increaseMasteryPoint)
    {
        if (card == null || !card.hasMastery) return;

        valueText.SetText(card.currentMasteryXP + " + " +
            "<color=#00EB00>" + increaseMasteryPoint + "</color>" +
            " / " + card.maxMasteryXP);

        float fillAmount = card.maxMasteryXP > 0
            ? card.currentMasteryXP / card.maxMasteryXP : 0f;
        masteryPointBar.fillAmount = Mathf.Clamp01(fillAmount);
    }

    public string SetMasteryDescription(string cardType)
    {
        switch(cardType)
        {
            case "Attack":
                return "카드 사용: 10\n" +
                       "적 처치: 50\n" +
                       "입힌 피해: 피해 수치";
            case "Skill":
                return "카드 사용: 10\n" +
                       "입힌 피해: 피해 수치\n" +
                       "막은 피해: 막힌 수치\n" +
                       "힐량 : 회복된 체력 수치";
            case "Utility":
                return "카드 사용: 10";
        }

        return "";
    }
}
