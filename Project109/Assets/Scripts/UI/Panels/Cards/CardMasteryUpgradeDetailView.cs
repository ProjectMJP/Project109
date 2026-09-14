using UnityEngine;
using UnityEngine.UI;

public class CardMasteryUpgradeDetailView : MonoBehaviour
{
    private Card selectedCardInstance;

    [SerializeField] private MasteryPointUI currentMasteryPointUI;

    public Button upgradeCardButton;

    void Start()
    {
        upgradeCardButton.onClick.AddListener(StartUpgradeCards);
        gameObject.SetActive(false);
    }

    //클릭한 카드 데이터를 확인하고 화면 상에 보여줌
    public void OnCardCheckUI(Card card)
    {
        if (currentMasteryPointUI == null || card == null || card.cardData == null)
        {
            Debug.Log("[CardMasteryUpgradeDetailView] OnCardCheckUI() : 카드가 없거나 카드의 데이터가 없습니다.");
            return;
        }

        selectedCardInstance = card;

        int upgradeValue = GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null
            ? GameSceneManager.instance.player.playerStat.UpgradeMasteryPointValue
            : 500;

        currentMasteryPointUI.PreviewUpgradeMasteryPointUI(selectedCardInstance, upgradeValue);

        gameObject.SetActive(true);
    }

    public void StartUpgradeCards()
    {
        if (selectedCardInstance != null)
        {
            int upgradeValue = GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null
                ? GameSceneManager.instance.player.playerStat.UpgradeMasteryPointValue
                : 500;
            selectedCardInstance.AddMasteryPoint(upgradeValue);
        }
        transform.parent.gameObject.SetActive(false);
        Destroy(transform.parent.gameObject);
    }
}
