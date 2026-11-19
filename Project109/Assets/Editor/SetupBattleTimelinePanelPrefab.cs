using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 타임라인 UI 프리팹(BattleTimelineSlotUI.prefab, BattleTimelinePanel.prefab)을 생성·갱신하고
/// Addressables(UI_Prefabs) 그룹에 자동 등록하는 에디터 도구입니다.
/// </summary>
public static class SetupBattleTimelinePanelPrefab
{
    private const string BattleUiDir = "Assets/Prefab/UI/Battle";
    private const string SlotPrefabPath = "Assets/Prefab/UI/Battle/BattleTimelineSlotUI.prefab";
    private const string PanelPrefabPath = "Assets/Prefab/UI/Battle/BattleTimelinePanel.prefab";

    [MenuItem("Tools/UI/Setup Battle Timeline Panel Prefabs")]
    public static string SetupAll()
    {
        if (!Directory.Exists(BattleUiDir))
        {
            Directory.CreateDirectory(BattleUiDir);
        }

        TMP_FontAsset baseFont = ResolveBaseFont();
        Sprite squareSprite = ResolveSquareSprite();

        // 1. BattleTimelineSlotUI.prefab 생성/갱신 (64x64, 중첩 Canvas 금지)
        BattleTimelineSlotUI slotPrefab = CreateOrUpdateSlotPrefab(baseFont, squareSprite);

        // 2. BattleTimelinePanel.prefab 생성/갱신 (상단 중앙 바, Top 레이어, 중첩 Canvas 금지)
        CreateOrUpdatePanelPrefab(slotPrefab, squareSprite);

        // 3. Addressables UI_Prefabs 그룹 자동 등록
        AddressableAutoRegister.RegisterAllUIPrefabs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = "[SetupBattleTimelinePanelPrefab] BattleTimelineSlotUI.prefab, BattleTimelinePanel.prefab 생성 및 Addressables UI_Prefabs 등록 완료";
        Debug.Log(summary);
        return summary;
    }

    private static TMP_FontAsset ResolveBaseFont()
    {
        GameObject battlePanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleUIPanel.prefab");
        if (battlePanelPrefab != null)
        {
            TextMeshProUGUI tmp = battlePanelPrefab.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && tmp.font != null)
            {
                return tmp.font;
            }
        }

