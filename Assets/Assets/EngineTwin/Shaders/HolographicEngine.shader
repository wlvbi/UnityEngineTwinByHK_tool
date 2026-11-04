Shader "EngineTwin/HolographicEngine"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.2, 0.6, 1, 1)
        _EdgeColor ("Edge Color", Color) = (0, 1, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 3
        _Alpha ("Alpha", Range(0, 1)) = 0.5

        [Header(Scan Line)]
        _ScanSpeed ("Scan Speed", Range(0, 5)) = 1
        _ScanWidth ("Scan Width", Range(0, 1)) = 0.1
        _ScanColor ("Scan Color", Color) = (0, 1, 1, 1)

        [Header(Flow)]
        _FlowMap ("Flow Map (RG)", 2D) = "white" {}
        _FlowSpeed ("Flow Speed", Range(0, 5)) = 1
        _FlowIntensity ("Flow Intensity", Range(0, 1)) = 0.5

        [Header(Grid)]
        _GridSize ("Grid Size", Range(0.01, 1)) = 0.1
        _GridThickness ("Grid Thickness", Range(0, 0.1)) = 0.02
        _GridColor ("Grid Color", Color) = (0.2, 0.8, 1, 0.3)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                half4 _EdgeColor;
                half _FresnelPower;
                half _Alpha;
                half _ScanSpeed;
                half _ScanWidth;
                half4 _ScanColor;
                half _FlowSpeed;
                half _FlowIntensity;
                half _GridSize;
                half _GridThickness;
                half4 _GridColor;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_FlowMap);
            SAMPLER(sampler_FlowMap);

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Fresnel 边缘光
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);

                // 基础颜色
                half4 color = _BaseColor;

                // 扫描线效果
                float scanPos = frac(_Time.y * _ScanSpeed);
                float scanLine = smoothstep(_ScanWidth, 0, abs(input.positionWS.y - scanPos * 2 - 1));
                color.rgb += _ScanColor.rgb * scanLine;

                // 网格效果
                float2 grid = abs(frac(input.positionWS.xz / _GridSize) - 0.5);
                float gridPattern = step(_GridThickness, min(grid.x, grid.y));
                color.rgb = lerp(_GridColor.rgb, color.rgb, gridPattern);

                // 流光效果
                float2 flowUV = input.positionWS.xz * 0.5 + _Time.y * _FlowSpeed * 0.1;
                half4 flow = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, flowUV);
                color.rgb += flow.rgb * _FlowIntensity * fresnel;

                // 边缘发光
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, fresnel * 0.8);

                // Alpha
                color.a = _Alpha + fresnel * 0.3;

                // 雾效
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
