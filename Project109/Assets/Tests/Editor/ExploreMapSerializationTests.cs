using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

[TestFixture]
public class ExploreMapSerializationTests
{
    private static ExploreMapData CreateSampleMapData()
    {
        ExploreMapData mapData = new ExploreMapData();

        ExploreFloorData floor0 = new ExploreFloorData();
        ExploreNode startNode = new ExploreNode(0, 0)
        {
            isRevealed = true,
            isCleared = false,
            isNodeChanged = false,
            isVisited = true
        };
        startNode.encounterData.locationType = LocationType.Temple;
        startNode.encounterData.encountType = EncountType.None;
        startNode.encounterData.interactableDataId = "Region_Start_Shrine_Data";
        startNode.nextNodeIndices.Add(0);
        startNode.nextNodeIndices.Add(1);
        floor0.nodes.Add(startNode);

        ExploreFloorData floor1 = new ExploreFloorData();
        ExploreNode battleNode = new ExploreNode(1, 0)
        {
            isRevealed = true,
            isCleared = true,
            isNodeChanged = true
        };
        battleNode.encounterData.locationType = LocationType.Temple;
        battleNode.encounterData.encountType = EncountType.Battle;
        battleNode.encounterData.extraEncountType = ExtraEncountType.Insight;
        battleNode.encounterData.battleExtraRewardType = BattleExtraRewardType.Relic;
        battleNode.encounterData.battleDataId = "Battle_Test_01";
        battleNode.encounterData.interactableDataId = "Insight_Event_Data";
        battleNode.nextNodeIndices.Add(0);

        ExploreNode storeNode = new ExploreNode(1, 1)
        {
            isRevealed = false,
            isCleared = false,
            isNodeChanged = true
        };
        storeNode.encounterData.locationType = LocationType.Temple;
        storeNode.encounterData.encountType = EncountType.Store;
        storeNode.encounterData.interactableDataId = "Shop_NPC_Data";
        storeNode.nextNodeIndices.Add(0);

        floor1.nodes.Add(battleNode);
        floor1.nodes.Add(storeNode);

        ExploreFloorData floor2 = new ExploreFloorData();
        ExploreNode bossNode = new ExploreNode(2, 0)
        {
            isRevealed = true,
            isCleared = false,
            isNodeChanged = false
        };
        bossNode.encounterData.locationType = LocationType.Temple;
        bossNode.encounterData.encountType = EncountType.Boss;
        bossNode.encounterData.battleDataId = "Battle_Boss_01";
        floor2.nodes.Add(bossNode);

        mapData.floors.Add(floor0);
        mapData.floors.Add(floor1);
        mapData.floors.Add(floor2);

        return mapData;
    }

    [Test]
    public void ExploreMapData_JsonUtilityRoundTrip_PreservesFloorsNodesAndFields()
    {
        RunSaveData source = new RunSaveData
        {
            isInDungeon = true,
            currentMapName = "Temple",
            currentStageLevel = 1,
            currentExploreMapFloor = 1,
            currentExploreNodeFloor = 1,
            currentExploreNodeIndex = 0,
            beforeExploreNodeFloor = 0,
            beforeExploreNodeIndex = 0,
            exploreMapData = CreateSampleMapData()
        };

        string json = JsonUtility.ToJson(source, true);
        Assert.IsFalse(string.IsNullOrEmpty(json));
        StringAssert.Contains("\"floors\"", json);
        StringAssert.Contains("\"nextNodeIndices\"", json);

        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(json);
        Assert.IsNotNull(restored);
        Assert.IsNotNull(restored.exploreMapData);
        Assert.IsNotNull(restored.exploreMapData.floors);
        Assert.AreEqual(3, restored.exploreMapData.floors.Count);

        ExploreNode restoredStart = restored.exploreMapData.floors[0].nodes[0];
        Assert.IsTrue(restoredStart.isVisited);

        ExploreNode restoredBattle = restored.exploreMapData.floors[1].nodes[0];
        Assert.AreEqual(1, restoredBattle.floor);
        Assert.AreEqual(0, restoredBattle.index);
        Assert.IsTrue(restoredBattle.isRevealed);
        Assert.IsTrue(restoredBattle.isCleared);
        Assert.IsTrue(restoredBattle.isNodeChanged);
        CollectionAssert.AreEqual(new List<int> { 0 }, restoredBattle.nextNodeIndices);
        Assert.AreEqual(LocationType.Temple, restoredBattle.encounterData.locationType);
        Assert.AreEqual(EncountType.Battle, restoredBattle.encounterData.encountType);
        Assert.AreEqual(ExtraEncountType.Insight, restoredBattle.encounterData.extraEncountType);
        Assert.AreEqual(BattleExtraRewardType.Relic, restoredBattle.encounterData.battleExtraRewardType);
        Assert.AreEqual("Battle_Test_01", restoredBattle.encounterData.battleDataId);
        Assert.AreEqual("Insight_Event_Data", restoredBattle.encounterData.interactableDataId);

        ExploreNode restoredStore = restored.exploreMapData.floors[1].nodes[1];
        Assert.IsFalse(restoredStore.isRevealed);
        Assert.IsFalse(restoredStore.isCleared);
        Assert.AreEqual(EncountType.Store, restoredStore.encounterData.encountType);
    }

