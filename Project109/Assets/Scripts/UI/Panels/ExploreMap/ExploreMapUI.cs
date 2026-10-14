using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExploreMapUI : UIPanelBase
{
    [SerializeField] private GameObject ViewLayout;
    [SerializeField] private GameObject ArrowObjects;
    [SerializeField] private int VerticalLayoutSpacing = 60;

    [Header("Prefab")]
    [SerializeField] private GameObject NodePrefab;
    [SerializeField] private GameObject ArrowLinePrefab;
    [SerializeField] private GameObject ExploreMapVerticalLayoutPrefab;

    private readonly List<List<ExploreNodeUI>> _exploreMap = new();
    private readonly List<VerticalLayoutGroup> _exploreVerticalObjects = new();
    private bool _needsArrowRebuild = false;

    public IReadOnlyList<List<ExploreNodeUI>> MapNodes => _exploreMap;

    public void CreateExploreMap(int mapLength)
    {
        // 기존 맵이 있다면 삭제
        EraseExploreMap();

        if (RunManager.instance == null || RunManager.instance.exploreMapManager == null) return;

        ExploreMapManager mapManager = RunManager.instance.exploreMapManager;
        int currentFloor = RunManager.instance.currentExploreMapFloor;

        // SaveSystem 등을 통해 이미 맵 데이터가 있다면 GenerateMap을 생략할 수 있습니다. 
        // 일단 여기서는 맵 데이터가 비어있으면 생성하도록 합니다.
        if (mapManager.MapData == null || mapManager.MapData.mapNodes.Count == 0)
        {
            mapManager.GenerateMap(mapLength, currentFloor);
        }

        ExploreMapData mapData = mapManager.MapData;
        if (mapData == null || ViewLayout == null) return;

        // 노드들을 담아두는 Vertical Layout들을 미리 생성
        for (int i = 0; i < mapLength; i++)
        {
            GameObject verticalObj = GameObject.Instantiate(ExploreMapVerticalLayoutPrefab, ViewLayout.transform);
            VerticalLayoutGroup vlg = verticalObj.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = VerticalLayoutSpacing;
            _exploreVerticalObjects.Add(vlg);
        }

        // 데이터(ExploreMapData) 기반으로 UI 노드 프리팹 인스턴스화
        for (int floor = 0; floor < mapData.mapNodes.Count; floor++)
        {
            _exploreMap.Add(new List<ExploreNodeUI>());
            List<ExploreNode> floorNodes = mapData.mapNodes[floor];

            for (int idx = 0; idx < floorNodes.Count; idx++)
            {
                ExploreNode nodeData = floorNodes[idx];
                ExploreNodeUI nodeUI = GameObject.Instantiate(NodePrefab, _exploreVerticalObjects[floor].transform).GetComponent<ExploreNodeUI>();
                nodeUI.InitExploreNodeUI(nodeData);

                _exploreMap[floor].Add(nodeUI);
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
        if (RunManager.instance.currentExploreNode == null && mapData.mapNodes.Count > 0 && mapData.mapNodes[0].Count > 0)
        {
            RunManager.instance.currentExploreNode = mapData.mapNodes[0][0];
        }

        // 현재 노드 하이라이트 및 시각 요소 갱신
        RefreshMapVisuals();

        // 게임오브젝트가 활성화 상태이면 즉시 레이아웃을 갱신하고 화살표 생성,
        // 비활성화(Inactive) 상태이면 OnOpen 시점에 화살표를 생성하도록 지연 처리
        if (gameObject.activeInHierarchy)
        {
            BuildArrowUI();
        }
        else
        {
            _needsArrowRebuild = true;
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();

        // 비활성화 상태에서 생성되어 화살표가 아직 그려지지 않았거나 재구축이 필요한 경우 생성
        if (_needsArrowRebuild)
        {
            BuildArrowUI();
        }

        RefreshMapVisuals();
    }

    /// <summary>
    /// 노드 데이터의 isRevealed 상태 및 현재 플레이어 위치 하이라이트를 UI에 동기화합니다.
    /// </summary>
    public void RefreshMapVisuals()
    {
        if (RunManager.instance == null) return;

        // 1. 모든 노드 시각 상태(가림막 공개/비공개 및 하이라이트 초기화) 동기화
        for (int f = 0; f < _exploreMap.Count; f++)
        {
            for (int i = 0; i < _exploreMap[f].Count; i++)
            {
                var nodeUI = _exploreMap[f][i];
                if (nodeUI != null)
                {
                    nodeUI.UpdateVisual();

                    if (nodeUI.ExploreNodeUICurrentHighlightCircleObject != null)
                    {
                        nodeUI.ExploreNodeUICurrentHighlightCircleObject.SetActive(false);
                    }
                }
            }
        }

        // 2. 현재 노드 하이라이트 활성화
        if (RunManager.instance.currentExploreNode != null)
        {
            int curFloor = RunManager.instance.currentExploreNode.floor;
            int curIndex = RunManager.instance.currentExploreNode.index;
            if (curFloor < _exploreMap.Count && curIndex < _exploreMap[curFloor].Count)
            {
                var curNodeUI = _exploreMap[curFloor][curIndex];
                if (curNodeUI != null && curNodeUI.ExploreNodeUICurrentHighlightCircleObject != null)
                {
                    curNodeUI.ExploreNodeUICurrentHighlightCircleObject.SetActive(true);
                }
            }
        }

        // 3. 이전 층에서 지나치지 않은 노드 닫기
        CloseBeforeNodes();
    }

    public void CloseBeforeNodes()
    {
        if (RunManager.instance == null) return;

        int beforeFloor = RunManager.instance.currentExploreMapFloor - 1;

        if (beforeFloor >= 0 && beforeFloor < _exploreMap.Count)
        {
            for (int i = 0; i < _exploreMap[beforeFloor].Count; i++)
            {
                // RunManager의 beforeExploreNode와 UI가 맵핑된 NodeData를 비교
                if (_exploreMap[beforeFloor][i] != null && _exploreMap[beforeFloor][i].NodeData != RunManager.instance.beforeExploreNode)
                {
                    _exploreMap[beforeFloor][i].HideNode();
                }
            }
        }
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
                        MakeArrowUI(fromNode.arrowRelativePos, nextNodeUI.arrowRelativePos);
                    }
                }
            }
        }
    }

    private void MakeArrowUI(Vector2 startPos, Vector2 endPos)
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
                    GameObject.Destroy(_exploreMap[i][j].gameObject);
                }
            }
        }

        // 3. 기존 세로 레이아웃 삭제
        for (int i = 0; i < _exploreVerticalObjects.Count; i++)
        {
            if (_exploreVerticalObjects[i] != null)
            {
                GameObject.Destroy(_exploreVerticalObjects[i].gameObject);
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
        if (ArrowObjects == null) return;

        for (int i = ArrowObjects.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = ArrowObjects.transform.GetChild(i);
            if (child != null)
            {
                GameObject.Destroy(child.gameObject);
            }
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
