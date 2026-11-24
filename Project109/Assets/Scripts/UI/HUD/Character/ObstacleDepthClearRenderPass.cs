using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 모든 3D 불투명·Skybox·반투명 렌더링 완료 직후(AfterRenderingTransparents) 화면에 실제로 노출된
/// 장애물(Obstacle 레이어: 불투명 기둥 및 반투명 페이드 기둥 포함) 픽셀의 깊이만 Far로 초기화한 뒤,
/// WorldDepthUI 레이어(적 월드 상태바 등)를 렌더링합니다.
/// 이를 통해 기둥·함정·바닥·Skybox·반투명 기둥이 손상되지 않으면서도, 앞쪽 캐릭터의 3D 깊이는 유지하고
/// 기둥 뒤에 숨은 적의 월드 상태바가 완전 불투명(1.0)으로 표시되도록 합니다.
/// </summary>
public sealed class ObstacleDepthClearRenderPass : ScriptableRenderPass, IDisposable
{
    public const string ResourceMaterialPath = "Rendering/ObstacleDepthClear";
    public const string ShaderName = "Hidden/Project109/ObstacleDepthClear";
    public const string WorldDepthUILayerName = "WorldDepthUI";

    private static readonly List<ShaderTagId> ShaderTagIdList = new List<ShaderTagId>
    {
        new ShaderTagId("SRPDefaultUnlit"),
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("UniversalForwardOnly")
    };

    private sealed class PassData
    {
        public RendererListHandle markObstacleList;
        public RendererListHandle resetObstacleDepthList;
        public RendererListHandle worldDepthUIList;
        public bool drawWorldDepthUI;
    }

    private readonly int _obstacleLayerMask;
    private readonly int _worldDepthUiLayerMask;
    private Material _overrideMaterial;
    private bool _ownsMaterial;

    public bool IsReady => _obstacleLayerMask != 0 && _overrideMaterial != null;
    public int WorldDepthUILayerMask => _worldDepthUiLayerMask;

    public ObstacleDepthClearRenderPass()
    {
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        _obstacleLayerMask = LayerMask.GetMask("Obstacle");
        _worldDepthUiLayerMask = LayerMask.GetMask(WorldDepthUILayerName);
        EnsureMaterial();
    }

    private void EnsureMaterial()
    {
        if (_overrideMaterial != null)
        {
            return;
        }

        Material resourceMat = Resources.Load<Material>(ResourceMaterialPath);
        if (resourceMat != null && resourceMat.shader != null)
        {
            _overrideMaterial = resourceMat;
            _ownsMaterial = false;
            return;
        }

        Shader fallbackShader = Shader.Find(ShaderName);
        if (fallbackShader != null)
        {
            _overrideMaterial = new Material(fallbackShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _ownsMaterial = true;
        }
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        EnsureMaterial();
        if (!IsReady)
        {
            return;
        }

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        UniversalLightData lightData = frameData.Get<UniversalLightData>();

        if (!resourceData.activeDepthTexture.IsValid() || !resourceData.activeColorTexture.IsValid())
        {
            return;
        }

        // 불투명 기둥(Geometry)과 반투명 페이드 기둥(ObstacleSeeThroughFade, DepthPrepass ZWrite On)을 모두 포함
        FilteringSettings obstacleFilteringSettings = new FilteringSettings(RenderQueueRange.all, _obstacleLayerMask);

        DrawingSettings markDrawingSettings = RenderingUtils.CreateDrawingSettings(
            ShaderTagIdList, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
        markDrawingSettings.overrideMaterial = _overrideMaterial;
        markDrawingSettings.overrideMaterialPassIndex = 0;

        DrawingSettings resetDrawingSettings = RenderingUtils.CreateDrawingSettings(
            ShaderTagIdList, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
        resetDrawingSettings.overrideMaterial = _overrideMaterial;
        resetDrawingSettings.overrideMaterialPassIndex = 1;

        RendererListParams markParams = new RendererListParams(renderingData.cullResults, markDrawingSettings, obstacleFilteringSettings);
        RendererListParams resetParams = new RendererListParams(renderingData.cullResults, resetDrawingSettings, obstacleFilteringSettings);

        bool drawWorldDepthUI = _worldDepthUiLayerMask != 0;
        RendererListParams worldDepthUiParams = default;
        if (drawWorldDepthUI)
        {
            FilteringSettings worldUiFilteringSettings = new FilteringSettings(RenderQueueRange.transparent, _worldDepthUiLayerMask);
            DrawingSettings worldUiDrawingSettings = RenderingUtils.CreateDrawingSettings(
                ShaderTagIdList, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
            worldDepthUiParams = new RendererListParams(renderingData.cullResults, worldUiDrawingSettings, worldUiFilteringSettings);
        }

        using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("ObstacleDepthClearAndWorldUIPass", out PassData passData))
        {
            passData.markObstacleList = renderGraph.CreateRendererList(markParams);
            passData.resetObstacleDepthList = renderGraph.CreateRendererList(resetParams);
            passData.drawWorldDepthUI = drawWorldDepthUI;

            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
            builder.UseRendererList(passData.markObstacleList);
            builder.UseRendererList(passData.resetObstacleDepthList);

            if (drawWorldDepthUI)
            {
                passData.worldDepthUIList = renderGraph.CreateRendererList(worldDepthUiParams);
                builder.UseRendererList(passData.worldDepthUIList);
            }

            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                // Pass 0 & 1은 ColorMask 0으로 컬러를 보존한 채 노출된 기둥 픽셀의 깊이만 Far로 초기화
                context.cmd.DrawRendererList(data.markObstacleList);
                context.cmd.DrawRendererList(data.resetObstacleDepthList);

                // 모든 3D 불투명·Skybox·반투명 렌더링이 끝난 뒤 WorldDepthUI를 완전 불투명으로 렌더링
                if (data.drawWorldDepthUI)
                {
                    context.cmd.DrawRendererList(data.worldDepthUIList);
                }
            });
        }
    }

    public void Dispose()
    {
        if (_ownsMaterial && _overrideMaterial != null)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_overrideMaterial);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_overrideMaterial);
            }
        }

        _overrideMaterial = null;
        _ownsMaterial = false;
    }
}
