using System.Collections.Generic;
using HKTools;
using QFramework;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>运行时部件：目录信息 + 场景中的渲染器/碰撞体 + 对应的 HK_Tips 标签</summary>
    public class EnginePart
    {
        public readonly EnginePartInfo Info;
        public readonly List<Renderer> Renderers = new List<Renderer>();
        public readonly List<Collider> Colliders = new List<Collider>();
        public Bounds Bounds;

        /// <summary>HKTools 悬浮提示标签（由 EngineTipPresenter 生成）</summary>
        public HK_TipsCtrl Tip;

        public EnginePart(EnginePartInfo info) { Info = info; }

        public string Id => Info.id;
        public string DisplayName => Info.displayName;
        public bool HasSensors => Info.sensors != null && Info.sensors.Length > 0;

        /// <summary>部件状态 = 绑定传感器中最严重的等级</summary>
        public SensorLevel GetLevel(IEngineTelemetryModel telemetry)
        {
            SensorLevel worst = SensorLevel.Normal;
            if (!HasSensors) return worst;
            foreach (var s in Info.sensors)
            {
                var l = telemetry.GetLevel(s);
                if (l > worst) worst = l;
            }
            return worst;
        }
    }

    public interface IEnginePartModel : IModel
    {
        IReadOnlyList<EnginePart> Parts { get; }
        BindableProperty<EnginePart> Selected { get; }
        BindableProperty<EnginePart> Hovered { get; }

        void SetParts(List<EnginePart> parts);
        EnginePart FindByCollider(Collider collider);
        EnginePart FindByTip(HK_TipsCtrl tip);
        EnginePart FindFirstWithSensor(SensorId sensor);
    }

    public class EnginePartModel : AbstractModel, IEnginePartModel
    {
        readonly List<EnginePart> parts = new List<EnginePart>();
        readonly Dictionary<Collider, EnginePart> byCollider = new Dictionary<Collider, EnginePart>();

        public IReadOnlyList<EnginePart> Parts => parts;
        public BindableProperty<EnginePart> Selected { get; } = new BindableProperty<EnginePart>();
        public BindableProperty<EnginePart> Hovered { get; } = new BindableProperty<EnginePart>();

        protected override void OnInit() { }

        public void SetParts(List<EnginePart> newParts)
        {
            parts.Clear();
            byCollider.Clear();
            parts.AddRange(newParts);
            foreach (var p in parts)
                foreach (var c in p.Colliders)
                    byCollider[c] = p;
        }

        public EnginePart FindByCollider(Collider collider)
        {
            if (collider == null) return null;
            return byCollider.TryGetValue(collider, out var p) ? p : null;
        }

        public EnginePart FindByTip(HK_TipsCtrl tip)
        {
            foreach (var p in parts)
                if (p.Tip == tip) return p;
            return null;
        }

        public EnginePart FindFirstWithSensor(SensorId sensor)
        {
            foreach (var p in parts)
                if (p.HasSensors && System.Array.IndexOf(p.Info.sensors, sensor) >= 0) return p;
            return null;
        }
    }
}
