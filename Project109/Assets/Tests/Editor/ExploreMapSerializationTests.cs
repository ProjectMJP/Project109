using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

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
            isNodeChanged = false
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
            CollectionAssert.AreEqual(new List<int> { 0 }, runManager.currentExploreNode.nextNodeIndices);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
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
}
