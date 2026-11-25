using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 좌·우 사이드 유닛 상세 HUD 프리팹(BattleSideUnitCardUI, BattleSideUnitHUDPanel) 생성,
/// 컴팩트 월드 상태바(64x14)·도트 이펙트 목록(2-B안)·툴팁 힌트·TopHUD 토글 버튼 갱신,
/// 레거시 CharacterEffectDetailUI/Item 프리팹 및 Addressables 엔트리 Clean Cut 정리를 수행하는 에디터 도구입니다.
/// </summary>
public static class SetupBattleSideUnitHUDAndCompactWorldUI
{
    private const string BattleUiDir = "Assets/Prefab/UI/Battle";
    private const string LegacyDetailPrefabPath = "Assets/Prefab/UI/Battle/CharacterEffectDetailUI.prefab";
    private const string LegacyDetailItemPrefabPath = "Assets/Prefab/UI/Battle/CharacterEffectDetailItemUI.prefab";

    private const string StatusBarPrefabPath = "Assets/Prefab/UI/Battle/CharacterStatusBarUI.prefab";
    private const string EffectListPrefabPath = "Assets/Prefab/UI/Battle/CharacterEffectListUI.prefab";
    private const string EffectItemPrefabPath = "Assets/Prefab/UI/Battle/CharacterEffectItemUI.prefab";
    private const string TooltipPanelPrefabPath = "Assets/Prefab/UI/TooltipPanel.prefab";
    private const string TopHudPrefabPath = "Assets/Prefab/UI/TopHUDPanel.prefab";
    private const string SideCardPrefabPath = "Assets/Prefab/UI/Battle/BattleSideUnitCardUI.prefab";
    private const string SideHudPrefabPath = "Assets/Prefab/UI/Battle/BattleSideUnitHUDPanel.prefab";

    private const string PlayerPrefabPath = "Assets/Prefab/PlayerCharacterSample.prefab";
    private const string WarriorPrefabPath = "Assets/Prefab/Enemy/Enemy_SkeletonWarrior.prefab";
    private const string GuardPrefabPath = "Assets/Prefab/Enemy/Enemy_SkeletonGuard.prefab";

