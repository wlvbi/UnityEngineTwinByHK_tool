using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace EngineTwin
{
    /// <summary>
    /// Shader 编译检查工具
    /// </summary>
    public static class ShaderCompileChecker
    {
        [MenuItem("EngineTwin/Check Shader Compilation", false, 10)]
        public static void CheckShaderCompilation()
        {
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("=== Shader 编译检查 ===\n");

            // 检查剪裁 Shader
            Shader clipShader = Shader.Find("EngineTwin/StandardClip");
            report.AppendLine("【剪裁 Shader】");
            if (clipShader == null)
            {
                report.AppendLine("❌ 未找到 EngineTwin/StandardClip");
                report.AppendLine("   路径: Assets/EngineTwin/Shaders/StandardClip.shader");
            }
            else
            {
                report.AppendLine($"✓ 找到: {clipShader.name}");
                report.AppendLine($"  支持: {(clipShader.isSupported ? "✓" : "❌")}");

                if (!clipShader.isSupported)
                {
                    report.AppendLine("  ❌ Shader 不被支持，可能有编译错误");
                    report.AppendLine("  请查看 Console 中的 Shader 错误信息");
                }
            }
            report.AppendLine();

            // 检查全息 Shader
            Shader holoShader = Shader.Find("EngineTwin/HolographicEngine");
            report.AppendLine("【全息 Shader】");
            if (holoShader == null)
            {
                report.AppendLine("⚠ 未找到 EngineTwin/HolographicEngine");
            }
            else
            {
                report.AppendLine($"✓ 找到: {holoShader.name}");
                report.AppendLine($"  支持: {(holoShader.isSupported ? "✓" : "❌")}");
            }
            report.AppendLine();

            // 检查 URP Lit
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            report.AppendLine("【URP Lit Shader】");
            if (urpLit == null)
            {
                report.AppendLine("❌ 未找到 Universal Render Pipeline/Lit");
                report.AppendLine("  请确保已安装 URP 包");
            }
            else
            {
                report.AppendLine($"✓ 找到: {urpLit.name}");
                report.AppendLine($"  支持: {(urpLit.isSupported ? "✓" : "❌")}");
            }
            report.AppendLine();

            // 检查场景中使用剪裁 Shader 的材质
            int clipMaterialCount = 0;
            int unsupportedClipMat = 0;

            Material[] allMaterials = Resources.FindObjectsOfTypeAll<Material>();
            foreach (var mat in allMaterials)
            {
                if (mat.shader != null && mat.shader.name == "EngineTwin/StandardClip")
                {
                    clipMaterialCount++;
                    if (!mat.shader.isSupported)
                    {
                        unsupportedClipMat++;
                    }
                }
            }

            report.AppendLine($"【使用剪裁 Shader 的材质】");
            report.AppendLine($"总数: {clipMaterialCount}");
            if (unsupportedClipMat > 0)
            {
                report.AppendLine($"❌ 不支持: {unsupportedClipMat}");
            }

            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("Shader 编译检查", report.ToString(), "确定");
        }

        [MenuItem("EngineTwin/Force Reimport All Shaders", false, 11)]
        public static void ForceReimportShaders()
        {
            if (!EditorUtility.DisplayDialog("重新导入 Shader",
                "将强制重新导入所有 Shader 文件\n\n是否继续？",
                "继续", "取消"))
            {
                return;
            }

            // 查找所有 Shader 文件
            string[] shaderGUIDs = AssetDatabase.FindAssets("t:Shader");
            int count = 0;

            foreach (string guid in shaderGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
                Debug.Log($"[Reimport] {path}");
            }

            AssetDatabase.Refresh();

            Debug.Log($"[Reimport] 重新导入了 {count} 个 Shader");
            EditorUtility.DisplayDialog("完成",
                $"已重新导入 {count} 个 Shader 文件\n\n" +
                "请查看 Console 是否有编译错误。",
                "确定");
        }
    }
}
#endif
