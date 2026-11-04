using System;
using System.IO;
using System.Linq;
using HKTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EngineTwin.Editor
{
    /// <summary>
    /// 一键搭建：生成适配本场景比例的 HK_Tip 预制体变体、标签线材质、部件目录资产，并在场景中创建 EngineTwin 入口物体。
    /// 不修改 HKTools 自带的预制体（通过 Prefab Variant 覆盖参数），方便以后升级框架。
    /// </summary>
    public static class EngineTwinSetup
    {
        const string OutDir = "Assets/EngineTwin";
        const string TipPrefabPath = "Assets/HKTools/ForRuntime/8.HK_Tips/Prefabs/HK_Tip.prefab";
        const string CurvePrefabPath = "Assets/HKTools/ForRuntime/12.HK_UGUITools/2.HK_Curve2D/Prefabs/HK_Curve2D.prefab";
        const string FontPath = "Assets/_Fonts/msyh SDF.asset";
        const string ModelPath = "Assets/Models/V8Engine/V8_Engine.fbx";
        const string ModelMaterialDir = "Assets/Models/V8Engine/Materials";

        [MenuItem("EngineTwin/1. 导入 TextMeshPro 基础资源", priority = 0)]
        public static void ImportTmpEssentials()
        {
            if (HasTmpEssentials())
            {
                Debug.Log("[EngineTwin] TextMeshPro 基础资源已存在");
                return;
            }

            // 通过反射调用，避免不同 TMP 版本的程序集/访问级别差异导致编译失败
            Type importer = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TMPro.TMP_PackageResourceImporter"))
                .FirstOrDefault(t => t != null);
            var method = importer?.GetMethod("ImportResources", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Debug.LogError("[EngineTwin] 未找到 TMP 导入接口，请手动执行 Window → TextMeshPro → Import TMP Essential Resources");
                return;
            }
            method.Invoke(null, new object[] { true, false, false });
            Debug.Log("[EngineTwin] 已开始导入 TextMeshPro 基础资源");
        }

        [MenuItem("EngineTwin/2. 一键搭建数字孪生场景", priority = 1)]
        public static void BuildScene()
        {
            if (!HasTmpEssentials())
            {
                Debug.LogError("[EngineTwin] 请先执行菜单 EngineTwin/1. 导入 TextMeshPro 基础资源");
                return;
            }

            EnsureFolder(OutDir);
            EnsureFolder(OutDir + "/Prefabs");
            EnsureFolder(OutDir + "/Materials");
            EnsureFolder(OutDir + "/Data");

            MakeModelReadable();
            EnableEmissionOnModelMaterials();

            Material lineMat = CreateTipLineMaterial();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning($"[EngineTwin] 未找到中文字体 {FontPath}");

            HK_TipsCtrl tipVariant = CreateTipVariant(lineMat, font);
            EnginePartCatalog catalog = CreateOrLoadCatalog();
            var curvePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CurvePrefabPath)?.GetComponent<HK_Curve2D>();
            if (curvePrefab == null)
            {
                Debug.LogError($"[EngineTwin] 未找到 {CurvePrefabPath} 或其上没有 HK_Curve2D 组件");
                return;
            }

            if (!SetupSceneObjects(tipVariant, curvePrefab, catalog, font)) return;

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[EngineTwin] 搭建完成：点击 Play 运行数字孪生");
        }

        static bool HasTmpEssentials() => AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>运行时要给网格加 MeshCollider，打包后需要网格可读</summary>
        static void MakeModelReadable()
        {
            if (AssetImporter.GetAtPath(ModelPath) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        /// <summary>高亮通过 MaterialPropertyBlock 写 _EmissionColor，需要材质开启 _EMISSION（自发光默认黑色，不改变外观）</summary>
        static void EnableEmissionOnModelMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ModelMaterialDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || !m.HasProperty("_EmissionColor")) continue;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(m);
            }
        }

        /// <summary>HK_Tips 自带的连线材质使用 URP 着色器，本项目是内置管线，换成 Sprites/Default（支持透明淡入淡出）</summary>
        static Material CreateTipLineMaterial()
        {
            string path = OutDir + "/Materials/EngineTipLine.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("Sprites/Default");
            mat.color = new Color(0.35f, 0.85f, 1f, 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static HK_TipsCtrl CreateTipVariant(Material lineMat, TMP_FontAsset font)
        {
            string path = OutDir + "/Prefabs/EngineTip.prefab";
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TipPrefabPath);
            if (basePrefab == null) throw new InvalidOperationException("未找到 " + TipPrefabPath);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                inst.name = "EngineTip";
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                inst.transform.localScale = Vector3.one * 0.11f;    // 原预制体按 10 米级展厅设计，发动机约 0.9 米

                var so = new SerializedObject(inst.GetComponent<HK_TipsCtrl>());
                SetFloat(so, "visibleDistance", 8f);
                SetBool(so, "isCanClick", true);
                SetBool(so, "isCanAutoScale", true);
                SetBool(so, "isFaceCamera", true);
                SetVector2(so, "camRange", new Vector2(1.2f, 4f));
                SetFloat(so, "minimum", 0.75f);
                SetFloat(so, "maximum", 1.35f);
                SetBool(so, "isCustomAdvanced", true);
                SetFloat(so, "titleOffSet", 0.03f);
                SetFloat(so, "lineDefaultWidth", 0.0035f);
                SetBool(so, "isUseCurve", true);
                SetFloat(so, "curveStrength", 0.6f);
                so.ApplyModifiedPropertiesWithoutUndo();

                foreach (var lr in inst.GetComponentsInChildren<LineRenderer>(true))
                {
                    lr.sharedMaterial = lineMat;
                    lr.startColor = lr.endColor = Color.white;
                }
                foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.GetComponent<TMP_Text>() == null) mr.sharedMaterial = lineMat;   // 锚点小球
                }

                var tmp = inst.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null)
                {
                    if (font != null) tmp.font = font;
                    tmp.fontSize = 50;
                    tmp.richText = true;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.lineSpacing = -10f;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(inst, path);
                return saved.GetComponent<HK_TipsCtrl>();
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        static EnginePartCatalog CreateOrLoadCatalog()
        {
            string path = OutDir + "/Data/EnginePartCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<EnginePartCatalog>(path);
            if (catalog != null)
            {
                // 保留用户修改，只把新版本默认目录中新增的传感器绑定合并进去
                foreach (var def in EnginePartCatalog.DefaultParts())
                {
                    var cur = catalog.parts.Find(p => p.id == def.id);
                    if (cur == null) { catalog.parts.Add(def); continue; }
                    var merged = cur.sensors.ToList();
                    foreach (var s in def.sensors) if (!merged.Contains(s)) merged.Add(s);
                    cur.sensors = merged.ToArray();
                }
                EditorUtility.SetDirty(catalog);
                return catalog;
            }

            catalog = EnginePartCatalog.CreateDefault();
            catalog.name = "EnginePartCatalog";
            AssetDatabase.CreateAsset(catalog, path);
            return catalog;
        }

        static bool SetupSceneObjects(HK_TipsCtrl tipVariant, HK_Curve2D curvePrefab, EnginePartCatalog catalog, TMP_FontAsset font)
        {
            GameObject engine = GameObject.Find("V8_Engine");
            if (engine == null)
            {
                Debug.LogError("[EngineTwin] 场景中没有名为 V8_Engine 的发动机模型");
                return false;
            }

            Camera cam = Camera.main;
            if (cam != null && !cam.TryGetComponent<HK_TipCamera>(out _))
                Undo.AddComponent<HK_TipCamera>(cam.gameObject);

            GameObject go = GameObject.Find("EngineTwin");
            if (go == null)
            {
                go = new GameObject("EngineTwin");
                Undo.RegisterCreatedObjectUndo(go, "Create EngineTwin");
            }
            if (!go.TryGetComponent(out EngineTwinBootstrap boot))
                boot = Undo.AddComponent<EngineTwinBootstrap>(go);

            var so = new SerializedObject(boot);
            so.FindProperty("engineRoot").objectReferenceValue = engine.transform;
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.FindProperty("partCatalog").objectReferenceValue = catalog;
            so.FindProperty("tipPrefab").objectReferenceValue = tipVariant;
            so.FindProperty("curvePrefab").objectReferenceValue = curvePrefab;
            so.FindProperty("uiFont").objectReferenceValue = font;
            so.FindProperty("clipShader").objectReferenceValue = Shader.Find("EngineTwin/StandardClip");
            so.FindProperty("fxAlphaMaterial").objectReferenceValue = FxMaterial("FX_AlphaBlended", "Legacy Shaders/Particles/Alpha Blended");
            so.FindProperty("fxAdditiveMaterial").objectReferenceValue = FxMaterial("FX_Additive", "Legacy Shaders/Particles/Additive");
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(go.scene);
            return true;
        }

        // ---------- 粒子材质（存为资产，保证打包时着色器被包含） ----------

        static Material FxMaterial(string name, string shaderName)
        {
            string texPath = OutDir + "/Materials/FX_SoftDot.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                tex = EngineEffects.SoftDot();
                AssetDatabase.CreateAsset(tex, texPath);
            }

            string path = $"{OutDir}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Sprites/Default");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.mainTexture = tex;
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- SerializedObject 小工具 ----------

        static SerializedProperty Prop(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            if (p == null) Debug.LogWarning($"[EngineTwin] HK_TipsCtrl 上未找到字段 {name}（HKTools 版本变化？）");
            return p;
        }

        static void SetFloat(SerializedObject so, string name, float v) { var p = Prop(so, name); if (p != null) p.floatValue = v; }
        static void SetBool(SerializedObject so, string name, bool v) { var p = Prop(so, name); if (p != null) p.boolValue = v; }
        static void SetVector2(SerializedObject so, string name, Vector2 v) { var p = Prop(so, name); if (p != null) p.vector2Value = v; }
    }
}
