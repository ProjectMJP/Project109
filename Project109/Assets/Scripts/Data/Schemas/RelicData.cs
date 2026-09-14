using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLua;
using YamlDotNet.Serialization;

public class RelicData : IModAssetResolver
{
    // relicName이 고유 식별자(ID) 역할을 겸합니다.
    public string relicName { get; set; }

    // 등장 가능한 캐릭터 클래스 목록.
    // 비어있거나 "All" 또는 "None" 포함 시 공용 유물로 취급됩니다.
    // YAML 예시 (전사/마법사 전용): classTypes:\n  - Warrior\n  - Wizard
    public List<string> classTypes { get; set; } = new List<string>();

    public GameItem.Types.RelicRarity rarity { get; set; }

    // 업그레이드 시 변환될 유물의 ID (null이거나 비어있으면 업그레이드 불가)
    public string upgradedRelicId { get; set; }

    // 효과에 대한 기계적 설명 (예: "매 턴 시작 시 힘 +1")
    // {placeholder} 문법으로 동적 수치 표현 가능 (예: "공격력 {damage}만큼 피해")
    public string description { get; set; }

    // 분위기/세계관 텍스트. 효과 설명과 분리된 풍미 문구입니다.
    public string flavorText { get; set; }

    /// <summary>
    /// 플레이어의 클래스에 이 유물이 등장할 수 있는지 체크합니다.
    /// </summary>
    public bool CanAppearForClass(string playerClass)
    {
        if (classTypes == null || classTypes.Count == 0 || classTypes.Contains("None") || classTypes.Contains("All"))
        {
            return true;
        }
        return classTypes.Contains(playerClass);
    }

    [YamlIgnore]
    public Sprite iconSprite { get; set; }

    [YamlIgnore]
    public LuaTable luaPrototype { get; set; }

    public bool ResolveAndValidate(string modDirectory)
    {
        bool isValid = true;

        // 1. 아이콘 스프라이트 링크 (relicName으로 자동 추론)
        string expectedImagePath = Path.Combine(modDirectory, "Icons", "Relics", relicName + ".png");

        if (File.Exists(expectedImagePath))
        {
            // 최대 128x128 픽셀로 제한하여 로드
            this.iconSprite = ImageLoader.LoadCustomSprite(expectedImagePath, 128);
        }
        else
        {
            // 전용 아이콘이 없을 경우 기본 아이콘(Default.png)으로 대체
            string defaultImagePath = Path.Combine(modDirectory, "Icons", "Relics", "Default.png");
            if (File.Exists(defaultImagePath))
            {
                this.iconSprite = ImageLoader.LoadCustomSprite(defaultImagePath, 128);
            }
            else
            {
                Debug.LogWarning($"[RelicData: {relicName}] 전용 아이콘 및 기본 아이콘(Default.png)을 찾을 수 없지만 실행을 계속합니다.");
            }
        }

        // 2. 루아 스크립트 검증 및 사전 로딩 (relicName으로 경로 자동 추론)
        string expectedScriptPath = Path.Combine(modDirectory, "Scripts", "Relics", relicName + ".lua");
        if (File.Exists(expectedScriptPath))
        {
            try
            {
                byte[] scriptBytes = File.ReadAllBytes(expectedScriptPath);

                // 루아 스크립트 실행 (컴파일 및 프로토타입 획득)
                object[] results = LuaManager.Instance.luaEnv.DoString(scriptBytes, relicName);

                LuaTable proto = null;
                if (results != null && results.Length > 0)
                {
                    proto = results[0] as LuaTable;
                }

                // [폴백] return이 누락되었을 경우를 대비하여 전역 환경에서 변수 검색
                if (proto == null)
                {
                    proto = LuaManager.Instance.luaEnv.Global.Get<LuaTable>("relic");
                    if (proto == null)
                    {
                        proto = LuaManager.Instance.luaEnv.Global.Get<LuaTable>(relicName);
                    }
                }

                if (proto != null)
                {
                    // [경고 검증] 필수 함수 정의 검사 (차단하지 않고 경고만 출력)
                    var onInit = proto.Get<LuaFunction>("OnInit");
                    if (onInit == null)
                    {
                        Debug.LogWarning($"[RelicData: {relicName}] 경고: 필수 함수 'OnInit'이 누락되었습니다. 게임 내에서 정상 작동하지 않을 수 있습니다.");
                    }

                    this.luaPrototype = proto;
                }
                else
                {
                    Debug.LogError($"[RelicData: {relicName}] 루아 파일 로드 실패: 테이블 형식이 아닙니다.");
                    isValid = false;
                }

                // 글로벌 오염 방지를 위해 임시 등록 변수 해제
                LuaManager.Instance.luaEnv.Global.Set<string, object>("relic", null);
                LuaManager.Instance.luaEnv.Global.Set<string, object>(relicName, null);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RelicData: {relicName}] 루아 구문 오류:\n{e.Message}");
                isValid = false;
            }
        }
        else
        {
            Debug.LogError($"[RelicData: {relicName}] 연동할 루아 스크립트 파일을 찾을 수 없습니다: {expectedScriptPath}");
            isValid = false;
        }

        return isValid;
    }
}
