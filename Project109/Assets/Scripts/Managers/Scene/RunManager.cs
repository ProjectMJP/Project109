using System.Collections.Generic;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager instance { get; private set; }

    public BattleManager battleManager { get; private set; }

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

            currentMap = new MapManager();
            currentMap.Initialize();
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

    [Header("Map Settings")]
    [SerializeField] private MapPrefabs mapPrefabs;
    public MapPrefabs MapPrefabs => mapPrefabs;

    [Header("Map")]
    //맵 관련 변수
    public string currentMapName;
    public int currentStageLevel = 0;
    public int currentExploreMapFloor = 0;
    public IncountNode currentIncountNode;
    public IncountNode beforeIncountNode;
    public MapManager currentMap = new();
    public ExploreUI currentExploreUI;

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

        // 1. 기본 게임 메타 복구
        currentMapName = data.currentMapName;
        currentStageLevel = data.currentStageLevel;
        currentExploreMapFloor = data.currentExploreMapFloor;

        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player == null)
        {
            Debug.LogError("[RunManager] 로드할 Player 인스턴스가 GameSceneManager에 없습니다.");
            return;
        }

        if (player.playerStat == null)
        {
            player.playerStat = new PlayerStat();
        }

        player.playerStat.InGameCurrencyGold = data.playerGold;
        player.playerStat.InGameCurrencyMemorySharp = data.playerMemorySharp;

        if (player.character != null)
        {
            player.character.curHealth = data.playerCurHealth;
        }

        // 2. 플레이어 덱 복구
        if (player.deck != null)
        {
            player.deck.Clear(); // 기존 덱 카드 초기화

            // 이제 세이브로부터 카드 생성 및 속성 복구
            HashSet<string> restoredCardMasteries = new HashSet<string>();
            foreach (var cardEntry in data.playerDeckCards)
            {
                if (ModLoader.Instance.CardDatabase.TryGetValue(cardEntry.cardName, out CardData cardData))
                {
                    Card newCard = player.deck.AddCard(cardData);
                    if (newCard != null)
                    {
                        // 동일한 카드 이름에 대해서 최초 1회만 마스터리 업그레이드 데이터를 복구합니다 (공유 상태이므로).
                        if (!restoredCardMasteries.Contains(cardEntry.cardName))
                        {
                            restoredCardMasteries.Add(cardEntry.cardName);
                            // AddMastery(upgrade.masteryId)를 count 번만큼 호출하여 순차적으로 마스터리 복구
                            if (cardEntry.masteryUpgrades != null)
                            {
                                foreach (var upgrade in cardEntry.masteryUpgrades)
                                {
                                    for (int i = 0; i < upgrade.count; i++)
                                    {
                                        newCard.AddMastery(upgrade.masteryId);
                                    }
                                }
                            }
                            // AddMastery 수행 시 XP 차감 연산이 들어가므로, 최종 마스터리 XP를 세이브값으로 명확히 덮어씌워 줍니다.
                            newCard.currentMasteryXP = cardEntry.currentMasteryXP;
                        }
                        else
                        {
                            // 이미 다른 동명 카드에 의해 공유 마스터리가 세팅되었으므로,
                            // 복제본의 수동 복원 작업(코스트 갱신 및 마스터리 태그 등록 등)만 적용합니다.
                            if (newCard.masteryUpgrades != null && cardData.masteryTags != null)
                            {
                                foreach (var kvp in newCard.masteryUpgrades)
                                {
                                    string masteryId = kvp.Key;
                                    int count = kvp.Value;
                                    if (cardData.masteryTags.TryGetValue(masteryId, out var tags))
                                    {
                                        for (int i = 0; i < count; i++)
                                        {
                                            foreach (var tag in tags)
                                            {
                                                newCard.AddTag(tag);
                                            }
                                        }
                                    }
                                }
                            }

                            // 코스트 재계산
                            float costMod = newCard.GetEffectiveValue("cost");
                            if (costMod != 0)
                            {
                                newCard.currentCost = Mathf.Max(0, cardData.stamina + (int)costMod);
                            }
                        }
                    }
                }
            }
            player.deck.RequestAllCardRefresh();
        }

        // 3. 유물 복구
        if (player.relicManager != null)
        {
            player.relicManager.ClearRelics();
            foreach (var relicName in data.playerRelicNames)
            {
                player.AddRelic(relicName);
            }
        }

        Debug.Log($"[RunManager] 슬롯 {slotIndex} 세이브 파일로부터 이전 세션 데이터를 완벽히 복구했습니다.");

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

    public void MoveToNode(IncountNode nextNode)
    {
        if (nextNode == null) return;

        if (UIManager.instance != null)
        {
            UIManager.instance.ClearAllCharacterStatusBars();
        }

        // 이전의 노드를 저장 후 다음 노드로 변경
        beforeIncountNode = currentIncountNode;
        if (currentIncountNode != null)
        {
            var prevNodeComponent = currentIncountNode.transform.GetComponent<IncountNode>();
            if (prevNodeComponent != null && prevNodeComponent.IncountNodeCurrentHighlightCircleObject != null)
            {
                prevNodeComponent.IncountNodeCurrentHighlightCircleObject.SetActive(false);
            }
        }

        currentIncountNode = nextNode; // 다음 맵 로딩을 위해 이동할 노드 정보를 저장
        currentExploreMapFloor += 1;

        if (nextNode.exploreUI != null)
        {
            if (nextNode.IncountNodeCurrentHighlightCircleObject != null)
            {
                nextNode.IncountNodeCurrentHighlightCircleObject.SetActive(true);
            }
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
        // 다음 노드로 이동하였으니 다음 노드들의 가려진 부분들 중 일부가 보이도록 ExploreMap 업데이트
        if (currentExploreUI != null)
        {
            currentExploreUI.OpenExploreMapNodesBasedOnFloorLength();
            // 이전에 이동한 노드를 제외한 나머지 노드 가리기
            currentExploreUI.CloseBeforeNodes();
            currentExploreUI.Close();
        }

        Debug.Log("currentIncountNode = " + currentIncountNode.incountType.ToString());

        IncountNode newIncountNode = currentIncountNode;
        if (newIncountNode != null)
        {
            var mapManager = currentMap;
            var playerChar = GameSceneManager.instance != null && GameSceneManager.instance.player != null
                ? GameSceneManager.instance.player.character
                : null;

            switch (newIncountNode.incountType)
            {
                case IncountType.None:
                    break;
                case IncountType.Battle:
                    mapManager.GenerateStage(LocationType.Temple, IncountType.Battle, playerChar, MapPrefabs, null, newIncountNode.battleNodeData);
                    break;
                case IncountType.Elite:
                    mapManager.GenerateStage(LocationType.Temple, IncountType.Elite, playerChar, MapPrefabs, null, newIncountNode.battleNodeData);
                    break;
                case IncountType.Boss:
                    mapManager.GenerateStage(LocationType.Temple, IncountType.Boss, playerChar, MapPrefabs, null, newIncountNode.battleNodeData);
                    break;
                case IncountType.Restore:
                    mapManager.GenerateStage(LocationType.Temple, IncountType.Restore, playerChar, MapPrefabs);
                    break;
                case IncountType.Store:
                    mapManager.GenerateStage(LocationType.Temple, IncountType.Store, playerChar, MapPrefabs);
                    break;
                case IncountType.SecretBox:
                    mapManager.GenerateStage(LocationType.Temple, IncountType.SecretBox, playerChar, MapPrefabs);
                    break;
                case IncountType.Secret:
                    if (newIncountNode.eventNodeData != null)
                    {
                        mapManager.GenerateStage(LocationType.Temple, IncountType.Secret, playerChar, MapPrefabs, newIncountNode.eventNodeData);
                    }
                    break;
                default:
                    break;
            }

            switch (newIncountNode.extraIncountType)
            {
                case ExtraIncountType.None:
                    break;
                case ExtraIncountType.Insight:
                    if (newIncountNode.eventNodeData != null)
                    {
                        mapManager.GenerateNPC(mapManager.currentMapData, IncountType.Secret, MapPrefabs, newIncountNode.eventNodeData);
                    }
                    break;
                case ExtraIncountType.ShineWell:
                    break;
            }

            // 맵 생성이 끝난 후 RunManager에게 상태 전이 알림
            OnMapStateChanged(mapManager.currentMapState);
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
