using GameItem.Types;
using System;
using UnityEngine;

/// <summary>
/// 다이얼로그 시스템에서 발생하는 게임플레이 액션(보상, 회복, 전투, 카드/유물 조작 등)을 전담 실행하는 컨트롤러입니다.
/// L4 UI 계층인 DialogueManager로부터 게임플레이 조작 책임을 분리합니다.
/// </summary>
public class DialogueActionExecutor
{
    private Player GetPlayer() => GameSceneManager.instance != null ? GameSceneManager.instance.player : null;

    #region Gold Control

    public int GetGold()
    {
        var p = GetPlayer();
        return (p != null && p.playerStat != null) ? p.playerStat.InGameCurrencyGold : 0;
    }

    public void ConsumeGold(int amount)
    {
        var p = GetPlayer();
        if (p != null && p.playerStat != null)
        {
            p.playerStat.InGameCurrencyGold = Mathf.Max(0, GetGold() - amount);
        }
    }

    public void AddGold(int amount)
    {
        var p = GetPlayer();
        if (p != null && p.playerStat != null)
        {
            p.playerStat.InGameCurrencyGold += amount;
        }
    }

    #endregion

    #region Card Control

    public int GetCardCount()
    {
        var p = GetPlayer();
        return (p != null && p.deck != null) ? p.deck.GetCards().Count : 0;
    }

    public string GetCardNameAt(int index)
    {
        var p = GetPlayer();
        if (p != null && p.deck != null)
        {
            var cards = p.deck.GetCards();
            if (index >= 0 && index < cards.Count)
            {
                return cards[index].cardName;
            }
        }
        return string.Empty;
    }

    public void AddCard(string cardName)
    {
        var p = GetPlayer();
        if (ModLoader.Instance != null && p != null && p.deck != null)
        {
            if (ModLoader.Instance.CardDatabase.TryGetValue(cardName, out CardData cardData))
            {
                p.deck.AddCard(cardData);
                Debug.Log($"[DialogueActionExecutor] 덱에 '{cardName}' 카드가 추가되었습니다.");
            }
            else
            {
                Debug.LogError($"[DialogueActionExecutor] 카드 데이터베이스에서 '{cardName}' 카드를 찾을 수 없습니다.");
            }
        }
    }

    public void RemoveCardAt(int index)
    {
        var p = GetPlayer();
        if (p != null && p.deck != null)
        {
            var cards = p.deck.GetCards();
            if (index >= 0 && index < cards.Count)
            {
                p.deck.RemoveCard(cards[index].runtimeID);
                Debug.Log($"[DialogueActionExecutor] index {index} 번의 카드가 제거되었습니다.");
            }
        }
    }

    #endregion

    #region Relic Control

    public int GetRelicCount()
    {
        var p = GetPlayer();
        return (p != null && p.relicManager != null) ? p.relicManager.GetRelics().Count : 0;
    }

    public string GetRelicNameAt(int index)
    {
        var p = GetPlayer();
        if (p != null && p.relicManager != null)
        {
            var relics = p.relicManager.GetRelics();
            if (index >= 0 && index < relics.Count)
            {
                return relics[index].Data.relicName;
            }
        }
        return string.Empty;
    }

    public void RemoveRelicAt(int index)
    {
        var p = GetPlayer();
        if (p != null && p.relicManager != null)
        {
            var relics = p.relicManager.GetRelics();
            if (index >= 0 && index < relics.Count)
            {
                p.RemoveRelic(relics[index].Data.relicName);
                Debug.Log($"[DialogueActionExecutor] index {index} 번의 유물('{relics[index].Data.relicName}')이 제거되었습니다.");
            }
        }
    }

    public void RemoveRelicByName(string relicName)
    {
        var p = GetPlayer();
        if (p != null)
        {
            p.RemoveRelic(relicName);
            Debug.Log($"[DialogueActionExecutor] 유물 '{relicName}'이 제거되었습니다.");
        }
    }

    public bool HasRelic(string relicName)
    {
        var p = GetPlayer();
        if (p != null && p.relicManager != null)
        {
            foreach (var relic in p.relicManager.GetRelics())
            {
                if (relic != null && relic.Data != null && relic.Data.relicName == relicName)
                {
                    return true;
                }
            }
        }
        return false;
    }

    #endregion

    #region Health Control

    public int GetCurrentHealth()
    {
        var p = GetPlayer();
        return (p != null && p.character != null) ? (int)p.character.curHealth : 0;
    }

    public int GetMaxHealth()
    {
        var p = GetPlayer();
        return (p != null && p.character != null && p.character.curCharacterStat != null)
            ? (int)p.character.curCharacterStat.maxHealth
            : 0;
    }

    public void HealPlayer(int amount)
    {
        var p = GetPlayer();
        var character = p != null ? p.character : null;
        if (character != null)
        {
            character.TakeHeal(new EventStructs.HealInfo(null, character, amount, EventStructs.HealFlag.Normal));
        }
    }

