using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>部件高亮：选中（青）、悬停（淡蓝）、预警（橙色呼吸）、报警（红色呼吸），用 MaterialPropertyBlock 写自发光颜色</summary>
    public class EngineHighlighter : EngineTwinController
    {
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly Color HoverColor = new Color(0.10f, 0.16f, 0.24f);
        static readonly Color SelectColor = new Color(0.05f, 0.38f, 0.62f);
        static readonly Color WarningColor = new Color(0.45f, 0.25f, 0.00f);
        static readonly Color AlarmColor = new Color(0.80f, 0.05f, 0.03f);

        IEnginePartModel partModel;
        IEngineTelemetryModel telemetry;
        MaterialPropertyBlock block;
        readonly Dictionary<EnginePart, Color> applied = new Dictionary<EnginePart, Color>();

        public void Init()
        {
            partModel = this.GetModel<IEnginePartModel>();
            telemetry = this.GetModel<IEngineTelemetryModel>();
            block = new MaterialPropertyBlock();
            EnsureEmissionKeyword();
        }

        void LateUpdate()
        {
            if (partModel == null) return;

            float pulse = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));

            foreach (var part in partModel.Parts)
            {
                Color c = Color.black;
                if (part == partModel.Selected.Value) c = SelectColor;
                else if (part == partModel.Hovered.Value) c = HoverColor;

                SensorLevel level = part.GetLevel(telemetry);
                if (level == SensorLevel.Alarm) c += AlarmColor * pulse;
                else if (level == SensorLevel.Warning) c += WarningColor * pulse;

                if (applied.TryGetValue(part, out Color prev) && prev == c) continue;
                applied[part] = c;

                block.SetColor(EmissionId, c);
                foreach (var r in part.Renderers) r.SetPropertyBlock(block);
            }
        }

        /// <summary>Standard 着色器需开启 _EMISSION 关键字，MaterialPropertyBlock 写入的自发光才会生效</summary>
        void EnsureEmissionKeyword()
        {
            var done = new HashSet<Material>();
            foreach (var part in partModel.Parts)
                foreach (var r in part.Renderers)
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null || !done.Add(m) || !m.HasProperty(EmissionId)) continue;
                        if (m.IsKeywordEnabled("_EMISSION")) continue;
                        m.EnableKeyword("_EMISSION");
                        m.SetColor(EmissionId, Color.black);
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
        }
    }
}
