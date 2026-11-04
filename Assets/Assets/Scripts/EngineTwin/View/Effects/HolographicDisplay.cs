using UnityEngine;
using System.Collections.Generic;
using QFramework;

namespace EngineTwin
{
    /// <summary>
    /// 全息显示系统：为发动机添加科技感的半透明流光效果
    /// </summary>
    public class HolographicDisplay : MonoBehaviour
    {
        [Header("全息材质")]
        public Material holographicMaterial;

        [Header("效果强度")]
        [Range(0, 1)] public float intensity = 1f;
        [Range(0, 1)] public float alpha = 0.5f;

        [Header("扫描线")]
        public bool enableScanLine = true;
        public float scanSpeed = 1f;
        public Color scanColor = new Color(0, 1, 1, 1);

        [Header("边缘光")]
        public Color edgeColor = new Color(0, 1, 1, 1);
        [Range(0.1f, 10f)] public float fresnelPower = 3f;

        [Header("流光")]
        public bool enableFlow = true;
        public float flowSpeed = 1f;
        [Range(0, 1)] public float flowIntensity = 0.5f;

        [Header("网格")]
        public bool enableGrid = true;
        public float gridSize = 0.1f;
        public Color gridColor = new Color(0.2f, 0.8f, 1f, 0.3f);

        Material holographicInstance;
        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        readonly List<Renderer> holographicRenderers = new List<Renderer>();

        bool isHolographicMode;
        Transform engineRoot;

        public void Init(Transform root)
        {
            engineRoot = root;

            // 创建材质实例
            if (holographicMaterial == null)
            {
                holographicMaterial = new Material(Shader.Find("EngineTwin/HolographicEngine"));
            }
            holographicInstance = new Material(holographicMaterial);

            // 生成科技感纹理
            Texture2D flowTexture = TechTextureGenerator.GenerateFlowTexture(256);
            holographicInstance.SetTexture("_FlowMap", flowTexture);

            // 收集所有渲染器
            CollectRenderers();
        }

        void CollectRenderers()
        {
            if (engineRoot == null) return;

            holographicRenderers.Clear();
            var renderers = engineRoot.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterials.Length == 0) continue;

                // 跳过粒子系统渲染器
                if (renderer is ParticleSystemRenderer) continue;

                // 跳过 UI 相关的渲染器
                if (renderer.GetComponent<UnityEngine.UI.Graphic>() != null) continue;
                if (renderer.transform.GetComponentInParent<Canvas>() != null) continue;

                // 跳过特效对象（以 FX_ 开头）
                if (renderer.name.StartsWith("FX_")) continue;

                // 跳过 Tip 标签对象及其所有子节点（父节点名称以 Tip_ 开头）
                if (IsUnderTipNode(renderer.transform)) continue;

                // 保存原始材质
                if (!originalMaterials.ContainsKey(renderer))
                {
                    originalMaterials[renderer] = renderer.sharedMaterials;
                }
                holographicRenderers.Add(renderer);
            }

            Debug.Log($"[HolographicDisplay] 收集到 {holographicRenderers.Count} 个模型渲染器（已过滤粒子、UI 和 Tip 标签树）");
        }

        /// <summary>检查节点是否在 Tip_ 开头的父节点下</summary>
        bool IsUnderTipNode(Transform t)
        {
            while (t != null)
            {
                if (t.name.StartsWith("Tip_")) return true;
                t = t.parent;
            }
            return false;
        }

        /// <summary>切换全息模式</summary>
        public void ToggleHolographicMode()
        {
            SetHolographicMode(!isHolographicMode);
        }

        /// <summary>设置全息模式</summary>
        public void SetHolographicMode(bool enable)
        {
            if (isHolographicMode == enable) return;
            isHolographicMode = enable;

            if (enable)
            {
                ApplyHolographicMaterial();
            }
            else
            {
                RestoreOriginalMaterials();
            }

            Debug.Log($"[HolographicDisplay] 全息模式: {(enable ? "开启" : "关闭")}");
        }

        void ApplyHolographicMaterial()
        {
            if (holographicInstance == null) return;

            UpdateMaterialProperties();

            foreach (var renderer in holographicRenderers)
            {
                if (renderer == null) continue;

                // 替换为全息材质
                Material[] mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = holographicInstance;
                }
                renderer.sharedMaterials = mats;
            }
        }

        void RestoreOriginalMaterials()
        {
            foreach (var kvp in originalMaterials)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.sharedMaterials = kvp.Value;
                }
            }
        }

        void UpdateMaterialProperties()
        {
            if (holographicInstance == null) return;

            holographicInstance.SetFloat("_Alpha", alpha * intensity);
            holographicInstance.SetColor("_EdgeColor", edgeColor);
            holographicInstance.SetFloat("_FresnelPower", fresnelPower);

            holographicInstance.SetFloat("_ScanSpeed", enableScanLine ? scanSpeed : 0);
            holographicInstance.SetColor("_ScanColor", scanColor);

            holographicInstance.SetFloat("_FlowSpeed", enableFlow ? flowSpeed : 0);
            holographicInstance.SetFloat("_FlowIntensity", flowIntensity);

            holographicInstance.SetFloat("_GridSize", gridSize);
            holographicInstance.SetColor("_GridColor", enableGrid ? gridColor : Color.clear);
        }

        void Update()
        {
            if (isHolographicMode && holographicInstance != null)
            {
                UpdateMaterialProperties();
            }
        }

        void OnDestroy()
        {
            if (holographicInstance != null)
            {
                Destroy(holographicInstance);
            }
        }
    }
}
