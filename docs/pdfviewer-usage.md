# PdfViewer 桌面阅读控件

最新为 **2026-10-25 P4-03 真实存储与耐久验证**，见[存储专项](pdfviewer-p4-03-storage-validation.md)；首轮见[P4-03 验证](pdfviewer-p4-03-validation.md)，保存控件基本接入见[P4-02 验证](pdfviewer-p4-02-validation.md)。

P2 历史状态为 **2026-10-21 P2（含 10-20 P2-01 / P2-02 收尾、10-21 P2-03 / P2-04 收尾与 10-21 收尾修复批 WP1–6：PNG 导出、触摸手势、链接目的地与书签边界、阅读交互契约、主题与窄窗口验收、MVVM 示例与包消费）**：页面布局模式、页标签、链接目的地（含完整拟合视图参数）、用户书签、FitHeight / 可配置缩放与阅读位置保存、渐进搜索与结果上限提示、清除搜索、页面 / 全文文本提取、页面 PNG 图片导出、细粒度工具开关、侧栏布局 / 宽度、独立默认外观与 Fluent 资源映射、字符串本地化（含错误、菜单、工具提示与无障碍名称）已交付，Windows 单平台验证结果见[状态记录](pdfviewer-status.md)。P1 批（2026-10-16）已加入 typed 数据源、元数据、权限与能力状态、可绑定命令、异步释放和串行公平调度，并在 Windows x64 与 Ubuntu 26.04 WSL2 x64 执行原生验证。接入迁移详见[P1 公共契约](pdfviewer-p1-contracts.md)，实际结果与限制见[P1 验证记录](pdfviewer-p1-validation.md)及[机器记录](validation/pdfviewer-p1-20261016.json)。Ubuntu 24.04 / Windows CI 已配置，尚未执行远端工作流；G1 未关闭。

当前读 / 写 / 清理 / 删除逐操作范围见[兼容性边界](pdfviewer-compatibility-boundaries.md)。真实文档能读取或原样保存，并不代表支持表单填写、任意结构改写或部分内容删除。

`NT.AvaloniaControl.PdfViewer` 是基于 PDFium 的独立桌面阅读控件，默认模板和亮暗资源放在单向依赖它的 `NT.AvaloniaControl.PdfViewer.Themes` 包中。功能包不依赖默认主题，也不依赖 Avalonia 商业 PDF Viewer。

目标为 .NET 10 / Avalonia 12。功能包依赖 `bblanchon.PDFium.Win32`、`.Linux`、`.macOS` 原生运行时；发布时保留 NuGet 选择的本机资产。Windows、Linux、macOS 的实际设备验证分别记录，不把 Windows 结果视为其他平台的验收结果。依赖许可见功能包中的 `THIRD-PARTY-NOTICES.md` 和 `Licenses/`。

## 接入

应用引用两个 NuGet 包或对应项目，并加载 Avalonia Fluent 和 PDF 默认主题：

```csharp
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.PdfViewer.Themes;

Styles.Add(new FluentTheme());
Styles.Add(new PdfViewerTheme());
```

也可用 `StyleInclude` 加载 `avares://NT.AvaloniaControl.PdfViewer.Themes/Themes/Generic.axaml`。

```xml
<pdf:PdfViewer xmlns:pdf="clr-namespace:NT.AvaloniaControl.PdfViewer;assembly=NT.AvaloniaControl.PdfViewer"
               Source="{Binding DocumentPath}"
               CurrentPage="{Binding Page, Mode=TwoWay}"
               IsSidebarVisible="{Binding SidebarVisible, Mode=TwoWay}" />
```

`samples/PdfViewer.Usage` 提供真实 AXAML 编译绑定和 MVVM 示例，覆盖布局模式、侧栏 offset / overlay 呈现、用户书签、阅读位置捕获 / 恢复、渐进搜索结果与进度、页面 PNG 导出等 P2 宿主接入面（阅读器经 `CommandParameter` 传入视图模型命令，视图模型不持有控件引用）。启动时默认生成一份可搜索的三页 PDF；命令行也可传入本地 PDF 路径。

```powershell
dotnet run --project samples/PdfViewer.Usage -c Release -- "D:\Documents\report.pdf"
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release -- --page pdfviewer
```

## 加载与生命周期

所有阅读控件的公共方法和属性在 UI 线程使用。文档读取和 PDFium 工作在后台完成。`LoadDocumentAsync` 保留本地路径、`Stream` 和 `byte[]` 重载，新增 `PdfDocumentSource` 重载，均支持密码及 `CancellationToken`。需要绑定非路径来源或在密码变化后重新打开时，使用 `DocumentSource`。

```csharp
await reader.LoadDocumentAsync(path, password: password, cancellationToken: token);
await using var stream = File.OpenRead(path);
await reader.LoadDocumentAsync(stream, cancellationToken: token);
reader.GoToPage(3);
reader.FitWidth();
var results = await reader.SearchAsync("Avalonia", token);
reader.FindNext();
await reader.CopySelectionToClipboardAsync();

// typed bytes 在创建来源时复制，可重复加载；也可绑定 reader.DocumentSource。
var source = PdfDocumentSource.FromBytes(pdfBytes, "report.pdf");
await reader.LoadDocumentAsync(source, password, token);

// 工厂应每次返回新流；加载器从当前位置复制并负责释放该流。
var streamSource = PdfDocumentSource.FromStreamFactory(
    cancellation => ValueTask.FromResult<Stream>(File.OpenRead(path)), "report.pdf");
await reader.LoadDocumentAsync(streamSource, cancellationToken: token);
```

旧式流重载复制流的当前位置到末尾，调用方继续拥有流；新流工厂返回的流则归加载器释放。输入字节由后端独立持有。加载新文档会关闭旧文档；失败后显示空态和 `ErrorMessage`，显式调用的任务同时抛出异常。绑定 `Source` / `DocumentSource` 触发的异步错误通过 `ErrorMessage`、`LastError`、旧 `LoadError` 和新 `DocumentLoadFailed` 呈现。取消不作为加载错误。后发加载覆盖先发加载；旧任务不能覆盖新文档，关闭或释放后也不能恢复旧文档。

控件的 `Stream` 重载只捕获一次输入，密码提示的后续尝试复用该自有快照；首次加载任务完成后，调用方可以释放原流。可寻址输入使用精确长度缓冲，不可寻址 / 超大输入继续使用临时文件。成功打开、新加载、关闭或释放会清理重试输入，已打开文档另持有自己的 pin / 文件句柄。`CancelPasswordPrompt()` 仍只隐藏提示并保留重试数据。底层 `PdfDocument.LoadAsync(Stream)` 的单次加载及 `leaveOpen` 契约不变。

