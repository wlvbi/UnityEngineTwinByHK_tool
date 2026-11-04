# Tasks

## 1. 前置：工作区整理

- [x] 1.1 将上一变更遗留的归档移动（`openspec/changes/` 下的移动）作为独立提交落盘，验证 `git status` 除本变更新文件外无其他未提交内容（README 提交保持纯净）

## 2. 撰写 README.md

- [x] 2.1 按 design D1 的六段式结构在仓库根目录撰写 `README.md`（标题/主视觉/简介与特性/技术栈表/快速开始/操作/目录结构/架构深链/第三方致谢），验证文件存在于根目录且各章节标题齐全
- [x] 2.2 逐条溯源核对文中的事实声明：Unity 版本对照 `ProjectSettings/ProjectVersion.txt`、URP 与包版本对照 `Packages/manifest.json`、功能与操作对照 `Assets/Scripts/EngineTwin/README_EngineTwin.md`，验证 README 中出现的每个版本号与功能声明都能在上述来源中找到出处（无虚构内容）
- [x] 2.3 验证图片与链接：主视觉相对路径指向 `Assets/逼真V8发动机工程渲染.png`、建模补充图指向 `blendermcp_work/final_v8.png`、内部文档链接指向 `Assets/Scripts/EngineTwin/README_EngineTwin.md`，验证三者在仓库中真实存在且 Markdown 引用语法正确

## 3. 发布与渲染验证（集成检查）

- [x] 3.1 提交并推送 `README.md`，验证远端 `origin/master` 包含 README.md（`git ls-remote` + 远端文件可访问）
- [x] 3.2 在 GitHub 仓库首页实测 README 渲染：图片正常显示、表格与深链可点，验证通过（若图片渲染失败，按 design D2 备选改 ASCII 路径方案补救并重新推送）

## Workflow follow-up

- 完成并验证后归档本变更（/opsx:archive）
