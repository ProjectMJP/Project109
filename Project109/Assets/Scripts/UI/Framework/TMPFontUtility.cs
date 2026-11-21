using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 런타임 동적 UI 생성 시 한글 지원 TMP 폰트(NEXON / LINE Seed)를 일관되게 조회하는 공용 유틸리티입니다.
/// </summary>
public static class TMPFontUtility
{
    public static bool IsLiberationSans(TMP_FontAsset font)
    {
        if (font == null || string.IsNullOrEmpty(font.name))
        {
            return false;
        }

        return font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsKoreanFontAsset(TMP_FontAsset font)
    {
        if (font == null || string.IsNullOrEmpty(font.name))
        {
            return false;
        }

        return font.name.IndexOf("NEXON", StringComparison.OrdinalIgnoreCase) >= 0 ||
               font.name.IndexOf("LINESeed", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 우선 참조 텍스트 컴포넌트, 전역 TMP_Settings, 로드된 한글 폰트 에셋 순으로 한글 지원 TMP_FontAsset을 반환합니다.
    /// </summary>
    public static TMP_FontAsset ResolveDefaultFont(params TMP_Text[] preferredSources)
    {
        if (preferredSources != null)
        {
            for (int i = 0; i < preferredSources.Length; i++)
            {
                TMP_Text source = preferredSources[i];
                if (source != null && source.font != null && !IsLiberationSans(source.font))
                {
                    return source.font;
                }
            }
        }

        TMP_FontAsset settingsDefault = TMP_Settings.defaultFontAsset;
        if (settingsDefault != null && !IsLiberationSans(settingsDefault))
        {
            return settingsDefault;
        }

        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        if (loadedFonts != null)
        {
            for (int i = 0; i < loadedFonts.Length; i++)
            {
                TMP_FontAsset font = loadedFonts[i];
                if (font != null && font.name.IndexOf("NEXON Lv2 Gothic OTF SDF", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return font;
                }
            }

            for (int i = 0; i < loadedFonts.Length; i++)
            {
                TMP_FontAsset font = loadedFonts[i];
                if (IsKoreanFontAsset(font))
                {
                    return font;
                }
            }
        }

        if (settingsDefault != null)
        {
            return settingsDefault;
        }

        if (loadedFonts != null && loadedFonts.Length > 0)
        {
            return loadedFonts[0];
        }

        return null;
    }
}
