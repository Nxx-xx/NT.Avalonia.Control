# NT.AvaloniaControl

基于 Avalonia 的桌面控件库，包含 TreeDataGrid、VirtualKeyboard、Markdown、Charts 和 PdfViewer。各控件提供独立功能包、默认主题、Usage 示例和统一 Demo。

当前版本为 **0.2.0-preview.2**，使用 .NET SDK **10.0.401**、`net10.0` 和 Avalonia **12.0.3**。

## 当前进度与下一步

更新日期：2026-10-23。

| 控件 | 当前进度 | 下一步 |
| --- | --- | --- |
| TreeDataGrid | 平面与树数据、MVVM、编辑校验、排序过滤、虚拟化、列管理及受控行移动已实现 | 异步加载产品化，补复制与布局持久化，并完成真实输入和无障碍验收 |
| VirtualKeyboard | 数字与英文输入、原生编辑、长按重复和生命周期管理已实现 | 中文输入原型、自动焦点与显隐，以及触屏和 IME 验收 |
| Markdown | 原生只读阅读、全文选择复制、流式更新、图片扩展和 TextMate 高亮已实现 | 目标设备、读屏、DPI 与长期运行验证，完善复杂内容组合 |
| Charts | 常用图族、C4 性能引擎、C5/C6 基础图族及七类统计补充已实现 | C7-01 全功能集成，再推进实机、性能和发布验收 |
| PdfViewer | 完整阅读功能及 P3-01～P3-03 的大文件读取、缓存预算和渲染质量分层已实现 | P3-04 长文档优化、P3-05 性能指标验收，再推进保存与编辑 |

详细安排见[当前进度与下一步](docs/current-status-and-next-steps.md)。

## Demo 截图

点击图片查看原图。图片于 2026-10-23 从当前 NT Demo 的真实 Avalonia 控件离屏渲染生成，展示卡片目录下的示例页面。场景、来源和呈现方式见 [截图说明](docs/screenshots/README.md)。

| TreeDataGrid · 自定义列模板与控件组合 | VirtualKeyboard · 英文键盘与输入框 |
| --- | --- |
| [![TreeDataGrid Demo：表格编辑与校验](docs/screenshots/treedatagrid.png)](docs/screenshots/treedatagrid.png) | [![VirtualKeyboard Demo：英文键盘、编辑目标和输入选项](docs/screenshots/virtualkeyboard.png)](docs/screenshots/virtualkeyboard.png) |

| Markdown · 原生阅读与代码高亮 | Charts · 折线、分组柱状与散点 |
| --- | --- |
| [![Markdown Demo：标题、列表、引用和语法高亮](docs/screenshots/markdown.png)](docs/screenshots/markdown.png) | [![Charts Demo：折线插值、面积与堆叠](docs/screenshots/charts.png)](docs/screenshots/charts.png) |

**PdfViewer · 缩略图、搜索与连续阅读**

[![PdfViewer Demo：内置 PDF、页面缩略图、搜索工具栏和正文](docs/screenshots/pdfviewer.png)](docs/screenshots/pdfviewer.png)

## 控件与文档

解决方案为 `NT.AvaloniaControl.slnx`。功能项目使用 `NT.AvaloniaControl.<控件名>`，默认主题使用 `NT.AvaloniaControl.<控件名>.Themes`；统一示例程序为 `NT.AvaloniaControl.Demo`。

| 控件 | 接入文档 | 开发计划 |
| --- | --- | --- |
| TreeDataGrid | [使用说明](docs/treedatagrid-usage.md) | [后续计划](docs/treedatagrid-next-development-plan.md) |
| VirtualKeyboard | [使用说明](docs/virtualkeyboard-usage.md) | [开发计划](docs/virtualkeyboard-development-plan.md) |
| Markdown | [使用说明](docs/markdown-usage.md) | [执行计划](docs/markdown-next-execution-plan-20261015.md) |
| Charts | [控件说明](src/NT.AvaloniaControl.Charts/README.md) | [开发计划](docs/charts-development-plan.md) |
| PdfViewer | [使用说明](docs/pdfviewer-usage.md) | [开发计划](docs/pdfviewer-development-plan.md) |

