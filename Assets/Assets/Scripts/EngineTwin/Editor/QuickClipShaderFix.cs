using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace EngineTwin
{
    public static class QuickClipShaderFix
    {
        [MenuItem("EngineTwin/USE FINAL CLIP SHADER", false, 5)]
        public static void UseFinClipShader()
        {
            var bootstrap = Object.FindObjectOfType<EngineTwinBootstrap>();
            if (bootstrap == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到 EngineTwinBootstrap", "确定");
                return;
            }

            Shader finalShader = Shader.Find("EngineTwin/ClipShaderFinal");
            if (finalShader == null)
            {
                EditorUtility.DisplayDialog("错误",
                    "未找到 ClipShaderFinal\n\n请等待 Unity 编译完成", "确定");
                return;
            }

            var field = typeof(EngineTwinBootstrap).GetField("clipShader",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(bootstrap, finalShader);
                EditorUtility.SetDirty(bootstrap);

                Debug.Log($"✓ 已设置剪裁 Shader: {finalShader.name}");
                EditorUtility.DisplayDialog("成功",
                    "已设置 ClipShaderFinal！\n\n保存场景并运行测试。", "确定");
            }
        }
    }
}
#endif
