using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIDialogPanel : UIPanelBase
{
    [Header("Dialog UI Components")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action _onConfirm;
    private Action _onCancel;

    protected override void OnEnable()
    {
        // 다이얼로그는 월드 터치를 차단해야 함
        blockWorldInput = true;
        base.OnEnable();
    }

    private void Start()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    public void Setup(string title, string message, Action onConfirm, Action onCancel)
    {
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        _onConfirm = onConfirm;
        _onCancel = onCancel;
    }

    public override bool DestroyOnClose => true;

    public override void OnCancelInput()
    {
        OnCancelClicked();
    }

    private void OnConfirmClicked()
    {
        _onConfirm?.Invoke();
        Close();
    }

    private void OnCancelClicked()
    {
        _onCancel?.Invoke();
        Close();
    }
}
