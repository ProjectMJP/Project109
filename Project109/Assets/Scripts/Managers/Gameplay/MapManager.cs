using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum MapState
{
    None,
    Battle,
    Secret
}

public class MapManager : IInitializable, System.IDisposable
{
    public void Initialize()
    {
        // 초기화가 필요한 멤버 변수가 있다면 여기서 처리
    }

    public void Initialize(GameMap gameMap = null)
    {
        this.currentGameMap = gameMap;
    }

    public void Dispose()
    {
        ClearStage();

        if (currentGameMap != null)
        {
            currentGameMap.ClearTiles();
            currentGameMap = null;
        }
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
        GenerateStage(enc.locationType, enc.encountType, playerChar, eventData, battleData);
    }

    // 이 함수는 노드에 진입할 때 호출됩니다.
    public void GenerateStage(LocationType mapLocation, EncountType EncountType, Character playerCharacter = null, InteractableData secretEventData = null, BattleData battleData = null)
    {
        // 플레이어 캐릭터가 인자로 오지 않은 경우 GameSceneManager에서 자동 조회
        Character targetPlayer = playerCharacter != null ? playerCharacter
            : (GameSceneManager.instance != null && GameSceneManager.instance.player != null ? GameSceneManager.instance.player.character : null);
        //플레이어를 제외한 생성된 모든 요소 제거
        ClearStage();

        //이전의 맵 타일 및 생성된 오브젝트 제거 후 새롭게 맵 데이터 업데이트 및 타일 생성하도록 코딩 진행
        UpdateMapData(mapLocation.ToString(), EncountType);

        // 1. 이미 존재하는 currentGameMap 인스턴스가 있다면 재사용
        if (currentGameMap == null)
        {
            // 2. 씬에 이미 배치된 GameMap이 있다면 우선 탐색하여 바인딩
            currentGameMap = GameMap.current != null ? GameMap.current : UnityEngine.Object.FindFirstObjectByType<GameMap>();

            if (currentGameMap == null)
            {
                Debug.LogError("[MapManager] 씬에서 GameMap을 찾을 수 없습니다! GameMap을 씬에 배치해주세요.");
                return;
            }
        }

        if (currentMapData == null || currentMapData.cells == null)
        {
            Debug.LogError($"[MapManager] 스테이지 생성 중단: currentMapData 또는 cells가 null입니다! (Location: {mapLocation}, Encount: {EncountType})");
            return;
        }

        // 1. 바닥 타일 및 이동 인디케이터 생성 (기존 타일/자식 정리 후 새로 생성)
        currentGameMap.TileCreateByMapData(currentMapData);

        spawnEnemyCells.Clear();
        spawnPlayerCells.Clear();
        spawnNPCCells.Clear();

        // 2. 타일 생성 후 지형(Terrain_Temple_Floor 등) 및 오브젝트 스폰
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
            GenerateNPC(currentMapData, EncountType, eventData);
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
                if (!ModLoader.Instance.NPCUnitDatabase.TryGetValue(monsterId, out aiData))
                {
                    // Fallback 1: "_Data" 접미사 제거 후 탐색
                    if (monsterId.EndsWith("_Data"))
                    {
                        string stripped = monsterId.Substring(0, monsterId.Length - 5);
                        ModLoader.Instance.NPCUnitDatabase.TryGetValue(stripped, out aiData);
                    }

                    // Fallback 2: "Monster_" 접두사 제거 후 탐색
                    if (aiData == null && monsterId.StartsWith("Monster_"))
                    {
                        string stripped = monsterId.Substring("Monster_".Length);
                        ModLoader.Instance.NPCUnitDatabase.TryGetValue(stripped, out aiData);
                    }

                    // Fallback 3: 데이터베이스 내 유닛 ID와의 부분 일치 탐색
                    if (aiData == null)
                    {
                        foreach (var pair in ModLoader.Instance.NPCUnitDatabase)
                        {
                            if (monsterId.Contains(pair.Key, System.StringComparison.OrdinalIgnoreCase) ||
                                pair.Key.Contains(monsterId, System.StringComparison.OrdinalIgnoreCase))
                            {
                                aiData = pair.Value;
                                break;
                            }
                        }
                    }
                }
            }

            // 2. 모델 프리팹 주소 결정 (지정된 prefabAddress 또는 monsterId 자체를 키로 사용)
            string modelAddress = (aiData != null && !string.IsNullOrEmpty(aiData.prefabAddress))
                ? aiData.prefabAddress
                : monsterId;

