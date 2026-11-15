using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class ModLoader
{
    // 로딩된 데이터를 보관할 글로벌 딕셔너리
    // Key: effectName (고유 ID), Value: EffectData
    public Dictionary<string, EffectData> EffectDatabase { get; private set; } = new Dictionary<string, EffectData>();

    // Key: relicName (고유 ID), Value: RelicData
    public Dictionary<string, RelicData> RelicDatabase { get; private set; } = new Dictionary<string, RelicData>();

    // Key: cardName (고유 ID), Value: CardData
    public Dictionary<string, CardData> CardDatabase { get; private set; } = new Dictionary<string, CardData>();

    // Key: loadoutId (고유 ID), Value: StarterKitData
    public Dictionary<string, StarterKitData> StarterKitDatabase { get; private set; } = new Dictionary<string, StarterKitData>();

    // Key: unitId (고유 ID), Value: NPCUnitData
    public Dictionary<string, NPCUnitData> NPCUnitDatabase { get; private set; } = new Dictionary<string, NPCUnitData>();

    // Key: battleId 또는 dataPath (고유 ID), Value: BattleData
    public Dictionary<string, BattleData> BattleDatabase { get; private set; } = new Dictionary<string, BattleData>();

    // Key: dialogueID (고유 ID), Value: DialogueData
    public Dictionary<string, DialogueData> DialogueDatabase { get; private set; } = new Dictionary<string, DialogueData>();

    // Key: interactableID (고유 ID), Value: InteractableData
    public Dictionary<string, InteractableData> InteractableDatabase { get; private set; } = new Dictionary<string, InteractableData>();

    // Key: dropTableID (고유 ID), Value: DropTableData
    public Dictionary<string, DropTableData> DropTableDatabase { get; private set; } = new Dictionary<string, DropTableData>();

    private static readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private int _starterKitLoadOrder;

    private static ModLoader _instance;
    public static ModLoader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new ModLoader();
            }
            return _instance;
        }
    }

    private ModLoader() { }

    public void LoadAllMods()
    {
        EffectDatabase.Clear();
        RelicDatabase.Clear();
        CardDatabase.Clear();
        StarterKitDatabase.Clear();
        NPCUnitDatabase.Clear();
        BattleDatabase.Clear();
        DialogueDatabase.Clear();
        InteractableDatabase.Clear();
        DropTableDatabase.Clear();
        LocalizationManager.Clear();

        // 1. 바닐라(Core) 모드 로딩
        // 경로: Assets/StreamingAssets/Mods/Core
        string coreModPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core");
        _starterKitLoadOrder = 0;
        LoadModDirectory(coreModPath, true);

        // 2. 유저 모드 로딩
        // 경로: C:/Users/.../AppData/LocalLow/Company/Project109/mods
        string userModsPath = Path.Combine(Application.persistentDataPath, "mods");
        if (Directory.Exists(userModsPath))
        {
            string[] userModDirs = Directory.GetDirectories(userModsPath);
            foreach (string modDir in userModDirs)
            {
                LoadModDirectory(modDir, false);
            }
        }
        else
        {
            // 유저 모드 폴더가 없으면 자동 생성
            Directory.CreateDirectory(userModsPath);
        }



        Debug.Log($"[ModLoader] 총 {EffectDatabase.Count}개의 이펙트 데이터, {RelicDatabase.Count}개의 유물 데이터, {CardDatabase.Count}개의 카드 데이터, {StarterKitDatabase.Count}개의 시작 키트 데이터, {NPCUnitDatabase.Count}개의 적 데이터, {BattleDatabase.Count}개의 전투 데이터, {InteractableDatabase.Count}개의 상호작용 데이터, {DropTableDatabase.Count}개의 드롭 테이블 데이터가 로드되었습니다.");
    }

    private void LoadModDirectory(string modDir, bool isCoreMod = false)
    {
        if (!Directory.Exists(modDir)) return;

        Debug.Log($"[ModLoader] 모드 폴더 탐색 중: {modDir}");

        // [루아 스크립트 경로 등록]
        // 모드가 가진 Scripts 폴더를 LuaManager에 검색 경로로 등록합니다.
        // (LuaManager는 내부적으로 하위 폴더(Scripts/Effects, Scripts/Relics 등)를 모두 자동 탐색합니다)
        string scriptsPath = Path.Combine(modDir, "Scripts");
        if (Directory.Exists(scriptsPath) && LuaManager.Instance != null)
        {
            LuaManager.Instance.AddSearchPath(scriptsPath);
        }

        // [카드 태그(CardTag) 로딩]
        // 모드 폴더 하위의 "Scripts/CardTags" 폴더 내 모든 루아 스크립트를 사전 컴파일 및 등록합니다.
        string cardTagsPath = Path.Combine(modDir, "Scripts", "CardTags");
        if (Directory.Exists(cardTagsPath))
        {
            LoadCardTagsFromPath(cardTagsPath);
        }

        // [다이얼로그 선택지(Choice) 로딩]
        // 유저 모드 호환용 구형("Scripts/Choice")과 표준("Scripts/Choices") 폴더를 순서대로 사전 컴파일·캐싱합니다.
        string legacyChoicesPath = Path.Combine(modDir, "Scripts", "Choice");
        if (Directory.Exists(legacyChoicesPath))
        {
            LoadChoicesFromPath(legacyChoicesPath);
        }

        string choicesPath = Path.Combine(modDir, "Scripts", "Choices");
        if (Directory.Exists(choicesPath))
        {
            LoadChoicesFromPath(choicesPath);
        }

        // [이펙트(Effect) 로딩]
        string effectsPath = Path.Combine(modDir, "YAML", "Effects");
        if (Directory.Exists(effectsPath))
        {
            LoadDataFromPath(effectsPath, modDir, EffectDatabase, d => d.effectName, "이펙트");
        }

        // [유물(Relic) 로딩]
        string relicsPath = Path.Combine(modDir, "YAML", "Relics");
        if (Directory.Exists(relicsPath))
        {
            LoadDataFromPath(relicsPath, modDir, RelicDatabase, d => d.relicName, "유물");
        }

        // [카드(Card) 로딩]
        string cardsPath = Path.Combine(modDir, "YAML", "Cards");
        if (Directory.Exists(cardsPath))
        {
            LoadDataFromPath(cardsPath, modDir, CardDatabase, d => d.cardName, "카드");
        }

        // [시작 키트(StarterKit) 로딩]
        string starterKitsPath = Path.Combine(modDir, "YAML", "StarterKits");
        if (Directory.Exists(starterKitsPath))
        {
            LoadDataFromPath(starterKitsPath, modDir, StarterKitDatabase, d => d.loadoutId, "시작 키트", kit =>
            {
                kit.isCoreMod = isCoreMod;
                kit.loadOrder = _starterKitLoadOrder++;
            });
        }

        // [적군 AI(Enemy) 로딩]
        string enemiesPath = Path.Combine(modDir, "YAML", "Enemies");
        if (Directory.Exists(enemiesPath))
        {
            LoadDataFromPath(enemiesPath, modDir, NPCUnitDatabase, d => d.unitId, "적");
        }

        // [전투(Battle) 로딩]
        string battlesPath = Path.Combine(modDir, "YAML", "Battles");
        if (Directory.Exists(battlesPath))
        {
            LoadDataFromPath(battlesPath, modDir, BattleDatabase, d => d.ID, "전투");
        }

        // [다이얼로그(Dialogue) 로딩]
        string dialoguesPath = Path.Combine(modDir, "YAML", "Dialogues");
        if (Directory.Exists(dialoguesPath))
        {
            LoadDataFromPath(dialoguesPath, modDir, DialogueDatabase, d => d.dialogueID, "다이얼로그");
        }

        // [상호작용(Interactable) 로딩]
        string interactablesPath = Path.Combine(modDir, "YAML", "Interactables");
        if (Directory.Exists(interactablesPath))
        {
            LoadDataFromPath(interactablesPath, modDir, InteractableDatabase, d => d.interactableID, "상호작용");
        }

        // [드롭 테이블(DropTable) 로딩]
        string dropTablesPath = Path.Combine(modDir, "YAML", "DropTables");
        if (Directory.Exists(dropTablesPath))
        {
            LoadDataFromPath(dropTablesPath, modDir, DropTableDatabase, d => d.dropTableID, "드롭 테이블");
        }
        LocalizationManager.LoadFromModDirectory(modDir);
        if (false)
        {
        }
    }

    private void LoadDataFromPath<T>(string dataPath, string modRootPath, Dictionary<string, T> targetDict, System.Func<T, string> keySelector, string typeName, System.Action<T> onLoaded = null) where T : class
    {
        var deserializer = _yamlDeserializer;



        string[] yamlFiles = Directory.GetFiles(dataPath, "*.yaml", SearchOption.AllDirectories);

        foreach (string file in yamlFiles)
        {
            string yamlContent = File.ReadAllText(file);
            try
            {
                T data = deserializer.Deserialize<T>(yamlContent);
                if (data != null)
                {
                    if (data is IModAssetResolver resolver)
                    {
                        if (!resolver.ResolveAndValidate(modRootPath))
                        {
                            Debug.LogWarning($"[ModLoader] 무결성 검증 실패로 {typeName} 데이터가 무시되었습니다: {file}");
                            continue;
                        }
                    }

                    string key = keySelector(data);
                    if (!string.IsNullOrEmpty(key))
                    {
                        onLoaded?.Invoke(data);
                        targetDict[key] = data;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ModLoader] YAML 파싱 에러 ({file}):\n{e.Message}");
            }
        }
    }

    private void LoadCardTagsFromPath(string cardTagsPath)
    {
        string[] luaFiles = Directory.GetFiles(cardTagsPath, "*.lua", SearchOption.AllDirectories);
        if (LuaManager.Instance != null)
        {
            foreach (string file in luaFiles)
            {
                LuaManager.Instance.LoadCardTagScript(file);
            }
        }
    }

    private void LoadChoicesFromPath(string choicesPath)
    {
        string[] luaFiles = Directory.GetFiles(choicesPath, "*.lua", SearchOption.AllDirectories);
        if (LuaManager.Instance != null)
        {
            foreach (string file in luaFiles)
            {
                LuaManager.Instance.LoadChoiceScript(file);
            }
        }
    }

    /// <summary>
    /// 특정 층(Floor) 번호에 등장할 수 있는 랜덤 상호작용 이벤트 목록을 반환합니다.
    /// </summary>
    public List<InteractableData> GetRandomInteractablesForFloor(int floor)
    {
        List<InteractableData> list = new List<InteractableData>();
        foreach (var data in InteractableDatabase.Values)
        {
            if (data.eventAppearLevels != null && data.eventAppearLevels.Contains(floor))
            {
                list.Add(data);
            }
        }
        return list;
    }
}
