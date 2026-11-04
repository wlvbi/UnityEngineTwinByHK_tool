using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace EngineTwin
{
    /// <summary>
    /// 场景诊断工具：检查并修复白屏问题
    /// </summary>
    public static class SceneDiagnosticTool
    {
        [MenuItem("EngineTwin/Diagnose White Screen", false, 200)]
        public static void DiagnoseWhiteScreen()
        {
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("=== 场景白屏诊断 ===\n");

            int issueCount = 0;

            // 检查 1：渲染管线
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null)
            {
                report.AppendLine("❌ 问题 1: 渲染管线未设置");
                report.AppendLine("   解决: 点击菜单 EngineTwin → Setup URP Pipeline\n");
                issueCount++;
            }
            else
            {
                report.AppendLine($"✓ 渲染管线: {pipeline.name}\n");
            }

            // 检查 2：场景光照
            Light[] lights = Object.FindObjectsOfType<Light>();
            if (lights.Length == 0)
            {
                report.AppendLine("❌ 问题 2: 场景中没有光源");
                report.AppendLine("   解决: 点击下方 [修复光照] 按钮\n");
                issueCount++;
            }
            else
            {
                report.AppendLine($"✓ 找到 {lights.Length} 个光源");
                foreach (var light in lights)
                {
                    report.AppendLine($"   - {light.name} ({light.type})");
                }
                report.AppendLine();
            }

            // 检查 3：材质
            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
            int pinkCount = 0;
            int nullShaderCount = 0;

            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null) continue;
                    if (mat.shader == null) nullShaderCount++;
                    else if (!mat.shader.isSupported) pinkCount++;
                }
            }

            if (pinkCount > 0 || nullShaderCount > 0)
            {
                report.AppendLine($"❌ 问题 3: 材质问题");
                if (pinkCount > 0) report.AppendLine($"   粉红色材质: {pinkCount} 个");
                if (nullShaderCount > 0) report.AppendLine($"   无效Shader: {nullShaderCount} 个");
                report.AppendLine("   解决: 点击菜单 EngineTwin → Fix Pink Materials\n");
                issueCount++;
            }
            else if (renderers.Length > 0)
            {
                report.AppendLine($"✓ 材质检查通过 ({renderers.Length} 个渲染器)\n");
            }

            // 检查 4：相机
            Camera[] cameras = Object.FindObjectsOfType<Camera>();
            if (cameras.Length == 0)
            {
                report.AppendLine("❌ 问题 4: 场景中没有相机");
                report.AppendLine("   解决: 点击下方 [创建相机] 按钮\n");
                issueCount++;
            }
            else
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    report.AppendLine($"✓ 主相机: {mainCam.name}");
                    report.AppendLine($"   背景: {mainCam.clearFlags}");
                    report.AppendLine($"   背景色: {mainCam.backgroundColor}\n");
                }
                else
                {
                    report.AppendLine($"⚠ 找到 {cameras.Length} 个相机，但没有 MainCamera 标签\n");
                }
            }

            // 检查 5：后处理
            var volumes = Object.FindObjectsOfType<UnityEngine.Rendering.Volume>();
            if (volumes.Length > 0)
            {
                report.AppendLine($"✓ 找到 {volumes.Length} 个 Volume\n");
            }

            // 总结
            report.AppendLine("==================");
            if (issueCount == 0)
            {
                report.AppendLine("✓ 未发现明显问题");
                report.AppendLine("\n如果仍然白屏，请检查:");
                report.AppendLine("1. 相机是否对准模型");
                report.AppendLine("2. 模型是否太大或太小");
                report.AppendLine("3. 材质颜色是否为白色");
            }
            else
            {
                report.AppendLine($"❌ 发现 {issueCount} 个问题，请根据提示修复");
            }

            Debug.Log(report.ToString());

            // 显示对话框
            ShowDiagnosticWindow(report.ToString(), issueCount);
        }

        static void ShowDiagnosticWindow(string report, int issueCount)
        {
            if (issueCount == 0)
            {
                EditorUtility.DisplayDialog("场景诊断", report, "确定");
            }
            else
            {
                if (EditorUtility.DisplayDialog("场景诊断",
                    report + "\n\n是否自动修复这些问题？", "自动修复", "稍后手动修复"))
                {
                    AutoFix();
                }
            }
        }

        static void AutoFix()
        {
            Debug.Log("[自动修复] 开始修复场景问题...");

            // 修复 1：设置 URP
            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                URPSetupTool.SetupURPPipeline();
            }

            // 修复 2：添加光照
            Light[] lights = Object.FindObjectsOfType<Light>();
            if (lights.Length == 0)
            {
                CreateDefaultLighting();
            }

            // 修复 3：修复材质
            MaterialUpgradeTool.FixPinkMaterials();

            // 修复 4：检查相机
            if (Camera.main == null)
            {
                Camera[] cameras = Object.FindObjectsOfType<Camera>();
                if (cameras.Length == 0)
                {
                    CreateDefaultCamera();
                }
                else
                {
                    cameras[0].tag = "MainCamera";
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[自动修复] 修复完成！");

            EditorUtility.DisplayDialog("修复完成",
                "场景问题已修复！\n\n请保存场景并重新运行。", "确定");
        }

        [MenuItem("EngineTwin/Fix Scene Lighting", false, 201)]
        public static void CreateDefaultLighting()
        {
            // 创建定向光
            GameObject directionalLight = new GameObject("Directional Light");
            Light light = directionalLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            directionalLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 设置环境光
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.212f, 0.227f, 0.259f);
            RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f);
            RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Fix Lighting] 已创建默认光照");
        }

        [MenuItem("EngineTwin/Create Default Camera", false, 202)]
        public static void CreateDefaultCamera()
        {
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.19f, 0.3f, 0.47f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 1000f;

            camObj.transform.position = new Vector3(0, 1, -3);
            camObj.transform.LookAt(Vector3.zero);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Create Camera] 已创建默认相机");
        }
    }
}
#endif
