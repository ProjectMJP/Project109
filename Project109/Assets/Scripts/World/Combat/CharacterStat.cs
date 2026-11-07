using UnityEngine;

[System.Serializable]
public class CharacterStat
{
    public float maxHealth;
    public float maxStamina;
    public float staminaRegenPerSecond;

    /// <summary>
    /// 턴당 기본 이동 가능 칸수 (이동력).
    /// </summary>
    public int moveRange;
}
