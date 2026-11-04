using UnityEngine;
using UnityEditor;

namespace EngineTwin.EditorTools
{
    public class ParticleEffectsDebugger : EditorWindow
    {
        [MenuItem("EngineTwin/Debug/粒子特效调试")]
        static void ShowWindow()
        {
            GetWindow<ParticleEffectsDebugger>("粒子特效调试");
        }

        Vector2 scrollPos;
        float globalScale = 1f;
        float globalAlpha = 1f;

        void OnGUI()
        {
            EditorGUILayout.HelpBox("调试和调整粒子特效参数", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 分析粒子系统"))
            {
                AnalyzeParticles();
            }
            if (GUILayout.Button("🔧 修复粒子大小"))
            {
                FixParticleSize();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("全局调整", EditorStyles.boldLabel);
            globalScale = EditorGUILayout.Slider("粒子大小缩放", globalScale, 0.1f, 5f);
            globalAlpha = EditorGUILayout.Slider("粒子透明度", globalAlpha, 0.1f, 2f);

            if (GUILayout.Button("应用全局调整"))
            {
                ApplyGlobalAdjustment();
            }

            EditorGUILayout.Space(10);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.TextArea(debugLog, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        string debugLog = "点击按钮开始分析...";

        void AnalyzeParticles()
        {
            var particles = FindObjectsOfType<ParticleSystem>();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== 粒子系统分析 (共 {particles.Length} 个) ===\n");

            foreach (var ps in particles)
            {
                if (ps.name.StartsWith("FX_"))
                {
                    var m = ps.main;
                    var r = ps.GetComponent<ParticleSystemRenderer>();

                    sb.AppendLine($"📦 {ps.name}");
                    sb.AppendLine($"   位置: {ps.transform.position}");
                    sb.AppendLine($"   大小: {m.startSize.constant:F4}");
                    sb.AppendLine($"   速度: {m.startSpeed.constant:F2}");
                    sb.AppendLine($"   生命: {m.startLifetime.constant:F2}s");
                    sb.AppendLine($"   颜色: {m.startColor.color}");
                    sb.AppendLine($"   发射率: {ps.emission.rateOverTime.constant:F0}/s");
                    sb.AppendLine($"   最大粒子: {m.maxParticles}");
                    sb.AppendLine($"   渲染模式: {r.renderMode}");
                    sb.AppendLine($"   材质: {(r.sharedMaterial != null ? r.sharedMaterial.name : "None")}");
                    sb.AppendLine($"   Shader: {(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "None")}");

                    // 检查异常值
                    if (m.startSize.constant > 0.1f)
                        sb.AppendLine($"   ⚠️ 粒子可能太大！");
                    if (m.startColor.color.a > 0.8f)
                        sb.AppendLine($"   ⚠️ 透明度可能太高！");
                    if (ps.emission.rateOverTime.constant > 500f)
                        sb.AppendLine($"   ⚠️ 发射率可能太高！");

                    sb.AppendLine();
                }
            }

            debugLog = sb.ToString();
        }

        void FixParticleSize()
        {
            var particles = FindObjectsOfType<ParticleSystem>();
            int fixedCount = 0;

            foreach (var ps in particles)
            {
                if (ps.name.StartsWith("FX_"))
                {
                    var m = ps.main;

                    // 如果粒子太大，缩小10倍
                    if (m.startSize.constant > 0.1f)
                    {
                        var newSize = m.startSize.constant * 0.1f;
                        m.startSize = newSize;
                        fixedCount++;
                        Debug.Log($"修复 {ps.name}: 大小 {m.startSize.constant:F4} -> {newSize:F4}");
                    }
                }
            }

            debugLog = $"已修复 {fixedCount} 个粒子系统的大小";
        }

        void ApplyGlobalAdjustment()
        {
            var particles = FindObjectsOfType<ParticleSystem>();
            int count = 0;

            foreach (var ps in particles)
            {
                if (ps.name.StartsWith("FX_"))
                {
                    var m = ps.main;

                    // 调整大小
                    if (m.startSize.constantMin > 0)
                    {
                        m.startSize = new ParticleSystem.MinMaxCurve(
                            m.startSize.constantMin * globalScale,
                            m.startSize.constantMax * globalScale
                        );
                    }
                    else
                    {
                        m.startSize = m.startSize.constant * globalScale;
                    }

                    // 调整透明度
                    Color c = m.startColor.color;
                    c.a = Mathf.Clamp01(c.a * globalAlpha);
                    m.startColor = c;

                    count++;
                }
            }

            debugLog = $"已调整 {count} 个粒子系统\n大小缩放: {globalScale:F2}x\n透明度: {globalAlpha:F2}x";
        }
    }
}
