using System;
using System.Collections.Generic;

public class EffectManager : IDisposable
{
    public event Action<Effect> OnEffectAdded;
    public event Action<Effect> OnEffectStacked;
    public event Action<Effect> OnEffectRemoved;

    private readonly List<Effect> effects = new();
    private readonly Character target;

    public IReadOnlyList<Effect> GetEffects() => effects;

    public EffectManager(Character character)
    {
        target = character;
    }

    public void AddEffect(Character caster, Effect effect, int stack = 1, float duration = 0f)
    {
        Effect existing = null;

        // 독립적인 버프(isIndependent)가 아닐 때만 기존 동일 이름의 버프를 찾아서 스택을 쌓음
        if (effect.Data == null || !effect.Data.isIndependent)
        {
            existing = effects.Find(e => e.Data != null && e.Data.effectName == effect.Data.effectName);
        }

        if (existing != null)
        {
            existing.OnStacked(stack, duration);
            OnEffectStacked?.Invoke(existing);
        }
        else
        {
            effects.Add(effect);
            effect.OnAdded(caster, target, stack, duration);
            OnEffectAdded?.Invoke(effect);
        }
    }

    public void RemoveEffect(Effect effect)
    {
        if (effects.Remove(effect))
        {
            effect.OnRemoved();
            OnEffectRemoved?.Invoke(effect);
        }
    }

    /// <summary>
    /// 전투 종료 시 남아있는 버프/디버프 이펙트들을 모두 제거합니다.
    /// </summary>
    public void ClearAllEffects(bool clearPermanent = true)
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            Effect effect = effects[i];
            if (effect != null)
            {
                if (!clearPermanent && effect.Data != null && effect.Data.isPermanent)
                    continue;

                effects.RemoveAt(i);
                effect.OnRemoved();
                OnEffectRemoved?.Invoke(effect);
            }
        }
    }

    public void Tick(float deltaTime)
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (i >= effects.Count) continue;
            Effect current = effects[i];
            if (current == null) continue;
            if (current.Data != null && current.Data.isPermanent) continue;

            current.elapsedSinceLastTick += deltaTime;
            if (current.elapsedSinceLastTick >= Effect.tickInterval)
            {
                current.OnTick();
                current.elapsedSinceLastTick -= Effect.tickInterval;
            }

            // OnTick 실행 중 대상이 사망했거나 이펙트 컬렉션이 초기화/수정된 경우 안전 탈출
            if (target != null && target.isDead) break;
            if (i >= effects.Count || effects[i] != current) continue;

            current.currentDuration -= deltaTime;
            if (current.currentDuration <= 0f)
            {
                current.OnTimeOut();
                if (i < effects.Count && effects[i] == current && current.currentStack <= 0)
                {
                    Effect removedEffect = effects[i];
                    removedEffect.OnRemoved();
                    effects.RemoveAt(i);
                    OnEffectRemoved?.Invoke(removedEffect);
                }
            }
        }
    }

    public void Dispose()
    {
        ClearAllEffects(true);
        OnEffectAdded = null;
        OnEffectStacked = null;
        OnEffectRemoved = null;
    }
}
