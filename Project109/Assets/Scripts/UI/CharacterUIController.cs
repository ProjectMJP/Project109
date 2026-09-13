using UnityEngine;

/// <summary>
/// 캐릭터 모델(Character, EffectManager)의 상태 변화 이벤트를 감지하여
/// UIManager에 요청하여 획득한 2개의 독립 머리 위 뷰(CharacterStatusBarUI, CharacterEffectListUI)에 데이터를 갱신하는 컨트롤러입니다.
/// (프리팹 소유 및 생성/소멸 관리는 UIManager가 전담합니다.)
/// </summary>
public class CharacterUIController : MonoBehaviour
{
    [Header("Model Reference")]
    [SerializeField] private Character character;

    [Header("World Position Offsets")]
    [SerializeField] private Vector3 statusBarOffset = UIConstants.DEFAULT_STATUS_BAR_OFFSET;
    [SerializeField] private Vector3 effectListOffset = UIConstants.DEFAULT_EFFECT_LIST_OFFSET;

    private CharacterStatusBarUI statusBarUI;
    private CharacterEffectListUI effectListUI;

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
        RefreshAll();
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
        // 버프/디버프 쿨다운 게이지 링 갱신
        if (effectListUI != null)
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

    public void ShowUI()
    {
        if (statusBarUI != null) statusBarUI.Show();
        if (effectListUI != null) effectListUI.Show();
        RefreshAll();
    }

    public void HideUI()
    {
        if (statusBarUI != null) statusBarUI.Hide();
        if (effectListUI != null) effectListUI.Hide();
    }

    // 하위 호환성 메서드
    public void ShowStatusBar() => ShowUI();
    public void HideStatusBar() => HideUI();

    #endregion
}
