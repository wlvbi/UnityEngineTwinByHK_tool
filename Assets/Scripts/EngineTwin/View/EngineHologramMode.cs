using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>
    /// 全息模式：将发动机模型切换为全息视觉效果（半透明、边缘光、扫描线）。
    /// 不影响粒子特效和 UI。
    /// </summary>
    public class EngineHologramMode : EngineTwinController
    {
        Shader hologramShader;
        readonly Dictionary<Material, Material> hologramMap = new Dictionary<Material, Material>();
        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

        bool isHologramActive;

        void Awake()
        {
            hologramShader = Shader.Find("EngineTwin/Hologram");
            if (hologramShader == null)
            {
                Debug.LogError("[EngineTwin] 未找到全息 Shader: EngineTwin/Hologram");
            }
        }

        /// <summary>
        /// 切换全息模式
        /// </summary>
        public void SetHologramMode(bool enabled, Transform engineRoot)
        {
            if (hologramShader == null) return;
            if (isHologramActive == enabled) return;

            isHologramActive = enabled;

            if (enabled)
            {
                ActivateHologram(engineRoot);
            }
            else
            {
                DeactivateHologram();
            }
        }

        void ActivateHologram(Transform root)
        {
            // 只转换模型渲染器，跳过粒子和 UI
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            foreach (var r in renderers)
            {
                // 跳过粒子系统渲染器
                if (r is ParticleSystemRenderer) continue;

                // 跳过 UI 相关的渲染器
                if (r.GetComponent<UnityEngine.UI.Graphic>() != null) continue;
                if (r.transform.GetComponentInParent<Canvas>() != null) continue;

                // 跳过特效对象（以 FX_ 开头）
                if (r.name.StartsWith("FX_")) continue;

                ConvertToHologram(r);
            }
        }

        void ConvertToHologram(Renderer r)
        {
            var mats = r.sharedMaterials;
            var hologramMats = new Material[mats.Length];

            // 保存原始材质
            if (!originalMaterials.ContainsKey(r))
            {
                originalMaterials[r] = mats;
            }

            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                if (src == null) continue;

                // 复用或创建全息材质
                if (!hologramMap.TryGetValue(src, out var hologram))
                {
                    hologram = new Material(hologramShader) { name = src.name + " (Hologram)" };
                    CopyMaterialProperties(src, hologram);
                    hologramMap[src] = hologram;
                }

                hologramMats[i] = hologram;
            }

            r.sharedMaterials = hologramMats;
        }

        void DeactivateHologram()
        {
            // 恢复原始材质
            foreach (var kvp in originalMaterials)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.sharedMaterials = kvp.Value;
                }
            }

            originalMaterials.Clear();
            hologramMap.Clear();
        }

        void CopyMaterialProperties(Material src, Material dst)
        {
            // 复制基础颜色
            if (src.HasProperty("_BaseColor"))
            {
                Color baseColor = src.GetColor("_BaseColor");
                // 转换为青色全息色调
                Color hologramColor = new Color(
                    baseColor.r * 0.3f + 0.3f,
                    baseColor.g * 0.3f + 0.8f,
                    baseColor.b * 0.3f + 1.0f,
                    0.6f
                );
                dst.SetColor("_BaseColor", hologramColor);
            }
            else if (src.HasProperty("_Color"))
            {
                Color baseColor = src.GetColor("_Color");
                Color hologramColor = new Color(
                    baseColor.r * 0.3f + 0.3f,
                    baseColor.g * 0.3f + 0.8f,
                    baseColor.b * 0.3f + 1.0f,
                    0.6f
                );
                dst.SetColor("_BaseColor", hologramColor);
            }

            // 复制纹理
            Texture mainTex = src.mainTexture;
            if (mainTex != null)
            {
                if (dst.HasProperty("_MainTex")) dst.SetTexture("_MainTex", mainTex);
                if (dst.HasProperty("_BaseMap")) dst.SetTexture("_BaseMap", mainTex);
            }

            // 复制纹理缩放和偏移（支持皮带等运动纹理）
            dst.SetTextureScale("_MainTex", src.mainTextureScale);
            dst.SetTextureOffset("_MainTex", src.mainTextureOffset);
            if (dst.HasProperty("_BaseMap"))
            {
                dst.SetTextureScale("_BaseMap", src.mainTextureScale);
                dst.SetTextureOffset("_BaseMap", src.mainTextureOffset);
            }

            // 复制剖切平面参数
            if (src.HasProperty("_EngineClipPlane") && dst.HasProperty("_EngineClipPlane"))
            {
                dst.SetVector("_EngineClipPlane", src.GetVector("_EngineClipPlane"));
            }
            if (src.HasProperty("_EngineClipEnabled") && dst.HasProperty("_EngineClipEnabled"))
            {
                dst.SetFloat("_EngineClipEnabled", src.GetFloat("_EngineClipEnabled"));
            }
        }

        void OnDestroy()
        {
            // 清理创建的全息材质
            foreach (var mat in hologramMap.Values)
            {
                if (mat != null) Destroy(mat);
            }
            hologramMap.Clear();
            originalMaterials.Clear();
        }
    }
}
