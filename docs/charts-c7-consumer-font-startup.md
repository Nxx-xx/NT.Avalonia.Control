# C7-02 普通消费者的 Linux 字体启动

更新日期：2026-10-24。本批处理 `C702-LINUX-STARTUP-01` 的普通应用接入，保留[上一批平台验收](charts-c7-platform-acceptance.md)的全部失败与设备限制。C7-02 整项仍未完成。

## 修正与接入

上一批定位到系统 HarfBuzz 与 HarfBuzzSharp 包内原生库的符号抢占；仅验收宿主的隔离选项有效，未覆盖 Demo、Usage 或普通包消费者。本批在 Charts 功能包提供明确的早期入口，在创建任何 Avalonia 应用或使用 HarfBuzzSharp 原生函数之前调用：

```csharp
using Avalonia;
using NT.AvaloniaControl.Charts;

public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
    .WithChartsNativeTextIsolation()
    .UsePlatformDetect();
```

Demo 与 Charts.Usage 的正常 Program 已接入，不需要验收宿主的 `--isolate-native-text` 参数。也可最早调用 `ChartNativeTextBinding.Initialize()`；`Diagnostics` 返回不可变快照。完整时序、线程、所有权和失败说明见[功能包 README](../src/NT.AvaloniaControl.Charts/README.md)。

glibc Linux 的实现检查当前 HarfBuzzSharp `8.3.1.3` 托管版本及对应架构的原生文件大小、SHA-256，然后以 `NOW | LOCAL | DEEPBIND` 加载包内绝对路径，核验 `hb_font_create` 与 `hb_version_string` 的实际提供者和原生版本 `8.3.1`，仅注册 HarfBuzzSharp 程序集的 resolver。标准 `runtimes/<rid>/native/` 与 RID 发布平铺目录都有路径处理；不搜索系统库、当前工作目录或 NuGet 缓存来代替应用缺失的文件。

Windows/macOS 等不适用平台为 `NotApplicable`，该入口不加载 Linux 库。没有 module initializer、`LD_PRELOAD`、系统库替换、驱动/黑名单调整或依赖版本升级。重复和并发调用共享状态；生效句柄保留至进程结束。适用 Linux 上缺资产、资产/版本不符、musl、过晚调用、无法确认原生模块或外部 resolver 冲突均明确失败，保留错误代码且禁止失败后默认回退；修正后必须重启。其他应用需要显式接入，安装 Charts 包不会自动修改其启动顺序。

linux-x64 原生资产为 2,808,040 bytes，SHA-256 `1D5C3AFEF13545BF34BF8F068B14E25EE619C3B6DEE235C44E260FE61CB24018`。linux-arm64 有单独资产映射与文件指纹，但本批没有 ARM64 原生执行资格。依赖升级需要重新评估托管版本、资产和原生隔离，不会静默接受未知二进制。

## Demo 与 Usage

Demo 的 Charts「交互与呈现」和 Usage 页尾新增「平台字体与启动诊断」折叠场景。它们显示真实 TextBlock、图表标题、坐标轴、系列与数据标签中的中文、组合字符、emoji 和阿拉伯文字，另可展开本次启动诊断。

正常创建面板只构造 UI 和读取状态；原生字体初始化只属于 Program 早期入口，Headless 消费者不在控件构造中加载原生库。专用 native smoke 在完整正常启动后另开真实窗口，逐类检查实际 glyph、字体匹配和文件指纹、原始 A/C 身份、文字隐藏/恢复像素及图表导出；完整旧冒烟也追加同样 17 项检查。

```powershell
dotnet run -c Release --project samples/Charts.Usage -- `
  --smoke --charts-font-startup-only `
  --smoke-output <新的绝对路径>/usage.json --smoke-image <新的绝对路径>/usage.png

dotnet run -c Release --project samples/NT.AvaloniaControl.Demo -- `
  --charts-smoke --charts-font-startup-only `
  --smoke-output <新的绝对路径>/demo.json --smoke-image <新的绝对路径>/demo.png
```

