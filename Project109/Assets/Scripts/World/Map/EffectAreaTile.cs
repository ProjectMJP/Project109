using UnityEngine;
using System.Collections.Generic;
using CardTypes;

/// <summary>
/// 타일 효과 영역 셰이더 마스크 텍스처의 제어 채널 타입
/// </summary>
public enum AreaIndicatorChannel
{
    Empty,
    TargetTile,          // R 채널: 타깃 타일 영역
    AdditionalEffectTile // G 채널: 추가 효과(AoE) 영역
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
        texture = new Texture2D(textureWidth, textureHeight);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] resetColors = new Color32[textureWidth * textureHeight];
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

    private Color GetChannelColor(AreaIndicatorChannel channel)
    {
        return channel switch
        {
            AreaIndicatorChannel.TargetTile => new Color(1, 0, 0, 0),          // R 채널에 타겟 타일 활성화
            AreaIndicatorChannel.AdditionalEffectTile => new Color(0, 1, 0, 0),// G 채널에 추가 효과 타일 활성화
            _ => Color.clear
        };
    }

    public void SetTileColor(int x, int y, AreaIndicatorChannel type)
    {
        texture.SetPixel(x, y, GetChannelColor(type));
        texture.Apply();
    }

    public void SetTileFromTargetDistance(TargetType cardTargetType, int minDistance, int maxDistance, AreaIndicatorChannel type)
    {
        if (gridAreaSearch == null) return;

        List<Vector2Int> areaTiles = gridAreaSearch.GetGridArea(cardTargetType, new Vector2Int(textureWidth, textureHeight), centerWidth, centerHeight, minDistance, maxDistance);
        Color controlColor = GetChannelColor(type);

        foreach (var coord in areaTiles)
        {
            texture.SetPixel(coord.x, coord.y, controlColor);
        }
        texture.Apply();
    }

    public void SetTileFromShapeGenerator(string shapeName, int shapeLength, int radius, AreaIndicatorChannel type)
    {
        if (gridAreaSearch == null) return;

        List<Vector2Int> areaTiles = EffectAreaShapeGenerator.GetShapePositions(shapeName, 
                                                                                new Vector2Int(textureWidth, textureHeight),
                                                                                shapeLength,
                                                                                centerWidth,
                                                                                centerHeight,
                                                                                radius);
        Color controlColor = GetChannelColor(type);

        foreach (var coord in areaTiles)
        {
            texture.SetPixel(coord.x, coord.y, controlColor);
        }
        texture.Apply();
    }

    public void ClearAllTiles()
    {
        Color32[] resetColors = new Color32[textureWidth * textureHeight];
        for (int i = 0; i < resetColors.Length; i++)
        {
            resetColors[i] = Color.clear;
        }
        texture.SetPixels32(resetColors);
        texture.Apply();
    }
}