密码加载失败时默认弹出内置密码提示（`IsPasswordPromptVisible` 与 `:password` 伪类）：`SubmitPassword(password)` 更新 `Password` 并用同一来源重开，错误密码会再次报告错误并重新弹出提示（重试由用户驱动，不会自动循环）；`CancelPasswordPrompt()` 只收起提示，错误状态保留，宿主之后仍可调用 `SubmitPassword` 重试。新加载、加载成功、`CloseDocument` 与释放都会收起提示并丢弃重试数据。宿主可用 `IsPasswordPromptEnabled = false` 关闭内置提示改用自有 UI（错误仍照常上报）；提示的标题、说明、按钮与输入无障碍名称来自 `PdfViewerStrings.PasswordPrompt*`。默认模板的 `PART_PasswordBox` 回车提交、Esc 取消，`PART_PasswordSubmit` / `PART_PasswordCancel` 按钮同义。

`Source` 只接受本地文件路径，内存加载会清空它；`PdfDocumentSource.FromFile` 将本地路径规范为绝对路径。旧式 path / stream / bytes 加载清理之前的 typed `DocumentSource`；typed 文件来源同时投影其本地 `Source`，typed bytes / factory 的 `Source` 为 `null`。需要网络内容时由应用取得流或字节，控件不下载 URI。`Password` 变化优先重开当前 typed 来源，否则重开旧式 `Source`；显式加载可传密码。错误密码会产生 `PdfDocumentException`，其 `ErrorCode == 4`，分类为 `PasswordRequiredOrIncorrect`，不区分缺少和错误密码。

宿主结束使用时可保留 `Dispose()`，但它只启动非阻塞释放；需要确认句柄与固定数组已经释放时，应在 UI 线程发起并等待 `DisposeAsync()`。`CloseDocument()` 立即清空文档和搜索结果，`CloseDocumentAsync()` 额外等待取消加载和旧文档原生释放。暂时移出可视树或隐藏时暂停页面和缩略图解码，保留文档以便返回继续阅读，不必为临时切页释放控件。取消是协作式的，不能强制中断任意一个正在执行的同步 PDFium 调用。

```csharp
await reader.CloseDocumentAsync();
await reader.DisposeAsync();

// 后端文档也支持 await using；不依赖控件 UI。
await using var document = await PdfDocument.LoadAsync(source, password, token);
```

## 权限、能力和可绑定状态

控件新增 `Metadata`、`Permissions`、`Capabilities`、`LastError`、`DocumentState` 和 `DocumentSessionId` direct properties。元数据读取标准 DocInfo 字段，日期保留原字符串；每字段最多 1 MiB，超出时明确拒绝。`SignatureCount` 只表示原生发现的签名数量，不验证证书信任或签名有效性。

`Permissions` 是原生文件权限快照，`Capabilities` 合并当前文档、宿主策略和功能是否实现。绑定功能入口应检查 `Capabilities.<功能>.CanExecute`，不能仅检查原始权限。各能力还提供 `IsImplemented`、`IsAllowed`、`Reason`；控件保存现为 `IsImplemented=true`，受只读和写入范围约束；打印、分享、批注编辑 UI 和表单输入仍未实现。`IsReadOnly` 默认 `true`，不影响复制和搜索；将其设为 `false` 也不会解锁尚未实现的控件功能。

- 搜索受 `AllowSearch` 控制；禁用会取消当前搜索、清理结果，显式 `SearchAsync` 返回空结果。搜索属于阅读操作，可在文件禁止复制时继续使用。
- 选择、全选和复制要求文件 `CanCopy`、宿主 `AllowCopy` 及 `AllowTextSelection` 同时允许。P1 新增行为：低层 `PdfDocument.GetTextAsync` 对禁止复制的受限 user 文档抛 `UnauthorizedAccessException`；渲染和搜索仍可用。
- 当前 PDFium 对 owner 的有效权限实际返回 `FFFFFFFC`，不能按头文件的 `FFFFFFFF` 常量猜身份。全许可 user 与 owner 无法通过公开权限 API 区分时，`PasswordAccess` 明确返回 `UserOrOwner`。这些行为限制不建立 DRM 或安全隔离保证。

`DocumentStateChanged` 携带 `State` 和 `SessionId`，成功打开或重开会取得新会话 ID；使用业务回调前应同时确认 `Ready` 和当前 ID。新 `DocumentLoadFailed` 携带 `PdfError` 与 typed 来源，旧 `DocumentLoaded` / `LoadError` 保留。显式等待加载方法仍需处理其抛出的原始异常。错误分类可通过 `LastError.Kind` 或 `PdfDocumentException.Kind` 取得，原始 `ErrorCode` 不变。

```csharp
reader.DocumentLoadFailed += (_, args) => ShowError(args.Error.Kind);
reader.DocumentStateChanged += (_, args) => UpdateSession(args.State, args.SessionId);
if (reader.NextPageCommand.CanExecute(null))
    reader.NextPageCommand.Execute(null);
```

可绑定命令包括打开、关闭、前后页、缩放、适配、旋转、搜索、前后匹配、复制和全选，例如 `OpenCommand`、`NextPageCommand`、`SearchCommand`、`CopyCommand`。不可执行命令不调用动作；默认工具栏、快捷键和公共操作应用相同限制。完整命令列表与事件语义见[P1 契约](pdfviewer-p1-contracts.md)。

`FileDialogService` 可注入 `IPdfFileDialogService`；未设置时使用附着窗口的 Avalonia StorageProvider。`CanOpen` / `OpenAsync` 已接入打开流程，`CanSave` / `GetSavePathAsync` 已接入保存 / 另存为；取消返回 null。`PrintService` / `ShareService` 及其不可变 `PdfPlatformDocument` 请求是 P8 适配边界；即使服务 `IsAvailable=true`，当前 `Capabilities.CanPrint` / `CanShare` 仍为 `false`。

## 阅读和搜索

