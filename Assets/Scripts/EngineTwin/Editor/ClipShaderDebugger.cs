using UnityEngine;
using UnityEditor;
using System.Text;

namespace EngineTwin.EditorTools
{
    public class ClipShaderDebugger : EditorWindow
    {
        [MenuItem("EngineTwin/Debug/剖切材质对比")]
        static void ShowWindow()
        {
            GetWindow<ClipShaderDebugger>("剖切材质对比");
        }

        Vector2 scrollPos;

        void OnGUI()
        {
            EditorGUILayout.HelpBox("对比运行前后的材质差异", MessageType.Info);

            if (GUILayout.Button("🔍 分析当前场景材质"))
            {
                AnalyzeMaterials();
            }

            EditorGUILayout.Space(10);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.TextArea(debugLog, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        string debugLog = "点击按钮开始分析...";

        void AnalyzeMaterials()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 材质分析报告 ===\n");

            // 查找所有 Renderer
            var renderers = FindObjectsOfType<Renderer>();
            sb.AppendLine($"场景中共有 {renderers.Length} 个 Renderer\n");

            int urpLitCount = 0;
            int clipShaderCount = 0;
            int otherCount = 0;

            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;

                    string shaderName = mat.shader.name;

                    if (shaderName.Contains("Lit") && !shaderName.Contains("Clip"))
                    {
                        urpLitCount++;
                        sb.AppendLine($"✅ URP Lit: {mat.name}");
                        sb.AppendLine($"   Shader: {shaderName}");
                        LogMaterialProperties(mat, sb);
                    }
                    else if (shaderName.Contains("Clip"))
                    {
                        clipShaderCount++;
                        sb.AppendLine($"🔪 Clip Shader: {mat.name}");
                        sb.AppendLine($"   Shader: {shaderName}");
                        LogMaterialProperties(mat, sb);
                    }
                    else
                    {
                        otherCount++;
                    }
                }
            }

            sb.AppendLine($"\n=== 统计 ===");
            sb.AppendLine($"URP Lit 材质: {urpLitCount}");
            sb.AppendLine($"Clip Shader 材质: {clipShaderCount}");
            sb.AppendLine($"其他材质: {otherCount}");

            // 检查 Shader 是否存在
            sb.AppendLine($"\n=== Shader 检查 ===");
            CheckShader("EngineTwin/URPLitWithClip", sb);
            CheckShader("EngineTwin/ClipShaderFinal", sb);
            CheckShader("EngineTwin/StandardClip", sb);

            debugLog = sb.ToString();
        }

        void LogMaterialProperties(Material mat, StringBuilder sb)
        {
            if (mat.HasProperty("_BaseColor"))
                sb.AppendLine($"   颜色: {mat.GetColor("_BaseColor")}");
            if (mat.HasProperty("_Metallic"))
                sb.AppendLine($"   金属度: {mat.GetFloat("_Metallic")}");
            if (mat.HasProperty("_Smoothness"))
                sb.AppendLine($"   光滑度: {mat.GetFloat("_Smoothness")}");
            if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)
                sb.AppendLine($"   纹理: {mat.GetTexture("_BaseMap").name}");
            sb.AppendLine();
        }

        void CheckShader(string shaderName, StringBuilder sb)
        {
            var shader = Shader.Find(shaderName);
            if (shader != null)
                sb.AppendLine($"✅ {shaderName} - 存在");
            else
                sb.AppendLine($"❌ {shaderName} - 未找到");
        }
    }
}
