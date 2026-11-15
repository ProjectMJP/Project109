using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleData", menuName = "Battle/BattleData")]
public class BattleData : ScriptableObject, IIdentifiable
{
    public string battleDataName;
    public string dataPath;
    public int battleAppearLevel;
    public string battleLocation;
    public List<string> monsterNames;
    public List<RewardItem> rewards;

    public string ID => dataPath;
}
