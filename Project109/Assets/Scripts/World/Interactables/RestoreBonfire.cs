using UnityEngine;


public class RestoreBonfire : InteractableObject
{
    private RestoreUIHandler restoreUI;

    [Header("Restore Dialogue Settings")]
    [SerializeField] private string defaultDialogueID = "Restore_Bonfire_Dialogue";

    private bool isUsed = false;
    public bool IsUsed => isUsed;

    public override void SetInteractableData(InteractableData data)
    {
        if (data == null && ModLoader.Instance != null && ModLoader.Instance.InteractableDatabase != null)
        {
            ModLoader.Instance.InteractableDatabase.TryGetValue("Restore_Bonfire_Data", out data);
        }
        base.SetInteractableData(data);
    }

    public void MarkAsUsed()
    {
        isUsed = true;
        Debug.Log("[RestoreBonfire] 모닥불이 사용 완료(소모) 처리되었습니다.");
    }

    public override void OnInteract()
    {
        string dialogueID = (interactableData != null && !string.IsNullOrEmpty(interactableData.targetDialogueID))
            ? interactableData.targetDialogueID
            : defaultDialogueID;

        if (DialogueManager.Instance == null)
        {
            Debug.LogError($"[RestoreBonfire] DialogueManager.Instance가 존재하지 않아 모닥불 다이얼로그({dialogueID})를 시작할 수 없습니다.");
            return;
        }

        if (ModLoader.Instance != null && ModLoader.Instance.DialogueDatabase.TryGetValue(dialogueID, out var dialogueData))
        {
            DialogueManager.Instance.StartDialogue(dialogueData, this);
            if (isUsed)
            {
                DialogueManager.Instance.GoToNode("USED");
            }
        }
        else
        {
            Debug.LogWarning($"[RestoreBonfire] 다이얼로그({dialogueID})를 찾을 수 없어 폴백 UI를 실행합니다.");
            OpenRestoreDirectly();
        }
    }

    public void CreateRestoreUI()
    {
        if (restoreUI != null)
        {
            return;
        }

        if (UIManager.instance == null)
        {
            Debug.LogError("[RestoreBonfire] UIManager.instance가 null이라 RestoreNPCUI를 열 수 없습니다.");
            return;
        }

        GameObject spawned = UIManager.instance.OpenUI(UIConstants.PANEL_RESTORE_NPC, UILayerType.Normal, false);
        if (spawned == null)
        {
            Debug.LogError("[RestoreBonfire] RestoreNPCUI 프리팹을 찾을 수 없습니다.");
            return;
        }

        restoreUI = spawned.GetComponentInChildren<RestoreUIHandler>(true);
        if (restoreUI == null)
        {
            Debug.LogError("[RestoreBonfire] RestoreNPCUI에서 RestoreUIHandler 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        restoreUI.gameObject.SetActive(false);
    }

    public RestoreUIHandler GetRestoreUI()
    {
        return restoreUI;
    }

    public void OpenRestoreDirectly()
    {
        if (restoreUI == null)
        {
            CreateRestoreUI();
        }

        if (restoreUI != null)
        {
            restoreUI.Open();
        }
        else
        {
            Debug.LogWarning("[RestoreBonfire] RestoreUI를 생성할 수 없습니다.");
        }
    }

    public void CloseRestoreUI()
    {
        if (restoreUI != null)
        {
            restoreUI.Close();
        }
    }
}
