param([switch]$NoPack, [ValidateSet('All', 'TreeDataGrid', 'VirtualKeyboard', 'Markdown', 'PdfViewer')][string]$Control = 'All', [string]$DependencyFeed)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    if ($DependencyFeed) {
        if ($Control -ne 'TreeDataGrid') { throw 'DependencyFeed currently supports Control=TreeDataGrid only.' }
        $DependencyFeed = [IO.Path]::GetFullPath($DependencyFeed)
        if (-not (Test-Path -LiteralPath $DependencyFeed -PathType Container)) { throw 'DependencyFeed must be an existing local directory.' }
        if (@(Get-ChildItem -LiteralPath $DependencyFeed -Recurse -Filter 'nt.avaloniacontrol.treedatagrid*.nupkg').Count) { throw 'DependencyFeed must contain dependencies only; product packages come from artifacts/packages.' }
    }
    $feed = Join-Path $repoRoot 'artifacts/packages'
    $buildProperties = [xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)
    $packageVersion = [string]$buildProperties.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($packageVersion)) { throw 'Product package version is missing.' }
    if (-not $NoPack) {
        $packProjects = switch ($Control) {
            'PdfViewer' { 'src/NT.AvaloniaControl.PdfViewer/NT.AvaloniaControl.PdfViewer.csproj'; 'src/NT.AvaloniaControl.PdfViewer.Themes/NT.AvaloniaControl.PdfViewer.Themes.csproj' }
            'Markdown' { 'src/NT.AvaloniaControl.Markdown/NT.AvaloniaControl.Markdown.csproj'; 'src/NT.AvaloniaControl.Markdown.Themes/NT.AvaloniaControl.Markdown.Themes.csproj'; 'src/NT.AvaloniaControl.Markdown.TextMate/NT.AvaloniaControl.Markdown.TextMate.csproj' }
            'VirtualKeyboard' { 'src/NT.AvaloniaControl.VirtualKeyboard/NT.AvaloniaControl.VirtualKeyboard.csproj'; 'src/NT.AvaloniaControl.VirtualKeyboard.Themes/NT.AvaloniaControl.VirtualKeyboard.Themes.csproj' }
            'TreeDataGrid' { 'src/NT.AvaloniaControl.TreeDataGrid/NT.AvaloniaControl.TreeDataGrid.csproj'; 'src/NT.AvaloniaControl.TreeDataGrid.Themes/NT.AvaloniaControl.TreeDataGrid.Themes.csproj' }
            default { 'NT.AvaloniaControl.slnx' }
        }
        foreach ($packProject in $packProjects) {
            dotnet pack $packProject -c Release --no-restore -m:1 -o $feed
            if ($LASTEXITCODE -ne 0) { throw "Package creation failed: $packProject" }
        }
    }
    if ($Control -in 'All', 'TreeDataGrid') {
    $consumer = Join-Path $repoRoot ('artifacts/consumer/' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $consumer -Force | Out-Null
    # These boundaries intentionally stop inherited repository build and central package settings.
    Set-Content -LiteralPath (Join-Path $consumer 'Directory.Build.props') -Value '<Project />' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $consumer 'Directory.Packages.props') -Value '<Project />' -Encoding utf8
    $consumerProject = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AvaloniaUseCompiledBindingsByDefault>false</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NT.AvaloniaControl.TreeDataGrid.Themes" Version="__PRODUCT_VERSION__" />
    <PackageReference Include="Avalonia.Headless" Version="12.0.3" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.3" />
  </ItemGroup>
</Project>
'@
    $consumerProject.Replace('__PRODUCT_VERSION__', $packageVersion) |
        Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj') -Encoding utf8
    @'
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System.Collections.ObjectModel;
using System.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;

namespace PackageConsumer;
internal static class Program
{
public static void Main()
{
AppBuilder.Configure<ConsumerApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
var root = new Person("独立 NuGet 消费", true);
using var source = new HierarchicalTreeDataGridSource<Person>(new[] { root }, item => item.Children,
    new HierarchicalTreeDataGridSourceOptions<Person> { HasChildrenSelector = item => ReferenceEquals(item, root) });
source.RowExpanding += (_, e) =>
{
    if (ReferenceEquals(e.Row.Model, root) && root.Children.Count == 0)
        root.Children.Add(new Person("包内按需加载", null));
};
var completed = 0;
source.RowExpanded += (_, _) => completed++;
var window = new ConsumerWindow();
var grid = window.FindControl<NT.AvaloniaControl.TreeDataGrid.TreeDataGrid>("Grid")!;
grid.Source = source;
window.Show();
window.UpdateLayout();
if (grid.RowsPresenter?.RealizedRowCount != 1 ||
    !grid.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "独立 NuGet 消费"))
    throw new InvalidOperationException("Packaged XAML, binding or theme could not render.");
if (source.ExpandAll() != 1 || completed != 1)
    throw new InvalidOperationException("Packaged tree expansion or events failed.");
window.UpdateLayout();
if (grid.RowsPresenter?.RealizedRowCount != 2 ||
    !grid.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "包内按需加载") ||
    !grid.GetVisualDescendants().OfType<CheckBox>().Any(c => c.IsChecked == true) ||
    !grid.GetVisualDescendants().OfType<CheckBox>().Any(c => c.IsThreeState && c.IsChecked is null))
    throw new InvalidOperationException("Packaged hierarchy, templates or nullable checkboxes failed.");
if (source.CollapseAll() != 1 || source.Rows.Count != 1)
    throw new InvalidOperationException("Packaged bulk collapse failed.");
var nameColumn = grid.Columns[1];
var rootId = source.Rows[0].Id;
grid.BeginEdit(new CellAddress(rootId, nameColumn.Id));
grid.State!.EditSession!.Value = "NuGet 编辑成功";
if (!grid.CommitEdit() || root.Name != "NuGet 编辑成功")
    throw new InvalidOperationException("Packaged transactional binding writeback failed.");
grid.BeginEdit(new CellAddress(rootId, grid.Columns[2].Id));
grid.State.EditSession!.Value = null;
if (!grid.CommitEdit() || root.Enabled is not null)
    throw new InvalidOperationException("Packaged nullable Boolean editing failed.");
var templateColumn = (TreeDataGridTemplateColumn)grid.Columns[4];
templateColumn.ValueGetter = item => ((Person)item).Name;
templateColumn.ValueSetter = (item, value) => ((Person)item).Name = (string)value!;
grid.BeginEdit(new CellAddress(rootId, templateColumn.Id));
window.UpdateLayout();
if (!grid.GetVisualDescendants().OfType<TextBox>().Any())
    throw new InvalidOperationException("Packaged CellEditingTemplate failed to mount.");
grid.State.EditSession!.Value = "NuGet 模板编辑成功";
if (!grid.CommitEdit() || root.Name != "NuGet 模板编辑成功")
    throw new InvalidOperationException("Packaged template draft did not commit.");
root.Children.Add(new Person("AAA", false));
source.ExpandAll();
var childId = source.Rows[1].Id;
grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
grid.State.SelectCellRange(new CellAddress(rootId, nameColumn.Id), new CellAddress(childId, grid.Columns[2].Id), grid.Columns.Select(column => column.Id).ToArray());
if (grid.State.SelectedCells.Count() != 4)
    throw new InvalidOperationException("Packaged rectangular selection failed.");
if (!grid.SortByColumn(nameColumn.Id, TreeDataGridSortDirection.Ascending) ||
    !source.Rows.Skip(1).Select(row => ((Person)row.Model).Name).SequenceEqual(root.Children.OrderBy(item => item.Name).Select(item => item.Name)))
    throw new InvalidOperationException("Packaged bound column sibling sorting failed.");
window.UpdateLayout();
if (!grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Any(header => header.SortIndicator == "▲"))
    throw new InvalidOperationException("Packaged sort indicator failed.");
grid.BeginEdit(new CellAddress(childId, nameColumn.Id));
grid.State.EditSession!.Value = "不可写回";
source.SetFilter(item => ReferenceEquals(item, root));
if (source.Rows.Count != 1 || grid.State.EditSession is not null || root.Children[0].Name != "包内按需加载")
    throw new InvalidOperationException("Packaged filtering failed to cancel a hidden editor.");
using (source.DeferRefresh()) { source.ClearSort(); source.SetFilter(null); }
if (source.Rows.Count != 3 || source.Rows[1].Id != childId)
    throw new InvalidOperationException("Packaged clear projection failed to restore source order.");
var originalName = root.Name;
grid.FrozenColumnCount = 1;
grid.BeginEdit(new CellAddress(rootId, nameColumn.Id));
grid.State.EditSession!.Value = "隐藏草稿";
nameColumn.IsVisible = false;
window.UpdateLayout();
if (grid.Columns.Count != 5 || grid.VisibleColumns.Count != 4 || grid.GetColumnIndex(nameColumn.Id) != -1 ||
    grid.State.EditSession is not null || root.Name != originalName || grid.RowsPresenter!.Layout!.ColumnCount != 4)
    throw new InvalidOperationException("Packaged visible-column mapping or zero-write cancellation failed.");
nameColumn.IsVisible = true;
grid.CanUserReorderColumns = true;
grid.BeginEdit(new CellAddress(rootId, nameColumn.Id));
var preservedEdit = grid.State.EditSession;
if (!grid.TryReorderColumn(nameColumn.Id, 2) || !ReferenceEquals(preservedEdit, grid.State.EditSession) || grid.GetColumnIndex(nameColumn.Id) != 2)
    throw new InvalidOperationException("Packaged column reorder failed to preserve identity and staged edit.");
grid.CancelEdit();
grid.GridLinesVisibility = TreeDataGridGridLinesVisibility.None;
grid.EmptyContent = "P6 包空内容";
source.SetFilter(_ => false);
window.UpdateLayout();
if (!grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "P6 包空内容"))
    throw new InvalidOperationException("Packaged configurable empty content failed.");
