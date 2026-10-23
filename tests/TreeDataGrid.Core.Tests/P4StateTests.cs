using System.Collections.ObjectModel;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Core.Tests;

public sealed class P4StateTests
{
    [Fact]
    public void Selection_batches_notify_once_and_single_mode_replaces_additive_selection()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("a"), new("b"), new("c")]);
        using var state = new TreeDataGridState(source);
        var selected = 0; var changed = 0;
        state.SelectionChanged += (_, _) => selected++;
        state.Changed += (_, _) => changed++;
        using (state.DeferSelectionNotifications())
        {
            state.Select(source.Rows[0].Id);
            state.Select(source.Rows[1].Id, additive: true);
            state.Select(source.Rows[2].Id, additive: true);
        }
        Assert.Equal(1, selected); Assert.Equal(1, changed); Assert.Equal(3, state.SelectedNodes.Count);
        state.SelectionMode = TreeDataGridSelectionMode.Single;
        state.Select(source.Rows[0].Id); state.Select(source.Rows[1].Id, additive: true);
        Assert.Equal(source.Rows[1].Id, Assert.Single(state.SelectedNodes));
    }

    [Fact]
    public void Cell_rectangle_preserves_identities_and_contracts_if_sort_would_add_unselected_nodes()
    {
        var items = new[] { new Item("a"), new Item("b"), new Item("c") };
        using var source = new FlatTreeDataGridSource<Item>(items);
        using var state = new TreeDataGridState(source);
        ColumnId[] columns = [new(1), new(2), new(3)];
        var a = source.Rows[0].Id; var b = source.Rows[1].Id; var c = source.Rows[2].Id;
        state.SelectCellRange(new(a, columns[0]), new(b, columns[1]), columns);
        Assert.Equal(4, state.SelectedCells.Count());
        source.Sort(columns[0], (left, right) => Rank((Item)left).CompareTo(Rank((Item)right)), TreeDataGridSortDirection.Ascending);
        Assert.Equal(new CellAddress(b, columns[1]), Assert.Single(state.SelectedCells));
        Assert.False(state.IsCellSelected(new(c, columns[1])));
        static int Rank(Item item) => item.Name == "a" ? 0 : item.Name == "c" ? 1 : 2;
    }

    [Fact]
    public void Invalid_rectangle_column_order_does_not_damage_existing_selection()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("a"), new("b")]);
        using var state = new TreeDataGridState(source);
        var first = new CellAddress(source.Rows[0].Id, new(1));
        var last = new CellAddress(source.Rows[1].Id, new(2));
        state.SelectCellRange(first, last, [new(1), new(2)]);
        var original = state.SelectedCells.ToHashSet();
        Assert.Throws<ArgumentException>(() => state.SelectCellRange(first, last, [new(1), default, new(2)]));
        Assert.Throws<ArgumentException>(() => state.SelectCellRange(first, last, [new(1), new(1), new(2)]));
        Assert.True(original.SetEquals(state.SelectedCells));
    }

    [Fact]
    public void Draft_does_not_write_and_validation_then_commit_writes_once()
    {
        var item = new Item("old"); using var source = new FlatTreeDataGridSource<Item>([item]);
        using var state = new TreeDataGridState(source); var writes = 0;
        state.BeginEdit(new(source.Rows[0].Id, new(1)), x => ((Item)x).Name, (x, value) => { writes++; ((Item)x).Name = (string)value!; }, value => (string?)value == "bad" ? "invalid" : null);
        state.EditSession!.Value = "bad";
        Assert.Equal("old", item.Name); Assert.False(state.CommitEdit()); Assert.Equal(0, writes);
        Assert.Equal("invalid", state.EditSession.ValidationError);
        state.EditSession.Value = "new"; Assert.True(state.CommitEdit()); Assert.Equal(1, writes); Assert.Equal("new", item.Name);
    }

    [Fact]
    public void Error_notification_cannot_write_a_target_removed_by_its_listener()
    {
        var items = new ObservableCollection<Item>([new("old")]); using var source = new FlatTreeDataGridSource<Item>(items);
        using var state = new TreeDataGridState(source); var writes = 0; var invalid = true;
        state.BeginEdit(new(source.Rows[0].Id, new(1)), x => ((Item)x).Name, (_, _) => writes++, _ => invalid ? "invalid" : null);
        Assert.False(state.CommitEdit("new"));
        state.EditSession!.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TreeDataGridEditSession.ValidationError)) items.Clear(); };
        invalid = false;
        Assert.Throws<InvalidOperationException>(() => state.CommitEdit("new")); Assert.Equal(0, writes); Assert.Null(state.EditSession);
    }

    [Fact]
    public void Selection_changed_inside_commit_is_reported_once()
    {
        var items = new ObservableCollection<Item>([new("a"), new("b")]); using var source = new FlatTreeDataGridSource<Item>(items);
        using var state = new TreeDataGridState(source);
        state.Select(source.Rows[0].Id); state.Select(source.Rows[1].Id, true);
        var events = 0; state.SelectionChanged += (_, _) => events++;
        state.BeginEdit(new(source.Rows[0].Id, new(1)), x => ((Item)x).Name, (_, _) => items.RemoveAt(1));
        Assert.True(state.CommitEdit("value")); Assert.Single(state.SelectedNodes); Assert.Equal(1, events);
    }

    [Fact]
    public void Column_removal_cancels_its_editor_and_shrinks_cell_selection()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("a"), new("b")]);
        using var state = new TreeDataGridState(source); ColumnId[] columns = [new(1), new(2)];
        state.SelectCellRange(new(source.Rows[0].Id, columns[0]), new(source.Rows[1].Id, columns[1]), columns);
        state.BeginEdit(new(source.Rows[1].Id, columns[1]), x => ((Item)x).Name, (_, _) => { });
        state.SetColumns([columns[0]]);
        Assert.Null(state.EditSession); Assert.Null(state.CurrentCell); Assert.Equal(2, state.SelectedCells.Count());
    }

    private sealed class Item(string name) { public string Name { get; set; } = name; }
}
