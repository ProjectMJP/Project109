using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 모델(Character, EffectManager)의 상태 변화 이벤트를 감지하여
/// UIManager에 요청하여 획득한 2개의 독립 머리 위 뷰(CharacterStatusBarUI, CharacterEffectListUI)에 데이터를 갱신하는 컨트롤러입니다.
/// (프리팹 소유 및 생성/소멸 관리는 UIManager가 전담합니다.)
/// </summary>
public class CharacterUIController : MonoBehaviour, IOnBattleStart, IOnBattleEnd
{
    [Header("Model Reference")]
    [SerializeField] private Character character;

    [Header("World Position Offsets")]
    [SerializeField] private Vector3 statusBarOffset = UIConstants.DEFAULT_STATUS_BAR_OFFSET;
    [SerializeField] private Vector3 effectListOffset = UIConstants.DEFAULT_EFFECT_LIST_OFFSET;
    [SerializeField] private Vector3 effectDetailOffset = UIConstants.DEFAULT_EFFECT_DETAIL_OFFSET;
    [SerializeField] private Vector3 intentOffset = UIConstants.DEFAULT_INTENT_OFFSET;

    private CharacterStatusBarUI statusBarUI;
    private CharacterEffectListUI effectListUI;
    private CharacterEffectDetailUI effectDetailUI;
    private CharacterIntentUI intentUI;
    private bool _isBattleActive = false;
    private bool _isEventBusRegistered = false;
    private bool _isEffectDetailOpen = false;
    private bool _isTimelineHighlighted = false;

    private static CharacterUIController _activeInspectedController;
    private static readonly Dictionary<Character, CharacterUIController> _controllersByCharacter = new Dictionary<Character, CharacterUIController>();

    /// <summary>
    /// 지정된 캐릭터에 대응하는 CharacterUIController의 타임라인 하이라이트 상태를 설정합니다.
    /// </summary>
    public static void SetTimelineHighlightFor(Character target, bool highlighted)
    {
        if (target == null) return;

        if (_controllersByCharacter.TryGetValue(target, out CharacterUIController ctrl) && ctrl != null)
        {
            ctrl.SetTimelineHighlight(highlighted);
        }
    }

    /// <summary>
    /// 현재 화면에 열려 있는 캐릭터 이펙트 상세 툴팁이 있는지 여부입니다.
    /// </summary>
    public static bool HasActiveEffectDetail =>
        _activeInspectedController != null && _activeInspectedController._isEffectDetailOpen;

    /// <summary>
    /// 현재 열려 있는 캐릭터 이펙트 상세 툴팁이 있다면 즉시 닫습니다.
    /// </summary>
    public static void CloseActiveEffectDetail()
    {
        if (_activeInspectedController != null)
        {
            _activeInspectedController.CloseEffectDetail();
        }
        else
        {
            _activeInspectedController = null;
        }
    }

    public Character Character => character;
    public CharacterStatusBarUI StatusBarUI => statusBarUI;
    public CharacterEffectListUI EffectListUI => effectListUI;
    public CharacterEffectDetailUI EffectDetailUI => effectDetailUI;
    public CharacterIntentUI IntentUI => intentUI;

    private void Awake()
    {
        if (character == null)
        {
            character = GetComponent<Character>();
        }

        // 동적 스폰 프레임에 Start() 이전에 전투 합류(NotifyJoinedBattle) 이벤트가 발생할 수 있으므로 선등록
        RegisterEvents();
    }

    private void Start()
    {
        RequestViewsFromUIManager();
        RegisterEvents();

        // Start() 이전에 이미 전투 합류로 활성화된 경우 숨기지 않고 UI를 유지합니다.
        if (_isBattleActive)
        {
            ShowUI();
        }
        else
        {
            // 초기 생성 시(은신처 및 필드 탐색)에는 머리 위 UI를 숨겨 시야와 가시성을 확보하고, 전투 시에만 켭니다.
            HideUI();
        }
    }

