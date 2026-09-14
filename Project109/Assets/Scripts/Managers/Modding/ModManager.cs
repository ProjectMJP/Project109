using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using NUnit.Framework;
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

    private void Awake()
    {
        bundleFinder = GetComponent<BundleLoader>();
        //모드 파일이 저장될 경로 설정
        modFolderPath = Path.Combine(Application.persistentDataPath, MOD_FOLDER_PATH);
    }

    public IEnumerator StartModLoading()
    {
        //List<string> bundlePaths = bundleFinder.GetAllBundleFileFromPath();
        //파일 내의 yaml파일 전부 탐색
        List<string> yamlPaths = GetAllYamlFileFromPath();

        foreach (string path in yamlPaths)
        {
            Debug.Log($"Load {path}");

            if(File.Exists(path))
            {
                //문자열 변환
                string yamlContent = File.ReadAllText(path);

                //Deserializer 생성
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                
                //역직렬화 진행
                ModData catalog = deserializer.Deserialize<ModData>(yamlContent);

                foreach (AssetData data in catalog.modCatalog)
                {
                    Debug.Log($"Load {data.bundlePath}");
                    string currentBundlePath = Path.Combine(modFolderPath, data.bundlePath);
                    yield return AssetCacheManager.instance.LoadAllAssetsFromBundle(data.key, currentBundlePath);
                }
            }
            
            Debug.Log($"Load complete {path}");
        }

        Debug.Log("모드 번들 로딩 완료");
    }

    private List<string> GetAllYamlFileFromPath()
    {
        List<string> yamlPaths = new List<string>();

        if (!Directory.Exists(modFolderPath))
        {
            Debug.LogWarning($"모드 폴더가 존재하지 않습니다. {modFolderPath}");
            return yamlPaths;
        }

        string[] files = Directory.GetFiles(modFolderPath, "*.yaml", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            yamlPaths.Add(file);
        }

        Debug.Log($"{yamlPaths.Count}개의 yaml  파일을 찾았습니다.");
        return yamlPaths;
    }
}
