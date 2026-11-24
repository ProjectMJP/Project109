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

    private float _lastFillAmount = -1f;
    private const float UpdateThreshold = 0.01f;

    private bool _hasCachedNumericText;
    private int _lastCurValue;
    private int _lastMaxValue;
    private int _lastShieldValue;

    /// <summary>
    /// RefreshNumeric 호출 중 실제 문자열 포맷팅이 수행된 누적 횟수입니다. (핫패스 GC 무할당 검증용)
    /// </summary>
    public int NumericTextFormatCount { get; private set; }

    private void Awake()
    {
        if (gaugeImageFilled == null)
        {
            gaugeImageFilled = GetComponent<Image>();
        }

        EnsureGaugeTextResolved();
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
}

