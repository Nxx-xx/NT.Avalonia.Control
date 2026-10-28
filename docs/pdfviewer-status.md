# PdfViewer 桌面控件状态

最新验证日期：**2026-10-25，P4-03 真实存储、最终竞争与耐久协议**；SDK 10.0.402、Avalonia 12.0.3，包版本 `0.2.0-preview.2`。

本批接入原路径完整内容指纹和 typed 冲突，拒绝同长度 / 同 mtime 的外部改写、替换与删除，保留外部文件及 Dirty，AutoSave 暂停；新增私有同目录保存事务、跨进程协作锁及显式扫描 / 恢复 / 丢弃 API。恢复会重新核对目标、候选身份和 PDF，不重放不完整、冲突、无效或越界事务。保存提交后的清理失败不假装回滚成功文件。

Windows / Ubuntu 26.04 WSL 同 IL 均 **Core 189/189、Headless 138/138**，新增正式回归 **16** 项；原生后端各 **11/11**、窗口各 **40/40**。故障 / 受控崩溃矩阵 **68 + 72** 项通过，分别使用 80 / 85 个 owned 子进程；双包全新缓存 **125 + 128 = 253** 项通过。独立 pypdf 重开 / AP / 字体 / 书签核验和 Poppler 渲染 **60** 份有效输出，另有 **6** 个损坏 PDF 负对照。详见[P4-03 验证](pdfviewer-p4-03-validation.md)及[机器记录](validation/pdfviewer-p4-03-20261025.json)。

以上为首轮历史结果。当前新增真实 NTFS / ext4 满盘、SMB tree 断连、NFS 三秒断连重连和 Linux VM 三点断电恢复；v2 事务保留独立输出及实际被置换版本，Linux 增加目录 fsync，公开 PublishedConflict / DurabilityPending 和控件耐久失败状态。Windows / Linux Core 195、Headless 140，独立进程竞争各 24 检查，双包 261 项、独立 PDF 解析 318 份 / 不同内容渲染 86 份；最新细项以[存储专项](pdfviewer-p4-03-storage-validation.md)和[机器记录](validation/pdfviewer-p4-03-storage-20261025.json)为准。Windows VM 掉电按用户要求延期；永久 NFS 失联隔离、物理介质耐久、完整元数据、目标实机和 G4 继续开放。

## P4-02 交付历史（2026-10-25）


P4-02 已接入控件的原路径保存、另存为、流保存 / 自有字节导出、AutoSave、防抖串行提交、Undo/Redo、Dirty 与可等待的未保存修改确认。默认主题、Usage 和统一 Demo 已接线。写入限定 `IsReadOnly=false` 的未加密、未签名且至多 256 MiB 的受支持 PDF；默认纯阅读路径不增加编辑快照。书签随控件保存持久化，在可写会话重开回读，且不进入内容撤销历史。

最终 Windows / Ubuntu 26.04 WSL 同 IL 均 **Core 176/176、Headless 135/135**，新增 **22** 项控件回归；两平台原生后端各 **11/11**、窗口各 **35/35**（新增 13 项 P4-02）；双包全新缓存消费者 **120/120 + 123/123 = 243/243**。Usage / 统一 Demo Release 零警告、零错误，最终 Demo 阅读 smoke 全部通过；独立 pypdf / Poppler 验证六份保存输出的实际 AP / 字体 / 书签与两平台像素。详见[P4-02 验证](pdfviewer-p4-02-validation.md)和[机器记录](validation/pdfviewer-p4-02-20261025.json)。

P4-02 功能接线已交付；保存性能峰值、大文件时延、P4-03 冲突 / 崩溃恢复、P4-04 加密 / 签名和 P4-05 完整外部兼容矩阵仍开放，G4 未关闭。Ubuntu 本次是 WSL 同 IL / Linux 原生库与 Xvfb 窗口验证；Ubuntu 24.04 hosted CI、麒麟 / ARM64 实机、系统文件选择器实际操作与物理输入尚未验收。

## 六处问题修复历史（2026-10-24）


本轮修复独立审查复现的 6 个问题：撤销后分支编辑误判干净、清理后撤销丢书签却误判干净、书签恢复保存集合后仍脏、旧异步 FitB 请求覆盖新导航、XYZ 单轴跳转重置另一轴、流加载密码重试读到流尾。正式新增 16 个回归用例：Core 8 个（含暂存生命周期），Headless 8 个；Windows 与 Ubuntu 26.04 WSL 同 IL 执行均为 **Core 176/176、Headless 113/113**。独立原生窗口、双包及六场景重放的最终结果见[修复记录](pdfviewer-six-fixes-20261024.md)和[机器记录](validation/pdfviewer-six-fixes-20261024.json)。

Ubuntu 本轮为同一程序集在 WSL 中调用 Linux 原生库的回归，没有执行 Linux 源码构建、物理输入或 Ubuntu 24.04 hosted CI。当时 P4 控件接线尚未完成；最新接线结果见上方 P4-02。P3 性能与官方对照保持开放；下面计数为各自版本的历史证据。

P2 已交付完整阅读扩展：单页 / 连续 / 双页连续 / 双页四种页面布局，双页的封面单独 / 奇数页配对规则（`ShowCoverPage`）与按跨页步进的翻页；文档 `/PageLabels` 页标签（`PageLabels` / `CurrentPageLabel`，无标签回退页码）；页内链接提取与完整内部目的地（/XYZ 位置、缩放、拟合类型与 Fit 族视图参数）导航，URI / 文件 / 不支持动作一律不打开，`LinkActivated` 供宿主拦截，目录项同样携带完整目的地；会话内用户书签（标题 / 页 / 粗斜标志，与文档目录分开建模，默认模板新增「书签」页签与工具栏按钮）；触摸双指捏合缩放与固定优先级的快捷键契约（含 Ctrl+0/1/2/3、方向键 / 空格翻页）。接入见[使用说明](pdfviewer-usage.md)。用户书签的 PDF 持久化与回读已在 P4-01 于编辑会话侧交付，控件保存接线已由 P4-02 交付，范围见本页最新批次。

## P4-01 批次（2026-10-24，第十六批：有界撤销历史、统一编辑事务与用户书签 PDF 持久化）

P4-01 的三条线——有界 Undo/Redo、统一编辑事务、用户书签 PDF 持久化与回读——已实现并验证；dirty 判定升级为内容状态身份 + 书签叠加层：

- **有界撤销历史（F28）**：内容编辑（FreeText、净化、红删）各记一条历史，`UndoAsync` / `RedoAsync` / `CanUndo` / `CanRedo` / `UndoCount` / `RedoCount` / `HistoryBytes`；`UndoLimit`（每栈条目数，默认 100，钳 ≥1）与 `HistoryByteLimit`（留存快照字节，默认 64 MiB，钳 ≥64 KiB）双限，调低即裁剪，**最新一条撤销记录永远保留**（字节可因此超出单条预算，显式文档化）；新编辑清空 redo；undo / redo 计入修订号；空历史 undo / redo 抛 `InvalidOperationException` 且会话不变。快照按字节状态身份（`stateId`）记账，undo 回到保存点即恢复该身份。
- **统一编辑事务**：`ExecuteTransactionAsync(Func<CancellationToken, Task>, CancellationToken)` 把整组编辑合并为**一条**历史记录；失败或取消全量回滚字节、修订与 dirty（事务内提交的中间编辑一并回退，历史栈不动）；事务未产生变化不记历史、不清 redo；嵌套事务、事务内 undo / redo、保存与书签修改一律拒绝（`InvalidOperationException`，事务作用域经 AsyncLocal 门语义，外部并发调用正常排队等待）。事务体不得启动回调会话的并行工作（文档注释明确）。
- **用户书签单独规则（F13 / F28）**：`PdfEditUserBookmark` record 与 `SetUserBookmarksAsync` / `GetUserBookmarksAsync`。书签写入私有 catalog 块 `/NTUserBookmarks`（其他阅读器按规范忽略未知键）：开档时**剥离**进会话叠加层（快照与历史永远不含书签）、导出 / 保存时**注入**并经原生重开校验，重开即回读。书签不进撤销历史、undo / redo 永不回滚；修改计入修订与 dirty，相同集合 no-op；净化 / 红删成功时按策略清空会话书签（该清空同样不进历史）。校验：ID 非空且唯一、标题 1–512 UTF-16 码元且无 null、页索引零基在界内、标志仅 None / Bold / Italic；损坏 / 越界的持久化块开档抛 `InvalidDataException`，不静默丢弃。
- **Dirty 契约（F08）**：`IsDirty` 由修订号比较改为内容状态身份 + 书签脏标志——编辑、撤销到保存点（干净）、redo 离开保存点（脏）、保存失败保留、书签集变化 / 恢复全部有测试锁定；`SaveAsync` 成功同时清除两侧。
- **输出确定性（本批实测缺陷）**：注入输出曾因 PDFsharp 6.2.4 每次结构化保存都在 `PrepareForSave` 里无条件以全新 `PdfMetadata` 替换 catalog `/Metadata`（DocumentID / InstanceID 取每次打开的随机 Guid，锁定版本源码已核对）而逐次不同——两次 Export 字节不一，包消费者 `p4UndoBackToSavedStateIsClean` 实测抓住。修复：会话按状态**缓存注入输出**（`CommitContent` / undo / redo / 书签变更 / 事务回滚 / Dispose 全部失效）——同一状态的重复导出与保存**字节一致**；跨会话 / 跨序列化趟次的字节复现按 PDFsharp 行为保持诚实声明（README 与类注释明确），不在静默中假装可复现。书签语义比较（内容 + 书签集 + dirty）覆盖跨趟次场景。

新增公共 API：`PdfEditUserBookmark`、`PdfEditSession.UndoLimit` / `HistoryByteLimit` / `UndoCount` / `RedoCount` / `HistoryBytes` / `CanUndo` / `CanRedo` / `UndoAsync` / `RedoAsync` / `ExecuteTransactionAsync` / `GetUserBookmarksAsync` / `SetUserBookmarksAsync`、常量 `DefaultUndoLimit` / `DefaultHistoryByteLimit` / `MinimumHistoryByteLimit`。行为变化：净化 / 红删现在各记一条撤销历史并清空会话书签；`IsDirty` 语义按状态身份（undo 到保存点即干净）；书签随导出 / 保存注入输出且同状态输出字节一致。原 FreeText / 保存 / 导出契约与既有 API 不变。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **168/168** | 含 27 项新增：`P4UndoRedoTests` 18 项（往返字节 / 批注 / dirty、新编辑清 redo、更新撤销回前文本、步数淘汰、字节预算淘汰留最新、降限即裁剪与钳制、跨流 / 文件保存点 dirty、undo / redo 计修订、空历史与预取消、撤销恢复渲染外观、事务分组 / 失败回滚 / 无变化不记 / 嵌套拒绝 / 事务内禁止 undo / 保存 / 书签 / 降限、净化与红删各记历史且 undo 复原）+ `P4BookmarkPersistenceTests` 9 项（导出重开往返含 PDFium 重开、文件保存回读、书签跨 undo / redo 存活且不进历史、同集合 no-op 与保存点 dirty、输入校验全家族、开档剥离、10 类损坏块拒绝、手写有效块回读、同状态输出字节一致） |
| PdfViewer.Headless.Tests | **105/105** | 与上批同数复跑通过（本批无控件面改动） |
| PdfViewer 范围 Release 构建 | **零警告、零错误** | 功能包 / Themes / Core / Headless / PdfViewer.Usage；全解决方案与统一 Demo 构建被并行 Charts 会话 WIP（`HierarchyChartBase.cs`）挡住，与 PdfViewer 无关 |
| 统一 Demo 原生 `--pdf-smoke` | 未在本批添加 P4 检查 | 按用户指示本批暂不添加 Demo smoke 的 P4 项；上批 68/68 记录保持为历史证据，不混作本批结果 |
| 两个全新缓存 NuGet 消费者（功能包 / Themes） | **108/108 + 111/111 = 219/219** | 重打包的实际 nupkg、无源码项目引用；原 93 / 96 项全保留，各新增 15 项 `p4*`；`artifacts/consumer/` 下 `result.json` |

控件级保存流程、AutoSave 与「书签不加入批注撤销历史」的完整闭环归 P4-02；P5–P7 各编辑类型接入同一事务与历史，表单合并连续输入归 P6；崩溃恢复 / 故障注入归 P4-03，签名与加密策略归 P4-04，结构检查与双引擎回读归 P4-05。PERF15 的历史预算记账已随本批落地（步数 + 字节双限即时裁剪），序列化时间 / 保存峰值归 P4-02。

## P2 Windows 验证结果（2026-10-20）

本轮为 Windows 单平台软件验证；同一源码的 Linux 证据尚未执行，见下方剩余问题。

