// HK_TipsCtrl 的 Inspector 定制面板：功能卡片化 + 中文直白命名 + 实时校验。
// 只改展示层（显示名 / 分组 / 提示文案），不动序列化字段名——预制体已存值不受影响。
//
// 面板结构（两级视觉层次）：
//   信息段（标识/显隐与交互/淡入淡出）：分隔线 + 粗体小标题，参数常驻
//   功能卡（距离缩放/遮挡检测/弧线连线）：边框卡片 + 复选框头部，勾选才展开参数

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace HKTools
{
    /// <summary>
    /// HK_TipsCtrl 的 Inspector 定制：
    /// 功能开关做成带复选框的卡片（勾选即启用、展开参数），信息段用分隔线标题，
    /// 参数错误实时提示，新人无需查文档即可正确配置。
    /// CanEditMultipleObjects：支持多选批量编辑（否则多选时 Unity 回退默认 Inspector）。
    /// </summary>
    [CustomEditor(typeof(HK_TipsCtrl))]
    [CanEditMultipleObjects]
    public class HK_TipsCtrlEditor : Editor
    {
        // ---------- 序列化属性（字段名与 HK_TipsCtrl 一一对应，改名会断序列化） ----------

        SerializedProperty tipGroup;
        SerializedProperty visibleDistance;
        SerializedProperty isCanClick;
        SerializedProperty isCanAutoScale;
        SerializedProperty camRange;
        SerializedProperty minimum;
        SerializedProperty maximum;
        SerializedProperty isFaceCamera;
        SerializedProperty isEnableGrowAnim;
        SerializedProperty growDuration;
        SerializedProperty isCustomAdvanced;
        SerializedProperty titleOffSet;
        SerializedProperty lineDefaultWidth;
        SerializedProperty isUseCurve;
        SerializedProperty curveSegments;
        SerializedProperty curveStrength;
        SerializedProperty fadeDuration;
        SerializedProperty isEnableFade;

        // ---------- 卡片头部视觉规范 ----------
        // 品牌青的沉稳变体（深青绿）：沿用连线同源的青色家族（hue≈190°），
        // 大幅降低饱和度与亮度避免刺眼；浅色文字压上去，双主题均可读
        static readonly Color BrandTeal = new Color(0.20f, 0.42f, 0.48f);
        static readonly Color OnTealTextColor = new Color(0.93f, 0.96f, 0.97f);
        static readonly Color OnTealHintEnabled = new Color(0.55f, 0.85f, 0.62f);     // 浅绿（启用）
        static readonly Color OnTealHintDisabled = new Color(0.70f, 0.73f, 0.75f);    // 浅灰（未启用）

        // 标题字号：在默认粗体（12px）基础上 +1，作卡片内二级标题；层级主要由青色背景条承担
        const int CardTitleFontSize = 13;

        static Texture2D brandTexture;
        static GUIStyle brandHeaderStyle;      // 青色背景条（克隆工具栏保留内边距）
        static GUIStyle brandTitleStyle;       // 青底上的深色粗体标题
        static GUIStyle brandHintEnabledStyle; // 青底上的深绿说明
        static GUIStyle brandHintDisabledStyle;// 青底上的深灰说明

        static void EnsureBrandStyles()
        {
            if (brandHeaderStyle != null)
                return;

            brandTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            brandTexture.SetPixel(0, 0, BrandTeal);
            brandTexture.Apply();

            // 克隆工具栏样式（保留内边距/边框/字体），仅把背景换成品牌青绿
            brandHeaderStyle = new GUIStyle(EditorStyles.toolbar);
            brandHeaderStyle.normal.background = brandTexture;

            brandTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = CardTitleFontSize };
            brandTitleStyle.normal.textColor = OnTealTextColor;

            brandHintEnabledStyle = new GUIStyle(EditorStyles.label);
            brandHintEnabledStyle.normal.textColor = OnTealHintEnabled;

            brandHintDisabledStyle = new GUIStyle(EditorStyles.label);
            brandHintDisabledStyle.normal.textColor = OnTealHintDisabled;
        }

        void OnEnable()
        {
            // 层级变化（增删折点/子物体）时结构校验缓存失效，下次重绘重算
            EditorApplication.hierarchyChanged += InvalidateStructureCache;

            tipGroup = serializedObject.FindProperty("tipGroup");
            visibleDistance = serializedObject.FindProperty("visibleDistance");
            isCanClick = serializedObject.FindProperty("isCanClick");
            isCanAutoScale = serializedObject.FindProperty("isCanAutoScale");
            camRange = serializedObject.FindProperty("camRange");
            minimum = serializedObject.FindProperty("minimum");
            maximum = serializedObject.FindProperty("maximum");
            isFaceCamera = serializedObject.FindProperty("isFaceCamera");
            isEnableGrowAnim = serializedObject.FindProperty("isEnableGrowAnim");
            growDuration = serializedObject.FindProperty("growDuration");
            isCustomAdvanced = serializedObject.FindProperty("isCustomAdvanced");
            titleOffSet = serializedObject.FindProperty("titleOffSet");
            lineDefaultWidth = serializedObject.FindProperty("lineDefaultWidth");
            isUseCurve = serializedObject.FindProperty("isUseCurve");
            curveSegments = serializedObject.FindProperty("curveSegments");
            curveStrength = serializedObject.FindProperty("curveStrength");
            fadeDuration = serializedObject.FindProperty("fadeDuration");
            isEnableFade = serializedObject.FindProperty("isEnableFade");
        }

        public override void OnInspectorGUI()
        {
            EnsureBrandStyles();

            serializedObject.Update();

            DrawStructureHelp();
            DrawWarnings();

            // ---------- 信息卡：参数常驻（无启用/禁用语义，不加复选框） ----------

            DrawInfoCard("基本信息", "所属分组，用于按组批量控制与分组过滤", () =>
            {
                EditorGUILayout.PropertyField(tipGroup, new GUIContent("所属分组", "用于按分组批量显隐（如 CMD_TIP_ShowTipsByGroup）"));
            });

            DrawInfoCard("显示与交互", "可见距离、可点击、朝向相机、生长动画、淡入淡出", () =>
            {
                EditorGUILayout.PropertyField(visibleDistance,
                    new GUIContent("最远可见距离（米）", "相机与标签距离超过此值时淡出隐藏；距离按标签归位后的位置计算（动画期间同样按归位位置，判定稳定），不是按锚点"));
                EditorGUILayout.PropertyField(isCanClick, new GUIContent("可点击", "关闭后标签不响应点击"));
                EditorGUILayout.PropertyField(isFaceCamera,
                    new GUIContent("朝向相机", "开启：标签始终面向观众（默认）；关闭：不控制朝向，保持预制体里摆好的角度（贴墙/平放/朝固定方向的标签）"));

                // 两类动画并列放，描述上说清各自变的是什么、什么时候生效，便于一眼区分
                EditorGUILayout.PropertyField(isEnableGrowAnim,
                    new GUIContent("生长动画（形状）", "指令显隐时的形状动画：从原点（第一个折点）生长出来、消失时缩回原点（位置+缩放+连线逐段画出）；关闭则瞬间显隐。与淡入淡出可同时开启"));

                if (isEnableGrowAnim.boolValue)
                {
                    EditorGUILayout.PropertyField(growDuration,
                        new GUIContent("生长动画时长（秒）", "出现/消失动画的播放时长"));
                }

                EditorGUILayout.PropertyField(isEnableFade,
                    new GUIContent("淡入淡出（透明度）", "透明度动画：显隐时渐入渐出（变清晰/变透明）。距离显隐（走远走近）时生效；生长动画关闭时，指令显隐也走它"));

                if (isEnableFade.boolValue)
                {
                    EditorGUILayout.PropertyField(fadeDuration,
                        new GUIContent("淡入淡出时长（秒）", "显隐切换的渐变时间；文字用透明度、连线与锚点用材质透明度。关闭开关则直接显示/隐藏，无过渡"));
                }
            });

            // ---------- 功能卡：勾选启用，启用才展开参数 ----------

            DrawFeatureCard("距离缩放", "远处自动放大，保证可读", isCanAutoScale, () =>
            {
                EditorGUILayout.PropertyField(camRange,
                    new GUIContent("生效距离", "相机与标签距离在此区间内时，缩放倍率在最小值~最大值间过渡；X=起始距离，Y=结束距离"));

                float minValue = minimum.floatValue;
                float maxValue = maximum.floatValue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("缩放范围", "缩放倍率的最小值与最大值"));
                EditorGUILayout.MinMaxSlider(ref minValue, ref maxValue, 0f, 3f);
                minValue = EditorGUILayout.FloatField(minValue, GUILayout.Width(40));
                maxValue = EditorGUILayout.FloatField(maxValue, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                minimum.floatValue = minValue;
                maximum.floatValue = maxValue;
            });

            DrawFeatureCard("弧线连线", "连线改为平滑弧线，穿过所有折点", isUseCurve, () =>
            {
                EditorGUILayout.PropertyField(curveSegments,
                    new GUIContent("每段采样数", "每两个折点之间分多少段画弧线；越大越平滑，连线点数越多"));

                EditorGUILayout.PropertyField(curveStrength,
                    new GUIContent("弧度", "曲线朝奇数折点（控制点）弯曲但不到达：0 = 锚点间直连；0.5 = 标准弧；1 = 弧度拉满。弧度 0 时奇数折点不影响形状"));
            });

            DrawFeatureCard("高级设置", "自定义标签上浮高度与连线宽度", isCustomAdvanced, () =>
            {
                EditorGUILayout.PropertyField(titleOffSet,
                    new GUIContent("标签上浮高度", "标签悬于最后一个折点上方的距离"));

                EditorGUILayout.PropertyField(lineDefaultWidth,
                    new GUIContent("连线宽度", "连线的基准粗细（世界空间）"));
            });

            serializedObject.ApplyModifiedProperties();
        }

        // ---------- 面板构件 ----------

        /// <summary>结构说明盒（常驻顶部）</summary>
        void DrawStructureHelp()
        {
            EditorGUILayout.HelpBox(
                "结构约定（改预制体时勿破坏）：\n" +
                "HK_Tip（根，挂本脚本）\n" +
                "├─ Line：LineRenderer + Pos 折点子物体（决定连线形状）\n" +
                "└─ Title：Collider + HK_TipTitle + TMP 文本（文本可挂在子物体上）",
                MessageType.Info);
        }

        /// <summary>信息段标题：分隔线 + 粗体小标题（与功能卡片区分）</summary>
        /// <summary>
        /// 信息卡片：与功能卡同款边框盒子 + 粗体标题，但不放复选框——
        /// 内部参数是常驻配置，没有“启用/禁用”语义，不硬造开关。
        /// 标题行垫品牌青背景条（层级感靠背景条不靠字号，且自动适配深浅主题），
        /// 标题名后以括号附一句说明。
        /// </summary>
        void DrawInfoCard(string title, string description, System.Action drawParams)
        {
            GUILayout.Space(8);

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal(brandHeaderStyle, GUILayout.Height(22f));
            EditorGUILayout.LabelField(title, brandTitleStyle, GUILayout.Width(TitleWidth(title)));
            EditorGUILayout.LabelField($"（{description}）", brandHintEnabledStyle);
            EditorGUILayout.EndHorizontal();

            drawParams();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 功能卡片：边框盒子 + 复选框头部（勾选启用）+ 右侧说明文字；
        /// 勾选时才绘制内部参数，未勾选整卡只有一行开关——一眼看出哪些是可选功能。
        /// </summary>
        void DrawFeatureCard(string title, string hint, SerializedProperty toggle, System.Action drawParams)
        {
            GUILayout.Space(8);

            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 卡片头：复选框 + 名称（说明）（垫品牌青背景条，一眼是分组头）
            EditorGUILayout.BeginHorizontal(brandHeaderStyle, GUILayout.Height(22f));

            bool isEnabled = toggle.boolValue;
            bool newValue = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(18));

            EditorGUILayout.LabelField(title, brandTitleStyle, GUILayout.Width(TitleWidth(title)));
            EditorGUILayout.LabelField($"（{hint}）", isEnabled ? brandHintEnabledStyle : brandHintDisabledStyle);

            EditorGUILayout.EndHorizontal();

            if (newValue != isEnabled)
                toggle.boolValue = newValue;

            // 勾选后才展开参数
            if (toggle.boolValue)
            {
                EditorGUI.indentLevel++;
                drawParams();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>标题实际文字宽度：LabelField 默认拉伸占满剩余空间，会把括号说明推到最右，故按文字宽度收窄</summary>
        static float TitleWidth(string title)
        {
            return brandTitleStyle.CalcSize(new GUIContent(title)).x + 6f;
        }

        // ---------- 实时校验 ----------

        // ---------- 结构校验缓存 ----------
        // 结构检查涉及 Find/GetComponent，每次 Repaint 都对所有选中对象跑一遍会卡（多选时尤甚）；
        // 故在选择变化或层级变化（增删折点/子物体）时才重算，DrawWarnings 平时只读缓存。

        List<string> structureWarnings = new List<string>();
        Object[] cachedTargets;

        // 订阅在类级 OnEnable（找 SerializedProperty 那个）里，这里只做退订
        void OnDisable()
        {
            EditorApplication.hierarchyChanged -= InvalidateStructureCache;
        }

        void InvalidateStructureCache()
        {
            cachedTargets = null;
        }

        bool IsCacheValid()
        {
            if (cachedTargets == null || cachedTargets.Length != targets.Length)
                return false;

            for (int i = 0; i < cachedTargets.Length; i++)
            {
                if (cachedTargets[i] != targets[i])
                    return false;
            }

            return true;
        }

        /// <summary>重建结构校验缓存：逐个检查所有选中对象（多选时带上物体名，便于定位是哪个出的问题）</summary>
        List<string> BuildStructureWarnings()
        {
            var warnings = new List<string>();
            bool isMultiSelect = targets.Length > 1;

            foreach (var selected in targets)
            {
                var ctrl = (HK_TipsCtrl)selected;
                string prefix = isMultiSelect ? ctrl.name + "：" : string.Empty;

                Transform line = ctrl.transform.Find("Line");
                Transform title = ctrl.transform.Find("Title");

                if (line == null)
                {
                    warnings.Add(prefix + "缺少 Line 子物体（连线 + Pos 折点）");
                }
                else
                {
                    if (line.GetComponent<LineRenderer>() == null)
                        warnings.Add(prefix + "Line 上缺少 LineRenderer 组件");

                    if (line.childCount == 0)
                        warnings.Add(prefix + "Line 下没有折点子物体（至少保留 Pos01）");
                }

                if (title == null)
                {
                    warnings.Add(prefix + "缺少 Title 子物体（标签）");
                }
                else
                {
                    if (title.GetComponent<Collider>() == null)
                        warnings.Add(prefix + "Title 上缺少 Collider 组件（点击需要）");

                    if (title.GetComponent<HK_TipTitle>() == null)
                        warnings.Add(prefix + "Title 上缺少 HK_TipTitle 组件");

                    if (title.GetComponentInChildren<TMP_Text>(true) == null)
                        warnings.Add(prefix + "Title 下未找到 TMP 文本，淡入淡出将退化为直接显隐");
                }
            }

            return warnings;
        }

        void DrawWarnings()
        {
            // 结构校验：读缓存（选择变化或层级变化后才重算）
            if (!IsCacheValid())
            {
                structureWarnings = BuildStructureWarnings();
                cachedTargets = targets;
            }

            foreach (string warning in structureWarnings)
            {
                Warning(warning);
            }

            // 参数检查：多选且值不一致时 SerializedProperty 无法代表公共值，跳过以免误报
            if (!isCanAutoScale.hasMultipleDifferentValues && isCanAutoScale.boolValue
                && !camRange.hasMultipleDifferentValues && camRange.vector2Value.x > camRange.vector2Value.y)
                Warning("缩放生效距离的起始值大于结束值，缩放会反向");

            if (!minimum.hasMultipleDifferentValues && !maximum.hasMultipleDifferentValues
                && minimum.floatValue >= maximum.floatValue)
                Warning("缩放范围的最小值大于等于最大值");

            if (!visibleDistance.hasMultipleDifferentValues && visibleDistance.floatValue <= 0f)
                Warning("最远可见距离应大于 0");

            if (!fadeDuration.hasMultipleDifferentValues && fadeDuration.floatValue < 0f)
                Warning("淡入淡出时长不能为负");
        }

        void Warning(string message)
        {
            EditorGUILayout.HelpBox(message, MessageType.Warning);
        }
    }
}
