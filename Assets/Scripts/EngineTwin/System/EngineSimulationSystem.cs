using QFramework;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>
    /// 发动机仿真系统：用一阶惯性模型生成各传感器数据（暖机、负载、故障注入）。
    /// 接入真实设备时，用读取 PLC / MQTT / 串口数据的系统替换本系统即可，界面与标签层无需改动。
    /// </summary>
    public interface IEngineSimulationSystem : ISystem
    {
        EngineFault ActiveFaults { get; }
        void Tick(float deltaTime);
        void SetFault(EngineFault fault, bool active);
        void ClearFaults();
    }

    public class EngineSimulationSystem : AbstractSystem, IEngineSimulationSystem
    {
        const float Ambient = 25f;
        const float StartupGraceSeconds = 4f;   // 起动后油压/燃油压力建立需要时间，期间不判定低限

        IEngineTelemetryModel model;
        float simTime, sampleTimer, batteryDrain, runningTime;

        // 物理状态（未加噪声）
        float throttle, rpm, map = 101f, coolant = Ambient, oilTemp = Ambient, oilPressure, egt = Ambient, voltage = 12.6f, fuelPressure;

        public EngineFault ActiveFaults { get; private set; }

        protected override void OnInit()
        {
            model = this.GetModel<IEngineTelemetryModel>();
            WriteValues();
        }

        public void SetFault(EngineFault fault, bool active)
        {
            EngineFault next = active ? ActiveFaults | fault : ActiveFaults & ~fault;
            if (next == ActiveFaults) return;
            if ((fault & EngineFault.AlternatorFailure) != 0 && !active) batteryDrain = 0f;
            ActiveFaults = next;
            this.SendEvent(new Event_FaultsChanged { Faults = ActiveFaults });
        }

        public void ClearFaults()
        {
            if (ActiveFaults == EngineFault.None) return;
            ActiveFaults = EngineFault.None;
            batteryDrain = 0f;
            this.SendEvent(new Event_FaultsChanged { Faults = ActiveFaults });
        }

        public void Tick(float deltaTime)
        {
            float dt = Mathf.Min(deltaTime, 0.1f);
            if (dt <= 0f) return;
            simTime += dt;

            bool running = model.IsRunning.Value;
            runningTime = running ? runningTime + dt : 0f;

            float throttleCmd;
            if (running && model.AutoCycle.Value)
            {
                throttleCmd = AutoThrottle(simTime);
                model.ThrottleCommand.Value = Mathf.Round(throttleCmd);
            }
            else
            {
                throttleCmd = running ? model.ThrottleCommand.Value : 0f;
            }

            throttle = Lag(throttle, throttleCmd, dt, 0.12f);
            float load = throttle / 100f;

            float rpmTarget = running ? 780f + 5400f * Mathf.Pow(load, 1.15f) : 0f;
            rpm = Lag(rpm, rpmTarget, dt, running ? (rpmTarget > rpm ? 0.45f : 0.7f) : 1.2f);

            float mapTarget = running ? Mathf.Lerp(32f, 99f, Mathf.Pow(load, 0.7f)) : 101f;
            map = Lag(map, mapTarget, dt, 0.08f);

            float coolantTarget = running ? 88f + 10f * load + rpm / 6500f * 4f : Ambient;
            if (running && Has(EngineFault.CoolantLeak)) coolantTarget += 48f;
            coolant = Lag(coolant, coolantTarget, dt, running ? 22f : 90f);

            float oilTarget = running ? coolant + 6f + 18f * load : Ambient;
            oilTemp = Lag(oilTemp, oilTarget, dt, 30f);

            float oilPressureTarget = 0f;
            if (running)
            {
                oilPressureTarget = 120f + rpm * 0.075f + Mathf.Max(0f, 80f - oilTemp) * 1.5f - Mathf.Max(0f, oilTemp - 100f) * 1.2f;
                if (Has(EngineFault.OilPumpWear)) oilPressureTarget *= 0.4f;
            }
            oilPressure = Lag(oilPressure, oilPressureTarget, dt, 0.25f);

            float egtTarget = running ? 330f + 470f * load + rpm * 0.025f : Ambient;
            egt = Lag(egt, egtTarget, dt, running ? 1.6f : 25f);

            float voltageTarget = running && rpm > 500f ? 14.25f - 0.15f * load : 12.6f;
            if (running && Has(EngineFault.AlternatorFailure))
            {
                batteryDrain = Mathf.Min(batteryDrain + dt * 0.012f, 1.2f);
                voltageTarget = 12.4f - batteryDrain;
            }
            voltage = Lag(voltage, voltageTarget, dt, 0.6f);

            fuelPressure = Lag(fuelPressure, running ? 385f - 18f * load : 0f, dt, running ? 0.3f : 4f);

            WriteValues();

            sampleTimer += dt;
            if (sampleTimer >= model.SampleInterval)
            {
                sampleTimer -= model.SampleInterval;
                model.Sample(simTime, running && runningTime > StartupGraceSeconds);
            }
        }

        bool Has(EngineFault f) => (ActiveFaults & f) != 0;

        void WriteValues()
        {
            bool running = model.IsRunning.Value;
            float n = running ? 1f : 0.2f;
            model.SetValue(SensorId.Throttle, throttle);
            model.SetValue(SensorId.Rpm, Mathf.Max(0f, rpm + Noise(1, 12f * n)));
            model.SetValue(SensorId.IntakePressure, map + Noise(2, 0.6f * n));
            model.SetValue(SensorId.CoolantTemp, coolant + Noise(3, 0.15f));
            model.SetValue(SensorId.OilTemp, oilTemp + Noise(4, 0.15f));
            model.SetValue(SensorId.OilPressure, Mathf.Max(0f, oilPressure + Noise(5, 4f * n)));
            model.SetValue(SensorId.ExhaustTemp, egt + Noise(6, 3f * n));
            model.SetValue(SensorId.BatteryVoltage, voltage + Noise(7, 0.02f));
            model.SetValue(SensorId.FuelPressure, Mathf.Max(0f, fuelPressure + Noise(8, 3f * n)));
        }

        /// <summary>平滑随机噪声（Perlin），避免数值每帧乱跳</summary>
        float Noise(int channel, float amplitude) =>
            (Mathf.PerlinNoise(simTime * 2.7f, channel * 7.31f) - 0.5f) * 2f * amplitude;

        /// <summary>自动工况：怠速、巡航、急加速交替的模拟驾驶循环</summary>
        static float AutoThrottle(float t)
        {
            float cruise = Mathf.Pow(Mathf.PerlinNoise(t * 0.07f, 0.37f), 1.6f) * 95f;
            float burst = Mathf.Max(0f, Mathf.Sin(t * 0.45f));
            return Mathf.Clamp(8f + cruise + 22f * burst * burst * burst, 0f, 100f);
        }

        /// <summary>一阶惯性：以时间常数 tau 逼近目标值</summary>
        static float Lag(float current, float target, float dt, float tau) =>
            current + (target - current) * (1f - Mathf.Exp(-dt / Mathf.Max(tau, 1e-4f)));
    }
}