source.SetFilter(null);
window.UpdateLayout();
if (!grid.TryScrollIntoView(new NT.AvaloniaControl.TreeDataGrid.IndexPath(0, 1), select: true))
    throw new InvalidOperationException("Packaged source-path locator failed.");
grid.CanUserMoveRows = true;
var moveResult = grid.TryMoveRow(childId, source.Rows[2].Id, TreeDataGridRowDropPosition.After);
if (!moveResult.IsCommitted || moveResult.NodeId != childId || source.Rows[2].Id != childId)
    throw new InvalidOperationException("Packaged same-parent row move failed.");
var typedColumn = new TreeDataGridTextColumn();
typedColumn.SetValueAccessors<Person, string>(person => person.Name, (person, value) => person.Name = value);
grid.Columns.Add(typedColumn);
if (!grid.BeginEdit(new CellAddress(rootId, typedColumn.Id))) throw new InvalidOperationException("Packaged typed editing accessors failed.");
grid.State.EditSession!.Value = "P6 类型化提交";
if (!grid.CommitEdit() || root.Name != "P6 类型化提交") throw new InvalidOperationException("Packaged typed writeback failed.");
Console.WriteLine("PASS: P6 visible columns, zero-write hide, reorder with draft, empty content, source-path location, same-parent row move and typed writeback.");
window.Close();
var vm = new ConsumerViewModel();
var mvvmWindow = new ConsumerMvvmWindow { DataContext = vm };
mvvmWindow.Show();
mvvmWindow.UpdateLayout();
var bound = mvvmWindow.FindControl<NT.AvaloniaControl.TreeDataGrid.TreeDataGrid>("BoundGrid")!;
var boundTree = mvvmWindow.FindControl<NT.AvaloniaControl.TreeDataGrid.TreeDataGrid>("BoundTree")!;
vm.Items[0].Children.Add(new Person("MVVM 层级子项", null));
if (boundTree.Source?.Rows.Count != 2 || boundTree.Source.ExpandAll() != 1 ||
    boundTree.Source.Rows.Count != 3 || !vm.Items[0].Expanded)
    throw new InvalidOperationException("Packaged declarative hierarchy bindings failed.");
