using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum ExploreNodeVisualState
{
    Current,
    Visited,
    NextAvailable,
    NextBlocked,
    FutureReachable,
    Unreachable
}

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

    private static readonly Color CurrentNodeColor = Color.white;
    private static readonly Color VisitedNodeColor = new Color(0.78f, 0.78f, 0.78f, 0.92f);
    private static readonly Color NextAvailableNodeColor = Color.white;
    private static readonly Color BlockedNodeColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
    private static readonly Color FutureReachableNodeColor = new Color(0.92f, 0.92f, 0.92f, 0.95f);
    private static readonly Color UnreachableNodeColor = new Color(0.32f, 0.32f, 0.35f, 0.42f);
    private static readonly Color CurrentRingColor = new Color(1f, 0.90f, 0.35f, 1f);
    private static readonly Color VisitedRingColor = new Color(0.75f, 0.75f, 0.75f, 0.75f);

    private Image _cachedImage;
    private Image _coverImage;
    private Image _extraRewardImage;
    private Image _currentHighlightImage;
    private bool _isMoveBlocked;
    private bool _isShowingMoveBlockedTooltip;

    public ExploreNodeVisualState CurrentVisualState { get; private set; } = ExploreNodeVisualState.FutureReachable;

    /// <summary>
    /// 현재 노드가 전투 또는 대화 제한으로 인해 이동 차단 상태인지 여부입니다.
    /// </summary>
    public bool IsMoveBlocked => _isMoveBlocked;

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

    /// <summary>
    /// 현재 탐험 진행 기준의 노드 시각 상태(현재 위치, 방문 완료, 다음 이동 가능/차단, 향후 도달 가능, 도달 불가능 음영)를 적용합니다.
    /// </summary>
    public void ApplyVisualState(ExploreNodeVisualState state)
    {
        CurrentVisualState = state;
        _isMoveBlocked = (state == ExploreNodeVisualState.NextBlocked);

        Color targetColor = ResolveNodeColor(state);

        if (_cachedImage == null)
        {
            _cachedImage = GetComponent<Image>();
        }
        if (_cachedImage != null)
        {
            _cachedImage.color = targetColor;
        }

        if (ExploreNodeUICoverObject != null)
        {
            if (_coverImage == null)
            {
                _coverImage = ExploreNodeUICoverObject.GetComponent<Image>();
            }
            if (_coverImage != null)
            {
                _coverImage.color = targetColor;
            }
        }

        if (ExploreNodeUIExtraRewardObject != null)
        {
            if (_extraRewardImage == null)
            {
                _extraRewardImage = ExploreNodeUIExtraRewardObject.GetComponent<Image>();
            }
            if (_extraRewardImage != null)
            {
                _extraRewardImage.color = targetColor;
            }
        }

        if (ExploreNodeUICurrentHighlightCircleObject != null)
        {
            bool showRing = state == ExploreNodeVisualState.Current || state == ExploreNodeVisualState.Visited;
            ExploreNodeUICurrentHighlightCircleObject.SetActive(showRing);
            if (showRing)
            {
                if (_currentHighlightImage == null)
                {
                    _currentHighlightImage = ExploreNodeUICurrentHighlightCircleObject.GetComponent<Image>();
                }
                if (_currentHighlightImage != null)
                {
                    _currentHighlightImage.color = state == ExploreNodeVisualState.Current ? CurrentRingColor : VisitedRingColor;
                }
            }
        }

        if (state == ExploreNodeVisualState.NextBlocked ||
            state == ExploreNodeVisualState.Unreachable ||
            state == ExploreNodeVisualState.Current ||
            state == ExploreNodeVisualState.Visited)
        {
            if (ExploreNodeUIHighlightCircleObject != null)
            {
                ExploreNodeUIHighlightCircleObject.SetActive(false);
            }
        }

        if (!_isMoveBlocked)
        {
            HideMoveBlockedTooltip();
        }
    }

    private static Color ResolveNodeColor(ExploreNodeVisualState state)
    {
        switch (state)
        {
            case ExploreNodeVisualState.Current:
                return CurrentNodeColor;
            case ExploreNodeVisualState.Visited:
                return VisitedNodeColor;
            case ExploreNodeVisualState.NextAvailable:
                return NextAvailableNodeColor;
            case ExploreNodeVisualState.NextBlocked:
                return BlockedNodeColor;
            case ExploreNodeVisualState.Unreachable:
                return UnreachableNodeColor;
            default:
                return FutureReachableNodeColor;
        }
    }

    public void LoadMapDataFromExploreNodeUI()
    {
        if (RunManager.instance == null) return;

        if (!RunManager.instance.CanMoveToNextNode(out string blockReason))
        {
            Debug.Log($"[ExploreNodeUI] 노드 이동 불가: {blockReason}");
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.ShowTooltip("이동 불가", blockReason, transform as RectTransform);
                _isShowingMoveBlockedTooltip = true;
            }
            return;
        }

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
        bool allowHoverHighlight = CurrentVisualState == ExploreNodeVisualState.NextAvailable ||
                                   CurrentVisualState == ExploreNodeVisualState.FutureReachable;
        if (!allowHoverHighlight)
        {
            if (ExploreNodeUIHighlightCircleObject != null)
            {
                ExploreNodeUIHighlightCircleObject.SetActive(false);
            }
            return;
        }

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

        HideMoveBlockedTooltip();
    }

    private void OnDisable()
    {
        if (ExploreNodeUIHighlightCircleObject != null)
        {
            ExploreNodeUIHighlightCircleObject.SetActive(false);
        }

        HideMoveBlockedTooltip();
    }

    private void HideMoveBlockedTooltip()
    {
        if (!_isShowingMoveBlockedTooltip)
        {
            return;
        }

        _isShowingMoveBlockedTooltip = false;
        if (TooltipManager.HasInstance && TooltipManager.Instance.gameObject.activeSelf)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}
