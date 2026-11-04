using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace EngineTwin
{
    /// <summary>
    /// URP 自动配置工具：一键设置 URP 渲染管线和全息效果资源
    /// </summary>
    public static class URPSetupTool
    {
        [MenuItem("EngineTwin/Setup URP Pipeline", false, 100)]
        public static void SetupURPPipeline()
        {
            Debug.Log("[URP Setup] 开始配置 URP 渲染管线...");

            // 1. 查找或创建 URP 资源
            UniversalRenderPipelineAsset urpAsset = FindOrCreateURPAsset();

            if (urpAsset == null)
            {
                Debug.LogError("[URP Setup] 无法创建 URP 资源！");
                return;
            }

            // 2. 设置为当前渲染管线
            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;

            Debug.Log($"[URP Setup] 已设置 URP 渲染管线: {AssetDatabase.GetAssetPath(urpAsset)}");

            // 3. 创建全息材质
            CreateHolographicMaterial();

            // 4. 保存设置
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[URP Setup] URP 配置完成！");
            EditorUtility.DisplayDialog("URP 配置",
                "URP 渲染管线配置完成！\n\n" +
                "已创建：\n" +
                "- URP 渲染管线资源\n" +
                "- 全息效果材质\n\n" +
                "现在可以启用全息模式了！", "确定");
        }

        static UniversalRenderPipelineAsset FindOrCreateURPAsset()
        {
            // 查找现有的 URP 资源
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                Debug.Log($"[URP Setup] 找到现有 URP 资源: {path}");
                return asset;
            }

            // 创建新的 URP 资源
            Debug.Log("[URP Setup] 创建新的 URP 资源...");

            // 创建 Renderer
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "EngineTwin_ForwardRenderer";

            // 配置 Renderer
            rendererData.postProcessData = null; // 使用默认

            string rendererPath = "Assets/EngineTwin/Settings/EngineTwin_ForwardRenderer.asset";
            EnsureDirectory(rendererPath);
            AssetDatabase.CreateAsset(rendererData, rendererPath);

            // 创建 URP Asset
            var urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();

            // 使用反射设置 Renderer（因为 API 可能变化）
            var rendererListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (rendererListField != null)
            {
                var rendererList = new ScriptableRendererData[] { rendererData };
                rendererListField.SetValue(urpAsset, rendererList);
            }

            string urpAssetPath = "Assets/EngineTwin/Settings/EngineTwin_URP.asset";
            EnsureDirectory(urpAssetPath);
            AssetDatabase.CreateAsset(urpAsset, urpAssetPath);

            Debug.Log($"[URP Setup] 已创建 URP 资源: {urpAssetPath}");

            return urpAsset;
        }

        static void CreateHolographicMaterial()
        {
            string shaderPath = "Assets/EngineTwin/Shaders/HolographicEngine.shader";

            // 检查 Shader 是否存在
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"[URP Setup] 未找到全息 Shader: {shaderPath}");
                return;
            }

            // 创建材质
            Material mat = new Material(shader);
            mat.name = "HolographicEngine";

            // 设置默认参数
            mat.SetColor("_BaseColor", new Color(0.2f, 0.6f, 1f, 1f));
            mat.SetColor("_EdgeColor", new Color(0f, 1f, 1f, 1f));
            mat.SetFloat("_FresnelPower", 3f);
            mat.SetFloat("_Alpha", 0.5f);
            mat.SetFloat("_ScanSpeed", 1f);
            mat.SetFloat("_ScanWidth", 0.1f);
            mat.SetColor("_ScanColor", new Color(0f, 1f, 1f, 1f));
            mat.SetFloat("_FlowSpeed", 1f);
            mat.SetFloat("_FlowIntensity", 0.5f);
            mat.SetFloat("_GridSize", 0.1f);
            mat.SetFloat("_GridThickness", 0.02f);
            mat.SetColor("_GridColor", new Color(0.2f, 0.8f, 1f, 0.3f));

            string matPath = "Assets/EngineTwin/Materials/HolographicEngine.mat";
            EnsureDirectory(matPath);
            AssetDatabase.CreateAsset(mat, matPath);

            Debug.Log($"[URP Setup] 已创建全息材质: {matPath}");
        }

        static void EnsureDirectory(string filePath)
        {
            string directory = System.IO.Path.GetDirectoryName(filePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
        }

        [MenuItem("EngineTwin/Setup URP Pipeline", true)]
        static bool ValidateSetupURPPipeline()
        {
            return true; // 总是可用
        }
    }
}
#endif
