using System.Collections;

public abstract class BattleAction
{
    public Character caster { get; protected set; }
    
    public BattleAction(Character caster)
    {
        this.caster = caster;
    }

    /// <summary>
    /// 액션의 연출 및 실정산이 이루어지는 코루틴입니다.
    /// 코루틴이 완전히 반환(yield break)될 때까지 액션이 지속되는 것으로 판정합니다.
    /// </summary>
    public abstract IEnumerator ExecuteRoutine();
}
