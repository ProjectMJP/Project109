using UnityEngine;
using System.Collections.Generic;

public class CardMasteryUpgradePanel : UIPanelBase
{
    [SerializeField] private GameObject masteryChoicePrefab;
    [SerializeField] private Transform contentTransform;

    public override bool CanCloseByCancel => false;
    public override bool DestroyOnClose => true;

    private Card selectedCardInstance;

    public void CreateMasteryChoices(Card card)
    {
        selectedCardInstance = card;

        //선택지를 생성할 위치의 자식들 제거
        foreach (Transform child in contentTransform)
        {
            Destroy(child.gameObject);
        }

        int choiceCount = GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null
            ? GameSceneManager.instance.player.playerStat.MasteryChoiceCount
            : 3;
        List<string> availableMasteryIds = card.GetRandomMasteryOption(choiceCount);

        foreach (string masteryId in availableMasteryIds)
        {
            GameObject choiceObj = Instantiate(masteryChoicePrefab, contentTransform);
            MasteryChoiceItem choiceHandler = choiceObj.GetComponent<MasteryChoiceItem>();
            if(choiceHandler != null)
            {
                choiceHandler.SetChoice(card, masteryId);
            }
            else
            {
                Debug.LogError("MasteryChoiceItem component not found on masteryChoicePrefab.");
            }
        }
    }
}