        GameObject intentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterIntentUI.prefab");
        if (intentPrefab != null)
        {
            TextMeshProUGUI tmp = intentPrefab.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && tmp.font != null)
            {
                return tmp.font;
            }
        }

        TMP_FontAsset fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NEXON Lv2 Gothic OTF SDF.asset");
        if (fallbackFont == null)
        {
            Debug.LogError("[SetupBattleTimelinePanelPrefab] 기본 TMP_FontAsset를 찾을 수 없습니다.");
        }
        return fallbackFont;
    }

    private static Sprite ResolveSquareSprite()
    {
        GameObject statusBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterStatusBarUI.prefab");
        if (statusBarPrefab != null)
        {
            Image[] images = statusBarPrefab.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].sprite != null)
                {
                    return images[i].sprite;
                }
            }
        }

        Sprite fallbackSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resource/Square.png");
        if (fallbackSprite == null)
        {
            Debug.LogError("[SetupBattleTimelinePanelPrefab] 기본 Square 스프라이트를 찾을 수 없습니다.");
        }
        return fallbackSprite;
    }

    private static BattleTimelineSlotUI CreateOrUpdateSlotPrefab(TMP_FontAsset baseFont, Sprite squareSprite)
    {
        GameObject root = new GameObject(
            "BattleTimelineSlotUI",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(BattleTimelineSlotUI));

        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(64f, 64f);

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // 루트 Image를 테두리 프레임 겸 전체 64x64 포인터 호버 히트 영역으로 사용
            Image borderImg = root.GetComponent<Image>();
            borderImg.sprite = squareSprite;
            borderImg.color = new Color(0.28f, 0.62f, 1f, 1f);
            borderImg.raycastTarget = true;

            // 내부 배경 (3px 인셋으로 깔끔한 외곽선 형성)
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(root.transform, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(3f, 3f);
            bgRect.offsetMax = new Vector2(-3f, -3f);

            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.sprite = squareSprite;
            bgImg.color = new Color(0.11f, 0.17f, 0.28f, 0.95f);
            bgImg.raycastTarget = false;

            // 유닛 초상화/역할 엠블럼
            GameObject portraitObj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitObj.transform.SetParent(bgObj.transform, false);
            RectTransform portraitRect = portraitObj.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
            portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = Vector2.zero;
            portraitRect.sizeDelta = new Vector2(30f, 30f);

            Image portraitImg = portraitObj.GetComponent<Image>();
            portraitImg.sprite = squareSprite;
            portraitImg.preserveAspect = true;
            portraitImg.color = Color.white;
            portraitImg.raycastTarget = false;

            // 상단 이름 라벨
            GameObject nameObj = new GameObject("NameLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameObj.transform.SetParent(bgObj.transform, false);
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -1f);
            nameRect.sizeDelta = new Vector2(-4f, 14f);

            TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) nameTmp.font = baseFont;
            nameTmp.text = "유닛";
            nameTmp.fontSize = 9f;
            nameTmp.enableAutoSizing = true;
            nameTmp.fontSizeMin = 6.5f;
            nameTmp.fontSizeMax = 9.5f;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;
            nameTmp.raycastTarget = false;

            // 하단 시간 표시 밴드 및 텍스트
            GameObject timeBandObj = new GameObject("TimeBand", typeof(RectTransform), typeof(Image));
            timeBandObj.transform.SetParent(bgObj.transform, false);
            RectTransform timeBandRect = timeBandObj.GetComponent<RectTransform>();
            timeBandRect.anchorMin = new Vector2(0f, 0f);
            timeBandRect.anchorMax = new Vector2(1f, 0f);
            timeBandRect.pivot = new Vector2(0.5f, 0f);
            timeBandRect.anchoredPosition = Vector2.zero;
            timeBandRect.sizeDelta = new Vector2(0f, 15f);

            Image timeBandImg = timeBandObj.GetComponent<Image>();
            timeBandImg.sprite = squareSprite;
            timeBandImg.color = new Color(0.05f, 0.06f, 0.09f, 0.78f);
            timeBandImg.raycastTarget = false;

            GameObject timeObj = new GameObject("TimeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            timeObj.transform.SetParent(timeBandObj.transform, false);
            RectTransform timeRect = timeObj.GetComponent<RectTransform>();
            timeRect.anchorMin = Vector2.zero;
            timeRect.anchorMax = Vector2.one;
            timeRect.offsetMin = new Vector2(2f, 0f);
            timeRect.offsetMax = new Vector2(-2f, 0f);

            TextMeshProUGUI timeTmp = timeObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) timeTmp.font = baseFont;
            timeTmp.text = "0.0s";
            timeTmp.fontSize = 10f;
            timeTmp.enableAutoSizing = true;
            timeTmp.fontSizeMin = 7f;
            timeTmp.fontSizeMax = 10.5f;
            timeTmp.fontStyle = FontStyles.Bold;
            timeTmp.color = new Color(1f, 0.93f, 0.65f, 1f);
            timeTmp.alignment = TextAlignmentOptions.Center;
            timeTmp.textWrappingMode = TextWrappingModes.NoWrap;
            timeTmp.overflowMode = TextOverflowModes.Overflow;
            timeTmp.raycastTarget = false;

            // 호버 하이라이트 외곽 4변 프레임
            GameObject highlightFrameObj = CreateOutlineFrame(root.transform, "HighlightFrame", squareSprite, new Color(1f, 0.94f, 0.42f, 1f), 2.5f);
            highlightFrameObj.SetActive(false);

            // 기절 배지 (좌상단)
            GameObject stunBadgeObj = new GameObject("StunBadge", typeof(RectTransform), typeof(Image));
            stunBadgeObj.transform.SetParent(root.transform, false);
            RectTransform stunRect = stunBadgeObj.GetComponent<RectTransform>();
            stunRect.anchorMin = new Vector2(0f, 1f);
            stunRect.anchorMax = new Vector2(0f, 1f);
            stunRect.pivot = new Vector2(0f, 1f);
            stunRect.anchoredPosition = new Vector2(-3f, 3f);
            stunRect.sizeDelta = new Vector2(26f, 15f);

            Image stunBg = stunBadgeObj.GetComponent<Image>();
            stunBg.sprite = squareSprite;
            stunBg.color = new Color(0.55f, 0.22f, 0.78f, 0.96f);
            stunBg.raycastTarget = false;

            GameObject stunTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            stunTextObj.transform.SetParent(stunBadgeObj.transform, false);
            RectTransform stunTextRect = stunTextObj.GetComponent<RectTransform>();
            stunTextRect.anchorMin = Vector2.zero;
            stunTextRect.anchorMax = Vector2.one;
            stunTextRect.offsetMin = Vector2.zero;
            stunTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI stunTmp = stunTextObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) stunTmp.font = baseFont;
            stunTmp.text = "기절";
            stunTmp.fontSize = 8f;
            stunTmp.enableAutoSizing = true;
            stunTmp.fontSizeMin = 6f;
            stunTmp.fontSizeMax = 8.5f;
            stunTmp.fontStyle = FontStyles.Bold;
            stunTmp.color = new Color(1f, 0.90f, 0.35f, 1f);
            stunTmp.alignment = TextAlignmentOptions.Center;
            stunTmp.textWrappingMode = TextWrappingModes.NoWrap;
            stunTmp.raycastTarget = false;
            stunBadgeObj.SetActive(false);

            // 적 행동 예고 소형 배지 (우상단, [17] 연동)
            GameObject intentBadgeObj = new GameObject("IntentBadge", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            intentBadgeObj.transform.SetParent(root.transform, false);
            RectTransform intentRect = intentBadgeObj.GetComponent<RectTransform>();
            intentRect.anchorMin = new Vector2(1f, 1f);
            intentRect.anchorMax = new Vector2(1f, 1f);
            intentRect.pivot = new Vector2(1f, 1f);
            intentRect.anchoredPosition = new Vector2(5f, 5f);
            intentRect.sizeDelta = new Vector2(32f, 18f);

            Image intentBg = intentBadgeObj.GetComponent<Image>();
            intentBg.sprite = squareSprite;
            intentBg.color = new Color(0.08f, 0.09f, 0.14f, 0.95f);
            intentBg.raycastTarget = false;

            HorizontalLayoutGroup intentLayout = intentBadgeObj.GetComponent<HorizontalLayoutGroup>();
            intentLayout.padding = new RectOffset(2, 2, 2, 2);
            intentLayout.spacing = 1f;
            intentLayout.childAlignment = TextAnchor.MiddleCenter;
            intentLayout.childControlWidth = true;
            intentLayout.childControlHeight = true;
            intentLayout.childForceExpandWidth = false;
            intentLayout.childForceExpandHeight = false;

            GameObject intentIconObj = new GameObject("IntentIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            intentIconObj.transform.SetParent(intentBadgeObj.transform, false);
            LayoutElement intentIconLayout = intentIconObj.GetComponent<LayoutElement>();
            intentIconLayout.minWidth = 14f;
            intentIconLayout.minHeight = 14f;
            intentIconLayout.preferredWidth = 14f;
            intentIconLayout.preferredHeight = 14f;
            intentIconLayout.flexibleWidth = 0f;
            intentIconLayout.flexibleHeight = 0f;

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
            intentValLayout.flexibleWidth = 0f;
            intentValLayout.flexibleHeight = 0f;

            TextMeshProUGUI intentValTmp = intentValObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) intentValTmp.font = baseFont;
            intentValTmp.text = "6";
            intentValTmp.fontSize = 10f;
            intentValTmp.enableAutoSizing = true;
            intentValTmp.fontSizeMin = 7f;
            intentValTmp.fontSizeMax = 10.5f;
            intentValTmp.fontStyle = FontStyles.Bold;
            intentValTmp.color = Color.white;
            intentValTmp.alignment = TextAlignmentOptions.Center;
            intentValTmp.textWrappingMode = TextWrappingModes.NoWrap;
            intentValTmp.overflowMode = TextOverflowModes.Overflow;
            intentValTmp.raycastTarget = false;
            intentBadgeObj.SetActive(false);

            // 고스트 슬롯 배지 (상단 중앙)
            GameObject ghostBadgeObj = new GameObject("GhostBadge", typeof(RectTransform), typeof(Image));
            ghostBadgeObj.transform.SetParent(root.transform, false);
            RectTransform ghostRect = ghostBadgeObj.GetComponent<RectTransform>();
            ghostRect.anchorMin = new Vector2(0.5f, 1f);
            ghostRect.anchorMax = new Vector2(0.5f, 1f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.anchoredPosition = new Vector2(0f, 3f);
            ghostRect.sizeDelta = new Vector2(36f, 14f);

            Image ghostBg = ghostBadgeObj.GetComponent<Image>();
            ghostBg.sprite = squareSprite;
            ghostBg.color = new Color(0.12f, 0.62f, 0.78f, 0.95f);
            ghostBg.raycastTarget = false;

            GameObject ghostTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            ghostTextObj.transform.SetParent(ghostBadgeObj.transform, false);
            RectTransform ghostTextRect = ghostTextObj.GetComponent<RectTransform>();
            ghostTextRect.anchorMin = Vector2.zero;
            ghostTextRect.anchorMax = Vector2.one;
            ghostTextRect.offsetMin = Vector2.zero;
            ghostTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI ghostTmp = ghostTextObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) ghostTmp.font = baseFont;
            ghostTmp.text = "예상";
            ghostTmp.fontSize = 8f;
            ghostTmp.fontStyle = FontStyles.Bold;
            ghostTmp.color = Color.white;
            ghostTmp.alignment = TextAlignmentOptions.Center;
            ghostTmp.textWrappingMode = TextWrappingModes.NoWrap;
            ghostTmp.raycastTarget = false;
            ghostBadgeObj.SetActive(false);

            // BattleTimelineSlotUI 직렬화 바인딩
            BattleTimelineSlotUI slotUI = root.GetComponent<BattleTimelineSlotUI>();
            SerializedObject serializedSlot = new SerializedObject(slotUI);
            serializedSlot.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            serializedSlot.FindProperty("backgroundImage").objectReferenceValue = bgImg;
            serializedSlot.FindProperty("borderImage").objectReferenceValue = borderImg;
            serializedSlot.FindProperty("portraitImage").objectReferenceValue = portraitImg;
            serializedSlot.FindProperty("nameLabelText").objectReferenceValue = nameTmp;
            serializedSlot.FindProperty("timeText").objectReferenceValue = timeTmp;
            serializedSlot.FindProperty("stunBadgeObj").objectReferenceValue = stunBadgeObj;
            serializedSlot.FindProperty("intentBadgeObj").objectReferenceValue = intentBadgeObj;
            serializedSlot.FindProperty("intentIconImage").objectReferenceValue = intentIconImg;
            serializedSlot.FindProperty("intentValueText").objectReferenceValue = intentValTmp;
            serializedSlot.FindProperty("highlightFrameObj").objectReferenceValue = highlightFrameObj;
            serializedSlot.FindProperty("ghostBadgeObj").objectReferenceValue = ghostBadgeObj;
            serializedSlot.ApplyModifiedPropertiesWithoutUndo();

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupBattleTimelinePanelPrefab] 슬롯 프리팹 저장 실패: {SlotPrefabPath}");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        GameObject loadedPrefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        return loadedPrefabObj != null ? loadedPrefabObj.GetComponent<BattleTimelineSlotUI>() : null;
    }

    private static void CreateOrUpdatePanelPrefab(BattleTimelineSlotUI slotPrefab, Sprite squareSprite)
    {
        GameObject root = new GameObject(
            "BattleTimelinePanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(BattleTimelinePanel));

        try
        {
            // TopHUDPanel 58px 바 아래 상단 중앙 가로 바
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -68f);
            rootRect.sizeDelta = new Vector2(760f, 76f);

            Image panelBg = root.GetComponent<Image>();
            panelBg.sprite = squareSprite;
            panelBg.color = new Color(0.06f, 0.08f, 0.13f, 0.72f);
            panelBg.raycastTarget = false;

            // 슬롯 배치 컨테이너
            GameObject containerObj = new GameObject("SlotContainer", typeof(RectTransform));
            containerObj.transform.SetParent(root.transform, false);
            RectTransform containerRect = containerObj.GetComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.offsetMin = new Vector2(12f, 4f);
            containerRect.offsetMax = new Vector2(-12f, -4f);

            // BattleTimelinePanel 직렬화 바인딩
            BattleTimelinePanel panel = root.GetComponent<BattleTimelinePanel>();
            panel.uiLayerType = UILayerType.Top;
            panel.blockWorldInput = false;

            SerializedObject serializedPanel = new SerializedObject(panel);
            serializedPanel.FindProperty("uiLayerType").enumValueIndex = (int)UILayerType.Top;
            serializedPanel.FindProperty("blockWorldInput").boolValue = false;
            serializedPanel.FindProperty("slotContainer").objectReferenceValue = containerRect;
            serializedPanel.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
            serializedPanel.FindProperty("slotSpacing").floatValue = 74f;
            serializedPanel.FindProperty("slideDuration").floatValue = 0.22f;
            serializedPanel.FindProperty("predictedSlotHighlightColor").colorValue = new Color(0.25f, 0.95f, 1f, 1f);
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupBattleTimelinePanelPrefab] 패널 프리팹 저장 실패: {PanelPrefabPath}");
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
}
