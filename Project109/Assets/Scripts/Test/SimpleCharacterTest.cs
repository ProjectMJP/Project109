using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using EventStructs;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// RunManager와 MapManager 등 실제 인게임 시스템을 활용하여
/// 맵 및 플레이어를 스폰하고 이동 테스트를 수행하는 매니저 클래스입니다.
/// </summary>
public class SimpleCharacterTest : MonoBehaviour
{
    [Header("프리팹 설정 (스폰 폴백용)")]
    [SerializeField] private GameObject playerPrefab;

    private Character playerCharacter;
    private RoutePathfinding routePathfinding = new RoutePathfinding();
    private List<Tile> debugCanMoveTiles = new List<Tile>();

    private IEnumerator Start()
    {
        Debug.Log("[SimpleCharacterTest] Start 코루틴 진입.");
        
        float logTimer = 0f;
        while (AssetCacheManager.instance == null || !AssetCacheManager.instance.isLoadComplete)
        {
            logTimer += Time.deltaTime;
            if (logTimer >= 1f)
            {
                Debug.Log($"[SimpleCharacterTest] 로딩 대기 중... AssetCacheManager.instance: {AssetCacheManager.instance != null}, isLoadComplete: {(AssetCacheManager.instance != null ? AssetCacheManager.instance.isLoadComplete : false)}");
                logTimer = 0f;
            }
            yield return null;
        }

        Debug.Log("[SimpleCharacterTest] 모든 데이터 로딩이 완료되었습니다. 테스트 맵 생성을 시작합니다.");

        InitializeTestRun();
    }

    private void InitializeTestRun()
    {
        if (RunManager.instance == null)
        {
            Debug.LogError("[SimpleCharacterTest] RunManager.instance를 찾을 수 없습니다. 씬에 GameManager(RunManager 부착)가 배치되어 있어야 합니다.");
            return;
        }

        // 1. GameSceneManager를 통해 런타임에 초기화된 플레이어 캐릭터 탐색
        playerCharacter = GameSceneManager.instance?.player?.character;

        if (playerCharacter != null)
        {
            playerCharacter.gameObject.layer = LayerMask.NameToLayer("Player");
            foreach (Transform child in playerCharacter.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = LayerMask.NameToLayer("Player");
            }
        }

        if (playerCharacter == null)
        {
            // 수동 배치된 캐릭터가 없으면 프리팹을 활용해 생성 시도
            if (playerPrefab != null)
            {
                GameObject pObj = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                pObj.name = "TestPlayer";
                playerCharacter = pObj.GetComponent<Character>();
                if (playerCharacter == null)
                {
                    playerCharacter = pObj.AddComponent<Character>();
                }

                // 임시 캐릭터 스탯 설정
                CharacterStat tempStat = new CharacterStat
                {
                    maxHealth = 100f,
                    maxStamina = 100f,
                    staminaRegenPerSecond = 10f,
                    maxMoveCount = 999,
                    maxTilesPerMove = 5 // 테스트용으로 이동력을 5칸으로 제한 설정
                };
                playerCharacter.InitializeStat(tempStat);
                playerCharacter.faction = CharacterFaction.Player;

                // GameSceneManager의 플레이어 인스턴스에 바인딩
                if (GameSceneManager.instance != null)
                {
                    if (GameSceneManager.instance.player != null)
                    {
                        GameSceneManager.instance.player.character = playerCharacter;
                    }
                    else
                    {
                        GameSceneManager.instance.player = new Player(playerCharacter);
                    }
                }
            }
            else
            {
                Debug.LogError("[SimpleCharacterTest] 플레이어 캐릭터 프리팹을 확보할 수 없어 스폰을 중단합니다.");
                return;
            }
        }

        // 2. 실제 MapManager의 GenerateStage를 이용해 정식 맵 스폰
        MapManager mapManager = RunManager.instance.currentMap;
        if (mapManager != null)
        {
            Debug.Log("[SimpleCharacterTest] MapManager.GenerateStage를 실행합니다.");

            // LostTemple 스테이지의 일반 전투 맵 데이터를 기반으로 맵을 생성
            LocationType mapLocation = LocationType.Temple;
            IncountType incountType = IncountType.Battle;

            mapManager.GenerateStage(
                mapLocation,
                incountType,
                playerCharacter,
                RunManager.instance.MapPrefabs
            );

            // 스폰된 모든 타일에 마우스 클릭 Raycast 처리가 가능하도록 BoxCollider 보강
            AddCollidersToTiles();

            // [테스트 셋업] 임시 벽(장애물)을 생성하여 맵 중심부에 배치하고 타일 상태를 Obstacle로 변경
            SpawnTemporaryObstacles();

            // 맵 및 캐릭터 스폰 완료 후 디버그 하이라이트 범위 최초 갱신
            UpdateCanMoveIndicator();

            // 입력 컨트롤러의 클릭 이벤트 구독 (드래그 필터링 적용된 클릭)
            if (PlayerInputController.instance != null)
            {
                PlayerInputController.instance.OnTouchClickEvent += HandleTileClick;
            }
        }
        else
        {
            Debug.LogError("[SimpleCharacterTest] RunManager.instance.currentMap이 존재하지 않습니다.");
        }
    }

