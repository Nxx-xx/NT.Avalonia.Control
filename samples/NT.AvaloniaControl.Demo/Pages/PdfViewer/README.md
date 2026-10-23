# PDF Viewer Demo

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -- --page pdfviewer
```

页面首次激活时加载自带三页 PDF，无需下载文件。样本文档由 `SamplePdf.cs` 使用 PDF 标准对象和交叉引用表生成，包含可搜索的英文文字、三个目录书签、纵向和横向页面。搜索 `Avalonia` 可测试跨页匹配。

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -- --pdf-smoke
```

原生验证会打开 PDF 页面，检查加载、渲染、搜索、导航、缩放、旋转及明暗主题，写出 `artifacts/platform/pdfviewer.json`、`pdfviewer.png`、`pdfviewer-dark.png` 后自动退出。也可传入 `--smoke-output` 与 `--smoke-image` 指定路径。图像由 Avalonia `RenderTargetBitmap` 生成，不是桌面截图；物理键鼠与其他桌面平台需另行验收。

“打开本地 PDF”可加载自己的文档；加密文档可先输入密码。阅读器自带连续滚动、缩略图、目录、页码跳转、缩放、整页/宽度适配、旋转、搜索与文字选择复制。关闭 Demo 时会释放文档和渲染资源。
