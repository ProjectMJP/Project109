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
    public void Initialize()
    {
        // 초기화 필요한 멤버 변수가 있다면 여기서 처리
    }

    public void Dispose()
    {
        ClearBattle();
        OnBattleWon = null;
        OnBattleLost = null;
        OnBattleEnded = null;
    }

    #region Battle Events

    /// <summary>전투 승리 시 발행되는 이벤트 (상위 시스템에서 보상 및 맵 상태 복귀 처리)</summary>
    public event Action OnBattleWon;

    /// <summary>전투 패배 시 발행되는 이벤트</summary>
    public event Action OnBattleLost;

    /// <summary>전투 종료 시 결과 타입과 함께 발행되는 이벤트</summary>
    public event Action<BattleResultType> OnBattleEnded;

    #endregion

    #region Battle State

    public BattleState battleState { get; private set; } = BattleState.None;

    private List<ICharacterController> playerTeam = new();
    private List<ICharacterController> enemyTeam = new();
    private readonly List<ICharacterController> allCombatantsCache = new();
    private ICharacterController currentTurnController;
    public float battleTimeScale = 1f;
    public int accumulativeGoldReward { get; set; }

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

        playerTeam = new List<ICharacterController>(players);
        enemyTeam = new List<ICharacterController>(enemies);

        // 스태미나 초기화 + 사망 이벤트 구독 (플레이어 → 적 순서)
        foreach (var combatant in playerTeam)
            InitCombatant(combatant);
        foreach (var combatant in enemyTeam)
            InitCombatant(combatant);

        battleState = BattleState.Intro;

        if (UIManager.instance != null)
        {
            GameObject noticeUI = UIManager.instance.OpenUI("UIBattleNotice", UILayerType.Popup, true);
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
            combatant.controlledCharacter.eventBus.Invoke<IOnBattleStart>(a => a.OnBattleStart());
        foreach (var combatant in enemyTeam)
            combatant.controlledCharacter.eventBus.Invoke<IOnBattleStart>(a => a.OnBattleStart());

        battleState = BattleState.Combat;
        Debug.Log("[BattleManager] Battle start intro finished. Combat began.");
    }

    /// <summary>
    /// 전투 중 아군/적군 추가 (소환, 증원 등)
    /// </summary>
    public void AddCombatant(ICharacterController controller, bool isPlayerTeam)
    {
        if (isPlayerTeam)
            playerTeam.Add(controller);
        else
            enemyTeam.Add(controller);

        InitCombatant(controller);
        controller.controlledCharacter.eventBus.Invoke<IOnBattleStart>(a => a.OnBattleStart());
    }

    private void InitCombatant(ICharacterController combatant)
    {
        combatant.controlledCharacter.ResetCharacterForBattle();
        combatant.controlledCharacter.OnCharacterDied += OnCharacterDied;
        combatant.controlledCharacter.GetComponent<CharacterUIController>()?.ShowUI();
    }

    public void Update(float dt)
    {
        if (battleState != BattleState.Combat) return;

        float scaledDt = dt * battleTimeScale;

        // 플레이어 팀 먼저 틱 (동시 MAX 시 플레이어 우선)
        foreach (var combatant in playerTeam)
            combatant.controlledCharacter.BattleTick(scaledDt);
        foreach (var combatant in enemyTeam)
            combatant.controlledCharacter.BattleTick(scaledDt);
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

        controller.controlledCharacter.ResetMoveStat();

        controller.OnTurnStart();
    }

    /// <summary>
    /// 현재 턴을 종료한다. 플레이어가 턴 종료 버튼을 누르거나, AI가 행동을 마쳤을 때 호출.
    /// </summary>
    public void EndTurn()
    {
        if (currentTurnController == null) return;

        currentTurnController.OnTurnEnd();

        currentTurnController.controlledCharacter.ResetStamina();
        currentTurnController.controlledCharacter.UpdateShieldDuration(); // 턴 종료 시 실드 지속 시간 업데이트

        currentTurnController = null;
        battleState = BattleState.Combat;
    }

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

        // 사망한 캐릭터가 현재 턴 소유자일 경우 소프트락 방지를 위해 턴을 강제 종료 처리
        if (currentTurnController != null && currentTurnController.controlledCharacter == deadCharacter)
        {
            EndTurn();
        }

        if (enemyTeam.Count == 0)
            WinBattle();
        else if (playerTeam.Count == 0)
            LoseBattle();
    }

    public void WinBattle()
    {
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
            combatant.controlledCharacter.eventBus.Invoke<IOnBattleEnd>(a => a.OnBattleEnd());
            combatant.controlledCharacter.ResetCharacterForBattle();
            combatant.controlledCharacter.OnCharacterDied -= OnCharacterDied;
            combatant.controlledCharacter.GetComponent<CharacterUIController>()?.HideUI();
        }
        foreach (var combatant in enemyTeam)
        {
            combatant.controlledCharacter.eventBus.Invoke<IOnBattleEnd>(a => a.OnBattleEnd());
            combatant.controlledCharacter.ResetCharacterForBattle();
            combatant.controlledCharacter.OnCharacterDied -= OnCharacterDied;
            combatant.controlledCharacter.GetComponent<CharacterUIController>()?.HideUI();
        }

        playerTeam.Clear();
        enemyTeam.Clear();
        allCombatantsCache.Clear();
        if (UIManager.instance != null)
        {
            UIManager.instance.ClearAllCharacterStatusBars();
        }
    }

    /// <summary>
    /// 전투 종료 및 매니저 상태 완전 초기화
    /// </summary>
    public void ClearBattle()
    {
        CleanupCombatants();
        battleState = BattleState.None;
        accumulativeGoldReward = 0;
    }

    #endregion
}
