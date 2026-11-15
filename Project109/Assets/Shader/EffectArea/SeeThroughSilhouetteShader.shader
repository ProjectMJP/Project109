Shader "Custom/SeeThroughSilhouetteShader"
{
    Properties
    {
        [HDR] _SilhouetteColor("Silhouette Color", Color) = (0.0, 0.8, 1.0, 0.5)
        _RimPower("Rim Power", Range(0.5, 8.0)) = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "SeeThroughSilhouette"
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Back
            
            Stencil
            {
                Ref 1
                Comp Equal
                Pass Keep
            }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
            };
            
            half4 _SilhouetteColor;
            float _RimPower;

            float4 _SeeThroughMousePos;
            float _SeeThroughRadius;
            float _SeeThroughSoftness;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float dist = distance(input.positionWS, _SeeThroughMousePos.xyz);
                
                float edgeStart = _SeeThroughRadius - _SeeThroughSoftness;
                float edgeEnd = _SeeThroughRadius;
                
                float mask = 1.0 - smoothstep(edgeStart, edgeEnd, dist);
                
                if (mask <= 0.0)
                {
                    discard;
                }

                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float3 normal = normalize(input.normalWS);
                float rim = 1.0 - saturate(dot(normal, viewDir));
                rim = pow(rim, _RimPower);
                
                half4 col = _SilhouetteColor;
                col.rgb += rim * 0.4;
                col.a *= mask;
                
                return col;
            }
            ENDHLSL
        }
    }
}