if (bound.Source?.Rows.Count != 2 || !ReferenceEquals(bound.SelectedItem, vm.Items[1]))
    throw new InvalidOperationException("Packaged compiled MVVM data/selection binding failed.");
vm.Items.Add(new Person("新增绑定项", true));
if (bound.Source.Rows.Count != 3) throw new InvalidOperationException("Packaged ItemsSource collection notifications failed.");
vm.SelectedItem = vm.Items[2];
if (!ReferenceEquals(bound.SelectedItem, vm.Items[2])) throw new InvalidOperationException("Packaged VM-to-selection failed.");
bound.SelectedItem = vm.Items[0];
if (!ReferenceEquals(vm.SelectedItem, vm.Items[0])) throw new InvalidOperationException("Packaged selection-to-VM failed.");
vm.CurrentCell = new CellAddress(bound.Source.Rows[0].Id, bound.Columns[0].Id);
if (bound.CurrentCell != vm.CurrentCell || !vm.Edits.BeginEditCommand.CanExecute(null))
    throw new InvalidOperationException("Packaged current cell or command binding failed.");
vm.Edits.BeginEditCommand.Execute(null);
if (!vm.Edits.IsEditing) throw new InvalidOperationException("Packaged VM begin command failed.");
vm.Edits.EditSession!.Value = "MVVM NuGet 提交";
vm.Edits.CommitEditCommand.Execute(null);
if (vm.Edits.IsEditing || vm.Items[0].Name != "MVVM NuGet 提交")
    throw new InvalidOperationException("Packaged VM edit commit failed.");