- `CurrentPage` 从 1 开始，未打开时为 0。`GoToPage` 自动限制范围，缩略图与目录跳转使用相同路径。新增缩放或旋转的导航等待布局更新后应用。`CurrentPageChanged` 在每次页码变化时触发，覆盖导航、滚动到新页和宿主直接赋值。
- `ZoomLevel == 1` 表示 100%，PDF 的 72 points/in 映射为 Avalonia 的 96 DIPs/in。允许范围由 `MinimumZoom` / `MaximumZoom` 配置（默认 10%–500%），`ZoomIn()` / `ZoomOut()` 按 `ZoomStep` 倍率步进（默认 1.2，必须大于 1）；越界设置被强制拉回，抬高 `MinimumZoom` 超过 `MaximumZoom` 时同步抬高上限（后写入者获胜）。直接修改缩放会进入 `Manual`；`FitWidth()` 按文档最宽页面适配连续阅读，`FitPage()` 适配当前页（或双页模式的当前跨页），`FitHeight()` 将当前页高度适配视口（双页模式取跨页较高页），宽度不参与约束。
- `Rotation` 为顺时针 0/90/180/270 度，叠加在 PDF 自带旋转上。搜索高亮和选择框采用相同坐标变换。
- `SearchQuery` 输入后有 300 ms 防抖；也可显式等待 `SearchAsync`。`SearchMatchCase`、`SearchWholeWord` 控制匹配方式。搜索按页渐进：每扫描完一页立即发布 `SearchResults`（只读快照数组）与 `SearchResultCount`，首个命中即时跳转，`SearchProgress` 为已扫描页比例（0..1）。命中达到 `SearchResultLimit`（styled，强制 ≥1，默认 10,000）时提前停止，置 `SearchResultsTruncated = true`、状态栏提示「已达上限」，此时 `SearchProgress` 停在实际扫描比例而非 1；完整扫描结束才为 1。结果按页及文内顺序排列；`CurrentSearchResultIndex` 从 0 开始，无匹配为 -1。搜索自然完成（含达到上限提前停止）时触发 `SearchCompleted`，携带 `ResultCount` 与 `Truncated`；取消、被新搜索覆盖和空查询不触发。`ClearSearch()` / `ClearSearchCommand` 取消进行中的搜索并清空查询、结果、进度与上限标志。低层 `PdfDocument.SearchPagesAsync` 以 `IAsyncEnumerable<PdfSearchPageResult>` 产出同一分页批次（无命中页也产出，便于进度上报），`SearchAsync` 是其扁平化结果。
- 文字选择支持跨行、跨页拖动与边缘自动滚动，正向 / 反向拖选语义一致。Ctrl/Cmd+A 选择当前页，Ctrl/Cmd+C 或上下文菜单复制。`SelectedText` 在异步选择解析完成后更新；`AllowTextSelection = false` 会清除选择。选择按码点原子处理，CJK 与代理对（如 emoji）不会被拆半；跨多页大范围选择整体复制，页间以 `Environment.NewLine` 连接。`IsTextSelected` 公开当前是否存在选择范围，`ClearSelection()` 与 `ClearSelectionCommand`（CanExecute 跟随 `IsTextSelected`）是宿主可用的清除入口，Esc 快捷键走同一路径。
- 图片扫描页须已有 OCR 文字层才能搜索或复制；本控件不执行 OCR。多栏和复杂排版的复制顺序依赖 PDFium 提供的文字顺序。

### 页面布局模式与页标签

- `PageDisplayMode` 选择布局：`Continuous`（连续，逐页竖排）、`SinglePage`（单页）、`TwoPageContinuous`（双页连续）、`TwoPage`（双页）。单页 / 双页模式只显示当前页或当前跨页，翻页时重新布局。`ShowCoverPage`（默认 true）在双页模式下让第 1 页单独成页（跨页为 2-3、4-5…）；关闭后按奇数页起配对（1-2、3-4…）。
- 双页模式的 `NextPage` / `PreviousPage` 及左右方向键按跨页步进；其他模式按页步进。`FitWidth` 在双页模式下按整个跨页宽度适配，`SinglePage` 按当前页适配；`FitHeight` 按当前页（双页模式为跨页较高页）的高度适配。
- `PageLabels` 返回文档 `/PageLabels` 页标签序列（未定义的页为空串）；`CurrentPageLabel` 返回当前页标签，文档未定义时回退为页码文本。状态栏在标签与页码不同时并列显示。
- 书签标题默认取当前页标签（无标签时为「第 N 页」）。

### 链接与目的地

`PdfDocument.GetLinksAsync(pageIndex)` 提取页内全部链接注释（0 基页序），返回 `PdfLink`：

- `Kind` 为 `Internal`（文档内目的地）、`Uri`、`File`（Launch / 远程跳转命名的文件）或 `Unsupported`（JavaScript、嵌入跳转等）。
- `Bounds` 与文字选区同坐标系：页内显示左上原点、PDF point、含内置旋转与裁剪。
- `Destination`（`PdfLinkDestination`）含目标页、`/XYZ` 目的地位置与缩放（`Zoom` 为 1=100%，null 表示不变）、`PdfDestinationFit` 拟合类型，以及完整拟合视图参数：`Rectangle` 为 `/FitR` 的显示坐标矩形，`Top` / `Left` 为 `/FitH`、`/FitBH` 的顶边与 `/FitV`、`/FitBV` 的左边显示坐标，以及只定义一个坐标的 `/XYZ` 单轴值（显示坐标系，归属轴随页面内置旋转确定；另一轴为 null，导航时保持当前滚动）；未定义的参数为 null。`Target` 为 URI 字符串或文件路径。
- `PdfDocument.GetPageContentBoundsAsync(pageIndex)` 返回页内容边界盒（页内全部对象包围盒的联合，含内置旋转与裁剪的显示坐标），是 `/FitB` 家族的适配依据；无内容对象的页返回 null。结果按页缓存。
- 点击链接触发 `LinkActivated` 事件。控件只对 `Internal` 目的地执行默认导航（含位置、缩放与拟合视图参数）：`Fit` → 整页；`FitB` / `FitBH` / `FitBV` → 以内容边界盒（无内容页回退为页框）代替页框适配：`FitB` 适配内容盒并双轴居中，`FitBH` 适合内容盒宽度并把 `Top`（无 `Top` 则内容盒顶边）钉到窗口上缘、水平居中，`FitBV` 适合内容盒高度并把 `Left`（无 `Left` 则内容盒左缘）钉到窗口左缘、垂直居中；`FitH` → 适合宽度并把 `Top` 顶边钉到窗口上缘（无 `Top` 则只滚到目标页）；`FitV` → 适合高度并把 `Left` 左边钉到窗口左缘；`FitR` → 矩形适配（缩放使 `Rectangle` 恰好放入视口并钉住其旋转后视图的左上角；查看器旋转 90°／270° 时按交换后的宽高适配非正方形矩形，受最小 / 最大缩放钳制）；`Xyz` → 保持 / 应用缩放后钉住位置（只定义一个坐标时只移动该轴）。滚动范围不足时钉点被视口钳制，与主流查看器一致。`Uri`、`File`、`Unsupported` 一律不打开，交由宿主处理。设置 `args.Handled = true` 可取消包括内部导航在内的默认行为。
- 目录（文档书签）项同样携带完整 `Destination`，点击按完整目的地跳转。`NavigateToDestination` 可供宿主直接调用。
- 链接悬停显示手型光标；按住后移动超过 4 DIP 不触发链接，按文本拖选处理。

