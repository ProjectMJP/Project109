using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 월드 이펙트 상세 툴팁 내부에 표시되는 개별 버프/디버프 항목(확대 아이콘 + 이름 + 설명) View 컴포넌트입니다.
/// </summary>
public class CharacterEffectDetailItemUI : MonoBehaviour
{
    [Header("UI Component References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image outlineBorderImage;
    [SerializeField] private Image durationFillImage;
    [SerializeField] private TextMeshProUGUI stackText;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Color Settings")]
    [SerializeField] private Color buffBorderColor = new Color(0.2f, 0.8f, 1.0f, 1.0f);
    [SerializeField] private Color debuffBorderColor = new Color(0.9f, 0.2f, 0.3f, 1.0f);

    private Effect targetEffect;

    public Effect TargetEffect => targetEffect;

    private void Awake()
    {
        if (durationFillImage != null)
        {
            durationFillImage.fillClockwise = false;
        }
    }

    /// <summary>
    /// 이펙트 데이터를 바인딩하고 아이콘·수치·설명을 갱신합니다.
    /// </summary>
    public void Setup(Effect effect)
    {
        targetEffect = effect;
        Refresh();
    }

    /// <summary>
    /// 바인딩된 이펙트의 아이콘, 테두리 색상, 스택/지속시간, 헤더·본문 텍스트를 최신 상태로 갱신합니다.
    /// </summary>
    public void Refresh()
    {
        if (targetEffect == null || targetEffect.Data == null) return;

        // 1. 아이콘 스프라이트 갱신
        if (iconImage != null)
        {
            if (targetEffect.Data.iconSprite != null)
            {
                iconImage.sprite = targetEffect.Data.iconSprite;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        // 2. 버프/디버프 테두리 색상 갱신
        if (outlineBorderImage != null)
        {
            outlineBorderImage.color = (targetEffect.Data.effectType == EffectType.Buff) ? buffBorderColor : debuffBorderColor;
        }

        // 3. 시계 유형(EffectClockType)별 스택 및 지속시간 게이지 갱신
        EffectClockType clockType = targetEffect.Data.clockType;
        if (stackText != null)
        {
            if (clockType == EffectClockType.Turn)
            {
                if (targetEffect.currentStack >= 1)
                {
                    stackText.text = $"{targetEffect.currentStack}T";
                    stackText.gameObject.SetActive(true);
                }
                else
                {
                    stackText.gameObject.SetActive(false);
                }
            }
            else if (targetEffect.currentStack > 1)
            {
                stackText.text = targetEffect.currentStack.ToString();
                stackText.gameObject.SetActive(true);
            }
            else
            {
                stackText.gameObject.SetActive(false);
            }
        }

        bool isTimedNonPermanent = clockType == EffectClockType.Time
            && !targetEffect.Data.isPermanent
            && targetEffect.duration > 0f;

        if (durationFillImage != null)
        {
            if (isTimedNonPermanent)
            {
                durationFillImage.fillAmount = Mathf.Clamp01(targetEffect.currentDuration / targetEffect.duration);
                durationFillImage.gameObject.SetActive(true);
            }
            else
            {
                durationFillImage.gameObject.SetActive(false);
            }
        }

        // 4. 헤더 및 본문 텍스트 갱신 (키워드 치환 적용)
        string rawName = string.IsNullOrEmpty(targetEffect.Data.displayName)
            ? targetEffect.Data.effectName
            : targetEffect.Data.displayName;

        string headerStr = rawName;
        if (isTimedNonPermanent)
        {
            float remaining = Mathf.Max(0f, targetEffect.currentDuration);
            headerStr = targetEffect.currentStack > 1
                ? $"{rawName} x{targetEffect.currentStack} ({remaining:0.0}s)"
                : $"{rawName} ({remaining:0.0}s)";
        }
        else if (clockType == EffectClockType.Turn && targetEffect.currentStack >= 1)
        {
            headerStr = $"{rawName} ({targetEffect.currentStack}T)";
        }
        else if (targetEffect.currentStack > 1)
        {
            headerStr = $"{rawName} x{targetEffect.currentStack}";
        }

        if (headerText != null)
        {
            headerText.text = TooltipManager.ReplaceKeywordsForDisplay(headerStr);
        }

        if (bodyText != null)
        {
            string rawDesc = targetEffect.GetDescription() ?? string.Empty;
            bodyText.text = TooltipManager.ReplaceKeywordsForDisplay(rawDesc);
        }
    }

    /// <summary>
    /// 매 프레임 호출되는 지속시간 게이지 링 전용 갱신 메서드 (GC 무할당)
    /// </summary>
    public void RefreshCooldownGauge()
    {
        if (targetEffect == null || targetEffect.Data == null) return;

        if (durationFillImage != null)
        {
            if (targetEffect.Data.clockType == EffectClockType.Time
                && !targetEffect.Data.isPermanent
                && targetEffect.duration > 0f)
            {
                durationFillImage.fillAmount = Mathf.Clamp01(targetEffect.currentDuration / targetEffect.duration);
                if (!durationFillImage.gameObject.activeSelf)
                {
                    durationFillImage.gameObject.SetActive(true);
                }
            }
            else if (durationFillImage.gameObject.activeSelf)
            {
                durationFillImage.gameObject.SetActive(false);
            }
        }
    }
}
