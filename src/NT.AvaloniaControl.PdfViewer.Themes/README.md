# NT.AvaloniaControl.PdfViewer.Themes

`NT.AvaloniaControl.PdfViewer` 的可选默认模板，独立提供明暗主题、阅读工具栏、搜索、缩略图、目录和状态界面。

```csharp
Styles.Add(new FluentTheme());
Styles.Add(new PdfViewerTheme());
```

```xml
<pdf:PdfViewer Source="{Binding DocumentPath}"
               CurrentPage="{Binding Page, Mode=TwoWay}"
               IsSidebarVisible="True" />
```

命名空间：`xmlns:pdf="clr-namespace:NT.AvaloniaControl.PdfViewer;assembly=NT.AvaloniaControl.PdfViewer"`。

可在控件或应用资源中覆盖 `PdfViewerBackgroundBrush`、`PdfViewerForegroundBrush`、`PdfViewerMutedForegroundBrush`、`PdfViewerBorderBrush`、`PdfViewerToolbarBrush`、`PdfViewerCanvasBrush`、`PdfViewerErrorBrush`、`PdfViewerCornerRadius`、`PdfViewerSidebarWidth`。

自定义模板保留 `PART_ScrollViewer`、`PART_DocumentPresenter` 即可承载文档。默认模板另提供 `PART_Thumbnails`、`PART_Outline`、`PART_PageNumber`、`PART_SearchBox` 及相应操作按钮。功能包不强制依赖本 Themes 包。