主 JSON 保留原断言结构，详细诊断为 `<stem>.font-startup.json`。每个过程另产出七张字体 PNG：窗口基准、四类文字分别隐藏、恢复图与 chart export。启动异常会尽量保留 `<stem>.startup-failure.json` 并非零退出；原生崩溃仍以监督器退出码、stdout/stderr 和已有证据为准，不补写通过。

开发首轮真实查看发现深色主题文字被导出到硬编码白色背景，字形存在但对比不足。两示例已改为按实际主题选择导出背景；旧 `dev/usage-v1` 图片保留，不能充当修正后的图像证据。

## 验证口径

双平台主回归使用同一 2,052 文件冻结候选 v1，manifest SHA-256 `8D5298B051237993EE3AD8F2777D7052CBAF4A55D3C1907C690D5880D068AD98`，各自正常还原、审计和重新构建。独立包夹具使用 v2，SHA-256 `93262D1E06CE3641B76F25B928E71F8EC17F9637A0D468F7E583C69C5F90157C`；只有四个夹具文件变化，产品、测试与示例输入未变。包消费者只通过本轮 nupkg 引用，检查实际加载核心 DLL 与包内 payload 的哈希。

每个原生子进程使用新私有 XDG、dotnet home、临时目录和空私有字体缓存目录；Linux 写独立 FONTCONFIG_FILE，保留系统字体配置及已存在的系统缓存。本批是全新进程/私有缓存回归，不是清空所有 OS 字体缓存。测试字体来自系统和显式测试字体目录，没有向产品包安装字体；普通应用仍需提供所需语言的字体。

独立检查关联进程 PID、平台/原生窗口、before-UI 与真实文字后的诊断、包内原生路径/大小/哈希、实际解析器调用和 A/C 原始身份；要求四类实际文字、九个指定码点、四份隐藏证明和七张唯一 PNG，重新解码像素核验差异及恢复。旧 Demo/Usage 检查按名称逐项核对，避免仅靠总数增加掩盖删测。

## 双平台验证结果

| 检查 | Windows x64 | Ubuntu 26.04 WSLg x64 |
| --- | --- | --- |
| SDK / runtime / Avalonia | 10.0.402 / 10.0.13 / 12.0.3 | 相同 |
| Headless 全量 | 3,191 / 3,191；含新增 20 项初始化回归，无跳过 | 3,191 / 3,191，无跳过 |
| 原平台校验器自测 | 106 / 106 | 106 / 106 |
| 字体专用原生过程 | Demo、Usage 各两次，全新私有缓存，全部退出 0 | 同左；普通 Program 入口，无宿主隔离参数 |
| 完整 Demo / Usage | 5,203 / 5,203；948 / 948 | 5,203 / 5,203；948 / 948 |
| 旧检查保留 | 逐名保留原 5,186 / 931 项 | 同左 |
| 字体原生证据 | 六个过程各 17 项新增检查；42 PNG 独立验证 | 同左；before-UI imports 为 0，真实文字后为 21，实际包内 provider/pin 一致 |
| 实际查看 | Demo/Usage 各窗口与导出，共四张新图 | 同左；Linux emoji 为正常单色字形，不声称与 Windows 彩色一致 |
| 独立原生 nupkg 消费者 | 正常启动、重复初始化，两过程与独立像素检查通过 | 正常、重复、系统 HB 先 GLOBAL 加载，三个真实窗口过程通过 |
| 独立失败处理 | 不适用的 Linux 策略未冒充 Windows 覆盖 | 缺资产、Avalonia 已启动、包内原生库已加载、外部 resolver 冲突，四过程检查通过；不计作窗口通过 |
| 既有独立包消费者 | function 1,113 / 1,113；themes 1,147 / 1,147 | 相同；两类均隔离 NuGet 缓存、无源码 ProjectReference |

两端每个示例均运行两次专用字体过程及一次完整冒烟，各过程均使用新的私有目录。独立像素检查共覆盖 84 张本轮字体图；渲染读回为 live native window 的 RenderTargetBitmap，未据此认证 compositor、物理输入、实际异 DPI 跨屏或帧率。Windows 的生产入口为无副作用的 `NotApplicable`；Linux 为 `Initialized` 且后续有真实 resolver 调用。

