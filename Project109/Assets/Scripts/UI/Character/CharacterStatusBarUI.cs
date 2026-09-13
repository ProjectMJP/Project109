using UnityEngine;

/// <summary>
/// 캐릭터의 체력/방어막/스태미나 게이지(GaugeUI) 및 위치 추적을 전담하는 순수 상태바 View 컴포넌트입니다.
/// </summary>
public class CharacterStatusBarUI : MonoBehaviour
{
    [Header("Target & Position Settings")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Vector3 worldOffset = UIConstants.DEFAULT_STATUS_BAR_OFFSET;
    [SerializeField] private bool useBillboard = true;

    [Header("Health & Shield Gauge")]
    [SerializeField] private GaugeUI healthGaugeUI;
    [SerializeField] private Color healthColor = UIConstants.COLOR_HEALTH;
    [SerializeField] private Color shieldColor = UIConstants.COLOR_SHIELD;

    [Header("Stamina Gauge")]
    [SerializeField] private GaugeUI staminaGaugeUI;
    [SerializeField] private Color staminaColor = UIConstants.COLOR_STAMINA;

    private Camera cachedCamera;

    public Transform TargetTransform => targetTransform;
    public Vector3 WorldOffset
    {
        get => worldOffset;
        set => worldOffset = value;
    }

    private void Awake()
    {
        cachedCamera = Camera.main;

        if (healthGaugeUI != null) healthGaugeUI.SetGaugeColor(healthColor);
        if (staminaGaugeUI != null) staminaGaugeUI.SetGaugeColor(staminaColor);
    }

    /// <summary>
    /// 상태바가 추적할 타깃 트랜스폼 및 오프셋을 설정합니다.
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
    /// 체력 및 실드 게이지의 채우기 비율, 색상, 수치 텍스트를 갱신합니다.
    /// </summary>
    public void UpdateHealth(float curHealth, float maxHealth, float shield)
    {
        if (healthGaugeUI == null) return;

        float rate = (maxHealth > 0f) ? Mathf.Clamp01(curHealth / maxHealth) : 0f;
        int curHp = Mathf.CeilToInt(curHealth);
        int maxHp = Mathf.CeilToInt(maxHealth);
        int shieldVal = Mathf.CeilToInt(shield);

        string text = (shield > 0f) ? $"({shieldVal}) {curHp} / {maxHp}" : $"{curHp} / {maxHp}";

        // 실드 보유 시 색상 전환 (실드: 파랑, 기본: 빨강)
        healthGaugeUI.SetGaugeColor(shield > 0f ? shieldColor : healthColor);
        healthGaugeUI.Refresh(rate, text);
    }

    /// <summary>
    /// 스태미나 게이지의 채우기 비율 및 수치 텍스트를 갱신합니다.
    /// </summary>
    public void UpdateStamina(float curStamina, float maxStamina)
    {
        if (staminaGaugeUI == null) return;

        float rate = (maxStamina > 0f) ? Mathf.Clamp01(curStamina / maxStamina) : 0f;
        string text = $"{Mathf.CeilToInt(curStamina)} / {Mathf.CeilToInt(maxStamina)}";

        staminaGaugeUI.Refresh(rate, text);
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

        // 2. Orthographic 뷰 최적화: 카메라의 전방 벡터를 반전하여 UI가 카메라 렌즈를 정면으로 바라보도록 회전
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
