Shader "EngineTwin/Hologram"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _MainTex ("Main Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0.3, 0.8, 1.0, 0.6)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 1.2
        _ScanlineSpeed ("Scanline Speed", Range(0, 10)) = 2.0
        _ScanlineScale ("Scanline Scale", Range(1, 50)) = 20.0
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.3
        _FlickerSpeed ("Flicker Speed", Range(0, 20)) = 5.0
        _FlickerIntensity ("Flicker Intensity", Range(0, 0.5)) = 0.15
        _Transparency ("Transparency", Range(0, 1)) = 0.6
        _FresnelBias ("Fresnel Bias", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

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
                float2 uvMainTex : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _RimPower;
                half _RimIntensity;
                half _ScanlineSpeed;
                half _ScanlineScale;
                half _ScanlineIntensity;
                half _FlickerSpeed;
                half _FlickerIntensity;
                half _Transparency;
                half _FresnelBias;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _EngineClipPlane;
            float _EngineClipEnabled;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uvMainTex = TRANSFORM_TEX(input.uv, _MainTex);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 剪裁平面
                if (_EngineClipEnabled > 0.5)
                {
                    float dist = dot(input.positionWS, _EngineClipPlane.xyz) + _EngineClipPlane.w;
                    clip(dist);
                }

                // 采样纹理
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvMainTex);

                // 纹理混合（支持运动纹理如皮带）
                half mainTexPresent = step(0.01, length(mainTex.rgb - half3(1,1,1)));
                half4 baseMapAnimated = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uvMainTex);
                baseMap = lerp(baseMap, baseMapAnimated, mainTexPresent);
                half4 finalTex = lerp(baseMap, mainTex, mainTexPresent);

                // Fresnel 边缘光
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float fresnel = _FresnelBias + (1.0 - _FresnelBias) * pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                half3 rimColor = _BaseColor.rgb * fresnel * _RimIntensity;

                // 扫描线效果（世界空间 Y 轴）
                float scanline = sin((input.positionWS.y * _ScanlineScale + _Time.y * _ScanlineSpeed) * 3.14159);
                scanline = scanline * 0.5 + 0.5; // 重映射到 [0,1]
                scanline = lerp(1.0, scanline, _ScanlineIntensity);

                // 闪烁效果
                float flicker = sin(_Time.y * _FlickerSpeed) * 0.5 + 0.5;
                flicker = lerp(1.0, flicker, _FlickerIntensity);

                // 合成最终颜色
                half3 finalColor = finalTex.rgb * _BaseColor.rgb * 0.5; // 纹理底色（降低亮度）
                finalColor += rimColor; // 边缘光
                finalColor *= scanline * flicker; // 扫描线和闪烁

                // 透明度：结合 Fresnel 和设定值
                half alpha = lerp(_Transparency, 1.0, fresnel * 0.5);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
