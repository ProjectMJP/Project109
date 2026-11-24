using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터의 버프/디버프(이펙트) 목록을 월드 공간에 렌더링하고 위치를 추적하는 독립 View 컴포넌트입니다.
/// 컴팩트 2-B안(useDotMode = true)에서는 툴팁 없는 소형 버프/디버프 점(Dot)만 풀링하여 표시합니다.
/// </summary>
public class CharacterEffectListUI : MonoBehaviour, IWorldFollowUI
{
    private sealed class DotEntry
    {
        public Effect effect;
        public GameObject go;
        public RectTransform rect;
        public Image image;
    }

    private const int FallbackDotTextureSize = 16;

    [Header("Target & Position Settings")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Vector3 worldOffset = UIConstants.DEFAULT_EFFECT_LIST_OFFSET;
    [SerializeField] private bool useBillboard = true;

    [Header("Buff/Debuff Container")]
    [SerializeField] private Transform effectContainer;
    [SerializeField] private CharacterEffectItemUI effectItemPrefab;

    [Header("Compact Dot Mode (2-B안)")]
    [SerializeField] private bool useDotMode = true;
    [SerializeField] private float dotSize = 7f;
    [SerializeField] private Color buffDotColor = new Color(0.24f, 0.78f, 1f, 1f);
    [SerializeField] private Color debuffDotColor = new Color(0.95f, 0.28f, 0.30f, 1f);
    [SerializeField] private Sprite dotSprite;

    private readonly List<CharacterEffectItemUI> activeEffectItems = new List<CharacterEffectItemUI>();
    private readonly List<DotEntry> activeDotEntries = new List<DotEntry>();
    private readonly List<DotEntry> pooledDotEntries = new List<DotEntry>();
    private static Sprite _fallbackDotSprite;
    private Camera cachedCamera;

    public Transform TargetTransform => targetTransform;
    public bool UseDotMode => useDotMode;
    public int ActiveEffectCount => useDotMode ? activeDotEntries.Count : activeEffectItems.Count;

    public Vector3 WorldOffset
    {
        get => worldOffset;
        set => worldOffset = value;
    }

    public Transform EffectContainer
    {
        get => effectContainer;
        set => effectContainer = value;
    }

    public CharacterEffectItemUI EffectItemPrefab
    {
        get => effectItemPrefab;
        set => effectItemPrefab = value;
    }

    private void Awake()
    {
        cachedCamera = Camera.main;
        if (effectContainer == null)
        {
            effectContainer = transform;
        }
    }

    /// <summary>
    /// 이펙트 목록 UI가 추적할 타깃 트랜스폼 및 오프셋을 설정합니다.
    /// </summary>
    public void SetTarget(Transform target, Vector3? offset = null)
    {
        targetTransform = target;
        if (offset.HasValue)
        {
            worldOffset = offset.Value;
        }

        UpdatePosition();
    }

    /// <summary>
    /// 새로운 이펙트 항목(점 또는 아이콘)을 컨테이너에 추가합니다.
    /// </summary>
    public void AddEffect(Effect effect)
    {
        if (effect == null) return;
        if (effectContainer == null)
        {
            effectContainer = transform;
        }

        if (useDotMode)
        {
            DotEntry existingDot = FindDotEntry(effect);
            if (existingDot != null)
            {
                if (existingDot.image != null)
                {
                    existingDot.image.color = ResolveDotColor(effect);
                }
                return;
            }

            DotEntry entry = AcquireDotEntry();
            entry.effect = effect;
            if (entry.image != null)
            {
                entry.image.color = ResolveDotColor(effect);
            }
            activeDotEntries.Add(entry);
            return;
        }

        // 이미 존재하는 경우 리프레시
        CharacterEffectItemUI existing = FindLegacyItem(effect);
        if (existing != null)
        {
            existing.Refresh();
            return;
        }

        CharacterEffectItemUI itemUI = null;
        GameObject prefabToInstantiate = null;

        if (effectItemPrefab != null)
        {
            prefabToInstantiate = effectItemPrefab.gameObject;
        }
        else if (AssetCacheManager.instance != null && AssetCacheManager.instance.TryGetUI(UIConstants.PREFAB_CHARACTER_EFFECT_ITEM, out GameObject cachedPrefab))
        {
            prefabToInstantiate = cachedPrefab;
        }

        if (prefabToInstantiate != null)
        {
            GameObject uiGo = Instantiate(prefabToInstantiate, effectContainer);
            itemUI = uiGo.GetComponent<CharacterEffectItemUI>();
            if (itemUI == null)
            {
                itemUI = uiGo.AddComponent<CharacterEffectItemUI>();
            }
        }
        else
        {
            Debug.LogError($"[CharacterEffectListUI] 필수 프리팹({UIConstants.PREFAB_CHARACTER_EFFECT_ITEM})을 찾을 수 없습니다.");
            return;
        }

        itemUI.Setup(effect);
        activeEffectItems.Add(itemUI);
    }

    /// <summary>
    /// 스택이나 지속시간이 갱신된 이펙트의 UI를 업데이트합니다.
    /// </summary>
    public void UpdateEffect(Effect effect)
    {
        if (effect == null) return;

        if (useDotMode)
        {
            DotEntry dot = FindDotEntry(effect);
            if (dot != null)
            {
                if (dot.image != null)
                {
                    dot.image.color = ResolveDotColor(effect);
                }
            }
            else
            {
                AddEffect(effect);
            }
            return;
        }

        CharacterEffectItemUI item = FindLegacyItem(effect);
        if (item != null)
        {
            item.Refresh();
        }
        else
        {
            AddEffect(effect);
        }
    }

    /// <summary>
    /// 만료/제거된 이펙트의 UI를 제거합니다.
    /// </summary>
    public void RemoveEffect(Effect effect)
    {
        if (effect == null) return;

        if (useDotMode)
        {
            for (int i = activeDotEntries.Count - 1; i >= 0; i--)
            {
                DotEntry entry = activeDotEntries[i];
                if (entry != null && entry.effect == effect)
                {
                    activeDotEntries.RemoveAt(i);
                    ReleaseDotEntry(entry);
                    return;
                }
            }
            return;
        }

        for (int i = activeEffectItems.Count - 1; i >= 0; i--)
        {
            CharacterEffectItemUI item = activeEffectItems[i];
            if (item != null && item.TargetEffect == effect)
            {
                activeEffectItems.RemoveAt(i);
                if (item.gameObject != null)
                {
                    Destroy(item.gameObject);
                }
                return;
            }
        }
    }

    /// <summary>
    /// 활성화된 모든 이펙트 아이콘의 남은 지속시간 쿨다운 링을 갱신합니다. (도트 모드에서는 즉시 반환하여 0 할당)
    /// </summary>
    public void RefreshDurations()
    {
        if (useDotMode) return;

        for (int i = 0; i < activeEffectItems.Count; i++)
        {
            if (activeEffectItems[i] != null)
            {
                activeEffectItems[i].RefreshCooldownGauge();
            }
        }
    }

    /// <summary>
    /// 모든 이펙트 UI를 즉시 제거하고 비웁니다.
    /// </summary>
    public void ClearAll()
    {
        for (int i = activeDotEntries.Count - 1; i >= 0; i--)
        {
            ReleaseDotEntry(activeDotEntries[i]);
        }
        activeDotEntries.Clear();

        for (int i = activeEffectItems.Count - 1; i >= 0; i--)
        {
            if (activeEffectItems[i] != null && activeEffectItems[i].gameObject != null)
            {
                Destroy(activeEffectItems[i].gameObject);
            }
        }
        activeEffectItems.Clear();
    }

    private DotEntry FindDotEntry(Effect effect)
    {
        for (int i = 0; i < activeDotEntries.Count; i++)
        {
            DotEntry entry = activeDotEntries[i];
            if (entry != null && entry.effect == effect)
            {
                return entry;
            }
        }
        return null;
    }

    private CharacterEffectItemUI FindLegacyItem(Effect effect)
    {
        for (int i = 0; i < activeEffectItems.Count; i++)
        {
            CharacterEffectItemUI item = activeEffectItems[i];
            if (item != null && item.TargetEffect == effect)
            {
                return item;
            }
        }
        return null;
    }

    private DotEntry AcquireDotEntry()
    {
        for (int i = pooledDotEntries.Count - 1; i >= 0; i--)
        {
            DotEntry pooled = pooledDotEntries[i];
            pooledDotEntries.RemoveAt(i);
            if (pooled != null && pooled.go != null)
            {
                if (pooled.rect != null)
                {
                    pooled.rect.SetParent(effectContainer, false);
                    pooled.rect.SetAsLastSibling();
                    pooled.rect.sizeDelta = new Vector2(dotSize, dotSize);
                }
                pooled.go.SetActive(true);
                return pooled;
            }
        }

        GameObject go = new GameObject("EffectDot", typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(effectContainer, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(dotSize, dotSize);

        Image img = go.GetComponent<Image>();
        img.sprite = dotSprite != null ? dotSprite : GetOrCreateFallbackDotSprite();
        img.preserveAspect = true;
        img.raycastTarget = false;

        return new DotEntry
        {
            go = go,
            rect = rect,
            image = img
        };
    }

    private void ReleaseDotEntry(DotEntry entry)
    {
        if (entry == null) return;
        entry.effect = null;
        if (entry.go != null)
        {
            entry.go.SetActive(false);
            pooledDotEntries.Add(entry);
        }
    }

    private Color ResolveDotColor(Effect effect)
    {
        if (effect != null && effect.Data != null && effect.Data.effectType == EffectType.Buff)
        {
            return buffDotColor;
        }
        return debuffDotColor;
    }

    private static Sprite GetOrCreateFallbackDotSprite()
    {
        if (_fallbackDotSprite != null)
        {
            return _fallbackDotSprite;
        }

        Color[] pixels = new Color[FallbackDotTextureSize * FallbackDotTextureSize];
        float center = (FallbackDotTextureSize - 1) * 0.5f;
        float radius = (FallbackDotTextureSize * 0.5f) - 1.5f;

        for (int y = 0; y < FallbackDotTextureSize; y++)
        {
            for (int x = 0; x < FallbackDotTextureSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                pixels[y * FallbackDotTextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        Texture2D tex = new Texture2D(FallbackDotTextureSize, FallbackDotTextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = "EffectDotFallbackCircle"
        };
        tex.SetPixels(pixels);
        tex.Apply(false, true);

        _fallbackDotSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, FallbackDotTextureSize, FallbackDotTextureSize),
            new Vector2(0.5f, 0.5f),
            100f);
        _fallbackDotSprite.hideFlags = HideFlags.HideAndDontSave;
        _fallbackDotSprite.name = tex.name;
        return _fallbackDotSprite;
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    /// <summary>
    /// Orthographic 카메라 환경에 맞추어 캐릭터 위치를 추적하고, 카메라의 반대 방향 벡터를 정면으로 바라보게 회전합니다.
    /// </summary>
    public void UpdatePosition()
    {
        if (targetTransform == null) return;

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
            if (cachedCamera == null) return;
        }

        // 1. 타깃 월드 위치 + 오프셋 반영 (바닥·자기 발 클리핑 방지 및 앞쪽 캐릭터 깊이 테스트 보장)
        transform.position = CharacterStatusBarUI.ComputeDepthSafeWorldPosition(targetTransform.position, worldOffset, cachedCamera);

        // 2. 카메라의 회전과 완전히 일치시켜 UI가 컬링이나 반전 없이 정면을 바라보도록 유지
        if (useBillboard)
        {
            transform.rotation = cachedCamera.transform.rotation;
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
