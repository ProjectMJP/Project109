using System.Collections.Generic;
using UnityEngine;

public class ExploreMapManager
{
    public ExploreMapData MapData { get; private set; }

    private AssetCacheManager dataLoader;
    private RandomItemPicker<BattleData> battleItemPicker;
    private RandomItemPicker<InteractableData> eventItemPicker;
    private int _cachedBattleListCount = 0;

    [Header("Map Setting")]
    public int StoreNumber = 1;
    public int RestoreNumber = 2;
    public int SecretNumber = 4;
    public int BoxNumber = 1;
    public int EliteNumber = 2;

    public ExploreMapManager()
    {
        EnsureBattleItemPicker();
    }

    private void EnsureBattleItemPicker()
    {
        if (dataLoader == null)
        {
            dataLoader = AssetCacheManager.instance;
        }

        if (dataLoader != null && dataLoader.battleList != null)
        {
            if (battleItemPicker == null || (_cachedBattleListCount == 0 && dataLoader.battleList.Count > 0))
            {
                battleItemPicker = new RandomItemPicker<BattleData>(dataLoader.battleList);
                _cachedBattleListCount = dataLoader.battleList.Count;
            }
        }
        else if (battleItemPicker == null)
        {
            battleItemPicker = new RandomItemPicker<BattleData>(new List<BattleData>());
            _cachedBattleListCount = 0;
        }
    }

