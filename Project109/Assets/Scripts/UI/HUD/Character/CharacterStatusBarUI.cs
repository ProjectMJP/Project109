using EventStructs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터의 체력/방어막/스태미나 게이지(GaugeUI) 및 위치 추적을 전담하는 순수 상태바 View 컴포넌트입니다.
/// </summary>
public class CharacterStatusBarUI : MonoBehaviour, IWorldFollowUI
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

    [Header("Display Options")]
    [SerializeField] private bool showValueText = false;

    [Header("Damage Preview")]
    [SerializeField] private GameObject damagePreviewContainer;
    [SerializeField] private Image damagePreviewBackground;
    [SerializeField] private TextMeshProUGUI damagePreviewText;
    [SerializeField] private GameObject facingBadgeContainer;
    [SerializeField] private Image facingBadgeBackground;
    [SerializeField] private TextMeshProUGUI facingBadgeText;
    [SerializeField] private Color normalDamagePreviewBgColor = new Color(0.08f, 0.08f, 0.12f, 0.90f);
    [SerializeField] private Color normalDamagePreviewTextColor = new Color(1f, 0.88f, 0.85f, 1f);
    [SerializeField] private Color backstabDamagePreviewBgColor = new Color(0.42f, 0.09f, 0.09f, 0.95f);
    [SerializeField] private Color backstabDamagePreviewTextColor = new Color(1f, 0.86f, 0.30f, 1f);
    [SerializeField] private Color backstabBadgeBgColor = new Color(0.95f, 0.52f, 0.08f, 0.96f);
    [SerializeField] private Color backstabBadgeTextColor = new Color(1f, 0.98f, 0.92f, 1f);

    private Camera cachedCamera;

    public Transform TargetTransform => targetTransform;
    public bool ShowValueText => showValueText;
    public bool IsDamagePreviewVisible => damagePreviewContainer != null && damagePreviewContainer.activeSelf;
    public bool IsFacingBadgeVisible => facingBadgeContainer != null && facingBadgeContainer.activeSelf;
    public string DamagePreviewDisplayText => damagePreviewText != null ? damagePreviewText.text : string.Empty;
    public string FacingBadgeDisplayText => facingBadgeText != null ? facingBadgeText.text : string.Empty;
    public Vector3 WorldOffset
    {
        get => worldOffset;
        set => worldOffset = value;
    }

    private void Awake()
    {
        cachedCamera = Camera.main;

        // 프리팹 직렬화 바인딩 검증 (Fail-Fast)
        if (healthGaugeUI == null)
        {
            Debug.LogError("[CharacterStatusBarUI] healthGaugeUI가 바인딩되지 않았습니다! CharacterStatusBarUI 프리팹 설정을 확인하세요.", this);
        }
        else
        {
            healthGaugeUI.SetGaugeColor(healthColor);
            healthGaugeUI.SetTextVisible(showValueText);
        }

        if (staminaGaugeUI == null)
        {
            Debug.LogError("[CharacterStatusBarUI] staminaGaugeUI가 바인딩되지 않았습니다! CharacterStatusBarUI 프리팹 설정을 확인하세요.", this);
        }
        else
        {
            staminaGaugeUI.SetGaugeColor(staminaColor);
            staminaGaugeUI.SetTextVisible(showValueText);
        }

        ClearDamagePreview();
    }

    /// <summary>
    /// 상태바 게이지 수치 텍스트 표시 여부를 동적으로 전환합니다.
    /// </summary>
    public void SetShowValueText(bool visible)
    {
        showValueText = visible;
        if (healthGaugeUI != null)
        {
            healthGaugeUI.SetTextVisible(visible);
        }
        if (staminaGaugeUI != null)
        {
            staminaGaugeUI.SetTextVisible(visible);
        }
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
    /// 체력 및 실드 게이지의 채우기 비율, 색상, (옵션) 수치 텍스트를 갱신합니다.
    /// </summary>
    public void UpdateHealth(float curHealth, float maxHealth, float shield)
    {
        if (healthGaugeUI == null) return;

        float rate = (maxHealth > 0f) ? Mathf.Clamp01(curHealth / maxHealth) : 0f;

        // 실드 보유 시 색상 전환 (실드: 파랑, 기본: 빨강)
        healthGaugeUI.SetGaugeColor(shield > 0f ? shieldColor : healthColor);

        if (showValueText)
        {
            int curHp = (curHealth >= maxHealth) ? Mathf.RoundToInt(curHealth) : Mathf.CeilToInt(curHealth);
            int maxHp = Mathf.RoundToInt(maxHealth);
            int shieldVal = shield > 0f ? Mathf.CeilToInt(shield) : 0;
            healthGaugeUI.RefreshNumeric(rate, curHp, maxHp, shieldVal);
        }
        else
        {
            healthGaugeUI.Refresh(rate);
        }
    }

    /// <summary>
    /// 스태미나 게이지의 채우기 비율 및 (옵션) 수치 텍스트를 갱신합니다.
    /// </summary>
    public void UpdateStamina(float curStamina, float maxStamina)
    {
        if (staminaGaugeUI == null) return;

        float rate = (maxStamina > 0f) ? Mathf.Clamp01(curStamina / maxStamina) : 0f;

        if (showValueText)
        {
            int displayStamina = (curStamina >= maxStamina) ? Mathf.RoundToInt(curStamina) : Mathf.CeilToInt(curStamina);
            int displayMaxStamina = Mathf.RoundToInt(maxStamina);
            staminaGaugeUI.RefreshNumeric(rate, displayStamina, displayMaxStamina);
        }
        else
        {
            staminaGaugeUI.Refresh(rate);
        }
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    /// <summary>
    /// 직교(Orthographic) 카메라 시선 반대 방향으로 최소 높이 및 전방 깊이 바이어스를 적용해
    /// 바닥·자기 발에는 파묻히지 않고 앞쪽 캐릭터 몸체에는 정상 가려지는 월드 좌표를 계산합니다.
    /// </summary>
    public static Vector3 ComputeDepthSafeWorldPosition(Vector3 targetPosition, Vector3 offset, Camera cam, float minSurfaceHeight = 0.16f, float minCameraPullDistance = 0.45f)
    {
        Vector3 pos = targetPosition + offset;
        if (cam == null)
        {
            return pos;
        }

        Vector3 camForward = cam.transform.forward;
        float liftDist = 0f;
        float minSurfaceY = targetPosition.y + minSurfaceHeight;
        if (pos.y < minSurfaceY && camForward.y < -0.05f)
        {
            liftDist = (minSurfaceY - pos.y) / -camForward.y;
        }

        float pullDist = Mathf.Max(liftDist, minCameraPullDistance);
        if (pullDist > 0f)
        {
            pos -= camForward * pullDist;
        }

        return pos;
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

        // 1. 타깃 월드 위치 + 오프셋 반영 (바닥·자기 발 클리핑 방지 및 앞쪽 캐릭터 깊이 테스트 보장)
        transform.position = ComputeDepthSafeWorldPosition(targetTransform.position, worldOffset, cachedCamera);

        // 2. 카메라의 회전과 완전히 일치시켜 UI가 거울 반전이나 컬링 없이 항상 정면으로 보이도록 유지
        if (useBillboard)
        {
            transform.rotation = cachedCamera.transform.rotation;
        }
    }

    /// <summary>
    /// 방향 보정이 반영된 예상 피해 수치 및 정후면 강조 배지를 표시하거나 숨깁니다.
    /// </summary>
    public void SetDamagePreview(in DamagePreviewInfo preview)
    {
        if (!preview.isValid)
        {
            ClearDamagePreview();
            return;
        }

        bool isBackstab = preview.IsBackstab;

        if (damagePreviewContainer != null)
        {
            damagePreviewContainer.SetActive(true);
            damagePreviewContainer.transform.SetAsLastSibling();
        }

        if (damagePreviewBackground != null)
        {
            damagePreviewBackground.color = isBackstab ? backstabDamagePreviewBgColor : normalDamagePreviewBgColor;
        }

        if (damagePreviewText != null)
        {
            damagePreviewText.color = isBackstab ? backstabDamagePreviewTextColor : normalDamagePreviewTextColor;
            damagePreviewText.text = $"-{preview.expectedDamage}";
        }

        if (facingBadgeContainer != null)
        {
            facingBadgeContainer.SetActive(isBackstab);
            if (isBackstab)
            {
                facingBadgeContainer.transform.SetAsLastSibling();
            }
        }

        if (isBackstab)
        {
            if (facingBadgeBackground != null)
            {
                facingBadgeBackground.color = backstabBadgeBgColor;
            }
            if (facingBadgeText != null)
            {
                facingBadgeText.color = backstabBadgeTextColor;
                facingBadgeText.text = $"정후면 {preview.facingMultiplier:0.#}x";
            }
        }
    }

    /// <summary>
    /// 예상 피해 미리보기 수치와 방향 배지를 숨깁니다.
    /// </summary>
    public void ClearDamagePreview()
    {
        if (damagePreviewContainer != null)
        {
            damagePreviewContainer.SetActive(false);
        }

        if (facingBadgeContainer != null)
        {
            facingBadgeContainer.SetActive(false);
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        ClearDamagePreview();
        gameObject.SetActive(false);
    }
}
