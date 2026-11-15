using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class TooltipPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _headerText;
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private LayoutElement _layoutElement;
    [SerializeField] private float _preferredWidthValue = 350f;

    private void Awake()
    {
        if (_headerText != null)
        {
            _headerText.textWrappingMode = TextWrappingModes.Normal;
        }
        else
        {
            Debug.LogError("[TooltipPanel] _headerText가 바인딩되지 않았습니다! TooltipPanel 프리팹을 확인하세요.", this);
        }

        if (_bodyText != null)
        {
            _bodyText.textWrappingMode = TextWrappingModes.Normal;
        }
        else
        {
            Debug.LogError("[TooltipPanel] _bodyText가 바인딩되지 않았습니다! TooltipPanel 프리팹을 확인하세요.", this);
        }

        if (_layoutElement == null)
        {
            _layoutElement = GetComponent<LayoutElement>();
            if (_layoutElement == null)
            {
                Debug.LogError("[TooltipPanel] LayoutElement가 바인딩되지 않았습니다! TooltipPanel 프리팹을 확인하세요.", this);
            }
        }
    }

    public void Setup(string header, string body)
    {
        // 1. 헤더 세팅
        if (_headerText != null)
        {
            if (string.IsNullOrEmpty(header))
            {
                _headerText.gameObject.SetActive(false);
            }
            else
            {
                _headerText.gameObject.SetActive(true);
                _headerText.text = header;
            }
        }
        // 2. 본문 세팅
        if (_bodyText != null)
        {
            if (string.IsNullOrEmpty(body))
            {
                _bodyText.gameObject.SetActive(false);
            }
            else
            {
                _bodyText.gameObject.SetActive(true);
                _bodyText.text = body;
            }
        }
        // 3. 줄바꿈 처리 및 크기 고정
        if (_layoutElement != null)
        {
            _layoutElement.enabled = true;
            _layoutElement.preferredWidth = _preferredWidthValue;
        }
    }
}
