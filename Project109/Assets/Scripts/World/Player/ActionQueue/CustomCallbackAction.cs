using System;
using System.Collections;

/// <summary>
/// 액션 큐 완료 시 실행할 임의의 콜백을 처리하는 범용 배틀 액션입니다.
/// </summary>
public class CustomCallbackAction : BattleAction
{
    private Action _callback;

    public CustomCallbackAction(Character caster, Action callback) : base(caster)
    {
        _callback = callback;
    }

    public override IEnumerator ExecuteRoutine()
    {
        _callback?.Invoke();
        yield break;
    }
}
