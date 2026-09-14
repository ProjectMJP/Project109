using EventStructs;

public interface IPlayerEvent {}

#region 유물 관련 인터페이스 (Relic Interfaces)
public interface IOnAddRelic : IPlayerEvent { void OnAddRelic(RelicData relicData); }
public interface IOnRemoveRelic : IPlayerEvent { void OnRemoveRelic(RelicData relicData); }
#endregion

#region 카드 관련 인터페이스 (Card Interfaces)
public interface IOnAddCard : IPlayerEvent { void OnAddCard(Card cardData); }
public interface IOnRemoveCard : IPlayerEvent { void OnRemoveCard(Card cardData); }
public interface IOnCardUpgrade : IPlayerEvent { void OnCardUpgrade(Card cardData); }
// 마스터리 레벨업 이벤트 (upgrade와 다른 시스템)
public interface IOnCardMasteryUpgrade : IPlayerEvent { void OnCardMasteryUpgrade(Card card, string masteryId); }
public interface IOnCardsRefreshed : IPlayerEvent { void OnCardsRefreshed(); }
#endregion

#region 재화 관련 인터페이스 (Currency Interfaces)
public interface IOnAddGold : IPlayerEvent { void OnAddGold(int gold); }
public interface IOnRemoveGold : IPlayerEvent { void OnRemoveGold(int gold); }

public interface IOnAddMemorySharp : IPlayerEvent { void OnAddMemorySharp(int memorySharp); }
public interface IOnRemoveMemorySharp : IPlayerEvent { void OnRemoveMemorySharp(int memorySharp); }
#endregion
