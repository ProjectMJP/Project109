using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 버튼 프리팹에 부착하여 클릭 시 공통 UI 효과음(SFX_UI_CLICK)을 재생하는 경량 컴포넌트입니다.
/// </summary>
public class UIButtonSoundEmitter : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private string sfxKey = AudioCueKeys.SFX_UI_CLICK;
    [SerializeField] private float pitchVariance = 0.03f;
    [SerializeField] private float volumeScale = 1f;

    private Selectable _selectable;
    private bool _cachedSelectable;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (!_cachedSelectable)
        {
            _selectable = GetComponent<Selectable>();
            _cachedSelectable = true;
        }

        if (_selectable != null && !_selectable.IsInteractable())
        {
            return;
        }

        AudioServiceLocator.PlaySFX(string.IsNullOrEmpty(sfxKey) ? AudioCueKeys.SFX_UI_CLICK : sfxKey, pitchVariance, volumeScale);
    }
}
