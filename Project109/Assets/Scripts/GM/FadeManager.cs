using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeManager : MonoBehaviour
{
    public static FadeManager instance { get; private set; }

    [SerializeField] private Image fadeImage;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (fadeImage != null)
            {
                fadeImage.gameObject.SetActive(false);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void FadeIn(float duration = 0.35f, Action onComplete = null)
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[FadeManager] FadeImage is not assigned!");
            onComplete?.Invoke();
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeImage.gameObject.SetActive(true);
        fadeCoroutine = StartCoroutine(CoFade(fadeImage.color.a, 1f, duration, onComplete));
    }

    public void FadeOut(float duration = 0.35f, Action onComplete = null)
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[FadeManager] FadeImage is not assigned!");
            onComplete?.Invoke();
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(CoFade(fadeImage.color.a, 0f, duration, () =>
        {
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
            elapsed += Time.deltaTime;
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
