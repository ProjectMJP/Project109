using GameItem.Types;
using System;
using System.Collections.Generic;
using System.IO;
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

    public GameObject dialogueUICanvasPrefab;
    private DialogueUI dialogueUI;

    private DialogueData activeDialogue;
    private Dictionary<string, DialogueNode> nodeMap = new Dictionary<string, DialogueNode>();
    private DialogueNode currentNode;
    private int currentLineIndex = 0;

    private readonly DialogueActionExecutor actionExecutor = new DialogueActionExecutor();

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
        }
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

        dialogueUI = spawned.GetComponentInChildren<DialogueUI>(true);

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
            dialogueUI.SetDescription(currentNode.lines[currentLineIndex]);

            Button nextBtn = dialogueUI.CreateChoiceButton("▶");
            nextBtn.onClick.AddListener(() =>
            {
                currentLineIndex++;
                ShowNextLineOrChoices();
            });
        }
        else
        {
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
                LoadAndExecuteLua(choice.luaScript, out var canSelect, out var getDesc, out _, out var isVisible);
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

    public void EndDialogue()
    {
        EnsureDialogueUI();
        if (dialogueUI != null)
        {
            dialogueUI.Close();
        }
        activeDialogue = null;
    }

    private string FindChoiceScriptPath(string luaScriptName)
    {
        string fileName = luaScriptName.EndsWith(".lua") ? luaScriptName : $"{luaScriptName}.lua";

        // 1. Core 모드 스트리밍 에셋 경로
        string streamingCorePath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Choices", fileName);
        if (File.Exists(streamingCorePath)) return streamingCorePath;

        string streamingCorePath2 = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Choice", fileName);
        if (File.Exists(streamingCorePath2)) return streamingCorePath2;

        // 2. 유저 모드 경로 (persistentDataPath/mods)
        string userModsPath = Path.Combine(Application.persistentDataPath, "mods");
        if (Directory.Exists(userModsPath))
        {
            string[] found = Directory.GetFiles(userModsPath, fileName, SearchOption.AllDirectories);
            if (found.Length > 0) return found[0];
        }

        // 3. 에디터 전용 경로 폴백
#if UNITY_EDITOR
        string editorPath = Path.Combine(Application.dataPath, "Scripts", "Lua", "Choice", fileName);
        if (File.Exists(editorPath)) return editorPath;

        string editorPath2 = Path.Combine(Application.dataPath, "Scripts", "Lua", "Choices", fileName);
        if (File.Exists(editorPath2)) return editorPath2;
#endif

        return null;
    }

    private void LoadAndExecuteLua(string luaScriptName, out CanSelectDialogueChoice canSelect, out GetDialogueChoiceDescription getDesc, out ExecuteDialogueChoice execute, out IsDialogueChoiceVisible isVisible)
    {
        canSelect = null;
        getDesc = null;
        execute = null;
        isVisible = null;

        string path = FindChoiceScriptPath(luaScriptName);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogError($"[DialogueManager] Lua 선택지 스크립트 파일을 찾을 수 없습니다: {luaScriptName}");
            return;
        }

        LuaEnv env = LuaManager.Instance != null ? LuaManager.Instance.luaEnv : null;
        if (env == null)
        {
            Debug.LogError("[DialogueManager] LuaManager.Instance.luaEnv가 초기화되지 않았습니다.");
            return;
        }

        try
        {
            // 스크립트 실행 전 전역 공간 초기화 (이전 선택지의 함수 오염 방지)
            env.DoString("CanSelect = nil; GetDescription = nil; ExecuteChoice = nil; IsVisible = nil;");

            string scriptContent = File.ReadAllText(path);
            env.DoString(scriptContent);
            canSelect = env.Global.Get<CanSelectDialogueChoice>("CanSelect");
            getDesc = env.Global.Get<GetDialogueChoiceDescription>("GetDescription");
            execute = env.Global.Get<ExecuteDialogueChoice>("ExecuteChoice");
            isVisible = env.Global.Get<IsDialogueChoiceVisible>("IsVisible");

            // 델리게이트 획득 후 전역 공간 재정리
            env.DoString("CanSelect = nil; GetDescription = nil; ExecuteChoice = nil; IsVisible = nil;");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueManager] Lua 파싱 에러 ({luaScriptName}): {e.Message}");
        }
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

    public void StartBattle(string battleName) => actionExecutor.StartBattle(battleName);
    public void RevealAllNodes() => actionExecutor.RevealAllNodes();

    [System.Obsolete("Use RevealAllNodes instead")]
    public void OpenAllExploreNodes() => RevealAllNodes();

    public void SpawnReward(string rewardTypeStr, string rarityStr) => actionExecutor.SpawnReward(rewardTypeStr, rarityStr, transform.position);

    public void OpenEraseCardUI(int count)
    {
        PauseDialogue();

        if (UIManager.instance != null)
        {
            var deckPanel = UIManager.instance.OpenUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK, UILayerType.Normal, true);
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
        EndDialogue();
        actionExecutor.OpenShop(ActiveInteractable);
    }

    public void OpenRestore()
    {
        EndDialogue();
        actionExecutor.OpenRestore(ActiveInteractable);
    }

    public void MarkActiveInteractableUsed() => actionExecutor.MarkActiveInteractableUsed(ActiveInteractable);

    public void OpenChest()
    {
        EndDialogue();
        actionExecutor.OpenChest(ActiveInteractable);
    }

    #endregion
}
