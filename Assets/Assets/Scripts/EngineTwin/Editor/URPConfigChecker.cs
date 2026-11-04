using UnityEngine;
using UnityEngine.Rendering;

namespace EngineTwin
{
    /// <summary>
    /// URP 配置诊断工具：检查 URP 配置状态并提供修复建议
    /// </summary>
    public class URPConfigChecker : MonoBehaviour
    {
        [Header("诊断信息")]
        [TextArea(5, 10)]
        public string diagnosticInfo = "点击 [检查配置] 按钮";

        void Start()
        {
            CheckConfiguration();
        }

        public void CheckConfiguration()
        {
            System.Text.StringBuilder info = new System.Text.StringBuilder();
            info.AppendLine("=== URP 配置诊断 ===\n");

            bool allGood = true;

            // 检查 1：渲染管线
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline != null)
            {
                info.AppendLine($"✓ 渲染管线: {pipeline.GetType().Name}");
                info.AppendLine($"  路径: {pipeline.name}\n");
            }
            else
            {
                info.AppendLine("✗ 渲染管线未设置！");
                info.AppendLine("  解决: Edit → Project Settings → Graphics\n");
                allGood = false;
            }

            // 检查 2：Quality 设置
            var qualityPipeline = QualitySettings.renderPipeline;
            if (qualityPipeline != null)
            {
                info.AppendLine($"✓ Quality 渲染管线: {qualityPipeline.GetType().Name}\n");
            }
            else
            {
                info.AppendLine("⚠ Quality 渲染管线未设置（使用全局设置）\n");
            }

            // 检查 3：全息 Shader
            Shader holographicShader = Shader.Find("EngineTwin/HolographicEngine");
            if (holographicShader != null)
            {
                info.AppendLine("✓ 全息 Shader 已找到");

                if (holographicShader.isSupported)
                {
                    info.AppendLine("  状态: 支持当前平台\n");
                }
                else
                {
                    info.AppendLine("  状态: 不支持当前平台！");
                    info.AppendLine("  解决: 检查 Shader 编译错误\n");
                    allGood = false;
                }
            }
            else
            {
                info.AppendLine("✗ 全息 Shader 未找到！");
                info.AppendLine("  预期路径: Assets/EngineTwin/Shaders/HolographicEngine.shader");
                info.AppendLine("  解决: 确保 Shader 文件存在\n");
                allGood = false;
            }

            // 检查 4：HolographicDisplay 组件
            var holographic = FindObjectOfType<HolographicDisplay>();
            if (holographic != null)
            {
                info.AppendLine("✓ HolographicDisplay 组件已找到");

                if (holographic.holographicMaterial != null)
                {
                    info.AppendLine($"  材质: {holographic.holographicMaterial.name}");
                    info.AppendLine($"  Shader: {holographic.holographicMaterial.shader.name}\n");
                }
                else
                {
                    info.AppendLine("  ⚠ 材质未赋值");
                    info.AppendLine("  解决: Inspector 中设置 Holographic Material\n");
                }
            }
            else
            {
                info.AppendLine("⚠ HolographicDisplay 组件未找到");
                info.AppendLine("  场景可能未完全初始化\n");
            }

            // 检查 5：相机设置
            var cameras = FindObjectsOfType<Camera>();
            if (cameras.Length > 0)
            {
                info.AppendLine($"✓ 找到 {cameras.Length} 个相机");
                foreach (var cam in cameras)
                {
                    info.AppendLine($"  - {cam.name}");
                }
                info.AppendLine();
            }

            // 总结
            info.AppendLine("=================");
            if (allGood)
            {
                info.AppendLine("✓ 配置完整，可以使用全息效果！");
            }
            else
            {
                info.AppendLine("✗ 发现配置问题，请根据提示修复");
            }

            diagnosticInfo = info.ToString();
            Debug.Log(diagnosticInfo);
        }
    }
}

#if UNITY_EDITOR
namespace EngineTwin.EditorTools
{
    using UnityEditor;

    [CustomEditor(typeof(URPConfigChecker))]
    public class URPConfigCheckerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            URPConfigChecker checker = (URPConfigChecker)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("检查配置", GUILayout.Height(40)))
            {
                checker.CheckConfiguration();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "使用此工具诊断 URP 配置状态\n\n" +
                "如果发现问题：\n" +
                "1. 使用菜单: EngineTwin → Setup URP Pipeline\n" +
                "2. 或查看: Assets/EngineTwin/URP_Setup_Guide.md",
                MessageType.Info);
        }
    }
}
#endif
