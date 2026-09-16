using UnityEngine;

public class InteractableObject : MonoBehaviour, IInteractable
{
    [SerializeField]
    protected InteractableData interactableData; // 상호작용 관련 기획 데이터

    public InteractableData InteractableData => interactableData;

    public bool RequiresCameraFocus => true;

    public virtual void SetInteractableData(InteractableData data)
    {
        interactableData = data;
        InstantiateModel();
    }

    /// <summary>
    /// Addressable 시스템을 통해 Interactable 3D/2D 모델을 동적으로 스폰하고 초기화합니다.
    /// </summary>
    public void InstantiateModel()
    {
        if (interactableData == null) return;

        if (AssetCacheManager.instance.TryGetModel(interactableData.modelPrefabPath, out GameObject prefab))
        {
            Debug.Log($"[InteractableObject] Found Model from Addressable: {interactableData.modelPrefabPath}");
            GameObject model = Instantiate(prefab, this.transform);
            if (model == null)
            {
                Debug.LogWarning("[InteractableObject] Failed to Instantiate Model!");
                return;
            }
            
            model.tag = "EventNPC";
            ChangeAllLayer(model, LayerMask.NameToLayer("NPC"));

            // TODO: EventObject 프리팹의 임시 플레이스홀더 Cube가 모델과 겹칠 경우 사용하거나 프리팹에서 직접 정리 후 제거 가능
            Transform placeholder = transform.Find("Cube");
            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.Log($"[InteractableObject] Found Failed from Addressable: {interactableData.modelPrefabPath}");
        }
    }

    /// <summary>
    /// 특정 오브젝트 및 자식 오브젝트들의 레이어를 모두 변경합니다.
    /// </summary>
    private void ChangeAllLayer(GameObject model, int layer)
    {
        model.layer = layer;

        foreach (Transform child in model.transform)
        {
            ChangeAllLayer(child.gameObject, layer);
        }
    }

    /// <summary>
    /// 플레이어와 상호작용 시 호출되는 인터페이스 구현부입니다.
    /// </summary>
    public virtual void OnInteract()
    {
        TriggerDialogue();
    }

    /// <summary>
    /// 오브젝트에 연결된 기본 다이얼로그를 시작합니다.
    /// </summary>
    protected void TriggerDialogue()
    {
        if (interactableData == null)
        {
            Debug.LogWarning("[InteractableObject] 상호작용 데이터가 존재하지 않습니다.");
            return;
        }

        if (!string.IsNullOrEmpty(interactableData.targetDialogueID))
        {
            if (ModLoader.Instance != null && ModLoader.Instance.DialogueDatabase.TryGetValue(interactableData.targetDialogueID, out var dialogueData))
            {
                DialogueManager.Instance.StartDialogue(dialogueData, this);
            }
            else
            {
                Debug.LogError($"[InteractableObject] 다이얼로그 데이터를 찾을 수 없습니다: {interactableData.targetDialogueID}");
            }
        }
        else
        {
            Debug.LogWarning("[InteractableObject] targetDialogueID가 설정되어 있지 않습니다.");
        }
    }
}