    public void GenerateMap(int mapLength, int currentMapFloor, LocationType locationType = LocationType.Temple)
    {
        MapData = new ExploreMapData();

        if (ModLoader.Instance != null)
        {
            eventItemPicker = new RandomItemPicker<InteractableData>(ModLoader.Instance.GetRandomInteractablesForFloor(currentMapFloor));
        }
        else
        {
            eventItemPicker = new RandomItemPicker<InteractableData>(new List<InteractableData>());
        }

        List<List<ExploreNode>> exploreNodeListInSection = new List<List<ExploreNode>>();
        int sectionIndex = 0;
        exploreNodeListInSection.Add(new List<ExploreNode>());

        // 1. 노드 생성 레이아웃 배치
        for (int index = 0; index < mapLength; index++)
        {
            ExploreFloorData floorData = new ExploreFloorData();
            MapData.floors.Add(floorData);

            if (index == 0)
            {
                ExploreNode node = new ExploreNode(index, 0);
                node.encounterData.locationType = locationType;
                node.encounterData.encountType = EncountType.None;
                node.encounterData.interactableDataId = "Region_Start_Shrine_Data";
                floorData.nodes.Add(node);
            }
            else if (index == mapLength - 1)
            {
                ExploreNode node = new ExploreNode(index, 0);
                node.encounterData.locationType = locationType;
                node.encounterData.encountType = EncountType.Boss;
                floorData.nodes.Add(node);
            }
            else if (index == mapLength / 2)
            {
                ExploreNode nodeRestore = new ExploreNode(index, 0);
                nodeRestore.encounterData.locationType = locationType;
                nodeRestore.encounterData.encountType = EncountType.Restore;
                nodeRestore.encounterData.interactableDataId = "Restore_Bonfire_Data";
                floorData.nodes.Add(nodeRestore);

                ExploreNode nodeStore = new ExploreNode(index, 1);
                nodeStore.encounterData.locationType = locationType;
                nodeStore.encounterData.encountType = EncountType.Store;
                nodeStore.encounterData.interactableDataId = "Shop_NPC_Data";
                floorData.nodes.Add(nodeStore);

                exploreNodeListInSection.Add(new List<ExploreNode>());
                sectionIndex++;
            }
            else
            {
                int createNodeCount = Random.Range(3, 5);
                for (int nIdx = 0; nIdx < createNodeCount; nIdx++)
                {
                    ExploreNode node = new ExploreNode(index, nIdx);
                    node.encounterData.locationType = locationType;
                    node.encounterData.encountType = EncountType.Battle;
                    floorData.nodes.Add(node);
                    exploreNodeListInSection[sectionIndex].Add(node);
                }
            }
        }

        // 보스 전 무조건 휴식
        if (mapLength >= 2)
        {
            foreach (ExploreNode node in MapData.floors[mapLength - 2].nodes)
            {
                node.encounterData.encountType = EncountType.Restore;
                node.encounterData.interactableDataId = "Restore_Bonfire_Data";
                node.isNodeChanged = true;
            }
        }

        // 2. 노드 타입 무작위 변경 (엘리트, 상자 등)
        for (int sectionIdx = 0; sectionIdx < exploreNodeListInSection.Count; sectionIdx++)
        {
            ExploreNode currentNode;

            // 천리안 노드
            if (sectionIdx == 0 && exploreNodeListInSection[sectionIdx].Count > 0)
            {
                currentNode = PickAvailableBattleNode(exploreNodeListInSection[sectionIdx], exploreNodeListInSection[sectionIdx].Count / 3);
                if (currentNode != null)
                {
                    currentNode.encounterData.encountType = EncountType.Battle;
                    currentNode.encounterData.extraEncountType = ExtraEncountType.Insight;
                    currentNode.isNodeChanged = true;

                    if (ModLoader.Instance != null && ModLoader.Instance.InteractableDatabase.TryGetValue("Insight_Event_Data", out InteractableData data))
                    {
                        currentNode.encounterData.interactableDataId = data.ID;
                    }
                }
            }

            // 상자 생성
            for (int i = 0; i < BoxNumber; i++)
            {
                if (exploreNodeListInSection[sectionIdx].Count == 0) continue;
                currentNode = PickAvailableBattleNode(exploreNodeListInSection[sectionIdx], exploreNodeListInSection[sectionIdx].Count / 2);
                if (currentNode != null)
                {
                    currentNode.encounterData.encountType = EncountType.SecretBox;
                    currentNode.encounterData.interactableDataId = "Reward_Chest_Data";
                    currentNode.isNodeChanged = true;
                }
            }

            // 엘리트 적 생성
            for (int i = 0; i < EliteNumber; i++)
            {
                if (exploreNodeListInSection[sectionIdx].Count == 0) continue;
                int startIdx = (sectionIdx == 0) ? (exploreNodeListInSection[sectionIdx].Count / 3) : 0;
                currentNode = PickAvailableBattleNode(exploreNodeListInSection[sectionIdx], startIdx);
                if (currentNode != null)
                {
                    currentNode.encounterData.encountType = EncountType.Elite;
                    currentNode.isNodeChanged = true;
                }
            }

            // 휴식 생성
            for (int i = 0; i < RestoreNumber; i++)
            {
                if (exploreNodeListInSection[sectionIdx].Count == 0) continue;
                currentNode = PickAvailableBattleNode(exploreNodeListInSection[sectionIdx], exploreNodeListInSection[sectionIdx].Count / 3);
                if (currentNode != null)
                {
                    currentNode.encounterData.encountType = EncountType.Restore;
                    currentNode.encounterData.interactableDataId = "Restore_Bonfire_Data";
                    currentNode.isNodeChanged = true;
                }
            }

            // 상점 생성
            for (int i = 0; i < StoreNumber; i++)
            {
                if (exploreNodeListInSection[sectionIdx].Count == 0) continue;
                currentNode = PickAvailableBattleNode(exploreNodeListInSection[sectionIdx], 0);
                if (currentNode != null)
                {
                    currentNode.encounterData.encountType = EncountType.Store;
                    currentNode.encounterData.interactableDataId = "Shop_NPC_Data";
                    currentNode.isNodeChanged = true;
                }
            }

            // 시크릿 생성
            for (int i = 0; i < SecretNumber; i++)
            {
                if (exploreNodeListInSection[sectionIdx].Count == 0) continue;
                currentNode = PickAvailableBattleNode(exploreNodeListInSection[sectionIdx], 0);
                if (currentNode != null)
                {
                    currentNode.encounterData.encountType = EncountType.Secret;
                    currentNode.isNodeChanged = true;

                    if (eventItemPicker.TryGetNext(out InteractableData data))
                    {
                        currentNode.encounterData.interactableDataId = data.ID;
                    }
                    else
                    {
                        eventItemPicker.Reset();
                        if (eventItemPicker.TryGetNext(out InteractableData newData))
                        {
                            currentNode.encounterData.interactableDataId = newData.ID;
                        }
                    }
                }
            }
        }

        // 일반 전투 노드 추가 보상 배정
        if (MapData != null && MapData.floors != null)
        {
            for (int i = 0; i < MapData.floors.Count; i++)
            {
                ExploreFloorData floorData = MapData.floors[i];
                if (floorData == null || floorData.nodes == null) continue;

                for (int j = 0; j < floorData.nodes.Count; j++)
                {
                    ExploreNode node = floorData.nodes[j];
                    if (node != null && node.encounterData.encountType == EncountType.Battle && !node.isNodeChanged)
                    {
                        int randomVal = Random.Range(0, 3);
                        BattleExtraRewardType rewardType = BattleExtraRewardType.Card;
                        if (randomVal == 1) rewardType = BattleExtraRewardType.Relic;
                        else if (randomVal == 2) rewardType = BattleExtraRewardType.Gold;

                        node.encounterData.battleExtraRewardType = rewardType;
                    }
                }
            }
        }

        SetBattleNodeData();
        LinkNodes();
        InitializeNodeVisibility(mapLength);
    }

