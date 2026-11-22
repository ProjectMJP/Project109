using GameItem.Types;
using System;
using System.Collections.Generic;
using UnityEngine;
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

/// <summary>
/// 다이얼로그 UI 표시, 지문 출력 및 선택지 분기 처리를 전담하는 L4 UI 매니저입니다.
/// 인게임 게임플레이 액션(보상, 회복, 전투 등)은 DialogueActionExecutor로 위임합니다.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    public bool IsDialogueActive => activeDialogue != null;

    /// <summary>
    /// 외부 시스템(유물, 퀘스트, 상태 효과 등)에서 다이얼로그 선택지를 동적으로 추가할 수 있는 이벤트 훅입니다.
    /// </summary>
    public static event Action<DialogueNode, List<ChoiceData>> OnPopulateChoices;

    private DialogueUI dialogueUI;

    private DialogueData activeDialogue;
    private Dictionary<string, DialogueNode> nodeMap = new Dictionary<string, DialogueNode>();
    private DialogueNode currentNode;
    private int currentLineIndex = 0;

    private readonly DialogueActionExecutor actionExecutor = new DialogueActionExecutor();

    private string luaNextNodeOverride = null;
    private bool endDialogueTriggered = false;
    private bool _isWaitingForChoice = false;

    public bool IsWaitingForChoice => _isWaitingForChoice;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[DialogueManager] 중복 DialogueManager 인스턴스가 감지되어 컴포넌트를 제거합니다.");
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(this);
                return;
            }
