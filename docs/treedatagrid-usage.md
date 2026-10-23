# TreeDataGrid 使用说明

本文说明本仓库 `NT.AvaloniaControl.TreeDataGrid.TreeDataGrid` 的接入方式、MVVM 示例和公共功能接口，适用于当前 `0.2.0-preview.2`。示例使用 .NET SDK `10.0.401`、`net10.0`、Avalonia `12.0.3` 和 `CommunityToolkit.Mvvm` `8.4.2`，以仓库源码为准。

本控件支持平面列表和树形列表、五种列、行列虚拟化、冻结列、行或单元格选择、暂存编辑与验证、同级排序及过滤。命名空间为 `NT.AvaloniaControl.TreeDataGrid`；功能程序集为 `NT.AvaloniaControl.TreeDataGrid`，默认主题位于独立的 `NT.AvaloniaControl.TreeDataGrid.Themes` 程序集。

`NT.AvaloniaControl` 作为控件系列前缀，每个控件使用自己的功能包和命名空间。旧版功能包 `NT.AvaloniaControl`、主题包 `NT.AvaloniaControl.Themes` 的迁移对照见 [README](../README.md)；下面的项目引用、C# 和 XAML 示例均使用当前名称。

## 目录

- [接入与主题](#接入与主题)
- [完整 MVVM 示例：ItemsSource](#完整-mvvm-示例itemssource)
- [选择、当前单元格与编辑](#选择当前单元格与编辑)
- [MVVM 中使用显式 Source](#mvvm-中使用显式-source)
- [树形数据与展开绑定](#树形数据与展开绑定)
- [自定义列模板与编辑验证](#自定义列模板与编辑验证)
- [排序、过滤与批量更新](#排序过滤与批量更新)
- [按节点身份或源路径定位](#按节点身份或源路径定位)
- [受控行与树节点移动](#受控行与树节点移动)
- [属性、方法与事件参考](#属性方法与事件参考)
- [常见问题与当前边界](#常见问题与当前边界)

## 接入与主题

在本仓库内的应用项目中引用两个项目。下文的完整 MVVM 示例已保存为 [samples/TreeDataGrid.Usage](../samples/TreeDataGrid.Usage/TreeDataGrid.Usage.csproj)，并加入仓库解决方案；包版本、目标框架和 compiled bindings 默认值继承仓库根目录配置。

[TreeDataGrid.Usage.csproj](../samples/TreeDataGrid.Usage/TreeDataGrid.Usage.csproj)：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia.Desktop" />
    <PackageReference Include="Avalonia.Themes.Fluent" />
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <ProjectReference Include="../../src/NT.AvaloniaControl.TreeDataGrid/NT.AvaloniaControl.TreeDataGrid.csproj" />
    <ProjectReference Include="../../src/NT.AvaloniaControl.TreeDataGrid.Themes/NT.AvaloniaControl.TreeDataGrid.Themes.csproj" />
  </ItemGroup>
</Project>
```

已有项目也可以使用本仓库打包生成的 NuGet 包。包消费验证入口为 [Test-PackageConsumer.ps1](../scripts/Test-PackageConsumer.ps1)，不应假设当前预览包已经发布到公共 NuGet 源。仓库外项目需自行设置 `TargetFramework`、可空引用类型和 Avalonia 包版本。

本仓库在 `Directory.Packages.props` 中统一管理 `CommunityToolkit.Mvvm` `8.4.2`；未使用集中版本管理的外部应用，在项目的 `ItemGroup` 中添加：

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
```

该依赖用于应用的模型和 ViewModel。以下示例使用 Toolkit 的 `ObservableObject`、`[ObservableProperty]`、`[RelayCommand]` 和 `[NotifyCanExecuteChangedFor]`；包含生成属性或命令的类型须声明为 `partial`。控件库本身无需依赖 Toolkit。

应用先加载 Avalonia Fluent 主题，再加载 `TreeDataGridTheme`。下文的 `App.cs` 已完成这一步。如果应用已有 `App.axaml`，也可把以下内容加入原有 `Application.Styles`，两种主题加载方式选一种即可：

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://NT.AvaloniaControl.TreeDataGrid.Themes/Themes/Generic.axaml" />
</Application.Styles>
```

XAML 使用以下命名空间：

```xml
xmlns:tg="clr-namespace:NT.AvaloniaControl.TreeDataGrid;assembly=NT.AvaloniaControl.TreeDataGrid"
```

数据入口选择：

| 使用场景 | 推荐入口 | 数据源所有权 |
| --- | --- | --- |
| 常规集合绑定，列在 XAML 中声明 | `ItemsSource="{Binding Items}"` | 控件自动创建、替换和释放适配 Source |
| ViewModel 需要排序、过滤、展开命令或自定义树委托 | `Source="{Binding Source}"` | ViewModel / 应用创建并负责 `Dispose()` |

`ItemsSource` 与显式 `Source` 互斥。切换入口时先清空当前入口。`ItemsSource` 模式下仍可读取控件的 `Source`，它是自动生成的适配源；不要再赋入外部 Source。

## 完整 MVVM 示例：ItemsSource

下面的六个文件组成一个最小桌面应用，其源码可直接在 [samples/TreeDataGrid.Usage](../samples/TreeDataGrid.Usage/TreeDataGrid.Usage.csproj) 查看和运行。模型和 ViewModel 使用 `CommunityToolkit.Mvvm`；ViewModel 不保存控件引用，通过可观察集合、生成的属性与命令和 `TreeDataGridEditCommands` 与 View 交互。

### 1. 模型、ViewModel 和命令

[PeopleViewModel.cs](../samples/TreeDataGrid.Usage/PeopleViewModel.cs)：

```csharp
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Usage;

public sealed partial class Person : ObservableObject
{
    private string _name = "";
    private int _quantity;

    [ObservableProperty]
    private bool? _enabled = true;

    public string Name
    {
        get => _name;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("名称不能为空。");
            SetProperty(ref _name, value);
        }
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (value is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(value), "数量须在 0–100 之间。");
            SetProperty(ref _quantity, value);
        }
    }
}

public sealed partial class PeopleViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private Person? _selectedItem;

    [ObservableProperty]
    private CellAddress? _currentCell;

    [ObservableProperty]
    private IndexPath _selectedIndex;

    private int _nextNumber = 3;

    public ObservableCollection<Person> Items { get; } = new()
    {
        new Person { Name = "记录 A", Quantity = 10, Enabled = true },
        new Person { Name = "记录 B", Quantity = 20, Enabled = null },
    };

    // 可变集合本身的增删会双向同步；不需要替换集合实例。
    public ObservableCollection<Person> SelectedItems { get; } = new();
    public TreeDataGridEditCommands Editing { get; } = new();

    [RelayCommand]
    private void Add()
    {
        Items.Add(new Person
        {
            Name = $"新记录 {_nextNumber++}", Quantity = 1, Enabled = true,
        });
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        if (SelectedItem is { } item) Items.Remove(item);
    }

    private bool CanRemove() => SelectedItem is not null;
}
```

`[ObservableProperty]` 为字段生成公开属性，例如 `_enabled` 生成 `Enabled`，并通过 `ObservableObject` 提供属性通知。`[RelayCommand]` 为 `Add()` / `Remove()` 生成 `AddCommand` / `RemoveCommand`，View 可直接绑定；`SelectedItem` 变化时，`[NotifyCanExecuteChangedFor]` 会刷新删除命令的 `CanExecute`，按钮同步启用或禁用。

`ObservableCollection<T>` 通知集合增删移动；`ObservableObject` 实现 `INotifyPropertyChanged`，通知行模型属性修改。需要校验的 `Name` / `Quantity` 保留自定义 setter，验证通过后调用 `SetProperty`，失败时保留原模型值，异常由编辑事务转换为 `ValidationError`。来自后台任务的数据应先切回 UI 线程，再更新集合和模型。

### 2. View

[MainWindow.axaml](../samples/TreeDataGrid.Usage/MainWindow.axaml)：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:TreeDataGrid.Usage"
        xmlns:tg="clr-namespace:NT.AvaloniaControl.TreeDataGrid;assembly=NT.AvaloniaControl.TreeDataGrid"
        x:Class="TreeDataGrid.Usage.MainWindow"
        x:DataType="vm:PeopleViewModel"
        x:CompileBindings="True"
        Title="TreeDataGrid MVVM" Width="900" Height="560">
  <Grid Margin="16" RowDefinitions="Auto,*,Auto,Auto" RowSpacing="12">
    <StackPanel Orientation="Horizontal" Spacing="8">
      <Button Content="增加" Command="{Binding AddCommand}" />
      <Button Content="删除选中项" Command="{Binding RemoveCommand}" />
      <Button Content="开始编辑" Command="{Binding Editing.BeginEditCommand}" />
      <Button Content="提交" Command="{Binding Editing.CommitEditCommand}" />
      <Button Content="取消" Command="{Binding Editing.CancelEditCommand}" />
    </StackPanel>

    <tg:TreeDataGrid Grid.Row="1" ItemsSource="{Binding Items}"
                     SelectionUnit="Row" SelectionMode="Multiple"
                     SelectedItem="{Binding SelectedItem, Mode=TwoWay}"
                     SelectedIndex="{Binding SelectedIndex, Mode=TwoWay}"
                     SelectedItems="{Binding SelectedItems, Mode=TwoWay}"
                     CurrentCell="{Binding CurrentCell, Mode=TwoWay}"
                     EditCommands="{Binding Editing}"
                     FrozenColumnCount="1" RowHeight="32"
                     IsThreeStateSort="True">
      <tg:TreeDataGridRowHeaderColumn Header="#" Width="48" IsReadOnly="True" />
      <tg:TreeDataGridTextColumn Header="名称" Width="2*" MinWidth="120"
                                 Binding="{ReflectionBinding Name}" />
      <tg:TreeDataGridTextColumn Header="数量" Width="100"
                                 Binding="{ReflectionBinding Quantity}" />
      <tg:TreeDataGridCheckBoxColumn Header="启用" Width="90" IsThreeState="True"
                                     Binding="{ReflectionBinding Enabled}" />
    </tg:TreeDataGrid>

    <StackPanel Grid.Row="2" Orientation="Horizontal" Spacing="8">
      <TextBlock Text="编辑草稿：" VerticalAlignment="Center" />
      <TextBox Width="260" IsEnabled="{Binding Editing.IsEditing}"
               Text="{Binding Editing.EditSession.Value, Mode=TwoWay}" />
      <TextBlock Text="{Binding Editing.EditSession.ValidationError}"
                 Foreground="Red" VerticalAlignment="Center" />
    </StackPanel>
    <TextBlock Grid.Row="3" Text="先点击名称或数量单元格，再按 F2、双击或点击开始编辑。" />
  </Grid>
</Window>
```

View 的外围绑定使用 compiled bindings。列上的 `ReflectionBinding` 以当前行模型为数据上下文，并允许控件推导编辑写回；列对象本身是普通元数据对象，不继承 View 的 `DataContext`。不能仅因显示绑定成功就认为任意绑定都支持自动编辑写回。

[MainWindow.axaml.cs](../samples/TreeDataGrid.Usage/MainWindow.axaml.cs)：

```csharp
using Avalonia.Controls;

namespace TreeDataGrid.Usage;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
```

### 3. 应用组合入口

[App.cs](../samples/TreeDataGrid.Usage/App.cs)：

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.TreeDataGrid.Themes;

namespace TreeDataGrid.Usage;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new TreeDataGridTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow { DataContext = new PeopleViewModel() };
            // 宿主最终关闭时解除绑定，使自动适配源不再订阅模型。
            window.Closed += (_, _) => window.DataContext = null;
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

[Program.cs](../samples/TreeDataGrid.Usage/Program.cs)：

```csharp
using System;
using Avalonia;

namespace TreeDataGrid.Usage;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}
```

在仓库根目录直接运行已保存的示例项目（首次运行会自动还原并构建）：

```powershell
dotnet run --project samples/TreeDataGrid.Usage
```

使用仓库根目录的锁定还原与 Release 构建流程时，该项目会随解决方案一起构建，之后可运行 `dotnet run --project samples/TreeDataGrid.Usage -c Release --no-build`。复制到仓库外时，创建上述目录与文件，并补齐目标框架、Avalonia 包版本和两个控件程序集的引用。

本例集中展示集合绑定、增删、多选、当前单元格、暂存编辑与验证和列头排序。需要树展开、排序过滤和其他组合场景时，可运行统一 Demo 的 TreeDataGrid 页面；`--mvvm` 直接选中该页面的 MVVM Tab，普通模式的十个场景位于另一个 Tab：

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -- --mvvm
```

操作验证：增加和删除会即时更新表格；修改数量草稿为 `200` 后提交会显示错误，模型值保持不变；改为 `42` 后提交成功；再次修改后取消会保留上次已提交的值。点击列头可切换升序、降序和原顺序。

本文的完整应用、显式 Source 与树形模型示例，以及三组对应的强类型 XAML 绑定，已从文档提取到临时项目完成 Release 编译，结果为 **0 警告 / 0 错误**。

## 选择、当前单元格与编辑

`SelectedItem`、`SelectedIndex`、`SelectedItems` 和 `CurrentCell` 默认都是 `TwoWay`。上例显式写出绑定模式，便于阅读。

| 属性 | ViewModel 中的用法 | 语义 |
| --- | --- | --- |
| `SelectedItem` | `SelectedItem = Items[0];` | 按模型对象引用选择；不会自动指定当前列 |
| `SelectedIndex` | `SelectedIndex = new IndexPath(0);` | 按原集合的零开始路径选择；空路径 `default(IndexPath)` 清空选择 |
| `SelectedItems` | 向 `SelectedItems` 增删模型对象 | 多选集合同步；搭配 `SelectionMode="Multiple"` |
| `CurrentCell` | 设置或保存 `CellAddress?` | 当前节点和列的身份，独立于行选择 |

树路径 `new IndexPath(0, 2)` 表示第一个根节点的第三个孩子。排序、过滤和折叠会改变显示行号，但不会把 `SelectedIndex` 改为显示索引。模型集合移动后源路径也可能改变；需要跨投影保持目标时使用 `NodeId`。同一模型对象在树中出现多次时，使用路径区分具体出现位置。

可写 `IList` 的实例会保留并同步。只读或定长列表可能回写新的集合快照，因此需要可写的绑定属性；上例的可变 `ObservableCollection<Person>` 可以使用只读属性。集合通知栈中发生选择规范化时，集合回写可能延后到 UI 调度；不要假定所有回写都在 `CollectionChanged` 回调返回前结束。

编辑命令通过 `EditCommands="{Binding Editing}"` 连接到表格：

| 命令 / 状态 | 功能 |
| --- | --- |
| `Editing.BeginEditCommand` | 无参数时编辑当前单元格；可传 `CellAddress` 为命令参数 |
| `Editing.CommitEditCommand` | 转换、验证并写入模型，失败保留草稿和会话 |
| `Editing.CancelEditCommand` | 取消草稿，不调用模型写回器 |
| `Editing.IsEditing` | 当前是否存在编辑会话，只读 |
| `Editing.EditSession` | 当前编辑会话，无编辑时为 `null` |
| `EditSession.Cell` / `OriginalValue` | 编辑地址 / 开始编辑时的原始值，只读 |
| `EditSession.Value` | 可写草稿，可与表格内编辑器或外部面板双向绑定 |
| `EditSession.ValidationError` | 最近一次转换、验证或写回错误，只读 |

这些对象实现 `INotifyPropertyChanged`；命令会更新 `CanExecute`。每个 `TreeDataGridEditCommands` 实例同时连接一个表格，多表格应各自创建实例。表格卸载时断开命令连接，重新挂载后恢复。`IsReadOnly=true` 会取消活动编辑。

## MVVM 中使用显式 Source

需要在 ViewModel 内调用过滤、排序或展开接口时，创建显式 Source。下面复用前例的 `Person`，属性和过滤命令继续由 `CommunityToolkit.Mvvm` 生成：

`PeopleSourceViewModel.cs`：

```csharp
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Usage;

public sealed partial class PeopleSourceViewModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private Person? _selectedItem;

    [ObservableProperty]
    private CellAddress? _currentCell;

    public PeopleSourceViewModel() => Source = new FlatTreeDataGridSource<Person>(Items);

    public ObservableCollection<Person> Items { get; } = new()
    {
        new Person { Name = "启用记录", Quantity = 10, Enabled = true },
        new Person { Name = "停用记录", Quantity = 20, Enabled = false },
    };
    public FlatTreeDataGridSource<Person> Source { get; }
    public TreeDataGridEditCommands Editing { get; } = new();

    [RelayCommand]
    private void ShowEnabled() => Source.SetFilter(model => ((Person)model).Enabled == true);

    [RelayCommand]
    private void ShowAll() => Source.SetFilter(null);

    public void Dispose() => Source.Dispose();
}
```

View 声明 `x:DataType="vm:PeopleSourceViewModel"`，使用以下表格和按钮即可。列仍在控件的 `Columns` 中，Source 不保存列集合：

```xml
<StackPanel Spacing="8">
  <StackPanel Orientation="Horizontal" Spacing="8">
    <Button Content="仅启用" Command="{Binding ShowEnabledCommand}" />
    <Button Content="全部" Command="{Binding ShowAllCommand}" />
  </StackPanel>
  <tg:TreeDataGrid Height="320" Source="{Binding Source}"
                   SelectedItem="{Binding SelectedItem, Mode=TwoWay}"
                   CurrentCell="{Binding CurrentCell, Mode=TwoWay}"
                   EditCommands="{Binding Editing}">
    <tg:TreeDataGridTextColumn Header="名称" Width="240"
                               Binding="{ReflectionBinding Name}" />
    <tg:TreeDataGridTextColumn Header="数量" Width="100"
                               Binding="{ReflectionBinding Quantity}" />
  </tg:TreeDataGrid>
</StackPanel>
```

应用组合入口负责设置 ViewModel，并在窗口最终关闭时先解除绑定，再释放它拥有的源；不要在 Tab 临时卸载时释放：

```csharp
// window 为使用 PeopleSourceViewModel 的 Window。
var viewModel = new PeopleSourceViewModel();
window.DataContext = viewModel;
window.Closed += (_, _) =>
{
    window.DataContext = null;
    viewModel.Dispose();
};
```

Source 的访问、模型通知、集合通知和 `Dispose()` 都须在源的创建线程执行，桌面应用通常为 UI 线程。更换 Source 后旧 NodeId 不再用于新源，选择和当前单元格应重新设置。

## 树形数据与展开绑定

### ItemsSource 自动树适配

模型继承 Toolkit 的 `ObservableObject`，通过 `[ObservableProperty]` 生成名称和展开属性，并提供孩子集合：

`TreeViewModel.cs`：

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TreeDataGrid.Usage;

public sealed partial class TreeNode : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<TreeNode> Children { get; } = new();
}

public sealed class TreeViewModel : ObservableObject
{
    public TreeViewModel()
    {
        var root = new TreeNode { Name = "研发部", IsExpanded = true };
        root.Children.Add(new TreeNode { Name = "客户端组" });
        root.Children.Add(new TreeNode { Name = "服务端组" });
        Roots.Add(root);
    }

    public ObservableCollection<TreeNode> Roots { get; } = new();
}
```

View 设置 `x:DataType="vm:TreeViewModel"`、`DataContext = new TreeViewModel()`，列声明如下：

```xml
<tg:TreeDataGrid Height="320" ItemsSource="{Binding Roots}">
  <tg:TreeDataGridHierarchicalExpanderColumn Header="部门" Width="*" Indent="20"
      ChildrenBinding="{ReflectionBinding Children}"
      IsExpandedBinding="{ReflectionBinding IsExpanded, Mode=TwoWay}">
    <tg:TreeDataGridTextColumn Binding="{ReflectionBinding Name}" />
  </tg:TreeDataGridHierarchicalExpanderColumn>
</tg:TreeDataGrid>
```

外层展开列控制表头、宽度和缩进，内层列控制单元格显示及编辑。`InnerColumn` 也可以是复选框或模板列，不能再嵌套展开列。

`ChildrenBinding` 返回 `IEnumerable` 或 `null`。可选 `HasChildrenBinding` 指向直接 `bool` 属性，用于提示尚未加载的孩子；实际孩子非空或提示为 true，都会显示展开器。`IsExpandedBinding` 可选：绑定后模型属性与展开操作双向同步，同一模型对象的多个出现位置共享展开状态；不绑定时由 Source 保存各出现位置的独立状态。

自动层级绑定的当前限制：

- 同时仅一列可定义层级数据绑定；设置 `HasChildrenBinding` 或 `IsExpandedBinding` 时必须同时设置 `ChildrenBinding`。
- 三种绑定均须为直接模型属性的 `ReflectionBinding`，不能使用 compiled binding、点分路径、索引器、转换器、外部 Source、`ElementName` 或 `RelativeSource`，也不能设置格式、回退值或目标空值。
- `ChildrenBinding` / `HasChildrenBinding` 的 Mode 为 `Default` 或 `OneWay`；`IsExpandedBinding` 为 `Default` 或 `TwoWay`，对应属性必须是公开可读写的 `bool`。
- 子集合增删通过集合通知同步；替换子集合须发出属性通知。没有通知时调用适配 Source 的 `Refresh()`。

### 显式树 Source、展开命令和同步按需加载

复杂模型可以使用委托描述层级，无需满足上述绑定路径限制。以下代码使用前例的 `TreeNode`，由应用创建并管理 Source：

```csharp
var tree = new TreeViewModel();
var source = new HierarchicalTreeDataGridSource<TreeNode>(
    tree.Roots,
    node => node.Children,
    new HierarchicalTreeDataGridSourceOptions<TreeNode>
    {
        IsExpandedSelector = node => node.IsExpanded,
        IsExpandedWriter = (node, expanded) => node.IsExpanded = expanded,
    });

source.SetExpandedAt(new IndexPath(0), true);
source.ExpandAll();
source.CollapseAll();
source.SetExpandedRecursive(true, row => row.Depth < 2);

// 宿主最终解除绑定后调用 source.Dispose()。
```

绑定 `Source` 时，展开列只需包含 `InnerColumn`，孩子和展开状态由 Source 委托提供。`HasChildrenSelector` 可预告未加载的孩子，`RowExpanding` 可在展开前同步填充集合，或者设置 `e.Cancel = true` 取消。以下是额外加载逻辑的接入示意，`LoadChildrenSynchronously` 由应用实现：

```csharp
source.RowExpanding += (_, e) =>
{
    var node = (TreeNode)e.Row.Model;
    // 应用须额外记录是否加载过，避免每次展开重复加载。
    // foreach (var child in LoadChildrenSynchronously(node)) node.Children.Add(child);
};
```

当前按需加载为同步机制。远程加载应由应用异步获取结果，再回到创建线程更新模型；控件没有内置异步 loader、加载占位行或取消令牌接口。不要在展开事件中重入调用展开命令。

## 自定义列模板与编辑验证

显示模板默认接收行模型，`ValueGetter` 可改变模板 Content。编辑模板接收 **`TreeDataGridEditSession`**，应双向绑定 `Value`，这样提交和取消仍经过表格事务。以下代码列可加入使用 `Person` 模型的表格：

```csharp
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using NT.AvaloniaControl.TreeDataGrid;

var quantityColumn = new TreeDataGridTemplateColumn
{
    Header = "自定义数量", Width = new GridLength(150),
    ValueGetter = model => ((Person)model).Quantity,
    ValueSetter = (model, value) => ((Person)model).Quantity = (int)value!,
    ValueType = typeof(int),
    ValueValidator = value => value is int quantity && quantity is >= 0 and <= 100
        ? null : "数量须在 0–100 之间。",
    CellTemplate = new FuncDataTemplate<int>((value, _) =>
        new TextBlock { Text = $"数量：{value}" }),
    CellEditingTemplate = new FuncDataTemplate<TreeDataGridEditSession>((session, _) =>
    {
        var editor = new TextBox { DataContext = session };
        editor.Bind(TextBox.TextProperty, new ReflectionBinding(nameof(TreeDataGridEditSession.Value))
        {
            Mode = BindingMode.TwoWay,
        });
        return editor;
    }),
};
```

在 View 初始化或应用组合层调用 `grid.Columns.Add(quantityColumn)`，将此列加入对应表格。文本和复选框列的简单 `ReflectionBinding` 可自动推导写回器；显式 `ValueSetter` 优先。编译绑定、转换后的显示值、复杂绑定或模板列应显式提供 `ValueGetter` / `ValueSetter`，并按需配置 `ValueType` 或 `ValueConverter`。

提交顺序是：草稿 → 类型转换 → `ValueValidator` → 模型写回。`ValueValidator` 返回 `null` 表示通过，字符串表示错误。`ValueConverter` 用于编辑值转换，区别于 Avalonia Binding 的显示转换器；未设置时按目标属性类型或 `ValueType`、原始值类型进行当前文化下的转换。

不要让编辑模板直接双向绑定 `Person.Quantity`，那会立即修改模型并绕过取消。模型 setter 也须在修改前验证；应用写回器如果先改数据再抛异常，控件不能自动回滚任意副作用。

## 排序、过滤与批量更新

列头用户排序需要表格 `CanUserSortColumns=true` 和列 `CanUserSort=true`。默认升序、降序切换；`IsThreeStateSort=true` 时增加清除排序。模板列和行头列默认不参与用户排序，可显式开启并提供比较器。

ViewModel 可以直接通过显式 Source 排序和过滤。以下以 `PeopleSourceViewModel` 为例，`ColumnId` 用于标识排序配置；真实列头关联需使用对应列的 `Id`：

```csharp
var viewModel = new PeopleSourceViewModel();
var sortKey = new ColumnId(1);
viewModel.Source.Sort(sortKey,
    (left, right) => ((Person)left).Quantity.CompareTo(((Person)right).Quantity),
    TreeDataGridSortDirection.Ascending);
viewModel.Source.SetFilter(model => ((Person)model).Enabled == true);

using (viewModel.Source.DeferRefresh())
{
    viewModel.Items.Add(new Person { Name = "批量记录 A", Quantity = 5 });
    viewModel.Items.Add(new Person { Name = "批量记录 B", Quantity = 15 });
}

viewModel.Source.ClearSort();
viewModel.Source.SetFilter(null);
// 完成使用并解除 View 绑定后 viewModel.Dispose()。
```

排序稳定且只重排同级节点，原集合顺序、源路径和 NodeId 不因排序变化。过滤逐节点判断；树的父节点不匹配会隐藏整支分支，不自动保留匹配孩子的祖先。比较器和谓词须为无副作用的同步函数。闭包条件改变后调用 `Refresh()` 重新应用。

列头或 `grid.SortByColumn(...)` 排序会先尝试提交当前编辑；验证失败时返回 `false`，保留草稿和原排序。直接 `Source.Sort(...)` 不执行此步骤。过滤隐藏编辑目标时取消编辑；隐藏行选择仍按身份保留，过滤清除后可恢复。

`DeferRefresh()` 合并投影发布，最外层作用域退出时发布最终结果；期间仍维护模型订阅，并非暂停所有结构计算。`IsRefreshDeferred=true` 时不要开始或提交编辑。

## 按节点身份或源路径定位

View 层可使用 `grid.TryScrollIntoView(...)`，默认只纵向滚动，不改变选择、当前单元格或编辑草稿。`IndexPath` 是源集合中从零开始的路径；排序后的显示行号不能作为该路径传入。

```csharp
// 已展开且未被过滤的节点：只定位，不改变选择。
bool shown = grid.TryScrollIntoView(nodeId);
// 展开源路径的祖先，然后选择目标；保留当前可见列，没有当前列时用首个可见列。
bool selected = grid.TryScrollIntoView(new IndexPath(0, 2), expandAncestors: true, select: true);
```

缺失、被过滤、默认不展开的折叠节点、延迟刷新中的投影或无可用的已附着视口返回 `false`。应用在布局完成后调用；已有视口中的待更新几何会同步校正。所有列隐藏时可只定位行，`select=true` 返回 `false`。选择其它单元格前尝试提交活动编辑，校验失败时拒绝；普通滚动保留同一会话。展开、提交或滚动回调改变 Source、列契约、目标身份/路径或当前版本后取消剩余操作，避免用过期路径选中其它行。已经成功的祖先展开不因后续失败回滚；业务回调异常遵循既有通知契约。

## 受控行与树节点移动

设置 `CanUserMoveRows=true` 后，可从行号列单元格拖动。目标行上部/下部分别插入到前面/后面，中部移入其子集合；Esc 或捕获丢失取消。应用也可调用 `TryMoveRow(node, target, position)` 走相同的请求、编辑和选择策略。

```csharp
grid.CanUserMoveRows = true;
grid.RowMoveRequested += (_, e) =>
{
    // 权限或业务条件拒绝时，Cancel=true；集合还没有移动。
    if (!CanMoveThisNode(e.Row.Model, e.Target.Model)) e.Cancel = true;
};
TreeDataGridRowMoveResult result = grid.TryMoveRow(nodeId, targetId, TreeDataGridRowDropPosition.Inside);
if (result.IsCommitted)
{
    // 同父节点保留身份；跨父以结果 NodeId 为准，不能继续使用旧身份。
    NodeId? currentIdentity = result.NodeId;
}
```

默认集合适配范围如下：

| 场景 | 默认适配 | 身份与一致性 |
| --- | --- | --- |
| 同父 | 精确 `ObservableCollection<T>` 或 `TreeDataGridRowMoveCollection<T>` | 使用精确索引 Move，重复模型不同出现位置仍保留各自身份 |
| 跨父 | 两边都是 sealed `TreeDataGridRowMoveCollection<T>` | 所有分配/验证在提交前；同时提交两边底层存储，然后发送 Remove/Add 通知；跨父产生的新身份由结果给出 |
| 普通可写 IList、只读集合、ObservableCollection 子类、跨父普通 ObservableCollection | `Unsupported` | 默认不修改；由应用通过请求处理自己的事务 |

`TreeDataGridRowMoveCollection<T>` 实现 `IList<T>` / `IReadOnlyList<T>`、INCC 和 INPC，访问与通知使用创建线程，通知内禁止再次修改这两个事务集合。其它观察者抛错仍执行余下处理器；如果存储已提交，结果为 `CommittedWithObserverError`，错误位于 `Error`，不能按普通失败重试同一移动。默认适配不承诺回滚业务观察者额外产生的副作用。排序或过滤存在时默认移动返回 `Rejected`，也拒绝自身、后代、循环模型目标、无变化落点及超过最大层级的移动。

批量导入可使用 `collection.AddRange(additions)`。方法先完整枚举、检查 null 项并准备新存储，再一次提交并发送带起始索引与全部新增项的 Add 通知；空范围无通知。枚举/验证失败不修改集合，枚举期间不能重入修改该集合。通知异常发生在提交之后，剩余监听者仍执行，不能据异常把同一批数据再次追加。旧节点身份、选择、暂存草稿与订阅由范围通知保留；`AddRange` 不改变旧 `DeferRefresh` 的投影合并语义。

受控集合的存储已提交而通知尚未完成时，`Source.IsRefreshDeferred` 为 true，编辑须等待；此时的显式 `Refresh` 会先排队，待精确索引通知与完成通知后处理，避免预先重建新范围再重复应用 Add。该握手仅用于此受控集合及其 ItemsSource 适配，不宣称可以检测任意外部 ObservableCollection 的预通知阶段。

控件在请求前尝试提交活动编辑；校验失败为 `ValidationFailed`。成功提交后、请求返回后都重新验证 Source、版本、路径与模型身份；在途失效为 `Stale`。`RowMoveRequested` 可取消；应用若设 `Handled=true`，必须给出自己的 `Result` 并承担写入一致性，不能只凭集合可写就采用 Remove+Insert。移动本身已经提交时，通知错误与提交结果分别记录。`Source.MoveRow` 是纯模型适配入口，直接调用不会处理控件的草稿、选择与焦点。

## 属性、方法与事件参考

下面列出本控件新增的常用公共接口；Avalonia 基类的 `DataContext`、字体、尺寸和布局等属性按其原有契约使用。精确类型和完整公开成员可查 [public-api.txt](public-api.txt)。

### TreeDataGrid 属性

| 属性 | 类型 | 默认值 | 功能 / 约束 |
| --- | --- | --- | --- |
| `ItemsSource` | `IEnumerable?` | `null` | 集合绑定入口，自动创建适配 Source；与显式 Source 互斥 |
| `Source` | `TreeDataGridSource?` | `null` | 显式源入口；应用拥有显式源 |
| `Columns` | `ObservableCollection<TreeDataGridColumn>` | 空集合 | 只读集合属性，支持 XAML 直接子列；同一列实例不能重复加入 |
| `RowHeight` | `double` | `28` | 基础行高，有限且大于 0，单位 DIP |
| `RowHeightSelector` | `Func<TreeDataGridRow, double>?` | `null` | 自定义每行高度；无效返回值回退到基础行高 |
| `AutoRowHeight` | `bool` | `false` | 按已实现行测量内容；最终高度为行高选择结果与实测内容高度的较大值，选择器未提供或返回无效值时使用 RowHeight |
| `FrozenColumnCount` | `int` | `0` | 冻结左侧列，非负，布局时不超过列数 |
| `CanUserResizeColumns` | `bool` | `true` | 用户拖动调宽总开关，还需列允许调宽 |
| `CanUserMoveRows` | `bool` | `false` | 行号单元格拖动及控件移动请求总开关 |
| `CanUserSortColumns` | `bool` | `true` | 用户列头排序总开关，不限制显式程序排序 |
| `IsThreeStateSort` | `bool` | `false` | 用户排序循环包含未排序状态 |
| `SelectionUnit` | `TreeDataGridSelectionUnit` | `Row` | `Row` / `Cell`，切换时清空选择 |
| `SelectionMode` | `TreeDataGridSelectionMode` | `Single` | `Single` / `Multiple`，切换时清空选择 |
| `SelectedItem` | `object?` | `null` | 所选模型，默认 TwoWay，按引用匹配 |
| `SelectedIndex` | `IndexPath` | 空路径 | 原集合路径，默认 TwoWay |
| `SelectedItems` | `IList?` | `null` | 多选模型列表，默认 TwoWay |
| `CurrentCell` | `CellAddress?` | `null` | 当前单元格身份，默认 TwoWay |
| `IsReadOnly` | `bool` | `false` | 表格级只读，启用时取消活动编辑 |
| `EditTriggers` | `TreeDataGridEditTriggers` | `F2 \| DoubleClick` | 标志枚举，还支持 `None`、`SingleClick` |
| `EditCommands` | `TreeDataGridEditCommands?` | `null` | 绑定 ViewModel 编辑命令端点 |
| `IsEditing` / `EditSession` | `bool` / `TreeDataGridEditSession?` | `false` / `null` | 只读编辑状态，也可从命令端点读取 |
| `BeginEditCommand` / `CommitEditCommand` / `CancelEditCommand` | `ICommand` | 自动创建 | 只读控件命令，纯 MVVM 优先使用端点 |
| `CellTheme` / `ColumnHeaderTheme` | `ControlTheme?` | `null` | 自定义单元格 / 列头主题 |
| `State` | `TreeDataGridState?` | 无源时 `null` | 控件拥有的选择编辑状态，只读 |
| `RowsPresenter` | `TreeDataGridRowsPresenter?` | 模板应用前 `null` | 只读呈现器，布局诊断或 View 层定位入口 |

`ItemsSource` 和只读编辑输出使用 Avalonia DirectProperty；其余表中的可绑定标量入口使用 StyledProperty。`Columns`、`State` 和 `RowsPresenter` 是普通 CLR 属性。

### TreeDataGrid 方法与事件

| 接口 | 功能 / 返回值 |
| --- | --- |
| `bool BeginEdit(CellAddress cell)` | 编辑可见节点的可写列；目标无效或不能编辑返回 false |
| `bool CommitEdit()` | 成功提交或无活动编辑返回 true；转换 / 验证失败返回 false |
| `void CancelEdit()` | 放弃草稿 |
| `bool SortByColumn(ColumnId columnId, TreeDataGridSortDirection direction)` | 按列比较器或读值排序；`None` 清除；不存在的列会抛参数异常 |
| `CellAddress? SearchText(string text)` | 在可见行中前缀搜索并选择、滚动到结果，未匹配返回 null；文本列默认参与 |
| `bool TryScrollIntoView(NodeId node, bool expandAncestors = false, bool select = false)` | 按节点身份定位；可选展开祖先/选择，失效或无可用视口返回 false |
| `bool TryScrollIntoView(IndexPath sourcePath, bool expandAncestors = false, bool select = false)` | 先解析源集合路径，再按身份定位；路径不表示排序后的显示行号 |
| `TreeDataGridRowMoveResult TryMoveRow(NodeId node, NodeId target, TreeDataGridRowDropPosition position)` | 先提交草稿，发送请求，再执行受控默认或应用事务 |
| `RowMoveRequested` / `RowMoveCompleted` | 可取消/由应用处理的请求，以及最终移动结果通知 |
| `SelectionChanged` | 选择变化事件 |
| `RowPrepared` | 虚拟行容器准备完成 |
| `RowClearing` | 行回收前、移除绑定和 DataContext 前触发 |

行事件参数 `TreeDataGridRowEventArgs` 提供 `Container`、`Row`、`RowIndex`。`RowIndex` 是当前可见行索引；容器会回收，不应将它作为模型的长期身份。

### 列公共属性

列基类 `TreeDataGridColumn` 实现 `INotifyPropertyChanged`，不属于 Avalonia 视觉树。除只读 `Id` 外，下列属性均可设置。

| 属性 | 类型 | 默认值 | 功能 |
| --- | --- | --- | --- |
| `Id` | `ColumnId` | 自动分配 | 稳定列身份，不是显示列索引 |
| `Header` | `object?` | `null` | 列头内容 |
| `Width` | `GridLength` | `120` | 固定宽、`Auto` 或星号宽，如 `2*` |
| `MinWidth` | `double` | `24` | 有限非负，不能大于 MaxWidth |
| `MaxWidth` | `double` | 正无穷 | 不能为 NaN 或小于 MinWidth |
| `EstimatedAutoWidth` | `double` | `120` | Auto 列尚未测量时的宽度估计，有限非负 |
| `CanUserResize` | `bool` | `true` | 列级用户调宽开关 |
| `CanUserSort` | `bool` | `true` | 列级用户排序开关；模板 / 行头列默认为 false |
| `Comparison` | `Comparison<object>?` | `null` | 比较模型对象；未设置时比较列读值 |
| `ValueGetter` | `Func<object, object?>?` | `null` | 读取模型值；没有绑定或 getter 时使用模型本身 |
| `ValueSetter` | `Action<object, object?>?` | `null` | 显式写回器，优先于自动推导 |
| `ValueType` | `Type?` | `null` | 编辑目标类型 |
| `ValueConverter` | `Func<object?, object?>?` | `null` | 自定义编辑值转换 |
| `ValueValidator` | `Func<object?, string?>?` | `null` | 验证转换后的值，null 通过、字符串报错 |
| `TextSearchValueSelector` | `Func<object, string?>?` | `null` | 自定义搜索文字，使其他列也可参与搜索 |
| `IsReadOnly` | `bool` | `false` | 列级只读；表格和有效内层列均须允许编辑 |

### 五种列的专用属性

| 列类型 | 专用属性 | 说明 |
| --- | --- | --- |
| `TreeDataGridTextColumn` | `Binding: BindingBase?`、`StringFormat: string?`、`TextWrapping` | Binding 优先于 ValueGetter；默认不换行 |
| `TreeDataGridCheckBoxColumn` | `Binding: BindingBase?`、`IsThreeState: bool` | 三态默认 false；三态模型一般使用 bool? |
| `TreeDataGridTemplateColumn` | `CellTemplate: IDataTemplate?`、`CellEditingTemplate: IDataTemplate?` | 显示模板接收 Content，编辑模板接收编辑会话 |
| `TreeDataGridRowHeaderColumn` | 使用基类属性 | 默认显示从 1 开始的当前可见行号，ValueGetter 可覆盖 |
| `TreeDataGridHierarchicalExpanderColumn` | `InnerColumn: TreeDataGridColumn`、`Indent: double`、`ChildrenBinding`、`HasChildrenBinding`、`IsExpandedBinding` | 默认内层文本列，Indent=16，有限非负；层级绑定为 BindingBase? |

展开列的编辑 getter / setter / converter / validator 和文本搜索属性应放在 `InnerColumn` 上；外层与内层 `IsReadOnly` 都生效。排序比较器可以放在外层，未提供时使用内层比较器。

### Source 接口

`FlatTreeDataGridSource<TModel>(IEnumerable<TModel> items)` 创建平面源；`HierarchicalTreeDataGridSource<TModel>(items, childrenSelector[, options])` 创建树源，`TModel : class`。所有 Source 访问及通知遵循创建线程约束。

| 属性 / 方法 | 功能 |
| --- | --- |
| `Rows: IReadOnlyList<TreeDataGridRow>` | 当前可见行快照，折叠或过滤的隐藏节点不在其中 |
| `Version: long` | 成功发布投影后递增 |
| `IsRefreshDeferred: bool` | 更新延迟或失败待恢复时为 true |
| `SortColumn: ColumnId?` / `SortDirection` / `Filter: Func<object, bool>?` | 最近成功发布的排序和过滤配置，只读 |
| `TryGetRow(NodeId id, out TreeDataGridRow? row)` | 按身份查行，可解析隐藏节点 |
| `TryGetNodeId(IndexPath modelIndex, out NodeId id)` | 按原集合路径查节点 |
| `GetViewIndex(NodeId id)` | 查询可见行号，隐藏或未知返回 -1 |
| `SetExpanded(NodeId id, bool expanded)` | 单节点展开，返回是否改变 |
| `SetExpandedAt(IndexPath modelIndex, bool expanded)` | 按路径展开，返回是否改变 |
| `ExpandAll()` / `CollapseAll()` | 展开 / 折叠，返回变化节点数 |
| `SetExpandedRecursive(bool expanded, Func<TreeDataGridRow, bool>? predicate = null)` | 条件递归；谓词 false 剪去整支子树，返回变化节点数 |
| `Sort(ColumnId column, Comparison<object> comparison, TreeDataGridSortDirection direction)` | 同级稳定排序，direction 为 Ascending / Descending |
| `ClearSort()` | 清除排序 |
| `SetFilter(Func<object, bool>? predicate)` | 设置同步过滤，null 清除 |
| `Refresh()` | 重建无通知变化，重新应用排序过滤 |
| `DeferRefresh(): IDisposable` | 合并发布作用域，可嵌套 |
| `MoveRow(NodeId node, NodeId target, TreeDataGridRowDropPosition position)` | 纯模型默认适配；基类返回 Unsupported，内置泛型源支持上述集合范围 |
| `Dispose()` | 解除模型 / 集合订阅，不释放模型对象 |

上表排序、过滤与批量展开适用于内置泛型 Source；自定义 Source 基类的默认实现可能不支持这些可选功能。

| Source 事件 | 参数与用途 |
| --- | --- |
| `ProjectionChanged` | `ProjectionChangedEventArgs`：Version、Reason、RemovedNodes；发布成功后通知 |
| `RowExpanding` / `RowCollapsing` | `TreeDataGridRowCancelEventArgs`：Row、可写 Cancel；可取消或同步加载 |
| `RowExpanded` / `RowCollapsed` | `TreeDataGridRowModelEventArgs`：Row；投影提交后通知 |

`ProjectionChangeReason` 包含 `Refresh`、`CollectionChanged`、`ModelChanged`、`ExpansionChanged`、`SortChanged`、`FilterChanged`。`RemovedNodes` 表示实际从模型图移除的身份，不包含仅因折叠或过滤而隐藏的节点。

`HierarchicalTreeDataGridSourceOptions<TModel>` 提供 `HasChildrenSelector`、`IsExpandedSelector` 和 `IsExpandedWriter`。展开 selector / writer 必须成对提供；writer 必须保存请求值，或在未修改模型时抛异常。

### 行与身份类型

| 类型 / 成员 | 含义 |
| --- | --- |
| `IndexPath` | 原始集合中零开始的索引路径；支持 Count、索引器、Append 和枚举 |
| `NodeId` | Source 内具体节点出现位置的稳定身份；同一对象重复出现会有不同身份 |
| `ColumnId` | 列实例的稳定身份 |
| `CellAddress(NodeId Node, ColumnId Column)` | 单元格地址 |
| `TreeDataGridRow.Id` / `Model` / `ModelIndex` | 节点身份 / 模型引用 / 原始路径 |
| `TreeDataGridRow.Depth` / `IsExpanded` / `HasChildren` | 根深度为 0 / 展开状态 / 孩子或加载提示 |

行对象是不可变投影快照，但 `Model` 仍引用可变业务对象。节点移动通常保留身份；Replace / Reset 或重新创建 Source 后不能假定旧身份继续有效。

### State 高级接口

常规 MVVM 优先使用控件绑定属性和命令端点。自定义选择或独立核心逻辑可使用 `TreeDataGridState`；控件的 `State` 由控件管理，不要自行释放或替换。独立创建的 `new TreeDataGridState(source)` 由调用方释放，再释放 Source。

| 接口 | 功能 |
| --- | --- |
| `SelectedNodes` / `SelectedCells` / `SelectionAnchor` | 节点选择、单元格选择和选择锚点，只读 |
| `CurrentCell` / `EditSession` | 当前地址和编辑会话，只读 |
| `SelectionMode` | 独立 State 默认 Multiple；控件会设置为自身的 SelectionMode |
| `Select(NodeId node, bool additive = false)` / `ToggleSelection(NodeId node)` / `ClearSelection()` | 节点选择、切换和清空 |
| `SetCurrentCell(CellAddress? cell)` | 设置当前单元格 |
| `SetColumns(IReadOnlyList<ColumnId> columns)` / `RemoveColumn(ColumnId column)` | 独立 State 的有效列管理；控件拥有的 State 由 Columns 同步 |
| `SelectCell(CellAddress cell, bool extend = false)` | 单元格选择 |
| `SelectCellRange(CellAddress anchor, CellAddress end, IReadOnlyList<ColumnId> columns)` | 单矩形范围选择 |
| `SelectRowRange(NodeId anchor, NodeId end)` | 行范围选择 |
| `DeferSelectionNotifications(): IDisposable` | 合并选择通知 |
| `BeginEdit(CellAddress cell, Func<object, object?> reader, Action<object, object?> writer, Func<object?, string?>? validator = null)` | 独立编辑事务；控件编辑应走控件或端点以应用列转换 |
| `CommitEdit()` / `CommitEdit(object? value)` / `CancelEdit()` | 提交或取消；State 不执行控件的列类型转换，无会话时提交会抛异常 |
| `Changed` / `SelectionChanged` / `Dispose()` | 状态事件和独立 State 的资源释放 |

## 常见问题与当前边界

| 现象 | 检查与处理 |
| --- | --- |
| 表格没有默认外观 | 检查功能 / 主题程序集引用，并加载 FluentTheme 和 TreeDataGridTheme |
| 行数据不刷新 | 集合使用 ObservableCollection，模型继承 Toolkit 的 ObservableObject，并用生成属性或 SetProperty 发出通知；非通知数据修改后调用 Source.Refresh() |
| 点击开始编辑按钮无反应 | 先点击可写单元格；仅设置 SelectedItem 不会选择当前列；检查 IsReadOnly 与写回器 |
| compiled 单元格绑定能显示却不能编辑 | 使用受支持的 ReflectionBinding、ValueSetter 或 SetValueAccessors<TModel,TValue>；compiled 显示不代表自动写回 |
| 树层级绑定抛异常 | 检查公共引用对象属性路径 ReflectionBinding、每层 INPC、绑定模式、Children 类型及 IsExpanded 的 bool setter |
| 更改 SelectedIndex 选中了意外的行 | 它是原集合路径，不是排序后的显示索引；使用 Source 映射接口查询 |
| 模板取消编辑仍然改了模型 | 编辑模板应绑定 EditSession.Value，避免直接 TwoWay 写模型 |
| 多个表格共用编辑端点失败 | 每个表格单独创建 TreeDataGridEditCommands |
| 树过滤后匹配的孩子消失 | 父节点过滤失败会隐藏整支分支；按业务需要在谓词中保留祖先 |
| 自动源在控件卸载后仍订阅集合 | 临时卸载保留源以支持恢复；永久弃用时清空 ItemsSource 或解除其数据绑定 |

为虚拟化提供有限高度的布局区域，例如主例 `Grid` 的 `*` 行；避免把大表格放入无限高度的外层纵向滚动区域。模板控件应将持久状态保存在模型里，行回收后不能依赖旧容器。

当前树模型提前索引全部节点，拒绝 null 模型、祖先循环和超过 256 层的图；每次通知仍可能维护完整图。行列虚拟化不等同于服务端分页或模型的惰性索引。已实现同区列重排、受控同表行移动与专用自动化 peer；多列排序描述集合、自动保留祖先过滤、跨控件移动、真实屏幕阅读器和原生 Grid/Table pattern 支持仍未完成。默认行移动适配通过显式 Source 接入；ItemsSource 的擦除适配器通过 RowMoveRequested 交给应用处理。

用户输入支持鼠标选择，多行模式下 Ctrl+左键切换行选择，Multiple 模式下 Shift 扩展范围、Ctrl+A 全选可见行或单元格；方向键、Home / End、PageUp / PageDown、Tab / Shift+Tab 导航，F2 编辑、Enter 提交后向下、Tab 提交后跨列、Escape 取消，以及文本前缀搜索。复选框单元格可用 Space 循环状态并提交，列头可用 Enter / Space 排序。单元格多选为单矩形范围。中文输入法预编辑、候选窗和麒麟平台的完整验收仍按仓库平台记录执行。

更多可运行样例见 [MVVM Demo](mvvm-demo.md)，精确行为见 [选择编辑契约](p4-interaction.md)、[树模型契约](p3-tree.md)、[排序过滤契约](p5-projection.md)及 [MVVM 入口范围](mvvm-official-alignment.md)。
