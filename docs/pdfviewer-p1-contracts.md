# PDF Viewer P1 公共契约与迁移说明

本文描述当前源代码的公共行为，不代替运行验证记录。PDF 阅读文档、控件及编辑会话仍是不同对象：`PdfDocument` 提供原生阅读能力，`PdfViewer` 应用界面、宿主策略和生命周期状态，已有 `PdfEditSession` 保留其 P0 明确支持与拒绝边界。控件未接入编辑会话不等于编辑会话 API 被删除。

## 数据源与所有权

`PdfDocumentSource` 是可重新打开的数据源，支持以下工厂。

| 工厂 | 数据捕获时机 | 所有权与重开行为 |
| --- | --- | --- |
| `FromFile(string path)` | 创建时规范为绝对本地路径，每次加载重新读文件 | 不下载 URI；文件变化会影响下一次加载 |
| `FromBytes(byte[] bytes, string displayName)` | 创建时复制数组 | 调用方随后可覆盖原数组；每次加载再取得独立副本 |
| `FromStreamFactory(Func<CancellationToken, ValueTask<Stream>>, string displayName)` | 每次加载调用工厂 | 工厂应每次返回新的可读流；从当前流位置复制，成功、失败、取消均释放已返回的流 |

`Kind`、`DisplayName`、`FilePath` 为只读描述。字节和流工厂来源的 `FilePath` 是 `null`。工厂自己在返回流前抛错时，其尚未交给加载器的资源仍由工厂管理。

```csharp
using NT.AvaloniaControl.PdfViewer;

var source = PdfDocumentSource.FromBytes(pdfBytes, "report.pdf");
await using var document = await PdfDocument.LoadAsync(source, password, cancellationToken);
var pixels = await document.RenderAsync(0, 600, 800, cancellationToken: cancellationToken);
if (document.Permissions.CanCopy)
{
    var text = await document.GetTextAsync(0, cancellationToken);
}

var reopenable = PdfDocumentSource.FromStreamFactory(
    token => ValueTask.FromResult<Stream>(File.OpenRead(localPath)), "local.pdf");
```

控件新增 `DocumentSource` styled property 和 `LoadDocumentAsync(PdfDocumentSource, ...)`。属性加载通过控件错误状态和事件报告；需要等待完成及捕获异常的调用方应使用并等待方法。修改 `Password` 时，会优先重开当前 `DocumentSource`；只有旧式本地路径来源时才退回 `Source`。直接传入的旧式 `Stream` 不被缓存为可重开的流工厂。

最后一次显式设置来源或调用加载方法取得当前加载权。旧式 path／bytes／stream 加载会清理之前的 `DocumentSource`。typed 文件来源的 `Source` 同时显示其本地路径，typed bytes／factory 的 `Source` 为 `null`。关闭文档清空两个来源。内部的路径同步不会再次触发同一加载。

## 原生元数据、权限和能力

`PdfDocument.Metadata`、`Permissions`、`Capabilities` 是加载完成时生成的不可变记录；读取它们不再进入原生调度器。文档释放后，已取得的元数据和权限快照仍是历史信息，不能据此启动新的操作。控件同名 direct properties 随当前会话、宿主策略和加载状态更新；控件空闲且有当前文档时才有可执行的阅读能力。

`PdfDocumentMetadata` 包含 `Title`、`Author`、`Subject`、`Keywords`、`Creator`、`Producer`、`CreationDate`、`ModificationDate`、`FileVersion` 和 `SignatureCount`。

- 标准 DocInfo 字符串由 PDFium 以 UTF-16LE 返回；每字段最多 1 MiB（包含结尾零），过大、奇数字节长度或两次读取长度不一致会拒绝加载，并分类为 `InvalidDocument`。
- 日期保留原始字符串，不把不完整 PDF 日期猜成精确时区时间。文件版本读取失败时为 `null`。
- 签名数量只表示原生 API 发现的签名对象数量，原生查询失败时可为 `-1`。不验证 CMS、证书信任、签署者身份、DocMDP 或签名覆盖最新修订。

`PdfDocumentPermissions` 保留 `EffectiveFlags`、`UserFlags`、`SecurityHandlerRevision` 和 `PasswordAccess`。`IsEncrypted` 由安全处理器 revision 是否非负判定。`CanCopy`、`CanPrint`、`CanPrintHighQuality`、`CanModify`、`CanAnnotate`、`CanFillForms`、`CanAssemble` 表达文件权限，不能单独用来决定控件是否已实现某功能。R2 的表单／页面组织权限按其批注／修改权限解释；R3 及以后使用对应独立权限位。

