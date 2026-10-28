# P4-02 控件保存与编辑历史接入（2026-10-25）

本批完成控件级保存、另存为、流保存、字节导出、AutoSave、Undo/Redo、Dirty 及未保存修改确认。编辑后会以新的 PDFium 阅读快照刷新页面、缩略图、选择和搜索；保留逻辑 `DocumentSessionId` 与阅读位置。功能包仍不依赖 Themes；默认主题增加四个按钮、未保存标识与可等待的确认界面，Usage 与统一 Demo 已接线。

## 公共行为

| 入口 / 状态 | 契约 |
| --- | --- |
| `SaveAsync(CancellationToken)` / `SaveCommand` | 原路径保存；没有本地目标时请求另存为；返回 `false` 表示取消 |
| `SaveAsAsync()` / `SaveAsAsync(string)` / `SaveAsCommand` | 请求路径或使用显式路径，成功后更新 `SavePath`、`Source`、`DocumentSource` 和显示名；后续 Save / AutoSave 使用新目标 |
| `SaveAsync(Stream)` | 在流当前位置写入并 Flush，保留流打开；成功清 Dirty，保留已有本地目标；失败可能有部分流输出 |
| `ExportDocumentAsync` | 返回自有 PDF 字节，不清 Dirty，不改变目标；与 `ExportPageImageAsync` 的 PNG 导出分开 |
| `ExecuteEditAsync` | 将宿主内容编辑回调作为一条 `PdfEditSession.ExecuteTransactionAsync` 事务；本批以 FreeText 验证。事务禁止保存、书签修改、嵌套事务和自身并行回调；净化 / 红删的控件 UI 不在本批范围 |
| `UndoAsync` / `RedoAsync` 及命令 | 回滚 / 重做内容并刷新画面，书签不回滚；回到保存状态时清 Dirty，新分支保持 Dirty |
| `IsDirty` / `IsEditing` / `IsSaving` / `CanUndo` / `CanRedo` / `Revision` / `SavePath` | Avalonia 可绑定状态；Revision 反映已同步到编辑会话的修订，书签 UI 改动先立即更新 Dirty，在编辑 / 导出 / 保存队列同步到会话 |
| `AutoSave` / `AutoSaveInterval` | 默认关闭、间隔 2 秒（100 ms–1 天）。只向已确定本地目标写入；字节 / 流来源须先成功另存为。防抖合并突发变更，与编辑 / 保存共用串行队列，最多一项自动提交在途 |
| `SaveRequested` / `DocumentSaved` / `SaveFailed` | 请求可取消或覆盖本地目标；成功事件携带目标、修订和是否自动保存；失败保留 Dirty / 历史并报告 typed 错误。成功重试清除旧错误；自动保存失败报告一次，后续编辑或显式保存可重试 |
| `FileDialogService` | 已接入 `CanSave` / `GetSavePathAsync`；未注入时用 Avalonia StorageProvider。文件原子保存要求本地路径；非本地存储由宿主提供 Stream |
| `RequestCloseDocumentAsync` | 返回是否获准关闭；等待在途编辑后检查 Dirty。关闭 / 替换可选择保存、放弃、取消。保存失败或取消选路径时保留当前文档 |
| `UnsavedChangesRequested` / `UnsavedChangesService` | 事件提供 `Decision` Task 优先于服务；否则使用默认主题提示。提示在编辑队列外等待；新请求或 Dispose 会取消旧提示，旧回答不能关闭新文档 |
| `RespondToUnsavedChanges` | 完成默认提示；功能包消费者可用自有模板 / 宿主 UI 调用此方法 |
| `CloseDocument` / `CloseDocumentAsync` / `DisposeAsync` | 有 Dirty 时关闭先请求确认；async 关闭等待资源释放。`Dispose` / `DisposeAsync` 是无条件资源释放，不弹提示。窗口宿主应先等待 `RequestCloseDocumentAsync` 成功，再真正关闭窗口 |

Ctrl/Cmd+S 保存，Ctrl/Cmd+Shift+S 另存为，Ctrl/Cmd+Z 撤销，Ctrl/Cmd+Y 或 Ctrl/Cmd+Shift+Z 重做；文字输入控件保留自己的按键，`AllowShortcuts=false` 关闭这些快捷键。

## 支持边界

