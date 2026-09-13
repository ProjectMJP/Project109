using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EventStructs;

public class NPCUnitController : ICharacterController
{
    public Character controlledCharacter { get; private set; }
    
    private NPCUnitData _unitData;
    public NPCUnitData unitData => _unitData;
    private string _nextPlannedCardId; // 다음 턴에 실행 예정인 카드 ID (의도 노출용)

    private XLua.LuaTable _luaTable;

    public NPCUnitController(Character character, NPCUnitData unitData)
    {
        controlledCharacter = character;
        _unitData = unitData;

        // 루아 테이블 복제 및 바인딩
        if (_unitData != null && _unitData.luaPrototype != null)
        {
            try
            {
                var newInstanceFunc = LuaManager.Instance?.luaEnv.Global.Get<Func<XLua.LuaTable, XLua.LuaTable>>("NewInstance");
                if (newInstanceFunc != null)
                {
                    _luaTable = newInstanceFunc.Invoke(_unitData.luaPrototype);
                    var luaOnInit = _luaTable?.Get<Action<XLua.LuaTable, Character, NPCUnitController>>("OnInit");
                    luaOnInit?.Invoke(_luaTable, controlledCharacter, this);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCUnitController] 루아 이펙트 초기화 오류: {e.Message}");
            }
        }

        if (controlledCharacter != null)
        {
            controlledCharacter.OnCharacterDied += HandleCharacterDied;
        }

        EvaluateNextIntent();
    }

    public void OnTurnStart()
    {
        // 1. 이벤트 버스 트리거
        controlledCharacter.eventBus.Invoke<IOnTurnStart>(a => a.OnTurnStart());

        // 2. 의도 최종 확정
        EvaluateNextIntent();

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
        if (ModLoader.Instance.CardDatabase.TryGetValue(cardId, out CardData cardData))
        {
            // 몬스터는 덱을 쓰지 않으므로 실시간으로 카드를 생성하여 시전
            Card tempCard = new Card(cardData, controlledCharacter, cardData.luaPrototype, -99);

            // [최소 조건 검증] 스태미나 소비 및 루아 CanPlay 평가 만족 시에만 카드 사용 가능
            if (!tempCard.CanPlay(controlledCharacter))
            {
                Debug.Log($"[NPCAI] '{controlledCharacter.name}'이 카드 '{cardId}'의 사용 조건을 만족하지 못해(Stamina 등) 행동 없이 턴을 종료합니다.");
                EndNPCTurn();
                return;
            }
            
            // 실시간 최적 타겟 검색 (IsHostileTo 판단 기준)
            Character hostileTarget = FindClosestHostile();
            List<Character> targets = new List<Character>();
            if (hostileTarget != null)
            {
                targets.Add(hostileTarget);
            }

            // [실시간 사거리 및 이동 계획 계산]
            var currentMap = RunManager.instance?.currentMap;
            if (hostileTarget != null && currentMap != null && currentMap.currentGameMap != null)
            {
                // 카드의 사거리를 만족할 수 있는 최적의 목적지 타일 계산
                Tile optimalDest = AIPositionPlanner.PlanOptimalPosition(
                    controlledCharacter, 
                    hostileTarget, 
                    tempCard, 
                    currentMap.currentGameMap
                );

                if (optimalDest != null)
                {
                    // 자신의 이동력 사정 한도로 이동 타일을 Clamp
                    Tile moveTarget = AIPositionPlanner.GetClampedMoveTarget(
                        controlledCharacter, 
                        optimalDest, 
                        currentMap.currentGameMap
                    );

                    Tile currentTile = controlledCharacter.characterMove?.GetCurrentTile();
                    // 실제로 움직여야 하는 경우, 이동 액션을 큐에 먼저 적재
                    if (moveTarget != null && moveTarget != currentTile)
                    {
                        Debug.Log($"[NPCAI] '{controlledCharacter.name}'이 타겟 '{hostileTarget.name}'에게 카드 '{cardId}'를 쓰기 위해 타일 {moveTarget.GetCoordToString()}로 이동합니다.");
                        ActionQueueManager.Instance.EnqueueAction(new EnemyMoveAction(controlledCharacter, moveTarget));
                    }
                }
            }

            Vector2Int targetPos = Vector2Int.zero;
            if (hostileTarget != null && hostileTarget.characterMove != null && hostileTarget.characterMove.GetCurrentTile() != null)
            {
                targetPos = hostileTarget.characterMove.GetCurrentTile().GetCoord();
            }

            // 2. 카드 시전 액션을 큐에 적재 (선입력 잠금으로 순차 실행됨)
            CardPlayAction playAction = new CardPlayAction(controlledCharacter, tempCard, targets, targetPos);
            ActionQueueManager.Instance.EnqueueAction(playAction);
            
            // 3. 액션 완료 후 턴을 마치는 콜백 액션을 큐에 추가
            ActionQueueManager.Instance.EnqueueAction(new CustomCallbackAction(controlledCharacter, () => {
                EndNPCTurn();
            }));
        }
        else
        {
            EndNPCTurn();
        }
    }

    private void EndNPCTurn()
    {
        // 턴 종료 처리
        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            RunManager.instance.battleManager.EndTurn();
        }
        
        // 턴 종료 후 다음 턴의 의도 미리 갱신 (플레이어 UI 예고용)
        EvaluateNextIntent();
    }

    public void OnTurnEnd()
    {
        controlledCharacter.eventBus.Invoke<IOnTurnEnd>(a => a.OnTurnEnd());
    }

    private void HandleCharacterDied(Character deadChar)
    {
        if (controlledCharacter != null)
        {
            controlledCharacter.OnCharacterDied -= HandleCharacterDied;
        }
        _luaTable?.Dispose();
        _luaTable = null;
    }

    /// <summary>
    /// 루아 스크립트 결정을 기반으로 우선순위가 가장 높은 의도를 예고합니다.
    /// </summary>
    public void EvaluateNextIntent()
    {
        _nextPlannedCardId = null;

        // 1. 루아 AI 스크립트 의사결정 함수 호출 시도
        if (_luaTable != null)
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
                        if (!string.IsNullOrEmpty(cardId))
                        {
                            _nextPlannedCardId = cardId;
                            decision.Dispose();
                            UpdateIntentHUD();
                            return;
                        }
                        decision.Dispose();
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
                        UpdateIntentHUD();
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NPCUnitController] Lua ChooseCard 실행 오류: {e.Message}");
                }
            }
        }

        UpdateIntentHUD();
    }

    private Character FindClosestHostile()
    {
        if (RunManager.instance == null || RunManager.instance.battleManager == null) return null;
        
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

    private int GetDistance(Character c1, Character c2)
    {
        if (c1 == null || c2 == null || c1.characterMove == null || c2.characterMove == null) return int.MaxValue;
        var tile1 = c1.characterMove.GetCurrentTile();
        var tile2 = c2.characterMove.GetCurrentTile();
        if (tile1 == null || tile2 == null) return int.MaxValue;
        
        var coord1 = tile1.GetCoord();
        var coord2 = tile2.GetCoord();
        return Mathf.Abs(coord1.x - coord2.x) + Mathf.Abs(coord1.y - coord2.y);
    }

    private void UpdateIntentHUD()
    {
        // HUD 머리 위 의도 UI 컴포넌트 갱신 연동부 (Slay the Spire 방식의 종류 및 강도만 예고)
    }
}
