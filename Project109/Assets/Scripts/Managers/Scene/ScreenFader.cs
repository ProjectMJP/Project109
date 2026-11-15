using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 암전(FadeIn) 및 밝아짐(FadeOut) 애니메이션만 순수하게 수행하는 뷰 컴포넌트입니다.
/// Canvas, Sorting Order 등 렌더링 설정은 프리팹에서 담당하며, 스크립트는 Image의 알파값 보간만 전담합니다.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private Image fadeImage;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 화면을 어둡게 만듭니다 (Alpha: 0 -> 1). 화면 전환 시작 시 호출합니다.
    /// </summary>
    public void FadeIn(float duration = 0.35f, Action onComplete = null)
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[ScreenFader] fadeImage가 바인딩되지 않았습니다.");
            onComplete?.Invoke();
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeImage.gameObject.SetActive(true);
        fadeImage.raycastTarget = true; // 암전 중 입력 차단
        fadeCoroutine = StartCoroutine(CoFade(fadeImage.color.a, 1f, duration, onComplete));
    }

    /// <summary>
    /// 화면을 밝게 만듭니다 (Alpha: 1 -> 0). 화면 전환 완료 후 호출합니다.
    /// </summary>
    public void FadeOut(float duration = 0.35f, Action onComplete = null)
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[ScreenFader] fadeImage가 바인딩되지 않았습니다.");
            onComplete?.Invoke();
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(CoFade(fadeImage.color.a, 0f, duration, () =>
        {
            fadeImage.raycastTarget = false;
            fadeImage.gameObject.SetActive(false);
            onComplete?.Invoke();
        }));
    }

    private IEnumerator CoFade(float startAlpha, float endAlpha, float duration, Action onComplete)
    {
        Color color = fadeImage.color;
        color.a = startAlpha;
        fadeImage.color = color;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = endAlpha;
        fadeImage.color = color;

        fadeCoroutine = null;
        onComplete?.Invoke();
    }
}
