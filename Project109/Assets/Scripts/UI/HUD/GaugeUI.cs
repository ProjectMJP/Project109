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

    private void Awake()
    {
        if (gaugeImageFilled == null)
        {
            gaugeImageFilled = GetComponent<Image>();
        }

        if (gaugeText == null)
        {
            gaugeText = GetComponentInChildren<TextMeshProUGUI>();
            if (gaugeText == null && transform.parent != null)
            {
                gaugeText = transform.parent.GetComponentInChildren<TextMeshProUGUI>();
            }
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
    /// 게이지 텍스트를 지정한 문자열로 변경합니다.
    /// </summary>
    public void SetText(string textValue)
    {
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

