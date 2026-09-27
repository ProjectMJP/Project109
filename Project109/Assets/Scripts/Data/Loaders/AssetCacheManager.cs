using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using YamlDotNet.Serialization;

public struct StageMapDataBundle : IIdentifiable
{
    public string stageName;
    public List<MapDataSO> battleMapDataList;
    public List<MapDataSO> eliteMapDataList;
    public List<MapDataSO> bossMapDataList;
    public List<MapDataSO> secretMapDataList;
    public List<MapDataSO> storeMapDataList;
    public List<MapDataSO> restoreMapDataList;


    public string ID => stageName;
}

public class AssetCacheManager : MonoBehaviour
{
    public static AssetCacheManager instance { get; private set; }
    public bool isLoadComplete { get; private set; } = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;

            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public string cardKey = "Card";
    public string eventKey = "Event";
    public string battleKey = "Battle";
    public string mapKey = "Map";
    public string modelKey = "Model";
    public string textureKey = "Texture";
    public string uiKey = "UI";
    public string interactableKey = "Interactable";

    public IList<BattleData> battleList;
    private Dictionary<string, BattleData> battleDict = new Dictionary<string, BattleData>();

    public IList<MapDataSO> mapList;
    private Dictionary<string, MapDataSO> mapDict = new Dictionary<string, MapDataSO>();
    private Dictionary<string, StageMapDataBundle> stageMapDataDict = new Dictionary<string, StageMapDataBundle>();

    public IList<GameObject> modelList;
    private Dictionary<string, GameObject> modelDict = new Dictionary<string, GameObject>();

    public IList<Sprite> textureList;
    private Dictionary<string, Sprite> textureDict = new Dictionary<string, Sprite>();

    public IList<GameObject> uiList;
    private Dictionary<string, GameObject> uiDict = new Dictionary<string, GameObject>();

