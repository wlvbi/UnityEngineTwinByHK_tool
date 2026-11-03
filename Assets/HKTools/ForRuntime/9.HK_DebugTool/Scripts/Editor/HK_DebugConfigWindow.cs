using System.IO;
using UnityEditor;
using UnityEngine;

namespace HKTools
{
    public class HK_DebugConfigWindow : EditorWindow
    {
        HK_DebugConfig config;
        string configPath = "Assets/HKTools/ForRuntime/9.HK_DebugTool/Resources/HKDebugConfig.asset";

        [MenuItem("HKTools/HKDebugSettings (HKDebug配置)", priority = HK_MenuPriority.HK_DebugConfig)]
        static void ShowWindow()
        {
            GetWindow<HK_DebugConfigWindow>("HKDebug配置").Show();
        }

        void OnEnable()
        {
            LoadConfig();
        }

        void LoadConfig()
        {
            config = Resources.Load<HK_DebugConfig>("HKDebugConfig");
            if (config == null)
            {
                // 尝试在指定路径加载
                config = AssetDatabase.LoadAssetAtPath<HK_DebugConfig>(configPath);

            }
        }

        void OnGUI()
        {
            EditorGUILayout.Space();

            if (config == null)
            {
                if (GUILayout.Button("创建 HKDebugConfig 配置文件"))
                {
                    CreateHKDebugConfig();
                }
                EditorGUILayout.Space();
            }

            if (config != null)
            {
                EditorGUI.BeginChangeCheck();

                config.isDebugTime = EditorGUILayout.Toggle(new GUIContent("是否打印时间"), config.isDebugTime);
                EditorGUILayout.Space();

                config.isSave = EditorGUILayout.Toggle(new GUIContent("是否存储日志"), config.isSave);
                EditorGUILayout.Space();

                config.isSaveNormalLog = EditorGUILayout.Toggle(new GUIContent("是否存储普通Log"), config.isSaveNormalLog);
                EditorGUILayout.Space();

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        void CreateHKDebugConfig()
        {
            string resourcesFolder = Path.GetDirectoryName(configPath);
            if (!AssetDatabase.IsValidFolder(resourcesFolder))
            {
                Directory.CreateDirectory(resourcesFolder);
                AssetDatabase.Refresh();
            }

            config = ScriptableObject.CreateInstance<HK_DebugConfig>();

            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("HKDebug Config", "新的 HKDebugConfig 配置文件已创建", "确定");
        }
    }
}