using System;

namespace EngineTwin
{
    /// <summary>每次采样（10Hz）后发送，UI 用它刷新数值与曲线</summary>
    public struct Event_TelemetrySampled
    {
    }

    /// <summary>传感器状态等级变化（正常/预警/报警）</summary>
    public struct Event_SensorLevelChanged
    {
        public SensorId Sensor;
        public SensorLevel Previous;
        public SensorLevel Current;
        public float Value;
        public float SimTime;
    }

    [Flags]
    public enum EngineFault
    {
        None = 0,
        CoolantLeak = 1 << 0,       // 冷却液泄漏 → 过热
        OilPumpWear = 1 << 1,       // 机油泵磨损 → 低油压
        AlternatorFailure = 1 << 2, // 发电机故障 → 电压下降
    }

    public struct Event_FaultsChanged
    {
        public EngineFault Faults;
    }
}
