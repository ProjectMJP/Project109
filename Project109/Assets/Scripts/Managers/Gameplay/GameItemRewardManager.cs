using UnityEngine;
using System.Collections.Generic;
using GameItem.Types;

public class GameItemRewardManager : MonoBehaviour, IOnAddRelic, IOnRemoveRelic
{
    public static GameItemRewardManager instance { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;

            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    [SerializeField] private GameObject rewardObjectPrefab;
    [SerializeField] private GameObject rewardBoxPrefab;

    //Cards
    private List<CardData> commonCardList = new List<CardData>();
    private List<CardData> uncommonCardList = new List<CardData>();
    private List<CardData> rareCardList = new List<CardData>();
    private List<CardData> uniqueCardList = new List<CardData>();

    RandomItemPicker<CardData> commonCardPicker;
    RandomItemPicker<CardData> uncommonCardPicker;
    RandomItemPicker<CardData> rareCardPicker;
    RandomItemPicker<CardData> uniqueCardPicker;

    //Relics
    private List<RelicData> commonRelicList = new List<RelicData>();
    private List<RelicData> rareRelicList = new List<RelicData>();
    private List<RelicData> uniqueRelicList = new List<RelicData>();
    private List<RelicData> bossRelicList = new List<RelicData>();

    RandomItemPicker<RelicData> commonRelicPicker;
    RandomItemPicker<RelicData> rareRelicPicker;
    RandomItemPicker<RelicData> uniqueRelicPicker;
    RandomItemPicker<RelicData> bossRelicPicker;

    public void SubscribeToPlayerEvents()
    {
        // TODO: 차후 보상 풀 자체를 유지하는 방식(Push) 대신, 보상 획득 시점에 
        // RunManager.instance.player.relics를 참조하여 동적으로 획득 가능 유물만 추려내는(Pull) 방식으로 변경 권장.
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            GameSceneManager.instance.player.eventBus.Add<IOnAddRelic>(this);
            GameSceneManager.instance.player.eventBus.Add<IOnRemoveRelic>(this);
        }
    }

    public void OnAddRelic(RelicData relicData)
    {
        // TODO: 동적 추출(Pull) 방식으로 변경 시 이 이벤트 콜백은 삭제할 수 있습니다.
        EraseRelicFromList(relicData);
    }

    public void OnRemoveRelic(RelicData relicData)
    {
        // TODO: 동적 추출(Pull) 방식으로 변경 시 이 이벤트 콜백은 삭제할 수 있습니다.
        AddRelicFromList(relicData);
    }

    public void ResetCardLists()
    {
        //Card Picker 초기화
        commonCardPicker = new RandomItemPicker<CardData>(commonCardList);
        uncommonCardPicker = new RandomItemPicker<CardData>(uncommonCardList);
        rareCardPicker = new RandomItemPicker<CardData>(rareCardList);
        uniqueCardPicker = new RandomItemPicker<CardData>(uniqueCardList);
    }

    public void ResetRelicLists()
    {
        //Relic Picker 초기화
        commonRelicPicker = new RandomItemPicker<RelicData>(commonRelicList);
        rareRelicPicker = new RandomItemPicker<RelicData>(rareRelicList);
        uniqueRelicPicker = new RandomItemPicker<RelicData>(uniqueRelicList);
        bossRelicPicker = new RandomItemPicker<RelicData>(bossRelicList);
    }

