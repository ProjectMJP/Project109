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


    [SerializeField]
    private Character _startingCharacter;

    [Header("Player")]
    //플레이어 관리 (세이브 슬롯은 최상위 GameSceneManager가 소유)
    private int _internalSaveSlot = 1;
    public int currentSaveSlot
    {
        get => GameSceneManager.instance != null ? GameSceneManager.instance.currentSaveSlot : _internalSaveSlot;
        set => _internalSaveSlot = value;
    }
    public Player player;
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



    [Header("Temp")]
    public PlayerStat testPlayerStat;

    [Header("Lobby Settings")]
    [SerializeField] private string _selectedStarterKitId = "warrior_starter";

    public void SetTempStarterKit(string kitId)
    {
        _selectedStarterKitId = kitId;
    }

    public void EnterDungeonRun()
    {
        if (player == null) return;

        // 1. 기존 플레이어 덱 및 유물 상태 완전 청소
        if (player.deck != null) player.deck.Clear();
        if (player.relicManager != null) player.relicManager.ClearRelics();

        // 2. 임시 보관되어 있던 로드아웃 ID로 시작 장비 지급
        ApplyStarterKit(_selectedStarterKitId);

        // 3. 던전 씬 진입 처리
        currentStageLevel = 1;
        currentExploreMapFloor = 1;

        if (FadeManager.instance != null)
        {
            FadeManager.instance.FadeIn();
        }
    }

    public void ApplyStarterKit(string loadoutId)
    {
        if (player == null) return;

        // 기존 덱과 유물 초기화
        if (player.deck != null) player.deck.Clear();
        if (player.relicManager != null) player.relicManager.ClearRelics();

        // 캐릭터 에셋 기본 스탯 로드
        CharacterData characterData;
        AssetCacheManager.instance.TryGetCharacter("전투광", out characterData);

        if (ModLoader.Instance.StarterKitDatabase.TryGetValue(loadoutId, out StarterKitData kitData))
        {
            player.playerStat.InGameCurrencyGold = kitData.startGold;

            if (kitData.characterStat != null && playerBattleController != null && playerBattleController.controlledCharacter != null)
            {
                playerBattleController.controlledCharacter.InitializeStat(kitData.characterStat);
            }
            else
            {
                Debug.LogError("[RunManager] 시작 키트에 'characterStat' 스탯 정보가 정의되어 있지 않거나 캐릭터 컨트롤러가 없습니다.");
            }

            foreach (string cardId in kitData.startCards)
            {
                if (ModLoader.Instance.CardDatabase.TryGetValue(cardId, out CardData cardData))
                {
                    player.deck.AddCard(cardData);
                }
                else
                {
                    Debug.LogWarning($"[RunManager] 시작 키트의 '{cardId}' 카드가 CardDatabase에 존재하지 않습니다.");
                }
            }

            foreach (string relicId in kitData.startRelics)
            {
                player.AddRelic(relicId);
            }

            foreach (var effInfo in kitData.startEffects)
            {
                if (ModLoader.Instance.EffectDatabase.TryGetValue(effInfo.effectName, out EffectData effectData))
                {
                    EventStructs.EffectInfo info = new EventStructs.EffectInfo(
                        player.character,
                        player.character,
                        new Effect(effectData, effectData.luaPrototype),
                        effInfo.stack,
                        effInfo.duration
                    );
                    player.character.TakeEffect(info);
                }
            }

            player.deck.RequestAllCardRefresh();
            Debug.Log($"[RunManager] '{kitData.displayName}'({loadoutId}) 로드아웃이 플레이어에게 성공적으로 적용되었습니다.");
        }
        else
        {
            Debug.LogError($"[RunManager] '{loadoutId}' 시작 키트를 StarterKitDatabase에서 찾을 수 없습니다.");
        }
    }

    private void Start()
    {
        // 1. 최상위 GameSceneManager에 던전 서브씬 매니저로 등록
        if (GameSceneManager.instance != null)
        {
            GameSceneManager.instance.RegisterDungeonManager(this);
        }

        // 2. 플레이어 및 컨트롤러 인스턴스 초기화
        InitializePlayerInstances();

        // 3. UI 및 리워드 매니저 이벤트 바인딩
        if (GameItemRewardManager.instance != null)
        {
            GameItemRewardManager.instance.SubscribeToPlayerEvents();
            GameItemRewardManager.instance.UpdateItemList();
        }

        // 3. UI 및 리워드 매니저 이벤트 바인딩
        if (GameItemRewardManager.instance != null)
        {
            GameItemRewardManager.instance.SubscribeToPlayerEvents();
            GameItemRewardManager.instance.UpdateItemList();
        }

        InitPlayerHUD();

#if UNITY_EDITOR
        // 4. [에디터 단독 테스트 전용] 타이틀/은신처 씬을 거치지 않고 에디터에서 GameScene을 직접 재생한 경우
        //    (세이브 파일도 없고 덱이 비어있을 때만 임시 테스트 세팅 주입)
        if (player.deck == null || player.deck.CardCount == 0)
        {
            StartCoroutine(CoSetupDebugStandalone());
        }
#endif
    }

    private void InitializePlayerInstances()
    {
        if (player == null)
        {
            player = new Player(_startingCharacter);
        }
        playerBattleController = new PlayerBattleController(player);
        playerExploreController = new PlayerExploreController(player);
        _activePlayerController = playerExploreController; // 기본적으로 탐색 컨트롤러가 액티브
    }

    /// <summary>
    /// 플레이어의 TopHUDPanel을 생성하고 플레이어 스탯 및 유물 데이터를 바인딩합니다.
    /// </summary>
    public void InitPlayerHUD()
    {
        if (player == null || UIManager.instance == null) return;

        GameObject hudObj = UIManager.instance.OpenUI(UIConstants.PANEL_TOP_HUD, UILayerType.Top, true);
        if (hudObj != null && hudObj.TryGetComponent<TopHUDPanel>(out var hud))
        {
            hud.BindPlayer(player);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// [에디터 단독 테스트 전용] 타이틀을 거치지 않고 GameScene을 직접 켰을 때 카드/스탯을 임시 세팅합니다.
    /// 릴리즈 빌드 시 컴파일되지 않습니다.
    /// </summary>
    private System.Collections.IEnumerator CoSetupDebugStandalone()
    {
        float timeout = 5.0f;
        float elapsed = 0f;

        while ((AssetCacheManager.instance == null || !AssetCacheManager.instance.isLoadComplete) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (AssetCacheManager.instance == null || !AssetCacheManager.instance.isLoadComplete)
        {
            Debug.LogWarning("[RunManager:DevOnly] 에셋 캐시 로딩 타임아웃.");
        }

        // 이미 세이브 로드 등으로 덱이 채워져 있다면 임시 주입 스킵 (세이브 데이터 덮어쓰기 방지)
        if (player.deck != null && player.deck.CardCount > 0)
        {
            yield break;
        }

        Debug.LogWarning("[RunManager:DevOnly] 에디터 단독 실행 감지: 테스트용 스타터 킷 및 기본 골드를 임시 세팅합니다.");
        currentMapName = "Temple";
        ApplyStarterKit(_selectedStarterKitId);

        if (player.playerStat != null)
        {
            player.playerStat.InGameCurrencyGold = 100;
            player.playerStat.InGameCurrencyMemorySharp = 1;
        }

        InitPlayerHUD();
    }
#endif

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
        SaveSystem.SaveGame(this, currentSaveSlot);
    }

    public void CreateNewRun(int slotIndex, string starterKitId)
    {
        currentSaveSlot = slotIndex;
        SetTempStarterKit(starterKitId);

        if (player == null)
        {
            player = new Player(_startingCharacter);
        }

        // 1. 기존 플레이어 덱 및 유물 상태 완전 청소
        if (player.deck != null) player.deck.Clear();
        if (player.relicManager != null) player.relicManager.ClearRelics();

        // 2. 스타터 키트 적용
        ApplyStarterKit(starterKitId);

        currentStageLevel = 1;
        currentExploreMapFloor = 1;
        currentMapName = "Temple";

        if (player.character != null && player.character.curCharacterStat != null)
        {
            player.character.curHealth = player.character.curCharacterStat.maxHealth;
        }

        // 3. 생성과 동시에 세이브 파일 생성
        SaveGame();
        Debug.Log($"[RunManager] 슬롯 {slotIndex}에 새 게임 세션이 저장되었습니다. 시작 키트: {starterKitId}");
    }

    public void LoadRun()
    {
        LoadRun(currentSaveSlot);
    }

    public void LoadRun(int slotIndex)
    {
        currentSaveSlot = slotIndex;
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

        if (player == null)
        {
            player = new Player(_startingCharacter);
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

        if (FadeManager.instance != null)
        {
            FadeManager.instance.FadeIn();
        }
    }

    public void AddInGame_Currency(CurrencyType type, int amount)
    {
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

        // 승리 시 플레이어 위치에 보상 상자 스폰
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

        if (GameSceneManager.instance != null)
        {
            GameSceneManager.instance.UnregisterDungeonManager(this);
        }

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

        if (FadeManager.instance != null)
        {
            FadeManager.instance.FadeIn(0.35f, () =>
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
            currentExploreUI.UIDeactive();
        }

        Debug.Log("currentIncountNode = " + currentIncountNode.incountType.ToString());

        IncountNode newIncountNode = currentIncountNode;
        if (newIncountNode != null)
        {
            var mapManager = currentMap;
            var playerChar = player.character;

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

        if (FadeManager.instance != null)
        {
            FadeManager.instance.FadeOut(0.35f);
        }
    }

    public void SpawnMonsterInBattleNodeData(BattleData nodeData)
    {
        // 맵에 몬스터 스폰 처리가 필요한 경우 여기에 구현
    }
}
