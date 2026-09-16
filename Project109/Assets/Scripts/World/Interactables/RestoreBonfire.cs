using UnityEngine;

namespace RestoreUI
{
    // We can define RestoreBonfire in the global namespace or namespace matching files.
    // Let's use global namespace just like other NPCs, or match original context.
}

public class RestoreBonfire : InteractableObject
{
    public GameObject restoreUIPrefab;
    private RestoreUIHandler restoreUI;

    [Header("Restore Dialogue Settings")]
    [SerializeField] private string defaultDialogueID = "Restore_Bonfire_Dialogue";

    private bool isUsed = false;
    public bool IsUsed => isUsed;

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

        GameObject spawned = null;
        if (UIManager.instance != null)
        {
            spawned = UIManager.instance.OpenUI("RestoreNPCUI", UILayerType.Normal, false);
        }

        if (spawned == null)
        {
            if (restoreUIPrefab != null)
            {
                spawned = Instantiate(restoreUIPrefab);
            }
            else
            {
                Debug.LogError("[RestoreBonfire] RestoreNPCUI 프리팹을 찾을 수 없습니다.");
                return;
            }
        }

        if (spawned.transform.childCount > 0)
        {
            restoreUI = spawned.transform.GetChild(0).GetComponent<RestoreUIHandler>();
            if (restoreUI == null)
            {
                restoreUI = spawned.GetComponent<RestoreUIHandler>();
            }
        }
        else
        {
            restoreUI = spawned.GetComponent<RestoreUIHandler>();
        }

        if (restoreUI != null)
        {
            restoreUI.gameObject.SetActive(false);
        }
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

            if (RunManager.instance != null && RunManager.instance.currentMap != null)
            {
                if (!RunManager.instance.currentMap.currentSpawnUIList.Contains(restoreUI.gameObject))
                {
                    RunManager.instance.currentMap.currentSpawnUIList.Add(restoreUI.gameObject);
                }
            }
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
