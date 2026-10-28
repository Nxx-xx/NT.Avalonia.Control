using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class ColumnSortOptionsTests
{
    [AvaloniaFact]
    public void Direction_comparers_express_the_final_order_without_double_reversal_or_negating_min_value()
    {
        var items = new[] { new Ranked("a", 2), new Ranked("b", 1), new Ranked("c", 3), new Ranked("d", 2) };
        using var source = new FlatTreeDataGridSource<Ranked>(items);
        var column = Column();
        column.CompareAscending = (left, right) => string.CompareOrdinal(((Ranked)left).Name, ((Ranked)right).Name);
        column.CompareDescending = (left, right) => ((Ranked)left).Rank == ((Ranked)right).Rank ? 0
            : ((Ranked)left).Rank > ((Ranked)right).Rank ? int.MinValue : int.MaxValue;
        var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            Assert.True(grid.SortByColumn(column.Id, TreeDataGridSortDirection.Ascending));
            Assert.Equal(new[] { "a", "b", "c", "d" }, Names(source));
            Assert.True(grid.SortByColumn(column.Id, TreeDataGridSortDirection.Descending));
            window.UpdateLayout();
            Assert.Equal(new[] { "c", "a", "d", "b" }, Names(source));
            Assert.Equal(TreeDataGridSortDirection.Descending, source.SortDirection);
            Assert.Equal("▼", A1TestHost.Header(grid).SortIndicator);
            Assert.Equal(new[] { "a", "b", "c", "d" }, items.Select(item => item.Name));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Ascending_fallback_overrides_legacy_comparison_and_runtime_changes_take_effect_at_the_next_explicit_sort()
    {
        using var source = new FlatTreeDataGridSource<Ranked>([new("bbb", 0), new("a", 0), new("cc", 0)]);
        var column = Column();
        column.Comparison = (left, right) => -string.CompareOrdinal(((Ranked)left).Name, ((Ranked)right).Name);
        column.CompareAscending = (left, right) => ((Ranked)left).Name.Length.CompareTo(((Ranked)right).Name.Length);
        var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            grid.SortByColumn(column.Id, TreeDataGridSortDirection.Descending);
            Assert.Equal(new[] { "bbb", "cc", "a" }, Names(source));
            column.CompareAscending = (left, right) => string.CompareOrdinal(((Ranked)left).Name, ((Ranked)right).Name);
            source.Refresh();
            Assert.Equal(new[] { "bbb", "cc", "a" }, Names(source));
            grid.SortByColumn(column.Id, TreeDataGridSortDirection.Descending);
            Assert.Equal(new[] { "cc", "bbb", "a" }, Names(source));
            column.CompareAscending = null;
            grid.SortByColumn(column.Id, TreeDataGridSortDirection.Ascending);
            Assert.Equal(new[] { "cc", "bbb", "a" }, Names(source));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Column_sort_cycle_overrides_both_true_and_false_global_values_and_null_inherits()
    {
        using var source = new FlatTreeDataGridSource<Ranked>([new("b", 0), new("a", 0)]);
        var column = Column(); column.AllowTriStateSorting = false;
        var grid = A1TestHost.Create(source, column); grid.IsThreeStateSort = true;
        var window = A1TestHost.Show(grid);
        try
        {
            SortKey(); SortKey(); SortKey();
            Assert.Equal(TreeDataGridSortDirection.Ascending, source.SortDirection);
            column.AllowTriStateSorting = null;
            SortKey(); SortKey();
            Assert.Equal(TreeDataGridSortDirection.None, source.SortDirection);
            grid.IsThreeStateSort = false; column.AllowTriStateSorting = true;
            SortKey(); SortKey(); SortKey();
            Assert.Equal(TreeDataGridSortDirection.None, source.SortDirection);
        }
        finally { window.Close(); }
        void SortKey() { A1TestHost.Header(grid).Focus(); A1TestHost.Key(window, Key.Enter); }
    }

    [AvaloniaFact]
    public void Tree_outer_comparer_and_cycle_precedence_is_applied_to_siblings_while_source_paths_remain_stable()
    {
        var root = new Ranked("root", 0) { Children = [new("a", 2), new("b", 1)] };
        using var source = new HierarchicalTreeDataGridSource<Ranked>([root], row => row.Children);
        source.ExpandAll();
        var inner = Column();
        inner.CompareAscending = (left, right) => ((Ranked)left).Rank.CompareTo(((Ranked)right).Rank);
        inner.AllowTriStateSorting = true;
        var tree = new TreeDataGridHierarchicalExpanderColumn { Width = new GridLength(220), InnerColumn = inner, AllowTriStateSorting = false };
        var grid = A1TestHost.Create(source, tree);
        var window = A1TestHost.Show(grid);
        try
        {
            grid.SortByColumn(tree.Id, TreeDataGridSortDirection.Ascending);
            Assert.Equal(new[] { "root", "b", "a" }, Names(source));
            Assert.Equal(new IndexPath(0, 1), source.Rows[1].ModelIndex);
            tree.CompareAscending = (left, right) => string.CompareOrdinal(((Ranked)left).Name, ((Ranked)right).Name);
            grid.SortByColumn(tree.Id, TreeDataGridSortDirection.Ascending);
            Assert.Equal(new[] { "root", "a", "b" }, Names(source));
            A1TestHost.Header(grid).Focus(); A1TestHost.Key(window, Key.Enter);
            A1TestHost.Header(grid).Focus(); A1TestHost.Key(window, Key.Enter);
            Assert.Equal(TreeDataGridSortDirection.Ascending, source.SortDirection);
            tree.AllowTriStateSorting = null;
            A1TestHost.Header(grid).Focus(); A1TestHost.Key(window, Key.Enter);
            A1TestHost.Header(grid).Focus(); A1TestHost.Key(window, Key.Enter);
            Assert.Equal(TreeDataGridSortDirection.None, source.SortDirection);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Comparer_column_mutation_is_rejected_before_publishing_and_a_subsequent_sort_can_recover()
    {
        using var source = new FlatTreeDataGridSource<Ranked>([new("b", 0), new("a", 0)]);
        var column = Column();
        var mutate = true;
        column.CompareAscending = (left, right) =>
        {
            if (mutate) { mutate = false; column.AllowTriStateSorting = true; }
            return string.CompareOrdinal(((Ranked)left).Name, ((Ranked)right).Name);
        };
        var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            Assert.Throws<InvalidOperationException>(() => grid.SortByColumn(column.Id, TreeDataGridSortDirection.Ascending));
            Assert.Null(source.SortColumn);
            Assert.Equal(new[] { "b", "a" }, Names(source));
            Assert.True(grid.SortByColumn(column.Id, TreeDataGridSortDirection.Ascending));
            Assert.Equal(new[] { "a", "b" }, Names(source));
        }
        finally { window.Close(); }
    }

    private static TreeDataGridTextColumn Column() => new() { Header = "Name", Width = new GridLength(220), ValueGetter = row => ((Ranked)row).Name };
    private static string[] Names(TreeDataGridSource source) => source.Rows.Select(row => ((Ranked)row.Model).Name).ToArray();
    private sealed record Ranked(string Name, int Rank) { public ObservableCollection<Ranked> Children { get; init; } = []; }
}