    public void DamagePlayer(int amount)
    {
        var p = GetPlayer();
        var character = p != null ? p.character : null;
        if (character != null)
        {
            character.curHealth = Mathf.Max(1f, character.curHealth - amount);
        }
    }

    public void ModifyMaxHealth(int amount)
    {
        var p = GetPlayer();
        var character = p != null ? p.character : null;
        if (character != null && character.curCharacterStat != null)
        {
            var stat = character.curCharacterStat;
            stat.maxHealth = Mathf.Max(1f, stat.maxHealth + amount);
            if (character.curHealth > stat.maxHealth)
            {
                character.curHealth = stat.maxHealth;
            }
        }
    }

    #endregion

    #region Battle & Map Progression

    public void StartBattle(string battleName)
    {
        if (AssetCacheManager.instance != null && AssetCacheManager.instance.TryGetBattle(battleName, out BattleData battleData))
        {
            if (RunManager.instance != null)
            {
                RunManager.instance.SpawnMonsterInBattleNodeData(battleData);
            }
        }
        else
        {
            Debug.LogError($"[DialogueActionExecutor] 전투 데이터를 찾을 수 없습니다: {battleName}");
        }
    }

    public void RevealAllNodes()
    {
        if (RunManager.instance != null && RunManager.instance.exploreMapManager != null)
        {
            RunManager.instance.exploreMapManager.RevealAllNodes();
        }

        if (UIManager.instance != null)
        {
            ExploreMapUI mapUI = UIManager.instance.GetUI<ExploreMapUI>(UIConstants.PANEL_EXPLORE_MAP);
            if (mapUI != null && mapUI.gameObject.activeSelf)
            {
                mapUI.RefreshMapVisuals();
            }
        }
    }

    public void SpawnReward(string rewardTypeStr, string rarityStr, Vector3 spawnPos)
    {
        if (GameItemRewardManager.instance == null)
        {
            Debug.LogError("[DialogueActionExecutor] GameItemRewardManager.instance가 존재하지 않습니다.");
            return;
        }

        RewardItemType rType = RewardItemType.Card;
        if (rewardTypeStr.Equals("relic", StringComparison.OrdinalIgnoreCase))
        {
            rType = RewardItemType.Relic;
        }

        // 1. 전달받은 rarityStr 자체가 ModLoader의 DropTableDatabase에 등록된 테이블 ID라면 직접 우선 사용
        string dropTableID = null;
        if (!string.IsNullOrEmpty(rarityStr) && ModLoader.Instance != null && ModLoader.Instance.DropTableDatabase != null &&
            ModLoader.Instance.DropTableDatabase.ContainsKey(rarityStr))
        {
            dropTableID = rarityStr;
        }
        else
        {
            // 2. 기본 희귀도 매핑 (폴백)
            if (rType == RewardItemType.Relic)
            {
                dropTableID = "Default_Relic_Table";
                if (rarityStr.Equals("rare", StringComparison.OrdinalIgnoreCase))
                {
                    dropTableID = "Rare_Relic_Table";
                }
                else if (rarityStr.Equals("unique", StringComparison.OrdinalIgnoreCase))
                {
                    dropTableID = "Unique_Relic_Table";
                }
            }
            else
            {
                dropTableID = "Default_Card_Table";
                if (rarityStr.Equals("rare", StringComparison.OrdinalIgnoreCase))
                {
                    dropTableID = "Rare_Card_Table";
                }
                else if (rarityStr.Equals("unique", StringComparison.OrdinalIgnoreCase))
                {
                    dropTableID = "Unique_Card_Table";
                }
            }
        }

        GameItemRewardManager.instance.InstantiateItemReward(rType, dropTableID, spawnPos);
    }

    #endregion

    #region Interactable Operations

    public void OpenShop(InteractableObject activeInteractable)
    {
        if (activeInteractable is ShopNPC shopNPC)
        {
            shopNPC.OpenShopDirectly();
        }
        else
        {
            Debug.LogWarning("[DialogueActionExecutor] ActiveInteractable이 ShopNPC가 아닙니다.");
        }
    }

    public void OpenRestore(InteractableObject activeInteractable)
    {
        if (activeInteractable is RestoreBonfire restoreBonfire)
        {
            restoreBonfire.OpenRestoreDirectly();
        }
        else
        {
            Debug.LogWarning("[DialogueActionExecutor] ActiveInteractable이 RestoreBonfire가 아닙니다.");
        }
    }

    public void MarkActiveInteractableUsed(InteractableObject activeInteractable)
    {
        if (activeInteractable is RestoreBonfire restoreBonfire)
        {
            restoreBonfire.MarkAsUsed();
        }
    }

    public void OpenChest(InteractableObject activeInteractable)
    {
        if (activeInteractable is RewardChest rewardChest)
        {
            rewardChest.OpenChestDirectly();
        }
        else
        {
            Debug.LogWarning("[DialogueActionExecutor] ActiveInteractable이 RewardChest가 아닙니다.");
        }
    }

    #endregion
}
