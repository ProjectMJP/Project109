using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExploreUI : UIPanelBase
{
    public List<List<IncountNode>> ExploreMap = new();

    //SO데이터 및 랜덤으로 선택된 데이터들
    AssetCacheManager dataLoader;
    RandomItemPicker<BattleData> battleItemPicker;
    RandomItemPicker<InteractableData> eventItemPicker;

    public GameObject ViewLayout;
    public GameObject ArrowObjects;
    public List<VerticalLayoutGroup> ExploreVerticalObjects;

    //public int mapLength;
    public int currentMapFloor = 1;

    [SerializeField] private int VerticalLayoutSpacing = 60;

    [Header("Map Setting")]
    [SerializeField] private int StoreNumber = 1;  //맵에 등장하는 상점 갯수
    [SerializeField] private int RestoreNumber = 2;  //맵에 등장하는 휴식 갯수
    [SerializeField] private int SecretNumber = 4;  //맵에 등장하는 시크릿 갯수
    [SerializeField] private int BoxNumber = 1;  //맵에 등장하는 상자 갯수
    [SerializeField] private int EliteNumber = 2;  //맵에 등장하는 엘리트 갯수

    [Header("Prefab")]
    [SerializeField] private GameObject NodePrefab;
    [SerializeField] private GameObject ArrowLinePrefab;
    [SerializeField] private GameObject ArrowHeadPrefab;
    [SerializeField] private GameObject ExploreMapVerticalLayoutPrefab;

    void Awake()
    {
        dataLoader = AssetCacheManager.instance;
        if (dataLoader != null)
        {
            battleItemPicker = new RandomItemPicker<BattleData>(dataLoader.battleList ?? new List<BattleData>());
        }
    }
    /// <summary>
    /// Vertical Layout의 padding시 노드가 2개 이상일 경우 390 - (노드의 갯수 * 65)만큼 top에 더해주면 중심이 맞게 정렬됨
    /// 1개일 경우는 325로 고정
    /// </summary>
    public void CreateExploreMap(int mapLength)
    {
        //기존 맵이 있다면 삭제
        EraseExploreMap();

        if (ModLoader.Instance != null)
        {
            eventItemPicker = new RandomItemPicker<InteractableData>(ModLoader.Instance.GetRandomInteractablesForFloor(currentMapFloor));
            Debug.Log($"[ExploreUI] Current Floor: {currentMapFloor}, Interactable Count: {eventItemPicker.Count()}");
        }
        else
        {
            Debug.LogWarning("[ExploreUI] ModLoader is not initialized! Using empty list for interactables.");
            eventItemPicker = new RandomItemPicker<InteractableData>(new List<InteractableData>());
        }

        List<List<IncountNode>> incountNodeListInSection = new List<List<IncountNode>>();

        //노드들을 담아두는 Vertical Leyout들을 미리 담아두기
        ExploreVerticalObjects = new List<VerticalLayoutGroup>();
        for (int i = 0; i < mapLength; i++)
        {
            ExploreVerticalObjects.Add(GameObject.Instantiate(ExploreMapVerticalLayoutPrefab, ViewLayout.transform).GetComponent<VerticalLayoutGroup>());
            ExploreVerticalObjects[i].spacing = VerticalLayoutSpacing;
        }

        int sectionIndex = 0;

        incountNodeListInSection.Add(new List<IncountNode>());

        for (int index = 0; index < mapLength; index++)
        {
            ExploreMap.Add(new List<IncountNode>());

            if (index == 0)  //처음 노드는 무조건 None으로 생성
            {
                IncountNode node = GameObject.Instantiate(NodePrefab, ExploreVerticalObjects[index].transform).GetComponent<IncountNode>();
                node.exploreUI = this;
                node.SetIncountNode(IncountType.None, ExtraIncountType.None);
                ExploreMap[index].Add(node);
                ExploreVerticalObjects[index].padding.top = 300;
            }
            else if (index == mapLength - 1) //마지막 노드는 무조건 Boss로 생성
            {
                IncountNode node = GameObject.Instantiate(NodePrefab, ExploreVerticalObjects[index].transform).GetComponent<IncountNode>();
                node.exploreUI = this;
                node.SetIncountNode(IncountType.Boss, ExtraIncountType.None);
                ExploreMap[index].Add(node);
                ExploreVerticalObjects[index].padding.top = 300;
            }
            else if (index == mapLength / 2) //맵 중간에 회복 및 상점 위치 생성
            {
                IncountNode node = GameObject.Instantiate(NodePrefab, ExploreVerticalObjects[index].transform).GetComponent<IncountNode>();
                node.exploreUI = this;
                node.SetIncountNode(IncountType.Restore, ExtraIncountType.None);
                ExploreMap[index].Add(node);
                ExploreVerticalObjects[index].padding.top = 225;

                IncountNode node1 = GameObject.Instantiate(NodePrefab, ExploreVerticalObjects[index].transform).GetComponent<IncountNode>();
                node1.exploreUI = this;
                node1.SetIncountNode(IncountType.Store, ExtraIncountType.None);
                ExploreMap[index].Add(node1);
                ExploreVerticalObjects[index].padding.top = 225;

                //다음 섹션의 노드 저장을 위해 List추가
                incountNodeListInSection.Add(new List<IncountNode>());
                sectionIndex++;
            }
            else //나머지 노드는 랜덤 갯수에 인카운트 노드 생성
            {
                int createNodeCount = Random.Range(3, 5);
                //정해진 수 만큼 랜덤한 인카운터 생성
                for (int mapIndex = 0; mapIndex < createNodeCount; mapIndex++)
                {
                    IncountNode node = GameObject.Instantiate(NodePrefab, ExploreVerticalObjects[index].transform).GetComponent<IncountNode>();
                    node.exploreUI = this;
                    node.SetIncountNode(IncountType.Battle, ExtraIncountType.None);
                    ExploreMap[index].Add(node);
                    ExploreVerticalObjects[index].padding.top = 375 - (createNodeCount * 75);

                    //섹션 내의 노드들 저장
                    incountNodeListInSection[sectionIndex].Add(node);
                }
            }
        }

        //보스 전에는 무조견 휴식 존재
        foreach (IncountNode node in ExploreMap[mapLength - 2])
        {
            node.SetIncountNode(IncountType.Restore, ExtraIncountType.None);
        }

        /// <summary>
        /// 섹션 내의 노드들 중에서 특정 노드들로 변경
        /// 추가되는 노드 종류는 엘리트, 시크릿, 박스, 휴식, 상점으로 총 5가지
        /// </summary>
        for (int sectionIdx = 0; sectionIdx < incountNodeListInSection.Count; sectionIdx++)
        {
            IncountNode currentNode;

            //천리안 노드 생성 (스테이지 당 1개만 존재)
            //일반 전투인 노드들 중 랜덤으로 한 개 선택
            if (sectionIdx == 0)
            {
                do
                {
                    //초반 부분에만 생성되도록 주의
                    currentNode = incountNodeListInSection[sectionIdx][Random.Range(incountNodeListInSection[sectionIdx].Count / 3, incountNodeListInSection[sectionIdx].Count)];
                }
                while (currentNode.incountType != IncountType.Battle || currentNode.isNodeChanged);
                currentNode.SetIncountNode(IncountType.Battle, ExtraIncountType.Insight);
                currentNode.isNodeChanged = true;   //노드 생성이 완료된 노드는 이후에 변경되지 않도록 설정

                //이벤트 데이터도 저장
                if (ModLoader.Instance != null && ModLoader.Instance.InteractableDatabase.TryGetValue("Insight_Event_Data", out InteractableData data))
                {
                    currentNode.eventNodeData = data;
                }
            }

            //상자 생성
            for (int index = 0; index < BoxNumber; index++)
            {
                //일반 전투인 노드들 중 랜덤으로 한 개 선택
                do
                {
                    currentNode = incountNodeListInSection[sectionIdx][Random.Range(incountNodeListInSection[sectionIdx].Count / 2, incountNodeListInSection[sectionIdx].Count)];
                }
                while (currentNode.incountType != IncountType.Battle || currentNode.isNodeChanged);

                currentNode.SetIncountNode(IncountType.SecretBox, ExtraIncountType.None);
            }

            //엘리트 적 생성
            for (int index = 0; index < EliteNumber; index++)
            {
                //일반 전투인 노드들 중 랜덤으로 한 개 선택
                do
                {
                    //초반 부분에는 생성하지 않도록 주의
                    if (sectionIdx == 0)
                    {
                        currentNode = incountNodeListInSection[sectionIdx][Random.Range(incountNodeListInSection[sectionIdx].Count / 3, incountNodeListInSection[sectionIdx].Count)];
                    }
                    else
                    {
                        currentNode = incountNodeListInSection[sectionIdx][Random.Range(0, incountNodeListInSection[sectionIdx].Count)];
                    }
                }
                while (currentNode.incountType != IncountType.Battle || currentNode.isNodeChanged);

                currentNode.SetIncountNode(IncountType.Elite, ExtraIncountType.None);
            }

            //휴식 생성
            for (int index = 0; index < RestoreNumber; index++)
            {
                //일반 전투인 노드들 중 랜덤으로 한 개 선택
                do
                {
                    currentNode = incountNodeListInSection[sectionIdx][Random.Range(incountNodeListInSection[sectionIdx].Count / 3, incountNodeListInSection[sectionIdx].Count)];
                }
                while (currentNode.incountType != IncountType.Battle || currentNode.isNodeChanged);

                currentNode.SetIncountNode(IncountType.Restore, ExtraIncountType.None);
            }

            //상점 생성
            for (int index = 0; index < StoreNumber; index++)
            {
                //일반 전투인 노드들 중 랜덤으로 한 개 선택
                do
                {
                    currentNode = incountNodeListInSection[sectionIdx][Random.Range(0, incountNodeListInSection[sectionIdx].Count)];
                }
                while (currentNode.incountType != IncountType.Battle || currentNode.isNodeChanged);

                currentNode.SetIncountNode(IncountType.Store, ExtraIncountType.None);
            }

            //시크릿 생성
            for (int index = 0; index < SecretNumber; index++)
            {
                //일반 전투인 노드들 중 랜덤으로 한 개 선택
                do
                {
                    currentNode = incountNodeListInSection[sectionIdx][Random.Range(0, incountNodeListInSection[sectionIdx].Count)];
                }
                while (currentNode.incountType != IncountType.Battle || currentNode.isNodeChanged);

                currentNode.SetIncountNode(IncountType.Secret, ExtraIncountType.None);

                //랜덤하게 섞인 데이터들 중 한 가지를 저장
                if (eventItemPicker.TryGetNext(out InteractableData data))
                {
                    currentNode.eventNodeData = data;
                }
                else
                {
                    eventItemPicker.Reset();
                    if (eventItemPicker.TryGetNext(out InteractableData newData))
                    {
                        currentNode.eventNodeData = newData;
                    }
                }
            }
        }

        // 일반 전투 노드에 추가 보상 무작위 배정 (1/3 확률씩)
        for (int i = 0; i < ExploreMap.Count; i++)
        {
            for (int j = 0; j < ExploreMap[i].Count; j++)
            {
                if (ExploreMap[i][j].incountType == IncountType.Battle)
                {
                    int randomVal = Random.Range(0, 3);
                    BattleExtraRewardType rewardType = BattleExtraRewardType.Card;
                    if (randomVal == 1) rewardType = BattleExtraRewardType.Relic;
                    else if (randomVal == 2) rewardType = BattleExtraRewardType.Gold;

                    ExploreMap[i][j].SetIncountNode(IncountType.Battle, ExploreMap[i][j].extraIncountType, rewardType);
                }
            }
        }

        //생성 시 필요한 만큼 노드 가리기
        //중간 지점의 휴식, 상점 2개의 노드만 있는 곳은 가리지 않기
        int checkLength = GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null
            ? GameSceneManager.instance.player.playerStat.MapFloorCheckLength
            : 3;
        for (int i = checkLength; i < ExploreMap.Count; i++)
        {
            if (mapLength / 2 != i)
            {
                for (int j = 0; j < ExploreMap[i].Count; j++)
                {
                    ExploreMap[i][j].CloseNodeCoverTexture();
                }
            }
        }

        SetBattleNodeData();

        //시작 지점 저장
        RunManager.instance.currentIncountNode = ExploreMap[0][0];
        ExploreMap[0][0].IncountNodeCurrentHighlightCircleObject.SetActive(true);

        //RunManager에 현재 ExploreMap 저장
        RunManager.instance.currentExploreUI = this;

        //각 노드끼리 연결하는 Arrow생성
        StartCoroutine(CreateArrowUI());
    }

    //몬스터 종류, 위치 등 여러 전투 맵의 데이터를 전투 노드에 랜덤으로 저장
    void SetBattleNodeData()
    {
        for (int i = 0; i < ExploreMap.Count; i++)
        {
            for (int j = 0; j < ExploreMap[i].Count; j++)
            {
                if (ExploreMap[i][j].incountType == IncountType.Battle)
                {
                    //나중에 스테이지 별로 다양한 데이터가 생기면 이에 맞게 변경 예정
                    //랜덤하게 섞인 데이터들 중 한 가지를 저장
                    if (battleItemPicker.TryGetNext(out BattleData data))
                    {
                        ExploreMap[i][j].battleNodeData = data;
                    }
                    else
                    {
                        battleItemPicker.Reset();
                        if (battleItemPicker.TryGetNext(out BattleData newData))
                        {
                            ExploreMap[i][j].battleNodeData = newData;
                        }
                    }
                }
            }
        }
    }

    public void OpenAllExploreMapNodes()
    {
        int currentFloor = RunManager.instance.currentExploreMapFloor;

        for (int i = currentFloor; i < ExploreMap.Count; i++)
        {
            for (int j = 0; j < ExploreMap[i].Count; j++)
            {
                ExploreMap[i][j].OpenNodeCoverTexture();
            }
        }
    }

    public void OpenExploreMapNodesBasedOnFloorLength()
    {
        int currentFloor = RunManager.instance != null ? RunManager.instance.currentExploreMapFloor : 0;
        int checkLength = GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null
            ? GameSceneManager.instance.player.playerStat.MapFloorCheckLength
            : 3;
        int openNodeLength = currentFloor + checkLength;
        openNodeLength = openNodeLength > ExploreMap.Count ? ExploreMap.Count : openNodeLength;

        for (int i = currentFloor; i < openNodeLength; i++)
        {
            for (int j = 0; j < ExploreMap[i].Count; j++)
            {
                ExploreMap[i][j].OpenNodeCoverTexture();
            }
        }
    }

    public void OpenRandomExploreMapNode()
    {
        int x = Random.Range(0, ExploreMap.Count);
        int y = Random.Range(0, ExploreMap[x].Count);

        ExploreMap[x][y].OpenNodeCoverTexture();
    }

    public void CloseBeforeNodes()
    {
        int beforeFloor = RunManager.instance.currentExploreMapFloor - 1;

        for (int i = 0; i < ExploreMap[beforeFloor].Count; i++)
        {
            if (ExploreMap[beforeFloor][i] != RunManager.instance.beforeIncountNode)
            {
                ExploreMap[beforeFloor][i].CloseNodeCoverTexture();
            }
        }
    }

    IEnumerator CreateArrowUI()
    {
        yield return new WaitForEndOfFrame();

        //레이아웃의 정렬이 끝난 후인 다음 프레임에 위치 계산 실행
        SetNodePosition();

        //각 노드 간의 연결 계산
        SetNextIncountNode();

        //연결에 맞게 화살표 생성
        for (int i = 0; i < ExploreMap.Count; i++)
        {
            for (int j = 0; j < ExploreMap[i].Count; j++)
            {
                foreach (GameObject nextNode in ExploreMap[i][j].nextIncountNode)
                {
                    MakeArrowUI(ExploreMap[i][j].arrowRelativePos, nextNode.transform.GetComponent<IncountNode>().arrowRelativePos);
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
    /// IncountNode의 nextIncountNode에 다음 노드들 등록
    /// </summary>
    void SetNextIncountNode()
    {
        for (int i = 0; i < ExploreMap.Count - 1; i++)
        {
            if (ExploreMap[i].Count == 1)
            {
                int currentNodeCount = 0;
                int nextNodeCount = 0;
                while (nextNodeCount < ExploreMap[i + 1].Count)
                {
                    ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount].gameObject);
                    nextNodeCount++;
                }
            }
            else if (ExploreMap[i + 1].Count == 1)
            {
                int currentNodeCount = 0;
                int nextNodeCount = 0;
                while (currentNodeCount < ExploreMap[i].Count)
                {
                    ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount].gameObject);
                    currentNodeCount++;
                }
            }
            else if (ExploreMap[i].Count < ExploreMap[i + 1].Count)  //왼쪽 노드가 오른쪽 노드보다 작을 경우
            {
                int currentNodeCount = 0;
                int nextNodeCount = 0;
                //다수의 노드에게 선택될 노드 한 개를 랜덤으로 선택
                int randomNodeNumber = Random.Range(0, ExploreMap[i].Count);

                while (currentNodeCount < ExploreMap[i].Count && nextNodeCount < ExploreMap[i + 1].Count)
                {
                    if (currentNodeCount == randomNodeNumber)
                    {
                        //왼쪽 노드의 남은 수 + 1 = 오른쪽 노드의 남은 수가 될 때 까지 왼쪽 노드를 내려가며 등록
                        while (ExploreMap[i].Count - currentNodeCount + 1 < ExploreMap[i + 1].Count - nextNodeCount)
                        {
                            ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount].gameObject);
                            nextNodeCount++;
                        }
                    }

                    ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount].gameObject);
                    //맨 처음 노드와 마지막 노드가 아닐 경우 50%의 확률로 아래 노드와 연결됨
                    //이전에 아래 노드와 연결이 안됬을 경우는 무조건 연결됨
                    if (nextNodeCount + 1 < ExploreMap[i + 1].Count)
                    {
                        if (Random.Range(0, 10) % 2 == 0 || currentNodeCount == 0 || currentNodeCount == ExploreMap[i].Count - 1)
                        {
                            ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount + 1].gameObject);
                            nextNodeCount++;
                        }
                        else
                        {
                            nextNodeCount++;
                        }
                    }

                    //만약 마지막까지 연결이 안된 노드가 존재 시 마지막 노드에 강제로 연결해줌
                    if (currentNodeCount == ExploreMap[i].Count - 1 && nextNodeCount < ExploreMap[i + 1].Count - 1)
                    {
                        while (nextNodeCount < ExploreMap[i + 1].Count)
                        {
                            ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount].gameObject);
                            nextNodeCount++;
                        }
                    }

                    currentNodeCount++;
                }
            }
            else  //왼쪽 노드가 오른쪽 노드보다 클 경우
            {
                int currentNodeCount = 0;
                int nextNodeCount = 0;
                bool recentlyIgnoreNode = false;
                //다수의 노드에게 선택될 노드 한 개를 랜덤으로 선택
                int randomNodeNumber = Random.Range(0, ExploreMap[i + 1].Count);

                while (currentNodeCount < ExploreMap[i].Count && nextNodeCount < ExploreMap[i + 1].Count)
                {
                    //일단 현재 선택된 왼쪽 노드에 선택된 오른쪽 노드를 등록
                    ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount].gameObject);
                    //만약 오른쪽 노드가 선택된 노드일 시
                    if (nextNodeCount == randomNodeNumber)
                    {
                        //왼쪽 노드의 남은 수 = 오른쪽 노드의 남은 수 - 1이 될 때 까지 왼쪽 노드를 내려가며 등록
                        while (ExploreMap[i].Count - currentNodeCount > ExploreMap[i + 1].Count - nextNodeCount)
                        {
                            if (currentNodeCount + 1 < ExploreMap[i].Count)
                            {
                                currentNodeCount++;
                                ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount].gameObject);
                            }
                            else
                            {
                                break;
                            }
                        }
                        if (currentNodeCount + 1 < ExploreMap[i].Count)
                        {
                            currentNodeCount++;
                            ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount].gameObject);
                        }
                    }

                    //맨 처음 노드와 마지막 노드가 아닐 경우 50%의 확률로 아래 노드와 연결됨
                    //이전에 아래 노드와 연결이 안됬을 경우는 무조건 연결됨
                    if (nextNodeCount + 1 < ExploreMap[i + 1].Count)
                    {
                        if (Random.Range(0, 10) % 2 == 0 || recentlyIgnoreNode || currentNodeCount == 0 || currentNodeCount == ExploreMap[i].Count - 1)
                        {
                            ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount + 1].gameObject);
                            nextNodeCount++;
                            recentlyIgnoreNode = false;
                        }
                        else
                        {
                            recentlyIgnoreNode = true;
                        }
                    }

                    //만약 마지막까지 연결이 안된 노드가 존재 시 마지막 노드에 강제로 연결해줌
                    if (currentNodeCount == ExploreMap[i].Count - 1 && nextNodeCount < ExploreMap[i + 1].Count - 1)
                    {
                        while (nextNodeCount < ExploreMap[i + 1].Count)
                        {
                            ExploreMap[i][currentNodeCount].nextIncountNode.Add(ExploreMap[i + 1][nextNodeCount].gameObject);
                            nextNodeCount++;
                        }
                    }

                    currentNodeCount++;
                }
            }

            //모든 노드 연결 이후 추가적인 노드 연결 진행
            //foreach (IncountNode node in ExploreMap[i])
            //{
            //    foreach (IncountNode nextNode in ExploreMap[i + 1])
            //    {
            //        if (Random.value < 0.3f)
            //        {
            //            if (!node.nextIncountNode.Contains(nextNode.gameObject))
            //            {
            //                node.nextIncountNode.Add(nextNode.gameObject);
            //                break;
            //            }
            //        }
            //    }
            //}
        }
    }
}
