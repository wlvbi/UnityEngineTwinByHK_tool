Shader "EngineTwin/ClipShaderFinal"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _MainTex ("Main Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0.8, 0.8, 0.8, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0
        _MetallicGlossMap ("Metallic Map", 2D) = "white" {}
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1.0
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

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
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 uvMainTex : TEXCOORD3; // 为 _MainTex 添加独立 UV
                float4 tangentWS : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _BumpScale;
                half _OcclusionStrength;
                half4 _EmissionColor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap);
            SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            float4 _EngineClipPlane;
            float _EngineClipEnabled;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uvMainTex = TRANSFORM_TEX(input.uv, _MainTex); // 为 _MainTex 生成独立 UV

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

                // 采样纹理（两者都支持动画）
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvMainTex);

                // 如果 _MainTex 有实际纹理（不是默认白色），用 _MainTex 的 UV 重新采样 _BaseMap
                half mainTexPresent = step(0.01, length(mainTex.rgb - half3(1,1,1)));
                half4 baseMapAnimated = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uvMainTex);
                baseMap = lerp(baseMap, baseMapAnimated, mainTexPresent);

                // 最终纹理：优先 _MainTex，否则 _BaseMap（可能带动画）
                half4 finalTex = lerp(baseMap, mainTex, mainTexPresent);
                half4 albedo = finalTex * _BaseColor;

                // 采样法线贴图
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = normalize(mul(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));

                // 采样金属度/光滑度贴图
                half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half metallic = metallicGloss.r * _Metallic;
                half smoothness = metallicGloss.a * _Smoothness;

                // 采样遮蔽贴图
                half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g;
                occlusion = lerp(1.0, occlusion, _OcclusionStrength);

                // 采样自发光
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                // PBR 光照
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = input.positionWS;
                lightingInput.normalWS = normalWS;
                lightingInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lightingInput.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lightingInput.bakedGI = SampleSH(normalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.occlusion = occlusion;
                surfaceData.emission = emission;
                surfaceData.alpha = 1;

                return UniversalFragmentPBR(lightingInput, surfaceData);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
