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
        Tags { "RenderType"="Transparent" "Queue"="AlphaTest+40" "RenderPipeline"="UniversalPipeline" }
        
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
                const float HALF_SQRT3 = 0.866025404;
                const float INV_SQRT3  = 0.577350269;

                // 1. Quad UV -> 정규화 육각 평면 좌표 (cellSize = 1.0 기준)
                float extraX = step(1.5, _GridRows) * 0.5;
                float totalW = _GridColumns + extraX;
                float totalH = (_GridRows - 1.0) * HALF_SQRT3 + 2.0 * INV_SQRT3;
                float2 p = float2(input.uv.x * totalW - 0.5, input.uv.y * totalH - INV_SQRT3);

                // 2. Fractional Axial -> Cube Round -> Odd-R (col, row) 역변환
                float rf = p.y / HALF_SQRT3;
                float qf = p.x - rf * 0.5;
                float sf = -qf - rf;

                float rq = round(qf);
                float rr = round(rf);
                float rs = round(sf);

                float qd = abs(rq - qf);
                float rd = abs(rr - rf);
                float sd = abs(rs - sf);

                if (qd > rd && qd > sd)
                {
                    rq = -rr - rs;
                }
                else if (rd > sd)
                {
                    rr = -rq - rs;
                }

                float oddShift = rr - 2.0 * floor(rr * 0.5);
                float col = rq + (rr - oddShift) * 0.5;
                float row = rr;

                // 3. 그리드 경계 클리핑 및 _MaskTex 텍셀 중앙 샘플링
                float inBounds = step(-0.5, col) * step(col, _GridColumns - 0.5)
                               * step(-0.5, row) * step(row, _GridRows - 0.5);
                float2 maskUV = (float2(col, row) + 0.5) / float2(max(1.0, _GridColumns), max(1.0, _GridRows));
                float4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV) * inBounds;

                // 4. Pointy-Top Hex 로컬 좌표 및 SDF 계산 (내접원 반지름 = 0.5)
                float2 hexCenter = float2(col + oddShift * 0.5, row * HALF_SQRT3);
                float2 localP = p - hexCenter;
                float2 ap = abs(localP);
                float hexDist = max(ap.x, ap.x * 0.5 + ap.y * HALF_SQRT3);
                float distToEdge = 0.5 - hexDist;

                float2 cellUV = saturate(localP + 0.5);

                // 5. 각 텍스처 샘플링 및 색상 보정
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, cellUV) * _BaseColor;
                float4 walkableColor = SAMPLE_TEXTURE2D(_WalkableTex, sampler_WalkableTex, cellUV) * _WalkableColor;
                float4 selectedColor = SAMPLE_TEXTURE2D(_SelectedTex, sampler_SelectedTex, cellUV) * _SelectedColor;

                // 6. 레이어별 순차적 합성 (배경 바닥 -> 이동 범위 -> 선택 효과)
                float4 finalColor = baseColor * mask.b;
                finalColor = lerp(finalColor, walkableColor, walkableColor.a * mask.r);
                finalColor = lerp(finalColor, selectedColor, selectedColor.a * mask.g);

                // 7. 안티앨리어싱 적용 Pointy-Top 육각 경계선 (B채널이 1인 유효 타일에만 출력)
                float aa = max(fwidth(distToEdge), 0.001);
                float hasLine = step(0.0001, _LineThickness);
                float gridLine = (1.0 - smoothstep(max(0.0, _LineThickness - aa), _LineThickness + aa, distToEdge)) * hasLine;

                finalColor = lerp(finalColor, _LineColor, gridLine * mask.b * _LineColor.a);
                finalColor *= inBounds;

                return finalColor;
            }
            ENDHLSL
        }
    }
    CustomEditor "MapGridShaderGUI"
}
