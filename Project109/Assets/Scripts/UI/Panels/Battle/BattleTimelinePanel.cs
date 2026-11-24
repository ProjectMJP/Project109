using System;
using System.Collections.Generic;
using CardTypes;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 전투 화면 상단 중앙에 살아있는 전투원의 다음 행동 순서와 플레이어 다음 차례를 표시하고,
/// 턴 종료·카드·타깃팅 호버 미리보기 및 월드 유닛 상호 하이라이트를 제공하는 상시 HUD 패널입니다.
/// </summary>
public class BattleTimelinePanel : UIPanelBase
{
    public static BattleTimelinePanel Instance { get; private set; }

    public event Action<Character> OnSlotClicked;

    [Header("Timeline Layout Settings")]
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private BattleTimelineSlotUI slotPrefab;
    [SerializeField] private float slotSpacing = 74f;
    [SerializeField] private float slideDuration = 0.22f;
    [SerializeField] private Color predictedSlotHighlightColor = new Color(0.25f, 0.95f, 1f, 1f);

    private sealed class SlotInstance
    {
        public BattleTimelineSlotUI ui;
        public RectTransform rect;
        public MotionHandle moveHandle;
        public Character boundCharacter;
        public bool isPredictedNextTurn;
        public float targetX;
    }

    // GC 무할당 타임라인 계산 및 슬롯 풀링 컬렉션
    private readonly List<TimelineEntry> _baseEntries = new List<TimelineEntry>(16);
    private readonly List<TimelineEntry> _previewEntries = new List<TimelineEntry>(16);
    private readonly List<SlotInstance> _activeSlots = new List<SlotInstance>(16);
    private readonly List<SlotInstance> _nextActiveSlots = new List<SlotInstance>(16);
    private readonly List<SlotInstance> _slotPool = new List<SlotInstance>(16);
    private readonly List<Character> _subscribedCharacters = new List<Character>(16);
    private readonly List<Character> _desiredCharacters = new List<Character>(16);
    private readonly HashSet<Vector2Int> _aoeCoordScratch = new HashSet<Vector2Int>();
    private readonly List<Character> _targetingHighlightedCharacters = new List<Character>(8);
    private readonly List<Character> _lastTargetingWorldChars = new List<Character>(8);
    private readonly List<Character> _targetingScratch = new List<Character>(8);
    private readonly List<Character> _singleTargetingScratch = new List<Character>(1);

    private BattleManager _battleManager;
    private bool _isEventsSubscribed;
    private bool _isDirty;

    // 호버 및 미리보기 상태
    private bool _isEndTurnHovered;
    private Card _hoveredCard;
    private Card _targetingCard;
    private Character _targetingHoveredCharacter;
    private Character _worldHoveredCharacter;
    private Character _externalHoveredCharacter;
    private Character _lastFocusedWorldChar;
    private BattleTimelineSlotUI _uiHoveredSlot;
    private TimelinePreviewRequest _currentPreview = TimelinePreviewRequest.None;

    private BattleTimelineSlotUI _ghostSlotUI;
    private BattleTimelineSlotUI _secondaryGhostSlotUI;

    // Update 핫패스 LayerMask 및 레이캐스트 Collider→Component 캐시
    private int _characterLayerMask;
    private int _tileLayerMask;
    private Collider _cachedCharHitCollider;
    private Character _cachedCharHitResult;
    private Collider _cachedTileHitCollider;
    private Tile _cachedTileHitResult;

    public override bool DestroyOnClose => false;
    public override bool CanCloseByCancel => false;

    private void Awake()
    {
        uiLayerType = UILayerType.Top;
        blockWorldInput = false;

        if (Instance == null || Instance == this)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _characterLayerMask = LayerMask.GetMask("Player", "Enemy");
        _tileLayerMask = LayerMask.GetMask("Tile");

        if (slotContainer == null)
        {
            Debug.LogError("[BattleTimelinePanel] slotContainer가 바인딩되지 않았습니다! BattleTimelinePanel 프리팹 설정을 확인하세요.", this);
        }
        if (slotPrefab == null)
        {
            Debug.LogError("[BattleTimelinePanel] slotPrefab이 바인딩되지 않았습니다! BattleTimelinePanel 프리팹 설정을 확인하세요.", this);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_battleManager != null)
        {
            SubscribeBattleEvents();
            MarkDirty();
        }
    }

    protected override void OnDisable()
    {
        ClearAllHoverState();
        CancelAllSlotMotions();
        UnsubscribeBattleEvents();
        base.OnDisable();
    }

    private void OnDestroy()
    {
        ClearAllHoverState();
        CancelAllSlotMotions();
        UnsubscribeBattleEvents();
        OnSlotClicked = null;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (_battleManager != null)
        {
            SubscribeBattleEvents();
            MarkDirty();
        }
    }

    public override void OnClose()
    {
        ClearAllHoverState();
        ReturnAllSlotsToPool();
        UnsubscribeBattleEvents();
        base.OnClose();
    }

