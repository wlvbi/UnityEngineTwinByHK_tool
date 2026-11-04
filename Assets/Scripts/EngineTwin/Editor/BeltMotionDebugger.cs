using UnityEngine;
using UnityEditor;

namespace EngineTwin.EditorTools
{
    public class BeltMotionDebugger : EditorWindow
    {
        [MenuItem("EngineTwin/Debug/皮带运动调试")]
        static void ShowWindow()
        {
            GetWindow<BeltMotionDebugger>("皮带运动调试");
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("诊断皮带转动问题", MessageType.Info);

            if (GUILayout.Button("🔍 检查皮带状态"))
            {
                CheckBeltStatus();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.TextArea(debugLog, GUILayout.ExpandHeight(true));
        }

        string debugLog = "点击按钮开始检查...";

        void CheckBeltStatus()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 皮带运动诊断 ===\n");

            // 查找 EngineMotion 组件
            var motion = FindObjectOfType<EngineTwin.EngineMotion>();
            if (motion == null)
            {
                sb.AppendLine("❌ 未找到 EngineMotion 组件");
                debugLog = sb.ToString();
                return;
            }

            sb.AppendLine("✅ 找到 EngineMotion 组件\n");

            // 检查皮带渲染器
            var beltRenderers = FindObjectsOfType<Renderer>();
            Renderer beltRenderer = null;
            foreach (var r in beltRenderers)
            {
                if (r.name.Contains("Belt") && r.name.Contains("Runtime"))
                {
                    beltRenderer = r;
                    break;
                }
            }

            if (beltRenderer == null)
            {
                sb.AppendLine("❌ 未找到运行时皮带渲染器 (Frt_Belt_Runtime)");
                sb.AppendLine("   可能原因：");
                sb.AppendLine("   1. 皮带部件未正确识别");
                sb.AppendLine("   2. BuildBelt() 未执行");
            }
            else
            {
                sb.AppendLine($"✅ 找到皮带渲染器: {beltRenderer.name}");
                sb.AppendLine($"   位置: {beltRenderer.transform.position}");

                var mat = beltRenderer.sharedMaterial;
                if (mat == null)
                {
                    sb.AppendLine("   ❌ 材质为空");
                }
                else
                {
                    sb.AppendLine($"   材质: {mat.name}");
                    sb.AppendLine($"   Shader: {mat.shader.name}");

                    if (mat.mainTexture == null)
                    {
                        sb.AppendLine("   ❌ 主纹理为空");
                    }
                    else
                    {
                        sb.AppendLine($"   ✅ 主纹理: {mat.mainTexture.name}");
                    }

                    var offset = mat.mainTextureOffset;
                    sb.AppendLine($"   纹理偏移: {offset}");
                }
            }

            // 检查皮带轮
            sb.AppendLine("\n=== 皮带轮检查 ===");
            string[] beltOrder = { "Frt_CrankDamper", "Frt_PulleyIdler", "Frt_PulleyAlt",
                                   "Frt_PulleyWP", "Frt_PulleyTens", "Frt_PulleyIdlerLo" };

            int foundCount = 0;
            foreach (var name in beltOrder)
            {
                bool found = false;
                foreach (var t in FindObjectsOfType<Transform>())
                {
                    if (t.name == name)
                    {
                        found = true;
                        foundCount++;
                        sb.AppendLine($"✅ {name}");
                        break;
                    }
                }
                if (!found)
                {
                    sb.AppendLine($"❌ {name} 未找到");
                }
            }

            sb.AppendLine($"\n找到 {foundCount}/{beltOrder.Length} 个皮带轮");

            // 检查发动机是否运行
            sb.AppendLine("\n=== 运行状态 ===");
            if (Application.isPlaying)
            {
                sb.AppendLine("✅ 游戏正在运行");
                sb.AppendLine("提示：观察皮带纹理是否滚动");
            }
            else
            {
                sb.AppendLine("⚠️ 游戏未运行");
                sb.AppendLine("需要进入 Play 模式才能看到皮带转动");
            }

            debugLog = sb.ToString();
        }
    }
}
