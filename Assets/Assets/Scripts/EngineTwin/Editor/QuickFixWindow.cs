using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace EngineTwin
{
    /// <summary>
    /// 一键修复场景 - 快速访问所有修复工具
    /// </summary>
    public class QuickFixWindow : EditorWindow
    {
        [MenuItem("EngineTwin/Quick Fix Window", false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<QuickFixWindow>("快速修复");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        Vector2 scrollPosition;

        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("EngineTwin 快速修复工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // === URP 配置 ===
            DrawSection("1. URP 配置");

            if (GUILayout.Button("配置 URP 渲染管线", GUILayout.Height(35)))
            {
                URPSetupTool.SetupURPPipeline();
            }

            if (GUILayout.Button("升级材质到 URP", GUILayout.Height(35)))
            {
                MaterialUpgradeTool.UpgradeMaterialsToURP();
            }

            if (GUILayout.Button("修复粉红色材质", GUILayout.Height(35)))
            {
                MaterialUpgradeTool.FixPinkMaterials();
            }

            if (GUILayout.Button("创建默认 URP 材质", GUILayout.Height(35)))
            {
                MaterialUpgradeTool.CreateDefaultURPMaterials();
            }

            EditorGUILayout.Space(15);

            // === Shader 转换 ===
            DrawSection("2. Shader 转换（解决白屏）");

            if (GUILayout.Button("转换 2D Shader 到 3D", GUILayout.Height(35)))
            {
                ShaderConversionTool.Convert2DTo3D();
            }

            if (GUILayout.Button("全部设置为 URP Lit (3D标准)", GUILayout.Height(35)))
            {
                ShaderConversionTool.SetAllToURPLit();
            }

            if (GUILayout.Button("查看材质报告", GUILayout.Height(35)))
            {
                ShaderConversionTool.ShowMaterialReport();
            }

            EditorGUILayout.Space(15);

            // === 场景诊断 ===
            DrawSection("3. 场景诊断");

            if (GUILayout.Button("诊断白屏问题", GUILayout.Height(35)))
            {
                SceneDiagnosticTool.DiagnoseWhiteScreen();
            }

            if (GUILayout.Button("修复场景光照", GUILayout.Height(35)))
            {
                SceneDiagnosticTool.CreateDefaultLighting();
            }

            if (GUILayout.Button("创建默认相机", GUILayout.Height(35)))
            {
                SceneDiagnosticTool.CreateDefaultCamera();
            }

            EditorGUILayout.Space(15);

            // === 专业渲染 ===
            DrawSection("4. 专业渲染（达到参考图效果）");

            EditorGUILayout.HelpBox(
                "一键配置：PBR材质 + 工作室光照 + 后处理",
                MessageType.Info);

            GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
            if (GUILayout.Button("应用专业渲染配置", GUILayout.Height(40)))
            {
                ProRenderingTool.ApplyProfessionalRendering();
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.2f, 1f, 0.5f);
            if (GUILayout.Button("自动分配专业材质", GUILayout.Height(40)))
            {
                AutoMaterialAssignTool.AutoAssignProfessionalMaterials();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("查看材质分配规则", GUILayout.Height(30)))
            {
                AutoMaterialAssignTool.ShowMaterialRules();
            }

            EditorGUILayout.Space(15);

            // === 科技风格 ===
            DrawSection("5. 科技风格（解决白模问题）");

            EditorGUILayout.HelpBox(
                "为白色模型添加科技感材质和光照",
                MessageType.Info);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 1f);
            if (GUILayout.Button("应用科技风格到场景", GUILayout.Height(40)))
            {
                TechStyleTool.ApplyTechStyle();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("重置为默认材质", GUILayout.Height(30)))
            {
                TechStyleTool.ResetToDefaultMaterials();
            }

            EditorGUILayout.Space(15);

            // === 一键修复 ===
            DrawSection("6. 一键修复全部");

            EditorGUILayout.HelpBox(
                "按顺序执行所有必要的修复步骤：\n" +
                "1. 配置 URP\n" +
                "2. 转换 Shader 到 3D\n" +
                "3. 修复粉红色材质\n" +
                "4. 应用专业渲染\n" +
                "5. 自动分配专业材质\n" +
                "6. 配置工作室光照和后处理",
                MessageType.Info);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("一键修复全部问题", GUILayout.Height(50)))
            {
                OneClickFixAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(20);

            EditorGUILayout.EndScrollView();
        }

        void DrawSection(string title)
        {
            EditorGUILayout.Space(5);
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.fontSize = 13;
            EditorGUILayout.LabelField(title, style);
            EditorGUILayout.Space(5);
        }

        void OneClickFixAll()
        {
            if (!EditorUtility.DisplayDialog("一键修复",
                "将执行以下操作：\n\n" +
                "1. 配置 URP 渲染管线\n" +
                "2. 转换所有 2D Shader 到 3D\n" +
                "3. 修复粉红色材质\n" +
                "4. 检查并修复光照\n" +
                "5. 检查并创建相机\n\n" +
                "建议先备份项目！\n\n是否继续？",
                "继续", "取消"))
            {
                return;
            }

            Debug.Log("=== 开始一键修复 ===");

            // 1. 配置 URP
            Debug.Log("[1/5] 配置 URP...");
            URPSetupTool.SetupURPPipeline();

            // 2. 转换 Shader
            Debug.Log("[2/5] 转换 Shader...");
            ShaderConversionTool.Convert2DTo3D();

            // 3. 修复粉红色
            Debug.Log("[3/7] 修复粉红色材质...");
            MaterialUpgradeTool.FixPinkMaterials();

            // 4. 应用专业渲染
            Debug.Log("[4/7] 应用专业渲染配置...");
            ProRenderingTool.ApplyProfessionalRendering();

            // 5. 自动分配专业材质
            Debug.Log("[5/7] 自动分配专业材质...");
            AutoMaterialAssignTool.AutoAssignProfessionalMaterials();

            // 6. 修复剪裁 Shader
            Debug.Log("[6/7] 修复剪裁 Shader...");
            ClipShaderFixTool.FixClipShaderReference();

            // 7. 检查相机
            Debug.Log("[7/7] 检查相机...");
            if (Camera.main == null)
            {
                Camera[] cameras = Object.FindObjectsOfType<Camera>();
                if (cameras.Length == 0)
                {
                    SceneDiagnosticTool.CreateDefaultCamera();
                }
                else
                {
                    cameras[0].tag = "MainCamera";
                }
            }

            Debug.Log("=== 一键修复完成！===");
            EditorUtility.DisplayDialog("修复完成",
                "所有修复步骤已完成！\n\n" +
                "请保存场景并运行查看效果。\n\n" +
                "如果还有问题，请使用单独的工具逐一检查。",
                "确定");
        }
    }
}
#endif
