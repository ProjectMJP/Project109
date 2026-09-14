using UnityEngine;
using TMPro;

public class PlayerCurrencyUI : MonoBehaviour
{
    [Header("Currency UI")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI memorySharpText;

    public void UpdateGoldText(int amount)
    {
        if (goldText != null)
        {
            goldText.text = amount.ToString();
        }
    }

    public void UpdateMemorySharpText(int amount)
    {
        if (memorySharpText != null)
        {
            memorySharpText.text = amount.ToString();
        }
    }
}
