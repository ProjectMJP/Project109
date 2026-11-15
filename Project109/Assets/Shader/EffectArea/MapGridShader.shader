Shader "Custom/MapGridShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color (Ground Tile Tint)", Color) = (1,1,1,1)
        _WalkableColor("Walkable Color Tint", Color) = (0.2, 0.6, 1.0, 1.0)
        _SelectedColor("Selected Color Tint", Color) = (1.0, 0.8, 0.2, 1.0)
        
        [NoScaleOffset] _MaskTex("Mask Texture (R:Walk, G:Select, B:Base)", 2D) = "black" {}
        [NoScaleOffset] _BaseTex("Base Tile Texture", 2D) = "white" {}
        [NoScaleOffset] _WalkableTex("Walkable Indicator Texture", 2D) = "white" {}
        [NoScaleOffset] _SelectedTex("Selected Indicator Texture", 2D) = "white" {}
        
        _GridColumns("Grid Columns", Float) = 14
        _GridRows("Grid Rows", Float) = 14
        [IntRange] _QueueOffset("Queue Offset (Sorting Priority)", Range(-50, 50)) = 0
        
        _LineColor("Grid Line Color", Color) = (0,0,0,0.5)
        _LineThickness("Grid Line Thickness", Range(0.0, 0.2)) = 0.01
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            TEXTURE2D(_BaseTex);
            SAMPLER(sampler_BaseTex);

            TEXTURE2D(_WalkableTex);
            SAMPLER(sampler_WalkableTex);

            TEXTURE2D(_SelectedTex);
            SAMPLER(sampler_SelectedTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _WalkableColor;
                float4 _SelectedColor;
                float _GridColumns;
                float _GridRows;
                float _QueueOffset;
                float4 _LineColor;
                float _LineThickness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. 마스크 텍스처 샘플링 (Point Filter를 타므로 칸 경계가 뚜렷함)
                float4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                
                // 2. 각 격자 칸 내부에서 0~1로 반복되는 타일 UV 계산
                float2 cellUV = frac(input.uv * float2(_GridColumns, _GridRows));
                
                // 3. 각 텍스처 샘플링 및 색상 보정
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, cellUV) * _BaseColor;
                float4 walkableColor = SAMPLE_TEXTURE2D(_WalkableTex, sampler_WalkableTex, cellUV) * _WalkableColor;
                float4 selectedColor = SAMPLE_TEXTURE2D(_SelectedTex, sampler_SelectedTex, cellUV) * _SelectedColor;
                
                // 4. 레이어별 순차적 합성 (배경 바닥 -> 이동 범위 -> 선택 효과)
                // B채널이 1인 타일(이동 가능 베이스 타일)에만 기본 바닥 출력
                float4 finalColor = baseColor * mask.b;
                
                // R채널에 따라 이동 표시 텍스처 오버레이
                finalColor = lerp(finalColor, walkableColor, walkableColor.a * mask.r);
                
                // G채널에 따라 캐릭터 선택 하이라이트 오버레이
                finalColor = lerp(finalColor, selectedColor, selectedColor.a * mask.g);
                
                // 5. 격자 경계선 그리기 (B채널이 1인 이동 가능한 타일에만 그리며, 기둥 등의 장애물 영역은 제외)
                float2 distToEdge = min(cellUV, 1.0 - cellUV);
                float minEdgeDist = min(distToEdge.x, distToEdge.y);
                float gridLine = step(minEdgeDist, _LineThickness);
                
                finalColor = lerp(finalColor, _LineColor, gridLine * mask.b * _LineColor.a);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    CustomEditor "MapGridShaderGUI"
}
