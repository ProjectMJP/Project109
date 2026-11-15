using System;
using UnityEngine;

public class PriorityQueue<T> where T : IComparable<T>
{
    private T[] data;
    public int Count { get; private set; }
    public int Capacity { get; private set; }

    #region 생성자
    public PriorityQueue()
    {
        Count = 0;
        Capacity = 1;
        data = new T[Capacity];
    }

    public PriorityQueue(int capacity)
    {
        Count = 0;
        Capacity = capacity;
        data = new T[Capacity];
    }
    #endregion

    public void Enqueue(T value)
    {
        if (Count >= Capacity)
        {
            Expand();
        }
        data[Count] = value;
        Count++;

        // 트리 마지막 위치에서 부모와 비교하며 올바른 위치로 이동
        int currentDataIndex = Count - 1;
        while (currentDataIndex > 0)
        {
            int parentDataIndex = (currentDataIndex - 1) / 2;

            // 부모 노드가 더 작거나 같다면 힙 조건 만족으로 중단 (최소 힙 기준)
            if (data[currentDataIndex].CompareTo(data[parentDataIndex]) >= 0)
                break;

            // 부모와 값 교환
            T temp = data[currentDataIndex];
            data[currentDataIndex] = data[parentDataIndex];
            data[parentDataIndex] = temp;

            currentDataIndex = parentDataIndex;
        }
    }

    public T Dequeue()
    {
        if (Count == 0)
            throw new IndexOutOfRangeException();

        // 최상단 루트 노드 반환
        T result = data[0];
        data[0] = data[Count - 1];
        data[Count - 1] = default(T);
        Count--;

        int currentDataIndex = 0;
        while (currentDataIndex < Count)
        {
            int left = (currentDataIndex * 2) + 1;
            int right = (currentDataIndex * 2) + 2;

            int nextDataIndex = currentDataIndex;

            // 좌/우 자식 중 가장 작은 노드를 nextDataIndex로 선택
            if (left < Count && data[left].CompareTo(data[nextDataIndex]) < 0)
                nextDataIndex = left;
            if (right < Count && data[right].CompareTo(data[nextDataIndex]) < 0)
                nextDataIndex = right;

            if (nextDataIndex == currentDataIndex)
                break;

            // 값 교환
            T temp = data[currentDataIndex];
            data[currentDataIndex] = data[nextDataIndex];
            data[nextDataIndex] = temp;

            currentDataIndex = nextDataIndex;
        }

        return result;
    }

    public T Peek()
    {
        if (Count == 0)
            throw new IndexOutOfRangeException();

        return data[0];
    }

    public void Clear()
    {
        Array.Clear(data, 0, Count);
        Count = 0;
    }

    private void Expand()
    {
        T[] newData = new T[Capacity * 2];
        for (int i = 0; i < Count; i++)
        {
            newData[i] = data[i];
        }

        data = newData;
        Capacity *= 2;
    }
}
