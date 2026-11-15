using System.Collections.Generic;
using UnityEngine;

public static class CollectionExtensions
{
    /// <summary>
    /// Fisher-Yates 알고리즘을 사용하여 리스트 요소를 인플레이스 무작위 셔플합니다. (GC Alloc 없음)
    /// </summary>
    public static void Shuffle<T>(this IList<T> list)
    {
        if (list == null || list.Count <= 1) return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
