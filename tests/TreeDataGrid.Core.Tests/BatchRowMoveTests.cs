using System.Collections.ObjectModel;
using NT.AvaloniaControl.TreeDataGrid;
using Xunit;

namespace TreeDataGrid.Core.Tests;

public sealed class BatchRowMoveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Same_parent_batch_keeps_source_order_and_duplicate_occurrence_subtrees(bool upward)
    {
        var repeat = new Item("repeat"); repeat.Children.Add(new("child"));
        var items = new TreeDataGridRowMoveCollection<Item> { repeat, new("a"), repeat, new("b"), new("c") };
        using var source = Tree(items); var ids = Enumerable.Range(0, 5).Select(index => Id(source, index)).ToArray();
        var child = Id(source, 2, 0); var notifications = 0; source.ProjectionChanged += (_, _) => notifications++;
        var moving = upward ? new[] { ids[4], ids[2] } : [ids[2], ids[0]];
        var result = source.MoveRows(moving, upward ? ids[0] : ids[4], upward ? TreeDataGridRowDropPosition.Before : TreeDataGridRowDropPosition.After);
        Assert.True(result.IsCommitted); Assert.Equal(1, notifications);
        var expected = upward ? new[] { ids[2], ids[4], ids[0], ids[1], ids[3] } : [ids[1], ids[3], ids[4], ids[0], ids[2]];
        Assert.Equal(expected, Enumerable.Range(0, 5).Select(index => Id(source, index)));
        Assert.Equal(child, Id(source, upward ? 0 : 4, 0)); Assert.Equal(child, result.IdentityMap[child]);
        Assert.All(moving, id => Assert.Equal(id, result.IdentityMap[id]));
    }

    [Fact]
    public void Cross_parent_batch_is_coherent_before_observers_and_maps_selected_descendants()
    {
        var a = new Item("a"); var first = new Item("first"); first.Children.Add(new("descendant"));
        a.Children.Add(first); a.Children.Add(new("second")); a.Children.Add(new("remaining"));
        var b = new Item("b"); b.Children.Add(new("target"));
        using var source = Tree(new TreeDataGridRowMoveCollection<Item> { a, b });
        var one = Id(source, 0, 0); var descendant = Id(source, 0, 0, 0); var two = Id(source, 0, 1); var target = Id(source, 1, 0);
        var coherent = 0; var notifications = 0;
        a.Children.CollectionChanged += (_, _) => { Assert.Single(a.Children); Assert.Equal(3, b.Children.Count); coherent++; };
        b.Children.CollectionChanged += (_, _) => { Assert.Single(a.Children); Assert.Equal(3, b.Children.Count); coherent++; };
        source.ProjectionChanged += (_, _) => notifications++;
        var result = source.MoveRows([two, descendant, one, one], target, TreeDataGridRowDropPosition.Before);
        Assert.True(result.IsCommitted); Assert.Equal(new[] { "first", "second", "target" }, b.Children.Select(item => item.Name));
        Assert.Equal(4, coherent); Assert.Equal(1, notifications);
        Assert.Equal(Id(source, 1, 0), result.IdentityMap[one]); Assert.Equal(Id(source, 1, 0, 0), result.IdentityMap[descendant]);
        Assert.Equal(Id(source, 1, 1), result.IdentityMap[two]); Assert.NotEqual(one, result.IdentityMap[one]);
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Batch_can_merge_multiple_parents_with_local_destination_occurrences()
    {
        var a = new Item("a"); a.Children.Add(new("a1")); a.Children.Add(new("a2"));
        var b = new Item("b"); b.Children.Add(new("b1")); b.Children.Add(new("b2")); b.Children.Add(new("b3"));
        var c = new Item("c"); c.Children.Add(new("c1"));
        using var source = Tree(new TreeDataGridRowMoveCollection<Item> { a, b, c });
        var local = Id(source, 1, 0); var target = Id(source, 1, 2);
        var result = source.MoveRows([Id(source, 2, 0), local, Id(source, 0, 1)], target, TreeDataGridRowDropPosition.After);
        Assert.True(result.IsCommitted); Assert.Equal(new[] { "b2", "b3", "a2", "b1", "c1" }, b.Children.Select(item => item.Name));
        Assert.Equal(local, Id(source, 1, 3)); Assert.Equal(local, result.IdentityMap[local]); Assert.Empty(c.Children); Assert.Single(a.Children);
    }

    [Fact]
    public void Observers_can_fail_after_atomic_commit_without_skipping_other_listeners_or_identity_capture()
    {
        var a = new Item("a"); a.Children.Add(new("one")); a.Children.Add(new("two")); var b = new Item("b");
        a.Children.CollectionChanged += (_, _) => throw new InvalidOperationException("observer");
        var reached = 0; a.Children.CollectionChanged += (_, _) => reached++;
        using var source = Tree(new TreeDataGridRowMoveCollection<Item> { a, b });
        var nodes = new[] { Id(source, 0, 0), Id(source, 0, 1) };
        source.ProjectionChanged += (_, _) => throw new InvalidOperationException("projection");
        var result = source.MoveRows(nodes, Id(source, 1), TreeDataGridRowDropPosition.Inside);
        Assert.Equal(TreeDataGridRowMoveStatus.CommittedWithObserverError, result.Status); Assert.NotNull(result.Error);
        Assert.Equal(2, reached); Assert.Empty(a.Children); Assert.Equal(2, b.Children.Count);
        Assert.Equal(2, result.IdentityMap.Count); Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Unsupported_batch_noop_sort_filter_invalid_node_and_cycle_do_not_write()
    {
        var a = new Item("a"); a.Children.Add(new("child")); var b = new Item("b"); var c = new Item("c");
        var items = new TreeDataGridRowMoveCollection<Item> { a, b, c }; using var source = Tree(items);
        var first = Id(source, 0); var second = Id(source, 1); var last = Id(source, 2);
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected, source.MoveRows([first, second], last, TreeDataGridRowDropPosition.Before).Status);
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected, source.MoveRows([first, second], Id(source, 0, 0), TreeDataGridRowDropPosition.Inside).Status);
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected, source.MoveRows([new NodeId(long.MaxValue)], last, TreeDataGridRowDropPosition.After).Status);
        source.SetFilter(_ => true); Assert.Equal(TreeDataGridRowMoveStatus.Rejected, source.MoveRows([first, second], last, TreeDataGridRowDropPosition.After).Status);
        source.SetFilter(null); source.Sort(new ColumnId(1), (_, _) => 0, TreeDataGridSortDirection.Ascending);
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected, source.MoveRows([first, second], last, TreeDataGridRowDropPosition.After).Status);
        Assert.Equal(new[] { a, b, c }, items);
        using var ordinary = new FlatTreeDataGridSource<Item>(new ObservableCollection<Item> { a, b, c });
        Assert.Equal(TreeDataGridRowMoveStatus.Unsupported, ordinary.MoveRows([Id(ordinary, 0), Id(ordinary, 1)], Id(ordinary, 2), TreeDataGridRowDropPosition.After).Status);
    }

    [Fact]
    public void Public_collection_batch_prepares_before_writes_and_blocks_notification_reentry()
    {
        var items = new TreeDataGridRowMoveCollection<Item> { new("a"), new("b"), new("c"), new("d") };
        Assert.Throws<ArgumentOutOfRangeException>(() => items.MoveMany([0, 9], 1));
        Assert.Throws<ArgumentException>(() => items.MoveMany([0, 0], 1));
        Assert.Equal(new[] { "a", "b", "c", "d" }, items.Select(item => item.Name));
        items.CollectionChanged += (_, _) => items.Clear();
        using var source = new FlatTreeDataGridSource<Item>(items);
        var first = Id(source, 0); var second = Id(source, 1);
        var result = source.MoveRows([first, second], Id(source, 3), TreeDataGridRowDropPosition.After);
        Assert.Equal(TreeDataGridRowMoveStatus.CommittedWithObserverError, result.Status);
        Assert.Equal(new[] { "c", "d", "a", "b" }, items.Select(item => item.Name)); Assert.Equal(first, Id(source, 2));
    }

    [Fact]
    public void Indexed_batch_notifications_match_every_small_subset_and_insertion_with_repeated_objects()
    {
        for (var count = 1; count <= 6; count++)
            for (var bits = 1; bits < 1 << count; bits++)
            {
                var indices = Enumerable.Range(0, count).Where(index => (bits & 1 << index) != 0).ToArray();
                for (var insertion = 0; insertion <= count - indices.Length; insertion++)
                {
                    var repeated = new Item("same"); var items = new TreeDataGridRowMoveCollection<Item>(Enumerable.Repeat(repeated, count));
                    using var source = new FlatTreeDataGridSource<Item>(items); var original = source.Rows.Select(row => row.Id).ToArray();
                    var expected = original.Where((_, index) => !indices.Contains(index)).ToList();
                    expected.InsertRange(insertion, indices.Select(index => original[index]));
                    items.MoveMany(indices.Reverse().ToArray(), insertion);
                    Assert.Equal(expected, source.Rows.Select(row => row.Id)); Assert.False(source.IsRefreshDeferred);
                }
            }
    }

    [Fact]
    public void Batch_result_captures_inserted_duplicate_identity_before_projection_observer_reorders_it()
    {
        var repeated = new Item("same"); var a = new Item("a"); a.Children.Add(repeated); a.Children.Add(repeated);
        var b = new Item("b"); b.Children.Add(repeated); using var source = Tree(new TreeDataGridRowMoveCollection<Item> { a, b });
        var moving = new[] { Id(source, 0, 0), Id(source, 0, 1) }; var existing = Id(source, 1, 0); var once = false;
        source.ProjectionChanged += (_, _) => { if (!once) { once = true; b.Children.Move(2, 0); } };
        var result = source.MoveRows(moving, Id(source, 1), TreeDataGridRowDropPosition.Inside);
        Assert.True(result.IsCommitted); Assert.Equal(result.IdentityMap[moving[1]], Id(source, 1, 0));
        Assert.Equal(existing, Id(source, 1, 1)); Assert.Equal(result.IdentityMap[moving[0]], Id(source, 1, 2));
    }

    [Fact]
    public void Moving_expanded_subtree_back_to_roots_keeps_expansion_and_maps_descendant_identity()
    {
        var parent = new Item("parent"); var child = new Item("child"); child.Children.Add(new("grandchild")); parent.Children.Add(child);
        using var source = Tree(new TreeDataGridRowMoveCollection<Item> { parent, new("target") });
        var moving = Id(source, 0, 0); var descendant = Id(source, 0, 0, 0); source.SetExpanded(Id(source, 0), true); source.SetExpanded(moving, true);
        var result = source.MoveRows([moving], Id(source, 1), TreeDataGridRowDropPosition.After);
        Assert.True(result.IsCommitted); Assert.True(source.GetViewIndex(result.IdentityMap[descendant]) >= 0);
        Assert.Equal(Id(source, 2, 0), result.IdentityMap[descendant]); Assert.Empty(parent.Children);
    }

    [Fact]
    public void Disposal_after_commit_reports_observer_error_with_no_surviving_identity_map()
    {
        var items = new TreeDataGridRowMoveCollection<Item> { new("a"), new("b"), new("c") };
        using var source = new FlatTreeDataGridSource<Item>(items); var nodes = source.Rows.Take(2).Select(row => row.Id).ToArray(); var target = Id(source, 2);
        source.ProjectionChanged += (_, _) => source.Dispose();
        var result = source.MoveRows(nodes, target, TreeDataGridRowDropPosition.After);
        Assert.Equal(TreeDataGridRowMoveStatus.CommittedWithObserverError, result.Status); Assert.Empty(result.IdentityMap);
        Assert.Equal(new[] { "c", "a", "b" }, items.Select(item => item.Name));
    }

    private static HierarchicalTreeDataGridSource<Item> Tree(IEnumerable<Item> items) => new(items, item => item.Children);
    private static NodeId Id(TreeDataGridSource source, params int[] path) { Assert.True(source.TryGetNodeId(new(path), out var id)); return id; }
    private sealed class Item(string name)
    {
        public string Name { get; } = name;
        public TreeDataGridRowMoveCollection<Item> Children { get; } = [];
    }
}
