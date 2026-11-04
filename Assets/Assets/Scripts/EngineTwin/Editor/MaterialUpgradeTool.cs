using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;

namespace EngineTwin
{
    /// <summary>
    /// 材质升级工具：将 Built-in 材质转换为 URP 材质
    /// </summary>
    public static class MaterialUpgradeTool
    {
        [MenuItem("EngineTwin/Upgrade Materials to URP", false, 101)]
        public static void UpgradeMaterialsToURP()
        {
            if (!EditorUtility.DisplayDialog("升级材质到 URP",
                "此操作将把场景中所有 Built-in 材质升级到 URP。\n\n" +
                "建议先备份项目！\n\n" +
                "是否继续？", "继续", "取消"))
            {
                return;
            }

            int upgradedCount = 0;
            int skippedCount = 0;

            // 查找所有材质
            string[] materialGUIDs = AssetDatabase.FindAssets("t:Material");

            foreach (string guid in materialGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat == null) continue;

                // 检查是否需要升级
                if (NeedUpgrade(mat))
                {
                    UpgradeMaterial(mat);
                    upgradedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Material Upgrade] 完成！升级: {upgradedCount}, 跳过: {skippedCount}");

            EditorUtility.DisplayDialog("升级完成",
                $"材质升级完成！\n\n" +
                $"升级: {upgradedCount} 个\n" +
                $"跳过: {skippedCount} 个\n\n" +
                $"请检查场景效果", "确定");
        }

        static bool NeedUpgrade(Material mat)
        {
            if (mat.shader == null) return false;

            string shaderName = mat.shader.name;

            // 需要升级的 Built-in Shader
            return shaderName.StartsWith("Standard") ||
                   shaderName.StartsWith("Legacy Shaders/") ||
                   shaderName.StartsWith("Mobile/") ||
                   shaderName.Contains("Specular") ||
                   shaderName.Contains("Diffuse") ||
                   shaderName.Contains("Bumped");
        }

        static void UpgradeMaterial(Material mat)
        {
            string oldShaderName = mat.shader.name;
            Shader newShader = null;

            // 映射到 URP Shader
            if (oldShaderName.Contains("Standard"))
            {
                newShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            else if (oldShaderName.Contains("Transparent"))
            {
                newShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            else if (oldShaderName.Contains("Particles"))
            {
                newShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }
            else
            {
                newShader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (newShader != null)
            {
                // 保存旧属性
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;

                // 设置新 Shader
                mat.shader = newShader;

                // 恢复属性
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_BaseMap") && mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

                Debug.Log($"[Material Upgrade] {mat.name}: {oldShaderName} → {newShader.name}");
            }
        }

        [MenuItem("EngineTwin/Fix Pink Materials", false, 102)]
        public static void FixPinkMaterials()
        {
            int fixedCount = 0;

            // 查找场景中的所有 Renderer
            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();

            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && mat.shader != null && !mat.shader.isSupported)
                    {
                        // Shader 不支持，替换为 URP Lit
                        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                        if (urpLit != null)
                        {
                            mat.shader = urpLit;
                            fixedCount++;
                            Debug.Log($"[Fix Pink] 修复材质: {mat.name} on {renderer.name}");
                        }
                    }
                }
            }

            Debug.Log($"[Fix Pink] 修复了 {fixedCount} 个粉红色材质");

            if (fixedCount > 0)
            {
                EditorUtility.DisplayDialog("修复完成",
                    $"修复了 {fixedCount} 个粉红色材质！", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("检查完成",
                    "没有发现需要修复的材质", "确定");
            }
        }

        [MenuItem("EngineTwin/Create Default URP Materials", false, 103)]
        public static void CreateDefaultURPMaterials()
        {
            string materialsPath = "Assets/EngineTwin/Materials/";

            if (!System.IO.Directory.Exists(materialsPath))
            {
                System.IO.Directory.CreateDirectory(materialsPath);
            }

            // 创建基础材质
            CreateURPMaterial("Metal_Steel", new Color(0.7f, 0.7f, 0.75f), 0.9f, 0.5f);
            CreateURPMaterial("Metal_Aluminum", new Color(0.85f, 0.85f, 0.87f), 0.85f, 0.6f);
            CreateURPMaterial("Plastic_Black", new Color(0.1f, 0.1f, 0.1f), 0.1f, 0.4f);
            CreateURPMaterial("Rubber", new Color(0.2f, 0.2f, 0.2f), 0f, 0.3f);
            CreateURPMaterial("Paint_Red", new Color(0.8f, 0.1f, 0.1f), 0.2f, 0.7f);
            CreateURPMaterial("Paint_Blue", new Color(0.1f, 0.3f, 0.8f), 0.2f, 0.7f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Create Materials] 已创建 6 个默认 URP 材质");
            EditorUtility.DisplayDialog("创建完成",
                "已创建以下材质：\n\n" +
                "- Metal_Steel\n" +
                "- Metal_Aluminum\n" +
                "- Plastic_Black\n" +
                "- Rubber\n" +
                "- Paint_Red\n" +
                "- Paint_Blue\n\n" +
                "路径: Assets/EngineTwin/Materials/", "确定");
        }

        static void CreateURPMaterial(string name, Color baseColor, float metallic, float smoothness)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("找不到 URP Lit Shader！");
                return;
            }

            Material mat = new Material(urpLit);
            mat.name = name;
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            string path = $"Assets/EngineTwin/Materials/{name}.mat";
            AssetDatabase.CreateAsset(mat, path);

            Debug.Log($"[Create Material] {name} → {path}");
        }
    }
}
#endif
