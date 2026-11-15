using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EffectArea
{
    public string areaType;
    public int distance;
    public int width;
    public bool isPiercing;
    public bool ignoreObstacles;
    public List<string> pattern;
}