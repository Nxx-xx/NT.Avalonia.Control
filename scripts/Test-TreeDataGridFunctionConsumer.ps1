param([switch]$NoPack)
$ErrorActionPreference = 'Stop'
$functionRepoRoot = [IO.Path]::GetDirectoryName($PSScriptRoot)
Push-Location $functionRepoRoot
try {
    $functionFeed = Join-Path $functionRepoRoot 'artifacts/packages'
    $functionBuildProperties = [xml](Get-Content -LiteralPath (Join-Path $functionRepoRoot 'Directory.Build.props') -Raw)
    $functionPackageVersion = [string]$functionBuildProperties.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($functionPackageVersion)) { throw 'TreeDataGrid package version is missing.' }
    if (-not $NoPack) {
        dotnet pack src/NT.AvaloniaControl.TreeDataGrid/NT.AvaloniaControl.TreeDataGrid.csproj -c Release --no-build --no-restore -m:1 -o $functionFeed
        if ($LASTEXITCODE -ne 0) { throw 'Function package creation failed; build Release before running this consumer.' }
    }
    $functionPackage = Join-Path $functionFeed ("NT.AvaloniaControl.TreeDataGrid.$functionPackageVersion.nupkg")
    if (-not (Test-Path -LiteralPath $functionPackage)) { throw "Function package is missing: $functionPackage" }
    $functionConsumer = Join-Path $functionRepoRoot ('artifacts/consumer/treedatagrid-function-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $functionConsumer -Force | Out-Null
    foreach ($functionBoundary in @('Directory.Build.props', 'Directory.Packages.props')) {
        Set-Content -LiteralPath (Join-Path $functionConsumer $functionBoundary) -Value '<Project />' -Encoding utf8
    }
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NT.AvaloniaControl.TreeDataGrid" Version="__PRODUCT_VERSION__" />
    <PackageReference Include="Avalonia.Headless" Version="12.0.3" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.3" />
  </ItemGroup>
</Project>
'@.Replace('__PRODUCT_VERSION__', [System.Security.SecurityElement]::Escape($functionPackageVersion)) |
        Set-Content -LiteralPath (Join-Path $functionConsumer 'Consumer.csproj') -Encoding utf8
    @'
<Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:tg="clr-namespace:NT.AvaloniaControl.TreeDataGrid;assembly=NT.AvaloniaControl.TreeDataGrid"
        xmlns:local="clr-namespace:TreeDataGridFunctionConsumer"
        x:Class="TreeDataGridFunctionConsumer.ConsumerWindow" x:DataType="local:RowModel" Width="480" Height="280">
  <Window.Resources>
    <ControlTheme x:Key="ApplicationCell" TargetType="tg:TreeDataGridCellPresenter">
      <Setter Property="BorderThickness" Value="3" />
      <Setter Property="BorderBrush" Value="Green" />
      <Setter Property="Template">
        <ControlTemplate>
          <Border Name="ApplicationCellBorder" BorderThickness="{TemplateBinding BorderThickness}" BorderBrush="{TemplateBinding BorderBrush}">
            <ContentPresenter Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" Padding="{TemplateBinding Padding}" />
          </Border>
        </ControlTemplate>
      </Setter>
    </ControlTheme>
    <ControlTheme x:Key="ApplicationGrid" TargetType="tg:TreeDataGrid">
      <Setter Property="Template">
        <ControlTemplate>
          <Grid RowDefinitions="Auto,*">
            <tg:TreeDataGridColumnHeadersPresenter Name="PART_ColumnHeadersPresenter" Height="28" />
            <ScrollViewer Name="PART_ScrollViewer" Grid.Row="1" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
              <tg:TreeDataGridRowsPresenter Name="PART_RowsPresenter" />
            </ScrollViewer>
            <ContentPresenter Name="ApplicationEmptyContent" Grid.Row="1" IsVisible="False"
                              Content="{TemplateBinding EmptyContent}" ContentTemplate="{TemplateBinding EmptyContentTemplate}"
                              HorizontalAlignment="Center" VerticalAlignment="Center" />
          </Grid>
        </ControlTemplate>
      </Setter>
      <Style Selector="^:empty /template/ ContentPresenter#ApplicationEmptyContent">
        <Setter Property="IsVisible" Value="True" />
      </Style>
    </ControlTheme>
  </Window.Resources>
  <tg:TreeDataGrid Name="FunctionGrid" Theme="{StaticResource ApplicationGrid}" CellTheme="{StaticResource ApplicationCell}"
                   GridLinesVisibility="Vertical" EmptyContent="功能包空内容" SelectionUnit="Cell"
                   CanUserReorderColumns="True" CanUserMoveRows="True">
    <tg:TreeDataGrid.Columns>
      <tg:TreeDataGridTextColumn Header="Name" Width="140" Binding="{CompiledBinding Name}" />
      <tg:TreeDataGridTextColumn Header="Hidden" Width="80" IsVisible="False" Binding="{CompiledBinding Name}" />
      <tg:TreeDataGridTextColumn Header="Detail" Width="140" Binding="{CompiledBinding Name}" />
    </tg:TreeDataGrid.Columns>
    <tg:TreeDataGrid.EmptyContentTemplate>
      <DataTemplate x:DataType="local:EmptyModel">
        <TextBlock Name="ConsumerEmptyText" Text="{CompiledBinding Message}" />
      </DataTemplate>
    </tg:TreeDataGrid.EmptyContentTemplate>
  </tg:TreeDataGrid>
</Window>
'@ | Set-Content -LiteralPath (Join-Path $functionConsumer 'Window.axaml') -Encoding utf8
    @'
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGridFunctionConsumer;

internal static class Program
{
    private static readonly Dictionary<string, bool> Checks = [];
    private static void Check(bool condition, string name)
    { Checks[name] = condition; if (!condition) throw new InvalidOperationException(name); }

    public static void Main()
    {
        var passed = false; string? error = null; ConsumerWindow? window = null; FlatTreeDataGridSource<RowModel>? source = null;
        try
        {
            Check(!Directory.EnumerateFiles(AppContext.BaseDirectory, "NT.AvaloniaControl.TreeDataGrid.Themes*.dll").Any() &&
                !typeof(GridControl).Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name == "NT.AvaloniaControl.TreeDataGrid.Themes"), "function_only_dependencies");
            AppBuilder.Configure<ConsumerApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            var items = new ObservableCollection<RowModel> { new("first"), new("second"), new("third") };
            source = new FlatTreeDataGridSource<RowModel>(items);
            window = new ConsumerWindow();
            var grid = window.FindControl<GridControl>("FunctionGrid")!;
            var name = grid.Columns[0]; var hidden = grid.Columns[1]; var detail = grid.Columns[2];
            Check(grid.GridLinesVisibility == TreeDataGridGridLinesVisibility.Vertical && Equals(grid.EmptyContent, "功能包空内容") && !hidden.IsVisible &&
                grid.Columns.Cast<TreeDataGridTextColumn>().All(column => column.Binding is CompiledBinding), "compiled_xaml_properties");
            var emptyModel = new EmptyModel("应用模板空内容");
            grid.EmptyContent = emptyModel;
            grid.Source = source;
            var writes = 0;
            name.SetValueAccessors<RowModel, string>(row => row.Name, (row, value) => { row.Name = value; writes++; });
            window.Show(); window.UpdateLayout();
            Check(grid.VisibleColumns.SequenceEqual(new[] { name, detail }) && grid.GetColumnIndex(hidden.Id) == -1 &&
                grid.RowsPresenter?.RealizedRowCount == 3, "source_and_visible_columns");
            Check(grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "first"), "compiled_display");
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var mode in Enum.GetValues<TreeDataGridGridLinesVisibility>())
                {
                    window.RequestedThemeVariant = variant; grid.GridLinesVisibility = mode; window.UpdateLayout();
                    var cells = grid.RowsPresenter!.RealizedRows.SelectMany(row => row.RealizedCells).ToArray();
                    Check(cells.Length > 0 && cells.All(cell => cell.BorderThickness == new Thickness(3) &&
                        cell.GetVisualDescendants().OfType<Border>().Any(border => border.Name == "ApplicationCellBorder" && border.BorderThickness == new Thickness(3))), "four_grid_modes_keep_custom_cell_theme");
                }
            var first = source.Rows[0].Id; var last = source.Rows[2].Id;
            var address = new CellAddress(first, name.Id);
            Check(grid.BeginEdit(address), "begin_typed_compiled_edit");
            grid.State!.EditSession!.Value = "committed";
            Check(items[0].Name == "first" && writes == 0 && grid.CommitEdit() && items[0].Name == "committed" && writes == 1, "staged_typed_writeback");
            Check(grid.BeginEdit(address), "begin_hide_draft");
            grid.State.EditSession!.Value = "discarded"; name.IsVisible = false; window.UpdateLayout();
            Check(grid.State.EditSession is null && writes == 1 && items[0].Name == "committed" && grid.State.CurrentCell?.Column == detail.Id, "hide_editor_zero_write");
            name.IsVisible = true; window.UpdateLayout();
            Check(grid.BeginEdit(address), "begin_reorder_draft");
            var edit = grid.State.EditSession!; edit.Value = "retained"; var width = name.Width;
            Check(grid.TryReorderColumn(name.Id, 1) && ReferenceEquals(edit, grid.State.EditSession) && edit.Value as string == "retained" &&
                name.Width == width && writes == 1 && grid.GetColumnIndex(name.Id) == 1, "reorder_preserves_identity_and_draft");
            grid.CancelEdit(); window.UpdateLayout();
            var moved = grid.TryMoveRow(first, last, TreeDataGridRowDropPosition.After);
            Check(moved.IsCommitted && moved.NodeId == first && source.Rows[2].Id == first && items[2].Name == "committed", "same_parent_row_move");
            Check(grid.TryScrollIntoView(new IndexPath(2), select: true) && grid.State.CurrentCell?.Node == first, "source_path_locator");
            var peer = ControlAutomationPeer.CreatePeerForElement(grid) as TreeDataGridAutomationPeer;
            Check(peer is { RowCount: 3, ColumnCount: 2 } && peer.GetAutomationControlType() == AutomationControlType.DataGrid &&
                peer.GetCellPeer(first, name.Id)?.GetProvider<IValueProvider>()?.Value == "committed", "identity_automation_peer");
            source.SetFilter(_ => false); window.UpdateLayout();
            var empty = grid.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "ApplicationEmptyContent");
            Check(empty.IsVisible && ReferenceEquals(empty.Content, emptyModel) && ReferenceEquals(empty.ContentTemplate, grid.EmptyContentTemplate) &&
                grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "ConsumerEmptyText" && text.Text == emptyModel.Message), "custom_empty_content_and_template_binding");
            grid.EmptyContent = new EmptyModel("更新空内容"); window.UpdateLayout();
            Check(grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "ConsumerEmptyText" && text.Text == "更新空内容"), "dynamic_empty_content");
            source.SetFilter(null); window.UpdateLayout();
            Check(!empty.IsVisible && grid.RowsPresenter!.RealizedRowCount == 3, "filtered_empty_restores_data");
            items.Clear(); window.UpdateLayout();
            Check(empty.IsVisible && source.Rows.Count == 0, "empty_source");
            passed = true;
            Console.WriteLine("PASS: TreeDataGrid function-only fresh NuGet consumer: compiled AXAML, custom templates, four grid modes, zero-write hide, staged edit/reorder, row move, source-path locator, empty content and automation peer.");
        }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); Environment.ExitCode = 1; }
        finally
        {
            window?.Close();
            source?.Dispose();
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "function-consumer-result.json"), JsonSerializer.Serialize(new
            {
                TimestampUtc = DateTimeOffset.UtcNow, PackageVersion = "__PRODUCT_VERSION__", Passed = passed,
                FunctionAssembly = typeof(GridControl).Assembly.FullName, Checks, Error = error,
                Notes = "Independent feature NuGet only; Headless custom-template checks do not establish native IME, DPI or screen-reader support."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
public sealed class ConsumerApp : Application
{ public override void Initialize() => Styles.Add(new FluentTheme()); }
public sealed partial class ConsumerWindow : Window
{ public ConsumerWindow() => AvaloniaXamlLoader.Load(this); }
public sealed record EmptyModel(string Message);
public sealed class RowModel(string name) : INotifyPropertyChanged
{
    private string _name = name;
    public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
'@.Replace('__PRODUCT_VERSION__', $functionPackageVersion) |
        Set-Content -LiteralPath (Join-Path $functionConsumer 'Program.cs') -Encoding utf8
    $functionCache = Join-Path $functionConsumer '.packages'
    $functionConfig = Join-Path $functionConsumer 'NuGet.Config'
    $functionEscapedFeed = [System.Security.SecurityElement]::Escape($functionFeed)
    "<configuration><packageSources><clear /><add key=`"local-preview`" value=`"$functionEscapedFeed`" /><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`" /></packageSources></configuration>" |
        Set-Content -LiteralPath $functionConfig -Encoding utf8
    $functionProject = Join-Path $functionConsumer 'Consumer.csproj'
    dotnet restore $functionProject --packages $functionCache --configfile $functionConfig
    if ($LASTEXITCODE -ne 0) { throw 'Function-only consumer restore failed.' }
    $functionAssets = Get-Content -LiteralPath (Join-Path $functionConsumer 'obj/project.assets.json') -Raw | ConvertFrom-Json
    if (@($functionAssets.libraries.PSObject.Properties.Name | Where-Object { $_ -match '^NT\.AvaloniaControl\.TreeDataGrid\.Themes/' }).Count -gt 0) {
        throw 'Function-only consumer unexpectedly restored the optional TreeDataGrid theme package.'
    }
    dotnet run --project $functionProject -c Release --no-restore
    $functionExitCode = $LASTEXITCODE
    $functionResultPath = Join-Path $functionConsumer 'bin/Release/net10.0/function-consumer-result.json'
    if (-not (Test-Path -LiteralPath $functionResultPath)) { throw "Function-only consumer produced no result (exit $functionExitCode)." }
    Copy-Item -LiteralPath $functionResultPath -Destination (Join-Path $functionConsumer 'result.json')
    $functionResult = Get-Content -LiteralPath $functionResultPath -Raw | ConvertFrom-Json
    $functionFailedChecks = @($functionResult.Checks.PSObject.Properties | Where-Object { $_.Value -ne $true })
    if ($functionExitCode -ne 0 -or $functionResult.Passed -ne $true -or $functionFailedChecks.Count -gt 0 -or $functionResult.Error) {
        throw "Function-only consumer failed: exit=$functionExitCode, Passed=$($functionResult.Passed), checks=$($functionFailedChecks.Name -join ', '), Error=$($functionResult.Error)."
    }
    Set-Content -LiteralPath (Join-Path $functionRepoRoot 'artifacts/treedatagrid-function-consumer-path.txt') -Value $functionConsumer -Encoding utf8
    Write-Output "Function-only consumer artifacts: $functionConsumer"
}
finally { Pop-Location }