    private void OnDestroy()
    {
        SetTimelineHighlight(false);
        if (character != null && _controllersByCharacter.TryGetValue(character, out CharacterUIController registered) && registered == this)
        {
            _controllersByCharacter.Remove(character);
        }

        CloseEffectDetail();
        UnregisterEvents();

        // UIManager에 UI 해제 및 소멸 요청
        if (UIManager.instance != null)
        {
            if (statusBarUI != null)
            {
                UIManager.instance.ReleaseWorldUI(statusBarUI);
                statusBarUI = null;
            }

            if (effectListUI != null)
            {
                UIManager.instance.ReleaseWorldUI(effectListUI);
                effectListUI = null;
            }

            if (effectDetailUI != null)
            {
                UIManager.instance.ReleaseWorldUI(effectDetailUI);
                effectDetailUI = null;
            }

            if (intentUI != null)
            {
                UIManager.instance.ReleaseWorldUI(intentUI);
                intentUI = null;
            }
        }
    }

    private void LateUpdate()
    {
        // UIManager 타이밍 지연 대응: 뷰가 아직 생성되지 않은 경우 재시도
        bool needsIntentView = character != null && character.faction == CharacterFaction.Enemy && intentUI == null;
        if ((statusBarUI == null || needsIntentView) && UIManager.instance != null)
        {
            RequestViewsFromUIManager();
            if (statusBarUI != null)
            {
                if (_isBattleActive)
                {
                    ShowUI();
                }
                else
                {
                    HideUI();
                }
            }
        }

        // 버프/디버프 쿨다운 게이지 링 갱신 (전투 활성 상태에서만 갱신)
        if (effectListUI != null && _isBattleActive)
        {
            effectListUI.RefreshDurations();
        }

        if (_isEffectDetailOpen && effectDetailUI != null)
        {
            effectDetailUI.RefreshDurations();
        }
    }

    /// <summary>
    /// UIManager에 상태바 및 이펙트 리스트 UI 생성을 요청하고 인스턴스를 바인딩합니다.
    /// </summary>
    private void RequestViewsFromUIManager()
    {
        if (UIManager.instance == null) return;

        if (statusBarUI == null)
        {
            statusBarUI = UIManager.instance.CreateStatusBarUI(transform, statusBarOffset);
            if (statusBarUI != null)
            {
                if (!_isBattleActive) statusBarUI.Hide();
                if (_isTimelineHighlighted)
                {
                    statusBarUI.transform.localScale = new Vector3(1.18f, 1.18f, 1.18f);
                }
            }
        }

        if (effectListUI == null)
        {
            effectListUI = UIManager.instance.CreateEffectListUI(transform, effectListOffset);
            if (!_isBattleActive && effectListUI != null) effectListUI.Hide();
        }

        if (intentUI == null && character != null && character.faction == CharacterFaction.Enemy)
        {
            intentUI = UIManager.instance.CreateIntentUI(transform, intentOffset);
            if (intentUI != null)
            {
                intentUI.SetIntent(character.CurrentIntent);
                if (!_isBattleActive || !character.CurrentIntent.isValid || character.CurrentIntent.intentType == EnemyIntentType.None)
                {
                    intentUI.Hide();
                }
            }
        }
    }

    #region Event Registration