    [Test]
    public void ExploreMapManager_GenerateMap_And_JsonRoundTrip_PreserveGraphLinks()
    {
        ExploreMapManager manager = new ExploreMapManager();
        manager.GenerateMap(15, 0, LocationType.Temple);

        Assert.IsNotNull(manager.MapData);
        Assert.AreEqual(15, manager.MapData.floors.Count);
        Assert.AreEqual(1, manager.MapData.floors[0].nodes.Count);
        Assert.AreEqual(EncountType.Boss, manager.MapData.floors[14].nodes[0].encounterData.encountType);
        Assert.Greater(manager.MapData.floors[0].nodes[0].nextNodeIndices.Count, 0);

        string json = JsonUtility.ToJson(manager.MapData);
        ExploreMapData deserialized = JsonUtility.FromJson<ExploreMapData>(json);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(15, deserialized.floors.Count);
        CollectionAssert.AreEqual(
            manager.MapData.floors[0].nodes[0].nextNodeIndices,
            deserialized.floors[0].nodes[0].nextNodeIndices);
    }

    [Test]
    public void SaveSystem_RestoreRunData_BindsExactExploreNodeReferences()
    {
        RunSaveData source = new RunSaveData
        {
            isInDungeon = true,
            currentMapName = "Temple",
            currentStageLevel = 1,
            currentExploreMapFloor = 1,
            currentExploreNodeFloor = 1,
            currentExploreNodeIndex = 0,
            beforeExploreNodeFloor = 0,
            beforeExploreNodeIndex = 0,
            exploreMapData = CreateSampleMapData()
        };

        string json = JsonUtility.ToJson(source);
        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(json);

        GameObject go = new GameObject("TestRunManager");
        try
        {
            RunManager runManager = go.AddComponent<RunManager>();
            runManager.exploreMapManager = new ExploreMapManager();

            SaveSystem.RestoreRunData(restored, runManager);

            Assert.IsNotNull(runManager.exploreMapManager.MapData);
            ExploreNode expectedCurrent = runManager.exploreMapManager.MapData.floors[1].nodes[0];
            ExploreNode expectedBefore = runManager.exploreMapManager.MapData.floors[0].nodes[0];

            Assert.AreSame(expectedCurrent, runManager.currentExploreNode);
            Assert.AreSame(expectedBefore, runManager.beforeExploreNode);
            Assert.IsTrue(runManager.currentExploreNode.isCleared);
            Assert.IsTrue(runManager.currentExploreNode.isVisited);
            Assert.IsTrue(runManager.beforeExploreNode.isVisited);
            CollectionAssert.AreEqual(new List<int> { 0 }, runManager.currentExploreNode.nextNodeIndices);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ExploreMap_MoveToNode_And_RefreshMapVisuals_HighlightsVisitedNodesAndKeepsRevealedNodes()
    {
        List<Object> createdObjects = new List<Object>();
        GameObject rmGo = new GameObject("TestRunManager");
        createdObjects.Add(rmGo);
        GameObject mapUiGo = new GameObject("TestExploreMapUI");
        createdObjects.Add(mapUiGo);
        GameObject viewLayoutGo = new GameObject("ViewLayout");
        createdObjects.Add(viewLayoutGo);
        viewLayoutGo.transform.SetParent(mapUiGo.transform, false);

        GameObject verticalPrefab = new GameObject("TestVerticalLayoutPrefab", typeof(RectTransform), typeof(VerticalLayoutGroup));
        createdObjects.Add(verticalPrefab);
        GameObject nodePrefab = new GameObject("TestExploreNodePrefab", typeof(RectTransform), typeof(Image), typeof(ExploreNodeUI));
        createdObjects.Add(nodePrefab);

        GameObject coverObj = new GameObject("Cover", typeof(RectTransform));
        coverObj.transform.SetParent(nodePrefab.transform, false);
        GameObject hoverRingObj = new GameObject("HoverRing", typeof(RectTransform));
        hoverRingObj.transform.SetParent(nodePrefab.transform, false);
        GameObject visitedRingObj = new GameObject("VisitedRing", typeof(RectTransform));
        visitedRingObj.transform.SetParent(nodePrefab.transform, false);

        ExploreNodeUI prefabNodeUi = nodePrefab.GetComponent<ExploreNodeUI>();
        prefabNodeUi.ExploreNodeUICoverObject = coverObj;
        prefabNodeUi.ExploreNodeUIHighlightCircleObject = hoverRingObj;
        prefabNodeUi.ExploreNodeUICurrentHighlightCircleObject = visitedRingObj;

        try
        {
            RunManager runManager = rmGo.AddComponent<RunManager>();
            typeof(RunManager).GetField("<instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, runManager);
            runManager.exploreMapManager = new ExploreMapManager();
            runManager.exploreMapManager.GenerateMap(4, 0, LocationType.Temple);

            ExploreMapData mapData = runManager.exploreMapManager.MapData;
            Assert.IsTrue(mapData.floors[0].nodes[0].isVisited, "시작 노드(Floor 0, Index 0)는 생성 시 방문 처리되어야 합니다.");

            // 테스트 시나리오 구성: Floor 0(방문) -> Floor 1 Node 0(방문, 공개) vs Node 1(미선택, 공개) -> Floor 2 Node 0(현재 위치, 공개)
            ExploreMapData customMap = CreateSampleMapData();
            customMap.floors[0].nodes[0].isVisited = true;
            customMap.floors[0].nodes[0].isRevealed = true;
            customMap.floors[1].nodes[0].isVisited = false;
            customMap.floors[1].nodes[0].isRevealed = true;
            customMap.floors[1].nodes[1].isVisited = false;
            customMap.floors[1].nodes[1].isRevealed = true;
            customMap.floors[2].nodes[0].isVisited = false;
            customMap.floors[2].nodes[0].isRevealed = true;

            runManager.exploreMapManager.RestoreMapData(customMap);
            runManager.currentExploreNode = customMap.floors[0].nodes[0];
            runManager.currentExploreMapFloor = 0;

            ExploreMapUI mapUI = mapUiGo.AddComponent<ExploreMapUI>();
            SetPrivateField(mapUI, "ViewLayout", viewLayoutGo);
            SetPrivateField(mapUI, "ExploreMapVerticalLayoutPrefab", verticalPrefab);
            SetPrivateField(mapUI, "NodePrefab", nodePrefab);

            mapUI.CreateExploreMap(3);

            // Floor 0 -> Floor 1 Node 0 -> Floor 2 Node 0 순서로 이동 시뮬레이션
            LogAssert.Expect(LogType.Error, new Regex("GameSceneManager에 플레이어 정보가 없어"));
            runManager.MoveToNode(mapUI.MapNodes[1][0]);
            Assert.IsTrue(customMap.floors[0].nodes[0].isVisited);
            Assert.IsTrue(customMap.floors[1].nodes[0].isVisited);
            Assert.IsFalse(customMap.floors[1].nodes[1].isVisited);

            LogAssert.Expect(LogType.Error, new Regex("GameSceneManager에 플레이어 정보가 없어"));
            runManager.MoveToNode(mapUI.MapNodes[2][0]);
            Assert.IsTrue(customMap.floors[2].nodes[0].isVisited);
            Assert.AreSame(customMap.floors[2].nodes[0], runManager.currentExploreNode);

            // 호버 링이 잔류한 상태를 강제로 만든 후 RefreshMapVisuals() 호출
            mapUI.MapNodes[1][1].ExploreNodeUIHighlightCircleObject.SetActive(true);
            mapUI.RefreshMapVisuals();

            // 1. 모든 노드의 호버 링이 초기화되어야 함
            Assert.IsFalse(mapUI.MapNodes[1][1].ExploreNodeUIHighlightCircleObject.activeSelf);

            // 2. 지나온 노드들(0,0 / 1,0)과 현재 노드(2,0)의 방문/현재 하이라이트 링이 모두 켜지고, 미선택 노드(1,1)는 꺼져야 함
            Assert.IsTrue(mapUI.MapNodes[0][0].ExploreNodeUICurrentHighlightCircleObject.activeSelf);
            Assert.IsTrue(mapUI.MapNodes[1][0].ExploreNodeUICurrentHighlightCircleObject.activeSelf);
            Assert.IsTrue(mapUI.MapNodes[2][0].ExploreNodeUICurrentHighlightCircleObject.activeSelf);
            Assert.IsFalse(mapUI.MapNodes[1][1].ExploreNodeUICurrentHighlightCircleObject.activeSelf);

            // 3. 이미 공개된(isRevealed == true) 직전 층의 미선택 노드(1,1)가 안개로 다시 가려지지 않아야 함
            Assert.IsFalse(mapUI.MapNodes[1][1].ExploreNodeUICoverObject.activeSelf);
        }
        finally
        {
            typeof(RunManager).GetField("<instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
        field.SetValue(target, value);
    }

    [Test]
    public void SaveSystem_RestoreRunData_WhenNodeIndicesUnset_ClearsStaleNodeReferences()
    {
        RunSaveData source = new RunSaveData
        {
            isInDungeon = true,
            currentMapName = "Temple",
            currentStageLevel = 1,
            currentExploreMapFloor = 0,
            currentExploreNodeFloor = -1,
            currentExploreNodeIndex = -1,
            beforeExploreNodeFloor = -1,
            beforeExploreNodeIndex = -1,
            exploreMapData = new ExploreMapData()
        };

        string json = JsonUtility.ToJson(source);
        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(json);

        GameObject go = new GameObject("TestRunManager");
        try
        {
            RunManager runManager = go.AddComponent<RunManager>();
            runManager.exploreMapManager = new ExploreMapManager();
            runManager.currentExploreNode = new ExploreNode(9, 9);
            runManager.beforeExploreNode = new ExploreNode(8, 8);

            SaveSystem.RestoreRunData(restored, runManager);

            Assert.IsNull(runManager.currentExploreNode);
            Assert.IsNull(runManager.beforeExploreNode);
            Assert.IsNotNull(runManager.exploreMapManager.MapData);
            Assert.AreEqual(0, runManager.exploreMapManager.MapData.floors.Count);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ReserializedAssets_IncountNodePrefabAndMapSOs_PreserveBindingsWithoutFormerlySerializedAs()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/ExploreMap/IncountNode.prefab");
        Assert.IsNotNull(prefab, "IncountNode.prefab을 로드할 수 없습니다.");

        ExploreNodeUI nodeUI = prefab.GetComponent<ExploreNodeUI>();
        Assert.IsNotNull(nodeUI, "IncountNode.prefab에 ExploreNodeUI 컴포넌트가 없습니다.");
        Assert.IsTrue(nodeUI.ExploreNodeUICoverObject != null, "ExploreNodeUICoverObject 직렬화 바인딩이 유실되었습니다.");
        Assert.IsTrue(nodeUI.ExploreNodeUIHighlightCircleObject != null, "ExploreNodeUIHighlightCircleObject 직렬화 바인딩이 유실되었습니다.");
        Assert.IsTrue(nodeUI.ExploreNodeUICurrentHighlightCircleObject != null, "ExploreNodeUICurrentHighlightCircleObject 직렬화 바인딩이 유실되었습니다.");
        Assert.IsTrue(nodeUI.ExploreNodeUIExtraRewardObject != null, "ExploreNodeUIExtraRewardObject 직렬화 바인딩이 유실되었습니다.");

        MapDataSO hideoutMap = AssetDatabase.LoadAssetAtPath<MapDataSO>("Assets/SO/Maps/Map_Hideout.asset");
        Assert.IsNotNull(hideoutMap, "Map_Hideout.asset을 로드할 수 없습니다.");
        Assert.AreEqual(EncountType.None, hideoutMap.EncountType);

        string[] mapGuids = AssetDatabase.FindAssets("t:MapDataSO", new[] { "Assets/SO/Maps" });
        Assert.GreaterOrEqual(mapGuids.Length, 9, "Assets/SO/Maps 하위에 최소 9개 이상의 MapDataSO 에셋이 존재해야 합니다.");
        foreach (string guid in mapGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            MapDataSO mapAsset = AssetDatabase.LoadAssetAtPath<MapDataSO>(assetPath);
            Assert.IsNotNull(mapAsset, $"MapDataSO 로드 실패: {assetPath}");
        }

        AssertNoFormerlySerializedAs(typeof(ExploreNodeUI));
        AssertNoFormerlySerializedAs(typeof(MapDataSO));
    }

    private static void AssertNoFormerlySerializedAs(System.Type type)
    {
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (FieldInfo field in fields)
        {
            object[] attrs = field.GetCustomAttributes(typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute), inherit: true);
            Assert.AreEqual(0, attrs.Length, $"{type.Name}.{field.Name}에 FormerlySerializedAs 어트리뷰트가 남아 있습니다.");
        }
    }
}