    public void UpdateItemList()
    {
        foreach(CardData data in ModLoader.Instance.CardDatabase.Values)
        {
            if(data.isUpgraded)
            {
                continue; //업그레이드 카드들은 보상으로 등장하지 않음
            }

            switch (data.rarity)
            {
                case CardRarity.Common:
                    commonCardList.Add(data);
                    break;
                case CardRarity.Uncommon:
                    uncommonCardList.Add(data);
                    break;
                case CardRarity.Rare:
                    rareCardList.Add(data);
                    break;
                case CardRarity.Unique:
                    uniqueCardList.Add(data);
                    break;
            }
        }

        foreach (RelicData data in ModLoader.Instance.RelicDatabase.Values)
        {
            switch (data.rarity)
            {
                case GameItem.Types.RelicRarity.Common:
                    commonRelicList.Add(data);
                    break;
                case GameItem.Types.RelicRarity.Rare:
                    rareRelicList.Add(data);
                    break;
                case GameItem.Types.RelicRarity.Unique:
                    uniqueRelicList.Add(data);
                    break;
                case GameItem.Types.RelicRarity.Boss:
                    bossRelicList.Add(data);
                    break;
            }
        }

        //Card Picker 초기화
        commonCardPicker = new RandomItemPicker<CardData>(commonCardList);
        uncommonCardPicker = new RandomItemPicker<CardData>(uncommonCardList);
        rareCardPicker = new RandomItemPicker<CardData>(rareCardList);
        uniqueCardPicker = new RandomItemPicker<CardData>(uniqueCardList);

        //Relic Picker 초기화
        commonRelicPicker = new RandomItemPicker<RelicData>(commonRelicList);
        rareRelicPicker = new RandomItemPicker<RelicData>(rareRelicList);
        uniqueRelicPicker = new RandomItemPicker<RelicData>(uniqueRelicList);
        bossRelicPicker = new RandomItemPicker<RelicData>(bossRelicList);
    }

    public void EraseRelicFromList(RelicData relicData)
    {
        switch (relicData.rarity)
        {
            case GameItem.Types.RelicRarity.Common:
                Debug.Log("Removing common Relic: " + relicData.relicName + ", Count: "+ commonRelicList.Count);
                foreach (RelicData data in commonRelicList)
                {
                    if (data.relicName == relicData.relicName)
                    {
                        commonRelicList.Remove(data);
                        break;
                    }
                }
                commonRelicPicker = new RandomItemPicker<RelicData>(commonRelicList);
                Debug.Log("commonRelicPicker Count: " + commonRelicPicker.Count());
                break;
            case GameItem.Types.RelicRarity.Rare:
                Debug.Log("Removing rare Relic: " + relicData.relicName + ", Count: " + rareRelicList.Count);
                foreach (RelicData data in rareRelicList)
                {
                    if (data.relicName == relicData.relicName)
                    {
                        rareRelicList.Remove(data);
                        break;
                    }
                }
                rareRelicPicker = new RandomItemPicker<RelicData>(rareRelicList);
                Debug.Log("rareRelicPicker Count: " + rareRelicPicker.Count());
                break;
            case GameItem.Types.RelicRarity.Unique:
                Debug.Log("Removing unique Relic: " + relicData.relicName + ", Count: " + uniqueRelicList.Count);
                foreach (RelicData data in uniqueRelicList)
                {
                    if (data.relicName == relicData.relicName)
                    {
                        uniqueRelicList.Remove(data);
                        break;
                    }
                }
                uniqueRelicPicker = new RandomItemPicker<RelicData>(uniqueRelicList);
                Debug.Log("uniqueRelicPicker Count: " + uniqueRelicPicker.Count());
                break;
            case GameItem.Types.RelicRarity.Boss:
                Debug.Log("Removing boss Relic: " + relicData.relicName + ", Count: " + bossRelicList.Count);
                foreach (RelicData data in bossRelicList)
                {
                    if (data.relicName == relicData.relicName)
                    {
                        bossRelicList.Remove(data);
                        break;
                    }
                }
                bossRelicPicker = new RandomItemPicker<RelicData>(bossRelicList);
                Debug.Log("bossRelicPicker Count: " + bossRelicPicker.Count());
                break;
        }
        Debug.Log("Relic Removed from List: " + relicData.relicName);
    }

    public void AddRelicFromList(RelicData relicData)
    {
        switch (relicData.rarity)
        {
            case GameItem.Types.RelicRarity.Common:
                if (commonRelicList.Contains(relicData))
                    return;

                commonRelicList.Add(relicData);
                commonRelicPicker = new RandomItemPicker<RelicData>(commonRelicList);
                break;
            case GameItem.Types.RelicRarity.Rare:
                if (rareRelicList.Contains(relicData))
                    return;

                rareRelicList.Add(relicData);
                rareRelicPicker = new RandomItemPicker<RelicData>(rareRelicList);
                break;
            case GameItem.Types.RelicRarity.Unique:
                if (uniqueRelicList.Contains(relicData))
                    return;

                uniqueRelicList.Add(relicData);
                uniqueRelicPicker = new RandomItemPicker<RelicData>(uniqueRelicList);
                break;
            case GameItem.Types.RelicRarity.Boss:
                if (bossRelicList.Contains(relicData))
                    return;

                bossRelicList.Add(relicData);
                bossRelicPicker = new RandomItemPicker<RelicData>(bossRelicList);
                break;
        }
        Debug.Log("Relic Added to List: " + relicData.relicName);
    }

