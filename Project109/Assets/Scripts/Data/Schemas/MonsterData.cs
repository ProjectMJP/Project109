using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MonsterData", menuName = "Monster/MonsterData")]
public class MonsterData : ScriptableObject, IIdentifiable
{
    public GameObject monsterPrefab;
    public string monsterName;
    public string objectPath;
    public string dataPath;

    public string ID => dataPath;
}
