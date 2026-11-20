using System;
using System.Collections.Generic;
using CardTypes;
using EventStructs;
using UnityEngine;

public class NPCUnitController : ICharacterController, IDisposable, ILuaOwnedResource
{
    public Character controlledCharacter { get; private set; }

    private NPCUnitData _unitData;
    public NPCUnitData unitData => _unitData;
    private string _nextPlannedCardId; // 다음 턴에 실행 예정인 고정 카드 ID (의도 노출용)
    private bool _hasLockedCard;
    private bool _isDisposed;

    private EnemyIntentInfo _currentIntent;
    public EnemyIntentInfo CurrentIntent => _currentIntent;
    public event Action<EnemyIntentInfo> OnIntentChanged;

    private readonly Func<string, CardData> _cardLookup;
    private XLua.LuaTable _luaTable;
    private bool _ownsLuaTable;
    private readonly Dictionary<string, Card> _cardPool = new Dictionary<string, Card>();

    public NPCUnitController(Character character, NPCUnitData unitData, Func<string, CardData> cardLookup = null)
    {
        controlledCharacter = character;
        _unitData = unitData;
        _cardLookup = cardLookup;
        LuaManager.RegisterResource(this);

        // 루아 테이블 복제 및 바인딩
        if (_unitData != null && _unitData.luaPrototype != null)
        {
            if (LuaManager.Instance != null)
            {
                _luaTable = LuaManager.Instance.NewInstance(_unitData.luaPrototype);
                _ownsLuaTable = true;
            }
            else
            {
                // EditMode 단위 테스트 등 LuaManager.Instance가 없는 환경에서도 프로토타입으로 동작 지원
                _luaTable = _unitData.luaPrototype;
                _ownsLuaTable = false;
            }

            if (_luaTable != null)
            {
                try
                {
                    var luaOnInit = _luaTable.Get<Action<XLua.LuaTable, Character, NPCUnitController>>("OnInit");
                    luaOnInit?.Invoke(_luaTable, controlledCharacter, this);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NPCUnitController] 루아 OnInit 실행 오류: {e.Message}");
                }
            }
        }

        if (controlledCharacter != null)
        {
            controlledCharacter.OnCharacterDied += HandleCharacterDied;
        }

