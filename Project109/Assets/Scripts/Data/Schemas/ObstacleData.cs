using UnityEngine;

[CreateAssetMenu(fileName = "ObstacleData", menuName = "ObstacleData")]
public class ObstacleData : ScriptableObject, IIdentifiable
{
    public string obstacleName;
    public string objectPath;
    public string dataPath;
    public float hp;

    public string ID => dataPath;
}
