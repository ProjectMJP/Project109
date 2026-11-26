using System.Collections.Generic;
using EventStructs;
using TMPro;
using UnityEngine;

/// <summary>
/// 캐릭터의 체력/방어막/스태미나 게이지(GaugeUI) 및 위치 추적을 전담하는 순수 상태바 View 컴포넌트입니다.
/// </summary>
public class CharacterStatusBarUI : MonoBehaviour, IWorldFollowUI
{
    private static readonly Vector2 DefaultDamagePreviewAnchoredPos = new Vector2(0f, 82f);
    private static readonly Vector2 DefaultFacingBadgeAnchoredPos = new Vector2(0f, 126f);
    private static readonly Dictionary<Material, Material> _sharedOutlineMaterials = new Dictionary<Material, Material>();

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
    [SerializeField] private TextMeshProUGUI damagePreviewText;
    [SerializeField] private GameObject lethalIndicatorContainer;
    [SerializeField] private TextMeshProUGUI lethalIndicatorText;
    [SerializeField] private GameObject facingBadgeContainer;
    [SerializeField] private TextMeshProUGUI facingBadgeText;
    [SerializeField] private Color normalDamagePreviewTextColor = new Color(1f, 0.94f, 0.88f, 1f);
    [SerializeField] private Color backstabDamagePreviewTextColor = new Color(1f, 0.82f, 0.22f, 1f);
    [SerializeField] private Color lethalDamagePreviewTextColor = new Color(1f, 0.30f, 0.24f, 1f);
    [SerializeField] private Color backstabBadgeTextColor = new Color(1f, 0.72f, 0.14f, 1f);
    [SerializeField] private Color32 worldOutlineColor = new Color32(14, 10, 18, 255);
    [SerializeField] private float worldOutlineWidth = 0.26f;
    [SerializeField] private Vector3 bodyCenterWorldOffset = new Vector3(0f, 0.92f, 0f);
    [SerializeField] private Vector3 facingBadgeWorldOffset = new Vector3(0f, 1.43f, 0f);

    private Camera cachedCamera;
    private float _lastCurHealth;
    private float _lastMaxHealth;
    private float _lastShield;
    private bool _hasCachedHealth;
    private DamagePreviewInfo _currentDamagePreview;

    public Transform TargetTransform => targetTransform;
    public bool ShowValueText => showValueText;
    public bool IsDamagePreviewVisible => damagePreviewContainer != null && damagePreviewContainer.activeSelf;
    public bool IsLethalPreviewVisible => lethalIndicatorContainer != null && lethalIndicatorContainer.activeSelf;
    public bool IsFacingBadgeVisible => facingBadgeContainer != null && facingBadgeContainer.activeSelf;
    public string DamagePreviewDisplayText => damagePreviewText != null ? damagePreviewText.text : string.Empty;
    public string LethalIndicatorDisplayText => lethalIndicatorText != null ? lethalIndicatorText.text : string.Empty;
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

        ApplyWorldOutline(damagePreviewText);
        ApplyWorldOutline(lethalIndicatorText);
        ApplyWorldOutline(facingBadgeText);

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
        _lastCurHealth = curHealth;
        _lastMaxHealth = maxHealth;
        _lastShield = shield;
        _hasCachedHealth = true;

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

        if (_currentDamagePreview.isValid)
        {
            ApplyDamagePreviewVisuals();
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

        if (IsDamagePreviewVisible || IsFacingBadgeVisible)
        {
            UpdatePreviewChildPositions();
        }
    }

    /// <summary>
    /// 캐릭터 높이에 맞춘 몸통 중앙 예상 피해 및 인텐트 하단 BACK ATTACK 월드 오프셋을 설정합니다.
    /// </summary>
    public void SetPreviewWorldOffsets(Vector3 bodyCenterOffset, Vector3 facingOffset)
    {
        bodyCenterWorldOffset = bodyCenterOffset;
        facingBadgeWorldOffset = facingOffset;

        if (IsDamagePreviewVisible || IsFacingBadgeVisible)
        {
            UpdatePreviewChildPositions();
        }
    }

    /// <summary>
    /// 방향 보정이 반영된 예상 피해 수치, 체력바 깎임 구간 및 BACK ATTACK 외곽선 텍스트를 표시하거나 숨깁니다.
    /// </summary>
    public void SetDamagePreview(in DamagePreviewInfo preview)
    {
        _currentDamagePreview = preview;
        if (!preview.isValid)
        {
            ClearDamagePreview();
            return;
        }

        ApplyDamagePreviewVisuals();
    }

