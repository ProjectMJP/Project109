using UnityEngine;

[System.Serializable]
public class CellData
{
    public Vector2Int position;

    public string terrainID;
    public string objectID;
    public string eventID;

    public CellData(Vector2Int pos)
    {
        position = pos;
        terrainID = "Empty";
        objectID = "Empty";
        eventID = "Empty";
    }
}
