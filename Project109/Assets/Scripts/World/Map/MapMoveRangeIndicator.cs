using UnityEngine;
using System.Collections.Generic;

public class MapMoveRangeIndicator : MonoBehaviour
{
    public Renderer tileRenderer;
    private Texture2D texture;
    
    private int gridColumns;
    private int gridRows;

    public void Initialize(int columns, int rows, float cellSize, Vector3 gridOffset, List<List<Tile>> tileMap)
    {
        gridColumns = columns;
        gridRows = rows;

        // 1. dynamic Texture2D 생성
        texture = new Texture2D(gridColumns, gridRows);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (tileRenderer != null && tileRenderer.material != null)
        {
            tileRenderer.material.SetTexture("_MaskTex", texture);
            tileRenderer.material.SetFloat("_GridColumns", gridColumns);
            tileRenderer.material.SetFloat("_GridRows", gridRows);
        }

        // 2. 오버레이 평면 스케일 및 위치 정렬
        transform.localScale = new Vector3(gridColumns * cellSize, gridRows * cellSize, 1f);
        float centerX = gridOffset.x + (gridColumns - 1) * cellSize / 2.0f;
        float centerZ = gridOffset.z + (gridRows - 1) * cellSize / 2.0f;
        
        transform.localPosition = new Vector3(centerX, gridOffset.y + 0.02f, centerZ);
        transform.localRotation = Quaternion.Euler(90, 0, 0);

        // 3. 전체 타일의 이동 가능 여부(B채널)를 칠하여 기본 바닥 렌더링
        RefreshBaseMap(tileMap);
    }

    public void RefreshBaseMap(List<List<Tile>> tileMap)
    {
        if (texture == null) return;

        for (int col = 0; col < gridColumns; col++)
        {
            for (int row = 0; row < gridRows; row++)
            {
                Tile tile = tileMap[col][row];
                // 갈 수 없는 장애물/빈공간(Full, Obstacle)은 B채널 = 0 (투명), 이동 가능 타일은 1 (기본 텍스처 표시)
                float baseMaskVal = (tile.tileState != TileState.Full && tile.tileState != TileState.Obstacle) ? 1f : 0f;
                texture.SetPixel(col, row, new Color(0f, 0f, baseMaskVal, 0f));
            }
        }
        texture.Apply();
    }

    public void ShowWalkableTiles(List<Tile> walkableTiles, List<List<Tile>> tileMap, bool useTargetStyle = true)
    {
        // R, G 하이라이트 리셋 및 B 채널 유지
        for (int col = 0; col < gridColumns; col++)
        {
            for (int row = 0; row < gridRows; row++)
            {
                Tile tile = tileMap[col][row];
                float baseMaskVal = (tile.tileState != TileState.Full && tile.tileState != TileState.Obstacle) ? 1f : 0f;
                // 기존의 R, G 상태를 제거한 상태로 리셋
                texture.SetPixel(col, row, new Color(0f, 0f, baseMaskVal, 0f));
            }
        }

        // 이동 범위 타일 R채널 설정
        foreach (var tile in walkableTiles)
        {
            if (tile.tileState == TileState.Full || tile.tileState == TileState.Obstacle)
                continue;

            int col = tile.GetCoord().x;
            int row = tile.GetCoord().y;

            if (col >= 0 && col < gridColumns && row >= 0 && row < gridRows)
            {
                Color current = texture.GetPixel(col, row);
                // B채널 유지하며 R채널 활성화
                texture.SetPixel(col, row, new Color(1f, 0f, current.b, 0f));
            }
        }

        texture.Apply();
    }

    public void SetCharacterSelectedTile(Tile selectedTile, List<List<Tile>> tileMap)
    {
        if (texture == null) return;

        // 모든 칸에서 캐릭터 선택 채널(G)만 리셋
        for (int col = 0; col < gridColumns; col++)
        {
            for (int row = 0; row < gridRows; row++)
            {
                Color current = texture.GetPixel(col, row);
                texture.SetPixel(col, row, new Color(current.r, 0f, current.b, 0f));
            }
        }

        // 특정 타일 선택 시 G채널 활성화
        if (selectedTile != null)
        {
            int col = selectedTile.GetCoord().x;
            int row = selectedTile.GetCoord().y;

            if (col >= 0 && col < gridColumns && row >= 0 && row < gridRows)
            {
                Color current = texture.GetPixel(col, row);
                texture.SetPixel(col, row, new Color(current.r, 1f, current.b, 0f));
            }
        }

        texture.Apply();
    }

    public void ClearAllTiles(List<List<Tile>> tileMap)
    {
        if (texture == null) return;
        RefreshBaseMap(tileMap);
    }
}