    [MenuItem("Tools/UI/Setup Battle Side Unit HUD And Compact World UI")]
    public static string SetupAll()
    {
        if (!Directory.Exists(BattleUiDir))
        {
            Directory.CreateDirectory(BattleUiDir);
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;

        int worldUiLayer = LayerMask.NameToLayer("WorldUI");
        if (worldUiLayer < 0) worldUiLayer = 11;

        TMP_FontAsset baseFont = ResolveBaseFont();
        Sprite squareSprite = ResolveSquareSprite();

        // 1. 레거시 CharacterEffectDetailUI / CharacterEffectDetailItemUI Clean Cut 삭제 및 Addressables 정리
        CleanupLegacyEffectDetailPrefabsAndAddressables();

        // 2. 월드 컴팩트 상태바 (64x14) 및 도트 이펙트 리스트 (2-B안) 갱신
        UpdateCompactStatusBarPrefab(worldUiLayer, baseFont, squareSprite);
        UpdateCompactEffectListPrefab(worldUiLayer);

        // 3. 캐릭터 프리팹 3종 오프셋 컴팩트화 (발아래 상태바/도트, 머리 위 적 행동 예고, 기존 BoxCollider 유지)
        ConfigureCharacterOffsets(
            PlayerPrefabPath,
            statusBarOffset: new Vector3(0f, -0.14f, 0f),
            effectListOffset: new Vector3(0f, -0.30f, 0f),
            intentOffset: null);
        ConfigureCharacterOffsets(
            WarriorPrefabPath,
            statusBarOffset: new Vector3(0f, -0.14f, 0f),
            effectListOffset: new Vector3(0f, -0.30f, 0f),
            intentOffset: new Vector3(0f, 1.72f, 0f));
        ConfigureCharacterOffsets(
            GuardPrefabPath,
            statusBarOffset: new Vector3(0f, -0.14f, 0f),
            effectListOffset: new Vector3(0f, -0.30f, 0f),
            intentOffset: new Vector3(0f, 1.86f, 0f));

        // 4. TooltipPanel 미니 힌트 캡션 및 TopHUDPanel [T] 툴팁 토글 버튼 구성
        UpdateTooltipPanelPrefab(baseFont);
        UpdateTopHudPanelPrefab(uiLayer, baseFont, squareSprite);

        // 5. 사이드 유닛 상세 카드 & HUD 패널 프리팹 생성 (Layer 5 UI, 중첩 Canvas 없음)
        BattleSideUnitCardUI cardPrefab = CreateOrUpdateSideUnitCardPrefab(uiLayer, baseFont, squareSprite);
        CreateOrUpdateSideUnitHudPanelPrefab(uiLayer, cardPrefab);

        // 6. Addressables UI_Prefabs 그룹 재등록 및 잔여 유실 엔트리 정리
        AddressableAutoRegister.RegisterAllUIPrefabs();
        PruneMissingUiPrefabEntries();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = "[SetupBattleSideUnitHUDAndCompactWorldUI] 레거시 2종 삭제, 컴팩트 월드바/도트/툴팁/TopHUD 갱신, 사이드 HUD 프리팹 2종 생성 및 Addressables 등록 완료";
        Debug.Log(summary);
        return summary;
    }

    private static TMP_FontAsset ResolveBaseFont()
    {
        GameObject topHud = AssetDatabase.LoadAssetAtPath<GameObject>(TopHudPrefabPath);
        if (topHud != null)
        {
            TextMeshProUGUI tmp = topHud.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && tmp.font != null) return tmp.font;
        }

        GameObject effectItem = AssetDatabase.LoadAssetAtPath<GameObject>(EffectItemPrefabPath);
        if (effectItem != null)
        {
            TextMeshProUGUI tmp = effectItem.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && tmp.font != null) return tmp.font;
        }

        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NEXON Lv2 Gothic OTF SDF.asset");
        if (fallback == null)
        {
            Debug.LogError("[SetupBattleSideUnitHUDAndCompactWorldUI] 기본 TMP_FontAsset를 찾을 수 없습니다.");
        }
        return fallback;
    }

    private static Sprite ResolveSquareSprite()
    {
        GameObject statusBar = AssetDatabase.LoadAssetAtPath<GameObject>(StatusBarPrefabPath);
        if (statusBar != null)
        {
            Image[] images = statusBar.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].sprite != null)
                {
                    return images[i].sprite;
                }
            }
        }

        Sprite fallback = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resource/Square.png");
        if (fallback == null)
        {
            Debug.LogError("[SetupBattleSideUnitHUDAndCompactWorldUI] 기본 Square 스프라이트를 찾을 수 없습니다.");
        }
        return fallback;
    }

    private static void CleanupLegacyEffectDetailPrefabsAndAddressables()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
        {
            AddressableAssetGroup uiGroup = settings.FindGroup("UI_Prefabs");
            if (uiGroup != null)
            {
                List<AddressableAssetEntry> toRemove = new List<AddressableAssetEntry>();
                foreach (AddressableAssetEntry entry in uiGroup.entries)
                {
                    if (entry == null) continue;
                    if (entry.address == "CharacterEffectDetailUI" ||
                        entry.address == "CharacterEffectDetailItemUI" ||
                        entry.AssetPath == LegacyDetailPrefabPath ||
                        entry.AssetPath == LegacyDetailItemPrefabPath)
                    {
                        toRemove.Add(entry);
                    }
                }

                for (int i = 0; i < toRemove.Count; i++)
                {
                    settings.RemoveAssetEntry(toRemove[i].guid);
                }
                if (toRemove.Count > 0)
                {
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, null, true, true);
                }
            }
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(LegacyDetailPrefabPath) != null)
        {
            AssetDatabase.DeleteAsset(LegacyDetailPrefabPath);
        }
        if (AssetDatabase.LoadAssetAtPath<Object>(LegacyDetailItemPrefabPath) != null)
        {
            AssetDatabase.DeleteAsset(LegacyDetailItemPrefabPath);
        }
    }

    private static void PruneMissingUiPrefabEntries()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        AddressableAssetGroup uiGroup = settings.FindGroup("UI_Prefabs");
        if (uiGroup == null) return;

        List<AddressableAssetEntry> invalidEntries = new List<AddressableAssetEntry>();
        foreach (AddressableAssetEntry entry in uiGroup.entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.AssetPath) || !File.Exists(entry.AssetPath))
            {
                invalidEntries.Add(entry);
            }
        }

        for (int i = 0; i < invalidEntries.Count; i++)
        {
            if (invalidEntries[i] != null)
            {
                settings.RemoveAssetEntry(invalidEntries[i].guid);
            }
        }

        if (invalidEntries.Count > 0)
        {
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, null, true, true);
        }
    }

    [MenuItem("Tools/UI/Update StatusBar Damage Preview Prefab")]
    public static void UpdateStatusBarDamagePreviewPrefab()
    {
        int worldUiLayer = LayerMask.NameToLayer("WorldUI");
        if (worldUiLayer < 0) worldUiLayer = 11;

        TMP_FontAsset baseFont = ResolveBaseFont();
        Sprite squareSprite = ResolveSquareSprite();

        GameObject root = PrefabUtility.LoadPrefabContents(StatusBarPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 프리팹 로드 실패: {StatusBarPrefabPath}");
            return;
        }

        try
        {
            ConfigureStatusBarDamagePreviewChildren(root, worldUiLayer, baseFont, squareSprite);
            SetLayerRecursively(root, worldUiLayer);

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, StatusBarPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 저장 실패: {StatusBarPrefabPath}");
            }
            else
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[SetupBattleSideUnitHUDAndCompactWorldUI] CharacterStatusBarUI 예상 피해 미리보기 UI 프리팹 갱신 완료");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpdateCompactStatusBarPrefab(int worldUiLayer, TMP_FontAsset baseFont, Sprite squareSprite)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(StatusBarPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 프리팹 로드 실패: {StatusBarPrefabPath}");
            return;
        }

        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(64f, 14f);

            // 미사용 레거시 EffectContainer 자식 제거
            Transform legacyEffectContainer = root.transform.Find("EffectContainer");
            if (legacyEffectContainer != null)
            {
                Object.DestroyImmediate(legacyEffectContainer.gameObject);
            }

            // HealthGroup (상단 64%) / StaminaGroup (하단 36%)
            GaugeUI healthGauge = ConfigureCompactGaugeGroup(
                root.transform,
                groupName: "HealthGroup",
                bgName: "HealthBackground",
                fillName: "HealthFill",
                anchorMin: new Vector2(0f, 0.36f),
                anchorMax: new Vector2(1f, 1f),
                fillColor: UIConstants.COLOR_HEALTH,
                squareSprite: squareSprite);

            GaugeUI staminaGauge = ConfigureCompactGaugeGroup(
                root.transform,
                groupName: "StaminaGroup",
                bgName: "StaminaBackground",
                fillName: "StaminaFill",
                anchorMin: new Vector2(0f, 0f),
                anchorMax: new Vector2(1f, 0.36f),
                fillColor: UIConstants.COLOR_STAMINA,
                squareSprite: squareSprite);

            // 모든 Graphic raycastTarget = false 보장
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null) graphics[i].raycastTarget = false;
            }

            CharacterStatusBarUI statusBar = root.GetComponent<CharacterStatusBarUI>();
            SerializedObject serializedStatus = new SerializedObject(statusBar);
            serializedStatus.FindProperty("worldOffset").vector3Value = UIConstants.DEFAULT_STATUS_BAR_OFFSET;
            serializedStatus.FindProperty("useBillboard").boolValue = true;
            serializedStatus.FindProperty("healthGaugeUI").objectReferenceValue = healthGauge;
            serializedStatus.FindProperty("staminaGaugeUI").objectReferenceValue = staminaGauge;
            serializedStatus.FindProperty("showValueText").boolValue = false;
            serializedStatus.ApplyModifiedPropertiesWithoutUndo();

            ConfigureStatusBarDamagePreviewChildren(root, worldUiLayer, baseFont, squareSprite);

            SetLayerRecursively(root, worldUiLayer);

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, StatusBarPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 저장 실패: {StatusBarPrefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureStatusBarDamagePreviewChildren(
        GameObject root,
        int worldUiLayer,
        TMP_FontAsset baseFont,
        Sprite squareSprite)
    {
        if (root == null) return;

        Transform dmgContainerTr = EnsurePreviewPillChild(
            root.transform,
            containerName: "DamagePreviewContainer",
            bgName: "DamagePreviewBackground",
            textName: "DamagePreviewText",
            anchoredPos: new Vector2(0f, 16f),
            sizeDelta: new Vector2(86f, 16f),
            bgColor: new Color(0.08f, 0.08f, 0.12f, 0.90f),
            textColor: new Color(1f, 0.88f, 0.85f, 1f),
            fontSize: 11f,
            defaultText: "-10",
            worldUiLayer: worldUiLayer,
            baseFont: baseFont,
            squareSprite: squareSprite,
            out Image dmgBgImg,
            out TextMeshProUGUI dmgTmp);

        Transform badgeContainerTr = EnsurePreviewPillChild(
            root.transform,
            containerName: "FacingBadgeContainer",
            bgName: "FacingBadgeBackground",
            textName: "FacingBadgeText",
            anchoredPos: new Vector2(0f, 31f),
            sizeDelta: new Vector2(86f, 13f),
            bgColor: new Color(0.95f, 0.52f, 0.08f, 0.96f),
            textColor: new Color(1f, 0.98f, 0.92f, 1f),
            fontSize: 9f,
            defaultText: "정후면 1.5x",
            worldUiLayer: worldUiLayer,
            baseFont: baseFont,
            squareSprite: squareSprite,
            out Image badgeBgImg,
            out TextMeshProUGUI badgeTmp);

        dmgContainerTr.gameObject.SetActive(false);
        badgeContainerTr.gameObject.SetActive(false);

        CharacterStatusBarUI statusBar = root.GetComponent<CharacterStatusBarUI>();
        if (statusBar != null)
        {
            SerializedObject serializedStatus = new SerializedObject(statusBar);
            serializedStatus.FindProperty("damagePreviewContainer").objectReferenceValue = dmgContainerTr.gameObject;
            serializedStatus.FindProperty("damagePreviewBackground").objectReferenceValue = dmgBgImg;
            serializedStatus.FindProperty("damagePreviewText").objectReferenceValue = dmgTmp;
            serializedStatus.FindProperty("facingBadgeContainer").objectReferenceValue = badgeContainerTr.gameObject;
            serializedStatus.FindProperty("facingBadgeBackground").objectReferenceValue = badgeBgImg;
            serializedStatus.FindProperty("facingBadgeText").objectReferenceValue = badgeTmp;
            serializedStatus.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Transform EnsurePreviewPillChild(
        Transform parent,
        string containerName,
        string bgName,
        string textName,
        Vector2 anchoredPos,
        Vector2 sizeDelta,
        Color bgColor,
        Color textColor,
        float fontSize,
        string defaultText,
        int worldUiLayer,
        TMP_FontAsset baseFont,
        Sprite squareSprite,
        out Image bgImage,
        out TextMeshProUGUI textComp)
    {
        Transform containerTr = parent.Find(containerName);
        if (containerTr == null)
        {
            GameObject go = new GameObject(containerName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            containerTr = go.transform;
        }
        containerTr.gameObject.layer = worldUiLayer;

        RectTransform containerRect = containerTr.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = anchoredPos;
        containerRect.sizeDelta = sizeDelta;

        Transform bgTr = containerTr.Find(bgName);
        if (bgTr == null)
        {
            GameObject bgGo = new GameObject(bgName, typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(containerTr, false);
            bgTr = bgGo.transform;
        }
        bgTr.gameObject.layer = worldUiLayer;
        RectTransform bgRect = bgTr.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        bgImage = bgTr.GetComponent<Image>();
        if (bgImage == null) bgImage = bgTr.gameObject.AddComponent<Image>();
        if (squareSprite != null) bgImage.sprite = squareSprite;
        bgImage.type = Image.Type.Simple;
        bgImage.color = bgColor;
        bgImage.raycastTarget = false;

        Transform textTr = containerTr.Find(textName);
        if (textTr == null)
        {
            GameObject textGo = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(containerTr, false);
            textTr = textGo.transform;
        }
        textTr.gameObject.layer = worldUiLayer;
        RectTransform textRect = textTr.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(2f, 0f);
        textRect.offsetMax = new Vector2(-2f, 0f);

        textComp = textTr.GetComponent<TextMeshProUGUI>();
        if (textComp == null) textComp = textTr.gameObject.AddComponent<TextMeshProUGUI>();
        if (baseFont != null) textComp.font = baseFont;
        textComp.fontSize = fontSize;
        textComp.fontStyle = FontStyles.Bold;
        textComp.alignment = TextAlignmentOptions.Center;
        textComp.textWrappingMode = TextWrappingModes.NoWrap;
        textComp.color = textColor;
        textComp.raycastTarget = false;
        textComp.text = defaultText;

        return containerTr;
    }

    private static GaugeUI ConfigureCompactGaugeGroup(
        Transform parent,
        string groupName,
        string bgName,
        string fillName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color fillColor,
        Sprite squareSprite)
    {
        Transform groupTr = parent.Find(groupName);
        if (groupTr == null) return null;

        RectTransform groupRect = groupTr.GetComponent<RectTransform>();
        groupRect.anchorMin = anchorMin;
        groupRect.anchorMax = anchorMax;
        groupRect.pivot = new Vector2(0.5f, 0.5f);
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;

        Transform bgTr = groupTr.Find(bgName);
        if (bgTr != null)
        {
            GaugeUI strayBgGauge = bgTr.GetComponent<GaugeUI>();
            if (strayBgGauge != null)
            {
                Object.DestroyImmediate(strayBgGauge);
            }

            Image bgImg = bgTr.GetComponent<Image>();
            if (bgImg != null)
            {
                if (bgImg.sprite == null && squareSprite != null) bgImg.sprite = squareSprite;
                bgImg.type = Image.Type.Simple;
                bgImg.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);
                bgImg.raycastTarget = false;
            }
        }

        Transform textTr = groupTr.Find("Text");
        TextMeshProUGUI valueTmp = textTr != null ? textTr.GetComponent<TextMeshProUGUI>() : null;
        if (textTr != null)
        {
            textTr.gameObject.SetActive(false);
        }
        if (valueTmp != null)
        {
            valueTmp.raycastTarget = false;
        }

        Transform fillTr = groupTr.Find(fillName);
        if (fillTr == null) return null;

        Image fillImg = fillTr.GetComponent<Image>();
        if (fillImg != null)
        {
            if (fillImg.sprite == null && squareSprite != null) fillImg.sprite = squareSprite;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1f;
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;
        }

        GaugeUI gauge = fillTr.GetComponent<GaugeUI>();
        if (gauge == null)
        {
            gauge = fillTr.gameObject.AddComponent<GaugeUI>();
        }

        SerializedObject serializedGauge = new SerializedObject(gauge);
        serializedGauge.FindProperty("gaugeImageFilled").objectReferenceValue = fillImg;
        serializedGauge.FindProperty("gaugeText").objectReferenceValue = valueTmp;
        serializedGauge.ApplyModifiedPropertiesWithoutUndo();

        return gauge;
    }

    private static void UpdateCompactEffectListPrefab(int worldUiLayer)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EffectListPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 프리팹 로드 실패: {EffectListPrefabPath}");
            return;
        }

        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(64f, 10f);

            // 고정 폭 64 유지를 위해 ContentSizeFitter 제거
            ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                Object.DestroyImmediate(fitter);
            }

            HorizontalLayoutGroup hlg = root.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = root.AddComponent<HorizontalLayoutGroup>();
            }
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 3f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            CharacterEffectListUI effectList = root.GetComponent<CharacterEffectListUI>();
            SerializedObject serializedList = new SerializedObject(effectList);
            serializedList.FindProperty("worldOffset").vector3Value = UIConstants.DEFAULT_EFFECT_LIST_OFFSET;
            serializedList.FindProperty("useBillboard").boolValue = true;
            serializedList.FindProperty("effectContainer").objectReferenceValue = root.transform;
            serializedList.FindProperty("useDotMode").boolValue = true;
            serializedList.FindProperty("dotSize").floatValue = 7f;
            serializedList.FindProperty("buffDotColor").colorValue = new Color(0.24f, 0.78f, 1f, 1f);
            serializedList.FindProperty("debuffDotColor").colorValue = new Color(0.95f, 0.28f, 0.30f, 1f);
            serializedList.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, worldUiLayer);

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, EffectListPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 저장 실패: {EffectListPrefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureCharacterOffsets(
        string prefabPath,
        Vector3 statusBarOffset,
        Vector3 effectListOffset,
        Vector3? intentOffset)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 캐릭터 프리팹 로드 실패: {prefabPath}");
            return;
        }

        try
        {
            CharacterUIController uiCtrl = root.GetComponent<CharacterUIController>();
            if (uiCtrl == null)
            {
                Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] CharacterUIController 없음: {prefabPath}");
                return;
            }

            SerializedObject serializedCtrl = new SerializedObject(uiCtrl);
            serializedCtrl.FindProperty("statusBarOffset").vector3Value = statusBarOffset;
            serializedCtrl.FindProperty("effectListOffset").vector3Value = effectListOffset;
            if (intentOffset.HasValue)
            {
                serializedCtrl.FindProperty("intentOffset").vector3Value = intentOffset.Value;
            }
            serializedCtrl.ApplyModifiedPropertiesWithoutUndo();

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 캐릭터 프리팹 저장 실패: {prefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpdateTooltipPanelPrefab(TMP_FontAsset baseFont)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(TooltipPanelPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 프리팹 로드 실패: {TooltipPanelPrefabPath}");
            return;
        }

        try
        {
            Transform hintTr = root.transform.Find("HintCaptionText");
            GameObject hintObj;
            if (hintTr == null)
            {
                hintObj = new GameObject("HintCaptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
                hintObj.transform.SetParent(root.transform, false);
            }
            else
            {
                hintObj = hintTr.gameObject;
            }

            hintObj.layer = root.layer;
            hintObj.transform.SetAsLastSibling();

            RectTransform hintRect = hintObj.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0f, 0.5f);
            hintRect.anchorMax = new Vector2(1f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.sizeDelta = new Vector2(0f, 16f);

            TextMeshProUGUI hintTmp = hintObj.GetComponent<TextMeshProUGUI>();
            if (hintTmp == null) hintTmp = hintObj.AddComponent<TextMeshProUGUI>();
            if (baseFont != null) hintTmp.font = baseFont;
            hintTmp.text = "[T] 툴팁 숨기기";
            hintTmp.fontSize = 11f;
            hintTmp.fontStyle = FontStyles.Normal;
            hintTmp.alignment = TextAlignmentOptions.Right;
            hintTmp.color = new Color(0.659f, 0.698f, 0.757f, 1f); // #A8B2C1
            hintTmp.textWrappingMode = TextWrappingModes.NoWrap;
            hintTmp.raycastTarget = false;
            hintObj.SetActive(false);

            TooltipPanel tooltipPanel = root.GetComponent<TooltipPanel>();
            SerializedObject serializedTooltip = new SerializedObject(tooltipPanel);
            serializedTooltip.FindProperty("_hintCaptionText").objectReferenceValue = hintTmp;
            serializedTooltip.ApplyModifiedPropertiesWithoutUndo();

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, TooltipPanelPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 저장 실패: {TooltipPanelPrefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpdateTopHudPanelPrefab(int uiLayer, TMP_FontAsset baseFont, Sprite squareSprite)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(TopHudPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 프리팹 로드 실패: {TopHudPrefabPath}");
            return;
        }

        try
        {
            Transform hudPanel = root.transform.Find("HUDPanel");
            if (hudPanel == null)
            {
                Debug.LogError("[SetupBattleSideUnitHUDAndCompactWorldUI] TopHUDPanel/HUDPanel을 찾을 수 없습니다.");
                return;
            }

            Transform btnTr = hudPanel.Find("TooltipToggleButton");
            GameObject btnObj;
            if (btnTr == null)
            {
                btnObj = new GameObject("TooltipToggleButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(hudPanel, false);
            }
            else
            {
                btnObj = btnTr.gameObject;
            }

            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-228f, 0f);
            btnRect.sizeDelta = new Vector2(128f, 34f);

            Image btnBg = btnObj.GetComponent<Image>();
            if (btnBg == null) btnBg = btnObj.AddComponent<Image>();
            if (squareSprite != null) btnBg.sprite = squareSprite;
            btnBg.color = new Color(0.14f, 0.28f, 0.36f, 0.95f);
            btnBg.raycastTarget = true;

            Button toggleBtn = btnObj.GetComponent<Button>();
            if (toggleBtn == null) toggleBtn = btnObj.AddComponent<Button>();
            toggleBtn.targetGraphic = btnBg;

            Transform labelTr = btnObj.transform.Find("Label");
            GameObject labelObj;
            if (labelTr == null)
            {
                labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObj.transform.SetParent(btnObj.transform, false);
            }
            else
            {
                labelObj = labelTr.gameObject;
            }

            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = new Vector2(4f, 2f);
            labelRect.offsetMax = new Vector2(-4f, -2f);

            TextMeshProUGUI labelTmp = labelObj.GetComponent<TextMeshProUGUI>();
            if (labelTmp == null) labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            if (baseFont != null) labelTmp.font = baseFont;
            labelTmp.text = "[T] 툴팁: ON";
            labelTmp.fontSize = 13f;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color = new Color(0.72f, 0.96f, 1f, 1f);
            labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
            labelTmp.raycastTarget = false;

            SetLayerRecursively(btnObj, uiLayer);

            TopHUDPanel topHud = root.GetComponent<TopHUDPanel>();
            SerializedObject serializedTop = new SerializedObject(topHud);
            serializedTop.FindProperty("tooltipToggleButton").objectReferenceValue = toggleBtn;
            serializedTop.FindProperty("tooltipToggleText").objectReferenceValue = labelTmp;
            serializedTop.FindProperty("tooltipToggleBgImage").objectReferenceValue = btnBg;
            serializedTop.ApplyModifiedPropertiesWithoutUndo();

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, TopHudPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 저장 실패: {TopHudPrefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static BattleSideUnitCardUI CreateOrUpdateSideUnitCardPrefab(int uiLayer, TMP_FontAsset baseFont, Sprite squareSprite)
    {
        CharacterEffectItemUI effectItemPrefabAsset = AssetDatabase.LoadAssetAtPath<CharacterEffectItemUI>(EffectItemPrefabPath);
        if (effectItemPrefabAsset == null)
        {
            Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] CharacterEffectItemUI 로드 실패: {EffectItemPrefabPath}");
        }

        GameObject root = new GameObject(
            "BattleSideUnitCardUI",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement),
            typeof(BattleSideUnitCardUI));

        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(270f, 96f);

            // 루트 Image를 2px 팀 테두리 겸 전체 카드 포인터 호버 히트 영역으로 사용
            Image borderImg = root.GetComponent<Image>();
            borderImg.sprite = squareSprite;
            borderImg.color = new Color(0.28f, 0.62f, 1f, 1f);
            borderImg.raycastTarget = true;

            LayoutElement rootLayout = root.GetComponent<LayoutElement>();
            rootLayout.minHeight = 92f;
            rootLayout.preferredHeight = 96f;
            rootLayout.preferredWidth = 270f;
            rootLayout.flexibleWidth = 0f;
            rootLayout.flexibleHeight = 0f;

            // 내부 반투명 배경 (2px 인셋 + 내부 4px 패딩 = 총 6px 여백)
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            bgObj.transform.SetParent(root.transform, false);
            SetStretchRect(bgObj.GetComponent<RectTransform>(), 2f);

            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.sprite = squareSprite;
            bgImg.color = new Color(0.11f, 0.17f, 0.28f, 0.92f);
            bgImg.raycastTarget = false;

            VerticalLayoutGroup bgVlg = bgObj.GetComponent<VerticalLayoutGroup>();
            bgVlg.padding = new RectOffset(4, 4, 4, 4);
            bgVlg.spacing = 4f;
            bgVlg.childAlignment = TextAnchor.UpperLeft;
            bgVlg.childControlWidth = true;
            bgVlg.childControlHeight = false;
            bgVlg.childForceExpandWidth = true;
            bgVlg.childForceExpandHeight = false;

            // 상단 박스 (PortraitBox + InfoColumn)
            GameObject topBox = new GameObject("TopBox", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            topBox.transform.SetParent(bgObj.transform, false);
            RectTransform topRect = topBox.GetComponent<RectTransform>();
            topRect.sizeDelta = new Vector2(0f, 54f);

            LayoutElement topLayoutElem = topBox.GetComponent<LayoutElement>();
            topLayoutElem.minHeight = 54f;
            topLayoutElem.preferredHeight = 54f;

            HorizontalLayoutGroup topHlg = topBox.GetComponent<HorizontalLayoutGroup>();
            topHlg.spacing = 8f;
            topHlg.childAlignment = TextAnchor.MiddleLeft;
            topHlg.childControlWidth = true;
            topHlg.childControlHeight = true;
            topHlg.childForceExpandWidth = false;
            topHlg.childForceExpandHeight = true;

            // 좌측 초상화 박스 (48x48) + 우상단 소형 행동 예고 배지
            GameObject portraitBox = new GameObject("PortraitBox", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            portraitBox.transform.SetParent(topBox.transform, false);
            RectTransform portraitBoxRect = portraitBox.GetComponent<RectTransform>();
            portraitBoxRect.sizeDelta = new Vector2(48f, 48f);

            Image portraitBoxBg = portraitBox.GetComponent<Image>();
            portraitBoxBg.sprite = squareSprite;
            portraitBoxBg.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
            portraitBoxBg.raycastTarget = false;

            LayoutElement portraitLayoutElem = portraitBox.GetComponent<LayoutElement>();
            portraitLayoutElem.minWidth = 48f;
            portraitLayoutElem.minHeight = 48f;
            portraitLayoutElem.preferredWidth = 48f;
            portraitLayoutElem.preferredHeight = 48f;
            portraitLayoutElem.flexibleWidth = 0f;
            portraitLayoutElem.flexibleHeight = 0f;

            GameObject portraitImgObj = new GameObject("PortraitImage", typeof(RectTransform), typeof(Image));
            portraitImgObj.transform.SetParent(portraitBox.transform, false);
            SetStretchRect(portraitImgObj.GetComponent<RectTransform>(), 2f);
            Image portraitImg = portraitImgObj.GetComponent<Image>();
            portraitImg.sprite = squareSprite;
            portraitImg.preserveAspect = true;
            portraitImg.color = Color.white;
            portraitImg.raycastTarget = false;

            // 소형 IntentBadge (적 전용, 26x18)
            GameObject intentBadgeObj = new GameObject("IntentBadge", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            intentBadgeObj.transform.SetParent(portraitBox.transform, false);
            RectTransform intentRect = intentBadgeObj.GetComponent<RectTransform>();
            intentRect.anchorMin = new Vector2(1f, 1f);
            intentRect.anchorMax = new Vector2(1f, 1f);
            intentRect.pivot = new Vector2(1f, 1f);
            intentRect.anchoredPosition = new Vector2(2f, 2f);
            intentRect.sizeDelta = new Vector2(26f, 18f);

            Image intentBg = intentBadgeObj.GetComponent<Image>();
            intentBg.sprite = squareSprite;
            intentBg.color = new Color(0.07f, 0.08f, 0.12f, 0.95f);
            intentBg.raycastTarget = false;

            HorizontalLayoutGroup intentHlg = intentBadgeObj.GetComponent<HorizontalLayoutGroup>();
            intentHlg.padding = new RectOffset(2, 2, 1, 1);
            intentHlg.spacing = 1f;
            intentHlg.childAlignment = TextAnchor.MiddleCenter;
            intentHlg.childControlWidth = true;
            intentHlg.childControlHeight = true;
            intentHlg.childForceExpandWidth = false;
            intentHlg.childForceExpandHeight = false;

            GameObject intentIconObj = new GameObject("IntentIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            intentIconObj.transform.SetParent(intentBadgeObj.transform, false);
            LayoutElement intentIconLayout = intentIconObj.GetComponent<LayoutElement>();
            intentIconLayout.minWidth = 12f;
            intentIconLayout.minHeight = 12f;
            intentIconLayout.preferredWidth = 12f;
            intentIconLayout.preferredHeight = 12f;

            Image intentIconImg = intentIconObj.GetComponent<Image>();
            intentIconImg.sprite = squareSprite;
            intentIconImg.preserveAspect = true;
            intentIconImg.color = Color.white;
            intentIconImg.raycastTarget = false;

            GameObject intentValObj = new GameObject("IntentValueText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            intentValObj.transform.SetParent(intentBadgeObj.transform, false);
            LayoutElement intentValLayout = intentValObj.GetComponent<LayoutElement>();
            intentValLayout.minWidth = 8f;
            intentValLayout.preferredHeight = 14f;

            TextMeshProUGUI intentValTmp = intentValObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) intentValTmp.font = baseFont;
            intentValTmp.text = "6";
            intentValTmp.fontSize = 10f;
            intentValTmp.fontStyle = FontStyles.Bold;
            intentValTmp.color = Color.white;
            intentValTmp.alignment = TextAlignmentOptions.Center;
            intentValTmp.textWrappingMode = TextWrappingModes.NoWrap;
            intentValTmp.overflowMode = TextOverflowModes.Overflow;
            intentValTmp.raycastTarget = false;
            intentBadgeObj.SetActive(false);

            // 우측 정보 컬럼 (NameText + HealthGauge + StaminaGauge)
            GameObject infoCol = new GameObject("InfoColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            infoCol.transform.SetParent(topBox.transform, false);
            LayoutElement infoLayoutElem = infoCol.GetComponent<LayoutElement>();
            infoLayoutElem.flexibleWidth = 1f;
            infoLayoutElem.minWidth = 100f;

            VerticalLayoutGroup infoVlg = infoCol.GetComponent<VerticalLayoutGroup>();
            infoVlg.spacing = 3f;
            infoVlg.childAlignment = TextAnchor.UpperLeft;
            infoVlg.childControlWidth = true;
            infoVlg.childControlHeight = false;
            infoVlg.childForceExpandWidth = true;
            infoVlg.childForceExpandHeight = false;

            GameObject nameObj = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            nameObj.transform.SetParent(infoCol.transform, false);
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(0f, 18f);
            nameObj.GetComponent<LayoutElement>().preferredHeight = 18f;

            TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) nameTmp.font = baseFont;
            nameTmp.text = "유닛 이름";
            nameTmp.fontSize = 14f;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
            nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;
            nameTmp.raycastTarget = false;

            GaugeUI healthGauge = CreateSideCardGauge(infoCol.transform, "HealthGauge", 16f, 11f, UIConstants.COLOR_HEALTH, baseFont, squareSprite);
            GaugeUI staminaGauge = CreateSideCardGauge(infoCol.transform, "StaminaGauge", 14f, 10f, UIConstants.COLOR_STAMINA, baseFont, squareSprite);

            // 하단 이펙트 아이콘 행 (높이 26, 좌측 정렬)
            GameObject effectRowObj = new GameObject("EffectRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            effectRowObj.transform.SetParent(bgObj.transform, false);
            effectRowObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 26f);
            LayoutElement effectRowLayoutElem = effectRowObj.GetComponent<LayoutElement>();
            effectRowLayoutElem.minHeight = 26f;
            effectRowLayoutElem.preferredHeight = 26f;

            HorizontalLayoutGroup effectHlg = effectRowObj.GetComponent<HorizontalLayoutGroup>();
            effectHlg.spacing = 4f;
            effectHlg.childAlignment = TextAnchor.MiddleLeft;
            effectHlg.childControlWidth = false;
            effectHlg.childControlHeight = false;
            effectHlg.childForceExpandWidth = false;
            effectHlg.childForceExpandHeight = false;

            // 호버 하이라이트 외곽 4변 프레임 (Background 형제, 레이아웃 영향 없음)
            GameObject highlightFrameObj = CreateOutlineFrame(root.transform, "HighlightFrame", squareSprite, new Color(1f, 0.94f, 0.42f, 1f), 2.5f);
            highlightFrameObj.SetActive(false);

            // 직렬화 바인딩
            BattleSideUnitCardUI cardUI = root.GetComponent<BattleSideUnitCardUI>();
            SerializedObject serializedCard = new SerializedObject(cardUI);
            serializedCard.FindProperty("backgroundImage").objectReferenceValue = bgImg;
            serializedCard.FindProperty("borderImage").objectReferenceValue = borderImg;
            serializedCard.FindProperty("highlightFrameObj").objectReferenceValue = highlightFrameObj;
            serializedCard.FindProperty("portraitImage").objectReferenceValue = portraitImg;
            serializedCard.FindProperty("intentBadgeObj").objectReferenceValue = intentBadgeObj;
            serializedCard.FindProperty("intentIconImage").objectReferenceValue = intentIconImg;
            serializedCard.FindProperty("intentValueText").objectReferenceValue = intentValTmp;
            serializedCard.FindProperty("nameText").objectReferenceValue = nameTmp;
            serializedCard.FindProperty("healthGaugeUI").objectReferenceValue = healthGauge;
            serializedCard.FindProperty("staminaGaugeUI").objectReferenceValue = staminaGauge;
            serializedCard.FindProperty("effectRowContainer").objectReferenceValue = effectRowObj.transform;
            serializedCard.FindProperty("effectItemPrefab").objectReferenceValue = effectItemPrefabAsset;
            serializedCard.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, uiLayer);

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, SideCardPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 저장 실패: {SideCardPrefabPath}");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>(SideCardPrefabPath);
        return loaded != null ? loaded.GetComponent<BattleSideUnitCardUI>() : null;
    }

    private static GaugeUI CreateSideCardGauge(
        Transform parent,
        string name,
        float height,
        float fontSize,
        Color fillColor,
        TMP_FontAsset baseFont,
        Sprite squareSprite)
    {
        GameObject gaugeRoot = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        gaugeRoot.transform.SetParent(parent, false);
        gaugeRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, height);

        LayoutElement le = gaugeRoot.GetComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        Image bgImg = gaugeRoot.GetComponent<Image>();
        bgImg.sprite = squareSprite;
        bgImg.color = new Color(0.10f, 0.11f, 0.15f, 0.94f);
        bgImg.raycastTarget = false;

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image), typeof(GaugeUI));
        fillObj.transform.SetParent(gaugeRoot.transform, false);
        SetStretchRect(fillObj.GetComponent<RectTransform>(), 0f);

        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.sprite = squareSprite;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = 0;
        fillImg.fillAmount = 1f;
        fillImg.color = fillColor;
        fillImg.raycastTarget = false;

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(gaugeRoot.transform, false);
        SetStretchRect(textObj.GetComponent<RectTransform>(), 0f);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (baseFont != null) tmp.font = baseFont;
        tmp.text = "100 / 100";
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;

        GaugeUI gauge = fillObj.GetComponent<GaugeUI>();
        SerializedObject serializedGauge = new SerializedObject(gauge);
        serializedGauge.FindProperty("gaugeImageFilled").objectReferenceValue = fillImg;
        serializedGauge.FindProperty("gaugeText").objectReferenceValue = tmp;
        serializedGauge.ApplyModifiedPropertiesWithoutUndo();

        return gauge;
    }

    private static void CreateOrUpdateSideUnitHudPanelPrefab(int uiLayer, BattleSideUnitCardUI cardPrefab)
    {
        GameObject root = new GameObject(
            "BattleSideUnitHUDPanel",
            typeof(RectTransform),
            typeof(BattleSideUnitHUDPanel));

        try
        {
            // 루트는 풀스트레치 투명 컨테이너 (Graphic 없음 = 배경 월드 입력 비차단)
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // 좌측 플레이어 팀 컨테이너 (중앙 타임라인 하단 아래 ~ 이동 모드 버튼 위)
            GameObject leftObj = new GameObject("LeftPlayerContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            leftObj.transform.SetParent(root.transform, false);
            RectTransform leftRect = leftObj.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0f, 1f);
            leftRect.pivot = new Vector2(0f, 1f);
            leftRect.offsetMin = new Vector2(16f, 200f);
            leftRect.offsetMax = new Vector2(286f, -160f);

            VerticalLayoutGroup leftVlg = leftObj.GetComponent<VerticalLayoutGroup>();
            leftVlg.spacing = 8f;
            leftVlg.childAlignment = TextAnchor.UpperLeft;
            leftVlg.childControlWidth = true;
            leftVlg.childControlHeight = true;
            leftVlg.childForceExpandWidth = true;
            leftVlg.childForceExpandHeight = false;

            // 우측 적 팀 컨테이너 (중앙 타임라인 하단 아래 ~ 턴 종료 버튼 위)
            GameObject rightObj = new GameObject("RightEnemyContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rightObj.transform.SetParent(root.transform, false);
            RectTransform rightRect = rightObj.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(1f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.pivot = new Vector2(1f, 1f);
            rightRect.offsetMin = new Vector2(-286f, 200f);
            rightRect.offsetMax = new Vector2(-16f, -160f);

            VerticalLayoutGroup rightVlg = rightObj.GetComponent<VerticalLayoutGroup>();
            rightVlg.spacing = 8f;
            rightVlg.childAlignment = TextAnchor.UpperRight;
            rightVlg.childControlWidth = true;
            rightVlg.childControlHeight = true;
            rightVlg.childForceExpandWidth = true;
            rightVlg.childForceExpandHeight = false;

            BattleSideUnitHUDPanel panel = root.GetComponent<BattleSideUnitHUDPanel>();
            panel.uiLayerType = UILayerType.Top;
            panel.blockWorldInput = false;

            SerializedObject serializedPanel = new SerializedObject(panel);
            serializedPanel.FindProperty("uiLayerType").enumValueIndex = (int)UILayerType.Top;
            serializedPanel.FindProperty("blockWorldInput").boolValue = false;
            serializedPanel.FindProperty("leftPlayerContainer").objectReferenceValue = leftRect;
            serializedPanel.FindProperty("rightEnemyContainer").objectReferenceValue = rightRect;
            serializedPanel.FindProperty("unitCardPrefab").objectReferenceValue = cardPrefab;
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, uiLayer);

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, SideHudPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupBattleSideUnitHUDAndCompactWorldUI] 저장 실패: {SideHudPrefabPath}");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateOutlineFrame(Transform parent, string name, Sprite squareSprite, Color color, float thickness)
    {
        GameObject frameObj = new GameObject(name, typeof(RectTransform));
        frameObj.transform.SetParent(parent, false);
        RectTransform frameRect = frameObj.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = new Vector2(-thickness, -thickness);
        frameRect.offsetMax = new Vector2(thickness, thickness);

        CreateFrameEdge(frameObj.transform, "TopEdge", squareSprite, color,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness));
        CreateFrameEdge(frameObj.transform, "BottomEdge", squareSprite, color,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness));
        CreateFrameEdge(frameObj.transform, "LeftEdge", squareSprite, color,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f));
        CreateFrameEdge(frameObj.transform, "RightEdge", squareSprite, color,
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f));

        return frameObj;
    }

    private static void CreateFrameEdge(
        Transform parent,
        string name,
        Sprite squareSprite,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta)
    {
        GameObject edgeObj = new GameObject(name, typeof(RectTransform), typeof(Image));
        edgeObj.transform.SetParent(parent, false);
        RectTransform rect = edgeObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;

        Image img = edgeObj.GetComponent<Image>();
        img.sprite = squareSprite;
        img.color = color;
        img.raycastTarget = false;
    }

    private static void SetStretchRect(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null || newLayer < 0) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }
}
