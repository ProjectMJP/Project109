using System;
using System.IO;
using UnityEngine;
using XLua;

/// <summary>
/// 모딩 데이터 스키마(CardData, EffectData, RelicData, NPCUnitData 등)에서 공통으로 사용하는
/// 외부 아이콘 스프라이트 및 XLua 프로토타입 테이블 로딩/검증 유틸리티입니다.
/// </summary>
public static class ModAssetLoader
{
    /// <summary>
    /// 지정된 모드 폴더 하위에서 아이템 ID와 일치하는 아이콘 이미지를 로드하고,
    /// 없을 경우 Default.png 폴백 로드를 시도합니다.
    /// </summary>
    public static bool TryLoadIconWithFallback(
        string modDirectory,
        string categoryFolder,
        string itemId,
        int maxPixelSize,
        out Sprite loadedSprite)
    {
        loadedSprite = null;
        if (string.IsNullOrEmpty(modDirectory) || string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        string expectedImagePath = Path.Combine(modDirectory, "Icons", categoryFolder, itemId + ".png");
        if (File.Exists(expectedImagePath))
        {
            loadedSprite = ImageLoader.LoadCustomSprite(expectedImagePath, maxPixelSize);
            return loadedSprite != null;
        }

        string defaultImagePath = Path.Combine(modDirectory, "Icons", categoryFolder, "Default.png");
        if (File.Exists(defaultImagePath))
        {
            loadedSprite = ImageLoader.LoadCustomSprite(defaultImagePath, maxPixelSize);
            return loadedSprite != null;
        }

        Debug.LogWarning($"[{categoryFolder}: {itemId}] 전용 아이콘 및 기본 아이콘(Default.png)을 찾을 수 없지만 실행을 계속합니다.");
        return false;
    }

    /// <summary>
    /// 지정된 경로의 Lua 스크립트를 실행하여 프로토타입 LuaTable을 획득하고,
    /// 전역 네임스페이스 오염을 방지하기 위해 임시 전역 변수를 정리합니다.
    /// </summary>
    public static bool TryLoadLuaPrototype(
        string scriptPath,
        string itemId,
        string logContext,
        string[] fallbackGlobalKeys,
        string requiredFunctionWarningName,
        out LuaTable prototype)
    {
        prototype = null;

        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
        {
            Debug.LogError($"[{logContext}: {itemId}] 연동할 루아 스크립트 파일을 찾을 수 없습니다: {scriptPath}");
            return false;
        }

        if (LuaManager.Instance == null || LuaManager.Instance.luaEnv == null)
        {
            Debug.LogError($"[{logContext}: {itemId}] LuaManager.Instance 또는 luaEnv가 초기화되지 않았습니다.");
            return false;
        }

        LuaEnv env = LuaManager.Instance.luaEnv;

        try
        {
            byte[] scriptBytes = File.ReadAllBytes(scriptPath);
            object[] results = env.DoString(scriptBytes, itemId);

            if (results != null && results.Length > 0)
            {
                prototype = results[0] as LuaTable;
            }

            // return이 누락되었을 경우를 대비하여 전역 환경에서 폴백 변수 검색
            if (prototype == null && fallbackGlobalKeys != null)
            {
                for (int i = 0; i < fallbackGlobalKeys.Length; i++)
                {
                    string key = fallbackGlobalKeys[i];
                    if (!string.IsNullOrEmpty(key))
                    {
                        prototype = env.Global.Get<LuaTable>(key);
                        if (prototype != null) break;
                    }
                }
            }

            if (prototype == null)
            {
                prototype = env.Global.Get<LuaTable>(itemId);
            }

            // 글로벌 오염 방지를 위해 임시 등록 변수 해제
            if (fallbackGlobalKeys != null)
            {
                for (int i = 0; i < fallbackGlobalKeys.Length; i++)
                {
                    string key = fallbackGlobalKeys[i];
                    if (!string.IsNullOrEmpty(key))
                    {
                        env.Global.Set<string, object>(key, null);
                    }
                }
            }
            env.Global.Set<string, object>(itemId, null);

            if (prototype == null)
            {
                Debug.LogError($"[{logContext}: {itemId}] 루아 파일 로드 실패: 테이블 형식이 아닙니다.");
                return false;
            }

            if (!string.IsNullOrEmpty(requiredFunctionWarningName))
            {
                var reqFunc = prototype.Get<LuaFunction>(requiredFunctionWarningName);
                if (reqFunc == null)
                {
                    Debug.LogWarning($"[{logContext}: {itemId}] 경고: 필수 함수 '{requiredFunctionWarningName}'이 누락되었습니다. 게임 내에서 정상 작동하지 않을 수 있습니다.");
                }
                else
                {
                    reqFunc.Dispose();
                }
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[{logContext}: {itemId}] 루아 구문 오류:\n{e.Message}");
            return false;
        }
    }
}

