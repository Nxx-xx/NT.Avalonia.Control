# Charts 0.2.0-preview.3 本地包接入

交付目录为 `artifacts/charts-preview-0.2.0-preview.3`。三个包保存在其 `packages` 子目录，Windows 和 Linux 使用同一套包。先查看[验收记录](charts-preview-release-validation.json)中的 `readyForPrivateTrial` 和[已知限制](charts-preview-release-notes.md)。项目分发许可尚待确定，目前用于本地/私有评估。

## 检查交付文件

在交付目录执行以下命令，Python 3 标准库即可运行，不需要安装第三方模块：

```text
python Verify-ChartsPreviewBundle.py --bundle .
```

该命令检查 SHA256 清单、三个 nupkg 的版本及程序集、验收记录一致性和文档链接。加上 `--require-ready` 会同时要求双平台完整 8 小时长稳通过；运行中的记录不能通过这项检查。此检查不重新执行长稳，也不提供数字签名或公开分发许可。

## 引用本地 NuGet 包

使用 .NET SDK 10.0.402、目标框架 `net10.0` 和 Avalonia 12.0.3。在现有 Avalonia 应用的 `NuGet.Config` 中添加本地源，保留公开源以还原 Avalonia 等依赖。下面的本地目录应替换为交付目录的实际绝对路径，Linux 可使用 `/home/用户/charts-preview/packages`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="charts-preview" value="D:/ChartPreview/packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

在应用项目中引用同版本的 Core 和 Themes。Skia 适配包按需添加：

```xml
<ItemGroup>
  <PackageReference Include="NT.AvaloniaControl.Charts" Version="0.2.0-preview.3" />
  <PackageReference Include="NT.AvaloniaControl.Charts.Themes" Version="0.2.0-preview.3" />
  <!-- 可选；精确适配 Avalonia/Avalonia.Skia 12.0.3 -->
  <PackageReference Include="NT.AvaloniaControl.Charts.Skia" Version="0.2.0-preview.3" />
</ItemGroup>
```

应用仍需正常引用 `Avalonia.Desktop` 和所用主题。首次还原成功后保留 `packages.lock.json`，后续可使用 `dotnet restore --locked-mode`。不要从 Linux 另一次构建中取同名同版本包替换交付包。

## 初始化与主题

在 `Program.cs` 中，字体隔离必须位于 Avalonia setup 和任何 HarfBuzzSharp 调用之前。以下代码适用于已经引用可选 Skia 包的应用；不使用该包时去掉对应的 using 和扩展调用：

```csharp
using Avalonia;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Skia;

public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
    .WithChartsNativeTextIsolation()
    .WithChartsSkiaRendering()
    .UsePlatformDetect();
```

在应用 `Initialize()` 中添加图表主题，顺序与已通过消费者一致：

```csharp
Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
Styles.Add(new NT.AvaloniaControl.Charts.Themes.ChartsTheme());
```

创建折线图并将其加入窗口或现有容器：

```csharp
var chart = new NT.AvaloniaControl.Charts.CartesianChart();
chart.Series.Add(new NT.AvaloniaControl.Charts.LineSeries
{
    ItemsSource = new[]
    {
        new NT.AvaloniaControl.Charts.ChartPoint(0, 2),
        new NT.AvaloniaControl.Charts.ChartPoint(1, 4),
        new NT.AvaloniaControl.Charts.ChartPoint(2, 3),
    },
    ShowMarkers = false,
});
Content = chart;
```

图表与数据缓冲区的更新遵循 UI 线程契约。完整属性和图族见随包 API 快照；业务接入可以参照仓库 `samples/Charts.Usage`。

## Windows 和 Linux 部署

本版验证普通 JIT、依赖运行时的目录部署：

```text
dotnet publish YourApp.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishAot=false
dotnet publish YourApp.csproj -c Release -r linux-x64 --self-contained false -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishAot=false
```

按目标平台选择一条命令，复制整个 publish 目录；目标机器需安装 .NET 10 运行时。不要只复制业务 DLL，也不要遗漏 `runtimes` 中的原生资产。Linux 还需可用的桌面会话、平台原生依赖及覆盖所需语言的字体。检查 `ChartNativeTextBinding.Diagnostics`：Windows 为 `NotApplicable`，已验证的 Linux/glibc 普通启动应为 `Initialized`，且实际观察到隔离库。初始化失败时修正部署配置并重启进程。

Linux 当前资格限于记录中的 WSLg/X11 软件环境。Ubuntu 22.04/24.04 实机、硬件 GPU、物理触控、ARM64、单文件、裁剪和 NativeAOT 留到后续验证。问题反馈请附包 SHA256、系统/运行时、初始化诊断、最小数据源和异常日志。
