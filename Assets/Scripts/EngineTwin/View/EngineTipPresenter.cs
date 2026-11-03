using System.Collections.Generic;
using HKTools;
using QFramework;
using TMPro;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>
    /// 部件提示：为每个部件生成一个 HKTools 的 HK_Tip 悬浮标签，
    /// 通过框架接口刷新实时数值（SetText）、按分组显隐（CMD_TIP_*），并响应框架的点击事件（Event_TIP_OnClick）。
    /// </summary>
    public class EngineTipPresenter : EngineTwinController
    {
        const float RefreshInterval = 0.25f;

        static readonly Color NormalValueColor = new Color(0.62f, 0.90f, 1.00f);

        IEnginePartModel partModel;
        IEngineTelemetryModel telemetry;
        readonly Dictionary<EnginePart, string> lastText = new Dictionary<EnginePart, string>();
        float refreshTimer;

        public void Spawn(HK_TipsCtrl tipPrefab, Transform engineRoot, Vector3 engineCenter, TMP_FontAsset font)
        {
            partModel = this.GetModel<IEnginePartModel>();
            telemetry = this.GetModel<IEngineTelemetryModel>();

            // 先在未激活的父节点下实例化：此时 HK_TipsCtrl.Awake 还没执行，
            // 摆好折点后再移到激活节点下，Awake 会自动注册到 IHK_TipSystem 并按新折点初始化
            var staging = new GameObject("TipStaging");
            staging.SetActive(false);
            staging.transform.SetParent(transform, false);

            foreach (var part in partModel.Parts)
            {
                HK_TipsCtrl tip = Instantiate(tipPrefab, staging.transform);
                tip.name = "Tip_" + part.Id;
                tip.tipGroup = part.Info.keyPart ? HK_TipGroup.TipGroup01 : HK_TipGroup.TipGroup02;

                Layout(tip, part, engineRoot, engineCenter);

                if (font != null)
                {
                    var tmp = tip.GetComponentInChildren<TMP_Text>(true);
                    if (tmp != null) tmp.font = font;
                }

                string text = BuildLabel(part);
                tip.SetText(text);
                lastText[part] = text;

                tip.transform.SetParent(transform, true);
                part.Tip = tip;
            }
            Destroy(staging);

            ShowKeyTipsOnly();

            // 框架事件：点击标签 → 选中对应部件
            this.RegisterEvent<Event_TIP_OnClick>(e =>
            {
                EnginePart part = partModel.FindByTip(e._tip);
                if (part != null) this.SendCommand(new CMD_SelectPart(part));
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

            // 选中非关键部件时，单独把它的标签显示出来
            partModel.Selected.Register(part =>
            {
                if (part != null && part.Tip != null && !part.Info.keyPart)
                    this.SendCommand(new CMD_TIP_ShowTips(new List<HK_TipsCtrl> { part.Tip }));
            }).UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        // ---------- 对外：标签显示模式（全部走 HKTools 指令） ----------

        public void ShowKeyTipsOnly()
        {
            this.SendCommand(new CMD_TIP_ShowTipsByGroup(HK_TipGroup.TipGroup01));
            this.SendCommand(new CMD_TIP_HideTipsByGroup(HK_TipGroup.TipGroup02));
        }

        public void ShowAllTips() => this.SendCommand(new CMD_TIP_ShowAllHKTips());

        public void HideAllTips() => this.SendCommand(new CMD_TIP_HideAllHKTips());

        // ---------- 实时刷新 ----------

        void Update()
        {
            if (partModel == null) return;

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < RefreshInterval) return;
            refreshTimer = 0f;

            foreach (var part in partModel.Parts)
            {
                if (part.Tip == null || !part.HasSensors) continue;
                string text = BuildLabel(part);
                if (lastText.TryGetValue(part, out var old) && old == text) continue;
                lastText[part] = text;
                part.Tip.SetText(text);   // HKTools 接口
            }
        }

        string BuildLabel(EnginePart part)
        {
            if (!part.HasSensors) return part.DisplayName;

            SensorSpec spec = SensorCatalog.Get(part.Info.sensors[0]);
            SensorLevel level = telemetry.GetLevel(spec.Id);
            Color c = level == SensorLevel.Normal ? NormalValueColor : SensorCatalog.LevelColor(level);
            string hex = ColorUtility.ToHtmlStringRGB(c);
            string value = spec.FormatValue(telemetry.GetValue(spec.Id));
            return $"{part.DisplayName}\n<size=78%><color=#{hex}>{value} {spec.Unit}</color></size>";
        }

        // ---------- 标签摆放 ----------

        static void Layout(HK_TipsCtrl tip, EnginePart part, Transform engineRoot, Vector3 engineCenter)
        {
            Vector3 dir = part.Info.tipDirection.sqrMagnitude > 1e-4f
                ? engineRoot.TransformDirection(part.Info.tipDirection)
                : part.Bounds.center - engineCenter + Vector3.up * 0.3f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
            dir.Normalize();

            // 取部件中最靠「引出方向」一侧的子网格，再用射线求它的表面点作为锚点
            Renderer best = part.Renderers[0];
            float bestDot = float.MinValue;
            foreach (var r in part.Renderers)
            {
                float d = Vector3.Dot(r.bounds.center - engineCenter, dir);
                if (d > bestDot) { bestDot = d; best = r; }
            }

            Vector3 target = best.bounds.center;
            Vector3 anchor = target;
            var ray = new Ray(target + dir * 1.5f, -dir);
            float nearest = float.MaxValue;
            foreach (var c in part.Colliders)
            {
                if (c.Raycast(ray, out RaycastHit hit, 3f) && hit.distance < nearest)
                {
                    nearest = hit.distance;
                    anchor = hit.point;
                }
            }

            tip.transform.SetPositionAndRotation(anchor, Quaternion.identity);

            Transform line = tip.transform.Find("Line");
            if (line == null) return;
            SetPoint(line, "Pos01", 0, anchor);
            SetPoint(line, "Pos02", 1, anchor + dir * 0.09f);
            SetPoint(line, "Pos03", 2, anchor + dir * 0.13f + Vector3.up * 0.09f);
        }

        static void SetPoint(Transform line, string name, int fallbackIndex, Vector3 worldPos)
        {
            Transform t = line.Find(name);
            if (t == null && fallbackIndex < line.childCount) t = line.GetChild(fallbackIndex);
            if (t != null) t.position = worldPos;
        }
    }
}
