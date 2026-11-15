using NUnit.Framework;

[TestFixture]
public class BattleDeckTests
{
    private Card CreateDummyCard(int id, int cost = 1)
    {
        var data = new CardData { stamina = cost };
        return new Card(data, null, null, id);
    }

    [Test]
    public void DrawCards_TransfersCardsFromDrawPileToHand()
    {
        // Arrange
        var deck = new BattleDeck(null);
        var card1 = CreateDummyCard(1);
        var card2 = CreateDummyCard(2);
        var card3 = CreateDummyCard(3);

        deck.drawPile.Add(card1);
        deck.drawPile.Add(card2);
        deck.drawPile.Add(card3);

        // Act
        deck.DrawCards(2);

        // Assert
        Assert.AreEqual(2, deck.hand.Count);
        Assert.AreEqual(1, deck.drawPile.Count);
        Assert.AreEqual(card1, deck.hand[0]);
        Assert.AreEqual(card2, deck.hand[1]);
        Assert.AreEqual(card3, deck.drawPile[0]);
    }

    [Test]
    public void DrawCards_WhenDrawPileEmpty_ReshufflesDiscardPileIntoDrawPile()
    {
        // Arrange
        var deck = new BattleDeck(null);
        var card1 = CreateDummyCard(1);
        var card2 = CreateDummyCard(2);

        // 드로우 덱은 비어있고, 버림패에 2장 존재
        deck.discardPile.Add(card1);
        deck.discardPile.Add(card2);

        // Act
        deck.DrawCards(1);

        // Assert
        Assert.AreEqual(1, deck.hand.Count);
        Assert.AreEqual(1, deck.drawPile.Count);
        Assert.AreEqual(0, deck.discardPile.Count);
    }

    [Test]
    public void DiscardHand_MovesCardsFromHandToDiscardPile()
    {
        // Arrange
        var deck = new BattleDeck(null);
        var card1 = CreateDummyCard(1);
        var card2 = CreateDummyCard(2);

        deck.hand.Add(card1);
        deck.hand.Add(card2);

        // Act
        deck.DiscardHand();

        // Assert
        Assert.AreEqual(0, deck.hand.Count);
        Assert.AreEqual(2, deck.discardPile.Count);
    }

    [Test]
    public void ExhaustCard_MovesCardToExhaustPile_IsolatedFromDiscardPile()
    {
        // Arrange
        var deck = new BattleDeck(null);
        var card = CreateDummyCard(99);
        deck.hand.Add(card);

        // Act
        deck.ExhaustCard(card);

        // Assert
        Assert.AreEqual(0, deck.hand.Count);
        Assert.AreEqual(0, deck.discardPile.Count);
        Assert.AreEqual(1, deck.exhaustPile.Count);
        Assert.AreEqual(card, deck.exhaustPile[0]);
    }

    [Test]
    public void DisposeDeck_ClearsAllPilesCompletely()
    {
        // Arrange
        var deck = new BattleDeck(null);
        deck.drawPile.Add(CreateDummyCard(1));
        deck.hand.Add(CreateDummyCard(2));
        deck.discardPile.Add(CreateDummyCard(3));
        deck.exhaustPile.Add(CreateDummyCard(4));

        // Act
        deck.DisposeDeck();

        // Assert
        Assert.AreEqual(0, deck.drawPile.Count);
        Assert.AreEqual(0, deck.hand.Count);
        Assert.AreEqual(0, deck.discardPile.Count);
        Assert.AreEqual(0, deck.exhaustPile.Count);
    }
}
