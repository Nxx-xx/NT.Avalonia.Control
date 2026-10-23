using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class RowDragAdvancedTests
{
    [AvaloniaFact]
    public async Task Inside_hover_expands_collapsed_target_and_cross_parent_drop_selects_the_new_node_identity()
    {
        var moving = new Item("moving"); var existingChild = new Item("existing child");
        var origin = new Item("origin"); origin.Children.Add(moving);
        var target = new Item("target"); target.Children.Add(existingChild);
        var roots = new TreeDataGridRowMoveCollection<Item> { origin, target };
        using var source = new HierarchicalTreeDataGridSource<Item>(roots, item => item.Children);
        var originId = Id(source, 0); var oldNode = Id(source, 0, 0); var targetId = Id(source, 1); var existingId = Id(source, 1, 0);
        source.SetExpanded(originId, true);
        var grid = Create(source, tree: true); var window = Show(grid);
        IPointer? pointer = null;
        grid.AddHandler(InputElement.PointerMovedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var requested = 0; TreeDataGridRowMoveCompletedEventArgs? completed = null;
        grid.RowMoveRequested += (_, _) => requested++;
        grid.RowMoveCompleted += (_, args) => completed = args;
        try
        {
            Assert.True(source.TryGetRow(targetId, out var collapsed)); Assert.False(collapsed!.IsExpanded);
            Assert.Equal(-1, source.GetViewIndex(existingId));
            grid.State!.Select(oldNode);
            var start = RowHeader(grid, oldNode).TranslatePoint(new Point(20, 14), window)!.Value;
            var targetHeader = RowHeader(grid, targetId);
            var inside = targetHeader.TranslatePoint(new Point(20, targetHeader.Bounds.Height / 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(inside);
            Assert.NotNull(pointer); Assert.Same(grid, pointer.Captured);
            Assert.Equal(0, requested);
            await Task.Delay(650, TestContext.Current.CancellationToken);
            await WaitForAsync(window, () => source.TryGetRow(targetId, out var row) && row!.IsExpanded);
            Assert.True(source.TryGetRow(targetId, out var expanded)); Assert.True(expanded!.IsExpanded);
            Assert.True(source.GetViewIndex(existingId) >= 0);
            Assert.Equal(0, requested); Assert.Same(grid, pointer.Captured);

            window.MouseUp(inside, MouseButton.Left);
            window.UpdateLayout();
            var newNode = Id(source, 1, 1);
            Assert.Equal(1, requested); Assert.NotNull(completed);
            Assert.Equal(TreeDataGridRowDropPosition.Inside, completed.Position);
            Assert.Equal(TreeDataGridRowMoveStatus.Committed, completed.Result.Status);
            Assert.Equal(newNode, completed.Result.NodeId); Assert.NotEqual(oldNode, newNode);
            Assert.False(source.TryGetRow(oldNode, out _)); Assert.Empty(origin.Children);
            Assert.Equal(new[] { existingChild, moving }, target.Children);
            Assert.Equal(existingId, Id(source, 1, 0)); Assert.Equal(targetId, Id(source, 1));
            Assert.Equal(newNode, grid.CurrentCell!.Value.Node); Assert.Contains(newNode, grid.State.SelectedNodes);
            Assert.Null(pointer.Captured);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Bottom_edge_drag_scrolls_after_origin_row_recycling_and_same_parent_drop_keeps_exact_identity()
    {
        var items = new TreeDataGridRowMoveCollection<Item>(Enumerable.Range(0, 200).Select(index => new Item($"row {index:D3}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        var initialIds = source.Rows.Select(row => row.Id).ToArray(); var moving = items[0]; var node = initialIds[0];
        var grid = Create(source, tree: false); var window = Show(grid);
        IPointer? pointer = null;
        grid.AddHandler(InputElement.PointerMovedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        TreeDataGridRowMoveCompletedEventArgs? completed = null;
        grid.RowMoveCompleted += (_, args) => completed = args;
        try
        {
            var presenter = grid.RowsPresenter!; Assert.Equal(0, presenter.Offset.Y);
            var start = RowHeader(grid, node).TranslatePoint(new Point(20, 14), window)!.Value;
            var edge = presenter.TranslatePoint(new Point(20, presenter.Viewport.Height - 4), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(edge);
            Assert.NotNull(pointer); Assert.Same(grid, pointer.Captured);
            await Task.Delay(450, TestContext.Current.CancellationToken);
            await WaitForAsync(window, () => presenter.Offset.Y > grid.RowHeight * 2);
            Assert.True(presenter.Offset.Y > 0); Assert.Same(grid, pointer.Captured);
            Assert.DoesNotContain(presenter.RealizedRows, row => row.Row?.Id == node);
            Assert.Null(completed); Assert.Equal(initialIds, source.Rows.Select(row => row.Id));

            // Release after a fully visible target, away from the edge band, so flat rows use an After drop.
            var destination = presenter.RealizedRows.Where(row => row.Bounds.Y >= 28 && row.Bounds.Bottom <= presenter.Viewport.Height - 28)
                .OrderBy(row => row.RowIndex).Last();
            var targetId = destination.Row!.Id; var targetIndex = source.GetViewIndex(targetId);
            Assert.True(targetIndex > 2);
            var finish = RowHeader(grid, targetId).TranslatePoint(new Point(20, destination.Bounds.Height - 2), window)!.Value;
            window.MouseMove(finish);
            window.MouseUp(finish, MouseButton.Left);
            window.UpdateLayout();
            Assert.NotNull(completed); Assert.Equal(targetId, completed.TargetNodeId);
            Assert.Equal(TreeDataGridRowDropPosition.After, completed.Position);
            Assert.Equal(TreeDataGridRowMoveStatus.Committed, completed.Result.Status); Assert.Equal(node, completed.Result.NodeId);
            Assert.Equal(targetIndex, source.GetViewIndex(node)); Assert.Same(moving, items[targetIndex]);
            Assert.Equal(new IndexPath(targetIndex), source.Rows[targetIndex].ModelIndex);
            Assert.Equal(initialIds.OrderBy(id => id.Value), source.Rows.Select(row => row.Id).OrderBy(id => id.Value));
            Assert.Equal(node, grid.CurrentCell!.Value.Node); Assert.Contains(node, grid.State!.SelectedNodes);
            Assert.Null(pointer.Captured);
        }
        finally { window.Close(); }
    }

    private static async Task WaitForAsync(Window window, Func<bool> condition)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            window.UpdateLayout();
            if (condition()) return;
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        window.UpdateLayout(); Assert.True(condition(), "The row drag timer did not reach the expected state within the bounded wait.");
    }

    private static TreeDataGridCellPresenter RowHeader(GridControl grid, NodeId node) => grid.GetVisualDescendants()
        .OfType<TreeDataGridCellPresenter>().Single(cell => cell.Row?.Id == node && cell.Column is TreeDataGridRowHeaderColumn);

    private static GridControl Create(TreeDataGridSource source, bool tree)
    {
        var grid = new GridControl
        {
            Source = source, CanUserMoveRows = true, FrozenColumnCount = 1,
            SelectionUnit = TreeDataGridSelectionUnit.Row, SelectionMode = TreeDataGridSelectionMode.Multiple,
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter();
                var headers = new TreeDataGridColumnHeadersPresenter { Height = 28 };
                var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ColumnHeadersPresenter", headers); scope.Register("PART_ScrollViewer", scroll);
                Grid.SetRow(scroll, 1); return new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { headers, scroll } };
            }),
        };
        grid.Columns.Add(new TreeDataGridRowHeaderColumn { Header = "Drag", Width = new GridLength(64), IsReadOnly = true, CanUserSort = false });
        var text = new TreeDataGridTextColumn { Header = "Name", Width = new GridLength(260), ValueGetter = model => ((Item)model).Name };
        grid.Columns.Add(tree ? new TreeDataGridHierarchicalExpanderColumn { Header = "Tree", Width = new GridLength(260), InnerColumn = text } : text);
        return grid;
    }
    private static Window Show(Control content)
    {
        var window = new Window { Width = 420, Height = 280, Content = content };
        window.Show(); window.UpdateLayout(); return window;
    }
    private static NodeId Id(TreeDataGridSource source, params int[] path)
    { Assert.True(source.TryGetNodeId(new IndexPath(path), out var node)); return node; }
    private sealed class Item(string name)
    {
        public string Name { get; } = name;
        public TreeDataGridRowMoveCollection<Item> Children { get; } = [];
    }
}
