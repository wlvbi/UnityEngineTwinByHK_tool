# HK_Tips 使用说明

## 功能描述

一个**三维场景悬浮提示标签工具**：在场景里摆好预制体，运行时标签自动面向相机、按距离自动显隐、支持点击事件回调；连线可走直线或平滑弧线，显隐可带「生长」和「淡入淡出」两类动画。

**适用场景**：展品标注（博物馆 / 展会）、工业巡检点位提示、VR 巡展指引、任何需要在三维空间里「指个地方 + 说句话 + 能点」的需求。

**原理（四句话）：**

1. **自动注册**：每个 Tip 在 `Awake` 时自行加入系统列表并初始化——不需要、也没有「手动收集」这一步（切场景、Instantiate、Additive 场景全部自动覆盖）。
2. **系统层统一管理**：`HK_TipSystem` 持有 Tip 列表与当前 Tip 相机，所有批量控制走指令（`CMD_TIP_Xxx`）。
3. **可见性两权分立**：`alpha` 归距离独占（走远淡出、走近淡入）；根物体激活归指令独占（`ShowWithAnim` / `HideWithAnim`）。
4. **点击走事件总线**：标签被点时发送 `Event_TIP_OnClick`，**直接携带被点的组件引用**，业务不用字符串匹配。

```
场景里的 Tip（预制体实例）
   │  Awake
   ▼
HK_TipSystem.TipsList（自动注册，无需手动收集）
   ▲                          ▲
   │ 指令 CMD_TIP_Show/Hide…  │ 每帧：距离 → alpha（淡入淡出）
   │                          │      ShowWithAnim/HideWithAnim → 生长动画
业务代码 ──SendCommand─────────┘
业务代码 ──RegisterEvent<Event_TIP_OnClick>── 点击（携带 _tip 引用）
```

---

## 资产构成

| 文件 | 职责 |
|---|---|
| `Scripts/System/IHK_TipSystem.cs` | 系统接口 + 系统实现 + 版本日志 + `HK_TipGroup` 分组枚举（核心） |
| `Scripts/ViewControllers/HK_TipsCtrl.cs` | 单 Tip 主控：初始化 / 距离显隐 / 朝向 / 缩放 / 淡入淡出 / 生长动画 / 点击转发 / 曲线数学 |
| `Scripts/ViewControllers/HK_TipTitle.cs` | 标签悬停放大 + 点击震动 + 点击回调转发 |
| `Scripts/ViewControllers/HK_TipCamera.cs` | 相机注册标记：挂到驱动相机上即注册（多相机切换、销毁自愈） |
| `Scripts/ViewControllers/HK_TipLine.cs` | 编辑器下的折线预览（`#if UNITY_EDITOR`，不进包） |
| `Scripts/ViewControllers/HKTips_BaseController.cs` | 架构基类（`IController`，接 QF） |
| `Scripts/ViewControllers/Test_UseHKTips.cs` | OnGUI 测试面板 + 用法示例，挂上就能跑 |
| `Scripts/Commands/CMD_TIP_*.cs`（7 个） | 指令层：全部 / 分组 / 列表显隐、批量可点击 |
| `Scripts/Events/Events_HKTips.cs` | 点击事件 `Event_TIP_OnClick` |
| `Scripts/Editor/HK_TipsCtrlEditor.cs` | Inspector 定制面板（卡片化分组 + 实时校验 + 中文 Tooltip） |
| `Prefabs/HK_Tip.prefab` | 预制体（结构约定见「注意事项」） |
| `HK_Tips.unity` | 演示场景（3 个 Tip + 相机 + 测试面板） |
| `Mas&Texs/HK_TipsLineMat.mat` | 连线材质（URP Unlit） |
| `Mas&Texs/HK_TipsTitle.png` | 标签底板图 |
| `Editor/ICON/` | 脚本图标，一般无需改动 |

---

## 使用说明

**1. 注册系统**（接入新项目时，在项目自己的 Architecture 里加一行）

```csharp
protected override void Init()
{
    this.RegisterSystem<IHK_TipSystem>(new HK_TipSystem());
}
```

演示场景已用 `HK_DefaultArchitecture` 注册好，直接跑即可。

**2. 摆预制体**：把 `HK_Tip.prefab` 拖进场景，摆到目标位置。**结构不要破坏**——根物体下必须有 `Line`（挂 LineRenderer，其下放 Pos 折点子物体）和 `Title`（挂 Collider + HK_TipTitle + TMP 文本）：

