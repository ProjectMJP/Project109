using UnityEngine;

public class InteractableObject : MonoBehaviour, IInteractable
{
    [SerializeField]
    protected InteractableData interactableData; // 상호작용 관련 기획 데이터

    protected bool isUsed = false;

    public InteractableData InteractableData => interactableData;
    public bool IsUsed => isUsed;

    public bool RequiresCameraFocus => true;

    /// <summary>
    /// 상호작용 오브젝트를 사용 완료(소모) 상태로 전환합니다.
    /// </summary>
    public virtual void MarkAsUsed()
    {
        isUsed = true;
        Debug.Log($"[InteractableObject] 상호작용 오브젝트가 사용 완료 처리되었습니다: {(interactableData != null ? interactableData.interactableID : name)}");
    }

    /// <summary>
    /// 스폰된 베이스 오브젝트에 상호작용 타입에 맞는 컴포넌트를 구성하고 데이터를 초기화합니다.
    /// 기본 Normal 타입일 경우 프리팹에 부착된 기존 InteractableObject 컴포넌트를 그대로 재사용합니다.
    /// </summary>
    public static InteractableObject ConfigureOnGameObject(GameObject target, CardTypes.InteractableType type, InteractableData eventData)
    {
        if (target == null) return null;

        InteractableObject existing = target.GetComponent<InteractableObject>();
        InteractableObject instance = null;

        switch (type)
        {
            case CardTypes.InteractableType.Chest:
                if (existing != null && !(existing is RewardChest))
                {
                    DestroyImmediate(existing);
                }
                instance = target.GetComponent<RewardChest>();
                if (instance == null)
                {
                    instance = target.AddComponent<RewardChest>();
                }
                break;

            case CardTypes.InteractableType.Shop:
                if (existing != null && !(existing is ShopNPC))
                {
                    DestroyImmediate(existing);
                }
                instance = target.GetComponent<ShopNPC>();
                if (instance == null)
                {
                    instance = target.AddComponent<ShopNPC>();
                }
                break;

            case CardTypes.InteractableType.RestoreBonfire:
                if (existing != null && !(existing is RestoreBonfire))
                {
                    DestroyImmediate(existing);
                }
                instance = target.GetComponent<RestoreBonfire>();
                if (instance == null)
                {
                    instance = target.AddComponent<RestoreBonfire>();
                }
                break;

            default:
                if (existing != null && existing.GetType() != typeof(InteractableObject))
                {
                    DestroyImmediate(existing);
                    existing = null;
                }
                instance = existing != null ? existing : target.AddComponent<InteractableObject>();
                break;
        }

        if (instance != null)
        {
            instance.SetInteractableData(eventData);
        }

        return instance;
    }

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
            if (DialogueManager.Instance == null)
            {
                Debug.LogError($"[InteractableObject] DialogueManager.Instance가 존재하지 않아 다이얼로그({interactableData.targetDialogueID})를 시작할 수 없습니다.");
                return;
            }

            if (ModLoader.Instance != null && ModLoader.Instance.DialogueDatabase.TryGetValue(interactableData.targetDialogueID, out var dialogueData))
            {
                if (isUsed)
                {
                    if (HasDialogueNode(dialogueData, "USED"))
                    {
                        DialogueManager.Instance.StartDialogue(dialogueData, this, "USED");
                    }
                    else
                    {
                        Debug.Log($"[InteractableObject] 이미 사용 완료된 상호작용 오브젝트입니다: {interactableData.interactableID}");
                    }
                    return;
                }

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

    protected static bool HasDialogueNode(DialogueData data, string nodeID)
    {
        if (data == null || data.nodes == null || string.IsNullOrEmpty(nodeID))
        {
            return false;
        }

        for (int i = 0; i < data.nodes.Count; i++)
        {
            DialogueNode node = data.nodes[i];
            if (node != null && string.Equals(node.nodeID, nodeID, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
