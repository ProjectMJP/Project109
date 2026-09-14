using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BundleLoader : MonoBehaviour
{
    private const string MOD_FOLDER_PATH = "mods";
    private string modFolderPath;

    private void Awake()
    {
        //모드 파일이 저장될 경로 설정
        modFolderPath = Path.Combine(Application.persistentDataPath, MOD_FOLDER_PATH);
    }

    public List<string> GetAllBundleFileFromPath()
    {
        List<string> bundlePaths = new List<string>();

        if (!Directory.Exists(modFolderPath))
        {
            Debug.LogWarning($"모드 폴더가 존재하지 않습니다. {modFolderPath}");
            return bundlePaths;
        }

        string[] files = Directory.GetFiles(modFolderPath, "*.bundle", SearchOption.AllDirectories);

        foreach (string file in files) 
        { 
            bundlePaths.Add(file);
        }

        Debug.Log($"{bundlePaths.Count}개의 번들 파일을 찾았습니다.");
        return bundlePaths;
    }
}
