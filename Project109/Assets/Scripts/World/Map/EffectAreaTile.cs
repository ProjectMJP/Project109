using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using CardTypes;

public enum TileType
{
    Empty,
    TargetTile,
    AdditionalEffectTile
}

public class EffectAreaTile : MonoBehaviour
{
    public Renderer tileRenderer;
    private Texture2D texture;

    [SerializeField] private int textureWidth = 11;
    [SerializeField] private int textureHeight = 11;

    private int centerWidth;
    private int centerHeight;

    GridAreaSearch gridAreaSearch;

    public bool isAdditionalEffectAreaActive;

    private void Start()
    {
        texture = new Texture2D(textureHeight, textureWidth);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] resetColors = new Color32[textureHeight * textureWidth];
        for(int i = 0; i < resetColors.Length; i++)
        {
            resetColors[i] = Color.clear;
        }

        texture.SetPixels32(resetColors);
        texture.Apply();

        tileRenderer.material.SetTexture("_MaskTex", texture);

        centerWidth = textureWidth / 2;
        centerHeight = textureHeight / 2;

        gridAreaSearch = new GridAreaSearch();

    }

    public void SetTileColor(int x, int y, TileType type)
    {
        Color controlColor = Color.clear;

        switch(type)
        {
            case TileType.Empty:                controlColor = new Color(0, 0, 0, 0); break;
            case TileType.TargetTile:           controlColor = new Color(1, 0, 0, 0); break;    //R 채널에 타켓 타일 활성화
            case TileType.AdditionalEffectTile: controlColor = new Color(0, 1, 0, 0); break;    //G 채널에 추가 효과 타일 활성화
        }

        texture.SetPixel(x, y, controlColor);
        texture.Apply();
    }

    public void SetTileFromTargetDistance(TargetType cardTargetType, int minDistance, int maxDistance, TileType type)
    {
        if(gridAreaSearch == null)
            return;

        // 특정 거리 범위 내의 타일 좌표들을 가져옴
        List<Vector2Int> areaTiles = gridAreaSearch.GetGridArea(cardTargetType, new Vector2Int(textureWidth, textureHeight), centerWidth, centerHeight, minDistance, maxDistance);

        //쉐이더에 맞는 색상 설정
        Color controlColor = Color.clear;
        switch (type)
        {
            case TileType.Empty:                controlColor = new Color(0, 0, 0, 0); break;
            case TileType.TargetTile:           controlColor = new Color(1, 0, 0, 0); break;    //R 채널에 타켓 타일 활성화
            case TileType.AdditionalEffectTile: controlColor = new Color(0, 1, 0, 0); break;    //G 채널에 추가 효과 타일 활성화
        }

        //타일 변경 진행
        foreach (var coord in areaTiles)
        {
            texture.SetPixel(coord.y, coord.x, controlColor);
        }

        texture.Apply();
    }

    public void SetTileFromShapeGenerator(string shapeName, int shapeLength, int radius, TileType type)
    {
        if (gridAreaSearch == null)
            return;

        // 특정 거리 범위 내의 타일 좌표들을 가져옴
        List<Vector2Int> areaTiles = EffectAreaShapeGenerator.instance.GetShapePositions(shapeName, 
                                                                                         new Vector2Int(textureWidth, textureHeight),
                                                                                         shapeLength,
                                                                                         centerWidth,
                                                                                         centerHeight,
                                                                                         radius);

        //쉐이더에 맞는 색상 설정
        Color controlColor = Color.clear;
        switch (type)
        {
            case TileType.Empty: controlColor = new Color(0, 0, 0, 0); break;
            case TileType.TargetTile: controlColor = new Color(1, 0, 0, 0); break;    //R 채널에 타켓 타일 활성화
            case TileType.AdditionalEffectTile: controlColor = new Color(0, 1, 0, 0); break;    //G 채널에 추가 효과 타일 활성화
        }

        //타일 변경 진행
        foreach (var coord in areaTiles)
        {
            texture.SetPixel(coord.y, coord.x, controlColor);
        }

        texture.Apply();
    }

    public void ClearAllTiles()
    {
        Color32[] resetColors = new Color32[textureHeight * textureWidth];
        for (int i = 0; i < resetColors.Length; i++)
        {
            resetColors[i] = Color.clear;
        }
        texture.SetPixels32(resetColors);
        texture.Apply();
    }
}