| P2 本次执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **98/98** | 含 6 项 P2 新增：页标签数字树 / 无标签回退、五类链接目标与 /XYZ 细节、命名目的地、空页链接、目录完整目的地 |
| PdfViewer.Headless.Tests | **41/41** | 含 8 项 P2 新增：四种布局与封面规则、跨页步进与按键、快捷键优先级、页标签状态、用户书签生命周期、内部链接点击导航、外链事件不导航、触摸捏合缩放 |
| 全解决方案 14 个测试项目 | **2967/2967** | PdfViewer 之外含 Charts 1868、Markdown 544、TreeDataGrid 328、VirtualKeyboard 88 等，全部通过 |
| Release 全解决方案构建（含 Usage / 统一 Demo） | **零警告、零错误** | `NT.AvaloniaControl.slnx` |
| 统一 Demo 原生 `--pdf-smoke` | **28/28 检查** | 原生窗口（1440×900、RenderScaling 1），含 7 项 P2 检查（单页 / 双页模式、跨页步进、页标签回退、书签增删、实际大小）；亮 / 暗渲染图已人工查看。机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者（功能包 / Themes） | **48/48 + 51/51 = 99/99** | 实际 nupkg、无源码项目引用；覆盖原生阅读、生产 FreeText 写入、编译 AXAML、TwoWay 绑定、布局与主题独立性。机器记录 `artifacts/pdfviewer-package-consumer-results.json` |

P2 新增公共 API：`PdfPageDisplayMode`、`ShowCoverPage`、`PageLabels` / `CurrentPageLabel`、`PdfLink` / `PdfLinkKind` / `PdfLinkDestination` / `PdfDestinationFit`、`PdfDocument.GetLinksAsync`、`LinkActivated` 与 `PdfLinkActivatedEventArgs`、`NavigateToDestination`、`ActualSize`、`UserBookmarks` 与 `AddUserBookmark` / `RemoveUserBookmark` / `RenameUserBookmark` / `SetUserBookmarkFlags` / `ClearUserBookmarks`、`AddUserBookmarkCommand` / `RemoveUserBookmarkCommand`、`PdfUserBookmark` / `PdfUserBookmarkFlags`。`PdfBookmark` 增加 `Destination`（原构造与成员不变）；`PdfPageLayout.Build` 为内部类型签名更新。默认主题新增 `PART_DisplayMode`、`PART_UserBookmarks`、`PART_AddUserBookmark`、`PART_RemoveUserBookmark` 部件与封面规则勾选。

## P2 剩余问题与待验范围

- **平台**：P2 回归已在 Windows x64 与 Ubuntu 26.04 WSL2 x64 双平台执行（见第十批；Linux 为 Xvfb CI 显示语义）。Ubuntu 24.04 CI 的 P2 回归尚未远端执行；麒麟 x64/ARM64、Windows ARM64/x86、多 DPI、多显示器、真实触摸屏与触控板硬件仍待设备验收。WSL 的 Xvfb 证据不是 Ubuntu 实机桌面，也不能替代 `ubuntu-24.04` hosted run。
- **输入实机**：触摸双指捏合走 Avalonia `Pinch` 手势（累计倍率自手势起始缩放、以捏合中心为锚点，结束 / 取消与选区冲突已处理，见收尾修复批）；触控板原生缩放手势（`PointerTouchPadGestureMagnify`）已接入并与 Ctrl/Cmd+滚轮共用缩放锚点路径。WP7 的可自动化部分（布局 × 旋转 × 混合页尺寸 × DPI 的定位、命中和阅读位置回归）已由 `PlatformMatrixTests` 4 项矩阵覆盖并关闭（见第十批）。轻点即链接 / 选择的触摸与鼠标差异、Windows IME 与 Linux IBus / Fcitx5 在搜索 / 密码框的真实行为、真实触摸屏与触控板硬件验收继续待验（设备验收清单）。快捷键契约目前由合成事件验证，真实键盘 / 读屏播报未验。
- **书签持久化**：用户书签为会话内模型，关闭 / 替换文档即清空；写入 PDF 与撤销历史按计划归 P4，G2 的 F13 保持部分状态。粗斜标志更新的绑定通知（`Weight` / `Style`）已在收尾修复批修正。
- **目的地边界**：完整 Fit 族参数已解析并参与导航（见下方 P2-02 收尾补充）；`/FitB` / `/FitBH` / `/FitBV` 为内容边界盒语义、部分 `/XYZ` 保留单轴（见收尾修复批）；钉点在滚动范围不足时被视口钳制（如末页下方内容不足一屏时内容盒居中 / `/FitBH` 顶边到不了窗口上缘），与主流查看器一致。远程跳转只报告文件名不跟随目标文档；命名目的地解析依赖 PDFium 名称树解析结果。双页模式的当前页跟踪取跨页左页，点击右页不改 `CurrentPage`。
- **P2-04 范围说明**：P2-04（细粒度工具可见性、侧栏布局 / 宽度、独立默认外观与 Fluent 资源映射、字符串本地化含错误 / 菜单 / 工具提示 / 无障碍名称）已于 2026-10-21 收尾批次完成，见下方补充。窄窗口以 `SidebarWidth` 与工具开关组合应对，未做独立断点式窄窗口布局。`Editing/*` 结构化写作后端面向开发者的异常消息按范围决定保持固定英文，不入字符串表；阅读面（`PdfDocument*`、`PdfDocumentSource`、`Controls/*`、`Interop`）已全覆盖。
- **历史开放项不因本批关闭**：G0（官方参考包 / 目标设备外部基线）、G1（Ubuntu 24.04 CI 实际 run）、完整 CJK / IME、表单 / 批注 / 保存 / 打印产品化（P4–P8）、物理输入与官方性能对照继续开放。像素证据仍为 Avalonia RenderTargetBitmap，不是桌面截图或实际 present 证据。

## P2-01 收尾补充（2026-10-20，同日第二批）

P2-01 的三个尾巴——FitHeight、可配置缩放上下限 / 步长、阅读位置保存接口——已实现并验证，至此 P2-01 全项完成（页面布局 / 封面规则、页标签、ActualSize 已在上表的 P2 主批交付）。

新增公共 API：`PdfZoomMode.FitHeight`、`FitHeight()` 与 `FitHeightCommand`、默认主题 `PART_FitHeight` 按钮、Ctrl/Cmd+3 快捷键；`MinimumZoom` / `MaximumZoom` / `ZoomStep` styled properties（默认 0.1 / 5 / 1.2，值须有限、缩放上下限须大于 0、步长须大于 1；`ZoomLevel` 强制拉回区间，抬高 `MinimumZoom` 超过 `MaximumZoom` 时同步抬高上限，后写入者获胜；`ZoomIn` / `ZoomOut` 按 `ZoomStep` 步进，命令 `CanExecute` 随边界更新）；`PdfReadingPosition` record 与 `CaptureReadingPosition()` / `RestoreReadingPosition()` 阅读位置保存接口（视口左上角页内锚点 + 缩放 / 模式 / 旋转，落在页间距时归属下方第一页；宿主自行持久化 record 字段后用公开构造函数重建）。`Fit` / `FitV` 目的地分别映射整页 / 适合高度；FitHeight 在双页模式取跨页较高页的高度适配，宽度不参与约束。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **98/98** | 复跑通过，与上表同数 |
| PdfViewer.Headless.Tests | **44/44** | 含 3 项新增：FitHeight 高度适配（含命令与按钮）、缩放上下限 / 步长可配置、阅读位置往返与 FitV 映射（键盘契约补 Ctrl+3） |
| Release 全解决方案构建（含 Usage / 统一 Demo） | **零警告、零错误** | `NT.AvaloniaControl.slnx` |
| 统一 Demo 原生 `--pdf-smoke` | **31/31 检查** | 新增 fitHeight、readingPositionCapture、readingPositionRestore；机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者（功能包 / Themes） | **48/48 + 51/51 = 99/99** | 重打包的实际 nupkg、无源码项目引用；机器记录 `artifacts/pdfviewer-package-consumer-results.json` |

## P2-02 收尾补充（2026-10-20，同日第三批）

P2-02 的缺口分析确认链接提取、宿主拦截外链、URI / 文件 / 不支持动作处理与用户书签已在 P2 主批交付，剩余尾巴为**完整内部目的地的拟合视图参数**：`PdfLinkDestination` 现携带 `/FitR` 矩形（`Rectangle`）与 `/FitH` / `/FitBH` 顶边（`Top`）、`/FitV` / `/FitBV` 左边（`Left`）显示坐标（未定义参数为 null，原 4 参构造兼容）；`NavigateToDestination` 按拟合类型完整映射：`Fit` / `FitB` → 整页，`FitH` / `FitBH` → 适合宽度并把 `Top` 钉到窗口上缘，`FitV` / `FitBV` → 适合高度并把 `Left` 钉到窗口左缘，`FitR` → 矩形适配（缩放使矩形恰好放入视口、钉住左上角，受最小 / 最大缩放钳制），`Xyz` → 应用缩放后钉位置。钉点按轴精确放置、另一轴滚到目标页，滚动范围不足时被视口钳制（与主流查看器一致）。参数捕获经 PDFium `FPDFDest_GetView` 并映射到与 `PdfLink.Bounds` 同系的显示坐标（含裁剪与内置旋转）。至此 P2-02 全项完成。

新增公共 API：`PdfLinkDestination` 可选参数 `Rectangle` / `Left` / `Top`（record 扩展，原构造与成员不变）。行为变化：Fit 族目的地不再按页级回退，`FitH` / `FitV` 现在真正钉住边坐标（P2-01 记录的「FitV 映射适合高度」扩展为适合高度 + 左边钉点）。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **99/99** | 含 1 项新增：Fit 族目的地视图参数提取（FitR 矩形、FitH/FitBH 顶边、FitV/FitBV 左边，显示坐标系） |
| PdfViewer.Headless.Tests | **45/45** | 含 1 项新增：Fit 族目的地导航（FitH/FitBH 顶边钉上缘、FitV 左边钉左缘、FitB 整页、FitR 矩形适配与缩放钳制；语料第 3 页改横向大页保证横向滚动范围） |
| Release 全解决方案构建（含 Usage / 统一 Demo） | **零警告、零错误** | `NT.AvaloniaControl.slnx` |
| 统一 Demo 原生 `--pdf-smoke` | **34/34 检查** | 新增 fitHNavigation、fitVNavigation、fitRNavigation、fitRZoomClamp；机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者（功能包 / Themes） | **48/48 + 51/51 = 99/99** | 重打包的实际 nupkg、无源码项目引用（record 可选参数扩展，源 / 二进制构造兼容）；机器记录 `artifacts/pdfviewer-package-consumer-results.json` |

## P2-03 收尾补充（2026-10-21，第四批）

P2-03 的缺口分析确认跨页 / 反向选区已有回归覆盖、全选维持「选择当前页」文档契约，故「选区加固」以**复杂文字与大范围复制回归测试**交付（CJK 与代理对按码点原子选中、正反向一致；跨 30 页大范围选区整体复制，页间以 `Environment.NewLine` 连接），无行为变更。其余缺口实现为：

- **渐进搜索**：`PdfDocument.SearchPagesAsync` 以 `IAsyncEnumerable<PdfSearchPageResult>` 按页产出批次（无命中页也产出，便于进度上报），`SearchAsync` 由同一路径扁平化。控件每批即时发布 `SearchResults`（只读快照数组）与 `SearchResultCount` 并跳转首个命中。
- **进度 / 结果限制提示**：`SearchProgress`（已扫描页比例 0..1）随批次更新；`SearchResultLimit`（styled，强制 ≥1，默认 10,000）截断时置 `SearchResultsTruncated`、状态栏提示「已达上限」，且 `SearchProgress` 停在实际扫描比例而非虚报 1；完整扫描结束才置 1。
- **清除搜索**：`ClearSearch()` / `ClearSearchCommand` 取消进行中的搜索并清空查询、结果、进度与上限标志。
- **文本提取**：控件 `GetPageTextAsync` / `GetDocumentTextAsync` 与文档层 `GetFullTextAsync`（页间换页符 `'\f'`，与 pdftotext 约定一致）；禁止复制的受限 user 文档统一抛 `UnauthorizedAccessException`。
- **页面图片导出**：`ExportPageImageAsync(pageIndex, dpi)`（36..1200，越界抛 `ArgumentOutOfRangeException`）返回不透明 PNG，页内置旋转与裁剪生效。PNG 由新增内置编码器生成（RGBA + zlib/deflate + CRC/Adler 校验），不依赖平台成像服务——Avalonia.Headless 下 `Bitmap.Save` 会静默写出 0 字节，内置编码器使 headless 测试与原生窗口行为一致。

新增公共 API：`PdfSearchPageResult`、`PdfDocument.SearchPagesAsync` / `GetFullTextAsync`；控件 `SearchResults` / `SearchProgress` / `SearchResultsTruncated` / `SearchResultLimit` 属性、`ClearSearch()` / `ClearSearchCommand`、`GetPageTextAsync` / `GetDocumentTextAsync` / `ExportPageImageAsync`。`SearchAsync` 的返回与渐进发布值同为结果快照数组。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **103/103** | 含 4 项新增：分页搜索批次顺序与扁平结果一致、命中上限在所在页截断、全文 `'\f'` 连接、单页无分隔；受限文档 `GetFullTextAsync` 拒绝（契约补测） |
| PdfViewer.Headless.Tests | **50/50** | 含 5 项新增：渐进结果发布与上限截断的进度 / 状态语义、清除搜索；页面 / 全文提取与 PNG 导出（签名 + IHDR 尺寸 + DPI 边界）；CJK / 代理对选区（正反向 2 例）；跨 30 页大范围复制 |
| Release 全解决方案构建（含 Usage / 统一 Demo） | **零警告、零错误** | `NT.AvaloniaControl.slnx` |
| 统一 Demo 原生 `--pdf-smoke` | **44/44 检查** | 新增 9 项：searchTruncatedAtLimit、searchBindableResults、searchLimitStatus、clearSearchCommand、clearSearch、pageTextExtraction、documentTextExtraction、pageImageExport、searchProgressComplete；机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者（功能包 / Themes） | **48/48 + 51/51 = 99/99** | 重打包的实际 nupkg、无源码项目引用（新增 API 为可选 / 增量面，原构造与绑定不变）；机器记录 `artifacts/pdfviewer-package-consumer-results.json` |