    public CardData GetRandomCardDataByPickupType(RandomCardPickupType pickupType)
    {
        //랜덤한 숫자 선택
        int pickNumber = Random.Range(1, 101);

        CardData cardData = null;

        int commonRate = 70;
        int uncommonRate = 0;
        int rareRate = 25;
        int uniqueRate = 5;

        //픽업 타입에 따른 확률 조정
        switch (pickupType)
        {
            case RandomCardPickupType.Common:
                commonRate = 100;
                uncommonRate = 0;
                rareRate = 0;
                uniqueRate = 0;
                break;
            case RandomCardPickupType.Uncommon:
                commonRate = 0;
                uncommonRate = 100;
                rareRate = 0;
                uniqueRate = 0;
                break;
            case RandomCardPickupType.Rare:
                commonRate = 0;
                uncommonRate = 0;
                rareRate = 100;
                uniqueRate = 0;
                break;
            case RandomCardPickupType.Unique:
                commonRate = 0;
                uncommonRate = 0;
                rareRate = 0;
                uniqueRate = 100;
                break;
            case RandomCardPickupType.CommonToUncommon:
                commonRate = 60;
                uncommonRate = 40;
                rareRate = 0;
                uniqueRate = 0;
                break;
            case RandomCardPickupType.RareToUnique:
                commonRate = 0;
                uncommonRate = 0;
                rareRate = 80;
                uniqueRate = 20;
                break;
        }

        if (pickNumber <= uniqueRate)
        {
            //unique카드들 중 랜덤한 1장 선택
            if (uniqueCardPicker.TryGetNext(out CardData data))
            {
                cardData = data;
            }
            else
            {
                uniqueCardPicker.Reset();
                if (uniqueCardPicker.TryGetNext(out CardData newData))
                {
                    cardData = newData;
                }
            }
        }
        else if (pickNumber > uniqueRate && pickNumber <= uniqueRate + rareRate)
        {
            //rare카드들 중 랜덤한 1장 선택
            if (rareCardPicker.TryGetNext(out CardData data))
            {
                cardData = data;
            }
            else
            {
                rareCardPicker.Reset();
                if (rareCardPicker.TryGetNext(out CardData newData))
                {
                    cardData = newData;
                }
            }
        }
        else if (pickNumber > uniqueRate + rareRate && pickNumber <= uniqueRate + rareRate + uncommonRate)
        {
            //uncommon카드들 중 랜덤한 1장 선택
            if (uncommonCardPicker.TryGetNext(out CardData data))
            {
                cardData = data;
            }
            else
            {
                uncommonCardPicker.Reset();
                if (uncommonCardPicker.TryGetNext(out CardData newData))
                {
                    cardData = newData;
                }
            }
        }
        else
        {
            //common카드들 중 랜덤한 1장 선택
            if (commonCardPicker.TryGetNext(out CardData data))
            {
                cardData = data;
            }
            else
            {
                commonCardPicker.Reset();
                if (commonCardPicker.TryGetNext(out CardData newData))
                {
                    cardData = newData;
                }
            }
        }

        return cardData;
    }

