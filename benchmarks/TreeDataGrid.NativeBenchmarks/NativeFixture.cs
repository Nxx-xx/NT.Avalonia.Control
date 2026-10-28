using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGridNativeBenchmarks;

internal sealed class ProbeModel(string name) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private string _name = name;
    internal int Writes { get; private set; }
    internal int Subscribers => _changed?.GetInvocationList().Length ?? 0;
    public string Name { get => _name; set { _name = value; Writes++; _changed?.Invoke(this, new(nameof(Name))); } }
    public TreeDataGridRowMoveCollection<ProbeModel> Children { get; } = [];
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}

internal sealed class ProbeFactory : TreeDataGridElementFactory
{
    private readonly HashSet<Control> _active = [];
    private readonly Dictionary<Control, (ProbeModel Model, PropertyChangedEventHandler Handler)> _subscriptions = [];
    internal long Created, Released, Prepared, Cleared;
    internal int Active => _active.Count;
    internal int Subscriptions => _subscriptions.Count;
    internal Action<TreeDataGridElementContext>? ObserveContext { get; init; }
    public override Control CreateElement(GridControl owner, TreeDataGridElementKind kind) { Created++; return base.CreateElement(owner, kind); }
    public override void PrepareElement(TreeDataGridElementContext context)
    {
        NativeFixture.Require(_active.Add(context.Element), "Factory prepared the same lifetime twice."); Prepared++;
        if (context.Kind == TreeDataGridElementKind.Cell && context.Row!.Model is ProbeModel model)
        {
            PropertyChangedEventHandler handler = (_, _) => { }; _subscriptions.Add(context.Element, (model, handler)); model.PropertyChanged += handler;
        }
        ObserveContext?.Invoke(context);
    }
    public override void ClearElement(TreeDataGridElementContext context)
    {
        NativeFixture.Require(_active.Remove(context.Element), "Factory cleared an unknown lifetime."); Cleared++;
        if (_subscriptions.Remove(context.Element, out var subscription)) subscription.Model.PropertyChanged -= subscription.Handler;
    }
    public override void ReleaseElement(GridControl owner, TreeDataGridElementKind kind, Control element)
    {
        NativeFixture.Require(!_active.Contains(element) && element.Parent is null && element.DataContext is null, "Released factory element was still attached/prepared."); Released++;
    }
    internal void AssertRetired() => NativeFixture.Require(Active == 0 && Subscriptions == 0 && Created == Released && Prepared == Cleared, "Retired factory retained elements/subscriptions.");
}

internal sealed record SceneSnapshot(long Revision, long SourceVersion, double OffsetX, double OffsetY, int ProjectionRows,
    int RealizedRows, int RetainedRows, int RealizedCells, long FirstNode, long LastNode, int Prepared, int Cleared, int Commits, string? CurrentCell,
    bool AutoRowHeight, double MaxRowHeight, int CreatedRows, int CreatedCells);

internal sealed class NativeFixture : IDisposable
{
    internal GridControl Grid { get; }
    internal TreeDataGridSource? Source { get; private set; }
    internal ProbeModel[] Models { get; private set; }
    internal ProbeFactory? Factory { get; private set; }
    internal long Revision { get; set; }
    internal int Prepared, Cleared, Commits;
    private readonly HashSet<TreeDataGridCellPresenter> _activeCells = [];
    private readonly bool _events;