## P2-04 收尾补充（2026-10-21，第五批）

P2-04 的四条线——细粒度工具可见性、侧栏布局 / 宽度、独立默认外观与 Fluent 资源映射、字符串本地化（含错误、菜单、工具提示与无障碍名称）——已实现并验证：

- **细粒度工具可见性**：11 个 styled 布尔开关（默认 `true`）：`IsOpenToolVisible`、`IsSidebarToggleVisible`、`IsNavigationToolVisible`（上一页 / 页码 / 下一页）、`IsZoomToolVisible`、`IsFitToolVisible`、`IsRotateToolVisible`、`IsCopyToolVisible`、`IsCloseToolVisible`、`IsDisplayModeToolVisible`（布局选择与封面规则）、`IsBookmarkToolVisible`、`IsSearchToolVisible`（整个搜索行）；默认模板按开关收起对应工具，`IsToolbarVisible` 仍整体隐藏工具栏。
- **侧栏布局 / 宽度**：`SidebarWidth`（styled `double`，默认 208，须有限且非负）由 `PdfViewerSidebarWidth` 资源播种、可逐控件覆盖；`SidebarPlacement`（`PdfSidebarPlacement.Left` / `Right`，默认 `Left`）经新增 `PART_BodyGrid` / `PART_Sidebar` / `PART_CanvasGrid` 部件切换网格列与分隔线方向。窄窗口按组合策略（调小宽度 / 收起侧栏与工具）应对，未做断点式独立布局。
- **独立默认外观与 Fluent 资源映射**：主题自带不依赖 Fluent 的亮 / 暗固定色板；`new PdfViewerTheme(useFluentResources: true)` 或合并 `Resources/Fluent.axaml`（`ResourceInclude`）把七色 `PdfViewer*Brush` 改写为 Avalonia Fluent `System*Color` token 包装刷（Background←SystemAltHighColor、Foreground←SystemBaseHighColor、MutedForeground←SystemBaseMediumColor、Border←SystemBaseLowColor、Toolbar←SystemChromeMediumLowColor、Canvas←SystemChromeMediumColor、Error←SystemErrorTextColor），随宿主亮 / 暗变体变化，独立色板保留为回退。Fluent 12.0.3 仅暴露颜色 token，映射层负责包 `SolidColorBrush`。
- **字符串本地化**：工具提示、按钮 / 页签文字、空态、加载、状态栏、文件对话框、上下文菜单、书签标题格式与阅读面错误消息集中到功能包静态 `PdfViewerStrings` 表（可读写属性，默认值与旧硬编码字面量逐字一致，测试锁定）；默认模板经 `x:Static` 在模板实例化时读取，C# 状态 / 错误走 `string.Format`（当前区域）。`PdfDocumentSource.FromBytes` / `FromStreamFactory` 新增 1 参重载，默认显示名取 `PdfViewerStrings.DefaultDocumentName`（原 2 参二进制签名保留）。
- **无障碍名称**：默认模板的按钮、输入框与勾选框全部提供 `AutomationProperties.Name`；带文字控件复用同一字符串表条目，符号按钮与页码 / 搜索框使用专用 `*AccessibleName` 条目。`Editing/*` 面向开发者的异常消息按范围保持固定英文，不入字符串表。

新增公共 API：`PdfViewerStrings`、`PdfSidebarPlacement`、`SidebarWidth` / `SidebarPlacement`、11 个 `Is*ToolVisible` styled 属性、`PdfViewerTheme(bool useFluentResources)`、Themes 包 `Resources/Fluent.axaml`、`PdfDocumentSource.FromBytes` / `FromStreamFactory` 1 参重载、默认模板 `PART_BodyGrid` / `PART_Sidebar` / `PART_CanvasGrid` 部件与全套 `AutomationProperties.Name`。原模板绑定与既有 API 不变。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **108/108** | 含 5 项新增：锁定状态 / 书签格式、锁定工具栏 / 对话框 / 无障碍字符串、锁定错误字符串、文档错误暴露字符串表值、字节 / 工厂来源默认显示名 |
| PdfViewer.Headless.Tests | **56/56** | 含 6 项新增：工具开关驱动默认模板、侧栏宽度 / 布局（含非法值）、字符串替换作用于状态 / 书签 / 无障碍名、默认无障碍名、Fluent 资源映射随宿主 token、Fluent 主题选项覆盖独立色板 |
| Release 全解决方案构建（含 Usage / 统一 Demo） | **零警告、零错误** | `NT.AvaloniaControl.slnx` |
| 统一 Demo 原生 `--pdf-smoke` | **49/49 检查** | 新增 5 项：toolVisibility、sidebarWidth、sidebarPlacement、automationNames、stringsSurface；机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者（功能包 / Themes） | **48/48 + 51/51 = 99/99** | 重打包的实际 nupkg、无源码项目引用（新增 API 为可选 / 增量面，原构造与绑定不变）；机器记录 `artifacts/pdfviewer-package-consumer-results.json` |

## P2 收尾修复批（2026-10-21，第六批：WP1–3）

P2 剩余工作 8 个工作包的前 3 项已完成（其余 WP4–8 见「P2 剩余问题与待验范围」后续批次）：

- **PNG 图片导出修复（WP1）**：修正内置 PNG 编码器 IHDR / IDAT 分块的 CRC-32 计算（此前 CRC 错误，只查文件头无法发现）；验证升级为独立解码 + 尺寸 + 像素校验——测试与原生 smoke 均用独立解码器解析导出字节，与 PDFium 参考渲染逐像素比对（PngExportTests 4 项；Demo `pageImageExport` 现为像素比对 793×1123）。
- **触摸与手势（WP2）**：注册 `PinchGestureRecognizer`；捏合按手势起始缩放计算累计倍率、以捏合中心为锚点缩放，处理手势结束 / 取消并抑制与文本拖选的冲突；接通触控板原生缩放手势（`PointerTouchPadGestureMagnify`）与 Ctrl/Cmd+滚轮路径共用缩放锚点逻辑（TouchGestureTests 4 项，合成指针事件验证）。真实触摸屏 / 触控板输入验证归 WP7。
- **链接目的地与书签边界（WP3）**：保留只定义一个坐标的 `/XYZ` 目的地（单轴值经坐标映射探测归属 `Left` / `Top`，随页面内置旋转确定轴；另一轴导航时保持当前滚动）；`/FitR` 在查看器旋转 90°／270° 时按交换后的宽高适配非正方形矩形，并按旋转后视图的左上角（逐象限选角）钉点；`/FitB` / `/FitBH` / `/FitBV` 补齐内容边界盒语义——新增 `PdfDocument.GetPageContentBoundsAsync(pageIndex)`（页内对象联合包围盒的显示坐标，无内容页返回 null，按页缓存），无内容页回退页框，`FitB` 适配内容盒并双轴居中、`FitBH` 适合内容盒宽度并把 `Top`（缺省内容盒顶边）钉到窗口上缘 + 水平居中、`FitBV` 适合内容盒高度并把 `Left`（缺省内容盒左缘）钉到窗口左缘 + 垂直居中，均为 Manual 缩放（特定倍率而非拟合模式）；书签 `Flags` 更新时同步通知 `Weight` / `Style`，主题粗斜体绑定即时刷新（DestinationBoundsTests 2 项、ContentFitTests 3 项；P2 内部链接导航测试的 FitB / FitBH 段按内容盒语义重写）。

新增公共 API：`PdfDocument.GetPageContentBoundsAsync`。行为变化：`/FitB` 家族由整页 / 页框适配改为内容边界盒适配（空页回退页框）；`/XYZ` 单轴目的地由丢弃改为保留单轴移动；`PdfUserBookmark.Flags` setter 现通知 `Weight` / `Style`。原 `PdfLinkDestination` 构造与成员不变。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **110/110** | 含 2 项新增：部分 `/XYZ` 单轴保留（含 `/Rotate 90` 页归属轴）、页内容边界盒联合与空页 null（含缓存） |
| PdfViewer.Headless.Tests | **67/67** | 含 11 项新增：PngExportTests 4（分块 CRC、独立解码、尺寸、像素比对）、TouchGestureTests 4（捏合累计倍率、锚点、结束 / 取消、选区冲突）、ContentFitTests 3（FitB 家族内容盒 / 空页回退、FitR 旋转宽高交换与钉角、书签粗斜通知）；P2 内部链接导航测试 FitB / FitBH 段按内容盒语义重写 |
| Release 全解决方案构建（含 Usage / 统一 Demo） | **零警告、零错误** | `NT.AvaloniaControl.slnx` |
| 统一 Demo 原生 `--pdf-smoke` | **49/49 检查** | `pageImageExport` 升级为独立解码 + 像素比对（793×1123，与 PDFium 参考渲染一致）；机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者（功能包 / Themes） | **48/48 + 51/51 = 99/99** | 重打包的实际 nupkg、无源码项目引用（新增 API 为增量面，原构造与绑定不变）；机器记录 `artifacts/pdfviewer-package-consumer-results.json` |

## P2 收尾修复批（2026-10-21，第七批：WP4 阅读交互契约）

补齐阅读交互契约（WP5–8 继续开放）：

- **默认密码输入、重试与取消**：密码加载失败（`PasswordRequiredOrIncorrect`）时默认弹出内置密码提示（`IsPasswordPromptVisible` direct property + `:password` 伪类）。`SubmitPassword(password)` 更新 `Password` 并用失败加载的同一读取通道 / 名称 / 来源重开；错误密码再次报错并重新弹出提示（重试由用户驱动，不自动循环）；`CancelPasswordPrompt()` 只收起提示、保留错误状态与重试数据，宿主可稍后 `SubmitPassword` 重试。新加载、加载成功、`CloseDocument`、`Dispose` 收起提示并丢弃重试数据。`IsPasswordPromptEnabled = false` 供宿主换用自有 UI（错误仍照常上报）。默认模板新增 `PART_PasswordBox`（回车提交 / Esc 取消）、`PART_PasswordSubmit`、`PART_PasswordCancel`，密码卡覆盖空态画布避免与空态面板重叠；`PdfViewerStrings.PasswordPrompt*` 5 条新字符串（标题 / 说明 / 确定 / 取消 / 输入无障碍名称与水印）。
- **整体禁用快捷键**：新增 `AllowShortcuts`（styled，默认 true），false 时整体关闭快捷键层——含 Ctrl/Cmd+O / F、F3、搜索框 Enter / F3 续查、阅读器全部快捷键与 Ctrl/Cmd+滚轮缩放（普通滚轮滚动不受影响）；`IsEnabled = false` 同样整体禁用。
- **公开选择状态与清除入口**：新增 `IsTextSelected`（direct，当前是否存在活动选择范围）与 `ClearSelection()`、`ClearSelectionCommand`（CanExecute 跟随 `IsTextSelected`）；Esc 快捷键改走同一公共入口；`AllowTextSelection = false`、文档清除 / 关闭同步复位状态。
- **页码 / 搜索完成事件与焦点优先级**：新增 `CurrentPageChanged`（每次页码变化触发，覆盖导航、滚动换页与宿主赋值）与 `SearchCompleted` / `PdfSearchCompletedEventArgs(ResultCount, Truncated)`（搜索自然完成触发，含达到上限提前停止；取消、被新搜索覆盖、空查询不触发）；键盘焦点优先级维持既有契约并以测试锁定。

新增公共 API：`AllowShortcuts`、`IsPasswordPromptEnabled`、`IsPasswordPromptVisible`、`IsTextSelected`、`SubmitPassword`、`CancelPasswordPrompt`、`ClearSelection`、`ClearSelectionCommand`、`CurrentPageChanged`、`SearchCompleted`、`PdfSearchCompletedEventArgs`、`PdfViewerStrings.PasswordPrompt*`（5 条）；默认主题新增密码提示部件。行为变化：Esc 清除选择改走公共 `ClearSelection()` 路径；`SubmitPassword` 更新 `Password` 时抑制属性驱动的重复加载（由显式重试接管）。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **111/111** | 含 1 项新增：密码提示 5 条字符串默认值锁 |
| PdfViewer.Headless.Tests | **73/73** | 含 6 项新增（ReadingContractTests）：密码提示错误密码重试后正确密码完成（模板回车 / 按钮路径）、取消保留错误且可事后重试、宿主关闭提示、AllowShortcuts / IsEnabled 整体禁用快捷键、选择状态与清除入口（API / 命令 / Esc / AllowTextSelection 复位）、CurrentPageChanged 与 SearchCompleted（含上限截断、空查询不触发） |
| Release 全解决方案构建（含 Usage / 统一 Demo） | **零警告、零错误** | `NT.AvaloniaControl.slnx` |
| 统一 Demo 原生 `--pdf-smoke` | **49/49 检查** | 与上批同数复跑通过；机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者（功能包 / Themes） | **48/48 + 51/51 = 99/99** | 重打包的实际 nupkg、无源码项目引用（新 API 为增量面）；机器记录 `artifacts/pdfviewer-package-consumer-results.json` |

