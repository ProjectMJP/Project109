using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 은신처(Hideout) 서브씬의 생명주기와 맵 생성, 던전 출격을 주관하는 매니저입니다.
/// MapDataSO(Map_Hideout) 데이터를 기반으로 타일과 사물을 동적 생성하고 플레이어 위치를 동기화합니다.
/// </summary>
public class HideoutManager : MonoBehaviour
{
    public static HideoutManager instance { get; private set; }

    [Header("Dungeon Entry Settings")]
    public string dungeonSceneName = "Assets/Scenes/Release/DungeonScene.unity";

    [Header("Hideout Map Settings")]
    [SerializeField] private MapDataSO _hideoutMapData;
    [SerializeField] private MapPrefabs _mapPrefabs;

    public GameMap currentGameMap { get; private set; }
    public PlayerExploreController playerExploreController { get; private set; }

    private List<GameObject> _spawnedObjects = new List<GameObject>();
    private Vector2Int _playerSpawnCellCoord = Vector2Int.zero;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeHideoutStage();
    }

    private void OnDestroy()
    {
        ClearHideoutStage();
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// 은신처 맵 데이터를 기반으로 타일 및 사물을 동적 생성하고 플레이어를 스폰합니다.
    /// </summary>
    public void InitializeHideoutStage()
    {
        if (_hideoutMapData == null)
        {
            Debug.LogWarning("[HideoutManager] HideoutMapData가 지정되지 않았습니다.");
            return;
        }

        if (_mapPrefabs.mapSpawnRootPrefab == null)
        {
            Debug.LogWarning("[HideoutManager] MapSpawnRootPrefab이 지정되지 않았습니다.");
            return;
        }

        // 1. GameMap 루트 생성
        GameObject mapRootObj = Instantiate(_mapPrefabs.mapSpawnRootPrefab, transform);
        currentGameMap = mapRootObj.GetComponent<GameMap>();
        if (currentGameMap == null)
        {
            Debug.LogError("[HideoutManager] MapSpawnRootPrefab에 GameMap 컴포넌트가 없습니다.");
            return;
        }

        // 2. 바닥 타일 생성
        currentGameMap.TileCreateByMapData(_hideoutMapData);

        // 3. 셀 데이터 순회하며 지형/오브젝트 스폰 및 이벤트 셀 탐색
        _spawnedObjects.Clear();
        bool hasSpawnPoint = false;

        foreach (var cell in _hideoutMapData.cells)
        {
            // 지형 스폰 (벽, 기둥 등)
            if (!string.IsNullOrEmpty(cell.terrainID) && cell.terrainID != "Empty")
            {
                SpawnObjectAtCell(cell.terrainID, cell.position);
            }

            // 오브젝트 스폰 (던전 게이트, 무기대 등)
            if (!string.IsNullOrEmpty(cell.objectID) && cell.objectID != "Empty")
            {
                GameObject spawned = SpawnObjectAtCell(cell.objectID, cell.position);
                if (spawned != null)
                {
                    // 스폰된 사물 타일은 플레이어가 겹쳐 걷지 못하도록 Obstacle로 마킹
                    currentGameMap.SetTileState(cell.position, TileState.Obstacle);
                }
            }

            // 플레이어 스폰 이벤트 셀 감지
            if (cell.eventID == "PlayerSpawn")
            {
                _playerSpawnCellCoord = cell.position;
                hasSpawnPoint = true;
            }
        }

        // 4. 플레이어 위치 동기화 및 탐색 모드 초기화
        SetupPlayer(hasSpawnPoint ? _playerSpawnCellCoord : Vector2Int.zero);
    }

    /// <summary>
    /// 특정 셀 좌표에 3D 모델/프리팹을 스폰하고 부모를 GameMap으로 설정합니다.
    /// </summary>
    private GameObject SpawnObjectAtCell(string objectID, Vector2Int cellPos)
    {
        if (AssetCacheManager.instance == null || !AssetCacheManager.instance.TryGetModel(objectID, out GameObject prefab))
        {
            return null;
        }

        GameObject spawned = Instantiate(prefab, currentGameMap.transform);
        Vector3 worldPos = new Vector3(
            cellPos.x * _hideoutMapData.cellSize + _hideoutMapData.gridOffset.x,
            _hideoutMapData.gridOffset.y,
            cellPos.y * _hideoutMapData.cellSize + _hideoutMapData.gridOffset.z
        );

        spawned.transform.position = worldPos;
        _spawnedObjects.Add(spawned);
        return spawned;
    }

    /// <summary>
    /// 세션에 상주하는 플레이어를 은신처 스폰 타일 위치로 이동시키고 탐색 컨트롤러를 초기화합니다.
    /// </summary>
    private void SetupPlayer(Vector2Int spawnCoord)
    {
        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player == null || player.character == null)
        {
            Debug.Log("[HideoutManager] 세션 플레이어가 아직 준비되지 않았습니다. 플레이어 생성 완료를 대기합니다.");
            StartCoroutine(CoWaitAndSetupPlayer(spawnCoord));
            return;
        }

        ApplyPlayerSetup(player, spawnCoord);
    }

    private System.Collections.IEnumerator CoWaitAndSetupPlayer(Vector2Int spawnCoord)
    {
        while (GameSceneManager.instance == null || GameSceneManager.instance.player == null || GameSceneManager.instance.player.character == null)
        {
            yield return null;
        }

        ApplyPlayerSetup(GameSceneManager.instance.player, spawnCoord);
    }

    private void ApplyPlayerSetup(Player player, Vector2Int spawnCoord)
    {
        Vector3 spawnWorldPos = new Vector3(
            spawnCoord.x * _hideoutMapData.cellSize + _hideoutMapData.gridOffset.x,
            _hideoutMapData.gridOffset.y,
            spawnCoord.y * _hideoutMapData.cellSize + _hideoutMapData.gridOffset.z
        );

        player.character.transform.position = spawnWorldPos;

        if (currentGameMap != null)
        {
            Tile spawnTile = currentGameMap.GetTile(spawnCoord.x, spawnCoord.y);
            if (spawnTile != null && player.character.characterMove != null)
            {
                player.character.characterMove.SetCurrentTile(spawnTile);
            }
        }

        // 기존 컨트롤러가 있다면 해제
        playerExploreController?.Dispose();

        // 은신처 탐색 전담 컨트롤러 생성 및 활성화
        playerExploreController = new PlayerExploreController(player);
        playerExploreController.Activate();

        // 카메라 포커스 이동
        if (CameraController.instance != null)
        {
            CameraController.instance.CameraFocusToTarget(spawnWorldPos);
        }
        else if (Camera.main != null)
        {
            // CameraController가 없거나 초기화 전인 경우 메인 카메라를 플레이어 위치로 즉시 이동
            Vector3 camPos = Camera.main.transform.position;
            Camera.main.transform.position = new Vector3(spawnWorldPos.x, camPos.y, spawnWorldPos.z - 7f);
        }

        Debug.Log($"[HideoutManager] 플레이어가 은신처 스폰 좌표({spawnCoord})로 배치 및 ExploreController 활성화 완료.");
    }

    /// <summary>
    /// 은신처 맵 및 스폰 오브젝트 정리
    /// </summary>
    private void ClearHideoutStage()
    {
        playerExploreController?.Dispose();
        playerExploreController = null;
        foreach (var obj in _spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedObjects.Clear();

        if (currentGameMap != null)
        {
            Destroy(currentGameMap.gameObject);
            currentGameMap = null;
        }
    }

    /// <summary>
    /// 던전 진입 게이트 등에서 출격을 요청할 때 호출됩니다.
    /// 출격 전 확인 절차 또는 검사를 거친 후 GameSceneManager를 통해 던전 서브씬으로 전환합니다.
    /// </summary>
    public void EnterDungeon()
    {
        Debug.Log("[HideoutManager] 던전 출격을 요청받았습니다. TransitionToDungeon을 진행합니다.");

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
