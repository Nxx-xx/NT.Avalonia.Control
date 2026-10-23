# MVVM 数据绑定与编辑样例

在仓库根目录运行：

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -- --mvvm
```

统一 Demo 的主窗口提供“概览”和“TreeDataGrid”页面导航，默认打开 TreeDataGrid 的普通模式。TreeDataGrid 页面内部以“普通模式”和“MVVM 模式”并列 Tab 切换；`--mvvm` 打开 TreeDataGrid 页面并选中 MVVM Tab。两个 Tab 使用各自的数据与选择状态，主题由主窗口统一切换。切换页面保留页面实例及状态，离开页面暂停计时器，窗口关闭统一释放资源。

MVVM 工具栏分为“数据操作”“选择定位”和“单元格编辑”，每个按钮均附有用途提示。右侧“编辑草稿”依次显示原值、可修改草稿和输入错误；内部绑定状态收在底部“绑定诊断”折叠面板中。

## 文件职责

| 文件 | 职责 |
| --- | --- |
| `samples/NT.AvaloniaControl.Demo/MainWindow.cs` | 统一导航窗口，承载页面、切换主题及管理页面生命周期 |
| `samples/NT.AvaloniaControl.Demo/DemoPageCatalog.cs` | 集中注册概览、TreeDataGrid 及后续控件页面 |
| `samples/NT.AvaloniaControl.Demo/Pages/TreeDataGrid/TreeDataGridPage*.cs` | TreeDataGrid 的普通 / MVVM Tab、十个普通模式场景及对应验证入口 |
| `samples/NT.AvaloniaControl.Demo/Pages/TreeDataGrid/Mvvm/MvvmView.axaml` | 强类型 `x:DataType` / compiled View 绑定；直接声明表格列、分组按钮命令和编辑草稿面板 |
| `MvvmView.axaml.cs` | 初始化 View；由页面生命周期释放其拥有的 ViewModel，不因 Tab 或页面临时卸载而释放 |
| `MvvmDemoViewModel.cs` | 继承 `ObservableObject`，通过 `[ObservableProperty]` 和 `[RelayCommand]` 生成选择属性与命令；拥有集合和数据源，不接收或查找控件 |
| `MvvmRecord.cs` | 继承 `ObservableObject` 的可观察模型；名称必填、数量范围 0–100，修改前验证，失败时不修改值 |
| `MvvmDemoLauncher.cs` | `CreateView()` 为 TreeDataGrid 页的 MVVM Tab 创建 View / ViewModel |
| `MvvmSmoke.cs` | 在统一 Demo 窗口中运行 MVVM 原生验证；测试逻辑不放进 View 代码隐藏或 ViewModel |

页面命名空间为 `NT.AvaloniaControl.Demo.Pages.TreeDataGrid`，MVVM 文件位于其 `Mvvm` 子命名空间。`--smoke`、`--demo-smoke` 和 `--mvvm-smoke` 均由统一 Demo 的启动入口分派，不再创建单独的 MVVM 演示窗口。

本例绑定应用拥有的 `FlatTreeDataGridSource<MvvmRecord>`，由 ViewModel 释放。集合增删、模型属性通知通过 Source 更新投影。另一种方式是直接绑定 `ItemsSource`，由控件创建和释放适配源；不能同时设置 `Source` 和 `ItemsSource`，切换方式前应先清除前者。

## CommunityToolkit.Mvvm

Demo 引用 `CommunityToolkit.Mvvm` **8.4.2**，版本统一在 `Directory.Packages.props` 管理，锁文件固定依赖。控件库无需引用该包。手写 `DemoCommand` 已移除，命令由 `[RelayCommand]` 生成；现有 XAML 属性和命令绑定名称保持一致。

`[NotifyPropertyChangedFor]` 更新选择和当前单元格的摘要，`[NotifyCanExecuteChangedFor]` 在选择或当前单元格变化时刷新按钮可用性。生成的属性通过 `On…Changing` / `On…Changed` 钩子管理模型与集合的订阅；记住单元格和切换数据源时显式调用 `NotifyCanExecuteChanged()`。

普通模式的 `DemoItem`、`EditingItem` 和模板状态也使用 `ObservableObject`。模板按钮绑定行状态生成的切换、执行、重置与选色命令，暂停后由命令的 `CanExecute` 禁用“执行 +1”。校验和进度钳制等自定义 setter 使用 `SetProperty`；只读行标识及嵌套模板状态继续保留。

## 选择与当前单元格

```xml
<tg:TreeDataGrid Source="{Binding Source}"
                 SelectionUnit="Row" SelectionMode="Multiple"
                 SelectedItem="{Binding SelectedItem, Mode=TwoWay}"
                 SelectedIndex="{Binding SelectedIndex, Mode=TwoWay}"
                 SelectedItems="{Binding SelectedItems, Mode=TwoWay}"
                 CurrentCell="{Binding CurrentCell, Mode=TwoWay}"
                 EditCommands="{Binding Editing}">
  <tg:TreeDataGridTextColumn Header="名称" Width="260"
                           Binding="{ReflectionBinding Name}" />
