using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ExploreFloorData
{
    // 한 층(floor)에 속한 ExploreNode 리스트 (Unity JsonUtility 직렬화 래퍼)
    public List<ExploreNode> nodes = new List<ExploreNode>();
}

[System.Serializable]
public class ExploreMapData
{
    // 층(floor)별 노드 래퍼 리스트
    public List<ExploreFloorData> floors = new List<ExploreFloorData>();
}
