using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 전투 타임라인 바에 배치되는 개별 유닛 행동 슬롯 UI 컴포넌트입니다.
/// 현재 턴, 다음 차례 예측, 대기 유닛, 기절 및 적 행동 예고([17]) 배지를 표시하고
/// 호버 시 월드 캐릭터 상태바 강조 및 L2 상세 툴팁을 제공합니다.
/// </summary>
public class BattleTimelineSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const int FallbackEmblemSize = 32;

    private static readonly Color ColorBorderCurrentTurn = new Color(1f, 0.82f, 0.22f, 1f);
    private static readonly Color ColorBgCurrentTurn = new Color(0.25f, 0.20f, 0.09f, 0.96f);

    private static readonly Color ColorBorderPredictedSelf = new Color(0.22f, 0.88f, 0.96f, 1f);
    private static readonly Color ColorBgPredictedSelf = new Color(0.09f, 0.22f, 0.28f, 0.92f);

    private static readonly Color ColorBorderPlayerTeam = new Color(0.28f, 0.62f, 1f, 1f);
    private static readonly Color ColorBgPlayerTeam = new Color(0.11f, 0.17f, 0.28f, 0.95f);

    private static readonly Color ColorBorderEnemyTeam = new Color(0.95f, 0.30f, 0.28f, 1f);
    private static readonly Color ColorBgEnemyTeam = new Color(0.25f, 0.11f, 0.13f, 0.95f);

    private static readonly Color ColorBorderGhost = new Color(0.45f, 0.95f, 1f, 0.95f);
    private static readonly Color ColorBgGhost = new Color(0.10f, 0.26f, 0.34f, 0.82f);

    private static readonly Color ColorPortraitPlayer = new Color(0.45f, 0.82f, 1f, 0.92f);
    private static readonly Color ColorPortraitEnemy = new Color(1f, 0.48f, 0.45f, 0.92f);
    private static readonly Color ColorPortraitPredicted = new Color(0.45f, 0.95f, 1f, 0.92f);

    private static Sprite _playerEmblemSprite;
    private static Sprite _enemyEmblemSprite;

    [Header("Slot UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameLabelText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private GameObject stunBadgeObj;
    [SerializeField] private GameObject intentBadgeObj;
    [SerializeField] private Image intentIconImage;
    [SerializeField] private TMP_Text intentValueText;
    [SerializeField] private GameObject highlightFrameObj;
    [SerializeField] private GameObject ghostBadgeObj;

    private RectTransform _rectTransform;
    private TimelineEntry _boundEntry;
    private bool _isGhost;
    private float _baseAlpha = 1f;
    private Color _baseBorderColor = ColorBorderPlayerTeam;
    private bool _isHighlighted;
    private bool _isPointerHovering;
    private bool _hasShownTooltip;

    // 툴팁 문자열 재사용 버퍼
    private readonly StringBuilder _tooltipBuilder = new StringBuilder(192);

    // 시간 문자열 불필요 할당 방지 캐시
    private int _cachedTenths = int.MinValue;
    private bool _cachedCurrentTurn;
    private bool _cachedStunned;
    private bool _cachedInfinite;

    public TimelineEntry BoundEntry => _boundEntry;
    public Character BoundCharacter => _boundEntry.character;
    public bool IsGhost => _isGhost;

    public RectTransform CachedRectTransform
    {
        get
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }
            return _rectTransform;
        }
    }

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (highlightFrameObj != null)
        {
            highlightFrameObj.SetActive(false);
        }
        if (stunBadgeObj != null)
        {
            stunBadgeObj.SetActive(false);
        }
        if (intentBadgeObj != null)
        {
            intentBadgeObj.SetActive(false);
        }
        if (ghostBadgeObj != null)
        {
            ghostBadgeObj.SetActive(false);
        }
    }

    private void OnDisable()
    {
        ClearHoverVisuals();
    }

    /// <summary>
    /// 타임라인 엔트리 데이터를 슬롯 뷰에 바인딩합니다.
    /// </summary>
    public void Bind(in TimelineEntry entry, bool isGhost = false)
    {
        _boundEntry = entry;
        _isGhost = isGhost;

        // 1. 상태별 알파·스케일·레이캐스트·배지 설정
        if (isGhost)
        {
            _baseAlpha = 0.55f;
            transform.localScale = new Vector3(0.95f, 0.95f, 1f);
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
            }
            if (ghostBadgeObj != null)
            {
                ghostBadgeObj.SetActive(true);
            }
        }
        else if (entry.isCurrentTurn)
        {
            _baseAlpha = 1f;
            transform.localScale = new Vector3(1.12f, 1.12f, 1f);
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
            }
            if (ghostBadgeObj != null)
            {
                ghostBadgeObj.SetActive(false);
            }
        }
        else if (entry.isPredictedNextTurn)
        {
            _baseAlpha = 0.65f;
            transform.localScale = Vector3.one;
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
            }
            if (ghostBadgeObj != null)
            {
                ghostBadgeObj.SetActive(false);
            }
        }
        else
        {
            _baseAlpha = 1f;
            transform.localScale = Vector3.one;
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
            }
            if (ghostBadgeObj != null)
            {
                ghostBadgeObj.SetActive(false);
            }
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = _baseAlpha;
        }

        // 2. 테두리 및 배경 색상 지정
        Color borderColor;
        Color bgColor;
        if (isGhost)
        {
            borderColor = ColorBorderGhost;
            bgColor = ColorBgGhost;
        }
        else if (entry.isCurrentTurn)
        {
            borderColor = ColorBorderCurrentTurn;
            bgColor = ColorBgCurrentTurn;
        }
        else if (entry.isPredictedNextTurn)
        {
            borderColor = ColorBorderPredictedSelf;
            bgColor = ColorBgPredictedSelf;
        }
        else if (entry.isPlayerTeam)
        {
            borderColor = ColorBorderPlayerTeam;
            bgColor = ColorBgPlayerTeam;
        }
        else
        {
            borderColor = ColorBorderEnemyTeam;
            bgColor = ColorBgEnemyTeam;
        }

        _baseBorderColor = borderColor;
        if (borderImage != null)
        {
            borderImage.color = borderColor;
        }
        if (backgroundImage != null)
        {
            backgroundImage.color = bgColor;
        }

        // 3. 유닛 이름 및 초상화
        string unitName = ResolveDisplayName(in entry);
        if (nameLabelText != null)
        {
            nameLabelText.text = entry.isPredictedNextTurn ? "내 다음" : unitName;
        }

        ApplyPortraitVisual(in entry);

        // 4. 기절 배지 및 시간 텍스트
        if (stunBadgeObj != null)
        {
            stunBadgeObj.SetActive(entry.isStunned);
        }

        UpdateTimeText(in entry);

        // 5. 적 행동 예고 소형 연동 ([17])
        UpdateEnemyIntentBadge(in entry);
    }

    /// <summary>
    /// 하이라이트 외곽 프레임 표시 여부를 설정합니다.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        _isHighlighted = highlighted;
        if (highlightFrameObj != null)
        {
            highlightFrameObj.SetActive(highlighted);
        }
    }

    /// <summary>
    /// 턴 종료 버튼 호버 점멸 등 외부 펄스 알파를 적용합니다.
    /// </summary>
    public void SetPulseAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }

    /// <summary>
    /// 펄스 종료 후 슬롯 기본 알파로 복원합니다.
    /// </summary>
    public void RestoreBaseAlpha()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = _baseAlpha;
        }
    }

    /// <summary>
    /// 턴 종료 호버 등 외부 강조 색상을 테두리에 임시 적용하거나 기본 테두리 색으로 복원합니다.
    /// </summary>
    public void SetBorderHighlight(bool useOverride, Color overrideColor)
    {
        if (borderImage != null)
        {
            borderImage.color = useOverride ? overrideColor : _baseBorderColor;
        }
    }

    /// <summary>
    /// 풀 반납 전 호버 상태와 시각적 강조를 초기화합니다.
    /// </summary>
    public void ResetForPool()
    {
        ClearHoverVisuals();
        SetHighlighted(false);
        _boundEntry = default;
        _isGhost = false;
        _baseAlpha = 1f;
        _cachedTenths = int.MinValue;
        transform.localScale = Vector3.one;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
        if (stunBadgeObj != null)
        {
            stunBadgeObj.SetActive(false);
        }
        if (intentBadgeObj != null)
        {
            intentBadgeObj.SetActive(false);
        }
        if (ghostBadgeObj != null)
        {
            ghostBadgeObj.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isGhost || _boundEntry.character == null)
        {
            return;
        }

        _isPointerHovering = true;
        SetHighlighted(true);

        if (BattleTimelinePanel.Instance != null)
        {
            BattleTimelinePanel.Instance.NotifySlotPointerEnter(this);
        }

        ShowSlotTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHoverVisuals();
    }

    private void ClearHoverVisuals()
    {
        bool wasHovering = _isPointerHovering;
        _isPointerHovering = false;

        if (wasHovering && BattleTimelinePanel.Instance != null)
        {
            BattleTimelinePanel.Instance.NotifySlotPointerExit(this);
        }

        if (_isHighlighted)
        {
            SetHighlighted(false);
        }

        if (_hasShownTooltip)
        {
            _hasShownTooltip = false;
            if (TooltipManager.HasInstance && TooltipManager.Instance.gameObject.activeSelf)
            {
                TooltipManager.Instance.HideTooltip();
            }
        }
    }

    private void ShowSlotTooltip()
    {
        if (TooltipManager.Instance == null || _boundEntry.character == null)
        {
            return;
        }

        string unitName = ResolveDisplayName(in _boundEntry);
        string header;
        if (_boundEntry.isCurrentTurn)
        {
            header = $"{unitName} (현재 턴)";
        }
        else if (_boundEntry.isPredictedNextTurn)
        {
            header = $"{unitName} (내 다음 차례)";
        }
        else
        {
            header = unitName;
        }

        _tooltipBuilder.Clear();

        // 1. 예상 도달 시간
        if (_boundEntry.isCurrentTurn)
        {
            _tooltipBuilder.Append("도달 시간: 현재 행동 중 (0.0s)");
        }
        else if (float.IsInfinity(_boundEntry.timeUntilTurn) || float.IsNaN(_boundEntry.timeUntilTurn))
        {
            _tooltipBuilder.Append("예상 도달 시간: 도달 불가 (∞)");
        }
        else
        {
            _tooltipBuilder.Append("예상 도달 시간: ")
                .Append(_boundEntry.timeUntilTurn.ToString("0.0"))
                .Append('s');
        }

        // 2. 현재 또는 예상 이월 스태미나 수치 및 %
        Character unit = _boundEntry.character;
        float maxStamina = ResolveEffectiveMaxStamina(unit, _boundEntry.isPredictedNextTurn);
        float staminaVal = _boundEntry.isPredictedNextTurn ? _boundEntry.predictedStartStamina : unit.curStamina;
        int pct = Mathf.RoundToInt((staminaVal / Mathf.Max(1f, maxStamina)) * 100f);

        _tooltipBuilder.Append('\n');
        if (_boundEntry.isPredictedNextTurn)
        {
            _tooltipBuilder.Append("예상 이월 스태미나: ");
        }
        else
        {
            _tooltipBuilder.Append("현재 스태미나: ");
        }

        _tooltipBuilder.Append(staminaVal.ToString("0.#"))
            .Append(" / ")
            .Append(maxStamina.ToString("0.#"))
            .Append(" (")
            .Append(pct)
            .Append("%)");

        if (_boundEntry.overflowStamina > 0.05f)
        {
            _tooltipBuilder.Append(" (+")
                .Append(_boundEntry.overflowStamina.ToString("0.#"))
                .Append(" 초과)");
        }

        // 3. 기절 여부
        _tooltipBuilder.Append('\n');
        if (_boundEntry.isStunned)
        {
            _tooltipBuilder.Append("기절 여부: <color=#FF6B6B>기절</color> (다음 턴 행동 불가)");
        }
        else
        {
            _tooltipBuilder.Append("기절 여부: 없음");
        }

        // 4. 적 행동 예고 상세 정보 ([17])
        if (!_boundEntry.isPlayerTeam && !_boundEntry.isPredictedNextTurn)
        {
            EnemyIntentInfo intent = unit.CurrentIntent;
            if (intent.isValid && intent.intentType != EnemyIntentType.None)
            {
                string intentLabel = IntentIconProvider.GetIntentLabel(intent.intentType);
                string cardName = string.Empty;
                if (intent.plannedCard != null && intent.plannedCard.cardData != null)
                {
                    cardName = intent.plannedCard.cardData.LocalizedName;
                }
                else if (!string.IsNullOrEmpty(intent.cardId))
                {
                    cardName = intent.cardId;
                }

                _tooltipBuilder.Append("\n예고 행동: [").Append(intentLabel).Append(']');
                if (!string.IsNullOrEmpty(cardName))
                {
                    _tooltipBuilder.Append(' ').Append(cardName);
                }

                if (intent.estimatedValue > 0)
                {
                    _tooltipBuilder.Append(" (예상 수치: ").Append(intent.estimatedValue).Append(')');
                }
            }
        }

        TooltipManager.Instance.ShowTooltip(header, _tooltipBuilder.ToString(), CachedRectTransform);
        _hasShownTooltip = true;
    }

    private void UpdateTimeText(in TimelineEntry entry)
    {
        if (timeText == null) return;

        bool isInf = float.IsInfinity(entry.timeUntilTurn) || float.IsNaN(entry.timeUntilTurn);
        int tenths = isInf ? -1 : Mathf.Max(0, Mathf.RoundToInt(entry.timeUntilTurn * 10f));

        if (_cachedTenths == tenths &&
            _cachedCurrentTurn == entry.isCurrentTurn &&
            _cachedStunned == entry.isStunned &&
            _cachedInfinite == isInf)
        {
            return;
        }

        _cachedTenths = tenths;
        _cachedCurrentTurn = entry.isCurrentTurn;
        _cachedStunned = entry.isStunned;
        _cachedInfinite = isInf;

        if (entry.isCurrentTurn)
        {
            timeText.text = entry.isStunned ? "NOW 기절" : "NOW";
        }
        else if (isInf)
        {
            timeText.text = entry.isStunned ? "기절" : "∞";
        }
        else
        {
            float displaySec = tenths * 0.1f;
            timeText.text = entry.isStunned ? $"{displaySec:0.0}s 기절" : $"{displaySec:0.0}s";
        }
    }

    private void UpdateEnemyIntentBadge(in TimelineEntry entry)
    {
        if (intentBadgeObj == null) return;

        if (!entry.isPlayerTeam && !entry.isPredictedNextTurn && entry.character != null)
        {
            EnemyIntentInfo intent = entry.character.CurrentIntent;
            if (intent.isValid && intent.intentType != EnemyIntentType.None)
            {
                intentBadgeObj.SetActive(true);

                if (intentIconImage != null)
                {
                    Sprite iconSprite = IntentIconProvider.GetIntentIcon(intent.intentType);
                    intentIconImage.sprite = iconSprite;
                    intentIconImage.color = IntentIconProvider.GetIntentColor(intent.intentType);
                    intentIconImage.enabled = (iconSprite != null);
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

    private void ApplyPortraitVisual(in TimelineEntry entry)
    {
        if (portraitImage == null) return;

        Sprite resolvedPortrait = null;
        if (AssetCacheManager.instance != null)
        {
            if (entry.controller is NPCUnitController npc && npc.unitData != null && !string.IsNullOrEmpty(npc.unitData.unitId))
            {
                if (!AssetCacheManager.instance.TryGetTexture("Portrait_" + npc.unitData.unitId, out resolvedPortrait))
                {
                    AssetCacheManager.instance.TryGetTexture(npc.unitData.unitId, out resolvedPortrait);
                }
            }
            else if (entry.character != null && entry.character.faction == CharacterFaction.Player)
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
            portraitImage.sprite = GetOrCreateFallbackEmblem(entry.isPlayerTeam);
            if (entry.isPredictedNextTurn)
            {
                portraitImage.color = ColorPortraitPredicted;
            }
            else
            {
                portraitImage.color = entry.isPlayerTeam ? ColorPortraitPlayer : ColorPortraitEnemy;
            }
        }
    }

    private static string ResolveDisplayName(in TimelineEntry entry)
    {
        if (entry.controller is NPCUnitController npc && npc.unitData != null)
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

        if (entry.character != null)
        {
            if (entry.character.faction == CharacterFaction.Player)
            {
                return "플레이어";
            }

            string rawName = entry.character.name;
            if (!string.IsNullOrEmpty(rawName))
            {
                return rawName.Replace("(Clone)", string.Empty).Trim();
            }
        }

        return "유닛";
    }

    private static float ResolveEffectiveMaxStamina(Character unit, bool excludeUntilTurnEnd)
    {
        if (unit == null) return 100f;

        if (excludeUntilTurnEnd && unit.baseCharacterStat != null)
        {
            return StatModifierEvaluator.EvaluateStat(
                unit.baseCharacterStat.maxStamina,
                StatModifierTarget.MaxStamina,
                unit.StatModifiers,
                atFutureTime: 0f,
                excludeUntilTurnEnd: true,
                minClamp: 1f);
        }

        if (unit.curCharacterStat != null && unit.curCharacterStat.maxStamina > 0f)
        {
            return unit.curCharacterStat.maxStamina;
        }

        return 100f;
    }

    private static Sprite GetOrCreateFallbackEmblem(bool isPlayerTeam)
    {
        if (isPlayerTeam)
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
                float dy = y - 15.5f;
                if (isPlayer)
                {
                    // 아군 방패/투구 문양 실루엣
                    bool head = (dx * dx + (y - 20f) * (y - 20f)) <= 34f;
                    bool shoulders = y >= 5 && y <= 14 && Mathf.Abs(dx) <= (9.5f - (14f - y) * 0.2f);
                    mask[y * FallbackEmblemSize + x] = head || shoulders;
                }
                else
                {
                    // 적군 해골/뿔 문양 실루엣
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
            name = isPlayer ? "TimelineEmblem_Player" : "TimelineEmblem_Enemy"
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
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = x + ox;
                int ny = y + oy;
                if (nx >= 0 && nx < FallbackEmblemSize && ny >= 0 && ny < FallbackEmblemSize)
                {
                    if (mask[ny * FallbackEmblemSize + nx])
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
