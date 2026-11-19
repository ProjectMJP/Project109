using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Placement Override")]
    [SerializeField] private RectTransform anchorRectOverride;
    [SerializeField] private TooltipPlacementMode placementMode = TooltipPlacementMode.AutoQuadrant;

    private string _headerText;
    private string _bodyText;
    private IDescribable _describableSource; // Card, Effect 등 GetDescription()이 구현된 인터페이스 소스

    /// <summary>
    /// 툴팁 위치 기준 RectTransform과 배치 모드를 지정합니다.
    /// </summary>
    public void ConfigureAnchor(RectTransform anchorOverride, TooltipPlacementMode mode)
    {
        anchorRectOverride = anchorOverride;
        placementMode = mode;
    }

    /// <summary>
    /// 단순 텍스트용 세팅
    /// </summary>
    public void SetStaticContent(string header, string body)
    {
        _headerText = header;
        _bodyText = body;
        _describableSource = null;
    }

    /// <summary>
    /// 동적 토큰 파싱이 필요한 Card, Effect, Relic 인스턴스 전용 세팅
    /// </summary>
    public void SetDynamicContent(string header, IDescribable source)
    {
        _headerText = header;
        _describableSource = source;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance == null) return;

        string finalBody = _bodyText;
        if (_describableSource != null)
        {
            // Lua 및 C# 내부에 바인딩된 토큰 변환을 실행하여 동적으로 갱신된 최종 설명 추출
            finalBody = _describableSource.GetDescription();
        }

        if (!string.IsNullOrEmpty(_headerText) || !string.IsNullOrEmpty(finalBody))
        {
            RectTransform rect = anchorRectOverride != null ? anchorRectOverride : (transform as RectTransform);
            TooltipManager.Instance.ShowTooltip(_headerText, finalBody, rect, placementMode);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.HasInstance)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

    private void OnDisable()
    {
        // UI가 비활성화될 때 툴팁이 화면에 찌꺼기로 남는 현상 방지
        if (TooltipManager.HasInstance && TooltipManager.Instance.gameObject.activeSelf)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}
