using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 클릭 시 우측 월드 공간에 표시되는 이펙트 상세 툴팁 패널(아이콘+설명 목록) View 컴포넌트입니다.
/// </summary>
public class CharacterEffectDetailUI : MonoBehaviour, IWorldFollowUI
{
    [Header("Target & Position Settings")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Vector3 worldOffset = UIConstants.DEFAULT_EFFECT_DETAIL_OFFSET;
    [SerializeField] private bool useBillboard = true;

    [Header("Container & Prefab")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private CharacterEffectDetailItemUI detailItemPrefab;
    [SerializeField] private CanvasGroup canvasGroup;

    private readonly List<CharacterEffectDetailItemUI> activeItems = new List<CharacterEffectDetailItemUI>();
    private readonly List<CharacterEffectDetailItemUI> pooledItems = new List<CharacterEffectDetailItemUI>();
    private Camera cachedCamera;
    private RectTransform cachedRectTransform;

    public int ActiveItemCount => activeItems.Count;

    public Transform TargetTransform => targetTransform;
    public Vector3 WorldOffset
    {
        get => worldOffset;
        set => worldOffset = value;
    }

    private void Awake()
    {
        cachedCamera = Camera.main;
        cachedRectTransform = transform as RectTransform;

        if (itemContainer == null)
        {
            itemContainer = transform;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        // 월드 툴팁이 마우스 클릭이나 UI 레이캐스트를 가로채지 않도록 보장
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// 상세 툴팁이 추적할 타깃 트랜스폼 및 오프셋을 설정합니다.
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
    /// 전달받은 이펙트 목록 전체로 상세 항목을 재구성합니다.
    /// </summary>
    public void RebuildAll(IReadOnlyList<Effect> effects)
    {
        ClearAll();
        if (effects == null || itemContainer == null) return;

        for (int i = 0; i < effects.Count; i++)
        {
            Effect effect = effects[i];
            if (effect == null || effect.Data == null) continue;

            CharacterEffectDetailItemUI itemUI = AcquireItem();
            if (itemUI == null) break;

            itemUI.Setup(effect);
            activeItems.Add(itemUI);
        }

        ForceRebuildLayout();
    }

    /// <summary>
    /// 이펙트를 추가하거나 이미 존재하면 내용을 갱신합니다.
    /// </summary>
    public void AddOrUpdateEffect(Effect effect)
    {
        if (effect == null || effect.Data == null || itemContainer == null) return;

        for (int i = 0; i < activeItems.Count; i++)
        {
            CharacterEffectDetailItemUI existing = activeItems[i];
            if (existing != null && existing.TargetEffect == effect)
            {
                existing.Refresh();
                ForceRebuildLayout();
                return;
            }
        }

        CharacterEffectDetailItemUI itemUI = AcquireItem();
        if (itemUI == null) return;

        itemUI.Setup(effect);
        activeItems.Add(itemUI);
        ForceRebuildLayout();
    }

    /// <summary>
    /// 만료/제거된 이펙트 항목을 목록에서 제거합니다.
    /// </summary>
    public void RemoveEffect(Effect effect)
    {
        if (effect == null) return;

        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            CharacterEffectDetailItemUI item = activeItems[i];
            if (item != null && item.TargetEffect == effect)
            {
                activeItems.RemoveAt(i);
                ReleaseItem(item);
                ForceRebuildLayout();
                return;
            }
        }
    }

    /// <summary>
    /// 활성화된 모든 상세 항목의 남은 지속시간 게이지를 갱신합니다. (GC 무할당)
    /// </summary>
    public void RefreshDurations()
    {
        for (int i = 0; i < activeItems.Count; i++)
        {
            if (activeItems[i] != null)
            {
                activeItems[i].RefreshCooldownGauge();
            }
        }
    }

    /// <summary>
    /// 모든 상세 항목을 비활성 풀로 반환하고 활성 목록을 비웁니다.
    /// </summary>
    public void ClearAll()
    {
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            ReleaseItem(activeItems[i]);
        }
        activeItems.Clear();
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    /// <summary>
    /// 카메라 기준 우측/상단/전방 축으로 오프셋을 적용해 캐릭터 오른쪽에 배치하고 빌보드 회전합니다.
    /// </summary>
    public void UpdatePosition()
    {
        if (targetTransform == null) return;

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
            if (cachedCamera == null) return;
        }

        Transform camTransform = cachedCamera.transform;
        transform.position = targetTransform.position
            + camTransform.right * worldOffset.x
            + Vector3.up * worldOffset.y
            + camTransform.forward * worldOffset.z;

        if (useBillboard)
        {
            transform.rotation = camTransform.rotation;
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        UpdatePosition();
        ForceRebuildLayout();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private CharacterEffectDetailItemUI AcquireItem()
    {
        for (int i = pooledItems.Count - 1; i >= 0; i--)
        {
            CharacterEffectDetailItemUI pooled = pooledItems[i];
            pooledItems.RemoveAt(i);
            if (pooled != null)
            {
                pooled.transform.SetParent(itemContainer, false);
                pooled.transform.SetAsLastSibling();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        GameObject prefabToInstantiate = null;
        if (detailItemPrefab != null)
        {
            prefabToInstantiate = detailItemPrefab.gameObject;
        }
        else if (AssetCacheManager.instance != null
            && AssetCacheManager.instance.TryGetUI(UIConstants.PREFAB_CHARACTER_EFFECT_DETAIL_ITEM, out GameObject cachedPrefab))
        {
            prefabToInstantiate = cachedPrefab;
        }

        if (prefabToInstantiate == null)
        {
            Debug.LogError($"[CharacterEffectDetailUI] 필수 프리팹({UIConstants.PREFAB_CHARACTER_EFFECT_DETAIL_ITEM})을 찾을 수 없습니다.", this);
            return null;
        }

        GameObject uiGo = Instantiate(prefabToInstantiate, itemContainer, false);
        CharacterEffectDetailItemUI itemUI = uiGo.GetComponent<CharacterEffectDetailItemUI>();
        if (itemUI == null)
        {
            Debug.LogError($"[CharacterEffectDetailUI] 프리팹({UIConstants.PREFAB_CHARACTER_EFFECT_DETAIL_ITEM})에 CharacterEffectDetailItemUI 컴포넌트가 누락되었습니다!", uiGo);
            Destroy(uiGo);
            return null;
        }

        return itemUI;
    }

    private void ReleaseItem(CharacterEffectDetailItemUI item)
    {
        if (item == null) return;
        if (item.gameObject != null)
        {
            item.gameObject.SetActive(false);
        }
        pooledItems.Add(item);
    }

    private void ForceRebuildLayout()
    {
        if (!gameObject.activeInHierarchy) return;

        Canvas.ForceUpdateCanvases();
        if (cachedRectTransform == null)
        {
            cachedRectTransform = transform as RectTransform;
        }
        if (cachedRectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(cachedRectTransform);
        }
    }
}
