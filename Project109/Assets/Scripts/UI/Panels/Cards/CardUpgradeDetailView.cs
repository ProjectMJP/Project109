using UnityEngine;
using UnityEngine.UI;

public class CardUpgradeDetailView : MonoBehaviour
{
    private Card currentCardInstance;
    private CardData selectedUpgradeCardData;

    [SerializeField] private CardUI currentCard;
    [SerializeField] private CardUI upgradeCard;

    public Button upgradeCardButton;

    void Start()
    {
        upgradeCardButton.onClick.AddListener(StartUpgradeCards);
        gameObject.SetActive(false);
    }

    //클릭한 카드 데이터를 확인하고 화면 상에 보여줌
    public void OnCardCheckUI(Card card)
    {
        Debug.Log("OnCardCheckUI");
        if (currentCard == null || upgradeCard == null || card == null || card.cardData == null)
        {
            Debug.Log("[CardUpgradeDetailView] OnCardCheckUI() : 카드가 없거나 카드의 데이터가 없습니다.");
            return;
        }

        currentCardInstance = card;

        currentCard.UpdateCardInstance(card);
        currentCard.bIsCardHighlight = false;

        string upgradeName = card.cardData.upgradedCardName;
        if (!string.IsNullOrEmpty(upgradeName) && ModLoader.Instance.CardDatabase.TryGetValue(upgradeName, out CardData upgradeCardData))
        {
            selectedUpgradeCardData = upgradeCardData;

            upgradeCard.UpdateCardData(selectedUpgradeCardData);
            upgradeCard.bIsCardHighlight = false;
        }

        gameObject.SetActive(true);
    }

    public void StartUpgradeCards()
    {
        if (currentCardInstance != null && GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            GameSceneManager.instance.player.deck.UpgradeCard(currentCardInstance.runtimeID);
        }

        UIPanelBase parentPanel = GetComponentInParent<UIPanelBase>();
        if (parentPanel != null)
        {
            parentPanel.Close();
        }
        else if (transform.parent != null)
        {
            Destroy(transform.parent.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