真实读屏 / IME 对密码提示与搜索输入框的行为仍归 WP5 / WP7 实机验收，本批只覆盖合成输入与默认模板契约。

## P2 收尾修复批（2026-10-21，第八批：WP5 主题与窄窗口验收）

补齐主题与窄窗口验收项（WP6–8 继续开放）：

- **侧栏 overlay / offset 对齐（F16 / F53）**：新增 `PdfSidebarPresentation { Offset, Overlay }` 枚举与 `SidebarPresentation` styled 属性（默认 `Offset`）。`Offset` 侧栏独占一列、文档重新排版（`Auto,*` 或 `*,Auto` 随 `SidebarPlacement`）；`Overlay` 单列布局，侧栏浮在文档层之上（`ZIndex` 提升、按放置侧对齐左右缘），文档保持完整宽度——即窄窗口呈现。默认模板经 `PART_BodyGrid` / `PART_Sidebar` / `PART_CanvasGrid` 实现，两属性动态切换即时重排。
- **侧栏关闭释放 / 恢复**：隐藏侧栏取消进行中的缩略图解码等非必要工作；重新打开恢复可见缩略图请求并同步选中项（`PART_Thumbnails.SelectedIndex` 跟随 `CurrentPage`），符合 F16「关闭侧栏后释放非必要工作，恢复后选中项正确」。
- **窄窗口、长译文、大字体与字符串格式参数**：420×640 窄窗口（`SidebarWidth = 120`、Overlay 呈现、工具组收起）下导航 / 缩放 / 布局可用；80–200 字符替换字符串与 `FontSize = 28` 下工具栏与状态栏渲染正常；`StatusPageFormat` 重排参数（`"页码 {1}←{0}"` → 「页码 3←2」）正确。
- **键盘焦点顺序**：真实 Tab 按键（`Window.KeyPress/KeyRelease`）走查默认模板，焦点停靠点全部留在阅读器内，工具栏顺序锁定为打开 → 页码 → 搜索。

新增公共 API：`PdfSidebarPresentation`、`SidebarPresentation`。行为变化：默认主题侧栏布局改由 `UpdateSidebarLayout()` 按 Placement × Presentation 组合生成网格列（Offset 行为与既有实现一致，Overlay 为新增呈现）；侧栏显隐现在接管缩略图工作生命周期。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **111/111** | 与上批同数复跑通过 |
| PdfViewer.Headless.Tests | **78/78** | 含 5 项新增（ThemeAcceptanceTests）：offset / overlay 布局（含 Right 放置与切换回流）、侧栏关闭释放与恢复选中、窄窗口可用性（含 Overlay 宽度与工具组收起）、长译文 + 大字体 + 格式参数重排、真实 Tab 焦点顺序 |
| Release 全解决方案构建（含 Usage / 统一 Demo） | **零警告、零错误** | `NT.AvaloniaControl.slnx` |
| 统一 Demo 原生 `--pdf-smoke` | **49/49 检查** | 与上批同数复跑通过；机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者（功能包 / Themes） | **48/48 + 51/51 = 99/99** | 重打包的实际 nupkg、无源码项目引用（新 API 为增量面，Offset 默认行为不变）；机器记录 `artifacts/pdfviewer-package-consumer-results.json` |

按任务要求「已有无障碍名称不能代替实机验收」：读屏软件（焦点朗读、名称播报）与 IME 的实机行为不在本批合成输入覆盖范围内，仍归 WP5 / WP7 设备验收清单；窄窗口下的系统大字体（OS 缩放 / 系统字体设置）同样只覆盖控件内 `FontSize` 继承路径。

## P2 收尾修复批（2026-10-21，第九批：WP6 MVVM 示例与包消费）

补齐 P2-06 的示例与消费面（WP7–8 继续开放）：

- **Usage 示例展示 P2 宿主接入面**：`samples/PdfViewer.Usage` 新增布局（`PageDisplayMode` ComboBox）、侧栏呈现（`SidebarPresentation` offset / overlay ComboBox）、用户书签（`AddUserBookmarkCommand` / `RemoveUserBookmarkCommand` + `UserBookmarks` 列表，粗斜体经 `PdfUserBookmark` 的 `Weight` / `Style` 通知即时刷新，双击跳转）、阅读位置（`CapturePositionCommand` / `RestorePositionCommand` 调 `CaptureReadingPosition` / `RestoreReadingPosition`，阅读器经 `CommandParameter` 传入、视图模型不持有控件引用，`PdfReadingPosition` 字段由宿主持久化）、渐进搜索结果与进度（`SearchResultCount` / `SearchProgress` 进度条 / `SearchResultsTruncated` 上限提示）、页面 PNG 导出（`ExportPageImageAsync` 144 DPI → 临时目录）。示例 README 补充对应说明。
- **双包消费者实际调用 P2 新 API**：`scripts/Consumers/PdfViewer` 两个全新缓存消费者在既有检查之外新增 **27 项 P2 契约检查**（`p2*`），实际调用页面布局模式与封面规则、用户书签增删改与绑定通知、阅读位置捕获 / 恢复往返、渐进搜索结果 / 进度 / 完成事件载荷 / 上限截断 / 清除、页面 PNG 导出（签名 + IHDR 尺寸 + 分块 CRC 独立校验）、选择状态与清除入口、`AllowShortcuts` 开关、`SidebarPresentation` 枚举，以及密码提示完整流程（错误分类 → 提示弹出 → 错误密码重试 → 取消 → 事后 `SubmitPassword` 打开）。消费者 fixture 新增确定性密码保护 PDF（V1 R2 RC4 40-bit）生成器。此前 99 项检查主要证明旧契约未被破坏，本批补充的 27 项按名字验证 P2 新 API 在还原的 nupkg 上真实可用。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Usage Release 构建 | **零警告、零错误** | 含 compiled bindings 与 CommunityToolkit 命令源生成 |
| 两个全新缓存 NuGet 消费者（功能包 / Themes） | **75/75 + 78/78 = 153/153** | 原 48 + 51 项旧契约检查全部保留，新增 27 项 `p2*` 实调检查（密码提示流程含错误密码重试与取消后重试）；机器记录 `artifacts/pdfviewer-package-consumer-results.json` |
| PdfViewer.Core.Tests / Headless.Tests | 与第八批同数 | 本批未改动功能包源码（仅示例与消费者模板）；Demo / 全解决方案构建当时受并行 Charts 会话的 `HierarchyChartBase.cs` 编辑影响，PdfViewer 三项目单独构建零警告零错误 |

消费者计数说明：`Consumer[].CheckCount` 由检查名字典动态统计，脚本未硬编码期望值；48 / 51 → 75 / 78 的增量即本批 27 项 P2 检查。Demo 原生 smoke（49/49）沿用 2026-10-21T13:27Z 复跑结果——WP6 只改示例与消费者模板，功能包与 Themes DLL 自该次构建后未再变化，消费者以重打包的同一 DLL 通过全部 153 项。

## P2 收尾修复批（2026-10-22，第十批：WP7 Windows／Linux 验收与布局×旋转×混合页尺寸×DPI 回归）

WP7 的可自动化部分（布局 × 旋转 × 混合页尺寸 × DPI 的定位、命中和阅读位置回归）此前唯一失败项命中真实产品缺陷，本批修复并补齐双平台证据。Windows IME、Linux IBus／Fcitx5、触摸和触控板硬件继续留在设备验收清单，Ubuntu 24.04 CI 仍未远端执行（见剩余问题）。

- **产品修复：导航后视口裁剪状态过期**（`Controls/PdfViewer.Interaction.cs` 的 `TryApplyNavigation`）。根因：Avalonia `ScrollViewer.ScrollChanged` 合并到 `LayoutUpdated` 触发，并在其事件处理器跑完后才把当前偏移记为 `_oldOffset` 基准（反编译确认 `OnLayoutUpdated → RaiseScrollChanged`）；`TryApplyNavigation` 在该事件链内应用目标偏移时，偏移变化被吸收、不再产生后续 `ScrollChanged`，`PdfDocumentPresenter._viewport` 停留在导航前的偏移（实测视口 `(0, 1359, 790, 521)` 对 `scroll.Offset == (0, 0)`），`Render` 按过期视口裁剪行 → 文档顶部空白、顶部链接点击落到 `ScrollContentPresenter`。修复：应用偏移后立即 `RefreshViewportWithoutPageChange()`（与 `GoToPage` 尾部同一路径；重入安全，pending 导航已清空）。修复后故障序列（深度缩放 + 导航 + 旋转 + 缩回）无需手动刷新即完全恢复（draw list 命中、窗口命中链回 `PdfDocumentPresenter`、渲染行覆盖全高度）。
- **WP7 布局矩阵回归**：`PlatformMatrixTests` 4 项——`Hit_testing_follows_rotation_on_mixed_page_sizes`（0/90/180/270° × 混合页尺寸：旋转后页面尺寸断言、链接矩形映射、真实点击命中源为呈现器、`LinkActivated` 携带 `/FitR` 目的地并导航至第 2 页）、`Reading_positions_roundtrip_across_rotation_and_mixed_sizes`（窄竖版 × 旋转捕获与横版独立恢复）、`Layout_hit_testing_and_reading_position_survive_render_scaling`（RenderScaling 1.0/1.5/2.0 下几何不变、点击导航与位置往返）、`Spreads_keep_mixed_sizes_side_by_side_under_rotation`（双页混尺寸并排与旋转换维）。点击点取链接矩形顶部内点：旋转 180° 时矩形中心恰落在视口底缘的 Fluent 覆盖滚动条上，遮挡属正确 chrome 行为（探针确认命中 `PART_HorizontalScrollBar` 的 `TrackRect`），顶部内点在同一链接内且无 chrome。
- **双平台原生证据**：Windows（`artifacts/pdfviewer-native/windows-20261022T021311Z`）与 Ubuntu 26.04 WSL2 x64（`artifacts/pdfviewer-native/linux-20261022T022442Z`，Xvfb CI 显示、锁定还原 / 构建、PDFium／Skia／HarfBuzz ELF 头／动态依赖／版本符号与 `ldd` 全过，运行时 `/proc/self/maps` 资产一致性审计含在内）各：后端 **11/11**、原生窗口 **22/22**、Core **111/111**、Headless **82/82**。Linux 首轮（`linux-20261022T022038Z`）因环境缺 CJK 字体在 `native-cjk-font-4E2D` 前置失败（窗口句柄已建立），保留为失败证据；装入 Noto CJK 后复跑全过并记录实际中文字族匹配。WSL 环境本轮自建：用户级 .NET SDK 10.0.402、Xvfb + X11／GL／freetype／libicu／Noto CJK（root apt）；Xvfb 是 CI 显示语义，不是麒麟 / Ubuntu 实机桌面。

| 本批执行（Windows 11 x64 26200 + Ubuntu 26.04 WSL2 x64，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **111/111** | Windows 与 Ubuntu WSL 同数同源 |
| PdfViewer.Headless.Tests | **82/82** | 78 项既有 + 4 项 WP7 布局矩阵（旋转 × 混合尺寸 × DPI 的定位 / 命中 / 阅读位置） |
| PdfViewer 三项目 Release 构建 | **零警告、零错误** | 功能包仅改 `PdfViewer.Interaction.cs` 一处 |
| 统一 Demo 原生 `--pdf-smoke` | **49/49 检查** | 2026-10-22T02:10Z 复跑（产品 DLL 已变）；机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者 | **75/75 + 78/78 = 153/153** | 重打包 + 全新缓存复跑，各含 27 项 `p2*`；`artifacts/consumer/` 下 `result.json` |
| Windows 原生证据 | 后端 **11/11**、原生窗口 **22/22**、Core **111/111**、Headless **82/82** | `artifacts/pdfviewer-native/windows-20261022T021311Z` |
| Ubuntu 26.04 WSL2 原生证据 | 后端 **11/11**、原生窗口 **22/22**、Core **111/111**、Headless **82/82** | `artifacts/pdfviewer-native/linux-20261022T022442Z`；Xvfb 显示，非实机 |

产品与包绑定：`NT.AvaloniaControl.PdfViewer.dll` SHA-256 `63A72574F4E7B149939792F167B68535C4C7EBF5E60ADEDB9C6C7B99AC911762`（369,664 字节）；`NT.AvaloniaControl.PdfViewer.Themes.dll` SHA-256 `664881CBDC4034F3F936D788E04B99003D662C095E424C2637E2DF9749414C45`（46,592 字节）；`NT.AvaloniaControl.PdfViewer.0.2.0-preview.2.nupkg` SHA-256 `213FEDA00F6189618F97F1191B0D6FEFB4F8BDCC740B6617C2FA3B77BEFAA9DB`、`NT.AvaloniaControl.PdfViewer.Themes.0.2.0-preview.2.nupkg` SHA-256 `46B909559D0D622A7B5F56CD7DC247A5B9B127B5C1194321846ABE70DBB0BB41`。原生证据内 `source-before.json` / `source-after.json` 记录 95 份源文件哈希前后一致。本批不重跑 P0 字体 / 表单 / 红删大矩阵，其历史冻结数字不沿用为本批结果。

