#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class EditorStartInit : Editor
{
    static EditorStartInit()
    {
        // 테스트를 위해 씬 강제 이동 지정을 해제합니다. (null 설정)
        EditorSceneManager.playModeStartScene = null;
        Debug.Log("Clear play mode start scene (playModeStartScene = null)");

        AddressableAutoRegister.RegisterAll();
        SetupBattleUIPanelPrefab.SetupReferences();
    }
}
#endif