            // 3. 몬스터 프리팹 탐색 (정확한 주소 -> _Data를 _Prefab으로 치환 -> 부분 일치 -> Fallback)
            GameObject monsterPrefab = null;
            if (AssetCacheManager.instance != null)
            {
                if (!AssetCacheManager.instance.TryGetModel(modelAddress, out monsterPrefab))
                {
                    // "_Data" 접미사를 "_Prefab"으로 치환하여 탐색
                    if (modelAddress.EndsWith("_Data"))
                    {
                        string prefabCandidate = modelAddress.Substring(0, modelAddress.Length - 5) + "_Prefab";
                        AssetCacheManager.instance.TryGetModel(prefabCandidate, out monsterPrefab);
                    }

                    // "_Data" 접미사를 제거하여 탐색
                    if (monsterPrefab == null && modelAddress.EndsWith("_Data"))
                    {
                        string candidate = modelAddress.Substring(0, modelAddress.Length - 5);
                        AssetCacheManager.instance.TryGetModel(candidate, out monsterPrefab);
                    }

                    // 캐시된 모델 목록에서 이름 유사도 검색
                    if (monsterPrefab == null && AssetCacheManager.instance.modelList != null)
                    {
                        foreach (var m in AssetCacheManager.instance.modelList)
                        {
                            if (m != null && (m.name.Contains(modelAddress) || modelAddress.Contains(m.name)))
                            {
                                monsterPrefab = m;
                                break;
                            }
                        }
                    }

                    // 최후 Fallback: 모델 목록 중 Monster 키워드를 포함하는 프리팹 탐색
                    if (monsterPrefab == null && AssetCacheManager.instance.modelList != null)
                    {
                        foreach (var m in AssetCacheManager.instance.modelList)
                        {
                            if (m != null && m.name.ToLower().Contains("monster"))
                            {
                                monsterPrefab = m;
                                break;
                            }
                        }
                    }
                }
            }

            if (monsterPrefab != null)
            {
                Debug.Log($"Monster prefab {monsterPrefab.name} (키: {modelAddress}) Load Success.");

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

    public void GenerateNPC(MapDataSO mapData, EncountType EncountType, InteractableData eventData = null)
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
        GameObject basePrefab = null;
        if (AssetCacheManager.instance != null)
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
            Debug.LogError("[MapManager] EventObject 프리팹을 Addressables 캐시에서 찾을 수 없습니다!");
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

        // 카메라를 새로운 스테이지의 플레이어 위치로 이동/포커싱
        if (CameraController.instance != null)
        {
            CameraController.instance.CameraFocusToTarget(worldPos);
        }
        else if (Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;
            Camera.main.transform.position = new Vector3(worldPos.x, camPos.y, worldPos.z - 7f);
        }
    }

    void UpdateMapData(string stageName, EncountType EncountType)
    {
        currentMapData = null;

        // 1. 스테이지 번들에서 해당 EncountType 맵 조회
        if (AssetCacheManager.instance != null && AssetCacheManager.instance.TryGetStageMapData(stageName, out StageMapDataBundle stageMapData))
        {
            List<MapDataSO> targetList = null;

            switch (EncountType)
            {
                case EncountType.Battle:
                case EncountType.None:
                    targetList = stageMapData.battleMapDataList;
                    break;
                case EncountType.Elite:
                    targetList = stageMapData.eliteMapDataList;
                    break;
                case EncountType.Boss:
                    targetList = stageMapData.bossMapDataList;
                    break;
                case EncountType.Secret:
                case EncountType.SecretBox:
                    targetList = stageMapData.secretMapDataList;
                    break;
                case EncountType.Store:
                    targetList = stageMapData.storeMapDataList;
                    break;
                case EncountType.Restore:
                    targetList = stageMapData.restoreMapDataList;
                    break;
            }

            if (targetList != null && targetList.Count > 0)
            {
                currentMapData = targetList[Random.Range(0, targetList.Count)];
            }
            else if (stageMapData.battleMapDataList != null && stageMapData.battleMapDataList.Count > 0)
            {
                // 요청한 타입의 맵이 없으면 기본 전투 맵으로 Fallback
                currentMapData = stageMapData.battleMapDataList[0];
                Debug.LogWarning($"[MapManager] {EncountType} 맵이 없어 기본 전투 맵({currentMapData.stageName})으로 Fallback합니다.");
            }
        }

        // 2. 번들에서 찾지 못했을 경우 전체 mapList에서 동일 locationType 또는 아무 맵이라도 Fallback
        if (currentMapData == null && AssetCacheManager.instance != null && AssetCacheManager.instance.mapList != null && AssetCacheManager.instance.mapList.Count > 0)
        {
            foreach (var m in AssetCacheManager.instance.mapList)
            {
                if (m != null && m.locationType.ToString() == stageName)
                {
                    currentMapData = m;
                    break;
                }
            }

            if (currentMapData == null)
            {
                currentMapData = AssetCacheManager.instance.mapList[0];
            }

            Debug.LogWarning($"[MapManager] StageMapDataBundle에서 맵을 찾지 못해 글로벌 맵 리스트({currentMapData?.stageName})에서 Fallback했습니다.");
        }

        if (currentMapData != null)
        {
            Debug.Log($"[MapManager] Selected MapData: {currentMapData.stageName} (Location: {stageName}, Encount: {EncountType})");
        }
        else
        {
            Debug.LogError($"[MapManager] MapData를 전혀 찾을 수 없습니다! (Location: {stageName}, Encount: {EncountType})");
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

        // GameMap 게임오브젝트를 파괴하지 않고 타일들만 정리하여 인스턴스를 재사용합니다.
        if (currentGameMap != null)
        {
            currentGameMap.ClearTiles();
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
