using UnityEngine;

public class RewardChest : InteractableObject
{
    private RewardData activeRewardData;
    private bool _sessionRegistered;

    public override void SetInteractableData(InteractableData data)
    {
        base.SetInteractableData(data);
        if (data != null)
        {
            activeRewardData = data.rewardData;
        }
    }

    public override void OnInteract()
    {
        if (interactableData != null && !string.IsNullOrEmpty(interactableData.targetDialogueID))
        {
            TriggerDialogue();
        }
        else
        {
            OpenChestDirectly();
        }
    }

    public void SetRewardData(RewardData data)
    {
        activeRewardData = data;
    }

    public void OpenChestDirectly()
    {
        if (RunManager.instance == null)
        {
            Debug.LogError("[RewardChest] RunManager.instance가 없어 보상을 등록할 수 없습니다.");
            return;
        }

        if (!_sessionRegistered)
        {
            RunManager.instance.PopulateRewardDataToSession(activeRewardData, 0);
            _sessionRegistered = true;
        }

        if (RunManager.instance.HasPendingRewards &&
            (DialogueManager.Instance == null || !DialogueManager.Instance.IsDialogueActive))
        {
            RunManager.instance.OpenPendingRewardUI();
        }
    }
}
