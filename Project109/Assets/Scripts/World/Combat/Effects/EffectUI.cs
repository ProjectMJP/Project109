using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EffectUI : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _stackText;
    [SerializeField] private TooltipTrigger _tooltipTrigger;

    private Effect _effect;

    public void Refresh(Effect effect)
    {
        _effect = effect;

        if (effect == null || effect.Data == null) return;

        // 아이콘 갱신
        if (_iconImage != null && effect.Data.iconSprite != null)
        {
            _iconImage.sprite = effect.Data.iconSprite;
        }

        // 스택 수치 가시성 분기
        if (_stackText != null)
        {
            if (effect.currentStack > 1)
            {
                _stackText.gameObject.SetActive(true);
                _stackText.text = effect.currentStack.ToString();
            }
            else
            {
                _stackText.gameObject.SetActive(false);
            }
        }

        // 동적 설명 툴팁 연결 (스택 변경 시마다 호버 내용 자동 업데이트됨)
        if (_tooltipTrigger != null)
        {
            _tooltipTrigger.SetDynamicContent(effect.Data.effectName, effect);
        }
    }
}
