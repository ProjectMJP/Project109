using System.IO;
using UnityEngine;
using XLua;
using YamlDotNet.Serialization;

public class EffectData : IModAssetResolver
{
    // effectName이 고유 식별자(ID) 역할을 겸합니다.
    public string effectName { get; set; }

    public string displayName { get; set; }
    // 효과에 대한 기계적 설명 (예: "적에게 {stacks}의 피해를 입힙니다.")
    // {stacks} 플레이스홀더로 현재 스택 수치를 동적으로 표현할 수 있습니다.
    public string description { get; set; }
    public EffectType effectType { get; set; }
    public bool isPermanent { get; set; }
    public bool isIndependent { get; set; } // true일 경우, 중첩(Stack)되지 않고 새로운 인스턴스로 각각 존재합니다.
    public int maxStack { get; set; } = int.MaxValue;

    // 런타임에 imagePath를 통해 디스크에서 직접 읽어온 스프라이트 
    // (YAML 파싱 대상에서 제외하기 위해 [YamlIgnore] 사용)
    [YamlIgnore]
    public Sprite iconSprite { get; set; }

    [YamlIgnore]
    public LuaTable luaPrototype { get; set; }

    public bool ResolveAndValidate(string modDirectory)
    {
        bool isValid = true;

        // 1. 아이콘 스프라이트 링크 (effectName으로 경로 자동 추론)
        string expectedImagePath = Path.Combine(modDirectory, "Icons", "Effects", effectName + ".png");

        if (File.Exists(expectedImagePath))
        {
            // 최대 128x128 픽셀로 제한하여 로드
            this.iconSprite = ImageLoader.LoadCustomSprite(expectedImagePath, 128);
        }
        else
        {
            // 전용 아이콘이 없을 경우 기본 아이콘(Default.png)으로 대체
            string defaultImagePath = Path.Combine(modDirectory, "Icons", "Effects", "Default.png");
            if (File.Exists(defaultImagePath))
            {
                this.iconSprite = ImageLoader.LoadCustomSprite(defaultImagePath, 128);
            }
            else
            {
                Debug.LogWarning($"[EffectData: {effectName}] 전용 아이콘 및 기본 아이콘(Default.png)을 찾을 수 없지만 실행을 계속합니다.");
                // isValid를 false로 만들지 않음으로써 이미지가 없어도 정상적으로 로드되도록 허용
            }
        }

        // 2. 루아 스크립트 검증 및 사전 로딩 (effectName으로 경로 자동 추론)
        string expectedScriptPath = Path.Combine(modDirectory, "Scripts", "Effects", effectName + ".lua");
        if (File.Exists(expectedScriptPath))
        {
            try
            {
                byte[] scriptBytes = File.ReadAllBytes(expectedScriptPath);

                // 루아 스크립트 실행 (컴파일 및 프로토타입 획득)
                object[] results = LuaManager.Instance.luaEnv.DoString(scriptBytes, effectName);

                LuaTable proto = null;
                if (results != null && results.Length > 0)
                {
                    proto = results[0] as LuaTable;
                }

                // [폴백] return이 누락되었을 경우를 대비하여 전역 환경에서 변수 검색
                if (proto == null)
                {
                    proto = LuaManager.Instance.luaEnv.Global.Get<LuaTable>("effect");
                    if (proto == null)
                    {
                        proto = LuaManager.Instance.luaEnv.Global.Get<LuaTable>(effectName);
                    }
                }

                if (proto != null)
                {
                    // [경고 검증] 필수 함수 정의 검사 (차단하지 않고 경고만 출력)
                    var onInit = proto.Get<LuaFunction>("OnInit");
                    if (onInit == null)
                    {
                        Debug.LogWarning($"[EffectData: {effectName}] 경고: 필수 함수 'OnInit'이 누락되었습니다. 게임 내에서 정상 작동하지 않을 수 있습니다.");
                    }

                    this.luaPrototype = proto;
                }
                else
                {
                    Debug.LogError($"[EffectData: {effectName}] 루아 파일 로드 실패: 테이블 형식이 아닙니다.");
                    isValid = false;
                }

                // 글로벌 오염 방지를 위해 임시 등록 변수 해제
                LuaManager.Instance.luaEnv.Global.Set<string, object>("effect", null);
                LuaManager.Instance.luaEnv.Global.Set<string, object>(effectName, null);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EffectData: {effectName}] 루아 구문 오류:\n{e.Message}");
                isValid = false;
            }
        }
        else
        {
            Debug.LogError($"[EffectData: {effectName}] 연동할 루아 스크립트 파일을 찾을 수 없습니다: {expectedScriptPath}");
            isValid = false;
        }

        return isValid;
    }
}
