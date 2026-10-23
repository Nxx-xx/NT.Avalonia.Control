# Demo 截图说明

本目录的五张 PNG 已于 2026-10-23 随项目命名统一更新。它们来自当前 `NT.AvaloniaControl.Demo` 的真实 Avalonia 控件，通过 Headless + Skia 和 `RenderTargetBitmap` 离屏渲染，客户区为 1440×900 DIP，亮色主题。

图片展示当前的大卡片目录、返回和面包屑导航下的示例页面，用于开发文档展示。它们不是物理输入、窗口合成器呈现或平台性能验收证据。历史验收报告及其原始图片仍按原采集批次理解。

| 图片 | 当前场景 |
| --- | --- |
| [treedatagrid.png](treedatagrid.png) | TreeDataGrid → 基础与编辑 → 编辑与校验 |
| [virtualkeyboard.png](virtualkeyboard.png) | VirtualKeyboard → 键盘布局 → 英文键盘 |
| [markdown.png](markdown.png) | Markdown → 阅读与语法 → 基础阅读 |
| [charts.png](charts.png) | Charts → 趋势与面积 → 折线与面积 |
| [pdfviewer.png](pdfviewer.png) | PDF Viewer → 缩放与布局 → 适合页面；内置文档使用 NT 标识 |

本次原始渲染位于本地 `artifacts/nt-validation/screenshots/`，文档图片按原字节复制并经目视检查。源码入口为 `samples/NT.AvaloniaControl.Demo`；项目命名和升级范围见[项目命名说明](../project-naming.md)。

## 后续更新

可用 `tests/Demo.Headless.Tests` 的布局测试生成基础截图，设置 `DEMO_GALLERY_SCREENSHOTS` 为输出目录。需要实际桌面窗口验证时，统一 Demo 仍提供 `--demo-smoke`、`--keyboard-smoke`、`--markdown-smoke`、`--charts-smoke` 和 `--pdf-smoke` 等入口，例如：

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release -- --charts-smoke --smoke-image artifacts/doc-screenshots/charts.png --smoke-output artifacts/doc-screenshots/charts.json
```

更新时同时记录场景、采集日期和呈现方式，不将文档预览与历史平台验收结果混用。