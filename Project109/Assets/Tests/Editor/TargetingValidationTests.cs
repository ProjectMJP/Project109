using NUnit.Framework;
using UnityEngine;
using CardTypes;

[TestFixture]
public class TargetingValidationTests
{
    private Card CreateCard(string cardName, string targetType, int minDistance = 1, int maxDistance = 1)
    {
        var data = new CardData 
        { 
            cardName = cardName, 
            targetType = targetType,
            targetMinDistance = minDistance,
            targetMaxDistance = maxDistance
        };
        return new Card(data, null, null, 1);
    }

    [Test]
    public void ParseTargetType_CorrectlyNormalizesAllSynonyms()
    {
        Assert.AreEqual(TargetType.SingleEnemy, GameMap.ParseTargetType("Enemy"));
        Assert.AreEqual(TargetType.SingleEnemy, GameMap.ParseTargetType("Single"));
        Assert.AreEqual(TargetType.SingleEnemy, GameMap.ParseTargetType("Target"));
        Assert.AreEqual(TargetType.SingleEnemy, GameMap.ParseTargetType("SingleEnemy"));

        Assert.AreEqual(TargetType.Self, GameMap.ParseTargetType("Self"));
        Assert.AreEqual(TargetType.AreaSelf, GameMap.ParseTargetType("Area"));
        Assert.AreEqual(TargetType.AreaSelf, GameMap.ParseTargetType("AreaSelf"));
        Assert.AreEqual(TargetType.AreaGround, GameMap.ParseTargetType("Ground"));
        Assert.AreEqual(TargetType.AreaGround, GameMap.ParseTargetType("AreaGround"));
        Assert.AreEqual(TargetType.RandomEnemy, GameMap.ParseTargetType("Random"));
        Assert.AreEqual(TargetType.RandomEnemy, GameMap.ParseTargetType("RandomEnemy"));
        Assert.AreEqual(TargetType.MultiEnemy, GameMap.ParseTargetType("Multi"));
        Assert.AreEqual(TargetType.MultiEnemy, GameMap.ParseTargetType("MultiEnemy"));
        Assert.AreEqual(TargetType.AllEnemies, GameMap.ParseTargetType("All"));
        Assert.AreEqual(TargetType.AllEnemies, GameMap.ParseTargetType("AllEnemies"));
        Assert.AreEqual(TargetType.SingleAlly, GameMap.ParseTargetType("Ally"));
        Assert.AreEqual(TargetType.SingleAlly, GameMap.ParseTargetType("SingleAlly"));
        Assert.AreEqual(TargetType.AllAllies, GameMap.ParseTargetType("AllAllies"));
    }

    [Test]
    public void HasValidTargetsInRange_Self_AlwaysReturnsTrue()
    {
        var controller = new PlayerBattleController(null);
        var card = CreateCard("방어", "Self", 0, 0);

        // Act & Assert: 적의 존재 여부와 무관하게 Self 카드는 항상 true
        Assert.IsTrue(controller.HasValidTargetsInRange(card, TargetType.Self));
    }

    [Test]
    public void HasValidTargetsInRange_SingleEnemy_WhenNoEnemiesInBattle_ReturnsFalse()
    {
        var controller = new PlayerBattleController(null);
        var card = CreateCard("검격", "Enemy", 1, 1);

        // Act & Assert: 전장에 적이 없으면 false 반환 (타겟팅 진입 및 소프트락 차단)
        Assert.IsFalse(controller.HasValidTargetsInRange(card, TargetType.SingleEnemy));
    }

    [Test]
    public void HasValidTargetsInRange_RandomEnemy_WhenNoEnemiesInBattle_ReturnsFalse()
    {
        var controller = new PlayerBattleController(null);
        var card = CreateCard("난타", "RandomEnemy", 1, 2);

        // Act & Assert: 사거리 내에 적이 없으므로 무작위 타격 카드 발동 불가
        Assert.IsFalse(controller.HasValidTargetsInRange(card, TargetType.RandomEnemy));
    }

    [Test]
    public void HasValidTargetsInRange_AreaSelf_WhenNoEnemiesInBattle_ReturnsFalse()
    {
        var controller = new PlayerBattleController(null);
        var card = CreateCard("회전베기", "AreaSelf", 0, 2);

        // Act & Assert: 시전자 중심 광역기지만 범위 내 적이 0마리면 헛방 방지를 위해 false
        Assert.IsFalse(controller.HasValidTargetsInRange(card, TargetType.AreaSelf));
    }
}
