using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class ColumnReorderTests
{
    [AvaloniaFact]
    public void ReorderUsesVisibleSlotsAndPreservesIdentityWidthCurrentCellAndDraft()
    {
        var item = new Item("original");
        using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = Create(source);
        grid.Columns[1].IsVisible = false;
        var moved = grid.Columns[0];
        var originalColumns = grid.Columns.ToArray();
        TreeDataGridColumnReorderingEventArgs? request = null;
        TreeDataGridColumnReorderedEventArgs? completed = null;
        grid.ColumnReordering += (_, e) => request = e;
        grid.ColumnReordered += (_, e) => completed = e;
        var window = Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, moved.Id);
            Assert.True(grid.BeginEdit(address));
            var edit = grid.State!.EditSession!;
            edit.Value = "draft";
            var width = moved.Width;
            Assert.True(grid.TryReorderColumn(moved.Id, 2));
            window.UpdateLayout();
            Assert.Equal(new[] { originalColumns[1], originalColumns[2], originalColumns[3], moved }, grid.Columns);
            Assert.Equal(2, grid.GetColumnIndex(moved.Id));
            Assert.Equal(width, moved.Width);
            Assert.Same(edit, grid.State.EditSession);
            Assert.Equal("draft", edit.Value);
            Assert.Equal(address, grid.State.CurrentCell);
            Assert.Equal("original", item.Name);
            Assert.NotNull(request);
            Assert.NotNull(completed);
            Assert.Equal(moved.Id, completed.ColumnId);
            Assert.Equal(0, completed.OldCollectionIndex);
            Assert.Equal(3, completed.NewCollectionIndex);
            Assert.Equal(0, completed.OldDisplayIndex);
            Assert.Equal(2, completed.NewDisplayIndex);
            Assert.Equal(request.NewCollectionIndex, completed.NewCollectionIndex);
            Assert.True(grid.CommitEdit());
            Assert.Equal("draft", item.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void UserPermissionsAndFrozenBoundaryApplyToProgrammaticEntryPoint()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("item")]);
        var grid = Create(source);
        var column = grid.Columns[0];
        grid.CanUserReorderColumns = false;
        Assert.False(grid.TryReorderColumn(column.Id, 1));
        grid.CanUserReorderColumns = true;
        column.CanUserReorder = false;
        Assert.False(grid.TryReorderColumn(column.Id, 1));
        column.CanUserReorder = true;
        grid.FrozenColumnCount = 2;
        Assert.False(grid.TryReorderColumn(column.Id, 2));
        Assert.True(grid.TryReorderColumn(column.Id, 1));
        Assert.False(grid.TryReorderColumn(column.Id, -1));
        Assert.False(grid.TryReorderColumn(column.Id, 4));
        Assert.False(grid.TryReorderColumn(column.Id, 1));
        column.IsVisible = false;
        Assert.False(grid.TryReorderColumn(column.Id, 0));
        Assert.Equal(1, grid.Columns.IndexOf(column));
    }

    [AvaloniaFact]
    public void CancellationAndReentrantCallbacksCannotMoveStaleTarget()
    {
        foreach (var mutation in new[] { "cancel", "source", "projection", "collection", "visibility", "frozen", "permission", "column-permission" })
        {
            using var source = new FlatTreeDataGridSource<Item>([new("item")]);
            using var replacement = new FlatTreeDataGridSource<Item>([new("replacement")]);
            var grid = Create(source);
            var moved = grid.Columns[0];
            var window = Show(grid);
            try
            {
                var completed = 0;
                grid.ColumnReordered += (_, _) => completed++;
                grid.ColumnReordering += (_, e) =>
                {
                    Assert.False(grid.TryReorderColumn(moved.Id, 1));
                    switch (mutation)
                    {
                        case "cancel": e.Cancel = true; break;
                        case "source": grid.Source = replacement; break;
                        case "projection": source.SetFilter(_ => false); break;
                        case "collection": grid.Columns.Move(2, 3); break;
                        case "visibility": grid.Columns[1].IsVisible = false; break;
                        case "frozen": grid.FrozenColumnCount = 1; break;
                        case "permission": grid.CanUserReorderColumns = false; break;
                        case "column-permission": moved.CanUserReorder = false; break;
                    }
                };
                Assert.False(grid.TryReorderColumn(moved.Id, 2));
                Assert.Same(moved, grid.Columns[0]);
                Assert.Equal(0, completed);
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaFact]
    public void HeaderDragReordersWithoutSortingOrResizing()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("b"), new("a")]);
        var grid = Create(source);
        var order = grid.Columns.ToArray();
        var widths = order.Select(column => column.Width).ToArray();
        var window = Show(grid);
        try
        {
            var start = Header(grid, order[0]).TranslatePoint(new Point(30, 12), window)!.Value;
            var end = Header(grid, order[2]).TranslatePoint(new Point(60, 12), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end);
            window.MouseUp(end, MouseButton.Left);
            window.UpdateLayout();
            Assert.Equal(new[] { order[1], order[2], order[0], order[3] }, grid.Columns);
            Assert.Null(source.SortColumn);
            Assert.Equal(widths, order.Select(column => column.Width));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ResizeThumbAndSmallClickStaySeparateFromReorder()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("b"), new("a")]);
        var grid = Create(source);
        var order = grid.Columns.ToArray();
        var window = Show(grid);
        try
        {
            var thumb = Header(grid, order[0]).GetVisualDescendants().OfType<Thumb>().Single();
            var point = thumb.TranslatePoint(new Point(4, 12), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(12, 0));
            window.MouseUp(point + new Vector(12, 0), MouseButton.Left);
            window.UpdateLayout();
            Assert.Equal(order, grid.Columns);
            Assert.True(order[0].Width.Value > 120);
            Assert.Null(source.SortColumn);
            var click = Header(grid, order[0]).TranslatePoint(new Point(30, 12), window)!.Value;
            window.MouseDown(click, MouseButton.Left);
            window.MouseMove(click + new Vector(2, 0));
            window.MouseUp(click + new Vector(2, 0), MouseButton.Left);
            window.UpdateLayout();
            Assert.Equal(order, grid.Columns);
            Assert.Equal(order[0].Id, source.SortColumn);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EscapeAndInvalidationCancelDragWithoutCancellingDraft()
    {
        foreach (var mutation in new[] { "escape", "source", "columns", "template" })
        {
            using var source = new FlatTreeDataGridSource<Item>([new("item")]);
            using var replacement = new FlatTreeDataGridSource<Item>([new("replacement")]);
            var grid = Create(source);
            var order = grid.Columns.ToArray();
            var window = Show(grid);
            try
            {
                Assert.True(grid.BeginEdit(new CellAddress(source.Rows[0].Id, order[0].Id)));
                var edit = grid.State!.EditSession!;
                edit.Value = "draft";
                var completed = 0;
                grid.ColumnReordered += (_, _) => completed++;
                var start = Header(grid, order[0]).TranslatePoint(new Point(30, 12), window)!.Value;
                var end = Header(grid, order[2]).TranslatePoint(new Point(60, 12), window)!.Value;
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(end);
                switch (mutation)
                {
                    case "escape":
                        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                        window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                        Assert.Same(edit, grid.State.EditSession);
                        break;
                    case "source": grid.Source = replacement; break;
                    case "columns": grid.Columns[1].IsVisible = false; break;
                    case "template": grid.Template = Template(); grid.ApplyTemplate(); break;
                }
                window.MouseUp(end, MouseButton.Left);
                window.UpdateLayout();
                Assert.Equal(order, grid.Columns);
                Assert.Equal(0, completed);
                Assert.Null(source.SortColumn);
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaFact]
    public void LosingOwnerPointerCaptureCancelsReorder()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("item")]);
        var grid = Create(source);
        var order = grid.Columns.ToArray();
        IPointer? pointer = null;
        grid.AddHandler(InputElement.PointerMovedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var window = Show(grid);
        try
        {
            var start = Header(grid, order[0]).TranslatePoint(new Point(30, 12), window)!.Value;
            var end = Header(grid, order[2]).TranslatePoint(new Point(60, 12), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end);
            Assert.NotNull(pointer);
            Assert.Same(grid, pointer.Captured);
            pointer.Capture(null);
            window.MouseUp(end, MouseButton.Left);
            window.UpdateLayout();
            Assert.Equal(order, grid.Columns);
            Assert.Null(source.SortColumn);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task EdgeScrollKeepsDraggingAfterOriginHeaderIsRecycled()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("item")]);
        var grid = Create(source);
        for (var i = 4; i < 12; i++)
            grid.Columns.Add(new TreeDataGridTextColumn { Header = $"Column {i}", Width = new GridLength(120), ValueGetter = model => ((Item)model).Name });
        grid.FrozenColumnCount = 1;
        var moved = grid.Columns[1];
        var window = Show(grid);
        try
        {
            var headers = grid.GetVisualDescendants().OfType<TreeDataGridColumnHeadersPresenter>().Single();
            var start = Header(grid, moved).TranslatePoint(new Point(30, 12), window)!.Value;
            var end = headers.TranslatePoint(new Point(headers.Bounds.Width - 8, 12), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end);
            await Task.Delay(750);
            window.UpdateLayout();
            Assert.True(grid.RowsPresenter!.Offset.X > 240);
            Assert.DoesNotContain(grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>(), header => header.Column == moved);
            window.MouseUp(end, MouseButton.Left);
            window.UpdateLayout();
            Assert.True(grid.GetColumnIndex(moved.Id) > 1);
            Assert.Null(source.SortColumn);
        }
        finally { window.Close(); }
    }

    private static TreeDataGridColumnHeader Header(GridControl grid, TreeDataGridColumn column) =>
        grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Single(header => header.Column == column);
    private static GridControl Create(TreeDataGridSource source)
    {
        var grid = new GridControl { Source = source, CanUserReorderColumns = true, Template = Template(), SelectionUnit = TreeDataGridSelectionUnit.Cell };
        for (var i = 0; i < 4; i++)
            grid.Columns.Add(new TreeDataGridTextColumn
            {
                Header = $"Column {i}", Width = new GridLength(120), ValueType = typeof(string),
                ValueGetter = model => ((Item)model).Name, ValueSetter = (model, value) => ((Item)model).Name = (string)value!,
            });
        return grid;
    }
    private static FuncControlTemplate<GridControl> Template() => new((_, scope) =>
    {
        var rows = new TreeDataGridRowsPresenter();
        var headers = new TreeDataGridColumnHeadersPresenter();
        var scroll = new ScrollViewer { Content = rows };
        scope.Register("PART_RowsPresenter", rows);
        scope.Register("PART_ColumnHeadersPresenter", headers);
        scope.Register("PART_ScrollViewer", scroll);
        Grid.SetRow(scroll, 1);
        return new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { headers, scroll } };
    });
    private static Window Show(Control content)
    {
        var window = new Window { Width = 520, Height = 280, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }
    private sealed class Item(string name)
    {
        public string Name { get; set; } = name;
    }
}
