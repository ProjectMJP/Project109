using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class TmpKoreanFontMigrationTests
{
    private const string LiberationSansGuid = "8f586378b4e144a9851e7b34d9b748ee";

    [Test]
    public void TMPSettings_DefaultAndFallbackFonts_UseKoreanAssetsAndHangulLineBreaking()
    {
        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        Assert.IsNotNull(defaultFont, "TMP_Settings.defaultFontAsset이 null이면 안 됩니다.");
        Assert.IsFalse(TMPFontUtility.IsLiberationSans(defaultFont), "기본 폰트가 LiberationSans이면 안 됩니다.");
        StringAssert.Contains("NEXON Lv2 Gothic OTF SDF", defaultFont.name);

        // 한글 및 도감/정렬 기호 커버리지 검증
        string requiredChars = "가나다유물도감▲▼힣";
        for (int i = 0; i < requiredChars.Length; i++)
        {
            char c = requiredChars[i];
            Assert.IsTrue(
                defaultFont.HasCharacter(c, searchFallbacks: false, tryAddCharacter: false),
                $"기본 폰트에 '{c}' 글리프가 포함되어 있어야 합니다.");
        }

        List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets;
        Assert.IsNotNull(fallbacks, "TMP_Settings.fallbackFontAssets가 초기화되어 있어야 합니다.");
        Assert.GreaterOrEqual(fallbacks.Count, 1, "한글 Dynamic 폴백 폰트가 최소 1개 이상 등록되어야 합니다.");

        bool hasDynamicLineSeed = false;
        for (int i = 0; i < fallbacks.Count; i++)
        {
            TMP_FontAsset fb = fallbacks[i];
            if (fb != null &&
                fb.name.Contains("LINESeedKR-Rg SDF") &&
                fb.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            {
                hasDynamicLineSeed = true;
                break;
            }
        }
        Assert.IsTrue(hasDynamicLineSeed, "LINESeedKR-Rg SDF (Dynamic) 폰트가 전역 폴백으로 등록되어야 합니다.");
        Assert.IsTrue(TMP_Settings.useModernHangulLineBreakingRules, "최신 한글 줄바꿈 규칙이 활성화되어야 합니다.");
    }

    [Test]
    public void AllPrefabsAndScenes_HaveNoLiberationSansReferences_AndTargetPrefabsUseKoreanFonts()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            TMP_Text[] tmps = prefab.GetComponentsInChildren<TMP_Text>(true);
            for (int t = 0; t < tmps.Length; t++)
            {
                TMP_Text tmp = tmps[t];
                if (tmp == null) continue;

                Assert.IsNotNull(tmp.font, $"{path} ({tmp.name})의 폰트가 null입니다.");
                Assert.IsFalse(
                    TMPFontUtility.IsLiberationSans(tmp.font),
                    $"{path} ({tmp.name})가 여전히 LiberationSans를 참조하고 있습니다.");
                Assert.IsNotNull(tmp.fontSharedMaterial, $"{path} ({tmp.name})의 fontSharedMaterial이 null입니다.");
                Assert.IsFalse(
                    tmp.fontSharedMaterial.name.Contains("LiberationSans"),
                    $"{path} ({tmp.name})의 머티리얼이 LiberationSans 아틀라스입니다.");
            }
        }

        string[] targetPrefabs =
        {
            "Assets/Prefab/UI/DamageIndicator.prefab",
            "Assets/Prefab/UI/NPC/Map/ExploreMap.prefab",
            "Assets/Prefab/UI/GameOverPanel.prefab",
            "Assets/Prefab/UI/NPC/ShopUI/ShopNPCUI.prefab",
            "Assets/Prefab/UI/TopHUDPanel.prefab"
        };
        for (int i = 0; i < targetPrefabs.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabs[i]);
            Assert.IsNotNull(prefab, $"대상 프리팹을 찾을 수 없습니다: {targetPrefabs[i]}");
            TMP_Text[] tmps = prefab.GetComponentsInChildren<TMP_Text>(true);
            Assert.Greater(tmps.Length, 0, $"{targetPrefabs[i]}에 TMP_Text가 존재해야 합니다.");
            for (int t = 0; t < tmps.Length; t++)
            {
                Assert.IsTrue(
                    TMPFontUtility.IsKoreanFontAsset(tmps[t].font),
                    $"{targetPrefabs[i]} ({tmps[t].name})가 한글 폰트를 사용해야 합니다.");
            }
        }

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            string raw = File.ReadAllText(scenePath);
            Assert.IsFalse(
                raw.Contains(LiberationSansGuid),
                $"씬 '{scenePath}'에 LiberationSans GUID 참조가 남아있습니다.");
        }
    }

    [Test]
    public void TMPFontUtility_ResolveDefaultFont_ReturnsKoreanFontAndSkipsLiberationSans()
    {
        TMP_FontAsset resolved = TMPFontUtility.ResolveDefaultFont();
        Assert.IsNotNull(resolved);
        Assert.IsFalse(TMPFontUtility.IsLiberationSans(resolved));
        Assert.IsTrue(TMPFontUtility.IsKoreanFontAsset(resolved));

        TMP_FontAsset libFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (libFont != null)
        {
            GameObject go = new GameObject("DummyLibText", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                TextMeshProUGUI dummy = go.GetComponent<TextMeshProUGUI>();
                dummy.font = libFont;

                TMP_FontAsset skipped = TMPFontUtility.ResolveDefaultFont(dummy);
                Assert.IsNotNull(skipped);
                Assert.IsFalse(TMPFontUtility.IsLiberationSans(skipped));
                Assert.IsTrue(TMPFontUtility.IsKoreanFontAsset(skipped));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