### 用户书签

`UserBookmarks` 是会话内的用户书签集合，与文档目录分开建模：`AddUserBookmark(title?, pageNumber?, flags?)`、`RemoveUserBookmark(id)`、`RenameUserBookmark(id, title)`、`SetUserBookmarkFlags(id, flags)`、`ClearUserBookmarks()`，以及 `AddUserBookmarkCommand` / `RemoveUserBookmarkCommand`。`PdfUserBookmarkFlags.Bold` / `Italic` 控制显示样式。书签在文档关闭或替换时自动清空；可写会话的 PDF 持久化与回读已在 P4-02 接入（私有 catalog 块）；只读会话继续使用临时书签。


### 控件保存与未保存修改（P4-02）

```csharp
reader.IsReadOnly = false;
await reader.EnableEditingAsync(); // 已加载文档；回读私有用户书签
reader.AddUserBookmark("阅读标记");
await reader.SaveAsAsync("edited.pdf"); // 建立新的 SavePath
reader.AutoSave = true;             // 默认防抖 2 秒，仅针对本地目标
await reader.ExecuteEditAsync(async (session, token) =>
    await session.AddFreeTextAsync(0, bounds, "A", font, cancellationToken: token));
await reader.UndoAsync();
await reader.RedoAsync();
byte[] pdf = await reader.ExportDocumentAsync(); // 不清 Dirty
await reader.SaveAsync();                        // 当前路径，无路径则请求另存为
// 窗口关闭事件应先 Cancel=true，然后等待；成功后再关闭并 DisposeAsync。
bool canClose = await reader.RequestCloseDocumentAsync();
```

`SaveAsync(Stream)` 在当前位置写入、Flush 并保留流打开；成功清 Dirty，但不改变本地 SavePath。`SaveCommand` / `SaveAsCommand` / `UndoCommand` / `RedoCommand` 与 `IsDirty` / `IsEditing` / `IsSaving` / `CanUndo` / `CanRedo` / `Revision` / `SavePath` 可绑定。快捷键为 Ctrl/Cmd+S、Shift+S、Z、Y / Shift+Z，输入控件保留自身按键。

写入要求可写模式、未加密 / 未签名、至多 256 MiB 且结构受支持。可写加载创建自有编辑快照，默认只读加载保持随机访问。`SaveRequested` 可取消 / 覆盖路径；`FileDialogService` 或 StorageProvider 提供本地选路径。`DocumentSaved` / `SaveFailed` 与 `LastError` 报告结果。AutoSave 无目标时不弹选路径，失败保留修改并停止本次重试循环。

关闭 / 替换会先等待在途编辑，再通过 `UnsavedChangesRequested.Decision`、`UnsavedChangesService` 或默认主题等待保存 / 放弃 / 取消。`RespondToUnsavedChanges` 可完成自定义模板的提示；保存失败或选路径取消都保留当前文档。`Dispose` / `DisposeAsync` 是无条件释放入口，宿主需在释放前请求确认。详见[完整契约与限制](pdfviewer-p4-02-validation.md)。

### 原路径冲突与保存事务恢复（P4-03）

保存基线来自编辑会话初始化 / 上次成功文件保存的完整 SHA-256。`SaveAsync()`、AutoSave 及同路径 Save As 都不会自动覆盖发生变化的目标：抛 `PdfSaveConflictException`，`LastSaveConflict` / `SaveConflict` 和 `LastError.Kind=SaveConflict` 可绑定；Dirty / 当前文档保留。AutoSave 在冲突后暂停；另存到其他路径或重新加载解除。默认纯阅读阶段不承担整文件指纹，启用编辑后可等待 `EnableEditingAsync()` 完成初始化。

独立后端可显式传入文件身份：

```csharp
var baseline = await PdfFileSnapshot.CaptureAsync(path);
// 打开并编辑相应字节之后，使用该身份保护原文件。
PdfFileSaveResult result = await edit.SaveAsync(path,
    new PdfFileSaveOptions { ExpectedTarget = baseline });
```

未传 ExpectedTarget 的独立字节会话只保护本次保存开始后的变化。控件自动保留更早的编辑基线。目标发生冲突时应让用户选择重新加载或新路径，不通过重新捕获身份来暗中取消保护。

保存使用私有 `.nt-pdf-txn-*` 暂存目录、落盘 journal、候选 SHA / PDF 重开校验和原子替换；稳定空 `.nt-pdf-save-*.lock` 文件用于跨进程协作，不存文档数据且不自动 unlink。重启后的显式入口如下，调用应由宿主向用户展示条目并取得选择后执行：

```csharp
IReadOnlyList<PdfSaveRecoveryEntry> pending = await PdfSaveRecovery.ScanAsync(directory);
// 对用户选中的完整候选 / 已提交项：重新校验并恢复或清理。
PdfFileSaveResult recovered = await PdfSaveRecovery.RecoverAsync(selected.TransactionDirectory);
// 对用户明确放弃的、inactive 且有效的已识别事务：
await PdfSaveRecovery.DiscardAsync(selected.TransactionDirectory);
```

扫描区分 Active / Incomplete / Prepared / AlreadyCommitted / Conflict / Invalid，并追加 PublishedConflict / DurabilityPending，原枚举数值保留。恢复不信任旧扫描缓存，不递归删除未知文件。它恢复已进入保存流程的快照，不自动持久化每次编辑和 Undo 栈。`DocumentSaved.FileResult` 提供提交后清理警告、恢复目录及 `Durability`。

v2 事务在 `output.pdf` 保留独立输出，`candidate.pdf` 用于发布。Windows ReplaceFile 的 backup / Linux `RENAME_EXCHANGE` 保留实际被置换文件，再核对原身份与当前目标。最终窗口有竞争时，`PdfSaveConflictException.WasPublished=true`，目标可能已更新；请展示 `RecoveryDirectory`，保留当前 Dirty，显式另存解决。`PublishedConflict` 不允许自动恢复或 `DiscardAsync`；宿主可用 `PdfSaveRecovery.ReadVersionAsync(transaction, PdfSaveRecoveryVersion.SavedOutput / DisplacedExternal)` 读取自有字节，单版本上限 256 MiB，更大版本直接检查保留路径。不要盲目回滚并覆盖第三方后续修改。