**P2 冻结（WP8）**：上述源码 / DLL / 包 / 测试 / 原生证据即 P2 收口绑定，WP1–7 的产品与文档落点见本文件对应批次。G2 逐条判定与「自动化门槛具备证据、待外部验收（Ubuntu 24.04 CI 远端 run、IME / IBus·Fcitx5 / 触摸触控板实机）闭合后终判」的结论见[开发计划](pdfviewer-development-plan.md)P2 首轮实施节；官方矩阵阅读面状态列已同步（[对齐矩阵](pdfviewer-official-alignment.md)）。

## P3-05 批次（2026-10-23，第十五批：性能指标记录、资源审计与内部回归）

P3-05 的四条线——飞行记录契约与完整埋点、资源审计、真实呈现探针、可复现 benchmark 工具链——已实现，内部回归（Windows + Linux WSL 补充 + 软件渲染子集）已出具结果；同机官方 A/B 对照按诚实规则保持 `blocked-reference`：

- **飞行记录契约（PERF01）**：公开 `PdfPerformanceSession`（进程级 ambient，`Begin` / `Record` / `NextCorrelationId` / `SampleResources` / `Totals`）、`PdfPerformanceStage`（InputRead / OpenParse / QueueWait / NativeExec / DecodeComplete / BitmapUpload / LayoutBuild / Search / CancelObserved / Release / UiSubmit / CompositorRendered）、`PdfPerformanceSpan` / `PdfPerformanceMark`（monotonic 起止 + 关联 ID，mark 名进程内唯一）。milestone 常量区分 `LoadStart` / `LoadReturn` / `FirstPageDecoded` / `FirstPageSubmitted` 与 **harness-only** 的 `FirstPagePresented` / `CloseRequested`：**产品代码永不写 present mark**（消费者与 smoke 双重锁定），`RenderAsync` 返回从未记作首屏。会话为进程级飞行记录器，Core 套件以 `[assembly: CollectionBehavior(DisableTestParallelization = true)]` 串行执行（子秒级，避免并行测试污染开放会话；关联 ID 前缀语义，不按绝对序号断言）。
- **资源审计（PERF16）**：`PdfPerformanceResourceSample`（managed heap / allocated、private bytes、working set、句柄、暂存输入、可得 GPU 字节）可并入 `PdfViewerCacheStatistics`（预算 / 跟踪字节、页面 / 缩略图 / 文本 / 解码中间数组在途与峰值），`CaptureResourceSample` 直接入当前会话；采样**不强制 GC**。open-close-cycles 场景以 100 循环（warm 20、GC 后取样）测留存增长。PDFium 内部堆不可测保持诚实声明。
- **真实呈现探针（PERF17）**：`PresentProbe` 以 `ElementComposition.GetElementVisual(window)?.Compositor` → `RequestCompositionBatchCommitAsync().Rendered` 记录 present 潜伏，方法名 `composition-batch-rendered`、`scanOutVerified=false`——**不是扫描输出/物理呈现**，headless 与合成批渲染均不能证明真实 FPS。Demo `--pdf-smoke` 在真实窗口（1440×900）实测 composition present 并记录 `perfPresentMethod` / `perfPresentUnavailableReason`。
- **benchmark 工具链（PERF19）**：`benchmarks/PdfViewer.Benchmarks`（入 `NT.AvaloniaControl.slnx`）——每个 语料×场景×迭代 一个 worker 进程（诚实 process-cache `new-process`），父进程以 **nearest-rank**（不插值，<20 样本不出 p99）聚合；raw 记录带 JSON Schema（`benchmarks/pdfviewer/schema/perf-record.schema.json`）、语料 manifest SHA-256、`referenceAvailability: blocked-reference`、`qualityStatus: not-run-no-golden`；超时 / 崩溃保留 stdout+stderr stub 记录。10 场景（open-first-screen、jump-page-cached / uncached、scroll-continuous、search-first-hit / full-document、cancel-queued / native-render、close-release、open-close-cycles）× 3 语料（S0 2 页 / S1 20 页 / S2 1000 页，fixture 生成、真实哈希）。入口：`scripts/Test-PdfViewerPerformance.ps1`（统一，门槛表单一判定、仅测量失败非零退出）、`scripts/pdfviewer/benchmark.ps1|sh`（Linux 无显示时 Xvfb 兜底并标注）、`compare-baseline.ps1`（仅自身历史基线）。`--rendering-mode software` 强制 Win32/X11 软件渲染以供 G3 渲染方式拆分。门槛判定全部标注「工程初始门槛（待校准）」，信息性输出。
- **内部回归结果（原始记录见 [Windows](validation/pdfviewer-performance/windows-2026-10-23.md) / [Linux](validation/pdfviewer-performance/linux-2026-10-23.md)）**：Windows `windows-2026-10-23` 280/280 ok（firstScreenMs p50 231.4 / p95 354.8；search-full p95 68.7ms；cancel-queued p95 3.0ms；closeUiMs p95 8.2ms）；软件渲染子集 `windows-2026-10-23-soft` 90/90 ok；Linux WSL 补充 `linux-2026-10-23` 280/280 ok（firstScreenMs p50 185.7 / p95 348.4）。not-met 共 3 类结构性门槛：jump-page-cached 被 150ms settle 计时器垫底（门槛 100ms）、frame pacing 强制 commit 节拍（`scanOutVerified=false` 不读作 FPS）、open-close 私有字节增量跨进程噪声（Win p95 25.8MB / Linux 10.3MB）——详见[性能计划](pdfviewer-performance-plan.md)P3-05 实测瓶颈表。Linux 为 WSL2 + WSLg 会话**补充结果，非 Linux 实机**。
- **记录缺陷（本批发现并修复）**：`FindNativeLibrarySha` 曾跨 RID 取首个同名文件，两平台 run-env 的 `nativeLibrarySha256` 误指 win-arm64 二进制；实际加载 win-x64 `04100c03…` / linux-x64 `b0361f8b…` 已独立校验并在两份验证记录中声明；修复后按当前 RID 匹配记录（`windows-2026-10-23-soft` 已按修复版记录）。指标不受影响。
- **脚本修复**：`dotnet build --locked-mode` 被 SDK 10 拒绝（MSB1001），改为 `-p:RestoreLockedMode=true`；`Test-PdfViewerPerformance.ps1` 对 `-Scenarios a,b,c` 的 PowerShell 数组绑定改为规范化逗号列表（此前会零场景匹配）。

行为变化：新增公共 API `PdfPerformanceSession`、`PdfPerformanceStage`、`PdfPerformanceSpan`、`PdfPerformanceMark`、`PdfPerformanceResourceSample` 及 milestone 常量（`PdfPerformanceMarks`）；`CaptureResourceSample` 挂在现有 `PdfViewerCacheStatistics` 之上，不改既有阅读行为。产品渲染路径不写 present mark 的约定由 smoke / 消费者检查锁定。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **141/141** | 含 6 项新增 `PdfPerformanceTests`（session 记账 / 单例生命周期、队列等待与执行拆分含取消、加载 input→parse→返回 mark 链、渲染搜索释放关联 ID、资源采样跨生命周期可读）；并行关闭后 3 连绿 + 收尾复跑 |
| PdfViewer.Headless.Tests | **105/105** | 含 3 项新增 `P3PerformanceTests`（首屏 stage 链不声称 present、搜索首命中可由 span 测量、关闭 Release 与资源采样接缓存） |
| PdfViewer 全范围 Release 构建 | **零警告、零错误** | 产品 / Themes / Core / Headless / Benchmarks / PdfViewer.Usage / 统一 Demo；全解决方案构建在收尾时被工作区 TreeDataGrid WIP 的 CS0841（`TreeDataGridSource.Generic.cs`，非本批改动）挡住，与 PdfViewer 无关 |
| 统一 Demo 原生 `--pdf-smoke` | **68/68 检查** | 新增 4 项 `perf*`：perfFirstScreenStageChain、perfProductNeverClaimsPresent、perfCompositionPresentObserved（真实窗口 composition batch）、perfResourceSampleJoinsCache；机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者 | **93/93 + 96/96 = 189/189** | 各含 5 项 `p3Performance*`（loadChainMarks / stageSpans / productNeverClaimsPresent / resourceSampleJoinsCache / markNamesUnique），累计 18 项 `p3*`；`artifacts/consumer/` 下 `result.json` |
| Windows 性能内部回归 | **280/280 ok** | `windows-2026-10-23`，10 场景 × 30/10 迭代，threshold 表见验证记录 |
| Windows 软件渲染子集 | **90/90 ok** | `windows-2026-10-23-soft`，`--rendering-mode software` |
| Linux WSL 补充回归 | **280/280 ok** | `linux-2026-10-23`，WSL2 Ubuntu 26.04 + WSLg，非实机 |

同机官方 A/B 对照保持 `blocked-reference`（无合法参考宿主，取得后按 PERF03 冻结配置执行）；门槛待校准；SSIM 金样与扫描输出验证归 PERF17 / P9；Linux 实机 / hosted run / 麒麟目标设备归 PERF18 / P8。G3 判定见[开发计划](pdfviewer-development-plan.md)P3 节：内部测量门槛具备证据，不提前关闭。

## P3-04 批次（2026-10-23，第十四批：长文档布局索引、搜索按页索引与拖选区间）

P3-04 的四条线——长文档布局索引与 Resize 合并、搜索按页索引与可见高亮、字符命中空间索引、拖选只维护区间——已实现并验证；等距命中平局、居中几何、行查找语义逐项与旧实现对拍锁定：

- **布局索引与 Resize 合并（PERF09）**：`PdfPageLayout.Build` 以签名（页列表引用、缩放、旋转、模式、封面、有效当前页——连续模式恒 0）判定返回 `BuildResult`：签名不变 **`Unchanged`** 跳过全部重建（连 `InvalidateMeasure` 都不发，仅刷新缩放 / 调度渲染 / 重绘移动后的视口）；仅宽度变化 **`Recentered`**——水平重排每行到与全量重建**逐像素一致**（单测锁定），垂直几何不动，只发 `InvalidateMeasure`（不进渲染突发）；签名变化才 **`Rebuilt`**（清空渲染键 + 保留缩放 / 旋转突发的预览语义）。行查找 O(1)/O(log N)：`RowIndexOf` 走行首页映射 O(1)（行首页外保持旧 `FindIndex` 的 -1 语义），`RowAt(y)` 行顶二分（保留旧中点规则），`PageAt` / `PageIndexAt` / `RowsBetween` 全部复用；2000 混合行单测锁定查找一致性与远端查找。滚动定位 O(log N) 由此保持；连续 resize / 缩放合并由签名跳过覆盖（同签名重复构建零分配）。
- **搜索按页索引与可见高亮（PERF10）**：`PdfSearchPageIndex.Build` 把搜索结果按页分桶并保留全局序号（单测锁定分桶保序），presenter 只在结果数组引用变化（每次搜索批量发布新数组）时重建桶；高亮绘制只走可见页的桶，整页在选区内的页跳过逐命中范围检查——不再每个可见页线性扫描全部结果。增量发布首结果 / 进度、取消与截断提示沿用 P2 搜索契约。
- **字符命中空间索引（PERF11）**：`PdfCharacterHitIndex` 以 12pt Y 带分桶（跨界字符登记全部触及的带），`FindNearest` 自查询带向外走 0,±1,±2… 并以平方距离界严格 `>` 早退（保留等距平局），平局取列表位置（与旧 `MinBy` 首个最小值逐点一致）。密集云 2000 随机探针（含带边界 / 角点）与旧全量扫描对拍通过；零尺寸边界跳过、跨带、重叠、稀疏远点各有锁定。
- **拖选只维护区间（PERF11）**：每次指针移动 = 带索引命中 + 更新焦点 + `SetSelectionRangeState` + 立即 `InvalidateVisual`（可见高亮跟随指针）+ **合并**的选中文本构建（单 runner、逐轮版本守卫、finally 重查重启，手势中 `SelectedText` 持续更新——测试以手势中断言锁定）；完整文本在复制时构造的路径不变。清除 / 新按下 / 中止拖选立即 `CancelSelectionText()`，取消的提取**永不迟到落地**（400 页拖选中途 `ClearSelection` 后 `SelectedText` 保持空，再选仍正确）；捏合 / 清除契约由版本递增兜底。`WaitForIdleAsync` 同时等待渲染任务与选中文本构建任务。
- **产品修复（本批实测缺陷）**：主题模板 `PART_PageNumber.Value` 与 `CurrentPage` 双向绑定，滚动 / 导航驱动的 `SetCurrentPage` 会经绑定回推 `NumericUpDown.ValueChanged` → `PageNumberChanged` → `GoToPage`，下一次布局把自由滚动的视口拽回当前页顶（长文档测试实测 offset 910.9 被改写为 553.3 = `page.Y − Gap`，既有测试因目标点恰好仍可见而一直被掩盖）。`PageNumberChanged` 现同样检查 `_updatingCurrent`：查看器自身驱动的当前页变化不再二次导航；用户输入页码与宿主直设 `CurrentPage` 行为不变。

