using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using CardTypes;

/// <summary>
/// 숙련도 업그레이드 UI 생성 및 경험치 정산 규칙을 연동하는 브릿지 클래스.
/// 실제 카드 숙련도 상태 데이터는 각 Card 인스턴스가 소유합니다.
/// </summary>
public class CardMasteryManager : MonoBehaviour
{
    public static CardMasteryManager instance { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    [SerializeField] private GameObject masteryUpgradeUIPrefab;

    void Start()
    {
        // 더 이상 매니저 내부에 개별 카드 숙련도 목록을 유지하지 않습니다. (Card로 단일화)
    }

    // CardMasteryStat은 Card에 인라인화됨 — card.hasMastery, card.masteryLevel 등 직접 접근
    
    public Dictionary<string, int> GetCardMasteryUpgrades(Card card)
    {
        return card?.masteryUpgrades ?? new Dictionary<string, int>();
    }

    public int GetCurrentMasteryUpgradeLevel(Card card, string masteryID)
    {
        return card?.GetMasteryLevel(masteryID) ?? 0;
    }

    public void AddMastery(Card card, string masteryID)
    {
        card?.AddMastery(masteryID);
    }

    public void ProcessMasteryUpgrade(Card card)
    {
        if (masteryUpgradeUIPrefab == null)
        {
            Debug.LogError("Mastery Upgrade UI Prefab is not assigned!");
            return;
        }

        GameObject uiObj = Instantiate(masteryUpgradeUIPrefab);
        CardMasteryUpgradePanel uiHandler = uiObj.GetComponent<CardMasteryUpgradePanel>();
        if (uiHandler != null)
        {
            uiObj.SetActive(false);
            uiHandler.Open();
            uiHandler.CreateMasteryChoices(card);
        }
        else
        {
            Debug.LogError("CardMasteryUpgradePanel component not found on masteryUpgradeUIPrefab.");
        }
    }

    public void ReportAction(CardMasteryType triggerType, float amount, Card card)
    {
        if (card == null) return;

        Debug.Log($"Reporting action {triggerType} for card {card.path} with amount {amount}");

        float xpGain = 0;
        switch (triggerType)
        {
            case CardMasteryType.UseCard:
                xpGain = 10;
                break;
            case CardMasteryType.DealDamage:
                xpGain = amount;
                break;
            case CardMasteryType.GuardDamage:
                xpGain = amount;
                break;
            case CardMasteryType.KillEnemy:
                xpGain = 50;
                break;
            case CardMasteryType.Heal:
                xpGain = amount * 1.5f;
                break;
            case CardMasteryType.DrawCard:
                xpGain = 5;
                break;
            case CardMasteryType.UpgradeCard:
                xpGain = amount;
                break;
        }
        card.AddMasteryPoint(xpGain);
    }
}
