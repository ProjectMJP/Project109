Shader "Custom/ObstacleSeeThroughFade"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (Texture)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _FadeAlpha("Fade Alpha", Range(0.0, 1.0)) = 0.35
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _FadeAlpha;
            half _Smoothness;
        CBUFFER_END
        ENDHLSL

        // Pass 1: 가장 앞쪽 표면의 깊이만 기록하여 반투명 시 뒷면 겹침 얼룩을 방지
        Pass
        {
            Name "DepthPrepass"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertDepth(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 fragDepth(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Pass 2: 기록된 최전면 깊이와 일치하는 픽셀만 URP 라이팅 + 알파 블렌딩 렌더링
        Pass
        {
            Name "ForwardFadeLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest Equal
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertLit
            #pragma fragment fragLit

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings vertLit(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 fragLit(Varyings input) : SV_Target
            {
                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                half3 diffuse = LightingLambert(mainLight.color, mainLight.direction, normalWS);
                half3 ambient = SampleSH(normalWS);

                half3 finalColor = texCol.rgb * (diffuse + ambient);
                half finalAlpha = saturate(texCol.a * _FadeAlpha);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}