</tg:TreeDataGrid>
```

`SelectedItem` 是模型对象，`SelectedItems` 是应用可变集合；本例使用 `ObservableCollection<MvvmRecord>`。`SelectedIndex` 的类型是 **IndexPath 源路径**，并非排序后的行号；`default(IndexPath)` 表示无选择。共享同一模型的多个节点需要用源路径精确区分。

可写 SelectedItems 保留原集合，双向同步增删；只读或定长 IList 则回写新的集合快照。若集合自身的 CollectionChanged 通知期间需要规范化选区（例如单选模式添加第二项），State 立即更新，集合回写延后到当前通知栈退出后的 UI 调度，避免 ObservableCollection 重入异常。清空已存在的 Source 会清空全部选择输出；初次绑定中尚未传入 Source 的预设选择会保留到源就绪。

`CurrentCell` 是可空 `CellAddress`，由稳定的 NodeId 和 ColumnId 组成。它与选择独立。点击表格会回传当前单元格；“记住单元格 / 恢复单元格”展示 ViewModel 主动设置该属性。程序只设置 SelectedItem 不会自动指定当前列。

顶部按钮可从 ViewModel 增加、删除或修改记录，按模型或源路径选择，修改多选集合，以及替换 Source。底部记录数量和“绑定诊断”中的状态文字均绑定 ViewModel 属性，用于检查两个方向是否一致。

## 绑定编辑命令、草稿与错误

ViewModel 创建非视觉命令端点：

```csharp
public TreeDataGridEditCommands Editing { get; } = new();
```

View 绑定 `EditCommands` 后，可以直接绑定以下命令和状态：

```xml
<Button Content="开始编辑" Command="{Binding Editing.BeginEditCommand}" />
<Button Content="提交" Command="{Binding Editing.CommitEditCommand}" />
<Button Content="取消" Command="{Binding Editing.CancelEditCommand}" />
<TextBox Text="{Binding Editing.EditSession.Value, Mode=TwoWay}"
         IsEnabled="{Binding Editing.IsEditing}" />
<TextBlock Text="{Binding Editing.EditSession.ValidationError}" />
```

开始命令未带参数时使用当前单元格，也可用 `CellAddress` 作为参数。端点一次连接一个表格，卸载时解除连接、命令失效；更换 DataContext 后按钮绑定到新的端点。VM 不需要保存 TreeDataGrid、Window 或编辑器实例。

右侧草稿 TextBox 与表格内编辑器共享 `EditSession.Value`。在数量列开始编辑，将草稿改为 200 后提交，会保留草稿、显示错误并保持模型不变；改为 42 可提交；改为 9 后取消则保留此前的 42。`OriginalValue` 和 `IsEditing` 也直接绑定到界面。

外围 View 使用 compiled bindings。本轮的可编辑列显式使用 `ReflectionBinding`，因为自动推导写回依赖可写属性路径；不把 compiled 显示绑定误称为可自动推导编辑写回。这个样例的范围验证位于模型 setter，校验在赋值之前完成，异常由编辑事务转成 `ValidationError`。

## 自动验证与证据

CommunityToolkit 重构后的 Release 构建为 **0 警告 / 0 错误**，自动测试 **204 / 204**，MVVM 原生探针 **29 / 29**，既有回归 **36 / 36**，界面及模板探针 **52 / 52**。新增的 8 项 MVVM 检查覆盖命令可执行通知、记住 / 恢复单元格与换源清空，以及选中名称和替换多选集合的摘要通知。按钮检查使用 `IsEffectivelyEnabled` 验证命令控制的有效状态。

本地原始结果位于 `artifacts/platform/communitytoolkit-mvvm.json`、`communitytoolkit-regression.json` 和 `communitytoolkit-demo.json`；该目录属于忽略的验证输出。上述通过项数为当时记录，统一 Demo 的当前重跑命令为：

```powershell
dotnet restore NT.AvaloniaControl.slnx --locked-mode
dotnet build NT.AvaloniaControl.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll --mvvm-smoke --smoke-output artifacts/platform/communitytoolkit-mvvm.json
dotnet samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll --smoke --smoke-output artifacts/platform/communitytoolkit-regression.json
dotnet samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll --demo-smoke --smoke-output artifacts/platform/communitytoolkit-demo.json
```

以下为重构前的历史验证命令和证据：

```powershell
dotnet build samples/TreeDataGrid.Demo --no-restore -m:1 -p:UseSharedCompilation=false
dotnet samples/TreeDataGrid.Demo/bin/Debug/net10.0/TreeDataGrid.Demo.dll --mvvm-smoke --smoke-output artifacts/platform/windows-mvvm-smoke-debug.json --smoke-image artifacts/platform/windows-mvvm.png
dotnet samples/TreeDataGrid.Demo/bin/Debug/net10.0/TreeDataGrid.Demo.dll --smoke --smoke-output artifacts/platform/windows-smoke-mvvm-regression-debug.json
```

首轮 Debug 探针于 2026-10-12 13:36（香港时间）通过；最终发布目录于 13:54 完成 Release 验收：MVVM **21 / 21**，原有回归 **36 / 36**，退出码均为 0。全套自动测试为 **196 / 196**，最终记录和截图见 [MVVM 集成验收](mvvm-status.md)。Windows 10.0.26200 x64、.NET 10.0.12、Avalonia 12.0.3，MVVM 窗口 1200 × 780 DIP、100% 缩放。

21 项检查覆盖强类型 View / Source 绑定、直接 XAML 子列、集合增删、模型通知、SelectedItem / SelectedIndex / SelectedItems / CurrentCell 双向同步、开始编辑、两个方向的草稿更新、验证失败、提交、取消、Source 替换和 DataContext 替换后的命令重新绑定。

开始、提交、取消、数据增删改和换源检查通过 **XAML 声明的实际 Button 的路由 Enter 输入**触发其 Command；当前项回传通过表格的路由方向键触发，草稿修改使用实际 TextBox。程序化路由输入不等同于操作系统键盘自动化。JSON 独立记录每项结果和异常；`windows-mvvm.png`、`windows-mvvm-validation.png` 已检查，展示命令禁用状态、外部草稿和错误文字。

PNG 是 Avalonia RenderTargetBitmap 的离屏控件渲染，不是桌面截图。真实中文输入法预编辑、候选窗、提交 / 取消及麒麟设备仍未验证；本次结果不改变这个验收边界。
