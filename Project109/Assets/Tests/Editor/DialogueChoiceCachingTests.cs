using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using XLua;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

[TestFixture]
public class DialogueChoiceCachingTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<string> _tempDirectories = new List<string>();
    private LuaManager _createdLuaManager;

    [TearDown]
    public void TearDown()
    {
        if (_createdLuaManager != null)
        {
            _createdLuaManager.Dispose();
            _createdLuaManager = null;
        }

        SetStaticField<LuaManager>("_instance", null);
        SetStaticField<LuaManager>("_isShuttingDown", false);
        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        SetStaticAutoProperty<DialogueManager>("Instance", null);
        SetStaticAutoProperty<UIManager>("instance", null);
        SetStaticAutoProperty<GameSceneManager>("instance", null);
        SetStaticAutoProperty<AssetCacheManager>("instance", null);
        ModLoader.Instance.DialogueDatabase.Clear();

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();

        for (int i = 0; i < _tempDirectories.Count; i++)
        {
            string dir = _tempDirectories[i];
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        _tempDirectories.Clear();
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private LuaManager CreateIsolatedLuaManager()
    {
        GameObject luaGo = Track(new GameObject("TestLuaManager"));
        LuaManager luaManager = luaGo.AddComponent<LuaManager>();
        SetStaticField<LuaManager>("_instance", luaManager);
        SetStaticField<LuaManager>("_isShuttingDown", false);
        InvokePrivateMethod(luaManager, "InitLuaEnv");
        _createdLuaManager = luaManager;
        return luaManager;
    }

    private DialogueManager CreateTestDialogueManager()
    {
        GameObject dmGo = Track(new GameObject("TestDialogueManager"));
        DialogueManager dm = dmGo.AddComponent<DialogueManager>();
        InvokePrivateMethod(dm, "Awake");
        return dm;
    }

    private string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "Project109_ChoiceTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    [Test]
    public void NormalizeChoiceKey_HandlesPathsExtensionsAndCase()
    {
        Assert.AreEqual("EventTest_Battle", LuaManager.NormalizeChoiceKey("EventTest_Battle"));
        Assert.AreEqual("EventTest_Battle", LuaManager.NormalizeChoiceKey("EventTest_Battle.lua"));
        Assert.AreEqual("EventTest_Battle", LuaManager.NormalizeChoiceKey("Choices/EventTest_Battle"));
        Assert.AreEqual("EventTest_Battle", LuaManager.NormalizeChoiceKey("Choice\\EventTest_Battle.LUA"));
        Assert.AreEqual("EventTest_Battle", LuaManager.NormalizeChoiceKey("  C:/Mods/Core/Scripts/Choices/EventTest_Battle.lua  "));
        Assert.AreEqual(string.Empty, LuaManager.NormalizeChoiceKey("   "));
        Assert.AreEqual(string.Empty, LuaManager.NormalizeChoiceKey(null));
    }

    [Test]
    public void LoadChoiceScript_AllCoreChoiceScriptsLoadAndResolveDialogueYamlReferences()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();
        RunCoreChoiceScriptsVerification(luaManager);
        ClearStackSlot();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunCoreChoiceScriptsVerification(LuaManager luaManager)
    {
        string coreChoicesDir = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Choices");
        Assert.IsTrue(Directory.Exists(coreChoicesDir), $"Core Choices 폴더가 존재해야 합니다: {coreChoicesDir}");

        string[] luaFiles = Directory.GetFiles(coreChoicesDir, "*.lua", SearchOption.AllDirectories);
        Assert.Greater(luaFiles.Length, 0, "Core Choice Lua 스크립트가 1개 이상 존재해야 합니다.");

        foreach (string file in luaFiles)
        {
            bool loaded = luaManager.LoadChoiceScript(file);
            Assert.IsTrue(loaded, $"Choice 스크립트 로드 실패: {file}");

            string stem = Path.GetFileNameWithoutExtension(file);
            Assert.IsTrue(
                luaManager.TryGetChoiceCallbacks(stem, out _, out _, out _, out _),
                $"기본 이름으로 Choice 콜백 조회 실패: {stem}");
            Assert.IsTrue(
                luaManager.TryGetChoiceCallbacks(stem.ToLowerInvariant() + ".lua", out _, out _, out _, out _),
                $"확장자·대소문자 무시 조회 실패: {stem}.lua");
            Assert.IsTrue(
                luaManager.TryGetChoiceCallbacks("Choices/" + stem, out _, out _, out _, out _),
                $"경로 접두 포함 조회 실패: Choices/{stem}");
        }

        // 전역 환경(luaEnv.Global)이 Choice 함수로 오염되지 않고 격리되어야 합니다.
        Assert.IsNull(luaManager.luaEnv.Global.Get<CanSelectDialogueChoice>("CanSelect"));
        Assert.IsNull(luaManager.luaEnv.Global.Get<GetDialogueChoiceDescription>("GetDescription"));
        Assert.IsNull(luaManager.luaEnv.Global.Get<ExecuteDialogueChoice>("ExecuteChoice"));
        Assert.IsNull(luaManager.luaEnv.Global.Get<IsDialogueChoiceVisible>("IsVisible"));

        // Core 다이얼로그 YAML에서 참조하는 모든 luaScript가 사전 캐시에 존재하는지 교차 검증합니다.
        string dialoguesDir = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "YAML", "Dialogues");
        string[] yamlFiles = Directory.GetFiles(dialoguesDir, "*.yaml", SearchOption.AllDirectories);
        Assert.Greater(yamlFiles.Length, 0);

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        foreach (string yamlFile in yamlFiles)
        {
            DialogueData dialogue = deserializer.Deserialize<DialogueData>(File.ReadAllText(yamlFile));
            Assert.IsNotNull(dialogue, $"YAML 역직렬화 실패: {yamlFile}");
            if (dialogue.nodes == null) continue;

            foreach (DialogueNode node in dialogue.nodes)
            {
                if (node == null || node.choices == null) continue;
                foreach (ChoiceData choice in node.choices)
                {
                    if (choice == null || string.IsNullOrEmpty(choice.luaScript)) continue;
                    Assert.IsTrue(
                        luaManager.TryGetChoiceCallbacks(choice.luaScript, out _, out _, out _, out _),
                        $"YAML({dialogue.dialogueID})의 Choice luaScript '{choice.luaScript}'가 캐시에 없습니다.");
                }
            }
        }

        // LoseCard / LoseRelic 스크립트가 ResetState 훅을 정상 노출하는지 검증합니다.
        Assert.IsTrue(luaManager.ResetChoiceState("EventTest_LoseCardGetGold"));
        Assert.IsTrue(luaManager.ResetChoiceState("EventTest_LoseRelicGetGold"));
    }

    [Test]
    public void LoadChoiceScript_IsolatesUpvalueStateAcrossScriptsAndResetsOnDemand()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();
        DialogueManager dm = CreateTestDialogueManager();
        string tempDir = CreateTempDir();

        string scriptAPath = Path.Combine(tempDir, "ChoiceAlpha.lua");
        string scriptBPath = Path.Combine(tempDir, "ChoiceBeta.lua");

        File.WriteAllText(scriptAPath, @"
local selectedIndex = -1
function CanSelect(dm)
    return selectedIndex == -1
end
function GetDescription(dm)
    if selectedIndex == -1 then
        selectedIndex = 42
    end
    return 'A:' .. tostring(selectedIndex)
end
function ExecuteChoice(dm)
    dm:OverrideNextNode('AFTER_A_' .. tostring(selectedIndex))
end
function ResetState()
    selectedIndex = -1
end
", System.Text.Encoding.UTF8);

        File.WriteAllText(scriptBPath, @"
local selectedIndex = -1
function CanSelect(dm)
    return true
end
function GetDescription(dm)
    if selectedIndex == -1 then
        selectedIndex = 7
    end
    return 'B:' .. tostring(selectedIndex)
end
function ExecuteChoice(dm)
    dm:OverrideNextNode('AFTER_B_' .. tostring(selectedIndex))
end
function ResetChoiceState()
    selectedIndex = -1
end
", System.Text.Encoding.UTF8);

        Assert.IsTrue(luaManager.LoadChoiceScript(scriptAPath));
        Assert.IsTrue(luaManager.LoadChoiceScript(scriptBPath));

        RunIsolatedUpvalueAssertions(luaManager, dm);
        ClearStackSlot();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunIsolatedUpvalueAssertions(LuaManager luaManager, DialogueManager dm)
    {
        Assert.IsTrue(luaManager.TryGetChoiceCallbacks("ChoiceAlpha", out var canA, out var descA, out var execA, out var visA));
        Assert.IsTrue(luaManager.TryGetChoiceCallbacks("ChoiceBeta", out var canB, out var descB, out var execB, out _));
        Assert.IsNull(visA);

        // 같은 노드 내 평가(GetDescription) 후 실행(ExecuteChoice)이 동일한 Upvalue를 유지해야 합니다.
        Assert.IsTrue(canA(dm));
        Assert.AreEqual("A:42", descA(dm));
        Assert.IsFalse(canA(dm), "selectedIndex == 42 상태에서 CanSelect는 false여야 합니다.");
        Assert.AreEqual("B:7", descB(dm), "스크립트 B의 Upvalue는 스크립트 A와 격리되어야 합니다.");
        Assert.AreEqual("A:42", descA(dm), "스크립트 B 실행이 스크립트 A의 상태를 오염시키면 안 됩니다.");

        execA(dm);
        Assert.AreEqual("AFTER_A_42", GetPrivateField<string>(dm, "luaNextNodeOverride"));
        execB(dm);
        Assert.AreEqual("AFTER_B_7", GetPrivateField<string>(dm, "luaNextNodeOverride"));

        // 노드 전환/종료 시 일괄 초기화로 잔존 상태가 리셋되어야 합니다.
        luaManager.ResetAllChoiceStates();
        Assert.IsTrue(canA(dm), "ResetAllChoiceStates 호출 후 ChoiceAlpha 상태가 초기화되어야 합니다.");

        execB(dm);
        Assert.AreEqual("AFTER_B_-1", GetPrivateField<string>(dm, "luaNextNodeOverride"));

        canA = null;
        descA = null;
        execA = null;
        visA = null;
        canB = null;
        descB = null;
        execB = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ClearStackSlot()
    {
        // Mono 보수적 스택 스캔이 이전 프레임의 DelegateBridge 레지스터/슬롯을 붙잡지 않도록 스택 슬롯을 덮어씁니다.
        Span<byte> scrub = stackalloc byte[512];
        scrub.Clear();
    }

    [Test]
    public void LoadChoiceScript_SameKeyOverride_ReplacesPreviousCallbacksCleanly()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();
        DialogueManager dm = CreateTestDialogueManager();
        string tempDir = CreateTempDir();
        string coreDir = Path.Combine(tempDir, "Core");
        string modDir = Path.Combine(tempDir, "UserMod");
        Directory.CreateDirectory(coreDir);
        Directory.CreateDirectory(modDir);

        string coreScript = Path.Combine(coreDir, "OverrideTarget.lua");
        string modScript = Path.Combine(modDir, "OverrideTarget.lua");

        File.WriteAllText(coreScript, "function GetDescription(dm) return 'CORE' end", System.Text.Encoding.UTF8);
        File.WriteAllText(modScript, "function GetDescription(dm) return 'USER_MOD' end", System.Text.Encoding.UTF8);

        RunSameKeyOverrideAssertions(luaManager, dm, coreScript, modScript);
        ClearStackSlot();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunSameKeyOverrideAssertions(LuaManager luaManager, DialogueManager dm, string coreScript, string modScript)
    {
        Assert.IsTrue(luaManager.LoadChoiceScript(coreScript));
        Assert.IsTrue(luaManager.TryGetChoiceCallbacks("OverrideTarget", out _, out var coreDesc, out _, out _));
        Assert.AreEqual("CORE", coreDesc(dm));
        coreDesc = null;

        Assert.IsTrue(luaManager.LoadChoiceScript(modScript));
        Assert.IsTrue(luaManager.TryGetChoiceCallbacks("OverrideTarget", out _, out var modDesc, out _, out _));
        Assert.AreEqual("USER_MOD", modDesc(dm));
        modDesc = null;
    }

    [Test]
    public void DialogueManager_LifecycleAndCallerGuards_FailFastWithoutCrash()
    {
        GameObject hostGo = Track(new GameObject("MockUIManagerHost"));
        DialogueManager first = hostGo.AddComponent<DialogueManager>();
        InvokePrivateMethod(first, "Awake");
        Assert.AreSame(first, DialogueManager.Instance);

        // 동일 GameObject에 중복 DialogueManager 추가 시 컴포넌트만 파괴되고 호스트 오브젝트는 유지되어야 합니다.
        GameObject secondGo = Track(new GameObject("DuplicateDMHost"));
        DialogueManager second = secondGo.AddComponent<DialogueManager>();
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("중복 DialogueManager"));
        InvokePrivateMethod(second, "Awake");
        Assert.AreSame(first, DialogueManager.Instance);
        Assert.IsTrue(second == null, "중복 DialogueManager 컴포넌트만 제거되어야 합니다.");
        Assert.IsTrue(secondGo != null, "중복 컴포넌트 제거 시 호스트 GameObject까지 파괴하면 안 됩니다.");

        // UIManager.instance == null 상태에서 StartDialogue는 예외 없이 Fail-Fast 로그만 남겨야 합니다.
        DialogueData dummyData = new DialogueData
        {
            dialogueID = "Test_FailFast",
            startNodeID = "START",
            nodes = new List<DialogueNode>()
        };
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("UIManager\\.instance가 null"));
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("DialogueUI를 초기화할 수 없어"));
        Assert.DoesNotThrow(() => first.StartDialogue(dummyData));
        Assert.IsFalse(first.IsDialogueActive);

        // OnDestroy 시 Instance 참조가 깨끗이 해제되어야 합니다.
        InvokePrivateMethod(first, "OnDestroy");
        Assert.IsNull(DialogueManager.Instance);

        // DialogueManager.Instance == null 상태에서 InteractableObject / RestoreBonfire 호출 시 Fail-Fast 에러 로그 확인
        GameObject interactGo = Track(new GameObject("TestInteractable"));
        InteractableObject interactable = interactGo.AddComponent<InteractableObject>();
        SetPrivateField(interactable, "interactableData", new InteractableData { targetDialogueID = "Test_ID" });
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("DialogueManager\\.Instance가 존재하지 않아"));
        Assert.DoesNotThrow(() => interactable.OnInteract());

        GameObject bonfireGo = Track(new GameObject("TestBonfire"));
        RestoreBonfire bonfire = bonfireGo.AddComponent<RestoreBonfire>();
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("DialogueManager\\.Instance가 존재하지 않아"));
        Assert.DoesNotThrow(() => bonfire.OnInteract());
    }

    [Test]
    public void DialogueUI_ClickToAdvance_SkipsTypingAdvancesLinesAndStopsAtChoices()
    {
        GameObject inputGo = Track(new GameObject("TestPlayerInputController"));
        PlayerInputController inputCtrl = inputGo.AddComponent<PlayerInputController>();
        SetStaticField<PlayerInputController>("_instance", inputCtrl);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);

        GameObject cacheGo = Track(new GameObject("TestAssetCacheManager"));
        AssetCacheManager cache = cacheGo.AddComponent<AssetCacheManager>();
        SetStaticAutoProperty<AssetCacheManager>("instance", cache);

        GameObject dialoguePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/NPC/DialogueUI.prefab");
        GameObject uiManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/UIManager.prefab");
        Assert.IsNotNull(dialoguePrefab, "DialogueUI.prefab을 찾을 수 없습니다.");
        Assert.IsNotNull(uiManagerPrefab, "UIManager.prefab을 찾을 수 없습니다.");

        SetPrivateField(cache, "uiDict", new Dictionary<string, GameObject>
        {
            { UIConstants.PANEL_DIALOGUE, dialoguePrefab }
        });

        GameObject uiManagerGo = Track(UnityEngine.Object.Instantiate(uiManagerPrefab));
        UIManager uiManager = uiManagerGo.GetComponent<UIManager>();
        DialogueManager dm = uiManagerGo.GetComponent<DialogueManager>();
        Assert.IsNotNull(uiManager);
        Assert.IsNotNull(dm);
        InvokePrivateMethod(uiManager, "Awake");
        InvokePrivateMethod(dm, "Awake");

        DialogueData data = new DialogueData
        {
            dialogueID = "Test_ClickToAdvance",
            startNodeID = "START",
            nodes = new List<DialogueNode>
            {
                new DialogueNode
                {
                    nodeID = "START",
                    speakerName = "테스터",
                    lines = new List<string>
                    {
                        "첫 번째 대사입니다.",
                        "두 번째 대사입니다."
                    },
                    choices = new List<ChoiceData>
                    {
                        new ChoiceData
                        {
                            description = "대화를 끝낸다",
                            nextNodeID = "END_DIALOGUE",
                            luaScript = string.Empty
                        }
                    }
                }
            }
        };

        dm.StartDialogue(data);
        DialogueUI ui = GetPrivateField<DialogueUI>(dm, "dialogueUI");
        Assert.IsNotNull(ui);
        Assert.IsTrue(dm.IsDialogueActive);
        Assert.IsFalse(dm.IsWaitingForChoice);
        Assert.IsTrue(ui.IsTyping, "대사 시작 직후 타이핑 코루틴이 실행 중이어야 합니다.");
        Assert.AreEqual("첫 번째 대사입니다.", ui.description.text);

        // 1. 선택지/넘기기 버튼 영역 버블링 클릭은 무시되어야 합니다.
        List<Button> activeButtons = GetPrivateField<List<Button>>(ui, "_activeButtons");
        Assert.AreEqual(1, activeButtons.Count);
        PointerEventData buttonAreaClick = new PointerEventData(EventSystem.current)
        {
            pointerPress = activeButtons[0].gameObject,
            pointerCurrentRaycast = new RaycastResult { gameObject = activeButtons[0].gameObject }
        };
        ui.OnPointerClick(buttonAreaClick);
        Assert.IsTrue(ui.IsTyping, "선택지 버튼 영역 클릭은 본문 스킵/진행을 중복 실행하면 안 됩니다.");

        // 2. 타이핑 중 본문 클릭 시 1회 클릭으로 스킵만 수행하고 줄을 넘기지 않아야 합니다.
        PointerEventData bodyClick = new PointerEventData(EventSystem.current)
        {
            pointerPress = ui.description.gameObject,
            pointerCurrentRaycast = new RaycastResult { gameObject = ui.description.gameObject }
        };
        ui.OnPointerClick(bodyClick);
        Assert.IsFalse(ui.IsTyping, "타이핑 중 클릭 시 즉시 타이핑이 스킵되어야 합니다.");
        Assert.AreEqual(0, GetPrivateField<int>(dm, "currentLineIndex"));
        Assert.AreEqual("첫 번째 대사입니다.", ui.description.text);

        // 3. 타이핑 완료 상태에서 본문 클릭 시 다음 대사로 진행해야 합니다.
        ui.OnPointerClick(bodyClick);
        Assert.AreEqual(1, GetPrivateField<int>(dm, "currentLineIndex"));
        Assert.AreEqual("두 번째 대사입니다.", ui.description.text);
        Assert.IsFalse(dm.IsWaitingForChoice);
        Assert.IsTrue(ui.IsTyping);

        // 4. 유지된 "▶" 버튼으로도 스킵 및 다음 진행(선택지 노출)이 동일 로직으로 동작해야 합니다.
        activeButtons = GetPrivateField<List<Button>>(ui, "_activeButtons");
        Assert.AreEqual(1, activeButtons.Count);
        activeButtons[0].onClick.Invoke();
        Assert.IsFalse(ui.IsTyping);
        Assert.AreEqual(1, GetPrivateField<int>(dm, "currentLineIndex"));

        activeButtons[0].onClick.Invoke();
        Assert.IsTrue(dm.IsWaitingForChoice, "마지막 대사 이후에는 선택지 대기 상태여야 합니다.");
        activeButtons = GetPrivateField<List<Button>>(ui, "_activeButtons");
        Assert.AreEqual(1, activeButtons.Count);
        StringAssert.Contains("대화를 끝낸다", activeButtons[0].GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text);

        // 5. 선택지 대기 중 본문 클릭으로는 대화가 진행되거나 닫히지 않아야 합니다.
        ui.OnPointerClick(bodyClick);
        Assert.IsTrue(dm.IsDialogueActive);
        Assert.IsTrue(dm.IsWaitingForChoice);

        // 6. 선택지 클릭 시 대화가 정상 종료되어야 합니다.
        activeButtons[0].onClick.Invoke();
        Assert.IsFalse(dm.IsDialogueActive);
        Assert.IsFalse(dm.IsWaitingForChoice);
        Assert.IsFalse(ui.gameObject.activeSelf);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        Type type = target.GetType();
        FieldInfo field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            type = type.BaseType;
        }
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
        field.SetValue(target, value);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} 메서드를 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
        return (T)field.GetValue(target);
    }

    private static void SetStaticField<T>(string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{typeof(T).Name}.{fieldName} 정적 필드를 찾을 수 없습니다.");
        field.SetValue(null, value);
    }

    private static void SetStaticAutoProperty<T>(string propertyName, object value)
    {
        FieldInfo backingField = typeof(T).GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(backingField, $"{typeof(T).Name}.{propertyName} 정적 자동 프로퍼티 백킹 필드를 찾을 수 없습니다.");
        backingField.SetValue(null, value);
    }
}
