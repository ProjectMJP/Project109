using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum MapState
{
    None,
    Battle,
    Secret
}

[System.Serializable]
public struct MapPrefabs
{
    public GameObject mapSpawnRootPrefab;
    public GameObject eventObjectPrefab;
    public GameObject shopObjectPrefab;
    public GameObject restoreObjectPrefab;
    public GameObject insightObjectPrefab;
    public GameObject rewardMapObjectPrefab;
}

public class MapManager : IInitializable, System.IDisposable
{
    public MapPrefabs mapPrefabs;

    public void Initialize()
    {
        // 초기화가 필요한 멤버 변수가 있다면 여기서 처리
    }

    public void Initialize(MapPrefabs prefabs)
    {
        this.mapPrefabs = prefabs;
    }

    public void Dispose()
    {
        ClearStage();
    }

    public MapDataSO currentMapData;
    public MapState currentMapState;
    public GameMap currentGameMap;

    private List<CellData> spawnEnemyCells = new List<CellData>();
    private List<CellData> spawnPlayerCells = new List<CellData>();
    private List<CellData> spawnNPCCells = new List<CellData>();

    //맵 이동 시 제거할 오브젝트 모음
    public List<GameObject> currentSpawnEnemyList = new List<GameObject>();
    public List<ICharacterController> currentEnemyControllers = new List<ICharacterController>();
    public List<GameObject> currentSpawnNPCList = new List<GameObject>();
    public List<GameObject> currentSpawnUIList = new List<GameObject>();
    public List<GameObject> currentSpawnEtcList = new List<GameObject>();

    public MapManager()
    {
    }

    /// <summary>
    /// ExploreNode에 사전 정의된 EncounterData 메타데이터를 기반으로 스테이지를 생성합니다.
    /// </summary>
    public void GenerateStage(ExploreNode node)
    {
        if (node == null || node.encounterData == null)
        {
            Debug.LogError("[MapManager] GenerateStage: node or encounterData is null!");
            return;
        }

        EncounterData enc = node.encounterData;

        // 1. 배틀 데이터 획득 (전투 노드일 경우)
        BattleData battleData = null;
        if (!string.IsNullOrEmpty(enc.battleDataId) && AssetCacheManager.instance != null)
        {
            if (!AssetCacheManager.instance.TryGetBattle(enc.battleDataId, out battleData))
            {
                if (AssetCacheManager.instance.battleList != null)
                {
                    foreach (var b in AssetCacheManager.instance.battleList)
                    {
                        if (b != null && (b.ID == enc.battleDataId || b.battleDataName == enc.battleDataId))
                        {
                            battleData = b;
                            break;
                        }
                    }
                }
            }
        }

        // 2. 인터랙터블/이벤트 데이터 획득
        InteractableData eventData = null;
        if (!string.IsNullOrEmpty(enc.interactableDataId) && ModLoader.Instance != null && ModLoader.Instance.InteractableDatabase != null)
        {
            ModLoader.Instance.InteractableDatabase.TryGetValue(enc.interactableDataId, out eventData);
        }

        // 3. 플레이어 캐릭터 참조 (GameSceneManager 세션 루트)
        Character playerChar = GameSceneManager.instance != null && GameSceneManager.instance.player != null
            ? GameSceneManager.instance.player.character
            : null;

        // 4. 스테이지 생성 실행
        GenerateStage(enc.locationType, enc.encountType, playerChar, this.mapPrefabs, eventData, battleData);
    }

