using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleState
{
    None,
    Intro,
    Combat,
    TurnInProgress,
    BattleEnd,
}

public enum BattleResultType
{
    Victory,
    Defeat,
    Escape,
}

public class BattleManager : IInitializable, IDisposable
{
    private const string PREF_BATTLE_SPEED_INDEX = "Settings_BattleSpeedIndex";
    public static readonly float[] SpeedSteps = { 1.0f, 1.5f, 2.0f };
    public static int CurrentSpeedIndex { get; private set; }
    public static float GlobalBattleSpeed => SpeedSteps[CurrentSpeedIndex];
    public static event Action<float> OnBattleSpeedChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadSavedBattleSpeedOnStartup()
    {
        int savedIndex = PlayerPrefs.GetInt(PREF_BATTLE_SPEED_INDEX, 0);
        SetGlobalBattleSpeedIndex(savedIndex, savePrefs: false);
    }

    public static void SetGlobalBattleSpeedIndex(int index, bool savePrefs = true)
    {
        int clampedIndex = Mathf.Clamp(index, 0, SpeedSteps.Length - 1);
        CurrentSpeedIndex = clampedIndex;

        float speed = GlobalBattleSpeed;
        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            RunManager.instance.battleManager.battleTimeScale = speed;
        }

        if (savePrefs)
        {
            PlayerPrefs.SetInt(PREF_BATTLE_SPEED_INDEX, clampedIndex);
            PlayerPrefs.Save();
        }