mvvmWindow.Close();
if (vm.Edits.BeginEditCommand.CanExecute(null)) throw new InvalidOperationException("Detached command controller retained its view.");
bound.ItemsSource = null;
boundTree.ItemsSource = null;
Console.WriteLine("PASS: independent NuGet consumer loaded both product assemblies, five column types, compiled XAML, themes, lazy expansion, nullable/template editing, rectangular selection, sorting and filtering.");
Console.WriteLine("PASS: compiled MVVM XAML, ItemsSource notifications, declarative hierarchy bindings, TwoWay selection/current cell and view-model-owned editing commands.");
}
}
public sealed class Person(string name, bool? enabled)
{
    public string Name { get; set; } = name;
    public bool? Enabled { get; set; } = enabled;
    public bool Expanded { get; set; }
    public ObservableCollection<Person> Children { get; } = new();
}
public sealed partial class ConsumerApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
public sealed partial class ConsumerWindow : Window
{
    public ConsumerWindow() => AvaloniaXamlLoader.Load(this);
}
public sealed partial class ConsumerMvvmWindow : Window
{
    public ConsumerMvvmWindow() => AvaloniaXamlLoader.Load(this);
}
public sealed class ConsumerViewModel : INotifyPropertyChanged
{
    private Person? _selectedItem;
    private CellAddress? _currentCell;
    public ConsumerViewModel() { _selectedItem = Items[1]; }
    public ObservableCollection<Person> Items { get; } = [new("甲", true), new("乙", false)];
    public TreeDataGridEditCommands Edits { get; } = new();
    public Person? SelectedItem { get => _selectedItem; set { _selectedItem = value; PropertyChanged?.Invoke(this, new(nameof(SelectedItem))); } }
    public CellAddress? CurrentCell { get => _currentCell; set { _currentCell = value; PropertyChanged?.Invoke(this, new(nameof(CurrentCell))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
'@ | Set-Content -LiteralPath (Join-Path $consumer 'Program.cs') -Encoding utf8
    @'
<Application xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="PackageConsumer.ConsumerApp">
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://NT.AvaloniaControl.TreeDataGrid.Themes/Themes/Generic.axaml" />
  </Application.Styles>
</Application>
'@ | Set-Content -LiteralPath (Join-Path $consumer 'App.axaml') -Encoding utf8
    @'
<Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:tg="clr-namespace:NT.AvaloniaControl.TreeDataGrid;assembly=NT.AvaloniaControl.TreeDataGrid"
        x:Class="PackageConsumer.ConsumerWindow" Width="880" Height="320">
  <tg:TreeDataGrid x:Name="Grid">
    <tg:TreeDataGrid.Columns>
      <tg:TreeDataGridRowHeaderColumn Header="#" Width="48" />
      <tg:TreeDataGridHierarchicalExpanderColumn Header="名称" Width="280">
        <tg:TreeDataGridHierarchicalExpanderColumn.InnerColumn>
          <tg:TreeDataGridTextColumn Binding="{ReflectionBinding Name}" />
        </tg:TreeDataGridHierarchicalExpanderColumn.InnerColumn>
      </tg:TreeDataGridHierarchicalExpanderColumn>
      <tg:TreeDataGridCheckBoxColumn Header="启用" Width="80" IsThreeState="True" Binding="{ReflectionBinding Enabled}" />
      <tg:TreeDataGridTextColumn Header="文本" Width="Auto" Binding="{ReflectionBinding Name}" StringFormat="值：{0}" />
      <tg:TreeDataGridTemplateColumn Header="模板" Width="*">
        <tg:TreeDataGridTemplateColumn.CellTemplate>
          <DataTemplate><TextBlock Text="{ReflectionBinding Name}" FontWeight="Bold" /></DataTemplate>
        </tg:TreeDataGridTemplateColumn.CellTemplate>
        <tg:TreeDataGridTemplateColumn.CellEditingTemplate>
          <DataTemplate><TextBox Text="{ReflectionBinding Value, Mode=TwoWay}" /></DataTemplate>
        </tg:TreeDataGridTemplateColumn.CellEditingTemplate>
      </tg:TreeDataGridTemplateColumn>
    </tg:TreeDataGrid.Columns>
  </tg:TreeDataGrid>
</Window>
'@ | Set-Content -LiteralPath (Join-Path $consumer 'Window.axaml') -Encoding utf8
    @'
<Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:tg="clr-namespace:NT.AvaloniaControl.TreeDataGrid;assembly=NT.AvaloniaControl.TreeDataGrid"
        xmlns:local="clr-namespace:PackageConsumer" x:Class="PackageConsumer.ConsumerMvvmWindow"
        x:DataType="local:ConsumerViewModel" x:CompileBindings="True" Width="700" Height="320">
  <Grid RowDefinitions="*,*">
  <tg:TreeDataGrid x:Name="BoundGrid" ItemsSource="{Binding Items}" SelectedItem="{Binding SelectedItem}"
                   CurrentCell="{Binding CurrentCell}" EditCommands="{Binding Edits}">
    <tg:TreeDataGridTextColumn Header="MVVM 名称" Width="*" Binding="{ReflectionBinding Name}" />
  </tg:TreeDataGrid>
  <tg:TreeDataGrid x:Name="BoundTree" Grid.Row="1" ItemsSource="{Binding Items}">
    <tg:TreeDataGridHierarchicalExpanderColumn Header="MVVM 层级" Width="*"
            ChildrenBinding="{ReflectionBinding Children}" IsExpandedBinding="{ReflectionBinding Expanded}">
      <tg:TreeDataGridTextColumn Binding="{ReflectionBinding Name}" />
    </tg:TreeDataGridHierarchicalExpanderColumn>
  </tg:TreeDataGrid>
  </Grid>
</Window>
'@ | Set-Content -LiteralPath (Join-Path $consumer 'MvvmWindow.axaml') -Encoding utf8
    # A fresh package cache prevents reusing a stale local preview of our packages.
    $packageCache = Join-Path $consumer '.packages'
    $escapedFeed = [System.Security.SecurityElement]::Escape($feed)
    $config = Join-Path $consumer 'NuGet.Config'
    $dependencySource = if ($DependencyFeed) { '<add key="dependencies" value="' + [System.Security.SecurityElement]::Escape($DependencyFeed) + '" />' } else { '<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />' }
    "<configuration><packageSources><clear /><add key=`"local-preview`" value=`"$escapedFeed`" />$dependencySource</packageSources></configuration>" |
        Set-Content -LiteralPath $config -Encoding utf8
    dotnet restore (Join-Path $consumer 'Consumer.csproj') --packages $packageCache --configfile $config
    if ($LASTEXITCODE -ne 0) { throw 'Independent package restore failed.' }
    dotnet run --project (Join-Path $consumer 'Consumer.csproj') -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Independent package consumer failed.' }
    [ordered]@{ passed=$true; timestampUtc=[DateTimeOffset]::UtcNow; consumer=$consumer; restoreMode=$(if($DependencyFeed){'IsolatedOfflineCache'}else{'IsolatedNetworkCache'}); dependencyFeed=$DependencyFeed; packages=@('NT.AvaloniaControl.TreeDataGrid','NT.AvaloniaControl.TreeDataGrid.Themes' | ForEach-Object { $packagePath=Join-Path $feed ("$_.$packageVersion.nupkg"); [ordered]@{path=$packagePath;sha256=(Get-FileHash -LiteralPath $packagePath).Hash} }) } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $consumer 'package-consumption.json') -Encoding utf8
    Set-Content -LiteralPath (Join-Path $repoRoot 'artifacts/treedatagrid-theme-consumer-path.txt') -Value $consumer -Encoding utf8
    }
    if ($Control -in 'All', 'VirtualKeyboard') {
        & (Join-Path $PSScriptRoot 'Test-VirtualKeyboardPackageConsumer.ps1')
    }
    if ($Control -in 'All', 'Markdown') {
        & (Join-Path $PSScriptRoot 'Test-MarkdownPackageConsumer.ps1') -NoPack
    }
    if ($Control -in 'All', 'PdfViewer') {
        & (Join-Path $PSScriptRoot 'Test-PdfViewerPackageConsumer.ps1') -NoPack
    }
}
finally { Pop-Location }
