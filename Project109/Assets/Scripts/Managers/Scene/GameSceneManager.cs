using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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

    /// <summary>
    /// 현재 던전 내부 서브씬에 진입해 있는지 여부입니다.
    /// </summary>
    public bool isInDungeon { get; private set; } = false;

    [Header("Player Settings")]
    [SerializeField]
    private AssetReferenceGameObject _playerCharacterReference;
    public Player player { get; set; }
    public TopHUDPanel topHUDPanel { get; private set; }

    [Header("Active SubScene Managers")]
    public HideoutManager currentHideoutManager { get; private set; }
    public RunManager currentDungeonManager { get; private set; }

    [Header("SubScene Settings")]
    [SerializeField]
    private string _hideoutScenePath = "Assets/Scenes/Release/HideoutScene.unity";

    [SerializeField]
    private string _dungeonScenePath = "Assets/Scenes/Release/DungeonScene.unity";

    public string HideoutScenePath => _hideoutScenePath;
    public string DungeonScenePath => _dungeonScenePath;

    private GameObject _spawnedPlayerObject;

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
        if (_spawnedPlayerObject != null)
        {
            Addressables.ReleaseInstance(_spawnedPlayerObject);
            _spawnedPlayerObject = null;
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    private IEnumerator Start()
    {
        // 1. 플레이어 인스턴스 비동기 초기화 (AssetReferenceGameObject 인스턴스화 및 바인딩)
        yield return InitializePlayerCoroutine();

        // 2. 세션 상주 TopHUD 생성 및 플레이어 바인딩
        InitializeTopHUD();

        // 3. 서브씬 로드 (은신처 or 던전)
        InitializeSubScene();
    }

    /// <summary>
    /// AssetReferenceGameObject를 통해 플레이어 캐릭터를 인스턴스화하고 바인딩하는 비동기 코루틴입니다.
    /// </summary>
    public IEnumerator InitializePlayerCoroutine()
    {
        if (player != null) yield break;

        if (_playerCharacterReference == null || !_playerCharacterReference.RuntimeKeyIsValid())
        {
            Debug.LogError("[GameSceneManager] Player Character AssetReference가 설정되지 않았거나 유효하지 않습니다.");
            yield break;
        }

        AsyncOperationHandle<GameObject> handle = _playerCharacterReference.InstantiateAsync(Vector3.zero, Quaternion.identity);
        yield return handle;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"[GameSceneManager] Player Character AssetReference 인스턴스화 실패: {handle.OperationException}");
            yield break;
        }

        _spawnedPlayerObject = handle.Result;
        _spawnedPlayerObject.name = "PlayerCharacter";

        Character character = _spawnedPlayerObject.GetComponent<Character>();
        if (character == null)
        {
            Debug.LogError("[GameSceneManager] 생성된 PlayerCharacter 프리팹에 Character 컴포넌트가 존재하지 않습니다.");
            yield break;
        }
        character.faction = CharacterFaction.Player;

        player = new Player(character);
        Debug.Log("[GameSceneManager] Player Character AssetReference 인스턴스화 및 Player 바인딩 완료.");

        // 세이브 데이터가 있으면 전체 세션 복원, 없으면 신규 플레이어 기본 초기화
        if (SaveSystem.HasSaveData(currentSaveSlot))
        {
            SaveSystem.RestoreSession(currentSaveSlot);
        }
        else
        {
            SaveSystem.InitializeDefaultPlayer(player);
        }
    }

    /// <summary>
    /// 동기적 초기화 호출이 필요할 때 코루틴을 시작하는 진입점입니다.
    /// </summary>
    public void InitializePlayer()
    {
        if (player == null)
        {
            StartCoroutine(InitializePlayerCoroutine());
        }
    }

    /// <summary>
    /// 세션 상주 TopHUD를 생성하고 Player의 스탯 및 유물 이벤트를 바인딩합니다.
    /// </summary>
    public void InitializeTopHUD()
    {
        if (player == null || UIManager.instance == null) return;

        GameObject hudObj = UIManager.instance.OpenUI(UIConstants.PANEL_TOP_HUD, UILayerType.Top, true);
        if (hudObj != null && hudObj.TryGetComponent<TopHUDPanel>(out var hud))
        {
            topHUDPanel = hud;
            hud.BindPlayer(player);
            Debug.Log("[GameSceneManager] TopHUDPanel이 생성되어 Player와 바인딩되었습니다.");
        }
    }

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
                // 던전 진입 세이브 저장
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
    #endregion
}


