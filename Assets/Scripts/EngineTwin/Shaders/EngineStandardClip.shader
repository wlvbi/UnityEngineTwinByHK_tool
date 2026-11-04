// 发动机剖切材质：Standard 光照 + 全局剖切平面裁剪，双面渲染，内表面显示为剖面色（带斜线）。
// 属性名与 Standard 一致（_Color/_MainTex/_Metallic/_Glossiness/_EmissionColor），高亮用的 MaterialPropertyBlock 照常生效。
Shader "EngineTwin/StandardClip"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _SectionColor ("Section Color", Color) = (0.90, 0.36, 0.16, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _EmissionColor;
        fixed4 _SectionColor;
        half _Metallic;
        half _Glossiness;

        // 全局：xyz = 平面法线（保留法线正方向一侧），w = 平面到原点距离
        float4 _EngineClipPlane;
        float _EngineClipEnabled;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float facing : VFACE;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            if (_EngineClipEnabled > 0.5)
                clip(dot(IN.worldPos, _EngineClipPlane.xyz) - _EngineClipPlane.w);

            if (IN.facing < 0)
            {
                // 剖面：看到的是模型内表面，画成剖面色 + 斜线（自发光补偿背面法线的光照）
                float hatch = step(0.5, frac((IN.worldPos.x + IN.worldPos.y + IN.worldPos.z) * 70.0));
                o.Albedo = _SectionColor.rgb * lerp(0.55, 0.85, hatch);
                o.Metallic = 0;
                o.Smoothness = 0.15;
                o.Emission = _SectionColor.rgb * lerp(0.25, 0.4, hatch);
                o.Alpha = 1;
                return;
            }

            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
