using UnityEngine;
using XLua;
using System.IO;
using System.Collections.Generic;

public class LuaManager : MonoBehaviour
{
    private static LuaManager _instance;
    private static bool _isShuttingDown = false;

    /// <summary>애플리케이션 종료 진행 여부.</summary>
    public static bool IsShuttingDown => _isShuttingDown;

    /// <summary>싱글턴과 LuaEnv 네이티브 상태가 모두 유효한지 안전하게 확인합니다. (Instance 생성 부수효과 없음)</summary>
    public static bool IsLuaEnvValid =>
        _instance != null &&
        !_isShuttingDown &&
        _instance.luaEnv != null &&
        _instance.luaEnv.rawL != System.IntPtr.Zero;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _isShuttingDown = false;
        _instance = null;
    }

    public static LuaManager Instance 
    { 
        get 
        { 
            if (_isShuttingDown || !Application.isPlaying) return null;

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<LuaManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[LuaManager]");
                    _instance = go.AddComponent<LuaManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance; 
        } 
    }
    
    public LuaEnv luaEnv { get; private set; }
    
    // 외부(ModLoader 등)에서 동적으로 추가해줄 루아 스크립트 검색 경로들
    private List<string> searchPaths = new List<string>();

    // Key: 파일명(예: "Strike.lua"), Value: 전체 디스크 경로 (런타임 디스크 전수 스캔 방지용 O(1) 인덱스)
    private readonly Dictionary<string, string> _scriptPathIndex = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    // Key: tagName, Value: 프로토타입 LuaTable
    private Dictionary<string, LuaTable> cardTagPrototypes = new Dictionary<string, LuaTable>();

    // NewInstance 델리게이트 캐싱 (반복 호출 시 GC Alloc 방지)
    private System.Func<LuaTable, LuaTable> _cachedNewInstance;

    /// <summary>
    /// 사전 로드 및 컴파일된 Choice Lua 스크립트의 격리 환경과 델리게이트 캐시 엔트리입니다.
    /// </summary>
    private sealed class ChoiceScriptEntry
    {
        public LuaTable Env;
        public CanSelectDialogueChoice CanSelect;
        public GetDialogueChoiceDescription GetDescription;
        public ExecuteDialogueChoice ExecuteChoice;
        public IsDialogueChoiceVisible IsVisible;
        public System.Action ResetState;

        public void ClearCallbacks()
        {
            CanSelect = null;
            GetDescription = null;
            ExecuteChoice = null;
            IsVisible = null;
            ResetState = null;
        }
    }

    // Key: 정규화된 Choice 스크립트 이름 (확장자·경로 제외, 대소문자 무시)
    private readonly Dictionary<string, ChoiceScriptEntry> _choiceScripts =
        new Dictionary<string, ChoiceScriptEntry>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Choice 스크립트 식별자 또는 파일 경로를 확장자 및 경로 없는 정규 키로 변환합니다.
    /// </summary>
    public static string NormalizeChoiceKey(string choiceNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(choiceNameOrPath))
        {
            return string.Empty;
        }

        string normalized = choiceNameOrPath.Trim().Replace('\\', '/');
        int lastSlash = normalized.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            normalized = normalized.Substring(lastSlash + 1);
        }

        if (normalized.EndsWith(".lua", System.StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - 4);
        }

        return normalized;
    }

    /// <summary>
    /// Choice Lua 스크립트를 스크립트별 격리 환경(LuaTable)으로 1회 컴파일하고 델리게이트를 캐싱합니다.
    /// 동일 이름의 스크립트가 이미 등록되어 있으면 기존 환경을 안전하게 해제하고 덮어씁니다(모드 오버라이드 지원).
    /// </summary>
    public bool LoadChoiceScript(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            return false;
        }

        if (luaEnv == null || luaEnv.rawL == System.IntPtr.Zero)
        {
            Debug.LogError("[LuaManager] LuaEnv가 초기화되지 않아 Choice 스크립트를 로드할 수 없습니다.");
            return false;
        }

        string choiceKey = NormalizeChoiceKey(filePath);
        if (string.IsNullOrEmpty(choiceKey))
        {
            return false;
        }

        LuaTable scriptEnv = luaEnv.NewTable();
        LuaTable meta = luaEnv.NewTable();
        meta.Set("__index", luaEnv.Global);
        scriptEnv.SetMetaTable(meta);
        meta.Dispose();

        try
        {
            // UTF-8 BOM을 안전하게 제거해 Lua 파서 오류를 방지합니다.
            string scriptText = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            byte[] scriptBytes = System.Text.Encoding.UTF8.GetBytes(scriptText);
            luaEnv.DoString(scriptBytes, choiceKey, scriptEnv);

            var entry = new ChoiceScriptEntry
            {
                Env = scriptEnv,
                CanSelect = scriptEnv.Get<CanSelectDialogueChoice>("CanSelect"),
                GetDescription = scriptEnv.Get<GetDialogueChoiceDescription>("GetDescription"),
                ExecuteChoice = scriptEnv.Get<ExecuteDialogueChoice>("ExecuteChoice"),
                IsVisible = scriptEnv.Get<IsDialogueChoiceVisible>("IsVisible"),
                ResetState = scriptEnv.Get<System.Action>("ResetState") ?? scriptEnv.Get<System.Action>("ResetChoiceState")
            };

            if (_choiceScripts.TryGetValue(choiceKey, out ChoiceScriptEntry previous) && previous != null)
            {
                previous.ClearCallbacks();
                if (previous.Env != null)
                {
                    previous.Env.Dispose();
                    previous.Env = null;
                }
            }

            _choiceScripts[choiceKey] = entry;
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LuaManager] Choice 스크립트 로드 오류 ({choiceKey}, {filePath}): {ex.Message}\n{ex.StackTrace}");
            if (luaEnv != null && luaEnv.rawL != System.IntPtr.Zero)
            {
                scriptEnv.Dispose();
            }
            return false;
        }
    }

    /// <summary>
    /// 사전 캐싱된 Choice 스크립트 콜백 델리게이트를 O(1)로 조회합니다.
    /// </summary>
    public bool TryGetChoiceCallbacks(
        string choiceScriptName,
        out CanSelectDialogueChoice canSelect,
        out GetDialogueChoiceDescription getDesc,
        out ExecuteDialogueChoice execute,
        out IsDialogueChoiceVisible isVisible)
    {
        canSelect = null;
        getDesc = null;
        execute = null;
        isVisible = null;

        string choiceKey = NormalizeChoiceKey(choiceScriptName);
        if (string.IsNullOrEmpty(choiceKey))
        {
            return false;
        }

        if (_choiceScripts.TryGetValue(choiceKey, out ChoiceScriptEntry entry) && entry != null)
        {
            canSelect = entry.CanSelect;
            getDesc = entry.GetDescription;
            execute = entry.ExecuteChoice;
            isVisible = entry.IsVisible;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 특정 Choice 스크립트의 Upvalue 상태 초기화 훅(ResetState/ResetChoiceState)을 실행합니다.
    /// </summary>
    public bool ResetChoiceState(string choiceScriptName)
    {
        if (luaEnv == null || luaEnv.rawL == System.IntPtr.Zero)
        {
            return false;
        }

        string choiceKey = NormalizeChoiceKey(choiceScriptName);
        if (string.IsNullOrEmpty(choiceKey))
        {
            return false;
        }

        if (_choiceScripts.TryGetValue(choiceKey, out ChoiceScriptEntry entry) && entry != null && entry.ResetState != null)
        {
            try
            {
                entry.ResetState.Invoke();
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LuaManager] Choice 스크립트 상태 초기화 오류 ({choiceKey}): {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// 캐싱된 모든 Choice 스크립트의 Upvalue 상태 초기화 훅(ResetState/ResetChoiceState)을 일괄 실행합니다.
    /// </summary>
    public void ResetAllChoiceStates()
    {
        if (luaEnv == null || luaEnv.rawL == System.IntPtr.Zero)
        {
            return;
        }

        foreach (var pair in _choiceScripts)
        {
            ChoiceScriptEntry entry = pair.Value;
            if (entry == null || entry.ResetState == null)
            {
                continue;
            }

            try
            {
                entry.ResetState.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LuaManager] Choice 스크립트 상태 초기화 오류 ({pair.Key}): {ex.Message}");
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void DisposeChoiceScripts()
    {
        bool canDisposeTables = luaEnv != null && luaEnv.rawL != System.IntPtr.Zero;
        foreach (var pair in _choiceScripts)
        {
            ChoiceScriptEntry entry = pair.Value;
            if (entry == null)
            {
                continue;
            }

            entry.ClearCallbacks();
            if (canDisposeTables && entry.Env != null)
            {
                entry.Env.Dispose();
                entry.Env = null;
            }
        }

        _choiceScripts.Clear();
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (luaEnv == null)
            {
                InitLuaEnv();
            }
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        Tick();
    }

    private void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            try
            {
                Dispose();
            }
            finally
            {
                _instance = null;
            }
        }
    }

    private void InitLuaEnv()
    {
        luaEnv = new LuaEnv();
        
        // XLua 커스텀 로더 등록
        luaEnv.AddLoader(CustomModLoader);

        // 전역 모딩 헬퍼 함수 주입
        luaEnv.DoString(@"
            -- 프로토타입으로부터 개별 인스턴스 테이블 생성 함수
            function NewInstance(proto)
                if not proto then return nil end
                local inst = {}
                setmetatable(inst, { __index = proto })
                return inst
            end

            -- 이펙트 정의 헬퍼 함수
            function DefineEffect(name)
                local effect = {}
                function effect:OnInit(effectBase)
                    self.base = effectBase
                end
                _G[name] = effect -- return 누락 시 폴백용 전역 등록
                return effect
            end

            -- 유물 정의 헬퍼 함수
            function DefineRelic(name)
                local relic = {}
                function relic:OnInit(relicBase)
                    self.base = relicBase
                end
                _G[name] = relic -- return 누락 시 폴백용 전역 등록
                return relic
            end

            -- 카드 태그 정의 헬퍼 함수
            function DefineCardTag(name)
                local cardTag = {}
                function cardTag:OnInit(tagBase, card)
                    self.base = tagBase
                    self.card = card
                end
                _G[name] = cardTag -- return 누락 시 폴백용 전역 등록
                return cardTag
            end

            -- 화이트리스트 테이블 생성 및 CS 전역 공간 제거
            EventStructs = {
                DamageInfo = CS.EventStructs.DamageInfo,
                HealInfo = CS.EventStructs.HealInfo,
                StaminaInfo = CS.EventStructs.StaminaInfo,
                ShieldInfo = CS.EventStructs.ShieldInfo,
                CardInfo = CS.EventStructs.CardInfo,
                MoveInfo = CS.EventStructs.MoveInfo,
                EffectInfo = CS.EventStructs.EffectInfo,

                DamageFlag = CS.EventStructs.DamageFlag,
                HealFlag = CS.EventStructs.HealFlag,
                StaminaFlag = CS.EventStructs.StaminaFlag,
                ShieldFlag = CS.EventStructs.ShieldFlag,
                CardFlag = CS.EventStructs.CardFlag,
                MoveFlag = CS.EventStructs.MoveFlag,
                EffectFlag = CS.EventStructs.EffectFlag,
                
                CardTag = CS.CardTag,
            }

            -- 루아 스크립트에서의 연산 및 디버깅을 위해 필수 유니티 유틸리티 클래스 주입
            UnityEngine = {
                Debug = CS.UnityEngine.Debug,
                Mathf = CS.UnityEngine.Mathf,
                Random = CS.UnityEngine.Random,
                Vector3 = CS.UnityEngine.Vector3,
                Vector2 = CS.UnityEngine.Vector2,
                Vector2Int = CS.UnityEngine.Vector2Int,
            }

            -- 두 개의 시계(Two-Clock) 상태이상(Stun 턴 종료 등)에서 안전하게 접근할 수 있도록 싱글턴 참조 주입
            RunManager = CS.RunManager

            CS = nil

            -- 샌드박스 보안 강화: 파일/프로세스/디버그 리플렉션 및 동적 로드 함수 차단 (XLua 에러 트레이스백만 유지)
            io = nil
            os = nil
            debug = debug and { traceback = debug.traceback } or nil
            package = nil
            dofile = nil
            loadfile = nil
            load = nil
        ");
    }

    /// <summary>
    /// 모드 로딩 시 루아 스크립트가 존재하는 폴더 경로를 등록하고 파일명 인덱스를 구축합니다.
    /// (나중에 로드된 유저 모드가 동일 키를 덮어써 오버라이드를 보장합니다.)
    /// </summary>
    public void AddSearchPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        if (!searchPaths.Contains(path))
        {
            searchPaths.Add(path);
        }

        if (Directory.Exists(path))
        {
            string[] luaFiles = Directory.GetFiles(path, "*.lua", SearchOption.AllDirectories);
            for (int i = 0; i < luaFiles.Length; i++)
            {
                string fileName = Path.GetFileName(luaFiles[i]);
                if (!string.IsNullOrEmpty(fileName))
                {
                    _scriptPathIndex[fileName] = luaFiles[i];
                }
            }
        }
    }

    private byte[] CustomModLoader(ref string filepath)
    {
        string fileName = filepath;
        if (!fileName.EndsWith(".lua"))
        {
            fileName += ".lua";
        }

        string justFileName = Path.GetFileName(fileName);
        if (!string.IsNullOrEmpty(justFileName) && _scriptPathIndex.TryGetValue(justFileName, out string indexedPath) && File.Exists(indexedPath))
        {
            return File.ReadAllBytes(indexedPath);
        }

        // 인덱스에 없을 경우 검색 경로 순회 후 캐싱
        foreach (string searchPath in searchPaths)
        {
            if (Directory.Exists(searchPath))
            {
                string[] files = Directory.GetFiles(searchPath, justFileName, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    _scriptPathIndex[justFileName] = files[0];
                    return File.ReadAllBytes(files[0]);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// XLua 내부의 가비지 컬렉션을 수행합니다.
    /// </summary>
    public void Tick()
    {
        if (luaEnv != null && luaEnv.rawL != System.IntPtr.Zero)
        {
            luaEnv.Tick();
        }
    }

    /// <summary>
    /// 프로토타입 LuaTable로부터 독립된 런타임 인스턴스 LuaTable을 생성합니다.
    /// NewInstance 델리게이트를 캐싱하여 GC Alloc을 최소화합니다.
    /// </summary>
    public LuaTable NewInstance(LuaTable proto)
    {
        if (proto == null || luaEnv == null) return null;

        try
        {
            _cachedNewInstance ??= luaEnv.Global.Get<System.Func<LuaTable, LuaTable>>("NewInstance");
            return _cachedNewInstance?.Invoke(proto);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LuaManager] NewInstance 호출 실패:\n{e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 카드 태그 루아 스크립트 파일을 읽어 프로토타입으로 컴파일 및 캐시합니다.
    /// </summary>
    public LuaTable LoadCardTagScript(string filePath)
    {
        if (!File.Exists(filePath) || luaEnv == null) return null;

        string tagName = Path.GetFileNameWithoutExtension(filePath);
        try
        {
            byte[] scriptBytes = File.ReadAllBytes(filePath);
            object[] results = luaEnv.DoString(scriptBytes, tagName);
            LuaTable resultProto = null;
            if (results != null && results.Length > 0)
            {
                resultProto = results[0] as LuaTable;
            }

            if (resultProto == null)
            {
                resultProto = luaEnv.Global.Get<LuaTable>(tagName);
            }

            if (resultProto != null)
            {
                cardTagPrototypes[tagName] = resultProto;
            }
            else
            {
                Debug.LogError($"[LuaManager] 카드 태그 {tagName} 로드 실패: 리턴된 루아 테이블이 없습니다 ({filePath}).");
            }

            // 글로벌 네임스페이스 오염 방지
            luaEnv.Global.Set<string, object>(tagName, null);
            return resultProto;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LuaManager] 카드 태그 {tagName} 루아 컴파일 오류 ({filePath}):\n{e.Message}");
        }

        return null;
    }

    /// <summary>
    /// 외부에서 카드 태그 프로토타입을 직접 등록합니다.
    /// </summary>
    public void RegisterCardTagPrototype(string tagName, LuaTable proto)
    {
        if (string.IsNullOrEmpty(tagName) || proto == null) return;
        cardTagPrototypes[tagName] = proto;
    }

    /// <summary>
    /// 모드가 등록한 경로들에서 카드 태그 스크립트(CardTags/{tagName}.lua)를 검색하여 캐싱 및 반환합니다.
    /// 사전 로드된 태그가 있을 경우 딕셔너리에서 O(1)로 즉시 반환합니다.
    /// </summary>
    public LuaTable GetCardTagPrototype(string tagName)
    {
        if (string.IsNullOrEmpty(tagName)) return null;

        if (cardTagPrototypes.TryGetValue(tagName, out var proto))
        {
            return proto;
        }

        // 사전 등록되지 않은 태그에 대한 런타임 온디맨드 검색 (인덱스 O(1) 조회 우선)
        string scriptFileName = tagName + ".lua";
        if (_scriptPathIndex.TryGetValue(scriptFileName, out string indexedPath) && File.Exists(indexedPath))
        {
            return LoadCardTagScript(indexedPath);
        }

        foreach (string searchPath in searchPaths)
        {
            if (Directory.Exists(searchPath))
            {
                string[] files = Directory.GetFiles(searchPath, scriptFileName, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    _scriptPathIndex[scriptFileName] = files[0];
                    return LoadCardTagScript(files[0]);
                }
            }
        }

        Debug.LogError($"[LuaManager] 카드 태그 {tagName}.lua 파일을 찾을 수 없습니다.");
        return null;
    }

    /// <summary>
    /// XLua 환경 및 내부 캐시를 안전하게 파괴합니다.
    /// </summary>
    public void Dispose()
    {
        _cachedNewInstance = null;
        cardTagPrototypes.Clear();
        _scriptPathIndex.Clear();
        DisposeChoiceScripts();

        if (luaEnv != null)
        {
            LuaEnv envToDispose = luaEnv;
            luaEnv = null;

            if (envToDispose.rawL != System.IntPtr.Zero)
            {
                envToDispose.Dispose();
            }
        }
    }
}
