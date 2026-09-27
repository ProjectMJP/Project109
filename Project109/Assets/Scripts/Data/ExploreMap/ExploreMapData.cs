using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ExploreMapData
{
    // 층(floor)별로 묶인 ExploreNode 2차원 리스트
    public List<List<ExploreNode>> mapNodes = new List<List<ExploreNode>>();
}