    public RelicData GetRandomRelicDataByPickupType(RandomRelicPickupType pickupType)
    {
        //랜덤한 유물 선택 후 등록
        int pickNumber = Random.Range(1, 101);

        RelicData relicData = new RelicData();

        int commonRate = 70;
        int rareRate = 30;
        int uniqueRate = 0;

        //픽업 타입에 따른 확률 조정
        switch (pickupType)
        {
            case RandomRelicPickupType.Common:
                commonRate = 100;
                rareRate = 0;
                uniqueRate = 0;
                break;
            case RandomRelicPickupType.Rare:
                commonRate = 0;
                rareRate = 100;
                uniqueRate = 0;
                break;
            case RandomRelicPickupType.Unique:
                commonRate = 0;
                rareRate = 0;
                uniqueRate = 100;
                break;
            case RandomRelicPickupType.CommonToUnique:
                commonRate = 60;
                rareRate = 30;
                uniqueRate = 10;
                break;
            case RandomRelicPickupType.CommonToRare:
                commonRate = 70;
                rareRate = 30;
                uniqueRate = 0;
                break;
            case RandomRelicPickupType.RareToUnique:
                commonRate = 0;
                rareRate = 80;
                uniqueRate = 20;
                break;
            case RandomRelicPickupType.Boss:
                //보스 유물은 무조건 보스 유물 중에서 선택
                if (bossRelicPicker.TryGetNext(out RelicData data))
                {
                    relicData = data;
                }
                else
                {
                    bossRelicPicker.Reset();
                    if (bossRelicPicker.TryGetNext(out RelicData newData))
                    {
                        relicData = newData;
                    }
                }
                return relicData;
        }

        if (pickNumber <= uniqueRate)
        {
            //unique유물들 중 랜덤한 1장 선택
            if (uniqueRelicPicker.TryGetNext(out RelicData data))
            {
                relicData = data;
            }
            else
            {
                uniqueRelicPicker.Reset();
                if (uniqueRelicPicker.TryGetNext(out RelicData newData))
                {
                    relicData = newData;
                }
            }
        }
        else if (pickNumber > uniqueRate && pickNumber <= uniqueRate + rareRate)
        {
            //rare유물들 중 랜덤한 1장 선택
            if (rareRelicPicker.TryGetNext(out RelicData data))
            {
                relicData = data;
            }
            else
            {
                rareRelicPicker.Reset();
                if (rareRelicPicker.TryGetNext(out RelicData newData))
                {
                    relicData = newData;
                }
            }
        }
        else
        {
            //common유물들 중 랜덤한 1장 선택
            if (commonRelicPicker.TryGetNext(out RelicData data))
            {
                relicData = data;
            }
            else
            {
                commonRelicPicker.Reset();
                if (commonRelicPicker.TryGetNext(out RelicData newData))
                {
                    relicData = newData;
                }
            }
        }

        return relicData;
    }

    public void RemoveObtainedRelicByPlayer(RelicData relicData)
    {
        switch(relicData.rarity)
        {
            case GameItem.Types.RelicRarity.Common:
                commonRelicList.Remove(relicData);
                commonRelicPicker = new RandomItemPicker<RelicData>(commonRelicList);
                break;
            case GameItem.Types.RelicRarity.Rare:
                rareRelicList.Remove(relicData);
                rareRelicPicker = new RandomItemPicker<RelicData>(rareRelicList);
                break;
            case GameItem.Types.RelicRarity.Unique:
                uniqueRelicList.Remove(relicData);
                uniqueRelicPicker = new RandomItemPicker<RelicData>(uniqueRelicList);
                break;
        }
    }

