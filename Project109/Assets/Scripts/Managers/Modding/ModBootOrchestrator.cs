using System.Collections;
using UnityEngine;

/// <summary>
/// L3 Managers 계층의 모드 부팅 오케스트레이터입니다.
/// L1 Data(AssetCacheManager)의 Addressable 캐시 완료 훅에 연결되어 ModManager·ModLoader 초기화를 순차 실행합니다.
/// </summary>
public static class ModBootOrchestrator
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void RegisterBootHook()
    {
        AssetCacheManager.RegisterPostLoadStep(ExecuteModBootSequence);
    }

    /// <summary>
    /// 에셋 캐시 완료 이후 모드 번들(ModManager)과 YAML/Lua 모드 데이터베이스(ModLoader)를 순서대로 로드합니다.
    /// </summary>
    public static IEnumerator ExecuteModBootSequence(AssetCacheManager cacheManager)
    {
        if (cacheManager != null)
        {
            ModManager modManager = cacheManager.GetComponent<ModManager>();
            if (modManager != null)
            {
                if (Application.isPlaying)
                {
                    yield return cacheManager.StartCoroutine(modManager.StartModLoading());
                }
                else
                {
                    IEnumerator modRoutine = modManager.StartModLoading();
                    while (modRoutine.MoveNext())
                    {
                        yield return modRoutine.Current;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[ModBootOrchestrator] Cannot find ModManager.");
            }
        }

        // YAML 기반 모드 로드 (이펙트, 유물, 카드 데이터 데이터베이스 구축)
        ModLoader.Instance.LoadAllMods();
    }
}