        EvaluateNextIntent();
    }

    /// <summary>
    /// 카드 풀에서 카드를 가져오거나 최초 1회 생성하여 캐싱합니다.
    /// 몬스터 사망 시 Dispose()에서 일괄 해제됩니다.
    /// </summary>
    private Card GetOrLoadCard(string cardId)
    {
        if (_isDisposed || string.IsNullOrEmpty(cardId)) return null;

        if (_cardPool.TryGetValue(cardId, out Card cachedCard))
        {
            return cachedCard;
        }

        CardData cardData = null;
        if (_cardLookup != null)
        {
            cardData = _cardLookup.Invoke(cardId);
        }

        if (cardData == null && ModLoader.Instance != null && ModLoader.Instance.CardDatabase != null)
        {
            ModLoader.Instance.CardDatabase.TryGetValue(cardId, out cardData);
        }

        if (cardData != null)
        {
            XLua.LuaTable luaInstance = null;
            if (cardData.luaPrototype != null)
            {
                if (LuaManager.Instance != null)
                {
                    luaInstance = LuaManager.Instance.NewInstance(cardData.luaPrototype);
                }
                else
                {
                    luaInstance = cardData.luaPrototype;
                }
            }

            int runtimeId = -100 - _cardPool.Count;
            Card newCard = new Card(cardData, controlledCharacter, luaInstance, runtimeId);
            _cardPool[cardId] = newCard;
            return newCard;
        }

        return null;
    }

    public void OnTurnStart()
    {
        if (_isDisposed || controlledCharacter == null || controlledCharacter.isDead)
        {
            return;
        }

        // 1. 이벤트 버스 트리거
        if (controlledCharacter.eventBus != null)
        {
            CombatEventDispatcher.DispatchTurnStart(controlledCharacter.eventBus);
        }

        // 기절(Stun) 등 턴 시작 이벤트로 턴이 즉시 종료되었다면 행동을 진행하지 않고 중단
        if (RunManager.instance != null && RunManager.instance.battleManager != null
            && !ReferenceEquals(RunManager.instance.battleManager.currentTurnController, this))
        {
            return;
        }

        // 2. 사전 고정된 의도가 없을 때만 새로 평가하고, 이미 고정되어 있으면 현재 타일/스태미나 기준 도달 여부만 재계산
        if (!_hasLockedCard || string.IsNullOrEmpty(_nextPlannedCardId))
        {
            EvaluateNextIntent();
        }
        else
        {
            RefreshIntentState(isTurnStart: true);
        }

        // 3. 결정된 카드가 있을 시, 실시간으로 타겟 위치에 기반해 이동과 공격을 큐에 계획
        if (!string.IsNullOrEmpty(_nextPlannedCardId))
        {
            ExecutePlannedAction(_nextPlannedCardId);
        }
        else
        {
            // 폴백 행동: 턴 종료
            EndNPCTurn();
        }
    }

    private void ExecutePlannedAction(string cardId)
    {
        Card plannedCard = GetOrLoadCard(cardId);
        if (plannedCard == null || plannedCard.cardData == null)
        {
            EndNPCTurn();
            return;
        }

        Character hostileTarget = FindClosestHostile();
        GameMap gameMap = ResolveGameMap();
        Tile currentTile = (controlledCharacter != null && controlledCharacter.characterMove != null)
            ? controlledCharacter.characterMove.GetCurrentTile()
            : null;

        bool requiresHostile = RequiresHostileTarget(plannedCard.cardData.parsedTargetType);
        Tile moveTarget = null;

        // [실시간 사거리 및 이동 계획 계산]
        if (requiresHostile && hostileTarget != null && gameMap != null && currentTile != null)
        {
            Tile optimalDest = AIPositionPlanner.PlanOptimalPosition(
                controlledCharacter,
                hostileTarget,
                plannedCard,
                gameMap);

            if (optimalDest != null)
            {
                moveTarget = AIPositionPlanner.GetClampedMoveTarget(
                    controlledCharacter,
                    optimalDest,
                    gameMap,
                    controlledCharacter.curMoveRange);
            }
        }

        bool shouldMove = (moveTarget != null && moveTarget != currentTile);
        bool canPlayCard = plannedCard.CanPlay(controlledCharacter);
        bool canUseCard = canPlayCard;

        // 이동 후 도착 예정 타일 기준 타깃 사거리 만족 여부 최종 검증 (MoveOnly 예고와 실행 100% 일치 보장)
        if (canUseCard && requiresHostile)
        {
            if (hostileTarget == null)
            {
                canUseCard = false;
            }
            else
            {
                Tile finalTile = (moveTarget != null) ? moveTarget : currentTile;
                Tile hostileTile = (hostileTarget.characterMove != null) ? hostileTarget.characterMove.GetCurrentTile() : null;
                if (finalTile == null || hostileTile == null)
                {
                    canUseCard = false;
                }
                else
                {
                    int dist = Tile.GetDistance(finalTile, hostileTile);
                    canUseCard = (dist >= plannedCard.cardData.targetMinDistance && dist <= plannedCard.cardData.targetMaxDistance);
                }
            }
        }

        if (!canUseCard)
        {
            if (shouldMove)
            {
                Debug.Log($"[NPCAI] '{controlledCharacter.name}'이 카드 '{cardId}' 사거리/조건 미달로 이동만 수행합니다 ({moveTarget.GetCoordToString()}).");
                ActionQueueManager.Instance.EnqueueAction(new EnemyMoveAction(controlledCharacter, moveTarget), forceEnqueue: true);
                ActionQueueManager.Instance.EnqueueAction(new CustomCallbackAction(
                    controlledCharacter,
                    callback: () => EndNPCTurn(),
                    onCanceled: () => EndNPCTurn()
                ), forceEnqueue: true);
            }
            else
            {
                Debug.Log($"[NPCAI] '{controlledCharacter.name}'이 카드 '{cardId}'의 사용 조건 또는 사거리를 만족하지 못해 행동 없이 턴을 종료합니다.");
                EndNPCTurn();
            }
            return;
        }

        // 1. 이동 액션 적재 (필요 시)
        if (shouldMove)
        {
            Debug.Log($"[NPCAI] '{controlledCharacter.name}'이 타겟 '{hostileTarget.name}'에게 카드 '{cardId}'를 쓰기 위해 타일 {moveTarget.GetCoordToString()}로 이동합니다.");
            ActionQueueManager.Instance.EnqueueAction(new EnemyMoveAction(controlledCharacter, moveTarget), forceEnqueue: true);
        }

        List<Character> targets = new List<Character>();
        if (hostileTarget != null)
        {
            targets.Add(hostileTarget);
        }

        Vector2Int targetPos = Vector2Int.zero;
        if (hostileTarget != null && hostileTarget.characterMove != null && hostileTarget.characterMove.GetCurrentTile() != null)
        {
            targetPos = hostileTarget.characterMove.GetCurrentTile().GetCoord();
        }
        else if (currentTile != null)
        {
            targetPos = currentTile.GetCoord();
        }

        // 2. 카드 시전 액션을 큐에 적재 (AI 전용 강제 적재)
        CardPlayAction playAction = new CardPlayAction(controlledCharacter, plannedCard, targets, targetPos);
        ActionQueueManager.Instance.EnqueueAction(playAction, forceEnqueue: true);

        // 3. 액션 완료 후 턴을 마치는 콜백 액션을 큐에 추가
        ActionQueueManager.Instance.EnqueueAction(new CustomCallbackAction(
            controlledCharacter,
            callback: () => EndNPCTurn(),
            onCanceled: () => EndNPCTurn()
        ), forceEnqueue: true);
    }

    private void EndNPCTurn()
    {
        bool hasBattleEnded = false;

        // 턴 종료 처리
        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            RunManager.instance.battleManager.EndTurn(this);
            BattleState state = RunManager.instance.battleManager.battleState;
            hasBattleEnded = (state == BattleState.BattleEnd || state == BattleState.None);
        }

        // 턴 종료 후 다음 턴의 의도 새로 평가·고정 (플레이어 UI 예고용)
        if (!_isDisposed && !hasBattleEnded && controlledCharacter != null && !controlledCharacter.isDead)
        {
            EvaluateNextIntent();
        }
    }

    public void OnTurnEnd()
    {
        if (controlledCharacter != null && controlledCharacter.eventBus != null)
        {
            CombatEventDispatcher.DispatchTurnEnd(controlledCharacter.eventBus);
        }
    }

    private void HandleCharacterDied(Character deadChar)
    {
        Dispose();
    }

    public void ReleaseLuaResources()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _nextPlannedCardId = null;
        _hasLockedCard = false;

        if (controlledCharacter != null)
        {
            controlledCharacter.OnCharacterDied -= HandleCharacterDied;
        }

        // 캐싱된 모든 카드의 Lua 바인딩 및 이벤트 버스 구독 해제
        foreach (var card in _cardPool.Values)
        {
            card?.Dispose();
        }
        _cardPool.Clear();

        if (_luaTable != null)
        {
            if (_ownsLuaTable && LuaManager.IsLuaEnvAlive)
            {
                _luaTable.Dispose();
            }
            _luaTable = null;
        }

        _currentIntent = default;
        OnIntentChanged = null;
    }

    /// <summary>
    /// 루아 스크립트 결정을 기반으로 다음 턴에 실행할 카드를 평가·고정하고 의도 스냅샷을 갱신합니다.
    /// </summary>
    public void EvaluateNextIntent()
    {
        _nextPlannedCardId = null;
        _hasLockedCard = false;

        if (_isDisposed)
        {
            _currentIntent = default;
            return;
        }

        if (controlledCharacter == null || controlledCharacter.isDead)
        {
            RefreshIntentState(isTurnStart: false);
            return;
        }

        // 1. 루아 AI 스크립트 의사결정 함수 호출 시도
        if (_luaTable != null && !LuaManager.IsShuttingDown && !LuaManager.IsDisposing)
        {
            // Evaluate 함수 (Table 반환) 호출 검사
            var luaEvaluate = _luaTable.Get<Func<XLua.LuaTable, Character, XLua.LuaTable>>("Evaluate");
            if (luaEvaluate != null)
            {
                try
                {
                    XLua.LuaTable decision = luaEvaluate.Invoke(_luaTable, controlledCharacter);
                    if (decision != null)
                    {
                        string cardId = decision.Get<string>("cardId");
                        decision.Dispose();
                        if (!string.IsNullOrEmpty(cardId))
                        {
                            _nextPlannedCardId = cardId;
                            _hasLockedCard = true;
                            RefreshIntentState(isTurnStart: false);
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NPCUnitController] Lua Evaluate 실행 오류: {e.Message}");
                }
            }

            // ChooseCard 함수 (String 반환) 호출 검사
            var luaChooseCard = _luaTable.Get<Func<XLua.LuaTable, Character, string>>("ChooseCard");
            if (luaChooseCard != null)
            {
                try
                {
                    string cardId = luaChooseCard.Invoke(_luaTable, controlledCharacter);
                    if (!string.IsNullOrEmpty(cardId))
                    {
                        _nextPlannedCardId = cardId;
                        _hasLockedCard = true;
                        RefreshIntentState(isTurnStart: false);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NPCUnitController] Lua ChooseCard 실행 오류: {e.Message}");
                }
            }
        }

        RefreshIntentState(isTurnStart: false);
    }

    /// <summary>
    /// 고정된 카드 의도(_nextPlannedCardId)는 유지한 채, 현재 위치·사거리·버프/디버프 상태를 반영하여
    /// 예고 아이콘 분류와 예상 수치를 실시간으로 재계산합니다.
    /// </summary>
    public void RefreshIntentState(bool isTurnStart = false)
    {
        if (_isDisposed)
        {
            _currentIntent = default;
            return;
        }

        if (controlledCharacter == null || controlledCharacter.isDead)
        {
            _currentIntent = default;
            UpdateIntentHUD();
            return;
        }

        // 1. 기절 상태 확인 (고정된 _nextPlannedCardId는 유지하여 해제 시 기존 의도로 복원)
        if (controlledCharacter.effectManager != null && controlledCharacter.effectManager.HasEffect("Stun"))
        {
            _currentIntent = new EnemyIntentInfo
            {
                intentType = EnemyIntentType.Stunned,
                cardId = _nextPlannedCardId,
                plannedCard = GetOrLoadCard(_nextPlannedCardId),
                targetCharacter = null,
                plannedMoveTile = null,
                requiresMove = false,
                canReachTarget = false,
                estimatedValue = 0,
                isValid = true
            };
            UpdateIntentHUD();
            return;
        }

        // 2. 예정 카드 조회
        Card plannedCard = GetOrLoadCard(_nextPlannedCardId);
        if (plannedCard == null || plannedCard.cardData == null)
        {
            _currentIntent = new EnemyIntentInfo
            {
                intentType = EnemyIntentType.None,
                cardId = _nextPlannedCardId,
                plannedCard = null,
                targetCharacter = null,
                plannedMoveTile = null,
                requiresMove = false,
                canReachTarget = false,
                estimatedValue = 0,
                isValid = false
            };
            UpdateIntentHUD();
            return;
        }

        CardData cardData = plannedCard.cardData;
        float expectedStamina = isTurnStart
            ? controlledCharacter.curStamina
            : (controlledCharacter.curCharacterStat != null ? controlledCharacter.curCharacterStat.maxStamina : controlledCharacter.curStamina);

        int moveBudget = isTurnStart
            ? controlledCharacter.curMoveRange
            : (controlledCharacter.curCharacterStat != null ? controlledCharacter.curCharacterStat.moveRange : controlledCharacter.curMoveRange);

        Character hostileTarget = FindClosestHostile();
        GameMap gameMap = ResolveGameMap();
        Tile currentTile = (controlledCharacter.characterMove != null) ? controlledCharacter.characterMove.GetCurrentTile() : null;
        bool requiresHostile = RequiresHostileTarget(cardData.parsedTargetType);

        Tile moveTarget = null;
        if (requiresHostile && hostileTarget != null && gameMap != null && currentTile != null)
        {
            Tile optimalDest = AIPositionPlanner.PlanOptimalPosition(controlledCharacter, hostileTarget, plannedCard, gameMap);
            if (optimalDest != null)
            {
                moveTarget = AIPositionPlanner.GetClampedMoveTarget(controlledCharacter, optimalDest, gameMap, moveBudget);
            }
        }

        bool requiresMove = (moveTarget != null && moveTarget != currentTile);

        // 3. 스태미나 및 무장해제(Disarm) 등 시전 가능 조건 평가
        if (!plannedCard.CanPlayWithStamina(controlledCharacter, expectedStamina))
        {
            _currentIntent = new EnemyIntentInfo
            {
                intentType = requiresMove ? EnemyIntentType.MoveOnly : EnemyIntentType.Stunned,
                cardId = _nextPlannedCardId,
                plannedCard = plannedCard,
                targetCharacter = requiresMove ? hostileTarget : null,
                plannedMoveTile = requiresMove ? moveTarget : null,
                requiresMove = requiresMove,
                canReachTarget = false,
                estimatedValue = 0,
                isValid = true
            };
            UpdateIntentHUD();
            return;
        }

        // 4. 이동 후 도착 예정 타일 기준 타깃 사거리 도달 가능 여부 판정
        bool canReachTarget = true;
        if (requiresHostile)
        {
            if (hostileTarget == null)
            {
                canReachTarget = false;
            }
            else
            {
                Tile finalTile = (moveTarget != null) ? moveTarget : currentTile;
                Tile hostileTile = (hostileTarget.characterMove != null) ? hostileTarget.characterMove.GetCurrentTile() : null;
                if (finalTile == null || hostileTile == null)
                {
                    canReachTarget = false;
                }
                else
                {
                    int dist = Tile.GetDistance(finalTile, hostileTile);
                    canReachTarget = (dist >= cardData.targetMinDistance && dist <= cardData.targetMaxDistance);
                }
            }
        }

        if (!canReachTarget)
        {
            EnemyIntentType resolvedFallback = requiresMove
                ? EnemyIntentType.MoveOnly
                : (hostileTarget == null ? EnemyIntentType.None : EnemyIntentType.Stunned);

            _currentIntent = new EnemyIntentInfo
            {
                intentType = resolvedFallback,
                cardId = _nextPlannedCardId,
                plannedCard = plannedCard,
                targetCharacter = hostileTarget,
                plannedMoveTile = requiresMove ? moveTarget : null,
                requiresMove = requiresMove,
                canReachTarget = false,
                estimatedValue = 0,
                isValid = (resolvedFallback != EnemyIntentType.None)
            };
            UpdateIntentHUD();
            return;
        }

        // 5. 도달 가능 시 카드 성격 및 실시간 버프/디버프 반영 예상 수치 계산
        bool hasDamage = cardData.baseValues != null && cardData.baseValues.TryGetValue("damage", out float dmgVal) && dmgVal > 0f;
        bool isAttack = hasDamage || cardData.cardType == "Attack";
        bool hasShield = cardData.baseValues != null && cardData.baseValues.TryGetValue("shield", out float shieldVal) && shieldVal > 0f;

        int estimatedValue = 0;
        if (isAttack)
        {
            DamageInfo simInfo = new DamageInfo(controlledCharacter, hostileTarget, plannedCard.GetEffectiveValue("damage"), DamageFlag.Normal);
            if (controlledCharacter.eventBus != null)
            {
                CombatEventDispatcher.DispatchBeforeDealDamage(controlledCharacter.eventBus, ref simInfo);
            }
            if (hostileTarget != null && hostileTarget.eventBus != null)
            {
                CombatEventDispatcher.DispatchBeforeTakeDamage(hostileTarget.eventBus, ref simInfo);
            }
            estimatedValue = Mathf.Max(0, Mathf.RoundToInt(simInfo.baseDamageAmount * simInfo.damageMultiplier));
        }
        else if (hasShield)
        {
            estimatedValue = Mathf.Max(0, Mathf.RoundToInt(plannedCard.GetEffectiveValue("shield")));
        }

        EnemyIntentType resolvedType;
        if (TryParseIntentTypeOverride(cardData.intentType, out EnemyIntentType overrideType))
        {
            resolvedType = overrideType;
        }
        else if (isAttack)
        {
            resolvedType = requiresMove ? EnemyIntentType.AttackMove : EnemyIntentType.Attack;
        }
        else if (hasShield)
        {
            resolvedType = EnemyIntentType.Defend;
        }
        else if (cardData.parsedTargetType == TargetType.Self ||
                 cardData.parsedTargetType == TargetType.SingleAlly ||
                 cardData.parsedTargetType == TargetType.AllAllies)
        {
            resolvedType = EnemyIntentType.Buff;
        }
        else if (requiresHostile || cardData.parsedTargetType == TargetType.AllEnemies)
        {
            resolvedType = EnemyIntentType.Debuff;
        }
        else
        {
            resolvedType = EnemyIntentType.Special;
        }

        _currentIntent = new EnemyIntentInfo
        {
            intentType = resolvedType,
            cardId = _nextPlannedCardId,
            plannedCard = plannedCard,
            targetCharacter = hostileTarget,
            plannedMoveTile = requiresMove ? moveTarget : null,
            requiresMove = requiresMove,
            canReachTarget = true,
            estimatedValue = estimatedValue,
            isValid = true
        };

        UpdateIntentHUD();
    }

    private static bool RequiresHostileTarget(TargetType targetType)
    {
        return targetType == TargetType.SingleEnemy
            || targetType == TargetType.MultiEnemy
            || targetType == TargetType.Directional
            || targetType == TargetType.RandomEnemy
            || targetType == TargetType.AreaGround;
    }

    private static bool TryParseIntentTypeOverride(string intentTypeStr, out EnemyIntentType intentType)
    {
        intentType = EnemyIntentType.None;
        if (string.IsNullOrWhiteSpace(intentTypeStr)) return false;

        if (Enum.TryParse(intentTypeStr.Trim(), true, out EnemyIntentType parsed) && parsed != EnemyIntentType.None)
        {
            intentType = parsed;
            return true;
        }

        return false;
    }

    private GameMap ResolveGameMap()
    {
        if (RunManager.instance != null && RunManager.instance.currentStage != null && RunManager.instance.currentStage.currentGameMap != null)
        {
            return RunManager.instance.currentStage.currentGameMap;
        }

        if (GameMap.current != null)
        {
            return GameMap.current;
        }

        Tile currentTile = (controlledCharacter != null && controlledCharacter.characterMove != null)
            ? controlledCharacter.characterMove.GetCurrentTile()
            : null;
        return currentTile != null ? currentTile.ownerMap : null;
    }

    private Character FindClosestHostile()
    {
        if (controlledCharacter == null) return null;

        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            IReadOnlyList<ICharacterController> all = RunManager.instance.battleManager.GetAllCombatants();
            Character closestHostile = null;
            int minDistance = int.MaxValue;

            foreach (var controller in all)
            {
                if (controller != null && controller.controlledCharacter != null)
                {
                    Character target = controller.controlledCharacter;
                    if (controlledCharacter.IsHostileTo(target))
                    {
                        int dist = GetDistance(controlledCharacter, target);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            closestHostile = target;
                        }
                    }
                }
            }
            return closestHostile;
        }

        // RunManager가 없는 단위 테스트 환경 폴백: GameMap 점유 캐릭터에서 적대 대상 탐색
        GameMap gameMap = ResolveGameMap();
        if (gameMap != null)
        {
            List<List<Tile>> tileMap = gameMap.GetTileMap();
            if (tileMap != null)
            {
                Character closestHostile = null;
                int minDistance = int.MaxValue;
                for (int c = 0; c < tileMap.Count; c++)
                {
                    List<Tile> col = tileMap[c];
                    if (col == null) continue;
                    for (int r = 0; r < col.Count; r++)
                    {
                        Tile tile = col[r];
                        if (tile == null || tile.occupyingCharacter == null) continue;
                        Character candidate = tile.occupyingCharacter;
                        if (controlledCharacter.IsHostileTo(candidate))
                        {
                            int dist = GetDistance(controlledCharacter, candidate);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                closestHostile = candidate;
                            }
                        }
                    }
                }
                return closestHostile;
            }
        }

        return null;
    }

    private int GetDistance(Character c1, Character c2)
    {
        if (c1 == null || c2 == null || c1.characterMove == null || c2.characterMove == null) return int.MaxValue;
        return Tile.GetDistance(c1.characterMove.GetCurrentTile(), c2.characterMove.GetCurrentTile());
    }

    private void UpdateIntentHUD()
    {
        // HUD 및 타임라인 등 구독자 동기화 (World -> UI 역참조 없이 Character 모델 이벤트로 전달)
        if (controlledCharacter != null)
        {
            controlledCharacter.UpdateIntent(in _currentIntent);
        }
        OnIntentChanged?.Invoke(_currentIntent);
    }
}
