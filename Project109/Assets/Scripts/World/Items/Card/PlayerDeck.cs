using System;
using System.Collections.Generic;
using System.Linq;
using GameItem.Types;
using XLua;

/// <summary>
/// 플레이어의 영속적인 마스터 덱(Master Deck)을 관리하는 클래스.
/// 런타임 카드 인스턴스 생성, 추가, 제거, 업그레이드 및 진화 로직을 담당합니다.
/// </summary>
public class PlayerDeck
{
    private readonly Player owner;
    private readonly List<Card> cards = new();
    private int nextRuntimeID = 0;
    private readonly Dictionary<string, CardMasteryState> sharedMasteryStates = new();

    public CardMasteryState GetOrCreateSharedMasteryState(string cardName, CardData data)
    {
        if (!sharedMasteryStates.TryGetValue(cardName, out var state))
        {
            state = new CardMasteryState();
            if (data != null && data.maxMasteryPoint > 0)
            {
                state.masteryLevel = 0;
                state.maxMasteryXP = data.maxMasteryPoint;
                state.currentMasteryXP = 0;
                state.masteryXPIncreasePerLevel = data.maxMasteryPoint / 2.0f;
            }
            sharedMasteryStates[cardName] = state;
        }
        return state;
    }

    public PlayerDeck(Player owner)
    {
        this.owner = owner;
    }

