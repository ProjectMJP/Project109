using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class MasteryUpgradeSaveEntry
{
    public string masteryId;
    public int count;
}

[Serializable]
public class CardSaveEntry
{
    public string cardName;
    public int masteryLevel;
    public List<MasteryUpgradeSaveEntry> masteryUpgrades = new List<MasteryUpgradeSaveEntry>();
    public float currentMasteryXP;
}

[Serializable]
public class RelicSaveEntry
{
    public string relicName;
    public int counter;
}

[Serializable]
public class ActiveShopItemSaveEntry
{
    public string itemId;
    public int basePrice;
    public bool isSold;
    public bool isOnSale;
    public float discountRate;
}

[Serializable]
public class ActiveShopSaveData
{
    public bool isValid;
    public int nodeFloor = -1;
    public int nodeIndex = -1;
    public int visitRerollCount;
    public List<ActiveShopItemSaveEntry> cards = new List<ActiveShopItemSaveEntry>();
    public List<ActiveShopItemSaveEntry> relics = new List<ActiveShopItemSaveEntry>();

    public ActiveShopSaveData Clone()
    {
        ActiveShopSaveData copy = new ActiveShopSaveData
        {
            isValid = isValid,
            nodeFloor = nodeFloor,
            nodeIndex = nodeIndex,
            visitRerollCount = visitRerollCount
        };

        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                ActiveShopItemSaveEntry c = cards[i];
                if (c == null) continue;
                copy.cards.Add(new ActiveShopItemSaveEntry
                {
                    itemId = c.itemId,
                    basePrice = c.basePrice,
                    isSold = c.isSold,
                    isOnSale = c.isOnSale,
                    discountRate = c.discountRate
                });
            }
        }

        if (relics != null)
        {
            for (int i = 0; i < relics.Count; i++)
            {
                ActiveShopItemSaveEntry r = relics[i];
                if (r == null) continue;
                copy.relics.Add(new ActiveShopItemSaveEntry
                {
                    itemId = r.itemId,
                    basePrice = r.basePrice,
                    isSold = r.isSold,
                    isOnSale = r.isOnSale,
                    discountRate = r.discountRate
                });
            }
        }

        return copy;
    }
}

[Serializable]
public class RunSaveData
{
    public string currentMapName;
    public int currentStageLevel;
    public int currentExploreMapFloor;

    // 현재 및 이전 노드 위치 좌표 (인덱스)
    public int currentExploreNodeFloor = -1;
    public int currentExploreNodeIndex = -1;
    public int beforeExploreNodeFloor = -1;
    public int beforeExploreNodeIndex = -1;

    public ExploreMapData exploreMapData; // 신규 맵 데이터 구조 저장용

    public int playerGold;
    public int playerMemorySharp;
    public int playerShopRerollCount;

    public float playerCurHealth;
    public float playerMaxHealth;
    public CharacterStat playerCharacterStat; // 플레이어 전체 스탯 데이터 (영구 업그레이드 포함)

    public bool isInDungeon; // 현재 던전 진행 중인지 여부 (은신처/던전 판정용)

    public List<CardSaveEntry> playerDeckCards = new List<CardSaveEntry>();
    public List<string> playerRelicNames = new List<string>();
    public List<RelicSaveEntry> playerRelicStates = new List<RelicSaveEntry>();
    public List<PendingRewardEntry> pendingRewards = new List<PendingRewardEntry>();
    public ActiveShopSaveData activeShopState;

    // 시작 무장(진열대) 선택 및 해금 상태 저장용
    public string selectedStarterKitId;
    public List<string> unlockedStarterKitIds = new List<string>();
    public List<string> unlockedLoreKeys = new List<string>();
}

/// <summary>
/// JSON 파일을 기반으로 게임 세션을 로컬 저장소에 저장하고 불러오는 시스템입니다.
/// </summary>
public static class SaveSystem
{
    // 슬롯별 세이브 JSON 텍스트 메모리 캐시 (중복 디스크 I/O 제거 및 공유 객체 인메모리 오염 차단)
    private static readonly Dictionary<int, string> _saveJsonCache = new Dictionary<int, string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticCache()
    {
        _saveJsonCache.Clear();
    }

