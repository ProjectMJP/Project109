using System.Collections.Generic;
using UnityEngine;
using EventStructs;
using XLua;

public class IntegrationVerificationTest : MonoBehaviour
{
    private static Player GetPlayer() => GameSceneManager.instance != null ? GameSceneManager.instance.player : null;

    [ContextMenu("Run Core Systems Test")]
    public void RunIntegrationTest()
    {
        Debug.Log("[VerificationTest] Starting core systems integration test...");
        
        // 1. Lobby and loadout selection phase
        Debug.Log("[VerificationTest] Phase 1: Player validation");
        Player player = GetPlayer();
        if (player != null)
        {
            int count = player.deck != null ? player.deck.GetCards().Count : 0;
            Debug.Log($"[VerificationTest] Loaded deck size: {count}");
        }
        else
        {
            Debug.LogWarning("[VerificationTest] GameSceneManager.instance.player is null in this context.");
        }

        // 2. Action Queue and Card Play
        Debug.Log("[VerificationTest] Phase 2: Action Queue execution");
        if (player != null)
        {
            Character playerChar = player.character;
            
            // Dummy card data for instantiation
            CardData dummyCardData = new CardData { cardName = "VerificationCard", stamina = 0 };
            Card tempCard = new Card(dummyCardData, playerChar, null, 123);
            
            CardPlayAction playAction = new CardPlayAction(playerChar, tempCard, new List<Character>(), Vector2Int.zero);
            ActionQueueManager.Instance.EnqueueAction(playAction);
            Debug.Log($"[VerificationTest] Enqueued action. Queue is busy: {ActionQueueManager.Instance.IsBusy}");
        }

        // 3. Damage floater triggers
        Debug.Log("[VerificationTest] Phase 3: Damage Floater triggering");
        if (player != null)
        {
            Character playerChar = player.character;
            DamageInfo info = new DamageInfo(playerChar, playerChar, 15f, DamageFlag.Normal);
            playerChar.TakeDamage(info);
            
            HealInfo heal = new HealInfo(playerChar, playerChar, 5f, HealFlag.Normal);
            playerChar.TakeHeal(heal);
        }

        // 4. Effect / Buff UI Refresh
        Debug.Log("[VerificationTest] Phase 4: Effect / Buff UI Refresh testing");
        if (player != null)
        {
            Character playerChar = player.character;
            EffectData testBuffData = new EffectData { effectName = "TestBuff", isPermanent = false, maxStack = 99 };
            Effect testEffect = new Effect(testBuffData, null);
            
            // Add effect
            EffectInfo effInfo = new EffectInfo(playerChar, playerChar, testEffect, 1, 5f);
            playerChar.TakeEffect(effInfo);
            Debug.Log($"[VerificationTest] Added effect: {testEffect.Data.effectName}, Stack: {testEffect.currentStack}");

            // Stack effect
            playerChar.TakeEffect(effInfo);
            Debug.Log($"[VerificationTest] Stacked effect. New Stack: {testEffect.currentStack}");
            
            // Remove effect
            playerChar.effectManager.RemoveEffect(testEffect);
            Debug.Log("[VerificationTest] Removed effect. Check if UI cleared.");
        }

        // 5. Stamina Cost and Block/Consumption
        Debug.Log("[VerificationTest] Phase 5: Stamina Cost check and consumption");
        if (RunManager.instance != null && player != null && RunManager.instance.playerBattleController != null)
        {
            Character playerChar = player.character;
            playerChar.curStamina = 10f; // 현재 스태미나 10

            CardData highCostData = new CardData { cardName = "HighCostCard", stamina = 30 }; // 비용 30
            Card highCostCard = new Card(highCostData, playerChar, null, 999);

            Debug.Log($"[VerificationTest] Trying high cost card. Stamina: {playerChar.curStamina}, Cost: {highCostCard.currentCost}");
            RunManager.instance.playerBattleController.TryUseCard(highCostCard);
            Debug.Log($"[VerificationTest] High cost card queue status: {ActionQueueManager.Instance.IsBusy} (Expected: false, as it should be blocked)");

            // 스태미나 50으로 증가
            playerChar.curStamina = 50f;
            Debug.Log($"[VerificationTest] Trying high cost card again with Stamina: {playerChar.curStamina}");
            RunManager.instance.playerBattleController.TryUseCard(highCostCard);
            Debug.Log($"[VerificationTest] High cost card queue status: {ActionQueueManager.Instance.IsBusy} (Expected: true)");
        }

        // 6. Save & Load 시스템 검증
        Debug.Log("[VerificationTest] Phase 6: Save & Load verification");
        if (RunManager.instance != null && player != null && player.playerStat != null)
        {
            int originalGold = player.playerStat.InGameCurrencyGold;
            player.playerStat.InGameCurrencyGold = 777;
            
            RunManager.instance.SaveGame();
            Debug.Log("[VerificationTest] Game saved with Gold: 777");
            
            player.playerStat.InGameCurrencyGold = 0;
            RunManager.instance.LoadRun();
            
            int loadedGold = player.playerStat.InGameCurrencyGold;
            Debug.Log($"[VerificationTest] Loaded Gold: {loadedGold} (Expected: 777)");
            
            // 원복
            player.playerStat.InGameCurrencyGold = originalGold;
        }

        // 7. Lua 기반 다중 비용 및 시전 조건(CanPlay, SpendCosts) 검증
        Debug.Log("[VerificationTest] Phase 7: Lua-based CanPlay and SpendCosts verification");
        if (LuaManager.Instance != null && player != null)
        {
            Character playerChar = player.character;
            var luaEnv = LuaManager.Instance.luaEnv;
            if (luaEnv != null)
            {
                LuaTable mockLuaTable = luaEnv.NewTable();
                
                // 루아 함수 대리자 정의 (HP 10 소모 등)
                System.Func<LuaTable, Card, Character, bool> canPlayFunc = (tbl, card, caster) => {
                    Debug.Log($"[Lua Mock] CanPlay called.");
                    return caster.curHealth > 10f; // 체력이 10 초과일 때 사용 가능
                };
                System.Action<LuaTable, Card, Character> spendCostsFunc = (tbl, card, caster) => {
                    Debug.Log($"[Lua Mock] SpendCosts called.");
                    caster.curHealth -= 10f; // 체력 10 차감
                };

                mockLuaTable.Set("CanPlay", canPlayFunc);
                mockLuaTable.Set("SpendCosts", spendCostsFunc);

                CardData luaCardData = new CardData { cardName = "LuaCard", stamina = 0 };
                Card luaCard = new Card(luaCardData, playerChar, mockLuaTable, 777);

                playerChar.curHealth = 5f;
                bool canPlay1 = luaCard.CanPlay(playerChar);
                Debug.Log($"[VerificationTest] Lua card CanPlay with 5 HP: {canPlay1} (Expected: false)");

                playerChar.curHealth = 25f;
                bool canPlay2 = luaCard.CanPlay(playerChar);
                Debug.Log($"[VerificationTest] Lua card CanPlay with 25 HP: {canPlay2} (Expected: true)");

                if (canPlay2)
                {
                    luaCard.SpendCosts(playerChar);
                    Debug.Log($"[VerificationTest] After SpendCosts, HP: {playerChar.curHealth} (Expected: 15)");
                }
                
                mockLuaTable.Dispose();
            }
        }

        // 8. Enemy AI Lua-based Decision & Planner verification
        Debug.Log("[VerificationTest] Phase 8: Enemy AI Lua-based Decision & Planner verification");
        if (player != null && LuaManager.Instance != null)
        {
            // 몬스터 더미 캐릭터 생성
            Character enemyChar = new Character();
            CharacterStat enemyStat = new CharacterStat { maxHealth = 100, maxStamina = 100 };
            enemyChar.InitializeStat(enemyStat);
            enemyChar.curHealth = 100;

            // AI 데이터 빌드
            NPCUnitData aiData = new NPCUnitData();
            aiData.unitId = "MockMonster";
            aiData.characterStat = enemyStat;

            // 루아 모의 환경 구축
            var luaEnv = LuaManager.Instance.luaEnv;
            if (luaEnv != null)
            {
                LuaTable mockLuaTable = luaEnv.NewTable();
                
                // 루아 행동 결정 함수 Mocking: 스태미나가 20 초과일 때 "검격" 카드 결정
                System.Func<LuaTable, Character, string> chooseCardFunc = (tbl, caster) => {
                    Debug.Log($"[Lua AI Mock] ChooseCard evaluated.");
                    return caster.curStamina > 20f ? "검격" : null;
                };
                mockLuaTable.Set("ChooseCard", chooseCardFunc);
                aiData.luaPrototype = mockLuaTable;

                // NPC AI Controller 생성
                NPCUnitController enemyController = new NPCUnitController(enemyChar, aiData);
                
                // 처음 스태미나 50 -> "검격" 의사결정이 정상 동작하는지 테스트
                enemyChar.curStamina = 50f;
                enemyController.EvaluateNextIntent();
                
                // AI Planner 모의 호출
                GameMap mockMap = RunManager.instance?.currentMap?.currentGameMap;
                if (mockMap != null)
                {
                    CardData testAttackCardData = new CardData { cardName = "Slash", targetMinDistance = 1, targetMaxDistance = 2 };
                    Card testAttackCard = new Card(testAttackCardData, enemyChar, null, 888);
                    
                    Tile optimal = AIPositionPlanner.PlanOptimalPosition(
                        enemyChar, 
                        player.character, 
                        testAttackCard, 
                        mockMap
                    );
                    Debug.Log($"[VerificationTest] Optimal Tile calculated: {(optimal != null ? optimal.GetCoordToString() : "None")}");
                }

                mockLuaTable.Dispose();
            }
        }

        // 9. ChoiceData & Lua Scripting 검증
        Debug.Log("[VerificationTest] Phase 9: ChoiceData & Lua Scripting validation");
        ChoiceData testChoice = new ChoiceData();
        testChoice.description = "Test Choice";
        testChoice.luaScript = "special_test_choice";
        Debug.Log($"[VerificationTest] Created ChoiceData: '{testChoice.description}' with luaScript: '{testChoice.luaScript}'");

        // 10. Gold Reward Accumulation & Merging 검증
        Debug.Log("[VerificationTest] Phase 10: Gold Reward Accumulation & Merging validation");
        if (RunManager.instance != null && RunManager.instance.battleManager != null)
        {
            var bm = RunManager.instance.battleManager;
            bm.ClearBattle();
            Debug.Log($"[VerificationTest] Initial accumulated gold: {bm.accumulativeGoldReward} (Expected: 0)");

            // 가상 적군 처치 상황 설정
            GameObject dummyGo = new GameObject("DummyEnemy");
            Character dummyEnemy = dummyGo.AddComponent<Character>();
            dummyEnemy.faction = CharacterFaction.Enemy;
            dummyEnemy.InitializeStat(new CharacterStat { maxHealth = 10 });
            dummyEnemy.curHealth = 10;

            NPCUnitData enemyAIData = new NPCUnitData();
            enemyAIData.minDropGoldAmount = 10;
            enemyAIData.maxDropGoldAmount = 20;

            NPCUnitController dummyEnemyController = new NPCUnitController(dummyEnemy, enemyAIData);

            // BattleManager의 InitBattle을 흉내내어 enemyTeam에 추가하고 OnCharacterDied 트리거
            bm.InitBattle(new List<ICharacterController>(), new List<ICharacterController> { dummyEnemyController });
            dummyEnemy.TakeDamage(new DamageInfo(dummyEnemy, dummyEnemy, 999f, DamageFlag.Normal));

            Debug.Log($"[VerificationTest] Accumulated gold after enemy death: {bm.accumulativeGoldReward} (Expected: 10~20)");
            bm.ClearBattle();
            Destroy(dummyGo);
        }

        Debug.Log("[VerificationTest] Integration test scenario initialized successfully!");
    }
}
