using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class A6IntegrationTests
{
    [AvaloniaFact]
    public void Cancelled_provider_navigation_keeps_the_same_draft_after_factory_template_and_theme_changes()
    {
        var models = new[] { new A6Model("first"), new A6Model("second") };
        using var source = new FlatTreeDataGridSource<A6Model>(models);
        var grid = A6Host.Create(source); var window = A1TestHost.Show(grid);
        try
        {
            var peer = A6Host.Peer(grid); var address = new CellAddress(source.Rows[0].Id, grid.Columns[0].Id);
            peer.GetCellPeer(address.Node, address.Column)!.GetProvider<ISelectionItemProvider>()!.Select();
            Assert.True(grid.BeginEdit(address)); var edit = grid.EditSession!; edit.Value = "draft";
            var cancelled = 0;
            grid.SelectionChanging += (_, args) => { if (args.Reason == TreeDataGridSelectionChangeReason.Automation) { args.Cancel = true; cancelled++; } };
            var target = peer.GetCellPeer(source.Rows[1].Id, address.Column)!;
            target.GetProvider<ISelectionItemProvider>()!.Select();
            grid.ElementFactory = new A6Factory(); grid.Template = A1TestHost.Template(); grid.ShowColumnHeaders = false;
            window.RequestedThemeVariant = ThemeVariant.Dark; window.UpdateLayout();
            target.GetProvider<ISelectionItemProvider>()!.Select();
            Assert.Equal(2, cancelled); Assert.Same(edit, grid.EditSession); Assert.Equal(address, grid.CurrentCell);
            Assert.Equal("draft", edit.Value); Assert.All(models, model => Assert.Equal(0, model.Writes));
            Assert.False(target.GetProvider<ISelectionItemProvider>()!.IsSelected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Validation_error_and_logical_value_survive_rebuilt_containers_and_recover_with_one_write()
    {
        var model = new A6Model("original"); using var source = new FlatTreeDataGridSource<A6Model>([model]);
        var grid = A6Host.Create(source); var window = A1TestHost.Show(grid);
        try
        {
            var peer = A6Host.Peer(grid).GetCellPeer(source.Rows[0].Id, grid.Columns[0].Id)!;
            var value = peer.GetProvider<IValueProvider>()!; var identity = peer.GetAutomationId();
            Assert.Throws<InvalidOperationException>(() => value.SetValue(""));
            var edit = grid.EditSession!; Assert.Contains("名称不能为空", peer.GetHelpText()); Assert.Equal(0, model.Writes);
            grid.ElementFactory = new A6Factory(); grid.Template = A1TestHost.Template(); window.UpdateLayout();
            Assert.Same(edit, grid.EditSession); Assert.Equal(identity, peer.GetAutomationId());
            Assert.Contains("名称不能为空", peer.GetHelpText()); Assert.Equal("", value.Value); Assert.Equal("original", model.Name);
            value.SetValue("recovered");
            Assert.Equal("recovered", model.Name); Assert.Equal(1, model.Writes); Assert.Null(grid.EditSession);
            Assert.DoesNotContain("名称不能为空", peer.GetHelpText());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Same_parent_batch_move_updates_existing_peer_positions_and_keeps_provider_selection()
    {
        var items = new TreeDataGridRowMoveCollection<A6Model> { new("a"), new("b"), new("c"), new("d") };
        using var source = new FlatTreeDataGridSource<A6Model>(items); var grid = A6Host.Create(source); grid.SelectionUnit = TreeDataGridSelectionUnit.Row;
        var window = A1TestHost.Show(grid);
        try
        {
            var peer = A6Host.Peer(grid); var first = source.Rows[0].Id; var second = source.Rows[1].Id;
            peer.GetRowPeer(first)!.GetProvider<ISelectionItemProvider>()!.Select();
            var rowPeer = peer.GetRowPeer(second)!; rowPeer.GetProvider<ISelectionItemProvider>()!.AddToSelection();
            var identity = rowPeer.GetAutomationId(); grid.State!.SetCurrentCell(new(second, grid.Columns[0].Id));
            Assert.True(grid.TryMoveRows([second, first], source.Rows[3].Id, TreeDataGridRowDropPosition.After).IsCommitted);
            grid.ElementFactory = new A6Factory(); window.UpdateLayout();
            Assert.Equal(new[] { "c", "d", "a", "b" }, items.Select(item => item.Name));
            Assert.Equal(identity, rowPeer.GetAutomationId()); Assert.Contains("rowIndex=3", rowPeer.GetItemStatus());
            Assert.True(rowPeer.GetProvider<ISelectionItemProvider>()!.IsSelected); Assert.Equal(2, peer.GetSelection().Count);
            Assert.Equal(second, grid.CurrentCell!.Value.Node);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Cross_parent_move_invalidates_old_descendant_peer_and_exposes_the_restored_identity()
    {
        var child = new A6Model("child"); var moving = new A6Model("moving"); moving.Children.Add(child);
        var parent = new A6Model("parent"); parent.Children.Add(moving); parent.Children.Add(new("sibling"));
        var target = new A6Model("target"); target.Children.Add(new("existing"));
        using var source = new HierarchicalTreeDataGridSource<A6Model>(new TreeDataGridRowMoveCollection<A6Model> { parent, target }, model => model.Children);
        source.ExpandAll(); var grid = A6Host.Create(source); grid.SelectionUnit = TreeDataGridSelectionUnit.Row;
        var window = A1TestHost.Show(grid);
        try
        {
            NodeId Id(params int[] path) { Assert.True(source.TryGetNodeId(new IndexPath(path), out var id)); return id; }
            var oldChild = Id(0, 0, 0); var oldRoot = Id(0, 0); var oldSibling = Id(0, 1); var column = grid.Columns[0].Id;
            var peer = A6Host.Peer(grid); var retired = peer.GetCellPeer(oldChild, column)!;
            grid.State!.Select(oldRoot); grid.State.Select(oldChild, true); grid.State.Select(oldSibling, true); grid.State.SetCurrentCell(new(oldChild, column));
            var result = grid.TryMoveRows([oldRoot, oldChild, oldSibling], Id(1), TreeDataGridRowDropPosition.Inside);
            Assert.True(result.IsCommitted); grid.ElementFactory = new A6Factory(); window.UpdateLayout();
            Assert.True(retired.IsOffscreen()); Assert.Throws<InvalidOperationException>(() => retired.GetProvider<IValueProvider>()!.SetValue("stale"));
            Assert.Equal(0, child.Writes); Assert.Equal(result.IdentityMap[oldChild], grid.CurrentCell!.Value.Node);
            Assert.Equal(3, peer.GetSelection().Count);
            peer.GetCellPeer(result.IdentityMap[oldChild], column)!.GetProvider<IValueProvider>()!.SetValue("moved");
            Assert.Equal("moved", child.Name); Assert.Equal(1, child.Writes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Header_visibility_reorder_factory_and_disabled_state_keep_provider_actions_consistent()
    {
        var model = new A6Model("first"); using var source = new FlatTreeDataGridSource<A6Model>([model]);
        var grid = A6Host.Create(source, 2); grid.FrozenColumnCount = 0; var window = A1TestHost.Show(grid);
        try
        {
            var peer = A6Host.Peer(grid); var column = grid.Columns[1]; var header = peer.GetColumnHeaderPeer(column.Id)!; var identity = header.GetAutomationId();
            grid.ShowColumnHeaders = false; window.UpdateLayout();
            Assert.DoesNotContain(peer.GetChildren()!, child => child.GetAutomationControlType() == AutomationControlType.HeaderItem);
            Assert.ThrowsAny<Exception>(() => header.GetProvider<IInvokeProvider>()!.Invoke()); Assert.Null(source.SortColumn);
            grid.ShowColumnHeaders = true; Assert.True(grid.TryReorderColumn(column.Id, 0)); grid.ElementFactory = new A6Factory(); window.UpdateLayout();
            Assert.Equal(identity, header.GetAutomationId()); Assert.Contains("columnIndex=0", header.GetItemStatus());
            header.GetProvider<IInvokeProvider>()!.Invoke(); Assert.Equal(column.Id, source.SortColumn);
            var value = peer.GetCellPeer(source.Rows[0].Id, column.Id)!.GetProvider<IValueProvider>()!;
            grid.IsReadOnly = true; Assert.True(value.IsReadOnly); Assert.Throws<InvalidOperationException>(() => value.SetValue("readonly"));
            grid.IsReadOnly = false; grid.IsEnabled = false; Assert.ThrowsAny<Exception>(() => value.SetValue("disabled")); Assert.Equal(0, model.Writes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Offscreen_identity_survives_filter_factory_and_column_visibility_without_selecting_another_cell()
    {
        var models = Enumerable.Range(0, 1000).Select(index => new A6Model("row " + index)).ToArray();
        using var source = new FlatTreeDataGridSource<A6Model>(models); var grid = A6Host.Create(source, 40); var window = A1TestHost.Show(grid);
        try
        {
            var peer = A6Host.Peer(grid); var address = new CellAddress(source.Rows[^1].Id, grid.Columns[^1].Id);
            var logical = peer.GetCellPeer(address.Node, address.Column)!; var identity = logical.GetAutomationId(); Assert.True(logical.IsOffscreen());
            var current = grid.CurrentCell; source.SetFilter(model => ReferenceEquals(model, models[0])); window.UpdateLayout();
            Assert.Throws<InvalidOperationException>(() => logical.BringIntoView());
            source.SetFilter(null); grid.ElementFactory = new A6Factory(); window.UpdateLayout(); logical.BringIntoView(); window.UpdateLayout();
            Assert.False(logical.IsOffscreen()); Assert.Equal(identity, logical.GetAutomationId()); Assert.Equal(current, grid.CurrentCell); Assert.Empty(peer.GetSelection());
            grid.Columns[^1].IsVisible = false; window.UpdateLayout();
            Assert.Throws<InvalidOperationException>(() => logical.GetProvider<IValueProvider>()!.SetValue("hidden")); Assert.Equal(0, models[^1].Writes);
        }
        finally { window.Close(); }
    }
}

internal static class A6Host
{
    internal static GridControl Create(TreeDataGridSource source, int columns = 4, TreeDataGridElementFactory? factory = null, bool controlled = true)
    {
        var grid = new GridControl { Source = source, ElementFactory = factory, Template = A1TestHost.Template(), FrozenColumnCount = 1,
            SelectionUnit = TreeDataGridSelectionUnit.Cell, SelectionMode = TreeDataGridSelectionMode.Multiple, CanUserMoveRows = true, CanUserReorderColumns = true };
        for (var index = 0; index < columns; index++)
            grid.Columns.Add(new TreeDataGridTextColumn { Header = "C" + index, Width = new GridLength(110),
                Binding = controlled ? TreeDataGridBinding.Create<A6Model, string>(model => model.Name) : new ReflectionBinding(nameof(A6Model.Name)),
                ValueValidator = value => string.IsNullOrWhiteSpace(value?.ToString()) ? "名称不能为空" : null });
        return grid;
    }
    internal static TreeDataGridAutomationPeer Peer(GridControl grid) => Assert.IsType<TreeDataGridAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(grid));
}

public sealed class A6Model(string name) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed; private string _name = name;
    public string Name { get => _name; set { _name = value; Writes++; _changed?.Invoke(this, new(nameof(Name))); } }
    public TreeDataGridRowMoveCollection<A6Model> Children { get; } = [];
    public int Writes { get; private set; }
    public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}

internal sealed class A6Factory : TreeDataGridElementFactory
{
    internal readonly HashSet<Control> Active = [];
    private readonly Dictionary<Control, (A6Model Model, PropertyChangedEventHandler Handler)> _subscriptions = [];
    internal int Created, Released;
    internal int Subscriptions => _subscriptions.Count;
    internal Action<TreeDataGridElementContext>? Prepared;
    public override Control CreateElement(GridControl owner, TreeDataGridElementKind kind) { Created++; return base.CreateElement(owner, kind); }
    public override void PrepareElement(TreeDataGridElementContext context)
    {
        Assert.True(Active.Add(context.Element));
        if (context.Kind == TreeDataGridElementKind.Cell && context.Row!.Model is A6Model model)
        {
            PropertyChangedEventHandler handler = (_, _) => { }; _subscriptions.Add(context.Element, (model, handler)); model.PropertyChanged += handler;
        }
        Prepared?.Invoke(context);
    }
    public override void ClearElement(TreeDataGridElementContext context)
    {
        Assert.True(Active.Remove(context.Element));
        if (_subscriptions.Remove(context.Element, out var subscription)) subscription.Model.PropertyChanged -= subscription.Handler;
    }
    public override void ReleaseElement(GridControl owner, TreeDataGridElementKind kind, Control element)
    { Released++; Assert.DoesNotContain(element, Active); Assert.Null(element.Parent); Assert.Null(element.DataContext); }
}
