using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터의 버프/디버프(이펙트) 아이콘 목록 및 쿨다운 링을 화면에 렌더링하고 위치를 추적하는 독립 View 컴포넌트입니다.
/// </summary>
public class CharacterEffectListUI : MonoBehaviour
{
    [Header("Target & Position Settings")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Vector3 worldOffset = UIConstants.DEFAULT_EFFECT_LIST_OFFSET;
    [SerializeField] private bool useBillboard = true;

    [Header("Buff/Debuff Container")]
    [SerializeField] private Transform effectContainer;
    [SerializeField] private CharacterEffectItemUI effectItemPrefab;

    private readonly List<CharacterEffectItemUI> activeEffectItems = new List<CharacterEffectItemUI>();
    private Camera cachedCamera;

    public Transform TargetTransform => targetTransform;
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
    /// 새로운 이펙트 아이콘을 컨테이너에 추가합니다.
    /// </summary>
    public void AddEffect(Effect effect)
    {
        if (effect == null || effectContainer == null) return;

        // 이미 존재하는 경우 리프레시
        var existing = activeEffectItems.Find(x => x != null && x.TargetEffect == effect);
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
            GameObject go = new GameObject($"EffectItem_{effect.Data?.effectName}");
            go.transform.SetParent(effectContainer, false);
            itemUI = go.AddComponent<CharacterEffectItemUI>();
        }

        itemUI.Setup(effect);
        activeEffectItems.Add(itemUI);
    }

    /// <summary>
    /// 스택이나 지속시간이 갱신된 이펙트의 UI를 업데이트합니다.
    /// </summary>
    public void UpdateEffect(Effect effect)
    {
        var item = activeEffectItems.Find(x => x != null && x.TargetEffect == effect);
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
        var item = activeEffectItems.Find(x => x != null && x.TargetEffect == effect);
        if (item != null)
        {
            activeEffectItems.Remove(item);
            if (item.gameObject != null)
            {
                Destroy(item.gameObject);
            }
        }
    }

    /// <summary>
    /// 활성화된 모든 이펙트 아이콘의 남은 지속시간 쿨다운 링을 갱신합니다.
    /// </summary>
    public void RefreshDurations()
    {
        for (int i = 0; i < activeEffectItems.Count; i++)
        {
            if (activeEffectItems[i] != null)
            {
                activeEffectItems[i].Refresh();
            }
        }
    }

    /// <summary>
    /// 모든 이펙트 UI를 즉시 제거하고 비웁니다.
    /// </summary>
    public void ClearAll()
    {
        for (int i = activeEffectItems.Count - 1; i >= 0; i--)
        {
            if (activeEffectItems[i] != null && activeEffectItems[i].gameObject != null)
            {
                Destroy(activeEffectItems[i].gameObject);
            }
        }
        activeEffectItems.Clear();
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

        // 1. 타깃 월드 위치 + 오프셋 반영
        transform.position = targetTransform.position + worldOffset;

        // 2. Orthographic 뷰 최적화: 카메라 전방 벡터를 반전하여 UI가 카메라를 정면으로 응시
        if (useBillboard)
        {
            transform.forward = -cachedCamera.transform.forward;
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