    public CardData GetRandomCardDataByDropTable(string dropTableID)
    {
        DropTableData dropTable = null;
        if (ModLoader.Instance != null)
        {
            ModLoader.Instance.DropTableDatabase.TryGetValue(dropTableID, out dropTable);
        }

        if (dropTable != null && dropTable.specificItemIDs != null && dropTable.specificItemIDs.Count > 0)
        {
            string randomName = dropTable.specificItemIDs[Random.Range(0, dropTable.specificItemIDs.Count)];
            if (ModLoader.Instance.CardDatabase.TryGetValue(randomName, out CardData specificCard))
            {
                return specificCard;
            }
        }

        // 필터링 적용
        List<CardData> filteredCommon = new List<CardData>();
        List<CardData> filteredUncommon = new List<CardData>();
        List<CardData> filteredRare = new List<CardData>();
        List<CardData> filteredUnique = new List<CardData>();

        if (ModLoader.Instance != null)
        {
            foreach (var card in ModLoader.Instance.CardDatabase.Values)
            {
                if (card.isUpgraded) continue;

                // 1. 제외 카드 필터
                if (dropTable != null && dropTable.excludedItemIDs != null && dropTable.excludedItemIDs.Contains(card.cardName))
                {
                    continue;
                }

                // 2. 직업 카드 필터
                if (dropTable != null && dropTable.allowedClassTypes != null && dropTable.allowedClassTypes.Count > 0)
                {
                    bool classMatched = false;
                    if (card.classTypes != null)
                    {
                        foreach (var cls in card.classTypes)
                        {
                            if (dropTable.allowedClassTypes.Contains(cls))
                            {
                                classMatched = true;
                                break;
                            }
                        }
                    }
                    if (!classMatched) continue;
                }

                // 3. 카드 타입 필터
                if (dropTable != null && dropTable.allowedCardTypes != null && dropTable.allowedCardTypes.Count > 0)
                {
                    if (string.IsNullOrEmpty(card.cardType) || !dropTable.allowedCardTypes.Contains(card.cardType))
                    {
                        continue;
                    }
                }

                // 등급별 리스트에 분류
                switch (card.rarity)
                {
                    case CardRarity.Common:
                        filteredCommon.Add(card);
                        break;
                    case CardRarity.Uncommon:
                        filteredUncommon.Add(card);
                        break;
                    case CardRarity.Rare:
                        filteredRare.Add(card);
                        break;
                    case CardRarity.Unique:
                        filteredUnique.Add(card);
                        break;
                }
            }
        }

        // 가중치 결정
        int commonRate = 60;
        int uncommonRate = 40;
        int rareRate = 0;
        int uniqueRate = 0;

        if (dropTable != null)
        {
            commonRate = dropTable.commonWeight;
            uncommonRate = dropTable.uncommonWeight;
            rareRate = dropTable.rareWeight;
            uniqueRate = dropTable.uniqueWeight;
        }

        int totalWeight = commonRate + uncommonRate + rareRate + uniqueRate;
        if (totalWeight <= 0) totalWeight = 100;

        int pickNumber = Random.Range(1, totalWeight + 1);
        CardData cardData = null;

        if (pickNumber <= uniqueRate)
        {
            cardData = PickRandomFromList(filteredUnique);
            if (cardData == null) cardData = PickRandomFromList(filteredRare);
            if (cardData == null) cardData = PickRandomFromList(filteredUncommon);
            if (cardData == null) cardData = PickRandomFromList(filteredCommon);
        }
        else if (pickNumber > uniqueRate && pickNumber <= uniqueRate + rareRate)
        {
            cardData = PickRandomFromList(filteredRare);
            if (cardData == null) cardData = PickRandomFromList(filteredUnique);
            if (cardData == null) cardData = PickRandomFromList(filteredUncommon);
            if (cardData == null) cardData = PickRandomFromList(filteredCommon);
        }
        else if (pickNumber > uniqueRate + rareRate && pickNumber <= uniqueRate + rareRate + uncommonRate)
        {
            cardData = PickRandomFromList(filteredUncommon);
            if (cardData == null) cardData = PickRandomFromList(filteredCommon);
            if (cardData == null) cardData = PickRandomFromList(filteredRare);
            if (cardData == null) cardData = PickRandomFromList(filteredUnique);
        }
        else
        {
            cardData = PickRandomFromList(filteredCommon);
            if (cardData == null) cardData = PickRandomFromList(filteredUncommon);
            if (cardData == null) cardData = PickRandomFromList(filteredRare);
            if (cardData == null) cardData = PickRandomFromList(filteredUnique);
        }

        // 만약 필터링 조건 때문에 아무 카드도 뽑지 못했다면 폴백으로 글로벌 picker에서 선택
        if (cardData == null)
        {
            if (pickNumber <= uniqueRate)
            {
                if (uniqueCardPicker.TryGetNext(out CardData data)) cardData = data;
                else { uniqueCardPicker.Reset(); if (uniqueCardPicker.TryGetNext(out CardData newData)) cardData = newData; }
            }
            else if (pickNumber > uniqueRate && pickNumber <= uniqueRate + rareRate)
            {
                if (rareCardPicker.TryGetNext(out CardData data)) cardData = data;
                else { rareCardPicker.Reset(); if (rareCardPicker.TryGetNext(out CardData newData)) cardData = newData; }
            }
            else if (pickNumber > uniqueRate + rareRate && pickNumber <= uniqueRate + rareRate + uncommonRate)
            {
                if (uncommonCardPicker.TryGetNext(out CardData data)) cardData = data;
                else { uncommonCardPicker.Reset(); if (uncommonCardPicker.TryGetNext(out CardData newData)) cardData = newData; }
            }
            else
            {
                if (commonCardPicker.TryGetNext(out CardData data)) cardData = data;
                else { commonCardPicker.Reset(); if (commonCardPicker.TryGetNext(out CardData newData)) cardData = newData; }
            }
        }

        return cardData;
    }