    /// <summary>
    /// 섹션 내에서 아직 변경되지 않은 일반 전투 노드를 안전하게 탐색하여 반환합니다.
    /// do-while 무한 루프를 방지하기 위해 최대 시도 횟수를 제한하고 후보 리스트 순회 폴백을 제공합니다.
    /// </summary>
    private ExploreNode PickAvailableBattleNode(List<ExploreNode> sectionNodes, int startIndex, int maxAttempts = 100)
    {
        if (sectionNodes == null || sectionNodes.Count == 0) return null;
        startIndex = Mathf.Clamp(startIndex, 0, sectionNodes.Count - 1);

        // 1. 빠른 무작위 시도 (기존 확률 분포 유지)
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int randIdx = Random.Range(startIndex, sectionNodes.Count);
            ExploreNode candidate = sectionNodes[randIdx];
            if (candidate != null && candidate.encounterData != null && 
                candidate.encounterData.encountType == EncountType.Battle && !candidate.isNodeChanged)
            {
                return candidate;
            }
        }

        // 2. 랜덤 시도 실패 시 startIndex 이후 사용 가능한 모든 후보 직접 순회 검색
        for (int i = startIndex; i < sectionNodes.Count; i++)
        {
            ExploreNode candidate = sectionNodes[i];
            if (candidate != null && candidate.encounterData != null && 
                candidate.encounterData.encountType == EncountType.Battle && !candidate.isNodeChanged)
            {
                return candidate;
            }
        }

        // 3. 만약 startIndex 이후에 남은 전투 노드가 없다면 전체 범위에서 폴백 검색
        for (int i = 0; i < sectionNodes.Count; i++)
        {
            ExploreNode candidate = sectionNodes[i];
            if (candidate != null && candidate.encounterData != null && 
                candidate.encounterData.encountType == EncountType.Battle && !candidate.isNodeChanged)
            {
                return candidate;
            }
        }