    // 이 함수는 노드에 진입할 때 호출됩니다.
    public void GenerateStage(LocationType mapLocation, EncountType EncountType, Character playerCharacter = null, MapPrefabs prefabs = default, InteractableData secretEventData = null, BattleData battleData = null)
    {
        // 프리팹이 기본값이면 멤버 변수 mapPrefabs 사용
        MapPrefabs targetPrefabs = prefabs.mapSpawnRootPrefab != null ? prefabs : this.mapPrefabs;

        // 플레이어 캐릭터가 인자로 오지 않은 경우 GameSceneManager에서 자동 조회
        Character targetPlayer = playerCharacter != null ? playerCharacter
            : (GameSceneManager.instance != null && GameSceneManager.instance.player != null ? GameSceneManager.instance.player.character : null);
        //플레이어를 제외한 생성된 모든 요소 제거
        ClearStage();

        //이전의 맵 타일 및 생성된 오브젝트 제거 후 새롭게 맵 데이터 업데이트 및 타일 생성하도록 코딩 진행
        UpdateMapData(mapLocation.ToString(), EncountType);

        currentGameMap = UnityEngine.Object.Instantiate(targetPrefabs.mapSpawnRootPrefab).GetComponent<GameMap>();

        spawnEnemyCells.Clear();
        spawnPlayerCells.Clear();
        spawnNPCCells.Clear();

        foreach (var cell in currentMapData.cells)
        {
            if (cell.terrainID != "Empty")
            {
                GenerateObjectInMap(cell.terrainID, cell.position);
            }

            if (cell.objectID != "Empty")
            {
                GenerateObjectInMap(cell.objectID, cell.position);
            }

            switch (cell.eventID)
            {
                case "EnemySpawn":
                    spawnEnemyCells.Add(cell);
                    break;
                case "PlayerSpawn":
                    spawnPlayerCells.Add(cell);
                    break;
                case "NPCSpawn":
                    spawnNPCCells.Add(cell);
                    break;
            }
        }

        currentGameMap.TileCreateByMapData(currentMapData);

        // 파괴 가능한 장애물(DestructibleObject)이 배치된 타일의 상태를 Obstacle로 동기화
        foreach (GameObject etcObj in currentSpawnEtcList)
        {
            if (etcObj != null && etcObj.TryGetComponent<DestructibleObject>(out var _))
            {
                Vector2Int cellPos = new Vector2Int(
                    Mathf.RoundToInt((etcObj.transform.position.x - currentMapData.gridOffset.x) / currentMapData.cellSize),
                    Mathf.RoundToInt((etcObj.transform.position.z - currentMapData.gridOffset.z) / currentMapData.cellSize)
                );
                currentGameMap.SetTileState(cellPos, TileState.Obstacle);
            }
        }

        if (battleData != null && battleData.monsterNames != null && (EncountType == EncountType.Battle || EncountType == EncountType.Elite || EncountType == EncountType.Boss))
        {
            ShuffleList(spawnEnemyCells);
            int spawnEnemyCount = Mathf.Min(spawnEnemyCells.Count, battleData.monsterNames.Count);
            GenerateEnemy(currentMapData, battleData.monsterNames, spawnEnemyCount);
        }

        SpawnPlayerInMap(currentMapData, targetPlayer, spawnPlayerCells.Count);

        if (EncountType == EncountType.Store || EncountType == EncountType.Restore || EncountType == EncountType.Secret || EncountType == EncountType.SecretBox)
        {
            InteractableData eventData = secretEventData;
            if (eventData == null && EncountType == EncountType.Restore)
            {
                if (ModLoader.Instance != null && ModLoader.Instance.InteractableDatabase.TryGetValue("Restore_Bonfire_Data", out var restoreData))
                {
                    eventData = restoreData;
                }
            }
            GenerateNPC(currentMapData, EncountType, targetPrefabs, eventData);
        }

        // 맵 상태 설정 자동화
        if (EncountType == EncountType.Battle || EncountType == EncountType.Elite || EncountType == EncountType.Boss)
        {
            currentMapState = MapState.Battle;
        }
        else if (EncountType == EncountType.Secret)
        {
            currentMapState = MapState.Secret;
        }
        else
        {
            currentMapState = MapState.None;
        }

        Debug.Log($"Map Generated Done : {EncountType}");
    }

