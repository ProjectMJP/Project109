using System.Collections.Generic;
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
        Assert.AreEqual(TargetType.SingleEnemy, CardData.ParseTargetType("Enemy"));
        Assert.AreEqual(TargetType.SingleEnemy, CardData.ParseTargetType("Single"));
        Assert.AreEqual(TargetType.SingleEnemy, CardData.ParseTargetType("Target"));
        Assert.AreEqual(TargetType.SingleEnemy, CardData.ParseTargetType("SingleEnemy"));

        Assert.AreEqual(TargetType.Self, CardData.ParseTargetType("Self"));
        Assert.AreEqual(TargetType.AreaSelf, CardData.ParseTargetType("Area"));
        Assert.AreEqual(TargetType.AreaSelf, CardData.ParseTargetType("AreaSelf"));
        Assert.AreEqual(TargetType.AreaGround, CardData.ParseTargetType("Ground"));
        Assert.AreEqual(TargetType.AreaGround, CardData.ParseTargetType("AreaGround"));
        Assert.AreEqual(TargetType.RandomEnemy, CardData.ParseTargetType("Random"));
        Assert.AreEqual(TargetType.RandomEnemy, CardData.ParseTargetType("RandomEnemy"));
        Assert.AreEqual(TargetType.MultiEnemy, CardData.ParseTargetType("Multi"));
        Assert.AreEqual(TargetType.MultiEnemy, CardData.ParseTargetType("MultiEnemy"));
        Assert.AreEqual(TargetType.AllEnemies, CardData.ParseTargetType("All"));
        Assert.AreEqual(TargetType.AllEnemies, CardData.ParseTargetType("AllEnemies"));
        Assert.AreEqual(TargetType.SingleAlly, CardData.ParseTargetType("Ally"));
        Assert.AreEqual(TargetType.SingleAlly, CardData.ParseTargetType("SingleAlly"));
        Assert.AreEqual(TargetType.AllAllies, CardData.ParseTargetType("AllAllies"));
        Assert.AreEqual(TargetType.Directional, CardData.ParseTargetType("Directional"));
        Assert.AreEqual(TargetType.Directional, CardData.ParseTargetType("AreaDirectional"));
        Assert.AreEqual(TargetType.Directional, CardData.ParseTargetType("Direction"));
        Assert.AreEqual(TargetType.Directional, CardData.ParseTargetType("Front"));

        // CardData.targetType 설정 시 parsedTargetType이 즉시 동기화되어야 합니다.
        var cardData = new CardData { targetType = "Area" };
        Assert.AreEqual(TargetType.AreaSelf, cardData.parsedTargetType);
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
    public void HasValidTargetsInRange_AreaSelf_WhenNoEnemiesInBattle_ReturnsTrue()
    {
        var controller = new PlayerBattleController(null);
        var card = CreateCard("회전베기", "AreaSelf", 0, 0);

        // Act & Assert: 시전자 중심 광역기는 적 0명 허공 시전 및 즉시 발동 허용
        Assert.IsFalse(controller.GetTargetingStrategy(TargetType.AreaSelf).IsManualSelectionRequired);
        Assert.IsTrue(controller.HasValidTargetsInRange(card, TargetType.AreaSelf));
    }

    [Test]
    public void HasValidTargetsInRange_Directional_WhenNoEnemiesInBattle_ReturnsTrue()
    {
        var controller = new PlayerBattleController(null);
        var card = CreateCard("검격", "Directional", 1, 1);

        // Act & Assert: 방향 즉발 카드는 선택 모드 없이 즉시 발동하며 적 0명 허공 시전을 허용합니다.
        Assert.IsFalse(controller.GetTargetingStrategy(TargetType.Directional).IsManualSelectionRequired);
        Assert.IsTrue(controller.HasValidTargetsInRange(card, TargetType.Directional));
    }

    [Test]
    public void AreaGroundStrategy_WhenTileClicked_FacesTargetDirection()
    {
        var charObj = new GameObject("TestPlayer");
        var tileObj = new GameObject("TargetTile");
        try
        {
            charObj.transform.position = Vector3.zero;
            tileObj.transform.position = new Vector3(3f, 0f, 0f);

            var character = charObj.AddComponent<Character>();
            typeof(Character).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(character, null);
            var tile = tileObj.AddComponent<Tile>();
            tile.SetCoord(3, 0);

            character.characterMove.SetFacingDirection(LookDirection.Down, false);
            using var player = new Player(character);
            var controller = new PlayerBattleController(player);
            var card = CreateCard("선수필승", "AreaGround", 1, 2);

            var strategy = controller.GetTargetingStrategy(TargetType.AreaGround);
            Assert.IsTrue(strategy.IsManualSelectionRequired);

            bool confirmed = strategy.TryCollectTargetsOnTileClicked(controller, card, tile, out _, out Vector2Int targetPos);
            Assert.IsTrue(confirmed);
            Assert.AreEqual(new Vector2Int(3, 0), targetPos);
            Assert.AreEqual(LookDirection.Right, character.characterMove.facingDirection);
        }
        finally
        {
            Object.DestroyImmediate(tileObj);
            Object.DestroyImmediate(charObj);
        }
    }

    [Test]
    public void EffectAreaShapeGenerator_RotateOffsetAndPatterns_WorkForAllDirections()
    {
        Vector2Int mapSize = new Vector2Int(10, 10);
        Vector2Int origin = new Vector2Int(5, 5);

        Assert.AreEqual(new Vector2Int(1, 2), EffectAreaShapeGenerator.RotateOffset(1, 2, LookDirection.Up));
        Assert.AreEqual(new Vector2Int(2, -1), EffectAreaShapeGenerator.RotateOffset(1, 2, LookDirection.Right));
        Assert.AreEqual(new Vector2Int(-1, -2), EffectAreaShapeGenerator.RotateOffset(1, 2, LookDirection.Down));
        Assert.AreEqual(new Vector2Int(-2, 1), EffectAreaShapeGenerator.RotateOffset(1, 2, LookDirection.Left));

        var rightLine = EffectAreaShapeGenerator.GetDirectionalLine(mapSize, origin, LookDirection.Right, 1, 3);
        CollectionAssert.AreEqual(
            new[] { new Vector2Int(6, 5), new Vector2Int(7, 5), new Vector2Int(8, 5) },
            rightLine);

        var arcPattern = new List<string> { "XXX", ".C." };
        var upArc = EffectAreaShapeGenerator.GetPatternWithRotation(mapSize, origin, arcPattern, LookDirection.Up);
        CollectionAssert.AreEquivalent(
            new[] { new Vector2Int(4, 6), new Vector2Int(5, 6), new Vector2Int(6, 6) },
            upArc);

        var downArc = EffectAreaShapeGenerator.GetPatternWithRotation(mapSize, origin, arcPattern, LookDirection.Down);
        CollectionAssert.AreEquivalent(
            new[] { new Vector2Int(4, 4), new Vector2Int(5, 4), new Vector2Int(6, 4) },
            downArc);

        var whole = EffectAreaShapeGenerator.GetWholeMap(new Vector2Int(3, 2));
        Assert.AreEqual(6, whole.Count);
    }

    [Test]
    public void GameMap_GetLineCoords_ReturnsBresenhamStepsExcludingOrigin()
    {
        var straight = GameMap.GetLineCoords(new Vector2Int(1, 1), new Vector2Int(4, 1));
        CollectionAssert.AreEqual(
            new[] { new Vector2Int(2, 1), new Vector2Int(3, 1), new Vector2Int(4, 1) },
            straight);

        var diagonal = GameMap.GetLineCoords(new Vector2Int(0, 0), new Vector2Int(2, 2));
        CollectionAssert.AreEqual(
            new[] { new Vector2Int(1, 1), new Vector2Int(2, 2) },
            diagonal);
    }
}
