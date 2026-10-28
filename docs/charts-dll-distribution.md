# Charts 0.2.0-preview.3 DLL 交付

控件库可以直接以 DLL 交付，无需安装程序或安装本项目的 NuGet 包。DLL 候选目录为 `artifacts/charts-dll-0.2.0-preview.3`，程序集从已经验证的规范候选包中提取，不重新编译，也不改变现有长稳使用的产品字节。

## 文件与依赖

| 文件 | 用途 |
| --- | --- |
| `NT.AvaloniaControl.Charts.dll` | 图表核心 |
| `NT.AvaloniaControl.Charts.Themes.dll` | 默认图表主题 |
| `NT.AvaloniaControl.Charts.Skia.dll` | 可选 Skia 加速适配层 |
| `dependencies/StbImageWriteSharp.dll` | JPEG/异步 PNG 编码的托管依赖，版本 1.16.7 |

这三个控件 DLL 为托管程序集，Windows/Linux 使用同一套字节。宿主仍需使用 .NET 10、Avalonia 12.0.3，并部署对应操作系统的 Avalonia/Skia/HarfBuzz 原生依赖及字体。DLL 交付不等于整个桌面应用只需这几个文件。

## 项目引用

例如将交付目录复制到应用的 `lib/Charts`，在应用 `.csproj` 中添加：

```xml
<ItemGroup>
  <Reference Include="NT.AvaloniaControl.Charts">
    <HintPath>lib/Charts/NT.AvaloniaControl.Charts.dll</HintPath>
    <Private>true</Private>
  </Reference>
  <Reference Include="NT.AvaloniaControl.Charts.Themes">
    <HintPath>lib/Charts/NT.AvaloniaControl.Charts.Themes.dll</HintPath>
    <Private>true</Private>
  </Reference>
  <Reference Include="StbImageWriteSharp">
    <HintPath>lib/Charts/dependencies/StbImageWriteSharp.dll</HintPath>
    <Private>true</Private>
  </Reference>
</ItemGroup>
```

需要可选 Skia 适配层时，再引用 `NT.AvaloniaControl.Charts.Skia.dll`。宿主的 `Avalonia`、`Avalonia.Desktop`、`Avalonia.Skia` 等依赖保持 12.0.3，可继续通过宿主现有 NuGet 引用管理；不能通过直接引用 DLL 绕过该版本要求。不要同时通过 NuGet 和文件路径引入两份 Charts。

初始化代码与现有消费者一致：先调用 `WithChartsNativeTextIsolation()`，再进行 Avalonia setup；使用可选适配层时调用 `WithChartsSkiaRendering()`。在应用样式中添加 `ChartsTheme`。普通 JIT、依赖运行时的目录部署和原平台限制保持不变。

## 验证边界

`files.json` 和 `SHA256SUMS` 记录当前交付字节。三个 Charts DLL 应与已验证的规范候选包逐字节一致，编码依赖应与 Windows/Linux 原生消费者使用的 DLL 一致。现有长稳验证这些产品程序集；直接文件引用的还原、构建及部署方式尚未单独进行干净消费者验收，因此不能将包消费者通过写成 DLL 直引专项已通过。

Linux 的完整 8 小时长稳仍以项目发布验收记录为准。项目公开分发许可尚待确定，本目录目前为本地/私有评估候选。原有 nupkg 保留为构建和验证证据，不是用户必须安装的程序。
