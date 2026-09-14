using UnityEngine;

[CreateAssetMenu(fileName = "TrapData", menuName = "TrapData")]
public class TrapData : ScriptableObject, IIdentifiable
{
    public string trapName;
    public string objectPath;
    public string dataPath;
    public float hp;
    public float damage;
    public int attackCount;

    public string ID => dataPath;
}
