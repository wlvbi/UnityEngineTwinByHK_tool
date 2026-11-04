# Design

## Context

见 proposal.md。写作素材已勘定：

- **权威事实源**：`Packages/manifest.json`（URP 17.5.0、Input System 1.19.0 等）、`ProjectSettings/ProjectVersion.txt`（Unity 6000.5.2f1）、`Assets/Scripts/EngineTwin/README_EngineTwin.md`（架构、功能、操作、接入真实数据、注意事项——已有完整内部文档）
- **主视觉候选**：`Assets/逼真V8发动机工程渲染.png`（写实渲染，效果最佳）、`blendermcp_work/final_v8.png`（Blender 白模建模展示）
- **口径冲突**：内部文档写"本项目为内置管线"，但 manifest 是 URP 17.5.0 且有 `UniversalRenderPipelineGlobalSettings.asset`、近期提交名为"urpV8发动机仿真"——内部文档该说法已过时

## Goals / Non-Goals

**Goals:**

- 仓库首页第一屏讲清楚：这是什么、能干什么、怎么跑、用了什么
- 所有技术声明可溯源到 manifest/代码，不允许出现"大概是""据说"式的虚构内容
- 与内部 README_EngineTwin.md 分工：主 README 管"门面与入门"，细节深链过去

**Non-Goals:**

- 不翻译/重写 README_EngineTwin.md（保持原样，只加链接）
- 不虚构开源协议、不写英文/双语版本（除非用户审阅时要求）
- 不新增图片资源（只引用仓库已有 PNG，避免 LFS 数据膨胀）
- 不做 CI 徽章、不写"安装到本地的 Unity 版本管理器"之类的进阶教程

## Decisions

### D1: 结构采用"门面六段式"

标题+一句话 → 主视觉 → 项目简介/功能特性 → 技术栈表 → 快速开始（含 LFS 提醒）→ 操作说明 → 目录结构 → 架构与扩展（深链内部 README）→ 第三方致谢。

- **理由**：GitHub 访客 30 秒内要 get 到"是什么/多酷/怎么跑"；技术栈用表格方便扫读；深入内容全部深链，避免两份文档失同步
- **备选（否决）**：写成完整 wiki 式长文——与 README_EngineTwin.md 职责重叠，后续必然失同步

### D2: 主视觉用 `Assets/逼真V8发动机工程渲染.png`，建模管线用 `blendermcp_work/final_v8.png` 佐证

- 相对路径引用（`![...](Assets/逼真V8发动机工程渲染.png)`）。GitHub 对 unicode 文件名自动编码，可正常渲染；LFS 图片经 GitHub media 端点出图，README 内可显示
- **备选（否决）**：复制图片到 `docs/` 改 ASCII 名——多一份 LFS 数据、仓库多一份冗余；仅当 GitHub 实测渲染失败时才作为补救

### D3: 技术栈表分"渲染/架构/工具链"三类，只列有据可查的

- 渲染：Unity 6（6000.5.2f1）、URP 17.5.0、Shader（Assets/Shader 入门示例）
- 架构/运行时：QFramework（分层）、DOTween、TextMeshPro、Input System 1.19.0
- 工具链：Blender + BlenderMCP（`blendermcp_work/` 建模脚本与 .blend）、HKTools（HK_Tips / HK_Curve2D / HK_DebugTool）、Git LFS
- manifest 里的 `com.unity.ai.*` 等 Unity 6 预装包不列入"技术栈"（未在工程代码中实际使用就不背书），如需完整清单指向 `Packages/manifest.json`
- **理由**：README 的可信度来自"列出来的每一项都经得起点进去验证"

### D4: 口径冲突以 manifest 为准，主 README 写 URP，不改内部文档

- 主 README 技术栈写 URP 17.5.0（有 manifest + URP 资产 + 提交记录三重佐证）
- README_EngineTwin.md 的过时表述**不在本变更中修改**（超出"加 README"的范围），如需修正另行处理

### D5: 不写 License 章节，只写"第三方组件与致谢"

- 仓库自身未声明开源协议——不虚构；仅列 HKTools（MIT）、DOTween、TextMeshPro 等各自的许可事实
- 中文字体 `msyh.ttc`（微软雅黑）的版权提示保留一句：引用内部 README 已有的提醒（商业发布前替换为可再分发字体），这是已存在的事实陈述

## Risks / Trade-offs

- [README 声明与实际不符（如写错版本号/功能）] → 任务中要求逐条溯源核对（版本号以 manifest/ProjectVersion.txt 为准，功能以 README_EngineTwin.md 与代码为准）
- [GitHub 对 unicode 图片路径或 LFS 图渲染异常] → 推送后用网页实测截图验证；失败则按 D2 备选改 ASCII 路径
- [两份 README 日后失同步] → 主 README 深链细节、只保留稳定不变的入门信息，把"易变细节"全部留给内部文档
- [用户期望英文/双语版] → 以中文为默认假设（已记录在 proposal），审阅工件时可直接推翻，成本只在写作阶段

## Migration Plan

纯文档新增，无迁移。推送后在 GitHub 仓库首页确认渲染效果即可。

## Open Questions

- 是否需要开源协议（License 文件/章节）？——不影响 README 结构与任务拆分，留给用户日后决定