#endif
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (dialogueUI != null)
        {
            dialogueUI.OnAdvanceRequested -= HandleAdvanceRequested;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureDialogueUI()
    {
        if (dialogueUI != null) return;

        if (UIManager.instance == null)
        {
            Debug.LogError("[DialogueManager] UIManager.instance가 null이라 DialogueUI를 열 수 없습니다.");
            return;
        }

        DialogueUI opened = UIManager.instance.OpenUI<DialogueUI>(UIConstants.PANEL_DIALOGUE, UILayerType.Normal, false);
        if (opened == null)
        {
            Debug.LogError("[DialogueManager] UIManager를 통해 DialogueUI를 획득하지 못했습니다. Addressables(UI_Prefabs) 및 UIConstants.PANEL_DIALOGUE 등록을 확인하세요.");
            return;
        }

        dialogueUI = opened;
        dialogueUI.OnAdvanceRequested -= HandleAdvanceRequested;
        dialogueUI.OnAdvanceRequested += HandleAdvanceRequested;
        dialogueUI.gameObject.SetActive(false);
    }

    public InteractableObject ActiveInteractable { get; private set; }

    /// <summary>
    /// 외부(예: NPCInteraction)에서 다이얼로그 대화를 개시할 때 호출하는 진입점입니다.
    /// </summary>
    public void StartDialogue(DialogueData data, InteractableObject trigger = null, string overrideStartNodeID = null)
    {
        if (data == null)
        {
            Debug.LogWarning("[DialogueManager] StartDialogue 호출에 빈 데이터가 들어왔습니다.");
            return;
        }

        EnsureDialogueUI();
        if (dialogueUI == null)
        {
            Debug.LogError("[DialogueManager] DialogueUI를 초기화할 수 없어 대화를 시작하지 못했습니다.");
            return;
        }

        ResetRuntimeState();

        ActiveInteractable = trigger;
        activeDialogue = data;
        nodeMap.Clear();
        foreach (var node in data.nodes)
        {
            nodeMap[node.nodeID] = node;
        }

        dialogueUI.Open();

        string initialNodeID = !string.IsNullOrEmpty(overrideStartNodeID) && nodeMap.ContainsKey(overrideStartNodeID)
            ? overrideStartNodeID
            : data.startNodeID;
        GoToNode(initialNodeID);
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

        if (LuaManager.Instance != null)
        {
            LuaManager.Instance.ResetAllChoiceStates();
        }

        currentNode = nodeMap[nodeID];
        currentLineIndex = 0;
        luaNextNodeOverride = null;
        endDialogueTriggered = false;

        ShowNextLineOrChoices();
    }

    private void ResetRuntimeState()
    {
        if (LuaManager.Instance != null)
        {
            LuaManager.Instance.ResetAllChoiceStates();
        }

        currentNode = null;
        currentLineIndex = 0;
        isDialoguePaused = false;
        pendingNextNodeID = null;
        luaNextNodeOverride = null;
        endDialogueTriggered = false;
        _isWaitingForChoice = false;
    }

    /// <summary>
    /// 대사의 다음 줄을 순차적으로 출력하거나, 대사가 전부 끝났다면 선택지를 세팅합니다.
    /// </summary>
    public void ShowNextLineOrChoices()
    {
        if (currentNode == null || dialogueUI == null) return;

        if (currentNode.lines != null && currentLineIndex < currentNode.lines.Count)
        {
            _isWaitingForChoice = false;
            dialogueUI.ClearChoiceButton();
            dialogueUI.SetDescription(currentNode.lines[currentLineIndex]);

            Button nextBtn = dialogueUI.CreateChoiceButton("▶");
            if (nextBtn != null)
            {
                nextBtn.onClick.AddListener(HandleAdvanceRequested);
            }
        }
        else
        {
            _isWaitingForChoice = true;
            SetupChoiceButtons();
        }
    }

    private void HandleAdvanceRequested()
    {
        if (!IsDialogueActive || isDialoguePaused || currentNode == null || _isWaitingForChoice)
        {
            return;
        }

        if (dialogueUI != null && dialogueUI.IsTyping)
        {
            dialogueUI.SkipTyping();
            return;
        }

        if (currentNode.lines != null && currentLineIndex < currentNode.lines.Count)
        {
            currentLineIndex++;
            ShowNextLineOrChoices();
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

        OnPopulateChoices?.Invoke(currentNode, choicesToDisplay);

        if (choicesToDisplay.Count == 0)
        {
            Button closeBtn = dialogueUI.CreateChoiceButton("대화를 끝마친다.");
            closeBtn.onClick.AddListener(EndDialogue);
            return;
        }

        foreach (var choice in choicesToDisplay)
        {
            string finalDescription = choice.description;
            bool isSelectable = true;

            if (!string.IsNullOrEmpty(choice.luaScript))
            {
                TryGetChoiceCallbacks(choice.luaScript, out var canSelect, out var getDesc, out _, out var isVisible);
                if (isVisible != null && !isVisible(this))
                {
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

    private void OnChoiceClicked(ChoiceData choice)
    {
        if (!IsDialogueActive || isDialoguePaused || !_isWaitingForChoice)
        {
            return;
        }

        _isWaitingForChoice = false;
        if (dialogueUI != null)
        {
            dialogueUI.SetChoiceButtonsInteractable(false);
        }

        if (!string.IsNullOrEmpty(choice.luaScript))
        {
            TryGetChoiceCallbacks(choice.luaScript, out _, out _, out var execute, out _);
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

    public void EndDialogue()
    {
        bool shouldCloseUI = activeDialogue != null || (dialogueUI != null && dialogueUI.gameObject.activeSelf);

        activeDialogue = null;
        ActiveInteractable = null;
        ResetRuntimeState();

        if (shouldCloseUI && dialogueUI != null)
        {
            dialogueUI.Close();
        }

        if (RunManager.instance != null && RunManager.instance.HasPendingRewards)
        {
            RunManager.instance.OpenPendingRewardUI();
        }
    }

    private bool TryGetChoiceCallbacks(
        string luaScriptName,
        out CanSelectDialogueChoice canSelect,
        out GetDialogueChoiceDescription getDesc,
        out ExecuteDialogueChoice execute,
        out IsDialogueChoiceVisible isVisible)
    {
        canSelect = null;
        getDesc = null;
        execute = null;
        isVisible = null;

        if (LuaManager.Instance == null)
        {
            Debug.LogError("[DialogueManager] LuaManager.Instance가 초기화되지 않았습니다.");
            return false;
        }

        if (!LuaManager.Instance.TryGetChoiceCallbacks(luaScriptName, out canSelect, out getDesc, out execute, out isVisible))
        {
            Debug.LogError($"[DialogueManager] 사전 로드된 Choice Lua 스크립트를 찾을 수 없습니다: {luaScriptName}");
            return false;
        }

        return true;
    }

    #region Action Delegation to DialogueActionExecutor (44개 기존 Lua 스크립트 100% 호환 포워딩)

    public int GetGold() => actionExecutor.GetGold();
    public void ConsumeGold(int amount) => actionExecutor.ConsumeGold(amount);
    public void AddGold(int amount) => actionExecutor.AddGold(amount);

    public int GetCardCount() => actionExecutor.GetCardCount();
    public string GetCardNameAt(int index) => actionExecutor.GetCardNameAt(index);
    public void AddCard(string cardName) => actionExecutor.AddCard(cardName);
    public void RemoveCardAt(int index) => actionExecutor.RemoveCardAt(index);

    public int GetRelicCount() => actionExecutor.GetRelicCount();
    public string GetRelicNameAt(int index) => actionExecutor.GetRelicNameAt(index);
    public void RemoveRelicAt(int index) => actionExecutor.RemoveRelicAt(index);
    public void RemoveRelicByName(string relicName) => actionExecutor.RemoveRelicByName(relicName);
    public bool HasRelic(string relicName) => actionExecutor.HasRelic(relicName);

    public int GetCurrentHealth() => actionExecutor.GetCurrentHealth();
    public int GetMaxHealth() => actionExecutor.GetMaxHealth();
    public void HealPlayer(int amount) => actionExecutor.HealPlayer(amount);
    public void DamagePlayer(int amount) => actionExecutor.DamagePlayer(amount);
    public void ModifyMaxHealth(int amount) => actionExecutor.ModifyMaxHealth(amount);
    public void HealWithOverhealToMaxHealth(int healAmount, float overhealRatio) => actionExecutor.HealWithOverhealToMaxHealth(healAmount, overhealRatio);

    public void StartBattle(string battleName) => actionExecutor.StartBattle(battleName);
    public void RevealAllNodes() => actionExecutor.RevealAllNodes();

    public void SpawnReward(string rewardTypeStr, string rarityStr)
    {
        Vector3 spawnPos = ActiveInteractable != null ? ActiveInteractable.transform.position : transform.position;
        actionExecutor.SpawnReward(rewardTypeStr, rarityStr, spawnPos);
    }

    public void OpenEraseCardUI(int count)
    {
        PauseDialogue();

        if (UIManager.instance != null)
        {
            var deckPanel = UIManager.instance.OpenUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK, UILayerType.Popup, true);
            if (deckPanel != null)
            {
                var player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
                var cards = (player != null && player.deck != null) ? player.deck.GetCards() : null;
                if (cards != null)
                {
                    deckPanel.Open("제거할 카드를 선택하세요", cards, CardViewMode.SingleSelect, count,
                        onConfirm: (selectedCards) =>
                        {
                            if (player != null && player.deck != null && selectedCards != null)
                            {
                                for (int i = 0; i < selectedCards.Count; i++)
                                {
                                    if (selectedCards[i] != null)
                                    {
                                        player.deck.RemoveCard(selectedCards[i].runtimeID);
                                    }
                                }
                            }
                            ResumeDialogue();
                        },
                        onCancel: () =>
                        {
                            ResumeDialogue();
                        });
                }
            }
        }
    }

    public void OpenUpgradeCardUI()
    {
        PauseDialogue();
        if (UIManager.instance != null)
        {
            UIManager.instance.OpenUI(UIConstants.PANEL_UPGRADE_CARD_DECK, UILayerType.Normal, true);
        }
    }

    public void OverrideNextNode(string nodeID) => luaNextNodeOverride = nodeID;
    public void TriggerEndDialogue() => endDialogueTriggered = true;

    public void OpenShop()
    {
        InteractableObject targetInteractable = ActiveInteractable;
        EndDialogue();
        actionExecutor.OpenShop(targetInteractable);
    }

    public void OpenRestore()
    {
        InteractableObject targetInteractable = ActiveInteractable;
        EndDialogue();
        actionExecutor.OpenRestore(targetInteractable);
    }

    public void MarkActiveInteractableUsed() => actionExecutor.MarkActiveInteractableUsed(ActiveInteractable);

    public void OpenChest()
    {
        InteractableObject targetInteractable = ActiveInteractable;
        EndDialogue();
        actionExecutor.OpenChest(targetInteractable);
    }

    #endregion
}