行为变化：布局构建从「任一输入变化即全量重建」改为「签名不变跳过 / 仅宽度变化水平居中 / 变化才重建」；搜索高亮从「每可见页扫全部结果」改为按页桶只扫可见命中；字符命中从「每指针移动全量 MinBy」改为带索引查找；拖选预览从「每移动完整提取」改为合并构建（复制仍完整提取）。公共选择 / 复制契约（`SelectedText` 异步解析后更新、跨页 `Environment.NewLine` 连接、`IsTextSelected` / `ClearSelection` 语义）不变。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **135/135** | 含 13 项新增：P3LayoutIndexTests 7 项（签名跳过与逐输入重建、连续模式忽略 / 离散模式跟随当前页、仅宽度居中不动垂直几何、居中与全量重建逐像素一致、2000 行查找一致性、双页行命中两侧成员、搜索分页分桶保全局序）+ P3CharacterHitIndexTests 6 项（空 / 零尺寸、等距平局取列表位置、重叠取列表位置、跨带可命中、稀疏远点、密集云 2000 探针对拍） |
| PdfViewer.Headless.Tests | **102/102** | 含 8 项新增（P3LongDocumentTests）：25 / 120 / 1005 页跨度 × 正 / 反向拖选共 6 组的区间与文本正确性（手势中预览更新 + 剪贴板全文比对）、400 页拖选中途清除取消挂起提取且无迟到落地（复选仍正确）、300 页远跳 + 窗口宽度 resize 后布局查找一致（X 重排、Y / 宽不变） |
| 全解决方案 Release 构建 | **零警告、零错误** | `NT.AvaloniaControl.slnx` |
| 统一 Demo 原生 `--pdf-smoke` | **64/64 检查** | 新增 2 项：searchResultsPageOrdered（搜索结果 `PageIndex` 非降）、selectionSurvivesViewportResize（全选后侧栏 208→320 宽，`IsTextSelected` 与 `SelectedText` 不变） |
| 两个全新缓存 NuGet 消费者 | **88/88 + 91/91 = 179/179** | 重打包 + 全新缓存，原 86 / 89 项全保留，各含 2 项新 `p3*`（p3SearchResultsPageOrdered、p3SelectionSurvivesViewportResize，累计 13 项 `p3*`）；`artifacts/consumer/` 下 `result.json` |

无新增公共 API：`PdfPageLayout.BuildResult`、`PdfCharacterHitIndex`、`PdfSearchPageIndex` 均为内部结构。长文档优化的完整指标记录与同机官方对照归 P3-05；搜索可复用文本索引的编辑后失效归 PERF14。

## P3-03 批次（2026-10-23，第十三批：同画质分层、质量上限与分块可行性）

P3-03 的四条线——可配置 DPI / 解码上限对齐官方、预取低清 / 可见高清分层、停滚高清与缩放预览、分块渲染可行性证明——已实现并验证；不以低分辨率取胜，任何低于目标的光栅都显式记账：

- **解码上限对齐官方**：替换旧硬编码（单边 4096 / 4MP / DPI 钳 1–2）为可配置样式属性 `MaxRenderDimension`（默认 **8192**，钳 256–16384）、`MaxRenderPixels`（默认 **30,000,000**，钳 65,536–67,108,864）、`MaxRenderScale`（默认 **8**，值须有限、钳 1–8；有效 DPI = `clamp(RenderScaling, 1, MaxRenderScale)`），与官方 8192 / 30MP 上限同口径。每页像素预算取 `min(MaxRenderPixels, PageBudget × 0.95 / 4 / 候选页数)`，缩放系数 `factor = min(1, MaxRenderDimension/长边, sqrt(预算/像素))`；ceil 取整的轻微越界再以 floor 收缩压回，上限是硬顶。改动即时重渲染（`RefreshRenderQuality`）。
- **质量分层**：可见页停稳后按目标光栅全质量解码；预取行与交互突发期缺页先以 **Preview 低清层**（线性 0.5×）快速填充。突发期间已缓存位图直接拉伸续用（**缩放预览**：缩放爆发不触发重解码，旧图兼作预览），只有缺失页才低清补位。滚动 / 缩放突发由 150ms 去抖（`DispatcherTimer`）判定停稳，停稳后重算请求集把可见页**升级到全质量**（停滚高清）并为预取行补升级机会；首帧不走突发路径（缓存为空直接全质量），`WaitForIdleAsync` 等到停稳升级完成才返回。
- **降级显式记账**：全质量层因预算 / 上限被迫低于屏幕目标光栅时记 `DegradedPageCount`（`factor < 1` 的非预览页），预览层记 `PreviewPageCount`；两者均只读计数，任何「少解码」都可解释，不在静默中取胜。默认预算 + 常规缩放下 `DegradedPageCount == 0`（测试与 smoke 双重锁定）；64 KiB 极限预算下 ≥1 且条目带 `Degraded` 标志。
- **分块渲染可行性（不接入显示）**：`PdfDocument.RenderPageRegionAsync` 以负起点偏移（`-regionX, -regionY`）把全幅光栅裁进瓦片位图，PDFium 按位图边界裁剪。可行性证明（`TiledRenderFeasibilityTests`）：奇数边界（x=287 / y=333，缝跨字形抗锯齿）**逐像素精确拼合**、瓦片与整幅渲染同为全不透明（alpha 255）、旋转 90° 下瓦片坐标映射一致、越界区域确定拒绝（`ArgumentOutOfRangeException` + 新锁定字符串 `ErrorRenderRegionRange`）。显示层分块留待深缩放超出上限的实需再接入，本批只证明方案本身可靠。

行为变化：旧「DPI 钳 1–2 / 单边 4096 / 4MP」上限契约由官方同口径可配置上限替换（默认即 8192 / 30MP / 8×）；渲染请求在交互突发期跳过已缓存页（旧图续用）并以 Preview 尺寸补缺页，停稳后自动升级——同一页面可能先低清后全质量两次解码（`RenderedPageCount` 相应增加），这是分层代价而非泄漏。`SettleDelay` 内部钩子在突发中改写即重启计时，供确定性观察中间态。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **122/122** | 含 4 项新增（TiledRenderFeasibilityTests）：奇数边界瓦片逐像素拼合、全不透明、旋转坐标映射、越界拒绝；另 StringsTests 锁定 `ErrorRenderRegionRange` 默认值 |
| PdfViewer.Headless.Tests | **94/94** | 含 6 项新增（P3RenderQualityTests）：停稳可见页全质量 + 预取行保持 Preview、滚动突发低清填充后停滚升级、缩放突发旧图续用至停稳重光栅、预算压力记 `DegradedPageCount`（默认预算为 0）、上限配置 / 强制钳制 / 生效绑定（512 边长与 15 万像素帽）、跨屏 `SetRenderScaling` 1→2→1 重光栅（DIP 布局不变）；原 P3-02 预算区间断言零改动复核通过 |
| 全解决方案 Release 构建 | **零警告、零错误** | `NT.AvaloniaControl.slnx`，含 Usage / 统一 Demo / 两测试工程 |
| 统一 Demo 原生 `--pdf-smoke` | **62/62 检查** | 新增 4 项：maxRenderConfiguration（默认 8192 / 30MP / 8）、maxRenderCoercion（10→256、1→65,536、0→1）、renderRegionTilesMatchFullRender（公共 API 分块拼合抽样）、qualitySettleReRasterizes（变焦后重解码且无降级）；观察值 `previewPageCount=1`、`degradedPageCount=0`、`renderedPageCount=47`、`cachedBytes=7,895,880` |
| 两个全新缓存 NuGet 消费者 | **86/86 + 89/89 = 175/175** | 重打包 + 全新缓存，原 82 / 85 项全保留，各含 4 项 `p3*`（p3MaxRenderConfiguration、p3MaxRenderCoercion、p3RenderRegionTiles、p3QualityStateCounters）；`artifacts/consumer/` 下 `result.json` |

新增公共 API：`PdfViewer.MaxRenderDimension` / `MaxRenderPixels` / `MaxRenderScale`（样式属性 + CLR 包装，含钳制与 NaN 校验）与常量 `DefaultMaxRenderDimension` / `DefaultMaxRenderPixels` / `DefaultMaxRenderScale`，只读计数 `PreviewPageCount` / `DegradedPageCount`；`PdfDocument.RenderPageRegionAsync(pageIndex, rasterWidth, rasterHeight, regionX, regionY, regionWidth, regionHeight, rotation, cancellationToken)`；`PdfViewerStrings.ErrorRenderRegionRange`。同画质的画质 / 性能对照记录归 P3-05 指标批次；分块显示接入不在本批范围。

## P3-02 批次（2026-10-22，第十二批：缓存预算、保留迟滞与全局配额）

P3-02 的四条线——配置化预渲染、保留迟滞、页面 / 缩略图 / 文字预算、全局配额与内存统计——已实现并验证：

- **预算与预渲染配置**：每 viewer 样式化属性 `PageCacheBudget`（默认 128 MiB）、`ThumbnailCacheBudget`（16 MiB）、`TextCacheBudget`（32 MiB）下限统一钳制到 `MinimumCacheBudget`（64 KiB）；`PrerenderRows`（默认 1，钳制 0–8）控制视口上下各预渲染行数（单页 / 连续一行 1 页，双页模式一行 2 页）。预算约束的是保留集合（retention）上限，不约束当前渲染 wanted 集合的下限语义（文档注释已明确）。
- **保留迟滞**：页面 / 文字池仅在超过有效预算（高水位）时裁剪，LRU 淘汰到 `0.85 × 预算`（低水位）；本轮渲染请求集与插入条目在裁剪中永不淘汰；条目安全阀 `MaxCachedPages` 2048、`MaxCachedTextPages` 1024、缩略图 1024；文本选区锚点 / 焦点页受保护。
- **全局配额（多 viewer）**：进程级 `PdfViewer.GlobalCacheBudget`（0 = 不限）与 `GlobalCacheBytes`；配额生效时 `EffectiveCombinedBudget = min(自身合计, 配额 − 他人占用)`，各池有效预算按比例缩放；viewer 只自淘汰，不做跨 viewer 淘汰。
- **内存统计**：`CacheStatistics` → `PdfViewerCacheStatistics` 记账页面位图、缩略图位图、文本估算（`96 + 2×字符` 加每条目开销）、解码中间数组在途字节与峰值、输入字节（钉住数组 / 暂存文件 / 快照长度）；解码中间数组即 `FPDFBitmap_CreateEx` 包裹的托管像素数组、同时是原生渲染目标内存，计数在工作项交接处结束；PDFium 内部堆不声称可测（文档注释明确）。
- **缩略图准入控制（本批产品修复）**：缩略图预算小于侧栏需求时，原「完成即重请求」路径会与 LRU 淘汰形成死循环（低优先级渲染永不空闲）；改为以 `_thumbnailReservation`（见过的最大缩略图）做准入判定（缓存为空时豁免），预算上调经 `RequestVisibleThumbnails()` 恢复预取。

行为变化：旧「按请求数上限淘汰」契约（`Math.Max(12, requests.Length)` 计数帽）由预算 + 迟滞保留替换；公开配置变化即时裁剪并恢复可见预取。Headless 既有契约（大文档视口 `[1,15]`、极小页 `[visible, visible+4]`）在预渲染 1 行 + 保留语义下逐项复核仍成立。调试中的测试竞态（缩略图渲染不走 presenter `_renderTask`，`WaitForIdleAsync` 不等待它）改为等待 `DecodeBufferBytes == 0` 后再断言，属测试修复非产品放宽。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **118/118** | 既有全保留，无新增 |
| PdfViewer.Headless.Tests | **88/88** | 含 5 项新增（P3CacheBudgetTests）：页面预算压力下保留字节上界与淘汰、保留迟滞契约（访 20 页留 ≥16，缩预算即裁剪）、`PrerenderRows` 解码扩展（0→2 行扩大 2–4 页）、缩略图 / 文字预算强制与记账恒等式（含解码峰值与在途归零）、多 viewer 全局配额与 `GlobalCacheBytes` 汇总；曾 4 轮压力复跑稳定 |
| PdfViewer 三项目 + 两测试工程 + Demo Release 构建 | **零警告、零错误** | 五工程单独构建；并行 Charts 会话 WIP 不影响 |
| 统一 Demo 原生 `--pdf-smoke` | **58/58 检查** | 新增 5 项：cacheBudgetConfiguration、prerenderRowsCoercion（99→8）、prerenderRowsFloor（−1→0）、cacheStatisticsAccounting（记账恒等式 + `InputBytes == SamplePdf` 长度）、thumbnailBudgetEnforced；观察值 `cacheTrackedBytes=48,233,282`、`decodeBufferPeakBytes=16,012,640` |
| 两个全新缓存 NuGet 消费者 | **82/82 + 85/85 = 167/167** | 重打包 + 全新缓存，原 78 / 81 项全保留，各含 4 项 `p3*`（p3CacheStatisticsAccounting、p3CacheBudgetConfiguration、p3ThumbnailBudgetEnforced、p3GlobalQuotaReporting）；`artifacts/consumer/` 下 `consumer-result.json` |

新增公共 API：`PdfViewer.PageCacheBudget` / `ThumbnailCacheBudget` / `TextCacheBudget` / `PrerenderRows`（样式属性 + CLR 包装，含钳制）、`PdfViewer.MinimumCacheBudget` 常量、`PdfViewer.GlobalCacheBudget` / `GlobalCacheBytes`、`PdfViewer.CacheStatistics` 与 `PdfViewerCacheStatistics` 记录。记账观察值不是性能基准；同画质对齐、长文档优化与完整指标记录分别归 P3-03～P3-05，实际进程内存测量归 P3-05 验收。