```
HK_Tip（根：HK_TipsCtrl）
├─ Line（LineRenderer）
│   ├─ Pos01        ← 锚点：连线起点（生长动画的"原点"）
│   ├─ Pos02        ← 弯点（弧线模式下是控制点，见「注意事项」）
│   └─ Pos03        ← 末端：标签所在
└─ Title（Collider + HK_TipTitle + Tmp_Title）
```

折点可以随意增减（至少保留 Pos01）；标签位置 = 最后一个折点上方「标签上浮高度」。

**3. 挂相机标记**：给驱动 Tip 的相机挂 `HK_TipCamera` 组件（不依赖 MainCamera 标签）。**不挂的后果**：Tip 不朝向 / 不按距离显隐，Console 会一次性报错指明缺什么。

**4. 调参数**（Inspector 已按卡片分组，鼠标悬停有中文说明）

- **基本信息**：所属分组（用于按组批量控制）
- **显示与交互**：最远可见距离、可点击、朝向相机、**生长动画（形状）**、**淡入淡出（透明度）**——两类动画的区别见「注意事项」
- **距离缩放**：远处自动放大保证可读（生效距离区间 + 缩放范围）
- **弧线连线**：直线改平滑弧线（每段采样数 + 弧度滑条）
- **高级设置**：标签上浮高度、连线宽度（取消「自定义」勾选则用内置默认值 0.2 / 0.04）

**5. 用指令控制**（推荐入口，全部走 `this.SendCommand`）

```csharp
// 全部显隐
this.SendCommand(new CMD_TIP_ShowAllHKTips());
this.SendCommand(new CMD_TIP_HideAllHKTips());

// 按分组（分组在 Inspector「基本信息」卡里配）
this.SendCommand(new CMD_TIP_ShowTipsByGroup(HK_TipGroup.TipGroup01));

// 按列表（精确控制哪几个）
List<HK_TipsCtrl> tips = this.GetSystem<IHK_TipSystem>().TipsList;
this.SendCommand(new CMD_TIP_ShowTips(tips));

// 批量禁点 / 放点（防重复触发）
this.SendCommand(new CMD_TIP_SetAllTipsClickable(false));
```

**6. 响应点击**（事件直接给组件引用，三种用法）

```csharp
this.RegisterEvent<Event_TIP_OnClick>(args =>
{
    HK_TipsCtrl tip = args._tip;

    // ① 取上下文：物体名（运行时 Instantiate 的带 (Clone) 后缀）与分组
    HKDebug.Log($"点击：{tip.name}（{tip.tipGroup}）");

    // ② 直接操作被点对象：点击即隐藏 + 禁点（防重复）
    tip.HideWithAnim();
    tip.SetIsCanClick(false);

    // ③ 按引用比较识别特定展品（推荐：改名不失效、重名不混淆）
    //    public HK_TipsCtrl exhibitA;   // Inspector 拖入
    //    if (tip == exhibitA) { ShowExhibitADetail(); }
}).UnRegisterWhenGameObjectDestroyed(gameObject);
```

**典型用法：展品点击联动**

思路：点展品 A 的标签 → 隐藏 A 的标签并禁点 → 弹出 A 的详情面板；关闭面板时再放点。整个流程只用到「事件拿引用 → 调对象方法」，不需要任何字符串匹配，也不需要自己维护对象表。

---

## 注意事项

* **结构约定不能破坏**：根物体下必须有 `Line`（含 LineRenderer 与至少一个 Pos 折点）和 `Title`（含 Collider 与 HK_TipTitle）。结构坏了 Console 会打明确错误，**修好后 1 秒内自动恢复**，不用重载场景
* **HK_TipCamera 必填**：不挂 = 不朝向 / 不按距离显隐；多相机场景后启用者覆盖先启用者（切相机即切 GameObject 激活）
* **可见性两权分立**：`alpha` 只由距离驱动（走远必淡出、走近必淡入，与指令状态无关）；根物体激活只由指令驱动。所以「指令隐藏后走远再走近」不会自己回来——那是 `Show` 的语义：**Show 是许可，不是强制**；超距时 Show 不显示，回到距离内才淡入
* **两类动画的区别**（都在「显示与交互」卡，可同时开）：
  * **生长动画（形状）**：指令显隐时从原点（第一个折点）生长出来 / 缩回——变的是位置 + 缩放 + 连线
  * **淡入淡出（透明度）**：显隐时渐入渐出——变的是清晰度。距离显隐（走远走近）始终走它；生长动画关闭时，指令显隐也走它
