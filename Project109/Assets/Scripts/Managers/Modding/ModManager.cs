using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class ModManager : MonoBehaviour
{
    public BundleLoader bundleFinder;

    private const string MOD_FOLDER_PATH = "mods";
    private string modFolderPath;
    private IDeserializer _catalogDeserializer;

    private void Awake()
    {
        bundleFinder = GetComponent<BundleLoader>();
        //모드 파일이 저장될 경로 설정
        modFolderPath = Path.Combine(Application.persistentDataPath, MOD_FOLDER_PATH);
        _catalogDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public IEnumerator StartModLoading()
    {
        if (AssetCacheManager.instance == null)
        {
            yield break;
        }

        if (_catalogDeserializer == null)
        {
            _catalogDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        //List<string> bundlePaths = bundleFinder.GetAllBundleFileFromPath();
        //파일 내의 번들 카탈로그 yaml파일 탐색 (ModLoader용 YAML 스키마 폴더 제외)
        List<string> yamlPaths = GetAllYamlFileFromPath();

        foreach (string path in yamlPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            ModData catalog = null;
            try
            {
                //문자열 변환 및 역직렬화 진행
                string yamlContent = File.ReadAllText(path);
                catalog = _catalogDeserializer.Deserialize<ModData>(yamlContent);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ModManager] 번들 카탈로그 YAML 파싱 건너뜀 ({path}): {ex.Message}");
                continue;
            }

            if (catalog == null || catalog.modCatalog == null || catalog.modCatalog.Count == 0)
            {
                continue;
            }

            Debug.Log($"Load {path}");
            foreach (AssetData data in catalog.modCatalog)
            {
                if (data == null || string.IsNullOrEmpty(data.bundlePath))
                {
                    continue;
                }

                string currentBundlePath = Path.Combine(modFolderPath, data.bundlePath);
                if (!File.Exists(currentBundlePath))
                {
                    Debug.LogWarning($"[ModManager] 번들 파일을 찾을 수 없습니다: {currentBundlePath}");
                    continue;
                }

                Debug.Log($"Load {data.bundlePath}");
                yield return AssetCacheManager.instance.LoadAllAssetsFromBundle(data.key, currentBundlePath);
            }

            Debug.Log($"Load complete {path}");
        }

        Debug.Log("모드 번들 로딩 완료");
    }

    private List<string> GetAllYamlFileFromPath()
    {
        List<string> yamlPaths = new List<string>();

        if (string.IsNullOrEmpty(modFolderPath))
        {
            modFolderPath = Path.Combine(Application.persistentDataPath, MOD_FOLDER_PATH);
        }

        if (!Directory.Exists(modFolderPath))
        {
            Debug.LogWarning($"모드 폴더가 존재하지 않습니다. {modFolderPath}");
            return yamlPaths;
        }

        string[] files = Directory.GetFiles(modFolderPath, "*.yaml", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            // ModLoader가 관리하는 데이터 스키마 서브디렉터리(YAML/*)는 번들 카탈로그 스캔에서 제외
            string relative = Path.GetRelativePath(modFolderPath, file);
            if (relative.IndexOf($"{Path.DirectorySeparatorChar}YAML{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                relative.StartsWith($"YAML{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yamlPaths.Add(file);
        }

        Debug.Log($"{yamlPaths.Count}개의 yaml  파일을 찾았습니다.");
        return yamlPaths;
    }
}
