using GameItem.Types;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using XLua;

// XLua에서 C# 호출을 프록시로 받아오기 위한 델리게이트 정의
[CSharpCallLua]
public delegate bool CanSelectDialogueChoice(DialogueManager dm);

[CSharpCallLua]
public delegate bool IsDialogueChoiceVisible(DialogueManager dm);

[CSharpCallLua]
public delegate void ExecuteDialogueChoice(DialogueManager dm);

[CSharpCallLua]
public delegate string GetDialogueChoiceDescription(DialogueManager dm);

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    public bool IsDialogueActive => activeDialogue != null;

    /// <summary>
    /// 외부 시스템(유물, 퀘스트, 상태 효과 등)에서 다이얼로그 선택지를 동적으로 추가할 수 있는 이벤트 훅입니다.
    /// </summary>
    public static event Action<DialogueNode, List<ChoiceData>> OnPopulateChoices;

    public GameObject dialogueUICanvasPrefab;
    private EventDescriptionScript dialogueUI; // 기존 UI 제어 클래스 재활용

    private DialogueData activeDialogue;
    private Dictionary<string, DialogueNode> nodeMap = new Dictionary<string, DialogueNode>();
    private DialogueNode currentNode;
    private int currentLineIndex = 0;

    private LuaEnv luaEnv;
    private string luaNextNodeOverride = null;
    private bool endDialogueTriggered = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        luaEnv = new LuaEnv();
    }

    private void EnsureDialogueUI()
    {
        if (dialogueUI != null) return;

        GameObject spawned = null;
        if (UIManager.instance != null)
        {
            spawned = UIManager.instance.OpenUI("DialogueUI", UILayerType.Normal, false);
        }

        if (spawned == null)
        {
            if (dialogueUICanvasPrefab != null)
            {
                spawned = Instantiate(dialogueUICanvasPrefab);
            }
            else
            {
                Debug.LogError("[DialogueManager] DialogueUI 프리팹을 찾을 수 없습니다.");
                return;
            }
        }

        dialogueUI = spawned.GetComponent<EventDescriptionScript>();
        if (dialogueUI == null && spawned.transform.childCount > 0)
        {
            dialogueUI = spawned.transform.GetChild(0).GetComponent<EventDescriptionScript>();
        }

        if (dialogueUI != null)
        {
            dialogueUI.gameObject.SetActive(false);
        }
    }

    public InteractableObject ActiveInteractable { get; private set; }

    /// <summary>
    /// 외부(예: NPCInteraction)에서 다이얼로그 대화를 개시할 때 호출하는 진입점입니다.
    /// </summary>
    public void StartDialogue(DialogueData data, InteractableObject trigger = null)
    {
        if (data == null)
        {
            Debug.LogWarning("[DialogueManager] StartDialogue 호출에 빈 데이터가 들어왔습니다.");
            return;
        }

        EnsureDialogueUI();

        ActiveInteractable = trigger;
        activeDialogue = data;
        nodeMap.Clear();
        foreach (var node in data.nodes)
        {
            nodeMap[node.nodeID] = node;
        }

        if (dialogueUI != null)
        {
            dialogueUI.Open();
        }

        GoToNode(data.startNodeID);
    }

    /// <summary>
    /// 특정 다이얼로그 노드 상태로 전이합니다.
    /// </summary>
    public void GoToNode(string nodeID)
    {
        if (string.IsNullOrEmpty(nodeID) || !nodeMap.ContainsKey(nodeID))
        {
            EndDialogue();
            return;
        }

        currentNode = nodeMap[nodeID];
        currentLineIndex = 0;
        luaNextNodeOverride = null;
        endDialogueTriggered = false;

        ShowNextLineOrChoices();
    }

    /// <summary>
    /// 대사의 다음 줄을 순차적으로 출력하거나, 대사가 전부 끝났다면 선택지를 세팅합니다.
    /// </summary>
    public void ShowNextLineOrChoices()
    {
        if (currentNode == null) return;

        if (currentLineIndex < currentNode.lines.Count)
        {
            dialogueUI.ClearChoiceButton();
            // 지문 대사 출력
            dialogueUI.SetDescription(currentNode.lines[currentLineIndex]);

            // 기존 EventDescriptionScript의 Next 기능이나 마우스 클릭으로 다음 줄을 출력하도록 바인딩하기 위해 기본 진행용 '다음' 버튼 생성
            Button nextBtn = dialogueUI.CreateChoiceButton("▶");
            nextBtn.onClick.AddListener(() =>
            {
                currentLineIndex++;
                ShowNextLineOrChoices();
            });
        }
        else
        {
            // 지문이 전부 끝났으므로 선택지 세팅
            SetupChoiceButtons();
        }
    }

    /// <summary>
    /// 선택지 UI를 동적으로 생성하고 이벤트 리스너를 매핑합니다.
    /// </summary>
    private void SetupChoiceButtons()
    {
        dialogueUI.ClearChoiceButton();

        List<ChoiceData> choicesToDisplay = new List<ChoiceData>();
        if (currentNode.choices != null)
        {
            choicesToDisplay.AddRange(currentNode.choices);
        }

        // 외부 훅에서 선택지 추가 기회 제공 (유물 등)
        OnPopulateChoices?.Invoke(currentNode, choicesToDisplay);

        if (choicesToDisplay.Count == 0)
        {
            // 대화 분기나 선택지가 없다면 대화 종료
            Button closeBtn = dialogueUI.CreateChoiceButton("대화를 끝마친다.");
            closeBtn.onClick.AddListener(EndDialogue);
            return;
        }

        foreach (var choice in choicesToDisplay)
        {
            string finalDescription = choice.description;
            bool isSelectable = true;

            // Lua 스크립트 기반 조건 및 설명 오버라이드
            if (!string.IsNullOrEmpty(choice.luaScript))
            {
                LoadAndExecuteLua(choice.luaScript, out var canSelect, out var getDesc, out _, out var isVisible);
                if (isVisible != null && !isVisible(this))
                {
                    // 조건에 맞지 않아 표시되지 않는 선택지 건너뜀
                    continue;
                }
                if (getDesc != null)
                {
                    string extraDesc = getDesc(this);
                    if (!string.IsNullOrEmpty(extraDesc))
                    {
                        finalDescription += "\n" + extraDesc;
                    }
                }
                if (canSelect != null)
                {
                    isSelectable = canSelect(this);
                }
            }

            Button choiceBtn = dialogueUI.CreateChoiceButton(finalDescription);
            if (choiceBtn != null)
            {
                choiceBtn.interactable = isSelectable;
                choiceBtn.onClick.AddListener(() => OnChoiceClicked(choice));
            }
        }
    }

    private bool isDialoguePaused = false;
    public bool IsDialoguePaused => isDialoguePaused;
    private string pendingNextNodeID = null;

    public void PauseDialogue()
    {
        isDialoguePaused = true;
        if (dialogueUI != null && dialogueUI.gameObject.activeSelf)
        {
            dialogueUI.gameObject.SetActive(false);
        }
    }

    public void ResumeDialogue()
    {
        if (!isDialoguePaused) return;
        isDialoguePaused = false;

        if (dialogueUI != null)
        {
            dialogueUI.Open();
        }

        if (pendingNextNodeID != null)
        {
            string target = pendingNextNodeID;
            pendingNextNodeID = null;
            ExecuteNodeTransition(target);
        }
    }

    private void ExecuteNodeTransition(string nodeID)
    {
        if (nodeID == "END_DIALOGUE")
        {
            EndDialogue();
        }
        else
        {
            GoToNode(nodeID);
        }
    }

    /// <summary>
    /// 플레이어가 특정 선택지 버튼을 눌렀을 때의 콜백입니다.
    /// </summary>
    private void OnChoiceClicked(ChoiceData choice)
    {
        if (!string.IsNullOrEmpty(choice.luaScript))
        {
            LoadAndExecuteLua(choice.luaScript, out _, out _, out var execute, out _);
            execute?.Invoke(this);
        }

        string targetNodeID = null;
        if (endDialogueTriggered)
        {
            targetNodeID = "END_DIALOGUE";
        }
        else if (!string.IsNullOrEmpty(luaNextNodeOverride))
        {
            targetNodeID = luaNextNodeOverride;
        }
        else
        {
            targetNodeID = choice.nextNodeID;
        }

        if (isDialoguePaused)
        {
            pendingNextNodeID = targetNodeID;
        }
        else
        {
            ExecuteNodeTransition(targetNodeID);
        }
    }

    /// <summary>
    /// 다이얼로그 대화창을 비활성화하고 정산합니다.
    /// </summary>
    public void EndDialogue()
    {
        EnsureDialogueUI();
        if (dialogueUI != null)
        {
            dialogueUI.Close();
        }
        activeDialogue = null;
        if (luaEnv != null)
        {
            luaEnv.FullGc(); // XLua 가비지 컬렉션 수행
        }
    }

    /// <summary>
    /// 개별 Lua 스크립트를 로드하여 델리게이트에 바인딩합니다.
    /// </summary>
    private void LoadAndExecuteLua(string luaScriptName, out CanSelectDialogueChoice canSelect, out GetDialogueChoiceDescription getDesc, out ExecuteDialogueChoice execute, out IsDialogueChoiceVisible isVisible)
    {
        canSelect = null;
        getDesc = null;
        execute = null;
        isVisible = null;

        string path = $"Assets/Scripts/Lua/Choice/{luaScriptName}.lua";
        if (!File.Exists(path))
        {
            Debug.LogError($"[DialogueManager] Lua 스크립트 파일을 찾을 수 없습니다: {path}");
            return;
        }

        try
        {
            string scriptContent = File.ReadAllText(path);
            luaEnv.DoString(scriptContent);
            canSelect = luaEnv.Global.Get<CanSelectDialogueChoice>("CanSelect");
            getDesc = luaEnv.Global.Get<GetDialogueChoiceDescription>("GetDescription");
            execute = luaEnv.Global.Get<ExecuteDialogueChoice>("ExecuteChoice");
            isVisible = luaEnv.Global.Get<IsDialogueChoiceVisible>("IsVisible");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueManager] Lua 파싱 에러 ({luaScriptName}): {e.Message}");
        }
    }

    #region Sandbox C# API for Lua (루아 바인딩용 샌드박스 API)

    private Player GetPlayer() => GameSceneManager.instance != null ? GameSceneManager.instance.player : null;

    // 1. 재화 (골드) 제어 API
    public int GetGold() => GetPlayer()?.playerStat != null ? GetPlayer().playerStat.InGameCurrencyGold : 0;

    public void ConsumeGold(int amount)
    {
        var p = GetPlayer();
        if (p?.playerStat != null)
        {
            p.playerStat.InGameCurrencyGold = Mathf.Max(0, GetGold() - amount);
        }
    }

    public void AddGold(int amount)
    {
        var p = GetPlayer();
        if (p?.playerStat != null)
        {
            p.playerStat.InGameCurrencyGold += amount;
        }
    }

    // 2. 카드 제어 API
    public int GetCardCount()
    {
        var p = GetPlayer();
        return p?.deck != null ? p.deck.GetCards().Count : 0;
    }

    public string GetCardNameAt(int index)
    {
        var p = GetPlayer();
        if (p?.deck != null)
        {
            var cards = p.deck.GetCards();
            if (index >= 0 && index < cards.Count)
            {
                return cards[index].cardName;
            }
        }
        return "";
    }

    public void AddCard(string cardName)
    {
        var p = GetPlayer();
        if (ModLoader.Instance != null && p?.deck != null)
        {
            if (ModLoader.Instance.CardDatabase.TryGetValue(cardName, out CardData cardData))
            {
                p.deck.AddCard(cardData);
                Debug.Log($"[DialogueManager] 덱에 '{cardName}' 카드가 추가되었습니다.");
            }
            else
            {
                Debug.LogError($"[DialogueManager] 카드 데이터베이스에서 '{cardName}' 카드를 찾을 수 없습니다.");
            }
        }
    }

    public void RemoveCardAt(int index)
    {
        var p = GetPlayer();
        if (p?.deck != null)
        {
            var cards = p.deck.GetCards();
            if (index >= 0 && index < cards.Count)
            {
                p.deck.RemoveCard(cards[index].runtimeID);
                Debug.Log($"[DialogueManager] index {index} 번의 카드가 제거되었습니다.");
            }
        }
    }

    // 3. 유물 제어 API
    public int GetRelicCount()
    {
        var p = GetPlayer();
        return p?.relicManager != null ? p.relicManager.GetRelics().Count : 0;
    }

    public string GetRelicNameAt(int index)
    {
        var p = GetPlayer();
        if (p?.relicManager != null)
        {
            var relics = p.relicManager.GetRelics();
            if (index >= 0 && index < relics.Count)
            {
                return relics[index].Data.relicName;
            }
        }
        return "";
    }

    public void RemoveRelicAt(int index)
    {
        var p = GetPlayer();
        if (p != null)
        {
            var relics = p.relicManager.GetRelics();
            if (index >= 0 && index < relics.Count)
            {
                p.RemoveRelic(relics[index].Data.relicName);
                Debug.Log($"[DialogueManager] index {index} 번의 유물('{relics[index].Data.relicName}')이 제거되었습니다.");
            }
        }
    }

    public void RemoveRelicByName(string relicName)
    {
        var p = GetPlayer();
        if (p != null)
        {
            p.RemoveRelic(relicName);
            Debug.Log($"[DialogueManager] 유물 '{relicName}'이 제거되었습니다.");
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

    // 4. 플레이어 체력 제어 API
    public int GetCurrentHealth() => (int)(GetPlayer()?.character != null ? GetPlayer().character.curHealth : 0);

    public int GetMaxHealth() => (int)(GetPlayer()?.character?.curCharacterStat != null ? GetPlayer().character.curCharacterStat.maxHealth : 0);

    public void HealPlayer(int amount)
    {
        var character = GetPlayer()?.character;
        if (character != null)
        {
            character.TakeHeal(new EventStructs.HealInfo(null, character, amount, EventStructs.HealFlag.Normal));
        }
    }

    public void DamagePlayer(int amount)
    {
        var character = GetPlayer()?.character;
        if (character != null)
        {
            character.curHealth = Mathf.Max(1f, character.curHealth - amount);
        }
    }

    public void ModifyMaxHealth(int amount)
    {
        var character = GetPlayer()?.character;
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

    // 5. 맵 노드 및 UI 제어 연출 API
    public void StartBattle(string battleName)
    {
        if (AssetCacheManager.instance.TryGetBattle(battleName, out BattleData battleData))
        {
            RunManager.instance.SpawnMonsterInBattleNodeData(battleData);
        }
        else
        {
            Debug.LogError($"[DialogueManager] 전투 데이터를 찾을 수 없습니다: {battleName}");
        }
    }

    public void OpenAllExploreNodes()
    {
        if (RunManager.instance != null && RunManager.instance.currentExploreUI != null)
        {
            RunManager.instance.currentExploreUI.OpenAllExploreMapNodes();
        }
    }

    public void SpawnReward(string rewardTypeStr, string rarityStr)
    {
        if (GameItemRewardManager.instance == null)
        {
            Debug.LogError("[DialogueManager] GameItemRewardManager.instance가 존재하지 않습니다.");
            return;
        }

        RewardItemType rType = RewardItemType.Card;
        if (rewardTypeStr.Equals("relic", StringComparison.OrdinalIgnoreCase))
        {
            rType = RewardItemType.Relic;
        }

        string dropTableID = "Default_Card_Table";
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
            if (rarityStr.Equals("rare", StringComparison.OrdinalIgnoreCase))
            {
                dropTableID = "Rare_Card_Table";
            }
            else if (rarityStr.Equals("unique", StringComparison.OrdinalIgnoreCase))
            {
                dropTableID = "Unique_Card_Table";
            }
        }

        // 현재 매니저의 위치(필드) 주변에 스폰
        Vector3 spawnPos = this.transform.position;
        GameItemRewardManager.instance.InstantiateItemReward(rType, dropTableID, spawnPos);
    }

    public void OpenEraseCardUI(int count)
    {
        PauseDialogue();

        if (UIManager.instance != null)
        {
            GameObject eraseUI = UIManager.instance.OpenUI("EraseCardDeckCanvas", UILayerType.Normal, true);
            if (eraseUI != null)
            {
                var panel = eraseUI.GetComponent<EraseCardDeckPanel>();
                if (panel != null)
                {
                    panel.SetEraseCardCount(count);
                }
            }
            else
            {
                Addressables.InstantiateAsync("EraseCardDeckCanvas").Completed += handle =>
                {
                    if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    {
                        var panel = handle.Result.GetComponent<EraseCardDeckPanel>();
                        if (panel != null)
                        {
                            panel.SetEraseCardCount(count);
                        }
                    }
                };
            }
        }
        else
        {
            Addressables.InstantiateAsync("EraseCardDeckCanvas").Completed += handle =>
            {
                if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    var panel = handle.Result.GetComponent<EraseCardDeckPanel>();
                    if (panel != null)
                    {
                        panel.SetEraseCardCount(count);
                    }
                }
            };
        }
    }

    public void OpenUpgradeCardUI()
    {
        PauseDialogue();

        if (UIManager.instance != null)
        {
            GameObject upgradeUI = UIManager.instance.OpenUI("UpgradeCardDeckCanvas", UILayerType.Normal, true);
            if (upgradeUI == null)
            {
                Addressables.InstantiateAsync("UpgradeCardDeckCanvas");
            }
        }
        else
        {
            Addressables.InstantiateAsync("UpgradeCardDeckCanvas");
        }
    }

    // 6. 대화 흐름 분기 제어 API
    public void OverrideNextNode(string nodeID)
    {
        luaNextNodeOverride = nodeID;
    }

    public void TriggerEndDialogue()
    {
        endDialogueTriggered = true;
    }

    // 7. 상점/회복/보물상자 UI 연동 API
    public void OpenShop()
    {
        if (ActiveInteractable is ShopNPC shopNPC)
        {
            EndDialogue();
            shopNPC.OpenShopDirectly();
        }
        else
        {
            Debug.LogWarning("[DialogueManager] ActiveInteractable이 ShopNPC가 아닙니다.");
        }
    }

    public void OpenRestore()
    {
        if (ActiveInteractable is RestoreBonfire restoreBonfire)
        {
            EndDialogue();
            restoreBonfire.OpenRestoreDirectly();
        }
        else
        {
            Debug.LogWarning("[DialogueManager] ActiveInteractable이 RestoreBonfire가 아닙니다.");
        }
    }

    public void MarkActiveInteractableUsed()
    {
        if (ActiveInteractable is RestoreBonfire restoreBonfire)
        {
            restoreBonfire.MarkAsUsed();
        }
    }

    public void OpenChest()
    {
        if (ActiveInteractable is RewardChest rewardChest)
        {
            EndDialogue();
            rewardChest.OpenChestDirectly();
        }
        else
        {
            Debug.LogWarning("[DialogueManager] ActiveInteractable이 RewardChest가 아닙니다.");
        }
    }

    #endregion

    private void OnDestroy()
    {
        if (luaEnv != null)
        {
            luaEnv.Dispose();
            luaEnv = null;
        }
    }
}
