using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GameItem.Types;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XLua;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// [74] Core 모드 전체 YAML 엄격 역직렬화, 도메인 간 ID 교차 참조,
/// XLua 전수 청크 로드·샌드박스 초기화 및 UI 프리팹 무결성을 검증하는 EditMode 통합 테스트입니다.
/// </summary>
[TestFixture]
public class ModIntegrityEditTests
{
    private static readonly IDeserializer StrictDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private LuaManager _createdLuaManager;

    private static string CoreModRoot => Path.Combine(Application.streamingAssetsPath, "Mods", "Core");

    [TearDown]
    public void TearDown()
    {
        try
        {
            ModObjectFactory.DisposeCachedDescriptions();
            if (_createdLuaManager != null)
            {
                _createdLuaManager.Dispose();
                _createdLuaManager = null;
            }
        }
        finally
        {
            InvokeStaticMethod<LuaManager>("ResetStaticState");
            InvokeStaticMethod<ModLoader>("ResetStaticState");
            InvokeStaticMethod(typeof(ModObjectFactory), "ResetStaticState");

            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }
            _createdObjects.Clear();
        }
    }

    // =========================================================================
    // 1. Core 모드 9종 YAML 전수 엄격 역직렬화 (미정의 프로퍼티/타입 불일치 즉시 실패)
    // =========================================================================
    [Test]
    public void CoreMod_AllNineYamlDomains_DeserializeStrictlyWithoutUnmatchedProperties()
    {
        var errors = new List<string>();

        var cards = LoadYamlDomainStrictly<CardData>("Cards", d => d.cardName, errors);
        var relics = LoadYamlDomainStrictly<RelicData>("Relics", d => d.relicName, errors);
        var effects = LoadYamlDomainStrictly<EffectData>("Effects", d => d.effectName, errors);
        var enemies = LoadYamlDomainStrictly<NPCUnitData>("Enemies", d => d.unitId, errors);
        var battles = LoadYamlDomainStrictly<BattleData>("Battles", d => d.ID, errors);
        var starterKits = LoadYamlDomainStrictly<StarterKitData>("StarterKits", d => d.loadoutId, errors);
        var dialogues = LoadYamlDomainStrictly<DialogueData>("Dialogues", d => d.dialogueID, errors);
        var interactables = LoadYamlDomainStrictly<InteractableData>("Interactables", d => d.interactableID, errors);
        var dropTables = LoadYamlDomainStrictly<DropTableData>("DropTables", d => d.dropTableID, errors);

        Assert.IsEmpty(errors, "Core 모드 YAML 엄격 역직렬화 오류:\n" + string.Join("\n", errors));

        Assert.Greater(cards.Count, 0, "Cards YAML이 1개 이상 로드되어야 합니다.");
        Assert.Greater(relics.Count, 0, "Relics YAML이 1개 이상 로드되어야 합니다.");
        Assert.Greater(effects.Count, 0, "Effects YAML이 1개 이상 로드되어야 합니다.");
        Assert.Greater(enemies.Count, 0, "Enemies YAML이 1개 이상 로드되어야 합니다.");
        Assert.Greater(battles.Count, 0, "Battles YAML이 1개 이상 로드되어야 합니다.");
        Assert.Greater(starterKits.Count, 0, "StarterKits YAML이 1개 이상 로드되어야 합니다.");
        Assert.Greater(dialogues.Count, 0, "Dialogues YAML이 1개 이상 로드되어야 합니다.");
        Assert.Greater(interactables.Count, 0, "Interactables YAML이 1개 이상 로드되어야 합니다.");
        Assert.Greater(dropTables.Count, 0, "DropTables YAML이 1개 이상 로드되어야 합니다.");
    }

    // =========================================================================
    // 2. Core 모드 도메인 간 교차 참조(Cross-Reference) 전수 검증
    // =========================================================================
    [Test]
    public void CoreMod_AllCrossReferences_ResolveToExistingTargets()
    {
        var errors = new List<string>();

        var cards = LoadYamlDomainStrictly<CardData>("Cards", d => d.cardName, errors);
        var relics = LoadYamlDomainStrictly<RelicData>("Relics", d => d.relicName, errors);
        var effects = LoadYamlDomainStrictly<EffectData>("Effects", d => d.effectName, errors);
        var enemies = LoadYamlDomainStrictly<NPCUnitData>("Enemies", d => d.unitId, errors);
        var battles = LoadYamlDomainStrictly<BattleData>("Battles", d => d.ID, errors);
        var starterKits = LoadYamlDomainStrictly<StarterKitData>("StarterKits", d => d.loadoutId, errors);
        var dialogues = LoadYamlDomainStrictly<DialogueData>("Dialogues", d => d.dialogueID, errors);
        var interactables = LoadYamlDomainStrictly<InteractableData>("Interactables", d => d.interactableID, errors);
        var dropTables = LoadYamlDomainStrictly<DropTableData>("DropTables", d => d.dropTableID, errors);

        var cardLuaSet = CollectLuaBaseNames("Cards");
        var relicLuaSet = CollectLuaBaseNames("Relics");
        var effectLuaSet = CollectLuaBaseNames("Effects");
        var cardTagLuaSet = CollectLuaBaseNames("CardTags");
        var choiceLuaSet = CollectLuaBaseNames("Choices");

        // 2-1. CardData: Lua 스크립트, 업그레이드 대상 카드, 마스터리 태그
        foreach (CardData card in cards.Values)
        {
            if (!cardLuaSet.Contains(card.cardName))
            {
                errors.Add($"[Card:{card.cardName}] 대응 Lua 스크립트 누락 (Scripts/Cards/{card.cardName}.lua)");
            }

            if (card.isUpgradable && !string.IsNullOrEmpty(card.upgradedCardName) && !cards.ContainsKey(card.upgradedCardName))
            {
                errors.Add($"[Card:{card.cardName}] upgradedCardName '{card.upgradedCardName}'가 Cards DB에 존재하지 않습니다.");
            }

            if (card.masteryTags != null)
            {
                foreach (var kv in card.masteryTags)
                {
                    if (kv.Value == null) continue;
                    foreach (string tagName in kv.Value)
                    {
                        if (!string.IsNullOrEmpty(tagName) && !cardTagLuaSet.Contains(tagName))
                        {
                            errors.Add($"[Card:{card.cardName}] masteryTags[{kv.Key}]의 태그 '{tagName}'가 Scripts/CardTags에 존재하지 않습니다.");
                        }
                    }
                }
            }
        }

        // 2-2. RelicData: Lua 스크립트, 업그레이드 대상 유물
        foreach (RelicData relic in relics.Values)
        {
            if (!relicLuaSet.Contains(relic.relicName))
            {
                errors.Add($"[Relic:{relic.relicName}] 대응 Lua 스크립트 누락 (Scripts/Relics/{relic.relicName}.lua)");
            }

            if (!string.IsNullOrEmpty(relic.upgradedRelicId) && !relics.ContainsKey(relic.upgradedRelicId))
            {
                errors.Add($"[Relic:{relic.relicName}] upgradedRelicId '{relic.upgradedRelicId}'가 Relics DB에 존재하지 않습니다.");
            }
        }

        // 2-3. EffectData: Lua 스크립트
        foreach (EffectData effect in effects.Values)
        {
            if (!effectLuaSet.Contains(effect.effectName))
            {
                errors.Add($"[Effect:{effect.effectName}] 대응 Lua 스크립트 누락 (Scripts/Effects/{effect.effectName}.lua)");
            }
        }

        // 2-4. BattleData: 등장 적 ID
        foreach (BattleData battle in battles.Values)
        {
            if (battle.monsterNames == null || battle.monsterNames.Count == 0)
            {
                errors.Add($"[Battle:{battle.ID}] monsterNames가 비어 있습니다.");
                continue;
            }

            foreach (string monsterName in battle.monsterNames)
            {
                if (string.IsNullOrEmpty(monsterName) || !enemies.ContainsKey(monsterName))
                {
                    errors.Add($"[Battle:{battle.ID}] monsterName '{monsterName}'가 Enemies DB에 존재하지 않습니다.");
                }
            }
        }

        // 2-5. StarterKitData: 시작 카드/유물/이펙트
        foreach (StarterKitData kit in starterKits.Values)
        {
            if (kit.startCards != null)
            {
                foreach (string cardId in kit.startCards)
                {
                    if (string.IsNullOrEmpty(cardId) || !cards.ContainsKey(cardId))
                    {
                        errors.Add($"[StarterKit:{kit.loadoutId}] startCard '{cardId}'가 Cards DB에 존재하지 않습니다.");
                    }
                }
            }

            if (kit.startRelics != null)
            {
                foreach (string relicId in kit.startRelics)
                {
                    if (string.IsNullOrEmpty(relicId) || !relics.ContainsKey(relicId))
                    {
                        errors.Add($"[StarterKit:{kit.loadoutId}] startRelic '{relicId}'가 Relics DB에 존재하지 않습니다.");
                    }
                }
            }

            if (kit.startEffects != null)
            {
                foreach (StartEffectInfo effectInfo in kit.startEffects)
                {
                    if (effectInfo == null || string.IsNullOrEmpty(effectInfo.effectName) || !effects.ContainsKey(effectInfo.effectName))
                    {
                        errors.Add($"[StarterKit:{kit.loadoutId}] startEffect '{effectInfo?.effectName}'가 Effects DB에 존재하지 않습니다.");
                    }
                }
            }
        }

        // 2-6. DialogueData: 시작 노드, 선택지 nextNodeID, Choice Lua 스크립트
        foreach (DialogueData dlg in dialogues.Values)
        {
            if (dlg.nodes == null || dlg.nodes.Count == 0)
            {
                errors.Add($"[Dialogue:{dlg.dialogueID}] nodes가 비어 있습니다.");
                continue;
            }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (DialogueNode node in dlg.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.nodeID))
                {
                    errors.Add($"[Dialogue:{dlg.dialogueID}] nodeID가 비어 있는 노드가 있습니다.");
                    continue;
                }
                nodeIds.Add(node.nodeID);
            }

            if (string.IsNullOrEmpty(dlg.startNodeID) || !nodeIds.Contains(dlg.startNodeID))
            {
                errors.Add($"[Dialogue:{dlg.dialogueID}] startNodeID '{dlg.startNodeID}'가 nodes 내에 존재하지 않습니다.");
            }

            foreach (DialogueNode node in dlg.nodes)
            {
                if (node == null || node.choices == null) continue;
                foreach (ChoiceData choice in node.choices)
                {
                    if (choice == null) continue;
                    if (!string.IsNullOrEmpty(choice.nextNodeID) && !nodeIds.Contains(choice.nextNodeID))
                    {
                        errors.Add($"[Dialogue:{dlg.dialogueID}/{node.nodeID}] nextNodeID '{choice.nextNodeID}'가 존재하지 않습니다.");
                    }

                    if (!string.IsNullOrEmpty(choice.luaScript))
                    {
                        string key = LuaManager.NormalizeChoiceKey(choice.luaScript);
                        if (!choiceLuaSet.Contains(key))
                        {
                            errors.Add($"[Dialogue:{dlg.dialogueID}/{node.nodeID}] choice luaScript '{choice.luaScript}' (key={key})가 Scripts/Choices에 존재하지 않습니다.");
                        }
                    }
                }
            }
        }

        // 2-7. InteractableData: targetDialogueID, 보상 referenceID
        foreach (InteractableData interactable in interactables.Values)
        {
            if (!string.IsNullOrEmpty(interactable.targetDialogueID) && !dialogues.ContainsKey(interactable.targetDialogueID))
            {
                errors.Add($"[Interactable:{interactable.interactableID}] targetDialogueID '{interactable.targetDialogueID}'가 Dialogues DB에 존재하지 않습니다.");
            }

            if (interactable.rewardData != null && interactable.rewardData.rewards != null)
            {
                foreach (RewardItemConfig reward in interactable.rewardData.rewards)
                {
                    if (reward == null || string.IsNullOrEmpty(reward.referenceID)) continue;
                    if (reward.rewardType == RewardType.CardChoice || reward.rewardType == RewardType.Card || reward.rewardType == RewardType.Relic)
                    {
                        bool exists = dropTables.ContainsKey(reward.referenceID) ||
                                      cards.ContainsKey(reward.referenceID) ||
                                      relics.ContainsKey(reward.referenceID);
                        if (!exists)
                        {
                            errors.Add($"[Interactable:{interactable.interactableID}] reward referenceID '{reward.referenceID}'가 DropTables/Cards/Relics DB에 존재하지 않습니다.");
                        }
                    }
                }
            }
        }

        // 2-8. DropTableData: specificItemIDs
        foreach (DropTableData table in dropTables.Values)
        {
            if (table.specificItemIDs == null) continue;
            foreach (string itemId in table.specificItemIDs)
            {
                if (string.IsNullOrEmpty(itemId)) continue;
                if (!cards.ContainsKey(itemId) && !relics.ContainsKey(itemId))
                {
                    errors.Add($"[DropTable:{table.dropTableID}] specificItemID '{itemId}'가 Cards/Relics DB에 존재하지 않습니다.");
                }
            }
        }

        Assert.IsEmpty(errors, "Core 모드 교차 참조 무결성 오류:\n" + string.Join("\n", errors));
    }

    // =========================================================================
    // 3. Core 모드 전체 Lua 스크립트 XLua 청크 로드 & 샌드박스 초기화 전수 검증
    // =========================================================================
    [Test]
    public void CoreMod_AllLuaScripts_CompileAndInitializeInSandboxWithoutErrors()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();
        string scriptsRoot = Path.Combine(CoreModRoot, "Scripts");
        Assert.IsTrue(Directory.Exists(scriptsRoot), $"Core Scripts 디렉터리를 찾을 수 없습니다: {scriptsRoot}");

        luaManager.AddSearchPath(scriptsRoot);

        // 샌드박스 전역 격리 확인 (CS/io/os/package/dofile/loadfile/load == nil, debug는 traceback만 허용)
        object[] sandboxCheck = luaManager.luaEnv.DoString(
            "return CS == nil, io == nil, os == nil, package == nil, dofile == nil, loadfile == nil, load == nil, (debug == nil or (debug.sethook == nil and debug.getinfo == nil))");
        Assert.IsTrue((bool)sandboxCheck[0], "XLua 샌드박스에서 CS 전역이 제거되어야 합니다.");
        Assert.IsTrue((bool)sandboxCheck[1], "XLua 샌드박스에서 io 전역이 제거되어야 합니다.");
        Assert.IsTrue((bool)sandboxCheck[2], "XLua 샌드박스에서 os 전역이 제거되어야 합니다.");
        Assert.IsTrue((bool)sandboxCheck[3], "XLua 샌드박스에서 package 전역이 제거되어야 합니다.");
        Assert.IsTrue((bool)sandboxCheck[4], "XLua 샌드박스에서 dofile 전역이 제거되어야 합니다.");
        Assert.IsTrue((bool)sandboxCheck[5], "XLua 샌드박스에서 loadfile 전역이 제거되어야 합니다.");
        Assert.IsTrue((bool)sandboxCheck[6], "XLua 샌드박스에서 load 전역이 제거되어야 합니다.");
        Assert.IsTrue((bool)sandboxCheck[7], "XLua 샌드박스에서 debug 리플렉션 함수가 제거되어야 합니다.");

        string[] allLuaFiles = Directory.GetFiles(scriptsRoot, "*.lua", SearchOption.AllDirectories);
        Assert.Greater(allLuaFiles.Length, 0, "Core 모드 Lua 스크립트가 1개 이상 존재해야 합니다.");

        var errors = new List<string>();
        foreach (string filePath in allLuaFiles)
        {
            string relPath = filePath.Substring(scriptsRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string topFolder = relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            string baseName = Path.GetFileNameWithoutExtension(filePath);

            // 3-1. LoadString으로 Lua 5.3 구문 파싱 검증 (UTF-8 BOM 제거)
            try
            {
                string scriptText = File.ReadAllText(filePath, Encoding.UTF8);
                LuaFunction compiledChunk = luaManager.luaEnv.LoadString(scriptText, relPath);
                if (compiledChunk == null)
                {
                    errors.Add($"[LuaSyntax:{relPath}] LoadString이 null을 반환했습니다.");
                    continue;
                }
                compiledChunk.Dispose();
            }
            catch (Exception ex)
            {
                errors.Add($"[LuaSyntax:{relPath}] 구문 오류: {ex.Message}");
                continue;
            }

            // 3-2. 카테고리별 최상위 스코프 샌드박스 초기화 검증
            if (string.Equals(topFolder, "Cards", StringComparison.OrdinalIgnoreCase))
            {
                if (ModAssetLoader.TryLoadLuaPrototype(filePath, baseName, "CardData", new[] { "card" }, null, luaManager.luaEnv, out LuaTable proto))
                {
                    proto.Dispose();
                }
                else
                {
                    errors.Add($"[LuaInit:{relPath}] Card 프로토타입 로드 실패");
                }
            }
            else if (string.Equals(topFolder, "Relics", StringComparison.OrdinalIgnoreCase))
            {
                if (ModAssetLoader.TryLoadLuaPrototype(filePath, baseName, "RelicData", new[] { "relic" }, null, luaManager.luaEnv, out LuaTable proto))
                {
                    proto.Dispose();
                }
                else
                {
                    errors.Add($"[LuaInit:{relPath}] Relic 프로토타입 로드 실패");
                }
            }
            else if (string.Equals(topFolder, "Effects", StringComparison.OrdinalIgnoreCase))
            {
                if (ModAssetLoader.TryLoadLuaPrototype(filePath, baseName, "EffectData", new[] { "effect" }, null, luaManager.luaEnv, out LuaTable proto))
                {
                    proto.Dispose();
                }
                else
                {
                    errors.Add($"[LuaInit:{relPath}] Effect 프로토타입 로드 실패");
                }
            }
            else if (string.Equals(topFolder, "Enemies", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(topFolder, "NPCs", StringComparison.OrdinalIgnoreCase))
            {
                if (ModAssetLoader.TryLoadLuaPrototype(filePath, baseName, "NPCUnitData", new[] { "enemy", "npc" }, null, luaManager.luaEnv, out LuaTable proto))
                {
                    proto.Dispose();
                }
                else
                {
                    errors.Add($"[LuaInit:{relPath}] Enemy/NPC 프로토타입 로드 실패");
                }
            }
            else if (string.Equals(topFolder, "CardTags", StringComparison.OrdinalIgnoreCase))
            {
                LuaTable tagProto = luaManager.LoadCardTagScript(filePath);
                if (tagProto == null)
                {
                    errors.Add($"[LuaInit:{relPath}] CardTag 프로토타입 로드 실패");
                }
            }
            else if (string.Equals(topFolder, "Choices", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(topFolder, "Choice", StringComparison.OrdinalIgnoreCase))
            {
                if (!luaManager.LoadChoiceScript(filePath))
                {
                    errors.Add($"[LuaInit:{relPath}] Choice 격리 환경 로드 실패");
                }
            }
        }

        Assert.IsEmpty(errors, "Core 모드 Lua 전수 검증 오류:\n" + string.Join("\n", errors));
    }

    // =========================================================================
    // 4. 핵심 UI 프리팹 Missing Script, 중첩 Canvas 및 UIManager 레이어 바인딩 검증
    // =========================================================================
    [Test]
    public void UIPrefabs_AllHaveNoMissingScripts_NoNestedCanvas_AndCoreBindingsValid()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab/UI" });
        Assert.Greater(prefabGuids.Length, 0, "Assets/Prefab/UI 하위에 프리팹이 1개 이상 존재해야 합니다.");

        var errors = new List<string>();
        UIManager uiManagerPrefabComp = null;

        foreach (string guid in prefabGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                errors.Add($"[UIPrefab:{assetPath}] 프리팹 로드 실패");
                continue;
            }

            // 4-1. Missing Script 검사
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    errors.Add($"[UIPrefab:{assetPath}] Missing Script 컴포넌트가 감지되었습니다.");
                    break;
                }
            }

            // 4-2. 중첩 Canvas / CanvasScaler 금지 검사 (UIManager, FadeCanvas 제외)
            string fileName = Path.GetFileName(assetPath);
            bool isAllowedCanvasRoot = string.Equals(fileName, "UIManager.prefab", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(fileName, "FadeCanvas.prefab", StringComparison.OrdinalIgnoreCase);
            if (!isAllowedCanvasRoot)
            {
                if (prefab.GetComponentsInChildren<Canvas>(true).Length > 0)
                {
                    errors.Add($"[UIPrefab:{assetPath}] 중첩 Canvas 컴포넌트가 감지되었습니다 (UI_PREFAB_GUIDELINE 위반).");
                }
                if (prefab.GetComponentsInChildren<CanvasScaler>(true).Length > 0)
                {
                    errors.Add($"[UIPrefab:{assetPath}] 중첩 CanvasScaler 컴포넌트가 감지되었습니다 (UI_PREFAB_GUIDELINE 위반).");
                }
            }

            if (string.Equals(fileName, "UIManager.prefab", StringComparison.OrdinalIgnoreCase))
            {
                uiManagerPrefabComp = prefab.GetComponent<UIManager>();
            }
        }

        // 4-3. UIManager.prefab 핵심 4개 레이어 캔버스 바인딩 확인
        Assert.IsNotNull(uiManagerPrefabComp, "Assets/Prefab/UI/UIManager.prefab에서 UIManager 컴포넌트를 찾을 수 없습니다.");
        if (uiManagerPrefabComp.worldUILayer == null) errors.Add("[UIManager.prefab] worldUILayer가 null입니다.");
        if (uiManagerPrefabComp.normalUILayer == null) errors.Add("[UIManager.prefab] normalUILayer가 null입니다.");
        if (uiManagerPrefabComp.topUILayer == null) errors.Add("[UIManager.prefab] topUILayer가 null입니다.");
        if (uiManagerPrefabComp.popupUILayer == null) errors.Add("[UIManager.prefab] popupUILayer가 null입니다.");

        Assert.IsEmpty(errors, "UI 프리팹 무결성 오류:\n" + string.Join("\n", errors));
    }

    // =========================================================================
    // 5. 부정 검증(Negative Guard): 엄격 YAML 파서 및 XLua LoadString이 불량 입력을 반드시 거부하는지 검증
    // =========================================================================
    [Test]
    public void StrictValidationMachinery_RejectsUnknownYamlKey_AndBrokenLuaSyntax()
    {
        const string invalidCardYaml = "cardName: \"NegativeTestCard\"\nunknownTypoField: 999\n";
        Assert.Throws<YamlException>(() => StrictDeserializer.Deserialize<CardData>(invalidCardYaml),
            "엄격 YAML 역직렬화는 미정의 프로퍼티를 즉시 예외로 거부해야 합니다.");

        LuaManager luaManager = CreateIsolatedLuaManager();
        const string brokenLua = "local function broken(\n  return 1\n";
        Assert.Throws<LuaException>(() =>
        {
            LuaFunction fn = luaManager.luaEnv.LoadString(brokenLua, "NegativeSyntaxTest.lua");
            fn?.Dispose();
        }, "XLua LoadString은 구문이 깨진 Lua 스크립트를 즉시 LuaException으로 거부해야 합니다.");
    }

    // =========================================================================
    // 내부 헬퍼
    // =========================================================================
    private static Dictionary<string, T> LoadYamlDomainStrictly<T>(
        string yamlSubFolder,
        Func<T, string> keySelector,
        List<string> errors) where T : class
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        string folderPath = Path.Combine(CoreModRoot, "YAML", yamlSubFolder);
        if (!Directory.Exists(folderPath))
        {
            errors.Add($"[YAML:{yamlSubFolder}] 폴더를 찾을 수 없습니다: {folderPath}");
            return result;
        }

        string[] yamlFiles = Directory.GetFiles(folderPath, "*.yaml", SearchOption.AllDirectories);
        foreach (string filePath in yamlFiles)
        {
            string fileName = Path.GetFileName(filePath);
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                T data = StrictDeserializer.Deserialize<T>(content);
                if (data == null)
                {
                    errors.Add($"[YAML:{yamlSubFolder}/{fileName}] 역직렬화 결과가 null입니다.");
                    continue;
                }

                string key = keySelector(data);
                if (string.IsNullOrWhiteSpace(key))
                {
                    errors.Add($"[YAML:{yamlSubFolder}/{fileName}] 고유 ID가 비어 있습니다.");
                    continue;
                }

                if (result.ContainsKey(key))
                {
                    errors.Add($"[YAML:{yamlSubFolder}/{fileName}] 중복 ID 감지: '{key}'");
                    continue;
                }

                result[key] = data;
            }
            catch (Exception ex)
            {
                errors.Add($"[YAML:{yamlSubFolder}/{fileName}] 파싱 실패: {ex.Message}");
            }
        }

        return result;
    }

    private static HashSet<string> CollectLuaBaseNames(string scriptSubFolder)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string folderPath = Path.Combine(CoreModRoot, "Scripts", scriptSubFolder);
        if (!Directory.Exists(folderPath)) return set;

        foreach (string file in Directory.GetFiles(folderPath, "*.lua", SearchOption.AllDirectories))
        {
            set.Add(Path.GetFileNameWithoutExtension(file));
        }
        return set;
    }

    private LuaManager CreateIsolatedLuaManager()
    {
        InvokeStaticMethod<LuaManager>("ResetStaticState");
        InvokeStaticMethod<ModLoader>("ResetStaticState");
        InvokeStaticMethod(typeof(ModObjectFactory), "ResetStaticState");

        GameObject luaGo = new GameObject("TestModIntegrityLuaManager");
        _createdObjects.Add(luaGo);

        LuaManager luaManager = luaGo.AddComponent<LuaManager>();
        SetStaticField<LuaManager>("_instance", luaManager);
        SetStaticField<LuaManager>("_isShuttingDown", false);
        InvokePrivateMethod(luaManager, "InitLuaEnv");
        _createdLuaManager = luaManager;
        return luaManager;
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} 메서드를 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private static void InvokeStaticMethod<T>(string methodName)
    {
        InvokeStaticMethod(typeof(T), methodName);
    }

    private static void InvokeStaticMethod(Type targetType, string methodName)
    {
        MethodInfo method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{targetType.Name}.{methodName} 정적 메서드를 찾을 수 없습니다.");
        method.Invoke(null, null);
    }

    private static void SetStaticField<T>(string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{typeof(T).Name}.{fieldName} 정적 필드를 찾을 수 없습니다.");
        field.SetValue(null, value);
    }
}