    private void GenerateObjectInMap(string objectID, Vector2Int pos)
    {
        if (AssetCacheManager.instance.TryGetModel(objectID, out GameObject prefab))
        {
            GameObject spawned = UnityEngine.Object.Instantiate(prefab);

            Vector3 worldPos = new Vector3(
                        pos.x * currentMapData.cellSize + currentMapData.gridOffset.x,
                        currentMapData.gridOffset.y,
                        pos.y * currentMapData.cellSize + currentMapData.gridOffset.z
                    );

            spawned.transform.position = worldPos;
            spawned.transform.SetParent(currentGameMap.transform);
            currentSpawnEtcList.Add(spawned);

            // 파괴 가능한 오브젝트인 경우, 파괴 시 타일을 Empty로 복구하도록 이벤트 구독
            DestructibleObject destructible = spawned.GetComponent<DestructibleObject>();
            if (destructible != null)
            {
                destructible.OnDestroyed += (obj) =>
                {
                    if (currentGameMap != null)
                    {
                        currentGameMap.SetTileState(pos, TileState.Empty);
                    }
                };
            }
        }
    }

    public void GenerateEnemy(MapDataSO mapData, List<string> spawnMonsterList, int spawnCount)
    {
        for (int i = 0; i < spawnCount; i++)
        {
            CellData cell = spawnEnemyCells[i];
            string monsterId = spawnMonsterList[i];

            Debug.Log($"Check MapManager enemy spawn: {monsterId}...");

            // 1. NPCUnitData 조회
            NPCUnitData aiData = null;
            if (ModLoader.Instance != null && ModLoader.Instance.NPCUnitDatabase != null)
            {
                ModLoader.Instance.NPCUnitDatabase.TryGetValue(monsterId, out aiData);
            }

            // 2. 모델 프리팹 주소 결정 (지정된 prefabAddress 또는 monsterId 자체를 키로 사용)
            string modelAddress = (aiData != null && !string.IsNullOrEmpty(aiData.prefabAddress))
                ? aiData.prefabAddress
                : monsterId;

            // 3. 몬스터 프리팹 탐색 및 스폰
            if (AssetCacheManager.instance.TryGetModel(modelAddress, out var monsterPrefab))
            {
                Debug.Log($"Monster prefab {modelAddress} Load Success.");

                // 셀의 그리드 좌표를 실제 월드 좌표로 변환 (셀 크기 및 오프셋 적용)
                Vector3 worldPos = new Vector3(
                    cell.position.x * mapData.cellSize + mapData.gridOffset.x,
                    mapData.gridOffset.y,
                    cell.position.y * mapData.cellSize + mapData.gridOffset.z
                );

                // 적 생성
                GameObject newMonster = UnityEngine.Object.Instantiate(monsterPrefab, worldPos, Quaternion.identity);

                // 적 캐릭터의 방향을 Down으로 설정하고 3D 회전 업데이트
                Character enemyCharacter = newMonster.GetComponent<Character>();
                if (enemyCharacter == null)
                {
                    var controller = newMonster.GetComponent<ICharacterController>();
                    if (controller != null)
                    {
                        enemyCharacter = controller.controlledCharacter;
                    }
                }

                if (enemyCharacter != null)
                {
                    // 몬스터 진영 설정
                    enemyCharacter.faction = CharacterFaction.Enemy;

                    // 1. Enemy 스탯 초기화
                    CharacterStat enemyStat = (aiData != null && aiData.characterStat != null)
                        ? aiData.characterStat
                        : new CharacterStat
                        {
                            maxHealth = 50f,
                            maxStamina = 100f,
                            staminaRegenPerSecond = 10f,
                            maxMoveCount = 3,
                            maxTilesPerMove = 2
                        };
                    enemyCharacter.InitializeStat(enemyStat);

                    // 2. NPC AI Controller 초기화 및 등록
                    NPCUnitData targetAiData = aiData ?? new NPCUnitData
                    {
                        unitId = monsterId,
                        characterStat = enemyStat
                    };
                    var enemyController = new NPCUnitController(enemyCharacter, targetAiData);
                    currentEnemyControllers.Add(enemyController);

                    // 3. 이동 컴포넌트 세팅 및 배치 타일 세팅
                    if (enemyCharacter.characterMove != null)
                    {
                        enemyCharacter.characterMove.SetFacingDirection(LookDirection.Down);

                        List<List<Tile>> tileMap = currentGameMap.GetTileMap();
                        if (tileMap != null && cell.position.x < tileMap.Count && cell.position.y < tileMap[cell.position.x].Count)
                        {
                            enemyCharacter.characterMove.SetCurrentTile(tileMap[cell.position.x][cell.position.y]);
                        }
                    }
                }

                currentSpawnEnemyList.Add(newMonster);
            }
            else
            {
                Debug.LogError($"[MapManager] 몬스터 프리팹 로드 실패: {modelAddress}");
            }
        }
    }

