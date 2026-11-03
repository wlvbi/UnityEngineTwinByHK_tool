using System.Collections.Generic;
using System.Text;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EngineTwin
{
    /// <summary>数字孪生监控面板（运行时构建 UGUI）</summary>
    public class EngineDashboard : EngineTwinController
    {
        const int MaxLogLines = 9;
        static readonly SensorId[] OverviewSensors = { SensorId.Rpm, SensorId.CoolantTemp, SensorId.OilPressure };

        IEngineTelemetryModel telemetry;
        IEnginePartModel partModel;
        IEngineSimulationSystem simulation;
        EngineTipPresenter tipPresenter;
        HK_Curve2D curvePrefab;

        readonly Dictionary<SensorId, TextMeshProUGUI> rowValues = new Dictionary<SensorId, TextMeshProUGUI>();
        readonly Dictionary<SensorId, Image> rowDots = new Dictionary<SensorId, Image>();
        readonly Dictionary<EngineFault, Image> faultButtons = new Dictionary<EngineFault, Image>();
        readonly List<HKCurveChart> charts = new List<HKCurveChart>();
        readonly LinkedList<string> logLines = new LinkedList<string>();

        TextMeshProUGUI statusText, partTitle, partStatus, partDesc, throttleValue, runLabel, logText;
        Slider throttleSlider;
        Toggle autoToggle;

        public void Build(HK_Curve2D curve2DPrefab, EngineTipPresenter tips)
        {
            curvePrefab = curve2DPrefab;
            tipPresenter = tips;
            telemetry = this.GetModel<IEngineTelemetryModel>();
            partModel = this.GetModel<IEnginePartModel>();
            simulation = this.GetSystem<IEngineSimulationSystem>();

            EnsureEventSystem();

            var canvasGo = new GameObject("EngineTwin_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasGo.transform;
            BuildTopBar(root);
            BuildSensorPanel(root);
            BuildLogPanel(root);
            BuildPartPanel(root);
            BuildControlBar(root);

            // ---------- 数据绑定 ----------
            this.RegisterEvent<Event_TelemetrySampled>(_ => OnSampled()).UnRegisterWhenGameObjectDestroyed(gameObject);
            this.RegisterEvent<Event_SensorLevelChanged>(OnLevelChanged).UnRegisterWhenGameObjectDestroyed(gameObject);
            this.RegisterEvent<Event_FaultsChanged>(e => RefreshFaultButtons(e.Faults)).UnRegisterWhenGameObjectDestroyed(gameObject);

            partModel.Selected.RegisterWithInitValue(OnPartSelected).UnRegisterWhenGameObjectDestroyed(gameObject);
            telemetry.IsRunning.RegisterWithInitValue(r => runLabel.text = r ? "停机" : "启动").UnRegisterWhenGameObjectDestroyed(gameObject);
            telemetry.AutoCycle.RegisterWithInitValue(a => autoToggle.SetIsOnWithoutNotify(a)).UnRegisterWhenGameObjectDestroyed(gameObject);
            telemetry.ThrottleCommand.RegisterWithInitValue(v =>
            {
                throttleSlider.SetValueWithoutNotify(v);
                throttleValue.text = $"{v:0}%";
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

            RefreshFaultButtons(simulation.ActiveFaults);
            AddLog("系统启动，数字孪生已连接（仿真数据源）", UIFactory.TextDim);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // ================= 布局 =================

        void BuildTopBar(Transform root)
        {
            Image bar = UIFactory.Panel(root, "TopBar", UIFactory.PanelColor);
            UIFactory.Place(bar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 60));

            var title = UIFactory.Label(bar.transform, "Title", "V8 发动机数字孪生", 26, UIFactory.TextMain, bold: true);
            UIFactory.Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 2), new Vector2(300, 40));

            var sub = UIFactory.Label(bar.transform, "Sub", "HKTools · HK_Tips 部件提示 · HK_Curve2D 实时曲线", 14, UIFactory.TextDim);
            UIFactory.Place(sub.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(270, 0), new Vector2(420, 30));

            var hint = UIFactory.Label(bar.transform, "Hint", "左键旋转 · 右键平移 · 滚轮缩放 · 点击部件或标签查看 · R 复位视角", 14, UIFactory.TextDim, TextAlignmentOptions.Center);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120, 0), new Vector2(560, 30));

            statusText = UIFactory.Label(bar.transform, "Status", "", 17, UIFactory.TextMain, TextAlignmentOptions.MidlineRight);
            UIFactory.Place(statusText.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(560, 40));
        }

        void BuildSensorPanel(Transform root)
        {
            Image panel = UIFactory.Panel(root, "SensorPanel", UIFactory.PanelColor);
            UIFactory.Place(panel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -76), new Vector2(340, 50 + SensorCatalog.All.Count * 40 + 12));
            UIFactory.VLayout(panel.gameObject, 2, new RectOffset(14, 14, 10, 12));

            var header = UIFactory.Label(panel.transform, "Header", "实时数据", 19, UIFactory.Accent, bold: true);
            UIFactory.Size(header, height: 36);

            foreach (var spec in SensorCatalog.All)
            {
                SensorId id = spec.Id;
                Image row = UIFactory.Panel(panel.transform, "Row_" + id, new Color(1, 1, 1, 0.03f));
                UIFactory.Size(row, height: 38);
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row;
                btn.onClick.AddListener(() =>
                {
                    EnginePart p = partModel.FindFirstWithSensor(id);
                    if (p != null) this.SendCommand(new CMD_SelectPart(p));
                });

                Image dot = UIFactory.Panel(row.transform, "Dot", SensorCatalog.LevelColor(SensorLevel.Normal));
                UIFactory.Place(dot.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(8, 8));
                rowDots[id] = dot;

                var name = UIFactory.Label(row.transform, "Name", spec.Name, 17, UIFactory.TextMain);
                UIFactory.Stretch(name.rectTransform, 26, 120);

                var value = UIFactory.Label(row.transform, "Value", "-", 18, UIFactory.TextMain, TextAlignmentOptions.MidlineRight, true);
                UIFactory.Stretch(value.rectTransform, 140, 10);
                rowValues[id] = value;
            }
        }

        void BuildLogPanel(Transform root)
        {
            float top = 76 + 50 + SensorCatalog.All.Count * 40 + 12 + 12;
            Image panel = UIFactory.Panel(root, "LogPanel", UIFactory.PanelColor);
            panel.rectTransform.anchorMin = new Vector2(0, 0);
            panel.rectTransform.anchorMax = new Vector2(0, 1);
            panel.rectTransform.pivot = new Vector2(0, 1);
            panel.rectTransform.offsetMin = new Vector2(16, 108);
            panel.rectTransform.offsetMax = new Vector2(16 + 340, -top);

            var header = UIFactory.Label(panel.transform, "Header", "事件记录", 19, UIFactory.Accent, bold: true);
            UIFactory.Place(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(14, -8), new Vector2(-28, 30));

            logText = UIFactory.Label(panel.transform, "Log", "", 14, UIFactory.TextMain, TextAlignmentOptions.TopLeft);
            logText.textWrappingMode = TextWrappingModes.Normal;
            logText.overflowMode = TextOverflowModes.Truncate;
            logText.lineSpacing = 6;
            UIFactory.Stretch(logText.rectTransform, 14, 12, 44, 10);
        }

        void BuildPartPanel(Transform root)
        {
            Image panel = UIFactory.Panel(root, "PartPanel", UIFactory.PanelColor);
            panel.rectTransform.anchorMin = new Vector2(1, 0);
            panel.rectTransform.anchorMax = new Vector2(1, 1);
            panel.rectTransform.pivot = new Vector2(1, 1);
            panel.rectTransform.offsetMin = new Vector2(-16 - 480, 108);
            panel.rectTransform.offsetMax = new Vector2(-16, -76);
            UIFactory.VLayout(panel.gameObject, 8, new RectOffset(16, 16, 12, 16));

            partTitle = UIFactory.Label(panel.transform, "PartTitle", "", 24, UIFactory.TextMain, bold: true);
            UIFactory.Size(partTitle, height: 34);

            partStatus = UIFactory.Label(panel.transform, "PartStatus", "", 16, UIFactory.TextMain);
            UIFactory.Size(partStatus, height: 24);

            partDesc = UIFactory.Label(panel.transform, "PartDesc", "", 15, UIFactory.TextDim, TextAlignmentOptions.TopLeft);
            partDesc.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Size(partDesc, height: 46);

            for (int i = 0; i < 3; i++)
                charts.Add(HKCurveChart.Create(panel.transform, curvePrefab, 180));
        }

        void BuildControlBar(Transform root)
        {
            Image bar = UIFactory.Panel(root, "ControlBar", UIFactory.PanelColor);
            UIFactory.Place(bar.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(-32, 80));
            bar.rectTransform.anchoredPosition = new Vector2(0, 16);
            UIFactory.HLayout(bar.gameObject, 12, new RectOffset(18, 18, 0, 0));
            Transform t = bar.transform;

            var runBtn = UIFactory.Button(t, "RunButton", "停机", () => this.SendCommand(new CMD_SetEngineRunning(!telemetry.IsRunning.Value)), 96);
            runLabel = runBtn.GetComponentInChildren<TextMeshProUGUI>();

            autoToggle = UIFactory.Toggle(t, "AutoCycle", "自动工况", true, on => this.SendCommand(new CMD_SetAutoCycle(on)), 120);

            var thrLabel = UIFactory.Label(t, "ThrottleLabel", "节气门", 17, UIFactory.TextMain);
            UIFactory.Size(thrLabel, 58, 40);
            throttleSlider = UIFactory.Slider(t, "Throttle", 220, 0, 100, v => this.SendCommand(new CMD_SetThrottle(v)));
            throttleValue = UIFactory.Label(t, "ThrottleValue", "0%", 17, UIFactory.Accent, TextAlignmentOptions.MidlineLeft, true);
            UIFactory.Size(throttleValue, 54, 40);

            Separator(t);
            FaultButton(t, "冷却液泄漏", EngineFault.CoolantLeak);
            FaultButton(t, "机油泵磨损", EngineFault.OilPumpWear);
            FaultButton(t, "发电机故障", EngineFault.AlternatorFailure);
            UIFactory.Button(t, "ClearFaults", "清除故障", () => this.SendCommand(new CMD_ClearFaults()), 100);

            Separator(t);
            UIFactory.Button(t, "TipsKey", "关键标签", () => tipPresenter.ShowKeyTipsOnly(), 100);
            UIFactory.Button(t, "TipsAll", "全部标签", () => tipPresenter.ShowAllTips(), 100);
            UIFactory.Button(t, "TipsHide", "隐藏标签", () => tipPresenter.HideAllTips(), 100);
        }

        void FaultButton(Transform parent, string label, EngineFault fault)
        {
            var btn = UIFactory.Button(parent, "Fault_" + fault, label, () =>
            {
                bool active = (simulation.ActiveFaults & fault) != 0;
                this.SendCommand(new CMD_SetFault(fault, !active));
            }, 118);
            faultButtons[fault] = (Image)btn.targetGraphic;
        }

        static void Separator(Transform parent)
        {
            Image sep = UIFactory.Panel(parent, "Separator", new Color(1, 1, 1, 0.12f));
            UIFactory.Size(sep, 2, 44);
        }

        // ================= 刷新 =================

        void OnSampled()
        {
            foreach (var spec in SensorCatalog.All)
            {
                SensorLevel level = telemetry.GetLevel(spec.Id);
                Color c = SensorCatalog.LevelColor(level);
                rowDots[spec.Id].color = c;
                rowValues[spec.Id].text = $"{spec.FormatValue(telemetry.GetValue(spec.Id))} <size=72%><color=#8FA3B5>{spec.Unit}</color></size>";
                rowValues[spec.Id].color = level == SensorLevel.Normal ? UIFactory.TextMain : c;
            }

            foreach (var chart in charts) chart.Refresh(telemetry);

            RefreshStatus();
            RefreshPartStatus(partModel.Selected.Value);
        }

        void RefreshStatus()
        {
            bool running = telemetry.IsRunning.Value;
            int seconds = Mathf.FloorToInt(telemetry.SimTime);

            SensorLevel worst = SensorLevel.Normal;
            foreach (var spec in SensorCatalog.All)
            {
                var l = telemetry.GetLevel(spec.Id);
                if (l > worst) worst = l;
            }

            string runHex = running ? "5BE38C" : "8FA3B5";
            string levelHex = ColorUtility.ToHtmlStringRGB(SensorCatalog.LevelColor(worst));
            statusText.text =
                $"<color=#{runHex}>● {(running ? "运行中" : "已停机")}</color>    " +
                $"仿真时间 {seconds / 60:00}:{seconds % 60:00}    " +
                $"整机状态 <color=#{levelHex}>{SensorCatalog.LevelText(worst)}</color>    " +
                $"故障注入 {FaultText(simulation.ActiveFaults)}";
        }

        static string FaultText(EngineFault faults)
        {
            if (faults == EngineFault.None) return "<color=#8FA3B5>无</color>";
            var sb = new StringBuilder("<color=#FF6B6B>");
            if ((faults & EngineFault.CoolantLeak) != 0) sb.Append("冷却液泄漏 ");
            if ((faults & EngineFault.OilPumpWear) != 0) sb.Append("机油泵磨损 ");
            if ((faults & EngineFault.AlternatorFailure) != 0) sb.Append("发电机故障 ");
            return sb.ToString().TrimEnd() + "</color>";
        }

        void OnPartSelected(EnginePart part)
        {
            var sensors = new List<SensorId>();
            if (part == null)
            {
                partTitle.text = "整机概览";
                partDesc.text = "点击模型上的部件、悬浮标签或左侧传感器，查看部件详情与实时曲线。";
            }
            else
            {
                partTitle.text = part.DisplayName;
                partDesc.text = part.Info.description;
                if (part.HasSensors) sensors.AddRange(part.Info.sensors);
            }

            foreach (var s in OverviewSensors)
                if (sensors.Count < charts.Count && !sensors.Contains(s)) sensors.Add(s);

            for (int i = 0; i < charts.Count; i++)
            {
                charts[i].Bind(sensors[i], animate: true);
                charts[i].Refresh(telemetry);
            }

            RefreshPartStatus(part);
        }

        void RefreshPartStatus(EnginePart part)
        {
            if (part == null)
            {
                partStatus.text = $"部件 {partModel.Parts.Count} 个 · 传感器 {SensorCatalog.All.Count} 路 · 采样 10 Hz";
                partStatus.color = UIFactory.TextDim;
                return;
            }

            SensorLevel level = part.GetLevel(telemetry);
            string hex = ColorUtility.ToHtmlStringRGB(SensorCatalog.LevelColor(level));
            partStatus.text = part.HasSensors
                ? $"状态 <color=#{hex}>{SensorCatalog.LevelText(level)}</color>   ·   监测 {part.Info.sensors.Length} 路传感器"
                : "状态 <color=#8FA3B5>无传感器（结构件）</color>";
            partStatus.color = UIFactory.TextMain;
        }

        void RefreshFaultButtons(EngineFault faults)
        {
            foreach (var kv in faultButtons)
                kv.Value.color = (faults & kv.Key) != 0 ? new Color(0.55f, 0.12f, 0.12f, 1f) : UIFactory.ButtonColor;
        }

        void OnLevelChanged(Event_SensorLevelChanged e)
        {
            SensorSpec spec = SensorCatalog.Get(e.Sensor);
            string text = e.Current == SensorLevel.Normal
                ? $"{spec.Name}恢复正常 {spec.FormatValue(e.Value)} {spec.Unit}"
                : $"{spec.Name}{SensorCatalog.LevelText(e.Current)} {spec.FormatValue(e.Value)} {spec.Unit}";
            AddLog(text, SensorCatalog.LevelColor(e.Current), e.SimTime);
        }

        void AddLog(string message, Color color, float simTime = 0f)
        {
            int s = Mathf.FloorToInt(simTime);
            string hex = ColorUtility.ToHtmlStringRGB(color);
            logLines.AddFirst($"<color=#8FA3B5>{s / 60:00}:{s % 60:00}</color>  <color=#{hex}>{message}</color>");
            while (logLines.Count > MaxLogLines) logLines.RemoveLast();
            logText.text = string.Join("\n", logLines);
        }
    }
}