* **弧线的奇数折点是「控制点」不是路点**：曲线朝它弯但**不到达**（防止尖角处弧往外乱扩）。弧度 0 时线在锚点间直连、奇数点不影响形状；想曲线穿过所有折点，请关闭「弧线连线」走直线 polyline
* **点击只响应屏幕指针**：`OnMouse` 体系覆盖鼠标 / 触摸（Windows 触摸屏走“触摸模拟鼠标”）；**XR 手柄交互需另接 IPointer 体系**，工具本身不依赖任何 VR SDK
* **连线材质是 URP shader**：分发到内置渲染管线的项目会显示粉色（淡入淡出的双属性兼容已做，换一个内置 Unlit 材质即可工作）
* **TMP 默认字体不含中文字形**：中文标签显示方块，换成项目中文字库（Unity 侧操作）
* **运行时 Instantiate 的 Tip**：物体名带 `(Clone)` 后缀，直接用物体名做标识时注意

---

## API 速查

### 指令（`this.SendCommand(new XXX())`）

| 指令 | 说明 |
|---|---|
| `CMD_TIP_ShowAllHKTips` | 显示全部 |
| `CMD_TIP_HideAllHKTips` | 隐藏全部 |
| `CMD_TIP_ShowTipsByGroup(group)` | 显示指定分组 |
| `CMD_TIP_HideTipsByGroup(group)` | 隐藏指定分组 |
| `CMD_TIP_ShowTips(List<HK_TipsCtrl>)` | 按列表显示 |
| `CMD_TIP_HideTips(List<HK_TipsCtrl>)` | 按列表隐藏 |
| `CMD_TIP_SetAllTipsClickable(bool)` | 批量设置可点击 |

### 系统层（`this.GetSystem<IHK_TipSystem>()`）

| 成员 | 说明 |
|---|---|
| `TipsList` | 当前注册的全部 Tip（只读；Awake 自动维护） |
| `HK_TipCamera` | 当前 Tip 相机（无注册返回 null） |
| `ShowAllTips / HideAllTips` | 全部显隐（内部走 `ShowWithAnim/HideWithAnim`） |
| `ShowTips / HideTips(List)` | 按列表显隐 |
| `ShowTipsByGroup / HideTipsByGroup(group)` | 按分组显隐 |
| `SetAllTipsClickable(bool)` | 批量可点击 |
| `AddTipItem / RemoveTipItem(tip)` | 手动入列 / 出列（正常不用，Awake/OnDestroy 自动） |
| `RegisterTipCamera / UnregisterTipCamera` | 相机注册（HK_TipCamera 内部调用，业务勿手动） |
| `LogMissingCameraErrorOnce()` | 缺相机诊断（只报一次） |

### 组件公开方法（`HK_TipsCtrl`）

| 成员 | 说明 |
|---|---|
| `ShowWithAnim()` | 带动画的完整显示（激活 + 距离重判 + 淡入/生长 + 缩放吸附） |
| `HideWithAnim()` | 带动画的完整隐藏（收缩动画播完才关闭） |
| `SetIsCanClick(bool)` | 设置可点击（初始化前调用会记录，初始化后生效） |
| `SetText(string)` | 运行时改标签文字；同时是「换内容」入口（刷新文本/渲染器缓存） |
| `RefreshLine()` | 重建折线与标签位置（运行时移动 / 增删 Pos 折点后调用） |
| `IsGrowAnimating` | 生长动画是否播放中（只读） |
| `tipGroup` | 公开字段：所属分组 |
| `TitleOffSet / LineWidth` | 生效值（关闭「自定义」时为内置默认 0.2 / 0.04） |
| `IsUseCurve / CurveSegments / CurveStrength` | 弧线配置（编辑器预览与运行时共用） |
| `Init()` | 初始化（幂等；失败可重试，业务一般不用调） |
| `static BuildLinePositions(...)` | 折线点计算（自定义折线需求时用） |

> 想要「瞬间激活 / 关闭」（不过状态机）用 QFramework 扩展 `tip.Show()` / `tip.Hide()`（即裸 `SetActive`）。

### 事件（`this.RegisterEvent<T>`）

| 事件 | 字段 | 说明 |
|---|---|---|
| `Event_TIP_OnClick` | `HK_TipsCtrl _tip` | 标签被点击，携带被点组件引用（直接操作对象 / 按引用比较识别） |

### Inspector 参数（选中 Tip 即可调）

