using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 월드 이펙트 상세 툴팁 프리팹(CharacterEffectDetailItemUI, CharacterEffectDetailUI)을 생성하고
/// 캐릭터 프리팹 3종의 콜라이더/UI 오프셋 및 Addressables(UI_Prefabs) 등록을 일괄 구성하는 에디터 도구입니다.
/// </summary>
public static class SetupCharacterWorldUIPrefabs
{
    private const string BattleUiDir = "Assets/Prefab/UI/Battle";
    private const string DetailItemPrefabPath = "Assets/Prefab/UI/Battle/CharacterEffectDetailItemUI.prefab";
    private const string DetailPanelPrefabPath = "Assets/Prefab/UI/Battle/CharacterEffectDetailUI.prefab";

    private const string PlayerPrefabPath = "Assets/Prefab/PlayerCharacterSample.prefab";
    private const string WarriorPrefabPath = "Assets/Prefab/Enemy/Enemy_SkeletonWarrior.prefab";
    private const string GuardPrefabPath = "Assets/Prefab/Enemy/Enemy_SkeletonGuard.prefab";

    [MenuItem("Tools/UI/Setup Character World UI Prefabs")]
    public static string SetupAll()
    {
        if (!Directory.Exists(BattleUiDir))
        {
            Directory.CreateDirectory(BattleUiDir);
        }

        int worldUiLayer = LayerMask.NameToLayer("WorldUI");
        if (worldUiLayer < 0)
        {
            worldUiLayer = 11;
            Debug.LogWarning("[SetupCharacterWorldUIPrefabs] WorldUI 레이어를 찾을 수 없어 기본값(11)을 사용합니다.");
        }

        TMP_FontAsset baseFont = ResolveBaseFont();
        Sprite squareSprite = ResolveSquareSprite();

        // 1. 개별 상세 이펙트 항목 프리팹 생성
        CreateOrUpdateDetailItemPrefab(worldUiLayer, baseFont, squareSprite);

        // 2. 상세 이펙트 패널 프리팹 생성 (항목 프리팹 참조 연결)
        CreateOrUpdateDetailPanelPrefab(worldUiLayer, squareSprite);

        // 3. 캐릭터 프리팹 3종 콜라이더(isTrigger=true) 및 월드 UI 오프셋 직렬화
        ConfigureCharacterPrefab(
            PlayerPrefabPath,
            targetLayerName: "Player",
            ensureColliderBox: true,
            colliderCenter: new Vector3(0f, 0.8f, 0f),
            colliderSize: new Vector3(0.95f, 1.6f, 0.6f),
            statusBarOffset: new Vector3(0f, 1.2f, 0f),
            effectListOffset: new Vector3(0f, 0.9f, 0f),
            effectDetailOffset: new Vector3(0.75f, 1.2f, 0f));

        ConfigureCharacterPrefab(
            WarriorPrefabPath,
            targetLayerName: "Enemy",
            ensureColliderBox: false,
            colliderCenter: Vector3.zero,
            colliderSize: Vector3.zero,
            statusBarOffset: new Vector3(0f, 1.7070832f, 0f),
            effectListOffset: new Vector3(0f, 1.4070832f, 0f),
            effectDetailOffset: new Vector3(0.75f, 1.5f, 0f));

        ConfigureCharacterPrefab(
            GuardPrefabPath,
            targetLayerName: "Enemy",
            ensureColliderBox: false,
            colliderCenter: Vector3.zero,
            colliderSize: Vector3.zero,
            statusBarOffset: new Vector3(0f, 1.8557914f, 0f),
            effectListOffset: new Vector3(0f, 1.5557914f, 0f),
            effectDetailOffset: new Vector3(0.75f, 1.6f, 0f));

        // 4. Addressables UI_Prefabs 그룹 자동 등록
        AddressableAutoRegister.RegisterAllUIPrefabs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = "[SetupCharacterWorldUIPrefabs] 프리팹 2종 생성, 캐릭터 프리팹 3종 정비, Addressables UI_Prefabs 등록 완료";
        Debug.Log(summary);
        return summary;
    }

    private static TMP_FontAsset ResolveBaseFont()
    {
        GameObject effectItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterEffectItemUI.prefab");
        if (effectItemPrefab != null)
        {
            TextMeshProUGUI tmp = effectItemPrefab.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && tmp.font != null)
            {
                return tmp.font;
            }
        }

