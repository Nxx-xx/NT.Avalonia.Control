# 项目命名

2026-10-23 起，项目品牌和代码前缀统一为 `NT`。

| 项目 | 当前名称或位置 |
| --- | --- |
| 解决方案 | `NT.AvaloniaControl.slnx` |
| 控件库目录 | `src/NT.AvaloniaControl.*` |
| Demo | `samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj` |
| 程序集、包与命名空间 | `NT.AvaloniaControl.*` |
| XAML 程序集资源 | `avares://NT.AvaloniaControl.<控件或主题>/...` |
| PDF Viewer XML 命名空间 | `https://schemas.nt-controls.dev/pdfviewer` |
| 包作者 | `NT` |

例如：

```csharp
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.TreeDataGrid;
```

```xml
xmlns:charts="clr-namespace:NT.AvaloniaControl.Charts;assembly=NT.AvaloniaControl.Charts"
```

```powershell
dotnet build NT.AvaloniaControl.slnx
dotnet run --project samples/NT.AvaloniaControl.Demo
```

升级消费项目时，需要同步调整 ProjectReference、PackageReference、using、完整类型名、XAML 资源 URI 以及按程序集名称加载的配置，再重新还原、编译和打包。新包标识与旧包标识不同；本次修改没有向 NuGet 发布包。

PDF 生成器使用 `NT.FreeText.` 注释标识、`NTFT_` 字体资源前缀和新的 CMap 名称。旧版保存的 FreeText 注释仍是普通 PDF 内容，但新结构写入器只将新前缀识别为可通过稳定 ID 编辑的注释。

开发文档、API 快照、验证清单和脚本中的项目名称及源码路径已同步规范化。历史报告中的日期、计数、耗时、提交号和 SHA 摘要仍代表原始采集批次，不代表本次更名后的重新验收；需要新的发布证据时，应重新运行相应验证脚本。被忽略的旧构建缓存、本地历史产物和 Git 历史不作为当前源码或发布输入。

性能场景中的实时缩写（例如 `S-RT-POC`）、文字方向 `RTL`、`RenderTargetBitmap` 缩写及第三方硬件名称属于原有技术术语，保持其含义。

## 本次更名验证（2026-10-23）

- 整个解决方案 Debug 构建通过，0 警告、0 错误；全部 ProjectReference 路径有效，PowerShell 脚本语法检查通过。
- 已生成并检查 11 个 `NT.AvaloniaControl.*` NuGet 包，包 ID、作者及程序集名称使用新前缀；未发布。
- 15 个测试项目最终 Debug 回归共 4015 项，通过 4013 项，失败 2 项，无跳过。PDF Headless 测试以重新构建后的 94 项全部通过为准。
- Charts 的 `Evicted_point_peers_are_collectible_and_source_replacement_releases_old_items` 在 Debug 下失败；更名前保留的 Debug 程序也在相同断言失败。
- Markdown 的 `ImageRuleOverridesInheritanceAndExplicitNullWhileRootChangesOnlyInheritedNodes` 在 Debug 下失败；更名前和更名后的 Release 程序分别单独运行此用例均通过。本次未修改该用例的行为或图片取消逻辑，不能据此宣称 Debug 回归全部通过。
- 已通过实际 Demo 的 Avalonia Headless + Skia 渲染更新 README 使用的五张控件截图；不作为原生窗口或硬件性能验收证据。

本地验证产物位于 `artifacts/nt-validation/`，构建和打包日志分别为 `artifacts/nt-build.log`、`artifacts/nt-pack.log`。为避开正在使用的旧构建文件，本次使用独立产物目录：

```powershell
dotnet restore NT.AvaloniaControl.slnx --artifacts-path artifacts/nt-validation -p:NuGetAudit=false
dotnet build NT.AvaloniaControl.slnx --artifacts-path artifacts/nt-validation --no-restore -m:1
dotnet pack NT.AvaloniaControl.slnx --artifacts-path artifacts/nt-validation --no-build --no-restore -c Debug -o artifacts/nt-validation/packages
```

还原命令中的 `NuGetAudit=false` 仅用于本次无法访问在线审计服务的本地环境，没有修改项目的审计设置。测试通过 xUnit v3 进程内运行器执行，例如 `dotnet artifacts/nt-validation/bin/Demo.Headless.Tests/debug/Demo.Headless.Tests.dll -parallel none`。
