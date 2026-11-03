using System.Reflection;
using HKTools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EngineTwin
{
    /// <summary>
    /// 实时曲线卡片：内部使用 HKTools 的 HK_Curve2D（曲线绘制 + 生长动画）。
    /// HK_Curve2dDrawer 的曲线数据是私有序列化字段（设计上在 Inspector 里手动编辑），
    /// 这里通过反射把它替换为本卡片持有的 AnimationCurve，再按采样数据实时改写关键帧。
    /// </summary>
    public class HKCurveChart : MonoBehaviour
    {
        const int KeyCount = 75;   // 30 秒窗口抽样为 75 个关键帧

        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly FieldInfo CurveField = typeof(HK_Curve2dDrawer).GetField("curve", Private);
        static readonly FieldInfo LineWidthField = typeof(HK_Curve2dDrawer).GetField("lineWidth", Private);
        static readonly FieldInfo AccuracyField = typeof(HK_Curve2dDrawer).GetField("accuracy", Private);

        HK_Curve2D curve2D;
        HK_Curve2dDrawer drawer;
        AnimationCurve data;
        readonly Keyframe[] keys = new Keyframe[KeyCount];
        readonly float[] samples = new float[KeyCount];

        TextMeshProUGUI titleText, valueText, maxText, midText, minText;
        RectTransform plot, warnHighLine, warnLowLine;

        public SensorId? Sensor { get; private set; }

        public static HKCurveChart Create(Transform parent, HK_Curve2D prefab, float preferredHeight)
        {
            Image card = UIFactory.Panel(parent, "Chart", UIFactory.CardColor);
            UIFactory.Size(card, height: preferredHeight, flexibleHeight: 1);
            var chart = card.gameObject.AddComponent<HKCurveChart>();
            chart.Build(prefab);
            return chart;
        }

        void Build(HK_Curve2D prefab)
        {
            var rt = (RectTransform)transform;

            titleText = UIFactory.Label(rt, "Title", "", 17, UIFactory.TextMain);
            UIFactory.Place(titleText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(14, -6), new Vector2(-28, 26));

            valueText = UIFactory.Label(rt, "Value", "", 20, UIFactory.TextMain, TextAlignmentOptions.MidlineRight, true);
            UIFactory.Place(valueText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -6), new Vector2(-28, 26));

            plot = UIFactory.Rect("Plot", rt);
            UIFactory.Stretch(plot, 52, 14, 40, 24);

            for (int i = 0; i <= 4; i++)
            {
                Image g = UIFactory.Panel(plot, "Grid" + i, new Color(1, 1, 1, i == 0 ? 0.16f : 0.06f));
                g.raycastTarget = false;
                float y = i / 4f;
                UIFactory.Place(g.rectTransform, new Vector2(0, y), new Vector2(1, y), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 1));
            }

            maxText = AxisLabel("Max", 1f);
            midText = AxisLabel("Mid", 0.5f);
            minText = AxisLabel("Min", 0f);

            var window = UIFactory.Label(rt, "Window", "近 30 秒", 12, UIFactory.TextDim, TextAlignmentOptions.BottomRight);
            UIFactory.Place(window.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 3), new Vector2(120, 18));

            warnHighLine = ThresholdLine("WarnHigh", new Color(1f, 0.35f, 0.3f, 0.55f));
            warnLowLine = ThresholdLine("WarnLow", new Color(1f, 0.75f, 0.3f, 0.55f));

            // ---------- HKTools 曲线组件 ----------
            curve2D = Instantiate(prefab, plot);
            curve2D.name = "HK_Curve2D";
            UIFactory.Stretch((RectTransform)curve2D.transform);
            drawer = curve2D.GetComponentInChildren<HK_Curve2dDrawer>(true);
            drawer.raycastTarget = false;

            data = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
            if (CurveField != null) CurveField.SetValue(drawer, data);
            else Debug.LogError("[EngineTwin] 未找到 HK_Curve2dDrawer.curve 字段（HKTools 版本变化？），曲线无法显示实时数据");
            LineWidthField?.SetValue(drawer, 3f);
            AccuracyField?.SetValue(drawer, 6f);

            // 预制体默认贴的是虚线纹理（蚂蚁线效果），数据曲线改为实线更易读
            drawer.sprite = null;
            curve2D.PlayFlowAnim(0f);
        }

        TextMeshProUGUI AxisLabel(string name, float y)
        {
            var t = UIFactory.Label(plot, name, "", 12, UIFactory.TextDim, TextAlignmentOptions.MidlineRight);
            UIFactory.Place(t.rectTransform, new Vector2(0, y), new Vector2(0, y), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(46, 16));
            return t;
        }

        RectTransform ThresholdLine(string name, Color color)
        {
            Image img = UIFactory.Panel(plot, name, color);
            img.raycastTarget = false;
            img.gameObject.SetActive(false);
            return img.rectTransform;
        }

        static void PlaceThreshold(RectTransform line, SensorSpec spec, float threshold)
        {
            bool show = !float.IsNaN(threshold);
            line.gameObject.SetActive(show);
            if (!show) return;
            float y = spec.Normalize(threshold);
            UIFactory.Place(line, new Vector2(0, y), new Vector2(1, y), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 2));
        }

        /// <summary>切换显示的传感器；换了传感器时播放框架的生长动画</summary>
        public void Bind(SensorId id, bool animate)
        {
            bool changed = Sensor != id;
            Sensor = id;

            SensorSpec spec = SensorCatalog.Get(id);
            titleText.text = $"{spec.Name}  <size=75%><color=#8FA3B5>{spec.Unit}</color></size>";
            maxText.text = spec.Max.ToString("0.##");
            midText.text = ((spec.Min + spec.Max) * 0.5f).ToString("0.##");
            minText.text = spec.Min.ToString("0.##");
            PlaceThreshold(warnHighLine, spec, float.IsNaN(spec.WarnHigh) ? spec.AlarmHigh : spec.WarnHigh);
            PlaceThreshold(warnLowLine, spec, float.IsNaN(spec.WarnLow) ? spec.AlarmLow : spec.WarnLow);
            drawer.color = spec.Accent;

            if (changed && animate)
                curve2D.PlayShowAnim(CurveAnimDirection.FromLeft);   // HKTools 生长动画
        }

        public void Refresh(IEngineTelemetryModel telemetry)
        {
            if (Sensor == null) return;
            SensorId id = Sensor.Value;
            SensorSpec spec = SensorCatalog.Get(id);
            SensorHistory history = telemetry.GetHistory(id);
            int n = history.Count;
            if (n == 0) return;

            // 时间轴：最新采样在最右侧（t = 1），窗口未填满时左侧保持首个采样值
            int span = history.Capacity - 1;
            for (int k = 0; k < KeyCount; k++)
            {
                float u = k / (float)(KeyCount - 1);
                float index = (n - 1) - (1f - u) * span;
                samples[k] = spec.Normalize(SampleAt(history, index));
            }

            // 线性切线：关键帧之间按直线插值，曲线严格经过采样点
            for (int k = 0; k < KeyCount; k++)
            {
                float u = k / (float)(KeyCount - 1);
                float inSlope = k > 0 ? (samples[k] - samples[k - 1]) * (KeyCount - 1) : 0f;
                float outSlope = k < KeyCount - 1 ? (samples[k + 1] - samples[k]) * (KeyCount - 1) : 0f;
                keys[k] = new Keyframe(u, samples[k], inSlope, outSlope);
            }
            data.keys = keys;
            drawer.SetVerticesDirty();

            float value = telemetry.GetValue(id);
            SensorLevel level = telemetry.GetLevel(id);
            Color levelColor = level == SensorLevel.Normal ? spec.Accent : SensorCatalog.LevelColor(level);
            valueText.text = $"{spec.FormatValue(value)} <size=70%>{spec.Unit}</size>";
            valueText.color = level == SensorLevel.Normal ? UIFactory.TextMain : levelColor;
            drawer.color = levelColor;
        }

        static float SampleAt(SensorHistory h, float index)
        {
            if (index <= 0f) return h[0];
            int i0 = Mathf.FloorToInt(index);
            int i1 = Mathf.Min(i0 + 1, h.Count - 1);
            return Mathf.Lerp(h[i0], h[i1], index - i0);
        }
    }
}
