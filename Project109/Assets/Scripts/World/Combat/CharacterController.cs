public interface ICharacterController 
{
    Character controlledCharacter { get; }

    void OnTurnStart();
    void OnTurnEnd();
}