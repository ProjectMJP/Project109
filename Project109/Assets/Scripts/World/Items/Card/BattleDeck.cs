using System;
using System.Collections.Generic;
using UnityEngine;
using EventStructs;

/// <summary>
/// 전투 중 플레이어의 카드 덱 상태 및 동작을 전담하는 클래스.
/// 드로우 더미, 손패, 버림패, 소멸패의 데이터를 소유하고 관련 로직(드로우, 셔플 등)을 처리합니다.
/// </summary>
public class BattleDeck
{
    private readonly Character owner;
    public event Action OnHandChanged;

    // 전투 중 카드 더미
    public List<Card> drawPile { get; } = new();
    public List<Card> hand { get; } = new();
    public List<Card> discardPile { get; } = new();
    public List<Card> exhaustPile { get; } = new();

    public BattleDeck(Character owner)
    {
        this.owner = owner;
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
        
        ShuffleDrawPile();

        hand.Clear();
        discardPile.Clear();
        exhaustPile.Clear();
        OnHandChanged?.Invoke();
    }

    /// <summary>
    /// 지정된 개수만큼 카드를 드로우합니다.
    /// </summary>
    public void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (drawPile.Count == 0)
            {
                // 뽑을 카드가 없으면 묘지(버림패) -> 드로우 파일로 셔플
                if (discardPile.Count == 0) return;
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                ShuffleDrawPile();
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
    }

    /// <summary>
    /// 드로우 더미를 셔플합니다.
    /// </summary>
    public void ShuffleDrawPile()
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
    }

    /// <summary>
    /// 현재 손패의 모든 카드를 버림패 더미로 보냅니다.
    /// </summary>
    public void DiscardHand()
    {
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            var card = hand[i];

            // 카드 자체의 마스터리 등으로 Preserve 기능이 켜져 있는지 확인하여 기본 플래그로 설정
            CardFlag initialFlags = CardFlag.Normal;
            if (card != null && card.HasTag("Preserve"))
            {
                initialFlags |= CardFlag.NoDiscard;
            }

            CardInfo info = new CardInfo(owner, CardInfo.EmptyTargets, Vector2Int.zero, card, initialFlags);
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
        }
        OnHandChanged?.Invoke();
    }

    /// <summary>
    /// 손패에서 특정 카드를 제거합니다. (단순 제거용)
    /// </summary>
    public bool RemoveFromHand(Card card)
    {
        bool removed = hand.Remove(card);
        OnHandChanged?.Invoke();
        return removed;
    }

    /// <summary>
    /// 특정 카드를 소거(Erase)하여 손패에서 제거하고 이벤트를 발생시킵니다.
    /// </summary>
    public void EraseCard(Card card, CardInfo info)
    {
        if (owner != null)
        {
            owner.eventBus.Invoke<IOnEraseCard>(c => c.OnEraseCard(info));
        }
        hand.Remove(card);
        OnHandChanged?.Invoke();
    }

    /// <summary>
    /// 특정 카드를 버림패 더미에 추가합니다.
    /// </summary>
    public void DiscardCard(Card card, CardInfo info)
    {
        if (owner != null)
        {
            owner.eventBus.Invoke<IOnDiscardCard>(c => c.OnDiscardCard(info));
        }
        discardPile.Add(card);
        hand.Remove(card);
        OnHandChanged?.Invoke();
    }

    /// <summary>
    /// 특정 카드를 소멸패 더미에 추가합니다.
    /// </summary>
    public void ExhaustCard(Card card, CardInfo info)
    {
        if (owner != null)
        {
            owner.eventBus.Invoke<IOnExhaustCard>(c => c.OnExhaustCard(info));
        }
        exhaustPile.Add(card);
        hand.Remove(card);
        OnHandChanged?.Invoke();
    }
}