    public RelicData GetRandomRelicDataByDropTable(string dropTableID)
    {
        DropTableData dropTable = null;
        if (ModLoader.Instance != null)
        {
            ModLoader.Instance.DropTableDatabase.TryGetValue(dropTableID, out dropTable);
        }

        if (dropTable != null && dropTable.specificItemIDs != null && dropTable.specificItemIDs.Count > 0)
        {
            string randomName = dropTable.specificItemIDs[Random.Range(0, dropTable.specificItemIDs.Count)];
            if (ModLoader.Instance.RelicDatabase.TryGetValue(randomName, out RelicData specificRelic))
            {
                return specificRelic;
            }
        }

        // 필터링 적용
        List<RelicData> filteredCommon = new List<RelicData>();
        List<RelicData> filteredRare = new List<RelicData>();
        List<RelicData> filteredUnique = new List<RelicData>();
        List<RelicData> filteredBoss = new List<RelicData>();

        if (ModLoader.Instance != null)
        {
            foreach (var relic in ModLoader.Instance.RelicDatabase.Values)
            {
                // 1. 제외 유물 필터
                if (dropTable != null && dropTable.excludedItemIDs != null && dropTable.excludedItemIDs.Contains(relic.relicName))
                {
                    continue;
                }

                // 플레이어가 소유한 유물 제외
                if (GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.relicManager != null)
                {
                    bool alreadyOwned = false;
                    foreach (var ownedRelic in GameSceneManager.instance.player.relicManager.GetRelics())
                    {
                        if (ownedRelic.Data != null && ownedRelic.Data.relicName == relic.relicName)
                        {
                            alreadyOwned = true;
                            break;
                        }
                    }
                    if (alreadyOwned) continue;
                }

                // 2. 직업 유물 필터
                if (dropTable != null && dropTable.allowedClassTypes != null && dropTable.allowedClassTypes.Count > 0)
                {
                    bool classMatched = false;
                    if (relic.classTypes != null)
                    {
                        foreach (var cls in relic.classTypes)
                        {
                            if (dropTable.allowedClassTypes.Contains(cls))
                            {
                                classMatched = true;
                                break;
                            }
                        }
                    }
                    if (!classMatched) continue;
                }

                // 등급별 리스트에 분류
                switch (relic.rarity)
                {
                    case GameItem.Types.RelicRarity.Common:
                        filteredCommon.Add(relic);
                        break;
                    case GameItem.Types.RelicRarity.Rare:
                        filteredRare.Add(relic);
                        break;
                    case GameItem.Types.RelicRarity.Unique:
                        filteredUnique.Add(relic);
                        break;
                    case GameItem.Types.RelicRarity.Boss:
                        filteredBoss.Add(relic);
                        break;
                }
            }
        }

        // 가중치 결정
        int commonRate = 70;
        int rareRate = 30;
        int uniqueRate = 0;
        int bossRate = 0;

        if (dropTable != null)
        {
            commonRate = dropTable.commonWeight;
            rareRate = dropTable.rareWeight;
            uniqueRate = dropTable.uniqueWeight;
            bossRate = dropTable.uncommonWeight; // Relic의 경우 uncommonWeight를 Boss 가중치로 맵핑
        }

        int totalWeight = commonRate + rareRate + uniqueRate + bossRate;
        if (totalWeight <= 0) totalWeight = 100;

        int pickNumber = Random.Range(1, totalWeight + 1);
        RelicData relicData = null;

        if (pickNumber <= bossRate)
        {
            relicData = PickRandomFromList(filteredBoss);
            if (relicData == null) relicData = PickRandomFromList(filteredUnique);
            if (relicData == null) relicData = PickRandomFromList(filteredRare);
            if (relicData == null) relicData = PickRandomFromList(filteredCommon);
        }
        else if (pickNumber > bossRate && pickNumber <= bossRate + uniqueRate)
        {
            relicData = PickRandomFromList(filteredUnique);
            if (relicData == null) relicData = PickRandomFromList(filteredRare);
            if (relicData == null) relicData = PickRandomFromList(filteredCommon);
            if (relicData == null) relicData = PickRandomFromList(filteredBoss);
        }
        else if (pickNumber > bossRate + uniqueRate && pickNumber <= bossRate + uniqueRate + rareRate)
        {
            relicData = PickRandomFromList(filteredRare);
            if (relicData == null) relicData = PickRandomFromList(filteredUnique);
            if (relicData == null) relicData = PickRandomFromList(filteredCommon);
            if (relicData == null) relicData = PickRandomFromList(filteredBoss);
        }
        else
        {
            relicData = PickRandomFromList(filteredCommon);
            if (relicData == null) relicData = PickRandomFromList(filteredRare);
            if (relicData == null) relicData = PickRandomFromList(filteredUnique);
            if (relicData == null) relicData = PickRandomFromList(filteredBoss);
        }

        // 폴백
        if (relicData == null)
        {
            if (pickNumber <= bossRate)
            {
                if (bossRelicPicker.TryGetNext(out RelicData data)) relicData = data;
                else { bossRelicPicker.Reset(); if (bossRelicPicker.TryGetNext(out RelicData newData)) relicData = newData; }
            }
            else if (pickNumber > bossRate && pickNumber <= bossRate + uniqueRate)
            {
                if (uniqueRelicPicker.TryGetNext(out RelicData data)) relicData = data;
                else { uniqueRelicPicker.Reset(); if (uniqueRelicPicker.TryGetNext(out RelicData newData)) relicData = newData; }
            }
            else if (pickNumber > bossRate + uniqueRate && pickNumber <= bossRate + uniqueRate + rareRate)
            {
                if (rareRelicPicker.TryGetNext(out RelicData data)) relicData = data;
                else { rareRelicPicker.Reset(); if (rareRelicPicker.TryGetNext(out RelicData newData)) relicData = newData; }
            }
            else
            {
                if (commonRelicPicker.TryGetNext(out RelicData data)) relicData = data;
                else { commonRelicPicker.Reset(); if (commonRelicPicker.TryGetNext(out RelicData newData)) relicData = newData; }
            }
        }

        return relicData;
    }

