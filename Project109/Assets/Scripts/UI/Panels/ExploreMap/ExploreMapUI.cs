using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExploreMapUI : UIPanelBase
{
    [SerializeField] private GameObject ViewLayout;
    [SerializeField] private GameObject ArrowObjects;
    [SerializeField] private int VerticalLayoutSpacing = 60;

    [Header("Optional Notice UI")]
    [SerializeField] private TMP_Text moveBlockedNoticeText;

    [Header("Prefab")]
    [SerializeField] private GameObject NodePrefab;
    [SerializeField] private GameObject ArrowLinePrefab;
    [SerializeField] private GameObject ExploreMapVerticalLayoutPrefab;

    private struct ArrowEdgeVisual
    {
        public ExploreNode fromNode;
        public ExploreNode toNode;
        public Image lineImage;
    }

    private static readonly Color VisitedEdgeColor = new Color(0.95f, 0.88f, 0.60f, 0.90f);
    private static readonly Color NextAvailableEdgeColor = Color.white;
    private static readonly Color NextBlockedEdgeColor = new Color(0.60f, 0.60f, 0.60f, 0.70f);
    private static readonly Color FutureReachableEdgeColor = new Color(0.85f, 0.85f, 0.85f, 0.85f);
    private static readonly Color UnreachableEdgeColor = new Color(0.32f, 0.32f, 0.35f, 0.25f);

    private readonly List<List<ExploreNodeUI>> _exploreMap = new();
    private readonly List<VerticalLayoutGroup> _exploreVerticalObjects = new();
    private readonly HashSet<ExploreNode> _reachableNodes = new HashSet<ExploreNode>();
    private readonly Queue<ExploreNode> _reachabilityQueue = new Queue<ExploreNode>();
    private readonly List<ArrowEdgeVisual> _arrowEdges = new List<ArrowEdgeVisual>();
    private bool _needsArrowRebuild = false;

    public IReadOnlyList<List<ExploreNodeUI>> MapNodes => _exploreMap;

    private void Awake()
    {
        uiLayerType = UILayerType.Popup;
        blockWorldInput = true;
    }

    public void CreateExploreMap(int mapLength)
    {
        // 기존 맵이 있다면 삭제
        EraseExploreMap();

        if (RunManager.instance == null || RunManager.instance.exploreMapManager == null) return;

        ExploreMapManager mapManager = RunManager.instance.exploreMapManager;
        int currentFloor = RunManager.instance.currentExploreMapFloor;

        // SaveSystem 등을 통해 이미 맵 데이터가 있다면 GenerateMap을 생략할 수 있습니다. 
        // 일단 여기서는 맵 데이터가 비어있으면 생성하도록 합니다.
        if (mapManager.MapData == null || mapManager.MapData.floors == null || mapManager.MapData.floors.Count == 0)
        {
            mapManager.GenerateMap(mapLength, currentFloor);
        }

        ExploreMapData mapData = mapManager.MapData;
        if (mapData == null || mapData.floors == null || ViewLayout == null) return;

        int floorCount = mapData.floors.Count;

        // 노드들을 담아두는 Vertical Layout들을 미리 생성
        for (int i = 0; i < floorCount; i++)
        {
            GameObject verticalObj = GameObject.Instantiate(ExploreMapVerticalLayoutPrefab, ViewLayout.transform);
            VerticalLayoutGroup vlg = verticalObj.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = VerticalLayoutSpacing;
            _exploreVerticalObjects.Add(vlg);
        }

        // 데이터(ExploreMapData) 기반으로 UI 노드 프리팹 인스턴스화
        for (int floor = 0; floor < floorCount; floor++)
        {
            _exploreMap.Add(new List<ExploreNodeUI>());
            ExploreFloorData floorData = mapData.floors[floor];
            List<ExploreNode> floorNodes = floorData != null && floorData.nodes != null ? floorData.nodes : new List<ExploreNode>();

            for (int idx = 0; idx < floorNodes.Count; idx++)
            {
                ExploreNode nodeData = floorNodes[idx];
                ExploreNodeUI nodeUI = GameObject.Instantiate(NodePrefab, _exploreVerticalObjects[floor].transform).GetComponent<ExploreNodeUI>();
                if (nodeUI != null)
                {
                    nodeUI.ExploreMapUI = this;
                    nodeUI.InitExploreNodeUI(nodeData);
                    _exploreMap[floor].Add(nodeUI);
                }
            }

            // UI 정렬을 위한 Padding 조절
            int nodeCount = floorNodes.Count;
            if (nodeCount == 1)
            {
                _exploreVerticalObjects[floor].padding.top = 300;
            }
            else if (nodeCount == 2)
            {
                _exploreVerticalObjects[floor].padding.top = 225;
            }
            else
            {
                _exploreVerticalObjects[floor].padding.top = 375 - (nodeCount * 75);
            }
        }

        // 노드 간 시각적 연결 데이터 등록
        SetNextExploreNodeUI();

        // 시작 지점 지정 (이미 로드된 노드가 없다면)
        if (RunManager.instance.currentExploreNode == null && mapData.floors.Count > 0 && mapData.floors[0] != null && mapData.floors[0].nodes != null && mapData.floors[0].nodes.Count > 0)
        {
            RunManager.instance.currentExploreNode = mapData.floors[0].nodes[0];
        }

        if (RunManager.instance.currentExploreNode != null)
        {
            RunManager.instance.currentExploreNode.isVisited = true;
        }

        // 게임오브젝트가 활성화 상태이면 즉시 레이아웃을 갱신하고 화살표를 생성한 뒤 시각 상태를 동기화,
        // 비활성화(Inactive) 상태이면 OnOpen 시점에 화살표를 생성하도록 지연 처리
        if (gameObject.activeInHierarchy)
        {
            BuildArrowUI();
        }
        else
        {
            _needsArrowRebuild = true;
        }

        // 현재 노드 하이라이트 및 도달 가능/불가능 시각 요소 갱신
        RefreshMapVisuals();
    }

    public override void OnOpen()
    {
        uiLayerType = UILayerType.Popup;
        base.OnOpen();

        // 비활성화 상태에서 생성되어 화살표가 아직 그려지지 않았거나 재구축이 필요한 경우 생성
        if (_needsArrowRebuild)
        {
            BuildArrowUI();
        }

        RefreshMapVisuals();
    }

    /// <summary>
    /// 노드 데이터의 isRevealed 상태, 현재 플레이어 위치 하이라이트 및 이동 제한 시각화를 UI에 동기화합니다.
    /// </summary>
    public void RefreshMapVisuals()
    {
        if (RunManager.instance == null) return;

        ExploreNode currentNode = RunManager.instance.currentExploreNode;
        bool canMove = RunManager.instance.CanMoveToNextNode(out string blockReason);
        ExploreMapData mapData = RunManager.instance.exploreMapManager != null
            ? RunManager.instance.exploreMapManager.MapData
            : null;
        ExploreMapManager.CollectReachableNodes(mapData, currentNode, _reachableNodes, _reachabilityQueue);

        if (moveBlockedNoticeText != null)
        {
            moveBlockedNoticeText.gameObject.SetActive(!canMove);
            moveBlockedNoticeText.text = canMove ? string.Empty : blockReason;
        }

        // 1. 모든 노드 시각 상태(가림막 공개/비공개, 호버 링 초기화, 현재/방문/이동 가능·차단/도달 가능·불가능 음영) 동기화
        for (int f = 0; f < _exploreMap.Count; f++)
        {
            for (int i = 0; i < _exploreMap[f].Count; i++)
            {
                var nodeUI = _exploreMap[f][i];
                if (nodeUI == null) continue;

                nodeUI.UpdateVisual();

                if (nodeUI.ExploreNodeUIHighlightCircleObject != null)
                {
                    nodeUI.ExploreNodeUIHighlightCircleObject.SetActive(false);
                }

                ExploreNode nodeData = nodeUI.NodeData;
                ExploreNodeVisualState visualState = ResolveNodeVisualState(nodeData, currentNode, canMove);
                nodeUI.ApplyVisualState(visualState);
            }
        }

        // 2. 노드 간 경로 라인(화살표) 색상 동기화
        for (int i = 0; i < _arrowEdges.Count; i++)
        {
            ArrowEdgeVisual edge = _arrowEdges[i];
            if (edge.lineImage == null) continue;

            edge.lineImage.color = ResolveEdgeColor(edge.fromNode, edge.toNode, currentNode, canMove);
        }
    }

    private ExploreNodeVisualState ResolveNodeVisualState(ExploreNode nodeData, ExploreNode currentNode, bool canMove)
    {
        if (nodeData == null)
        {
            return ExploreNodeVisualState.Unreachable;
        }

        if (currentNode == null)
        {
            return nodeData.isVisited ? ExploreNodeVisualState.Visited : ExploreNodeVisualState.FutureReachable;
        }

        if (nodeData == currentNode)
        {
            return ExploreNodeVisualState.Current;
        }

        if (nodeData.isVisited)
        {
            return ExploreNodeVisualState.Visited;
        }

        bool isNextConnectedNode = nodeData.floor == currentNode.floor + 1
            && currentNode.nextNodeIndices != null
            && currentNode.nextNodeIndices.Contains(nodeData.index);
        if (isNextConnectedNode)
        {
            return canMove ? ExploreNodeVisualState.NextAvailable : ExploreNodeVisualState.NextBlocked;
        }

        if (_reachableNodes.Contains(nodeData))
        {
            return ExploreNodeVisualState.FutureReachable;
        }

        return ExploreNodeVisualState.Unreachable;
    }

    private Color ResolveEdgeColor(ExploreNode fromNode, ExploreNode toNode, ExploreNode currentNode, bool canMove)
    {
        if (fromNode == null || toNode == null)
        {
            return UnreachableEdgeColor;
        }

        if (fromNode.isVisited && toNode.isVisited)
        {
            return VisitedEdgeColor;
        }

        if (currentNode != null && fromNode == currentNode && _reachableNodes.Contains(toNode))
        {
            return canMove ? NextAvailableEdgeColor : NextBlockedEdgeColor;
        }

        if (_reachableNodes.Contains(fromNode) && _reachableNodes.Contains(toNode))
        {
            return FutureReachableEdgeColor;
        }

        return UnreachableEdgeColor;
    }

    /// <summary>
    /// 동기식으로 UI 레이아웃을 즉시 강제 갱신한 후 각 노드 간 화살표를 생성합니다.
    /// 코루틴 대기 없이 동기적으로 동작합니다.
    /// </summary>
    private void BuildArrowUI()
    {
        _needsArrowRebuild = false;

        if (ArrowObjects == null) return;

        // 기존 화살표 오브젝트들 완전 삭제 (누수 방지)
        ClearArrowUI();

        // 캔버스 레이아웃을 즉시 강제 갱신하여 자식 노드들의 RectTransform 위치를 확정
        Canvas.ForceUpdateCanvases();

        // 레이아웃이 정렬된 노드 위치 계산
        SetNodePosition();

        // 연결에 맞게 화살표 생성
        for (int i = 0; i < _exploreMap.Count; i++)
        {
            for (int j = 0; j < _exploreMap[i].Count; j++)
            {
                var fromNode = _exploreMap[i][j];
                if (fromNode == null) continue;

                foreach (GameObject nextNode in fromNode.nextExploreNodeUI)
                {
                    if (nextNode != null && nextNode.TryGetComponent<ExploreNodeUI>(out var nextNodeUI))
                    {
                        MakeArrowUI(fromNode.NodeData, nextNodeUI.NodeData, fromNode.arrowRelativePos, nextNodeUI.arrowRelativePos);
                    }
                }
            }
        }
    }

    private void MakeArrowUI(ExploreNode fromNode, ExploreNode toNode, Vector2 startPos, Vector2 endPos)
    {
        if (ArrowLinePrefab == null || ArrowObjects == null) return;

        Vector2 dir = endPos - startPos;
        float dist = dir.magnitude - 120;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        RectTransform arrowLine = GameObject.Instantiate(ArrowLinePrefab, ArrowObjects.transform).GetComponent<RectTransform>();
        if (arrowLine != null)
        {
            arrowLine.anchoredPosition = ((startPos + endPos) / 2f);
            arrowLine.sizeDelta = new Vector2(dist, 5); // 선 두께 5
            arrowLine.rotation = Quaternion.Euler(0, 0, angle);

            Image lineImage = arrowLine.GetComponent<Image>();
            if (lineImage != null)
            {
                lineImage.raycastTarget = false;
            }

            _arrowEdges.Add(new ArrowEdgeVisual
            {
                fromNode = fromNode,
                toNode = toNode,
                lineImage = lineImage
            });
        }
    }

    private void SetNodePosition()
    {
        if (ArrowObjects == null) return;
        RectTransform parentRect = ArrowObjects.GetComponent<RectTransform>();
        if (parentRect == null) return;

        for (int i = 0; i < _exploreMap.Count; i++)
        {
            for (int j = 0; j < _exploreMap[i].Count; j++)
            {
                var nodeUI = _exploreMap[i][j];
                if (nodeUI != null)
                {
                    RectTransform nodeRect = nodeUI.GetComponent<RectTransform>();
                    if (nodeRect != null)
                    {
                        Vector2 relativeVector = (Vector2)parentRect.InverseTransformPoint(nodeRect.position);
                        nodeUI.arrowRelativePos = relativeVector;
                    }
                }
            }
        }
    }

    private void EraseExploreMap()
    {
        // 1. 기존 화살표 오브젝트들 완전 삭제 (누수 방지)
        ClearArrowUI();

        // 2. 기존 노드 UI 삭제
        for (int i = 0; i < _exploreMap.Count; i++)
        {
            for (int j = 0; j < _exploreMap[i].Count; j++)
            {
                if (_exploreMap[i][j] != null)
                {
                    DestroyMapUiObject(_exploreMap[i][j].gameObject);
                }
            }
        }

        // 3. 기존 세로 레이아웃 삭제
        for (int i = 0; i < _exploreVerticalObjects.Count; i++)
        {
            if (_exploreVerticalObjects[i] != null)
            {
                DestroyMapUiObject(_exploreVerticalObjects[i].gameObject);
            }
        }

        _exploreMap.Clear();
        _exploreVerticalObjects.Clear();
        _needsArrowRebuild = false;
    }

    /// <summary>
    /// ArrowObjects 하위에 생성된 모든 화살표 인스턴스를 파괴합니다.
    /// </summary>
    private void ClearArrowUI()
    {
        _arrowEdges.Clear();
        if (ArrowObjects == null) return;

        for (int i = ArrowObjects.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = ArrowObjects.transform.GetChild(i);
            if (child != null)
            {
                DestroyMapUiObject(child.gameObject);
            }
        }
    }

    private static void DestroyMapUiObject(GameObject target)
    {
        if (target == null) return;
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    /// <summary>
    /// ExploreNodeUI의 nextExploreNodeUI에 다음 노드들 시각적 연결 등록 (Data 기반)
    /// </summary>
    private void SetNextExploreNodeUI()
    {
        for (int i = 0; i < _exploreMap.Count; i++)
        {
            for (int j = 0; j < _exploreMap[i].Count; j++)
            {
                var currentNode = _exploreMap[i][j];
                if (currentNode == null || currentNode.NodeData == null) continue;

                ExploreNode nodeData = currentNode.NodeData;
                currentNode.nextExploreNodeUI.Clear();

                foreach (int nextIndex in nodeData.nextNodeIndices)
                {
                    if (i + 1 < _exploreMap.Count && nextIndex < _exploreMap[i + 1].Count)
                    {
                        var nextNodeUI = _exploreMap[i + 1][nextIndex];
                        if (nextNodeUI != null)
                        {
                            currentNode.nextExploreNodeUI.Add(nextNodeUI.gameObject);
                        }
                    }
                }
            }
        }
    }
}
