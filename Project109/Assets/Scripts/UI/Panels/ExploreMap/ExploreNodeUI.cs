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
        
        string textureName = "IncountNodeTexture";
        switch (NodeData.encounterData.encountType)
        {
            case EncountType.None:
                textureName = "IncountNodeTexture";
                break;
            case EncountType.Battle:
                switch (NodeData.encounterData.battleExtraRewardType)
                {
                    case BattleExtraRewardType.Card:
                        textureName = "IncountNodeBattleCardTexture";
                        break;
                    case BattleExtraRewardType.Relic:
                        textureName = "IncountNodeBattleRelicTexture";
                        break;
                    case BattleExtraRewardType.Gold:
                        textureName = "IncountNodeBattleGoldTexture";
                        break;
                    default:
                        textureName = "IncountNodeBattleTexture";
                        break;
                }
                break;
            case EncountType.Elite:
                textureName = "IncountNodeEliteEnemyTexture";
                break;
            case EncountType.Boss:
                textureName = "IncountNodeBossTexture";
                break;
            case EncountType.Restore:
                textureName = "IncountNodeRestoreTexture";
                break;
            case EncountType.Store:
                textureName = "IncountNodeStoreTexture";
                break;
            case EncountType.SecretBox:
                textureName = "IncountNodeSecretBoxTexture";
                break;
            case EncountType.Secret:
                textureName = "IncountNodeSecretTexture";
                break;
            default:
                textureName = "IncountNodeTexture";
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
        
        string textureName = "IncountNodeTexture"; // default/none
        switch (NodeData.encounterData.extraEncountType)
        {
            case ExtraEncountType.None:
                textureName = "IncountNodeTexture";
                break;
            case ExtraEncountType.Insight:
                textureName = "IncountNodeInsightTexture";
                break;
            case ExtraEncountType.ShineWell:
                textureName = "IncountNodeInsightTexture";
                break;
            default:
                textureName = "IncountNodeTexture";
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

        if (RunManager.instance == null) return;
        var cur = RunManager.instance.currentExploreNode;

        if (cur != null && NodeData != null)
        {
            bool isNextFloor = (cur.floor + 1 == NodeData.floor);
            bool isConnected = cur.nextNodeIndices.Contains(NodeData.index);

            if (isNextFloor && isConnected)
            {
                RunManager.instance.MoveToNode(this);
            }
            else
            {
                string reason;
                if (cur.floor == NodeData.floor)
                {
                    reason = $"같은 층(Floor {NodeData.floor}) 노드로는 이동할 수 없습니다 (다음 층으로만 전진 가능)";
                }
                else if (cur.floor > NodeData.floor)
                {
                    reason = $"이미 지나온 이전 층(Floor {NodeData.floor}) 노드로는 되돌아갈 수 없습니다";
                }
                else if (cur.floor + 1 < NodeData.floor)
                {
                    reason = $"바로 다음 층이 아닙니다 (현재: Floor {cur.floor} -> 목표: Floor {NodeData.floor})";
                }
                else
                {
                    reason = $"화살표(선)로 연결되어 있지 않은 노드입니다 (이동 가능한 노드 인덱스: [{string.Join(", ", cur.nextNodeIndices)}])";
                }

                Debug.Log($"[ExploreNodeUI] 노드 이동 불가: {reason} | 현재 위치: (Floor {cur.floor}, Index {cur.index}) -> 클릭한 노드: (Floor {NodeData.floor}, Index {NodeData.index})");
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
