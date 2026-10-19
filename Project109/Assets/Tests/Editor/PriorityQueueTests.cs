using NUnit.Framework;
using System;

[TestFixture]
public class PriorityQueueTests
{
    private class TestNode : IComparable<TestNode>
    {
        public string Name { get; set; }
        public float Cost { get; set; }

        public TestNode(string name, float cost)
        {
            Name = name;
            Cost = cost;
        }

        public int CompareTo(TestNode other)
        {
            if (other == null) return 1;
            return Cost.CompareTo(other.Cost);
        }
    }

    [Test]
    public void PriorityQueue_EnqueueDequeue_ReturnsInAscendingOrder()
    {
        // Arrange
        var pq = new PriorityQueue<TestNode>(4);

        pq.Enqueue(new TestNode("NodeC", 30f));
        pq.Enqueue(new TestNode("NodeA", 10f));
        pq.Enqueue(new TestNode("NodeD", 40f));
        pq.Enqueue(new TestNode("NodeB", 20f));

        // Act & Assert
        Assert.AreEqual(4, pq.Count);
        Assert.AreEqual("NodeA", pq.Dequeue().Name);
        Assert.AreEqual("NodeB", pq.Dequeue().Name);
        Assert.AreEqual("NodeC", pq.Dequeue().Name);
        Assert.AreEqual("NodeD", pq.Dequeue().Name);
        Assert.AreEqual(0, pq.Count);
    }

    [Test]
    public void PriorityQueue_Peek_ReturnsMinWithoutRemoving()
    {
        // Arrange
        var pq = new PriorityQueue<TestNode>(4);
        pq.Enqueue(new TestNode("High", 100f));
        pq.Enqueue(new TestNode("Low", 5f));

        // Act
        var peeked = pq.Peek();

        // Assert
        Assert.AreEqual("Low", peeked.Name);
        Assert.AreEqual(2, pq.Count);
    }

    [Test]
    public void PriorityQueue_Clear_ResetsCount()
    {
        // Arrange
        var pq = new PriorityQueue<TestNode>(4);
        pq.Enqueue(new TestNode("A", 1f));
        pq.Enqueue(new TestNode("B", 2f));

        // Act
        pq.Clear();

        // Assert
        Assert.AreEqual(0, pq.Count);
    }
}
