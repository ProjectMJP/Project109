using System;
using System.IO;
using System.Collections.Generic;
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
public class RunSaveData
{
    public string currentMapName;
    public int currentStageLevel;
    public int currentExploreMapFloor;
    public int playerGold;
    public int playerMemorySharp;
    
    public float playerCurHealth;
    public float playerMaxHealth;
    public CharacterStat playerCharacterStat; // 플레이어 전체 스탯 데이터 (영구 업그레이드 포함)

    public bool isInDungeon; // 현재 던전 진행 중인지 여부 (은신처/던전 판정용)

    public List<CardSaveEntry> playerDeckCards = new List<CardSaveEntry>();
    public List<string> playerRelicNames = new List<string>();
}

/// <summary>
/// JSON 파일을 기반으로 게임 세션을 로컬 저장소에 저장하고 불러오는 시스템입니다.
/// </summary>
public static class SaveSystem
{
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
            data.currentMapName = runManager != null ? runManager.currentMapName : "Temple";
            data.currentStageLevel = runManager != null ? runManager.currentStageLevel : 1;
            data.currentExploreMapFloor = runManager != null ? runManager.currentExploreMapFloor : 1;
            data.isInDungeon = GameSceneManager.instance != null && GameSceneManager.instance.isInDungeon;
            
            data.playerGold = player.playerStat != null ? player.playerStat.InGameCurrencyGold : 0;
            data.playerMemorySharp = player.playerStat != null ? player.playerStat.InGameCurrencyMemorySharp : 0;

            if (player.character != null)
            {
                data.playerCurHealth = player.character.curHealth;
                data.playerMaxHealth = player.character.curCharacterStat != null ? player.character.curCharacterStat.maxHealth : 100f;
                data.playerCharacterStat = player.character.curCharacterStat;
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

            // 플레이어 유물 저장
            if (player.relicManager != null)
            {
                foreach (var relic in player.relicManager.GetRelics())
                {
                    if (relic == null || relic.Data == null) continue;
                    data.playerRelicNames.Add(relic.Data.relicName);
                }
            }

            string json = JsonUtility.ToJson(data, true);
            string savePath = GetSavePath(slotIndex);
            File.WriteAllText(savePath, json, System.Text.Encoding.UTF8);
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
        return File.Exists(GetSavePath(slotIndex));
    }