        OnBattleSpeedChanged?.Invoke(speed);
    }

    public static void SetGlobalBattleSpeed(float speed, bool savePrefs = true)
    {
        int bestIndex = 0;
        float minDiff = float.MaxValue;
        for (int i = 0; i < SpeedSteps.Length; i++)
        {
            float diff = Mathf.Abs(SpeedSteps[i] - speed);
            if (diff < minDiff)
            {
                minDiff = diff;
                bestIndex = i;
            }
        }

        SetGlobalBattleSpeedIndex(bestIndex, savePrefs);
    }

    public static void CycleGlobalBattleSpeed()
    {
        int nextIndex = (CurrentSpeedIndex + 1) % SpeedSteps.Length;
        SetGlobalBattleSpeedIndex(nextIndex, savePrefs: true);
    }

    public BattleManager()
    {
        battleTimeScale = GlobalBattleSpeed;
        OnBattleSpeedChanged -= HandleBattleSpeedChanged;
        OnBattleSpeedChanged += HandleBattleSpeedChanged;
    }

    public void Initialize()
    {
        // 초기화 필요한 멤버 변수가 있다면 여기서 처리
        battleTimeScale = GlobalBattleSpeed;
        OnBattleSpeedChanged -= HandleBattleSpeedChanged;
        OnBattleSpeedChanged += HandleBattleSpeedChanged;
    }

    private void HandleBattleSpeedChanged(float speed)
    {
        battleTimeScale = speed;
    }

    public void Dispose()
    {
        OnBattleSpeedChanged -= HandleBattleSpeedChanged;
        OnBattleIntroRequested = null;
        OnBattleWon = null;
        OnBattleLost = null;
        OnBattleEnded = null;
        OnTimelineDirty = null;

        if (LuaManager.IsShuttingDown || LuaManager.IsDisposing)
        {
            // 종료/LuaEnv 해제 경로에서는 Lua OnTurnEnd·OnBattleEnd 콜백을 유발하지 않고 안전 정리만 수행합니다.
            CleanupCombatantsForShutdown();
            battleState = BattleState.None;
            accumulativeGoldReward = 0;

            if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
            {
                GameSceneManager.instance.player.DisposeBattleDeck(suppressEvents: true);
            }
        }
        else
        {
            ClearBattle();
        }
    }

    #region Battle Events

    /// <summary>전투 인트로 연출 시작 요청 이벤트. 구독자가 연출 완료 후 인자로 넘겨진 onComplete(StartCombat)를 호출합니다.</summary>
    public event Action<Action> OnBattleIntroRequested;

    /// <summary>전투 승리 시 발행되는 이벤트 (상위 시스템에서 보상 및 맵 상태 복귀 처리)</summary>
    public event Action OnBattleWon;

    /// <summary>전투 패배 시 발행되는 이벤트</summary>
    public event Action OnBattleLost;

    /// <summary>전투 종료 시 결과 타입과 함께 발행되는 이벤트</summary>
    public event Action<BattleResultType> OnBattleEnded;

    /// <summary>턴 시작/종료·전투원 합류/사망 등으로 타임라인 순서 재계산이 필요할 때 발행됩니다.</summary>
    public event Action OnTimelineDirty;

    #endregion

    #region Battle State

    public BattleState battleState { get; private set; } = BattleState.None;

    private List<ICharacterController> playerTeam = new();
    private List<ICharacterController> enemyTeam = new();
    private readonly List<ICharacterController> allCombatantsCache = new();
    private readonly TimelinePredictor _timelinePredictor = new TimelinePredictor();
    public IReadOnlyList<ICharacterController> PlayerTeam => playerTeam;
    public IReadOnlyList<ICharacterController> EnemyTeam => enemyTeam;
    public ICharacterController currentTurnController { get; private set; }
    public float battleTimeScale = 1f;
    public int accumulativeGoldReward { get; set; }

    /// <summary>
    /// 현재 전투원들의 다음 1회 행동 순서 및 플레이어 다음 차례(이월 포함)를 예측합니다. (GC 무할당)
    /// </summary>
    public void PredictTimeline(List<TimelineEntry> results, in TimelinePreviewRequest preview = default)
    {
        _timelinePredictor.Predict(playerTeam, enemyTeam, currentTurnController, results, in preview);
    }

    /// <summary>
    /// 전장의 아군 및 적군 컨트롤러 목록을 합쳐서 반환합니다. (내부 캐시 재사용으로 GC 무할당)
    /// </summary>
    public IReadOnlyList<ICharacterController> GetAllCombatants()
    {
        allCombatantsCache.Clear();
        allCombatantsCache.AddRange(playerTeam);
        allCombatantsCache.AddRange(enemyTeam);
        return allCombatantsCache;
    }

    #endregion

    #region Battle Lifecycle

    public void InitBattle(List<ICharacterController> players, List<ICharacterController> enemies)
    {
        // 이전 전투 잔여 데이터 강제 정리
        ClearBattle();
        battleTimeScale = GlobalBattleSpeed;

        playerTeam = new List<ICharacterController>();
        if (players != null)
        {
            foreach (var combatant in players)
            {
                if (combatant == null || combatant.controlledCharacter == null)
                {
                    Debug.LogError("[BattleManager] InitBattle: playerTeam에 null 컨트롤러 또는 controlledCharacter가 null인 요소가 전달되었습니다.");
                    continue;
                }
                playerTeam.Add(combatant);
            }
        }

        enemyTeam = new List<ICharacterController>();
        if (enemies != null)
        {
            foreach (var combatant in enemies)
            {
                if (combatant == null || combatant.controlledCharacter == null)
                {
                    Debug.LogError("[BattleManager] InitBattle: enemyTeam에 null 컨트롤러 또는 controlledCharacter가 null인 요소가 전달되었습니다.");
                    continue;
                }
                enemyTeam.Add(combatant);
            }
        }

        if (playerTeam.Count == 0)
        {
            Debug.LogError("[BattleManager] InitBattle: 유효한 플레이어 전투원이 없어 전투 초기화를 중단합니다.");
            return;
        }

        // 스태미나 초기화 + 사망 이벤트 구독 (플레이어 → 적 순서)
        foreach (var combatant in playerTeam)
            InitCombatant(combatant);
        foreach (var combatant in enemyTeam)
            InitCombatant(combatant);

        battleState = BattleState.Intro;

        // UI 계층이 구독 중이면 인트로 연출 위임, 구독자가 없으면 기존 UIManager 확인 또는 즉시 전투 개시
        if (OnBattleIntroRequested != null)
        {
            OnBattleIntroRequested.Invoke(StartCombat);
        }
        else if (UIManager.instance != null)
        {
            GameObject noticeUI = UIManager.instance.OpenUI(UIConstants.PANEL_BATTLE_NOTICE, UILayerType.Popup, true);
            if (noticeUI != null && noticeUI.TryGetComponent<UIBattleNotice>(out var battleNotice))
            {
                battleNotice.ShowNotice(BattleNoticeType.BattleStart, StartCombat);
            }
            else
            {
                StartCombat();
            }
        }
        else
        {
            StartCombat();
        }
    }

    public void StartCombat()
    {
        if (battleState != BattleState.Intro) return;

        // BattleStart 이벤트 (플레이어 우선) - 실제 전투 개시 시점에 호출
        foreach (var combatant in playerTeam)
        {
            if (combatant != null && combatant.controlledCharacter != null && combatant.controlledCharacter.eventBus != null)
            {
                CombatEventDispatcher.DispatchBattleStart(combatant.controlledCharacter.eventBus);
            }
        }
        foreach (var combatant in enemyTeam)
        {
            if (combatant != null && combatant.controlledCharacter != null && combatant.controlledCharacter.eventBus != null)
            {
                CombatEventDispatcher.DispatchBattleStart(combatant.controlledCharacter.eventBus);
            }
        }

        battleState = BattleState.Combat;
        RefreshAllEnemyIntents();
        OnTimelineDirty?.Invoke();
        Debug.Log("[BattleManager] Battle start intro finished. Combat began.");
    }

    /// <summary>
    /// 전투 중 아군/적군 추가 (소환, 증원 등)
    /// </summary>
    public void AddCombatant(ICharacterController controller, bool isPlayerTeam)
    {
        if (controller == null || controller.controlledCharacter == null)
        {
            Debug.LogError("[BattleManager] AddCombatant: null 컨트롤러 또는 controlledCharacter가 null인 전투원은 추가할 수 없습니다.");
            return;
        }

        if (isPlayerTeam)
            playerTeam.Add(controller);
        else
            enemyTeam.Add(controller);

        InitCombatant(controller);
        if (controller.controlledCharacter.eventBus != null)
        {
            CombatEventDispatcher.DispatchBattleStart(controller.controlledCharacter.eventBus);
        }
        RefreshAllEnemyIntents();
        OnTimelineDirty?.Invoke();
    }

    private void InitCombatant(ICharacterController combatant)
    {
        if (combatant == null || combatant.controlledCharacter == null)
        {
            Debug.LogError("[BattleManager] InitCombatant: combatant 또는 controlledCharacter가 null입니다.");
            return;
        }

        combatant.controlledCharacter.ResetCharacterForBattle();
        combatant.controlledCharacter.OnCharacterDied += OnCharacterDied;
        SubscribeCombatantIntentEvents(combatant.controlledCharacter);
        combatant.controlledCharacter.NotifyJoinedBattle();
    }

    public void Update(float dt)
    {
        if (battleState != BattleState.Combat) return;

        float scaledDt = dt * battleTimeScale;

        // 모든 전투원에 대해 공정하게 스태미나 틱을 먼저 진행 (프레임 dt 누수 방지)
        ICharacterController highestOverflowCombatant = null;
        float maxOverflow = float.MinValue;
        bool isPlayerOverflow = false;

        // 플레이어 팀 및 적 팀 틱 순차 진행 (단일 헬퍼 호출로 코드 중복 및 Fake Null 위험 제거)
        TickTeamCombatants(playerTeam, scaledDt, isPlayerTeam: true, ref highestOverflowCombatant, ref maxOverflow, ref isPlayerOverflow);
        TickTeamCombatants(enemyTeam, scaledDt, isPlayerTeam: false, ref highestOverflowCombatant, ref maxOverflow, ref isPlayerOverflow);

        // 턴 획득 조건을 만족한 전투원 중 최대 초과자에게 턴 부여
        if (highestOverflowCombatant != null)
        {
            RequestTurnStart(highestOverflowCombatant.controlledCharacter);
        }
    }

    private void TickTeamCombatants(List<ICharacterController> team, float scaledDt, bool isPlayerTeam,
        ref ICharacterController highestOverflowCombatant, ref float maxOverflow, ref bool isPlayerOverflow)
    {
        for (int i = 0; i < team.Count; i++)
        {
            var combatant = team[i];
            if (combatant == null || combatant.controlledCharacter == null || combatant.controlledCharacter.isDead) continue;

            combatant.controlledCharacter.BattleTick(scaledDt);
            if (combatant.controlledCharacter.curCharacterStat == null) continue;

            if (combatant.controlledCharacter.curStamina >= combatant.controlledCharacter.curCharacterStat.maxStamina)
            {
                float overflow = combatant.controlledCharacter.overflowStamina;
                bool shouldReplace;
                if (isPlayerTeam)
                {
                    shouldReplace = (highestOverflowCombatant == null || overflow > maxOverflow);
                }
                else
                {
                    // 동률일 경우 플레이어 우선권 유지 (isPlayerOverflow && overflow <= maxOverflow 인 경우 교체 안 함)
                    shouldReplace = (highestOverflowCombatant == null ||
                                    (!isPlayerOverflow && overflow > maxOverflow) ||
                                    (isPlayerOverflow && overflow > maxOverflow + 0.0001f));
                }

                if (shouldReplace)
                {
                    maxOverflow = overflow;
                    highestOverflowCombatant = combatant;
                    isPlayerOverflow = isPlayerTeam;
                }
            }
        }
    }

    public void RequestTurnStart(Character character)
    {
        if (battleState != BattleState.Combat) return;

        ICharacterController controller = FindController(character);

        if (controller != null)
            StartTurn(controller);
    }

    private void StartTurn(ICharacterController controller)
    {
        battleState = BattleState.TurnInProgress;
        currentTurnController = controller;
        controller.controlledCharacter.currentTurn++;
        controller.controlledCharacter.ExpireTurnStartModifiers();

        // 턴 시작 시 자연 회복 등으로 maxStamina를 초과한 경우 maxStamina로 보정
        if (controller.controlledCharacter != null && controller.controlledCharacter.curCharacterStat != null)
        {
            if (controller.controlledCharacter.curStamina > controller.controlledCharacter.curCharacterStat.maxStamina)
            {
                controller.controlledCharacter.curStamina = controller.controlledCharacter.curCharacterStat.maxStamina;
            }
        }

        if (controller.controlledCharacter != null)
        {
            controller.controlledCharacter.UpdateShieldDuration(); // 턴 시작 시 실드 지속 시간 갱신 (만료 정산)
            controller.controlledCharacter.ResetMoveStat();
        }

        controller.OnTurnStart();
        OnTimelineDirty?.Invoke();
    }

    /// <summary>
    /// 현재 턴을 종료한다. 플레이어가 턴 종료 버튼을 누르거나, AI가 행동을 마쳤을 때 호출.
    /// requester가 전달된 경우 현재 턴 소유자인지 검증하여 무효한 콜백의 턴 가로채기를 방어합니다.
    /// </summary>
    public void EndTurn(ICharacterController requester = null)
    {
        if (currentTurnController == null) return;

        // 호출자가 지정되어 있는데 현재 턴 소유자가 아니면 무시 (stale 콜백 또는 사망 유닛의 부정 턴 종료 방어)
        if (requester != null && requester != currentTurnController)
        {
            Debug.LogWarning($"[BattleManager] EndTurn ignored: Requester ({requester}) is not current turn controller ({currentTurnController})");
            return;
        }

        Character endingCharacter = currentTurnController.controlledCharacter;
        // 1. 턴 종료 스태미나 변동량 선계산 (OnTurnEnd에서 버프/유물이 제거·초기화되기 전에 실행)
        EventStructs.TurnEndStaminaInfo turnEndInfo = endingCharacter != null
            ? endingCharacter.CalculateTurnEndStamina()
            : default;

        // 2. 턴 종료 이벤트 실행 (버프 스택 감소/제거, 덱 버리기 등)
        currentTurnController.OnTurnEnd();

        if (battleState == BattleState.BattleEnd)
        {
            currentTurnController = null;
            OnTimelineDirty?.Invoke();
            return;
        }

        // 3. 선계산 결과 커밋 및 UntilTurnEnd 스탯 모디파이어 만료 처리
        if (endingCharacter != null)
        {
            endingCharacter.CommitTurnEndStamina(in turnEndInfo);
        }
        // 실드 지속시간 차감은 다음 턴 시작(StartTurn) 시점으로 이관됨

        currentTurnController = null;
        battleState = BattleState.Combat;
        RefreshAllEnemyIntents();
        OnTimelineDirty?.Invoke();
    }

    #region Enemy Intent Synchronization

    private void SubscribeCombatantIntentEvents(Character character)
    {
        if (character == null) return;

        if (character.characterMove != null)
        {
            character.characterMove.OnTileChanged -= HandleCombatantTileChanged;
            character.characterMove.OnTileChanged += HandleCombatantTileChanged;
        }

        if (character.effectManager != null)
        {
            character.effectManager.OnEffectAdded -= HandleCombatantEffectChanged;
            character.effectManager.OnEffectAdded += HandleCombatantEffectChanged;

            character.effectManager.OnEffectStacked -= HandleCombatantEffectChanged;
            character.effectManager.OnEffectStacked += HandleCombatantEffectChanged;

            character.effectManager.OnEffectRemoved -= HandleCombatantEffectChanged;
            character.effectManager.OnEffectRemoved += HandleCombatantEffectChanged;
        }
    }

    private void UnsubscribeCombatantIntentEvents(Character character)
    {
        if (character == null) return;

        if (character.characterMove != null)
        {
            character.characterMove.OnTileChanged -= HandleCombatantTileChanged;
        }

        if (character.effectManager != null)
        {
            character.effectManager.OnEffectAdded -= HandleCombatantEffectChanged;
            character.effectManager.OnEffectStacked -= HandleCombatantEffectChanged;
            character.effectManager.OnEffectRemoved -= HandleCombatantEffectChanged;
        }
    }

    private void HandleCombatantTileChanged(Character character, Tile previousTile, Tile newTile)
    {
        RefreshAllEnemyIntents();
    }

    private void HandleCombatantEffectChanged(Effect effect)
    {
        RefreshAllEnemyIntents();
    }

    /// <summary>
    /// 전투 중 살아있는 적 NPC들의 행동 예고 상태를 현재 타일 위치 및 버프/디버프에 맞춰 즉시 재계산합니다.
    /// (현재 턴 실행 중인 적 자신은 이동-공격 액션 큐 교란 방지를 위해 제외합니다.)
    /// </summary>
    public void RefreshAllEnemyIntents()
    {
        if (battleState != BattleState.Combat && battleState != BattleState.TurnInProgress)
        {
            return;
        }

        for (int i = 0; i < enemyTeam.Count; i++)
        {
            ICharacterController controller = enemyTeam[i];
            if (controller == null || controller.controlledCharacter == null || controller.controlledCharacter.isDead)
            {
                continue;
            }

            if (battleState == BattleState.TurnInProgress && ReferenceEquals(controller, currentTurnController))
            {
                continue;
            }

            if (controller is NPCUnitController npcController)
            {
                npcController.RefreshIntentState(isTurnStart: false);
            }
        }
    }

    #endregion

    #region Helper Methods (Zero-Alloc)

    /// <summary>
    /// 캐릭터에 대응하는 ICharacterController를 무할당(Zero-Alloc)으로 검색합니다.
    /// </summary>
    public ICharacterController FindController(Character character)
    {
        if (character == null) return null;

        ICharacterController controller = FindControllerInList(playerTeam, character);
        if (controller != null) return controller;

        return FindControllerInList(enemyTeam, character);
    }

    private ICharacterController FindControllerInList(List<ICharacterController> list, Character character)
    {
        if (character == null || list == null) return null;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].controlledCharacter == character)
                return list[i];
        }
        return null;
    }

    private bool RemoveCombatantByCharacter(List<ICharacterController> list, Character character)
    {
        if (character == null || list == null) return false;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].controlledCharacter == character)
            {
                list.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    #endregion

    #endregion

    #region Battle End

    private void OnCharacterDied(Character deadCharacter)
    {
        if (battleState == BattleState.BattleEnd) return;

        if (deadCharacter.faction == CharacterFaction.Enemy)
        {
            ICharacterController controller = FindControllerInList(enemyTeam, deadCharacter);
            if (controller is NPCUnitController npcController && npcController.unitData != null)
            {
                int goldDrop = UnityEngine.Random.Range(npcController.unitData.minDropGoldAmount, npcController.unitData.maxDropGoldAmount + 1);
                accumulativeGoldReward += goldDrop;
                Debug.Log($"[BattleManager] 적 '{deadCharacter.name}' 처치 전리품 골드 {goldDrop} 누적 (현재 총 누적: {accumulativeGoldReward})");
            }
        }

        RemoveCombatantByCharacter(playerTeam, deadCharacter);
        RemoveCombatantByCharacter(enemyTeam, deadCharacter);

        deadCharacter.OnCharacterDied -= OnCharacterDied;
        UnsubscribeCombatantIntentEvents(deadCharacter);

        if (deadCharacter != null && deadCharacter.faction == CharacterFaction.Enemy)
        {
            if (deadCharacter.characterMove != null)
            {
                deadCharacter.characterMove.StopMoving();
            }
            deadCharacter.NotifyLeftBattle();
            deadCharacter.gameObject.SetActive(false);
        }

        // 사망한 캐릭터가 현재 턴 소유자일 경우 소프트락 방지를 위해 턴을 강제 종료 처리
        if (currentTurnController != null && currentTurnController.controlledCharacter == deadCharacter)
        {
            EndTurn();
        }

        if (enemyTeam.Count == 0)
            WinBattle();
        else if (playerTeam.Count == 0)
            LoseBattle();
        else
        {
            RefreshAllEnemyIntents();
            OnTimelineDirty?.Invoke();
        }
    }

    public void WinBattle()
    {
        if (battleState == BattleState.BattleEnd) return;
        battleState = BattleState.BattleEnd;

        if (ActionQueueManager.Instance != null)
        {
            ActionQueueManager.Instance.ClearQueue();
        }

        CleanupCombatants();

        // 상위 시스템에 전투 승리 알림 (RunManager가 맵 상태 복귀 및 보상 상자 스폰 처리)
        OnBattleWon?.Invoke();
        OnBattleEnded?.Invoke(BattleResultType.Victory);
    }

    public void LoseBattle()
    {
        if (battleState == BattleState.BattleEnd) return;
        battleState = BattleState.BattleEnd;

        if (ActionQueueManager.Instance != null)
        {
            ActionQueueManager.Instance.ClearQueue();
        }

        CleanupCombatants();

        // 상위 시스템에 전투 패배 알림
        OnBattleLost?.Invoke();
        OnBattleEnded?.Invoke(BattleResultType.Defeat);
    }

    private void CleanupCombatants()
    {
        if (currentTurnController != null)
        {
            currentTurnController.OnTurnEnd();
            currentTurnController = null;
        }

        foreach (var combatant in playerTeam)
        {
            if (combatant == null || combatant.controlledCharacter == null) continue;

            combatant.controlledCharacter.OnCharacterDied -= OnCharacterDied;
            UnsubscribeCombatantIntentEvents(combatant.controlledCharacter);

            if (combatant.controlledCharacter.eventBus != null)
            {
                CombatEventDispatcher.DispatchBattleEnd(combatant.controlledCharacter.eventBus);
            }
            combatant.controlledCharacter.ResetCharacterForBattle();
            combatant.controlledCharacter.NotifyLeftBattle();
        }
        foreach (var combatant in enemyTeam)
        {
            if (combatant == null || combatant.controlledCharacter == null) continue;

            combatant.controlledCharacter.OnCharacterDied -= OnCharacterDied;
            UnsubscribeCombatantIntentEvents(combatant.controlledCharacter);

            if (combatant.controlledCharacter.eventBus != null)
            {
                CombatEventDispatcher.DispatchBattleEnd(combatant.controlledCharacter.eventBus);
            }

            if (combatant.controlledCharacter.isDead)
            {
                if (combatant.controlledCharacter.characterMove != null)
                {
                    combatant.controlledCharacter.characterMove.StopMoving();
                }
                combatant.controlledCharacter.NotifyLeftBattle();
                combatant.controlledCharacter.gameObject.SetActive(false);
            }
            else
            {
                combatant.controlledCharacter.ResetCharacterForBattle();
                combatant.controlledCharacter.NotifyLeftBattle();
            }
        }

        playerTeam.Clear();
        enemyTeam.Clear();
        allCombatantsCache.Clear();
        if (UIManager.instance != null)
        {
            UIManager.instance.ClearWorldUILayer();
        }
    }

    private void CleanupCombatantsForShutdown()
    {
        currentTurnController = null;

        for (int i = 0; i < playerTeam.Count; i++)
        {
            ICharacterController combatant = playerTeam[i];
            if (combatant != null && combatant.controlledCharacter != null)
            {
                combatant.controlledCharacter.OnCharacterDied -= OnCharacterDied;
                UnsubscribeCombatantIntentEvents(combatant.controlledCharacter);
            }
        }

        for (int i = 0; i < enemyTeam.Count; i++)
        {
            ICharacterController combatant = enemyTeam[i];
            if (combatant != null)
            {
                if (combatant.controlledCharacter != null)
                {
                    combatant.controlledCharacter.OnCharacterDied -= OnCharacterDied;
                    UnsubscribeCombatantIntentEvents(combatant.controlledCharacter);
                }

                if (combatant is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        playerTeam.Clear();
        enemyTeam.Clear();
        allCombatantsCache.Clear();
    }

    /// <summary>
    /// 전투 종료 및 매니저 상태 완전 초기화
    /// </summary>
    public void ClearBattle()
    {
        CleanupCombatants();
        battleState = BattleState.None;
        accumulativeGoldReward = 0;

        // 전투 복제 카드 및 이벤트 리스너 안전 해제
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            GameSceneManager.instance.player.DisposeBattleDeck();
        }
    }

    #endregion
}