`PdfSaveDurabilityException` / `LastSaveDurabilityFailure` / `LastError.Kind=SaveDurabilityUncertain` 表示提交结果或耐久确认不确定；不清 Dirty，AutoSave 在后续编辑后仍暂停。恢复连接后检查条目；DurabilityPending 在确认目标 / 旧版本身份后可补写确认记录。成功文件另存或重新加载清除控件的暂停状态。Linux 返回 FileAndDirectoryEntriesFlushed；Windows 返回 PostRenameFileAndCommitRecordFlushed，不作目录 fsync 或 Windows 掉电验收声明。

Windows 暂存使用当前用户私有 ACL；Unix 要求目录实际 0700，文件创建为 0600，并保留现有目标 Unix mode。权限无法落实、符号链接 / reparse point 明确拒绝。Linux 文件系统不支持原子交换时拒绝已有目标替换；实测 NFS 新路径可以 no-replace 发布，已有目标不可降级覆盖。NFSv4 永久失联的内核 I/O 不保证随 CancellationToken 立即退出，宿主应隔离进程并实施恢复 / 超时策略；本轮只验收三秒链路中断及重连。该方案仍不是 CAS，也不防止调用返回后的外部改写。完整元数据及掉电边界见[专项记录](pdfviewer-p4-03-storage-validation.md)。

### 阅读位置保存

`CaptureReadingPosition()` 返回 `PdfReadingPosition?`：视口左上角对应的页内锚点（`PageIndex` 0 基、`PagePosition` 与 `PdfLink.Bounds` 同坐标系）加上 `ZoomLevel` / `ZoomMode` / `Rotation` 视图状态。视口左上角落在页间距时，位置归属间距下方的第一页（锚点近其页顶），保证 `PageIndex` 与正在阅读的页一致。`RestoreReadingPosition(position)` 先恢复缩放模式 / 缩放 / 旋转，再把锚点滚回视口左上角；存储值中非法的缩放 / 旋转被忽略（页与锚点仍恢复），超范围页码取最近合法值。未打开文档时捕获返回 null、恢复为空操作。模型是公开 record 构造函数，宿主自行持久化各字段后重建即可；页面布局模式与封面规则属于视图设置，不在位置内。

### 文本提取与页面图片导出

- `GetPageTextAsync(pageIndex)` 返回单页文字与字符位置（`PdfTextPage`）；`GetDocumentTextAsync()` 返回全文，页间以换页符 `'\f'` 连接（与 pdftotext 约定一致）。两者与 `PdfDocument.GetTextAsync` / 新增的 `GetFullTextAsync` 一样受复制权限约束：禁止复制的受限文档抛 `UnauthorizedAccessException`，渲染与搜索不受影响。
- `ExportPageImageAsync(pageIndex, dpi = 96)` 按 DPI（36..1200，越界抛 `ArgumentOutOfRangeException`）渲染整页并返回不透明 PNG 字节，页内置旋转与裁剪生效，文档在导出完成前被替换时取消。PNG 由控件内置编码器生成（分块 CRC 正确，可被任意标准解码器独立校验），不依赖平台成像服务，headless 与原生窗口行为一致。
- `PdfDocument.RenderPageRegionAsync(pageIndex, rasterWidth, rasterHeight, regionX, regionY, regionWidth, regionHeight, rotation = 0, cancellationToken = default)` 渲染整幅光栅中的一个矩形区域（瓦片原语）：区域必须落在 `rasterWidth × rasterHeight` 光栅内且为正，越界抛 `ArgumentOutOfRangeException`（消息取 `PdfViewerStrings.ErrorRenderRegionRange`）。瓦片与整幅渲染裁剪逐像素一致（含共享边界、抗锯齿缝）、同为全不透明，旋转坐标映射一致——这是分块渲染的可行性原语；控件显示层暂不使用分块，深缩放超出解码上限时再按此方案接入。

### 触控与快捷键

触摸拖动为原生滚动，双指捏合（`Pinch` 手势）以手势起始缩放为基准计算累计倍率、以捏合中心为锚点缩放；手势结束 / 取消时收尾复位，捏合期间抑制文本拖选避免选区与缩放冲突。轻点用于链接跳转与选择起点。触控板原生缩放手势（平台 magnify 事件）与 Ctrl/Cmd+滚轮两指缩放接入同一缩放锚点路径。快捷键优先级固定为：搜索框 Enter / F3 切换匹配（任意焦点）→ Ctrl/Cmd+O 打开、Ctrl/Cmd+F 聚焦搜索（任意焦点）→ 文字输入 / 按钮焦点保留自身按键 → 其余快捷键作用于阅读器。`AllowShortcuts = false` 整体关闭快捷键层（含 Ctrl/Cmd+O / F、F3 与 Ctrl/Cmd+滚轮缩放，普通滚轮滚动不受影响）；控件 `IsEnabled = false` 时同样整体禁用。

阅读器快捷键：Ctrl/Cmd+加减号缩放，Ctrl/Cmd+0 实际大小（100%），Ctrl/Cmd+1 适合整页，Ctrl/Cmd+2 适合宽度，Ctrl/Cmd+3 适合高度，Home / End 跳至首尾页，左右方向键上一 / 下一页（双页模式按跨页），PageUp / PageDown 或 Shift+空格 / 空格滚动一屏（双页模式为上一 / 下一跨页），Esc 清除选择。

页面采用连续绘制，按视口加预渲染行调度原生解码：`PrerenderRows`（styled `int`，默认 1，钳制 0–8）控制视口上下各预渲染的行数（单页 / 连续一行 1 页，双页模式一行 2 页），设 0 时只解码可见页。渲染按质量分层：停稳的可见页以屏幕目标光栅全质量解码，预取行与滚动 / 缩放交互期间的缺页先以约半分辨率的预览层快速填充，交互停稳（约 150ms 去抖）后自动升级全质量（停滚高清）；缩放交互期间已缓存位图直接拉伸续用、兼作预览，不触发重解码（缩放预览）。首帧渲染不走交互突发路径，直接全质量。

单页解码上限与官方同口径且可配置：`MaxRenderDimension`（styled `int`，默认 8192，钳 256–16384）限制光栅长边，`MaxRenderPixels`（styled `long`，默认 30,000,000，钳 65,536–67,108,864）限制总像素，`MaxRenderScale`（styled `double`，默认 8，须有限、钳 1–8）限制有效 DPI（`clamp(RenderScaling, 1, MaxRenderScale)`；跨屏 DPI 变化自动按新比例重光栅，DIP 布局不变）；三者为硬上限，取整不越界。高倍率时保留布局尺寸，渲染分辨率受上限与页面预算共同约束（每页像素预算 = `min(MaxRenderPixels, PageCacheBudget × 0.95 / 4 / 候选页数)`，可见页很多时按预算分配），修改任一上限立即重渲染。

