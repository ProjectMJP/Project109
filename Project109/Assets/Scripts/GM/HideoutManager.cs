using UnityEngine;

public class HideoutManager : MonoBehaviour
{
    [Header("Dungeon Entry Settings")]
    public string dungeonSceneName = "Assets/Scenes/Release/DungeonScene.unity";

    private void Start()
    {
        // 최상위 GameSceneManager에 은신처 서브씬 매니저 등록
        if (GameSceneManager.instance != null)
        {
            GameSceneManager.instance.RegisterHideoutManager(this);
        }
    }

    private void OnDestroy()
    {
        if (GameSceneManager.instance != null)
        {
            GameSceneManager.instance.UnregisterHideoutManager(this);
        }
    }

    // 던전 진입 오브젝트에서 이 메서드를 호출하거나, 직접 상호작용 시 호출됨
    public void EnterDungeon()
    {
        Debug.Log("[HideoutManager] 던전 진입 오브젝트와 상호작용했습니다.");

        // 최상위 GameSceneManager의 단일 출격 창구를 통해 서브씬 전환 및 던전 런 개시
        if (GameSceneManager.instance != null)
        {
            GameSceneManager.instance.TransitionToDungeon(() =>
            {
                Debug.Log("[HideoutManager] 던전 서브씬 전환 완료.");
            });
        }
        else if (SceneLoadManager.instance != null)
        {
            // Fallback: GameSceneManager가 없는 경우 직접 전환
            SceneLoadManager.instance.TransitionToSubScene(dungeonSceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(dungeonSceneName);
        }
    }
}