    /// <summary>
    /// 현재 전투의 BattleManager를 바인딩하고 타임라인 갱신 이벤트를 구독합니다.
    /// </summary>
    public void BindBattle(BattleManager bm)
    {
        if (ReferenceEquals(_battleManager, bm))
        {
            if (_battleManager != null && isActiveAndEnabled)
            {
                SubscribeBattleEvents();
                MarkDirty();
            }
            return;
        }

        ClearAllHoverState();
        UnsubscribeBattleEvents();
        _battleManager = bm;

        if (_battleManager != null && isActiveAndEnabled)
        {
            SubscribeBattleEvents();
            MarkDirty();
        }
        else if (_battleManager == null)
        {
            _baseEntries.Clear();
            ReturnAllSlotsToPool();
        }
    }

    /// <summary>
    /// 다음 LateUpdate에서 타임라인을 재계산하도록 Dirty 플래그를 세웁니다.
    /// </summary>
    public void MarkDirty()
    {
        _isDirty = true;
    }

    /// <summary>
    /// 턴 종료 버튼 호버 시 "내 다음 차례" 슬롯 강조 및 알파 점멸 미리보기를 토글합니다.
    /// </summary>
    public void SetEndTurnHoverPreview(bool isHovered)
    {
        if (_isEndTurnHovered == isHovered) return;
        _isEndTurnHovered = isHovered;

        if (!isHovered)
        {
            for (int i = 0; i < _activeSlots.Count; i++)
            {
                if (_activeSlots[i].isPredictedNextTurn && _activeSlots[i].ui != null)
                {
                    _activeSlots[i].ui.RestoreBaseAlpha();
                }
            }
        }

        RefreshSlotHighlights();
    }

    /// <summary>
    /// 전투 손패 카드 호버 시 예상 소모 스태미나 및 대상 효과를 고스트 슬롯으로 미리보기합니다.
    /// </summary>
    public void SetCardHoverPreview(Card card)
    {
        _hoveredCard = card;
        RefreshGhostPreview();
    }

    /// <summary>
    /// 손패 카드 호버 이탈 시 카드 미리보기 고스트 슬롯을 정리합니다.
    /// </summary>
    public void ClearCardHoverPreview()
    {
        if (_hoveredCard == null) return;
        _hoveredCard = null;
        RefreshGhostPreview();
    }

    /// <summary>
    /// 타깃팅 모드 중 마우스가 가리키는 단일 대상 캐릭터를 타임라인 미리보기에 반영합니다.
    /// </summary>
    public void SetTargetingHoverCharacter(Character target, Card targetingCard = null)
    {
        _singleTargetingScratch.Clear();
        if (target != null && !target.isDead)
        {
            _singleTargetingScratch.Add(target);
        }
        SetTargetingHoverCharacters(_singleTargetingScratch, targetingCard);
        _singleTargetingScratch.Clear();
    }

