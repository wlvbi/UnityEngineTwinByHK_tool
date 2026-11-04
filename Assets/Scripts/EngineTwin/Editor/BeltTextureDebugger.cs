using UnityEngine;
using UnityEditor;

namespace EngineTwin.EditorTools
{
    public class BeltTextureDebugger : EditorWindow
    {
        [MenuItem("EngineTwin/Debug/皮带纹理偏移调试")]
        static void ShowWindow()
        {
            GetWindow<BeltTextureDebugger>("皮带纹理偏移");
        }

        Renderer beltRenderer;
        Vector2 lastOffset;
        float updateInterval = 0.1f;
        float nextUpdate;

        void OnGUI()
        {
            EditorGUILayout.HelpBox("实时监控皮带纹理偏移", MessageType.Info);

            if (GUILayout.Button("🔍 查找皮带渲染器"))
            {
                FindBeltRenderer();
            }

            if (beltRenderer != null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("皮带渲染器", beltRenderer.name, EditorStyles.boldLabel);

                var mat = beltRenderer.sharedMaterial;
                if (mat != null)
                {
                    EditorGUILayout.LabelField("材质名称", mat.name);
                    EditorGUILayout.LabelField("Shader", mat.shader.name);

                    if (mat.mainTexture != null)
                    {
                        EditorGUILayout.LabelField("主纹理", mat.mainTexture.name);

                        Vector2 offset = mat.mainTextureOffset;
                        EditorGUILayout.LabelField("纹理偏移", $"({offset.x:F4}, {offset.y:F4})");

                        if (Application.isPlaying)
                        {
                            float delta = Mathf.Abs(offset.x - lastOffset.x);
                            if (delta > 0.0001f)
                            {
                                EditorGUILayout.HelpBox($"✅ 偏移变化: {delta:F6}", MessageType.Info);
                            }
                            else
                            {
                                EditorGUILayout.HelpBox("❌ 偏移未变化（皮带静止）", MessageType.Warning);
                            }
                            lastOffset = offset;
                        }

                        // 手动测试偏移
                        EditorGUILayout.Space(10);
                        EditorGUILayout.LabelField("手动测试", EditorStyles.boldLabel);
                        if (GUILayout.Button("测试偏移 +0.1"))
                        {
                            mat.mainTextureOffset = new Vector2(offset.x + 0.1f, offset.y);
                            Debug.Log($"手动设置纹理偏移为 {mat.mainTextureOffset}");
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("❌ 主纹理为空", MessageType.Error);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("❌ 材质为空", MessageType.Error);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("未找到皮带渲染器", MessageType.Warning);
            }
        }

        void OnInspectorUpdate()
        {
            if (Application.isPlaying && Time.time > nextUpdate)
            {
                nextUpdate = Time.time + updateInterval;
                Repaint();
            }
        }

        void FindBeltRenderer()
        {
            foreach (var r in FindObjectsOfType<Renderer>())
            {
                if (r.name.Contains("Belt") && r.name.Contains("Runtime"))
                {
                    beltRenderer = r;
                    Debug.Log($"找到皮带渲染器: {r.name}");
                    return;
                }
            }
            Debug.LogWarning("未找到运行时皮带渲染器");
        }
    }
}
