using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class A4RowMoveTests
{
    [AvaloniaFact]
    public void Routed_drag_events_precede_edit_and_legacy_transaction_and_bubble_to_parent()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source);
        var order = new List<string>(); var column = grid.Columns[1];
        column.ValueSetter = (_, _) => order.Add("write"); var window = A1TestHost.Show(grid);
        grid.RowDragStarted += (_, args) => { order.Add("start"); Assert.Single(args.Rows); Assert.Same(source, args.DataSource); };
        grid.RowDragOver += (_, _) => order.Add("over");
        grid.RowDrop += (_, _) => { Assert.DoesNotContain("write", order); order.Add("drop"); };
        window.AddHandler(GridControl.RowDropEvent, (_, _) => order.Add("bubble"));
        grid.RowMoveRequested += (_, _) => order.Add("request"); grid.RowMoveCompleted += (_, _) => order.Add("complete");
        try
        {
            var node = source.Rows[0].Id; Assert.True(grid.BeginEdit(new(node, column.Id))); grid.EditSession!.Value = "draft";
            Drag(window, grid); Assert.Equal("start", order[0]);
            Assert.True(order.IndexOf("drop") < order.IndexOf("bubble")); Assert.True(order.IndexOf("bubble") < order.IndexOf("write"));
            Assert.True(order.IndexOf("write") < order.IndexOf("request")); Assert.Equal("complete", order[^1]);
            Assert.Equal(node, source.Rows[^1].Id); Assert.Equal(1, order.Count(value => value == "write"));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("start")]
    [InlineData("over")]
    [InlineData("drop")]
    [InlineData("handled")]
    [InlineData("copy")]
    public void Lifecycle_veto_keeps_draft_and_storage_without_entering_move_request(string veto)
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source);
        var writes = 0; var requests = 0; grid.Columns[1].ValueSetter = (_, _) => writes++; grid.RowMoveRequested += (_, _) => requests++;
        if (veto == "start") grid.RowDragStarted += (_, args) => args.Cancel = true;
        if (veto == "over") grid.RowDragOver += (_, args) => args.Cancel = true;
        grid.RowDrop += (_, args) => { if (veto == "drop") args.Cancel = true; if (veto == "handled") args.Handled = true; if (veto == "copy") args.AllowedEffects = DragDropEffects.Copy; };
        var window = A1TestHost.Show(grid);
        try
        {
            var ids = source.Rows.Select(row => row.Id).ToArray(); grid.BeginEdit(new(ids[0], grid.Columns[1].Id));
            var edit = grid.EditSession!; edit.Value = "draft"; Drag(window, grid);
            Assert.Equal(0, writes); Assert.Equal(0, requests); Assert.Same(edit, grid.EditSession);
            Assert.Equal(ids, source.Rows.Select(row => row.Id));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Handled_drop_can_own_one_transaction_and_skips_default_and_legacy_requests()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source); var requests = 0;
        grid.RowMoveRequested += (_, _) => requests++;
        grid.RowDrop += (_, args) => { args.Handled = true; Assert.True(source.MoveRow(args.Rows[0].Id, args.Target!.Id, args.Position).IsCommitted); };
        var window = A1TestHost.Show(grid);
        try { var node = source.Rows[0].Id; Drag(window, grid); Assert.Equal(node, source.Rows[^1].Id); Assert.Equal(0, requests); }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("source")]
    [InlineData("columns")]
    [InlineData("permission")]
    [InlineData("handle")]
    [InlineData("throw")]
    public void Drop_callback_changes_or_exception_never_runs_a_stale_default_transaction(string action)
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); using var replacement = new FlatTreeDataGridSource<Item>(Items());
        var grid = Create(source); var window = A1TestHost.Show(grid); IPointer? pointer = null;
        grid.AddHandler(InputElement.PointerMovedEvent, (_, args) => pointer = args.Pointer, handledEventsToo: true);
        grid.RowDrop += (_, _) =>
        {
            if (action == "source") grid.Source = replacement;
            else if (action == "columns") grid.Columns.Clear();
            else if (action == "permission") { grid.CanUserMoveRows = false; grid.CanUserMoveRows = true; }
            else if (action == "handle") { grid.RowDragHandle = TreeDataGridRowDragHandle.WholeRow; grid.RowDragHandle = TreeDataGridRowDragHandle.RowHeader; }
            else throw new InvalidOperationException("drop");
        };
        try
        {
            if (action == "throw") Assert.Throws<InvalidOperationException>(() => Drag(window, grid)); else Drag(window, grid);
            Assert.Equal(new[] { "a", "b", "c", "d" }, items.Select(item => item.Name)); Assert.Null(pointer?.Captured);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Selected_pointer_batch_preserves_selection_until_drop_and_uses_one_batch_request()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source); var window = A1TestHost.Show(grid);
        var requests = 0; var singles = 0; grid.RowsMoveRequested += (_, args) => { requests++; Assert.Equal(2, args.Rows.Count); };
        grid.RowMoveRequested += (_, _) => singles++;
        try
        {
            var first = source.Rows[0].Id; var second = source.Rows[1].Id;
            grid.State!.Select(first); grid.State.Select(second, true); grid.State.SetCurrentCell(new(second, grid.Columns[1].Id));
            grid.RowDragStarted += (_, _) => Assert.Equal(2, grid.State.SelectedNodes.Count);
            Drag(window, grid); Assert.Equal(1, requests); Assert.Equal(0, singles);
            Assert.Equal(new[] { "c", "d", "a", "b" }, items.Select(item => item.Name));
            Assert.Equal(new[] { first, second }.OrderBy(id => id.Value), grid.State.SelectedNodes.OrderBy(id => id.Value));
            Assert.Equal(second, grid.CurrentCell!.Value.Node);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Cross_parent_batch_restores_descendant_current_and_selection_from_the_identity_map()
    {
        var parent = new Item("parent"); var first = new Item("first"); first.Children.Add(new("child")); parent.Children.Add(first); parent.Children.Add(new("second"));
        var target = new Item("target"); target.Children.Add(new("existing")); using var source = new HierarchicalTreeDataGridSource<Item>(new TreeDataGridRowMoveCollection<Item> { parent, target }, item => item.Children);
        source.SetExpanded(Id(source, 0), true); source.SetExpanded(Id(source, 0, 0), true); source.SetExpanded(Id(source, 1), true);
        var grid = Create(source); var window = A1TestHost.Show(grid);
        try
        {
            var root = Id(source, 0, 0); var child = Id(source, 0, 0, 0); var second = Id(source, 0, 1);
            grid.State!.Select(root); grid.State.Select(child, true); grid.State.Select(second, true); grid.State.SetCurrentCell(new(child, grid.Columns[1].Id));
            var result = grid.TryMoveRows([second, child, root], Id(source, 1), TreeDataGridRowDropPosition.Inside);
            Assert.True(result.IsCommitted); Assert.Equal(3, grid.State.SelectedNodes.Count);
            Assert.Equal(result.IdentityMap[child], grid.CurrentCell!.Value.Node);
            Assert.All(new[] { root, child, second }, old => Assert.Contains(result.IdentityMap[old], grid.State.SelectedNodes));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ItemsSource_uses_original_collection_capabilities_and_reconciles_throwing_observers(bool transaction)
    {
        var values = new[] { new Item("a"), new Item("b"), new Item("c") };
        System.Collections.IEnumerable items = transaction ? new TreeDataGridRowMoveCollection<Item>(values) : new ObservableCollection<Item>(values);
        ((System.Collections.Specialized.INotifyCollectionChanged)items).CollectionChanged += (_, _) => throw new InvalidOperationException("observer before adapter");
        var grid = Create(null); grid.ItemsSource = items; var window = A1TestHost.Show(grid);
        try
        {
            var source = grid.Source!; var first = source.Rows[0].Id; var last = source.Rows[2].Id;
            var result = grid.TryMoveRow(first, last, TreeDataGridRowDropPosition.After);
            Assert.Equal(TreeDataGridRowMoveStatus.CommittedWithObserverError, result.Status); Assert.Equal(first, source.Rows[^1].Id);
            Assert.Same(values[0], items.Cast<Item>().Last()); Assert.False(source.IsRefreshDeferred);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ItemsSource_compiled_hierarchy_moves_transaction_collections_and_rejects_plain_list()
    {
        var parent = new Item("parent"); parent.Children.Add(new("one")); parent.Children.Add(new("two")); var target = new Item("target");
        var grid = Create(null); grid.Columns[1] = new TreeDataGridHierarchicalExpanderColumn
        { ChildrenBinding = TreeDataGridBinding.Create<Item, TreeDataGridRowMoveCollection<Item>>(item => item.Children), InnerColumn = new TreeDataGridTextColumn() };
        grid.ItemsSource = new TreeDataGridRowMoveCollection<Item> { parent, target }; var window = A1TestHost.Show(grid);
        try
        {
            var source = grid.Source!; source.SetExpanded(Id(source, 0), true); source.SetExpanded(Id(source, 1), true);
            var result = grid.TryMoveRows([Id(source, 0, 0), Id(source, 0, 1)], Id(source, 1), TreeDataGridRowDropPosition.Inside);
            Assert.True(result.IsCommitted); Assert.Empty(parent.Children); Assert.Equal(2, target.Children.Count);
            grid.ItemsSource = new List<Item> { parent, target };
            Assert.Equal(TreeDataGridRowMoveStatus.Unsupported, grid.TryMoveRow(grid.Source!.Rows[0].Id, grid.Source.Rows[1].Id, TreeDataGridRowDropPosition.After).Status);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Whole_row_gesture_is_opt_in_and_template_input_keeps_its_pointer()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source); var window = A1TestHost.Show(grid);
        try
        {
            var first = source.Rows[0].Id; Drag(window, grid, column: 1); Assert.Equal(first, source.Rows[0].Id);
            grid.RowDragHandle = TreeDataGridRowDragHandle.WholeRow; Drag(window, grid, column: 1); Assert.Equal(first, source.Rows[^1].Id);
            var started = 0; grid.RowDragStarted += (_, _) => started++;
            grid.Columns.Add(new TreeDataGridTemplateColumn { CellTemplate = new FuncDataTemplate<Item>((_, _) => new TextBox { Text = "native" }), Width = new GridLength(180) });
            window.UpdateLayout(); Drag(window, grid, column: 2); Assert.Equal(0, started);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Batch_cancel_stale_and_committed_handler_errors_have_distinct_outcomes()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source); var window = A1TestHost.Show(grid);
        try
        {
            var nodes = source.Rows.Take(2).Select(row => row.Id).ToArray(); var target = source.Rows[^1].Id;
            EventHandler<TreeDataGridRowsMoveRequestedEventArgs> cancel = (_, args) => args.Cancel = true;
            grid.RowsMoveRequested += cancel; Assert.Equal(TreeDataGridRowMoveStatus.Cancelled, grid.TryMoveRows(nodes, target, TreeDataGridRowDropPosition.After).Status); grid.RowsMoveRequested -= cancel;
            grid.RowsMoveRequested += (_, args) =>
            {
                Assert.Equal(TreeDataGridRowMoveStatus.Rejected, grid.TryMoveRows(nodes, target, TreeDataGridRowDropPosition.After).Status);
                args.Handled = true; args.Result = source.MoveRows(nodes, target, TreeDataGridRowDropPosition.After); args.Cancel = true;
                throw new InvalidOperationException("after commit");
            };
            Assert.Equal(TreeDataGridRowMoveStatus.CommittedWithObserverError, grid.TryMoveRows(nodes, target, TreeDataGridRowDropPosition.After).Status);
            Assert.Equal(new[] { "c", "d", "a", "b" }, items.Select(item => item.Name));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Batch_validates_edit_and_rechecks_request_source_without_partial_storage_changes()
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source); var window = A1TestHost.Show(grid);
        try
        {
            var nodes = source.Rows.Take(2).Select(row => row.Id).ToArray(); var target = source.Rows[^1].Id; var requests = 0; var writes = 0;
            grid.RowsMoveRequested += (_, _) => requests++; grid.Columns[1].ValueSetter = (_, _) => writes++; grid.Columns[1].ValueValidator = _ => "blocked";
            grid.BeginEdit(new(nodes[0], grid.Columns[1].Id));
            Assert.Equal(TreeDataGridRowMoveStatus.ValidationFailed, grid.TryMoveRows(nodes, target, TreeDataGridRowDropPosition.After).Status);
            Assert.Equal(0, writes); Assert.Equal(0, requests); grid.CancelEdit();
            grid.RowsMoveRequested += (_, _) => items.RemoveAt(2);
            Assert.Equal(TreeDataGridRowMoveStatus.Stale, grid.TryMoveRows(nodes, target, TreeDataGridRowDropPosition.After).Status);
            Assert.Equal(new[] { "a", "b", "d" }, items.Select(item => item.Name));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("start")]
    [InlineData("over")]
    public void Gesture_exceptions_release_capture_and_started_callback_cannot_reenter_pointer_move(string phase)
    {
        var items = Items(); using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(source); var window = A1TestHost.Show(grid);
        IPointer? pointer = null; window.AddHandler(InputElement.PointerPressedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Tunnel, true);
        grid.RowDragStarted += (_, _) => { if (phase == "start") throw new InvalidOperationException("start"); window.MouseMove(new(20, 20)); };
        grid.RowDragOver += (_, _) => { if (phase == "over") throw new InvalidOperationException("over"); };
        try { Assert.Throws<InvalidOperationException>(() => Drag(window, grid)); Assert.Null(pointer!.Captured); Assert.Equal("a", items[0].Name); }
        finally { window.Close(); }
    }

    private static TreeDataGridRowMoveCollection<Item> Items() => new([new("a"), new("b"), new("c"), new("d")]);
    private static GridControl Create(TreeDataGridSource? source)
    {
        var grid = new GridControl { Source = source, Template = A1TestHost.Template(), CanUserMoveRows = true,
            SelectionUnit = TreeDataGridSelectionUnit.Row, SelectionMode = TreeDataGridSelectionMode.Multiple };
        grid.Columns.Add(new TreeDataGridRowHeaderColumn { Width = new GridLength(50) });
        grid.Columns.Add(new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name, Width = new GridLength(220) });
        return grid;
    }
    private static void Drag(Window window, GridControl grid, int column = 0)
    {
        window.UpdateLayout(); var cells = grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().Where(cell => cell.ColumnIndex == column).OrderBy(cell => grid.Source!.GetViewIndex(cell.Row!.Id)).ToArray();
        var start = cells[0].TranslatePoint(new Point(20, 14), window)!.Value;
        var end = cells[^1].TranslatePoint(new Point(20, 26), window)!.Value;
        window.MouseDown(start, MouseButton.Left); window.MouseMove(end); window.MouseUp(end, MouseButton.Left); window.UpdateLayout();
    }
    private static NodeId Id(TreeDataGridSource source, params int[] path) { Assert.True(source.TryGetNodeId(new(path), out var id)); return id; }
    private sealed class Item(string name)
    { public string Name { get; } = name; public TreeDataGridRowMoveCollection<Item> Children { get; } = []; }
}
