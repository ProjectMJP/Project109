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

    private CharacterStatusBarUI statusBarUI;
    private CharacterEffectListUI effectListUI;
    private bool _isBattleActive = false;

    public Character Character => character;
    public CharacterStatusBarUI StatusBarUI => statusBarUI;
    public CharacterEffectListUI EffectListUI => effectListUI;

    private void Awake()
    {
        if (character == null)
        {
            character = GetComponent<Character>();
        }
    }

    private void Start()
    {
        RequestViewsFromUIManager();
        RegisterEvents();
        // 초기 생성 시(은신처 및 필드 탐색)에는 머리 위 UI를 숨겨 시야와 가시성을 확보하고, 전투 시에만 켭니다.
        HideUI();
    }

    private void OnDestroy()
    {
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
        }
    }

    private void LateUpdate()
    {
        // UIManager 타이밍 지연 대응: 뷰가 아직 생성되지 않은 경우 재시도
        if (statusBarUI == null && UIManager.instance != null)
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
        }

        if (effectListUI == null)
        {
            effectListUI = UIManager.instance.CreateEffectListUI(transform, effectListOffset);
        }
    }

    #region Event Registration

    private void RegisterEvents()
    {
        if (character == null) return;

        character.OnCharacterHealthChanged -= OnHealthChanged;
        character.OnCharacterHealthChanged += OnHealthChanged;

        character.OnCharacterShieldChanged -= OnShieldChanged;
        character.OnCharacterShieldChanged += OnShieldChanged;

        character.OnCharacterStaminaChanged -= OnStaminaChanged;
        character.OnCharacterStaminaChanged += OnStaminaChanged;

        character.OnCharacterDied -= OnCharacterDied;
        character.OnCharacterDied += OnCharacterDied;

        if (character.effectManager != null)
        {
            character.effectManager.OnEffectAdded -= OnEffectAdded;
            character.effectManager.OnEffectAdded += OnEffectAdded;

            character.effectManager.OnEffectStacked -= OnEffectStacked;
            character.effectManager.OnEffectStacked += OnEffectStacked;

            character.effectManager.OnEffectRemoved -= OnEffectRemoved;
            character.effectManager.OnEffectRemoved += OnEffectRemoved;
        }

        if (character.eventBus != null)
        {
            character.eventBus.Add<IOnBattleStart>(this);
            character.eventBus.Add<IOnBattleEnd>(this);
        }
    }

    private void UnregisterEvents()
    {
        if (character == null) return;

        character.OnCharacterHealthChanged -= OnHealthChanged;
        character.OnCharacterShieldChanged -= OnShieldChanged;
        character.OnCharacterStaminaChanged -= OnStaminaChanged;
        character.OnCharacterDied -= OnCharacterDied;

        if (character.effectManager != null)
        {
            character.effectManager.OnEffectAdded -= OnEffectAdded;
            character.effectManager.OnEffectStacked -= OnEffectStacked;
            character.effectManager.OnEffectRemoved -= OnEffectRemoved;
        }

        if (character.eventBus != null)
        {
            character.eventBus.Remove<IOnBattleStart>(this);
            character.eventBus.Remove<IOnBattleEnd>(this);
        }
    }

    #endregion

    #region Model Event Handlers

    private void OnHealthChanged(Character c)
    {
        if (statusBarUI == null || c == null) return;
        float maxHp = (c.curCharacterStat != null && c.curCharacterStat.maxHealth > 0f)
            ? c.curCharacterStat.maxHealth
            : Mathf.Max(1f, c.curHealth);

        statusBarUI.UpdateHealth(c.curHealth, maxHp, c.shield);
    }

    private void OnShieldChanged(Character c)
    {
        if (statusBarUI == null || c == null) return;
        float maxHp = (c.curCharacterStat != null && c.curCharacterStat.maxHealth > 0f)
            ? c.curCharacterStat.maxHealth
            : Mathf.Max(1f, c.curHealth);

        statusBarUI.UpdateHealth(c.curHealth, maxHp, c.shield);
    }

    private void OnStaminaChanged(Character c)
    {
        if (statusBarUI == null || c == null) return;
        float maxStamina = (c.curCharacterStat != null && c.curCharacterStat.maxStamina > 0f)
            ? c.curCharacterStat.maxStamina
            : Mathf.Max(1f, c.curStamina);

        statusBarUI.UpdateStamina(c.curStamina, maxStamina);
    }

    private void OnCharacterDied(Character c)
    {
        HideUI();
    }

    private void OnEffectAdded(Effect effect)
    {
        if (effectListUI != null)
        {
            effectListUI.AddEffect(effect);
        }
    }

    private void OnEffectStacked(Effect effect)
    {
        if (effectListUI != null)
        {
            effectListUI.UpdateEffect(effect);
        }
    }

    private void OnEffectRemoved(Effect effect)
    {
        if (effectListUI != null)
        {
            effectListUI.RemoveEffect(effect);
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

        OnHealthChanged(character);
        OnStaminaChanged(character);

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
    }

    #region Battle Lifecycle Events

    public void OnBattleStart()
    {
        ShowUI();
    }

    public void OnBattleEnd()
    {
        HideUI();
    }

    #endregion

    public void ShowUI()
    {
        _isBattleActive = true;
        if (statusBarUI != null) statusBarUI.Show();
        if (effectListUI != null) effectListUI.Show();
        RefreshAll();
    }

    public void HideUI()
    {
        _isBattleActive = false;
        if (statusBarUI != null) statusBarUI.Hide();
        if (effectListUI != null) effectListUI.Hide();
    }

    // 하위 호환성 메서드
    public void ShowStatusBar() => ShowUI();
    public void HideStatusBar() => HideUI();

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