    private IEnumerator Start()
    {
        bool isBattleLoaded = false;
        bool isMapLoaded = false;
        bool isModelLoaded = false;
        bool isTextureLoaded = false;
        bool isUiLoaded = false;

        // 3. 전투 데이터 할당 시작
        StartCoroutine(LoadAndCacheFromAddressableData<BattleData>(battleKey, (list, dict) =>
        {
            battleList = list;
            battleDict = dict;
            isBattleLoaded = true;
        }));

        // 6. 맵 데이터 할당 시작
        StartCoroutine(LoadAndCacheFromAddressableData<MapDataSO>(mapKey, (list, dict) =>
        {
            mapList = list;
            mapDict = dict;

            //맵 데이터를 스테이지별로 분류하여 저장
            foreach (var mapData in list)
            {
                Debug.Log($"mapData : {mapData.stageName}");
                if (!stageMapDataDict.ContainsKey(mapData.locationType.ToString()))
                {
                    Debug.Log($"stageMapData 추가 : {mapData.locationType.ToString()}");
                    stageMapDataDict[mapData.locationType.ToString()] = new StageMapDataBundle
                    {
                        stageName = mapData.stageName,
                        battleMapDataList = new List<MapDataSO>(),
                        eliteMapDataList = new List<MapDataSO>(),
                        bossMapDataList = new List<MapDataSO>(),
                        secretMapDataList = new List<MapDataSO>(),
                        storeMapDataList = new List<MapDataSO>(),
                        restoreMapDataList = new List<MapDataSO>()
                    };
                }
                switch (mapData.EncountType)
                {
                    case EncountType.Battle:
                        stageMapDataDict[mapData.locationType.ToString()].battleMapDataList.Add(mapData);
                        break;
                    case EncountType.Elite:
                        stageMapDataDict[mapData.locationType.ToString()].eliteMapDataList.Add(mapData);
                        break;
                    case EncountType.Boss:
                        stageMapDataDict[mapData.locationType.ToString()].bossMapDataList.Add(mapData);
                        break;
                    case EncountType.Secret:
                        stageMapDataDict[mapData.locationType.ToString()].secretMapDataList.Add(mapData);
                        break;
                    case EncountType.Store:
                        stageMapDataDict[mapData.locationType.ToString()].storeMapDataList.Add(mapData);
                        break;
                    case EncountType.Restore:
                        stageMapDataDict[mapData.locationType.ToString()].restoreMapDataList.Add(mapData);
                        break;
                }
            }
            isMapLoaded = true;
        }));

        // 8. 모델링 및 오브젝트 데이터 할당 시작
        StartCoroutine(LoadAndCacheGameObjectFromAddressableData(modelKey, (list, dict) =>
        {
            modelList = list;
            modelDict = dict;
            isModelLoaded = true;
        }));

        // 9. 텍스처 데이터 할당 시작
        StartCoroutine(LoadAndCacheTextureFromAddressableData(textureKey, (list, dict) =>
        {
            textureList = list;
            textureDict = dict;
            isTextureLoaded = true;
        }));

        // 12. UI 데이터 할당 시작
        StartCoroutine(LoadAndCacheGameObjectFromAddressableData(uiKey, (list, dict) =>
        {
            uiList = list;
            uiDict = dict;
            isUiLoaded = true;
        }));

        // 모든 비동기 작업이 병렬 완료될 때까지 대기
        float waitTimer = 0f;
        while (!(isBattleLoaded &&
                 isMapLoaded &&
                 isModelLoaded &&
                 isTextureLoaded &&
                 isUiLoaded))
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= 2f)
            {
                Debug.Log($"[AssetCacheManager] Waiting for flags: " +
                    $"Battle={isBattleLoaded}, " +
                    $"Map={isMapLoaded}, " +
                    $"Model={isModelLoaded}, " +
                    $"Texture={isTextureLoaded}, " +
                    $"UI={isUiLoaded}");
                waitTimer = 0f;
            }
            yield return null;
        }

        ModManager modManager = GetComponent<ModManager>();
        if (modManager != null)
        {
            yield return StartCoroutine(modManager.StartModLoading());
        }
        else
        {
            Debug.LogWarning("Cannot find ModManager.");
        }

        // YAML 기반 모드 로드 (이펙트, 유물, 카드 데이터 데이터베이스 구축)
        ModLoader.Instance.LoadAllMods();

        // 데이터 로드가 정상 완료되었음을 상태 변수로 보존합니다.
        Debug.Log("All Data Load is Complete.");
        isLoadComplete = true;
    }

    public IEnumerator LoadAllAssetsFromBundle(string key, string bundlePath)
    {
        if (!File.Exists(bundlePath))
        {
            Debug.Log($"번들 파일을 찾을 수 없습니다. {bundlePath}");
            yield break;
        }

        //에셋 번들 로드
        AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(bundlePath);
        yield return request;

        AssetBundle loadedAssetBundle = request.assetBundle;
        if (loadedAssetBundle == null)
        {
            Debug.LogError("에셋 번들 로드에 실패하였습니다.");
            yield break;
        }

        //번들 내에 저장된 모든 에셋 이름 가져오기
        string[] allAssetNames = loadedAssetBundle.GetAllAssetNames();

        Debug.Log($"Find {allAssetNames.Length} Assets");

        //각 에셋을 반복문으로 로드
        foreach (string assetName in allAssetNames)
        {
            Debug.Log($"Load: {assetName}");

            GameObject asset = loadedAssetBundle.LoadAsset<GameObject>(assetName);
            if (asset != null)
            {
                //새로운 오브젝트를 캐시에 등록
                SetNewAssetInCache(key, asset);
            }
            else
            {
                Debug.Log($"Load falied: {assetName}");
            }
        }

        loadedAssetBundle.Unload(false);
    }

    private void SetNewAssetInCache(string key, GameObject newObject)
    {
        switch (key)
        {
            case "Model":
                Debug.Log($"Cache new Data {modelDict[newObject.name].name} -> {newObject.name}");
                modelDict[newObject.name] = newObject;
                break;
        }
    }

    /// <summary>
    /// Addressable 키를 통해 데이터를 비동기로 불러오고 후처리 실행
    /// </summary>
    IEnumerator LoadAndCacheFromAddressableData<T>(string key, Action<IList<T>, Dictionary<string, T>> onLoaded) where T : ScriptableObject, IIdentifiable
    {
        AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(key, null);

        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            //불러온 데이터 저장
            var list = handle.Result;
            var dict = new Dictionary<string, T>();

            //Dictionary에 데이터 저장
            foreach (var item in list)
            {
                if (!dict.ContainsKey(item.ID))
                {
                    dict[item.ID] = item;
                    //Debug.Log($"{item.ID} Load");
                }
            }

            Debug.Log($"{key}, {dict.Count} item Load Complete");
            //저장된 데이터 반환
            onLoaded?.Invoke(list, dict);
        }
        else
        {
            Debug.LogError($"{typeof(T).Name} Addressables 로드 실패");
            onLoaded?.Invoke(null, null);
        }
    }

    /// <summary>
    /// Addressable 키를 통해 오브젝트를 비동기로 불러오고 후처리 실행
    /// </summary>
    IEnumerator LoadAndCacheGameObjectFromAddressableData(string key, Action<IList<GameObject>, Dictionary<string, GameObject>> onLoaded)
    {
        AsyncOperationHandle<IList<GameObject>> handle = Addressables.LoadAssetsAsync<GameObject>(key, null);

        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            //불러온 데이터 저장
            var list = handle.Result;
            var dict = new Dictionary<string, GameObject>();

            //Dictionary에 데이터 저장
            foreach (var item in list)
            {
                if (!dict.ContainsKey(item.name))
                {
                    dict[item.name] = item;
                    Debug.Log($"{item.name} Load");
                }
            }

            Debug.Log($"{key}, {dict.Count} item Load Complete");
            //저장된 데이터 반환
            onLoaded?.Invoke(list, dict);
        }
        else
        {
            Debug.LogError($"{key} Addressables 로드 실패");
            onLoaded?.Invoke(null, null);
        }
    }

    /// <summary>
    /// Addressable 키를 통해 텍스처를 비동기로 불러오고 후처리 실행
    /// </summary>
    IEnumerator LoadAndCacheTextureFromAddressableData(string key, Action<IList<Sprite>, Dictionary<string, Sprite>> onLoaded)
    {
        AsyncOperationHandle<IList<Texture2D>> handle = Addressables.LoadAssetsAsync<Texture2D>(key, null);

        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            var list = new List<Sprite>();
            var dict = new Dictionary<string, Sprite>();

            foreach (var texture in handle.Result)
            {
                if (texture != null)
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    sprite.name = texture.name;
                    list.Add(sprite);
                    dict[texture.name] = sprite;
                }
            }

            Debug.Log($"{key}, {dict.Count} item Load Complete");
            //저장된 데이터 반환
            onLoaded?.Invoke(list, dict);
        }
        else
        {
            Debug.LogError($"{key} Addressables 로드 실패");
            onLoaded?.Invoke(null, null);
        }
    }


    public bool TryGetBattle(string name, out BattleData battle) => battleDict.TryGetValue(name, out battle);
    public bool TryGetMap(string name, out MapDataSO mapData) => mapDict.TryGetValue(name, out mapData);
    public bool TryGetStageMapData(string name, out StageMapDataBundle stageMapData) => stageMapDataDict.TryGetValue(name, out stageMapData);
    public bool TryGetModel(string name, out GameObject model) => modelDict.TryGetValue(name, out model);
    public bool TryGetTexture(string name, out Sprite texture) => textureDict.TryGetValue(name, out texture);
    public bool TryGetUI(string name, out GameObject uiPrefab) => uiDict.TryGetValue(name, out uiPrefab);

    public System.Collections.IEnumerator GetUIAsyncCoroutine(string addressableKey, Action<GameObject> onLoaded)
    {
        if (uiDict.TryGetValue(addressableKey, out GameObject cachedPrefab))
        {
            onLoaded?.Invoke(cachedPrefab);
            yield break;
        }

        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(addressableKey);
        yield return handle;
        
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            uiDict[addressableKey] = handle.Result;
            onLoaded?.Invoke(handle.Result);
        }
        else
        {
            Debug.LogError($"[AssetCacheManager] Failed to load UI async: {addressableKey}");
            onLoaded?.Invoke(null);
        }
    }
}
