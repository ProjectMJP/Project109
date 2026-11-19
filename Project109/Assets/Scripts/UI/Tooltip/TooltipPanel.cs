using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class TooltipPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _headerText;
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private TextMeshProUGUI _hintCaptionText;
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
        if (_layoutElement == null)
        {
            _layoutElement = GetComponent<LayoutElement>();
        }

        // 3. 줄바꿈 처리 및 크기 고정
        if (_layoutElement != null)
        {
            _layoutElement.enabled = true;
            _layoutElement.preferredWidth = _preferredWidthValue;
        }

        // 4. 기본 상태에서는 힌트 캡션 우측 정렬 복원 및 숨김 (TooltipManager에서 최하단 패널에만 표시)
        if (_hintCaptionText != null)
        {
            _hintCaptionText.alignment = TextAlignmentOptions.Right;
            _hintCaptionText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 전역 툴팁이 꺼져 있을 때 1줄 미니 힌트([T] 툴팁 보기)만 콘텐츠 크기에 맞게 표시합니다.
    /// </summary>
    public void SetupCompactHint(string caption = "[T] 툴팁 보기")
    {
        if (_headerText != null)
        {
            _headerText.gameObject.SetActive(false);
        }

        if (_bodyText != null)
        {
            _bodyText.gameObject.SetActive(false);
        }

        if (_layoutElement == null)
        {
            _layoutElement = GetComponent<LayoutElement>();
        }

        if (_layoutElement != null)
        {
            _layoutElement.enabled = false;
        }

        if (_hintCaptionText != null)
        {
            _hintCaptionText.gameObject.SetActive(true);
            _hintCaptionText.alignment = TextAlignmentOptions.Center;
            _hintCaptionText.text = caption;
        }
    }

    /// <summary>
    /// 툴팁 하단 미니 안내 캡션([T] 툴팁 숨기기)의 표시 여부를 설정합니다.
    /// </summary>
    public void SetHintCaptionVisible(bool visible, string caption = "[T] 툴팁 숨기기")
    {
        if (_hintCaptionText == null) return;

        _hintCaptionText.gameObject.SetActive(visible);
        if (visible)
        {
            _hintCaptionText.alignment = TextAlignmentOptions.Right;
            _hintCaptionText.text = caption;
        }
    }
}
