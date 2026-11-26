using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class GaugeUI : MonoBehaviour
{
    [SerializeField] 
    private Image gaugeImageFilled;

    [SerializeField]
    private TextMeshProUGUI gaugeText;

    [Header("Damage Preview Ghost Bar")]
    [SerializeField]
    private Image damagePreviewOverlayImage;

    [SerializeField]
    private Color damagePreviewColor = new Color(1f, 0.88f, 0.28f, 0.92f);

    [SerializeField]
    private Color lethalPreviewColor = new Color(1f, 0.26f, 0.22f, 0.96f);

    [SerializeField]
    private float previewPulseSpeed = 6.5f;

    private float _lastFillAmount = -1f;
    private const float UpdateThreshold = 0.01f;

    private bool _hasCachedNumericText;
    private int _lastCurValue;
    private int _lastMaxValue;
    private int _lastShieldValue;

    private bool _isDamagePreviewActive;
    private bool _isLethalPreview;
    private float _previewStartRate;
    private float _previewEndRate;
    private Color _activePreviewBaseColor;

    /// <summary>
    /// RefreshNumeric 호출 중 실제 문자열 포맷팅이 수행된 누적 횟수입니다. (핫패스 GC 무할당 검증용)
    /// </summary>
    public int NumericTextFormatCount { get; private set; }

    public bool IsDamagePreviewActive => _isDamagePreviewActive;
    public bool IsLethalPreview => _isLethalPreview;
    public float DamagePreviewStartRate => _previewStartRate;
    public float DamagePreviewEndRate => _previewEndRate;
    public Image DamagePreviewOverlayImage => damagePreviewOverlayImage;
    public string CurrentDisplayText => gaugeText != null ? gaugeText.text : string.Empty;

    private void Awake()
    {
        if (gaugeImageFilled == null)
        {
            gaugeImageFilled = GetComponent<Image>();
        }

        EnsureGaugeTextResolved();
        EnsureDamagePreviewOverlayResolved();
        ClearDamagePreview();
    }

    private void Update()
    {
        if (!_isDamagePreviewActive || damagePreviewOverlayImage == null || !damagePreviewOverlayImage.gameObject.activeSelf)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * previewPulseSpeed);
        Color c = _activePreviewBaseColor;
        c.a = _activePreviewBaseColor.a * Mathf.Lerp(0.55f, 1.0f, pulse);
        damagePreviewOverlayImage.color = c;
    }

    private void EnsureDamagePreviewOverlayResolved()
    {
        if (damagePreviewOverlayImage == null)
        {
            Transform tr = transform.Find("DamagePreviewFill");
            if (tr != null)
            {
                damagePreviewOverlayImage = tr.GetComponent<Image>();
            }
        }
    }

    private void EnsureGaugeTextResolved()
    {
        if (gaugeText == null)
        {
            gaugeText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (gaugeText == null && transform.parent != null)
            {
                gaugeText = transform.parent.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }
    }

    /// <summary>
    /// 게이지 수치 텍스트의 활성/비활성 표시 여부를 설정합니다.
    /// </summary>
    public void SetTextVisible(bool visible)
    {
        EnsureGaugeTextResolved();
        if (gaugeText != null)
        {
            gaugeText.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 게이지 채우기 비율만 갱신합니다.
    /// </summary>
    public void Refresh(float fillRate) 
    {
        if (gaugeImageFilled == null)
        {
            gaugeImageFilled = GetComponent<Image>();
        }

        if (Mathf.Abs(fillRate - _lastFillAmount) >= UpdateThreshold || fillRate == 0f || fillRate == 1f)
        {
            _lastFillAmount = fillRate;
            if (gaugeImageFilled != null)
            {
                gaugeImageFilled.fillAmount = fillRate;
            }
        }
    }

    /// <summary>
    /// 게이지 채우기 비율과 함께 텍스트 값을 업데이트합니다.
    /// </summary>
    public void Refresh(float fillRate, string textValue)
    {
        Refresh(fillRate);
        SetText(textValue);
    }

    /// <summary>
    /// 게이지 채우기 비율을 갱신하고, 표시 정수 수치가 변동되었을 때만 문자열을 생성해 텍스트를 갱신합니다.
    /// BattleTick 등 매 프레임 호출되는 핫패스에서 불필요한 문자열 힙 할당을 방지합니다.
    /// </summary>
    public void RefreshNumeric(float fillRate, int curValue, int maxValue, int shieldValue = 0)
    {
        Refresh(fillRate);
        EnsureGaugeTextResolved();
        if (gaugeText == null)
        {
            return;
        }

        int effectiveShield = shieldValue > 0 ? shieldValue : 0;
        if (_hasCachedNumericText &&
            _lastCurValue == curValue &&
            _lastMaxValue == maxValue &&
            _lastShieldValue == effectiveShield)
        {
            return;
        }

        _hasCachedNumericText = true;
        _lastCurValue = curValue;
        _lastMaxValue = maxValue;
        _lastShieldValue = effectiveShield;
        NumericTextFormatCount++;

        gaugeText.text = effectiveShield > 0
            ? $"({effectiveShield}) {curValue} / {maxValue}"
            : $"{curValue} / {maxValue}";
    }

    /// <summary>
    /// 게이지 텍스트를 지정한 문자열로 변경합니다.
    /// </summary>
    public void SetText(string textValue)
    {
        _hasCachedNumericText = false;
        EnsureGaugeTextResolved();
        if (gaugeText != null)
        {
            gaugeText.text = textValue;
        }
    }

    /// <summary>
    /// 게이지 이미지의 색상을 지정합니다. (예: 방어막 시 파란색, 기본은 빨간색)
    /// </summary>
    public void SetGaugeColor(Color color)
    {
        if (gaugeImageFilled != null)
        {
            gaugeImageFilled.color = color;
        }
    }

    /// <summary>
    /// 현재 체력/실드와 예상 피해를 바탕으로 게이지 깎임 비율과 잔여 수치·처치 여부를 계산합니다.
    /// </summary>
    public static void ComputeDamagePreviewMetrics(
        float curHealth,
        float maxHealth,
        float shield,
        int expectedDamage,
        out float currentRate,
        out float remainingRate,
        out int remainingHpInt,
        out int hpDamageInt,
        out int shieldDamageInt,
        out bool isLethal)
    {
        float safeMax = maxHealth > 0f ? maxHealth : Mathf.Max(1f, curHealth);
        float safeCur = Mathf.Max(0f, curHealth);
        float safeShield = Mathf.Max(0f, shield);
        float dmg = Mathf.Max(0f, expectedDamage);

        float absorbedByShield = Mathf.Min(safeShield, dmg);
        float hpLoss = Mathf.Max(0f, dmg - absorbedByShield);
        float remainingHp = Mathf.Max(0f, safeCur - hpLoss);

        currentRate = Mathf.Clamp01(safeCur / safeMax);
        remainingRate = Mathf.Clamp01(remainingHp / safeMax);

        int curHpInt = (safeCur >= safeMax) ? Mathf.RoundToInt(safeCur) : Mathf.CeilToInt(safeCur);
        int shieldInt = safeShield > 0f ? Mathf.CeilToInt(safeShield) : 0;
        int clampedDmgInt = Mathf.Max(0, expectedDamage);

        shieldDamageInt = Mathf.Min(shieldInt, clampedDmgInt);
        hpDamageInt = Mathf.Max(0, clampedDmgInt - shieldDamageInt);
        remainingHpInt = Mathf.Max(0, curHpInt - hpDamageInt);
        isLethal = safeCur > 0f && remainingHp <= 0.0001f;
    }

    /// <summary>
    /// 체력바 내부에 예상 피해로 깎일 구간(Ghost/Preview Bar)을 표시합니다.
    /// </summary>
    public void SetDamagePreview(
        float curHealth,
        float maxHealth,
        float shield,
        int expectedDamage,
        out int remainingHpInt,
        out int hpDamageInt,
        out int shieldDamageInt,
        out bool isLethal)
    {
        ComputeDamagePreviewMetrics(
            curHealth,
            maxHealth,
            shield,
            expectedDamage,
            out float currentRate,
            out float remainingRate,
            out remainingHpInt,
            out hpDamageInt,
            out shieldDamageInt,
            out isLethal);

        EnsureDamagePreviewOverlayResolved();

        _isLethalPreview = isLethal;
        _previewStartRate = remainingRate;
        _previewEndRate = currentRate;

        if (currentRate > remainingRate + 0.0005f)
        {
            _isDamagePreviewActive = true;
            _activePreviewBaseColor = isLethal ? lethalPreviewColor : damagePreviewColor;

            if (damagePreviewOverlayImage != null)
            {
                RectTransform rt = damagePreviewOverlayImage.rectTransform;
                rt.anchorMin = new Vector2(remainingRate, 0f);
                rt.anchorMax = new Vector2(currentRate, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                damagePreviewOverlayImage.color = _activePreviewBaseColor;
                damagePreviewOverlayImage.gameObject.SetActive(true);
            }
        }
        else
        {
            _isDamagePreviewActive = false;
            if (damagePreviewOverlayImage != null)
            {
                damagePreviewOverlayImage.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 체력바 내부 예상 피해 깎임 구간 표시를 숨깁니다.
    /// </summary>
    public void ClearDamagePreview()
    {
        _isDamagePreviewActive = false;
        _isLethalPreview = false;
        _previewStartRate = 0f;
        _previewEndRate = 0f;

        EnsureDamagePreviewOverlayResolved();
        if (damagePreviewOverlayImage != null)
        {
            damagePreviewOverlayImage.gameObject.SetActive(false);
        }
    }
}