## P3-01 批次（2026-10-22，第十一批：文件随机读取 / 暂存与外部变更策略）

P3-01 的四条线——文件随机读取消除路径加载整文件双份峰值、不可寻址流暂存、外部文件变更策略、句柄释放策略——已实现并验证：

- **文件随机访问加载**：`PdfDocument.LoadFromFileAsync` 经 `FPDF_LoadCustomDocument` + `FPDF_FILEACCESS` 由 GetBlock 按块读取（`RandomAccess.Read` + `FileOptions.RandomAccess`），不在托管内存保留整文件副本；16 MiB 未引用填充流加载只服务不足 1/4 字节（`InputBytesServed` 测试钩子），证明按需读取而非整文件缓冲。打开时快照长度为 `m_FileLen`；Windows 上该长度是 C `unsigned long`（LLP64，32 位），超过 `uint.MaxValue` 的文件明确报 `ErrorFileTooLargeForRandomAccess`。CJK 路径（「随机访问-读取-」）与内存加载的页数 / 尺寸 / 页标签 / 目录 / 文本一致。
- **不可寻址流暂存**：无 `Length` / `Seek` 的输入复制到进程唯一临时文件（`nt-pdf-{guid}.tmp`），读句柄带 `DeleteOnClose`，文档 Dispose 后临时文件确定删除（smoke 观察无残留）；空不可寻址流与字节路径同报 `ErrorEmptyDocument`。
- **外部文件变更策略（本批产品修复）**：文件以 `FileShare.ReadWrite | FileShare.Delete` 打开，打开时快照长度、此后增长忽略。外部截断 / 替换造成块读取短读时，**GetBlock 不再报告失败**——根因实测：失败块读取会让 PDFium 渲染路径在原生 CHECK 处直接终止进程（STATUS_BREAKPOINT，崩溃点为「截断后首次渲染未缓存页」；同一截断若先走文本路径则静默缓存失败并渲染空白页）。改为真实字节 + 零填充补全请求并记录读取缺口（`ReadGaps`），文档层检测缺口后抛出新锁定字符串 `ErrorFileChanged`（`PdfErrorKind.FileAccess`），并**粘性拒绝后续内容操作**——PDFium 已解析对象可能混合快照与替换字节，宁可干净报错也不返回空白 / 混杂内容；文本路径此前静默返回空文本的弱点一并修复。加载期出现缺口同样拒绝交付文档。同长度原地改写的字节级检测不在本层能力内；删除 + 重建场景按 Windows 共享删除语义使句柄继续指向原文件数据，反而是一致读取行为。
- **句柄释放策略**：文件 / 暂存输入持有 `SafeFileHandle`，文档释放顺序固定为先 `FPDF_CloseDocument` 再关闭句柄、释放 `FPDF_FILEACCESS` 块与 GCHandle 自引用（PDFium 关闭期间仍可能读块，输入必须活过关闭调用）；暂存路径 Dispose 双保险删除（delete-on-close + 兜底 `File.Delete`）。

新增公共 API：`PdfViewerStrings.ErrorFileChanged`（默认值锁定 "The PDF file changed on disk while the document was open."）。行为变化：外部截断 / 读取失败由「GetBlock 返回失败 → 渲染路径原生崩溃 / 文本路径静默空结果」改为「零填充 + 记录缺口 → `PdfDocumentException`（`ErrorFileChanged`，`PdfErrorKind.FileAccess`），后续内容操作粘性拒绝」。

| 本批执行（Windows 11 x64 26200，.NET 10.0.13，Avalonia 12.0.3） | 结果 | 说明 |
| --- | --- | --- |
| PdfViewer.Core.Tests | **118/118** | 含 7 项新增（RandomAccessLoadTests）：文件 / 内存加载一致（含 CJK 路径）、16 MiB 未引用填充按需读取、非可寻址流暂存与 Dispose 删除、空流同错误、缺失 / 空 / 不可寻址长度报错、外部截断粘性 `ErrorFileChanged`（含 `InputReadGaps` 缺口钩子与后续操作拒绝）、密码文件路径与内存错误契约一致；另 StringsTests 锁定 `ErrorFileChanged` 默认值 |
| PdfViewer.Headless.Tests | **83/83** | 含 1 项新增（ReadingContractTests）：文件路径来源的密码重试经同一打开通道从磁盘重开（opener-channel 回归锁，与既有字节来源密码测试互补） |
| PdfViewer 三项目 + Demo Release 构建 | **零警告、零错误** | 并行 Charts 会话 WIP 不影响 PdfViewer 三项目单独构建 |
| 统一 Demo 原生 `--pdf-smoke` | **53/53 检查** | 新增 4 项：fileRandomAccessLoad（CJK 路径）、nonSeekableStreamStaging、stagedTempFileReleased、externalTruncationError（截断后渲染报 `ErrorFileChanged` 而非进程终止）；机器记录 `artifacts/platform/pdfviewer.json` |
| 两个全新缓存 NuGet 消费者 | **78/78 + 81/81 = 159/159** | 重打包 + 全新缓存，原 75 / 78 项全保留，各含 3 项 `p3*`（p3FileRandomAccessLoad、p3NonSeekableStreamStaging、p3ExternalTruncationError）；`artifacts/consumer/` 下 `result.json` |

本批调试用临时探针（`TempFileProbe.cs`）已删除，不入源码绑定。大文档内存统计、预渲染预算、同画质性能与指标记录分别归 P3-02～P3-05；本批不引入新的性能数字。

## P1 历史执行（2026-10-16）

P1 已交付 typed 数据源、原生元数据和权限、宿主能力状态、可绑定命令、typed 错误与会话事件、异步关闭 / 释放，以及前台 / 按页搜索 / 缩略图 `4:2:1` 串行公平调度。Windows x64 与 Ubuntu 26.04 WSL2 x64 已执行原生验证；Ubuntu 24.04 / Windows CI 工作流已配置但尚未远端执行，G1 仍开放。打印 / 分享仅固定 P8 服务边界，控件相关能力保持未实现。[P1 契约与迁移说明](pdfviewer-p1-contracts.md)、[P1 验证记录](pdfviewer-p1-validation.md)和[机器记录](validation/pdfviewer-p1-20261016.json)定义该批范围。

Windows 与 Ubuntu 主验证使用同一功能包 IL，SHA-256 为 `60422D2DE73383593AA68F47445B1D682C8E33924E963E0622D5D96A3E9C1693`；Ubuntu 原生 SDK 独立锁定构建另记编译产物。最终源码数量、目录及哈希见机器记录。此前候选目录保留，未混作本次最终源码的执行证据。

| P1 本次执行 | 结果 | 说明 |
| --- | --- | --- |
| Windows x64 | Core **92/92**、Headless **33/33**、后端 **11/11**、原生窗口 **22/22** | 契约 / 权限 / 调度 / 生命周期及阅读回归 |
| Ubuntu 26.04 WSL2 x64，同 IL 主验证 | Core **92/92**、Headless **33/33**、后端 **11/11**、原生窗口 **22/22** | Linux PDFium、C ABI 和 X11 路径，不是 Ubuntu 24.04 CI 或麒麟实机 |
| Ubuntu 独立锁定还原 / 构建补充轮 | Core **92/92**、Headless **33/33**、后端 **11/11**、原生窗口 **22/22** | Linux SDK 编译的 DLL，单独绑定哈希，不混作同 IL |
| 全新缓存 NuGet 功能包 / Themes 消费者 | **48/48 + 51/51 = 99/99** | 实际 nupkg，无源码项目引用 |
| Usage / 统一 Demo Release 构建 | **零警告、零错误** | 构建结果不等于全部系统交互验收 |

最终包消费还原使用已校验离线源，`NuGetAudit=false` 仅用于本次任务；**本轮没有新增漏洞审计通过结论**。字体匹配和少量字形检查不代表完整 CJK / IME 验收。P0 完整字体、表单、部分删除和真实语料大矩阵未在本次新 DLL 上整体复跑，以下旧数字保留为各自冻结版本的历史证据，不能沿用为 P1 全矩阵结果。

P0 最后一次真实语料轮补入 6 份 / 314 页出版 PDF，冻结来源 / 条件许可 / 原件 SHA 及逐操作预期。[兼容性边界](pdfviewer-compatibility-boundaries.md)区分读取、原样保存、FreeText 重写、严格清理和真正删除；四类原型继续作为有限可行性证据。生产化任务归 P4–P7，完整性能 / 平台终验归 P3 / P9。官方可运行参考版本、目标设备及最终门槛仍按计划保留外部依赖，不能计为官方性能通过。

该 P0 轮冻结 345 份文件，原生 **117/117**、独立 **645/645**、语义负对照 **12/12**、Core **67/67**、Headless **19/19**；37 项真实操作为 26 接受 / 11 明确拒绝。人工查看 30 张 Poppler 和 15 张 PDFium 抽样图，记录固定批注位置覆盖 IRS 页脚及 NIST 深色封面低对比限制。[P0 机器记录](validation/pdfviewer-p0-real-corpus-20261015.json)绑定 264 份证据。其功能包 / Themes 与当时父轮相同，不是本次 P1 DLL。

## 已实现

- 功能包提供 `Editing.PdfEditSession` 受限生产写入 API：单行 FreeText、显式静态 TrueType 默认自动子集 / Full / NoSubsetting、持久 ID 新增 / 更新、修订与 Dirty、字节导出 / 流保存 / 原子文件保存。字体字形 / 复合依赖及 Unicode 标量 CID 映射在 C# 处理，支持已验证的 CJK / 扩展汉字 / Unicode 别名；不需要 Python 或系统字体回退。使用 MIT PDFsharp Core `[6.2.4]`，Pkcs 安全依赖覆盖为 10.0.13。仅处理未加密、未签名 PDF，复杂排版及编辑 UI 尚未实现。

- 独立 `NT.AvaloniaControl.PdfViewer` 功能包及单向依赖的 `.PdfViewer.Themes` 明暗主题包，功能包可配合自定义模板独立消费。
- PDFium 后端支持 typed 本地文件 / 自有字节 / 可重开流工厂及旧式字节、流、密码加载，真实页面渲染、Unicode 文字坐标、搜索和分层目录。全进程原生调用按 `4:2:1` 加权轮转串行执行，搜索逐页让出；排队取消及时完成，正在执行的原生调用只支持安全点协作取消，不承诺强制中断或硬时延。
- `PdfDocument.Metadata` / `Permissions` / `Capabilities` 为加载快照；控件同名状态结合文档、宿主开关和实际实现能力动态更新。`IsReadOnly` 默认开启但不影响阅读、搜索和获准的复制；低层 `GetTextAsync` 对原生权限禁止复制的 user 文档明确拒绝。全许可 user / owner 无法辨别时返回 `UserOrOwner`，不猜认证身份。元数据单字段最多 1 MiB，签名计数仅表示存在，不验证信任。
- 保留 `Source`、旧加载方法、`DocumentLoaded`、`LoadError` 和原始 `ErrorCode`，新增 typed 来源、错误分类、`DocumentLoadFailed`、`DocumentStateChanged` / `DocumentSessionId` 及打开 / 导航 / 搜索 / 复制命令。`Dispose()` 发起非阻塞释放，`DisposeAsync()` / `CloseDocumentAsync()` 等待原生释放完成；取消、替换与事件内重入不能发布旧文档。
- `IPdfFileDialogService.OpenAsync` 已接入打开流程，未注入时使用 Avalonia StorageProvider；P4-02 已接入保存路径服务与 StorageProvider 后备；打印和分享仍为后续边界，服务可用不解锁未实现能力。
- 桌面阅读控件支持本地文件打开、单页 / 连续 / 双页连续 / 双页布局（`PageDisplayMode`，双页含封面单独 / 奇数页配对规则 `ShowCoverPage`，双页翻页按跨页步进）、页码与 `/PageLabels` 页标签、缩略图、目录、缩放（含 `ActualSize`、可配置上下限 / 步长）与适配宽度 / 整页 / 高度、90° 旋转、搜索定位、跨页拖选及复制。
- 页内链接注释提取（`PdfDocument.GetLinksAsync`）与完整内部目的地（/XYZ 位置、缩放、拟合类型与 Fit 族视图参数）导航；目录项携带完整 `Destination`。URI / 文件 / 不支持动作一律不由控件打开，`LinkActivated` 事件供宿主拦截，`Handled` 可取消默认导航。
- 会话内用户书签（`UserBookmarks`：添加 / 删除 / 重命名 / 粗斜标志 / 清空与命令，与文档目录分开建模），默认主题新增「书签」页签；关闭 / 替换文档自动清空。编辑会话侧 PDF 持久化与回读已由 P4-01 交付（私有 catalog 块、不进撤销历史、undo / redo 不回滚）；控件保存接线已由 P4-02 交付，范围见本页最新批次。
- 编辑会话有界撤销历史与统一事务（P4-01）：`UndoAsync` / `RedoAsync` / `CanUndo` / `CanRedo` / `UndoCount` / `RedoCount` / `HistoryBytes`，`UndoLimit`（步数）与 `HistoryByteLimit`（字节）双限即时裁剪、最新条目保留、新编辑清 redo；`ExecuteTransactionAsync` 分组单条历史、失败全量回滚；`IsDirty` 按内容状态身份 + 书签叠加层，撤销到保存点即干净、保存失败保留；净化 / 红删各记一条历史并按策略清空会话书签。
- 触摸双指捏合以捏合中心缩放（Avalonia `Pinch` 手势），拖动为原生滚动；快捷键优先级固定并有契约测试（搜索框匹配键 → 全局打开 / 聚焦搜索 → 文字输入保留自身按键 → 阅读器快捷键）。
- 可见页面和邻近页调度，页面位图缓存预算 96 MiB，单页约 400 万像素上限；低缩放同屏多页时按总预算分配分辨率。缩略图最多保留 64 张。
- 缩放和旋转保持视口中心对应的 PDF 物理位置；末页和混合尺寸导航等待布局更新，避免使用旧滚动范围。
- 阅读位置保存接口（`PdfReadingPosition`、`CaptureReadingPosition` / `RestoreReadingPosition`）：锚点为视口左上角页内位置（与 `PdfLink.Bounds` 同坐标系）加缩放 / 模式 / 旋转，页间距归属下方第一页；宿主持久化 record 字段后重建恢复。
- 外观与本地化面：11 个细粒度工具可见性开关、`SidebarWidth` / `SidebarPlacement` 侧栏布局、独立亮暗色板与可选 Fluent `System*Color` 映射、静态 `PdfViewerStrings` 字符串表（阅读面文字 / 提示 / 状态 / 错误 / 无障碍名称）及默认模板全套 `AutomationProperties.Name`。
- 文档替换/关闭/释放使旧请求失效；移出可视树或隐藏会暂停渲染和拖选，返回后恢复需要的图像。模板替换解除旧事件并清理旧呈现器。
- 统一 Demo 的 `--page pdfviewer`、内置可搜索三页样例、原生 `--pdf-smoke`，以及独立 `samples/PdfViewer.Usage` AXAML/MVVM 应用。

