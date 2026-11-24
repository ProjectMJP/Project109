using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;
using XLua;

/// <summary>
/// 강제 종료(PlayMode Stop / Application Quit) 시 파괴 순서 비결정성과
/// 모든 LuaTable·DelegateBridge 소유자의 결정론적 해제 파이프라인을 검증하는 회귀 테스트입니다.
/// </summary>
[TestFixture]
public class LuaEnvShutdownLifecycleTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private LuaManager _createdLuaManager;

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (_createdLuaManager != null)
            {
                _createdLuaManager.Dispose();
                _createdLuaManager = null;
            }
        }
        finally
        {
            InvokeStaticMethod<LuaManager>("ResetStaticState");
            InvokeStaticMethod<ModLoader>("ResetStaticState");
            InvokeStaticMethod<ActionQueueManager>("ResetStaticState");
            SetStaticAutoProperty<GameSceneManager>("instance", null);
            SetStaticAutoProperty<RunManager>("instance", null);
            SetStaticAutoProperty<DialogueManager>("Instance", null);

            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }
            _createdObjects.Clear();
        }
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private LuaManager CreateIsolatedLuaManager()
    {
        InvokeStaticMethod<LuaManager>("ResetStaticState");
        InvokeStaticMethod<ModLoader>("ResetStaticState");
        InvokeStaticMethod<ActionQueueManager>("ResetStaticState");

        GameObject luaGo = Track(new GameObject("TestLuaManager"));
        LuaManager luaManager = luaGo.AddComponent<LuaManager>();
        SetStaticField<LuaManager>("_instance", luaManager);
        SetStaticField<LuaManager>("_isShuttingDown", false);
        InvokePrivateMethod(luaManager, "InitLuaEnv");
        _createdLuaManager = luaManager;
        return luaManager;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ScrubStack()
    {
        Span<byte> scrub = stackalloc byte[512];
        scrub.Clear();
    }

    // =========================================================================
    // 시나리오 1: 타이틀 즉시 종료 (Core 모드 전체 로드 직후 OnApplicationQuit + OnDestroy)
    // =========================================================================
    [Test]
    public void Scenario1_TitleImmediateQuit_AfterFullCoreModLoad_DisposesLuaEnvWithoutException()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();
        RunScenario1LoadAllMods();
        ScrubStack();

        Assert.Greater(ModLoader.Instance.CardDatabase.Count, 0);
        Assert.Greater(ModLoader.Instance.RelicDatabase.Count, 0);
        Assert.Greater(ModLoader.Instance.EffectDatabase.Count, 0);

        Assert.DoesNotThrow(() =>
        {
            InvokePrivateMethod(luaManager, "OnApplicationQuit");
            Assert.IsTrue(LuaManager.IsShuttingDown);
            Assert.IsFalse(LuaManager.IsLuaEnvValid);
            Assert.IsTrue(LuaManager.IsLuaEnvAlive, "종료 정리 전까지 네이티브 LuaState는 살아 있어야 합니다.");

            InvokePrivateMethod(luaManager, "OnDestroy");
        });

        _createdLuaManager = null;
        Assert.IsFalse(LuaManager.IsLuaEnvAlive);
        foreach (CardData cardData in ModLoader.Instance.CardDatabase.Values)
        {
            Assert.IsNull(cardData.luaPrototype, $"카드 '{cardData.cardName}'의 luaPrototype이 해제되지 않았습니다.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunScenario1LoadAllMods()
    {
        ModLoader.Instance.LoadAllMods();
    }

    // =========================================================================
    // 시나리오 2: 은신처 도감 + 무장 진열대 + CardData._dummyInstance 열람 후 역순 파괴
    // =========================================================================
    [Test]
    public void Scenario2_HideoutCompendiumAndDummyInstances_ReverseDestroyOrder_ZeroExceptions()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();
        GameObject pauseGo = Track(new GameObject("TestPauseMenuPanel"));
        PauseMenuPanel pausePanel = pauseGo.AddComponent<PauseMenuPanel>();
        InvokePrivateMethod(pausePanel, "Awake");

        GameObject facilityGo = Track(new GameObject("TestHideoutFacility"));
        facilityGo.AddComponent<BoxCollider>();
        HideoutFacilityObject facility = facilityGo.AddComponent<HideoutFacilityObject>();
        InvokePrivateMethod(facility, "Awake");

        GameObject showcaseGo = Track(new GameObject("TestStarterKitShowcase"));
        StarterKitShowcasePanel showcase = showcaseGo.AddComponent<StarterKitShowcasePanel>();
        InvokePrivateMethod(showcase, "Awake");

        RunScenario2PopulateCompendium(pausePanel, facility, showcase);
        ScrubStack();

        // 최악의 파괴 순서: LuaManager가 UI 패널 및 은신처 오브젝트보다 먼저 파괴됨
        Assert.DoesNotThrow(() =>
        {
            InvokePrivateMethod(luaManager, "OnApplicationQuit");
            InvokePrivateMethod(luaManager, "OnDestroy");
        });
        _createdLuaManager = null;

        // LuaEnv가 이미 닫힌 뒤에 UI 패널들이 OnDestroy()를 실행해도 예외가 없어야 함
        Assert.DoesNotThrow(() =>
        {
            InvokePrivateMethod(pausePanel, "OnDestroy");
            InvokePrivateMethod(facility, "OnDestroy");
            InvokePrivateMethod(showcase, "OnDestroy");
        });

        Assert.AreEqual(0, pausePanel.CachedCompendiumCards.Count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunScenario2PopulateCompendium(
        PauseMenuPanel pausePanel,
        HideoutFacilityObject facility,
        StarterKitShowcasePanel showcase)
    {
        ModLoader.Instance.LoadAllMods();

        // 1. 모든 CardData의 설명 미리보기 Card 인스턴스 강제 생성
        foreach (CardData cardData in ModLoader.Instance.CardDatabase.Values)
        {
            _ = ModObjectFactory.GetCardDescription(cardData);
        }

        // 2. PauseMenuPanel 카드 도감 캐시 구축
        pausePanel.EnsureCompendiumCardsBuilt();
        Assert.Greater(pausePanel.CachedCompendiumCards.Count, 0);

        // 3. HideoutFacilityObject 카드 도감 캐시 구축
        InvokePrivateMethod(facility, "EnsureCompendiumCardsBuilt");

        // 4. StarterKitShowcasePanel 미리보기 카드 구축
        foreach (StarterKitData kit in ModLoader.Instance.StarterKitDatabase.Values)
        {
            InvokePrivateMethod(showcase, "BuildPreviewCards", kit);
            break;
        }
    }

    // =========================================================================
    // 시나리오 3A: 전투 진행 중 강제 종료 (LuaManager가 먼저 파괴되는 역순 케이스)
    // =========================================================================
    [Test]
    public void Scenario3A_ActiveCombatForceQuit_LuaManagerDestroyedFirst_ZeroExceptions()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();
        var ctx = SetupActiveCombatContext();
        ScrubStack();

        // 1. OnApplicationQuit → LuaManager가 모든 씬 매니저보다 먼저 파괴됨
        Assert.DoesNotThrow(() =>
        {
            InvokePrivateMethod(luaManager, "OnApplicationQuit");
            InvokePrivateMethod(luaManager, "OnDestroy");
        });
        _createdLuaManager = null;

        // 2. 이후 씬 오브젝트들의 OnDestroy가 실행되어도 this lua env had disposed 예외 0건
        Assert.DoesNotThrow(() =>
        {
            InvokePrivateMethod(ctx.actionQueue, "OnDestroy");
            InvokePrivateMethod(ctx.runManager, "OnDestroy");
            InvokePrivateMethod(ctx.gameSceneManager, "OnDestroy");
            InvokePrivateMethod(ctx.playerCharacter, "OnDestroy");
            InvokePrivateMethod(ctx.enemyCharacter, "OnDestroy");
        });
    }

    // =========================================================================
    // 시나리오 3B: 전투 진행 중 강제 종료 (씬 오브젝트들이 먼저 파괴된 후 LuaManager 파괴)
    // =========================================================================
    [Test]
    public void Scenario3B_ActiveCombatForceQuit_SceneObjectsDestroyedFirst_ZeroExceptions()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();
        var ctx = SetupActiveCombatContext();
        ScrubStack();

        // 1. OnApplicationQuit 먼저 호출 (기존 버그: 이 시점에 IsLuaEnvValid가 false가 되어 모든 OnDestroy가 luaTable.Dispose()를 건너뛰었음)
        Assert.DoesNotThrow(() =>
        {
            InvokePrivateMethod(luaManager, "OnApplicationQuit");
            InvokePrivateMethod(ctx.actionQueue, "OnApplicationQuit");

            // 2. 씬 오브젝트들 먼저 파괴
            InvokePrivateMethod(ctx.actionQueue, "OnDestroy");
            InvokePrivateMethod(ctx.runManager, "OnDestroy");
            InvokePrivateMethod(ctx.gameSceneManager, "OnDestroy");
            InvokePrivateMethod(ctx.playerCharacter, "OnDestroy");
            InvokePrivateMethod(ctx.enemyCharacter, "OnDestroy");

            // 3. 마지막으로 LuaManager 파괴
            InvokePrivateMethod(luaManager, "OnDestroy");
        });
        _createdLuaManager = null;
    }

    private struct CombatTestContext
    {
        public GameSceneManager gameSceneManager;
        public RunManager runManager;
        public ActionQueueManager actionQueue;
        public Character playerCharacter;
        public Character enemyCharacter;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private CombatTestContext SetupActiveCombatContext()
    {
        ModLoader.Instance.LoadAllMods();

        GameObject gsmGo = Track(new GameObject("TestGameSceneManager"));
        GameSceneManager gsm = gsmGo.AddComponent<GameSceneManager>();
        InvokePrivateMethod(gsm, "Awake");

        GameObject playerGo = Track(new GameObject("TestPlayerCharacter"));
        Character playerChar = playerGo.AddComponent<Character>();
        InvokePrivateMethod(playerChar, "Awake");
        playerChar.faction = CharacterFaction.Player;
        playerChar.InitializeStat(new CharacterStat { maxHealth = 100f, maxStamina = 10f, staminaRegenPerSecond = 1f, moveRange = 3 });

        Player player = new Player(playerChar);
        gsm.player = player;

        // 마스터 덱에 실제 Core 카드 추가 + 카드 태그 부착
        int addedCards = 0;
        foreach (CardData cardData in ModLoader.Instance.CardDatabase.Values)
        {
            Card added = player.deck.AddCard(cardData);
            if (addedCards == 0)
            {
                added.AddTag("Preserve");
            }
            addedCards++;
            if (addedCards >= 5) break;
        }

        // 유물 장착 (Character/Player EventBus에 Lua InterfaceBridge 등록)
        int addedRelics = 0;
        foreach (string relicId in ModLoader.Instance.RelicDatabase.Keys)
        {
            player.AddRelic(relicId);
            addedRelics++;
            if (addedRelics >= 3) break;
        }

        // 전투 덱 초기화 (복제 Card 생성 및 드로우)
        player.InitBattleDeck();
        player.battleDeck.DrawCards(3);

        // 활성 상태이상 부여 (스택 병합 및 취소 경로 포함)
        int addedEffects = 0;
        foreach (string effectId in ModLoader.Instance.EffectDatabase.Keys)
        {
            playerChar.TakeEffect(playerChar, effectId, 2, 5f);
            // 동일 이펙트 1회 추가하여 스택 병합 경로의 임시 Effect LuaTable 누수도 함께 검증
            playerChar.TakeEffect(playerChar, effectId, 1, 5f);
            addedEffects++;
            if (addedEffects >= 3) break;
        }

        // 적 캐릭터 및 NPCUnitController 생성 (AI LuaTable + 내부 _cardPool 생성)
        GameObject enemyGo = Track(new GameObject("TestEnemyCharacter"));
        Character enemyChar = enemyGo.AddComponent<Character>();
        InvokePrivateMethod(enemyChar, "Awake");
        enemyChar.faction = CharacterFaction.Enemy;

        NPCUnitData enemyData = null;
        foreach (NPCUnitData unit in ModLoader.Instance.NPCUnitDatabase.Values)
        {
            if (unit != null && unit.luaPrototype != null)
            {
                enemyData = unit;
                break;
            }
        }
        Assert.IsNotNull(enemyData, "Core 모드에 Lua AI를 가진 적이 1종 이상 필요합니다.");
        enemyChar.InitializeStat(enemyData.characterStat ?? new CharacterStat { maxHealth = 50f, maxStamina = 10f, staminaRegenPerSecond = 1f, moveRange = 2 });

        NPCUnitController enemyCtrl = new NPCUnitController(enemyChar, enemyData);
        enemyCtrl.EvaluateNextIntent();

        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        InvokePrivateMethod(runManager, "Awake");
        runManager.currentStage.RegisterSpawnEnemy(enemyGo, enemyCtrl);
        runManager.battleManager.InitBattle(
            new List<ICharacterController> { runManager.playerBattleController },
            new List<ICharacterController> { enemyCtrl });

        GameObject aqGo = Track(new GameObject("TestActionQueueManager"));
        ActionQueueManager aq = aqGo.AddComponent<ActionQueueManager>();
        InvokePrivateMethod(aq, "Awake");

        return new CombatTestContext
        {
            gameSceneManager = gsm,
            runManager = runManager,
            actionQueue = aq,
            playerCharacter = playerChar,
            enemyCharacter = enemyChar
        };
    }

    // =========================================================================
    // 시나리오 4: 다이얼로그 Choice 스크립트 실행 중 강제 종료
    // =========================================================================
    [Test]
    public void Scenario4_DialogueChoiceActiveForceQuit_ZeroExceptions()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();
        GameObject dmGo = Track(new GameObject("TestDialogueManager"));
        DialogueManager dm = dmGo.AddComponent<DialogueManager>();
        InvokePrivateMethod(dm, "Awake");

        RunScenario4ExecuteChoiceCallbacks(luaManager, dm);
        ScrubStack();

        Assert.DoesNotThrow(() =>
        {
            InvokePrivateMethod(luaManager, "OnApplicationQuit");
            InvokePrivateMethod(luaManager, "OnDestroy");
            InvokePrivateMethod(dm, "OnDestroy");
        });
        _createdLuaManager = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunScenario4ExecuteChoiceCallbacks(LuaManager luaManager, DialogueManager dm)
    {
        ModLoader.Instance.LoadAllMods();
        foreach (DialogueData dialogue in ModLoader.Instance.DialogueDatabase.Values)
        {
            if (dialogue == null || dialogue.nodes == null) continue;
            foreach (DialogueNode node in dialogue.nodes)
            {
                if (node == null || node.choices == null) continue;
                foreach (ChoiceData choice in node.choices)
                {
                    if (choice == null || string.IsNullOrEmpty(choice.luaScript)) continue;
                    if (luaManager.TryGetChoiceCallbacks(choice.luaScript, out var canSelect, out var getDesc, out _, out var isVisible))
                    {
                        _ = isVisible?.Invoke(dm);
                        _ = canSelect?.Invoke(dm);
                        _ = getDesc?.Invoke(dm);
                        canSelect = null;
                        getDesc = null;
                        isVisible = null;
                    }
                }
            }
        }
    }

    // =========================================================================
    // 시나리오 5: WeakReference 안전망 검증 (소유자 Dispose 누락된 고아 Lua 객체 일괄 회수)
    // =========================================================================
    [Test]
    public void Scenario5_OrphanedLuaResources_ReclaimedByWeakRegistrySafetyNet_ZeroExceptions()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();
        List<object> intentionallyLeakedHolders = new List<object>();

        CreateOrphanedLuaObjects(luaManager, intentionallyLeakedHolders);
        ScrubStack();

        // 소유자가 단 한 번도 Dispose()를 부르지 않고 리스트에 강한 참조를 쥐고 있는 상태에서도
        // LuaManager.Dispose()가 WeakRegistry 안전망으로 DelegateBridge/LuaTable을 모두 회수해야 함
        Assert.DoesNotThrow(() =>
        {
            luaManager.Dispose();
        });
        _createdLuaManager = null;

        Assert.Greater(intentionallyLeakedHolders.Count, 0);
        foreach (object holder in intentionallyLeakedHolders)
        {
            if (holder is Card card)
            {
                Assert.IsTrue(card.IsDisposed, "고아 Card가 안전망에 의해 Dispose되지 않았습니다.");
                Assert.IsNull(card.luaTable);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateOrphanedLuaObjects(LuaManager luaManager, List<object> holders)
    {
        object[] cardRes = luaManager.luaEnv.DoString(@"
            return {
                CanPlay = function(self, card, caster) return true end,
                SpendCosts = function(self, card, caster) end,
                GetDescription = function(self, card, tpl) return tpl end
            }
        ");
        LuaTable cardProto = cardRes[0] as LuaTable;
        CardData cardData = new CardData { cardName = "OrphanCard", stamina = 1, description = "Test" };
        cardData.luaPrototype = cardProto;
        ModLoader.Instance.CardDatabase[cardData.cardName] = cardData;

        LuaTable cardInst = luaManager.NewInstance(cardProto);
        Card orphanCard = new Card(cardData, null, cardInst, -777);
        holders.Add(orphanCard);

        object[] effectRes = luaManager.luaEnv.DoString(@"
            return {
                OnInit = function(self, eff) self.base = eff end,
                OnRemoved = function(self, eff) error('Should not invoke OnRemoved during LuaManager.Dispose') end
            }
        ");
        LuaTable effectProto = effectRes[0] as LuaTable;
        EffectData effectData = new EffectData { effectName = "OrphanEffect" };
        effectData.luaPrototype = effectProto;
        ModLoader.Instance.EffectDatabase[effectData.effectName] = effectData;

        LuaTable effectInst = luaManager.NewInstance(effectProto);
        Effect orphanEffect = new Effect(effectData, effectInst);
        holders.Add(orphanEffect);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} 메서드를 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private static void InvokeStaticMethod<T>(string methodName)
    {
        MethodInfo method = typeof(T).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{typeof(T).Name}.{methodName} 정적 메서드를 찾을 수 없습니다.");
        method.Invoke(null, null);
    }

    private static void SetStaticField<T>(string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{typeof(T).Name}.{fieldName} 정적 필드를 찾을 수 없습니다.");
        field.SetValue(null, value);
    }

    private static void SetStaticAutoProperty<T>(string propertyName, object value)
    {
        FieldInfo backingField = typeof(T).GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(backingField, $"{typeof(T).Name}.{propertyName} 정적 자동 프로퍼티 백킹 필드를 찾을 수 없습니다.");
        backingField.SetValue(null, value);
    }
}
