using System.Collections.Generic;
using QFramework;

namespace EngineTwin
{
    /// <summary>定长环形缓冲：保存某个传感器最近 N 个采样</summary>
    public class SensorHistory
    {
        readonly float[] buffer;
        int head;   // 下一个写入位置

        public SensorHistory(int capacity) { buffer = new float[capacity]; }

        public int Capacity => buffer.Length;
        public int Count { get; private set; }

        public void Add(float v)
        {
            buffer[head] = v;
            head = (head + 1) % buffer.Length;
            if (Count < buffer.Length) Count++;
        }

        /// <summary>按时间顺序取值：0 = 最旧，Count-1 = 最新</summary>
        public float this[int i] => buffer[(head - Count + i + buffer.Length * 2) % buffer.Length];

        public void Clear() { head = 0; Count = 0; }
    }

    public interface IEngineTelemetryModel : IModel
    {
        BindableProperty<bool> IsRunning { get; }
        BindableProperty<bool> AutoCycle { get; }
        /// <summary>节气门指令 0~100（手动模式下由界面设置，自动工况下由仿真写入）</summary>
        BindableProperty<float> ThrottleCommand { get; }

        float SampleInterval { get; }
        float SimTime { get; }

        float GetValue(SensorId id);
        SensorLevel GetLevel(SensorId id);
        SensorHistory GetHistory(SensorId id);

        /// <summary>仿真系统写入当前值（每帧）</summary>
        void SetValue(SensorId id, float value);

        /// <summary>仿真系统按采样周期调用：写入历史、判定等级、派发事件</summary>
        /// <param name="checkLowLimits">是否判定低限（停机或刚起动时油压/燃油压力/电压偏低属正常）</param>
        void Sample(float simTime, bool checkLowLimits);
    }

    public class EngineTelemetryModel : AbstractModel, IEngineTelemetryModel
    {
        public const int HistoryCapacity = 300;   // 300 × 0.1s = 30 秒窗口

        public BindableProperty<bool> IsRunning { get; } = new BindableProperty<bool>(true);
        public BindableProperty<bool> AutoCycle { get; } = new BindableProperty<bool>(true);
        public BindableProperty<float> ThrottleCommand { get; } = new BindableProperty<float>(0f);

        public float SampleInterval => 0.1f;
        public float SimTime { get; private set; }

        readonly Dictionary<SensorId, float> values = new Dictionary<SensorId, float>();
        readonly Dictionary<SensorId, SensorLevel> levels = new Dictionary<SensorId, SensorLevel>();
        readonly Dictionary<SensorId, SensorHistory> histories = new Dictionary<SensorId, SensorHistory>();

        protected override void OnInit()
        {
            foreach (var spec in SensorCatalog.All)
            {
                values[spec.Id] = 0f;
                levels[spec.Id] = SensorLevel.Normal;
                histories[spec.Id] = new SensorHistory(HistoryCapacity);
            }
        }

        public float GetValue(SensorId id) => values[id];
        public SensorLevel GetLevel(SensorId id) => levels[id];
        public SensorHistory GetHistory(SensorId id) => histories[id];

        public void SetValue(SensorId id, float value) => values[id] = value;

        public void Sample(float simTime, bool checkLowLimits)
        {
            SimTime = simTime;
            bool checkLow = checkLowLimits;

            foreach (var spec in SensorCatalog.All)
            {
                float v = values[spec.Id];
                histories[spec.Id].Add(v);

                SensorLevel before = levels[spec.Id];
                SensorLevel now = spec.Evaluate(v, checkLow);
                // 等级降低（好转）时要求越过回差带，防止在阈值附近来回报警
                if (now < before && checkLow == lastCheckLow)
                {
                    SensorLevel sticky = spec.Evaluate(v, checkLow, spec.Hysteresis);
                    if (sticky > now) now = sticky < before ? sticky : before;
                }
                if (now == before) continue;

                levels[spec.Id] = now;
                this.SendEvent(new Event_SensorLevelChanged
                {
                    Sensor = spec.Id, Previous = before, Current = now, Value = v, SimTime = simTime,
                });
            }

            this.SendEvent<Event_TelemetrySampled>();
            lastCheckLow = checkLow;
        }

        bool lastCheckLow;
    }
}
