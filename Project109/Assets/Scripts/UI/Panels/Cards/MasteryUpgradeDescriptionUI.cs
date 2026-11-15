using TMPro;
using UnityEngine;

public class MasteryUpgradeDescriptionUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI descriptionText;

    public void SetDescriptionText(string newDescription)
    {
        descriptionText.SetText(newDescription);
    }
}