    private void RegisterEvents()
    {
        if (character == null) return;

        _controllersByCharacter[character] = this;

        character.OnCharacterHealthChanged -= OnHealthChanged;
        character.OnCharacterHealthChanged += OnHealthChanged;

        character.OnCharacterShieldChanged -= OnShieldChanged;
        character.OnCharacterShieldChanged += OnShieldChanged;

        character.OnCharacterStaminaChanged -= OnStaminaChanged;
        character.OnCharacterStaminaChanged += OnStaminaChanged;

        character.OnCharacterDied -= OnCharacterDied;
        character.OnCharacterDied += OnCharacterDied;

        character.OnCharacterJoinedBattle -= OnJoinedBattle;
        character.OnCharacterJoinedBattle += OnJoinedBattle;

        character.OnCharacterLeftBattle -= OnLeftBattle;
        character.OnCharacterLeftBattle += OnLeftBattle;

        character.OnIntentChanged -= OnIntentChanged;
        character.OnIntentChanged += OnIntentChanged;

        if (character.effectManager != null)
        {
            character.effectManager.OnEffectAdded -= OnEffectAdded;
            character.effectManager.OnEffectAdded += OnEffectAdded;

            character.effectManager.OnEffectStacked -= OnEffectStacked;
            character.effectManager.OnEffectStacked += OnEffectStacked;

            character.effectManager.OnEffectRemoved -= OnEffectRemoved;
            character.effectManager.OnEffectRemoved += OnEffectRemoved;
        }

        if (!_isEventBusRegistered && character.eventBus != null)
        {
            character.eventBus.Add<IOnBattleStart>(this);
            character.eventBus.Add<IOnBattleEnd>(this);
            _isEventBusRegistered = true;
        }
    }

    private void UnregisterEvents()
    {
        if (character == null) return;

        character.OnCharacterHealthChanged -= OnHealthChanged;
        character.OnCharacterShieldChanged -= OnShieldChanged;
        character.OnCharacterStaminaChanged -= OnStaminaChanged;
        character.OnCharacterDied -= OnCharacterDied;
        character.OnCharacterJoinedBattle -= OnJoinedBattle;
        character.OnCharacterLeftBattle -= OnLeftBattle;
        character.OnIntentChanged -= OnIntentChanged;

        if (character.effectManager != null)
        {
            character.effectManager.OnEffectAdded -= OnEffectAdded;
            character.effectManager.OnEffectStacked -= OnEffectStacked;
            character.effectManager.OnEffectRemoved -= OnEffectRemoved;
        }

        if (_isEventBusRegistered && character.eventBus != null)
        {
            character.eventBus.Remove<IOnBattleStart>(this);
            character.eventBus.Remove<IOnBattleEnd>(this);
            _isEventBusRegistered = false;
        }
    }

    #endregion

    #region Model Event Handlers

    private void OnHealthChanged(Character c)
    {
        if (!_isBattleActive || statusBarUI == null || c == null) return;
        float maxHp = (c.curCharacterStat != null && c.curCharacterStat.maxHealth > 0f)
            ? c.curCharacterStat.maxHealth
            : Mathf.Max(1f, c.curHealth);

        statusBarUI.UpdateHealth(c.curHealth, maxHp, c.shield);
    }

    private void OnShieldChanged(Character c)
    {
        if (!_isBattleActive || statusBarUI == null || c == null) return;
        float maxHp = (c.curCharacterStat != null && c.curCharacterStat.maxHealth > 0f)
            ? c.curCharacterStat.maxHealth
            : Mathf.Max(1f, c.curHealth);

        statusBarUI.UpdateHealth(c.curHealth, maxHp, c.shield);
    }

    private void OnStaminaChanged(Character c)
    {
        if (!_isBattleActive || statusBarUI == null || c == null) return;
        float maxStamina = (c.curCharacterStat != null && c.curCharacterStat.maxStamina > 0f)
            ? c.curCharacterStat.maxStamina
            : Mathf.Max(1f, c.curStamina);

        statusBarUI.UpdateStamina(c.curStamina, maxStamina);
    }

    private void OnCharacterDied(Character c)
    {
        CloseEffectDetail();
        HideUI();
    }

    private void OnJoinedBattle(Character c)
    {
        ShowUI();
    }

    private void OnLeftBattle(Character c)
    {
        HideUI();
    }

