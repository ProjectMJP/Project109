using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 적 유닛의 머리 위에 다음 턴 행동 예고(Intent) 아이콘과 예상 수치를 표시하고,
/// 마우스 오버 시 L2 상세 툴팁과 예상 공격 범위 하이라이트, 일반 모드 좌클릭 시 해당 유닛 방향 회전을 제공하는 월드 UI View 컴포넌트입니다.
/// </summary>
public class CharacterIntentUI : MonoBehaviour, IWorldFollowUI, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Target & Position Settings")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Vector3 worldOffset = UIConstants.DEFAULT_INTENT_OFFSET;
    [SerializeField] private bool useBillboard = true;

    [Header("UI Component References")]
    [SerializeField] private Image backgroundBadgeImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text valueText;

    private Camera cachedCamera;
    private Character _targetCharacter;
    private EnemyIntentInfo _currentIntent;
    private bool _isPointerHovering;
    private bool _hasShownTooltip;
    private bool _hasHighlightedAoE;

    public Transform TargetTransform => targetTransform;
    public Vector3 WorldOffset
    {
        get => worldOffset;
        set => worldOffset = value;
    }

    private void Awake()
    {
        cachedCamera = Camera.main;

        if (iconImage == null)
        {
            Debug.LogError("[CharacterIntentUI] iconImage가 바인딩되지 않았습니다! CharacterIntentUI 프리팹 설정을 확인하세요.", this);
        }
    }

    private void OnDisable()
    {
        ClearHoverVisuals();
    }

    /// <summary>
    /// 예고 UI가 추적할 타깃 트랜스폼 및 월드 오프셋을 설정합니다.
    /// </summary>
    public void SetTarget(Transform target, Vector3? offset = null)
    {
        targetTransform = target;
        _targetCharacter = targetTransform != null ? targetTransform.GetComponent<Character>() : null;
        if (offset.HasValue)
        {
            worldOffset = offset.Value;
        }

        UpdatePosition();
    }

    /// <summary>
    /// 적 행동 예고 스냅샷 데이터를 L1 상시 UI(아이콘, 배경색, 수치)에 반영합니다.
    /// </summary>
    public void SetIntent(in EnemyIntentInfo intent)
    {
        _currentIntent = intent;

        if (!_currentIntent.isValid || _currentIntent.intentType == EnemyIntentType.None)
        {
            ClearHoverVisuals();
            if (valueText != null)
            {
                valueText.gameObject.SetActive(false);
            }
            return;
        }

        Color intentColor = IntentIconProvider.GetIntentColor(_currentIntent.intentType);
        Sprite intentSprite = IntentIconProvider.GetIntentIcon(_currentIntent.intentType);

        if (backgroundBadgeImage != null)
        {
            backgroundBadgeImage.color = new Color(
                intentColor.r * 0.72f,
                intentColor.g * 0.72f,
                intentColor.b * 0.72f,
                0.92f);
        }

        if (iconImage != null)
        {
            iconImage.sprite = intentSprite;
            iconImage.color = Color.white;
            iconImage.enabled = (intentSprite != null);
        }

        if (valueText != null)
        {
            if (_currentIntent.estimatedValue > 0)
            {
                valueText.text = _currentIntent.estimatedValue.ToString();
                valueText.gameObject.SetActive(true);
            }
            else
            {
                // Stunned, MoveOnly, Buff, Debuff 등 수치 없는 의도는 텍스트를 숨겨 아이콘을 중앙 정렬 표시
                valueText.gameObject.SetActive(false);
            }
        }

        if (_isPointerHovering)
        {
            RefreshHoverPresentation();
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        UpdatePosition();
    }

    public void Hide()
    {
        ClearHoverVisuals();
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    public void UpdatePosition()
    {
        if (targetTransform == null) return;

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
            if (cachedCamera == null) return;
        }

        transform.position = targetTransform.position + worldOffset;

        if (useBillboard)
        {
            transform.rotation = cachedCamera.transform.rotation;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_currentIntent.isValid || _currentIntent.intentType == EnemyIntentType.None)
        {
            return;
        }

        _isPointerHovering = true;
        RefreshHoverPresentation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHoverVisuals();
    }

    /// <summary>
    /// 일반 전투 모드에서 적 행동 예고 배지 좌클릭 시 플레이어가 해당 적 방향을 바라보도록 회전합니다.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (RunManager.instance != null && RunManager.instance.playerBattleController != null)
        {
            RunManager.instance.playerBattleController.TryRotatePlayerTowardCharacter(ResolveTargetCharacter());
        }
    }

    private void RefreshHoverPresentation()
    {
        if (!_currentIntent.isValid || _currentIntent.intentType == EnemyIntentType.None)
        {
            ClearHoverVisuals();
            return;
        }

        // 1. L2 상세 툴팁 표시 (예고 분류명, 카드명, 예상 수치, 이동 여부, 설명)
        if (TooltipManager.Instance != null)
        {
            string title = IntentIconProvider.GetIntentLabel(_currentIntent.intentType);
            string body = BuildTooltipBody(in _currentIntent);
            TooltipManager.Instance.ShowTooltip(title, body, transform as RectTransform);
            _hasShownTooltip = true;
        }

        // 2. 플레이어 Normal 조작 상태일 때 도달 가능한 공격/방해/특수 행동의 예상 AoE 범위 하이라이트
        if (IsPlayerNormalControlState() &&
            _currentIntent.canReachTarget &&
            _currentIntent.plannedCard != null &&
            _currentIntent.plannedCard.cardData != null &&
            ShouldHighlightIntentAoE(_currentIntent.intentType) &&
            GameMap.current != null)
        {
            Character ownerChar = ResolveTargetCharacter();
            Vector2Int originCoord;
            if (_currentIntent.requiresMove && _currentIntent.plannedMoveTile != null)
            {
                originCoord = _currentIntent.plannedMoveTile.GetCoord();
            }
            else if (ownerChar != null)
            {
                originCoord = GameMap.current.GetCharacterCoordinate(ownerChar);
            }
            else
            {
                originCoord = Vector2Int.zero;
            }

            Vector2Int targetCoord = _currentIntent.targetCharacter != null
                ? GameMap.current.GetCharacterCoordinate(_currentIntent.targetCharacter)
                : originCoord;

            LookDirection facing = LookDirection.East;
            if (originCoord != targetCoord)
            {
                facing = EffectAreaShapeGenerator.GetDirectionFromCoords(originCoord, targetCoord, LookDirection.East);
            }
            else if (ownerChar != null && ownerChar.characterMove != null)
            {
                facing = ownerChar.characterMove.facingDirection;
            }

            GameMap.current.ShowCardAoE(originCoord, targetCoord, _currentIntent.plannedCard.cardData, facing);
            _hasHighlightedAoE = true;
        }
        else if (_hasHighlightedAoE)
        {
            _hasHighlightedAoE = false;
            if (GameMap.current != null && IsPlayerNormalControlState())
            {
                GameMap.current.ClearEffectAreaTiles();
            }
        }
    }

    private void ClearHoverVisuals()
    {
        if (!_isPointerHovering && !_hasShownTooltip && !_hasHighlightedAoE)
        {
            return;
        }

        _isPointerHovering = false;

        if (_hasShownTooltip)
        {
            _hasShownTooltip = false;
            if (TooltipManager.HasInstance && TooltipManager.Instance.gameObject.activeSelf)
            {
                TooltipManager.Instance.HideTooltip();
            }
        }

        if (_hasHighlightedAoE)
        {
            _hasHighlightedAoE = false;
            if (GameMap.current != null && IsPlayerNormalControlState())
            {
                GameMap.current.ClearEffectAreaTiles();
            }
        }
    }

    private Character ResolveTargetCharacter()
    {
        if (_targetCharacter == null && targetTransform != null)
        {
            _targetCharacter = targetTransform.GetComponent<Character>();
        }
        return _targetCharacter;
    }

    private static bool IsPlayerNormalControlState()
    {
        return RunManager.instance != null
            && RunManager.instance.playerBattleController != null
            && RunManager.instance.playerBattleController.controlState == PlayerControlState.Normal;
    }

    private static bool ShouldHighlightIntentAoE(EnemyIntentType intentType)
    {
        return intentType == EnemyIntentType.Attack
            || intentType == EnemyIntentType.AttackMove
            || intentType == EnemyIntentType.Debuff
            || intentType == EnemyIntentType.Special;
    }

    private static string BuildTooltipBody(in EnemyIntentInfo intent)
    {
        StringBuilder sb = new StringBuilder(128);

        string cardTitle = string.Empty;
        if (intent.plannedCard != null && intent.plannedCard.cardData != null)
        {
            cardTitle = intent.plannedCard.cardData.LocalizedName;
        }
        else if (!string.IsNullOrEmpty(intent.cardId))
        {
            cardTitle = intent.cardId;
        }

        if (!string.IsNullOrEmpty(cardTitle))
        {
            sb.Append("예고 행동: ").Append(cardTitle);
        }

        if (intent.estimatedValue > 0)
        {
            if (sb.Length > 0) sb.Append('\n');
            if (intent.intentType == EnemyIntentType.Attack || intent.intentType == EnemyIntentType.AttackMove)
            {
                sb.Append("예상 피해: ").Append(intent.estimatedValue);
            }
            else if (intent.intentType == EnemyIntentType.Defend)
            {
                sb.Append("예상 방어도: ").Append(intent.estimatedValue);
            }
            else
            {
                sb.Append("예상 수치: ").Append(intent.estimatedValue);
            }
        }

        if (intent.intentType != EnemyIntentType.Stunned)
        {
            if (sb.Length > 0) sb.Append('\n');
            if (intent.intentType == EnemyIntentType.MoveOnly)
            {
                sb.Append("이동 여부: 사거리 밖으로 이동만 수행");
            }
            else if (intent.requiresMove)
            {
                sb.Append("이동 여부: 이동 후 행동");
            }
            else
            {
                sb.Append("이동 여부: 제자리 행동");
            }
        }

        string desc = string.Empty;
        if (intent.intentType == EnemyIntentType.Stunned)
        {
            desc = "기절·무장해제·이동 불가 등으로 이번 턴에 행동할 수 없어 대기합니다.";
        }
        else if (intent.intentType == EnemyIntentType.MoveOnly && !intent.canReachTarget)
        {
            desc = "사거리가 닿지 않아 대상을 향해 이동만 수행합니다.";
        }
        else if (intent.plannedCard != null)
        {
            desc = intent.plannedCard.GetDescription();
        }

        if (!string.IsNullOrEmpty(desc))
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(desc);
        }

        return sb.ToString();
    }
}
