using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ExploreNodeUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public ExploreNode NodeData { get; private set; }
    public List<GameObject> nextExploreNodeUI = new List<GameObject>();
    public ExploreMapUI ExploreMapUI;

    //화살표 기준 노드의 위치
    public Vector2 arrowRelativePos;

    //가리기, 선택 등 노드의 추가적인 생김새 변경을 위한 오브젝트들 (기존 프리팹 직렬화 호환을 위해 FormerlySerializedAs 지정)
    [FormerlySerializedAs("IncountNodeCoverObject")]
    public GameObject ExploreNodeUICoverObject;

    [FormerlySerializedAs("IncountNodeHighlightCircleObject")]
    public GameObject ExploreNodeUIHighlightCircleObject;

    [FormerlySerializedAs("IncountNodeCurrentHighlightCircleObject")]
    public GameObject ExploreNodeUICurrentHighlightCircleObject;

    [FormerlySerializedAs("IncountNodeExtraRewardObject")]
    public GameObject ExploreNodeUIExtraRewardObject;

    public void InitExploreNodeUI(ExploreNode data)
    {
        NodeData = data;
        SetNodeTexture();
        SetExtraNodeTexture();
        UpdateVisual();
    }

    /// <summary>
    /// 노드 타입에 맞는 sprite 등록
    /// </summary>
    void SetNodeTexture()
    {
        Image image = GetComponent<Image>();
        if (image == null) return;

        if (AssetCacheManager.instance == null)
        {
            Debug.LogWarning("[ExploreNodeUI] AssetCacheManager.instance is null");
            return;
        }

        if (NodeData == null) return;
        
        string textureName = "ExploreNodeUITexture";
        switch (NodeData.encounterData.encountType)
        {
            case EncountType.None:
                textureName = "ExploreNodeUITexture";
                break;
            case EncountType.Battle:
                switch (NodeData.encounterData.battleExtraRewardType)
                {
                    case BattleExtraRewardType.Card:
                        textureName = "ExploreNodeUIBattleCardTexture";
                        break;
                    case BattleExtraRewardType.Relic:
                        textureName = "ExploreNodeUIBattleRelicTexture";
                        break;
                    case BattleExtraRewardType.Gold:
                        textureName = "ExploreNodeUIBattleGoldTexture";
                        break;
                    default:
                        textureName = "ExploreNodeUIBattleTexture";
                        break;
                }
                break;
            case EncountType.Elite:
                textureName = "ExploreNodeUIEliteEnemyTexture";
                break;
            case EncountType.Boss:
                textureName = "ExploreNodeUIBossTexture";
                break;
            case EncountType.Restore:
                textureName = "ExploreNodeUIRestoreTexture";
                break;
            case EncountType.Store:
                textureName = "ExploreNodeUIStoreTexture";
                break;
            case EncountType.SecretBox:
                textureName = "ExploreNodeUISecretBoxTexture";
                break;
            case EncountType.Secret:
                textureName = "ExploreNodeUISecretTexture";
                break;
            default:
                textureName = "ExploreNodeUITexture";
                break;
        }

        if (AssetCacheManager.instance.TryGetTexture(textureName, out Sprite sprite))
        {
            image.sprite = sprite;
        }
        else
        {
            Debug.LogWarning($"[ExploreNodeUI] Failed to load Addressable texture: {textureName}");
        }
    }

    void SetExtraNodeTexture()
    {
        if (ExploreNodeUIExtraRewardObject == null) return;

        Image image = ExploreNodeUIExtraRewardObject.GetComponent<Image>();
        if (image == null) return;

        if (AssetCacheManager.instance == null) return;

        if (NodeData == null) return;
        
        string textureName = "ExploreNodeUITexture"; // default/none
        switch (NodeData.encounterData.extraEncountType)
        {
            case ExtraEncountType.None:
                textureName = "ExploreNodeUITexture";
                break;
            case ExtraEncountType.Insight:
                textureName = "ExploreNodeUIInsightTexture";
                break;
            case ExtraEncountType.ShineWell:
                textureName = "ExploreNodeUIInsightTexture";
                break;
            default:
                textureName = "ExploreNodeUITexture";
                break;
        }

        if (AssetCacheManager.instance.TryGetTexture(textureName, out Sprite sprite))
        {
            image.sprite = sprite;
        }
        else
        {
            image.sprite = null;
        }
    }

    /// <summary>
    /// 노드의 데이터(NodeData.isRevealed) 상태에 따라 가림막을 갱신합니다.
    /// </summary>
    public void UpdateVisual()
    {
        if (NodeData != null && NodeData.isRevealed)
        {
            RevealNode();
        }
        else
        {
            HideNode();
        }
    }

    /// <summary>
    /// 노드의 가림막을 해제(공개)합니다.
    /// </summary>
    public void RevealNode()
    {
        if (ExploreNodeUICoverObject != null)
        {
            ExploreNodeUICoverObject.SetActive(false);
        }

        if (NodeData != null && NodeData.encounterData.extraEncountType != ExtraEncountType.None)
        {
            if (ExploreNodeUIExtraRewardObject != null)
            {
                ExploreNodeUIExtraRewardObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 노드를 가립니다(안개 처리).
    /// </summary>
    public void HideNode()
    {
        if (ExploreNodeUICoverObject != null)
        {
            ExploreNodeUICoverObject.SetActive(true);
        }

        if (ExploreNodeUIExtraRewardObject != null)
        {
            ExploreNodeUIExtraRewardObject.SetActive(false);
        }
    }

    // 하위 호환성 메서드 (기존 명칭 유지)
    public void OpenNodeCoverTexture() => RevealNode();
    public void CloseNodeCoverTexture() => HideNode();

    public void LoadMapDataFromExploreNodeUI()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            Debug.Log("[ExploreNodeUI] NPC 대화 중에는 맵 노드를 이동할 수 없습니다.");
            return;
        }
        if (RunManager.instance.currentExploreNode != null && NodeData != null)
        {
            if (RunManager.instance.currentExploreNode.nextNodeIndices.Contains(NodeData.index) && 
                RunManager.instance.currentExploreNode.floor + 1 == NodeData.floor)
            {
                RunManager.instance.MoveToNode(this);
            }
            else
            {
                Debug.Log("This node is not connected to the current node.");
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        LoadMapDataFromExploreNodeUI();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ExploreNodeUIHighlightCircleObject != null)
        {
            ExploreNodeUIHighlightCircleObject.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ExploreNodeUIHighlightCircleObject != null)
        {
            ExploreNodeUIHighlightCircleObject.SetActive(false);
        }
    }
}
