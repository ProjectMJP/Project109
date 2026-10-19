using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EventStructs;

public class CardPlayAction : BattleAction
{
    private static readonly WaitForSeconds _playDelay = new WaitForSeconds(0.4f);
    private static readonly WaitForSeconds _postPlayDelay = new WaitForSeconds(0.3f);

    public Card card { get; private set; }
    public List<Character> targets { get; private set; }
    public Vector2Int targetPosition { get; private set; }

    public CardPlayAction(Character caster, Card card, List<Character> targets, Vector2Int targetPosition) 
        : base(caster)
    {
        this.card = card;
        this.targets = targets;
        this.targetPosition = targetPosition;
    }

    public override IEnumerator ExecuteRoutine()
    {
        if (caster == null || caster.isDead) yield break;

        // 1. 카드 발동 이벤트 Payload 송출
        CardInfo info = new CardInfo(caster, targets, targetPosition, card, CardFlag.Normal);
        caster.eventBus.Invoke<IOnBeforeUseCard>(c => c.OnBeforeUseCard(info));

        // 2. 캐릭터 공격/스킬 애니메이션 트리거 및 상태 전이
        caster.currentState = CharacterState.Skill;
        // 실제 프로젝트 애니메이터 연동이 있다면 여기서 트리거 재생 가능

        // 3. 연출을 위한 인게임 대기 (예: 투사체 날아가는 시간, 이펙트 시작 등)
        yield return _playDelay;

        // 대기 중 시전자가 사망했거나 파괴되었으면 행동 중단
        if (caster == null || caster.isDead) yield break;

        // 사망한 타겟 필터링
        if (targets != null)
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i] == null || targets[i].isDead)
                {
                    targets.RemoveAt(i);
                }
            }
        }

        // 4. 다중 비용(Stamina, HP, Gold 등) 선차감 및 실제 카드 로직 실행 (Lua 및 C#)
        if (card != null)
        {
            if (!info.cardFlags.HasFlag(CardFlag.FreeToPlay))
            {
                card.SpendCosts(caster);
            }
            card.Execute(info);
        }

        // 5. 사용 카드 손패에서 무덤으로 이동 처리 (플레이어의 경우에만)
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null && caster.Equals(GameSceneManager.instance.player.character))
        {
            var battleDeck = GameSceneManager.instance.player.battleDeck;
            if (battleDeck != null)
            {
                if (!info.cardFlags.HasFlag(CardFlag.NoDiscard))
                {
                    bool isExhaustCard = (card != null && card.isExhaustOnPlay) || info.cardFlags.HasFlag(CardFlag.Exhaust);

                    if (!info.cardFlags.HasFlag(CardFlag.NoExhaust) && isExhaustCard)
                    {
                        battleDeck.ExhaustCard(card, info);
                    }
                    else
                    {
                        battleDeck.DiscardCard(card, info);
                    }
                }
            }
        }

        // 6. 후처리 연출 대기 (애니메이션 마무리 동작)
        yield return _postPlayDelay;
        
        if (caster != null && !caster.isDead)
        {
            caster.currentState = CharacterState.Idle;
            caster.eventBus.Invoke<IOnAfterUseCard>(c => c.OnAfterUseCard(info));
        }
    }
}