    /// <summary>
    /// 마스터 덱의 모든 카드를 제거하고 이벤트를 발행합니다.
    /// </summary>
    public void Clear()
    {
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            RemoveCard(cards[i]);
        }
        sharedMasteryStates.Clear();
    }

    /// <summary>
    /// 현재 덱에 보관된 카드 리스트를 반환합니다. (불필요한 복사 할당을 방지하기 위해 읽기 전용으로 노출)
    /// </summary>
    public IReadOnlyList<Card> GetCards()
    {
        return cards;
    }

    /// <summary>
    /// 현재 덱에 보관된 카드 개수
    /// </summary>
    public int CardCount => cards.Count;

    /// <summary>
    /// 새로운 카드 데이터를 마스터 덱에 추가합니다.
    /// </summary>
    public Card AddCard(CardData cardData)
    {
        Card newCard = CreateNewCard(cardData);
        cards.Add(newCard);
        
        owner.eventBus.Invoke<IOnAddCard>(c => c.OnAddCard(newCard));
        return newCard;
    }

    /// <summary>
    /// 기존 카드 인스턴스를 마스터 덱에 추가합니다.
    /// </summary>
    public void AddCard(Card card)
    {
        if (card != null)
        {
            // 외부 임시 복제본이거나(음수 ID), 기존 덱 내에 이미 동일한 ID를 가진 카드가 존재할 경우 ID를 고유 양수로 강제 갱신
            if (card.runtimeID < 0 || cards.Any(c => c.runtimeID == card.runtimeID))
            {
                card.UpdateRuntimeID(nextRuntimeID++);
            }
        }

        cards.Add(card);
        owner.eventBus.Invoke<IOnAddCard>(c => c.OnAddCard(card));
    }

    /// <summary>
    /// 마스터 덱의 특정 카드를 제거합니다.
    /// </summary>
    public void RemoveCard(int runtimeID)
    {
        Card cardToRemove = cards.FirstOrDefault(c => c.runtimeID == runtimeID);
        if (cardToRemove != null)
        {
            if (cards.Remove(cardToRemove))
            {
                owner.eventBus.Invoke<IOnRemoveCard>(c => c.OnRemoveCard(cardToRemove));
                cardToRemove.Dispose();
            }
        }
    }

    /// <summary>
    /// 기존 카드 인스턴스를 마스터 덱에서 제거합니다.
    /// </summary>
    public void RemoveCard(Card card)
    {
        if (cards.Remove(card))
        {
            owner.eventBus.Invoke<IOnRemoveCard>(c => c.OnRemoveCard(card));
            card.Dispose();
        }
    }

    /// <summary>
    /// 특정 카드를 다음 등급으로 영구 업그레이드합니다.
    /// </summary>
    public void UpgradeCard(int runtimeID)
    {
        Card cardToUpgrade = cards.FirstOrDefault(c => c.runtimeID == runtimeID);
        if (cardToUpgrade != null && cardToUpgrade.cardData != null && cardToUpgrade.cardData.isUpgradable)
        {
            string upgradedName = cardToUpgrade.cardData.upgradedCardName;
            if (!string.IsNullOrEmpty(upgradedName))
            {
                if (ModLoader.Instance.CardDatabase.TryGetValue(upgradedName, out CardData upgradeCardData))
                {
                    Card newCard = CreateNewCard(upgradeCardData, runtimeID);
                    
                    // 기존 마스터리 정보 보존
                    if (cardToUpgrade.hasMastery)
                    {
                        newCard.currentMasteryXP = cardToUpgrade.currentMasteryXP;
                        newCard.masteryLevel = cardToUpgrade.masteryLevel;
                    }
                    
                    if (cardToUpgrade.masteryUpgrades != null)
                    {
                        foreach (var kvp in cardToUpgrade.masteryUpgrades)
                        {
                            newCard.masteryUpgrades[kvp.Key] = kvp.Value;
                        }
                    }

                    // 마스터리로 붙은 태그도 보존 (AddTag를 통해 C# 및 Lua 로직 바인딩)
                    if (cardToUpgrade.tagNames != null)
                    {
                        foreach (var tag in cardToUpgrade.tagNames)
                        {
                            newCard.AddTag(tag);
                        }
                    }


                    int index = cards.IndexOf(cardToUpgrade);
                    if (index >= 0)
                    {
                        cards[index] = newCard;
                        cardToUpgrade.Dispose();
                    }

                    owner.eventBus.Invoke<IOnCardUpgrade>(c => c.OnCardUpgrade(newCard));
                }
            }
        }

        RequestAllCardRefresh();
    }

    /// <summary>
    /// 카드에 마스터리 업그레이드를 적용하고 IOnCardMasteryUpgrade 이벤트를 발행합니다.
    /// MasteryChoiceItem에서 card.AddMastery()를 직접 호출하는 대신 이 메서드를 사용합니다.
    /// </summary>
    public void ApplyMastery(Card card, string masteryId)
    {
        if (card == null || !cards.Contains(card)) return;

        card.AddMastery(masteryId);
        owner.eventBus.Invoke<IOnCardMasteryUpgrade>(c => c.OnCardMasteryUpgrade(card, masteryId));
    }

    /// <summary>
    /// 덱에서 랜덤하게 한 장의 카드를 가져옵니다.
    /// </summary>
    public Card GetRandomCard()
    {
        if (cards.Count == 0) return null;
        return cards[UnityEngine.Random.Range(0, cards.Count)];
    }

    /// <summary>
    /// 지정된 이름(식별자)의 카드를 가져옵니다.
    /// </summary>
    public Card GetSpecificCard(string name)
    {
        return cards.FirstOrDefault(c => c.cardData != null && c.cardData.cardName == name);
    }

    /// <summary>
    /// 지정된 ID(경로)의 카드를 가져옵니다.
    /// </summary>
    public Card GetCardByID(string id)
    {
        return cards.FirstOrDefault(c => c.cardData != null && c.cardData.cardName == id);
    }

    /// <summary>
    /// 모든 카드 변경 이벤트(새고고침)를 강제 호출합니다.
    /// </summary>
    public void RequestAllCardRefresh()
    {
        owner.eventBus.Invoke<IOnCardsRefreshed>(c => c.OnCardsRefreshed());
    }

    private Card CreateNewCard(CardData cardData, int? customRuntimeID = null)
    {
        LuaTable luaInstance = null;
        if (cardData.luaPrototype != null)
        {
            try
            {
                var newInstanceFunc = LuaManager.Instance.luaEnv.Global.Get<System.Func<LuaTable, LuaTable>>("NewInstance");
                if (newInstanceFunc != null)
                {
                    luaInstance = newInstanceFunc(cardData.luaPrototype);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[PlayerDeck] '{cardData.cardName}' Lua 인스턴스 생성 실패:\n{e.Message}");
            }
        }

        int id = customRuntimeID ?? nextRuntimeID++;
        Card newCard = new Card(cardData, owner.character, luaInstance, id);

        if (cardData != null && cardData.maxMasteryPoint > 0)
        {
            newCard.masteryState = GetOrCreateSharedMasteryState(cardData.cardName, cardData);
        }

        return newCard;
    }
}
