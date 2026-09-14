using System;
using UnityEngine;

public class PriorityQueue<T> where T : IComparable<T>
{
    T[] data;
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
        if(Count >= Capacity)
        {
            Expand();
        }
        data[Count] = value;
        Count++;

        //트리 노드의 오름차순 정렬을 위한 데이터 이동
        int currentDataIndex = Count - 1;
        while(currentDataIndex > 0)
        {
            int parentDataIndex = (currentDataIndex - 1) / Capacity;

            //부모 노드의 값이 더 크거나 같다면 정지
            if (data[currentDataIndex].CompareTo(data[parentDataIndex]) > 0)
                break;

            //작을 경우 서로 교환
            T temp = data[currentDataIndex];
            data[currentDataIndex] = data[parentDataIndex];
            data[parentDataIndex] = temp;

            currentDataIndex = parentDataIndex;
        }
    }

    public T Dequeue()
    {
        if(Count == 0)
            throw new IndexOutOfRangeException();

        //제일 처음 값 추출
        T result = data[0];
        data[0] = data[Count - 1];
        data[Count - 1] = default(T);
        Count--;

        int currentDataIndex = 0;
        while(currentDataIndex < Count) 
        {
            int left = (currentDataIndex * 2) + 1;
            int right = (currentDataIndex * 2) + 2;

            int nextDataIndex = currentDataIndex;

            //왼쪽, 오른쪽 값들 중 더 작은 값이 존재한다면 nextData 갱신
            //같다면 정렬할 필요가 없으니 while문 종료
            if(left < Count && data[currentDataIndex].CompareTo(data[left]) > 0)
                nextDataIndex = left;
            if (right < Count && data[currentDataIndex].CompareTo(data[right]) > 0)
                nextDataIndex = right;
            if (nextDataIndex == currentDataIndex)
                break;

            //값 교환
            T temp = data[currentDataIndex];
            data[currentDataIndex] = data[nextDataIndex];
            data[nextDataIndex] = temp;

            currentDataIndex = nextDataIndex;
        }

        return result;
    }

    public T Peek()
    {
        if(Count == 0)
            throw new IndexOutOfRangeException();

        return data[0];
    }

    void Expand()
    {
        T[] newData = new T[Capacity * 2];
        for(int i = 0; i < Count; i++)
        {
            newData[i] = data[i];
        }
        
        data = newData;
        Capacity *= 2;
    }
}