    public void GenerateNPC(MapDataSO mapData, EncountType EncountType, MapPrefabs prefabs, InteractableData eventData = null)
    {
        if (spawnNPCCells.Count == 0)
        {
            Debug.LogWarning("No NPC Spawn Cells Found in Map Data!");
            return;
        }

        CellData cell = spawnNPCCells[0];
        spawnNPCCells.RemoveAt(0);

        Vector3 worldPos = new Vector3(
            cell.position.x * mapData.cellSize + mapData.gridOffset.x,
            mapData.gridOffset.y,
            cell.position.y * mapData.cellSize + mapData.gridOffset.z
        );

        GameObject npcObj = null;
        GameObject uiObj = null;

        // [단일 베이스 프리팹 사용]
        // 모든 NPC는 "EventObject" 프리팹을 공통 기반으로 생성하며, 
        // 런타임에 필요한 컴포넌트를 추가하여 역할을 분화합니다.
        GameObject basePrefab = prefabs.eventObjectPrefab;
        if (basePrefab == null)
        {
            AssetCacheManager.instance.TryGetModel("EventObject", out basePrefab);
        }

        if (basePrefab != null)
        {
            GameObject spawned = UnityEngine.Object.Instantiate(basePrefab, worldPos, Quaternion.identity);
            
            // 기존 프리팹에 이미 부착되어 있을 수 있는 기본 InteractableObject 컴포넌트 제거
            var existing = spawned.GetComponent<InteractableObject>();
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            InteractableObject newNPC = null;
            
            // EncountType 및 eventData에 맞춰 알맞은 컴포넌트 타입 판단
            bool isRestore = (EncountType == EncountType.Restore);
            bool isShop = (EncountType == EncountType.Store);
            bool isChest = (EncountType == EncountType.SecretBox);

            if (EncountType == EncountType.Secret && eventData != null)
            {
                if (eventData.rewardData != null && eventData.rewardData.rewards != null && eventData.rewardData.rewards.Count > 0)
                {
                    isChest = true;
                }
                else
                {
                    string idLower = !string.IsNullOrEmpty(eventData.interactableID) ? eventData.interactableID.ToLower() : "";
                    string modelLower = !string.IsNullOrEmpty(eventData.modelPrefabPath) ? eventData.modelPrefabPath.ToLower() : "";

                    if (idLower.Contains("shop") || modelLower.Contains("shop") || idLower.Contains("store") || modelLower.Contains("store"))
                    {
                        isShop = true;
                    }
                    else if (idLower.Contains("restore") || modelLower.Contains("restore") || idLower.Contains("bonfire") || modelLower.Contains("bonfire") || idLower.Contains("rest") || modelLower.Contains("rest"))
                    {
                        isRestore = true;
                    }
                }
            }

            // 컴포넌트 할당 및 초기화
            if (isChest)
            {
                var chest = spawned.AddComponent<RewardChest>();
                if (eventData != null)
                {
                    chest.SetInteractableData(eventData);
                }
                newNPC = chest;
            }
            else if (isShop)
            {
                var shop = spawned.AddComponent<ShopNPC>();
                if (eventData != null)
                {
                    shop.SetInteractableData(eventData);
                }
                shop.InitializeShop();
                uiObj = shop.GetShopUI() != null ? shop.GetShopUI().gameObject : null;
                newNPC = shop;
            }
            else if (isRestore)
            {
                var restore = spawned.AddComponent<RestoreBonfire>();
                if (eventData != null)
                {
                    restore.SetInteractableData(eventData);
                }
                else if (ModLoader.Instance != null && ModLoader.Instance.InteractableDatabase.TryGetValue("Restore_Bonfire_Data", out var fallbackData))
                {
                    restore.SetInteractableData(fallbackData);
                }
                restore.CreateRestoreUI();
                uiObj = restore.GetRestoreUI() != null ? restore.GetRestoreUI().gameObject : null;
                newNPC = restore;
            }
            else
            {
                var normalNPC = spawned.AddComponent<InteractableObject>();
                if (eventData != null)
                {
                    normalNPC.SetInteractableData(eventData);
                }
                newNPC = normalNPC;
            }

            if (newNPC != null)
            {
                npcObj = spawned;
            }
        }
        else
        {
            Debug.LogError("[MapManager] EventObject 프리팹을 MapPrefabs 또는 Addressables 캐시에서 찾을 수 없습니다!");
        }

        if (npcObj != null)
        {
            currentSpawnNPCList.Add(npcObj);
        }
        if (uiObj != null)
        {
            currentSpawnUIList.Add(uiObj);
        }
    }

