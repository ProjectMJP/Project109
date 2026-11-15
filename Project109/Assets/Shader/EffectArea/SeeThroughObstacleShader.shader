Shader "Custom/SeeThroughObstacleShader"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (Texture)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "Queue"="Geometry" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD3;
                float4 screenPos    : TEXCOORD4;
                half3 vertexLight   : TEXCOORD5; // 버텍스 셰이더에서 계산된 라이팅 컬러 보간 전달용
            };
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            // 글로벌 마스킹 변수 (MouseSeeThroughController에서 갱신)
            float4 _SeeThroughMousePos; // x: 마우스 X * Aspect, y: 마우스 Y, z: Aspect
            float _SeeThroughRadius; // 스크린 단위 투과 반경
            float _SeeThroughSoftness; // 스크린 단위 테두리 감쇄

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);

                // 라이팅 연산(Lambert + Ambient SH)을 정점(Vertex) 단계에서 수행하여 GPU 픽셀 연산 오버헤드 극대화 억제
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                Light mainLight = GetMainLight();
                
                half3 diffuse = LightingLambert(mainLight.color, mainLight.direction, normalWS);
                half3 ambient = SampleSH(normalWS);
                
                output.vertexLight = diffuse + ambient;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 스크린 공간 픽셀 좌표 원근 복원 및 종횡비 보정
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float aspect = _SeeThroughMousePos.z;
                
                float2 correctedPixelUV = float2(screenUV.x * aspect, screenUV.y);
                float2 correctedMouseUV = _SeeThroughMousePos.xy;
                
                // 비싼 distance(제곱근) 함수 대신 dot() 내적을 활용해 제곱 거리 연산 적용 (GPU 연산 대폭 최적화)
                float2 offset = correctedPixelUV - correctedMouseUV;
                float distSq = dot(offset, offset);
                
                float radius = _SeeThroughRadius;
                float softness = _SeeThroughSoftness;
                
                float edgeStart = radius - softness;
                float edgeStartSq = edgeStart * edgeStart;
                float edgeEndSq = radius * radius;
                
                // 부드러운 외곽선 감쇄 마스크 연산
                float mask = smoothstep(edgeStartSq, edgeEndSq, distSq);
                
                // 마스크 영역 내부(마우스 시선 터널)의 픽셀을 완전히 날려버림
                clip(mask - 0.01);

                // 2. URP 표준 매크로 기반 텍스처 샘플링
                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                
                // 3. 버텍스 단계에서 완성된 라이팅 값을 최종 보간 결합 (픽셀당 SH 계산 제거로 렉 현상 차단)
                half3 finalColor = texCol.rgb * input.vertexLight;
                
                return half4(finalColor, texCol.a);
            }
            ENDHLSL
        }
    }
}