## 构建与运行

```powershell
dotnet restore NT.AvaloniaControl.slnx --locked-mode
dotnet build NT.AvaloniaControl.slnx -c Release --no-restore
dotnet test NT.AvaloniaControl.slnx -c Release --no-build --no-restore
dotnet run --project samples/TreeDataGrid.Usage -c Release --no-build
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release --no-build
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release --no-build -- --mvvm
```

[TreeDataGrid.Usage](samples/TreeDataGrid.Usage/TreeDataGrid.Usage.csproj) 是使用说明中的独立最小 MVVM 应用，展示集合绑定、增删、多选、当前单元格、暂存编辑与验证和列头排序；无需启动完整 Demo 即可运行。

Demo 的主窗口提供“概览”、TreeDataGrid、VirtualKeyboard、Markdown、Charts 和 PDF Viewer 页面，默认打开 TreeDataGrid，可用 `--page markdown`、`--page charts` 或 `--page pdfviewer` 选择对应页面。TreeDataGrid 页面保留“普通模式”和“MVVM 模式”两个 Tab；`--mvvm` 打开该页面并选中 MVVM Tab。两个模式都使用 `CommunityToolkit.Mvvm` 8.4.2 管理可观察属性与命令；树展开、排序过滤和组合场景实现见 [MVVM 样例](docs/mvvm-demo.md)。
普通模式通过“选择示例”下拉框切换编辑校验、树节点、按需加载、列宽换行、大数据、横向滚动、列显隐、外观配置与受控行移动。
按钮只在对应场景显示，并附操作说明和悬停提示；布局与诊断功能放在可折叠面板中。

“自定义列模板”包含四个可选示例：行内操作按钮、选色盘、模拟与数字时钟，以及按钮、颜色、时间选择、开关、滑块和进度条的组合。
控件直接操作对应行的数据；滚动回收后保留状态。主窗口统一切换亮暗主题，切换页面保留已有页面实例和数据状态。时钟使用共享计时器，切到 MVVM Tab、离开时钟场景或离开 TreeDataGrid 页面时暂停；窗口关闭时统一释放页面资源。

后续控件页面按以下方式加入统一 Demo：

1. 在 [Pages](samples/NT.AvaloniaControl.Demo/Pages/) 下建立 `<控件名>/`，实现 [Navigation/IDemoPage](samples/NT.AvaloniaControl.Demo/Navigation/IDemoPage.cs)：`View` 提供页面内容，`Activate()` 恢复活动工作，`Deactivate()` 暂停计时器等后台活动，`Dispose()` 释放页面拥有的数据源、订阅和其他资源。
2. 在 Demo 项目中引用该控件独立的功能项目，并按需引用和加载它的主题项目。
3. 在根目录的 [DemoPageCatalog](samples/NT.AvaloniaControl.Demo/DemoPageCatalog.cs) 添加 `DemoPageDefinition`，提供唯一页面标识、名称、说明和页面创建工厂。主窗口负责导航和生命周期；首次打开时创建页面，切换时调用 `Deactivate()` / `Activate()`，窗口关闭调用 `Dispose()`。现有页面标识为 `overview`、`treedatagrid`、`virtualkeyboard`、`markdown`、`charts` 和 `pdfviewer`。

原生窗口验证入口都使用统一 Demo：

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -- --smoke --smoke-output artifacts/platform/regression.json
dotnet run --project samples/NT.AvaloniaControl.Demo -- --demo-smoke --smoke-output artifacts/platform/demo.json --smoke-image artifacts/platform/demo.png
dotnet run --project samples/NT.AvaloniaControl.Demo -- --mvvm-smoke --smoke-output artifacts/platform/mvvm.json
```

应用加载 Avalonia Fluent 主题，再显式加载可选的 `NT.AvaloniaControl.TreeDataGrid.Themes.TreeDataGridTheme`，或通过 `StyleInclude` 加载 `avares://NT.AvaloniaControl.TreeDataGrid.Themes/Themes/Generic.axaml`。功能程序集也支持应用自定义模板。

