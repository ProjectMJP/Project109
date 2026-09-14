using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(TooltipTrigger))]
public class CharacterEffectItemUI : MonoBehaviour
{
    [Header("UI Component References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI stackText;
    [SerializeField] private Image durationFillImage;
    [SerializeField] private Image outlineBorderImage;

    [Header("Color Settings")]
    [SerializeField] private Color buffBorderColor = new Color(0.2f, 0.8f, 1.0f, 1.0f);
    [SerializeField] private Color debuffBorderColor = new Color(0.9f, 0.2f, 0.3f, 1.0f);

    private Effect targetEffect;
    private TooltipTrigger tooltipTrigger;

    private void Awake()
    {
        tooltipTrigger = GetComponent<TooltipTrigger>();
        if (tooltipTrigger == null)
        {
            tooltipTrigger = gameObject.AddComponent<TooltipTrigger>();
        }
    }

    public Effect TargetEffect => targetEffect;

    /// <summary>
    /// 버프/디버프 이펙트 정보와 UI를 바인딩하고 갱신합니다.
    /// </summary>
    public void Setup(Effect effect)
    {
        targetEffect = effect;
        Refresh();
    }

    /// <summary>
    /// 이펙트 수치 및 연동 UI를 최신 상태로 업데이트합니다.
    /// </summary>
    public void Refresh()
    {
        if (targetEffect == null || targetEffect.Data == null) return;
        // 1. 아이콘 스프라이트 갱신
        if (iconImage != null && targetEffect.Data.iconSprite != null)
        {
            iconImage.sprite = targetEffect.Data.iconSprite;
            iconImage.enabled = true;
        }
        // 2. 중첩(Stack) 수치 표시 (2 이상일 경우 숫자 표기, 1 이하일 경우 숨김)
        if (stackText != null)
        {
            if (targetEffect.currentStack > 1)
            {
                stackText.text = targetEffect.currentStack.ToString();
                stackText.gameObject.SetActive(true);
            }
            else
            {
                stackText.gameObject.SetActive(false);
            }
        }
        // 3. 지속시간 게이지 갱신
        if (durationFillImage != null)
        {
            if (!targetEffect.Data.isPermanent && targetEffect.duration > 0f)
            {
                float fillRatio = Mathf.Clamp01(targetEffect.currentDuration / targetEffect.duration);
                durationFillImage.fillAmount = fillRatio;
                durationFillImage.gameObject.SetActive(true);
            }
            else
            {
                durationFillImage.gameObject.SetActive(false);
            }
        }
        // 4. 버프/디버프 테두리 색상 설정
        if (outlineBorderImage != null)
        {
            outlineBorderImage.color = (targetEffect.Data.effectType == EffectType.Buff) ? buffBorderColor : debuffBorderColor;
        }
        // 5. 툴팁 연동 갱신
        if (tooltipTrigger != null)
        {
            string header = string.IsNullOrEmpty(targetEffect.Data.displayName) ? targetEffect.Data.effectName : targetEffect.Data.displayName;
            tooltipTrigger.SetDynamicContent(header, targetEffect);
        }
    }
}
