using System.IO;
using XLua;
using YamlDotNet.Serialization;

public class NPCUnitData : IModAssetResolver, IIdentifiable
{
    private static readonly string[] _luaFallbackGlobals = { "enemy", "npc" };

    // 유닛 고유 식별 ID (기존 enemyId에서 변경)
    public string unitId { get; set; }
    public string prefabAddress { get; set; }  // Addressables 모델 프리팹 주소 (미지정 시 unitId 사용)
    public string className { get; set; }
    public string monsterType { get; set; }
    public int appearLevel { get; set; }
    
    public string ID => unitId;

    public string displayName { get; set; }

    [YamlIgnore]
    public string LocalizedName => LocalizationManager.GetOrDefault($"{ID}.name", !string.IsNullOrEmpty(displayName) ? displayName : unitId);
    
    // 캐릭터 기본 스탯 정보
    public CharacterStat characterStat { get; set; }
    
    // 몬스터 처치 보상 정보 통합 (아군 소환수 등은 미사용)
    public int minDropGoldAmount { get; set; }
    public int maxDropGoldAmount { get; set; }

    [YamlIgnore]
    public LuaTable luaPrototype { get; set; }

    /// <summary>
    /// 프로토타입 LuaTable 네이티브 참조를 안전하게 해제합니다.
    /// </summary>
    public void DisposeLuaResources()
    {
        if (luaPrototype != null)
        {
            if (LuaManager.IsLuaEnvAlive)
            {
                luaPrototype.Dispose();
            }
            luaPrototype = null;
        }
    }

    public bool ResolveAndValidate(string modDirectory)
    {
        // 루아 스크립트 검증 및 사전 로딩 (NPCs 또는 Enemies 폴더 호환성 지원)
        string expectedScriptPath = Path.Combine(modDirectory, "Scripts", "NPCs", unitId + ".lua");
        if (!File.Exists(expectedScriptPath))
        {
            expectedScriptPath = Path.Combine(modDirectory, "Scripts", "Enemies", unitId + ".lua");
        }

        if (File.Exists(expectedScriptPath))
        {
            if (ModAssetLoader.TryLoadLuaPrototype(expectedScriptPath, unitId, "NPCUnitData", _luaFallbackGlobals, null, out LuaTable proto))
            {
                this.luaPrototype = proto;
                return true;
            }
            return false;
        }

        return true;
    }
}