```csharp
using System.Collections.ObjectModel;
using NT.AvaloniaControl.TreeDataGrid;

var items = new ObservableCollection<Person>();
var source = new FlatTreeDataGridSource<Person>(items);
var grid = new TreeDataGrid { Source = source };
grid.Columns.Add(new TreeDataGridTextColumn
{
    Header = "姓名",
    ValueGetter = item => ((Person)item).Name,
    ValueSetter = (item, value) => ((Person)item).Name = (string)value!,
    ValueType = typeof(string),
    ValueValidator = value => string.IsNullOrWhiteSpace(value as string) ? "姓名不能为空" : null,
});
// 应用关闭且不再使用时 Dispose source；控件不拥有应用的数据源。
```

常规 ViewModel 可直接绑定集合，列在 XAML 中声明；需要自定义树模型、排序或过滤时也可继续绑定 ViewModel 创建的 `Source`。两种数据入口互斥。

```xml
<tg:TreeDataGrid ItemsSource="{Binding Records}"
                 SelectedItem="{Binding SelectedItem}"
                 CurrentCell="{Binding CurrentCell}"
                 EditCommands="{Binding Editing}">
    <tg:TreeDataGridTextColumn Header="名称" Binding="{ReflectionBinding Name}" />
</tg:TreeDataGrid>
<Button Content="提交" Command="{Binding Editing.CommitEditCommand}" />
```

`Editing` 是 ViewModel 创建的 `TreeDataGridEditCommands`。选择和当前单元格属性默认 TwoWay；`SelectedIndex` 为源路径 `IndexPath`，不是显示行号。外围 ViewModel 可以使用 compiled bindings，单元格自动编辑写回及层级绑定使用支持的 ReflectionBinding。具体范围见官方对照文档。

- [后续完善执行计划](docs/treedatagrid-next-development-plan.md)：从当前预览版继续推进的优先级、任务拆分、依赖和验收标准。
- [总体开发计划](docs/treedatagrid-development-plan.md)：P0–P7 总体范围。
- [版本、功能和 API 基线](docs/p0-baseline.md)：已实现条目、差异及后续测试编号。
- [核心公共契约](docs/p1-contracts.md)、[呈现契约](docs/p1-layout.md)、[API 快照](docs/public-api.txt)。
- [P2 呈现布局](docs/p2-rendering.md)、[P3 树模型](docs/p3-tree.md)、[主题与示例](docs/p2-p3-examples.md)。
- [P4 选择编辑](docs/p4-interaction.md)、[P5 排序过滤](docs/p5-projection.md)、[可编辑样例](docs/p4-p5-examples.md)。
- [平台记录](docs/platform-probes.md)、[性能方法](docs/performance-baseline.md)、[P0/P1 历史验收](docs/p0-p1-status.md)、[P2/P3 集成验收](docs/p2-p3-status.md)。

`scripts/Verify.ps1` 执行锁定还原、构建、测试、产品包打包与独立消费者验证。`scripts/Test-PackageConsumer.ps1` 支持 TreeDataGrid、VirtualKeyboard、Markdown 和 PdfViewer，验证真实 NuGet 包的 XAML/主题及功能包自定义模板，不使用源码项目引用。PDF 独立验证也可运行 `scripts/Test-PdfViewerPackageConsumer.ps1`。基准程序：

```powershell
dotnet run --project benchmarks/TreeDataGrid.Benchmarks -c Release -- --output artifacts/benchmarks/p4-p5.json
```

基准程序在控制台和 JSON 文件中输出中文字段、场景名称及说明；耗时单位为毫秒，内存指标为累计分配字节数。`是否通过`、`全部通过` 使用 JSON 布尔值 `true`/`false`。

Windows 原生探针已提供；麒麟环境尚未提供，中文输入法预编辑、候选框和完整 DPI/无障碍验收保持待验证。