缓存按预算保留而非按浏览历史无限增长，预算约束的是保留上界，不保证当前渲染集合的下限（页很大时渲染集合可临时占到约 95% 预算）：`PageCacheBudget`（默认 128 MiB）、`ThumbnailCacheBudget`（16 MiB）、`TextCacheBudget`（32 MiB）均为 styled `long`，写入时钳制到 `MinimumCacheBudget`（64 KiB）。超过预算（高水位）才裁剪，LRU 淘汰到预算的 85%（低水位）——这就是保留迟滞：短时回看不会来回抖动，当前渲染请求集与文本选区锚点 / 焦点页永不中途淘汰。进程级 `PdfViewer.GlobalCacheBudget`（静态，默认 0 = 不限）给出多 viewer 总配额，`GlobalCacheBytes` 汇总当前占用；配额收紧时各 viewer 自行按比例收缩有效预算并淘汰，不做跨 viewer 淘汰。修改任一预算或 `PrerenderRows` 会立即裁剪并恢复可见预取。

PDFium 调用跨文档全局串行，前台 / 按页搜索 / 缩略图使用 `4:2:1` 加权轮转，权重表示调度机会，不是 CPU 时间或时延保证。可见页与邻页预取目前共享前台队列，独立预取优先级及完整性能验收归 P3。

`RenderedPageCount` 是当前模板实例累计完成的页面解码数（预览填充 + 停稳升级的分层渲染会让同一页面先低清后全质量各计一次），`CachedPageCount` / `CachedBytes` 只计页面位图。完整内存记账看 `CacheStatistics`（`PdfViewerCacheStatistics`）：页面 / 缩略图位图、文本估算、解码中间数组在途字节与峰值（即 `FPDFBitmap_CreateEx` 的托管像素数组、同时是原生渲染目标内存）、输入字节（钉住数组 / 暂存文件 / 快照长度）与 `BudgetedBytes` / `TrackedBytes` 汇总恒等式；PDFium 内部堆不在可测范围，不计入也不声称。质量状态可读计数 `PreviewPageCount` / `DegradedPageCount`：前者是当前缓存中仍在预览层的页，后者是全质量层因预算 / 上限被迫低于屏幕目标光栅的页——降级是显式记账，不是静默取胜。`WaitForIdleAsync` 等待当前可见页面渲染及其停稳后的质量升级，不代替布局、缩略图或解码缓冲归零等待（后者可轮询 `CacheStatistics.DecodeBufferBytes`）。

## 受限 FreeText 写入后端

功能包的 `NT.AvaloniaControl.PdfViewer.Editing.PdfEditSession` 提供代码驱动的生产写入入口，独立于当前阅读控件的 UI 和只读文档快照：

```csharp
await using var edit = await PdfEditSession.OpenAsync(await File.ReadAllBytesAsync("input.pdf"));
var font = new PdfEmbeddedTrueTypeFont(await File.ReadAllBytesAsync("licensed-static-font.ttf"));
var note = await edit.AddFreeTextAsync(0, new PdfEditRectangle(40, 300, 300, 50),
    "中文批注 ABC", font, fontSize: 18);
await edit.SaveAsync("output.pdf");
```

需引用 `NT.AvaloniaControl.PdfViewer.Editing` 命名空间。矩形采用 PDF 原始左下原点、point 单位，位于 crop / rotation 变换之前；不能直接传入阅读控件的左上原点选区。字体必须是有嵌入权限且包含所需字形的静态 TrueType；单参数字体构造函数默认自动子集，保留请求字形、`.notdef` 和复合字形依赖。`fsType.NoSubsetting` 自动完整嵌入，其他禁止可编辑嵌入的许可拒绝。需要完整字体时使用 `new PdfEmbeddedTrueTypeFont(bytes, PdfFontEmbeddingMode.Full)`；原单参数构造函数的二进制入口保留，默认行为从完整嵌入改为自动子集。

每次更新仍须提供原始字体对象，重新生成包含新增字符的子集；不能拿已经保存的子集补出缺失字形。使用显式 Unicode 标量 / CID / GID 和 UTF-16BE ToUnicode，支持经过验证的扩展汉字及同字形 Unicode 别名，批注 Contents 与实际 AP 文本保持一致。当前验证范围为三套 OFL 静态 TrueType 字体的简繁中文、日文、预组韩文、Ext-B、复合轮廓与 cmap 4/12；不是任意 CJK 字体的兼容保证。可变 / CFF / TTC、竖排、变体序列、复杂排版 / RTL、换行、字体回退仍未实现，缺字直接拒绝。字体不随产品捆绑，宿主负责提供可用且有许可的字体；已很小的字体可能因规范表 / 名称开销略增。

