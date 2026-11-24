using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 월드 UI의 회전 Y-Order 정렬, 직교 시선 깊이 오프셋, 바닥 그리드 렌더 큐 및 장애물 깊이 초기화 패스를 검증하는 EditMode 테스트입니다.
/// </summary>
public class WorldUIDepthAndRotatedYOrderTests
{
    private readonly List<Object> _createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();
    }

    private T Track<T>(T obj) where T : Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    [Test]
    public void ComputeDepthSafeWorldPosition_PullsAlongCameraBackwardWithoutShiftingOrthoProjection()
    {
        GameObject camGo = Track(new GameObject("TestOrthoCamera", typeof(Camera)));
        camGo.transform.position = new Vector3(-10f, 15f, -10f);
        camGo.transform.rotation = Quaternion.Euler(30f, 30f, 0f);

        Camera cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 10f;

        Vector3 targetPos = new Vector3(3f, 0f, 4f);
        Vector3 offset = UIConstants.DEFAULT_STATUS_BAR_OFFSET;

        Vector3 rawPos = targetPos + offset;
        Vector3 safePos = CharacterStatusBarUI.ComputeDepthSafeWorldPosition(targetPos, offset, cam);

        // 1. 바닥 타일(y=0)보다 최소 0.16 이상 위에 위치해야 함
        Assert.GreaterOrEqual(safePos.y, targetPos.y + 0.159f);

        // 2. 카메라 쪽으로 최소 0.45 이상 당겨져 자기 발보다 앞에 위치해야 함
        float pullDistance = Vector3.Dot(rawPos - safePos, cam.transform.forward);
        Assert.GreaterOrEqual(pullDistance, 0.449f);

        // 3. 직교 투영 화면상 위치(Screen/Viewport X, Y)는 변하지 않아야 함
        Vector3 rawViewport = cam.WorldToViewportPoint(rawPos);
        Vector3 safeViewport = cam.WorldToViewportPoint(safePos);
        Assert.AreEqual(rawViewport.x, safeViewport.x, 0.001f);
        Assert.AreEqual(rawViewport.y, safeViewport.y, 0.001f);
    }

    [Test]
    public void SortWorldUILayersByRotatedDepth_SortsFarToNearAndKeepsHighlightedOnTop()
    {
        GameObject camGo = Track(new GameObject("TestOrthoCamera", typeof(Camera)));
        camGo.transform.rotation = Quaternion.Euler(30f, 30f, 0f);
        Camera cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;

        GameObject uiManagerGo = Track(new GameObject("TestUIManager"));
        UIManager uiManager = uiManagerGo.AddComponent<UIManager>();

        GameObject worldLayerGo = Track(new GameObject("WorldUILayer", typeof(RectTransform), typeof(Canvas)));
        worldLayerGo.transform.SetParent(uiManagerGo.transform, false);
        Canvas worldCanvas = worldLayerGo.GetComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        uiManager.worldUILayer = worldCanvas;

        // 3개의 타깃 (카메라 회전 yaw=30° 기준: +X, +Z 방향이 멀리/화면 위쪽)
        Transform farTarget = Track(new GameObject("FarTarget")).transform;
        farTarget.position = new Vector3(5f, 0f, 5f);

        Transform midTarget = Track(new GameObject("MidTarget")).transform;
        midTarget.position = new Vector3(2f, 0f, 2f);

        Transform nearTarget = Track(new GameObject("NearTarget")).transform;
        nearTarget.position = new Vector3(0f, 0f, 0f);

        // 의도적으로 역순(near -> mid -> far)으로 자식 추가
        CharacterStatusBarUI nearStatus = CreateStubStatusBar(worldLayerGo.transform, "NearStatus", nearTarget);
        CharacterStatusBarUI midStatus = CreateStubStatusBar(worldLayerGo.transform, "MidStatus", midTarget);
        CharacterStatusBarUI farStatus = CreateStubStatusBar(worldLayerGo.transform, "FarStatus", farTarget);
        CharacterEffectListUI midEffects = CreateStubEffectList(worldLayerGo.transform, "MidEffects", midTarget);

        RegisterMeta(uiManager, nearStatus, 1);
        RegisterMeta(uiManager, midStatus, 1);
        RegisterMeta(uiManager, farStatus, 1);
        RegisterMeta(uiManager, midEffects, 0);

        uiManager.SortWorldUILayersByRotatedDepth(cam);

        // 먼 유닛(far) -> 중간 유닛(midEffects -> midStatus) -> 가까운 유닛(near) 순으로 SiblingIndex 배치
        Assert.Less(farStatus.transform.GetSiblingIndex(), midEffects.transform.GetSiblingIndex());
        Assert.Less(midEffects.transform.GetSiblingIndex(), midStatus.transform.GetSiblingIndex());
        Assert.Less(midStatus.transform.GetSiblingIndex(), nearStatus.transform.GetSiblingIndex());

        // 먼 유닛을 강조하면 최상단(가장 큰 SiblingIndex)으로 승격되어야 함
        uiManager.SetWorldUIHighlighted(farStatus, true);
        uiManager.SortWorldUILayersByRotatedDepth(cam);

        Assert.AreEqual(worldLayerGo.transform.childCount - 1, farStatus.transform.GetSiblingIndex());
    }

    [Test]
    public void MapMoveRangeMaterial_RendersBeforeWorldDepthUI()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shader/EffectArea/MapMoveRange.mat");
        Assert.IsNotNull(mat, "MapMoveRange.mat을 찾을 수 없습니다.");
        Assert.Less(mat.renderQueue, (int)UnityEngine.Rendering.RenderQueue.Transparent,
            "바닥 그리드 머티리얼은 월드 UI(3000)보다 먼저 렌더링되어야 합니다.");
        Assert.AreEqual(2490, mat.renderQueue);
    }

    [Test]
    public void ObstacleDepthClearAssets_ExistAndBindValidShader()
    {
        Material mat = Resources.Load<Material>(ObstacleDepthClearRenderPass.ResourceMaterialPath);
        Assert.IsNotNull(mat, "Resources/Rendering/ObstacleDepthClear.mat을 찾을 수 없습니다.");
        Assert.IsNotNull(mat.shader, "ObstacleDepthClear.mat의 셰이더가 누락되었습니다.");
        Assert.AreEqual(ObstacleDepthClearRenderPass.ShaderName, mat.shader.name);
        Assert.AreEqual(2, mat.passCount, "ObstacleDepthClear 셰이더는 Stencil 마킹(Pass 0) + 깊이 초기화(Pass 1) 2패스여야 합니다.");
    }

    [Test]
    public void TestFloorMaterial_IsOpaqueAndWritesDepth()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TestFloor.mat");
        Assert.IsNotNull(mat, "Assets/Materials/TestFloor.mat을 찾을 수 없습니다.");
        Assert.AreEqual(0f, mat.GetFloat("_Surface"), "바닥 머티리얼 TestFloor는 Opaque(_Surface=0)여야 합니다.");
        Assert.AreEqual(1f, mat.GetFloat("_ZWrite"), "바닥 머티리얼 TestFloor는 ZWrite가 활성화되어야 합니다.");
        Assert.Less(mat.renderQueue, (int)UnityEngine.Rendering.RenderQueue.AlphaTest,
            "바닥 머티리얼 TestFloor는 Opaque(2000) 큐에서 바닥 그리드(2490)보다 먼저 렌더링되어야 합니다.");
        Assert.IsTrue(mat.GetShaderPassEnabled("DepthOnly"), "바닥 머티리얼은 DepthOnly 패스가 활성화되어야 합니다.");
    }

    [Test]
    public void WorldDepthUI_LayerAndRenderPass_RunAfterTransparentsWithoutDuplicateMainTransparentDraw()
    {
        int worldDepthUiLayer = LayerMask.NameToLayer(ObstacleDepthClearRenderPass.WorldDepthUILayerName);
        Assert.GreaterOrEqual(worldDepthUiLayer, 0, "TagManager에 WorldDepthUI 레이어가 등록되어 있어야 합니다.");

        int worldDepthUiBit = 1 << worldDepthUiLayer;
        UniversalRendererData pcRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        Assert.IsNotNull(pcRenderer, "PC_Renderer.asset을 찾을 수 없습니다.");
        Assert.AreEqual(0, pcRenderer.transparentLayerMask.value & worldDepthUiBit,
            "PC_Renderer의 TransparentLayerMask에서 WorldDepthUI 레이어를 제외해야 AfterRenderingTransparents 패스에서만 1회 렌더링됩니다.");

        UniversalRendererData mobileRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
        Assert.IsNotNull(mobileRenderer, "Mobile_Renderer.asset을 찾을 수 없습니다.");
        Assert.AreEqual(0, mobileRenderer.transparentLayerMask.value & worldDepthUiBit,
            "Mobile_Renderer의 TransparentLayerMask에서 WorldDepthUI 레이어를 제외해야 합니다.");

        using (ObstacleDepthClearRenderPass pass = new ObstacleDepthClearRenderPass())
        {
            Assert.IsTrue(pass.IsReady, "ObstacleDepthClearRenderPass가 준비 상태여야 합니다.");
            Assert.AreEqual(RenderPassEvent.AfterRenderingTransparents, pass.renderPassEvent,
                "기둥·함정·Skybox·반투명 기둥 손상을 막기 위해 반드시 AfterRenderingTransparents에서 실행되어야 합니다.");
            Assert.AreEqual(worldDepthUiBit, pass.WorldDepthUILayerMask,
                "ObstacleDepthClearRenderPass가 WorldDepthUI 레이어 마스크를 정상 바인딩해야 합니다.");
        }
    }

    private CharacterStatusBarUI CreateStubStatusBar(Transform parent, string name, Transform target)
    {
        GameObject go = Track(new GameObject(name, typeof(RectTransform)));
        go.transform.SetParent(parent, false);
        CharacterStatusBarUI ui = go.AddComponent<CharacterStatusBarUI>();
        ui.SetTarget(target, Vector3.zero);
        return ui;
    }

    private CharacterEffectListUI CreateStubEffectList(Transform parent, string name, Transform target)
    {
        GameObject go = Track(new GameObject(name, typeof(RectTransform)));
        go.transform.SetParent(parent, false);
        CharacterEffectListUI ui = go.AddComponent<CharacterEffectListUI>();
        ui.SetTarget(target, Vector3.zero);
        return ui;
    }

    private static void RegisterMeta(UIManager uiManager, Component comp, int typeOrder)
    {
        FieldInfo field = typeof(UIManager).GetField("_worldUIMetaByTransform", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        object dict = field.GetValue(uiManager);

        System.Type metaType = typeof(UIManager).GetNestedType("WorldUISortMeta", BindingFlags.NonPublic);
        Assert.IsNotNull(metaType);
        object meta = System.Activator.CreateInstance(metaType);
        metaType.GetField("component").SetValue(meta, comp);
        metaType.GetField("followUI").SetValue(meta, comp as IWorldFollowUI);
        metaType.GetField("typeOrder").SetValue(meta, typeOrder);

        MethodInfo setItem = dict.GetType().GetMethod("set_Item");
        setItem.Invoke(dict, new object[] { comp.transform, meta });
    }
}