    public void SpawnPlayerInMap(MapDataSO mapData, Character playerCharacter, int spawnCount)
    {
        if (spawnPlayerCells.Count == 0)
        {
            Debug.LogWarning("No Player Spawn Cells Found in Map Data!");
            return;
        }
        ShuffleList(spawnPlayerCells);

        // 셀의 그리드 좌표를 실제 월드 좌표로 변환 (셀 크기 및 오프셋 적용)
        Vector3 worldPos = new Vector3(
            spawnPlayerCells[0].position.x * mapData.cellSize + mapData.gridOffset.x,
            mapData.gridOffset.y,
            spawnPlayerCells[0].position.y * mapData.cellSize + mapData.gridOffset.z
        );
        // 플레이어 진영 설정
        playerCharacter.faction = CharacterFaction.Player;

        //플레이어 이동 및 타일 정보 업데이트
        playerCharacter.transform.position = worldPos;
        if (playerCharacter.characterMove != null)
        {
            playerCharacter.characterMove.SetCurrentTile(currentGameMap.GetTileMap()[spawnPlayerCells[0].position.x][spawnPlayerCells[0].position.y]);
        }
    }

    void UpdateMapData(string stageName, EncountType EncountType)
    {
        //맵 데이터에서 기본 베이스와 추가 바리에이션을 찾아서 등록 진행
        if (AssetCacheManager.instance.TryGetStageMapData(stageName, out StageMapDataBundle stageMapData))
        {

            // Fallback: 기본적으로 전투 맵을 할당해두어 에셋이 없는 경우에 대비합니다.
            if (stageMapData.battleMapDataList != null && stageMapData.battleMapDataList.Count > 0)
            {
                currentMapData = stageMapData.battleMapDataList[0];
            }

            switch (EncountType)
            {
                case EncountType.Battle:
                    if (stageMapData.battleMapDataList != null && stageMapData.battleMapDataList.Count > 0)
                    {
                        currentMapData = stageMapData.battleMapDataList[Random.Range(0, stageMapData.battleMapDataList.Count)];
                        Debug.Log($"ChooseBattleMapData: {currentMapData.stageName}");
                    }
                    break;
                case EncountType.Elite:
                    if (stageMapData.eliteMapDataList != null && stageMapData.eliteMapDataList.Count > 0)
                    {
                        currentMapData = stageMapData.eliteMapDataList[Random.Range(0, stageMapData.eliteMapDataList.Count)];
                        Debug.Log($"ChooseEliteMapData: {currentMapData.stageName}");
                    }
                    break;
                case EncountType.Boss:
                    if (stageMapData.bossMapDataList != null && stageMapData.bossMapDataList.Count > 0)
                    {
                        currentMapData = stageMapData.bossMapDataList[Random.Range(0, stageMapData.bossMapDataList.Count)];
                        Debug.Log($"ChooseBossMapData: {currentMapData.stageName}");
                    }
                    break;
                case EncountType.Secret:
                    if (stageMapData.secretMapDataList != null && stageMapData.secretMapDataList.Count > 0)
                    {
                        currentMapData = stageMapData.secretMapDataList[Random.Range(0, stageMapData.secretMapDataList.Count)];
                        Debug.Log($"ChooseSecretMapData: {currentMapData.stageName}");
                    }
                    break;
                case EncountType.Store:
                    if (stageMapData.storeMapDataList != null && stageMapData.storeMapDataList.Count > 0)
                    {
                        currentMapData = stageMapData.storeMapDataList[Random.Range(0, stageMapData.storeMapDataList.Count)];
                        Debug.Log($"ChooseStoreMapData: {currentMapData.stageName}");
                    }
                    break;
                case EncountType.Restore:
                    if (stageMapData.restoreMapDataList != null && stageMapData.restoreMapDataList.Count > 0)
                    {
                        currentMapData = stageMapData.restoreMapDataList[Random.Range(0, stageMapData.restoreMapDataList.Count)];
                        Debug.Log($"ChooseRestoreMapData: {currentMapData.stageName}");
                    }
                    break;
                case EncountType.SecretBox:
                    if (stageMapData.secretMapDataList != null && stageMapData.secretMapDataList.Count > 0)
                    {
                        currentMapData = stageMapData.secretMapDataList[Random.Range(0, stageMapData.secretMapDataList.Count)];
                        Debug.Log($"ChooseSecretBoxMapData (using Secret): {currentMapData.stageName}");
                    }
                    break;
            }
        }
        else
        {
            Debug.LogWarning($"Failed to Find MapData");
        }
    }

