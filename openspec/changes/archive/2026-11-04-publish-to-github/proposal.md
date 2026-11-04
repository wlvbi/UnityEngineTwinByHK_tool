# Proposal

## Why

项目（Unity URP 发动机数字孪生）目前只存在于本地磁盘，没有任何远程备份，也没有对外展示入口。提交到 GitHub 公开仓库可以拿到异地备份、完整版本历史和作品可见性。用户已在 GitHub 上建好空仓库，现在要做首次发布。

## What Changes

- 配置 git remote 指向用户已创建的 GitHub **公开**仓库，并完成首次推送
- 引入 Git LFS：把大二进制（字体 `.ttc`、模型 `.fbx`/`.blend`、渲染 PNG、TMP SDF 字体资产）纳入 LFS 跟踪，避免普通 git 对象持续膨胀
- 用 `git lfs migrate import` 重写现有 3 个本地提交，使大文件在整个历史中都以 LFS 指针存在（首次推送体积最小）。提交哈希会改变——仓库从未推送过，无外部协作者，影响仅限本地
- 新增/更新 `.gitattributes`（LFS 规则），核对 `.gitignore` 与实际推送内容一致
- 推送后验证远端内容完整（LFS 对象齐全、Unity 工程可克隆）

## Capabilities

无能力（capability）增改。本变更是工具/运维类操作（仓库发布），不改变 Unity 工程的任何运行行为，不在 `openspec/specs/` 下引入行为规格；已在 `.openspec.yaml` 中设置 `skip_specs: true`。

### New Capabilities

（无）

### Modified Capabilities

（无）

## Impact

- **仓库历史**：3 个本地提交被重写，哈希变化（仅本地，无下游影响）
- **新增文件**：`.gitattributes`（LFS 规则）；`.gitignore` 视核对结果微调
- **GitHub 远程**：仓库公开可见可搜索；LFS 用量计入账号配额（免费档 1GB 存储 / 1GB 月流量，本次 LFS 载荷约 85MB，首次装得下；公开仓库被他人克隆会消耗你的月流量）
- **风险（已知，用户拍板接受）**：
  - `Assets/_Fonts/msyh.ttc` 是微软版权字体，LFS 只改变存储方式，公开可获取性不变，存在被 DMCA 举报下架的风险
  - 提交者邮箱（`15255564834@163.com`）将随公开提交历史暴露
- **依赖**：`git-lfs` 3.7.1 已安装；**远程仓库 URL 待用户提供**（https 或 ssh）；执行前需确认远程为空仓库
- **不改动**：Unity 工程代码、资源、场景均不修改