    /// <summary>
    /// 특정 슬롯의 캐시된 세이브 데이터를 무효화합니다.
    /// </summary>
    public static void InvalidateCache(int slotIndex)
    {
        _saveJsonCache.Remove(slotIndex);
    }

    /// <summary>
    /// 모든 슬롯의 캐시된 세이브 데이터를 무효화합니다.
    /// </summary>
    public static void InvalidateAllCaches()
    {
        _saveJsonCache.Clear();
    }

    private static string GetSavePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"save_{slotIndex}.json");
    }

    /// <summary>
    /// 특정 슬롯에 현재 세션 상태를 저장합니다.
    /// </summary>
    public static void SaveGame(RunManager runManager, int slotIndex)
    {
        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player == null)
        {
            Debug.LogError("[SaveSystem] GameSceneManager에 플레이어 정보가 없어 저장할 수 없습니다.");
            return;
        }

        try
        {
            RunSaveData data = new RunSaveData();
            data.isInDungeon = GameSceneManager.instance != null && GameSceneManager.instance.isInDungeon;

            if (data.isInDungeon && runManager != null)
            {
                string resolvedMapName = runManager.currentMapName;
                if (string.IsNullOrEmpty(resolvedMapName))
                {
                    if (runManager.currentStage != null && runManager.currentStage.currentMapData != null)
                    {
                        resolvedMapName = runManager.currentStage.currentMapData.locationType.ToString();
                    }
                    else if (runManager.currentExploreNode != null && runManager.currentExploreNode.encounterData != null)
                    {
                        resolvedMapName = runManager.currentExploreNode.encounterData.locationType.ToString();
                    }
                    else
                    {
                        resolvedMapName = "Temple";
                    }
                }

                data.currentMapName = resolvedMapName;
                data.currentStageLevel = runManager.currentStageLevel > 0 ? runManager.currentStageLevel : 1;
                data.currentExploreMapFloor = runManager.currentExploreMapFloor;

                if (runManager.exploreMapManager != null)
                {
                    data.exploreMapData = runManager.exploreMapManager.MapData;

                    if (runManager.currentExploreNode != null)
                    {
                        data.currentExploreNodeFloor = runManager.currentExploreNode.floor;
                        data.currentExploreNodeIndex = runManager.currentExploreNode.index;
                    }

                    if (runManager.beforeExploreNode != null)
                    {
                        data.beforeExploreNodeFloor = runManager.beforeExploreNode.floor;
                        data.beforeExploreNodeIndex = runManager.beforeExploreNode.index;
                    }
                }

                data.pendingRewards = runManager.ExportPendingRewardsForSave();
                data.activeShopState = runManager.ExportActiveShopStateForSave();
            }
            else
            {
                // 은신처(비던전) 상태인 경우 런 진행도 데이터 초기화 저장
                data.currentMapName = "Temple";
                data.currentStageLevel = 1;
                data.currentExploreMapFloor = 0;
                data.exploreMapData = null;
                data.currentExploreNodeFloor = -1;
                data.currentExploreNodeIndex = -1;
                data.beforeExploreNodeFloor = -1;
                data.beforeExploreNodeIndex = -1;
                data.pendingRewards = new List<PendingRewardEntry>();
                data.activeShopState = null;
            }


            data.playerGold = player.playerStat != null ? player.playerStat.InGameCurrencyGold : 0;
            data.playerMemorySharp = player.playerStat != null ? player.playerStat.InGameCurrencyMemorySharp : 0;
            data.playerShopRerollCount = (data.isInDungeon && player.playerStat != null)
                ? Mathf.Max(0, player.playerStat.ShopRerollCount)
                : 0;

            if (player.character != null)
            {
                CharacterStat permanentStat = player.character.baseCharacterStat ?? player.character.curCharacterStat;
                data.playerCurHealth = player.character.curHealth;
                data.playerMaxHealth = permanentStat != null ? permanentStat.maxHealth : 100f;
                data.playerCharacterStat = permanentStat;
            }

            // 플레이어 덱 저장
            if (player.deck != null)
            {
                foreach (var card in player.deck.GetCards())
                {
                    if (card == null || card.cardData == null) continue;

                    CardSaveEntry entry = new CardSaveEntry();
                    entry.cardName = card.cardData.cardName;
                    entry.masteryLevel = card.masteryLevel;
                    entry.currentMasteryXP = card.currentMasteryXP;

                    entry.masteryUpgrades = new List<MasteryUpgradeSaveEntry>();
                    if (card.masteryUpgrades != null)
                    {
                        foreach (var kvp in card.masteryUpgrades)
                        {
                            entry.masteryUpgrades.Add(new MasteryUpgradeSaveEntry { masteryId = kvp.Key, count = kvp.Value });
                        }
                    }
                    data.playerDeckCards.Add(entry);
                }
            }

            // 플레이어 유물 및 카운터 상태 저장
            if (player.relicManager != null)
            {
                foreach (var relic in player.relicManager.GetRelics())
                {
                    if (relic == null || relic.Data == null) continue;
                    data.playerRelicNames.Add(relic.Data.relicName);
                    data.playerRelicStates.Add(new RelicSaveEntry
                    {
                        relicName = relic.Data.relicName,
                        counter = relic.Counter
                    });
                }
            }

            data.selectedStarterKitId = player.selectedStarterKitId;
            data.unlockedStarterKitIds = new List<string>(player.unlockedStarterKitIds);
            data.unlockedLoreKeys = new List<string>(player.unlockedLoreKeys);

            string json = JsonUtility.ToJson(data, true);
            string savePath = GetSavePath(slotIndex);
            File.WriteAllText(savePath, json, System.Text.Encoding.UTF8);
            _saveJsonCache[slotIndex] = json;
            Debug.Log($"[SaveSystem] 게임 상태가 '{savePath}'에 정상적으로 저장되었습니다.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 저장 중 예외 발생: {e.Message}");
        }
    }

    /// <summary>
    /// 특정 슬롯에 세이브 파일이 존재하는지 검사합니다.
    /// </summary>
    public static bool HasSaveData(int slotIndex)
    {
        bool exists = File.Exists(GetSavePath(slotIndex));
        if (!exists)
        {
            _saveJsonCache.Remove(slotIndex);
        }
        return exists;
    }

    /// <summary>
    /// 특정 슬롯 저장소로부터 데이터를 조회합니다.
    /// 캐시된 JSON 텍스트로부터 매번 독립된 인스턴스를 역직렬화하여 공유 참조 인메모리 오염을 방지합니다.
    /// </summary>
    public static bool TryLoadGameData(int slotIndex, out RunSaveData data, bool forceReload = false)
    {
        if (!forceReload && _saveJsonCache.TryGetValue(slotIndex, out string cachedJson) && !string.IsNullOrEmpty(cachedJson))
        {
            data = JsonUtility.FromJson<RunSaveData>(cachedJson);
            if (data != null)
            {
                return true;
            }
        }

        string savePath = GetSavePath(slotIndex);
        if (!File.Exists(savePath))
        {
            _saveJsonCache.Remove(slotIndex);
            data = null;
            return false;
        }

        try
        {
            string json = File.ReadAllText(savePath, System.Text.Encoding.UTF8);
            data = JsonUtility.FromJson<RunSaveData>(json);
            if (data != null)
            {
                _saveJsonCache[slotIndex] = json;
                return true;
            }

            _saveJsonCache.Remove(slotIndex);
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 로드 중 예외 발생: {e.Message}");
            _saveJsonCache.Remove(slotIndex);
            data = null;
            return false;
        }
    }

    /// <summary>
    /// 특정 슬롯 저장소로부터 데이터를 읽어 RunSaveData 객체로 역직렬화합니다.
    /// </summary>
    public static RunSaveData LoadGameData(int slotIndex, bool forceReload = false)
    {
        if (TryLoadGameData(slotIndex, out RunSaveData data, forceReload))
        {
            return data;
        }

        if (!File.Exists(GetSavePath(slotIndex)))
        {
            Debug.LogWarning($"[SaveSystem] 슬롯 {slotIndex}에 로드할 세이브 파일이 존재하지 않습니다.");
        }
        return null;
    }

    /// <summary>
    /// 세이브 파일의 데이터를 읽어 현재 게임 세션(Player, RunManager 등)에 직접 주입 및 복원합니다.
    /// </summary>
    public static bool RestoreSession(int slotIndex)
    {
        if (!TryLoadGameData(slotIndex, out RunSaveData data))
        {
            if (!File.Exists(GetSavePath(slotIndex)))
            {
                Debug.LogWarning($"[SaveSystem] 슬롯 {slotIndex}에 로드할 세이브 데이터가 존재하지 않습니다.");
            }
            else
            {
                Debug.LogError($"[SaveSystem] 슬롯 {slotIndex}의 세이브 데이터 역직렬화에 실패했습니다.");
            }
            return false;
        }

        RestoreSession(data);
        Debug.Log($"[SaveSystem] 슬롯 {slotIndex}로부터 게임 세션 복원을 성공적으로 완료했습니다.");
        return true;
    }

    /// <summary>
    /// 이미 로드된 RunSaveData 객체를 재사용하여 현재 게임 세션(Player, RunManager 등)에 주입 및 복원합니다.
    /// </summary>
    public static bool RestoreSession(RunSaveData data)
    {
        if (data == null)
        {
            return false;
        }

        // 1. 플레이어 데이터 복원
        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player != null)
        {
            RestorePlayerData(data, player);
        }

        // 2. 런 진행 데이터 복원 (현재 던전 매니저가 활성화되어 있는 경우)
        RunManager runManager = GameSceneManager.instance != null ? GameSceneManager.instance.currentDungeonManager : null;
        if (runManager != null)
        {
            RestoreRunData(data, runManager);
        }

        return true;
    }

    /// <summary>
    /// 신규 플레이어에 대해 기본 스탯(또는 선택된 시작 키트)을 안전하게 초기화합니다.
    /// </summary>
    public static void InitializeDefaultPlayer(Player player)
    {
        if (player == null || player.character == null) return;

        StarterKitData kitData = null;
        string kitId = !string.IsNullOrEmpty(player.selectedStarterKitId) ? player.selectedStarterKitId : "warrior_starter";

        if (ModLoader.Instance != null && ModLoader.Instance.StarterKitDatabase != null)
        {
            if (!ModLoader.Instance.StarterKitDatabase.TryGetValue(kitId, out kitData))
            {
                // 지정된 키트가 없을 경우 첫 번째 등록된 시작 키트를 fallback으로 사용
                foreach (var kvp in ModLoader.Instance.StarterKitDatabase)
                {
                    kitData = kvp.Value;
                    kitId = kvp.Key;
                    break;
                }
            }
        }

        if (kitData != null)
        {
            ApplyStarterKit(player, kitData, resetGold: true);
            return;
        }

        CharacterStat existingBaseStat = player.character.baseCharacterStat ?? player.character.curCharacterStat;
        CharacterStat fallbackStat = (existingBaseStat != null && existingBaseStat.maxHealth > 0f)
            ? CloneCharacterStat(existingBaseStat)
            : new CharacterStat
            {
                maxHealth = 100,
                maxStamina = 120,
                staminaRegenPerSecond = 40,
                moveRange = 3,
            };

        player.character.InitializeStat(fallbackStat);
        Debug.Log($"[SaveSystem] 시작 키트('{kitId}') 미발견으로 기본 폴백 스탯을 적용했습니다.");
    }

    /// <summary>
    /// 지정된 시작 키트(무장)를 플레이어에게 적용합니다. 기억의 조각(MemorySharp)은 유지됩니다.
    /// </summary>
    public static bool ApplyStarterKit(Player player, StarterKitData kitData, bool resetGold = true)
    {
        if (player == null || player.character == null || kitData == null) return false;

        player.selectedStarterKitId = kitData.loadoutId;

        CharacterStat initialStat = null;
        CharacterStat existingBaseStat = player.character.baseCharacterStat ?? player.character.curCharacterStat;
        if (kitData.characterStat != null)
        {
            initialStat = CloneCharacterStat(kitData.characterStat);
        }
        else if (existingBaseStat != null && existingBaseStat.maxHealth > 0f)
        {
            initialStat = CloneCharacterStat(existingBaseStat);
        }
        else
        {
            initialStat = new CharacterStat
            {
                maxHealth = 100,
                maxStamina = 120,
                staminaRegenPerSecond = 40,
                moveRange = 3,
            };
        }

        player.character.InitializeStat(initialStat);

        // 재화(골드만 선택적으로 키트 기본값 적용, 기억의 조각은 보존) 및 기본 드로우 수 반영
        if (player.playerStat != null)
        {
            player.playerStat.ResetSessionDefaults();
            if (resetGold)
            {
                player.playerStat.InGameCurrencyGold = kitData.startGold;
            }

            player.playerStat.BaseDrawCount = kitData.baseDrawCount > 0 ? kitData.baseDrawCount : 5;
        }

        // 시작 카드 덱 초기화
        if (player.deck != null)
        {
            player.deck.Clear();
            if (kitData.startCards != null)
            {
                foreach (string cardId in kitData.startCards)
                {
                    if (ModLoader.Instance != null && ModLoader.Instance.CardDatabase != null &&
                        ModLoader.Instance.CardDatabase.TryGetValue(cardId, out CardData cardData))
                    {
                        player.deck.AddCard(cardData);
                    }
                    else
                    {
                        Debug.LogWarning($"[SaveSystem] 시작 카드 '{cardId}'를 CardDatabase에서 찾을 수 없습니다.");
                    }
                }
            }
            player.deck.RequestAllCardRefresh();
        }

        // 시작 유물 초기화
        if (player.relicManager != null)
        {
            player.relicManager.ClearRelics();
            if (player.playerStat != null)
            {
                player.playerStat.BattleMapLockBypassCount = 0;
            }
            if (kitData.startRelics != null)
            {
                foreach (string relicId in kitData.startRelics)
                {
                    player.AddRelic(relicId);
                }
            }
        }
        else if (player.playerStat != null)
        {
            player.playerStat.BattleMapLockBypassCount = 0;
        }

        Debug.Log($"[SaveSystem] 시작 키트 적용 완료 (키트: '{kitData.loadoutId}', 체력: {initialStat.maxHealth}, 스태미나: {initialStat.maxStamina}, 이동력: {initialStat.moveRange}).");
        return true;
    }

    /// <summary>
    /// 이미 해금된 시작 키트를 은신처 등에서 장착합니다.
    /// </summary>
    public static bool TryEquipStarterKit(Player player, StarterKitData kitData)
    {
        if (player == null || kitData == null) return false;
        if (!player.IsStarterKitUnlocked(kitData)) return false;

        bool applied = ApplyStarterKit(player, kitData, resetGold: true);
        if (applied && GameSceneManager.instance != null && !GameSceneManager.instance.isInDungeon)
        {
            int slot = GameSceneManager.instance.currentSaveSlot;
            if (HasSaveData(slot))
            {
                SaveGame(null, slot);
            }
        }

        return applied;
    }

    /// <summary>
    /// 잠긴 시작 키트를 기억의 조각으로 해금한 뒤 즉시 장착합니다.
    /// </summary>
    public static bool TryUnlockAndEquipStarterKit(Player player, StarterKitData kitData, out string failReason)
    {
        failReason = string.Empty;
        if (player == null || kitData == null)
        {
            failReason = "플레이어 또는 무장 데이터가 유효하지 않습니다.";
            return false;
        }

        if (player.IsStarterKitUnlocked(kitData))
        {
            return TryEquipStarterKit(player, kitData);
        }

        int cost = Mathf.Max(0, kitData.unlockShardCost);
        int currentShards = player.playerStat != null ? player.playerStat.InGameCurrencyMemorySharp : 0;
        if (player.playerStat == null || currentShards < cost)
        {
            failReason = $"기억의 조각이 부족합니다. (필요: {cost}, 보유: {currentShards})";
            return false;
        }

        player.playerStat.InGameCurrencyMemorySharp -= cost;
        player.UnlockStarterKit(kitData.loadoutId);

        bool applied = ApplyStarterKit(player, kitData, resetGold: true);
        if (applied && GameSceneManager.instance != null && !GameSceneManager.instance.isInDungeon)
        {
            SaveGame(null, GameSceneManager.instance.currentSaveSlot);
        }

        return applied;
    }

    /// <summary>
    /// 세이브 데이터로부터 플레이어의 스탯, 재화, 덱, 유물(카운터 포함)을 순서 부수효과 없이 정확히 복원합니다.
    /// </summary>
    public static void RestorePlayerData(RunSaveData data, Player player)
    {
        if (data == null || player == null || player.character == null) return;

        // 1. 플레이어 세션 스탯 기본값 리셋 (유물 OnInit 패시브 증감이 누적 오염되지 않도록 선초기화)
        if (player.playerStat == null) player.playerStat = new PlayerStat();
        player.playerStat.player = player;
        player.playerStat.ResetSessionDefaults();

        // 시작 무장 선택 및 기본 드로우 수 선반영
        if (!string.IsNullOrEmpty(data.selectedStarterKitId))
        {
            player.selectedStarterKitId = data.selectedStarterKitId;
            if (ModLoader.Instance != null && ModLoader.Instance.StarterKitDatabase != null &&
                ModLoader.Instance.StarterKitDatabase.TryGetValue(data.selectedStarterKitId, out StarterKitData savedKit) &&
                savedKit != null && savedKit.baseDrawCount > 0)
            {
                player.playerStat.BaseDrawCount = savedKit.baseDrawCount;
            }
        }

        // 2. 캐릭터 영구 기본 스탯 복원
        if (data.playerCharacterStat != null)
        {
            player.character.InitializeStat(CloneCharacterStat(data.playerCharacterStat));
        }
        else
        {
            // 구버전 세이브 호환: 프리팹 기본 스탯 복제 및 최대 체력 반영
            CharacterStat baseStat = CloneCharacterStat(player.character.baseCharacterStat ?? player.character.curCharacterStat);
            if (data.playerMaxHealth > 0f) baseStat.maxHealth = data.playerMaxHealth;
            player.character.InitializeStat(baseStat);
        }

        // 3. 덱 및 마스터리 상태 복원 (빈 덱으로 저장된 경우에도 기존 잔여 덱을 비웁니다)
        if (player.deck != null && data.playerDeckCards != null)
        {
            player.deck.Clear();
            HashSet<string> restoredCardMasteries = new HashSet<string>();
            foreach (var cardEntry in data.playerDeckCards)
            {
                if (cardEntry == null || string.IsNullOrEmpty(cardEntry.cardName)) continue;
                if (ModLoader.Instance != null && ModLoader.Instance.CardDatabase != null &&
                    ModLoader.Instance.CardDatabase.TryGetValue(cardEntry.cardName, out CardData cardData))
                {
                    Card newCard = player.deck.AddCard(cardData);
                    if (newCard != null)
                    {
                        // 동일한 카드 이름에 대해서 최초 1회만 마스터리 업그레이드 데이터를 복구합니다 (공유 상태이므로).
                        if (!restoredCardMasteries.Contains(cardEntry.cardName))
                        {
                            restoredCardMasteries.Add(cardEntry.cardName);
                            newCard.RestoreMasteryState(cardEntry.masteryLevel, cardEntry.currentMasteryXP, cardEntry.masteryUpgrades);
                        }
                        else
                        {
                            newCard.RefreshMasteryStatsAndTags();
                        }
                    }
                }
            }
            player.deck.RequestAllCardRefresh();
        }

        // 4. 유물 및 카운터 상태 복원 (isRestoredFromSave=true로 1회성 획득 골드/힐 중복 발동 차단)
        if (player.relicManager != null)
        {
            player.relicManager.ClearRelics();
            player.playerStat.BattleMapLockBypassCount = 0;

            if (data.playerRelicStates != null && data.playerRelicStates.Count > 0)
            {
                for (int i = 0; i < data.playerRelicStates.Count; i++)
                {
                    RelicSaveEntry relicEntry = data.playerRelicStates[i];
                    if (relicEntry == null || string.IsNullOrEmpty(relicEntry.relicName)) continue;
                    player.AddRelic(relicEntry.relicName, isRestoredFromSave: true, restoredCounter: relicEntry.counter);
                }
            }
            else if (data.playerRelicNames != null)
            {
                foreach (var relicName in data.playerRelicNames)
                {
                    if (string.IsNullOrEmpty(relicName)) continue;
                    player.AddRelic(relicName, isRestoredFromSave: true, restoredCounter: 0);
                }
            }
        }
        else
        {
            player.playerStat.BattleMapLockBypassCount = 0;
        }

        // 5. 유물의 최대 체력 보너스 등이 반영된 이후 최종 저장된 현재 체력·재화·리롤 횟수를 정확히 확정
        if (data.playerCurHealth > 0f && player.character.curCharacterStat != null)
        {
            player.character.curHealth = Mathf.Clamp(data.playerCurHealth, 1f, player.character.curCharacterStat.maxHealth);
        }

        player.playerStat.InGameCurrencyGold = data.playerGold;
        player.playerStat.InGameCurrencyMemorySharp = data.playerMemorySharp;
        player.playerStat.ShopRerollCount = Mathf.Max(0, data.playerShopRerollCount);

        // 6. 해금 기록 복원
        player.unlockedStarterKitIds.Clear();
        if (data.unlockedStarterKitIds != null)
        {
            foreach (string unlockedId in data.unlockedStarterKitIds)
            {
                player.UnlockStarterKit(unlockedId);
            }
        }

        player.unlockedLoreKeys.Clear();
        if (data.unlockedLoreKeys != null)
        {
            foreach (string loreKey in data.unlockedLoreKeys)
            {
                if (!string.IsNullOrEmpty(loreKey))
                {
                    player.unlockedLoreKeys.Add(loreKey);
                }
            }
        }

        Debug.Log("[SaveSystem] 플레이어 스탯/재화/덱/유물 복원 완료.");
    }

    /// <summary>
    /// 세이브 데이터로부터 던전 런 진행 상태(맵, 스테이지, 층수 등)를 복원합니다.
    /// </summary>
    public static void RestoreRunData(RunSaveData data, RunManager runManager)
    {
        if (data == null || runManager == null) return;

        if (!data.isInDungeon || data.exploreMapData == null)
        {
            runManager.ResetRunProgress();
            Debug.Log("[SaveSystem] 은신처 또는 빈 데이터 상태이므로 던전 런 진행도를 초기화했습니다.");
            return;
        }

        runManager.currentExploreMapFloor = data.currentExploreMapFloor;

        if (runManager.exploreMapManager != null)
        {
            runManager.exploreMapManager.RestoreMapData(data.exploreMapData);

            runManager.currentExploreNode = FindExploreNode(
                data.exploreMapData,
                data.currentExploreNodeFloor,
                data.currentExploreNodeIndex);

            runManager.beforeExploreNode = FindExploreNode(
                data.exploreMapData,
                data.beforeExploreNodeFloor,
                data.beforeExploreNodeIndex);

            if (runManager.currentExploreNode != null)
            {
                runManager.currentExploreNode.isVisited = true;
            }

            if (runManager.beforeExploreNode != null)
            {
                runManager.beforeExploreNode.isVisited = true;
            }
        }
        else
        {
            runManager.currentExploreNode = null;
            runManager.beforeExploreNode = null;
        }

        string resolvedMapName = data.currentMapName;
        if (string.IsNullOrEmpty(resolvedMapName))
        {
            if (runManager.currentExploreNode != null && runManager.currentExploreNode.encounterData != null)
            {
                resolvedMapName = runManager.currentExploreNode.encounterData.locationType.ToString();
            }
            else
            {
                resolvedMapName = "Temple";
            }
        }

        runManager.currentMapName = resolvedMapName;
        runManager.currentStageLevel = data.currentStageLevel > 0 ? data.currentStageLevel : 1;

        runManager.RestorePendingRewardsFromSave(data.pendingRewards);
        runManager.RestoreActiveShopStateFromSave(data.activeShopState);

        Debug.Log($"[SaveSystem] 던전 런 진행도 복원 완료: Map={runManager.currentMapName}, Stage={runManager.currentStageLevel}, Floor={data.currentExploreMapFloor}");
    }

    /// <summary>
    /// 직렬화된 탐험 맵 데이터에서 층(floor)과 순번(index)에 일치하는 실제 ExploreNode 인스턴스를 찾아 반환합니다.
    /// </summary>
    private static ExploreNode FindExploreNode(ExploreMapData mapData, int floor, int index)
    {
        if (mapData == null || mapData.floors == null) return null;
        if (floor < 0 || index < 0) return null;

        if (floor < mapData.floors.Count)
        {
            ExploreFloorData floorData = mapData.floors[floor];
            if (floorData != null && floorData.nodes != null)
            {
                if (index < floorData.nodes.Count)
                {
                    ExploreNode directMatch = floorData.nodes[index];
                    if (directMatch != null && directMatch.floor == floor && directMatch.index == index)
                    {
                        return directMatch;
                    }
                }

                for (int i = 0; i < floorData.nodes.Count; i++)
                {
                    ExploreNode candidate = floorData.nodes[i];
                    if (candidate != null && candidate.floor == floor && candidate.index == index)
                    {
                        return candidate;
                    }
                }
            }
        }

        // 희소 또는 재구성된 ExploreMapData에서도 (floor, index) 정확 일치 노드를 복원할 수 있도록 전체 층 폴백 탐색
        for (int f = 0; f < mapData.floors.Count; f++)
        {
            ExploreFloorData candidateFloor = mapData.floors[f];
            if (candidateFloor == null || candidateFloor.nodes == null) continue;

            for (int i = 0; i < candidateFloor.nodes.Count; i++)
            {
                ExploreNode candidate = candidateFloor.nodes[i];
                if (candidate != null && candidate.floor == floor && candidate.index == index)
                {
                    return candidate;
                }
            }
        }

        if (floor < mapData.floors.Count)
        {
            ExploreFloorData floorData = mapData.floors[floor];
            if (floorData != null && floorData.nodes != null && index < floorData.nodes.Count)
            {
                return floorData.nodes[index];
            }
        }

        return null;
    }

    public static CharacterStat CloneCharacterStat(CharacterStat source)
    {
        if (source == null)
        {
            return new CharacterStat
            {
                maxHealth = 100f,
                maxStamina = 100f,
                staminaRegenPerSecond = 10f,
                moveRange = 3,
            };
        }

        return new CharacterStat
        {
            maxHealth = source.maxHealth,
            maxStamina = source.maxStamina,
            staminaRegenPerSecond = source.staminaRegenPerSecond,
            moveRange = source.moveRange
        };
    }

    /// <summary>
    /// 특정 슬롯의 세이브 파일을 영구적으로 삭제합니다.
    /// </summary>
    public static void DeleteSaveFile(int slotIndex)
    {
        InvalidateCache(slotIndex);
        if (File.Exists(GetSavePath(slotIndex)))
        {
            try
            {
                string savePath = GetSavePath(slotIndex);
                File.Delete(savePath);
                Debug.Log($"[SaveSystem] 슬롯 {slotIndex}의 세이브 파일이 성공적으로 삭제되었습니다.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 슬롯 {slotIndex}의 세이브 파일 삭제 중 예외 발생: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 저장소의 모든 세이브 슬롯 파일을 일괄 삭제합니다.
    /// </summary>
    public static void DeleteAllSaveFiles()
    {
        InvalidateAllCaches();
        try
        {
            string saveDir = Application.persistentDataPath;
            string[] saveFiles = Directory.GetFiles(saveDir, "save_*.json");
            foreach (var file in saveFiles)
            {
                File.Delete(file);
                Debug.Log($"[SaveSystem] 세이브 파일 삭제됨: {file}");
            }
            Debug.Log("[SaveSystem] 모든 세이브 데이터가 성공적으로 초기화되었습니다.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 전체 세이브 파일 삭제 중 예외 발생: {e.Message}");
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Save Data/Clear All Save Data", priority = 100)]
    public static void EditorClearAllSaveData()
    {
        if (UnityEditor.EditorUtility.DisplayDialog("세이브 데이터 전체 삭제", "모든 세이브 슬롯의 데이터를 영구적으로 삭제하시겠습니까?", "삭제", "취소"))
        {
            DeleteAllSaveFiles();
        }
    }

    [UnityEditor.MenuItem("Tools/Save Data/Delete Slot 1", priority = 101)]
    public static void EditorDeleteSlot1() => DeleteSaveFile(1);

    [UnityEditor.MenuItem("Tools/Save Data/Delete Slot 2", priority = 102)]
    public static void EditorDeleteSlot2() => DeleteSaveFile(2);

    [UnityEditor.MenuItem("Tools/Save Data/Delete Slot 3", priority = 103)]
    public static void EditorDeleteSlot3() => DeleteSaveFile(3);

    [UnityEditor.MenuItem("Tools/Save Data/Open Save Folder in Explorer", priority = 120)]
    public static void EditorOpenSaveFolder()
    {
        UnityEditor.EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
#endif
}
