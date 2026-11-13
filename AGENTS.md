
<!-- project-change-log:start -->
## 项目变更记录与本地提交

- 此节仅适用于本仓库。用户已为本项目启用自动本地提交：完成修改并通过相关验证后，无需重复询问，执行下面的功能提交与日志提交。用户当次要求先查看或暂不提交时优先遵循。
- 开始和提交前检查 Git 工作区与暂存区，仅提交当前任务的文件或代码块，保留其他修改；不得用全量暂存混入无关工作。无法可靠分离时说明阻碍。
- 先验证，再提交功能代码及相关测试/文档；提交信息说明具体行为变化。禁止用此流程自动 push、部署或改写历史。
- 读取并核实功能提交的完整 SHA、提交时间与文件清单，在根目录 CHANGELOG.md 的“正式记录”区顶部新增条目。记录修改完成时间、Git 提交时间、具体修改内容、验证结果和兼容性说明。按 changelog.config.json 的时区填写实际偏移，使用该文件的条目模板。
- 执行 node tools/build-changelog.mjs 和 node tools/build-changelog.mjs --check；然后只提交日志与生成的 CHANGELOG.html，标题说明关联的功能短 SHA。日志提交不再生成日志，避免自引用循环。
- 报告功能提交、日志提交及剩余未提交内容。失败保留现场、报告原因，不伪造检查或提交结果；功能已提交而日志失败时，补齐同一功能 SHA 的记录。
- CHANGELOG.md 是记录源，CHANGELOG.html 是可离线搜索的阅读页。首页介绍网页用途，记录最新优先，Markdown 页内阅读。HTML 由 tools/changelog-template.html.in 生成，不手工修改；tools/changelog-template.html 仅作兼容跳转入口。
- 调整项目名称/时区可修改 changelog.config.json 并重新生成。新对话继续遵循本节；可用 $project-change-log 维护此流程。这是代理任务收尾规则，不是后台文件监视器。
<!-- project-change-log:end -->