    private void OnIntentChanged(EnemyIntentInfo intent)
    {
        if (!_isBattleActive || character == null || character.isDead) return;

        if (intentUI == null && character.faction == CharacterFaction.Enemy && UIManager.instance != null)
        {
            RequestViewsFromUIManager();
        }

        if (intentUI != null)
        {
            intentUI.SetIntent(intent);
            if (intent.isValid && intent.intentType != EnemyIntentType.None)
            {
                intentUI.Show();
            }
            else
            {
                intentUI.Hide();
            }
        }
    }

    private void OnEffectAdded(Effect effect)
    {
        if (!_isBattleActive) return;

        if (effectListUI != null)
        {
            effectListUI.AddEffect(effect);
        }

        if (_isEffectDetailOpen && effectDetailUI != null)
        {
            effectDetailUI.AddOrUpdateEffect(effect);
        }
    }

    private void OnEffectStacked(Effect effect)
    {
        if (!_isBattleActive) return;

        if (effectListUI != null)
        {
            effectListUI.UpdateEffect(effect);
        }

        if (_isEffectDetailOpen && effectDetailUI != null)
        {
            effectDetailUI.AddOrUpdateEffect(effect);
        }
    }

    private void OnEffectRemoved(Effect effect)
    {
        if (!_isBattleActive) return;

        if (effectListUI != null)
        {
            effectListUI.RemoveEffect(effect);
        }

        if (_isEffectDetailOpen && effectDetailUI != null)
        {
            effectDetailUI.RemoveEffect(effect);
            if (effectDetailUI.ActiveItemCount == 0)
            {
                CloseEffectDetail();
            }
        }
    }

    #endregion

    #region Public Controls

