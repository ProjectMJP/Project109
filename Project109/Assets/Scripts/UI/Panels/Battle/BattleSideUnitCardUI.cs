using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 전투 화면 좌·우 사이드 유닛 상세 HUD에 표시되는 개별 유닛 상태 카드 뷰입니다.
/// 초상화, 적 행동 예고 배지, 이름, 수치 포함 HP/Shield·Stamina 게이지, 버프/디버프 아이콘 행을 표시하며
/// 카드 호버 시 3방향 상호 하이라이트와 화면 중앙 방향 툴팁 배치를, 일반 모드 좌클릭 시 유닛 방향 회전을 제공합니다.
/// </summary>
public class BattleSideUnitCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private const int FallbackEmblemSize = 32;

    private static readonly Color ColorBorderPlayerTeam = new Color(0.28f, 0.62f, 1f, 1f);
    private static readonly Color ColorBgPlayerTeam = new Color(0.11f, 0.17f, 0.28f, 0.92f);

    private static readonly Color ColorBorderEnemyTeam = new Color(0.95f, 0.30f, 0.28f, 1f);
    private static readonly Color ColorBgEnemyTeam = new Color(0.25f, 0.11f, 0.13f, 0.92f);

    private static readonly Color ColorBorderCurrentTurn = new Color(1f, 0.82f, 0.22f, 1f);
    private static readonly Color ColorBorderHighlighted = new Color(1f, 0.94f, 0.42f, 1f);

    private static readonly Color ColorPortraitPlayer = new Color(0.45f, 0.82f, 1f, 0.92f);
    private static readonly Color ColorPortraitEnemy = new Color(1f, 0.48f, 0.45f, 0.92f);

    private static Sprite _playerEmblemSprite;
    private static Sprite _enemyEmblemSprite;

    [Header("Card Frame References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private GameObject highlightFrameObj;

    [Header("Portrait & Intent References")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject intentBadgeObj;
    [SerializeField] private Image intentIconImage;
    [SerializeField] private TMP_Text intentValueText;

    [Header("Stats & Gauges References")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GaugeUI healthGaugeUI;
    [SerializeField] private GaugeUI staminaGaugeUI;

    [Header("Effect Row References")]
    [SerializeField] private Transform effectRowContainer;
    [SerializeField] private CharacterEffectItemUI effectItemPrefab;

    private readonly List<CharacterEffectItemUI> _activeEffectItems = new List<CharacterEffectItemUI>(8);
    private readonly List<CharacterEffectItemUI> _pooledEffectItems = new List<CharacterEffectItemUI>(8);

    private ICharacterController _boundController;
    private Character _boundCharacter;
    private bool _isPlayerSide;
    private bool _isCurrentTurn;
    private bool _isHighlighted;
    private bool _isPointerHovering;
    private bool _isSubscribed;

    public Character BoundCharacter => _boundCharacter;
    public ICharacterController BoundController => _boundController;
    public bool IsPlayerSide => _isPlayerSide;
    public bool IsCurrentTurn => _isCurrentTurn;
    public bool IsHighlighted => _isHighlighted;
    public IReadOnlyList<CharacterEffectItemUI> ActiveEffectItems => _activeEffectItems;

    private void Awake()
    {
        if (highlightFrameObj != null)
        {
            highlightFrameObj.SetActive(false);
        }

        if (intentBadgeObj != null)
        {
            intentBadgeObj.SetActive(false);
        }

        if (healthGaugeUI != null)
        {
            healthGaugeUI.SetTextVisible(true);
        }

        if (staminaGaugeUI != null)
        {
            staminaGaugeUI.SetTextVisible(true);
        }
    }

    private void OnDisable()
    {
        ClearPointerHover();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    /// <summary>
    /// 전투원 컨트롤러와 진영 정보를 카드에 바인딩하고 이벤트를 구독합니다.
    /// 동일 캐릭터/컨트롤러 재바인딩 시에는 호버 상태를 유지한 채 시각 정보만 갱신합니다.
    /// </summary>
    public void Bind(ICharacterController controller, bool isPlayerSide)
    {
        if (controller == null || controller.controlledCharacter == null)
        {
            Unbind();
            return;
        }

        if (ReferenceEquals(_boundController, controller) &&
            _boundCharacter == controller.controlledCharacter &&
            _isPlayerSide == isPlayerSide)
        {
            RefreshAll();
            return;
        }

        Unbind();

        _boundController = controller;
        _boundCharacter = controller.controlledCharacter;
        _isPlayerSide = isPlayerSide;

        ApplyTeamVisuals();
        ApplyNameAndPortrait();
        SubscribeCharacterEvents();
        RefreshAll();
    }

    /// <summary>
    /// 이벤트 구독 해제, 호버 초기화, 이펙트 아이콘 풀 반납 후 바인딩 참조를 정리합니다.
    /// </summary>
    public void Unbind()
    {
        ClearPointerHover();
        UnsubscribeCharacterEvents();
        ReturnAllEffectItemsToPool();

        _boundController = null;
        _boundCharacter = null;
        _isCurrentTurn = false;
        _isHighlighted = false;

        if (highlightFrameObj != null)
        {
            highlightFrameObj.SetActive(false);
        }

        if (intentBadgeObj != null)
        {
            intentBadgeObj.SetActive(false);
        }
    }

    /// <summary>
    /// 현재 턴 진행 여부 표시를 갱신합니다.
    /// </summary>
    public void SetCurrentTurn(bool isCurrentTurn)
    {
        _isCurrentTurn = isCurrentTurn;
        ApplyBorderColor();
    }

    /// <summary>
    /// 3방향 상호 하이라이트 강조 프레임 및 테두리 색상을 설정합니다.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        _isHighlighted = highlighted;
        if (highlightFrameObj != null)
        {
            highlightFrameObj.SetActive(highlighted);
        }
        ApplyBorderColor();
    }

    /// <summary>
    /// 체력/실드, 스태미나, 행동 예고, 이펙트 목록을 모두 최신 상태로 갱신합니다.
    /// </summary>
    public void RefreshAll()
    {
        if (_boundCharacter == null) return;

        RefreshHealth();
        RefreshStamina();
        RefreshIntent();
        RebuildEffects();
    }

    /// <summary>
    /// 매 프레임 활성 이펙트 아이콘의 지속시간 쿨다운 게이지를 갱신합니다. (GC 무할당)
    /// </summary>
    public void RefreshEffectDurations()
    {
        for (int i = 0; i < _activeEffectItems.Count; i++)
        {
            CharacterEffectItemUI item = _activeEffectItems[i];
            if (item != null)
            {
                item.RefreshCooldownGauge();
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_boundCharacter == null) return;

        _isPointerHovering = true;
        if (BattleSideUnitHUDPanel.Instance != null)
        {
            BattleSideUnitHUDPanel.Instance.NotifyCardPointerEnter(this);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearPointerHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_boundCharacter == null) return;
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;

        if (BattleSideUnitHUDPanel.Instance != null)
        {
            BattleSideUnitHUDPanel.Instance.NotifyCardClicked(this);
        }
    }

    private void ClearPointerHover()
    {
        if (!_isPointerHovering) return;
        _isPointerHovering = false;

        if (BattleSideUnitHUDPanel.Instance != null)
        {
            BattleSideUnitHUDPanel.Instance.NotifyCardPointerExit(this);
        }
    }

    private void SubscribeCharacterEvents()
    {
        if (_boundCharacter == null || _isSubscribed) return;

        _boundCharacter.OnCharacterHealthChanged -= HandleHealthOrShieldChanged;
        _boundCharacter.OnCharacterHealthChanged += HandleHealthOrShieldChanged;

        _boundCharacter.OnCharacterShieldChanged -= HandleHealthOrShieldChanged;
        _boundCharacter.OnCharacterShieldChanged += HandleHealthOrShieldChanged;

        _boundCharacter.OnCharacterStaminaChanged -= HandleStaminaChanged;
        _boundCharacter.OnCharacterStaminaChanged += HandleStaminaChanged;

        _boundCharacter.OnIntentChanged -= HandleIntentChanged;
        _boundCharacter.OnIntentChanged += HandleIntentChanged;

        if (_boundCharacter.effectManager != null)
        {
            _boundCharacter.effectManager.OnEffectAdded -= HandleEffectAdded;
            _boundCharacter.effectManager.OnEffectAdded += HandleEffectAdded;

            _boundCharacter.effectManager.OnEffectStacked -= HandleEffectStacked;
            _boundCharacter.effectManager.OnEffectStacked += HandleEffectStacked;

            _boundCharacter.effectManager.OnEffectRemoved -= HandleEffectRemoved;
            _boundCharacter.effectManager.OnEffectRemoved += HandleEffectRemoved;
        }

        _isSubscribed = true;
    }

    private void UnsubscribeCharacterEvents()
    {
        if (!_isSubscribed || _boundCharacter == null)
        {
            _isSubscribed = false;
            return;
        }

        _boundCharacter.OnCharacterHealthChanged -= HandleHealthOrShieldChanged;
        _boundCharacter.OnCharacterShieldChanged -= HandleHealthOrShieldChanged;
        _boundCharacter.OnCharacterStaminaChanged -= HandleStaminaChanged;
        _boundCharacter.OnIntentChanged -= HandleIntentChanged;

        if (_boundCharacter.effectManager != null)
        {
            _boundCharacter.effectManager.OnEffectAdded -= HandleEffectAdded;
            _boundCharacter.effectManager.OnEffectStacked -= HandleEffectStacked;
            _boundCharacter.effectManager.OnEffectRemoved -= HandleEffectRemoved;
        }

        _isSubscribed = false;
    }

    private void HandleHealthOrShieldChanged(Character character)
    {
        RefreshHealth();
    }

    private void HandleStaminaChanged(Character character)
    {
        RefreshStamina();
    }

    private void HandleIntentChanged(EnemyIntentInfo intent)
    {
        RefreshIntent();
    }

    private void HandleEffectAdded(Effect effect)
    {
        if (effect == null || effectRowContainer == null) return;

        CharacterEffectItemUI existing = FindActiveEffectItem(effect);
        if (existing != null)
        {
            existing.Refresh();
            return;
        }

        CharacterEffectItemUI item = AcquireEffectItem();
        if (item == null) return;

        item.Setup(effect);
        _activeEffectItems.Add(item);
    }

    private void HandleEffectStacked(Effect effect)
    {
        if (effect == null) return;

        CharacterEffectItemUI existing = FindActiveEffectItem(effect);
        if (existing != null)
        {
            existing.Refresh();
        }
        else
        {
            HandleEffectAdded(effect);
        }
    }

    private void HandleEffectRemoved(Effect effect)
    {
        if (effect == null) return;

        for (int i = _activeEffectItems.Count - 1; i >= 0; i--)
        {
            CharacterEffectItemUI item = _activeEffectItems[i];
            if (item != null && item.TargetEffect == effect)
            {
                _activeEffectItems.RemoveAt(i);
                ReturnEffectItemToPool(item);
                return;
            }
        }
    }

    private void RefreshHealth()
    {
        if (_boundCharacter == null || healthGaugeUI == null) return;

        healthGaugeUI.SetTextVisible(true);

        float maxHp = (_boundCharacter.curCharacterStat != null && _boundCharacter.curCharacterStat.maxHealth > 0f)
            ? _boundCharacter.curCharacterStat.maxHealth
            : Mathf.Max(1f, _boundCharacter.curHealth);
        float rate = maxHp > 0f ? Mathf.Clamp01(_boundCharacter.curHealth / maxHp) : 0f;

        int curHp = (_boundCharacter.curHealth >= maxHp)
            ? Mathf.RoundToInt(_boundCharacter.curHealth)
            : Mathf.CeilToInt(_boundCharacter.curHealth);
        int maxHpInt = Mathf.RoundToInt(maxHp);
        int shieldVal = Mathf.CeilToInt(_boundCharacter.shield);

        string hpText = (_boundCharacter.shield > 0f)
            ? $"({shieldVal}) {curHp} / {maxHpInt}"
            : $"{curHp} / {maxHpInt}";

        healthGaugeUI.SetGaugeColor(_boundCharacter.shield > 0f ? UIConstants.COLOR_SHIELD : UIConstants.COLOR_HEALTH);
        healthGaugeUI.Refresh(rate, hpText);
    }

    private void RefreshStamina()
    {
        if (_boundCharacter == null || staminaGaugeUI == null) return;

        staminaGaugeUI.SetTextVisible(true);

        float maxStamina = (_boundCharacter.curCharacterStat != null && _boundCharacter.curCharacterStat.maxStamina > 0f)
            ? _boundCharacter.curCharacterStat.maxStamina
            : Mathf.Max(1f, _boundCharacter.curStamina);
        float rate = maxStamina > 0f ? Mathf.Clamp01(_boundCharacter.curStamina / maxStamina) : 0f;

        int curStam = (_boundCharacter.curStamina >= maxStamina)
            ? Mathf.RoundToInt(_boundCharacter.curStamina)
            : Mathf.CeilToInt(_boundCharacter.curStamina);
        int maxStamInt = Mathf.RoundToInt(maxStamina);

        staminaGaugeUI.SetGaugeColor(UIConstants.COLOR_STAMINA);
        staminaGaugeUI.Refresh(rate, $"{curStam} / {maxStamInt}");
    }

    private void RefreshIntent()
    {
        if (intentBadgeObj == null) return;

        if (!_isPlayerSide && _boundCharacter != null)
        {
            EnemyIntentInfo intent = _boundCharacter.CurrentIntent;
            if (intent.isValid && intent.intentType != EnemyIntentType.None)
            {
                intentBadgeObj.SetActive(true);

                if (intentIconImage != null)
                {
                    Sprite iconSprite = IntentIconProvider.GetIntentIcon(intent.intentType);
                    intentIconImage.sprite = iconSprite;
                    intentIconImage.color = IntentIconProvider.GetIntentColor(intent.intentType);
                    intentIconImage.enabled = iconSprite != null;
                }

                if (intentValueText != null)
                {
                    if (intent.estimatedValue > 0)
                    {
                        intentValueText.text = intent.estimatedValue.ToString();
                        intentValueText.gameObject.SetActive(true);
                    }
                    else
                    {
                        intentValueText.gameObject.SetActive(false);
                    }
                }
                return;
            }
        }

        intentBadgeObj.SetActive(false);
    }

    private void RebuildEffects()
    {
        ReturnAllEffectItemsToPool();

        if (_boundCharacter == null || _boundCharacter.effectManager == null || effectRowContainer == null)
        {
            return;
        }

        IReadOnlyList<Effect> effects = _boundCharacter.effectManager.GetEffects();
        if (effects == null) return;

        for (int i = 0; i < effects.Count; i++)
        {
            Effect effect = effects[i];
            if (effect == null) continue;

            CharacterEffectItemUI item = AcquireEffectItem();
            if (item == null) break;

            item.Setup(effect);
            _activeEffectItems.Add(item);
        }
    }

    private CharacterEffectItemUI FindActiveEffectItem(Effect effect)
    {
        for (int i = 0; i < _activeEffectItems.Count; i++)
        {
            CharacterEffectItemUI item = _activeEffectItems[i];
            if (item != null && item.TargetEffect == effect)
            {
                return item;
            }
        }
        return null;
    }

    private CharacterEffectItemUI AcquireEffectItem()
    {
        if (effectRowContainer == null) return null;

        CharacterEffectItemUI item = null;
        for (int i = _pooledEffectItems.Count - 1; i >= 0; i--)
        {
            CharacterEffectItemUI candidate = _pooledEffectItems[i];
            _pooledEffectItems.RemoveAt(i);
            if (candidate != null)
            {
                item = candidate;
                break;
            }
        }

        if (item == null)
        {
            GameObject prefabObj = null;
            if (effectItemPrefab != null)
            {
                prefabObj = effectItemPrefab.gameObject;
            }
            else if (AssetCacheManager.instance != null &&
                AssetCacheManager.instance.TryGetUI(UIConstants.PREFAB_CHARACTER_EFFECT_ITEM, out GameObject cachedPrefab))
            {
                prefabObj = cachedPrefab;
            }

            if (prefabObj == null)
            {
                Debug.LogError($"[BattleSideUnitCardUI] effectItemPrefab({UIConstants.PREFAB_CHARACTER_EFFECT_ITEM})을 찾을 수 없습니다.", this);
                return null;
            }

            GameObject go = Instantiate(prefabObj, effectRowContainer, false);
            item = go.GetComponent<CharacterEffectItemUI>();
            if (item == null)
            {
                item = go.AddComponent<CharacterEffectItemUI>();
            }
        }

        if (item.transform.parent != effectRowContainer)
        {
            item.transform.SetParent(effectRowContainer, false);
        }
        item.transform.SetAsLastSibling();

        // WorldUI(Layer 11) 프리팹을 Screen Space Canvas(Layer 5 UI) 레이어로 이동시켜 정상 렌더링/레이캐스트 보장
        UIManager.SetLayerRecursively(item.gameObject, gameObject.layer);

        // 이펙트 아이콘 호버 시 카드 RectTransform을 기준으로 화면 중앙 방향(HorizontalInward) 툴팁 배치
        TooltipTrigger trigger = item.GetComponent<TooltipTrigger>();
        if (trigger != null)
        {
            trigger.ConfigureAnchor(transform as RectTransform, TooltipPlacementMode.HorizontalInward);
        }

        item.gameObject.SetActive(true);
        return item;
    }

    private void ReturnEffectItemToPool(CharacterEffectItemUI item)
    {
        if (item == null) return;
        if (item.gameObject != null)
        {
            item.gameObject.SetActive(false);
        }
        _pooledEffectItems.Add(item);
    }

    private void ReturnAllEffectItemsToPool()
    {
        for (int i = _activeEffectItems.Count - 1; i >= 0; i--)
        {
            ReturnEffectItemToPool(_activeEffectItems[i]);
        }
        _activeEffectItems.Clear();
    }

    private void ApplyTeamVisuals()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = _isPlayerSide ? ColorBgPlayerTeam : ColorBgEnemyTeam;
        }
        ApplyBorderColor();
    }

    private void ApplyBorderColor()
    {
        if (borderImage == null) return;

        if (_isHighlighted)
        {
            borderImage.color = ColorBorderHighlighted;
        }
        else if (_isCurrentTurn)
        {
            borderImage.color = ColorBorderCurrentTurn;
        }
        else
        {
            borderImage.color = _isPlayerSide ? ColorBorderPlayerTeam : ColorBorderEnemyTeam;
        }
    }

    private void ApplyNameAndPortrait()
    {
        if (nameText != null)
        {
            nameText.text = ResolveDisplayName();
        }

        if (portraitImage == null) return;

        Sprite resolvedPortrait = null;
        if (AssetCacheManager.instance != null)
        {
            if (_boundController is NPCUnitController npc && npc.unitData != null && !string.IsNullOrEmpty(npc.unitData.unitId))
            {
                if (!AssetCacheManager.instance.TryGetTexture("Portrait_" + npc.unitData.unitId, out resolvedPortrait))
                {
                    AssetCacheManager.instance.TryGetTexture(npc.unitData.unitId, out resolvedPortrait);
                }
            }
            else if (_boundCharacter != null && _boundCharacter.faction == CharacterFaction.Player)
            {
                AssetCacheManager.instance.TryGetTexture("Portrait_Player", out resolvedPortrait);
            }
        }

        if (resolvedPortrait != null)
        {
            portraitImage.sprite = resolvedPortrait;
            portraitImage.color = Color.white;
        }
        else
        {
            portraitImage.sprite = GetOrCreateFallbackEmblem(_isPlayerSide);
            portraitImage.color = _isPlayerSide ? ColorPortraitPlayer : ColorPortraitEnemy;
        }
    }

    private string ResolveDisplayName()
    {
        if (_boundController is NPCUnitController npc && npc.unitData != null)
        {
            if (!string.IsNullOrEmpty(npc.unitData.LocalizedName))
            {
                return npc.unitData.LocalizedName;
            }
            if (!string.IsNullOrEmpty(npc.unitData.unitId))
            {
                return npc.unitData.unitId;
            }
        }

        if (_boundCharacter != null)
        {
            if (_boundCharacter.faction == CharacterFaction.Player)
            {
                return "플레이어";
            }

            string rawName = _boundCharacter.name;
            if (!string.IsNullOrEmpty(rawName))
            {
                return rawName.Replace("(Clone)", string.Empty).Trim();
            }
        }

        return "유닛";
    }

    private static Sprite GetOrCreateFallbackEmblem(bool isPlayerSide)
    {
        if (isPlayerSide)
        {
            if (_playerEmblemSprite == null)
            {
                _playerEmblemSprite = CreateEmblemSprite(isPlayer: true);
            }
            return _playerEmblemSprite;
        }

        if (_enemyEmblemSprite == null)
        {
            _enemyEmblemSprite = CreateEmblemSprite(isPlayer: false);
        }
        return _enemyEmblemSprite;
    }

    private static Sprite CreateEmblemSprite(bool isPlayer)
    {
        bool[] mask = new bool[FallbackEmblemSize * FallbackEmblemSize];
        for (int y = 0; y < FallbackEmblemSize; y++)
        {
            for (int x = 0; x < FallbackEmblemSize; x++)
            {
                float dx = x - 15.5f;
                if (isPlayer)
                {
                    bool head = (dx * dx + (y - 20f) * (y - 20f)) <= 34f;
                    bool shoulders = y >= 5 && y <= 14 && Mathf.Abs(dx) <= (9.5f - (14f - y) * 0.2f);
                    mask[y * FallbackEmblemSize + x] = head || shoulders;
                }
                else
                {
                    bool cranium = (dx * dx + (y - 18f) * (y - 18f)) <= 46f;
                    bool jaw = y >= 6 && y <= 13 && Mathf.Abs(dx) <= 4.5f;
                    bool leftHorn = y >= 21 && y <= 27 && Mathf.Abs(x - (10f - (y - 21f) * 0.5f)) <= 1.4f;
                    bool rightHorn = y >= 21 && y <= 27 && Mathf.Abs(x - (21f + (y - 21f) * 0.5f)) <= 1.4f;
                    mask[y * FallbackEmblemSize + x] = cranium || jaw || leftHorn || rightHorn;
                }
            }
        }

        Color[] pixels = new Color[FallbackEmblemSize * FallbackEmblemSize];
        Color outline = new Color(0.08f, 0.09f, 0.12f, 0.9f);
        for (int y = 0; y < FallbackEmblemSize; y++)
        {
            for (int x = 0; x < FallbackEmblemSize; x++)
            {
                int idx = y * FallbackEmblemSize + x;
                if (mask[idx])
                {
                    pixels[idx] = Color.white;
                }
                else if (HasNeighbor(mask, x, y))
                {
                    pixels[idx] = outline;
                }
                else
                {
                    pixels[idx] = Color.clear;
                }
            }
        }

        Texture2D tex = new Texture2D(FallbackEmblemSize, FallbackEmblemSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = isPlayer ? "SideCardEmblem_Player" : "SideCardEmblem_Enemy"
        };
        tex.SetPixels(pixels);
        tex.Apply(false, true);

        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, FallbackEmblemSize, FallbackEmblemSize), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        sprite.name = tex.name;
        return sprite;
    }

    private static bool HasNeighbor(bool[] mask, int x, int y)
    {
        for (int oy = -1; oy <= 1; oy++)
        {
            int ny = y + oy;
            if (ny < 0 || ny >= FallbackEmblemSize) continue;

            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = x + ox;
                if (nx < 0 || nx >= FallbackEmblemSize) continue;

                if (mask[ny * FallbackEmblemSize + nx])
                {
                    return true;
                }
            }
        }
        return false;
    }
}
