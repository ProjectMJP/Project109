using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MapDataInfo", menuName = "Map/MapDataInfo")]
public class MapDataInfo : ScriptableObject, IIdentifiable
{
    public string mapName;
    public string dataPath;
    public List<string> appearMonstersDataPath;
    public List<string> appearObstaclesDataPath;
    public List<string> appearTrapsDataPath;

    public string ID => mapName;
}
