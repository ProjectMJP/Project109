using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum IncountType
{ 
    None,       //비어있음
    Elite,      //엘리트 몬스터
    Boss,       //보스
    Store,      //상점
    Restore,    //휴식
    Battle,     //전투
    SecretBox,  //박스
    Secret,     //비밀
}

public enum ExtraIncountType
{
    None,
    Insight,    //천리안
    ShineWell   //빛나는 우물
}

public enum BattleExtraRewardType
{
    None,
    Card,       //희귀등급 이상의 카드
    Relic,      //일반 등급 이상의 유물
    Gold        //보너스 재화
}

[RequireComponent(typeof(Image))]
public class IncountNode : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public IncountType incountType;
    public ExtraIncountType extraIncountType;
    public BattleExtraRewardType battleExtraRewardType = BattleExtraRewardType.None;
    public List<GameObject> nextIncountNode;
    public ExploreUI exploreUI;
    public bool isNodeChanged;  //노드가 생성되고 노드 타입이 한번 이상 변경되었는지 여부

    public BattleData battleNodeData;
    public InteractableData eventNodeData;

    //화살표 기준 노드의 위치
    public Vector2 arrowRelativePos;

    //가리기, 선택 등 노드의 추가적인 생김새 변경을 위한 오브젝트들
    public GameObject IncountNodeCoverObject;
    public GameObject IncountNodeHighlightCircleObject;
    public GameObject IncountNodeCurrentHighlightCircleObject;
    public GameObject IncountNodeExtraRewardObject;



    public void SetIncountNode(IncountType newIncountType, ExtraIncountType newExtraIncountType, BattleExtraRewardType newExtraRewardType = BattleExtraRewardType.None)
    {
        incountType = newIncountType;
        extraIncountType = newExtraIncountType;
        battleExtraRewardType = newExtraRewardType;
        SetNodeTexture();
        SetExtraNodeTexture();
    }

    /// <summary>
    /// 아무것도 없는 상태를 제외한 모든 노드 종류들 중 랜덤으로 노드 변경
    /// </summary>
    public void SetRandomIncountNode()
    {
        var enumValue = System.Enum.GetValues(enumType:typeof(IncountType));
        incountType = (IncountType)enumValue.GetValue(Random.Range(1, enumValue.Length));
        battleExtraRewardType = BattleExtraRewardType.None;
        SetNodeTexture();
        SetExtraNodeTexture();
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
            Debug.LogWarning("[IncountNode] AssetCacheManager.instance is null");
            return;
        }

        string textureName = "IncountNodeTexture";
        switch (incountType)
        {
            case IncountType.None:
                textureName = "IncountNodeTexture";
                break;
            case IncountType.Battle:
                switch (battleExtraRewardType)
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
            case IncountType.Elite:
                textureName = "IncountNodeEliteEnemyTexture";
                break;
            case IncountType.Boss:
                textureName = "IncountNodeBossTexture";
                break;
            case IncountType.Restore:
                textureName = "IncountNodeRestoreTexture";
                break;
            case IncountType.Store:
                textureName = "IncountNodeStoreTexture";
                break;
            case IncountType.SecretBox:
                textureName = "IncountNodeSecretBoxTexture";
                break;
            case IncountType.Secret:
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
            Debug.LogWarning($"[IncountNode] Failed to load Addressable texture: {textureName}");
        }
    }

    void SetExtraNodeTexture()
    {
        Image image = IncountNodeExtraRewardObject.GetComponent<Image>();
        if (image == null) return;

        if (AssetCacheManager.instance == null) return;

        string textureName = "IncountNodeTexture"; // default/none
        switch (extraIncountType)
        {
            case ExtraIncountType.None:
                textureName = "IncountNodeTexture";
                break;
            case ExtraIncountType.Insight:
                textureName = "IncountNodeInsightTexture";
                break;
            case ExtraIncountType.ShineWell:
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

    public void OpenNodeCoverTexture()
    {
        IncountNodeCoverObject.SetActive(false);
        if (extraIncountType != ExtraIncountType.None)
        {
            IncountNodeExtraRewardObject.SetActive(true);
        }

    }

    public void CloseNodeCoverTexture()
    {
        IncountNodeCoverObject.SetActive(true);
        IncountNodeExtraRewardObject.SetActive(false);
    }

    public void LoadMapDataFromIncountNode()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            Debug.Log("[IncountNode] NPC 대화 중에는 맵 노드를 이동할 수 없습니다.");
            return;
        }
        if (RunManager.instance.currentIncountNode.nextIncountNode.Contains(this.gameObject))
        {
            RunManager.instance.MoveToNode(this);
        }
        else
        {
            Debug.Log("This node is nextIncountNode");
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        LoadMapDataFromIncountNode();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IncountNodeHighlightCircleObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IncountNodeHighlightCircleObject.SetActive(false);
    }
}
