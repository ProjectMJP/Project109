using UnityEngine;
using UnityEngine.UI;
using EventStructs;

public class MasterUIManager : MonoBehaviour
{
    [SerializeField]
    Character playerCharacter;

    [SerializeField]
    Character enemyCharacter;

    [SerializeField]
    Button masterUIButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        masterUIButton.onClick.AddListener(() =>
        {
            Debug.Log("Master UI Button Clicked");
            // Example action: Heal the player character by 10 health points

            DamageInfo damageInfo = new DamageInfo(enemyCharacter, playerCharacter, 10.0f, DamageFlag.Normal | DamageFlag.IgnoreShield);
            playerCharacter.TakeDamage(damageInfo);
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

