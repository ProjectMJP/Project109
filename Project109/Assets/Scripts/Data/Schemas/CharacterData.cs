using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Character/CharacterData")]
public class CharacterData : ScriptableObject, IIdentifiable
{
    public GameObject characterObject;
    public string characterName;
    public string assetPath;
    public string ID => characterName;
}
