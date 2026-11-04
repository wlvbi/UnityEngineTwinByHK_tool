using System.Collections.Generic;
using UnityEngine;

namespace EngineTwin
{
    public enum SensorId
    {
        Rpm,
        Throttle,
        IntakePressure,
        CoolantTemp,
        OilTemp,
        OilPressure,
        ExhaustTemp,
        BatteryVoltage,
        FuelPressure,
        Vibration,
    }

    public enum SensorLevel
    {
        Normal = 0,
        Warning = 1,
        Alarm = 2,
    }

    /// <summary>传感器定义：显示名、单位、曲线量程、预警/报警阈值（NaN 表示不检查）</summary>
    public sealed class SensorSpec
    {
        public readonly SensorId Id;
        public readonly string Name;
        public readonly string Unit;
        public readonly float Min, Max;
        public readonly float WarnLow, WarnHigh, AlarmLow, AlarmHigh;
        public readonly string Format;
        public readonly Color Accent;

        public SensorSpec(SensorId id, string name, string unit, float min, float max, string format, Color accent,
            float warnLow = float.NaN, float alarmLow = float.NaN, float warnHigh = float.NaN, float alarmHigh = float.NaN)
        {
            Id = id; Name = name; Unit = unit; Min = min; Max = max; Format = format; Accent = accent;
            WarnLow = warnLow; AlarmLow = alarmLow; WarnHigh = warnHigh; AlarmHigh = alarmHigh;
        }

        public string FormatValue(float v) => v.ToString(Format);

        public float Normalize(float v) => Mathf.Clamp01(Mathf.InverseLerp(Min, Max, v));

        /// <param name="checkLow">低限判断是否生效（停机时机油压力/燃油压力为 0 属正常）</param>
        /// <param name="margin">回差：阈值向「更严重」方向放宽 margin，用于判断能否从报警恢复，避免在阈值附近反复跳变</param>
        public SensorLevel Evaluate(float v, bool checkLow, float margin = 0f)
        {
            if (!float.IsNaN(AlarmHigh) && v >= AlarmHigh - margin) return SensorLevel.Alarm;
            if (checkLow && !float.IsNaN(AlarmLow) && v <= AlarmLow + margin) return SensorLevel.Alarm;
            if (!float.IsNaN(WarnHigh) && v >= WarnHigh - margin) return SensorLevel.Warning;
            if (checkLow && !float.IsNaN(WarnLow) && v <= WarnLow + margin) return SensorLevel.Warning;
            return SensorLevel.Normal;
        }

        /// <summary>回差宽度：量程的 3%</summary>
        public float Hysteresis => (Max - Min) * 0.03f;
    }

    public static class SensorCatalog
    {
        static readonly Color Cyan = new Color(0.25f, 0.80f, 1.00f);
        static readonly Color Green = new Color(0.35f, 0.90f, 0.55f);
        static readonly Color Orange = new Color(1.00f, 0.62f, 0.25f);
        static readonly Color Violet = new Color(0.70f, 0.55f, 1.00f);
        static readonly Color Yellow = new Color(1.00f, 0.85f, 0.30f);

        static readonly SensorSpec[] specs =
        {
            new SensorSpec(SensorId.Rpm,            "转速",       "rpm", 0, 7000, "F0", Cyan,   warnHigh: 6000, alarmHigh: 6500),
            new SensorSpec(SensorId.Throttle,       "节气门开度", "%",   0, 100,  "F0", Green),
            new SensorSpec(SensorId.IntakePressure, "进气压力",   "kPa", 0, 110,  "F0", Green),
            new SensorSpec(SensorId.CoolantTemp,    "冷却液温度", "°C",  0, 140,  "F1", Orange, warnHigh: 105, alarmHigh: 115),
            new SensorSpec(SensorId.OilTemp,        "机油温度",   "°C",  0, 160,  "F1", Orange, warnHigh: 125, alarmHigh: 135),
            new SensorSpec(SensorId.OilPressure,    "机油压力",   "kPa", 0, 700,  "F0", Yellow, warnLow: 150, alarmLow: 80),
            new SensorSpec(SensorId.ExhaustTemp,    "排气温度",   "°C",  0, 1000, "F0", Orange, warnHigh: 850, alarmHigh: 920),
            new SensorSpec(SensorId.BatteryVoltage, "发电电压",   "V",   10, 16,  "F2", Violet, warnLow: 13.0f, alarmLow: 12.2f, warnHigh: 14.8f, alarmHigh: 15.2f),
            new SensorSpec(SensorId.FuelPressure,   "燃油压力",   "kPa", 0, 500,  "F0", Yellow, warnLow: 320, alarmLow: 260),
            new SensorSpec(SensorId.Vibration,      "振动烈度",   "mm/s", 0, 20,  "F1", Violet, warnHigh: 7f, alarmHigh: 11f),
        };

        static readonly Dictionary<SensorId, SensorSpec> map = BuildMap();

        static Dictionary<SensorId, SensorSpec> BuildMap()
        {
            var d = new Dictionary<SensorId, SensorSpec>();
            foreach (var s in specs) d[s.Id] = s;
            return d;
        }

        public static IReadOnlyList<SensorSpec> All => specs;

        public static SensorSpec Get(SensorId id) => map[id];

        public static Color LevelColor(SensorLevel level)
        {
            switch (level)
            {
                case SensorLevel.Alarm: return new Color(1.00f, 0.30f, 0.30f);
                case SensorLevel.Warning: return new Color(1.00f, 0.76f, 0.29f);
                default: return new Color(0.45f, 0.92f, 0.60f);
            }
        }

        public static string LevelText(SensorLevel level)
        {
            switch (level)
            {
                case SensorLevel.Alarm: return "报警";
                case SensorLevel.Warning: return "预警";
                default: return "正常";
            }
        }
    }
}