    private void OnDestroy()
    {
        // 이벤트 해제
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchClickEvent -= HandleTileClick;
        }
    }

    /// <summary>
    /// 생성된 타일 맵 오브젝트들에 마우스 클릭 Raycast 처리가 가능하도록 BoxCollider를 보강해 줍니다.
    /// </summary>
    private void AddCollidersToTiles()
    {
        var gameMap = RunManager.instance.currentMap.currentGameMap;
        if (gameMap == null) return;

        List<List<Tile>> tileMap = gameMap.GetTileMap();
        if (tileMap == null) return;

        foreach (var column in tileMap)
        {
            foreach (var tile in column)
            {
                if (tile != null && tile.GetComponent<Collider>() == null)
                {
                    BoxCollider box = tile.gameObject.AddComponent<BoxCollider>();
                }
            }
        }
        Debug.Log("[SimpleCharacterTest] 타일 그리드에 BoxCollider 부착 및 물리 레이캐스트 지원 세팅 완료.");
    }

#if UNITY_EDITOR || DEBUG_MODE
    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                bool isGhost = playerCharacter != null && playerCharacter.characterMove != null && playerCharacter.characterMove.capabilities.HasFlag(MoverCapability.PassWalls);
                if (playerCharacter != null && playerCharacter.characterMove != null)
                {
                    if (isGhost)
                    {
                        playerCharacter.characterMove.capabilities &= ~(MoverCapability.PassWalls | MoverCapability.PassObstacles);
                    }
                    else
                    {
                        playerCharacter.characterMove.capabilities |= (MoverCapability.PassWalls | MoverCapability.PassObstacles);
                    }
                    Debug.Log($"[SimpleCharacterTest] Keyboard G - 고스트 모드 토글됨. capabilities: {playerCharacter.characterMove.capabilities}");
                    UpdateCanMoveIndicator();
                }
            }
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                bool isFlying = playerCharacter != null && playerCharacter.characterMove != null && playerCharacter.characterMove.capabilities.HasFlag(MoverCapability.IgnoreTraps);
                if (playerCharacter != null && playerCharacter.characterMove != null)
                {
                    if (isFlying)
                    {
                        playerCharacter.characterMove.capabilities &= ~(MoverCapability.PassObstacles | MoverCapability.IgnoreTraps);
                    }
                    else
                    {
                        playerCharacter.characterMove.capabilities |= (MoverCapability.PassObstacles | MoverCapability.IgnoreTraps);
                    }
                    Debug.Log($"[SimpleCharacterTest] Keyboard F - 플라이 모드 토글됨. capabilities: {playerCharacter.characterMove.capabilities}");
                    UpdateCanMoveIndicator();
                }
            }
        }
    }

    private void HandleTileClick(Vector2 mousePos)
    {
        if (Camera.main == null) return;
        if (playerCharacter == null) return;

        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        // 오직 Tile 레이어(인덱스 10)만 타겟으로 클릭 처리합니다. (기타 Map 레이어의 큐브 장애물 등은 관통)
        int tileMask = LayerMask.GetMask("Tile");
        if (Physics.Raycast(ray, out RaycastHit hit, 10000.0f, tileMask))
        {
            Tile clickedTile = hit.collider.GetComponentInParent<Tile>();
            if (clickedTile == null)
            {
                clickedTile = hit.collider.GetComponent<Tile>();
            }

            if (clickedTile != null)
            {
                Debug.Log($"[SimpleCharacterTest] 클릭한 타일: {clickedTile.GetCoordToString()}");

                // 플레이어가 유휴 상태(Idle)일 때만 이동 명령 수용
                if (playerCharacter.currentState == CharacterState.Idle)
                {
                    Tile startTile = playerCharacter.characterMove.GetCurrentTile();
                    if (startTile == clickedTile)
                    {
                        Debug.Log("[SimpleCharacterTest] 현재 캐릭터가 딛고 서 있는 타일입니다.");
                        return;
                    }

                    // A* 알고리즘으로 이동 가능 경로 탐색
                    var gameMap = RunManager.instance.currentMap.currentGameMap;
                    if (gameMap != null)
                    {
                        var tileMap = gameMap.GetTileMap();

                        // 목적지 타일에 멈춰설 수 있는지 검증 (벽 관통/비행 캐릭터는 벽에 멈춤 차단, 일반 캐릭터는 장애물/벽 멈춤 차단)
                        if (!clickedTile.CanEnter(playerCharacter.characterMove))
                        {
                            Debug.LogWarning("[SimpleCharacterTest] 해당 타일에는 멈춰 설 수 없습니다.");
                            return;
                        }

                        // 클릭한 타일이 계산된 이동 가능 범위(debugCanMoveTiles)에 속해 있는지 체크
                        if (!debugCanMoveTiles.Contains(clickedTile))
                        {
                            Debug.LogWarning("[SimpleCharacterTest] 이동 범위를 벗어난 타일입니다.");
                            return;
                        }

                        // 다이렉트로 길찾기 수행 (capabilities 적용)
                        MoverCapability caps = playerCharacter.characterMove != null ? playerCharacter.characterMove.capabilities : MoverCapability.None;
                        List<Tile> path = routePathfinding.TilePathfinding(startTile, clickedTile, tileMap, caps);

                        if (path != null && path.Count > 0)
                        {
                            Debug.Log($"[SimpleCharacterTest] 경로가 탐색되어 이동을 수행합니다. 경로 타일 수: {path.Count}");
                            
                            // 이동 시작 전에 디버그 이동 범위 하이라이트 평면 끄기
                            ClearDebugIndicator();

                            playerCharacter.characterMove.MoveAlongPath(path, clickedTile, () =>
                            {
                                Debug.Log($"[SimpleCharacterTest] 이동 완료. 현재 좌표: {playerCharacter.characterMove.GetCurrentTile().GetCoordToString()}");
                                // 이동 완료 후 새 좌표 기준으로 디버그 하이라이트 영역 재생성
                                UpdateCanMoveIndicator();
                            });
                        }
                        else
                        {
                            // 디버깅 정보 상세 출력
                            string tileStateMapStr = "";
                            for (int col = 0; col < tileMap.Count; col++)
                            {
                                for (int row = 0; row < tileMap[col].Count; row++)
                                {
                                    var t = tileMap[col][row];
                                    tileStateMapStr += $"[{col},{row}]:{t.tileState} ";
                                }
                                tileStateMapStr += "\n";
                            }

                            Debug.LogWarning($"[SimpleCharacterTest] 경로 탐색 실패!\n" +
                                $"시작 타일: {startTile.GetCoordToString()} (상태: {startTile.tileState})\n" +
                                $"목적 타일: {clickedTile.GetCoordToString()} (상태: {clickedTile.tileState})\n" +
                                $"전체 타일 상태:\n{tileStateMapStr}");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 디버그용 이동 가능 타일 범위를 계산하고 맵 인디케이터(오버레이)에 표시합니다.
    /// </summary>
    private void UpdateCanMoveIndicator()
    {
        ClearDebugIndicator();

        if (playerCharacter == null)
        {
            Debug.LogWarning("[SimpleCharacterTest] UpdateCanMoveIndicator - playerCharacter가 null입니다.");
            return;
        }
        if (playerCharacter.characterMove == null)
        {
            Debug.LogWarning("[SimpleCharacterTest] UpdateCanMoveIndicator - characterMove가 null입니다.");
            return;
        }

        Debug.Log($"[SimpleCharacterTest] UpdateCanMoveIndicator 호출됨. 현재 상태: {playerCharacter.currentState}");

        if (playerCharacter.currentState != CharacterState.Idle)
        {
            Debug.LogWarning($"[SimpleCharacterTest] UpdateCanMoveIndicator - 캐릭터가 Idle 상태가 아닙니다. 현재 상태: {playerCharacter.currentState}");
            return;
        }

        var gameMap = RunManager.instance?.currentMap?.currentGameMap;
        if (gameMap == null)
        {
            Debug.LogWarning("[SimpleCharacterTest] UpdateCanMoveIndicator - currentGameMap이 null입니다.");
            return;
        }

        Tile startTile = playerCharacter.characterMove.GetCurrentTile();
        if (startTile == null)
        {
            Debug.LogWarning("[SimpleCharacterTest] UpdateCanMoveIndicator - startTile이 null입니다.");
            return;
        }

        int maxMoveDist = 5;
        if (playerCharacter.curCharacterStat != null && playerCharacter.curCharacterStat.maxTilesPerMove > 0)
        {
            maxMoveDist = playerCharacter.curCharacterStat.maxTilesPerMove;
        }
        else
        {
            Debug.LogWarning($"[SimpleCharacterTest] curCharacterStat이 null이거나 maxTilesPerMove가 0입니다. 기본값인 {maxMoveDist}로 강제 설정합니다. curCharacterStat null 여부: {playerCharacter.curCharacterStat == null}");
        }

        // 캐릭터의 이동 능력(capabilities)을 넘겨서 해당 능력에 맞춰 탐색된 타일 범위를 수집
        debugCanMoveTiles = gameMap.GetReachableTiles(startTile, maxMoveDist, playerCharacter.characterMove);

        Debug.Log($"[SimpleCharacterTest] GetReachableTiles 완료. 탐색된 이동 범위 타일 수: {debugCanMoveTiles.Count}");

        // 맵 오버레이 평면 활성화 및 그리기
        var indicator = gameMap.GetMoveRangeIndicator();
        if (indicator != null)
        {
            indicator.gameObject.SetActive(true);
            indicator.ShowWalkableTiles(debugCanMoveTiles, gameMap.GetTileMap());
        }
    }

    /// <summary>
    /// 현재 활성화되어 있는 디버그용 하이라이트 정보 및 오버레이 메쉬를 모두 초기화합니다.
    /// </summary>
    private void ClearDebugIndicator()
    {
        var gameMap = RunManager.instance?.currentMap?.currentGameMap;
        if (gameMap != null)
        {
            var indicator = gameMap.GetMoveRangeIndicator();
            if (indicator != null)
            {
                indicator.ClearAllTiles(gameMap.GetTileMap());
                indicator.gameObject.SetActive(false);
            }
        }

        foreach (var tile in debugCanMoveTiles)
        {
            if (tile != null)
            {
                tile.SetMoveIndicator(false);
            }
        }
        debugCanMoveTiles.Clear();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (playerPrefab == null)
        {
            playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/PlayerCharacterSample.prefab");
        }
    }
#endif

    private Rect debugWindowRect = new Rect(20, 20, 280, 480);

    private void OnGUI()
    {
        if (playerCharacter == null || playerCharacter.characterMove == null) return;

        // 드래그 윈도우 스타일의 디버그 치트 컨트롤 패널 표시
        debugWindowRect = GUI.Window(999, debugWindowRect, DrawDebugCheatWindow, "Debug Cheat Panel");
    }

    private void DrawDebugCheatWindow(int windowID)
    {
        // 상단 바 드래그 활성화
        GUI.DragWindow(new Rect(0, 0, 280, 20));

        GUILayout.BeginVertical();
        GUILayout.Space(5);

        // 1. Capabilities 설정 (체크박스 Toggle)
        GUILayout.Label("<b>[ Mover Capabilities ]</b>");
        
        MoverCapability currentCaps = playerCharacter.characterMove.capabilities;
        bool passWalls = currentCaps.HasFlag(MoverCapability.PassWalls);
        bool passObstacles = currentCaps.HasFlag(MoverCapability.PassObstacles);
        bool ignoreTraps = currentCaps.HasFlag(MoverCapability.IgnoreTraps);

        bool newPassWalls = GUILayout.Toggle(passWalls, " Pass Walls (벽 관통)");
        bool newPassObstacles = GUILayout.Toggle(passObstacles, " Pass Obstacles (장애물 관통)");
        bool newIgnoreTraps = GUILayout.Toggle(ignoreTraps, " Ignore Traps (함정 무시)");

        // 체크박스 값 변경 시 즉시 capability 비트 조합 갱신 및 오버레이 리드로잉
        if (newPassWalls != passWalls || newPassObstacles != passObstacles || newIgnoreTraps != ignoreTraps)
        {
            MoverCapability targetCaps = MoverCapability.None;
            if (newPassWalls) targetCaps |= (MoverCapability.PassWalls | MoverCapability.PassObstacles);
            if (newPassObstacles) targetCaps |= MoverCapability.PassObstacles;
            if (newIgnoreTraps) targetCaps |= (MoverCapability.PassObstacles | MoverCapability.IgnoreTraps);

            playerCharacter.characterMove.capabilities = targetCaps;
            Debug.Log($"[SimpleCharacterTest] UI Toggle - capabilities 갱신 완료: {playerCharacter.characterMove.capabilities}");
            UpdateCanMoveIndicator();
        }

        GUILayout.Space(10);

        // 2. Character Stats 조작
        GUILayout.Label("<b>[ Character Stats ]</b>");
        GUILayout.Label($"HP: {playerCharacter.curHealth:F0} / {playerCharacter.curCharacterStat.maxHealth:F0}");
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Damage 10"))
        {
            DamageInfo dmg = new DamageInfo(null, playerCharacter, 10f, DamageFlag.Normal);
            playerCharacter.TakeDamage(dmg);
            Debug.Log($"[SimpleCharacterTest] 치트: 플레이어 10 데미지 적용. 현재 체력: {playerCharacter.curHealth}");
        }
        if (GUILayout.Button("Damage 50"))
        {
            DamageInfo dmg = new DamageInfo(null, playerCharacter, 50f, DamageFlag.Normal);
            playerCharacter.TakeDamage(dmg);
            Debug.Log($"[SimpleCharacterTest] 치트: 플레이어 50 데미지 적용. 현재 체력: {playerCharacter.curHealth}");
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Heal 10"))
        {
            HealInfo hl = new HealInfo(null, playerCharacter, 10f, HealFlag.Normal);
            playerCharacter.TakeHeal(hl);
            Debug.Log($"[SimpleCharacterTest] 치트: 플레이어 10 힐 적용. 현재 체력: {playerCharacter.curHealth}");
        }
        if (GUILayout.Button("Heal 50"))
        {
            HealInfo hl = new HealInfo(null, playerCharacter, 50f, HealFlag.Normal);
            playerCharacter.TakeHeal(hl);
            Debug.Log($"[SimpleCharacterTest] 치트: 플레이어 50 힐 적용. 현재 체력: {playerCharacter.curHealth}");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // 3. Battle Deck 및 카드 조작
        GUILayout.Label("<b>[ Battle Deck Control ]</b>");
        var battleController = RunManager.instance?.playerBattleController;
        if (battleController != null && battleController.battleDeck != null)
        {
            GUILayout.Label($"Hand: {battleController.battleDeck.hand.Count} 장, DrawPile: {battleController.battleDeck.drawPile.Count} 장");
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Draw 1 Card"))
            {
                battleController.battleDeck.DrawCards(1);
                Debug.Log("[SimpleCharacterTest] 치트: 카드 1장 드로우.");
            }
            if (GUILayout.Button("Draw 5 Cards"))
            {
                battleController.battleDeck.DrawCards(5);
                Debug.Log("[SimpleCharacterTest] 치트: 카드 5장 드로우.");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Discard Hand"))
            {
                battleController.battleDeck.DiscardHand();
                Debug.Log("[SimpleCharacterTest] 치트: 손패 전체 버리기.");
            }
            if (GUILayout.Button("Shuffle Deck"))
            {
                battleController.battleDeck.ShuffleDrawPile();
                Debug.Log("[SimpleCharacterTest] 치트: 뽑는 덱 셔플 완료.");
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("<color=yellow>전투(BattleController) 대기 상태</color>");
        }

        GUILayout.EndVertical();
    }
#endif

    /// <summary>
    /// 테스트용 임시 벽(장애물) 큐브를 생성하여 배치하고 타일 상태를 Obstacle로 변경합니다.
    /// </summary>
    private void SpawnTemporaryObstacles()
    {
        var gameMap = RunManager.instance.currentMap.currentGameMap;
        if (gameMap == null) return;

        List<List<Tile>> tileMap = gameMap.GetTileMap();
        if (tileMap == null) return;

        int width = tileMap.Count;
        int height = tileMap[0].Count;

        // 맵 중간 쯤에 일렬로 벽을 세워 길을 막습니다 (예: X=3, Y=1~3 범위)
        int targetCol = width / 2;
        int startRow = 1;
        int endRow = Mathf.Min(height - 2, 3);

        for (int r = startRow; r <= endRow; r++)
        {
            if (targetCol >= 0 && targetCol < width && r >= 0 && r < height)
            {
                Tile tile = tileMap[targetCol][r];
                if (tile != null)
                {
                    // 1. 타일 상태를 Obstacle로 변경하여 길찾기 차단
                    tile.tileState = TileState.Obstacle;

                    // 2. 비주얼 확인을 위한 임시 3D 큐브 생성
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = $"TempObstacle_{targetCol}_{r}";
                    
                    // 큐브가 맵 오브젝트이므로 레이어를 Map(인덱스 9)으로 지정합니다.
                    wall.layer = LayerMask.NameToLayer("Map");

                    // 타일 위치보다 살짝 위에 큐브 배치 (높이는 1.5로)
                    wall.transform.position = tile.transform.position + new Vector3(0, 0.75f, 0);
                    wall.transform.localScale = new Vector3(1.0f, 1.5f, 1.0f);
                    wall.transform.SetParent(tile.transform);

                    // 큐브 머티리얼 색상을 어두운 회색으로 변경해 벽 느낌 제공
                    Renderer wallRenderer = wall.GetComponent<Renderer>();
                    if (wallRenderer != null)
                    {
                        wallRenderer.material.color = new Color(0.3f, 0.3f, 0.3f, 1.0f); // 어두운 회색 벽
                    }

                    Debug.Log($"[SimpleCharacterTest] 임시 벽 생성 완료. 좌표: ({targetCol}, {r}), 타일 상태: Obstacle");
                }
            }
        }
    }
}
