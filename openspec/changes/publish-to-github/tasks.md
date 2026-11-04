# Tasks

## 1. 备份与 LFS 历史迁移

- [x] 1.1 创建离线备份 `git bundle create ../UnityShader-pre-lfs-backup.bundle --all`，验证 `git bundle list-heads` 能列出 `master` 引用
- [x] 1.2 执行 `git lfs migrate import --include="*.ttc,*.ttf,*.otf,*.fbx,*.blend,*.png" --everything`，验证 `.gitattributes` 已生成且包含全部 6 类跟踪模式
- [x] 1.3 验证迁移结果：`git lfs ls-files` 覆盖 msyh.ttc、V8_Engine.fbx、blendermcp_work 渲染 PNG；`git show HEAD:Assets/_Fonts/msyh.ttc` 输出以 `version https://git-lfs.github.com/spec/v1` 开头（是指针不是实体）；`git lfs fsck` 通过
- [x] 1.4 收缩本地对象库 `git reflog expire --expire=now --all && git gc --prune=now`，验证 `du -sh .git` 明显下降且 `git log --oneline` 仍有 4 个提交（3 个原始 + 1 个预备提交；哈希可变、内容不变）

## 2. 配置远程与推送

- [x] 2.1 向用户获取远程仓库 URL，执行 `git remote add origin <URL>`，验证 `git remote -v` 显示预期地址
- [ ] 2.2 推送前检查：`git ls-remote origin` 输出为空（若有任何引用，停下来征询用户整合方式，不得 force push）；`git status` 干净；`git ls-files` 中无 Library/Temp/Logs/UserSettings 等目录
- [ ] 2.3 执行 `git push -u origin master`，验证命令成功退出且 `git status` 显示本地 master 与 origin/master 同步

## 3. 远端完整性验证（集成检查）

- [ ] 3.1 在临时目录 `git clone <URL>` 并执行 `git lfs pull`，验证 msyh.ttc、V8_Engine.fbx 等大文件实体还原（文件大小与本地一致，不是指针文本），完成后删除临时目录
- [ ] 3.2 对比远端与本地 `git log --oneline` 提交哈希序列完全一致，且 GitHub 网页上大文件标注 "stored with Git LFS"

## Workflow follow-up

- 完成并验证后归档本变更（/opsx:archive）
- 备份文件 `../UnityShader-pre-lfs-backup.bundle` 保留至确认线上稳定后再清理
