using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace EngineTwin
{
    /// <summary>
    /// 剪裁 Shader 修复工具
    /// </summary>
    public static class ClipShaderFixTool
    {
        [MenuItem("EngineTwin/Fix Clip Shader Reference", false, 4)]
        public static void FixClipShaderReference()
        {
            Debug.Log("[Clip Shader Fix] 开始修复剪裁 Shader 引用...");

            // 1. 查找 Bootstrap
            var bootstrap = Object.FindObjectOfType<EngineTwinBootstrap>();
            if (bootstrap == null)
            {
                EditorUtility.DisplayDialog("错误",
                    "未找到场景中的 EngineTwinBootstrap 组件！\n\n" +
                    "请确保场景已正确设置。",
                    "确定");
                return;
            }

            // 2. 加载剪裁 Shader
            Shader clipShader = Shader.Find("EngineTwin/StandardClip");
            if (clipShader == null)
            {
                string shaderPath = "Assets/EngineTwin/Shaders/StandardClip.shader";
                clipShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

                if (clipShader == null)
                {
                    EditorUtility.DisplayDialog("错误",
                        "未找到剪裁 Shader！\n\n" +
                        $"预期路径: {shaderPath}\n\n" +
                        "请确保 Shader 文件存在。",
                        "确定");
                    return;
                }
            }

            // 3. 使用反射设置私有字段
            var clipShaderField = typeof(EngineTwinBootstrap).GetField("clipShader",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (clipShaderField == null)
            {
                EditorUtility.DisplayDialog("错误",
                    "无法找到 clipShader 字段！\n\n" +
                    "可能代码结构已更改。",
                    "确定");
                return;
            }

            // 4. 设置 Shader
            clipShaderField.SetValue(bootstrap, clipShader);
            EditorUtility.SetDirty(bootstrap);

            Debug.Log($"[Clip Shader Fix] 已设置剪裁 Shader: {clipShader.name}");

            // 5. 检查其他材质引用
            CheckMaterialReferences(bootstrap);

            EditorUtility.DisplayDialog("修复完成",
                "剪裁 Shader 引用已修复！\n\n" +
                $"Shader: {clipShader.name}\n\n" +
                "保存场景并运行查看效果。",
                "确定");
        }

        static void CheckMaterialReferences(EngineTwinBootstrap bootstrap)
        {
            var fxAlphaField = typeof(EngineTwinBootstrap).GetField("fxAlphaMaterial",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fxAdditiveField = typeof(EngineTwinBootstrap).GetField("fxAdditiveMaterial",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (fxAlphaField != null)
            {
                Material fxAlpha = (Material)fxAlphaField.GetValue(bootstrap);
                if (fxAlpha == null)
                {
                    Debug.LogWarning("[Clip Shader Fix] fxAlphaMaterial 未设置");
                }
            }

            if (fxAdditiveField != null)
            {
                Material fxAdditive = (Material)fxAdditiveField.GetValue(bootstrap);
                if (fxAdditive == null)
                {
                    Debug.LogWarning("[Clip Shader Fix] fxAdditiveMaterial 未设置");
                }
            }
        }

        [MenuItem("EngineTwin/Show Bootstrap Inspector", false, 5)]
        public static void ShowBootstrapInspector()
        {
            var bootstrap = Object.FindObjectOfType<EngineTwinBootstrap>();
            if (bootstrap == null)
            {
                EditorUtility.DisplayDialog("提示",
                    "未找到场景中的 EngineTwinBootstrap 组件",
                    "确定");
                return;
            }

            Selection.activeGameObject = bootstrap.gameObject;
            EditorGUIUtility.PingObject(bootstrap);

            Debug.Log("[Bootstrap] 已选中 EngineTwinBootstrap，请在 Inspector 中检查所有引用");
        }
    }
}
#endif
