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
        if (caster == null) yield break;

        // 1. 카드 발동 이벤트 Payload 송출
        CardInfo info = new CardInfo(caster, targets, targetPosition, card, CardFlag.Normal);
        caster.eventBus.Invoke<IOnBeforeUseCard>(c => c.OnBeforeUseCard(info));

        // 2. 캐릭터 공격/스킬 애니메이션 트리거 및 상태 전이
        caster.currentState = CharacterState.Skill;
        // 실제 프로젝트 애니메이터 연동이 있다면 여기서 트리거 재생 가능

        // 3. 연출을 위한 인게임 대기 (예: 투사체 날아가는 시간, 이펙트 시작 등)
        yield return _playDelay;

        // 4. 실제 카드 로직 실행 (Lua 및 C#) 및 다중 비용(Stamina, HP, Gold 등) 차감
        if (card != null)
        {
            card.Execute(info);
            card.SpendCosts(caster);
        }

        // 5. 사용 카드 손패에서 무덤으로 이동 처리 (플레이어의 경우에만)
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null && caster.Equals(GameSceneManager.instance.player.character))
        {
            if (RunManager.instance != null && RunManager.instance.playerBattleController != null)
            {
                var battleDeck = RunManager.instance.playerBattleController.battleDeck;
                if (battleDeck != null)
                {
                    battleDeck.RemoveFromHand(card);

                    if (!info.cardFlags.HasFlag(CardFlag.NoDiscard))
                    {
                        bool isExhaustCard = card != null && (card.HasTag("Destroy") || card.HasTag("Single_use"));

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
        }

        // 6. 후처리 연출 대기 (애니메이션 마무리 동작)
        yield return _postPlayDelay;
        
        caster.currentState = CharacterState.Idle;
        caster.eventBus.Invoke<IOnAfterUseCard>(c => c.OnAfterUseCard(info));
    }
}