    private void ApplyDamagePreviewVisuals()
    {
        bool isBackstab = _currentDamagePreview.IsBackstab;
        bool isLethal = false;

        if (healthGaugeUI != null && _hasCachedHealth)
        {
            healthGaugeUI.SetDamagePreview(
                _lastCurHealth,
                _lastMaxHealth,
                _lastShield,
                _currentDamagePreview.expectedDamage,
                out _,
                out _,
                out _,
                out isLethal);
        }

        if (damagePreviewContainer != null)
        {
            damagePreviewContainer.SetActive(true);
            damagePreviewContainer.transform.SetAsLastSibling();
        }

        if (damagePreviewText != null)
        {
            damagePreviewText.color = isLethal
                ? lethalDamagePreviewTextColor
                : (isBackstab ? backstabDamagePreviewTextColor : normalDamagePreviewTextColor);
            damagePreviewText.text = $"-{_currentDamagePreview.expectedDamage}";
        }

        if (lethalIndicatorContainer != null)
        {
            lethalIndicatorContainer.SetActive(isLethal);
        }

        if (isLethal && lethalIndicatorText != null)
        {
            lethalIndicatorText.color = lethalDamagePreviewTextColor;
            lethalIndicatorText.text = "KILL";
        }

        if (facingBadgeContainer != null)
        {
            facingBadgeContainer.SetActive(isBackstab);
            if (isBackstab)
            {
                facingBadgeContainer.transform.SetAsLastSibling();
            }
        }

        if (isBackstab && facingBadgeText != null)
        {
            facingBadgeText.color = backstabBadgeTextColor;
            facingBadgeText.text = "BACK ATTACK";
        }

        UpdatePreviewChildPositions();
    }

    /// <summary>
    /// 예상 피해 미리보기 수치, 체력바 깎임 구간, 방향 텍스트를 모두 숨깁니다.
    /// </summary>
    public void ClearDamagePreview()
    {
        _currentDamagePreview = default;

        if (damagePreviewContainer != null)
        {
            damagePreviewContainer.SetActive(false);
        }

        if (lethalIndicatorContainer != null)
        {
            lethalIndicatorContainer.SetActive(false);
        }

        if (facingBadgeContainer != null)
        {
            facingBadgeContainer.SetActive(false);
        }

        if (healthGaugeUI != null)
        {
            healthGaugeUI.ClearDamagePreview();
        }
    }

    private void UpdatePreviewChildPositions()
    {
        RectTransform dmgRect = damagePreviewContainer != null ? damagePreviewContainer.transform as RectTransform : null;
        RectTransform badgeRect = facingBadgeContainer != null ? facingBadgeContainer.transform as RectTransform : null;
        if (dmgRect == null && badgeRect == null)
        {
            return;
        }

        if (targetTransform != null && cachedCamera != null && Mathf.Abs(transform.up.y) >= 0.15f)
        {
            Vector3 lossyScale = transform.lossyScale;
            float scaleX = Mathf.Abs(lossyScale.x) > 0.0001f ? lossyScale.x : 1f;
            float scaleY = Mathf.Abs(lossyScale.y) > 0.0001f ? lossyScale.y : 1f;
            Vector3 origin = transform.position;
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            if (dmgRect != null)
            {
                Vector3 delta = (targetTransform.position + bodyCenterWorldOffset) - origin;
                dmgRect.anchoredPosition = new Vector2(
                    Vector3.Dot(delta, right) / scaleX,
                    Vector3.Dot(delta, up) / scaleY);
            }

            if (badgeRect != null)
            {
                Vector3 delta = (targetTransform.position + facingBadgeWorldOffset) - origin;
                badgeRect.anchoredPosition = new Vector2(
                    Vector3.Dot(delta, right) / scaleX,
                    Vector3.Dot(delta, up) / scaleY);
            }
        }
        else
        {
            if (dmgRect != null)
            {
                dmgRect.anchoredPosition = DefaultDamagePreviewAnchoredPos;
            }
            if (badgeRect != null)
            {
                badgeRect.anchoredPosition = DefaultFacingBadgeAnchoredPos;
            }
        }
    }

    private void ApplyWorldOutline(TextMeshProUGUI tmp)
    {
        if (tmp == null || tmp.font == null)
        {
            return;
        }

        Material baseMat = tmp.font.material;
        if (baseMat == null)
        {
            return;
        }

        if (!_sharedOutlineMaterials.TryGetValue(baseMat, out Material outlineMat) || outlineMat == null)
        {
            outlineMat = new Material(baseMat)
            {
                name = baseMat.name + "_WorldPreviewOutline",
                hideFlags = HideFlags.HideAndDontSave
            };
            outlineMat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            if (outlineMat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                outlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, worldOutlineWidth);
            }
            if (outlineMat.HasProperty(ShaderUtilities.ID_OutlineColor))
            {
                outlineMat.SetColor(ShaderUtilities.ID_OutlineColor, worldOutlineColor);
            }
            if (outlineMat.HasProperty(ShaderUtilities.ID_FaceDilate))
            {
                outlineMat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.08f);
            }

            _sharedOutlineMaterials[baseMat] = outlineMat;
        }

        tmp.fontSharedMaterial = outlineMat;
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
