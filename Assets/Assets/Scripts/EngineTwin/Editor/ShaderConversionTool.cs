using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace EngineTwin
{
    /// <summary>
    /// Shader 转换工具：将 URP 2D Shader 转换为 URP 3D Shader
    /// </summary>
    public static class ShaderConversionTool
    {
        [MenuItem("EngineTwin/Convert 2D Shaders to 3D", false, 210)]
        public static void Convert2DTo3D()
        {
            int convertedCount = 0;
            int totalMaterials = 0;

            // 查找场景中所有 Renderer
            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();

            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null || mat.shader == null) continue;

                    totalMaterials++;
                    string shaderName = mat.shader.name;

                    // 检查是否是 2D Shader
                    if (Is2DShader(shaderName))
                    {
                        Shader newShader = Get3DShader(shaderName);
                        if (newShader != null)
                        {
                            // 保存旧属性
                            Color baseColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                            if (mat.HasProperty("_BaseColor"))
                                baseColor = mat.GetColor("_BaseColor");

                            Texture mainTex = null;
                            if (mat.HasProperty("_MainTex"))
                                mainTex = mat.GetTexture("_MainTex");
                            if (mat.HasProperty("_BaseMap"))
                                mainTex = mat.GetTexture("_BaseMap");

                            // 应用新 Shader
                            mat.shader = newShader;

                            // 恢复属性
                            if (mat.HasProperty("_BaseColor"))
                                mat.SetColor("_BaseColor", baseColor);
                            if (mat.HasProperty("_BaseMap") && mainTex != null)
                                mat.SetTexture("_BaseMap", mainTex);

                            // 设置默认的 3D 材质属性
                            if (mat.HasProperty("_Metallic"))
                                mat.SetFloat("_Metallic", 0.5f);
                            if (mat.HasProperty("_Smoothness"))
                                mat.SetFloat("_Smoothness", 0.5f);

                            convertedCount++;
                            Debug.Log($"[Shader Convert] {mat.name}: {shaderName} → {newShader.name} (on {renderer.name})");
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Shader 转换完成",
                $"检查了 {totalMaterials} 个材质\n" +
                $"转换了 {convertedCount} 个 2D Shader 到 3D\n\n" +
                $"请检查场景效果", "确定");

            Debug.Log($"[Shader Convert] 完成！转换: {convertedCount}/{totalMaterials}");
        }

        static bool Is2DShader(string shaderName)
        {
            return shaderName.Contains("2D") ||
                   shaderName.Contains("Sprite") ||
                   shaderName.Contains("UI/") ||
                   shaderName.Contains("Universal Render Pipeline/2D");
        }

        static Shader Get3DShader(string oldShaderName)
        {
            // 映射到合适的 3D Shader
            if (oldShaderName.Contains("Lit"))
            {
                return Shader.Find("Universal Render Pipeline/Lit");
            }
            else if (oldShaderName.Contains("Unlit"))
            {
                return Shader.Find("Universal Render Pipeline/Unlit");
            }
            else if (oldShaderName.Contains("Sprite"))
            {
                return Shader.Find("Universal Render Pipeline/Lit");
            }
            else
            {
                // 默认使用 Lit
                return Shader.Find("Universal Render Pipeline/Lit");
            }
        }

        [MenuItem("EngineTwin/Set All Materials to URP Lit", false, 211)]
        public static void SetAllToURPLit()
        {
            if (!EditorUtility.DisplayDialog("设置为 URP Lit",
                "这将把场景中所有材质的 Shader 设置为 URP/Lit (标准3D光照)\n\n" +
                "建议先备份！\n\n是否继续？", "继续", "取消"))
            {
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                EditorUtility.DisplayDialog("错误", "找不到 URP Lit Shader！\n请确保已安装 URP。", "确定");
                return;
            }

            int convertedCount = 0;
            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();

            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null) continue;

                    // 保存属性
                    Color color = Color.white;
                    if (mat.HasProperty("_Color")) color = mat.GetColor("_Color");
                    else if (mat.HasProperty("_BaseColor")) color = mat.GetColor("_BaseColor");

                    Texture mainTex = null;
                    if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                    else if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");

                    // 设置 Shader
                    mat.shader = urpLit;

                    // 恢复属性
                    mat.SetColor("_BaseColor", color);
                    if (mainTex != null)
                        mat.SetTexture("_BaseMap", mainTex);

                    // 设置默认物理属性
                    mat.SetFloat("_Metallic", 0.5f);
                    mat.SetFloat("_Smoothness", 0.5f);

                    convertedCount++;
                    Debug.Log($"[Set URP Lit] {mat.name} on {renderer.name}");
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("设置完成",
                $"已将 {convertedCount} 个材质设置为 URP Lit", "确定");

            Debug.Log($"[Set URP Lit] 完成！转换了 {convertedCount} 个材质");
        }

        [MenuItem("EngineTwin/Show Material Report", false, 212)]
        public static void ShowMaterialReport()
        {
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("=== 场景材质报告 ===\n");

            System.Collections.Generic.Dictionary<string, int> shaderCount =
                new System.Collections.Generic.Dictionary<string, int>();

            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
            int totalMaterials = 0;
            int nullShaders = 0;
            int unsupportedShaders = 0;

            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null) continue;

                    totalMaterials++;

                    if (mat.shader == null)
                    {
                        nullShaders++;
                        continue;
                    }

                    if (!mat.shader.isSupported)
                    {
                        unsupportedShaders++;
                    }

                    string shaderName = mat.shader.name;
                    if (!shaderCount.ContainsKey(shaderName))
                        shaderCount[shaderName] = 0;
                    shaderCount[shaderName]++;
                }
            }

            report.AppendLine($"总材质数: {totalMaterials}");
            report.AppendLine($"渲染器数: {renderers.Length}");
            if (nullShaders > 0)
                report.AppendLine($"⚠ 空 Shader: {nullShaders}");
            if (unsupportedShaders > 0)
                report.AppendLine($"❌ 不支持的 Shader: {unsupportedShaders}");
            report.AppendLine("\n--- Shader 使用情况 ---\n");

            var sortedShaders = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(shaderCount);
            sortedShaders.Sort((a, b) => b.Value.CompareTo(a.Value));

            foreach (var kvp in sortedShaders)
            {
                string icon = kvp.Key.Contains("2D") || kvp.Key.Contains("Sprite") ? "❌ [2D]" : "✓";
                report.AppendLine($"{icon} {kvp.Key}: {kvp.Value}");
            }

            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("材质报告", report.ToString(), "确定");
        }
    }
}
#endif
