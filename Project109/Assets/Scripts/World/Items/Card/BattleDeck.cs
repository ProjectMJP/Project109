using EventStructs;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 중 플레이어의 카드 덱 상태 및 동작을 전담하는 클래스.
/// 드로우 더미, 손패, 버림패, 소멸패의 데이터를 소유하고 관련 로직(드로우, 셔플 등)을 처리합니다.
/// </summary>
public class BattleDeck
{
    private readonly Character owner;

    // 더미별 변경 이벤트
    public event Action OnHandChanged;
    public event Action OnDrawPileChanged;
    public event Action OnDiscardPileChanged;
    public event Action OnExhaustPileChanged;

    // 전투 중 카드 더미
    public List<Card> drawPile { get; } = new();
    public List<Card> hand { get; } = new();
    public List<Card> discardPile { get; } = new();
    public List<Card> exhaustPile { get; } = new();

    // 읽기 전용 컬렉션 접근자
    public IReadOnlyList<Card> drawPileList => drawPile;
    public IReadOnlyList<Card> handList => hand;
    public IReadOnlyList<Card> discardPileList => discardPile;
    public IReadOnlyList<Card> exhaustPileList => exhaustPile;

    public BattleDeck(Character owner)
    {
        this.owner = owner;
    }

    /// <summary>
    /// CardInfo가 주어지지 않았을 때 이벤트용 기본 CardInfo를 생성합니다.
    /// </summary>
    private CardInfo EnsureCardInfo(Card card, CardInfo info, CardFlag defaultFlags = CardFlag.Normal)
    {
        if (info != null) return info;
        return new CardInfo(owner, card, defaultFlags);
    }

    public void InitDeck(IReadOnlyList<Card> masterDeck)
    {
        drawPile.Clear();
        
        // 마스터 덱 카드를 복제하여 전투 전용 덱 구성 (🚨 참조 공유로 인한 데이터 오염 방지)
        if (masterDeck != null)
        {
            foreach (var card in masterDeck)
            {
                if (card != null)
                {
                    // 전투 중에는 마스터 덱 카드의 runtimeID를 그대로 활용해 고유성을 띰
                    drawPile.Add(card.Clone(owner, card.runtimeID));
                }
            }
        }
        
        ShuffleDrawPile(suppressEvent: true);

        hand.Clear();
        discardPile.Clear();
        exhaustPile.Clear();

        OnHandChanged?.Invoke();
        OnDrawPileChanged?.Invoke();
        OnDiscardPileChanged?.Invoke();
        OnExhaustPileChanged?.Invoke();
    }

    /// <summary>
    /// 지정된 개수만큼 카드를 드로우합니다.
    /// </summary>
    public void DrawCards(int count)
    {
        bool reshuffled = false;
        for (int i = 0; i < count; i++)
        {
            if (drawPile.Count == 0)
            {
                // 뽑을 카드가 없으면 묘지(버림패) -> 드로우 파일로 셔플
                if (discardPile.Count == 0) break;
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                reshuffled = true;
                ShuffleDrawPile(suppressEvent: true);
            }

            var card = drawPile[0];
            drawPile.RemoveAt(0);

            if (owner != null)
            {
                owner.eventBus.Invoke<IOnDrawCard>(c => c.OnDrawCard(card));
            }

            hand.Add(card);
        }

        OnHandChanged?.Invoke();
        OnDrawPileChanged?.Invoke();
        if (reshuffled)
        {
            OnDiscardPileChanged?.Invoke();
        }
    }

    /// <summary>
    /// 드로우 더미를 셔플합니다.
    /// </summary>
    public void ShuffleDrawPile(bool suppressEvent = false)
    {
        if (owner != null)
        {
            owner.eventBus.Invoke<IOnShuffleDeck>(c => c.OnShuffleDeck(drawPile));
        }

        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
        }

        if (!suppressEvent)
        {
            OnDrawPileChanged?.Invoke();
        }
    }

    /// <summary>
    /// 현재 손패의 모든 카드를 버림패 더미로 보냅니다.
    /// </summary>
    public void DiscardHand()
    {
        bool hasDiscarded = false;
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            var card = hand[i];

            // 카드 자체의 마스터리 등으로 Preserve 기능이 켜져 있는지 확인하여 기본 플래그로 설정
            CardFlag initialFlags = CardFlag.Normal;
            if (card != null && card.HasTag("Preserve"))
            {
                initialFlags |= CardFlag.NoDiscard;
            }

            CardInfo info = EnsureCardInfo(card, null, initialFlags);
            if (owner != null)
            {
                owner.eventBus.Invoke<IOnDiscardCard>(c => c.OnDiscardCard(info));
            }

            // NoDiscard 플래그가 세팅되어 있다면 버리지 않고 보존
            if (info.cardFlags.HasFlag(CardFlag.NoDiscard))
            {
                continue;
            }

            discardPile.Add(card);
            hand.RemoveAt(i);
            hasDiscarded = true;
        }

        if (hasDiscarded)
        {
            OnHandChanged?.Invoke();
            OnDiscardPileChanged?.Invoke();
        }
    }

    /// <summary>
    /// 손패에서 특정 카드를 제거합니다. (단순 제거용)
    /// </summary>
    public bool RemoveFromHand(Card card)
    {
        bool removed = hand.Remove(card);
        if (removed)
        {
            OnHandChanged?.Invoke();
        }
        return removed;
    }

    /// <summary>
    /// 특정 카드를 소거(Erase)하여 손패에서 영구 제거하고 이벤트를 발생시킵니다.
    /// </summary>
    public void EraseCard(Card card, CardInfo info = null)
    {
        if (card == null) return;

        CardInfo finalInfo = EnsureCardInfo(card, info);
        if (owner != null)
        {
            owner.eventBus.Invoke<IOnEraseCard>(c => c.OnEraseCard(finalInfo));
        }

        hand.Remove(card);
        OnHandChanged?.Invoke();
    }

    /// <summary>
    /// 특정 카드를 버림패(묘지) 더미에 추가합니다.
    /// </summary>
    public void DiscardCard(Card card, CardInfo info = null)
    {
        if (card == null) return;

        CardInfo finalInfo = EnsureCardInfo(card, info);
        if (owner != null)
        {
            owner.eventBus.Invoke<IOnDiscardCard>(c => c.OnDiscardCard(finalInfo));
        }

        discardPile.Add(card);
        hand.Remove(card);
        OnHandChanged?.Invoke();
        OnDiscardPileChanged?.Invoke();
    }

    /// <summary>
    /// 특정 카드를 소멸패(제외) 더미에 추가합니다.
    /// </summary>
    public void ExhaustCard(Card card, CardInfo info = null)
    {
        if (card == null) return;

        CardInfo finalInfo = EnsureCardInfo(card, info);
        if (owner != null)
        {
            owner.eventBus.Invoke<IOnExhaustCard>(c => c.OnExhaustCard(finalInfo));
        }

        exhaustPile.Add(card);
        hand.Remove(card);
        OnHandChanged?.Invoke();
        OnExhaustPileChanged?.Invoke();
    }
}
