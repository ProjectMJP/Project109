using NUnit.Framework;
using UnityEngine;
using EventStructs;

[TestFixture]
public class CombatPipelineTests
{
    private GameObject _testGo;
    private Character _character;

    [SetUp]
    public void SetUp()
    {
        _testGo = new GameObject("TestCombatant");
        _character = _testGo.AddComponent<Character>();
        _character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 10f,
            maxMoveCount = 3,
            maxTilesPerMove = 2
        });
    }

    [TearDown]
    public void TearDown()
    {
        if (_testGo != null)
        {
            Object.DestroyImmediate(_testGo);
        }
    }

    [Test]
    public void DamageCalculation_WithMultiplier_ReducesHpByCalculatedAmount()
    {
        // Arrange: 20 기본 데미지에 1.5x 배율 (총 30 데미지)
        var info = new DamageInfo(null, _character, 20f, DamageFlag.Normal);
        info.damageMultiplier = 1.5f;

        // Act: 실제 Character.TakeDamage 호출!
        _character.TakeDamage(info);

        // Assert
        Assert.AreEqual(70f, _character.curHealth);
        Assert.AreEqual(0f, _character.shield);
        Assert.IsFalse(_character.isDead);
    }

    [Test]
    public void ShieldAbsorption_PartialAbsorption_BreaksShieldAndPassesRemainderToHp()
    {
        // Arrange: 20 쉴드 부여 후 35 데미지 (실드 20 흡수 파괴 + HP 15 차감)
        _character.TakeShield(new ShieldInfo(null, _character, 20f, 2));
        Assert.AreEqual(20f, _character.shield);

        var info = new DamageInfo(null, _character, 35f, DamageFlag.Normal);

        // Act: 실제 Character.TakeDamage 호출!
        _character.TakeDamage(info);

        // Assert
        Assert.AreEqual(0f, _character.shield);
        Assert.AreEqual(85f, _character.curHealth); // 100 - 15 = 85
        Assert.IsFalse(_character.isDead);
    }

    [Test]
    public void ShieldAbsorption_FullAbsorption_PreservesHpAndRetainsRemainingShield()
    {
        // Arrange: 50 쉴드 부여 후 20 데미지 (실드 20 차감, 잔여 실드 30, HP 무피해)
        _character.TakeShield(new ShieldInfo(null, _character, 50f, 2));

        var info = new DamageInfo(null, _character, 20f, DamageFlag.Normal);

        // Act: 실제 Character.TakeDamage 호출!
        _character.TakeDamage(info);

        // Assert
        Assert.AreEqual(30f, _character.shield);
        Assert.AreEqual(100f, _character.curHealth);
        Assert.IsFalse(_character.isDead);
    }

    [Test]
    public void DamageFlag_IgnoreShield_BypassesShieldCompletely()
    {
        // Arrange: 100 쉴드 부여 후 40 관통 데미지
        _character.TakeShield(new ShieldInfo(null, _character, 100f, 2));

        var info = new DamageInfo(null, _character, 40f, DamageFlag.IgnoreShield);

        // Act: 실제 Character.TakeDamage 호출!
        _character.TakeDamage(info);

        // Assert: 쉴드는 100 그대로 유지되고 HP만 40 차감
        Assert.AreEqual(100f, _character.shield);
        Assert.AreEqual(60f, _character.curHealth);
    }

    [Test]
    public void FatalDamage_ReducesHpToZero_TransitionsToDieState()
    {
        // Arrange: HP 100인 상태에서 120 치사 데미지
        var info = new DamageInfo(null, _character, 120f, DamageFlag.Normal);

        bool diedEventFired = false;
        _character.OnCharacterDied += (c) => diedEventFired = true;

        // Act: 실제 Character.TakeDamage 호출!
        _character.TakeDamage(info);

        // Assert: 체력 0, 사망 상태 및 Die 전이, 사망 이벤트 발행
        Assert.AreEqual(0f, _character.curHealth);
        Assert.IsTrue(_character.isDead);
        Assert.AreEqual(CharacterState.Die, _character.currentState);
        Assert.IsTrue(diedEventFired);
    }

    [Test]
    public void TakeHeal_Normal_RecoversHpUpToMaxHealth()
    {
        // Arrange: 50 데미지 입혀 50 HP 상태로 만든 후 30 힐 (-> 80 HP)
        _character.TakeDamage(new DamageInfo(null, _character, 50f, DamageFlag.Normal));
        Assert.AreEqual(50f, _character.curHealth);

        // Act: 실제 Character.TakeHeal 호출!
        _character.TakeHeal(new HealInfo(null, _character, 30f));

        // Assert
        Assert.AreEqual(80f, _character.curHealth);

        // 50 추가 힐 시 maxHealth(100)를 초과하지 않음
        _character.TakeHeal(new HealInfo(null, _character, 50f));
        Assert.AreEqual(100f, _character.curHealth);
    }

    [Test]
    public void TakeHeal_OverHealFlag_AllowsHpExceedingMaxHealth()
    {
        // Arrange: 이미 100 풀피 상태에서 OverHeal 30 힐
        _character.TakeHeal(new HealInfo(null, _character, 30f, HealFlag.OverHeal));

        // Assert: 130까지 초과 회복 허용
        Assert.AreEqual(130f, _character.curHealth);
    }
}
