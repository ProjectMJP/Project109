using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[TestFixture]
public class ObstacleSeeThroughTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    [Test]
    public void FadeableObstacle_TransitionsOpaqueToFadeAndRestoresOriginalMaterial()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        Shader fadeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shader/Map/ObstacleSeeThroughFade.shader");
        Assert.IsNotNull(urpLit, "URP Lit 셰이더를 찾을 수 없습니다.");
        Assert.IsNotNull(fadeShader, "ObstacleSeeThroughFade.shader를 찾을 수 없습니다.");

        Material originalMat = Track(new Material(urpLit));
        originalMat.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.6f, 1f));

        GameObject go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = originalMat;

        FadeableObstacle fadeable = go.AddComponent<FadeableObstacle>();
        SetPrivateField(fadeable, "fadeShader", fadeShader);
        InvokePrivateMethod(fadeable, "Awake");

        Assert.IsFalse(fadeable.IsOccluding);
        Assert.AreEqual(1f, fadeable.CurrentAlpha, 0.001f);
        Assert.IsFalse(fadeable.IsUsingFadeMaterial);
        Assert.AreSame(originalMat, renderer.sharedMaterial);

        // 1. 가림 진입 -> 반투명 셰이더 전환 및 중간 보간 확인
        fadeable.SetOccluding(true);
        bool keepTicking = fadeable.TickFade(0.03f);
        Assert.IsTrue(keepTicking);
        Assert.IsTrue(fadeable.IsOccluding);
        Assert.Less(fadeable.CurrentAlpha, 1f);
        Assert.Greater(fadeable.CurrentAlpha, fadeable.FadedAlpha);
        Assert.IsTrue(fadeable.IsUsingFadeMaterial);
        Assert.IsNotNull(renderer.sharedMaterial);
        Assert.AreEqual("Custom/ObstacleSeeThroughFade", renderer.sharedMaterial.shader.name);

        // 2. 목표 알파(0.35) 수렴 확인
        bool finishedAtTarget = !fadeable.TickFade(1f);
        Assert.IsTrue(finishedAtTarget);
        Assert.AreEqual(fadeable.FadedAlpha, fadeable.CurrentAlpha, 0.001f);
        Assert.IsTrue(fadeable.IsUsingFadeMaterial);

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Assert.AreEqual(fadeable.FadedAlpha, block.GetFloat("_FadeAlpha"), 0.001f);

        // 3. 가림 이탈 -> 불투명(1.0) 및 원본 Opaque 머티리얼 복원 확인
        fadeable.SetOccluding(false);
        bool restoredDone = !fadeable.TickFade(1f);
        Assert.IsTrue(restoredDone);
        Assert.IsFalse(fadeable.IsOccluding);
        Assert.AreEqual(1f, fadeable.CurrentAlpha, 0.001f);
        Assert.IsFalse(fadeable.IsUsingFadeMaterial);
        Assert.AreSame(originalMat, renderer.sharedMaterial);
    }

    [Test]
    public void ObstacleSeeThroughController_OrthoCamera_DetectsFrontObstacle_IgnoresBehindUnit_AndRestoresOnExit()
    {
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        Assert.AreNotEqual(-1, obstacleLayer, "'Obstacle' 레이어가 등록되어 있어야 합니다.");

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        Shader fadeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shader/Map/ObstacleSeeThroughFade.shader");
        Assert.IsNotNull(urpLit);
        Assert.IsNotNull(fadeShader);

        GameObject camGo = Track(new GameObject("TestOrthoCamera"));
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
        cam.transform.position = -cam.transform.forward * 15f;

        ObstacleSeeThroughController controller = camGo.AddComponent<ObstacleSeeThroughController>();
        SetPrivateField(controller, "targetCamera", cam);
        SetPrivateField(controller, "obstacleLayerMask", (LayerMask)(1 << obstacleLayer));
        SetPrivateField(controller, "castRadius", 0.35f);
        SetPrivateField(controller, "unitHeightOffset", 0.9f);
        InvokePrivateMethod(controller, "Awake");

        GameObject unitGo = Track(new GameObject("TestUnit"));
        unitGo.transform.position = Vector3.zero;
        controller.RegisterTarget(unitGo.transform);

        Vector3 unitOrigin = unitGo.transform.position + Vector3.up * 0.9f;
        Vector3 toCameraDir = -cam.transform.forward;

        FadeableObstacle frontPillar = CreateObstaclePillar("FrontPillar", unitOrigin + toCameraDir * 2.0f, obstacleLayer, urpLit, fadeShader);
        FadeableObstacle behindFarPillar = CreateObstaclePillar("BehindFarPillar", unitOrigin - toCameraDir * 2.0f, obstacleLayer, urpLit, fadeShader);
        FadeableObstacle behindOverlapPillar = CreateObstaclePillar("BehindOverlapPillar", unitOrigin - toCameraDir * 0.4f, obstacleLayer, urpLit, fadeShader);

        Physics.SyncTransforms();
        controller.ForceScanNow(fadeDeltaTime: 1f);

        Assert.IsTrue(frontPillar.IsOccluding, "카메라와 유닛 사이의 기둥은 가림 상태로 감지되어야 합니다.");
        Assert.AreEqual(frontPillar.FadedAlpha, frontPillar.CurrentAlpha, 0.001f);
        Assert.IsTrue(frontPillar.IsUsingFadeMaterial);

        Assert.IsFalse(behindFarPillar.IsOccluding, "유닛 뒤쪽에 있는 기둥은 반투명화되면 안 됩니다.");
        Assert.AreEqual(1f, behindFarPillar.CurrentAlpha, 0.001f);
        Assert.IsFalse(behindFarPillar.IsUsingFadeMaterial);

        Assert.IsFalse(behindOverlapPillar.IsOccluding, "초기 구체와 겹치더라도 중심이 유닛 뒤쪽인 기둥은 무시되어야 합니다.");
        Assert.AreEqual(1f, behindOverlapPillar.CurrentAlpha, 0.001f);

        // 유닛이 기둥 가림 영역을 벗어나면 즉시 원복 대상이 되어 불투명으로 복원되어야 함
        unitGo.transform.position = new Vector3(15f, 0f, 15f);
        Physics.SyncTransforms();
        controller.ForceScanNow(fadeDeltaTime: 1f);

        Assert.IsFalse(frontPillar.IsOccluding);
        Assert.AreEqual(1f, frontPillar.CurrentAlpha, 0.001f);
        Assert.IsFalse(frontPillar.IsUsingFadeMaterial);
    }

    [Test]
    public void ObstacleSeeThrough_AssetsSceneSetupAndLegacyCleanup_AreValid()
    {
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        Assert.AreNotEqual(-1, obstacleLayer, "TagManager에 'Obstacle' 레이어가 등록되어 있어야 합니다.");

        // 1. 기존 레거시 Cutoff 스크립트·셰이더·머티리얼 Clean Cut 삭제 검증
        string[] deletedPaths =
        {
            "Assets/Scripts/World/Player/MouseSeeThroughController.cs",
            "Assets/Shader/EffectArea/SeeThroughObstacleShader.shader",
            "Assets/Shader/EffectArea/SeeThroughSilhouetteShader.shader",
            "Assets/Shader/EffectArea/SeeThroughSilhouette_Mat.mat"
        };
        for (int i = 0; i < deletedPaths.Length; i++)
        {
            Assert.IsFalse(File.Exists(deletedPaths[i]), $"레거시 파일이 삭제되어야 합니다: {deletedPaths[i]}");
        }

        // 2. Obstacle_Test.mat가 URP Lit Opaque로 교체되었는지 확인
        Material obstacleMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Map/Obstacle_Test.mat");
        Assert.IsNotNull(obstacleMat);
        Assert.AreEqual("Universal Render Pipeline/Lit", obstacleMat.shader.name);

        // 3. 장애물 프리팹 2종 레이어, FadeableObstacle, 렌더러/셰이더 연결 검증
        string[] prefabPaths =
        {
            "Assets/Prefab/Map/Object/Temple/Obstacle_Temple_Pillar.prefab",
            "Assets/Prefab/Map/Object/Forest/Obstacle_Forest_Pillar 1.prefab"
        };
        for (int i = 0; i < prefabPaths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
            Assert.IsNotNull(prefab, $"프리팹을 찾을 수 없습니다: {prefabPaths[i]}");
            Assert.AreEqual(obstacleLayer, prefab.layer, $"{prefabPaths[i]} 루트 레이어가 Obstacle이어야 합니다.");

            FadeableObstacle fadeable = prefab.GetComponent<FadeableObstacle>();
            Assert.IsNotNull(fadeable, $"{prefabPaths[i]}에 FadeableObstacle이 부착되어야 합니다.");

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0);
            for (int r = 0; r < renderers.Length; r++)
            {
                Assert.AreEqual(obstacleLayer, renderers[r].gameObject.layer);
                Assert.IsNotNull(renderers[r].sharedMaterial);
                Assert.AreEqual("Universal Render Pipeline/Lit", renderers[r].sharedMaterial.shader.name);
            }
        }

        // 4. GameScene.unity Main Camera에 ObstacleSeeThroughController 정적 배치 검증
        Scene additiveGameScene = EditorSceneManager.OpenScene("Assets/Scenes/Release/GameScene.unity", OpenSceneMode.Additive);
        try
        {
            ObstacleSeeThroughController foundController = null;
            GameObject[] rootObjs = additiveGameScene.GetRootGameObjects();
            for (int i = 0; i < rootObjs.Length; i++)
            {
                foundController = rootObjs[i].GetComponentInChildren<ObstacleSeeThroughController>(true);
                if (foundController != null)
                {
                    break;
                }
            }

            Assert.IsNotNull(foundController, "GameScene.unity에 ObstacleSeeThroughController가 배치되어야 합니다.");
            Assert.AreEqual("Main Camera", foundController.gameObject.name);
        }
        finally
        {
            EditorSceneManager.CloseScene(additiveGameScene, true);
        }
    }

    private FadeableObstacle CreateObstaclePillar(string name, Vector3 position, int layer, Shader urpLit, Shader fadeShader)
    {
        GameObject pillar = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        pillar.name = name;
        pillar.layer = layer;
        pillar.transform.position = position;
        pillar.transform.localScale = new Vector3(0.7f, 1.5f, 0.7f);

        Material mat = Track(new Material(urpLit));
        pillar.GetComponent<MeshRenderer>().sharedMaterial = mat;

        FadeableObstacle fadeable = pillar.AddComponent<FadeableObstacle>();
        SetPrivateField(fadeable, "fadeShader", fadeShader);
        InvokePrivateMethod(fadeable, "Awake");
        return fadeable;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}");
        return method.Invoke(target, args);
    }
}