加密及签名输入拒绝。`ExportAsync` 返回自有副本且不清 Dirty；流保存保留流打开，失败可能有部分输出；文件保存采用验证后原子替换。细节和限制见[功能包 README](../src/NT.AvaloniaControl.PdfViewer/README.md#bounded-freetext-writing-backend)。

### 有界撤销历史、编辑事务与用户书签持久化

P4-01 起内容编辑（FreeText、净化、红删）进入有界撤销历史，用户书签按单独规则持久化：

```csharp
await using var edit = await PdfEditSession.OpenAsync(bytes);
await edit.AddFreeTextAsync(0, bounds, "第一笔", font, 18);
await edit.AddFreeTextAsync(0, otherBounds, "第二笔", font, 18);
await edit.UndoAsync();            // 撤销「第二笔」；CanUndo / CanRedo / UndoCount / HistoryBytes 实时反映
await edit.RedoAsync();            // 重做；新的编辑会清空 redo 栈

// 事务把一组编辑合并为一条历史记录，失败全量回滚字节、修订与 dirty：
await edit.ExecuteTransactionAsync(async ct =>
{
    await edit.UpdateFreeTextAsync(id, 0, bounds, "改名", font, 18, ct);
    await edit.AddFreeTextAsync(0, bounds, "新增", font, 18, ct);
});

// 书签：不进撤销历史、undo / redo 永不回滚；导出 / 保存时注入私有 catalog 块，重开即回读
await edit.SetUserBookmarksAsync(
[
    new PdfEditUserBookmark(Guid.NewGuid(), "第一章", 0, PdfUserBookmarkFlags.Bold | PdfUserBookmarkFlags.Italic),
    new PdfEditUserBookmark(Guid.NewGuid(), "附注", 1, PdfUserBookmarkFlags.Bold),
]);
var saved = await edit.ExportAsync();
await using var reopened = await PdfEditSession.OpenAsync(saved);
var persisted = await reopened.GetUserBookmarksAsync(); // 与写入集合一致
```

历史约束：`UndoLimit`（每栈条目数，默认 100，下限 1）与 `HistoryByteLimit`（留存快照字节，默认 64 MiB，下限 64 KiB）双限；调低即裁剪，最新一条撤销记录永远保留（字节可因此超出单条预算）。`IsDirty` 按内容状态身份加书签叠加层判定：撤销回到保存点即干净，保存失败保留 dirty，重复写入同一书签集合是 no-op。新分支分配全新的内容状态身份；书签与最后成功保存的集合逐项比较，改回该集合只清除书签脏状态，不清除未保存的内容变更。净化 / 红删清除书签后，Undo 只恢复内容；若保存集合中仍有书签，状态保持脏。同一状态的重复导出 / 保存字节一致（会话按状态缓存注入输出）；跨会话的字节复现不保证——PDFsharp 6.2.4 每次结构化保存都会以全新随机 XMP 文档 ID 替换 catalog 元数据（锁定版本源码已核对）。事务内禁止嵌套事务、undo / redo、保存、书签修改与历史限制调整；净化 / 红删各记一条历史并按其破坏性策略清空会话书签。持久化块（`/NTUserBookmarks`）属本包私有命名空间，其他阅读器忽略；损坏或越界的持久化块在打开时明确报错，不静默丢弃。书签标题限 1–512 个 UTF-16 码元、ID 非空且唯一、页索引零基、标志仅 None / Bold / Italic。

上述完整字体 / 表单 / 部分删除兼容矩阵来自 P0 历史证据。P1 已改变功能包 DLL，本轮没有在新 DLL 上重新执行这些完整大矩阵；新一轮包消费者检查不等同于重新验收全部 CJK、复杂布局或 IME。

## 外观与本地化

### 细粒度工具可见性

`IsToolbarVisible` / `IsSidebarVisible` 之外，工具栏按工具组提供 styled 开关（默认全部 `true`）：`IsOpenToolVisible`、`IsSidebarToggleVisible`、`IsNavigationToolVisible`（上一页 / 页码 / 下一页）、`IsZoomToolVisible`、`IsFitToolVisible`、`IsRotateToolVisible`、`IsCopyToolVisible`、`IsCloseToolVisible`、`IsDisplayModeToolVisible`（页面布局选择与封面规则勾选）、`IsBookmarkToolVisible`、`IsSearchToolVisible`（整个搜索行）。默认模板按开关收起对应工具，可与窄窗口策略组合；自定义模板可自行取舍这些开关。

### 侧栏布局与宽度

- `SidebarWidth`（styled `double`，默认 208，须有限且非负）控制侧栏宽度。默认主题从 `PdfViewerSidebarWidth` 资源播种该属性：改资源可整体换宽度，改属性可逐控件覆盖（含绑定）；窄窗口可把它调小，或结合 `IsSidebarVisible` 与工具开关收起非必要区域。
- `SidebarPlacement`（`PdfSidebarPlacement.Left` / `Right`，默认 `Left`）把侧栏放到文档左侧或右侧。默认模板经 `PART_BodyGrid` / `PART_Sidebar` / `PART_CanvasGrid` 实现并随属性切换网格列与分隔线方向；自定义模板可忽略该属性。
- `SidebarPresentation`（`PdfSidebarPresentation.Offset` / `Overlay`，默认 `Offset`）定义侧栏与文档的相遇方式：`Offset` 让侧栏独占一列、文档在旁重新排版；`Overlay` 让侧栏浮在文档之上（`ZIndex` 高于文档层），文档保持完整宽度——即窄窗口呈现。两种呈现均随 `SidebarPlacement` 切换浮动侧（右侧放置时浮 / 停靠在右缘）。默认模板同样经 `PART_BodyGrid` / `PART_Sidebar` / `PART_CanvasGrid` 实现，自定义模板可忽略。
- 关闭侧栏会取消进行中的缩略图解码等非必要工作；重新打开恢复缩略图可见区请求并同步选中项（`PART_Thumbnails.SelectedIndex` 跟随 `CurrentPage`），阅读页码与用户书签选择状态不受影响。

窄窗口、长译文与大字体的自动化验收范围（2026-10-21 WP5）：420×640 窄窗口（含 `SidebarWidth = 120`、Overlay 呈现与工具组收起）下导航、缩放与布局可用；80–200 字符替换字符串与 `Window.FontSize = 28` 下工具栏、搜索占位与状态栏渲染正常；状态格式参数可重排（`StatusPageFormat = "页码 {1}←{0}"` 实际渲染「页码 3←2」）；真实 Tab 按键走查工具栏焦点顺序（打开 → 页码 → 搜索，全部焦点停靠点留在阅读器内）。读屏软件与输入法的实机行为不能由既有无障碍名称代替，仍在设备验收清单（WP5 / WP7）。

### 独立默认外观与 Fluent 资源映射

主题自带不依赖 Fluent 的亮 / 暗固定色板（`Colors.axaml`：`PdfViewer*Brush` 七色，以及 `PdfViewerCornerRadius` / `PdfViewerSidebarWidth`）。两种加载方式之外，`new PdfViewerTheme(useFluentResources: true)` 会把七色改写为 Avalonia Fluent `System*Color` token 的包装刷（`Resources/Fluent.axaml`）：Background ← `SystemAltHighColor`、Foreground ← `SystemBaseHighColor`、MutedForeground ← `SystemBaseMediumColor`、Border ← `SystemBaseLowColor`、Toolbar ← `SystemChromeMediumLowColor`、Canvas ← `SystemChromeMediumColor`、Error ← `SystemErrorTextColor`。映射随宿主 Fluent 亮 / 暗变体自动变化，独立色板保留为回退；也可把 `avares://NT.AvaloniaControl.PdfViewer.Themes/Resources/Fluent.axaml` 以 `ResourceInclude` 合并进应用 / 窗口资源实现同样效果。Fluent 12.0.3 只暴露 `System*Color` 颜色 token（无语义刷），映射层负责包一层 `SolidColorBrush`。

### 字符串与本地化（PdfViewerStrings）

工具提示、按钮 / 页签文字、空态、加载、密码提示、状态栏、文件对话框、上下文菜单、书签标题格式与阅读面错误消息都集中在功能包的静态 `PdfViewerStrings` 表。默认值与旧硬编码字面量逐字一致（含测试锁定的「已达上限」「第 N 页」「搜索 …」等），宿主在创建阅读器 UI 前替换即可本地化；默认模板在控件模板实例化时读取（`x:Static`），已创建的控件不会热更新：

```csharp
PdfViewerStrings.OpenText = "Open PDF";
PdfViewerStrings.StatusPageFormat = "Page {0} of {1}";
PdfViewerStrings.ErrorEmptyDocument = "The PDF file is empty.";
```

`PdfDocumentSource.FromBytes` / `FromStreamFactory` 不传显示名时取 `PdfViewerStrings.DefaultDocumentName`。`Editing` 结构化写作后端面向开发者的异常消息不在该表内，保持固定英文。

### 无障碍名称

默认模板的按钮、输入框与勾选框全部提供 `AutomationProperties.Name`：带文字的控件复用同一字符串表条目，符号按钮（放大 / 缩小 / 旋转）与页码 / 搜索框使用专用的 `*AccessibleName` 条目（`ZoomInAccessibleName` 等）。字符串表替换同时作用于可见文字、工具提示与无障碍名称。

## 自定义模板

功能包支持独立消费。模板至少包含 `PART_ScrollViewer`（`ScrollViewer`）和其内容 `PART_DocumentPresenter`（`PdfDocumentPresenter`）。其余侧栏、输入框和按钮都是可选的；缺少工具栏时宿主通过公共方法提供操作。

默认模板还有 `PART_Thumbnails`（`ListBox`）、`PART_Outline`（`TreeView`）、`PART_UserBookmarks`（`ListBox`）、`PART_PageNumber`（`NumericUpDown`）、`PART_SearchBox`（`TextBox`）、`PART_PasswordBox`（`TextBox`，回车提交、Esc 取消）、`PART_DisplayMode`（`ComboBox`，四项依次为连续 / 单页 / 双页连续 / 双页），以及 `PART_Open`、`PART_Close`、`PART_PreviousPage`、`PART_NextPage`、`PART_ZoomIn`、`PART_ZoomOut`、`PART_FitWidth`、`PART_FitPage`、`PART_FitHeight`、`PART_RotateLeft`、`PART_RotateRight`、`PART_FindNext`、`PART_FindPrevious`、`PART_Copy`、`PART_AddUserBookmark`、`PART_RemoveUserBookmark`、`PART_PasswordSubmit`、`PART_PasswordCancel` 按钮。侧栏「封面单独显示」勾选框通过绑定 `ShowCoverPage` 提供，无需具名部件。`PART_BodyGrid`（`Grid`）、`PART_Sidebar`（`Border`）、`PART_CanvasGrid`（`Grid`）承载侧栏行布局，供 `SidebarPlacement` / `SidebarPresentation` / `SidebarWidth` 使用；缺少这些部件时这三项不生效。更换模板会解除旧事件和清理旧呈现器的缓存。

主题资源以 `PdfViewer` 开头，包含 Background、Foreground、MutedForeground、Border、Toolbar、Canvas、Error 画刷，以及 `PdfViewerCornerRadius` / `PdfViewerSidebarWidth`（后者播种 `SidebarWidth` 属性）。画刷可整体替换为 Fluent token 映射，见上文[外观与本地化](#外观与本地化)。PDF 页面的原始白底与文档内容保持原样，应用主题作用于阅读器外观。

当前为桌面阅读与受限控件保存 / 内容历史；批注编辑 UI、表单填写、内置打印、移动端及浏览器仍待后续阶段。

## P1 当前验证范围

2026-10-16 最终记录冻结 372 份源文件。Windows x64 和 Ubuntu 26.04 WSL2 x64 的同 IL 主验证分别通过 Core **92/92**、Headless **33/33**、原生后端 **11/11**、原生窗口 **22/22**；Ubuntu 的独立锁定还原 / 构建补充轮也通过相同检查，其重新编译 DLL 单独绑定。两个全新缓存 NuGet 消费者通过 **48/48 + 51/51**，Usage 与 Demo Release 构建零警告、零错误。完整路径及哈希见[P1 验证记录](pdfviewer-p1-validation.md)与[机器记录](validation/pdfviewer-p1-20261016.json)。

最终包消费者使用已校验的离线源，`NuGetAudit=false` 仅用于本次任务的还原；本轮没有取得新的依赖漏洞审计通过结论。Ubuntu 24.04 / Windows CI 仅完成配置，尚未远端执行。Ubuntu 26.04 WSL2 的字体匹配与少量字形检查不能替代完整 CJK、输入法、麒麟 x64/ARM64、真实设备、多 DPI 或官方性能验收；G1 仍开放。

## 显式隐藏信息清理与整对象删除

功能包提供 `PdfEditSession.SanitizeAsync` 和 `ApplyRedactionsAsync`，生产路径仅使用 C# / PDFium / PDFsharp。两者都必须传入 `PdfHiddenInformationPolicy.RemoveAllNonPageData`。这一策略会删除全部批注及其可见外观、表单字段 / V / DV / AP、书签、附件、动作、原始 Info / XMP、结构树、缩略图、私有字典和无用资源；不进行表单或批注扁平化。宿主应在自己的交互中清楚说明这个有意选择的破坏性策略。

```csharp
await using var edit = await PdfEditSession.OpenAsync(inputBytes, cancellationToken);
var audit = await edit.ApplyRedactionsAsync(
    [new PdfRedactionRegion(0, new PdfEditRectangle(40, 200, 240, 40))],
    PdfHiddenInformationPolicy.RemoveAllNonPageData, cancellationToken);
await edit.SaveAsync(outputPath, cancellationToken);
```

区域按 PDF 左下原点、裁剪 / 旋转之前的用户空间坐标指定，索引从 0 开始。当前须完整覆盖文字对象、图像、路径或整次 Form 调用；只覆盖部分对象、空选择、带内置旋转 / 透明 / 自定义裁剪的受影响页面会拒绝。不是按字提取范围，也不会画覆盖框。共享 Form 的单页调用可以删除；另一页保留的内容仍应留在输出中，不能用全文件关键字不存在作为共享实例的正确性判断。

处理顺序为清理和预检 → 整对象删除 / 内容重新生成 → 递归资源修剪 / 新目录完整重写 → 结构审计 / 原生重开 / 目标区域检查 → 单修订提交。失败或取消不改变会话字节、Revision 和 IsDirty；导出 / 保存沿用生产写入层的语义。报告的输入 / 输出 SHA-256 用于绑定证据，不是任意 PDF 不可恢复的证明。

严格清理器支持经过验证的有限绘制结构。可选内容、带属性的 marked content、Type3、Pattern / Shading、ICC 等未实现颜色空间、JPEG / JPX / JBIG2 容器、未知过滤器和非默认 UserUnit 均拒绝；输出允许 PDFsharp 新生成的技术元数据。保留的字体程序与图像像素不进行隐写扫描。局部字符 / 像素 / 路径切分、任意嵌套 Form 改写、控件工具栏与撤销历史、原文件或调用方内存的安全擦除、Linux 实机及完整官方对齐均不计入这个后端闭环。