当前锁定的 PDFium 155.0.8044 随包头文件称 owner 返回 `FFFFFFFF`，Windows x64 原生调用实际返回 `FFFFFFFC`。因此身份判断比较当前有效权限与 user 权限，不依赖该常量。初始自造 R2 语料的原始结果如下；这张表记录具体语料，未穷举全部加密算法。

| 文件 `/P` | 实际使用的密码 | EffectiveFlags | UserFlags | `PasswordAccess` |
| --- | --- | --- | --- | --- |
| `-64`，限制复制等权限 | user | `FFFFFFC0` | `FFFFFFC0` | `User` |
| `-64`，限制复制等权限 | owner | `FFFFFFFC` | `FFFFFFC0` | `Owner` |
| `-4`，user 已有全部权限 | user | `FFFFFFFC` | `FFFFFFFC` | `UserOrOwner` |
| `-4`，user 已有全部权限 | owner | `FFFFFFFC` | `FFFFFFFC` | `UserOrOwner` |
| 未加密 | 无密码 | `FFFFFFFF` | `FFFFFFFF` | `Unencrypted` |

全许可 user 与 owner 在这两个公开权限 API 中不可区分，`UserOrOwner` 明确保留这一歧义。该值不表示认证失败，也不提升权限。密码身份的独立诊断使用原始输入和另一解析器核对；生产代码不依赖该解析器。

`PdfCapability` 分开提供 `IsImplemented`、`IsAllowed`、`Reason` 和 `CanExecute`，其中 `CanExecute = IsImplemented && IsAllowed`。`PdfDocumentCapabilities` 的 `CanRead` 等快捷属性与对应项的 `CanExecute` 一致。`Reason` 说明当前最先遇到的限制；未来功能也可能显示 `ReadOnly` 或 `DocumentPermission`，调用方仍须检查 `IsImplemented`，不能将该原因当作功能已实现的证据。

以下为控件已有文档、未处于加载／释放状态时的执行规则。

| 操作 | PDF 文件权限 | 宿主开关 | P1 控件行为 |
| --- | --- | --- | --- |
| 阅读／渲染 | 成功打开即可 | 无额外开关 | 可执行 |
| 搜索 | 作为阅读操作允许 | `AllowSearch` | 允许时可执行；禁用时取消当前搜索、清结果，显式搜索返回空结果 |
| 文本提取 `PdfDocument.GetTextAsync` | `Permissions.CanCopy` | 低层文档不应用控件开关 | 禁止复制时抛 `UnauthorizedAccessException` |
| 控件选择、全选、复制 | `Permissions.CanCopy` | `AllowCopy && AllowTextSelection` | API、命令、工具栏和指针选择共用限制；撤销宿主允许时清除选择 |
| 批注 | `CanAnnotate` | `AllowAnnotations`、`IsReadOnly` | `IsImplemented=false`，无控件编辑入口 |
| 表单输入 | `CanFillForms` | `AllowForms`、`IsReadOnly` | `IsImplemented=false`，无原生交互宿主 |
| 保存 | 尚无控件写入事务 | 尚未接入保存流程 | `IsImplemented=false`；不能从文件选择器 `CanSave` 推导可保存 |
| 打印 | `CanPrint` | `AllowPrinting` | `IsImplemented=false`，即使注册可用服务仍不可执行 |
| 分享 | 由未来宿主流程决定 | 尚未接入分享流程 | `IsImplemented=false`，即使注册可用服务仍不可执行 |

`IsReadOnly` 默认 `true`，不禁用复制或搜索。宿主各 Allow 开关默认 `true`，只表达宿主政策。没有文档、加载中和已释放时，能力分别返回 `NoDocument`、`Busy`、`Disposed`。文件权限是应用行为约束；搜索仍会返回匹配文字，PDF 字节也由调用方提供，因此这些接口不建立 DRM 或安全隔离保证。

## 状态、事件和命令

