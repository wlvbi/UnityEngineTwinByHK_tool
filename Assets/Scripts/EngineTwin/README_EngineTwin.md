# V8 发动机数字孪生 · HKTools 接入说明

## 运行

1. 打开 `Assets/Scenes/SampleScene.unity`，点击 Play。
2. 若场景被重置，依次执行菜单：
   - `EngineTwin/1. 导入 TextMeshPro 基础资源`
   - `EngineTwin/2. 一键搭建数字孪生场景`

操作：左键旋转 · 右键/中键平移 · 滚轮缩放 · 点击部件或悬浮标签查看详情 · `R` 复位视角。
底部栏可启停发动机、切换自动工况/手动节气门、注入故障（冷却液泄漏 / 机油泵磨损 / 发电机故障）、切换标签显示模式。

## 架构（QFramework 分层）

```
EngineTwinArchitecture  ← HK_ArchitectureProvider 指向这里，HKTools 与本项目共用一套架构
├─ System  IHK_TipSystem（HKTools）       悬浮提示标签系统
├─ System  IEngineSimulationSystem        仿真数据源（一阶惯性模型 + 故障注入）
├─ Model   IEngineTelemetryModel          9 路传感器当前值 / 30 秒历史 / 等级判定
└─ Model   IEnginePartModel               16 个逻辑部件、选中/悬停状态
```

| 目录 | 内容 |
|---|---|
| `Core/` | 架构、Controller 基类、Command、Event |
| `Data/` | 传感器定义（量程/阈值）、部件目录 ScriptableObject |
| `Model/` `System/` | 数据与仿真 |
| `View/` | 入口 Bootstrap、标签、拾取、高亮、环绕相机 |
| `View/UI/` | 监控面板、`HKCurveChart`（HK_Curve2D 适配层） |
| `Editor/` | 一键搭建工具 |

生成的资源在 `Assets/EngineTwin/`：`Prefabs/EngineTip.prefab`（HK_Tip 的预制体变体）、`Data/EnginePartCatalog.asset`（可在 Inspector 中编辑部件名称、描述、网格前缀、绑定传感器、标签方向）。

## 用到的 HKTools 功能

| 功能 | 调用位置 | 接口 |
|---|---|---|
| 部件提示标签 | `EngineTipPresenter` | `HK_TipsCtrl.SetText`（实时数值）、`tipGroup` 分组 |
| 标签显隐 | `EngineTipPresenter` | `CMD_TIP_ShowTipsByGroup` / `CMD_TIP_HideTipsByGroup` / `CMD_TIP_ShowAllHKTips` / `CMD_TIP_HideAllHKTips` / `CMD_TIP_ShowTips` |
| 标签点击 | `EngineTipPresenter` + `EnginePicker` | `Event_TIP_OnClick`、`HK_TipTitle.OnClickCallback` |
| 标签相机 | `EngineTwinBootstrap` | `HK_TipCamera` |
| 2D 曲线 | `HKCurveChart` | `HK_Curve2D.PlayShowAnim`（切换传感器时的生长动画）、`PlayFlowAnim`、`HK_Curve2dDrawer` 绘制 |

## 接入真实数据

把 `EngineSimulationSystem` 换成读取 PLC / MQTT / OPC UA / 串口（HKTools 自带 `HK_SerialPortSystem`、TCP/UDP 系统）的系统，
在收到数据后调用 `IEngineTelemetryModel.SetValue(...)` 与 `Sample(...)` 即可，标签、曲线、报警、事件记录无需改动。

## 对 HKTools 源码的改动（升级框架时需重新应用）

| 文件 | 改动 | 原因 |
|---|---|---|
| `GlobalSettings/HK_ArchitectureProvider.cs` | 指向 `EngineTwinArchitecture` | README 规定的接入步骤 |
| `12.HK_UGUITools/2.HK_Curve2D/Scripts/HK_Curve2D.cs.meta` | GUID 改为 `bc5f5e16…` | 原仓库两个脚本 GUID 重复，且与预制体引用不符（预制体脚本丢失） |
| `12.HK_UGUITools/2.HK_Curve2D/Scripts/HK_Curve2dDrawer.cs.meta` | GUID 改为 `22cf49bc…` | 同上 |
| `12.HK_UGUITools/2.HK_Curve2D/Scripts/HK_Curve2D.cs` | 测试热键包进 `#if ENABLE_LEGACY_INPUT_MANAGER` | 本项目只启用新输入系统，旧 `Input` 会每帧抛异常 |
| `9.HK_DebugTool/Scripts/Editor/HK_DebugToolEditor.cs` | 6000.5+ 不编译 | `GetInstanceID()` 在 Unity 6.5 为编译错误；仅影响「双击日志跳转」便利功能 |

只导入了用到的子集：QFramework 核心、DOTween（免费版）、HK_Tips、HK_Curve2D、HK_DebugTool、中文字体。
完整框架在 `D:\UnityProgram\_external\hk_unity_tools`（MIT 许可）。

## 注意事项

- **播放中不要修改脚本**：HKTools 的组件不支持热重载，播放中重新编译会导致 `HK_TipsCtrl` 报 NullReferenceException。
  建议在 `Edit → Preferences → General → Script Changes While Playing` 选择 *Recompile After Finished Playing*。
- **中文字体**：运行时由 `RuntimeFonts` 基于 `msyh.ttc` 创建多图集动态字体，不修改 `msyh SDF.asset`
  （编辑器下往持久化字体资产追加图集会触发重新导入，曾导致 D3D12 驱动崩溃）。
  `msyh.ttc` 为微软雅黑，商业发布前请替换为可再分发的字体（如思源黑体）。
- HK_Tip 原预制体的连线材质使用 URP 着色器，本项目为内置管线，变体中已替换为 `Sprites/Default`。