| 参数 | 默认 | 说明 |
|---|---|---|
| 所属分组 | TipGroup01 | 按组批量控制用（枚举可随时拓展，加一个值就多一个分组） |
| 最远可见距离 | 15 | 超过即淡出隐藏（按标签归位位置算） |
| 可点击 | 关 | 关闭后不响应点击 |
| 朝向相机 | 开 | 关闭则保持预制体摆好的角度 |
| 生长动画 | **开** | 指令显隐的形状动画（见「注意事项」） |
| 生长动画时长 | 0.3 | 秒 |
| 生效距离 | (0,0) | 距离缩放的起止区间（**开缩放前必须设**） |
| 缩放范围 | 0.5 ~ 2 | 近小远大 |
| 弧线连线 | 关 | 平滑弧线开关 |
| 每段采样数 / 弧度 | 8 / 0.5 | 弧度 0=直连、0.5=标准弧、1=拉满 |
| 淡入淡出 | 开 | 透明度动画（见「注意事项」） |
| 淡入淡出时长 | 0.3 | 秒 |
| 自定义高级参数 | 开 | 勾选后才可调下面的上浮高度与线宽 |
| 标签上浮高度 / 连线宽度 | 0.2 / 0.04 | |

---

## 常见问题

**Q：跑起来了，但标签不动、不按距离消失？**
A：九成是没挂 `HK_TipCamera`。给驱动相机挂上，Console 的报错也会指明这一点。

**Q：Console 一直刷「初始化失败」？**
A：预制体结构被破坏了（缺 Line / Title / LineRenderer / Collider / HK_TipTitle / Pos 折点）。按错误提示补回结构，**1 秒内自动恢复**，不用重载场景。

**Q：点标签没反应？**
A：依次查——①「可点击」是否勾选；② Title 上 Collider 是否还在；③ 标签是否已被距离淡出（半透明以下时点击区域仍在，完全透明时子物体已关闭）。

**Q：弧线的弯比我摆的折点大很多，往外扩？**
A：那是旧的 Catmull-Rom 行为，已重写。现在奇数折点是**控制点**——曲线朝它弯但不到达；想让弯小一点就调小「弧度」，或把折点往预期弧顶方向挪。

**Q：中文标签显示方块？**
A：TMP 默认字体（LiberationSans）不含中文字形，换项目中文字库（Unity 侧操作）。

**Q：切场景后新场景的标签不受控？**
A：不存在这个问题——Tip 由 `Awake` 自动注册入列，切场景 / Instantiate / Additive 全部自动覆盖。如果看到不受控，先看 Console 有没有初始化失败（结构坏的自愈是 1 秒一次，恢复前指令照发但 Update 不跑）。

**Q：隐藏再显示，为什么不是淡入而是直接出现？**
A：旧版本的行为（隐藏时 alpha 没归零，显示时无淡可入），已修复——现在 `Hide` 归零 alpha，`Show` 从 0 淡入。

**Q：超距时按了显示，标签没出现？**
A：设计如此——`Show` 是许可不是强制：超距时不淡入也不播生长，回到距离内自动淡入。

---

## 外部依赖

* QFramework（工程内置）：系统架构、指令 / 事件总线、`Component.Show/Hide` 扩展
* DOTween：悬停放大 / 点击震动动画
* TextMeshPro：标签文本

---

## 维护注意

* **字段名即序列化名**：改 `HK_TipsCtrl` 的序列化字段名会使预制体 / 场景里已存的值丢失，只改文案不改名（面板显示名在 `HK_TipsCtrlEditor` 里改，不影响序列化）
* **LineRenderer 写入规矩**：`SetPositions` **不会恢复 positionCount**（只写到当前数量为止）——所有恢复 / 截断必须「先设 `positionCount` 再逐点 `SetPosition`」（`ApplyGrow` 与 `RestoreFullGrowState` 都是这个模式，改动时别改回 `SetPositions` 一把梭）
* **距离判定用归位位置**：按 `GetTitleTargetPosition()`（最后一个折点 + 上浮高度）算，不要用 `title_TF.position`——生长动画期间标题在滑动，用实时位置会在边界抖动
* **事件字段 `_小驼峰`**：`Event_TIP_OnClick` 的字段带下划线前缀是工程约定，新增字段保持一致
* **版本记录**：改 `IHK_TipSystem.cs` 头部的版本日志（同一天的改动合并成一条，不拆连号）
* **README 待同步**：根目录 README §8 的指令名 / 事件名还是旧版（`CMD_Xxx` / `Event_HKTipClick` / `tipName`），随文档批一起更新