控件公开 `DocumentState`（`Empty`／`Loading`／`Ready`／`Error`／`Disposed`）、`DocumentSessionId` 和 `LastError`。每次成功打开或重开产生新的会话 ID；关闭／失败后没有当前文档则 ID 为 `null`。`DocumentStateChanged` 传入当次 `State`、`SessionId`，在状态或当前文档身份改变时触发。加载过程中可能已经分配新文档的 ID，业务逻辑应同时检查 `State == Ready`。属性逐项通知，不承诺多个 Avalonia 属性构成原子事务。

旧的 `DocumentLoaded`、`LoadError` 继续存在；新增 `DocumentLoadFailed` 提供 `PdfError` 和失败的 `PdfDocumentSource`。加载方法仍向显式等待方抛出原异常。取消不当作加载失败上报，过时加载不能覆盖新会话。事件回调可以关闭或替换文档，后续发布会检查当前加载版本。呈现或搜索发生错误时，文档可以继续处于 `Ready`，具体错误在 `LastError` 中；不能只用状态枚举代替错误诊断。

`PdfDocumentException.ErrorCode` 保留原始 `FPDF_ERR_*` 数值，并新增 `Kind`；`PdfError.FromException` 将密码、格式、文件访问、权限、原生运行库、未实现操作、取消和释放错误分类。密码缺失与错误密码共用 `PasswordRequiredOrIncorrect`，不猜测哪一种情况。`PdfError.Exception` 保留原异常；宿主记录日志时应自行控制消息、文件路径和文档内容的披露范围，控件事件不附加密码值。

```csharp
reader.DocumentLoadFailed += (_, args) => ShowError(args.Error.Kind);
reader.DocumentStateChanged += (_, args) => UpdateSession(args.State, args.SessionId);
await reader.LoadDocumentAsync(source, password, cancellationToken);

if (reader.NextPageCommand.CanExecute(null))
    reader.NextPageCommand.Execute(null);
```

可绑定 `ICommand` 包括 `OpenCommand`、`CloseCommand`、`NextPageCommand`、`PreviousPageCommand`、`ZoomInCommand`、`ZoomOutCommand`、`FitWidthCommand`、`FitPageCommand`、`FitHeightCommand`、`RotateLeftCommand`、`RotateRightCommand`、`SearchCommand`、`FindNextCommand`、`FindPreviousCommand`、`CopyCommand`、`SelectAllCommand`。`SearchCommand` 接受可选的字符串参数，否则使用 `SearchQuery`。不可执行的命令不会调用其动作；控件操作及命令执行须在 Avalonia UI 线程发起。

打开命令在未释放、未加载且宿主未明确关闭文件选择服务时可执行。默认服务还依赖控件所在顶层窗口的 StorageProvider；没有可用窗口时调用正常结束，不代表已弹出系统选择器。关闭命令也可在加载中执行，以取消当前加载。

## 异步释放与串行调度

`PdfDocument` 和 `PdfViewer` 实现 `IAsyncDisposable`，原来的 `Dispose()` 保留。

- `Dispose()` 停止接收新工作、请求协作取消并开始释放，不在 UI 线程阻塞等待原生关闭。返回时原生句柄及固定数组可能尚未释放。
- `await DisposeAsync()` 等待已接收操作退出及本对象拥有的原生句柄、固定数组完成释放。控件还会等待其已取消加载的收尾与旧文档释放；释放失败会通过等待任务报告。重复调用不会重复关闭同一句柄。
- 控件 `CloseDocument()` 立即清理界面并启动旧文档释放；`CloseDocumentAsync()` 额外等待其跟踪的旧加载／原生释放。控件 close／dispose 的发起仍须在 UI 线程。
- 取消排队项目可使调用方及时完成取消，不必等到前面的原生调用返回；执行中的原生操作保留租约直到安全退出。渐进渲染在 PDFium 暂停点及托管步骤观察取消，搜索按页让出调度器。不能强制中断任意一个正在执行的同步 PDFium 调用，也没有完成时延的硬保证。

```csharp
// 在 UI 线程发起；await 等待句柄实际释放，不能使用 .Wait() / .Result 阻塞 UI。
await reader.CloseDocumentAsync();
await reader.DisposeAsync();
```

原生调用仍跨文档全局串行，使用三队列加权轮转：`Foreground : Search : Thumbnail = 4 : 2 : 1`。各队列内部 FIFO；空队列会被跳过，不会为其空等。权重限制的是调度机会，不是 CPU 时间或实时延迟。打开、前台阅读和原生关闭使用前台队列；搜索每页一个工作项；缩略图使用低优先队列。可见页和邻页预取当前共享前台队列，独立预取优先级和进一步性能调优属于 P3。进程级 PDFium 初始化保持到进程结束，避免另一个消费者仍使用其全局状态时被销毁。

