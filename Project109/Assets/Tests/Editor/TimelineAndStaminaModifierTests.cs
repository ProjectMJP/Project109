using System;
using System.Collections.Generic;
using System.IO;
using EventStructs;
using NUnit.Framework;
using UnityEngine;
using XLua;
using YamlDotNet.Serialization;

[TestFixture]
public class TimelineAndStaminaModifierTests
{
    private sealed class StubCharacterController : ICharacterController
    {
        public Character controlledCharacter { get; }
        public int TurnStartCount { get; private set; }
        public int TurnEndCount { get; private set; }

        public StubCharacterController(Character character)
        {
            controlledCharacter = character;
        }

        public void OnTurnStart()
        {
            TurnStartCount++;
            if (controlledCharacter != null && controlledCharacter.eventBus != null)
            {
                CombatEventDispatcher.DispatchTurnStart(controlledCharacter.eventBus);
            }
        }

        public void OnTurnEnd()
        {
            TurnEndCount++;
            if (controlledCharacter != null && controlledCharacter.eventBus != null)
            {
                CombatEventDispatcher.DispatchTurnEnd(controlledCharacter.eventBus);
            }
        }
    }

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<LuaTable> _createdLuaTables = new List<LuaTable>();
    private readonly List<Relic> _createdRelics = new List<Relic>();
    private LuaEnv _luaEnv;

    [SetUp]
    public void SetUp()
    {
        _luaEnv = new LuaEnv();
        _luaEnv.DoString(@"
            EventStructs = {
                DamageInfo = CS.EventStructs.DamageInfo,
                HealInfo = CS.EventStructs.HealInfo,
                StaminaInfo = CS.EventStructs.StaminaInfo,
                TurnEndStaminaInfo = CS.EventStructs.TurnEndStaminaInfo,
                ShieldInfo = CS.EventStructs.ShieldInfo,
                CardInfo = CS.EventStructs.CardInfo,
                MoveInfo = CS.EventStructs.MoveInfo,
                EffectInfo = CS.EventStructs.EffectInfo,
                DamageFlag = CS.EventStructs.DamageFlag,
                HealFlag = CS.EventStructs.HealFlag,
                StaminaFlag = CS.EventStructs.StaminaFlag,
                ShieldFlag = CS.EventStructs.ShieldFlag,
                CardFlag = CS.EventStructs.CardFlag,
                MoveFlag = CS.EventStructs.MoveFlag,
                EffectFlag = CS.EventStructs.EffectFlag,
                StatModifierTarget = CS.StatModifierTarget,
                StatModifierOp = CS.StatModifierOp,
                StatModifierLifetime = CS.StatModifierLifetime,
                DefaultDamageTypeID = CS.EventStructs.DefaultDamageTypeID,
            }
            UnityEngine = {
                Debug = CS.UnityEngine.Debug,
                Mathf = CS.UnityEngine.Mathf,
                Random = CS.UnityEngine.Random,
                Vector3 = CS.UnityEngine.Vector3,
                Vector2 = CS.UnityEngine.Vector2,
                Vector2Int = CS.UnityEngine.Vector2Int,
            }
            RunManager = CS.RunManager
            CS = nil
        ");
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdRelics.Count - 1; i >= 0; i--)
        {
            _createdRelics[i]?.Dispose();
        }
        _createdRelics.Clear();

        for (int i = _createdLuaTables.Count - 1; i >= 0; i--)
        {
            _createdLuaTables[i]?.Dispose();
        }
        _createdLuaTables.Clear();

        if (_luaEnv != null)
        {
            _luaEnv.FullGc();
            _luaEnv.Dispose();
            _luaEnv = null;
        }

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();
    }

