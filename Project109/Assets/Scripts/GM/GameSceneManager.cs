using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameScene에서 사용할 Manager 클래스 ( DontDestroyOnLoad 미적용 싱글턴 )
/// GameScene 진입 시 세이브 데이터를 확인하여 은신처 또는 던전 서브씬을 가산 로드합니다.
/// </summary>
public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager instance { get; private set; }
    public static GameSceneManager Instance => instance;

    /// <summary>
    /// 타이틀 씬(MainMenu)에서 선택한 세이브 슬롯 번호 (정적 캐시)
    /// </summary>
    public static int selectedSaveSlot = 1;

    /// <summary>
    /// 현재 활성화된 세이브 슬롯 번호
    /// </summary>
    public int currentSaveSlot { get; private set; } = 1;

    [Header("SubScene Settings")]
    [SerializeField]
    private string _hideoutScenePath = "Assets/Scenes/Release/HideoutScene.unity";

    [SerializeField]
    private string _dungeonScenePath = "Assets/Scenes/Release/DungeonScene.unity";

    public string HideoutScenePath => _hideoutScenePath;
    public string DungeonScenePath => _dungeonScenePath;

    /// <summary>
    /// 현재 던전 내부 서브씬에 진입해 있는지 여부입니다.
    /// </summary>
    public bool isInDungeon { get; private set; } = false;

    [Header("Active SubScene Managers")]
    public HideoutManager currentHideoutManager { get; private set; }
    public RunManager currentDungeonManager { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            currentSaveSlot = selectedSaveSlot;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Start()
    {
        InitializeSubScene();
    }

    #region SubScene Manager Registration

    public void RegisterHideoutManager(HideoutManager hm)
    {
        currentHideoutManager = hm;
        Debug.Log("[GameSceneManager] 은신처 서브씬 매니저(HideoutManager)가 등록되었습니다.");
    }

    public void UnregisterHideoutManager(HideoutManager hm)
    {
        if (currentHideoutManager == hm)
        {
            currentHideoutManager = null;
            Debug.Log("[GameSceneManager] 은신처 서브씬 매니저(HideoutManager) 등록이 해제되었습니다.");
        }
    }

    public void RegisterDungeonManager(RunManager rm)
    {
        currentDungeonManager = rm;
        Debug.Log("[GameSceneManager] 던전 서브씬 매니저(RunManager)가 등록되었습니다.");
    }

    public void UnregisterDungeonManager(RunManager rm)
    {
        if (currentDungeonManager == rm)
        {
            currentDungeonManager = null;
            Debug.Log("[GameSceneManager] 던전 서브씬 매니저(RunManager) 등록이 해제되었습니다.");
        }
    }

    #endregion

    #region SubScene Transitions

    /// <summary>
    /// 세이브 데이터를 읽어 던전 내 종료 여부에 따라 적절한 서브씬(던전 또는 은신처)을 가산 로드합니다.
    /// </summary>
    public void InitializeSubScene()
    {
        bool savedInDungeon = false;

        // 세이브 파일 존재 시 던전 진행 중이었는지 여부를 체크합니다.
        if (SaveSystem.HasSaveData(currentSaveSlot))
        {
            RunSaveData saveData = SaveSystem.LoadGameData(currentSaveSlot);
            if (saveData != null)
            {
                savedInDungeon = saveData.isInDungeon;
            }
        }

        string targetSubScenePath = savedInDungeon ? _dungeonScenePath : _hideoutScenePath;
        isInDungeon = savedInDungeon;

        if (SceneLoadManager.instance != null)
        {
            SceneLoadManager.instance.LoadInitialSubScene(targetSubScenePath);
            Debug.Log($"[GameSceneManager] 세이브 데이터 검사 완료 (슬롯 {currentSaveSlot}, 던전 진입 상태: {isInDungeon}). 서브씬('{targetSubScenePath}') 로딩을 시작합니다.");
        }
        else
        {
            Debug.LogError("[GameSceneManager] SceneLoadManager 인스턴스를 찾을 수 없어 서브씬을 불러오지 못했습니다.");
        }
    }

    /// <summary>
    /// 은신처에서 던전 서브씬으로 전환합니다. (단일 출격 창구)
    /// </summary>
    public void TransitionToDungeon(System.Action onComplete = null)
    {
        if (isInDungeon)
        {
            Debug.LogWarning("[GameSceneManager] 이미 던전 서브씬에 진입한 상태입니다.");
            return;
        }

        isInDungeon = true;
        Debug.Log($"[GameSceneManager] 던전 서브씬('{_dungeonScenePath}')으로 전환을 시작합니다.");

        if (SceneLoadManager.instance != null)
        {
            SceneLoadManager.instance.TransitionToSubScene(_dungeonScenePath, () =>
            {
                // 던전 런 준비 및 세이브 저장
                if (currentDungeonManager != null)
                {
                    currentDungeonManager.EnterDungeonRun();
                }
                SaveSystem.SaveGame(currentDungeonManager, currentSaveSlot);
                onComplete?.Invoke();
            });
        }
        else
        {
            Debug.LogError("[GameSceneManager] SceneLoadManager 인스턴스가 없습니다.");
        }
    }

    /// <summary>
    /// 던전에서 은신처 서브씬으로 복귀합니다. (런 종료/정산 시 단일 복귀 창구)
    /// </summary>
    public void TransitionToHideout(System.Action onComplete = null)
    {
        if (!isInDungeon)
        {
            Debug.LogWarning("[GameSceneManager] 이미 은신처 서브씬에 위치하고 있습니다.");
            return;
        }

        isInDungeon = false;
        Debug.Log($"[GameSceneManager] 은신처 서브씬('{_hideoutScenePath}')으로 복귀를 시작합니다.");

        if (SceneLoadManager.instance != null)
        {
            SceneLoadManager.instance.TransitionToSubScene(_hideoutScenePath, () =>
            {
                // 던전에서 은신처로 복귀 시 던전 진입 플래그를 해제하고 세이브
                SaveSystem.SaveGame(currentDungeonManager, currentSaveSlot);
                onComplete?.Invoke();
            });
        }
        else
        {
            Debug.LogError("[GameSceneManager] SceneLoadManager 인스턴스가 없습니다.");
        }
    }

    /// <summary>
    /// 던전 서브씬 진입/퇴출 시 던전 상태 플래그를 변경합니다.
    /// </summary>
    public void SetInDungeonState(bool inDungeon)
    {
        isInDungeon = inDungeon;
    }

    #endregion
}


