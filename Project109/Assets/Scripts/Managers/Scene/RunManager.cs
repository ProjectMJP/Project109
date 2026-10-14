using System.Collections.Generic;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager instance { get; private set; }

    public BattleManager battleManager { get; private set; }

    [Header("Scene References")]
    [SerializeField] private GameMap sceneGameMap;
    public GameMap SceneGameMap => sceneGameMap;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;

            // DontDestroyOnLoad(this.gameObject);
            battleManager = new();
            battleManager.Initialize();
            battleManager.OnBattleWon += HandleBattleWon;
            battleManager.OnBattleLost += HandleBattleLost;

            // 씬에 배치된 GameMap이 인스펙터에 미할당된 경우 자동 탐색
            if (sceneGameMap == null)
            {
                sceneGameMap = GameMap.current != null ? GameMap.current : UnityEngine.Object.FindFirstObjectByType<GameMap>();
            }

            currentMap = new MapManager();
            currentMap.Initialize(sceneGameMap);

            exploreMapManager = new ExploreMapManager();
        }
        else
        {
            Destroy(this.gameObject);
        }
    }


    [Header("Player Controllers")]
    public PlayerBattleController playerBattleController;
    public PlayerExploreController playerExploreController;

    private ICharacterController _activePlayerController;
    public ICharacterController activePlayerController => _activePlayerController;

    [Header("Map")]
    //맵 관련 변수
    public string currentMapName;
    public int currentStageLevel = 0;
    public int currentExploreMapFloor = 0;
    [System.NonSerialized] public ExploreNode currentExploreNode;
    [System.NonSerialized] public ExploreNode beforeExploreNode;

    public MapManager currentMap;
    public ExploreMapManager exploreMapManager;
    public ExploreMapUI currentExploreUI;

    private void Start()
    {
        // 1. 플레이어 컨트롤러 인스턴스 초기화 (Player 자체는 GameSceneManager가 소유)
        InitializePlayerInstances();

        // 2. UI 및 리워드 매니저 이벤트 바인딩
        if (GameItemRewardManager.instance != null)
        {
            GameItemRewardManager.instance.SubscribeToPlayerEvents();
            GameItemRewardManager.instance.UpdateItemList();
        }
    }

    private void InitializePlayerInstances()
    {
        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player != null)
        {
            playerBattleController = new PlayerBattleController(player);
            playerExploreController = new PlayerExploreController(player);
            _activePlayerController = playerExploreController; // 기본적으로 탐색 컨트롤러가 액티브
        }
    }

    private void Update()
    {
        if (currentMap.currentMapState == MapState.Battle)
        {
            battleManager?.Update(Time.deltaTime);
        }
    }

    public void InitRun()
    {
        if (playerBattleController != null)
        {
            playerBattleController.OnTurnStart();
        }
    }

    public void SaveGame()
    {
        int slot = GameSceneManager.instance != null ? GameSceneManager.instance.currentSaveSlot : 1;
        SaveSystem.SaveGame(this, slot);
    }

    public void LoadRun()
    {
        int slot = GameSceneManager.instance != null ? GameSceneManager.instance.currentSaveSlot : 1;
        LoadRun(slot);
    }

    public void LoadRun(int slotIndex)
    {
        RunSaveData data = SaveSystem.LoadGameData(slotIndex);
        if (data == null)
        {
            Debug.LogWarning($"[RunManager] 슬롯 {slotIndex}에 로드할 게임 데이터가 존재하지 않습니다.");
            return;
        }

        // 1. 던전 런 진행도 메타 복구 (SaveSystem에 위임)
        SaveSystem.RestoreRunData(data, this);

        if (SceneLoadManager.instance != null)
        {
            SceneLoadManager.instance.FadeIn();
        }
    }

    public void AddInGame_Currency(CurrencyType type, int amount)
    {
        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player == null || player.playerStat == null) return;

        switch (type)
        {
            case CurrencyType.Gold:
                player.playerStat.InGameCurrencyGold += amount;
                break;
            case CurrencyType.MemorySharp:
                player.playerStat.InGameCurrencyMemorySharp += amount;
                break;
            default:
                break;
        }
    }

    public void OnMapStateChanged(MapState newState)
    {
        // 이전 탐색 컨트롤러 해제
        if (_activePlayerController == playerExploreController && playerExploreController != null)
        {
            playerExploreController.Deactivate();
        }

        if (newState == MapState.Battle)
        {
            _activePlayerController = playerBattleController;

            // 전투 매니저 시작!
            List<ICharacterController> players = new List<ICharacterController> { playerBattleController };
            List<ICharacterController> enemies = new List<ICharacterController>();
            if (currentMap != null && currentMap.currentEnemyControllers != null)
            {
                enemies.AddRange(currentMap.currentEnemyControllers);
            }

            if (battleManager != null)
            {
                battleManager.InitBattle(players, enemies);
            }

            if (playerBattleController != null)
            {
                playerBattleController.OnBattleStart();
            }

            foreach (var enemy in enemies)
            {
                if (enemy is NPCUnitController enemyCtrl)
                {
                    enemyCtrl.EvaluateNextIntent();
                }
            }
        }
        else
        {
            _activePlayerController = playerExploreController;
            if (playerExploreController != null)
            {
                playerExploreController.Activate();
            }

            if (UIManager.instance != null)
            {
                UIManager.instance.ClearAllCharacterStatusBars();
            }
            if (battleManager != null)
            {
                battleManager.ClearBattle();
            }
        }
    }

    #region Battle Event Handlers

    private void HandleBattleWon()
    {
        Debug.Log("[RunManager] 전투 승리 이벤트 수신: 맵 상태 복귀 및 보상 처리 시작");

        if (currentMap != null)
        {
            currentMap.currentMapState = MapState.None;
        }

        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (GameItemRewardManager.instance != null && player != null && player.character != null)
        {
            Vector3 spawnPos = player.character.transform.position;
            GameItemRewardManager.instance.SpawnRewardBox(spawnPos);
        }

        OnMapStateChanged(MapState.None);
    }

    private void HandleBattleLost()
    {
        Debug.Log("[RunManager] 전투 패배 이벤트 수신");
        // TODO: 향후 패배 결과창 또는 게임 오버 처리 추가
    }

    #endregion

    void OnDestroy()
    {
        // C# 컨트롤러 및 매니저 생명주기 마무리 (Dispose 일괄 호출)
        playerBattleController?.Dispose();
        playerExploreController?.Dispose();

        if (battleManager != null)
        {
            battleManager.OnBattleWon -= HandleBattleWon;
            battleManager.OnBattleLost -= HandleBattleLost;
            battleManager.Dispose();
        }

        currentMap?.Dispose();


        if (instance != null)
        {
            instance = null;
        }
    }

    public void MoveToNode(ExploreNodeUI nextNodeUI)
    {
        if (nextNodeUI == null || nextNodeUI.NodeData == null) return;

        if (UIManager.instance != null)
        {
            UIManager.instance.ClearAllCharacterStatusBars();
        }

        // 이전의 노드를 저장 후 다음 노드로 변경
        beforeExploreNode = currentExploreNode;
        currentExploreNode = nextNodeUI != null ? nextNodeUI.NodeData : null;
        currentExploreMapFloor += 1;

        // 다음 층으로 이동했으므로 플레이어 시야 범위 내의 노드 데이터 공개 상태 갱신
        if (exploreMapManager != null)
        {
            int checkLength = GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null
                ? GameSceneManager.instance.player.playerStat.MapFloorCheckLength
                : 3;
            exploreMapManager.RevealNodesByFloor(currentExploreMapFloor, checkLength);
        }

        // 이동 시작 시 화면에 열려있는 ExploreMap 창 닫기 요청
        if (UIManager.instance != null)
        {
            UIManager.instance.CloseUI(UIConstants.PANEL_EXPLORE_MAP);
        }

        if (SceneLoadManager.instance != null)
        {
            SceneLoadManager.instance.FadeIn(0.35f, () =>
            {
                LoadCurrentNodeDataInMap();
            });
        }
        else
        {
            LoadCurrentNodeDataInMap();
        }
    }

    private void LoadCurrentNodeDataInMap()
    {
        Debug.Log("currentExploreNode = " + (currentExploreNode != null ? currentExploreNode.encounterData.encountType.ToString() : "null"));

        if (currentExploreNode != null && currentMap != null)
        {
            currentMap.GenerateStage(currentExploreNode);

            switch (currentExploreNode.encounterData.extraEncountType)
            {
                case ExtraEncountType.None:
                    break;
                case ExtraEncountType.Insight:
                    if (!string.IsNullOrEmpty(currentExploreNode.encounterData.interactableDataId) &&
                        ModLoader.Instance != null && ModLoader.Instance.InteractableDatabase != null &&
                        ModLoader.Instance.InteractableDatabase.TryGetValue(currentExploreNode.encounterData.interactableDataId, out var interactableData))
                    {
                        currentMap.GenerateNPC(currentMap.currentMapData, EncountType.Secret, interactableData);
                    }
                    break;
                case ExtraEncountType.ShineWell:
                    break;
            }

            // 맵 생성이 끝난 후 RunManager에게 상태 전이 알림
            OnMapStateChanged(currentMap.currentMapState);
        }

        if (SceneLoadManager.instance != null)
        {
            SceneLoadManager.instance.FadeOut(0.35f);
        }
    }

    public void SpawnMonsterInBattleNodeData(BattleData nodeData)
    {
        // 맵에 몬스터 스폰 처리가 필요한 경우 여기에 구현
    }
}