        GameObject tooltipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/TooltipPanel.prefab");
        if (tooltipPrefab != null)
        {
            TextMeshProUGUI tmp = tooltipPrefab.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && tmp.font != null)
            {
                return tmp.font;
            }
        }

        TMP_FontAsset fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NEXON Lv2 Gothic OTF SDF.asset");
        if (fallbackFont == null)
        {
            Debug.LogError("[SetupCharacterWorldUIPrefabs] 기본 TMP_FontAsset를 찾을 수 없습니다.");
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
            Debug.LogError("[SetupCharacterWorldUIPrefabs] 기본 Square 스프라이트를 찾을 수 없습니다.");
        }
        return fallbackSprite;
    }

    private static void CreateOrUpdateDetailItemPrefab(int worldUiLayer, TMP_FontAsset baseFont, Sprite squareSprite)
    {
        GameObject root = new GameObject("CharacterEffectDetailItemUI", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(CharacterEffectDetailItemUI));
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(254f, 48f);

            Image bgImage = root.GetComponent<Image>();
            bgImage.sprite = squareSprite;
            bgImage.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);
            bgImage.raycastTarget = false;

            HorizontalLayoutGroup rootLayout = root.GetComponent<HorizontalLayoutGroup>();
            rootLayout.padding = new RectOffset(6, 6, 6, 6);
            rootLayout.spacing = 8f;
            rootLayout.childAlignment = TextAnchor.UpperLeft;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = false;
            rootLayout.childForceExpandHeight = false;

            // 좌측 확대 아이콘 영역 (36x36)
            GameObject iconRoot = new GameObject("IconRoot", typeof(RectTransform), typeof(LayoutElement));
            iconRoot.transform.SetParent(root.transform, false);
            RectTransform iconRootRect = iconRoot.GetComponent<RectTransform>();
            iconRootRect.sizeDelta = new Vector2(36f, 36f);

            LayoutElement iconLayoutElem = iconRoot.GetComponent<LayoutElement>();
            iconLayoutElem.minWidth = 36f;
            iconLayoutElem.minHeight = 36f;
            iconLayoutElem.preferredWidth = 36f;
            iconLayoutElem.preferredHeight = 36f;
            iconLayoutElem.flexibleWidth = 0f;
            iconLayoutElem.flexibleHeight = 0f;

            // 테두리
            GameObject outlineObj = new GameObject("IconOutline", typeof(RectTransform), typeof(Image));
            outlineObj.transform.SetParent(iconRoot.transform, false);
            SetStretchRect(outlineObj.GetComponent<RectTransform>(), 0f);
            Image outlineImg = outlineObj.GetComponent<Image>();
            outlineImg.sprite = squareSprite;
            outlineImg.color = Color.black;
            outlineImg.raycastTarget = false;

            // 아이콘
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(iconRoot.transform, false);
            SetStretchRect(iconObj.GetComponent<RectTransform>(), 2f);
            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // 지속시간 게이지 링
            GameObject durationObj = new GameObject("DurationFill", typeof(RectTransform), typeof(Image));
            durationObj.transform.SetParent(iconRoot.transform, false);
            SetStretchRect(durationObj.GetComponent<RectTransform>(), 2f);
            Image durationImg = durationObj.GetComponent<Image>();
            durationImg.sprite = squareSprite;
            durationImg.type = Image.Type.Filled;
            durationImg.fillMethod = Image.FillMethod.Radial360;
            durationImg.fillOrigin = (int)Image.Origin360.Top;
            durationImg.fillClockwise = false;
            durationImg.fillAmount = 1f;
            durationImg.color = new Color(0f, 0f, 0f, 0.55f);
            durationImg.raycastTarget = false;

            // 스택 텍스트
            GameObject stackObj = new GameObject("StackText", typeof(RectTransform), typeof(TextMeshProUGUI));
            stackObj.transform.SetParent(iconRoot.transform, false);
            RectTransform stackRect = stackObj.GetComponent<RectTransform>();
            stackRect.anchorMin = Vector2.zero;
            stackRect.anchorMax = Vector2.one;
            stackRect.offsetMin = new Vector2(2f, 1f);
            stackRect.offsetMax = new Vector2(-2f, -1f);
            TextMeshProUGUI stackTmp = stackObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) stackTmp.font = baseFont;
            stackTmp.text = "0";
            stackTmp.fontSize = 10f;
            stackTmp.fontStyle = FontStyles.Bold;
            stackTmp.color = Color.white;
            stackTmp.alignment = TextAlignmentOptions.BottomRight;
            stackTmp.textWrappingMode = TextWrappingModes.NoWrap;
            stackTmp.raycastTarget = false;

            // 우측 텍스트 영역 (Header + Body)
            GameObject textGroup = new GameObject("TextGroup", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            textGroup.transform.SetParent(root.transform, false);
            LayoutElement textGroupLayoutElem = textGroup.GetComponent<LayoutElement>();
            textGroupLayoutElem.minWidth = 100f;
            textGroupLayoutElem.flexibleWidth = 1f;

            VerticalLayoutGroup textLayout = textGroup.GetComponent<VerticalLayoutGroup>();
            textLayout.spacing = 2f;
            textLayout.childAlignment = TextAnchor.UpperLeft;
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = true;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;

            GameObject headerObj = new GameObject("HeaderText", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerObj.transform.SetParent(textGroup.transform, false);
            TextMeshProUGUI headerTmp = headerObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) headerTmp.font = baseFont;
            headerTmp.text = "효과 이름";
            headerTmp.fontSize = 12f;
            headerTmp.fontStyle = FontStyles.Bold;
            headerTmp.color = new Color(1f, 0.92f, 0.65f, 1f);
            headerTmp.alignment = TextAlignmentOptions.TopLeft;
            headerTmp.textWrappingMode = TextWrappingModes.Normal;
            headerTmp.raycastTarget = false;

            GameObject bodyObj = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyObj.transform.SetParent(textGroup.transform, false);
            TextMeshProUGUI bodyTmp = bodyObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) bodyTmp.font = baseFont;
            bodyTmp.text = "효과 상세 설명이 여기에 표시됩니다.";
            bodyTmp.fontSize = 9f;
            bodyTmp.fontStyle = FontStyles.Normal;
            bodyTmp.color = new Color(0.92f, 0.94f, 0.97f, 1f);
            bodyTmp.alignment = TextAlignmentOptions.TopLeft;
            bodyTmp.textWrappingMode = TextWrappingModes.Normal;
            bodyTmp.raycastTarget = false;

            // 직렬화 바인딩
            CharacterEffectDetailItemUI detailItem = root.GetComponent<CharacterEffectDetailItemUI>();
            SerializedObject serializedItem = new SerializedObject(detailItem);
            serializedItem.FindProperty("iconImage").objectReferenceValue = iconImg;
            serializedItem.FindProperty("outlineBorderImage").objectReferenceValue = outlineImg;
            serializedItem.FindProperty("durationFillImage").objectReferenceValue = durationImg;
            serializedItem.FindProperty("stackText").objectReferenceValue = stackTmp;
            serializedItem.FindProperty("headerText").objectReferenceValue = headerTmp;
            serializedItem.FindProperty("bodyText").objectReferenceValue = bodyTmp;
            serializedItem.FindProperty("buffBorderColor").colorValue = new Color(0.2f, 0.8f, 1.0f, 1.0f);
            serializedItem.FindProperty("debuffBorderColor").colorValue = new Color(0.9f, 0.2f, 0.3f, 1.0f);
            serializedItem.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, worldUiLayer);

            bool saved = false;
            PrefabUtility.SaveAsPrefabAsset(root, DetailItemPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupCharacterWorldUIPrefabs] 프리팹 저장 실패: {DetailItemPrefabPath}");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateOrUpdateDetailPanelPrefab(int worldUiLayer, Sprite squareSprite)
    {
        CharacterEffectDetailItemUI itemPrefabAsset = AssetDatabase.LoadAssetAtPath<CharacterEffectDetailItemUI>(DetailItemPrefabPath);
        if (itemPrefabAsset == null)
        {
            Debug.LogError($"[SetupCharacterWorldUIPrefabs] 항목 프리팹 로드 실패: {DetailItemPrefabPath}");
        }

        GameObject root = new GameObject(
            "CharacterEffectDetailUI",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(CharacterEffectDetailUI));

        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(270f, 64f);

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            canvasGroup.ignoreParentGroups = false;

            Image bgImage = root.GetComponent<Image>();
            bgImage.sprite = squareSprite;
            bgImage.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            bgImage.raycastTarget = false;

            VerticalLayoutGroup layoutGroup = root.GetComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(8, 8, 8, 8);
            layoutGroup.spacing = 6f;
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            ContentSizeFitter sizeFitter = root.GetComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CharacterEffectDetailUI detailUI = root.GetComponent<CharacterEffectDetailUI>();
            SerializedObject serializedUI = new SerializedObject(detailUI);
            serializedUI.FindProperty("worldOffset").vector3Value = UIConstants.DEFAULT_EFFECT_DETAIL_OFFSET;
            serializedUI.FindProperty("useBillboard").boolValue = true;
            serializedUI.FindProperty("itemContainer").objectReferenceValue = root.transform;
            serializedUI.FindProperty("detailItemPrefab").objectReferenceValue = itemPrefabAsset;
            serializedUI.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            serializedUI.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, worldUiLayer);

            bool saved = false;
            PrefabUtility.SaveAsPrefabAsset(root, DetailPanelPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupCharacterWorldUIPrefabs] 프리팹 저장 실패: {DetailPanelPrefabPath}");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureCharacterPrefab(
        string prefabPath,
        string targetLayerName,
        bool ensureColliderBox,
        Vector3 colliderCenter,
        Vector3 colliderSize,
        Vector3 statusBarOffset,
        Vector3 effectListOffset,
        Vector3 effectDetailOffset)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError($"[SetupCharacterWorldUIPrefabs] 캐릭터 프리팹을 불러오지 못했습니다: {prefabPath}");
            return;
        }

        try
        {
            int targetLayer = LayerMask.NameToLayer(targetLayerName);
            if (targetLayer >= 0)
            {
                root.layer = targetLayer;
            }
            else
            {
                Debug.LogWarning($"[SetupCharacterWorldUIPrefabs] 레이어 '{targetLayerName}'을 찾을 수 없습니다: {prefabPath}");
            }

            BoxCollider boxCollider = root.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = root.AddComponent<BoxCollider>();
            }

            boxCollider.isTrigger = true;
            if (ensureColliderBox)
            {
                boxCollider.center = colliderCenter;
                boxCollider.size = colliderSize;
            }
            else
            {
                // 모델 영역을 감싸는 기존 콜라이더가 비어있거나 지나치게 작은 경우 자동 보정
                Vector3 currentSize = boxCollider.size;
                if (currentSize.x < 0.5f || currentSize.y < 0.8f || currentSize.z < 0.4f)
                {
                    Bounds bounds = CalculateLocalRendererBounds(root);
                    if (bounds.size.y > 0.1f)
                    {
                        boxCollider.center = new Vector3(
                            bounds.center.x,
                            bounds.center.y,
                            bounds.center.z);
                        boxCollider.size = new Vector3(
                            Mathf.Max(0.85f, bounds.size.x),
                            Mathf.Max(1.2f, bounds.size.y),
                            Mathf.Max(0.6f, bounds.size.z));
                    }
                }
            }

            CharacterUIController uiController = root.GetComponent<CharacterUIController>();
            if (uiController == null)
            {
                Debug.LogError($"[SetupCharacterWorldUIPrefabs] CharacterUIController가 없습니다: {prefabPath}");
            }
            else
            {
                SerializedObject serializedCtrl = new SerializedObject(uiController);
                serializedCtrl.FindProperty("statusBarOffset").vector3Value = statusBarOffset;
                serializedCtrl.FindProperty("effectListOffset").vector3Value = effectListOffset;
                serializedCtrl.FindProperty("effectDetailOffset").vector3Value = effectDetailOffset;
                serializedCtrl.ApplyModifiedPropertiesWithoutUndo();
            }

            bool saved = false;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupCharacterWorldUIPrefabs] 캐릭터 프리팹 저장 실패: {prefabPath}");
            }
            else
            {
                Debug.Log($"[SetupCharacterWorldUIPrefabs] 캐릭터 프리팹 정비 완료: {prefabPath} (Layer={root.layer}, BoxCollider={boxCollider.center}/{boxCollider.size}, Trigger={boxCollider.isTrigger})");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Bounds CalculateLocalRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds combined = new Bounds(Vector3.zero, Vector3.zero);
        Matrix4x4 rootInv = root.transform.worldToLocalMatrix;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            Bounds worldBounds = r.bounds;
            Vector3 localCenter = rootInv.MultiplyPoint3x4(worldBounds.center);
            Bounds localBounds = new Bounds(localCenter, worldBounds.size);
            if (!hasBounds)
            {
                combined = localBounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(localBounds);
            }
        }

        return combined;
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