- `IsReadOnly` 默认 `true`；设为 `false` 才能保存 / 编辑。保存能力现为 `IsImplemented=true`，由文档、宿主只读策略和实现边界决定 `CanExecute`。加密、签名、超过 256 MiB 或不支持的结构不进入写入会话，阅读能力继续保留；这不等于 P4-04 的完整加密 / 签名策略已完成。
- 纯阅读模式继续沿用随机访问加载与会话内书签。可写模式加载时建立编辑快照并回读私有 `/NTUserBookmarks`；已打开的纯阅读文档切换为可写后，可等待 `EnableEditingAsync()` 初始化。编辑快照会增加输入和历史内存，默认只读模式不承担这份整文件副本。
- 书签通过控件 API / 集合同步到编辑会话，持久化使用私有 catalog 块，不转成标准 Outlines；保存期间新增书签改动保持 Dirty，后续导出 / 保存包含新状态。只读会话中的临时书签本身不触发退出提示，启用编辑后纳入保存状态。
- 文件写入复用已有唯一同目录临时文件、原生重开验证和原子替换；本批验证 Windows 目标占用、跨平台非法目标、流失败 / 取消及临时文件清理。外部同长度改写、冲突解决、磁盘满、网络挂载、断电和崩溃恢复仍归 P4-03。
- 统一 Demo 增加“启用保存”和“自动保存”，本地 PDF 采用路径加载；退出窗口由独立宿主对话框确认，避免 PDF 页面隐藏时提示不可操作。Usage 的窗口关闭等待控件内确认。Demo 切换页面保留同一控件会话；没有新增批注 / 表单编辑工具。

## 最终验证

机器记录：[pdfviewer-p4-02-20261025.json](validation/pdfviewer-p4-02-20261025.json)。最终证据在 `artifacts/pdfviewer/p4-02-20261025/release/`；父目录是开发期尝试，不能混计为最终产物。

| 平台 / 入口 | 结果 | 实际范围 |
| --- | --- | --- |
| Windows 11 x64，.NET 10.0.13 | Core **176/176**、Headless **135/135** | 新增 `P402SavingTests` **22** 项，全部通过，无跳过 / 错误 |
| Ubuntu 26.04 WSL2 x64，同 IL | Core **176/176**、Headless **135/135** | 实际调用 Linux PDFium / Skia；本批未做 Linux 源码重新编译 |
| Windows / Ubuntu Xvfb 原生 | 各后端 **11/11**、窗口 **35/35** | 每个平台新增 **13** 项 `p402-*`，覆盖编辑、另存、Undo/Redo、书签、流、AutoSave、提示按钮、原路径保存与重开 |
| 两个全新缓存 NuGet 消费者 | **120/120 + 123/123 = 243/243** | 每个新增 **12** 项 `p402*`；实际 nupkg，无源码项目引用；功能包不取得 Themes |
| Usage / 统一 Demo Release | 零警告、零错误 | 最终 Demo 原生阅读 smoke 全部检查通过；P4 专项使用上行原生 probe 单独记录 |
| 独立输出核验 | **6** 份 PDF | pypdf 严格重开，逐项核对 FreeText Contents、实际 AP 字符编码及 ToUnicode、嵌入字体和书签；Poppler 六页渲染成功，Windows / Linux 对应输出的像素一致 |

新增回归覆盖：原路径 / 另存 / 流与自有字节导出、保存点与新分支、书签跨历史与重开、文件锁 / 目录目标 / 流故障 / 取消、请求事件与服务、三种提示决定、选路径取消 / 保存失败阻止关闭、替换取消恢复绑定、新旧确认竞争、Dispose 取消提示、等待在途事务、AutoSave 防抖 / 目标切换 / 停用 / 失败不循环、保存期间的书签改动、只读 / 加密 / 签名能力、真实主题按钮、输入快捷键所有权、事务回滚、保存路径通知内 Dispose 不发布旧 Source / 名称。原有六处修复回归随全套执行。

最终功能 DLL SHA-256：`3E4AAFE6D610DB91C6EBA8CBA6DDD0C1E38E8601C6FDD9159DF392B032BAE17E`。机器记录核对源码前后、实际执行 DLL、原生模块与包内 DLL。Windows / Linux 提示 PNG 和 Poppler A / B 页面已人工查看；原生界面图为 RenderTargetBitmap，不是桌面 scan-out。Poppler 的系统默认显示字体告警单独保留，嵌入 A / B 字形与实际 AP 已独立验证。

包消费者使用 43 个已校验缓存依赖的本地源和全新缓存；仅本次命令设置 `NuGetAudit=false`，没有修改项目或 CI 的审计配置，未宣称新增漏洞审计通过。

开发期记录保留：测试曾误用裁剪字体中不存在的英文长词，改成夹具已覆盖的 A / B；事务测试曾调用事务内明确禁止的净化入口，改用已支持的 FreeText；快捷键测试曾伪造未入树的 Source，改为真实模板 TextBox 路由。首个 Windows 包装器因沙箱不能读取 .NET 环境检查所需服务管理器而退出，之后以只读环境检查权限完成；首个 Linux 原生轮因缓存路径指向失败目录而触发下载 / SSL 错误，最终轮使用已验本地语料。失败记录没有删除。最终加固保存通知内重入释放和保存成功清除错误后，整套验证重新绑定最终 DLL。

## 后续门槛

P4-02 的功能接线已落地；大文件序列化时延 / 峰值及完整保存性能门槛尚未通过。下一步 P4-03 应先做外部变化检测、两平台完整故障注入与恢复，再推进 P4-04 / P4-05。G4 保持开放；Ubuntu 24.04 hosted CI、麒麟 x64/ARM64 实机、物理输入 / IME / 辅助技术、系统文件选择器实际交互及官方性能对照均未由本批结果替代。
