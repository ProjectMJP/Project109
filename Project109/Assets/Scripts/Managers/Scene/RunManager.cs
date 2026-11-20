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

            currentStage = new StageManager();
            currentStage.Initialize(sceneGameMap);

            exploreMapManager = new ExploreMapManager();

            EnsurePlayerInstancesInitialized();
            LuaManager.OnBeforeLuaEnvDispose -= CleanupRunManagers;
            LuaManager.OnBeforeLuaEnvDispose += CleanupRunManagers;
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

    [Header("Stage & Map")]
    //맵 관련 변수
    public string currentMapName;
    public int currentStageLevel = 0;
    public int currentExploreMapFloor = 0;
    [System.NonSerialized] public ExploreNode currentExploreNode;
    [System.NonSerialized] public ExploreNode beforeExploreNode;

    public StageManager currentStage;
    public ExploreMapManager exploreMapManager;
    public ExploreMapUI currentExploreUI;

    public static event System.Action OnRunLocationChanged;

    private void Start()
    {
        // 1. 플레이어 컨트롤러 인스턴스 초기화 (Player 자체는 GameSceneManager가 소유)
        EnsurePlayerInstancesInitialized();
        if (_activePlayerController == playerExploreController &&
            playerExploreController != null &&
            (currentStage == null || currentStage.currentMapState != MapState.Battle))
        {
            playerExploreController.Activate();
        }
        OnRunLocationChanged?.Invoke();

        // 2. 리워드 매니저 아이템 목록 동기화
        if (GameItemRewardManager.instance != null)
        {
            GameItemRewardManager.instance.UpdateItemList();
        }
    }

    /// <summary>
    /// 플레이어 배틀/탐색 컨트롤러가 아직 생성되지 않은 경우 1회만 생성합니다. (멱등 보장)
    /// Start() 이전의 세이브 복원 또는 맵 상태 전환 시점에서도 안전하게 선초기화되며,
    /// 이미 활성화된 _activePlayerController를 덮어쓰지 않습니다.
    /// </summary>
    public bool EnsurePlayerInstancesInitialized()
    {
        if (playerBattleController != null && playerExploreController != null)
        {
            if (_activePlayerController == null)
            {
                _activePlayerController = playerExploreController;
            }
            return true;
        }

        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player == null)
        {
            return false;
        }

        if (playerBattleController == null)
        {
            playerBattleController = new PlayerBattleController(player);
        }

        if (playerExploreController == null)
        {
            playerExploreController = new PlayerExploreController(player);
        }

        if (_activePlayerController == null)
        {
            _activePlayerController = playerExploreController;
        }

        return true;
    }

    private void Update()
    {
        if (currentStage != null && currentStage.currentMapState == MapState.Battle)
        {
            battleManager?.Update(Time.deltaTime);
        }
    }

    /// <summary>
    /// 던전 런 종료 시 또는 은신처 복귀 시 층수, 노드, 맵 데이터를 완전히 리셋합니다.
    /// </summary>
    public void ResetRunProgress()
    {
        currentStageLevel = 1;
        currentExploreMapFloor = 0;
        currentExploreNode = null;
        beforeExploreNode = null;
        OnRunLocationChanged?.Invoke();

        if (exploreMapManager != null)
        {
            exploreMapManager.ClearMap();
        }

        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null)
        {
            GameSceneManager.instance.player.playerStat.ShopRerollCount = 0;
        }

        Debug.Log("[RunManager] 런 진행 데이터(스테이지, 층수, 탐험 노드, 맵)가 초기화되었습니다.");
    }


    public void SaveGame()
    {
        int slot = GameSceneManager.instance != null ? GameSceneManager.instance.currentSaveSlot : 1;
        SaveSystem.SaveGame(this, slot);
    }

    /// <summary>
    /// 탐험 맵 UI가 생성되지 않았거나 노드가 비어 있으면 생성·초기화하고, 화면에 표시되도록 엽니다.
    /// </summary>
    public ExploreMapUI OpenOrShowExploreMap(int mapLength = 15)
    {
        if (UIManager.instance == null) return null;

        ExploreMapUI exploreMapUI = UIManager.instance.GetUI<ExploreMapUI>(UIConstants.PANEL_EXPLORE_MAP);
        if (exploreMapUI == null)
        {
            // 비활성 상태로 먼저 생성하여 CreateExploreMap 이후 PushActiveUIPanel 시 OnOpen(화살표/시각 동기화)이 한 번에 실행되도록 함
            exploreMapUI = UIManager.instance.OpenUI<ExploreMapUI>(UIConstants.PANEL_EXPLORE_MAP, UILayerType.Normal, false);
        }

        if (exploreMapUI == null) return null;

        currentExploreUI = exploreMapUI;

        if (exploreMapUI.MapNodes == null || exploreMapUI.MapNodes.Count == 0)
        {
            exploreMapUI.CreateExploreMap(mapLength);
        }

        if (!exploreMapUI.gameObject.activeSelf)
        {
            UIManager.instance.PushActiveUIPanel(exploreMapUI);
        }

        return exploreMapUI;
    }

    /// <summary>
    /// 세이브 데이터 복원 후 노드 진행 상태에 맞춰 스테이지를 재구축하거나 맵 창을 엽니다.
    /// </summary>
    public void ResumeRunAfterLoad()
    {
        EnsurePlayerInstancesInitialized();
        OnRunLocationChanged?.Invoke();
        if (currentExploreNode == null || currentExploreNode.isCleared)
        {
            OpenOrShowExploreMap();
        }
        else
        {
            LoadCurrentNodeDataInMap();
        }
    }

    public void OnMapStateChanged(MapState newState)
    {
        EnsurePlayerInstancesInitialized();

        // 이전 탐색 컨트롤러 해제
        if (_activePlayerController == playerExploreController && playerExploreController != null)
        {
            playerExploreController.Deactivate();
        }

        if (newState == MapState.Battle)
        {
            if (playerBattleController == null)
            {
                Debug.LogError("[RunManager] playerBattleController가 초기화되지 않아 전투 상태 전환을 중단합니다.");
                return;
            }

            _activePlayerController = playerBattleController;

            // 전투 매니저 시작!
            List<ICharacterController> players = new List<ICharacterController> { playerBattleController };
            List<ICharacterController> enemies = new List<ICharacterController>();
            if (currentStage != null && currentStage.currentEnemyControllers != null)
            {
                enemies.AddRange(currentStage.currentEnemyControllers);
            }

            if (battleManager != null)
            {
                battleManager.InitBattle(players, enemies);
            }

            playerBattleController.OnBattleStart();

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
                UIManager.instance.ClearWorldUILayer();
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

        if (currentStage != null)
        {
            currentStage.currentMapState = MapState.None;
        }

        // 전투 중 누적된 처치 골드를 보상 상자에 안전하게 이관
        int battleGold = (battleManager != null) ? battleManager.accumulativeGoldReward : 0;
        if (battleManager != null)
        {
            battleManager.accumulativeGoldReward = 0; // 중복 합산 방지
        }

        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (GameItemRewardManager.instance != null && player != null && player.character != null)
        {
            Vector3 spawnPos = player.character.transform.position;
            GameItemRewardManager.instance.SpawnRewardBox(spawnPos, battleGold);
        }

        if (currentExploreNode != null)
        {
            currentExploreNode.isCleared = true;
        }
        SaveGame();

        OnMapStateChanged(MapState.None);
    }

    private void HandleBattleLost()
    {
        Debug.Log("[RunManager] 전투 패배 이벤트 수신: 게임 오버 결과 패널을 엽니다.");
        if (UIManager.instance != null)
        {
            GameOverPanel panel = UIManager.instance.OpenUI<GameOverPanel>(UIConstants.PANEL_GAME_OVER, UILayerType.Popup, true);
            if (panel != null)
            {
                panel.Setup(currentExploreMapFloor, currentMapName);
            }
        }
    }

    #endregion

    private void CleanupRunManagers()
    {
        if (ActionQueueManager.HasInstance)
        {
            ActionQueueManager actionQueue = ActionQueueManager.Instance;
            if (actionQueue != null)
            {
                actionQueue.ClearQueue(invokeCanceled: false);
            }
        }

        // C# 컨트롤러 및 매니저 생명주기 마무리 (Dispose 일괄 호출, 멱등 보장)
        if (playerBattleController != null)
        {
            playerBattleController.Dispose();
            playerBattleController = null;
        }
        if (playerExploreController != null)
        {
            playerExploreController.Dispose();
            playerExploreController = null;
        }
        _activePlayerController = null;

        if (battleManager != null)
        {
            battleManager.OnBattleWon -= HandleBattleWon;
            battleManager.OnBattleLost -= HandleBattleLost;
            battleManager.Dispose();
            battleManager = null;
        }

        if (currentStage != null)
        {
            currentStage.Dispose();
            currentStage = null;
        }
    }

    void OnDestroy()
    {
        LuaManager.OnBeforeLuaEnvDispose -= CleanupRunManagers;
        CleanupRunManagers();

        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// 플레이어가 전투 중 탐험 지도 이동 제한을 우회할 수 있는지(테스트 통행증 등) 확인합니다.
    /// </summary>
    public bool HasBattleMapLockBypass(Player playerOverride = null)
    {
        Player player = playerOverride ?? (GameSceneManager.instance != null ? GameSceneManager.instance.player : null);
        return player != null && player.playerStat != null && player.playerStat.CanBypassBattleMapLock;
    }

    /// <summary>
    /// 현재 게임 상태에 따라 탐험 지도를 통해 다음 노드로 이동할 수 있는지 판정합니다.
    /// </summary>
    public bool CanMoveToNextNode(out string blockReason)
    {
        bool isDialogueActive = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;
        return CanMoveToNextNode(null, isDialogueActive, out blockReason);
    }

    /// <summary>
    /// 플레이어 및 대화 상태 주입을 지원하여 탐험 지도 다음 노드 이동 가능 여부와 차단 사유를 판정합니다.
    /// </summary>
    public bool CanMoveToNextNode(Player playerOverride, bool isDialogueActive, out string blockReason)
    {
        if (isDialogueActive)
        {
            blockReason = "NPC 대화 중에는 다음 노드로 이동할 수 없습니다.";
            return false;
        }

        bool isBattleActive =
            (currentStage != null && currentStage.currentMapState == MapState.Battle) ||
            (battleManager != null && battleManager.battleState != BattleState.None && battleManager.battleState != BattleState.BattleEnd);

        if (isBattleActive)
        {
            if (HasBattleMapLockBypass(playerOverride))
            {
                blockReason = string.Empty;
                return true;
            }

            blockReason = "전투를 완료해야 다음 노드로 이동할 수 있습니다.";
            return false;
        }

        blockReason = string.Empty;
        return true;
    }

    /// <summary>
    /// 테스트 우회 등으로 전투 중 다음 노드로 강제 이동할 때 액션 큐, 전투 상태, 컨트롤러 및 전투 UI를 안전하게 정리합니다.
    /// </summary>
    public void CleanupActiveBattleForForcedMove()
    {
        if (ActionQueueManager.HasInstance)
        {
            ActionQueueManager.Instance.ClearQueue();
        }

        if (currentStage != null)
        {
            currentStage.currentMapState = MapState.None;
        }

        if (playerBattleController != null)
        {
            playerBattleController.CancelCurrentState();
        }

        OnMapStateChanged(MapState.None);

        // OnMapStateChanged 내부의 DispatchBattleEnd를 타지 못했거나 잔여 UI가 남은 경우를 대비한 멱등 정리 보장
        if (playerBattleController != null)
        {
            playerBattleController.OnBattleEnd();
        }

        if (UIManager.instance != null)
        {
            UIManager.instance.ClearEffectAreaTiles();
        }
    }

    public void MoveToNode(ExploreNodeUI nextNodeUI)
    {
        if (nextNodeUI == null || nextNodeUI.NodeData == null) return;

        if (!CanMoveToNextNode(out string blockReason))
        {
            Debug.LogWarning($"[RunManager] 노드 이동이 차단되었습니다: {blockReason}");
            return;
        }

        if ((currentStage != null && currentStage.currentMapState == MapState.Battle) ||
            (battleManager != null && battleManager.battleState != BattleState.None))
        {
            CleanupActiveBattleForForcedMove();
        }

        if (UIManager.instance != null)
        {
            UIManager.instance.ClearWorldUILayer();
        }

        // 이전의 노드를 저장 후 다음 노드로 변경
        beforeExploreNode = currentExploreNode;
        if (beforeExploreNode != null)
        {
            beforeExploreNode.isVisited = true;
        }

        currentExploreNode = nextNodeUI != null ? nextNodeUI.NodeData : null;
        if (currentExploreNode != null)
        {
            currentExploreNode.isVisited = true;
        }
        currentExploreMapFloor += 1;
        OnRunLocationChanged?.Invoke();

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
                SaveGame();
            });
        }
        else
        {
            LoadCurrentNodeDataInMap();
            SaveGame();
        }
    }

    private void LoadCurrentNodeDataInMap()
    {
        Debug.Log("currentExploreNode = " + (currentExploreNode != null ? currentExploreNode.encounterData.encountType.ToString() : "null"));

        if (currentExploreNode != null && currentStage != null)
        {
            currentStage.GenerateStage(currentExploreNode);

            switch (currentExploreNode.encounterData.extraEncountType)
            {
                case ExtraEncountType.None:
                    break;
                case ExtraEncountType.Insight:
                    if (!string.IsNullOrEmpty(currentExploreNode.encounterData.interactableDataId) &&
                        ModLoader.Instance != null && ModLoader.Instance.InteractableDatabase != null &&
                        ModLoader.Instance.InteractableDatabase.TryGetValue(currentExploreNode.encounterData.interactableDataId, out var interactableData))
                    {
                        currentStage.GenerateNPC(currentStage.currentMapData, EncountType.Secret, interactableData);
                    }
                    break;
                case ExtraEncountType.ShineWell:
                    break;
            }

            // 맵 생성이 끝난 후 RunManager에게 상태 전이 알림
            OnMapStateChanged(currentStage.currentMapState);
            OnRunLocationChanged?.Invoke();
        }

        if (SceneLoadManager.instance != null)
        {
            SceneLoadManager.instance.FadeOut(0.35f);
        }
    }

    public void SpawnMonsterInBattleNodeData(BattleData nodeData)
    {
        if (nodeData == null || currentStage == null) return;

        LocationType loc = (currentStage.currentMapData != null) ? currentStage.currentMapData.locationType : LocationType.Temple;
        Character playerChar = (GameSceneManager.instance != null && GameSceneManager.instance.player != null) ? GameSceneManager.instance.player.character : null;

        currentStage.GenerateStage(loc, EncountType.Battle, playerChar, null, nodeData);
        OnMapStateChanged(MapState.Battle);
    }
}
