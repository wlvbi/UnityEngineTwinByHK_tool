# Proposal

## Why

仓库已在 GitHub 公开（wlvbi/UnityEngineTwinByHK_tool），但没有 README——访客点进来看不到任何项目介绍、技术栈和运行方式，第一屏是空的。需要一份 README.md 让访问者快速理解这个 V8 发动机数字孪生项目是什么、用了什么、怎么跑起来。

## What Changes

- 在仓库根目录新增 `README.md`：项目介绍、功能特性、技术栈说明、目录结构、快速开始、操作说明、第三方组件与致谢
- 用仓库内已有渲染图作主视觉（相对路径引用，不外链、不重复存图）
- 内容基于项目实际事实（Unity 6000.5.2f1 / URP 17.5.0 / QFramework / HKTools / BlenderMCP 管线等），并与已有的 `Assets/Scripts/EngineTwin/README_EngineTwin.md` 互相链接、不重复其细节
- 修正口径：内部文档里"本项目为内置管线"的说法已过时，README 以 `Packages/manifest.json` 的 URP 17.5.0 为准（不动内部文档本身）
- 提交并推送到 GitHub，让 README 在仓库首页生效

## Capabilities

无能力（capability）增改。本变更是文档类工作（新增 README），不改变工程的任何运行行为；已在 `.openspec.yaml` 中设置 `skip_specs: true`。

### New Capabilities

（无）

### Modified Capabilities

（无）

## Impact

- **新增文件**：`README.md`（仓库根目录）
- **不改动**：Unity 工程代码/资源/场景；`Assets/Scripts/EngineTwin/README_EngineTwin.md` 保持原样
- **对外影响**：README 将出现在公开仓库首页，是项目的门面；文中一切声明必须与仓库实际内容一致
- **记录的假设**（可在审阅工件时推翻）：README 以中文为主（技术名词保留英文原名）；不虚构开源协议章节，仅列第三方组件各自许可（HKTools MIT、DOTween、TextMeshPro 等）；图片用相对路径引用仓库内已有 PNG
- **环境前置**：工作区当前有上一变更归档移动产生的未提交变更，需先独立提交，保持 README 提交纯净