    /// <summary>
    /// 타깃팅 모드 중 피격·적용 예정 다중 대상 목록을 타임라인·사이드 HUD·월드 캐릭터 하이라이트에 동시 반영합니다. (GC 무할당)
    /// </summary>
    public void SetTargetingHoverCharacters(IReadOnlyList<Character> targets, Card targetingCard = null)
    {
        bool cardChanged = targetingCard != null && !ReferenceEquals(_targetingCard, targetingCard);
        if (targetingCard != null)
        {
            _targetingCard = targetingCard;
        }

        _targetingScratch.Clear();
        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Character c = targets[i];
                if (c != null && !c.isDead && !ContainsCharacter(_targetingScratch, c))
                {
                    _targetingScratch.Add(c);
                }
            }
        }

        bool targetsChanged = !AreCharacterListsEquivalent(_targetingHighlightedCharacters, _targetingScratch);
        if (!cardChanged && !targetsChanged)
        {
            _targetingScratch.Clear();
            return;
        }

        _targetingHighlightedCharacters.Clear();
        for (int i = 0; i < _targetingScratch.Count; i++)
        {
            _targetingHighlightedCharacters.Add(_targetingScratch[i]);
        }
        _targetingScratch.Clear();

        _targetingHoveredCharacter = _targetingHighlightedCharacters.Count > 0 ? _targetingHighlightedCharacters[0] : null;
        RefreshSlotHighlights();
        RefreshGhostPreview();
    }

    /// <summary>
    /// 타임라인 슬롯 좌클릭 시 클릭 이벤트를 발행하고 일반 전투 모드에서는 플레이어를 해당 유닛 방향으로 회전시킵니다.
    /// </summary>
    public void NotifySlotClicked(BattleTimelineSlotUI slot)
    {
        if (slot == null || slot.IsGhost) return;

        Character clickedChar = slot.BoundCharacter;
        if (clickedChar == null) return;

        OnSlotClicked?.Invoke(clickedChar);

        if (RunManager.instance != null && RunManager.instance.playerBattleController != null)
        {
            RunManager.instance.playerBattleController.TryRotatePlayerTowardCharacter(clickedChar);
        }
    }

    /// <summary>
    /// 타깃팅 모드 종료 또는 취소 시 타깃팅 미리보기 상태를 초기화합니다.
    /// </summary>
    public void ClearTargetingHoverPreview()
    {
        if (_targetingCard == null
            && _targetingHoveredCharacter == null
            && _targetingHighlightedCharacters.Count == 0
            && _lastTargetingWorldChars.Count == 0)
        {
            return;
        }

        _targetingCard = null;
        _targetingHoveredCharacter = null;
        _targetingHighlightedCharacters.Clear();
        RefreshSlotHighlights();
        RefreshGhostPreview();
    }

    /// <summary>
    /// 사이드 유닛 HUD 등 외부 패널에서 호버된 캐릭터를 타임라인 및 월드 하이라이트에 동기화합니다.
    /// </summary>
    public void SetExternalHoverCharacter(Character target)
    {
        if (_externalHoveredCharacter == target) return;
        _externalHoveredCharacter = target;
        RefreshSlotHighlights();
    }

    /// <summary>
    /// 타임라인 슬롯 자체에 포인터가 진입했을 때 호버 하이라이트 상태를 동기화합니다.
    /// </summary>
    public void NotifySlotPointerEnter(BattleTimelineSlotUI slot)
    {
        _uiHoveredSlot = slot;
        RefreshSlotHighlights();
    }

    /// <summary>
    /// 타임라인 슬롯 자체에서 포인터가 이탈했을 때 호버 하이라이트 상태를 동기화합니다.
    /// </summary>
    public void NotifySlotPointerExit(BattleTimelineSlotUI slot)
    {
        if (_uiHoveredSlot == slot)
        {
            _uiHoveredSlot = null;
            RefreshSlotHighlights();
        }
    }

    private void Update()
    {
        // 1. 턴 종료 버튼 호버 시 예측 슬롯 사인파 점멸 (0.5f <-> 1.0f)
        if (_isEndTurnHovered)
        {
            float pulse = Mathf.Lerp(0.5f, 1.0f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            for (int i = 0; i < _activeSlots.Count; i++)
            {
                if (_activeSlots[i].isPredictedNextTurn && _activeSlots[i].ui != null)
                {
                    _activeSlots[i].ui.SetPulseAlpha(pulse);
                }
            }
        }

        // 2. 월드 유닛 마우스 오버 감지 -> 매칭 타임라인 슬롯 하이라이트
        Character detectedWorldChar = DetectHoveredWorldCharacter();
        if (detectedWorldChar != _worldHoveredCharacter)
        {
            _worldHoveredCharacter = detectedWorldChar;
            RefreshSlotHighlights();
        }
    }

    private void LateUpdate()
    {
        if (_isDirty && _battleManager != null)
        {
            _isDirty = false;
            RebuildTimeline();
        }
    }

    private Character DetectHoveredWorldCharacter()
    {
        if (_battleManager == null)
        {
            return null;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            return null;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return null;
        }

        Vector2 screenPos;
        if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
        }
        else if (Pointer.current != null)
        {
            screenPos = Pointer.current.position.ReadValue();
        }
        else
        {
            return null;
        }

        Ray ray = mainCam.ScreenPointToRay(screenPos);

        if (_characterLayerMask != 0 && Physics.Raycast(ray, out RaycastHit charHit, 10000f, _characterLayerMask, QueryTriggerInteraction.Collide))
        {
            if (charHit.collider != _cachedCharHitCollider)
            {
                _cachedCharHitCollider = charHit.collider;
                _cachedCharHitResult = charHit.collider != null ? charHit.collider.GetComponentInParent<Character>() : null;
            }

            if (_cachedCharHitResult != null && !_cachedCharHitResult.isDead)
            {
                return _cachedCharHitResult;
            }
        }

        if (_tileLayerMask != 0 && Physics.Raycast(ray, out RaycastHit tileHit, 10000f, _tileLayerMask, QueryTriggerInteraction.Collide))
        {
            if (tileHit.collider != _cachedTileHitCollider)
            {
                _cachedTileHitCollider = tileHit.collider;
                _cachedTileHitResult = tileHit.collider != null ? tileHit.collider.GetComponentInParent<Tile>() : null;
            }

            if (_cachedTileHitResult != null && _cachedTileHitResult.occupyingCharacter != null && !_cachedTileHitResult.occupyingCharacter.isDead)
            {
                return _cachedTileHitResult.occupyingCharacter;
            }
        }

        return null;
    }

    private void RebuildTimeline()
    {
        if (_battleManager == null || slotContainer == null || slotPrefab == null)
        {
            return;
        }

        _battleManager.PredictTimeline(_baseEntries);

        int count = _baseEntries.Count;
        float effectiveSpacing = ComputeEffectiveSpacing(count);
        float startX = count > 0 ? -0.5f * (count - 1) * effectiveSpacing : 0f;
        float duration = slideDuration / Mathf.Max(0.1f, _battleManager.battleTimeScale);

        _nextActiveSlots.Clear();

        for (int i = 0; i < count; i++)
        {
            TimelineEntry entry = _baseEntries[i];
            float targetX = startX + i * effectiveSpacing;

            SlotInstance slot = TakeMatchingActiveSlot(entry.character, entry.isPredictedNextTurn);
            bool isNewlyPlaced = false;
            if (slot == null)
            {
                slot = AcquireSlotFromPool();
                isNewlyPlaced = true;
            }

            if (slot == null) continue;

            slot.boundCharacter = entry.character;
            slot.isPredictedNextTurn = entry.isPredictedNextTurn;
            slot.ui.Bind(in entry, isGhost: false);

            // 턴 종료 호버 중이면 예측 슬롯의 기본 알파가 덮어씌워지지 않도록 즉시 동기화
            if (_isEndTurnHovered && entry.isPredictedNextTurn)
            {
                float pulse = Mathf.Lerp(0.5f, 1.0f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
                slot.ui.SetPulseAlpha(pulse);
            }

            if (isNewlyPlaced || duration <= 0f || !isActiveAndEnabled)
            {
                CancelSlotMotion(slot);
                slot.rect.anchoredPosition = new Vector2(targetX, 0f);
                slot.targetX = targetX;
            }
            else if (Mathf.Abs(slot.targetX - targetX) > 0.5f)
            {
                CancelSlotMotion(slot);
                slot.targetX = targetX;
                slot.moveHandle = LMotion.Create(slot.rect.anchoredPosition, new Vector2(targetX, 0f), duration)
                    .WithEase(Ease.OutCubic)
                    .BindToAnchoredPosition(slot.rect);
            }

            slot.rect.SetSiblingIndex(i);
            _nextActiveSlots.Add(slot);
        }

        // 더 이상 타임라인에 존재하지 않는 기존 슬롯 풀 반납
        for (int i = 0; i < _activeSlots.Count; i++)
        {
            ReturnSlotToPool(_activeSlots[i]);
        }
        _activeSlots.Clear();
        _activeSlots.AddRange(_nextActiveSlots);
        _nextActiveSlots.Clear();

        if (_ghostSlotUI != null)
        {
            _ghostSlotUI.transform.SetAsLastSibling();
        }
        if (_secondaryGhostSlotUI != null)
        {
            _secondaryGhostSlotUI.transform.SetAsLastSibling();
        }

        RefreshSlotHighlights();
        RefreshGhostPreview();
    }

    private float ComputeEffectiveSpacing(int count)
    {
        if (count <= 1) return slotSpacing;

        float containerWidth = slotContainer != null && slotContainer.rect.width > 0f
            ? slotContainer.rect.width - 16f
            : 720f;
        float maxSpan = Mathf.Max(64f, containerWidth - 64f);
        float requiredSpan = (count - 1) * slotSpacing;
        if (requiredSpan > maxSpan)
        {
            return maxSpan / (count - 1);
        }
        return slotSpacing;
    }

    private float GetSlotPositionX(int index, int totalCount)
    {
        if (totalCount <= 0) return 0f;
        float spacing = ComputeEffectiveSpacing(totalCount);
        float startX = -0.5f * (totalCount - 1) * spacing;
        return startX + index * spacing;
    }

    private SlotInstance TakeMatchingActiveSlot(Character character, bool isPredictedNextTurn)
    {
        for (int i = 0; i < _activeSlots.Count; i++)
        {
            SlotInstance candidate = _activeSlots[i];
            if (candidate.boundCharacter == character && candidate.isPredictedNextTurn == isPredictedNextTurn)
            {
                _activeSlots.RemoveAt(i);
                return candidate;
            }
        }
        return null;
    }

    private SlotInstance AcquireSlotFromPool()
    {
        if (_slotPool.Count > 0)
        {
            int last = _slotPool.Count - 1;
            SlotInstance pooled = _slotPool[last];
            _slotPool.RemoveAt(last);
            pooled.ui.gameObject.SetActive(true);
            return pooled;
        }

        if (slotPrefab == null || slotContainer == null)
        {
            return null;
        }

        BattleTimelineSlotUI ui = Instantiate(slotPrefab, slotContainer, false);
        ui.gameObject.SetActive(true);
        return new SlotInstance
        {
            ui = ui,
            rect = ui.CachedRectTransform,
            moveHandle = MotionHandle.None
        };
    }

    private void ReturnSlotToPool(SlotInstance slot)
    {
        if (slot == null) return;
        CancelSlotMotion(slot);
        slot.boundCharacter = null;
        slot.isPredictedNextTurn = false;
        slot.targetX = 0f;
        if (slot.ui != null)
        {
            slot.ui.ResetForPool();
            slot.ui.gameObject.SetActive(false);
        }
        _slotPool.Add(slot);
    }

    private void ReturnAllSlotsToPool()
    {
        for (int i = 0; i < _activeSlots.Count; i++)
        {
            ReturnSlotToPool(_activeSlots[i]);
        }
        _activeSlots.Clear();
    }

    private void CancelAllSlotMotions()
    {
        for (int i = 0; i < _activeSlots.Count; i++)
        {
            CancelSlotMotion(_activeSlots[i]);
        }
    }

    private static void CancelSlotMotion(SlotInstance slot)
    {
        if (slot != null && slot.moveHandle.IsActive())
        {
            slot.moveHandle.TryCancel();
        }
        if (slot != null)
        {
            slot.moveHandle = MotionHandle.None;
        }
    }

    private void RefreshSlotHighlights()
    {
        for (int i = 0; i < _activeSlots.Count; i++)
        {
            SlotInstance slot = _activeSlots[i];
            if (slot.ui == null) continue;

            bool isEndTurnPredicted = _isEndTurnHovered && slot.isPredictedNextTurn;
            bool isTargetingTarget = slot.boundCharacter != null && ContainsCharacter(_targetingHighlightedCharacters, slot.boundCharacter);
            bool shouldHighlight = (slot.ui == _uiHoveredSlot)
                || (_worldHoveredCharacter != null && slot.boundCharacter == _worldHoveredCharacter)
                || (_externalHoveredCharacter != null && slot.boundCharacter == _externalHoveredCharacter)
                || isTargetingTarget
                || isEndTurnPredicted;

            slot.ui.SetHighlighted(shouldHighlight);

            if (slot.ui != null)
            {
                slot.ui.SetBorderHighlight(isEndTurnPredicted, predictedSlotHighlightColor);
                if (isEndTurnPredicted)
                {
                    // 턴 종료 호버 강조 알파 즉시 반영
                    slot.ui.SetPulseAlpha(Mathf.Lerp(0.5f, 1.0f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)));
                }
            }
        }

        // 단일 포커스 캐릭터 산출 및 월드 UI 최상단 포커스(SetAsLastSibling) 동기화
        Character focusedChar = _uiHoveredSlot != null
            ? _uiHoveredSlot.BoundCharacter
            : (_externalHoveredCharacter != null
                ? _externalHoveredCharacter
                : (_worldHoveredCharacter != null ? _worldHoveredCharacter : _targetingHoveredCharacter));

        if (_lastFocusedWorldChar != focusedChar)
        {
            if (_lastFocusedWorldChar != null)
            {
                CharacterUIController.SetTimelineHighlightFor(_lastFocusedWorldChar, false);
            }
            if (focusedChar != null)
            {
                CharacterUIController.SetTimelineHighlightFor(focusedChar, true);
            }
            _lastFocusedWorldChar = focusedChar;
        }

        // 다중 타깃팅 대상 월드 캐릭터 하이라이트 차분 동기화
        for (int i = _lastTargetingWorldChars.Count - 1; i >= 0; i--)
        {
            Character prev = _lastTargetingWorldChars[i];
            if (prev == null || !ContainsCharacter(_targetingHighlightedCharacters, prev))
            {
                if (prev != null)
                {
                    CharacterUIController.SetTargetingHighlightFor(prev, false);
                }
                _lastTargetingWorldChars.RemoveAt(i);
            }
        }

        for (int i = 0; i < _targetingHighlightedCharacters.Count; i++)
        {
            Character target = _targetingHighlightedCharacters[i];
            if (target != null && !target.isDead && !ContainsCharacter(_lastTargetingWorldChars, target))
            {
                CharacterUIController.SetTargetingHighlightFor(target, true);
                _lastTargetingWorldChars.Add(target);
            }
        }

        // 사이드 유닛 HUD로 외부 호버 및 타깃팅 목록 전파 (_externalHoveredCharacter 제외하여 재귀 루프 방지)
        if (BattleSideUnitHUDPanel.Instance != null)
        {
            Character sideExternal = _uiHoveredSlot != null
                ? _uiHoveredSlot.BoundCharacter
                : (_worldHoveredCharacter != null ? _worldHoveredCharacter : _targetingHoveredCharacter);
            BattleSideUnitHUDPanel.Instance.SetExternalHoverCharacter(sideExternal);
            BattleSideUnitHUDPanel.Instance.SetExternalTargetingCharacters(_targetingHighlightedCharacters);
        }
    }

    private void RefreshGhostPreview()
    {
        Card activeCard = _hoveredCard != null ? _hoveredCard : _targetingCard;
        Character explicitTarget = _hoveredCard != null ? null : _targetingHoveredCharacter;

        if (_battleManager == null || activeCard == null || slotContainer == null || slotPrefab == null || !IsPlayerTurnActive(out Character playerChar))
        {
            _currentPreview = TimelinePreviewRequest.None;
            HideGhostSlots();
            return;
        }

        _currentPreview = BuildPreviewRequest(activeCard, playerChar, explicitTarget);
        if (!_currentPreview.hasPreview)
        {
            HideGhostSlots();
            return;
        }

        _battleManager.PredictTimeline(_previewEntries, in _currentPreview);

        bool hasBasePredicted = TryFindEntry(_baseEntries, playerChar, isPredictedNextTurn: true, out int basePredIdx, out TimelineEntry basePredEntry);
        bool hasPrevPredicted = TryFindEntry(_previewEntries, playerChar, isPredictedNextTurn: true, out int prevPredIdx, out TimelineEntry prevPredEntry);

        bool predictedChanged = hasBasePredicted && hasPrevPredicted &&
            (basePredIdx != prevPredIdx || Mathf.Abs(basePredEntry.timeUntilTurn - prevPredEntry.timeUntilTurn) > 0.01f);

        bool targetChanged = false;
        int prevTargetIdx = -1;
        TimelineEntry prevTargetEntry = default;
        if (_currentPreview.targetCharacter != null && _currentPreview.targetCharacter != playerChar)
        {
            bool hasBaseTarget = TryFindEntry(_baseEntries, _currentPreview.targetCharacter, isPredictedNextTurn: false, out int baseTargetIdx, out TimelineEntry baseTargetEntry);
            bool hasPrevTarget = TryFindEntry(_previewEntries, _currentPreview.targetCharacter, isPredictedNextTurn: false, out prevTargetIdx, out prevTargetEntry);
            targetChanged = hasBaseTarget && hasPrevTarget &&
                (baseTargetIdx != prevTargetIdx ||
                 Mathf.Abs(baseTargetEntry.timeUntilTurn - prevTargetEntry.timeUntilTurn) > 0.01f ||
                 baseTargetEntry.isStunned != prevTargetEntry.isStunned);
        }

        if (!predictedChanged && !targetChanged)
        {
            HideGhostSlots();
            return;
        }

        int previewCount = _previewEntries.Count;
        if (predictedChanged && targetChanged)
        {
            ShowGhostSlot(ref _ghostSlotUI, in prevPredEntry, GetSlotPositionX(prevPredIdx, previewCount));
            ShowGhostSlot(ref _secondaryGhostSlotUI, in prevTargetEntry, GetSlotPositionX(prevTargetIdx, previewCount));
        }
        else if (targetChanged)
        {
            ShowGhostSlot(ref _ghostSlotUI, in prevTargetEntry, GetSlotPositionX(prevTargetIdx, previewCount));
            HideGhostSlot(_secondaryGhostSlotUI);
        }
        else
        {
            ShowGhostSlot(ref _ghostSlotUI, in prevPredEntry, GetSlotPositionX(prevPredIdx, previewCount));
            HideGhostSlot(_secondaryGhostSlotUI);
        }
    }

    private void ShowGhostSlot(ref BattleTimelineSlotUI ghostSlot, in TimelineEntry entry, float targetX)
    {
        if (ghostSlot == null)
        {
            ghostSlot = Instantiate(slotPrefab, slotContainer, false);
            ghostSlot.name = "BattleTimelineGhostSlotUI";
        }

        ghostSlot.gameObject.SetActive(true);
        ghostSlot.Bind(in entry, isGhost: true);
        ghostSlot.SetHighlighted(false);
        ghostSlot.CachedRectTransform.anchoredPosition = new Vector2(targetX, -14f);
        ghostSlot.transform.SetAsLastSibling();
    }

    private void HideGhostSlots()
    {
        HideGhostSlot(_ghostSlotUI);
        HideGhostSlot(_secondaryGhostSlotUI);
    }

    private static void HideGhostSlot(BattleTimelineSlotUI ghostSlot)
    {
        if (ghostSlot != null && ghostSlot.gameObject.activeSelf)
        {
            ghostSlot.ResetForPool();
            ghostSlot.gameObject.SetActive(false);
        }
    }

    private bool IsPlayerTurnActive(out Character playerChar)
    {
        playerChar = null;
        if (_battleManager == null || _battleManager.battleState != BattleState.TurnInProgress)
        {
            return false;
        }

        ICharacterController currentCtrl = _battleManager.currentTurnController;
        if (currentCtrl == null || currentCtrl.controlledCharacter == null || currentCtrl.controlledCharacter.isDead)
        {
            return false;
        }

        if (currentCtrl.controlledCharacter.faction != CharacterFaction.Player)
        {
            return false;
        }

        playerChar = currentCtrl.controlledCharacter;
        return true;
    }

    private TimelinePreviewRequest BuildPreviewRequest(Card card, Character playerChar, Character explicitTarget)
    {
        if (card == null || playerChar == null)
        {
            return TimelinePreviewRequest.None;
        }

        TimelinePreviewRequest request = new TimelinePreviewRequest
        {
            hasPreview = true,
            casterStaminaSpent = Mathf.Max(0, card.currentCost),
            targetCharacter = null,
            targetStaminaDelta = 0f,
            targetRegenFlatDelta = 0f,
            targetRegenPercentDelta = 0f,
            targetRegenDuration = 0f,
            targetGrantsStun = false,
        };

        CardData data = card.cardData;
        if (data == null)
        {
            return request;
        }

        Character resolvedTarget = ResolvePreviewTargetCharacter(card, playerChar, explicitTarget);
        float bonusRegen = card.GetEffectiveValue("bonusRegen");
        float bonusRegenPercent = card.GetEffectiveValue("bonusRegenPercent");
        float staminaLoss = card.GetEffectiveValue("staminaLoss");
        bool grantsStun = DoesCardGrantStun(card, data);

        if ((bonusRegen > 0f || bonusRegenPercent > 0f) && data.parsedTargetType == TargetType.Self)
        {
            request.targetCharacter = playerChar;
            request.targetRegenFlatDelta = bonusRegen;
            request.targetRegenPercentDelta = bonusRegenPercent;

            float duration = card.GetEffectiveValue("duration");
            if (duration <= 0f)
            {
                duration = TryParseSecondsFromDescription(data.description, 10f);
            }
            request.targetRegenDuration = duration;
        }

        if (resolvedTarget != null && resolvedTarget != playerChar)
        {
            if (staminaLoss > 0f)
            {
                request.targetCharacter = resolvedTarget;
                request.targetStaminaDelta = -staminaLoss;
            }

            if (grantsStun)
            {
                request.targetCharacter = resolvedTarget;
                request.targetGrantsStun = true;
            }
        }
        else if (resolvedTarget == playerChar && grantsStun)
        {
            request.targetCharacter = playerChar;
            request.targetGrantsStun = true;
        }

        return request;
    }

    private Character ResolvePreviewTargetCharacter(Card card, Character playerChar, Character explicitTarget)
    {
        if (explicitTarget != null && !explicitTarget.isDead)
        {
            return explicitTarget;
        }

        if (card == null || card.cardData == null || _battleManager == null)
        {
            return null;
        }

        TargetType targetType = card.cardData.parsedTargetType;
        if (targetType == TargetType.Self)
        {
            return playerChar;
        }

        IReadOnlyList<ICharacterController> combatants = _battleManager.GetAllCombatants();
        if (targetType == TargetType.AllEnemies)
        {
            for (int i = 0; i < combatants.Count; i++)
            {
                Character c = combatants[i]?.controlledCharacter;
                if (c != null && !c.isDead && playerChar.IsHostileTo(c))
                {
                    return c;
                }
            }
            return null;
        }

        if (targetType == TargetType.AreaSelf || targetType == TargetType.Directional)
        {
            Tile playerTile = playerChar.characterMove != null ? playerChar.characterMove.GetCurrentTile() : null;
            if (playerTile == null) return null;

            if (GameMap.current != null && card.cardData.additionalEffectAreaList != null && card.cardData.additionalEffectAreaList.Count > 0)
            {
                LookDirection facing = playerChar.characterMove != null ? playerChar.characterMove.facingDirection : LookDirection.Down;
                Vector2Int originCoord = playerTile.GetCoord();
                List<Vector2Int> coords = GameMap.current.CalculateCardAoECoords(originCoord, originCoord, card.cardData, facing);

                _aoeCoordScratch.Clear();
                if (coords != null)
                {
                    for (int i = 0; i < coords.Count; i++)
                    {
                        _aoeCoordScratch.Add(coords[i]);
                    }
                }
                if (targetType == TargetType.AreaSelf)
                {
                    _aoeCoordScratch.Add(originCoord);
                }

                for (int i = 0; i < combatants.Count; i++)
                {
                    Character c = combatants[i]?.controlledCharacter;
                    if (c == null || c.isDead || !playerChar.IsHostileTo(c)) continue;
                    Tile enemyTile = c.characterMove != null ? c.characterMove.GetCurrentTile() : null;
                    if (enemyTile != null && _aoeCoordScratch.Contains(enemyTile.GetCoord()))
                    {
                        return c;
                    }
                }
                return null;
            }

            if (targetType == TargetType.AreaSelf)
            {
                int minDist = card.cardData.targetMinDistance;
                int maxDist = card.cardData.targetMaxDistance;
                for (int i = 0; i < combatants.Count; i++)
                {
                    Character c = combatants[i]?.controlledCharacter;
                    if (c == null || c.isDead || !playerChar.IsHostileTo(c)) continue;
                    Tile enemyTile = c.characterMove != null ? c.characterMove.GetCurrentTile() : null;
                    if (enemyTile != null)
                    {
                        int dist = Tile.GetDistance(playerTile, enemyTile);
                        if (dist >= minDist && dist <= maxDist)
                        {
                            return c;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static bool DoesCardGrantStun(Card card, CardData data)
    {
        if (card.GetEffectiveValue("stun") > 0f ||
            card.GetEffectiveValue("stunStack") > 0f ||
            card.GetEffectiveValue("stunTurns") > 0f)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(data.description) &&
            data.description.IndexOf("[effect.stun]", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    private static float TryParseSecondsFromDescription(string desc, float fallbackSeconds)
    {
        if (string.IsNullOrEmpty(desc)) return fallbackSeconds;

        int secIdx = desc.IndexOf('초');
        if (secIdx <= 0) return fallbackSeconds;

        int start = secIdx - 1;
        while (start >= 0 && (char.IsDigit(desc[start]) || desc[start] == '.'))
        {
            start--;
        }
        start++;

        if (start < secIdx && float.TryParse(desc.Substring(start, secIdx - start), out float parsed) && parsed > 0f)
        {
            return parsed;
        }

        return fallbackSeconds;
    }

    private static bool TryFindEntry(List<TimelineEntry> entries, Character targetChar, bool isPredictedNextTurn, out int foundIndex, out TimelineEntry foundEntry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].character == targetChar && entries[i].isPredictedNextTurn == isPredictedNextTurn)
            {
                foundIndex = i;
                foundEntry = entries[i];
                return true;
            }
        }

        foundIndex = -1;
        foundEntry = default;
        return false;
    }

    private void ClearAllHoverState()
    {
        _isEndTurnHovered = false;
        _hoveredCard = null;
        _targetingCard = null;
        _targetingHoveredCharacter = null;
        _targetingHighlightedCharacters.Clear();
        _targetingScratch.Clear();
        _singleTargetingScratch.Clear();
        _worldHoveredCharacter = null;
        _externalHoveredCharacter = null;
        _uiHoveredSlot = null;
        _cachedCharHitCollider = null;
        _cachedCharHitResult = null;
        _cachedTileHitCollider = null;
        _cachedTileHitResult = null;
        _currentPreview = TimelinePreviewRequest.None;

        if (_lastFocusedWorldChar != null)
        {
            CharacterUIController.SetTimelineHighlightFor(_lastFocusedWorldChar, false);
            _lastFocusedWorldChar = null;
        }

        for (int i = 0; i < _lastTargetingWorldChars.Count; i++)
        {
            Character prev = _lastTargetingWorldChars[i];
            if (prev != null)
            {
                CharacterUIController.SetTargetingHighlightFor(prev, false);
            }
        }
        _lastTargetingWorldChars.Clear();

        if (BattleSideUnitHUDPanel.Instance != null)
        {
            BattleSideUnitHUDPanel.Instance.SetExternalHoverCharacter(null);
            BattleSideUnitHUDPanel.Instance.SetExternalTargetingCharacters(null);
        }

        HideGhostSlots();
    }

    #region Battle Event Subscriptions

    private void SubscribeBattleEvents()
    {
        if (_battleManager == null) return;

        if (!_isEventsSubscribed)
        {
            _battleManager.OnTimelineDirty -= HandleTimelineDirty;
            _battleManager.OnTimelineDirty += HandleTimelineDirty;
            _isEventsSubscribed = true;
        }

        SyncCombatantSubscriptions();
    }

    private void UnsubscribeBattleEvents()
    {
        if (_battleManager != null && _isEventsSubscribed)
        {
            _battleManager.OnTimelineDirty -= HandleTimelineDirty;
        }
        _isEventsSubscribed = false;

        for (int i = 0; i < _subscribedCharacters.Count; i++)
        {
            UnsubscribeCharacterEvents(_subscribedCharacters[i]);
        }
        _subscribedCharacters.Clear();
        _desiredCharacters.Clear();
    }

    private void SyncCombatantSubscriptions()
    {
        _desiredCharacters.Clear();

        if (_battleManager != null)
        {
            IReadOnlyList<ICharacterController> combatants = _battleManager.GetAllCombatants();
            for (int i = 0; i < combatants.Count; i++)
            {
                Character c = combatants[i]?.controlledCharacter;
                if (c != null && !c.isDead && !_desiredCharacters.Contains(c))
                {
                    _desiredCharacters.Add(c);
                }
            }
        }

        // 제거/사망 전투원 구독 해제
        for (int i = _subscribedCharacters.Count - 1; i >= 0; i--)
        {
            Character existing = _subscribedCharacters[i];
            if (existing == null || !_desiredCharacters.Contains(existing))
            {
                UnsubscribeCharacterEvents(existing);
                _subscribedCharacters.RemoveAt(i);
            }
        }

        // 신규 참전 전투원 구독 등록
        for (int i = 0; i < _desiredCharacters.Count; i++)
        {
            Character desired = _desiredCharacters[i];
            if (!_subscribedCharacters.Contains(desired))
            {
                SubscribeCharacterEvents(desired);
                _subscribedCharacters.Add(desired);
            }
        }

        _desiredCharacters.Clear();
    }

    private void SubscribeCharacterEvents(Character c)
    {
        if (c == null) return;

        c.OnCharacterStaminaChanged -= HandleCharacterStateDirty;
        c.OnCharacterStaminaChanged += HandleCharacterStateDirty;

        c.OnCharacterDied -= HandleCharacterDied;
        c.OnCharacterDied += HandleCharacterDied;

        c.OnIntentChanged -= HandleIntentChanged;
        c.OnIntentChanged += HandleIntentChanged;

        if (c.effectManager != null)
        {
            c.effectManager.OnEffectAdded -= HandleEffectChanged;
            c.effectManager.OnEffectAdded += HandleEffectChanged;

            c.effectManager.OnEffectStacked -= HandleEffectChanged;
            c.effectManager.OnEffectStacked += HandleEffectChanged;

            c.effectManager.OnEffectRemoved -= HandleEffectChanged;
            c.effectManager.OnEffectRemoved += HandleEffectChanged;
        }
    }

    private void UnsubscribeCharacterEvents(Character c)
    {
        if (c == null) return;

        c.OnCharacterStaminaChanged -= HandleCharacterStateDirty;
        c.OnCharacterDied -= HandleCharacterDied;
        c.OnIntentChanged -= HandleIntentChanged;

        if (c.effectManager != null)
        {
            c.effectManager.OnEffectAdded -= HandleEffectChanged;
            c.effectManager.OnEffectStacked -= HandleEffectChanged;
            c.effectManager.OnEffectRemoved -= HandleEffectChanged;
        }
    }

    private void HandleTimelineDirty()
    {
        SyncCombatantSubscriptions();
        MarkDirty();
    }

    private void HandleCharacterStateDirty(Character c)
    {
        MarkDirty();
    }

    private void HandleCharacterDied(Character c)
    {
        UnsubscribeCharacterEvents(c);
        _subscribedCharacters.Remove(c);
        if (_worldHoveredCharacter == c)
        {
            _worldHoveredCharacter = null;
        }
        if (_targetingHoveredCharacter == c)
        {
            _targetingHoveredCharacter = null;
        }
        RemoveCharacterFromList(_targetingHighlightedCharacters, c);
        if (_targetingHoveredCharacter == null && _targetingHighlightedCharacters.Count > 0)
        {
            _targetingHoveredCharacter = _targetingHighlightedCharacters[0];
        }
        if (_externalHoveredCharacter == c)
        {
            _externalHoveredCharacter = null;
        }
        if (_lastFocusedWorldChar == c)
        {
            CharacterUIController.SetTimelineHighlightFor(c, false);
            _lastFocusedWorldChar = null;
        }
        if (RemoveCharacterFromList(_lastTargetingWorldChars, c))
        {
            CharacterUIController.SetTargetingHighlightFor(c, false);
        }
        RefreshSlotHighlights();
        MarkDirty();
    }

    private void HandleIntentChanged(EnemyIntentInfo intent)
    {
        MarkDirty();
    }

    private void HandleEffectChanged(Effect effect)
    {
        MarkDirty();
    }

    private static bool ContainsCharacter(List<Character> list, Character target)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], target))
            {
                return true;
            }
        }
        return false;
    }

    private static bool AreCharacterListsEquivalent(List<Character> a, List<Character> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!ContainsCharacter(b, a[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool RemoveCharacterFromList(List<Character> list, Character target)
    {
        bool removed = false;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(list[i], target))
            {
                list.RemoveAt(i);
                removed = true;
            }
        }
        return removed;
    }

    #endregion
}
