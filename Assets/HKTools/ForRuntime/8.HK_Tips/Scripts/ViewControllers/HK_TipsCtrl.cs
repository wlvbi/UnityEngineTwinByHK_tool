using QFramework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HKTools
{
    /// <summary>
    /// HK_Tips 标签控制管理：负责单个 Tip 的初始化、距离显隐、朝向、缩放、淡入淡出、指令级
    /// 生长动画与点击转发。
    /// 可见性语义（两套逻辑分工）：alpha 归距离独占（超距淡出、距离内淡入，与指令状态无关）；
    /// 根物体激活归指令独占（ShowWithAnim/HideWithAnim——不遮蔽 QF 的同名扩展方法，
    /// 业务要裸激活仍可用 tip.Show()）。完全不可见时缩放直接到位，
    /// 避免再次出现时从冻结旧值 Lerp 出来（先大后小）。
    /// 面板分组与提示见 HK_TipsCtrlEditor（只改展示层，不动本文件的序列化字段名）。
    /// 结构约定（改预制体时不要破坏）：根物体下必须有 Line（LineRenderer + Pos 折点子物体）与 Title（Collider + HK_TipTitle）。
    /// 初始化失败会打明确错误日志且不标记完成，修好结构后自动重试，不会永久失效。
    /// </summary>
    [DisallowMultipleComponent]
    public class HK_TipsCtrl : HKTips_BaseController
    {
        // ---------- 配置（Inspector 可调；面板分组与提示见 HK_TipsCtrlEditor）----------
        // 注意：字段名即序列化名——改名会使预制体/场景里已存的值丢失，只改文案不改名

        [Header("Tip类型：")] public HK_TipGroup tipGroup;

        [Header("最远可见距离："), SerializeField] float visibleDistance = 15;

        [Header("是否可点击："), SerializeField] bool isCanClick;
        [Header("是否可缩放："), SerializeField] bool isCanAutoScale;
        [Header("是否朝向相机："), Tooltip("开启：标签始终面向观众（默认）；关闭：不控制朝向，保持预制体里摆好的角度（贴墙/平放/朝固定方向的标签）"), SerializeField] bool isFaceCamera = true;

        [Header("是否启用生长动画："), Tooltip("启用后：指令显隐（ShowWithAnim/HideWithAnim）时从原点（第一个折点）生长出来、消失时缩回原点；关闭则瞬间显隐。距离显隐不受影响，仍走淡入淡出"), SerializeField] bool isEnableGrowAnim = true;
        [Header("生长动画时长（秒）："), SerializeField] float growDuration = 0.3f;

        [Header("默认摄像机距离："), SerializeField] Vector2 camRange;
        float defaultCamDis;

        [Header("缩放最小值："), SerializeField, Range(0, 1)] float minimum = 0.5f;
        [Header("缩放最大值："), SerializeField, Range(1, 2)] float maximum = 2f;
        Vector3 initScale;

        [Header("是否自定义高级参数："), SerializeField] bool isCustomAdvanced = true;
        [Header("Title标签向上的偏移值："), SerializeField] float titleOffSet = 0.2f;

        [Header("LineRenderer线条默认宽度："), SerializeField] float lineDefaultWidth = 0.04f;

        // 高级参数关闭时的内置默认值（与预制体摆好的值一致：关闭高级时标签位置不变）
        const float DefaultTitleOffSet = 0.2f;
        const float DefaultLineWidth = 0.04f;

        [Header("是否使用曲线连线："), SerializeField] bool isUseCurve;
        [Header("曲线每段采样数："), SerializeField, Range(2, 20)] int curveSegments = 8;
        [Header("曲线弧度："), SerializeField, Range(0f, 1f)] float curveStrength = 0.5f;

        [Header("淡入淡出时长（秒）："), SerializeField] float fadeDuration = 0.3f;
        [Header("是否开启淡入淡出："), SerializeField] bool isEnableFade = true;

        // ---------- 组件缓存（初始化时填充） ----------

        Transform line_TF;          // 提示线物体
        Transform title_TF;         // 提示标签物体
        LineRenderer lineRenderer;  // 折线渲染器
        Collider titleCollider;     // Title 碰撞体（点击射线命中用）
        HK_TipTitle tipTitle;       // Title 控制脚本（悬停/点击动画与回调）
        TMP_Text titleText;         // Title 文本（淡入淡出/半透明用，可空）

        // 需要跟随淡入淡出的渲染器（线条、锚点小球等；TMP 文本单独用 titleText.color）
        Renderer[] tintRenderers;
        Color[] tintBaseColors;
        MaterialPropertyBlock alphaBlock;

        // 材质颜色属性：内置管线为 _Color，URP 为 _BaseColor（两个都写，兼容两种材质）
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        // 折线折点列表（Line 的子物体，顺序即折线顺序）
        List<Transform> linePos_List = new List<Transform>();

        // 完整折线点缓存（生长动画截断/恢复用；TryBuildLineRenderer 时更新）
        Vector3[] linePosesCache;

        // 标题原始缩放（生长动画的缩放基准；Init 时缓存）
        Vector3 titleBaseScale = Vector3.one;

        // ---------- 运行状态 ----------

        // 指令级生长动画（与距离显隐是两套逻辑：这套只管 Show/Hide 指令）
        float growProgress = 1f;          // 生长进度 0~1（1=完全展开）
        float growTargetProgress = 1f;    // 生长目标（Show=1 / Hide=0）
        bool isCommandHidden;             // 命令级隐藏意图：收缩归零后才真正关闭根物体

        bool isInit;                // 初始化是否已完成（失败不置位，可重试）
        bool isPresent = true;      // 当前是否应显示（按与相机的距离判断）

        // 淡入淡出
        float currentAlpha = 1f;        // 当前不透明度（1=完全显示，0=完全隐藏）
        float targetAlpha = 1f;         // 目标不透明度
        float currentScaleFactor = 1f;  // 当前距离缩放系数（线宽计算用）

        // 静止跳过用：相机/自身/Title 相对「上次实际更新」的位置，以及缩放是否已收敛
        Vector3 lastCameraPos;
        Vector3 lastSelfPos;
        Vector3 lastTitlePos;
        bool isScaleSettled;
        bool isFirstUpdate = true;

        // 位移阈值（平方）：超过约 1cm 才视为相机/自身移动
        const float MoveEpsilonSqr = 0.0001f;

        // 初始化失败的自愈重试间隔（秒）：结构修好后不用重载场景；失败才打日志，节流避免刷屏
        const float InitRetryInterval = 1f;
        float lastInitRetryTime;

        // 缩放插值速度：每秒向目标缩放逼近的比例，越大响应越快（原为散落的 15）
        const float ScaleLerpSpeed = 15f;

        // 缩放收敛阈值（平方）：与目标差异小于 0.01 视为收敛（供静止跳过判断）
        const float ScaleSettleEpsilonSqr = 0.0001f;

        // 系统层引用（Tip 相机等共享数据统一从系统层获取）
        IHK_TipSystem tipSystem;
        Vector3 cameraPos;

        // 运行时配置变更检测：记录上次生效的配置值，Update 中比对，改动即时应用
        bool lastIsUseCurve;
        int lastCurveSegments;
        float lastCurveStrength;
        float lastTitleOffSet;
        float lastLineDefaultWidth;
        bool lastIsEnableGrowAnim;

        // ================= 生命周期 =================

        void Awake()
        {
            tipSystem = this.GetSystem<IHK_TipSystem>();

            // Awake 即入列：系统首次初始化（OnInit）时就应收集到本 Tip，
            // 否则后续指令（显隐/分组/批量）都控制不到它
            tipSystem.AddTipItem(this);
        }

        void OnDestroy()
        {
            // 销毁时 在HKTips 系统层的列表中，移除当前 HKTip
            this.GetSystem<IHK_TipSystem>().RemoveTipItem(this);
        }

        // ================= 初始化 =================

        /// <summary>
        /// 初始化：找结构 → 取组件 → 建折线，全部就绪后才标记 isInit。
        /// 任一步缺失都会打明确错误日志并返回（不标记完成），修好结构后下次调用自动重试。
        /// 幂等：已初始化的 Tip 重复调用直接返回。
        /// </summary>
        public void Init()
        {
            if (isInit)
                return;

            // 三步校验，任一步失败即中止（此时 isInit 仍为 false）
            if (!TryFindStructure() || !TryCacheComponents() || !TryBuildLineRenderer())
                return;

            isInit = true;

            // 以下为初始化接线（此时所有引用已确保非空）
            titleCollider.enabled = isCanClick;
            tipTitle.OnClickCallback = OnClickTip;

            title_TF.position = GetTitleTargetPosition();
            titleBaseScale = title_TF.localScale;

            defaultCamDis = (camRange.y + camRange.x) / 2f;
            initScale = transform.localScale;

            // 记录配置初值，供运行时变更检测（记生效值：切换自定义开关也要能触发）
            lastIsUseCurve = isUseCurve;
            lastCurveSegments = curveSegments;
            lastCurveStrength = curveStrength;
            lastTitleOffSet = TitleOffSet;
            lastLineDefaultWidth = LineWidth;
            lastIsEnableGrowAnim = isEnableGrowAnim;
        }

        /// <summary>查找 Line / Title 子物体，结构不符时打错误日志并返回 false</summary>
        bool TryFindStructure()
        {
            line_TF = transform.Find("Line");
            title_TF = transform.Find("Title");

            if (line_TF == null || title_TF == null)
            {
                Debug.LogError($"[HK_Tips] {name} 初始化失败：缺少 {(line_TF == null ? "Line" : "Title")} 子物体，请保持预制体结构", this);
                return false;
            }

            return true;
        }

        /// <summary>缓存 LineRenderer / Collider / HK_TipTitle 组件，缺失时打错误日志并返回 false</summary>
        bool TryCacheComponents()
        {
            lineRenderer = line_TF.GetComponent<LineRenderer>();
            titleCollider = title_TF.GetComponent<Collider>();
            tipTitle = title_TF.GetComponent<HK_TipTitle>();

            // 文本组件通常在 Title 的子物体上（如 Tmp_Title），先找子物体再兜底整个 Tip
            titleText = FindTitleText();

            if (titleText == null && fadeDuration > 0f)
                Debug.LogWarning($"[HK_Tips] {name} 未找到 TMP_Text，淡入淡出将退化为直接显隐", this);

            // 收集需要跟随透明度的渲染器（含线条与锚点小球等）
            CacheTintRenderers();

            if (lineRenderer == null || titleCollider == null || tipTitle == null)
            {
                Debug.LogError($"[HK_Tips] {name} 初始化失败：Line/Title 上缺少 LineRenderer/Collider/HK_TipTitle 组件", this);
                return false;
            }

            return true;
        }

        /// <summary>收集折点并构建 LineRenderer 折线，Line 下无折点时打错误日志并返回 false</summary>
        bool TryBuildLineRenderer()
        {
            linePos_List.Clear();

            foreach (Transform child_TF in line_TF)
            {
                linePos_List.Add(child_TF);
            }

            if (linePos_List.Count == 0)
            {
                Debug.LogError($"[HK_Tips] {name} 初始化失败：Line 下没有折点子物体（至少保留 Pos01）", this);
                return false;
            }

            Vector3[] linePoses = BuildLinePositions(linePos_List, isUseCurve, curveSegments, curveStrength);

            // 缓存完整折线点（生长动画的截断/恢复都基于它）
            linePosesCache = linePoses;

            lineRenderer.positionCount = linePoses.Length;
            lineRenderer.SetPositions(linePoses);

            return true;
        }

        /// <summary>
        /// 生成折线点。关闭曲线时取全部折点（直线穿过所有点）。
        /// 开启后按「锚点 / 控制点」交替解释折点：偶数索引与末点为锚点（曲线经过），
        /// 奇数索引为控制点（曲线朝它弯曲但不到达）——相邻锚点间夹 1 个控制点用二次贝塞尔，
        /// 夹 2 个控制点用三次贝塞尔（偶数折点数时的第一段）。
        /// strength 为弧度（0~1），内部控制点拉力 k = strength × 2：
        /// 0 = 锚点间直连（奇数控制点不影响形状）；0.5 = 标准弧（二次弧顶约为控制点幅度的一半）；
        /// 1 = 弧度拉满（二次弧顶到达控制点幅度，三次控制点外拉）。全程有界不过冲。
        /// 编辑器预览（HK_TipLine）与运行时共用本方法，保证预览与运行一致。
        /// </summary>
        public static Vector3[] BuildLinePositions(List<Transform> points, bool isUseCurve, int segments, float strength)
        {
            if (!isUseCurve || points.Count < 3 || segments < 2)
                return StraightPositions(points);

            List<Vector3> result = new List<Vector3>();
            float k = strength * 2f;

            int total = points.Count;
            int anchor = 0;

            // 偶数折点数：第一段用三次贝塞尔（前 4 点全部参与形状），之后每 2 点一段二次
            if (total % 2 == 0)
            {
                SampleCubic(points, 0, 1, 2, 3, k, segments, result);
                anchor = 3;
            }

            // 之后每 2 点一段二次贝塞尔（k=0 时自动退化为锚点间直连）
            while (anchor + 2 < total)
            {
                SampleQuadratic(points, anchor, anchor + 1, anchor + 2, k, segments, result);
                anchor += 2;
            }

            // 各段只采样到 t<1，末锚点在此补上
            result.Add(points[total - 1].localPosition);

            return result.ToArray();
        }

        static Vector3[] StraightPositions(List<Transform> points)
        {
            Vector3[] positions = new Vector3[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                positions[i] = points[i].localPosition;
            }

            return positions;
        }

        /// <summary>二次贝塞尔段：锚点 a/b + 控制点 c，按拉力 k 朝锚点弦的中点收放（k=1 为标准二次贝塞尔）</summary>
        static void SampleQuadratic(List<Transform> points, int a, int c, int b, float k, int segments, List<Vector3> result)
        {
            Vector3 pA = points[a].localPosition;
            Vector3 pC = points[c].localPosition;
            Vector3 pB = points[b].localPosition;

            Vector3 chordMid = (pA + pB) * 0.5f;
            Vector3 control = chordMid + (pC - chordMid) * k;

            for (int s = 0; s < segments; s++)
            {
                float t = s / (float)segments;
                float u = 1f - t;

                result.Add(u * u * pA + 2f * u * t * control + t * t * pB);
            }
        }

        /// <summary>三次贝塞尔段：锚点 p0/p3 + 控制点 p1/p2，控制点从端点朝控制点方向伸缩（k=1 为完整三次贝塞尔）</summary>
        static void SampleCubic(List<Transform> points, int i0, int i1, int i2, int i3, float k, int segments, List<Vector3> result)
        {
            Vector3 p0 = points[i0].localPosition;
            Vector3 p1 = points[i1].localPosition;
            Vector3 p2 = points[i2].localPosition;
            Vector3 p3 = points[i3].localPosition;

            Vector3 c1 = p0 + (p1 - p0) * k;
            Vector3 c2 = p3 + (p2 - p3) * k;

            for (int s = 0; s < segments; s++)
            {
                float t = s / (float)segments;
                float u = 1f - t;

                result.Add(u * u * u * p0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * p3);
            }
        }

        /// <summary>Title 目标位置：最后一个折点上方 TitleOffSet 处</summary>
        Vector3 GetTitleTargetPosition()
        {
            return linePos_List[linePos_List.Count - 1].position + new Vector3(0, TitleOffSet, 0);
        }

        // ================= 每帧更新 =================

        void Update()
        {
            if (!isInit)
            {
                // 初始化失败的自愈：结构修好后不用重载场景。节流重试（Init 幂等，失败才打日志）
                if (Time.time - lastInitRetryTime >= InitRetryInterval)
                {
                    lastInitRetryTime = Time.time;
                    Init();
                }

                return;
            }

            bool hasCamera = UpdateCameraPosition();

            UpdateGrowAnim();               // 指令级生长动画（独立于距离显隐与静止跳过）

            UpdateFade();                   // 淡入淡出推进（独立于静止跳过，避免淡到一半冻住）

            UpdateConfigChanges();          // 运行时参数变更检测（同样先于静止跳过）

            if (!hasCamera)
            {
                // 相机不可用时报错一次并跳过本帧（不抛异常）；相机注册/切换后自动恢复
                tipSystem.LogMissingCameraErrorOnce();
                return;
            }

            // 静止跳过：相机、自身、Title 相对「上次实际更新」都没动，且缩放已收敛 → 本帧无事可做。
            // Title 位置单独比较：Tip 挂在旋转父级下时根位置可能不变，但 Title 的世界位置在变（显隐/缩放依赖它）。
            bool isCameraStill = (cameraPos - lastCameraPos).sqrMagnitude < MoveEpsilonSqr;
            bool isSelfStill = (transform.position - lastSelfPos).sqrMagnitude < MoveEpsilonSqr;
            bool isTitleStill = (title_TF.position - lastTitlePos).sqrMagnitude < MoveEpsilonSqr;

            // 淡入淡出进行中不跳过
            bool isFading = !Mathf.Approximately(currentAlpha, targetAlpha);

            // 生长动画进行中不跳过（与淡入淡出并列）
            bool isGrowAnimating = isEnableGrowAnim && !Mathf.Approximately(growProgress, growTargetProgress);

            if (!isFirstUpdate && isCameraStill && isSelfStill && isTitleStill
                && (!isCanAutoScale || isScaleSettled) && !isFading && !isGrowAnimating)
                return;

            isFirstUpdate = false;
            lastCameraPos = cameraPos;
            lastSelfPos = transform.position;
            lastTitlePos = title_TF.position;

            // 一次计算，显隐与缩放共用（统一按标签归位后的位置：生长动画期间标题在锚点与
            // 归位点之间滑动，用最终位置计算可避免动画中距离抖动导致显隐判定闪烁）
            float titleDistance = Vector3.Distance(GetTitleTargetPosition(), cameraPos);

            UpdateVisibilityByDistance(titleDistance);

            // 缩放先于 isPresent 提前执行：不可见期间也要让缩放跟踪距离（不可见时直接到位），
            // 否则再次出现时会从隐藏期间冻结的旧缩放 Lerp 出来——一出现很大再快速缩小
            if (isCanAutoScale)
                UpdateScaleByDistance(titleDistance);

            // 超距时不做朝向
            if (!isPresent)
                return;

            UpdateTitleRotation();
        }

        /// <summary>
        /// 运行时配置变更检测：Play 模式下改 Inspector、或脚本改参数时即时生效，
        /// 不必等下次初始化。放在静止跳过之前——相机静止时改参数同样生效。
        /// </summary>
        void UpdateConfigChanges()
        {
            // 弧线开关 / 采样数 / 弧度变化 → 重建折线（RefreshLine 内部会重设 Title 位置）
            if (isUseCurve != lastIsUseCurve || curveSegments != lastCurveSegments
                || !Mathf.Approximately(curveStrength, lastCurveStrength))
            {
                lastIsUseCurve = isUseCurve;
                lastCurveSegments = curveSegments;
                lastCurveStrength = curveStrength;
                RefreshLine();
            }

            // 上浮高度（含自定义开关）变化 → 只重设 Title 位置（折线形状不变）
            if (!Mathf.Approximately(TitleOffSet, lastTitleOffSet))
            {
                lastTitleOffSet = TitleOffSet;
                title_TF.position = GetTitleTargetPosition();
            }

            // 可点击变化 → 同步 Collider 启用（与 SetIsCanClick 走同一状态，不会来回抖动）
            if (titleCollider.enabled != isCanClick)
            {
                titleCollider.enabled = isCanClick;
            }

            // 连线宽度（含自定义开关）变化 → 重新应用线宽
            if (!Mathf.Approximately(LineWidth, lastLineDefaultWidth))
            {
                lastLineDefaultWidth = LineWidth;
                ApplyLineWidth();
            }

            // 生长动画开关变化 → 对齐状态（防止出现半截线卡住）
            if (isEnableGrowAnim != lastIsEnableGrowAnim)
            {
                lastIsEnableGrowAnim = isEnableGrowAnim;

                if (isEnableGrowAnim)
                {
                    // 启用时对齐当前可见状态：根物体不可见则从 0 开始，下次 Show 有完整动画
                    growProgress = gameObject.activeSelf ? 1f : 0f;
                    growTargetProgress = growProgress;
                }
                else
                {
                    RestoreFullGrowState();
                }
            }
        }

        /// <summary>
        /// 刷新相机位置：向系统层取当前 Tip 相机（由 HK_TipCamera 注册而来），
        /// 取不到则返回 false（本帧跳过更新，不抛异常）；相机注册/切换后自动恢复。
        /// </summary>
        bool UpdateCameraPosition()
        {
            Camera camera = tipSystem.HK_TipCamera;

            if (camera == null)
                return false;

            cameraPos = camera.transform.position;
            return true;
        }

        /// <summary>
        /// 按与相机的距离切换显示状态（超出 visibleDistance 即隐藏）。
        /// 距离由 Update 统一计算（按 Title 位置），这里不再重复算。
        /// alpha 归距离独占：本方法只改目标透明度，实际渐变由 UpdateFade 推进。
        /// </summary>
        void UpdateVisibilityByDistance(float distance)
        {
            bool shouldPresent = distance <= visibleDistance;

            if (shouldPresent == isPresent)
                return;

            isPresent = shouldPresent;
            RefreshTargetAlpha();

            // 已处于指令隐藏（收缩动画播放中）时超距：不播淡出动画，alpha 直接归零
            // （整体已经在隐藏流程里，淡给谁看；收缩播完后照常关闭根物体）
            if (!isPresent && isCommandHidden)
            {
                currentAlpha = 0f;
                ApplyAlpha(0f);
            }
        }

        /// <summary>Title 朝向：开启时保持水平面向相机；关闭时不控制朝向（用预制体摆好的角度）</summary>
        void UpdateTitleRotation()
        {
            if (!isFaceCamera)
                return;

            title_TF.LookAt(new Vector3(cameraPos.x, title_TF.position.y, cameraPos.z));
        }

        /// <summary>按相机距离在 [minimum, maximum] 区间内插值缩放整体，并同步线条宽度（视觉宽度基本恒定）</summary>
        void UpdateScaleByDistance(float camDis)
        {
            float scaleFactor = GetDistanceScaleFactor(camDis);
            currentScaleFactor = scaleFactor;

            // 保留原始比例（支持非等比设计的标签），只做整体倍率
            Vector3 targetScale = initScale * scaleFactor;

            if (currentAlpha <= 0.001f)
            {
                // 完全不可见（透明度为 0）时直接到位：没人看得到跳变，
                // 且保证再次出现时缩放就是当前距离的正确值——否则会从隐藏期间
                // 冻结的旧缩放 Lerp 出来（ Tip 一出现很大、再快速缩到正常）
                transform.localScale = targetScale;
            }
            else
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, ScaleLerpSpeed * Time.deltaTime);
            }

            ApplyLineWidth();

            // 缩放收敛标记（供静止跳过用）：与目标差异小于 0.01 视为收敛
            isScaleSettled = Vector3.SqrMagnitude(transform.localScale - targetScale) < ScaleSettleEpsilonSqr;
        }

        /// <summary>
        /// 按当前距离直接把缩放到位（不做 Lerp）：用于隐藏后重新显示时消除"先大后小"跳变。
        /// 调用时机在子物体激活/淡入之前（此刻不可见），跳变不会被看到。
        /// </summary>
        void SnapScaleByDistance()
        {
            Camera camera = tipSystem.HK_TipCamera;

            if (camera == null)
                return;

            float titleDistance = Vector3.Distance(GetTitleTargetPosition(), camera.transform.position);

            currentScaleFactor = GetDistanceScaleFactor(titleDistance);
            transform.localScale = initScale * currentScaleFactor;
            ApplyLineWidth();
            isScaleSettled = true;
        }

        /// <summary>线宽 = 基准宽 × 距离缩放系数（淡入淡出不再影响线宽）</summary>
        void ApplyLineWidth()
        {
            lineRenderer.widthMultiplier = LineWidth * currentScaleFactor;
        }

        // ---------- 淡入淡出 ----------

        /// <summary>刷新目标透明度：不可见（超距）→ 0；否则 1</summary>
        void RefreshTargetAlpha()
        {
            targetAlpha = isPresent ? 1f : 0f;
        }

        /// <summary>推进淡入淡出；淡入前先激活子物体，完全淡出后关闭子物体省渲染</summary>
        void UpdateFade()
        {
            // 生长动画播放期间由动画掌管可见性，淡入淡出让位（动画结束后自动恢复同步）
            if (isEnableGrowAnim && !Mathf.Approximately(growProgress, growTargetProgress))
                return;

            if (Mathf.Approximately(currentAlpha, targetAlpha))
                return;

            if (targetAlpha > 0f && !title_TF.gameObject.activeSelf)
            {
                title_TF.gameObject.SetActive(true);
                line_TF.gameObject.SetActive(true);
            }

            // 未开启淡入淡出、无文本组件或时长为 0：透明度一步到位（表现为直接显隐）
            if (!isEnableFade || titleText == null || fadeDuration <= 0f)
                currentAlpha = targetAlpha;
            else
                currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime / fadeDuration);

            ApplyAlpha(currentAlpha);

            if (currentAlpha <= 0.001f)
            {
                title_TF.gameObject.SetActive(false);
                line_TF.gameObject.SetActive(false);
            }
        }

        /// <summary>应用不透明度：文本改 TMP 颜色 alpha，其余渲染器（线条/锚点小球等）改材质颜色 alpha</summary>
        void ApplyAlpha(float alpha)
        {
            currentAlpha = alpha;

            if (titleText != null)
            {
                Color textColor = titleText.color;
                textColor.a = alpha;
                titleText.color = textColor;
            }

            ApplyRenderersAlpha(alpha);
        }

        /// <summary>收集需要跟随透明度的渲染器（排除 TMP 文本，文本走 titleText.color）</summary>
        void CacheTintRenderers()
        {
            List<Renderer> rendererList = new List<Renderer>();
            List<Color> colorList = new List<Color>();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.GetComponent<TMP_Text>() != null)
                    continue;

                rendererList.Add(renderer);
                colorList.Add(ReadRendererBaseColor(renderer));
            }

            tintRenderers = rendererList.ToArray();
            tintBaseColors = colorList.ToArray();
            alphaBlock = new MaterialPropertyBlock();
        }

        /// <summary>读取渲染器材质的基准颜色（兼容 _BaseColor / _Color）</summary>
        static Color ReadRendererBaseColor(Renderer renderer)
        {
            Material material = renderer.sharedMaterial;

            if (material == null)
                return Color.white;

            if (material.HasProperty(BaseColorId))
                return material.GetColor(BaseColorId);

            if (material.HasProperty(ColorId))
                return material.GetColor(ColorId);

            return Color.white;
        }

        /// <summary>
        /// 用 MaterialPropertyBlock 逐个渲染器覆盖颜色 alpha：
        /// 同一材质被多个渲染器（线条 + 锚点小球）共用时各自独立淡化，且不污染共享材质资产。
        /// </summary>
        void ApplyRenderersAlpha(float alpha)
        {
            if (tintRenderers == null || alphaBlock == null)
                return;

            for (int i = 0; i < tintRenderers.Length; i++)
            {
                Renderer renderer = tintRenderers[i];

                if (renderer == null)
                    continue;

                Color color = tintBaseColors[i];
                color.a = alpha;

                alphaBlock.Clear();
                alphaBlock.SetColor(ColorId, color);
                alphaBlock.SetColor(BaseColorId, color);
                renderer.SetPropertyBlock(alphaBlock);
            }
        }

        /// <summary>
        /// 距离 → 缩放系数：camRange.x 内取 minimum，默认距离（camRange 中点）取中间值，camRange.y 外取 maximum。
        /// camRange.x 与 y 相同时退化为两端钳制，不会除零。
        /// </summary>
        float GetDistanceScaleFactor(float camDis)
        {
            float index;

            if (camDis < defaultCamDis)
            {
                index = camDis >= camRange.x ? (camDis - camRange.x) / (defaultCamDis - camRange.x) - 1 : -1;
            }
            else if (camDis > defaultCamDis)
            {
                index = camDis <= camRange.y ? 1 - (camRange.y - camDis) / (camRange.y - defaultCamDis) : 1;
            }
            else
            {
                index = 0;
            }

            // index [-1, 1] 映射到 [minimum, maximum]
            return (index + 1) / 2f * (maximum - minimum) + minimum;
        }

        // ================= 对外 API =================

        /// <summary>
        /// Title 向上偏移值（生效值：关闭自定义时用内置默认；编辑器预览 HK_TipLine 与运行时共用）
        /// </summary>
        public float TitleOffSet => isCustomAdvanced ? titleOffSet : DefaultTitleOffSet;

        /// <summary>连线宽度（生效值：关闭自定义时用内置默认；编辑器预览 HK_TipLine 读取）</summary>
        public float LineWidth => isCustomAdvanced ? lineDefaultWidth : DefaultLineWidth;

        /// <summary>是否使用曲线连线（编辑器预览 HK_TipLine 读取）</summary>
        public bool IsUseCurve => isUseCurve;

        /// <summary>曲线每段采样数（编辑器预览 HK_TipLine 读取）</summary>
        public int CurveSegments => curveSegments;

        /// <summary>曲线弧度（编辑器预览 HK_TipLine 读取）</summary>
        public float CurveStrength => curveStrength;

        /// <summary>
        /// 重建折线与 Title 位置（初始化时会自动构建一次；运行时移动/增删 Pos 子物体后调用）
        /// </summary>
        public void RefreshLine()
        {
            if (!TryBuildLineRenderer())
                return;

            title_TF.position = GetTitleTargetPosition();
        }

        /// <summary>
        /// 外界调用，设置当前 Tip 是否可点击。
        /// 初始化未完成时先记录开关，初始化成功后按此值生效（collider 就绪前不会抛异常）。
        /// </summary>
        public void SetIsCanClick(bool _isCanClick)
        {
            isCanClick = _isCanClick;

            if (titleCollider != null)
                titleCollider.enabled = _isCanClick;
        }

        /// <summary>
        /// 查找 Title 下的 TMP 文本：先找 Title 子物体，兜底整个 Tip（含未激活物体）。
        /// Init 与 SetText 的缓存刷新共用，保证两处查找逻辑一致。
        /// </summary>
        TMP_Text FindTitleText()
        {
            if (title_TF != null)
            {
                TMP_Text found = title_TF.GetComponentInChildren<TMP_Text>(true);

                if (found != null)
                    return found;
            }

            return GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>
        /// 外界调用，运行时设置标签文字（Title 或其子物体上需有 TMP 文本组件）。
        /// 同时作为「运行时换内容」的统一入口：调用时会刷新淡入淡出相关缓存
        /// （Init 之后才挂/换 TMP、换渲染器的场景），并按当前不透明度同步一次，
        /// 避免淡入淡出进行中换内容导致新组件全亮/全暗。
        /// </summary>
        public void SetText(string text)
        {
            // 刷新缓存：文本引用失效（销毁/替换）时重找，渲染器集合重新收集
            titleText = FindTitleText();
            CacheTintRenderers();

            if (titleText == null)
            {
                Debug.LogError($"[HK_Tips] {name} 未找到 TMP_Text，无法设置文字（Title 或其子物体上需挂 TMP 文本）", this);
                return;
            }

            titleText.text = text;

            // 换内容后按当前透明度同步（无淡入淡出进行中时是恒等操作）
            ApplyAlpha(currentAlpha);
        }

        /// <summary>
        /// 显示 Tip（带动画的完整显示：激活 + 淡入/生长 + 状态同步）。
        /// 不叫 Show 是为了避免遮蔽 QFramework 的 Component 扩展 Show（那是裸 SetActive）——
        /// 业务要“瞬间激活”可用 tip.Show()，要完整表现用本方法。
        /// alpha 归距离独占：本方法只负责激活，淡入由距离系统按当前真实距离驱动
        /// （根物体刚激活，之前的 isPresent 是过期的，故此处用当前距离重新判定）。
        /// 启用生长动画且处于距离内时：播放「从原点生长」作为出现动画（alpha 直接到位，不叠加淡入）。
        /// </summary>
        public void ShowWithAnim()
        {
            isCommandHidden = false;
            gameObject.SetActive(true);

            if (!isInit)
                return;

            RefreshPresentByDistance();

            if (!isPresent)
            {
                // 超距：不淡入也不播生长。先恢复完整视觉状态——收缩动画可能把标题留在锚点、
                // 折线截断（残废形态），不恢复的话之后淡入会出现"只有标题没有线/标题跑位"；
                // 恢复后再归零关闭，回到距离内即可直接淡入出完整形态
                if (isEnableGrowAnim)
                    RestoreFullGrowState();

                currentAlpha = 0f;
                ApplyAlpha(0f);
                title_TF.gameObject.SetActive(false);
                line_TF.gameObject.SetActive(false);
                return;
            }

            // 距离内：激活子物体
            title_TF.gameObject.SetActive(true);
            line_TF.gameObject.SetActive(true);

            // 隐藏期间 Update 不跑、缩放被冻结在旧值：先按当前距离直接到位。
            // 此刻子物体刚激活、alpha 仍为 0，跳变不会被看到；
            // 不这么做的话，淡入/生长的同时会从旧缩放 Lerp 出来（一出现很大再缩小）
            if (isCanAutoScale)
                SnapScaleByDistance();

            if (!isEnableGrowAnim)
            {
                // 无生长动画：从 0 开始，交给淡入淡出系统渐变（fade 关闭时一步到位）
                currentAlpha = 0f;
                ApplyAlpha(0f);
                return;
            }

            // 生长动画：生长即出现动画，alpha 直接到位（不叠加淡入）
            currentAlpha = 1f;
            ApplyAlpha(1f);
            growTargetProgress = 1f;
        }

        /// <summary>
        /// 隐藏 Tip（带动画的完整隐藏）。与 ShowWithAnim 对应，同样不遮蔽 QF 扩展 Hide。
        /// 指令隐藏是瞬间的：alpha 直接归零（不播淡出动画——都不可见了，淡给谁看），
        /// 再关根物体；启用生长动画时改为播放「缩回原点」，播完归零关闭。
        /// </summary>
        public void HideWithAnim()
        {
            if (!isEnableGrowAnim)
            {
                if (isInit)
                {
                    currentAlpha = 0f;
                    targetAlpha = 0f;
                    ApplyAlpha(0f);
                }

                gameObject.SetActive(false);
                return;
            }

            // 已经不可见（被距离淡出等）：无需播放收缩动画，直接关闭
            if (isInit && !title_TF.gameObject.activeSelf)
            {
                currentAlpha = 0f;
                ApplyAlpha(0f);
                gameObject.SetActive(false);
                return;
            }

            isCommandHidden = true;
            growTargetProgress = 0f;
        }

        /// <summary>
        /// 用当前相机距离刷新 isPresent 与 targetAlpha（根物体刚激活、需要同步状态时调用；
        /// Update 内的距离检测走原有路径，不经过这里）。取不到相机时保持原状。
        /// 距离按标签归位后的位置计算（不用 title_TF.position）——生长动画期间标题在滑动，
        /// 用最终位置判定才稳定，避免动画中/收缩残留状态下的误判。
        /// </summary>
        void RefreshPresentByDistance()
        {
            Camera camera = tipSystem.HK_TipCamera;

            if (camera == null)
            {
                // 取不到相机时无法判断距离：按可见处理。否则 Show() 会把 alpha 归零后
                // 无人恢复（没相机时 Update 在淡入淡出之前就返回了）——Tip 隐形卡死
                isPresent = true;
                RefreshTargetAlpha();
                return;
            }

            float titleDistance = Vector3.Distance(GetTitleTargetPosition(), camera.transform.position);

            isPresent = titleDistance <= visibleDistance;
            RefreshTargetAlpha();
        }

        /// <summary>
        /// 指令级生长动画推进（Show/Hide 触发，与距离显隐是两套逻辑）。
        /// 播放期间距离淡入淡出让位（见 UpdateFade 守卫）；收缩归零时才关闭根物体。
        /// </summary>
        void UpdateGrowAnim()
        {
            if (!isEnableGrowAnim)
                return;

            if (Mathf.Approximately(growProgress, growTargetProgress))
            {
                // 防御：目标已到零且带命令隐藏意图（如启用开关时根物体已不可见）→ 收尾关闭
                if (isCommandHidden && growProgress <= 0f)
                {
                    isCommandHidden = false;
                    currentAlpha = 0f;
                    ApplyAlpha(0f);
                    gameObject.SetActive(false);
                }

                return;
            }

            // 时长下限保护：配 0 或负值时一步到位（表现为瞬间显隐）
            float duration = growDuration > 0f ? growDuration : 0.0001f;

            growProgress = Mathf.MoveTowards(growProgress, growTargetProgress, Time.deltaTime / duration);

            ApplyGrow(growProgress);

            if (growProgress <= 0.001f)
            {
                // 收缩到底：alpha 归零（保证下次 Show 的淡入从 0 开始）+ 关闭子物体省渲染；
                // 带命令隐藏意图时连根物体一起关
                currentAlpha = 0f;
                ApplyAlpha(0f);

                title_TF.gameObject.SetActive(false);
                line_TF.gameObject.SetActive(false);

                if (isCommandHidden)
                {
                    isCommandHidden = false;
                    gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 应用生长进度：连线从锚点（第一个折点）逐段画出，标题从锚点滑向目标位置并同步缩放。
        /// 标题用位置插值 + 缩放而非单纯滑动；缩放与悬停动画的互斥由 HK_TipTitle 侧让位保证。
        /// 注意：positionCount 增减时 Unity 内部缓冲行为不一致（新增点可能填充旧值），
        /// 故设完数量后逐点显式写入，不依赖 SetPositions 的截断/保留语义。
        /// </summary>
        void ApplyGrow(float progress)
        {
            if (linePosesCache == null || linePosesCache.Length == 0 || linePos_List.Count == 0)
                return;

            int fullCount = linePosesCache.Length;
            int targetCount = Mathf.Clamp(Mathf.RoundToInt(1 + (fullCount - 1) * progress), 1, fullCount);

            lineRenderer.positionCount = targetCount;

            for (int i = 0; i < targetCount; i++)
            {
                lineRenderer.SetPosition(i, linePosesCache[i]);
            }

            title_TF.position = Vector3.Lerp(linePos_List[0].position, GetTitleTargetPosition(), progress);

            // 标题同步缩放（下限保护，避免 0 缩放）
            title_TF.localScale = titleBaseScale * Mathf.Max(progress, 0.0001f);
        }

        /// <summary>运行时关闭生长动画：恢复完整折线、标题位置与缩放，防止出现半截线卡住</summary>
        void RestoreFullGrowState()
        {
            if (!isInit || linePosesCache == null)
                return;

            // 与 ApplyGrow 同一套写入方式：先设数量再逐点写。
            // SetPositions 不会恢复 positionCount（只写到当前数量为止），收缩后直接调它
            // 会把数量留在 1——1 个点画不出线段，表现为"Pos 位置对但线不显示"
            lineRenderer.positionCount = linePosesCache.Length;

            for (int i = 0; i < linePosesCache.Length; i++)
            {
                lineRenderer.SetPosition(i, linePosesCache[i]);
            }

            title_TF.position = GetTitleTargetPosition();
            title_TF.localScale = titleBaseScale;

            growProgress = 1f;
            growTargetProgress = 1f;
            isCommandHidden = false;
        }

        /// <summary>生长动画是否播放中（供 HK_TipTitle 悬停/震动动画让位判断）</summary>
        public bool IsGrowAnimating => isEnableGrowAnim && !Mathf.Approximately(growProgress, growTargetProgress);

        // ================= 内部回调 =================

        // 当前标签被点击时调用（由 HK_TipTitle 的 OnClickCallback 转发）
        void OnClickTip()
        {
            this.SendEvent(new Event_TIP_OnClick { _tip = this });
        }

        // ================= 编辑器 =================

#if UNITY_EDITOR
        void OnValidate() // 当Inspector中 isCanClick参数变化时，实时调整Title子物体是否可点击
        {
            if (title_TF == null)
            {
                title_TF = transform.Find("Title");
            }

            if (title_TF == null)
                return;

            if (titleCollider == null)
            {
                titleCollider = title_TF.GetComponent<Collider>();
            }

            if (titleCollider != null)
            {
                titleCollider.enabled = isCanClick;
            }
        }
#endif
    }
}