## 初版阅读器历史验证

下表及其产物属于早期阅读器版本，P1 当前结果以上方表格与独立记录为准。

| 验证 | 结果 | 覆盖内容 |
| --- | --- | --- |
| PdfViewer.Core.Tests | 18 / 18 | 原生像素、加密、无效文件、字符索引、中文/emoji、裁剪、内置旋转、搜索、目录、取消、并发和释放 |
| PdfViewer.Headless.Tests | 19 / 19 | 默认和自定义模板、页码与目录、Source、加载竞态、四方向真实注入指针拖选及复制、双向跨页选择、低缩放超过12页可见、缓存、隐藏恢复、连续缩放/旋转锚点 |
| Windows 原生 Demo | 21 / 21 | 原生窗口、三页文档、目录、缩略图、选择、跨页搜索、导航、缩放旋转及亮暗渲染 |
| 独立 NuGet 功能包消费者 | 21 / 21 | 全新缓存、无源码项目引用、无 Themes 依赖、自定义 AXAML 模板、MVVM、真实 PDFium |
| 独立 NuGet Themes 消费者 | 24 / 24 | 全新缓存、默认 AXAML 主题、工具栏、暗色资源、MVVM、真实 PDFium |

已有控件的当次回归共 935 项通过。完整解决方案曾在独立输出目录构建通过，最终 PDF 功能包、主题包、Usage 和统一 Demo 构建均无警告。常规 Demo 输出目录有运行中的进程占用，因此原生验证使用 `artifacts/pdfviewer-validation/`，没有结束其他 Demo 进程。

本机生成的验证材料（`artifacts/` 按仓库规则不提交）：

- `artifacts/platform/pdfviewer.json`：Windows x64、1440×900、100% 显示缩放下的原生检查结果。
- `artifacts/platform/pdfviewer.png` / `pdfviewer-dark.png`：原生窗口可视树的 Avalonia RenderTargetBitmap 预览，已人工查看；它们不是操作系统桌面截图。
- `artifacts/pdfviewer-package-consumer-results.json`：两个独立 NuGet 消费者结果、包 SHA-256、全新缓存和详细证据路径。
- `artifacts/pdfviewer-regression/results.json`：已有控件的当次回归结果。
- `artifacts/packages/NT.AvaloniaControl.PdfViewer.0.2.0-preview.2.nupkg` 及对应 Themes 包。

复现：

```powershell
dotnet restore NT.AvaloniaControl.slnx --locked-mode
dotnet build NT.AvaloniaControl.slnx -c Release --no-restore -m:1
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release --no-build -- --page pdfviewer
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release --no-build -- --pdf-smoke
pwsh -NoProfile -File scripts/Test-PdfViewerPackageConsumer.ps1
```

PDF 测试已加入 `scripts/Verify.ps1`，包消费也已加入 `scripts/Test-PackageConsumer.ps1 -Control PdfViewer`。

## 原生分发与待验范围

三个原生 NuGet 依赖固定为 `155.0.8044`。功能包内保留 matching `chromium/8044` 官方发行资产中全部 12 个 RID 的 180 份原始许可文本；每个归档 SHA-256 与官方 release API 相符，归档原生库哈希也与对应 NuGet 资产逐一相符。来源和校验清单一并打包，许可目录在 nupkg 中保持层级。

Windows x64 与 Ubuntu 26.04 WSL2 x64 已做本次原生后端及窗口验证；指针和剪贴板自动化在 Headless 环境执行。Ubuntu 24.04 CI 尚未远端执行，麒麟 x64/ARM64、Windows ARM64/x86、macOS、不同 DPI、多显示器、真实设备输入和辅助技术仍待相应设备验收。WSL2 结果不扩展为所有 Linux 发行版通过，提供原生运行时资产也不等于对应平台已验收。

阅读控件仍未集成批注编辑 UI、表单填写、内置打印、OCR、移动端或浏览器支持；功能包另有上文的受限 FreeText 写入后端。高倍率页面分辨率受缓存预算约束；复杂版面复制顺序取决于 PDF 文本层及 PDFium 的字符顺序。现有 PDF 注释作为页面内容渲染。

接入及公共行为见 [使用说明](pdfviewer-usage.md)。

## P0 验证历史与后续工作计划

以下续轮按历史保留；其中“本轮”只指对应 P0 记录，不能解释为 2026-10-16 P1 新 DLL 的完整矩阵执行。

P0 已执行当前实现快照冻结及保存、批注字体、表单、内容删除四组原型，详见 [P0 验证记录](pdfviewer-p0-validation.md)及[机器记录](validation/pdfviewer-p0-20261015.json)。这次没有修改产品阅读 API；原型验证工程位于 `benchmarks/PdfViewer.P0`。保存和基础表单有限可行，FreeText 需要字体资源写入桥，隐藏数据与共享嵌套 Form 的原生删除存在已复现反例；G0、Linux 实机与官方性能对照保持未完成。

续轮已实现生产 FreeText 写入桥，并扩充真正删除的 7 类验证；初轮上段按历史保留。当前后端边界、字段 / 附件 / 目录 / 字典保持、字体资源和失败验证见 [续轮记录](pdfviewer-p0-validation.md#生产写入层与内容删除续轮)及[续轮机器记录](validation/pdfviewer-p0-writer-20261015.json)。G0 未关闭。

续轮当前结果：Core 34/34、Headless 19/19、独立生产检查 49 项通过，功能包 / Themes 独立消费者 31/31 与 34/34。初轮上表的 18 / 19 / 21 / 24 是历史结果；本轮新增结果按续轮机器记录及其源码 / 包哈希识别。

后续清理 / 删除轮新增 `SanitizeAsync` 和 `ApplyRedactionsAsync`：在显式 `RemoveAllNonPageData` 策略下，整对象删除、递归资源修剪、新目录完整重写及输出审计已形成生产后端闭环，失败不提交。策略会移除全部批注 / 表单外观、书签和附件；部分对象及未支持绘制结构明确拒绝。冻结 305 份源文件后 Core **53/53**、Headless **19/19**、公共入口 **15/15**、独立验证 **64/64**、负对照 **6/6** 通过。详见[本轮记录](pdfviewer-p0-validation.md#隐藏信息清理与完整流水线续轮)与[机器记录](validation/pdfviewer-p0-redaction-20261015.json)。这不是完整字符级删除、任意 PDF 清理、编辑 UI 或 Linux 实机的通过结论。

清理 / 删除轮两个全新缓存包消费者 **36/36、39/39** 通过；功能包、Themes、Usage 和统一 Demo Release 构建均零警告 / 零错误，依赖漏洞审计通过。先前 FreeText 续轮的 31/34 项与 Demo 阻挡按历史保留，不与本轮结果混计。

最新字体续轮冻结 322 份源码，默认自动子集、NoSubsetting 和显式 Full 已交付。三套完整 OFL 字体、10 类样本、41 份 PDF 验证简繁中 / 日 / 韩 / Ext-B、复合字形、cmap4/12 和 Unicode 别名；Core **67/67**、Headless **19/19**、生产探针 **50/50**、独立检查 **465/465**、负对照 **5/5** 通过。实际 AP 文本、字形 / 度量 / 全部字体表校验和、CID 字宽及 Full/Auto 像素一致；更新没有旧子集资源残留。详见[字体记录](pdfviewer-p0-validation.md#freetext-自动子集与-cjk-字体续轮)和[机器证据](validation/pdfviewer-p0-font-subset-20261015.json)。外部编辑器、复杂排版 / 其他字体格式及 Linux 实机仍待验，G0 未关闭。

字体轮双包新缓存消费者 **41/41、44/44** 通过，包内 DLL 与探针哈希一致；Usage / Demo 构建及最终依赖漏洞审计通过。性能记录仅为单次后端耗时 / 分配观察，不是峰值、UI 指标或官方对比。

最新表单续轮冻结 **330 份**源码，补齐单选、多行、单 / 多选列表、只读 / 必填的两轮原生输入、失焦、保存和重开。高级字段 **26** 项原生 / **69** 项独立 / **7** 个负对照通过；三套本族 CJK 字体 **30** 项原生 / **229** 项独立 / **9** 个负对照通过，其中原生 CJK 自动 AP 的 **6** 份未嵌入 fallback 输出按反例保留。可行方案为完整静态 TTF BMP 默认资源、原生提交字段值后逐次托管重建 AP；实际字体、ToUnicode / CIDGID / 字宽、第二轮新字及选择高亮均单独核验。既有 P0 / 写入 / 子集 / 清理删除验证及 Core **67/67**、Headless **19/19** 同步复跑。详见[本轮验证](pdfviewer-p0-validation.md#acroform-高级字段与-cjk-表单字体续轮)、[机器记录](validation/pdfviewer-p0-forms-20261015.json)和[字体方案](pdfviewer-acroform-font-scheme.md)。

本轮只修改原型与验证工程，功能包 / Themes 源码及实际 DLL 与字体轮相同；此前包消费者与构建 / 依赖审计按父证据引用，没有算作新的表单覆盖。表单编辑公共 API / UI、输入期间字体、自动布局 / 滚动、跨页生命周期、外部交互编辑器和 Windows / Linux 实机 IME 仍待 P6；G0、F40–F42 / G6 保持开放。P6 条件预算因外观桥增加 2–4 工程日，完整主表调整为 102–158。

最新部分删除续轮冻结 **338 份**源码，完成 CJK 字符簇与部分图像的受限可行性验证：CJK **7** 个原始单对象 packed Tj 正例，按完整 CID / 字素簇扩张删除并保留邻字位置；图像 **7** 个像素重写正例，覆盖 RGB / 灰度、旋转 / 镜像 / 仿射及同页 / 跨页共享实例。原型检查 **117/117**、独立检查 **325/325**、语义负对照 **15/15**、Poppler **30 页**通过，Core **67/67**、Headless **19/19**。真实字体 SHA / cmap / GID / 轮廓 / 字宽、实际文本操作数与映射清理、所有 xref / 旧修订、目标与外部像素及未选实例分别核验。详见[本轮记录](pdfviewer-p0-validation.md#cjk-字符簇与部分图像真正删除续轮)、[机器证据](validation/pdfviewer-p0-partial-redaction-20261015.json)和[方案边界](pdfviewer-partial-redaction-scheme.md)。

本轮没有改变生产功能包；公共删除入口仍拒绝部分对象。Jamo / 变体 / 单字形多汉字是合成映射边界样本，不是完整整形支持；部分图像按相交的完整像素单元保守删除，未选共享调用仍可保留原图。TJ / 通用编码 / 复杂排版、JPEG / 透明 / 裁剪 / 嵌套内容、生产事务 / UI / Undo、真实规模峰值及 Ubuntu / 麒麟仍待验。既有生产写入 / 清理删除本轮已复跑，未变的字体子集 / 高级表单和包消费按同源同 DLL 父证据引用，未混计为新结果。G0 / F43 / F44 / G7 保持开放，P7 仍为 14–24 工程日条件预算。

剩余开发按 [完整开发计划](pdfviewer-development-plan.md) 的 P0–P9 推进，配有 [64 项官方功能对照](pdfviewer-official-alignment.md)、[性能专项](pdfviewer-performance-plan.md)和 [Windows / Linux 平台计划](pdfviewer-platform-plan.md)。Linux 按用户确认的默认范围规划 Ubuntu LTS x64 CI 与麒麟 x64/ARM64 实机验收。上述文件定义待实施工作和完成门槛，不增加本状态记录中的已完成功能、已测性能或已验证平台。
