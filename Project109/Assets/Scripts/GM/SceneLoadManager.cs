using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadManager : MonoBehaviour
{
    public static SceneLoadManager instance { get; private set; }

    private string currentLoadedSubScene = "";

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void LoadInGameScene()
    {
        SceneManager.LoadScene("InGameScene");
    }

    // 서브씬 전환 통합 인터페이스
    public void TransitionToSubScene(string newSubSceneName, Action onComplete = null)
    {
        StartCoroutine(CoTransitionSubScene(newSubSceneName, onComplete));
    }

    private IEnumerator CoTransitionSubScene(string newSubSceneName, Action onComplete)
    {
        // 1. FadeManager를 통한 화면 암전 (FadeIn)
        if (FadeManager.instance != null)
        {
            bool fadeDone = false;
            FadeManager.instance.FadeIn(0.5f, () => fadeDone = true);
            yield return new WaitUntil(() => fadeDone);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        // 2. 기존 로드된 서브씬이 있다면 비동기 언로드
        if (!string.IsNullOrEmpty(currentLoadedSubScene))
        {
            Scene prevScene = SceneManager.GetSceneByPath(currentLoadedSubScene);
            if (prevScene.IsValid() && prevScene.isLoaded)
            {
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(prevScene);
                yield return unloadOp;
            }
        }

        // 3. 신규 서브씬을 Additive(가산) 모드로 비동기 로드
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(newSubSceneName, LoadSceneMode.Additive);
        yield return loadOp;

        // 4. 로드 완료 후, 해당 씬을 Active Scene으로 변경
        Scene loadedScene = SceneManager.GetSceneByPath(newSubSceneName);
        if (loadedScene.IsValid() && loadedScene.isLoaded)
        {
            SceneManager.SetActiveScene(loadedScene);
        }

        currentLoadedSubScene = newSubSceneName;

        // 5. 화면 밝아짐 (FadeOut)
        if (FadeManager.instance != null)
        {
            FadeManager.instance.FadeOut(0.5f);
        }

        onComplete?.Invoke();
    }

    // 초기 서브씬 설정용 (최초 GameScene 진입 시 은신처 씬을 덧붙여 로드할 때 사용)
    public void LoadInitialSubScene(string subSceneName)
    {
        SceneManager.LoadSceneAsync(subSceneName, LoadSceneMode.Additive).completed += (op) =>
        {
            Scene loadedScene = SceneManager.GetSceneByPath(subSceneName);
            if (loadedScene.IsValid())
            {
                SceneManager.SetActiveScene(loadedScene);
                currentLoadedSubScene = subSceneName;
            }
        };
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
