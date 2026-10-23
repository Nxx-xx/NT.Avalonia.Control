using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class ReadReviewTests
{
    [Fact]
    public void SameParentMoveResultKeepsMovedIdentityWhenProjectionObserverMovesDuplicateAgain()
    {
        var repeated = new Item("repeated");
        var items = new ObservableCollection<Item> { repeated, new("middle"), repeated };
        using var source = new FlatTreeDataGridSource<Item>(items);
        var moving = source.Rows[0].Id;
        var otherOccurrence = source.Rows[2].Id;
        var observed = false;
        source.ProjectionChanged += (_, _) =>
        {
            if (observed) return;
            observed = true;
            items.Move(2, 1);
        };
        var result = source.MoveRow(moving, otherOccurrence, TreeDataGridRowDropPosition.After);
        Assert.True(result.IsCommitted);
        Assert.Equal(moving, source.Rows[1].Id);
        Assert.Equal(otherOccurrence, source.Rows[2].Id);
        Assert.Equal(moving, result.NodeId);
    }

    [Fact]
    public void CrossParentMoveResultCannotReturnAnExistingDuplicateAfterObserverMovesInsertedOccurrence()
    {
        var repeated = new TreeItem("repeated");
        var a = new TreeItem("a") { Children = new TreeDataGridRowMoveCollection<TreeItem> { repeated } };
        var b = new TreeItem("b") { Children = new TreeDataGridRowMoveCollection<TreeItem> { repeated } };
        using var source = new HierarchicalTreeDataGridSource<TreeItem>(new TreeDataGridRowMoveCollection<TreeItem> { a, b }, item => item.Children);
        Assert.True(source.TryGetNodeId(new IndexPath(0, 0), out var moving));
        Assert.True(source.TryGetNodeId(new IndexPath(1, 0), out var existingDuplicate));
        var observed = false;
        source.ProjectionChanged += (_, _) =>
        {
            if (observed) return;
            observed = true;
            b.Children.Move(0, 1);
        };
        var result = source.MoveRow(moving, existingDuplicate, TreeDataGridRowDropPosition.Before);
        Assert.True(result.IsCommitted);
        Assert.True(source.TryGetNodeId(new IndexPath(1, 1), out var inserted));
        Assert.True(source.TryGetNodeId(new IndexPath(1, 0), out var stationary));
        Assert.Equal(existingDuplicate, stationary);
        Assert.NotEqual(existingDuplicate, result.NodeId);
        // If an observer changed the fresh identity's location, the adapter may retain
        // that identity or omit it; it must never report the other repeated occurrence.
        Assert.True(result.NodeId is null || result.NodeId == inserted);
    }

    [AvaloniaFact]
    public void DisablingRowMoveDuringRequestRejectsDefaultMove()
    {
        var items = new ObservableCollection<Item> { new("a"), new("b"), new("c") };
        using var source = new FlatTreeDataGridSource<Item>(items);
        var grid = Create(source);
        var original = items.ToArray();
        grid.RowMoveRequested += (_, _) => grid.CanUserMoveRows = false;
        var window = Show(grid);
        try
        {
            var result = grid.TryMoveRow(source.Rows[0].Id, source.Rows[2].Id, TreeDataGridRowDropPosition.After);
            Assert.False(result.IsCommitted);
            Assert.Equal(original, items);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisablingRowMoveDuringEditCommitRejectsDefaultMove()
    {
        var items = new ObservableCollection<Item> { new("a"), new("b"), new("c") };
        using var source = new FlatTreeDataGridSource<Item>(items);
        var grid = Create(source);
        var column = grid.Columns[0];
        column.ValueSetter = (model, value) => { ((Item)model).Name = (string)value!; grid.CanUserMoveRows = false; };
        var original = items.ToArray();
        var window = Show(grid);
        try
        {
            Assert.True(grid.BeginEdit(new CellAddress(source.Rows[0].Id, column.Id)));
            grid.State!.EditSession!.Value = "committed";
            var result = grid.TryMoveRow(source.Rows[0].Id, source.Rows[2].Id, TreeDataGridRowDropPosition.After);
            Assert.False(result.IsCommitted);
            Assert.Equal(original, items);
            Assert.Equal("committed", items[0].Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReplacedNestedObjectRejectsOldDraftWithoutWritingEitherObject()
    {
        var originalNested = new NestedValue("original");
        var model = new NestedRoot(originalNested);
        var replacementNested = new NestedValue("replacement");
        using var source = new FlatTreeDataGridSource<NestedRoot>([model]);
        var grid = CreateNestedGrid(source);
        var window = Show(grid);
        try
        {
            Assert.True(grid.BeginEdit(new CellAddress(source.Rows[0].Id, grid.Columns[0].Id)));
            grid.State!.EditSession!.Value = "old draft";
            model.Nested = replacementNested;
            Assert.False(grid.CommitEdit());
            Assert.Equal("original", originalNested.Name);
            Assert.Equal("replacement", replacementNested.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NestedGetterReplacingSourceBeforeLeafSetterDoesNotWriteOldDraft()
    {
        var originalNested = new NestedValue("original");
        var model = new NestedRoot(originalNested);
        var replacementNested = new NestedValue("replacement");
        using var source = new FlatTreeDataGridSource<NestedRoot>([model]);
        using var replacement = new FlatTreeDataGridSource<NestedRoot>([new NestedRoot(replacementNested)]);
        var grid = CreateNestedGrid(source);
        var window = Show(grid);
        try
        {
            Assert.True(grid.BeginEdit(new CellAddress(source.Rows[0].Id, grid.Columns[0].Id)));
            grid.State!.EditSession!.Value = "old draft";
            model.Read = () => grid.Source = replacement;
            Assert.False(grid.CommitEdit());
            Assert.Same(replacement, grid.Source);
            Assert.Equal("original", originalNested.Name);
            Assert.Equal("replacement", replacementNested.Name);
        }
        finally { window.Close(); }
    }

    private static GridControl CreateNestedGrid(TreeDataGridSource source)
    {
        var grid = Create(source);
        grid.Columns.Clear();
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "Nested name", Width = new GridLength(180), ValueType = typeof(string), Binding = new ReflectionBinding("Nested.Name") });
        return grid;
    }

    private static GridControl Create(TreeDataGridSource source)
    {
        var grid = new GridControl { Source = source, CanUserMoveRows = true,
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter();
                var scroll = new ScrollViewer { Content = rows };
                scope.Register("PART_RowsPresenter", rows);
                scope.Register("PART_ScrollViewer", scroll);
                return scroll;
            }) };
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "Name", Width = new GridLength(140), ValueType = typeof(string), ValueGetter = model => ((Item)model).Name, ValueSetter = (model, value) => ((Item)model).Name = (string)value! });
        return grid;
    }
    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 240, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }
    private sealed class Item(string name)
    {
        public string Name { get; set; } = name;
    }
    private sealed class TreeItem(string name)
    {
        public string Name { get; } = name;
        public TreeDataGridRowMoveCollection<TreeItem> Children { get; init; } = [];
    }
    public sealed class NestedValue(string name)
    {
        public string Name { get; set; } = name;
    }
    public sealed class NestedRoot(NestedValue nested) : INotifyPropertyChanged
    {
        private NestedValue _nested = nested;
        public Action? Read { get; set; }
        public NestedValue Nested
        {
            get { Read?.Invoke(); return _nested; }
            set { _nested = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nested))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
