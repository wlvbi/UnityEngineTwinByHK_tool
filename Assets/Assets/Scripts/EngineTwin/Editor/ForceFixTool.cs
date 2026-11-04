using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace EngineTwin
{
    /// <summary>
    /// 强力修复工具：解决粉红色材质问题
    /// </summary>
    public static class ForceFixTool
    {
        [MenuItem("EngineTwin/FORCE FIX PINK MATERIALS NOW", false, 1)]
        public static void ForceFixPinkMaterialsNow()
        {
            Debug.Log("=== 开始强力修复粉红色材质 ===");

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                EditorUtility.DisplayDialog("错误",
                    "找不到 URP Lit Shader！\n\n请确保已安装 Universal RP 包。", "确定");
                return;
            }

            int fixedCount = 0;
            int totalCount = 0;

            // 方法1：修复场景中的材质
            Renderer[] renderers = Object.FindObjectsOfType<Renderer>(true); // 包括未激活的
            Debug.Log($"[Force Fix] 找到 {renderers.Length} 个渲染器");

            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0)
                    continue;

                Material[] newMaterials = new Material[renderer.sharedMaterials.Length];
                bool needsUpdate = false;

                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    Material mat = renderer.sharedMaterials[i];
                    totalCount++;

                    if (mat == null)
                    {
                        // 创建新材质
                        mat = new Material(urpLit);
                        mat.name = $"Auto_Material_{renderer.name}_{i}";
                        mat.SetColor("_BaseColor", Color.gray);
                        mat.SetFloat("_Metallic", 0.5f);
                        mat.SetFloat("_Smoothness", 0.5f);
                        newMaterials[i] = mat;
                        needsUpdate = true;
                        fixedCount++;
                        Debug.Log($"[Force Fix] 创建材质: {renderer.name}");
                    }
                    else if (mat.shader == null || !mat.shader.isSupported)
                    {
                        // Shader 不支持，替换
                        Color oldColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                        if (mat.HasProperty("_BaseColor")) oldColor = mat.GetColor("_BaseColor");

                        mat.shader = urpLit;
                        mat.SetColor("_BaseColor", oldColor);
                        mat.SetFloat("_Metallic", 0.5f);
                        mat.SetFloat("_Smoothness", 0.5f);

                        newMaterials[i] = mat;
                        needsUpdate = true;
                        fixedCount++;
                        Debug.Log($"[Force Fix] 修复 Shader: {mat.name} on {renderer.name}");
                    }
                    else
                    {
                        newMaterials[i] = mat;
                    }
                }

                if (needsUpdate)
                {
                    renderer.sharedMaterials = newMaterials;
                }
            }

            // 方法2：修复项目中的材质资源
            string[] materialGUIDs = AssetDatabase.FindAssets("t:Material");
            Debug.Log($"[Force Fix] 找到 {materialGUIDs.Length} 个材质资源");

            foreach (string guid in materialGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat != null && (mat.shader == null || !mat.shader.isSupported))
                {
                    Color oldColor = Color.white;
                    if (mat.HasProperty("_Color")) oldColor = mat.GetColor("_Color");
                    if (mat.HasProperty("_BaseColor")) oldColor = mat.GetColor("_BaseColor");

                    mat.shader = urpLit;
                    mat.SetColor("_BaseColor", oldColor);
                    mat.SetFloat("_Metallic", 0.5f);
                    mat.SetFloat("_Smoothness", 0.5f);

                    EditorUtility.SetDirty(mat);
                    Debug.Log($"[Force Fix] 修复资源材质: {path}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("强力修复完成",
                $"检查了 {totalCount} 个材质\n" +
                $"修复了 {fixedCount} 个粉红色材质\n\n" +
                $"现在运行场景查看效果！",
                "确定");

            Debug.Log($"=== 强力修复完成：{fixedCount}/{totalCount} ===");
        }

        [MenuItem("EngineTwin/Apply Gray Material to All", false, 2)]
        public static void ApplyGrayMaterialToAll()
        {
            if (!EditorUtility.DisplayDialog("应用灰色材质",
                "将为所有对象应用统一的灰色 URP 材质\n\n" +
                "这会覆盖所有现有材质！\n\n是否继续？",
                "继续", "取消"))
            {
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                EditorUtility.DisplayDialog("错误", "找不到 URP Lit Shader！", "确定");
                return;
            }

            // 创建一个灰色材质
            Material grayMat = new Material(urpLit);
            grayMat.name = "Unified_Gray";
            grayMat.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.5f));
            grayMat.SetFloat("_Metallic", 0.6f);
            grayMat.SetFloat("_Smoothness", 0.5f);

            Renderer[] renderers = Object.FindObjectsOfType<Renderer>(true);
            int count = 0;

            foreach (var renderer in renderers)
            {
                Material[] mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = grayMat;
                }
                renderer.sharedMaterials = mats;
                count++;
            }

            Debug.Log($"[Gray Material] 应用到 {count} 个对象");
            EditorUtility.DisplayDialog("完成", $"已应用灰色材质到 {count} 个对象", "确定");
        }

        [MenuItem("EngineTwin/Debug Material Info", false, 3)]
        public static void DebugMaterialInfo()
        {
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("=== 材质调试信息 ===\n");

            // 检查 URP Shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            report.AppendLine($"URP Lit Shader: {(urpLit != null ? "✓ 存在" : "✗ 未找到")}");
            if (urpLit != null)
            {
                report.AppendLine($"  支持: {urpLit.isSupported}");
            }
            report.AppendLine();

            // 检查场景中的材质
            Renderer[] renderers = Object.FindObjectsOfType<Renderer>(true);
            report.AppendLine($"场景渲染器数量: {renderers.Length}\n");

            int nullMat = 0, nullShader = 0, unsupported = 0, ok = 0;

            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null)
                    {
                        nullMat++;
                        report.AppendLine($"✗ {renderer.name}: 材质为 null");
                    }
                    else if (mat.shader == null)
                    {
                        nullShader++;
                        report.AppendLine($"✗ {renderer.name}: Shader 为 null ({mat.name})");
                    }
                    else if (!mat.shader.isSupported)
                    {
                        unsupported++;
                        report.AppendLine($"❌ {renderer.name}: Shader 不支持 ({mat.shader.name})");
                    }
                    else
                    {
                        ok++;
                    }
                }
            }

            report.AppendLine($"\n=== 统计 ===");
            report.AppendLine($"✓ 正常: {ok}");
            report.AppendLine($"✗ 空材质: {nullMat}");
            report.AppendLine($"✗ 空Shader: {nullShader}");
            report.AppendLine($"❌ 不支持的Shader: {unsupported}");

            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("调试信息", report.ToString(), "确定");
        }
    }
}
#endif
