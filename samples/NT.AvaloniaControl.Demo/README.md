# NT.AvaloniaControl.Demo

运行后从 Charts → 交互与呈现 → 平台字体进入字体诊断场景。它同时显示真实 `TextBlock` 与图表标题、图例、默认标签中的中文、`é`、😀 和阿拉伯文字；详细原生绑定只在折叠的诊断区显示。其他控件页面和图表场景保持原入口。

正常启动在创建平台、字体和界面之前接入生产 API，不需要验收宿主的隔离参数：

```csharp
AppBuilder.Configure<App>()
    .WithChartsNativeTextIsolation()
    .UsePlatformDetect();
```

Linux/glibc 使用打包的原生文本库并提供实际加载路径、SHA256、版本、符号提供者及 resolver 调用诊断；Windows 为 `NotApplicable`。初始化失败终止启动，不会继续创建可能损坏的文本界面。API 的使用条件见 [Charts README](../../src/NT.AvaloniaControl.Charts/README.md)。

仅验证字体启动（Windows/Linux 使用相同参数）：

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -- --charts-smoke --charts-font-startup-only --smoke-output <新绝对目录>/demo.json --smoke-image <新绝对目录>/demo.png
```

去掉 `--charts-font-startup-only` 可执行既有完整 Charts 冒烟，同时追加字体检查。主 JSON 保留原有格式，详细证据另存为 `demo.font-startup.json`，schema 为 `charts-sample-font-startup/v1`：包含创建界面前与实际文字绘制后的绑定快照、真实 glyph ID/字体哈希、各脚本的 shaped runs、原始 A/C 数据身份及 PNG 哈希。七张 PNG 包含窗口基准图、四个逐项隐藏 TextBlock 的对照图、恢复图及图表导出。任一脚本缺字、缺少实际墨迹或原生绑定未生效都会使检查失败。已有 PNG 不覆盖，请为每次运行指定新目录。

启动失败时另存 `demo.startup-failure.json` 并返回非零退出码。冷缓存回归应为每个新进程提供独立且开始时为空的字体缓存目录；证据记录 `HOME`/`XDG_CACHE_HOME`/`XDG_DATA_HOME`，这些环境变量本身不证明缓存为空。窗口 PNG 是可见原生窗口视觉树的 RTB 读回，不代表桌面 compositor、硬件 GPU 或全部文字塑形验收。