    private T PickRandomFromList<T>(List<T> list) where T : class
    {
        if (list == null || list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    public void InstantiateItemReward(RewardItemType rewardType, string dropTableID, Vector3 spawnPosition)
    {
        if (rewardBoxPrefab == null)
            return;

        RewardChest newRewardBox = Instantiate(rewardBoxPrefab, spawnPosition, Quaternion.identity).GetComponent<RewardChest>();

        if (newRewardBox != null)
        {
            RewardData tempReward = new RewardData();
            RewardItemConfig config = new RewardItemConfig();
            config.rewardType = (rewardType == RewardItemType.Card) ? RewardType.CardChoice : RewardType.Relic;
            config.minValue = 1;
            config.maxValue = 1;
            config.referenceID = dropTableID;
            tempReward.rewards.Add(config);

            newRewardBox.SetRewardData(tempReward);
        }

        //맵 이동 시 지워질 오브젝트 목록으로 등록
        if (newRewardBox != null)
        {
            RunManager.instance.currentMap.currentSpawnNPCList.Add(newRewardBox.gameObject);
            RunManager.instance.currentMap.currentSpawnUIList.Add(newRewardBox.GetRewardListUI());
        }
    }

    public void SpawnRewardBox(Vector3 spawnPosition)
    {
        if (RunManager.instance.currentIncountNode == null)
        {
            return;
        }

        RewardChest newRewardBox = Instantiate(rewardBoxPrefab, spawnPosition, Quaternion.identity).GetComponent<RewardChest>();

        if (newRewardBox != null)
        {
            newRewardBox.SetRewardItemList();
        }

        //맵 이동 시 지워질 오브젝트 목록으로 등록
        if (newRewardBox != null)
        {
            RunManager.instance.currentMap.currentSpawnNPCList.Add(newRewardBox.gameObject);
            RunManager.instance.currentMap.currentSpawnUIList.Add(newRewardBox.GetRewardListUI());
        }
    }

    public List<CardData> GetCommonCardList() { return commonCardList; }
    public List<CardData> GetRareCardList() { return rareCardList; }
    public List<CardData> GetUniqueCardList() { return uniqueCardList; }

    public List<RelicData> GetCommonRelicList() { return commonRelicList; }
    public List<RelicData> GetRareRelicList() { return rareRelicList; }
    public List<RelicData> GetUniqueRelicList() { return uniqueRelicList; }
}
