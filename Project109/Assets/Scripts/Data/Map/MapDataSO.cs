using UnityEngine;
using System.Collections.Generic;

public enum LocationType { Dock, Dungeon, Temple }

[CreateAssetMenu(fileName = "NewMapData", menuName = "Map Paint Data")]
public class MapDataSO : ScriptableObject, IIdentifiable
{
    [Header("Map Data")]
    public string stageName; // 맵의 이름
    public LocationType locationType; // 맵의 위치 유형 (예: 항구, 던전, 사원, 랜덤)
    [UnityEngine.Serialization.FormerlySerializedAs("incountType")]
    [UnityEngine.Serialization.FormerlySerializedAs("encountType")]
    public EncountType EncountType; //어디 노드에서 사용되는 데이터인지 구분하기 위한 변수

    [Header("Grid Settings")]
    public int width; // 맵의 가로 크기
    public int height; // 맵의 세로 크기
    public float cellSize = 1.0f; // 각 셀의 크기
    public Vector3 gridOffset; // 그리드의 시작 위치 (씬에서의 오프셋)
    public List<CellData> cells = new List<CellData>(); // 맵의 각 셀에 대한 데이터 리스트

    public string ID => !string.IsNullOrEmpty(stageName) ? stageName : name;

    /// <summary>
    /// 그리드 셀 좌표를 월드(또는 맵 기준 로컬) 좌표로 변환합니다.
    /// </summary>
    public Vector3 CellToWorld(Vector2Int cellCoord)
    {
        return CellToWorld(cellCoord.x, cellCoord.y, gridOffset, cellSize);
    }

    /// <summary>
    /// 그리드 열(col), 행(row) 인덱스를 월드(또는 맵 기준 로컬) 좌표로 변환합니다.
    /// </summary>
    public Vector3 CellToWorld(int col, int row)
    {
        return CellToWorld(col, row, gridOffset, cellSize);
    }

    /// <summary>
    /// 월드 좌표를 가장 가까운 그리드 셀 좌표로 변환합니다.
    /// </summary>
    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        return WorldToCell(worldPos, gridOffset, cellSize);
    }

    /// <summary>
    /// 지정한 오프셋과 셀 크기를 기준으로 셀 좌표를 3D 공간 좌표로 변환합니다. (Pointy-Top Odd-R 육각 격자)
    /// </summary>
    public static Vector3 CellToWorld(int col, int row, Vector3 gridOffset, float cellSize)
    {
        return HexMath.CellToWorld(col, row, gridOffset, cellSize);
    }

    /// <summary>
    /// 지정한 오프셋과 셀 크기를 기준으로 3D 공간 좌표를 가장 가까운 그리드 셀 좌표로 변환합니다. (Pointy-Top Odd-R 육각 격자)
    /// </summary>
    public static Vector2Int WorldToCell(Vector3 worldPos, Vector3 gridOffset, float cellSize)
    {
        return HexMath.WorldToCell(worldPos, gridOffset, cellSize);
    }

    //맵 초기화 함수
    public void InitializeGrid()
    {
        cells.Clear();

        for (int i = 0; i < width; i++)
        {
            for(int j = 0; j < height; j++)
            {
                cells.Add(new CellData(new Vector2Int(i, j)));
            }
        }
    }
}
