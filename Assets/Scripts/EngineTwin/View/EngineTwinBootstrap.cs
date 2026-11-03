using HKTools;
using QFramework;
using TMPro;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>
    /// 数字孪生入口：挂在场景的 EngineTwin 物体上（由菜单 EngineTwin/2. 一键搭建数字孪生场景 自动创建）。
    /// 负责组装：部件归类 → HK_Tips 标签 → 高亮/拾取 → 监控面板 → 每帧驱动仿真。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class EngineTwinBootstrap : EngineTwinController
    {
        [Header("场景引用")]
        [SerializeField] Transform engineRoot;
        [SerializeField] Camera viewCamera;

        [Header("数据")]
        [Tooltip("留空则使用代码内置的默认部件目录")]
        [SerializeField] EnginePartCatalog partCatalog;

        [Header("HKTools 框架资源")]
        [SerializeField] HK_TipsCtrl tipPrefab;
        [SerializeField] HK_Curve2D curvePrefab;
        [SerializeField] TMP_FontAsset uiFont;

        IEngineSimulationSystem simulation;

        void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (!Validate())
            {
                enabled = false;
                return;
            }

            TMP_FontAsset font = RuntimeFonts.CreateFrom(uiFont);
            UIFactory.Font = font;

            // HK_Tips 需要 HK_TipCamera 标记来朝向相机、按距离缩放与显隐
            if (!viewCamera.TryGetComponent<HK_TipCamera>(out _))
                viewCamera.gameObject.AddComponent<HK_TipCamera>();

            EnginePartCatalog catalog = partCatalog != null ? partCatalog : EnginePartCatalog.CreateDefault();
            var parts = EnginePartRegistry.Build(engineRoot, catalog, out int unmatched);
            var partModel = this.GetModel<IEnginePartModel>();
            partModel.SetParts(parts);
            Debug.Log($"[EngineTwin] 已归类部件 {parts.Count} 个，未归类网格 {unmatched} 个");

            Vector3 center = EnginePartRegistry.ComputeCenter(parts);

            if (!viewCamera.TryGetComponent(out EngineOrbitCamera orbit))
                orbit = viewCamera.gameObject.AddComponent<EngineOrbitCamera>();
            orbit.Init(center);

            var tips = gameObject.AddComponent<EngineTipPresenter>();
            tips.Spawn(tipPrefab, engineRoot, center, font);

            gameObject.AddComponent<EngineHighlighter>().Init();
            gameObject.AddComponent<EnginePicker>().Init(viewCamera);
            gameObject.AddComponent<EngineDashboard>().Build(curvePrefab, tips);

            partModel.Selected.Register(p =>
            {
                if (p != null) orbit.Focus(p.Bounds);
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

            simulation = this.GetSystem<IEngineSimulationSystem>();
        }

        void Update()
        {
            simulation?.Tick(Time.deltaTime);
        }

        bool Validate()
        {
            bool ok = true;
            if (engineRoot == null) { Debug.LogError("[EngineTwin] 未指定 engineRoot（发动机模型根节点）", this); ok = false; }
            if (viewCamera == null) { Debug.LogError("[EngineTwin] 场景中没有可用相机", this); ok = false; }
            if (tipPrefab == null) { Debug.LogError("[EngineTwin] 未指定 tipPrefab（HK_Tip 预制体）", this); ok = false; }
            if (curvePrefab == null) { Debug.LogError("[EngineTwin] 未指定 curvePrefab（HK_Curve2D 预制体）", this); ok = false; }
            if (uiFont == null) Debug.LogWarning("[EngineTwin] 未指定中文字体，界面中文可能显示为方块", this);
            return ok;
        }
    }
}
