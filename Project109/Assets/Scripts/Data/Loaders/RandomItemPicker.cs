using UnityEngine;
using System.Collections.Generic;

public class RandomItemPicker<T>
{
    private List<T> shuffledList;
    private int currentIndex = 0;

    public RandomItemPicker(IList<T> sourceList)
    {
        // 원본 리스트를 복사한 후 셔플
        shuffledList = new List<T>(sourceList);
        Shuffle(shuffledList);
    }

    //셔플 알고리즘
    private void Shuffle(List<T> list)
    {
        list.Shuffle();
    }

    //다음 아이템 반환 (없으면 false)
    public bool TryGetNext(out T item)
    {
        if (currentIndex < shuffledList.Count)
        {
            item = shuffledList[currentIndex];
            currentIndex++;
            return true;
        }

        item = default;
        return false;
    }

    public int Count()
    {
        return shuffledList.Count;
    }

    // 필요 시 리셋 기능
    public void Reset()
    {
        currentIndex = 0;
        Shuffle(shuffledList);
    }
}
