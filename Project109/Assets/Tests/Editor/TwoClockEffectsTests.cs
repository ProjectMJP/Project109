using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EventStructs;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XLua;
using YamlDotNet.Serialization;

[TestFixture]
public class TwoClockEffectsTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<LuaTable> _createdLuaTables = new List<LuaTable>();
    private LuaEnv _luaEnv;
    private GameObject _casterGo;
    private GameObject _targetGo;
    private Character _caster;
    private Character _target;

    [SetUp]
    public void SetUp()
    {
        _casterGo = Track(new GameObject("TwoClockCaster"));
        _caster = _casterGo.AddComponent<Character>();
        _caster.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 10f,
            moveRange = 3,
        });
        _caster.effectManager = new EffectManager(_caster);
        _caster.curStamina = 100f;

        _targetGo = Track(new GameObject("TwoClockTarget"));
        _target = _targetGo.AddComponent<Character>();
        _target.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 10f,
            moveRange = 3,
        });
        _target.effectManager = new EffectManager(_target);
        _target.curStamina = 100f;

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
        if (_caster != null && _caster.effectManager != null)
        {
            _caster.effectManager.ClearAllEffects(true);
        }
        if (_target != null && _target.effectManager != null)
        {
            _target.effectManager.ClearAllEffects(true);
        }

        for (int i = _createdLuaTables.Count - 1; i >= 0; i--)
        {
            if (_createdLuaTables[i] != null)
            {
                _createdLuaTables[i].Dispose();
            }
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

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null) _createdObjects.Add(obj);
        return obj;
    }

    private string GetEffectsYamlDir()
    {
        return Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "YAML", "Effects");
    }

    private string GetEffectsScriptsDir()
    {
        return Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Effects");
    }

    private EffectData LoadEffectDataYaml(string effectId)
    {
        string path = Path.Combine(GetEffectsYamlDir(), effectId + ".yaml");
        Assert.IsTrue(File.Exists(path), $"YAML file missing: {path}");
        IDeserializer deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        return deserializer.Deserialize<EffectData>(File.ReadAllText(path));
    }

    private LuaTable LoadEffectLuaInstance(string effectId)
    {
        string path = Path.Combine(GetEffectsScriptsDir(), effectId + ".lua");
        Assert.IsTrue(File.Exists(path), $"Lua file missing: {path}");
        object[] res = _luaEnv.DoString(File.ReadAllText(path), effectId);
        Assert.IsNotNull(res);
        Assert.GreaterOrEqual(res.Length, 1);
        LuaTable table = res[0] as LuaTable;
        Assert.IsNotNull(table, $"{effectId}.lua did not return a table");
        _createdLuaTables.Add(table);
        return table;
    }

    private Effect CreateAndAttachEffect(Character target, Character caster, string effectId, int stack, float duration)
    {
        EffectData data = LoadEffectDataYaml(effectId);
        LuaTable logic = LoadEffectLuaInstance(effectId);
        Effect effect = new Effect(data, logic);
        target.effectManager.AddEffect(caster, effect, stack, duration);
        return effect;
    }

    [Test]
    public void AllSixteenEffectYamls_HaveExpectedClockTypesAndFlags()
    {
        var expected = new Dictionary<string, (EffectClockType clock, bool permanent)>
        {
            { "Poison", (EffectClockType.Time, false) },
            { "Bleeding", (EffectClockType.Time, false) },
            { "Burn", (EffectClockType.Time, false) },
            { "Adrenaline", (EffectClockType.Time, false) },
            { "Vulnerable", (EffectClockType.Time, false) },
            { "Weakness", (EffectClockType.Time, false) },
            { "Frenzy", (EffectClockType.Turn, false) },
            { "Juggernaut", (EffectClockType.Turn, false) },
            { "Vigor", (EffectClockType.Turn, false) },
            { "Stun", (EffectClockType.Turn, false) },
            { "Disarm", (EffectClockType.Turn, false) },
            { "Fatigue", (EffectClockType.Turn, false) },
            { "Fortress", (EffectClockType.Turn, false) },
            { "Fortitude", (EffectClockType.Event, true) },
            { "Strength", (EffectClockType.Event, true) },
            { "Thorns", (EffectClockType.Event, true) },
        };

        foreach (var kv in expected)
        {
            EffectData data = LoadEffectDataYaml(kv.Key);
            Assert.IsNotNull(data, kv.Key);
            Assert.AreEqual(kv.Key, data.effectName, kv.Key);
            Assert.AreEqual(kv.Value.clock, data.clockType, $"{kv.Key} clockType mismatch");
            Assert.AreEqual(kv.Value.permanent, data.isPermanent, $"{kv.Key} isPermanent mismatch");
        }
    }

    [Test]
    public void EffectManager_Tick_SkipsTurnAndEventEffects_AndNotifiesStackOnTimeOut()
    {
        var turnData = new EffectData
        {
            effectName = "MockTurn",
            clockType = EffectClockType.Turn,
            isPermanent = false
        };
        var eventData = new EffectData
        {
            effectName = "MockEvent",
            clockType = EffectClockType.Event,
            isPermanent = true
        };
        var timeData = new EffectData
        {
            effectName = "MockTime",
            clockType = EffectClockType.Time,
            isPermanent = false
        };

        var turnEffect = new Effect(turnData, null);
        var eventEffect = new Effect(eventData, null);
        var timeEffect = new Effect(timeData, null);

        _target.effectManager.AddEffect(_caster, turnEffect, 2, 1.0f);
        _target.effectManager.AddEffect(_caster, eventEffect, 3, 1.0f);
        _target.effectManager.AddEffect(_caster, timeEffect, 2, 1.0f);

        int stackedNotifications = 0;
        Effect lastStackedEffect = null;
        _target.effectManager.OnEffectStacked += (eff) =>
        {
            stackedNotifications++;
            lastStackedEffect = eff;
        };

        _target.effectManager.Tick(1.0f);

        Assert.AreEqual(2, turnEffect.currentStack);
        Assert.AreEqual(1.0f, turnEffect.currentDuration, 0.0001f);
        Assert.AreEqual(3, eventEffect.currentStack);
        Assert.AreEqual(1, timeEffect.currentStack);
        Assert.AreEqual(1.0f, timeEffect.currentDuration, 0.0001f);
        Assert.AreEqual(1, stackedNotifications);
        Assert.AreSame(timeEffect, lastStackedEffect);

        _target.effectManager.NotifyEffectUpdated(turnEffect);
        Assert.AreEqual(2, stackedNotifications);
        Assert.AreSame(turnEffect, lastStackedEffect);
    }

    [Test]
    public void PoisonAndBurn_TimeClock_DealHpLossIgnoringShield_AndDecayPerSecond()
    {
        _target.TakeShield(new ShieldInfo(_target, _target, 50f, 1));
        Effect poison = CreateAndAttachEffect(_target, _caster, "Poison", 3, 0f);
        Assert.AreEqual(1.0f, poison.duration, 0.0001f);
        Assert.AreEqual(1.0f, poison.currentDuration, 0.0001f);

        _target.effectManager.Tick(1.0f);
        Assert.AreEqual(97f, _target.curHealth, 0.001f);
        Assert.AreEqual(50f, _target.shield, 0.001f);
        Assert.AreEqual(2, poison.currentStack);

        _target.effectManager.Tick(1.0f);
        Assert.AreEqual(95f, _target.curHealth, 0.001f);
        Assert.AreEqual(1, poison.currentStack);

        _target.effectManager.Tick(1.0f);
        Assert.AreEqual(94f, _target.curHealth, 0.001f);
        Assert.AreEqual(0, _target.effectManager.GetEffects().Count);

        Effect burn = CreateAndAttachEffect(_target, _caster, "Burn", 2, 999f);
        Assert.AreEqual(1.0f, burn.duration, 0.0001f);
        _target.effectManager.Tick(1.0f);
        Assert.AreEqual(92f, _target.curHealth, 0.001f);
        Assert.AreEqual(1, burn.currentStack);
    }

    [Test]
    public void Bleeding_MoveEventDealsDistanceDamage_AndTimeDecaysStack()
    {
        Effect bleeding = CreateAndAttachEffect(_target, _caster, "Bleeding", 2, 0f);
        Assert.AreEqual(3.0f, bleeding.duration, 0.0001f);

        var moveInfo = new MoveInfo(_target, new Vector2Int(1, 1), new Vector2Int(3, 2), MoveFlag.Normal);
        CombatEventDispatcher.DispatchAfterMove(_target.eventBus, moveInfo);
        Assert.AreEqual(94f, _target.curHealth, 0.001f); // distance 3 * stack 2 = 6 damage

        _target.effectManager.Tick(3.0f);
        Assert.AreEqual(1, bleeding.currentStack);
        Assert.AreEqual(3.0f, bleeding.currentDuration, 0.0001f);
    }

    [Test]
    public void VulnerableAndWeakness_FallbackDuration5SecondsPerStack()
    {
        Effect vuln = CreateAndAttachEffect(_target, _caster, "Vulnerable", 2, 0f);
        Effect weak = CreateAndAttachEffect(_caster, _target, "Weakness", 2, 999f);
        Assert.AreEqual(5.0f, vuln.duration, 0.0001f);
        Assert.AreEqual(5.0f, weak.duration, 0.0001f);

        var dmgInfo = new DamageInfo(_caster, _target, 20f, DamageFlag.Normal);
        CombatEventDispatcher.DispatchBeforeDealDamage(_caster.eventBus, ref dmgInfo);
        CombatEventDispatcher.DispatchBeforeTakeDamage(_target.eventBus, ref dmgInfo);
        Assert.AreEqual(1.5f * 0.75f, dmgInfo.damageMultiplier, 0.0001f);

        _target.effectManager.Tick(5.0f);
        _caster.effectManager.Tick(5.0f);
        Assert.AreEqual(1, vuln.currentStack);
        Assert.AreEqual(1, weak.currentStack);
    }

    [Test]
    public void Disarm_BlocksAttackCards_AllowsSkillCards_AndDecrementsOnTurnEnd()
    {
        Effect disarm = CreateAndAttachEffect(_target, _caster, "Disarm", 2, 1f);
        var attackData = new CardData { cardName = "Slash", cardType = "Attack", stamina = 10 };
        var skillData = new CardData { cardName = "Defend", cardType = "Skill", stamina = 10 };
        var attackCard = new Card(attackData, _target, null, 1);
        var skillCard = new Card(skillData, _target, null, 2);

        Assert.IsFalse(attackCard.CanPlay(_target));
        Assert.IsTrue(skillCard.CanPlay(_target));

        CombatEventDispatcher.DispatchTurnEnd(_target.eventBus);
        Assert.AreEqual(1, disarm.currentStack);
        Assert.IsFalse(attackCard.CanPlay(_target));

        CombatEventDispatcher.DispatchTurnEnd(_target.eventBus);
        Assert.AreEqual(0, _target.effectManager.GetEffects().Count);
        Assert.IsTrue(attackCard.CanPlay(_target));
    }

    [Test]
    public void Stun_Fatigue_Fortress_TurnClockHooksWorkCorrectly()
    {
        // Fatigue: 100 -> 70 stamina on turn start, stack 1 -> removed
        CreateAndAttachEffect(_target, _caster, "Fatigue", 1, 1f);
        CombatEventDispatcher.DispatchTurnStart(_target.eventBus);
        Assert.AreEqual(70f, _target.curStamina, 0.001f);
        Assert.AreEqual(0, _target.effectManager.GetEffects().Count);

        // Stun: sets stamina & moveRange to 0, consumes stack
        _target.curStamina = 80f;
        _target.curMoveRange = 3;
        Effect stun = CreateAndAttachEffect(_target, _caster, "Stun", 2, 1f);
        CombatEventDispatcher.DispatchTurnStart(_target.eventBus);
        Assert.AreEqual(0f, _target.curStamina, 0.001f);
        Assert.AreEqual(0, _target.curMoveRange);
        Assert.AreEqual(1, stun.currentStack);

        // Fortress: preserves shield across turn start UpdateShieldDuration
        _target.TakeShield(new ShieldInfo(_target, _target, 25f, 1));
        CreateAndAttachEffect(_target, _target, "Fortress", 1, 1f);
        CombatEventDispatcher.DispatchTurnEnd(_target.eventBus);
        Assert.AreEqual(2, _target.shieldDurationTurns);
        _target.UpdateShieldDuration();
        Assert.AreEqual(25f, _target.shield, 0.001f);
        Assert.AreEqual(1, _target.shieldDurationTurns);
    }

    [Test]
    public void CharacterEffectItemUIPrefab_HasDurationFillAndDisplaysClocksCorrectly()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterEffectItemUI.prefab");
        Assert.IsNotNull(prefab);

        GameObject instance = Track(UnityEngine.Object.Instantiate(prefab));
        CharacterEffectItemUI itemUi = instance.GetComponent<CharacterEffectItemUI>();
        Assert.IsNotNull(itemUi);

        FieldInfo stackField = typeof(CharacterEffectItemUI).GetField("stackText", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fillField = typeof(CharacterEffectItemUI).GetField("durationFillImage", BindingFlags.Instance | BindingFlags.NonPublic);
        TextMeshProUGUI stackText = stackField.GetValue(itemUi) as TextMeshProUGUI;
        Image durationFillImage = fillField.GetValue(itemUi) as Image;

        Assert.IsNotNull(stackText);
        Assert.IsNotNull(durationFillImage, "CharacterEffectItemUI.prefab must have durationFillImage assigned");
        Assert.AreEqual(Image.Type.Filled, durationFillImage.type);
        Assert.AreEqual(Image.FillMethod.Radial360, durationFillImage.fillMethod);

        // Turn clock: stack 1 shows "1T" and hides fill gauge
        var turnEffect = new Effect(new EffectData { effectName = "Stun", clockType = EffectClockType.Turn, isPermanent = false }, null);
        turnEffect.OnAdded(_caster, _target, 1, 1f);
        itemUi.Setup(turnEffect);
        Assert.IsTrue(stackText.gameObject.activeSelf);
        Assert.AreEqual("1T", stackText.text);
        Assert.IsFalse(durationFillImage.gameObject.activeSelf);

        // Time clock: stack 1 hides stackText, shows radial fill
        var timeEffect = new Effect(new EffectData { effectName = "Poison", clockType = EffectClockType.Time, isPermanent = false }, null);
        timeEffect.OnAdded(_caster, _target, 1, 4f);
        timeEffect.currentDuration = 2f;
        itemUi.Setup(timeEffect);
        Assert.IsFalse(stackText.gameObject.activeSelf);
        Assert.IsTrue(durationFillImage.gameObject.activeSelf);
        Assert.AreEqual(0.5f, durationFillImage.fillAmount, 0.001f);

        // Event clock: stack 2 shows "2", hides fill gauge
        var eventEffect = new Effect(new EffectData { effectName = "Strength", clockType = EffectClockType.Event, isPermanent = true }, null);
        eventEffect.OnAdded(_caster, _target, 2, 999f);
        itemUi.Setup(eventEffect);
        Assert.IsTrue(stackText.gameObject.activeSelf);
        Assert.AreEqual("2", stackText.text);
        Assert.IsFalse(durationFillImage.gameObject.activeSelf);
    }
}
