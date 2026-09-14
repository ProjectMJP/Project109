using UnityEngine;

[System.Serializable]
public class EntityData
{
    public Vector2Int position; // 그리드 좌표 (x, y)
    public string entityID; // 몬스터, 함정, 플레이어 등 개체 종류 ID
}
