using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 적 행동 예고 월드 UI 프리팹(CharacterIntentUI.prefab)을 생성하고
/// 적 캐릭터 프리팹(Enemy_SkeletonWarrior, Enemy_SkeletonGuard)의 intentOffset 직렬화 및
/// Addressables(UI_Prefabs) 등록을 일괄 구성하는 에디터 도구입니다.
/// </summary>
public static class SetupEnemyIntentUIPrefab
{
    private const string BattleUiDir = "Assets/Prefab/UI/Battle";
    private const string IntentPrefabPath = "Assets/Prefab/UI/Battle/CharacterIntentUI.prefab";

    private const string WarriorPrefabPath = "Assets/Prefab/Enemy/Enemy_SkeletonWarrior.prefab";
    private const string GuardPrefabPath = "Assets/Prefab/Enemy/Enemy_SkeletonGuard.prefab";

    [MenuItem("Tools/UI/Setup Enemy Intent UI Prefab")]
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
            Debug.LogWarning("[SetupEnemyIntentUIPrefab] WorldUI 레이어를 찾을 수 없어 기본값(11)을 사용합니다.");
        }

        TMP_FontAsset baseFont = ResolveBaseFont();
        Sprite squareSprite = ResolveSquareSprite();

        // 1. CharacterIntentUI.prefab 생성/갱신
        CreateOrUpdateIntentPrefab(worldUiLayer, baseFont, squareSprite);

        // 2. 적 캐릭터 프리팹 2종 intentOffset 직렬화 (statusBarOffset + (0, 0.35, 0))
        ConfigureEnemyIntentOffset(WarriorPrefabPath);
        ConfigureEnemyIntentOffset(GuardPrefabPath);

        // 3. Addressables UI_Prefabs 그룹 자동 등록
        AddressableAutoRegister.RegisterAllUIPrefabs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = "[SetupEnemyIntentUIPrefab] CharacterIntentUI.prefab 생성, 적 프리팹 2종 intentOffset 직렬화, Addressables UI_Prefabs 등록 완료";
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

        GameObject statusBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterStatusBarUI.prefab");
        if (statusBarPrefab != null)
        {
            TextMeshProUGUI tmp = statusBarPrefab.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && tmp.font != null)
            {
                return tmp.font;
            }
        }

        TMP_FontAsset fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NEXON Lv2 Gothic OTF SDF.asset");
        if (fallbackFont == null)
        {
            Debug.LogError("[SetupEnemyIntentUIPrefab] 기본 TMP_FontAsset를 찾을 수 없습니다.");
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
            Debug.LogError("[SetupEnemyIntentUIPrefab] 기본 Square 스프라이트를 찾을 수 없습니다.");
        }
        return fallbackSprite;
    }

    private static void CreateOrUpdateIntentPrefab(int worldUiLayer, TMP_FontAsset baseFont, Sprite squareSprite)
    {
        GameObject root = new GameObject(
            "CharacterIntentUI",
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(CharacterIntentUI));

        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(42f, 28f);

            // 배경 배지 (중첩 Canvas 없음, 호버 감지는 24x24 아이콘 Image가 전담)
            Image badgeImage = root.GetComponent<Image>();
            badgeImage.sprite = squareSprite;
            badgeImage.color = new Color(0.14f, 0.16f, 0.22f, 0.92f);
            badgeImage.raycastTarget = false;

            HorizontalLayoutGroup layoutGroup = root.GetComponent<HorizontalLayoutGroup>();
            layoutGroup.padding = new RectOffset(3, 3, 2, 2);
            layoutGroup.spacing = 2f;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // 아이콘 (24x24, 마우스 오버용 raycastTarget = true)
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObj.transform.SetParent(root.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(24f, 24f);

            LayoutElement iconLayout = iconObj.GetComponent<LayoutElement>();
            iconLayout.minWidth = 24f;
            iconLayout.minHeight = 24f;
            iconLayout.preferredWidth = 24f;
            iconLayout.preferredHeight = 24f;
            iconLayout.flexibleWidth = 0f;
            iconLayout.flexibleHeight = 0f;

            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = squareSprite;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = true;

            // 예상 수치 텍스트 (raycastTarget = false)
            GameObject valueObj = new GameObject("ValueText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            valueObj.transform.SetParent(root.transform, false);
            RectTransform valueRect = valueObj.GetComponent<RectTransform>();
            valueRect.sizeDelta = new Vector2(12f, 22f);

            LayoutElement valueLayout = valueObj.GetComponent<LayoutElement>();
            valueLayout.minWidth = 10f;
            valueLayout.preferredHeight = 22f;
            valueLayout.flexibleWidth = 0f;
            valueLayout.flexibleHeight = 0f;

            TextMeshProUGUI valueTmp = valueObj.GetComponent<TextMeshProUGUI>();
            if (baseFont != null) valueTmp.font = baseFont;
            valueTmp.text = "6";
            valueTmp.fontSize = 12f;
            valueTmp.enableAutoSizing = true;
            valueTmp.fontSizeMin = 8f;
            valueTmp.fontSizeMax = 12f;
            valueTmp.fontStyle = FontStyles.Bold;
            valueTmp.color = Color.white;
            valueTmp.alignment = TextAlignmentOptions.Center;
            valueTmp.textWrappingMode = TextWrappingModes.NoWrap;
            valueTmp.overflowMode = TextOverflowModes.Overflow;
            valueTmp.raycastTarget = false;

            // 직렬화 바인딩
            CharacterIntentUI intentUI = root.GetComponent<CharacterIntentUI>();
            SerializedObject serializedUI = new SerializedObject(intentUI);
            serializedUI.FindProperty("worldOffset").vector3Value = UIConstants.DEFAULT_INTENT_OFFSET;
            serializedUI.FindProperty("useBillboard").boolValue = true;
            serializedUI.FindProperty("backgroundBadgeImage").objectReferenceValue = badgeImage;
            serializedUI.FindProperty("iconImage").objectReferenceValue = iconImg;
            serializedUI.FindProperty("valueText").objectReferenceValue = valueTmp;
            serializedUI.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, worldUiLayer);

            bool saved = false;
            PrefabUtility.SaveAsPrefabAsset(root, IntentPrefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupEnemyIntentUIPrefab] 프리팹 저장 실패: {IntentPrefabPath}");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureEnemyIntentOffset(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError($"[SetupEnemyIntentUIPrefab] 적 프리팹을 불러오지 못했습니다: {prefabPath}");
            return;
        }

        try
        {
            CharacterUIController uiController = root.GetComponent<CharacterUIController>();
            if (uiController == null)
            {
                Debug.LogError($"[SetupEnemyIntentUIPrefab] CharacterUIController가 없습니다: {prefabPath}");
                return;
            }

            SerializedObject serializedCtrl = new SerializedObject(uiController);
            Vector3 statusOffset = serializedCtrl.FindProperty("statusBarOffset").vector3Value;
            Vector3 intentOffset = statusOffset + new Vector3(0f, 0.35f, 0f);
            serializedCtrl.FindProperty("intentOffset").vector3Value = intentOffset;
            serializedCtrl.ApplyModifiedPropertiesWithoutUndo();

            bool saved = false;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out saved);
            if (!saved)
            {
                Debug.LogError($"[SetupEnemyIntentUIPrefab] 적 프리팹 저장 실패: {prefabPath}");
            }
            else
            {
                Debug.Log($"[SetupEnemyIntentUIPrefab] 적 프리팹 intentOffset 직렬화 완료: {prefabPath} -> {intentOffset}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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
