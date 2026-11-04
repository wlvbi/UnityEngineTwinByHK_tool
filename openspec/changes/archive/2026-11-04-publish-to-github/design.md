# Design

## Context

见 proposal.md（动机与范围）。与设计相关的现状约束：

- 本地 3 个提交（`master` 分支），**从未推送过**，无远程、无协作者——历史可以安全重写
- `.git` 当前 109MB；大二进制分布：`blendermcp_work/*.png` 约 28 张（合计 ~55MB）、`Assets/_Fonts/msyh.ttc`（19MB）、`Assets/Models/V8Engine/V8_Engine.fbx`（2.6MB）、若干 `.blend`；`*SDF.asset` 与 `StudioReflection.asset` 是 Unity YAML 文本（2MB 级，可压缩，保留在普通 git）
- `git-lfs` 3.7.1 已安装；`gh` CLI 未安装（不需要——用户在网页建好仓库）
- 远程仓库已存在（公开），URL 待用户提供

## Goals / Non-Goals

**Goals:**

- 首次推送后远程拥有完整版本历史，且大二进制自始至终以 LFS 指针形态存在于 git 对象库
- `.gitattributes` 一劳永逸：后续新增同类大文件自动进 LFS，不会再污染普通对象库
- 推送前留好本地安全网，任何一步可回退

**Non-Goals:**

- 不解决 msyh.ttc 的版权问题（用户已知并接受风险；见 Risks）
- 不改 Unity 工程内容、不做提交拆分或信息重写（除 LFS 迁移导致的哈希变化）
- 不配置 CI、不迁移到 `main` 分支命名（推送 `master` 原样；GitHub 首个推送分支即默认分支）
- 不做 LFS 配额扩容规划（当前用量远低于免费档）

## Decisions

### D1: 用 `git lfs migrate import` 重写历史，而不是只 `track` + `--renormalize`

- **选择**：`git lfs migrate import --include="*.ttc,*.ttf,*.otf,*.fbx,*.blend,*.png" --everything`，一次性把 3 个历史提交里的大文件全部转成 LFS 指针
- **理由**：仓库从未推送、无协作者，重写零外部影响；首次推送只传 LFS 指针 + 通过 LFS 协议传对象，git 对象库最小
- **备选（否决）**：`git lfs track` + `git add --renormalize .` 只对新提交生效，旧 3 个提交的大 blob 照样以普通对象推送（~109MB），首次发布就带着包袱，达不到目的
- migrate import 会自动生成/更新 `.gitattributes` 并改写全部引用；提交哈希变化，仅影响本地

### D2: LFS 只跟踪真二进制，Unity YAML `.asset` 留在普通 git

- **跟踪模式**：`*.ttc` `*.ttf` `*.otf`（字体）、`*.fbx`（模型）、`*.blend`（Blender 工程）、`*.png`（渲染图与纹理）
- **理由**：`msyh SDF.asset`、`StudioReflection.asset` 是 YAML 文本，git zlib 压缩率高，且保留文本 diff/merge 能力；LFS 化反而失去可比性。纯二进制进 LFS 才是净收益
- `*.png` 全局跟踪（含 HKTools/DOTween 的小图标）：规则简单、对后续新增纹理免维护；小文件进 LFS 的开销可忽略
- **备选（否决）**：按路径精确列举（`blendermcp_work/*.png` 等）——省不了几个对象，却要为每个新目录维护规则

### D3: 推送前用 `git bundle` 建离线备份，而不是只留 tag

- **选择**：迁移前 `git bundle create ../UnityShader-pre-lfs-backup.bundle --all`
- **理由**：`git lfs migrate import --everything` 会改写**全部**引用（含 tag），备份 tag 自身也会被改写、失去锚点价值；bundle 是迁移外的独立副本，真出事可整体恢复
- **备选（否决）**：`git tag backup` —— 会被 `--everything` 一起改写，等没有备份

### D4: 远程非空则暂停征询，绝不强推

- 推送前 `git ls-remote origin` 验证：输出为空 → 正常推送；有任何引用（比如建仓时勾了 README/.gitignore）→ **停下来问用户**如何整合（rebase 合并 vs 覆盖远程），不擅自 force push
- 公开仓库的 force push 覆盖面不可控，这个决定留给用户

### D5: 认证走 Git Credential Manager（https）或 ssh 密钥，失败再议

- 用户给什么协议的 URL 就用什么。Windows 上 Git 自带 GCM，https 推送通常弹浏览器登录即可
- 备选：ssh 需已有密钥并添加到 GitHub 账号；PAT 方式仅在 GCM 不可用时兜底

## Risks / Trade-offs

- [msyh.ttc 版权：LFS 不改变公开可获取性，微软字体禁止再分发] → 用户明确接受；若日后要撤下，需 `git filter-repo` 清除 + force push（再次历史重写），方案已预留 bundle 备份使清除可行
- [提交历史暴露个人邮箱 `15255564834@163.com`] → 用户默认接受；可后续在 GitHub 设置中启用 noreply 转发（不改已有历史）
- [LFS 免费配额 1GB 存储 / 1GB·月流量；公开仓库被克隆计人你的流量账单] → 当前载荷约 85MB 无压力；超额时 GitHub 会冻结 LFS 上传，届时需购买流量包或把渲染图移出仓库
- [`migrate import` 重写历史出错导致本地混乱] → 推送前 bundle 全量备份（D3）；重写后先 `git lfs fsck` + 抽查指针再推送
- [首次 LFS 上传中途断网/超时] → `git push` 可安全重跑续传 LFS 对象，无一致性风险
- [公开后被搜索引擎/爬虫收录，日后删除文件不等于消失] → 提交前逐项确认内容（blendermcp_work 建模脚本、渲染图均无秘密；文本扫描未发现密钥）

## Migration Plan

1. `git bundle create ../UnityShader-pre-lfs-backup.bundle --all`（安全网）
2. `git lfs migrate import --include="*.ttc,*.ttf,*.otf,*.fbx,*.blend,*.png" --everything`
3. 验证：`git lfs ls-files` 列表符合预期；抽查 `git show HEAD:Assets/_Fonts/msyh.ttc` 头部是 LFS 指针；`git lfs fsck` 通过；确认 `.gitattributes` 已生成
4. （可选）`git reflog expire --expire=now --all && git gc --prune=now` 收缩本地 `.git`
5. `git remote add origin <URL>`；`git ls-remote origin` 确认为空仓库（非空则按 D4 停下）
6. `git push -u origin master`
7. 远端验证：GitHub 网页确认文件与历史；大文件显示为 LFS；克隆一份到临时目录跑 `git lfs pull` 验证可还原

**回滚**：步骤 6 之前任何一步出错，直接用 bundle 恢复（解包回原目录或删目录重克隆）；推送之后的回退只能追加修复提交或 force push（公开仓库慎用）。

## Open Questions

- 远程仓库 URL（https 或 ssh）待用户提供——不影响方案，apply 时直接代入即可
