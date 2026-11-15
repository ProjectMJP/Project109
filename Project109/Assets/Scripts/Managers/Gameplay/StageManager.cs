using CardTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;


public enum MapState
{
    None,
    Battle,
    Secret
}

public class StageManager : IInitializable, System.IDisposable
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

        _npcUnitDataCache.Clear();
        _monsterPrefabCache.Clear();

        if (currentGameMap != null)
        {
            currentGameMap.ClearTiles();
            currentGameMap = null;
        }
    }

    public MapDataSO currentMapData;
    public MapState currentMapState;
    public GameMap currentGameMap;

    private readonly Dictionary<string, NPCUnitData> _npcUnitDataCache = new Dictionary<string, NPCUnitData>();
    private readonly Dictionary<string, GameObject> _monsterPrefabCache = new Dictionary<string, GameObject>();

    private List<CellData> spawnEnemyCells = new List<CellData>();
    private List<CellData> spawnPlayerCells = new List<CellData>();
    private List<CellData> spawnNPCCells = new List<CellData>();

    // 맵 이동 시 제거할 오브젝트 컬렉션 (IReadOnlyList로 캡슐화)
    private readonly List<GameObject> _currentSpawnEnemyList = new List<GameObject>();
    private readonly List<ICharacterController> _currentEnemyControllers = new List<ICharacterController>();
    private readonly List<GameObject> _currentSpawnNPCList = new List<GameObject>();
    private readonly List<GameObject> _currentSpawnEtcList = new List<GameObject>();

    public IReadOnlyList<GameObject> currentSpawnEnemyList => _currentSpawnEnemyList;
    public IReadOnlyList<ICharacterController> currentEnemyControllers => _currentEnemyControllers;
    public IReadOnlyList<GameObject> currentSpawnNPCList => _currentSpawnNPCList;
    public IReadOnlyList<GameObject> currentSpawnEtcList => _currentSpawnEtcList;

    public void RegisterSpawnEnemy(GameObject enemy, ICharacterController controller = null)
    {
        if (enemy != null && !_currentSpawnEnemyList.Contains(enemy))
        {
            _currentSpawnEnemyList.Add(enemy);
        }
        if (controller != null && !_currentEnemyControllers.Contains(controller))
        {
            _currentEnemyControllers.Add(controller);
        }
    }

    public void RegisterSpawnNPC(GameObject npc)
    {
        if (npc != null && !_currentSpawnNPCList.Contains(npc))
        {
            _currentSpawnNPCList.Add(npc);
        }
    }

    public void RegisterSpawnEtc(GameObject etc)
    {
        if (etc != null && !_currentSpawnEtcList.Contains(etc))
        {
            _currentSpawnEtcList.Add(etc);
        }
    }

    /// <summary>
    /// ExploreNode에 사전 정의된 EncounterData 메타데이터를 기반으로 스테이지를 생성합니다.
    /// </summary>
    public void GenerateStage(ExploreNode node)
    {
        if (node == null || node.encounterData == null)
        {
            Debug.LogError("[StageManager] GenerateStage: node or encounterData is null!");
            return;
        }

        EncounterData enc = node.encounterData;

        // 1. 배틀 데이터 획득 (전투 노드일 경우)
        BattleData battleData = null;
        if (!string.IsNullOrEmpty(enc.battleDataId) && ModLoader.Instance != null && ModLoader.Instance.BattleDatabase != null)
        {
            ModLoader.Instance.BattleDatabase.TryGetValue(enc.battleDataId, out battleData);
        }

        if (enc.encountType.IsCombat() && battleData == null)
        {
            Debug.LogError($"[StageManager] 전투 노드({enc.encountType})에 해당하는 BattleData를 찾을 수 없습니다! (battleDataId='{enc.battleDataId}')");
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
                Debug.LogError("[StageManager] 씬에서 GameMap을 찾을 수 없습니다! GameMap을 씬에 배치해주세요.");
                return;
            }
        }

        if (currentMapData == null || currentMapData.cells == null)
        {
            Debug.LogError($"[StageManager] 스테이지 생성 중단: currentMapData 또는 cells가 null입니다! (Location: {mapLocation}, Encount: {EncountType})");
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

            GameObject spawnedObj = null;
            if (cell.objectID != "Empty")
            {
                spawnedObj = GenerateObjectInMap(cell.objectID, cell.position);

                // 장애물 타일 상태 동기화 (Trap 계열은 제외)
                bool isTrapObj = !string.IsNullOrEmpty(cell.objectID) && cell.objectID.StartsWith("Trap", StringComparison.OrdinalIgnoreCase);
                if (!isTrapObj)
                {
                    bool isObstacle = (!string.IsNullOrEmpty(cell.objectID) && cell.objectID.StartsWith("Obstacle", StringComparison.OrdinalIgnoreCase))
                                      || (spawnedObj != null && spawnedObj.GetComponent<DestructibleObject>() != null);
                    if (isObstacle && currentGameMap != null)
                    {
                        currentGameMap.SetTileState(cell.position, TileState.Obstacle);
                    }
                }
            }

            bool isTrapCell = (!string.IsNullOrEmpty(cell.objectID) && cell.objectID.StartsWith("Trap", StringComparison.OrdinalIgnoreCase))
                              || string.Equals(cell.eventID, "Trap", StringComparison.OrdinalIgnoreCase);
            if (isTrapCell && currentGameMap != null)
            {
                currentGameMap.SetTileState(cell.position, TileState.Trap);
                if (spawnedObj != null)
                {
                    BindTrapToTile(spawnedObj, cell.position);
                }
            }

            switch (cell.eventID)
            {
                case "EnemySpawn":
                case "BossSpawn":
                    spawnEnemyCells.Add(cell);
                    break;
                case "PlayerSpawn":
                    spawnPlayerCells.Add(cell);
                    break;
                case "NPCSpawn":
                    spawnNPCCells.Add(cell);
                    break;
                case "Block":
                    if (currentGameMap != null)
                    {
                        currentGameMap.SetTileState(cell.position, TileState.Obstacle);
                    }
                    break;
            }
        }

        if (currentGameMap != null)
        {
            MapMoveRangeIndicator rangeIndicator = currentGameMap.GetMoveRangeIndicator();
            if (rangeIndicator != null)
            {
                rangeIndicator.RefreshBaseMap(currentGameMap.GetTileMap());
            }
        }

        if (battleData != null && battleData.monsterNames != null && EncountType.IsCombat())
        {
            spawnEnemyCells.Shuffle();
            int spawnEnemyCount = Mathf.Min(spawnEnemyCells.Count, battleData.monsterNames.Count);
            GenerateEnemy(currentMapData, battleData.monsterNames, spawnEnemyCount);
        }

        SpawnPlayerInMap(currentMapData, targetPlayer, spawnPlayerCells.Count);

        if (!EncountType.IsCombat())
        {
            InteractableData eventData = secretEventData;
            if (eventData == null && EncountType == EncountType.Restore)
            {
                if (ModLoader.Instance != null && ModLoader.Instance.InteractableDatabase.TryGetValue("Restore_Bonfire_Data", out var restoreData))
                {
                    eventData = restoreData;
                }
            }
            else if (eventData == null && EncountType == EncountType.None)
            {
                if (ModLoader.Instance != null && ModLoader.Instance.InteractableDatabase.TryGetValue("Region_Start_Shrine_Data", out var shrineData))
                {
                    eventData = shrineData;
                }
            }

            if (eventData != null || EncountType != EncountType.None)
            {
                GenerateNPC(currentMapData, EncountType, eventData);
            }
        }

        // 맵 상태 설정 자동화
        if (EncountType.IsCombat())
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

    private GameObject GenerateObjectInMap(string objectID, Vector2Int pos)
    {
        if (AssetCacheManager.instance.TryGetModel(objectID, out GameObject prefab))
        {
            GameObject spawned = UnityEngine.Object.Instantiate(prefab);

            Vector3 worldPos = currentMapData.CellToWorld(pos);

            spawned.transform.position = worldPos;
            spawned.transform.SetParent(currentGameMap.transform);
            RegisterSpawnEtc(spawned);

            // 파괴 가능한 오브젝트인 경우, 파괴 시 타일을 Empty로 복구하도록 이벤트 구독 및 타일 참조 바인딩
            DestructibleObject destructible = spawned.GetComponent<DestructibleObject>();
            if (destructible != null)
            {
                if (currentGameMap != null)
                {
                    Tile tile = currentGameMap.GetTile(pos);
                    if (tile != null)
                    {
                        tile.occupyingDestructible = destructible;
                    }
                }

                destructible.OnDestroyed += (obj) =>
                {
                    if (currentGameMap != null)
                    {
                        currentGameMap.SetTileState(pos, TileState.Empty);
                        Tile t = currentGameMap.GetTile(pos);
                        if (t != null && t.occupyingDestructible == obj)
                        {
                            t.occupyingDestructible = null;
                        }
                    }
                };
            }

            // 함정 오브젝트인 경우 타일 상태 및 점유 위험 요소 바인딩
            if ((!string.IsNullOrEmpty(objectID) && objectID.StartsWith("Trap", StringComparison.OrdinalIgnoreCase))
                || spawned.GetComponent<TrapObject>() != null)
            {
                if (currentGameMap != null)
                {
                    currentGameMap.SetTileState(pos, TileState.Trap);
                }
                BindTrapToTile(spawned, pos);
            }

            return spawned;
        }

        return null;
    }

    private void BindTrapToTile(GameObject spawnedObj, Vector2Int pos)
    {
        if (spawnedObj == null || currentGameMap == null) return;

        Tile tile = currentGameMap.GetTile(pos);
        if (tile == null) return;

        TrapObject trap = spawnedObj.GetComponent<TrapObject>();
        if (trap == null)
        {
            trap = spawnedObj.AddComponent<TrapObject>();
        }

        if (!ReferenceEquals(tile.occupyingHazard, trap))
        {
            trap.InitializeOnTile(tile);
        }
        trap.SetTriggerCondition(IsBattleTrapActive);
    }

    private bool IsBattleTrapActive()
    {
        if (currentMapState != MapState.Battle) return false;
        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            BattleState state = RunManager.instance.battleManager.battleState;
            return state != BattleState.None && state != BattleState.BattleEnd;
        }
        return true;
    }

    public void GenerateEnemy(MapDataSO mapData, List<string> spawnMonsterList, int spawnCount)
    {
        int count = Mathf.Min(spawnCount, Mathf.Min(spawnEnemyCells.Count, spawnMonsterList.Count));
        List<List<Tile>> tileMap = currentGameMap != null ? currentGameMap.GetTileMap() : null;

        for (int i = 0; i < count; i++)
        {
            CellData cell = spawnEnemyCells[i];
            string monsterId = spawnMonsterList[i];

            NPCUnitData aiData = ResolveNPCUnitData(monsterId);
            if (aiData == null || aiData.characterStat == null)
            {
                Debug.LogError($"[StageManager] 몬스터 데이터(NPCUnitData 또는 CharacterStat)를 찾을 수 없어 스폰을 건너뜁니다: {monsterId}");
                continue;
            }

            string modelAddress = !string.IsNullOrEmpty(aiData.prefabAddress)
                ? aiData.prefabAddress
                : monsterId;

            GameObject monsterPrefab = ResolveMonsterPrefab(modelAddress);
            if (monsterPrefab == null)
            {
                Debug.LogError($"[StageManager] 몬스터 프리팹 로드 실패: {modelAddress}");
                continue;
            }

            Vector3 worldPos = mapData.CellToWorld(cell.position);

            GameObject newMonster = UnityEngine.Object.Instantiate(monsterPrefab, worldPos, Quaternion.identity);
            Character enemyCharacter = newMonster.GetComponent<Character>();
            if (enemyCharacter == null)
            {
                var controller = newMonster.GetComponent<ICharacterController>();
                if (controller != null) enemyCharacter = controller.controlledCharacter;
            }

            ICharacterController enemyController = null;
            if (enemyCharacter != null)
            {
                enemyCharacter.faction = CharacterFaction.Enemy;
                enemyCharacter.InitializeStat(aiData.characterStat);
                enemyController = new NPCUnitController(enemyCharacter, aiData);

                if (enemyCharacter.characterMove != null)
                {
                    enemyCharacter.characterMove.SetFacingDirection(LookDirection.Down);
                    if (tileMap != null &&
                        cell.position.x >= 0 && cell.position.x < tileMap.Count &&
                        cell.position.y >= 0 && cell.position.y < tileMap[cell.position.x].Count)
                    {
                        enemyCharacter.characterMove.SetCurrentTile(tileMap[cell.position.x][cell.position.y]);
                    }
                }
            }

            RegisterSpawnEnemy(newMonster, enemyController);
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

        Vector3 worldPos = mapData.CellToWorld(cell.position);

        GameObject basePrefab = null;
        if (AssetCacheManager.instance != null)
        {
            AssetCacheManager.instance.TryGetModel("EventObject", out basePrefab);
        }

        if (basePrefab == null)
        {
            Debug.LogError("[StageManager] EventObject 프리팹을 Addressables 캐시에서 찾을 수 없습니다!");
            return;
        }

        GameObject spawned = UnityEngine.Object.Instantiate(basePrefab, worldPos, Quaternion.identity);
        InteractableType resolvedType = InteractableData.ResolveType(EncountType, eventData);
        InteractableObject newNPC = InteractableObject.ConfigureOnGameObject(spawned, resolvedType, eventData);

        if (newNPC != null)
        {
            RegisterSpawnNPC(spawned);
        }
    }
    public void SpawnPlayerInMap(MapDataSO mapData, Character playerCharacter, int spawnCount)
    {
        if (playerCharacter == null || mapData == null)
        {
            Debug.LogError("[StageManager] SpawnPlayerInMap: playerCharacter 또는 mapData가 null입니다.");
            return;
        }

        if (spawnPlayerCells.Count == 0)
        {
            Debug.LogWarning("No Player Spawn Cells Found in Map Data!");
            return;
        }
        spawnPlayerCells.Shuffle();

        Vector2Int spawnCoord = spawnPlayerCells[0].position;

        // 셀의 그리드 좌표를 실제 월드 좌표로 변환 (셀 크기 및 오프셋 적용)
        Vector3 worldPos = mapData.CellToWorld(spawnCoord);
        // 플레이어 진영 설정
        playerCharacter.faction = CharacterFaction.Player;

        //플레이어 이동 및 타일 정보 업데이트
        if (playerCharacter.characterMove != null)
        {
            playerCharacter.characterMove.StopMoving();
        }
        playerCharacter.transform.position = worldPos;
        if (playerCharacter.characterMove != null && currentGameMap != null)
        {
            List<List<Tile>> tileMap = currentGameMap.GetTileMap();
            if (tileMap != null &&
                spawnCoord.x >= 0 && spawnCoord.x < tileMap.Count &&
                spawnCoord.y >= 0 && spawnCoord.y < tileMap[spawnCoord.x].Count)
            {
                playerCharacter.characterMove.SetCurrentTile(tileMap[spawnCoord.x][spawnCoord.y]);
            }
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
                case EncountType.None:
                    if (stageMapData.startMapDataList != null && stageMapData.startMapDataList.Count > 0)
                    {
                        targetList = stageMapData.startMapDataList;
                    }
                    else if (stageMapData.secretMapDataList != null && stageMapData.secretMapDataList.Count > 0)
                    {
                        targetList = stageMapData.secretMapDataList;
                    }
                    else
                    {
                        targetList = stageMapData.battleMapDataList;
                    }
                    break;
                case EncountType.Battle:
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
                Debug.LogWarning($"[StageManager] {EncountType} 맵이 없어 기본 전투 맵({currentMapData.stageName})으로 Fallback합니다.");
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

            string fallbackStageName = currentMapData != null ? currentMapData.stageName : "null";
            Debug.LogWarning($"[StageManager] StageMapDataBundle에서 맵을 찾지 못해 글로벌 맵 리스트({fallbackStageName})에서 Fallback했습니다.");
        }

        if (currentMapData != null)
        {
            Debug.Log($"[StageManager] Selected MapData: {currentMapData.stageName} (Location: {stageName}, Encount: {EncountType})");
        }
        else
        {
            Debug.LogError($"[StageManager] MapData를 전혀 찾을 수 없습니다! (Location: {stageName}, Encount: {EncountType})");
        }
    }

    public void ClearStage()
    {
        // 살아남은 적 컨트롤러들의 루아 리소스 및 이벤트 바인딩 해제
        for (int i = 0; i < _currentEnemyControllers.Count; i++)
        {
            if (_currentEnemyControllers[i] is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _currentEnemyControllers.Clear();

        // 현재 씬에 존재하는 적 오브젝트를 모두 제거
        foreach (GameObject enemy in _currentSpawnEnemyList)
        {
            if (enemy != null)
            {
                UnityEngine.Object.Destroy(enemy);
            }
        }
        _currentSpawnEnemyList.Clear();


        // 현재 씬에 존재하는 NPC 오브젝트를 모두 제거
        foreach (GameObject obj in _currentSpawnNPCList)
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }
        _currentSpawnNPCList.Clear();

        // 현재 씬에 존재하는 장애물, 함정 오브젝트를 모두 제거
        foreach (GameObject etc in _currentSpawnEtcList)
        {
            if (etc != null)
            {
                UnityEngine.Object.Destroy(etc);
            }
        }
        _currentSpawnEtcList.Clear();

        // GameMap 게임오브젝트를 파괴하지 않고 타일들만 정리하여 인스턴스를 재사용합니다.
        if (currentGameMap != null)
        {
            currentGameMap.ClearTiles();
        }
    }

    #region Monster & NPC Resolution Helpers (O(1) Cached)

    private NPCUnitData ResolveNPCUnitData(string monsterId)
    {
        if (string.IsNullOrEmpty(monsterId) || ModLoader.Instance == null || ModLoader.Instance.NPCUnitDatabase == null)
            return null;

        if (_npcUnitDataCache.TryGetValue(monsterId, out var cachedData))
            return cachedData;

        if (ModLoader.Instance.NPCUnitDatabase.TryGetValue(monsterId, out var aiData))
        {
            _npcUnitDataCache[monsterId] = aiData;
            return aiData;
        }

        Debug.LogError($"[StageManager] ModLoader.NPCUnitDatabase에서 적 데이터를 찾을 수 없습니다: '{monsterId}'");
        _npcUnitDataCache[monsterId] = null;
        return null;
    }

    private GameObject ResolveMonsterPrefab(string modelAddress)
    {
        if (string.IsNullOrEmpty(modelAddress) || AssetCacheManager.instance == null)
            return null;

        if (_monsterPrefabCache.TryGetValue(modelAddress, out var cachedPrefab))
            return cachedPrefab;

        if (AssetCacheManager.instance.TryGetModel(modelAddress, out GameObject prefab))
        {
            _monsterPrefabCache[modelAddress] = prefab;
            return prefab;
        }

        Debug.LogError($"[StageManager] AssetCacheManager에서 몬스터 프리팹을 찾을 수 없습니다: '{modelAddress}'");
        _monsterPrefabCache[modelAddress] = null;
        return null;
    }

    #endregion
}
