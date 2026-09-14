using UnityEngine;

[RequireComponent(typeof(Character))]
public class CharacterAnimation : MonoBehaviour
{
    private Animator animator;
    private Character character;
    
    void Start()
    {
        animator = transform.GetChild(0).GetComponent<Animator>();
        character = GetComponent<Character>();

        if (character != null)
        {
            character.OnCharacterStateChanged += HandleStateChanged;
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
        if (character != null)
        {
            character.OnCharacterStateChanged -= HandleStateChanged;
        }
    }
}
