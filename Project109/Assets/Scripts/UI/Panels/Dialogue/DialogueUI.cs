using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 대화/이벤트 UI의 본문 텍스트 연출 및 선택지 버튼 라이프사이클을 전담하는 UI 패널입니다.
/// TextMeshPro 기반 타이핑(Typewriter) 효과와 선택지 버튼 오브젝트 풀링을 지원합니다.
/// </summary>
public class DialogueUI : UIPanelBase, IPointerClickHandler
{
    [Header("Text & Layout Settings")]
    public TextMeshProUGUI description;
    public Transform eventButtonSpawnPoint;
    public GameObject eventButtonPrefab;

    [Header("Typewriter Settings")]
    [SerializeField] private float charactersPerSecond = 35f;

    // 선택지 버튼 오브젝트 풀
    private readonly List<Button> _buttonPool = new List<Button>();
    private readonly List<Button> _activeButtons = new List<Button>();

    private Coroutine _typingCoroutine;
    private string _fullDescriptionText = string.Empty;

    public bool IsTyping => _typingCoroutine != null;

    private void OnDestroy()
    {
        // 씬 전환/UI 파괴 시 풀링된 버튼들 정리
        foreach (var btn in _buttonPool)
        {
            if (btn != null)
            {
                Destroy(btn.gameObject);
            }
        }
        _buttonPool.Clear();
        _activeButtons.Clear();
    }

    /// <summary>
    /// 본문 설명 텍스트를 설정합니다. 옵션에 따라 타이핑 연출을 실행합니다.
    /// </summary>
    public void SetDescription(string newDescription, bool useTypewriter = true)
    {
        if (description == null) return;

        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        _fullDescriptionText = newDescription ?? string.Empty;

        if (useTypewriter && gameObject.activeInHierarchy && _fullDescriptionText.Length > 0)
        {
            _typingCoroutine = StartCoroutine(TypewriterCoroutine(_fullDescriptionText));
        }
        else
        {
            description.text = _fullDescriptionText;
            description.maxVisibleCharacters = _fullDescriptionText.Length;
        }
    }

    private IEnumerator TypewriterCoroutine(string text)
    {
        description.text = text;
        description.maxVisibleCharacters = 0;

        float delay = 1f / Mathf.Max(1f, charactersPerSecond);
        var wait = new WaitForSeconds(delay);

        int totalChars = text.Length;
        for (int i = 1; i <= totalChars; i++)
        {
            description.maxVisibleCharacters = i;
            yield return wait;
        }

        _typingCoroutine = null;
    }

    /// <summary>
    /// 타이핑 연출 진행 중 클릭 시 전체 텍스트를 즉시 노출합니다.
    /// </summary>
    public void SkipTyping()
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        if (description != null)
        {
            description.text = _fullDescriptionText;
            description.maxVisibleCharacters = _fullDescriptionText.Length;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsTyping)
        {
            SkipTyping();
        }
    }

    /// <summary>
    /// 오브젝트 풀링을 활용하여 선택지 버튼을 재사용 또는 생성합니다.
    /// </summary>
    public Button CreateChoiceButton(string buttonDescription)
    {
        if (eventButtonPrefab == null || eventButtonSpawnPoint == null)
        {
            Debug.LogError("[DialogueUI] eventButtonPrefab 또는 eventButtonSpawnPoint가 바인딩되지 않았습니다!", this);
            return null;
        }

        Button targetButton = null;

        // 1. 풀에서 비활성 버튼 탐색
        for (int i = 0; i < _buttonPool.Count; i++)
        {
            if (_buttonPool[i] != null && !_buttonPool[i].gameObject.activeSelf)
            {
                targetButton = _buttonPool[i];
                break;
            }
        }

        // 2. 풀에 없으면 신규 생성
        if (targetButton == null)
        {
            GameObject buttonObj = Instantiate(eventButtonPrefab, eventButtonSpawnPoint);
            targetButton = buttonObj != null ? buttonObj.GetComponent<Button>() : null;
            if (targetButton != null)
            {
                _buttonPool.Add(targetButton);
            }
        }

        if (targetButton != null)
        {
            targetButton.gameObject.SetActive(true);
            targetButton.transform.SetAsLastSibling();

            var textMesh = targetButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (textMesh != null)
            {
                textMesh.text = buttonDescription;
                textMesh.textWrappingMode = TextWrappingModes.Normal;
            }

            var rectTransform = targetButton.transform as RectTransform;
            if (rectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }

            targetButton.onClick.RemoveAllListeners();
            _activeButtons.Add(targetButton);
            return targetButton;
        }

        return null;
    }

    /// <summary>
    /// 현재 활성화된 선택지 버튼들을 Destroy하지 않고 비활성화하여 풀로 반환합니다.
    /// </summary>
    public void ClearChoiceButton()
    {
        for (int i = 0; i < _activeButtons.Count; i++)
        {
            if (_activeButtons[i] != null)
            {
                _activeButtons[i].onClick.RemoveAllListeners();
                _activeButtons[i].gameObject.SetActive(false);
            }
        }
        _activeButtons.Clear();
    }
}

/// <summary>
/// 이전 레거시 참조 호환을 위한 별칭 클래스입니다.
/// </summary>
[Obsolete("Use DialogueUI instead.")]
public class EventDescriptionScript : DialogueUI { }
