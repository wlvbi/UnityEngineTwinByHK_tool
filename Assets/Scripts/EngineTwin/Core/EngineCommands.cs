using QFramework;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>选中部件（null = 取消选中）</summary>
    public class CMD_SelectPart : AbstractCommand
    {
        readonly EnginePart part;
        public CMD_SelectPart(EnginePart part) { this.part = part; }
        protected override void OnExecute() => this.GetModel<IEnginePartModel>().Selected.Value = part;
    }

    /// <summary>手动设置节气门（会退出自动工况）</summary>
    public class CMD_SetThrottle : AbstractCommand
    {
        readonly float value;
        public CMD_SetThrottle(float value) { this.value = value; }
        protected override void OnExecute()
        {
            var m = this.GetModel<IEngineTelemetryModel>();
            m.AutoCycle.Value = false;
            m.ThrottleCommand.Value = Mathf.Clamp(value, 0f, 100f);
        }
    }

    public class CMD_SetAutoCycle : AbstractCommand
    {
        readonly bool enabled;
        public CMD_SetAutoCycle(bool enabled) { this.enabled = enabled; }
        protected override void OnExecute() => this.GetModel<IEngineTelemetryModel>().AutoCycle.Value = enabled;
    }

    public class CMD_SetEngineRunning : AbstractCommand
    {
        readonly bool running;
        public CMD_SetEngineRunning(bool running) { this.running = running; }
        protected override void OnExecute() => this.GetModel<IEngineTelemetryModel>().IsRunning.Value = running;
    }

    public class CMD_SetFault : AbstractCommand
    {
        readonly EngineFault fault;
        readonly bool active;
        public CMD_SetFault(EngineFault fault, bool active) { this.fault = fault; this.active = active; }
        protected override void OnExecute() => this.GetSystem<IEngineSimulationSystem>().SetFault(fault, active);
    }

    public class CMD_ClearFaults : AbstractCommand
    {
        protected override void OnExecute() => this.GetSystem<IEngineSimulationSystem>().ClearFaults();
    }
}
