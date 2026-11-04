# V8 发动机数字孪生

基于 Unity URP 的 V8 发动机交互式数字孪生：可拆解查看的 3D 发动机、10 路传感器实时仿真、历史曲线、故障注入与悬浮标签监控——开箱即用的工业可视化原型。

![V8 发动机渲染](Assets/逼真V8发动机工程渲染.png)

## 功能特性

- **交互式 3D 查看**：环绕相机自由浏览，点击/悬浮部件高亮并弹出信息标签，`R` 一键复位视角
- **传感器仿真**：10 路传感器（转速、节气门开度、进气压力、冷却液温度、机油温度、机油压力、排气温度、发电电压、燃油压力、振动烈度）实时数值 + 30 秒历史曲线，带量程阈值与等级判定
- **故障注入**：一键模拟冷却液泄漏 / 机油泵磨损 / 发电机故障 / 漏油 / 曲轴轴承磨损，观察指标劣化与报警联动
- **工况控制**：启停发动机、自动工况 / 手动节气门切换
- **16 个逻辑部件**：部件目录（名称/描述/绑定传感器/标签方向）可在 Inspector 中可视化编辑
- **可接入真实数据**：仿真数据源为可替换的 System，对接 PLC / MQTT / OPC UA / 串口后监控 UI 无需改动（详见[接入说明](Assets/Scripts/EngineTwin/README_EngineTwin.md)）

## 技术栈

| 类别 | 技术 | 说明 |
|---|---|---|
| 渲染 | Unity 6（6000.5.2f1） | 游戏引擎 |
| 渲染 | URP 17.5.0 | 通用渲染管线（`Packages/manifest.json` 为准） |
| 渲染 | Shader | `Assets/Shader/` Shader 入门示例 |
| 架构 | QFramework | 分层架构（System / Model / View / Core） |
| 运行时 | TextMeshPro | 文本与中文字体（动态多图集） |
| 运行时 | DOTween | 动画补间（HKTools 动效） |
| 运行时 | Input System 1.19.0 | 新输入系统 |
| 工具 | HKTools | 工业 UI 工具库子集：HK_Tips / HK_Curve2D / HK_DebugTool（MIT） |
| 工具 | Blender + BlenderMCP | V8 模型建模管线（`blendermcp_work/` 脚本与 .blend 工程） |
| 仓库 | Git LFS | 字体/模型/渲染图等大二进制走 LFS 存储 |

## 快速开始

> 仓库大文件由 Git LFS 托管，克隆前请先安装 [Git LFS](https://git-lfs.com/)。

```bash
git lfs install
git clone https://github.com/wlvbi/UnityEngineTwinByHK_tool.git
```

1. 用 **Unity 6000.5.2f1**（Unity Hub 安装同版本）打开工程目录
2. 打开场景 `Assets/Scenes/SampleScene.unity`，点击 **Play**
3. 若场景被重置，依次执行菜单：
   - `EngineTwin/1. 导入 TextMeshPro 基础资源`
   - `EngineTwin/2. 一键搭建数字孪生场景`

## 操作说明

| 操作 | 效果 |
|---|---|
| 左键拖动 | 旋转视角 |
| 右键 / 中键拖动 | 平移 |
| 滚轮 | 缩放 |
| 点击部件 / 悬浮标签 | 查看部件详情 |
| `R` | 复位视角 |
| 底部栏 | 启停发动机 · 自动/手动节气门 · 注入故障 · 切换标签显示模式 |

## 目录结构

```
Assets/
├─ EngineTwin/           生成的资源（EngineTip 预制体、部件目录、材质）
├─ Scripts/EngineTwin/   核心代码（Core / Data / Model / System / View / Editor）+ 详细文档
├─ HKTools/              HKTools 功能子集（HK_Tips / HK_Curve2D / HK_DebugTool）
├─ Models/               V8 发动机模型（FBX）
├─ Shader/               Shader 入门示例
├─ QFramework/           QFramework 框架核心
└─ Scenes/               SampleScene
blendermcp_work/         Blender + BlenderMCP 建模管线（Python 脚本与 .blend 工程）
```

## 架构与扩展

采用 QFramework 分层架构：`System`（仿真与提示系统）→ `Model`（遥测数据与部件状态）→ `View`（拾取、高亮、标签、监控面板）。

![Blender 建模](blendermcp_work/final_v8.png)

架构详解、HKTools 接入细节、真实数据对接（PLC / MQTT / OPC UA / 串口）与已知注意事项见
**[Assets/Scripts/EngineTwin/README_EngineTwin.md](Assets/Scripts/EngineTwin/README_EngineTwin.md)**。

## 第三方组件与致谢

| 组件 | 许可 |
|---|---|
| [QFramework](https://github.com/liangxiegame/QFramework) | MIT |
| HKTools | MIT |
| DOTween（Demigiant） | 免费版授权 |
| TextMeshPro（Unity 官方） | Unity Companion License |

> **字体提示**：中文运行字体 `Assets/_Fonts/msyh.ttc` 为微软雅黑（Microsoft YaHei），仅限学习研究使用；商业发布前请替换为可再分发字体（如思源黑体）。