## 平台服务边界

控件的 `FileDialogService`、`PrintService`、`ShareService` 是可注入 styled properties，类型分别为 `IPdfFileDialogService`、`IPdfPrintService`、`IPdfShareService`。

| 服务 | P1 接入情况 | 后续阶段 |
| --- | --- | --- |
| `IPdfFileDialogService.CanOpen`／`OpenAsync` | 打开流程已调用；服务返回 `PdfDocumentSource?`，取消可返回 `null` | 各系统交互终验继续按平台计划推进 |
| `IPdfFileDialogService.CanSave`／`GetSavePathAsync` | 固定宿主契约，控件尚无保存流程调用 | P4 事务／保存后接入对应功能 |
| `IPdfPrintService.IsAvailable`／`PrintAsync` | P8 适配边界，P1 控件不调用 | P8 打印、预览、范围、平台适配与失败报告 |
| `IPdfShareService.IsAvailable`／`ShareAsync` | P8 适配边界，P1 控件不调用 | P8 宿主分享流程与平台适配 |

`FileDialogService == null` 时使用附着窗口的 Avalonia StorageProvider。其返回的外部存储流会先完整复制，随后释放存储文件和流。服务有无打印／分享适配器不影响打开和阅读，也不会自动启动外部进程。

`PdfPlatformDocument` 为未来打印／分享服务提供自有字节快照：构造时复制输入，`OpenRead()` 每次返回调用方负责释放的不可写流，且不暴露底层数组。对象不借用原生句柄，也不携带密码。它本身不验证 PDF 权限；直接调用自定义服务的宿主须先执行自己的权限及授权判断。注册一个 `IsAvailable=true` 的适配器只说明适配器存在，不能将其记为控件打印／分享功能完成。

## 兼容迁移表

| 旧用法／行为 | P1 行为 | 调用方迁移建议 |
| --- | --- | --- |
| `Source:string`，path／bytes／stream 加载方法 | 签名保留，新增 typed source | 需要绑定字节、重新打开或密码重试时使用 `DocumentSource` |
| 原生 `LoadAsync(Stream, leaveOpen)` | 保留当前流位置及 leaveOpen 语义 | 与新 factory 的“返回流交由加载器释放”区别对待 |
| `DocumentLoaded`／`LoadError`、`ErrorMessage` | 保留，增加 typed 错误和会话状态 | 新宿主绑定 `LastError.Kind`，异步回调关联当前 SessionId |
| `PdfDocumentException.ErrorCode` | 数值不变，增加分类 | 旧代码继续可用；新代码用 `Kind` 处理分支 |
| 受限 user 密码仍能直接 `GetTextAsync` | 现在明确拒绝未经文件允许的全文提取 | 先判断 `Permissions.CanCopy` 并处理 `UnauthorizedAccessException`；阅读／搜索仍允许 |
| 只检查文档原始权限 | 能力同时含实现与宿主限制 | 按 `Capabilities.<功能>.CanExecute` 决定功能入口，选择操作再考虑 `AllowTextSelection` |
| `Dispose()` 返回即假定原生已关闭 | 现在仅启动非阻塞释放 | 关文件、退出宿主前需要释放完成时等待 `DisposeAsync()`／`CloseDocumentAsync()` |
| 保存／打印／分享服务可用即认为功能存在 | 服务可用与控件实现分开 | P1 保持对应功能禁用，按 P4–P8 计划接入 |

源代码入口：[文档契约](../src/NT.AvaloniaControl.PdfViewer/PdfDocument.Contracts.cs)、[数据源](../src/NT.AvaloniaControl.PdfViewer/PdfDocumentSource.cs)、[公共模型](../src/NT.AvaloniaControl.PdfViewer/PdfDocumentContracts.cs)、[控件契约](../src/NT.AvaloniaControl.PdfViewer/Controls/PdfViewer.Contracts.cs)、[平台服务](../src/NT.AvaloniaControl.PdfViewer/Services/PdfPlatformServices.cs)、[调度器](../src/NT.AvaloniaControl.PdfViewer/Interop/PdfiumWorkScheduler.cs)。本轮实际执行平台、结果及限制由单独的 P1 验证记录提供。
