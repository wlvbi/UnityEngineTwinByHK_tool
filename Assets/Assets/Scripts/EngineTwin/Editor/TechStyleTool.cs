using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace EngineTwin
{
    /// <summary>
    /// 科技风格配置工具：为场景添加科技感材质和环境
    /// </summary>
    public static class TechStyleTool
    {
        [MenuItem("EngineTwin/Apply Tech Style to Scene", false, 220)]
        public static void ApplyTechStyle()
        {
            if (!EditorUtility.DisplayDialog("应用科技风格",
                "将为场景应用科技感配置：\n\n" +
                "1. 创建金属/塑料材质\n" +
                "2. 根据部件自动分配材质\n" +
                "3. 配置冷色调光照\n" +
                "4. 添加后处理效果\n\n" +
                "是否继续？",
                "应用", "取消"))
            {
                return;
            }

            Debug.Log("[Tech Style] 开始应用科技风格...");

            // 1. 创建科技感材质库
            CreateTechMaterials();

            // 2. 自动分配材质
            AssignMaterialsByName();

            // 3. 配置科技感光照
            SetupTechLighting();

            // 4. 配置环境
            SetupTechEnvironment();

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("完成",
                "科技风格已应用！\n\n" +
                "材质位置: Assets/EngineTwin/Materials/Tech/\n\n" +
                "运行场景查看效果",
                "确定");

            Debug.Log("[Tech Style] 科技风格应用完成！");
        }

        static void CreateTechMaterials()
        {
            string matPath = "Assets/EngineTwin/Materials/Tech/";
            if (!System.IO.Directory.Exists(matPath))
            {
                System.IO.Directory.CreateDirectory(matPath);
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            // 金属材质 - 钢铁（深灰）
            CreateMaterial("Tech_Steel_Dark", urpLit,
                new Color(0.3f, 0.32f, 0.35f), 0.9f, 0.5f, matPath);

            // 金属材质 - 铝合金（浅灰）
            CreateMaterial("Tech_Aluminum", urpLit,
                new Color(0.65f, 0.67f, 0.7f), 0.85f, 0.6f, matPath);

            // 金属材质 - 铜（橙铜色）
            CreateMaterial("Tech_Copper", urpLit,
                new Color(0.72f, 0.45f, 0.2f), 0.95f, 0.7f, matPath);

            // 塑料 - 黑色
            CreateMaterial("Tech_Plastic_Black", urpLit,
                new Color(0.08f, 0.08f, 0.1f), 0.1f, 0.4f, matPath);

            // 塑料 - 深蓝
            CreateMaterial("Tech_Plastic_Blue", urpLit,
                new Color(0.1f, 0.15f, 0.3f), 0.15f, 0.5f, matPath);

            // 橡胶
            CreateMaterial("Tech_Rubber", urpLit,
                new Color(0.15f, 0.15f, 0.15f), 0f, 0.2f, matPath);

            // 涂漆 - 发动机红
            CreateMaterial("Tech_Paint_Red", urpLit,
                new Color(0.7f, 0.1f, 0.1f), 0.2f, 0.75f, matPath);

            // 涂漆 - 科技蓝
            CreateMaterial("Tech_Paint_TechBlue", urpLit,
                new Color(0.1f, 0.4f, 0.8f), 0.25f, 0.8f, matPath);

            // 玻璃/透明
            CreateMaterial("Tech_Glass", urpLit,
                new Color(0.8f, 0.9f, 1f, 0.3f), 0.1f, 0.95f, matPath);

            Debug.Log("[Tech Style] 创建了 9 个科技感材质");
        }

        static void CreateMaterial(string name, Shader shader, Color color, float metallic, float smoothness, string path)
        {
            Material mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            // 如果是透明材质
            if (color.a < 1f)
            {
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0); // Alpha
                mat.renderQueue = 3000;
            }

            AssetDatabase.CreateAsset(mat, path + name + ".mat");
        }

        static void AssignMaterialsByName()
        {
            Material matSteel = AssetDatabase.LoadAssetAtPath<Material>("Assets/EngineTwin/Materials/Tech/Tech_Steel_Dark.mat");
            Material matAluminum = AssetDatabase.LoadAssetAtPath<Material>("Assets/EngineTwin/Materials/Tech/Tech_Aluminum.mat");
            Material matCopper = AssetDatabase.LoadAssetAtPath<Material>("Assets/EngineTwin/Materials/Tech/Tech_Copper.mat");
            Material matPlasticBlack = AssetDatabase.LoadAssetAtPath<Material>("Assets/EngineTwin/Materials/Tech/Tech_Plastic_Black.mat");
            Material matPlasticBlue = AssetDatabase.LoadAssetAtPath<Material>("Assets/EngineTwin/Materials/Tech/Tech_Plastic_Blue.mat");
            Material matRubber = AssetDatabase.LoadAssetAtPath<Material>("Assets/EngineTwin/Materials/Tech/Tech_Rubber.mat");
            Material matPaintRed = AssetDatabase.LoadAssetAtPath<Material>("Assets/EngineTwin/Materials/Tech/Tech_Paint_Red.mat");
            Material matPaintBlue = AssetDatabase.LoadAssetAtPath<Material>("Assets/EngineTwin/Materials/Tech/Tech_Paint_TechBlue.mat");

            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
            int assignedCount = 0;

            foreach (var renderer in renderers)
            {
                string objName = renderer.name.ToLower();
                Material selectedMat = matSteel; // 默认钢铁

                // 根据名称智能分配材质
                if (objName.Contains("block") || objName.Contains("cylinder") || objName.Contains("crank"))
                {
                    selectedMat = matSteel; // 缸体、曲轴 - 钢铁
                }
                else if (objName.Contains("head") || objName.Contains("cover"))
                {
                    selectedMat = matAluminum; // 缸盖、罩盖 - 铝合金
                }
                else if (objName.Contains("pulley") || objName.Contains("belt"))
                {
                    selectedMat = matPlasticBlack; // 皮带轮 - 黑色塑料
                }
                else if (objName.Contains("oil") || objName.Contains("filter"))
                {
                    selectedMat = matPaintBlue; // 滤清器 - 蓝色涂漆
                }
                else if (objName.Contains("manifold") || objName.Contains("exhaust"))
                {
                    selectedMat = matCopper; // 排气管 - 铜色
                }
                else if (objName.Contains("alternator") || objName.Contains("pump"))
                {
                    selectedMat = matPlasticBlue; // 发电机、泵 - 蓝色塑料
                }
                else if (objName.Contains("gasket") || objName.Contains("seal"))
                {
                    selectedMat = matRubber; // 密封件 - 橡胶
                }
                else if (objName.Contains("spark") || objName.Contains("plug"))
                {
                    selectedMat = matPaintRed; // 火花塞 - 红色
                }

                // 应用材质
                Material[] mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = selectedMat;
                }
                renderer.sharedMaterials = mats;
                assignedCount++;
            }

            Debug.Log($"[Tech Style] 为 {assignedCount} 个对象分配了材质");
        }

        static void SetupTechLighting()
        {
            // 清除旧光源
            Light[] oldLights = Object.FindObjectsOfType<Light>();
            foreach (var light in oldLights)
            {
                Object.DestroyImmediate(light.gameObject);
            }

            // 主光源 - 冷白色定向光
            GameObject mainLight = new GameObject("Main Light");
            Light main = mainLight.AddComponent<Light>();
            main.type = LightType.Directional;
            main.color = new Color(0.9f, 0.95f, 1f); // 冷白色
            main.intensity = 1.2f;
            mainLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 辅助光源 - 蓝色填充光
            GameObject fillLight = new GameObject("Fill Light (Blue)");
            Light fill = fillLight.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.4f, 0.6f, 1f); // 科技蓝
            fill.intensity = 0.3f;
            fillLight.transform.rotation = Quaternion.Euler(-20f, 150f, 0f);

            // 环境光 - 冷色调
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.3f, 0.4f, 0.6f); // 蓝灰
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.25f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.15f);

            Debug.Log("[Tech Style] 配置了科技感光照");
        }

        static void SetupTechEnvironment()
        {
            // 雾效 - 淡蓝色
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.1f, 0.15f, 0.25f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 5f;
            RenderSettings.fogEndDistance = 20f;

            // 天空盒 - 深色
            RenderSettings.skybox = null;
            Camera.main.backgroundColor = new Color(0.05f, 0.08f, 0.15f); // 深蓝黑

            Debug.Log("[Tech Style] 配置了科技感环境");
        }

        [MenuItem("EngineTwin/Reset to Default Materials", false, 221)]
        public static void ResetToDefaultMaterials()
        {
            if (!EditorUtility.DisplayDialog("重置材质",
                "将所有对象恢复为默认灰色材质\n\n是否继续？",
                "继续", "取消"))
            {
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material defaultMat = new Material(urpLit);
            defaultMat.SetColor("_BaseColor", Color.grey);
            defaultMat.SetFloat("_Metallic", 0.5f);
            defaultMat.SetFloat("_Smoothness", 0.5f);

            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
            foreach (var renderer in renderers)
            {
                Material[] mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = defaultMat;
                }
                renderer.sharedMaterials = mats;
            }

            Debug.Log($"[Reset] 重置了 {renderers.Length} 个对象的材质");
        }
    }
}
#endif