    internal NativeFixture(string scenario, bool durability = false, Action<string, object>? track = null, bool observeEvents = false)
    {
        var auto = scenario.StartsWith("auto-", StringComparison.Ordinal);
        var tree = scenario == "wide-tree";
        _events = durability || observeEvents || scenario is "auto-factory" or "navigation-edit";
        var count = durability ? 1000 : scenario == "flat-100k" || tree ? 100000 : 10000;
        var columns = durability ? 40 : scenario is "wide-tree" or "navigation-edit" or "observer" ? 20 : 100;
        Models = Enumerable.Range(0, count).Select(index => new ProbeModel(auto && index % 11 == 0
            ? string.Join(' ', Enumerable.Repeat("自动行高 wrapped 文本", 12)) : "row " + index)).ToArray();
        if (tree)
        {
            var root = new ProbeModel("wide root"); root.Children.AddRange(Models);
            Source = new HierarchicalTreeDataGridSource<ProbeModel>([root], model => model.Children); Source.ExpandAll();
        }
        else Source = new FlatTreeDataGridSource<ProbeModel>(new TreeDataGridRowMoveCollection<ProbeModel>(Models));
        if (durability || scenario == "auto-factory") Factory = new ProbeFactory { ObserveContext = context => track?.Invoke("contexts", context) };
        Grid = new GridControl { Source = Source, ElementFactory = Factory, FrozenColumnCount = 2, AutoRowHeight = auto,
            SelectionMode = TreeDataGridSelectionMode.Multiple, CanUserMoveRows = durability, CanUserReorderColumns = true };
        for (var index = 0; index < columns; index++) Grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "C" + index, Width = new GridLength(110), TextWrapping = auto ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Binding = scenario == "flat-100k" || tree || scenario == "observer" ? new ReflectionBinding(nameof(ProbeModel.Name)) : TreeDataGridBinding.Create<ProbeModel, string>(model => model.Name),
            ValueValidator = value => string.IsNullOrWhiteSpace(value?.ToString()) ? "Name required" : null,
        });
        if (_events)
        {
            Grid.CellPrepared += OnPrepared; Grid.CellClearing += OnClearing; Grid.CellValueChanged += OnChanged;
        }
    }
    private void OnPrepared(object? sender, TreeDataGridCellEventArgs args) { Require(_activeCells.Add(args.Container), "Duplicate A2 preparation."); Prepared++; }
    private void OnClearing(object? sender, TreeDataGridCellEventArgs args) { Require(_activeCells.Remove(args.Container), "Unpaired A2 clearing."); Cleared++; }
    private void OnChanged(object? sender, TreeDataGridCellValueChangedEventArgs args) { if (args.Reason == TreeDataGridCellValueChangeReason.EditCommit) Commits++; }

    internal SceneSnapshot Snapshot()
    {
        var source = Source ?? throw new InvalidOperationException("No active source.");
        var rows = Grid.RowsPresenter ?? throw new InvalidOperationException("No rows presenter.");
        var realized = rows.RealizedRows.OrderBy(row => row.RowIndex).ToArray();
        var rowBudget = (int)Math.Ceiling(rows.Viewport.Height / Grid.RowHeight) + 5;
        var columnBudget = (int)Math.Ceiling(rows.Viewport.Width / 110) + Grid.FrozenColumnCount + 4;
        Require(rows.RetainedRowCount is > 0 && rows.RetainedRowCount <= rowBudget && rows.RealizedCellCount > 0 && rows.RealizedCellCount <= rowBudget * columnBudget, "Viewport container budget exceeded.");
        foreach (var row in realized)
        {
            Require(ReferenceEquals(source.Rows[row.RowIndex].Model, row.DataContext), "Wrong realized row model.");
            foreach (var cell in row.RealizedCells)
            {
                Require(ReferenceEquals(rows.Layout!.Columns[cell.ColumnIndex], cell.Column) && ReferenceEquals(cell.Row?.Model, row.DataContext), "Wrong realized column/model identity.");
                if (cell.Content is TextBlock text) Require(text.Text == ((ProbeModel)row.DataContext!).Name, "Native visible text differs from the current model.");
            }
        }
        if (_events) Require(_activeCells.Count == rows.RealizedCellCount && Prepared - Cleared == _activeCells.Count, "A2 active cell count differs from viewport.");
        if (Factory is { } factory) Require(factory.Active == rows.RealizedRowCount + rows.RealizedCellCount + Grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Count(), "Factory active count differs from visual lifetimes.");
        return new(Revision, source.Version, rows.Offset.X, rows.Offset.Y, source.Rows.Count, rows.RealizedRowCount, rows.RetainedRowCount, rows.RealizedCellCount,
            realized[0].Row!.Id.Value, realized[^1].Row!.Id.Value, Prepared, Cleared, Commits, Grid.CurrentCell?.ToString(),
            Grid.AutoRowHeight, realized.Max(row => rows.Layout!.GetRowHeight(row.RowIndex)), rows.CreatedRowCount, rows.CreatedCellCount);
    }

    internal void Scroll(int step)
    {
        var rows = Grid.RowsPresenter!;
        rows.Offset = new Vector(step * 137L % Math.Max(1, (long)(rows.Extent.Width - rows.Viewport.Width)),
            step * 701L % Math.Max(1, (long)(rows.Extent.Height - rows.Viewport.Height))); Revision++;
    }

    internal void ReplaceFactory(Action<string, object> track)
    {
        var old = Factory; if (old is not null) track("factories", old);
        Factory = new ProbeFactory { ObserveContext = context => track("contexts", context) }; Grid.ElementFactory = Factory; old?.AssertRetired(); Revision++;
    }
    internal void UseDefaultFactory(Action<string, object> track)
    {
        var old = Factory!; track("factories", old); Grid.ElementFactory = null; Factory = null; old.AssertRetired(); Revision++;
    }
    internal void ReplaceSource(Action<string, object> track)
    {
        var oldSource = Source!; var oldModels = Models; track("sources", oldSource); foreach (var model in oldModels) track("models", model);
        Models = Enumerable.Range(0, 1000).Select(index => new ProbeModel(Grid.AutoRowHeight && index % 11 == 0
            ? string.Join(' ', Enumerable.Repeat("replacement 自动行高 wrapped 文本", 12)) : "replacement " + index)).ToArray();
        Source = new FlatTreeDataGridSource<ProbeModel>(new TreeDataGridRowMoveCollection<ProbeModel>(Models)); Grid.Source = Source; oldSource.Dispose();
        foreach (var model in oldModels) Require(model.Subscribers == 0, "Replaced source model retained subscriptions."); Revision++;
    }
    internal void RebuildTemplate()
    {
        Grid.Template = new FuncControlTemplate<GridControl>((_, scope) =>
        {
            var rows = new TreeDataGridRowsPresenter(); var headers = new TreeDataGridColumnHeadersPresenter { Height = 28 };
            var region = new Border { Child = headers, [!Control.IsVisibleProperty] = new TemplateBinding(GridControl.ShowColumnHeadersProperty) };
            var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ColumnHeadersPresenter", headers); scope.Register("PART_ScrollViewer", scroll);
            Avalonia.Controls.Grid.SetRow(scroll, 1);
            return new Avalonia.Controls.Grid { RowDefinitions = new("Auto,*"), Children = { region, scroll } };
        }); Revision++;
    }

    internal void NavigateAndEdit(int step)
    {
        var row = Source!.Rows[step % Math.Min(100, Source.Rows.Count)]; var address = new CellAddress(row.Id, Grid.Columns[0].Id);
        Grid.State!.Select(row.Id); Require(Grid.BeginEdit(address), "Native staged editing failed."); var model = (ProbeModel)row.Model; var writes = model.Writes;
        Grid.EditSession!.Value = "cancelled"; Grid.CancelEdit(); Require(model.Writes == writes, "Cancelled native draft wrote its model.");
        Require(Grid.BeginEdit(address), "Native second editing failed."); Grid.EditSession!.Value = "edit " + step;
        Require(Grid.CommitEdit() && model.Writes == writes + 1, "Native edit did not write once.");
        Require(Grid.TryScrollIntoView(row.Id), "Native navigation failed."); Revision++;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void TrackRetired(Action<string, object> track)
    {
        foreach (var control in Grid.GetVisualDescendants().OfType<Control>().Prepend(Grid))
            if (Avalonia.Automation.Peers.ControlAutomationPeer.FromElement(control) is { } peer) track("automationPeers", peer);
        foreach (var model in Models) track("models", model);
        track("sources", Source!); track("grids", Grid);
        if (Factory is { } factory) track("factories", factory);
        if (Grid.RowsPresenter is { } rows)
        {
            track("presenters", rows);
            foreach (var row in rows.RealizedRows) { track("rows", row); foreach (var cell in row.RealizedCells) track("cells", cell); }
        }
        foreach (var header in Grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>()) track("headers", header);
    }

    public void Dispose()
    {
        var source = Source; var factory = Factory; Source = null; Factory = null;
        try { Grid.ElementFactory = null; Grid.Source = null; source?.Dispose(); }
        finally
        {
            if (_events) { Grid.CellPrepared -= OnPrepared; Grid.CellClearing -= OnClearing; Grid.CellValueChanged -= OnChanged; }
        }
        Require(_activeCells.Count == 0 && Prepared == Cleared, "Retired native A2 lifetimes did not pair."); factory?.AssertRetired();
        foreach (var model in Models) Require(model.Subscribers == 0, "Retired native model still has subscriptions.");
        Models = []; Grid.Columns.Clear();
    }
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
