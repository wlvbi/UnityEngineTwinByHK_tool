using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace EngineTwin
{
    /// <summary>
    /// 专业渲染配置：达到产品级渲染效果
    /// </summary>
    public static class ProRenderingTool
    {
        [MenuItem("EngineTwin/Apply Professional Rendering", false, 300)]
        public static void ApplyProfessionalRendering()
        {
            if (!EditorUtility.DisplayDialog("应用专业渲染",
                "将为场景配置专业级渲染效果：\n\n" +
                "1. 高质量 PBR 材质\n" +
                "2. 工作室级光照\n" +
                "3. 后处理效果（Bloom、AO、色调映射）\n" +
                "4. 反射探针\n" +
                "5. 阴影优化\n\n" +
                "是否继续？",
                "应用", "取消"))
            {
                return;
            }

            Debug.Log("[Pro Rendering] 开始配置专业渲染...");

            // 1. 配置 URP 资源质量
            ConfigureURPQuality();

            // 2. 创建专业级材质
            CreateProfessionalMaterials();

            // 3. 配置工作室光照
            SetupStudioLighting();

            // 4. 添加后处理
            SetupPostProcessing();

            // 5. 添加反射探针
            SetupReflectionProbe();

            // 6. 优化相机设置
            OptimizeCameraSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("配置完成",
                "专业渲染配置已完成！\n\n" +
                "运行场景查看效果。\n\n" +
                "建议：调整后处理强度以获得最佳效果。",
                "确定");

            Debug.Log("[Pro Rendering] 专业渲染配置完成！");
        }

        static void ConfigureURPQuality()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                Debug.LogWarning("[Pro Rendering] 未找到 URP 资源");
                return;
            }

            // 使用反射设置质量参数（因为某些属性可能是私有的）
            var type = typeof(UniversalRenderPipelineAsset);

            // MSAA
            var msaaField = type.GetProperty("msaaSampleCount");
            if (msaaField != null)
            {
                msaaField.SetValue(pipeline, 4); // 4x MSAA
            }

            // Render Scale
            var renderScaleField = type.GetProperty("renderScale");
            if (renderScaleField != null)
            {
                renderScaleField.SetValue(pipeline, 1.0f);
            }

            EditorUtility.SetDirty(pipeline);
            Debug.Log("[Pro Rendering] URP 质量已优化");
        }

        static void CreateProfessionalMaterials()
        {
            string matPath = "Assets/EngineTwin/Materials/Professional/";
            if (!System.IO.Directory.Exists(matPath))
            {
                System.IO.Directory.CreateDirectory(matPath);
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            // 铸铁 - 缸体
            CreatePBRMaterial("Pro_CastIron", urpLit,
                new Color(0.15f, 0.15f, 0.17f), 0.95f, 0.35f, matPath);

            // 抛光铝合金 - 缸盖
            CreatePBRMaterial("Pro_PolishedAluminum", urpLit,
                new Color(0.75f, 0.77f, 0.8f), 0.92f, 0.75f, matPath);

            // 不锈钢 - 排气管
            CreatePBRMaterial("Pro_StainlessSteel", urpLit,
                new Color(0.65f, 0.67f, 0.68f), 0.95f, 0.8f, matPath);

            // 黑色塑料 - 进气歧管
            CreatePBRMaterial("Pro_PlasticBlack", urpLit,
                new Color(0.05f, 0.05f, 0.05f), 0.05f, 0.45f, matPath);

            // 橡胶 - 密封件
            CreatePBRMaterial("Pro_Rubber", urpLit,
                new Color(0.08f, 0.08f, 0.08f), 0f, 0.25f, matPath);

            // 金色黄铜 - 连接件
            CreatePBRMaterial("Pro_Brass", urpLit,
                new Color(0.8f, 0.6f, 0.2f), 0.98f, 0.7f, matPath);

            // 氧化铜 - 旧管道
            CreatePBRMaterial("Pro_Copper", urpLit,
                new Color(0.72f, 0.45f, 0.2f), 0.95f, 0.65f, matPath);

            // 黑色涂漆 - 罩盖
            CreatePBRMaterial("Pro_PaintBlack", urpLit,
                new Color(0.08f, 0.08f, 0.1f), 0.15f, 0.6f, matPath);

            // 深蓝涂漆 - 滤清器
            CreatePBRMaterial("Pro_PaintBlue", urpLit,
                new Color(0.05f, 0.1f, 0.25f), 0.15f, 0.65f, matPath);

            Debug.Log("[Pro Rendering] 创建了 9 个专业级材质");
        }

        static void CreatePBRMaterial(string name, Shader shader, Color color, float metallic, float smoothness, string path)
        {
            Material mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_OcclusionStrength", 1.0f);

            // 启用环境反射
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_ON");
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_ON");

            AssetDatabase.CreateAsset(mat, path + name + ".mat");
        }

        static void SetupStudioLighting()
        {
            // 清除旧光源
            Light[] oldLights = Object.FindObjectsOfType<Light>();
            foreach (var light in oldLights)
            {
                Object.DestroyImmediate(light.gameObject);
            }

            // 主光源 - 模拟工作室主灯
            GameObject mainLight = new GameObject("Studio Main Light");
            Light main = mainLight.AddComponent<Light>();
            main.type = LightType.Directional;
            main.color = new Color(1f, 0.98f, 0.95f); // 暖白色
            main.intensity = 1.5f;
            main.shadows = LightShadows.Soft;
            main.shadowStrength = 0.7f;
            mainLight.transform.rotation = Quaternion.Euler(45f, -45f, 0f);

            // 补光 1 - 右侧填充
            GameObject fillLight1 = new GameObject("Studio Fill Light 1");
            Light fill1 = fillLight1.AddComponent<Light>();
            fill1.type = LightType.Directional;
            fill1.color = new Color(0.9f, 0.95f, 1f); // 冷白色
            fill1.intensity = 0.4f;
            fill1.shadows = LightShadows.None;
            fillLight1.transform.rotation = Quaternion.Euler(25f, 135f, 0f);

            // 补光 2 - 后方边缘光
            GameObject rimLight = new GameObject("Studio Rim Light");
            Light rim = rimLight.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.95f, 0.98f, 1f); // 浅蓝色
            rim.intensity = 0.6f;
            rim.shadows = LightShadows.None;
            rimLight.transform.rotation = Quaternion.Euler(-15f, 180f, 0f);

            // 环境光 - 柔和工作室环境
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.65f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.48f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.27f, 0.3f);
            RenderSettings.ambientIntensity = 1.2f;

            Debug.Log("[Pro Rendering] 工作室光照已配置");
        }

        static void SetupPostProcessing()
        {
            // 查找或创建 Volume
            var volume = Object.FindObjectOfType<Volume>();
            if (volume == null)
            {
                GameObject volObj = new GameObject("Global Post-Processing");
                volume = volObj.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1;
            }

            // 创建 Profile
            if (volume.profile == null)
            {
                VolumeProfile newProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                string profilePath = "Assets/EngineTwin/Settings/PostProcessProfile.asset";

                if (!System.IO.Directory.Exists("Assets/EngineTwin/Settings"))
                {
                    System.IO.Directory.CreateDirectory("Assets/EngineTwin/Settings");
                }

                AssetDatabase.CreateAsset(newProfile, profilePath);
                volume.profile = newProfile;
            }

            var volumeProfile = volume.profile;

            // Bloom - 高光溢出
            if (!volumeProfile.TryGet<Bloom>(out var bloom))
            {
                bloom = volumeProfile.Add<Bloom>(true);
            }
            bloom.active = true;
            bloom.intensity.value = 0.3f;
            bloom.threshold.value = 1.0f;
            bloom.scatter.value = 0.7f;

            // Tonemapping - 色调映射
            if (!volumeProfile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = volumeProfile.Add<Tonemapping>(true);
            }
            tonemapping.active = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            // Color Adjustments - 颜色调整
            if (!volumeProfile.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj = volumeProfile.Add<ColorAdjustments>(true);
            }
            colorAdj.active = true;
            colorAdj.contrast.value = 10f;
            colorAdj.saturation.value = 5f;

            EditorUtility.SetDirty(volumeProfile);
            Debug.Log("[Pro Rendering] 后处理已配置");
        }

        static void SetupReflectionProbe()
        {
            // 查找或创建反射探针
            var probe = Object.FindObjectOfType<ReflectionProbe>();
            if (probe == null)
            {
                GameObject probeObj = new GameObject("Reflection Probe");
                probe = probeObj.AddComponent<ReflectionProbe>();
                probeObj.transform.position = Vector3.up * 0.5f;
            }

            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
            probe.size = new Vector3(5f, 5f, 5f);
            probe.intensity = 1.0f;
            probe.resolution = 512;
            probe.hdr = true;

            Debug.Log("[Pro Rendering] 反射探针已配置");
        }

        static void OptimizeCameraSettings()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var cameraData = cam.GetUniversalAdditionalCameraData();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
            }

            cam.allowHDR = true;
            cam.allowMSAA = true;

            Debug.Log("[Pro Rendering] 相机设置已优化");
        }
    }
}
#endif
