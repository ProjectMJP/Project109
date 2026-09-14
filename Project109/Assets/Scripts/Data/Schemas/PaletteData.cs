using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class PaletteData
{
    public List<string> terrainIDs = new List<string>();
    public List<string> objectIDs = new List<string>();
    public List<string> eventIDs = new List<string>();
}
