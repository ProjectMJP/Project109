using UnityEngine;
using System.Collections.Generic;

public enum LocationType { Dock, Dungeon, Temple }

[CreateAssetMenu(fileName = "NewMapData", menuName = "Map Paint Data")]
public class MapDataSO : ScriptableObject, IIdentifiable
{
    [Header("Map Data")]
    public string stageName; // 맵의 이름
    public LocationType locationType; // 맵의 위치 유형 (예: 항구, 던전, 사원, 랜덤)
    public IncountType incountType; //어디 노드에서 사용되는 데이터인지 구분하기 위한 변수

    [Header("Grid Settings")]
    public int width; // 맵의 가로 크기
    public int height; // 맵의 세로 크기
    public float cellSize = 1.0f; // 각 셀의 크기
    public Vector3 gridOffset; // 그리드의 시작 위치 (씬에서의 오프셋)
    public List<CellData> cells = new List<CellData>(); // 맵의 각 셀에 대한 데이터 리스트

    public string ID => locationType.ToString();

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
