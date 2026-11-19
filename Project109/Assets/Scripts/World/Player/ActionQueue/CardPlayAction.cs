using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EventStructs;

public class CardPlayAction : BattleAction
{
    private static readonly WaitForSeconds[] _playDelays = CreateScaledDelays(0.4f);
    private static readonly WaitForSeconds[] _postPlayDelays = CreateScaledDelays(0.3f);

    private static WaitForSeconds[] CreateScaledDelays(float baseSeconds)
    {
        float[] steps = BattleManager.SpeedSteps;
        WaitForSeconds[] delays = new WaitForSeconds[steps.Length];
        for (int i = 0; i < steps.Length; i++)
        {
            delays[i] = new WaitForSeconds(baseSeconds / Mathf.Max(0.1f, steps[i]));
        }
        return delays;
    }

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
        if (caster == null || caster.isDead || IsBattleEnded())
        {
            RestoreCasterState();
            yield break;
        }

        // 1. 카드 발동 이벤트 Payload 송출
        CardInfo info = new CardInfo(caster, targets, targetPosition, card, CardFlag.Normal);
        CombatEventDispatcher.DispatchBeforeUseCard(caster.eventBus, info);

        // 2. 캐릭터 공격/스킬 애니메이션 트리거 및 상태 전이
        caster.currentState = CharacterState.Skill;

        // 3. 연출을 위한 인게임 대기 (예: 투사체 날아가는 시간, 이펙트 시작 등)
        yield return _playDelays[Mathf.Clamp(BattleManager.CurrentSpeedIndex, 0, _playDelays.Length - 1)];

        // 대기 중 시전자가 사망했거나 전투가 종료되었으면 즉시 중단
        if (caster == null || caster.isDead || IsBattleEnded())
        {
            RestoreCasterState();
            yield break;
        }

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

            if (IsBattleEnded())
            {
                RestoreCasterState();
                yield break;
            }

            card.Execute(info);
            DamageDestructiblesInRange(info);
        }

        // 카드 실행 결과로 전투가 승리/종료되었다면 이미 Dispose된 BattleDeck 조작 차단
        if (IsBattleEnded())
        {
            RestoreCasterState();
            yield break;
        }

        // 5. 사용 카드 손패에서 무덤으로 이동 처리 (플레이어의 경우에만)
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null && caster.Equals(GameSceneManager.instance.player.character))
        {
            var battleDeck = GameSceneManager.instance.player.battleDeck;
            if (battleDeck != null)
            {
                if (card != null && !card.IsDisposed && !info.cardFlags.HasFlag(CardFlag.NoDiscard))
                {
                    bool isExhaustCard = card.isExhaustOnPlay || info.cardFlags.HasFlag(CardFlag.Exhaust);

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
        yield return _postPlayDelays[Mathf.Clamp(BattleManager.CurrentSpeedIndex, 0, _postPlayDelays.Length - 1)];

        // 돌진/도약 스킬 등으로 시전자가 이동 중이라면 이동이 완료될 때까지 안전하게 대기 (최대 3초 타임아웃 가드)
        float moveWaitElapsed = 0f;
        while (caster != null && !caster.isDead && caster.currentState == CharacterState.Move && !IsBattleEnded() && moveWaitElapsed < 3.0f)
        {
            moveWaitElapsed += Time.deltaTime;
            yield return null;
        }
        
        if (caster != null && !caster.isDead)
        {
            caster.currentState = CharacterState.Idle;
            CombatEventDispatcher.DispatchAfterUseCard(caster.eventBus, info);
        }
    }

    private void DamageDestructiblesInRange(CardInfo info)
    {
        if (GameMap.current == null || card == null || card.cardData == null) return;

        float baseDmg = 0f;
        if (card.cardData.baseValues != null && card.cardData.baseValues.TryGetValue("damage", out float dmgVal))
        {
            baseDmg = dmgVal;
        }

        if (baseDmg <= 0f) return;

        Vector2Int originCoord = GameMap.current.GetCharacterCoordinate(caster);
        LookDirection facing = (caster != null && caster.characterMove != null)
            ? caster.characterMove.facingDirection
            : LookDirection.Down;

        HashSet<Vector2Int> hitCoords = new HashSet<Vector2Int>();
        var aoeCoords = GameMap.current.CalculateCardAoECoords(originCoord, info.targetPosition, card.cardData, facing);
        if (aoeCoords != null && aoeCoords.Count > 0)
        {
            for (int i = 0; i < aoeCoords.Count; i++)
            {
                hitCoords.Add(aoeCoords[i]);
            }
        }
        else
        {
            hitCoords.Add(info.targetPosition);
        }

        foreach (var coord in hitCoords)
        {
            Tile tile = GameMap.current.GetTile(coord);
            if (tile != null && tile.occupyingDestructible != null && !tile.occupyingDestructible.isDead)
            {
                DamageInfo objDmg = new DamageInfo(caster, null, baseDmg, DamageFlag.Normal);
                tile.occupyingDestructible.TakeDamage(objDmg);
            }
        }
    }

    public override void OnCanceled()
    {
        base.OnCanceled();
        RestoreCasterState();
    }

    private bool IsBattleEnded()
    {
        if (RunManager.instance == null || RunManager.instance.battleManager == null)
        {
            return true;
        }

        BattleState state = RunManager.instance.battleManager.battleState;
        return state == BattleState.BattleEnd || state == BattleState.None;
    }

    private void RestoreCasterState()
    {
        if (caster != null && !caster.isDead && caster.currentState != CharacterState.Idle)
        {
            caster.currentState = CharacterState.Idle;
        }
    }
}
