using UnityEngine;

/// <summary>
/// 슬더스식 모달 팝업 및 UI 패널의 베이스 클래스입니다.
/// UIManager의 모달 팝업 스택 및 인스턴스 캐싱과 연동되며, Open/Close 라이프사이클을 제공합니다.
/// </summary>
public abstract class UIPanelBase : MonoBehaviour
{
    [Header("UI Layer Type Settings")]
    public UILayerType uiLayerType = UILayerType.Normal;

    [Header("Input Block Settings")]
    public bool blockWorldInput = true;

    /// <summary>
    /// ESC(취소) 키를 눌렀을 때 스스로 닫힐 수 있는지 여부입니다.
    /// 카드 보상 선택이나 필수 이벤트 선택지 등 강제 선택 팝업은 false로 재정의합니다.
    /// </summary>
    public virtual bool CanCloseByCancel => true;

    /// <summary>
    /// 닫힐 때 파괴할 것인지 여부입니다.
    /// 기본값 false(인스턴스 캐싱 재활용)이며, 메모리가 큰 1회성 팝업만 true로 재정의합니다.
    /// </summary>
    public virtual bool DestroyOnClose => false;

    /// <summary>
    /// UIManager에 의해 팝업이 열릴 때 호출되는 가상 메서드입니다. (데이터 바인딩 및 초기화)
    /// </summary>
    public virtual void OnOpen() { }

    /// <summary>
    /// UIManager에 의해 팝업이 닫힐 때 호출되는 가상 메서드입니다. (상태 리셋)
    /// </summary>
    public virtual void OnClose() { }

    protected virtual void OnEnable() { }
    protected virtual void OnDisable() { }

    /// <summary>
    /// 팝업을 엽니다 (UIManager 스택에 등록 및 활성화).
    /// </summary>
    public virtual void Open()
    {
        if (UIManager.instance != null)
        {
            UIManager.instance.PushActiveUIPanel(this);
        }
        else
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 팝업을 닫습니다 (UIManager 스택에서 제거 및 비활성화/파괴).
    /// </summary>
    public virtual void Close()
    {
        if (UIManager.instance != null)
        {
            UIManager.instance.CloseUI(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// ESC(취소 키) 입력이 이 패널로 전달되었을 때 호출됩니다.
    /// </summary>
    public virtual void OnCancelInput()
    {
        if (CanCloseByCancel)
        {
            Close();
        }
        else
        {
            Debug.Log($"[{name}] 이 팝업은 선택지를 완료해야만 닫을 수 있습니다.");
        }
    }
}
