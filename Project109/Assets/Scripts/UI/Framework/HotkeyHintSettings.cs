using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 UI(손패 카드 1~9, 이동 모드 Q, 턴 종료 E)의 단축키 배지 표시 설정과
/// 좌상단 배지 스타일·생성을 통합 관리하는 설정 클래스입니다.
/// </summary>
public static class HotkeyHintSettings
{
    public const string PREF_SHOW_HOTKEY_HINTS = "Settings_ShowHotkeyHints";

    public static readonly Vector2 BadgeSize = new Vector2(26f, 22f);
    public static readonly Color BadgeBackgroundColor = new Color(0.08f, 0.10f, 0.15f, 0.88f);
    public static readonly Color BadgeTextColor = Color.white;
    public const float BadgeFontSize = 14f;
    public static readonly Vector2 ButtonTopLeftBadgeOffset = new Vector2(-6f, 8f);

    private static bool _showHotkeyHints = true;
    private static bool _isInitialized = false;

    public static event Action<bool> OnShowHotkeyHintsChanged;

    public static bool ShowHotkeyHints
    {
        get
        {
            EnsureLoaded();
            return _showHotkeyHints;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _showHotkeyHints = true;
        _isInitialized = false;
        OnShowHotkeyHintsChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ReloadFromPrefs()
    {
        bool previous = _showHotkeyHints;
        bool hadInit = _isInitialized;
        _showHotkeyHints = PlayerPrefs.GetInt(PREF_SHOW_HOTKEY_HINTS, 1) == 1;
        _isInitialized = true;

        if (hadInit && previous != _showHotkeyHints)
        {
            OnShowHotkeyHintsChanged?.Invoke(_showHotkeyHints);
        }
    }

    public static void SetShowHotkeyHints(bool enabled, bool savePrefs = true)
    {
        EnsureLoaded();

        if (savePrefs)
        {
            PlayerPrefs.SetInt(PREF_SHOW_HOTKEY_HINTS, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (_showHotkeyHints == enabled)
        {
            return;
        }

        _showHotkeyHints = enabled;
        OnShowHotkeyHintsChanged?.Invoke(_showHotkeyHints);
    }

    /// <summary>
    /// 버튼 좌상단에 카드 단축키 배지와 동일한 스타일의 단축키 배지를 보장합니다.
    /// </summary>
    public static void EnsureButtonHotkeyBadge(
        Transform buttonTransform,
        ref GameObject badgeObject,
        ref TextMeshProUGUI badgeText,
        params TMP_Text[] fontFallbackSources)
    {
        if (buttonTransform == null)
        {
            return;
        }

        if (badgeObject == null)
        {
            Transform existing = buttonTransform.Find("HotkeyHintBadge");
            if (existing != null)
            {
                badgeObject = existing.gameObject;
            }
        }

        if (badgeObject == null)
        {
            GameObject badgeObj = new GameObject("HotkeyHintBadge", typeof(RectTransform), typeof(Image));
            badgeObj.layer = buttonTransform.gameObject.layer != 0 ? buttonTransform.gameObject.layer : 5;
            badgeObj.transform.SetParent(buttonTransform, false);

            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.anchoredPosition = ButtonTopLeftBadgeOffset;
            badgeRect.sizeDelta = BadgeSize;

            Image badgeBg = badgeObj.GetComponent<Image>();
            badgeBg.color = BadgeBackgroundColor;
            badgeBg.raycastTarget = false;

            badgeObject = badgeObj;
        }

        if (badgeText == null)
        {
            badgeText = badgeObject.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (badgeText == null)
        {
            GameObject textObj = new GameObject("HotkeyHintText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.layer = badgeObject.layer;
            textObj.transform.SetParent(badgeObject.transform, false);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            badgeText = textObj.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset fontAsset = TMPFontUtility.ResolveDefaultFont(fontFallbackSources);
            if (fontAsset != null)
            {
                badgeText.font = fontAsset;
            }
            badgeText.fontSize = BadgeFontSize;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = BadgeTextColor;
            badgeText.raycastTarget = false;
        }
    }

    private static void EnsureLoaded()
    {
        if (!_isInitialized)
        {
            _showHotkeyHints = PlayerPrefs.GetInt(PREF_SHOW_HOTKEY_HINTS, 1) == 1;
            _isInitialized = true;
        }
    }
}
