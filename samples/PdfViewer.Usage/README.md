# PdfViewer.Usage

最小 AXAML / MVVM 桌面接入示例。`Source` 绑定视图模型中的实际 PDF 路径，`CurrentPage` 和侧栏开关双向绑定；窗口关闭时释放阅读器与临时样本文档。

```powershell
dotnet run --project samples/PdfViewer.Usage
dotnet run --project samples/PdfViewer.Usage -- "D:\Documents\example.pdf"
```

不带路径时自动生成并打开内置三页 PDF。也可在窗口中选择文件，或修改上方路径。应用同时添加 Fluent 主题与 `PdfViewerTheme`；具体控件由功能包提供，样式由独立 Themes 包提供。

## P2 契约示例（2026-10-21）

窗口按 P2-06 要求演示宿主侧完整接入面：

- **布局**：`PageDisplayMode` 绑定布局模式 ComboBox（连续 / 单页 / 双页连续 / 双页），`SidebarPresentation` 绑定侧栏 offset / overlay 呈现 ComboBox。
- **书签**：`AddUserBookmarkCommand` / `RemoveUserBookmarkCommand` 驱动用户书签；左侧列表展示 `UserBookmarks`，`PdfUserBookmark` 的 `Title` / `Weight` / `Style` 绑定通知演示粗斜体即时刷新，双击按 `PageNumber` 跳转。
- **阅读位置**：`CapturePositionCommand` / `RestorePositionCommand`（视图模型命令，阅读器经 `CommandParameter` 传入、不被视图模型持有）调用 `CaptureReadingPosition` / `RestoreReadingPosition`，`PdfReadingPosition` 各字段由宿主持久化，状态行显示捕获的页 / 锚点 / 缩放 / 旋转。
- **渐进结果与进度**：搜索按页渐进发布，示例绑定 `SearchResultCount`、`SearchProgress`（进度条 + 百分比）与 `SearchResultsTruncated`（「已达上限」提示）。
- **导出**：`ExportPageCommand` 调用 `ExportPageImageAsync` 按 144 DPI 导出当前页 PNG 到临时目录并报告路径与字节数。

示例同时保留 `AllowCopy` / `AllowSearch` 宿主策略勾选、`CloseCommand`、`DocumentState` 与密码绑定。验证状态见[状态记录](../../docs/pdfviewer-status.md)。
