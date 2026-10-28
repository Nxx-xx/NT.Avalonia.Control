param([switch]$NoPack, [string]$DependencyFeed)
$ErrorActionPreference = 'Stop'
$functionRepoRoot = [IO.Path]::GetDirectoryName($PSScriptRoot)
Push-Location $functionRepoRoot
try {
    if ($DependencyFeed) {
        $DependencyFeed = [IO.Path]::GetFullPath($DependencyFeed)
        if (-not (Test-Path -LiteralPath $DependencyFeed -PathType Container)) { throw 'DependencyFeed must be an existing local directory.' }
        if (@(Get-ChildItem -LiteralPath $DependencyFeed -Recurse -Filter 'nt.avaloniacontrol.treedatagrid*.nupkg').Count) { throw 'DependencyFeed must contain dependencies only; product packages come from artifacts/packages.' }
    }
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
    <local:AmountConverter x:Key="AmountConverter" />
    <local:ConsumerFactory x:Key="ConsumerFactory" Label="XAML" />
    <ControlTheme x:Key="ApplicationCell" TargetType="tg:TreeDataGridCellPresenter">
      <Setter Property="BorderThickness" Value="3" />
      <Setter Property="BorderBrush" Value="Green" />
      <Setter Property="Template">
        <ControlTemplate>
          <Border Name="ApplicationCellBorder" Background="Transparent" BorderThickness="{TemplateBinding BorderThickness}" BorderBrush="{TemplateBinding BorderBrush}">
            <ContentPresenter Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" Padding="{TemplateBinding Padding}" />
          </Border>
        </ControlTemplate>
      </Setter>
    </ControlTheme>
    <ControlTheme x:Key="ApplicationGrid" TargetType="tg:TreeDataGrid">
      <Setter Property="Template">
        <ControlTemplate>
          <Grid RowDefinitions="Auto,*">
            <tg:TreeDataGridColumnHeadersPresenter Name="PART_ColumnHeadersPresenter" Height="28" IsVisible="{TemplateBinding ShowColumnHeaders}" />
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
                   CanUserReorderColumns="True" CanUserMoveRows="True" RowDragHandle="WholeRow" ElementFactory="{StaticResource ConsumerFactory}">
    <tg:TreeDataGrid.Columns>
      <tg:TreeDataGridTextColumn Header="Name" Width="140" Binding="{CompiledBinding Name}" TextAlignment="Right" TextTrimming="None"
                                BeginEditGestures="F2" AllowTriStateSorting="False" />
      <tg:TreeDataGridTextColumn Header="Hidden" Width="80" IsVisible="False" Binding="{CompiledBinding Name}" />
      <tg:TreeDataGridTextColumn Header="Detail" Width="140" Binding="{CompiledBinding Name}" />
      <tg:TreeDataGridTextColumn Header="Amount" Width="140" IsVisible="False"
          Binding="{tg:TreeDataGridBindingAdapter Path=Detail.Amount, Binding={CompiledBinding Detail.Amount, Converter={StaticResource AmountConverter}, ConverterCulture={x:Static local:AmountConverter.French}, ConverterParameter=amount}}" />
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
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
            var lifetimes = new Dictionary<TreeDataGridCellPresenter, TreeDataGridCellEventArgs>();
            var preparedCells = 0; var clearedCells = 0;
            grid.CellPrepared += (_, e) => { if (!lifetimes.TryAdd(e.Container, e) || !ReferenceEquals(e.Model, e.Container.DataContext)) throw new InvalidOperationException("cell prepared identity"); preparedCells++; };
            grid.CellClearing += (_, e) => { if (!lifetimes.Remove(e.Container, out var old) || !ReferenceEquals(old, e) || !ReferenceEquals(e.Model, e.Container.DataContext)) throw new InvalidOperationException("cell clearing identity"); clearedCells++; };
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
            var displayedName = (TextBlock)grid.RowsPresenter!.RealizedRows[0].RealizedCells.Single(cell => cell.Column?.Id == name.Id).Content!;
            Check(displayedName.TextAlignment == TextAlignment.Right && ReferenceEquals(displayedName.TextTrimming, TextTrimming.None) &&
                displayedName.TextLayout.TextLines[0].Start > 0, "text_column_alignment_and_trimming");
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
            var peer = (TreeDataGridAutomationPeer)ControlAutomationPeer.CreatePeerForElement(grid)!;
            Check(peer is { RowCount: 3, ColumnCount: 2 } && peer.GetAutomationControlType() == AutomationControlType.DataGrid &&
                peer.GetCellPeer(first, name.Id)?.GetProvider<IValueProvider>()?.Value == "committed", "identity_automation_peer");
            var headerHeight = grid.RowsPresenter!.Viewport.Height;
            Check(grid.BeginEdit(new(first, name.Id)), "begin_header_visibility_draft");
            var headerDraft = grid.State.EditSession!; headerDraft.Value = "preserved";
            grid.ShowColumnHeaders = false; window.UpdateLayout();
            Check(!grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Any() && grid.RowsPresenter.Viewport.Height >= headerHeight + 28 &&
                ReferenceEquals(headerDraft, grid.State.EditSession) && peer.GetColumnHeaderPeer(name.Id) is null, "headers_hide_preserves_draft_and_releases_height");
            grid.ShowColumnHeaders = true; window.UpdateLayout();
            Check(grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Any() && ReferenceEquals(headerDraft, grid.State.EditSession) &&
                name.BeginEditGestures == TreeDataGridBeginEditGestures.F2 && name.AllowTriStateSorting == false, "headers_restore_and_compiled_column_options");
            grid.CancelEdit();
            var searchable = new TreeDataGridTemplateColumn { TextSearchBinding = CompiledBinding.Create<RowModel, string>(row => row.Name) };
            name.IsTextSearchEnabled = false; detail.IsTextSearchEnabled = false;
            grid.Columns.Add(searchable); window.UpdateLayout();
            Check(grid.SearchText("third")?.Column == searchable.Id, "compiled_template_search_binding");
            searchable.IsTextSearchEnabled = false;
            Check(grid.SearchText("third") is null, "disabled_search_columns");
            grid.Columns.Remove(searchable); name.IsTextSearchEnabled = true; detail.IsTextSearchEnabled = true;
            name.CompareAscending = (left, right) => string.CompareOrdinal(((RowModel)left).Name, ((RowModel)right).Name);
            name.CompareDescending = (left, right) => string.CompareOrdinal(((RowModel)right).Name, ((RowModel)left).Name);
            Check(grid.SortByColumn(name.Id, TreeDataGridSortDirection.Descending) &&
                source.Rows.Select(row => ((RowModel)row.Model).Name).SequenceEqual(items.Select(row => row.Name).OrderDescending(StringComparer.Ordinal)), "directional_descending_comparer");
            window.UpdateLayout();
            grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
            grid.State.SelectCell(new(first, name.Id));
            Check(grid.BeginEdit(new(first, name.Id)), "begin_selection_transaction_draft");
            var transactionDraft = grid.EditSession!; transactionDraft.Value = "transaction value";
            var originalOffset = grid.RowsPresenter.Offset;
            var beforeSelection = 0; var completedSelection = 0;
            EventHandler<TreeDataGridSelectionChangingEventArgs> rejectSelection = (_, e) => { beforeSelection++; e.Cancel = true; };
            grid.SelectionChanging += rejectSelection;
            grid.SelectionChangeCompleted += (_, e) => { if (!e.IsForced) completedSelection++; };
            var transactionTarget = source.Rows.First(row => row.Id != first).Id;
            Check(!grid.TryScrollIntoView(transactionTarget, select: true) && ReferenceEquals(transactionDraft, grid.EditSession)
                && grid.State.CurrentCell == new CellAddress(first, name.Id) && writes == 1 && beforeSelection == 1
                && completedSelection == 0 && grid.RowsPresenter.Offset == originalOffset, "cancel_selection_before_write_and_scroll");
            grid.SelectionChanging -= rejectSelection;
            var nameValues = new List<TreeDataGridCellValueChangedEventArgs>();
            grid.CellValueChanged += (_, e) => { if (e.Cell.Column == name.Id) nameValues.Add(e); };
            Check(grid.TryScrollIntoView(transactionTarget, select: true) && grid.EditSession is null && writes == 2
                && completedSelection == 1 && nameValues.Count == 1 && nameValues[0].Reason == TreeDataGridCellValueChangeReason.EditCommit
                && nameValues[0].HasOldValue && Equals(nameValues[0].OldValue, "committed") && Equals(nameValues[0].NewValue, "transaction value"), "accepted_selection_and_single_commit_value_event");
            window.UpdateLayout();
            var changedModel = (RowModel)source.Rows.First(row => row.Id == transactionTarget).Model;
            var oldName = changedModel.Name; changedModel.Name = "external value"; window.UpdateLayout();
            Check(nameValues.Count == 2 && nameValues[1].Reason == TreeDataGridCellValueChangeReason.ModelNotification
                && Equals(nameValues[1].OldValue, oldName) && Equals(nameValues[1].NewValue, "external value"), "observable_model_value_event");
            var amount = (TreeDataGridTextColumn)grid.Columns.Single(column => Equals(column.Header, "Amount"));
            amount.IsVisible = true; window.UpdateLayout();
            var amountId = source.Rows[0].Id; var amountModel = (RowModel)source.Rows[0].Model;
            var amountConverter = (AmountConverter)((CompiledBinding)amount.Binding!).Converter!;
            Check(amount.ValueSetter is null && amount.GetEditBindingError(amountModel) is null && grid.BeginEdit(new(amountId, amount.Id))
                && Equals(grid.EditSession!.OriginalValue, 12.5m) && Equals(grid.EditSession.Value, "amount 12,5"), "compiled_xaml_adapter_automatic_edit");
            grid.EditSession!.Value = "amount 25,75";
            Check(grid.CommitEdit() && amountModel.Detail.Amount == 25.75m && amountModel.Detail.Writes == 1
                && amountConverter.TargetType == typeof(decimal) && amountConverter.Parameter as string == "amount"
                && amountConverter.Culture?.Name == "fr-FR", "compiled_ConvertBack_type_parameter_culture");
            grid.BeginEdit(new(amountId, amount.Id)); amountConverter.NoUpdate = true; grid.EditSession!.Value = "amount 33,5";
            Check(!grid.CommitEdit() && grid.EditSession.ValidationError is not null && amountModel.Detail.Writes == 1, "converter_no_update_token_zero_write");
            grid.CancelEdit(); amountConverter.NoUpdate = false;
            grid.BeginEdit(new(amountId, amount.Id)); grid.EditSession!.Value = "amount 33,5";
            var oldDetail = amountModel.Detail; amountModel.Detail = new();
            Check(!grid.CommitEdit() && oldDetail.Writes == 1 && amountModel.Detail.Writes == 0, "compiled_adapter_nested_target_guard");
            grid.CancelEdit();
            Check(detail.GetEditBindingError(amountModel)?.Contains("opaque", StringComparison.Ordinal) == true, "opaque_compiled_binding_diagnostic");
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
            Check(preparedCells > 0 && clearedCells == preparedCells && lifetimes.Count == 0, "cell_lifetimes_pair_through_recycle_and_empty");
            Check(grid.RowDragHandle == TreeDataGridRowDragHandle.WholeRow, "compiled_XAML_whole_row_drag_handle");
            grid.Source = null; grid.SelectionUnit = TreeDataGridSelectionUnit.Row; grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
            var observable = new ObservableCollection<RowModel> { new("oa"), new("ob"), new("oc") };
            grid.ItemsSource = observable; window.UpdateLayout(); var adapted = grid.Source!;
            var observableFirst = adapted.Rows[0].Id;
            Check(grid.TryMoveRow(observableFirst, adapted.Rows[2].Id, TreeDataGridRowDropPosition.After).IsCommitted
                && observable[2].Name == "oa" && adapted.Rows[2].Id == observableFirst, "ItemsSource_original_observable_move");
            Check(grid.TryMoveRows([adapted.Rows[0].Id, adapted.Rows[1].Id], observableFirst, TreeDataGridRowDropPosition.After).Status == TreeDataGridRowMoveStatus.Unsupported,
                "ordinary_collection_batch_has_no_partial_commit");
            var batch = new TreeDataGridRowMoveCollection<RowModel> { new("ba"), new("bb"), new("bc") };
            grid.ItemsSource = batch; window.UpdateLayout(); adapted = grid.Source!;
            var batchNodes = adapted.Rows.Take(2).Select(row => row.Id).ToArray(); var batchTarget = adapted.Rows[2].Id;
            var batchResult = grid.TryMoveRows(batchNodes, batchTarget, TreeDataGridRowDropPosition.After);
            Check(batchResult.IsCommitted && batchResult.IdentityMap.Count == 2 && batchNodes.All(node => batchResult.IdentityMap[node] == node)
                && batch.Select(row => row.Name).SequenceEqual(new[] { "bc", "ba", "bb" }), "ItemsSource_atomic_batch_identity_map");
            var batchRequests = 0; grid.RowsMoveRequested += (_, args) => { batchRequests++; args.Cancel = true; };
            Check(grid.TryMoveRows(batchNodes, batchTarget, TreeDataGridRowDropPosition.Before).Status == TreeDataGridRowMoveStatus.Cancelled
                && batchRequests == 1 && batch[0].Name == "bc", "batch_request_cancels_once");
            var dragEvents = 0; grid.RowDragStarted += (_, _) => dragEvents++; grid.RowDrop += (_, args) => args.Cancel = true;
            var dragNode = adapted.Rows[0].Id; Check(grid.BeginEdit(new(dragNode, name.Id)), "begin_cancelled_drop_draft");
            var dragDraft = grid.EditSession!; dragDraft.Value = "never written";
            grid.RowsPresenter!.Offset = default; window.UpdateLayout();
            var dragCells = grid.RowsPresenter!.RealizedRows.SelectMany(row => row.RealizedCells).Where(cell => cell.Column == detail).OrderBy(cell => adapted.GetViewIndex(cell.Row!.Id)).ToArray();
            var dragStart = dragCells[0].TranslatePoint(new Point(25, 12), window)!.Value;
            var dragEnd = dragCells[^1].TranslatePoint(new Point(25, 26), window)!.Value;
            window.MouseDown(dragStart, MouseButton.Left); window.MouseMove(dragEnd); window.MouseUp(dragEnd, MouseButton.Left); window.UpdateLayout();
            Check(dragEvents == 1 && ReferenceEquals(dragDraft, grid.EditSession) && batch[0].Name == "bc", "routed_drop_cancel_preserves_draft_and_model");
            grid.CancelEdit(); grid.ItemsSource = null;
            var xamlFactory = (ConsumerFactory)grid.ElementFactory!;
            Check(xamlFactory.Label == "XAML" && xamlFactory.Kinds.Count == 3 && xamlFactory.Created > 0 && xamlFactory.Recycled > 0,
                "compiled_factory_three_roles_and_pool_hooks");
            var factoryModel = new RowModel("factory model");
            grid.ItemsSource = new ObservableCollection<RowModel> { factoryModel, new("other factory model") }; window.UpdateLayout();
            Check(xamlFactory.Subscriptions.Count > 0 && xamlFactory.Active.Values.All(context => ReferenceEquals(context.Source, grid.Source)), "factory_source_rebind_clears_old_lifetimes");
            var factoryAddress = new CellAddress(grid.Source!.Rows[0].Id, name.Id);
            Check(grid.BeginEdit(factoryAddress), "factory_begin_draft");
            var factoryDraft = grid.EditSession!; factoryDraft.Value = "factory committed"; var writesBeforeFactory = writes;
            var replacementFactory = new ConsumerFactory { Label = "Code" }; grid.ElementFactory = replacementFactory; window.UpdateLayout();
            Check(ReferenceEquals(factoryDraft, grid.EditSession) && writes == writesBeforeFactory && factoryModel.Name == "factory model",
                "runtime_factory_keeps_draft_zero_write");
            Check(xamlFactory.Active.Count == 0 && xamlFactory.Subscriptions.Count == 0 && xamlFactory.Created == xamlFactory.Released,
                "factory_old_subscriptions_and_containers_released");
            window.RequestedThemeVariant = ThemeVariant.Dark; grid.ShowColumnHeaders = false; window.UpdateLayout();
            window.RequestedThemeVariant = ThemeVariant.Light; grid.ShowColumnHeaders = true; window.UpdateLayout();
            Check(ReferenceEquals(factoryDraft, grid.EditSession) && replacementFactory.Kinds.Count == 3
                && grid.RowsPresenter!.RealizedRows.SelectMany(row => row.RealizedCells).All(cell => Equals(cell.Tag, "factory-Code")), "factory_theme_and_headers_preserve_draft");
            var factoryCommitted = grid.CommitEdit() && writes == writesBeforeFactory + 1 && factoryModel.Name == "factory committed";
            var factoryPeer = (TreeDataGridAutomationPeer)ControlAutomationPeer.CreatePeerForElement(grid)!;
            factoryPeer.GetCellPeer(factoryAddress.Node, factoryAddress.Column)!.GetProvider<IValueProvider>()!.SetValue("factory automation");
            Check(factoryCommitted && factoryModel.Name == "factory automation" && writes == writesBeforeFactory + 2, "factory_commit_and_identity_automation");
            grid.ElementFactory = null; window.UpdateLayout();
            Check(replacementFactory.Active.Count == 0 && replacementFactory.Subscriptions.Count == 0 && replacementFactory.Created == replacementFactory.Released,
                "factory_reset_to_default_releases_all_owned_elements");
            grid.ItemsSource = null;
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
public sealed class ConsumerFactory : TreeDataGridElementFactory
{
    public string Label { get; set; } = "factory";
    public int Created, Recycled, Released;
    public HashSet<TreeDataGridElementKind> Kinds { get; } = [];
    public Dictionary<Control, TreeDataGridElementContext> Active { get; } = [];
    public Dictionary<Control, (RowModel Model, PropertyChangedEventHandler Handler)> Subscriptions { get; } = [];
    public override Control CreateElement(GridControl owner, TreeDataGridElementKind kind)
    {
        var element = base.CreateElement(owner, kind); Created++; Kinds.Add(kind); element.Tag = "factory-" + Label;
        if (element is TreeDataGridCellPresenter cell) cell.Padding = new Thickness(9, 2);
        return element;
    }
    public override void PrepareElement(TreeDataGridElementContext context)
    {
        if (!Active.TryAdd(context.Element, context)) throw new InvalidOperationException("factory duplicate prepare");
        if (context.Kind == TreeDataGridElementKind.Cell && context.Row!.Model is RowModel model)
        {
            PropertyChangedEventHandler handler = (_, _) => { }; model.PropertyChanged += handler; Subscriptions.Add(context.Element, (model, handler));
        }
    }
    public override void ClearElement(TreeDataGridElementContext context)
    {
        if (!Active.Remove(context.Element)) throw new InvalidOperationException("factory unmatched clear");
        if (Subscriptions.Remove(context.Element, out var subscription)) subscription.Model.PropertyChanged -= subscription.Handler;
    }
    public override bool RecycleElement(TreeDataGridElementContext context)
    {
        if (context.Element.Parent is not null || context.Element.DataContext is not null) throw new InvalidOperationException("factory recycle before cleanup");
        Recycled++; return true;
    }
    public override void ReleaseElement(GridControl owner, TreeDataGridElementKind kind, Control element) => Released++;
}
public sealed partial class ConsumerWindow : Window
{ public ConsumerWindow() => AvaloniaXamlLoader.Load(this); }
public sealed record EmptyModel(string Message);
public sealed class RowModel(string name) : INotifyPropertyChanged
{
    private string _name = name;
    private RowDetail _detail = new();
    public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
    public RowDetail Detail { get => _detail; set { _detail = value; PropertyChanged?.Invoke(this, new(nameof(Detail))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed class RowDetail : INotifyPropertyChanged
{
    private decimal _amount = 12.5m;
    public decimal Amount { get => _amount; set { _amount = value; Writes++; PropertyChanged?.Invoke(this, new(nameof(Amount))); } }
    public int Writes { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed class AmountConverter : IValueConverter
{
    public static CultureInfo French => CultureInfo.GetCultureInfo("fr-FR");
    public bool NoUpdate { get; set; }
    public Type? TargetType { get; private set; }
    public object? Parameter { get; private set; }
    public CultureInfo? Culture { get; private set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => parameter + " " + System.Convert.ToString(value, culture);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        TargetType = targetType; Parameter = parameter; Culture = culture;
        return NoUpdate ? BindingOperations.DoNothing : decimal.Parse(value!.ToString()![(parameter!.ToString()!.Length + 1)..], culture);
    }
}
'@.Replace('__PRODUCT_VERSION__', $functionPackageVersion) |
        Set-Content -LiteralPath (Join-Path $functionConsumer 'Program.cs') -Encoding utf8
    $functionCache = Join-Path $functionConsumer '.packages'
    $functionConfig = Join-Path $functionConsumer 'NuGet.Config'
    $functionEscapedFeed = [System.Security.SecurityElement]::Escape($functionFeed)
    $functionDependencySource = if ($DependencyFeed) { '<add key="dependencies" value="' + [System.Security.SecurityElement]::Escape($DependencyFeed) + '" />' } else { '<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />' }
    "<configuration><packageSources><clear /><add key=`"local-preview`" value=`"$functionEscapedFeed`" />$functionDependencySource</packageSources></configuration>" |
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
    [ordered]@{ passed=$true; timestampUtc=[DateTimeOffset]::UtcNow; consumer=$functionConsumer; restoreMode=$(if($DependencyFeed){'IsolatedOfflineCache'}else{'IsolatedNetworkCache'}); dependencyFeed=$DependencyFeed; productPackageSha256=(Get-FileHash -LiteralPath $functionPackage).Hash } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $functionConsumer 'package-consumption.json') -Encoding utf8
    Write-Output "Function-only consumer artifacts: $functionConsumer"
}
finally { Pop-Location }