    /// <summary>
    /// 모든 UI 상태(체력, 스태미나, 활성 이펙트 목록)를 모델 상태에 맞추어 즉시 동기화합니다.
    /// </summary>
    public void RefreshAll()
    {
        if (character == null) return;

        if (statusBarUI != null)
        {
            float maxHp = (character.curCharacterStat != null && character.curCharacterStat.maxHealth > 0f)
                ? character.curCharacterStat.maxHealth
                : Mathf.Max(1f, character.curHealth);
            statusBarUI.UpdateHealth(character.curHealth, maxHp, character.shield);

            float maxStamina = (character.curCharacterStat != null && character.curCharacterStat.maxStamina > 0f)
                ? character.curCharacterStat.maxStamina
                : Mathf.Max(1f, character.curStamina);
            statusBarUI.UpdateStamina(character.curStamina, maxStamina);
        }

        if (effectListUI != null && character.effectManager != null)
        {
            effectListUI.ClearAll();
            var effects = character.effectManager.GetEffects();
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    effectListUI.AddEffect(effect);
                }
            }
        }

        if (intentUI != null)
        {
            intentUI.SetIntent(character.CurrentIntent);
            if (_isBattleActive && character.CurrentIntent.isValid && character.CurrentIntent.intentType != EnemyIntentType.None)
            {
                intentUI.Show();
            }
            else
            {
                intentUI.Hide();
            }
        }
    }

    #region Battle Lifecycle Events

    public void OnBattleStart()
    {
        ShowUI();
    }

    public void OnBattleEnd()
    {
        CloseEffectDetail();
        HideUI();
    }

    #endregion

    public void ShowUI()
    {
        _isBattleActive = true;
        bool missingIntentView = character != null && character.faction == CharacterFaction.Enemy && intentUI == null;
        if ((statusBarUI == null || effectListUI == null || missingIntentView) && UIManager.instance != null)
        {
            RequestViewsFromUIManager();
        }
        if (statusBarUI != null) statusBarUI.Show();
        if (effectListUI != null) effectListUI.Show();
        RefreshAll();
    }

    public void HideUI()
    {
        _isBattleActive = false;
        SetTimelineHighlight(false);
        CloseEffectDetail();
        if (statusBarUI != null) statusBarUI.Hide();
        if (effectListUI != null) effectListUI.Hide();
        if (intentUI != null) intentUI.Hide();
    }

    /// <summary>
    /// 타임라인 슬롯 호버 시 월드 캐릭터 상태바를 강조하거나 기본 크기로 복원합니다.
    /// </summary>
    public void SetTimelineHighlight(bool highlighted)
    {
        _isTimelineHighlighted = highlighted;
        if (statusBarUI != null)
        {
            statusBarUI.transform.localScale = highlighted ? new Vector3(1.18f, 1.18f, 1.18f) : Vector3.one;
        }
    }

    /// <summary>
    /// 이펙트 상세 툴팁 패널을 토글합니다.
    /// </summary>
    public bool ToggleEffectDetail()
    {
        if (_isEffectDetailOpen)
        {
            CloseEffectDetail();
            return false;
        }

        return OpenEffectDetail();
    }

    /// <summary>
    /// 현재 캐릭터의 활성 이펙트 상세 툴팁 패널을 엽니다. (활성 이펙트가 없으면 false 반환)
    /// </summary>
    public bool OpenEffectDetail()
    {
        if (!_isBattleActive || character == null || character.isDead)
        {
            return false;
        }

        // 다른 캐릭터의 상세 패널이 열려 있으면 먼저 닫습니다.
        if (_activeInspectedController != null && _activeInspectedController != this)
        {
            CloseActiveEffectDetail();
        }

        var effects = character.effectManager != null ? character.effectManager.GetEffects() : null;
        if (effects == null || effects.Count == 0)
        {
            CloseEffectDetail();
            return false;
        }

        if (effectDetailUI == null && UIManager.instance != null)
        {
            effectDetailUI = UIManager.instance.CreateEffectDetailUI(transform, effectDetailOffset);
        }

        if (effectDetailUI == null)
        {
            return false;
        }

        effectDetailUI.RebuildAll(effects);
        effectDetailUI.Show();

        _isEffectDetailOpen = true;
        _activeInspectedController = this;
        return true;
    }

    /// <summary>
    /// 이펙트 상세 툴팁 패널을 닫습니다.
    /// </summary>
    public void CloseEffectDetail()
    {
        _isEffectDetailOpen = false;
        if (_activeInspectedController == this)
        {
            _activeInspectedController = null;
        }

        if (effectDetailUI != null)
        {
            effectDetailUI.Hide();
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 basePos = transform.position;
        Vector3 statusPos = basePos + statusBarOffset;
        Vector3 effectPos = basePos + effectListOffset;

        // 1. 상태바 (체력/스태미나 바) 프리뷰
        // 캐릭터 중심 -> 상태바 연결선
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.7f);
        Gizmos.DrawLine(basePos, statusPos);

        // 체력바 형태의 와이어 박스 (가로 1.2, 세로 0.2, 두께 0.05)
        Gizmos.DrawWireCube(statusPos, new Vector3(1.2f, 0.2f, 0.05f));
        // 내부 반투명 채우기
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.2f);
        Gizmos.DrawCube(statusPos, new Vector3(1.2f, 0.2f, 0.05f));

        // 2. 이펙트 리스트 (버프/디버프 아이콘 슬롯) 프리뷰
        // 상태바 -> 이펙트 리스트 연결선
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.7f);
        Gizmos.DrawLine(statusPos, effectPos);

        // 버프 아이콘 슬롯 형태 (작은 구체 3개로 슬롯 느낌 표현)
        Gizmos.DrawWireSphere(effectPos + Vector3.left * 0.25f, 0.1f);
        Gizmos.DrawWireSphere(effectPos, 0.1f);
        Gizmos.DrawWireSphere(effectPos + Vector3.right * 0.25f, 0.1f);

        // 3. 씬 뷰 텍스트 라벨 표시
        GUIStyle labelStyle = new GUIStyle();
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontSize = 11;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        UnityEditor.Handles.Label(statusPos + Vector3.up * 0.18f, "Status Bar", labelStyle);
        UnityEditor.Handles.Label(effectPos + Vector3.up * 0.18f, "Effect List", labelStyle);
    }
#endif
}
