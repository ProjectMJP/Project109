using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExploreMapUI : UIPanelBase
{
    public List<List<ExploreNodeUI>> ExploreMap = new();

    public GameObject ViewLayout;
    public GameObject ArrowObjects;
    public List<VerticalLayoutGroup> ExploreVerticalObjects;

    public int currentMapFloor = 1;

    [SerializeField] private int VerticalLayoutSpacing = 60;

    [Header("Prefab")]
    [SerializeField] private GameObject NodePrefab;
    [SerializeField] private GameObject ArrowLinePrefab;
    [SerializeField] private GameObject ArrowHeadPrefab;
    [SerializeField] private GameObject ExploreMapVerticalLayoutPrefab;


    public void CreateExploreMap(int mapLength)
    {
        //기존 맵이 있다면 삭제
        EraseExploreMap();

        ExploreMapManager mapManager = RunManager.instance.exploreMapManager;
        
        // SaveSystem 등을 통해 이미 맵 데이터가 있다면 GenerateMap을 생략할 수 있습니다. 
        // 일단 여기서는 맵 데이터가 비어있으면 생성하도록 합니다.
        if (mapManager.MapData == null || mapManager.MapData.mapNodes.Count == 0)
        {
            mapManager.GenerateMap(mapLength, currentMapFloor);
        }

        ExploreMapData mapData = mapManager.MapData;

        //노드들을 담아두는 Vertical Leyout들을 미리 담아두기
        ExploreVerticalObjects = new List<VerticalLayoutGroup>();
        for (int i = 0; i < mapLength; i++)
        {
            ExploreVerticalObjects.Add(GameObject.Instantiate(ExploreMapVerticalLayoutPrefab, ViewLayout.transform).GetComponent<VerticalLayoutGroup>());
            ExploreVerticalObjects[i].spacing = VerticalLayoutSpacing;
        }

        // 데이터(ExploreMapData) 기반으로 UI 노드 프리팹 인스턴스화
        for (int floor = 0; floor < mapData.mapNodes.Count; floor++)
        {
            ExploreMap.Add(new List<ExploreNodeUI>());
            List<ExploreNode> floorNodes = mapData.mapNodes[floor];

            for (int idx = 0; idx < floorNodes.Count; idx++)
            {
                ExploreNode nodeData = floorNodes[idx];
                ExploreNodeUI nodeUI = GameObject.Instantiate(NodePrefab, ExploreVerticalObjects[floor].transform).GetComponent<ExploreNodeUI>();
                nodeUI.ExploreMapUI = this;
                nodeUI.InitExploreNodeUI(nodeData);

                ExploreMap[floor].Add(nodeUI);
            }

            // UI 정렬을 위한 Padding 조절
            int nodeCount = floorNodes.Count;
            if (nodeCount == 1)
            {
                ExploreVerticalObjects[floor].padding.top = 300;
            }
            else if (nodeCount == 2)
            {
                ExploreVerticalObjects[floor].padding.top = 225;
            }
            else
            {
                ExploreVerticalObjects[floor].padding.top = 375 - (nodeCount * 75);
            }
        }

        //시작 지점 지정 (이미 로드된 노드가 없다면)
        if (RunManager.instance != null && RunManager.instance.currentExploreNode == null)
        {
            RunManager.instance.currentExploreNode = mapData.mapNodes[0][0];
        }

        if (RunManager.instance != null)
        {
            RunManager.instance.currentExploreUI = this;
        }

        // 현재 노드 하이라이트 및 시각 요소 갱신
        RefreshMapVisuals();

        //각 노드끼리 연결하는 Arrow생성
        StartCoroutine(CreateArrowUI());
    }

    public override void OnOpen()
    {
        base.OnOpen();
        RefreshMapVisuals();
    }

    /// <summary>
    /// 노드 데이터의 isRevealed 상태 및 현재 플레이어 위치 하이라이트를 UI에 동기화합니다.
    /// </summary>
    public void RefreshMapVisuals()
    {
        if (RunManager.instance == null) return;

        // 1. 모든 노드 시각 상태(가림막 공개/비공개 및 하이라이트 초기화) 동기화
        for (int f = 0; f < ExploreMap.Count; f++)
        {
            for (int i = 0; i < ExploreMap[f].Count; i++)
            {
                var nodeUI = ExploreMap[f][i];
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
            if (curFloor < ExploreMap.Count && curIndex < ExploreMap[curFloor].Count)
            {
                var curNodeUI = ExploreMap[curFloor][curIndex];
                if (curNodeUI != null && curNodeUI.ExploreNodeUICurrentHighlightCircleObject != null)
                {
                    curNodeUI.ExploreNodeUICurrentHighlightCircleObject.SetActive(true);
                }
            }
        }

        // 3. 이전 층에서 지나치지 않은 노드 닫기
        CloseBeforeNodes();
    }

    public void OpenAllExploreMapNodes()
    {
        if (RunManager.instance != null && RunManager.instance.exploreMapManager != null)
        {
            RunManager.instance.exploreMapManager.RevealAllNodes();
        }
        RefreshMapVisuals();
    }

    public void OpenExploreMapNodesBasedOnFloorLength()
    {
        if (RunManager.instance != null && RunManager.instance.exploreMapManager != null)
        {
            int currentFloor = RunManager.instance.currentExploreMapFloor;
            int checkLength = GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null
                ? GameSceneManager.instance.player.playerStat.MapFloorCheckLength
                : 3;
            RunManager.instance.exploreMapManager.RevealNodesByFloor(currentFloor, checkLength);
        }
        RefreshMapVisuals();
    }

    public void OpenRandomExploreMapNode()
    {
        if (RunManager.instance != null && RunManager.instance.exploreMapManager != null)
        {
            RunManager.instance.exploreMapManager.RevealRandomNode();
        }
        RefreshMapVisuals();
    }

    public void CloseBeforeNodes()
    {
        if (RunManager.instance == null) return;

        int beforeFloor = RunManager.instance.currentExploreMapFloor - 1;

        if (beforeFloor >= 0 && beforeFloor < ExploreMap.Count)
        {
            for (int i = 0; i < ExploreMap[beforeFloor].Count; i++)
            {
                // RunManager의 beforeExploreNode와 UI가 맵핑된 NodeData를 비교
                if (ExploreMap[beforeFloor][i] != null && ExploreMap[beforeFloor][i].NodeData != RunManager.instance.beforeExploreNode)
                {
                    ExploreMap[beforeFloor][i].HideNode();
                }
            }
        }
    }

    IEnumerator CreateArrowUI()
    {
        yield return new WaitForEndOfFrame();

        //레이아웃의 정렬이 끝난 후인 다음 프레임에 위치 계산 실행
        SetNodePosition();

        //각 노드 간의 연결 계산
        SetNextExploreNodeUI();

        //연결에 맞게 화살표 생성
        for (int i = 0; i < ExploreMap.Count; i++)
        {
            for (int j = 0; j < ExploreMap[i].Count; j++)
            {
                foreach (GameObject nextNode in ExploreMap[i][j].nextExploreNodeUI)
                {
                    MakeArrowUI(ExploreMap[i][j].arrowRelativePos, nextNode.transform.GetComponent<ExploreNodeUI>().arrowRelativePos);
                }
            }
        }
    }

    void MakeArrowUI(Vector2 startPos, Vector2 endPos)
    {
        Vector2 offset = (endPos - startPos).normalized * 50;
        Vector2 dir = endPos - startPos;
        float dist = dir.magnitude - 120;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // Debug.Log("StartPos : " + startPos + ", EndPos : " + endPos + ", Position : " + ((startPos + endPos) / 2f).ToString());

        if (ArrowLinePrefab != null)
        {
            RectTransform arrowLine = GameObject.Instantiate(ArrowLinePrefab, ArrowObjects.transform).GetComponent<RectTransform>();

            arrowLine.anchoredPosition = ((startPos + endPos) / 2f);
            arrowLine.sizeDelta = new Vector2(dist, 5); //선 두께 5
            arrowLine.rotation = Quaternion.Euler(0, 0, angle);
        }

        /*if (ArrowHeadPrefab != null)
        {
            RectTransform arrowHead = GameObject.Instantiate(ArrowHeadPrefab, ArrowObjects.transform).GetComponent<RectTransform>();
            arrowHead.anchoredPosition = endPos - (offset * 1.4f);
            arrowHead.rotation = Quaternion.Euler(0, 0, angle);
        }*/
    }


    void SetNodePosition()
    {
        RectTransform parentRect = ArrowObjects.GetComponent<RectTransform>();

        for (int i = 0; i < ExploreMap.Count; i++)
        {
            for (int j = 0; j < ExploreMap[i].Count; j++)
            {
                Vector2 relativeVector = (Vector2)parentRect.InverseTransformPoint(ExploreMap[i][j].transform.GetComponent<RectTransform>().position);
                ExploreMap[i][j].arrowRelativePos = relativeVector;
            }
        }
    }

    void EraseExploreMap()
    {
        for (int i = 0; i < ExploreMap.Count; i++)
        {
            for (int j = 0; j < ExploreMap[i].Count; j++)
            {
                GameObject.Destroy(ExploreMap[i][j].gameObject);
            }
        }

        for (int i = 0; i < ExploreVerticalObjects.Count; i++)
        {
            GameObject.Destroy(ExploreVerticalObjects[i].gameObject);
        }

        ExploreMap.Clear();
        ExploreVerticalObjects.Clear();
    }

    /// <summary>
    /// ExploreNodeUI의 nextExploreNodeUI에 다음 노드들 시각적 연결 등록 (Data 기반)
    /// </summary>
    void SetNextExploreNodeUI()
    {
        for (int i = 0; i < ExploreMap.Count; i++)
        {
            for (int j = 0; j < ExploreMap[i].Count; j++)
            {
                ExploreNode nodeData = ExploreMap[i][j].NodeData;
                ExploreMap[i][j].nextExploreNodeUI.Clear();

                foreach (int nextIndex in nodeData.nextNodeIndices)
                {
                    if (i + 1 < ExploreMap.Count && nextIndex < ExploreMap[i + 1].Count)
                    {
                        ExploreMap[i][j].nextExploreNodeUI.Add(ExploreMap[i + 1][nextIndex].gameObject);
                    }
                }
            }
        }
    }
}
