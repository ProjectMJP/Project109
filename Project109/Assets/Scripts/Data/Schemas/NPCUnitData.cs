using System.Collections.Generic;
using System.IO;
using XLua;
using YamlDotNet.Serialization;

public class NPCUnitData : IModAssetResolver
{
    // 유닛 고유 식별 ID (기존 enemyId에서 변경)
    public string unitId { get; set; }
    public string className { get; set; }
    public string monsterType { get; set; }
    public int appearLevel { get; set; }
    
    // 캐릭터 기본 스탯 정보
    public CharacterStat characterStat { get; set; }
    
    // 몬스터 처치 보상 정보 통합 (아군 소환수 등은 미사용)
    public int minDropGoldAmount { get; set; }
    public int maxDropGoldAmount { get; set; }

    [YamlIgnore]
    public LuaTable luaPrototype { get; set; }

    public bool ResolveAndValidate(string modDirectory)
    {
        bool isValid = true;
        
        // 루아 스크립트 검증 및 사전 로딩 (NPCs 또는 Enemies 폴더 호환성 지원)
        string expectedScriptPath = Path.Combine(modDirectory, "Scripts", "NPCs", unitId + ".lua");
        if (!File.Exists(expectedScriptPath))
        {
            expectedScriptPath = Path.Combine(modDirectory, "Scripts", "Enemies", unitId + ".lua");
        }

        if (File.Exists(expectedScriptPath))
        {
            try
            {
                byte[] scriptBytes = File.ReadAllBytes(expectedScriptPath);
                object[] results = LuaManager.Instance.luaEnv.DoString(scriptBytes, unitId);
                
                LuaTable proto = null;
                if (results != null && results.Length > 0)
                {
                    proto = results[0] as LuaTable;
                }

                // 폴백: 리턴 누락 시 글로벌에서 검색
                if (proto == null)
                {
                    proto = LuaManager.Instance.luaEnv.Global.Get<LuaTable>("enemy");
                    if (proto == null)
                    {
                        proto = LuaManager.Instance.luaEnv.Global.Get<LuaTable>("npc");
                    }
                    if (proto == null)
                    {
                        proto = LuaManager.Instance.luaEnv.Global.Get<LuaTable>(unitId);
                    }
                }

                if (proto != null)
                {
                    this.luaPrototype = proto;
                }
                else
                {
                    UnityEngine.Debug.LogError($"[NPCUnitData: {unitId}] 루아 파일 로드 실패: 테이블 형식이 아닙니다.");
                    isValid = false;
                }

                // 글로벌 오염 방지 해제
                LuaManager.Instance.luaEnv.Global.Set<string, object>("enemy", null);
                LuaManager.Instance.luaEnv.Global.Set<string, object>("npc", null);
                LuaManager.Instance.luaEnv.Global.Set<string, object>(unitId, null);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[NPCUnitData: {unitId}] 루아 구문 오류:\n{e.Message}");
                isValid = false;
            }
        }

        return isValid;
    }
}