独立 NuGet 消费者入口为 `scripts/Test-ChartsNativeTextStartup.ps1`，使用正常 Desktop 窗口、本轮核心 nupkg 与隔离包缓存，无源码 ProjectReference。Linux 额外先以显式 `dlopen(NOW|GLOBAL)` 加载已知系统 HarfBuzz，分别观测普通生产 API 与未调用 API 的诊断基线；缺资产、过晚、原生库已加载、resolver 冲突为单独的失败处理过程，不能当作窗口启动通过数。

该系统库实际版本 `12.3.2`、1,220,816 bytes，SHA-256 `B330CA2B82B22FBCE5E1A15DABFEFFDD97E9976C3A3E48A2696628BFD34526D5`。未调用 API 的 `baseline-system` 过程 PID 930，stderr 为 `free(): invalid pointer`、退出 134，没有终态 raw 或图像通过资格；已保存预加载路径/标志/版本/符号提供者与实际模块记录。对应 `preloaded-system-api` 过程 PID 810 正常退出 0，真实窗口、F 字形与原始点通过，实际使用包内 `8.3.1`，resolver 被调用。这是同一夹具的受控进程对照，结合上一批原生堆栈支持当前符号抢占修正；不把任意其他异常退出都归因为字体问题。

五个通过的独立包窗口各有三张 F 基准/隐藏/恢复图，15 张 PNG 由独立检查器解码；加上示例的 84 张，本批字体专项核验 99 张新 PNG。四个失败策略过程均捕获预期失败并验证不可重试，未创建验证窗口。Linux 诊断基线的 134 与缺少终态报告原样保留，没有计入通过或重试成通过。

独立汇总见[验证 JSON](charts-c7-consumer-font-startup-validation.json)，SHA-256 `D4BB675C92EE7D68A956B0EC7A8BB9C8655B5E690FF155E8F3EECBEEB06A5606`，183,133 bytes。汇总重新核验当前 625 项 Charts 产品、测试、示例与验证输入，且保留 `milestoneComplete=false`。当前锁定版本、显式早期入口及本次实际环境中的普通消费者启动问题已验证修正；未接入此入口的应用不据此取得资格。

```powershell
# 先正常 build/pack 本轮产品，再使用新的隔离输出目录。
pwsh -NoProfile -File scripts/Test-ChartsNativeTextStartup.ps1 `
  -Feed <本轮产品nupkg目录> -OutputDirectory <新证据目录>
```

Linux 同一入口还须传实际的 `-SystemHarfBuzzPath`；需要测试字体或依赖镜像时传 `-FontDirectory` / `-DependencyFeed`。这些参数及边界见[独立夹具说明](../scripts/Consumers/ChartsNativeTextStartup/README.md)。正常还原、锁定还原和审计均保留，没有绕过还原、使用旧程序集或关闭审计。

## 尚待完成

- Ubuntu 22.04/24.04 实机与指定桌面会话尚未执行；当前 Linux 是 Ubuntu 26.04 WSL2/WSLg、X11/XWayland。
- 上一批 llvmpipe GLX GPU 失败、硬件 GPU、物理异 DPI 跨屏、触摸/精密触控板、IME/辅助工具与 compositor 性能资格保持开放；本批没有重跑全部渲染器矩阵或改变其失败状态。
- ARM64 仅有资产/路径与状态机检查；musl、单文件、裁剪、NativeAOT、发布/升级组合及额外发行版未认证，保留后续发布专项。
- 本入口修正当前版本的原生绑定，不代表全部 Unicode 整形、所有语言、全部官方图族/性能或 C7-02 整项完成。高级图例/标签避让、C7-03 官方对照和 C7-04～06 工作继续按总计划推进。
- 百万点命中尾延迟与 UIA/AT-SPI 原生选中项枚举继续按用户决定留待后续专项。

本批证据根目录：`C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-consumer-font-startup-20261024/`。上一批 v6 原始验证 JSON、失败图、崩溃报告与批次未通过状态均保持原样。