    private Character CreateCharacter(string name, CharacterFaction faction, float maxStamina, float regenPerSec)
    {
        var go = new GameObject(name);
        _createdObjects.Add(go);
        Character c = go.AddComponent<Character>();
        c.faction = faction;
        c.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = maxStamina,
            staminaRegenPerSecond = regenPerSec,
            moveRange = 3,
        });
        c.effectManager = new EffectManager(c);
        return c;
    }

    private Effect AttachEffectFromCore(Character target, string effectId, int stack, float duration)
    {
        string yamlPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "YAML", "Effects", effectId + ".yaml");
        string luaPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Effects", effectId + ".lua");
        IDeserializer deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        EffectData data = deserializer.Deserialize<EffectData>(File.ReadAllText(yamlPath));

        object[] res = _luaEnv.DoString(File.ReadAllText(luaPath), effectId);
        LuaTable table = res[0] as LuaTable;
        _createdLuaTables.Add(table);

        Effect effect = new Effect(data, table);
        target.effectManager.AddEffect(target, effect, stack, duration);
        return effect;
    }

    private Relic AttachRelicFromCore(Player player, string relicId)
    {
        string yamlPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "YAML", "Relics", relicId + ".yaml");
        string luaPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Relics", relicId + ".lua");
        IDeserializer deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        RelicData data = deserializer.Deserialize<RelicData>(File.ReadAllText(yamlPath));

        object[] res = _luaEnv.DoString(File.ReadAllText(luaPath), relicId);
        LuaTable table = res[0] as LuaTable;
        _createdLuaTables.Add(table);

        Relic relic = new Relic(data, player, table);
        _createdRelics.Add(relic);
        return relic;
    }

    [Test]
    public void Adrenaline_TimeLimitedModifier_PredictionMatchesActualBattleTick()
    {
        Character unit = CreateCharacter("Unit", CharacterFaction.Player, maxStamina: 100f, regenPerSec: 20f);
        AttachEffectFromCore(unit, "Adrenaline", 1, 1.0f);

        Assert.AreEqual(40f, unit.curCharacterStat.staminaRegenPerSecond, 0.001f);

        var breakpoints = new List<float>(4);
        float predictedTime = StatModifierEvaluator.CalculateTimeUntilTurn(
            unit.baseCharacterStat,
            unit.StatModifiers,
            unit.curStamina,
            unit.overflowStamina,
            excludeUntilTurnEnd: false,
            breakpoints,
            out _,
            out _);

        // 1초 동안 40/s = 40 누적 후, 나머지 60을 20/s로 채워 총 4.0초 도달
        Assert.AreEqual(4.0f, predictedTime, 0.001f);

        // 실제 BattleTick으로 0.1초씩 시뮬레이션하여 동일 시점 도달 검증
        float elapsed = 0f;
        bool reached = false;
        for (int step = 0; step < 50; step++)
        {
            reached = unit.BattleTick(0.1f);
            elapsed += 0.1f;
            if (reached) break;
        }

        Assert.IsTrue(reached);
        Assert.AreEqual(predictedTime, elapsed, 0.11f);
        Assert.AreEqual(20f, unit.curCharacterStat.staminaRegenPerSecond, 0.001f);
    }

    [Test]
    public void BaseCharacterStat_IsolatedFromTemporaryModifiers()
    {
        Character playerChar = CreateCharacter("Player", CharacterFaction.Player, maxStamina: 120f, regenPerSec: 40f);
        playerChar.AddOrRefreshStatModifier(
            "TempBoost",
            StatModifierTarget.MaxStamina,
            StatModifierOp.Flat,
            30f,
            StatModifierLifetime.UntilTurnEnd,
            isBattleScoped: true);

        Assert.AreEqual(150f, playerChar.curCharacterStat.maxStamina, 0.001f);
        Assert.AreEqual(120f, playerChar.baseCharacterStat.maxStamina, 0.001f);

        playerChar.ExpireTurnEndModifiers();
        Assert.AreEqual(120f, playerChar.curCharacterStat.maxStamina, 0.001f);
    }

    [Test]
    public void TurnEnd_CalculateAndCommit_VigorAndSuspiciousDrug_MatchTimelinePrediction()
    {
        Character playerChar = CreateCharacter("Player", CharacterFaction.Player, maxStamina: 120f, regenPerSec: 40f);
        Character enemyChar = CreateCharacter("Enemy", CharacterFaction.Enemy, maxStamina: 100f, regenPerSec: 25f);

        Player player = new Player(playerChar);
        AttachRelicFromCore(player, "수상한 약물");

        var playerCtrl = new StubCharacterController(playerChar);
        var enemyCtrl = new StubCharacterController(enemyChar);
        var battleManager = new BattleManager();
        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { enemyCtrl });
        battleManager.StartCombat();

        // 전투 시작 시 수상한 약물 발동 -> maxStamina 140, curStamina 20
        Assert.AreEqual(140f, playerChar.curCharacterStat.maxStamina, 0.001f);
        Assert.AreEqual(20f, playerChar.curStamina, 0.001f);

        // 플레이어가 턴을 획득하고 80 사용 -> 잔여 60, Vigor 2스택(+20 후속 회복) 보유
        playerChar.curStamina = 140f;
        battleManager.RequestTurnStart(playerChar);
        playerChar.curStamina = 60f;
        AttachEffectFromCore(playerChar, "Vigor", 2, 1f);

        enemyChar.curStamina = 25f; // 도달까지 3.0초 ((100-25)/25)

        var timeline = new List<TimelineEntry>();
        battleManager.PredictTimeline(timeline);

        // 예측: 수상한 약물(-20 preReset) -> (60-20)*0.5 = 20 -> + Vigor 20 postReset = 40
        // 턴 종료 후 maxStamina는 120으로 복귀 -> 다음 차례까지 (120-40)/40 = 2.0초 (적 3.0초보다 앞!)
        TimelineEntry predictedEntry = default;
        bool foundPredicted = false;
        for (int i = 0; i < timeline.Count; i++)
        {
            if (timeline[i].isPredictedNextTurn)
            {
                predictedEntry = timeline[i];
                foundPredicted = true;
                break;
            }
        }

        Assert.IsTrue(foundPredicted);
        Assert.AreEqual(40f, predictedEntry.predictedStartStamina, 0.001f);
        Assert.AreEqual(2.0f, predictedEntry.timeUntilTurn, 0.001f);
        Assert.IsTrue(timeline[1].isPredictedNextTurn, "이월+활력으로 적(3.0초)보다 앞(1번 슬롯)에 예측되어야 함");

        // 실제 EndTurn 실행 후 예측과 100% 일치하는지 검증
        battleManager.EndTurn(playerCtrl);
        Assert.AreEqual(40f, playerChar.curStamina, 0.001f);
        Assert.AreEqual(120f, playerChar.curCharacterStat.maxStamina, 0.001f);
        Assert.AreEqual(0, playerChar.effectManager.GetEffects().Count);

        battleManager.Dispose();
        player.Dispose();
    }

    [Test]
    public void TimelinePredictor_PlayerTurnHoverPreview_AndEnemyTurnOmitsNextTurn()
    {
        Character playerChar = CreateCharacter("Player", CharacterFaction.Player, maxStamina: 120f, regenPerSec: 40f);
        Character enemyA = CreateCharacter("EnemyA", CharacterFaction.Enemy, maxStamina: 100f, regenPerSec: 40f);
        Character enemyB = CreateCharacter("EnemyB", CharacterFaction.Enemy, maxStamina: 100f, regenPerSec: 25f);

        var playerCtrl = new StubCharacterController(playerChar);
        var enemyACtrl = new StubCharacterController(enemyA);
        var enemyBCtrl = new StubCharacterController(enemyB);

        var battleManager = new BattleManager();
        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { enemyACtrl, enemyBCtrl });
        battleManager.StartCombat();

        playerChar.curStamina = 120f;
        battleManager.RequestTurnStart(playerChar);
        playerChar.curStamina = 80f; // 이월 시 40 -> (120-40)/40 = 2.0초
        enemyA.curStamina = 60f;     // (100-60)/40 = 1.0초
        enemyB.curStamina = 25f;     // (100-25)/25 = 3.0초

        var timeline = new List<TimelineEntry>();
        battleManager.PredictTimeline(timeline);

        // 순서: [나(현재)] - [EnemyA(1.0s)] - [내 다음 턴(2.0s)] - [EnemyB(3.0s)]
        Assert.AreEqual(4, timeline.Count);
        Assert.IsTrue(timeline[0].isCurrentTurn && timeline[0].character == playerChar);
        Assert.AreEqual(enemyA, timeline[1].character);
        Assert.IsTrue(timeline[2].isPredictedNextTurn && timeline[2].character == playerChar);
        Assert.AreEqual(enemyB, timeline[3].character);

        // 80코스트 카드 마우스 오버: 잔여 0 -> 이월 0 -> (120-0)/40 = 3.0s
        // EnemyB도 3.0s 동률일 경우 BattleManager.TickTeamCombatants와 동일하게 플레이어 팀이 우선 배치됨
        var cost80Preview = new TimelinePreviewRequest
        {
            hasPreview = true,
            casterStaminaSpent = 80f,
        };
        battleManager.PredictTimeline(timeline, in cost80Preview);
        Assert.AreEqual(0f, timeline[2].predictedStartStamina, 0.001f);
        Assert.IsTrue(timeline[2].isPredictedNextTurn, "도달 시간·오버플로우 동률 시 플레이어 팀이 우선 정렬되어야 함");

        // EnemyB가 2.8s로 더 빠를 때는 80코스트 사용 시 EnemyB 뒤로 밀려야 함
        enemyB.curStamina = 30f; // (100-30)/25 = 2.8초
        battleManager.PredictTimeline(timeline, in cost80Preview);
        Assert.AreEqual(enemyB, timeline[2].character);
        Assert.IsTrue(timeline[3].isPredictedNextTurn, "80코스트 사용 시 EnemyB(2.8s) 뒤로 밀려야 함");

        // 실제 플레이어 스태미나는 호버로 변경되지 않았어야 함
        Assert.AreEqual(80f, playerChar.curStamina, 0.001f);

        // 적 턴일 때는 적의 다음 턴 예측 슬롯이 추가되지 않아야 함
        battleManager.EndTurn(playerCtrl);
        enemyA.curStamina = 100f;
        battleManager.RequestTurnStart(enemyA);
        battleManager.PredictTimeline(timeline);

        Assert.AreEqual(3, timeline.Count);
        for (int i = 0; i < timeline.Count; i++)
        {
            Assert.IsFalse(timeline[i].isPredictedNextTurn);
        }

        battleManager.Dispose();
    }

    [Test]
    public void TimelinePredictor_MarksStunnedUnits_AndSupportsPreviewStun()
    {
        Character playerChar = CreateCharacter("Player", CharacterFaction.Player, maxStamina: 120f, regenPerSec: 40f);
        Character enemyChar = CreateCharacter("Enemy", CharacterFaction.Enemy, maxStamina: 100f, regenPerSec: 25f);

        var playerCtrl = new StubCharacterController(playerChar);
        var enemyCtrl = new StubCharacterController(enemyChar);
        var battleManager = new BattleManager();
        battleManager.InitBattle(
            new List<ICharacterController> { playerCtrl },
            new List<ICharacterController> { enemyCtrl });
        battleManager.StartCombat();

        playerChar.curStamina = 120f;
        battleManager.RequestTurnStart(playerChar);

        var timeline = new List<TimelineEntry>();

        // 1. 카드 호버로 적에게 기절을 걸 때 미리보기 확인
        var stunPreview = new TimelinePreviewRequest
        {
            hasPreview = true,
            casterStaminaSpent = 30f,
            targetCharacter = enemyChar,
            targetGrantsStun = true,
        };
        battleManager.PredictTimeline(timeline, in stunPreview);
        Assert.IsTrue(timeline[1].isStunned || timeline[2].isStunned);

        // 2. 실제로 기절 상태이상 부여 시 타임라인에서 Stun 감지 확인
        AttachEffectFromCore(enemyChar, "Stun", 1, 1f);
        battleManager.PredictTimeline(timeline);
        bool enemyMarkedStunned = false;
        for (int i = 0; i < timeline.Count; i++)
        {
            if (timeline[i].character == enemyChar && timeline[i].isStunned)
            {
                enemyMarkedStunned = true;
                break;
            }
        }
        Assert.IsTrue(enemyMarkedStunned);

        battleManager.Dispose();
    }
}