    public void ClearStage()
    {
        // 현재 씬에 존재하는 적 오브젝트를 모두 제거
        foreach (GameObject enemy in currentSpawnEnemyList)
        {
            if (enemy != null)
            {
                UnityEngine.Object.Destroy(enemy);
            }
        }
        currentSpawnEnemyList.Clear();
        currentEnemyControllers.Clear();
        // 현재 씬에 존재하는 NPC 오브젝트를 모두 제거
        foreach (GameObject obj in currentSpawnNPCList)
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }
        currentSpawnNPCList.Clear();
        // 현재 씬에 존재하는 UI 오브젝트를 모두 제거
        foreach (GameObject ui in currentSpawnUIList)
        {
            if (ui != null)
            {
                UnityEngine.Object.Destroy(ui);
            }
        }
        currentSpawnUIList.Clear();
        // 현재 씬에 존재하는 장애물, 함정 오브젝트를 모두 제거
        foreach (GameObject etc in currentSpawnEtcList)
        {
            if (etc != null)
            {
                UnityEngine.Object.Destroy(etc);
            }
        }
        currentSpawnEtcList.Clear();

        if (currentGameMap != null)
        {
            UnityEngine.Object.Destroy(currentGameMap.gameObject);
            currentGameMap = null;
        }
    }

    // 리스트를 무작위로 섞는 유틸리티 함수
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