        // 4. 섹션 내에 조건을 만족하는 노드가 전혀 없는 경우
        Debug.LogWarning("[ExploreMapManager] 해당 섹션 내에 변경 가능한 Battle 노드가 부족하여 특수 노드 배정을 건너뜁니다.");
        return null;
    }

    private void SetBattleNodeData()
    {
        if (MapData == null || MapData.floors == null) return;
        EnsureBattleItemPicker();

        for (int i = 0; i < MapData.floors.Count; i++)
        {
            List<ExploreNode> floorNodes = MapData.floors[i] != null ? MapData.floors[i].nodes : null;
            if (floorNodes == null) continue;

            for (int j = 0; j < floorNodes.Count; j++)
            {
                if (floorNodes[j] == null || floorNodes[j].encounterData == null) continue;
                var enc = floorNodes[j].encounterData;
                if (enc.encountType.IsCombat())
                {
                    if (battleItemPicker != null)
                    {
                        if (battleItemPicker.TryGetNext(out BattleData data))
                        {
                            enc.battleDataId = data.ID;
                        }
                        else
                        {
                            battleItemPicker.Reset();
                            if (battleItemPicker.TryGetNext(out BattleData newData))
                            {
                                enc.battleDataId = newData.ID;
                            }
                        }
                    }
                }
            }
        }
    }

    private void LinkNodes()
    {
        if (MapData == null || MapData.floors == null) return;

        for (int i = 0; i < MapData.floors.Count - 1; i++)
        {
            ExploreFloorData currentFloorData = MapData.floors[i];
            ExploreFloorData nextFloorData = MapData.floors[i + 1];
            List<ExploreNode> currentFloorNodes = currentFloorData != null ? currentFloorData.nodes : null;
            List<ExploreNode> nextFloorNodes = nextFloorData != null ? nextFloorData.nodes : null;
            if (currentFloorNodes == null || nextFloorNodes == null || currentFloorNodes.Count == 0 || nextFloorNodes.Count == 0)
            {
                continue;
            }

            if (currentFloorNodes.Count == 1)
            {
                int currentNodeCount = 0;
                int nextNodeCount = 0;
                while (nextNodeCount < nextFloorNodes.Count)
                {
                    currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount);
                    nextNodeCount++;
                }
            }
            else if (nextFloorNodes.Count == 1)
            {
                int currentNodeCount = 0;
                int nextNodeCount = 0;
                while (currentNodeCount < currentFloorNodes.Count)
                {
                    currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount);
                    currentNodeCount++;
                }
            }
            else if (currentFloorNodes.Count < nextFloorNodes.Count)
            {
                int currentNodeCount = 0;
                int nextNodeCount = 0;
                int randomNodeNumber = Random.Range(0, currentFloorNodes.Count);

                while (currentNodeCount < currentFloorNodes.Count && nextNodeCount < nextFloorNodes.Count)
                {
                    if (currentNodeCount == randomNodeNumber)
                    {
                        while (currentFloorNodes.Count - currentNodeCount + 1 < nextFloorNodes.Count - nextNodeCount)
                        {
                            currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount);
                            nextNodeCount++;
                        }
                    }

                    currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount);
                    
                    if (nextNodeCount + 1 < nextFloorNodes.Count)
                    {
                        if (Random.Range(0, 10) % 2 == 0 || currentNodeCount == 0 || currentNodeCount == currentFloorNodes.Count - 1)
                        {
                            currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount + 1);
                            nextNodeCount++;
                        }
                        else
                        {
                            nextNodeCount++;
                        }
                    }

                    if (currentNodeCount == currentFloorNodes.Count - 1 && nextNodeCount < nextFloorNodes.Count - 1)
                    {
                        while (nextNodeCount < nextFloorNodes.Count)
                        {
                            currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount);
                            nextNodeCount++;
                        }
                    }

                    currentNodeCount++;
                }
            }
            else
            {
                int currentNodeCount = 0;
                int nextNodeCount = 0;
                bool recentlyIgnoreNode = false;
                int randomNodeNumber = Random.Range(0, nextFloorNodes.Count);

                while (currentNodeCount < currentFloorNodes.Count && nextNodeCount < nextFloorNodes.Count)
                {
                    currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount);
                    
                    if (nextNodeCount == randomNodeNumber)
                    {
                        while (currentFloorNodes.Count - currentNodeCount > nextFloorNodes.Count - nextNodeCount)
                        {
                            if (currentNodeCount + 1 < currentFloorNodes.Count)
                            {
                                currentNodeCount++;
                                currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount);
                            }
                            else
                            {
                                break;
                            }
                        }
                        if (currentNodeCount + 1 < currentFloorNodes.Count)
                        {
                            currentNodeCount++;
                            currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount);
                        }
                    }

                    if (nextNodeCount + 1 < nextFloorNodes.Count)
                    {
                        if (Random.Range(0, 10) % 2 == 0 || recentlyIgnoreNode || currentNodeCount == 0 || currentNodeCount == currentFloorNodes.Count - 1)
                        {
                            currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount + 1);
                            nextNodeCount++;
                            recentlyIgnoreNode = false;
                        }
                        else
                        {
                            recentlyIgnoreNode = true;
                        }
                    }

                    if (currentNodeCount == currentFloorNodes.Count - 1 && nextNodeCount < nextFloorNodes.Count - 1)
                    {
                        while (nextNodeCount < nextFloorNodes.Count)
                        {
                            currentFloorNodes[currentNodeCount].nextNodeIndices.Add(nextNodeCount);
                            nextNodeCount++;
                        }
                    }

                    currentNodeCount++;
                }
            }
        }
    }

    /// <summary>
    /// 맵 생성 직후 플레이어 시야 범위(기본 3층) 및 중간 거점 노드의 공개 상태를 초기화합니다.
    /// </summary>
    public void InitializeNodeVisibility(int mapLength)
    {
        if (MapData == null || MapData.floors == null) return;

        int checkLength = GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null
            ? GameSceneManager.instance.player.playerStat.MapFloorCheckLength
            : 3;

        int midFloor = mapLength / 2;

        for (int floor = 0; floor < MapData.floors.Count; floor++)
        {
            ExploreFloorData floorData = MapData.floors[floor];
            if (floorData == null || floorData.nodes == null) continue;

            bool shouldReveal = (floor < checkLength) || (floor == midFloor);
            foreach (var node in floorData.nodes)
            {
                if (node != null)
                {
                    node.isRevealed = shouldReveal;
                }
            }
        }
    }

    /// <summary>
    /// 맵의 모든 노드를 공개(Reveal)합니다.
    /// </summary>
    public void RevealAllNodes()
    {
        if (MapData == null || MapData.floors == null) return;

        foreach (var floorData in MapData.floors)
        {
            if (floorData == null || floorData.nodes == null) continue;

            foreach (var node in floorData.nodes)
            {
                if (node != null)
                {
                    node.isRevealed = true;
                }
            }
        }
    }

    /// <summary>
    /// 특정 인카운터 타입의 모든 노드를 공개(Reveal)합니다.
    /// </summary>
    public void RevealNodesByType(EncountType type)
    {
        if (MapData == null || MapData.floors == null) return;

        foreach (var floorData in MapData.floors)
        {
            if (floorData == null || floorData.nodes == null) continue;

            foreach (var node in floorData.nodes)
            {
                if (node != null && node.encounterData != null && node.encounterData.encountType == type)
                {
                    node.isRevealed = true;
                }
            }
        }
    }

    /// <summary>
    /// 현재 층을 기준으로 플레이어의 시야(checkLength) 내에 있는 노드들을 공개(Reveal)합니다.
    /// </summary>
    public void RevealNodesByFloor(int currentFloor, int checkLength)
    {
        if (MapData == null || MapData.floors == null) return;

        int startFloor = Mathf.Max(0, currentFloor);
        int endFloor = Mathf.Min(MapData.floors.Count, currentFloor + checkLength);
        for (int floor = startFloor; floor < endFloor; floor++)
        {
            ExploreFloorData floorData = MapData.floors[floor];
            if (floorData == null || floorData.nodes == null) continue;

            foreach (var node in floorData.nodes)
            {
                if (node != null)
                {
                    node.isRevealed = true;
                }
            }
        }
    }

    /// <summary>
    /// 아직 공개되지 않은 노드 중 하나를 무작위로 공개(Reveal)합니다.
    /// </summary>
    public void RevealRandomNode()
    {
        if (MapData == null || MapData.floors == null) return;

        List<ExploreNode> hiddenNodes = new List<ExploreNode>();
        foreach (var floorData in MapData.floors)
        {
            if (floorData == null || floorData.nodes == null) continue;

            foreach (var node in floorData.nodes)
            {
                if (node != null && !node.isRevealed)
                {
                    hiddenNodes.Add(node);
                }
            }
        }

        if (hiddenNodes.Count > 0)
        {
            int randIdx = Random.Range(0, hiddenNodes.Count);
            hiddenNodes[randIdx].isRevealed = true;
        }
    }

    /// <summary>
    /// 세이브 파일이나 외부 데이터로부터 탐험 맵 데이터를 직접 복원 주입합니다.
    /// </summary>
    public void RestoreMapData(ExploreMapData data)
    {
        MapData = data;
    }

    /// <summary>
    /// 현재 보관 중인 맵 데이터를 완전히 비워 초기화합니다.
    /// </summary>
    public void ClearMap()
    {
        MapData = null;
    }
}

