using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ExploreNode
{
    public int floor; // 세로줄(Layer)
    public int index; // 층 내 순번

    public EncounterData encounterData = new EncounterData();
    
    // 이 노드에서 다음으로 이동 가능한 노드의 인덱스(같은 다음 층의 index)들
    public List<int> nextNodeIndices = new List<int>();
    
    public bool isNodeChanged; // 노드 타입이 한 번 이상 변경되었는지 여부
    public bool isRevealed;    // 안개/가림이 해제되어 노드 정보가 시각적으로 공개되었는지 여부
    public bool isCleared;     // 해당 노드의 전투/이벤트가 클리어되었는지 여부
    
    public ExploreNode(int floor, int index)
    {
        this.floor = floor;
        this.index = index;
        this.isRevealed = false;
        this.isCleared = false;
    }
}