    /// <summary>
    /// 특정 슬롯 저장소로부터 데이터를 읽어 RunSaveData 객체로 역직렬화합니다.
    /// </summary>
    public static RunSaveData LoadGameData(int slotIndex)
    {
        if (!HasSaveData(slotIndex))
        {
            Debug.LogWarning($"[SaveSystem] 슬롯 {slotIndex}에 로드할 세이브 파일이 존재하지 않습니다.");
            return null;
        }

        try
        {
            string savePath = GetSavePath(slotIndex);
            string json = File.ReadAllText(savePath, System.Text.Encoding.UTF8);
            RunSaveData data = JsonUtility.FromJson<RunSaveData>(json);
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 로드 중 예외 발생: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 세이브 파일의 데이터를 읽어 현재 게임 세션(Player, RunManager 등)에 직접 주입 및 복원합니다.
    /// </summary>
    public static bool RestoreSession(int slotIndex)
    {
        if (!HasSaveData(slotIndex))
        {
            Debug.LogWarning($"[SaveSystem] 슬롯 {slotIndex}에 로드할 세이브 데이터가 존재하지 않습니다.");
            return false;
        }

        RunSaveData data = LoadGameData(slotIndex);
        if (data == null)
        {
            Debug.LogError($"[SaveSystem] 슬롯 {slotIndex}의 세이브 데이터 역직렬화에 실패했습니다.");
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

        Debug.Log($"[SaveSystem] 슬롯 {slotIndex}로부터 게임 세션 복원을 성공적으로 완료했습니다.");
        return true;
    }

    /// <summary>
    /// 신규 플레이어에 대해 기본 스탯(또는 프리팹 설정값)을 안전하게 초기화합니다.
    /// </summary>
    public static void InitializeDefaultPlayer(Player player)
    {
        if (player == null || player.character == null) return;

        CharacterStat initialStat = CloneCharacterStat(player.character.curCharacterStat);
        player.character.InitializeStat(initialStat);
        Debug.Log($"[SaveSystem] 신규 플레이어 기본 스탯 초기화 완료 (최대 체력: {initialStat.maxHealth}, 이동력: {initialStat.maxTilesPerMove}).");
    }

    /// <summary>
    /// 세이브 데이터로부터 플레이어의 스탯, 재화, 덱, 유물을 완벽히 복원합니다.
    /// </summary>
    public static void RestorePlayerData(RunSaveData data, Player player)
    {
        if (data == null || player == null || player.character == null) return;

        // 1. 캐릭터 스탯 복원
        if (data.playerCharacterStat != null)
        {
            player.character.InitializeStat(CloneCharacterStat(data.playerCharacterStat));
        }
        else
        {
            // 구버전 세이브 호환: 프리팹 기본 스탯 복제 및 최대 체력 반영
            CharacterStat baseStat = CloneCharacterStat(player.character.curCharacterStat);
            if (data.playerMaxHealth > 0f) baseStat.maxHealth = data.playerMaxHealth;
            player.character.InitializeStat(baseStat);
        }

        // 현재 체력 복원
        if (data.playerCurHealth > 0f)
        {
            player.character.curHealth = Mathf.Min(data.playerCurHealth, player.character.curCharacterStat.maxHealth);
        }

        // 2. 재화 복원
        if (player.playerStat == null) player.playerStat = new PlayerStat();
        player.playerStat.InGameCurrencyGold = data.playerGold;
        player.playerStat.InGameCurrencyMemorySharp = data.playerMemorySharp;

        // 3. 덱 복원
        if (player.deck != null && data.playerDeckCards != null && data.playerDeckCards.Count > 0)
        {
            player.deck.Clear();
            HashSet<string> restoredCardMasteries = new HashSet<string>();
            foreach (var cardEntry in data.playerDeckCards)
            {
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
                            if (cardEntry.masteryUpgrades != null)
                            {
                                foreach (var upgrade in cardEntry.masteryUpgrades)
                                {
                                    for (int i = 0; i < upgrade.count; i++)
                                    {
                                        newCard.AddMastery(upgrade.masteryId);
                                    }
                                }
                            }
                            newCard.currentMasteryXP = cardEntry.currentMasteryXP;
                        }
                        else
                        {
                            if (newCard.masteryUpgrades != null && cardData.masteryTags != null)
                            {
                                foreach (var kvp in newCard.masteryUpgrades)
                                {
                                    string masteryId = kvp.Key;
                                    int count = kvp.Value;
                                    if (cardData.masteryTags.TryGetValue(masteryId, out var tags))
                                    {
                                        for (int i = 0; i < count; i++)
                                        {
                                            foreach (var tag in tags)
                                            {
                                                newCard.AddTag(tag);
                                            }
                                        }
                                    }
                                }
                            }

                            float costMod = newCard.GetEffectiveValue("cost");
                            if (costMod != 0)
                            {
                                newCard.currentCost = Mathf.Max(0, cardData.stamina + (int)costMod);
                            }
                        }
                    }
                }
            }
            player.deck.RequestAllCardRefresh();
        }

        // 4. 유물 복원
        if (player.relicManager != null && data.playerRelicNames != null)
        {
            player.relicManager.ClearRelics();
            foreach (var relicName in data.playerRelicNames)
            {
                player.AddRelic(relicName);
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

        runManager.currentMapName = data.currentMapName;
        runManager.currentStageLevel = data.currentStageLevel;
        runManager.currentExploreMapFloor = data.currentExploreMapFloor;

        Debug.Log($"[SaveSystem] 던전 런 진행도 복원 완료: Map={data.currentMapName}, Stage={data.currentStageLevel}, Floor={data.currentExploreMapFloor}");
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
                maxMoveCount = 2,
                maxTilesPerMove = 3
            };
        }

        return new CharacterStat
        {
            maxHealth = source.maxHealth,
            maxStamina = source.maxStamina,
            staminaRegenPerSecond = source.staminaRegenPerSecond,
            maxMoveCount = source.maxMoveCount,
            maxTilesPerMove = source.maxTilesPerMove
        };
    }

    /// <summary>
    /// 특정 슬롯의 세이브 파일을 영구적으로 삭제합니다.
    /// </summary>
    public static void DeleteSaveFile(int slotIndex)
    {
        if (HasSaveData(slotIndex))
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
