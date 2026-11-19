using UnityEngine;

[RequireComponent(typeof(Character))]
public class CharacterAnimation : MonoBehaviour
{
    private Animator animator;
    private Character character;
    
    void Start()
    {
        if (transform.childCount > 0)
        {
            animator = transform.GetChild(0).GetComponent<Animator>();
        }
        character = GetComponent<Character>();

        if (character != null)
        {
            character.OnCharacterStateChanged += HandleStateChanged;
        }

        if (animator != null)
        {
            animator.speed = BattleManager.GlobalBattleSpeed;
        }

        BattleManager.OnBattleSpeedChanged -= HandleBattleSpeedChanged;
        BattleManager.OnBattleSpeedChanged += HandleBattleSpeedChanged;
    }

    private void HandleBattleSpeedChanged(float speed)
    {
        if (animator != null)
        {
            animator.speed = BattleManager.GlobalBattleSpeed;
        }
    }

    private void HandleStateChanged(Character character, CharacterState newState)
    {
        if (animator == null)
            return;

        switch (newState)
        {
            case CharacterState.Idle:
                animator.SetBool("IsMove", false);
                break;
            case CharacterState.Move:
                animator.SetBool("IsMove", true);
                break;
            case CharacterState.Attack:
                // 추후 구현: animator.SetTrigger("Attack");
                break;
            case CharacterState.Hit:
                // 추후 구현: animator.SetTrigger("Hit");
                break;
            case CharacterState.Die:
                // 추후 구현: animator.SetBool("IsDead", true);
                break;
        }
    }

    private void OnDestroy()
    {
        BattleManager.OnBattleSpeedChanged -= HandleBattleSpeedChanged;

        if (character != null)
        {
            character.OnCharacterStateChanged -= HandleStateChanged;
        }
    }
}
