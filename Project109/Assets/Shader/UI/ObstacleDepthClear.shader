Shader "Hidden/Project109/ObstacleDepthClear"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Pass 0: 화면에 실제로 노출된 Obstacle 픽셀에만 Stencil Bit 2(Ref 2)를 마킹 (캐릭터 뒤에 가려진 기둥은 ZTest LEqual 탈락)
        Pass
        {
            Name "MarkVisibleObstacleStencil"
            Tags { "LightMode" = "UniversalForward" }

            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Cull Back

            Stencil
            {
                Ref 2
                WriteMask 2
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex vertNormal
            #pragma fragment fragEmpty

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertNormal(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 fragEmpty(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Pass 1: Pass 0에서 마킹된(실제 노출된) 기둥 픽셀의 깊이만 Far로 초기화하고 스텐실 비트 원복
        Pass
        {
            Name "ResetVisibleObstacleDepth"
            Tags { "LightMode" = "UniversalForward" }

            ColorMask 0
            ZWrite On
            ZTest Always
            Cull Back

            Stencil
            {
                Ref 2
                ReadMask 2
                WriteMask 2
                Comp Equal
                Pass Zero
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex vertFar
            #pragma fragment fragEmpty

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertFar(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                #if UNITY_REVERSED_Z
                    output.positionCS.z = 0.0;
                #else
                    output.positionCS.z = output.positionCS.w;
                #endif
                return output;
            }

            half4 fragEmpty(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
