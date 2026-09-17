using UnityEngine;
using XLua;
using System.IO;
using System.Collections.Generic;

public class LuaManager : MonoBehaviour
{
    private static LuaManager _instance;
    private static bool _isShuttingDown = false;

    public static LuaManager Instance 
    { 
        get 
        { 
            if (_isShuttingDown) return null;

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

    // Key: tagName, Value: 프로토타입 LuaTable
    private Dictionary<string, LuaTable> cardTagPrototypes = new Dictionary<string, LuaTable>();

    // NewInstance 델리게이트 캐싱 (반복 호출 시 GC Alloc 방지)
    private System.Func<LuaTable, LuaTable> _cachedNewInstance;

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
            Dispose();
            _instance = null;
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

            CS = nil
        ");
    }

    /// <summary>
    /// 모드 로딩 시 루아 스크립트가 존재하는 폴더 경로를 등록합니다.
    /// </summary>
    public void AddSearchPath(string path)
    {
        if (!searchPaths.Contains(path))
        {
            searchPaths.Add(path);
        }
    }

    private byte[] CustomModLoader(ref string filepath)
    {
        string fileName = filepath;
        if (!fileName.EndsWith(".lua"))
        {
            fileName += ".lua";
        }

        // 등록된 모든 검색 경로를 순회하며 파일을 찾음
        foreach (string searchPath in searchPaths)
        {
            if (Directory.Exists(searchPath))
            {
                // 해당 모드 경로 내 모든 하위 디렉토리에서 검색
                string[] files = Directory.GetFiles(searchPath, fileName, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return File.ReadAllBytes(files[0]);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// XLua 내부의 가비지 컬렉션을 수행합니다.
    /// 어딘가(예: RunManager의 Update)에서 주기적으로 호출해주는 것이 좋습니다.
    /// </summary>
    public void Tick()
    {
        luaEnv?.Tick();
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

        // 사전 등록되지 않은 태그에 대한 런타임 온디맨드 검색 (폴백)
        string scriptFileName = tagName + ".lua";
        foreach (string searchPath in searchPaths)
        {
            if (Directory.Exists(searchPath))
            {
                string[] files = Directory.GetFiles(searchPath, scriptFileName, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
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

        if (luaEnv != null)
        {
            luaEnv.Dispose();
            luaEnv = null;
        }
    }
}
