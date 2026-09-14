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
}
