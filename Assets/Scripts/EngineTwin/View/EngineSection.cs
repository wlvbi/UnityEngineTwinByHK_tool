using System.Collections.Generic;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>
    /// 剖切：把发动机外壳材质换成 EngineTwin/StandardClip 的运行时副本（不改工程里的材质资产），
    /// 用全局着色器参数控制剖切平面。内部的曲轴/活塞用普通材质，不会被裁掉。
    /// </summary>
    public class EngineSection : MonoBehaviour
    {
        public enum CutAxis { Longitudinal, Transverse }

        static readonly int PlaneId = Shader.PropertyToID("_EngineClipPlane");
        static readonly int EnabledId = Shader.PropertyToID("_EngineClipEnabled");

        Transform root;
        Bounds localBounds;
        bool isOn;
        CutAxis axis = CutAxis.Longitudinal;
        float position01 = 0.5f;

        public bool IsOn => isOn;
        public CutAxis Axis => axis;
        public float Position01 => position01;

        public void Init(Transform engineRoot, IReadOnlyList<EnginePart> parts, Shader clipShader)
        {
            root = engineRoot;
            if (clipShader == null) clipShader = Shader.Find("EngineTwin/StandardClip");
            if (clipShader == null)
            {
                Debug.LogError("[EngineTwin] 未找到 EngineTwin/StandardClip 着色器，剖切不可用");
                enabled = false;
                return;
            }

            var map = new Dictionary<Material, Material>();
            clipMap = map;
            this.clipShader = clipShader;
            bool first = true;
            foreach (var part in parts)
                foreach (var r in part.Renderers)
                {
                    ConvertRenderer(r);

                    Bounds lb = ToLocal(r.bounds);
                    if (first) { localBounds = lb; first = false; }
                    else localBounds.Encapsulate(lb);
                }

            Apply();
        }

        Dictionary<Material, Material> clipMap;
        Shader clipShader;

        /// <summary>让额外的物体（如内部曲柄连杆机构）也参与剖切</summary>
        public void Include(Transform t)
        {
            if (t == null || clipMap == null) return;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) ConvertRenderer(r);
        }

        void ConvertRenderer(Renderer r)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                if (src == null) continue;
                if (!clipMap.TryGetValue(src, out var clip))
                {
                    clip = new Material(clipShader) { name = src.name + " (Clip)" };

                    // 复制所有 URP Lit 的属性
                    CopyMaterialProperties(src, clip);

                    clipMap[src] = clip;
                }
                mats[i] = clip;
            }
            r.sharedMaterials = mats;
        }

        void CopyMaterialProperties(Material src, Material dst)
        {
            // 基础颜色和纹理
            if (src.HasProperty("_BaseColor")) dst.SetColor("_BaseColor", src.GetColor("_BaseColor"));
            if (src.HasProperty("_Color")) dst.SetColor("_BaseColor", src.GetColor("_Color"));

            // 主纹理（支持 Unity 的快捷属性 mainTexture）
            Texture mainTex = src.mainTexture; // 优先使用快捷属性
            if (mainTex != null)
            {
                if (dst.HasProperty("_MainTex")) dst.SetTexture("_MainTex", mainTex);
                if (dst.HasProperty("_BaseMap")) dst.SetTexture("_BaseMap", mainTex);
            }
            else
            {
                if (src.HasProperty("_BaseMap")) dst.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                if (src.HasProperty("_MainTex") && dst.HasProperty("_MainTex"))
                    dst.SetTexture("_MainTex", src.GetTexture("_MainTex"));
            }

            // 纹理缩放和偏移（优先从 mainTextureScale/Offset 获取）
            dst.SetTextureScale("_MainTex", src.mainTextureScale);
            dst.SetTextureOffset("_MainTex", src.mainTextureOffset);
            if (dst.HasProperty("_BaseMap"))
            {
                dst.SetTextureScale("_BaseMap", src.mainTextureScale);
                dst.SetTextureOffset("_BaseMap", src.mainTextureOffset);
            }

            // 金属度和光滑度
            if (src.HasProperty("_Metallic")) dst.SetFloat("_Metallic", src.GetFloat("_Metallic"));
            if (src.HasProperty("_Smoothness")) dst.SetFloat("_Smoothness", src.GetFloat("_Smoothness"));
            if (src.HasProperty("_Glossiness")) dst.SetFloat("_Smoothness", src.GetFloat("_Glossiness"));
            if (src.HasProperty("_MetallicGlossMap")) dst.SetTexture("_MetallicGlossMap", src.GetTexture("_MetallicGlossMap"));

            // 法线贴图
            if (src.HasProperty("_BumpMap"))
            {
                dst.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
                if (src.HasProperty("_BumpScale")) dst.SetFloat("_BumpScale", src.GetFloat("_BumpScale"));
            }

            // 遮蔽贴图
            if (src.HasProperty("_OcclusionMap"))
            {
                dst.SetTexture("_OcclusionMap", src.GetTexture("_OcclusionMap"));
                if (src.HasProperty("_OcclusionStrength")) dst.SetFloat("_OcclusionStrength", src.GetFloat("_OcclusionStrength"));
            }

            // 自发光
            if (src.HasProperty("_EmissionColor")) dst.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
            if (src.HasProperty("_EmissionMap")) dst.SetTexture("_EmissionMap", src.GetTexture("_EmissionMap"));
            if (src.IsKeywordEnabled("_EMISSION")) dst.EnableKeyword("_EMISSION");

            // 高光
            if (src.HasProperty("_SpecColor")) dst.SetColor("_SpecColor", src.GetColor("_SpecColor"));
            if (src.HasProperty("_SpecGlossMap")) dst.SetTexture("_SpecGlossMap", src.GetTexture("_SpecGlossMap"));

            // 渲染模式关键字
            if (src.IsKeywordEnabled("_NORMALMAP")) dst.EnableKeyword("_NORMALMAP");
            if (src.IsKeywordEnabled("_METALLICSPECGLOSSMAP")) dst.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (src.IsKeywordEnabled("_OCCLUSIONMAP")) dst.EnableKeyword("_OCCLUSIONMAP");
            if (src.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")) dst.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            if (src.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF")) dst.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (src.IsKeywordEnabled("_SPECULAR_SETUP")) dst.EnableKeyword("_SPECULAR_SETUP");

            // 渲染队列和模式
            dst.renderQueue = src.renderQueue;

            // 复制所有浮点属性（通用方法）
            var srcShader = src.shader;
            int propCount = srcShader.GetPropertyCount();
            for (int i = 0; i < propCount; i++)
            {
                var propName = srcShader.GetPropertyName(i);
                var propType = srcShader.GetPropertyType(i);

                if (!dst.HasProperty(propName)) continue;

                try
                {
                    switch (propType)
                    {
                        case UnityEngine.Rendering.ShaderPropertyType.Float:
                        case UnityEngine.Rendering.ShaderPropertyType.Range:
                            if (src.HasProperty(propName) && !propName.StartsWith("_Engine"))
                                dst.SetFloat(propName, src.GetFloat(propName));
                            break;
                        case UnityEngine.Rendering.ShaderPropertyType.Vector:
                            if (src.HasProperty(propName) && !propName.StartsWith("_Engine"))
                                dst.SetVector(propName, src.GetVector(propName));
                            break;
                    }
                }
                catch { /* 忽略不兼容的属性 */ }
            }
        }

        Bounds ToLocal(Bounds world)
        {
            var b = new Bounds(root.InverseTransformPoint(world.center), Vector3.zero);
            b.Encapsulate(root.InverseTransformPoint(world.min));
            b.Encapsulate(root.InverseTransformPoint(world.max));
            return b;
        }

        public void SetOn(bool on) { isOn = on; Apply(); }
        public void SetAxis(CutAxis a) { axis = a; Apply(); }
        public void SetPosition(float t01) { position01 = Mathf.Clamp01(t01); Apply(); }

        void Apply()
        {
            if (root == null) return;

            // 默认相机在发动机右前方：纵剖去掉 -X（排气侧）一半，横剖去掉前端（+Z）一部分
            Vector3 n; float c;
            if (axis == CutAxis.Longitudinal)
            {
                n = root.right;
                c = Mathf.Lerp(localBounds.min.x, localBounds.max.x, position01);
            }
            else
            {
                n = -root.forward;
                c = -Mathf.Lerp(localBounds.min.z, localBounds.max.z, position01);
            }

            Vector3 pointOnPlane = root.TransformPoint(axis == CutAxis.Longitudinal
                ? new Vector3(c, 0f, 0f)
                : new Vector3(0f, 0f, -c));
            Shader.SetGlobalVector(PlaneId, new Vector4(n.x, n.y, n.z, Vector3.Dot(n, pointOnPlane)));
            Shader.SetGlobalFloat(EnabledId, isOn ? 1f : 0f);
        }

        void LateUpdate()
        {
            if (isOn) Apply();   // 发动机根节点被移动/振动时剖切面跟随
        }

        void OnDisable() => Shader.SetGlobalFloat(EnabledId, 0f);
    }
}
