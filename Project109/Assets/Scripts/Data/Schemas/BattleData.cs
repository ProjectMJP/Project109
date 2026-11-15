using System.Collections.Generic;

[System.Serializable]
public class BattleData : IIdentifiable
{
    public string battleId;
    public string battleDataName;
    public string dataPath;
    public int battleAppearLevel;
    public string battleLocation;
    public List<string> monsterNames;
    public List<RewardItem> rewards;

    public string ID => !string.IsNullOrEmpty(battleId) ? battleId : dataPath;
}
