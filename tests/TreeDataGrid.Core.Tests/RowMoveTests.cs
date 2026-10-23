using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;
using Xunit;

namespace TreeDataGrid.Core.Tests;

public sealed class RowMoveTests
{
    [Fact]
    public void Same_parent_observable_move_preserves_exact_duplicate_occurrence_and_subtree_identity()
    {
        var repeated = new Item("repeated"); repeated.Children.Add(new("child"));
        var items = new ObservableCollection<Item> { repeated, new("middle"), repeated };
        using var source = Tree(items);
        var first = Id(source, 0); var child = Id(source, 0, 0); var second = Id(source, 2);
        var notifications = 0; source.ProjectionChanged += (_, _) => notifications++;
        var result = source.MoveRow(first, second, TreeDataGridRowDropPosition.After);
        Assert.Equal(TreeDataGridRowMoveStatus.Committed, result.Status);
        Assert.Equal(first, result.NodeId);
        Assert.Equal(first, Id(source, 2)); Assert.Equal(child, Id(source, 2, 0)); Assert.Equal(second, Id(source, 1));
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void Throwing_observable_listener_before_source_subscription_still_reconciles_exact_move()
    {
        var repeated = new Item("repeated");
        var items = new ObservableCollection<Item> { repeated, new("other"), repeated };
        items.CollectionChanged += (_, _) => throw new InvalidOperationException("observer");
        using var source = new FlatTreeDataGridSource<Item>(items);
        var first = Id(source, 0); var last = Id(source, 2);
        var result = source.MoveRow(first, last, TreeDataGridRowDropPosition.After);
        Assert.Equal(TreeDataGridRowMoveStatus.CommittedWithObserverError, result.Status);
        Assert.NotNull(result.Error); Assert.Equal(first, result.NodeId);
        Assert.Equal(first, Id(source, 2)); Assert.Equal(last, Id(source, 1));
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Transaction_cross_parent_storage_is_coherent_before_notifications_and_keeps_other_identities()
    {
        var a = new Item("a"); var moved = new Item("moved"); var untouched = new Item("untouched");
        a.Children.Add(moved); a.Children.Add(untouched);
        var b = new Item("b"); var target = new Item("target"); b.Children.Add(target);
        var roots = new TreeDataGridRowMoveCollection<Item> { a, b };
        using var source = Tree(roots);
        var old = Id(source, 0, 0); var untouchedId = Id(source, 0, 1); var targetId = Id(source, 1, 0);
        var sourceA = a.Children; var sourceB = b.Children;
        var coherent = new List<bool>();
        sourceA.CollectionChanged += (_, _) => coherent.Add(!sourceA.Contains(moved) && sourceB.Contains(moved));
        sourceB.CollectionChanged += (_, _) => coherent.Add(!sourceA.Contains(moved) && sourceB.Contains(moved));
        var notifications = 0; source.ProjectionChanged += (_, _) => notifications++;
        var result = source.MoveRow(old, targetId, TreeDataGridRowDropPosition.Before);
        Assert.Equal(TreeDataGridRowMoveStatus.Committed, result.Status);
        Assert.All(coherent, value => Assert.True(value)); Assert.Equal(2, coherent.Count);
        Assert.False(source.TryGetRow(old, out _)); Assert.NotEqual(old, result.NodeId);
        Assert.Equal(result.NodeId, Id(source, 1, 0)); Assert.Equal(targetId, Id(source, 1, 1));
        Assert.Equal(untouchedId, Id(source, 0, 0)); Assert.Equal(1, notifications);
    }

    [Fact]
    public void Transaction_observer_errors_run_remaining_subscribers_and_leave_both_committed_collections()
    {
        var a = new Item("a"); var moved = new Item("moved"); a.Children.Add(moved);
        var b = new Item("b");
        var reached = 0;
        a.Children.CollectionChanged += (_, _) => throw new InvalidOperationException("first");
        a.Children.CollectionChanged += (_, _) => reached++;
        b.Children.PropertyChanged += (_, _) => throw new InvalidOperationException("property");
        b.Children.CollectionChanged += (_, _) => reached++;
        using var source = Tree(new TreeDataGridRowMoveCollection<Item> { a, b });
        var result = source.MoveRow(Id(source, 0, 0), Id(source, 1), TreeDataGridRowDropPosition.Inside);
        Assert.Equal(TreeDataGridRowMoveStatus.CommittedWithObserverError, result.Status);
        Assert.IsType<AggregateException>(result.Error); Assert.Equal(2, reached);
        Assert.Empty(a.Children); Assert.Same(moved, Assert.Single(b.Children));
        Assert.Equal(result.NodeId, Id(source, 1, 0)); Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Moving_root_inside_later_root_resolves_destination_parent_by_identity_after_index_shift()
    {
        var moved = new Item("moved"); var parent = new Item("parent");
        using var source = Tree(new TreeDataGridRowMoveCollection<Item> { moved, parent });
        var parentId = Id(source, 1);
        var result = source.MoveRow(Id(source, 0), parentId, TreeDataGridRowDropPosition.Inside);
        Assert.True(result.IsCommitted); Assert.Equal(parentId, Id(source, 0));
        Assert.Equal(result.NodeId, Id(source, 0, 0));
        Assert.Same(moved, Assert.Single(parent.Children));
    }

    [Fact]
    public void Unsupported_collections_and_cross_parent_observable_collections_remain_unchanged()
    {
        var list = new List<Item> { new("a"), new("b") };
        using var flat = new FlatTreeDataGridSource<Item>(list);
        Assert.Equal(TreeDataGridRowMoveStatus.Unsupported, flat.MoveRow(Id(flat, 0), Id(flat, 1), TreeDataGridRowDropPosition.After).Status);
        Assert.Equal(new[] { "a", "b" }, list.Select(item => item.Name));
        var a = new ObservableItem("a"); a.Children.Add(new("moved")); var b = new ObservableItem("b");
        using var tree = new HierarchicalTreeDataGridSource<ObservableItem>(new ObservableCollection<ObservableItem> { a, b }, item => item.Children);
        var result = tree.MoveRow(Id(tree, 0, 0), Id(tree, 1), TreeDataGridRowDropPosition.Inside);
        Assert.Equal(TreeDataGridRowMoveStatus.Unsupported, result.Status);
        Assert.Single(a.Children); Assert.Empty(b.Children);
    }

    [Fact]
    public void Sorting_filtering_noop_self_descendant_and_duplicate_model_cycles_reject_before_storage_write()
    {
        var a = new Item("a"); var b = new Item("b"); a.Children.Add(b);
        var roots = new TreeDataGridRowMoveCollection<Item> { a, new("other") };
        using var source = Tree(roots);
        var aId = Id(source, 0); var otherId = Id(source, 1);
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected, source.MoveRow(aId, aId, TreeDataGridRowDropPosition.Before).Status);
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected, source.MoveRow(aId, otherId, TreeDataGridRowDropPosition.Before).Status);
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected, source.MoveRow(aId, Id(source, 0, 0), TreeDataGridRowDropPosition.Inside).Status);
        source.Sort(new ColumnId(1), (x, y) => StringComparer.Ordinal.Compare(((Item)x).Name, ((Item)y).Name), TreeDataGridSortDirection.Ascending);
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected, source.MoveRow(aId, otherId, TreeDataGridRowDropPosition.After).Status);
        source.ClearSort(); source.SetFilter(_ => true);
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected, source.MoveRow(aId, otherId, TreeDataGridRowDropPosition.After).Status);
        Assert.Equal(new[] { a, roots[1] }, roots);

        var repeated = new Item("repeated");
        var holder = new Item("holder"); holder.Children.Add(repeated);
        using var duplicates = Tree(new TreeDataGridRowMoveCollection<Item> { repeated, holder });
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected,
            duplicates.MoveRow(Id(duplicates, 0), Id(duplicates, 1, 0), TreeDataGridRowDropPosition.Inside).Status);
        Assert.Empty(repeated.Children); Assert.Single(holder.Children);
    }

    [Fact]
    public void Cross_parent_result_keeps_exact_inserted_identity_if_projection_observer_reorders_duplicate_models()
    {
        var repeated = new Item("repeated");
        var a = new Item("a"); a.Children.Add(repeated);
        var b = new Item("b"); b.Children.Add(repeated);
        using var source = Tree(new TreeDataGridRowMoveCollection<Item> { a, b });
        var existing = Id(source, 1, 0); var old = Id(source, 0, 0); var parent = Id(source, 1);
        var reordered = false;
        source.ProjectionChanged += (_, _) =>
        {
            if (reordered) return;
            reordered = true; b.Children.Move(1, 0);
        };
        var result = source.MoveRow(old, parent, TreeDataGridRowDropPosition.Inside);
        Assert.True(result.IsCommitted); Assert.Equal(result.NodeId, Id(source, 1, 0));
        Assert.Equal(existing, Id(source, 1, 1)); Assert.NotEqual(existing, result.NodeId);
    }

    [Fact]
    public void Shared_child_collection_target_rejects_a_cycle_before_storage_changes()
    {
        var shared = new TreeDataGridRowMoveCollection<Item>();
        var moving = new Item("moving") { Children = shared };
        var target = new Item("target") { Children = shared };
        var roots = new TreeDataGridRowMoveCollection<Item> { moving, target };
        using var source = Tree(roots);
        var result = source.MoveRow(Id(source, 0), Id(source, 1), TreeDataGridRowDropPosition.Inside);
        Assert.Equal(TreeDataGridRowMoveStatus.Rejected, result.Status);
        Assert.Equal(2, roots.Count); Assert.Empty(shared); Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Move_during_deferred_source_or_move_notifications_is_stale_without_reentry()
    {
        var items = new TreeDataGridRowMoveCollection<Item> { new("a"), new("b"), new("c") };
        using var source = new FlatTreeDataGridSource<Item>(items);
        var first = Id(source, 0); var last = Id(source, 2);
        using (source.DeferRefresh()) Assert.Equal(TreeDataGridRowMoveStatus.Stale, source.MoveRow(first, last, TreeDataGridRowDropPosition.After).Status);
        TreeDataGridRowMoveResult? nested = null;
        items.CollectionChanged += (_, _) => nested = source.MoveRow(last, first, TreeDataGridRowDropPosition.Before);
        Assert.True(source.MoveRow(first, last, TreeDataGridRowDropPosition.After).IsCommitted);
        Assert.Equal(TreeDataGridRowMoveStatus.Stale, nested!.Status);
        Assert.Equal(first, Id(source, 2));
    }

    [Fact]
    public void Row_collection_disallows_notification_mutation_and_reaches_later_property_and_collection_handlers()
    {
        var collection = new TreeDataGridRowMoveCollection<Item>();
        var propertyNames = new List<string?>(); var collectionReached = 0;
        collection.PropertyChanged += (_, _) => collection.Add(new("reentrant"));
        collection.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);
        collection.CollectionChanged += (_, _) => collectionReached++;
        Assert.Throws<AggregateException>(() => collection.Add(new("stored")));
        Assert.Equal(new[] { "Count", "Item[]" }, propertyNames);
        Assert.Equal(1, collectionReached); Assert.Equal("stored", Assert.Single(collection).Name);
        Assert.True(((IList<Item>)collection).Contains(collection[0]));
        Assert.False(((ICollection<Item>)collection).IsReadOnly);
    }

    [Fact]
    public async Task Row_collection_enforces_creating_thread_for_reads_writes_and_enumeration()
    {
        var items = new TreeDataGridRowMoveCollection<Item> { new("a") };
        var enumerator = items.GetEnumerator();
        await Task.Run(() =>
        {
            Assert.Throws<InvalidOperationException>(() => _ = items.Count);
            Assert.Throws<InvalidOperationException>(() => items.Add(new("b")));
            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Projection_observer_failure_reports_committed_without_undoing_storage_or_skipping_cleanup()
    {
        var items = new TreeDataGridRowMoveCollection<Item> { new("a"), new("b") };
        using var source = new FlatTreeDataGridSource<Item>(items);
        var first = Id(source, 0); var last = Id(source, 1);
        var later = 0;
        source.ProjectionChanged += (_, _) => throw new InvalidOperationException("projection observer");
        source.ProjectionChanged += (_, _) => later++;
        var result = source.MoveRow(first, last, TreeDataGridRowDropPosition.After);
        Assert.Equal(TreeDataGridRowMoveStatus.CommittedWithObserverError, result.Status);
        Assert.Equal(first, result.NodeId); Assert.Equal(first, Id(source, 1)); Assert.Equal(1, later);
        Assert.Equal(new[] { "b", "a" }, items.Select(item => item.Name)); Assert.False(source.IsRefreshDeferred);
    }

    private static HierarchicalTreeDataGridSource<Item> Tree(IEnumerable<Item> items) => new(items, item => item.Children);
    private static NodeId Id(TreeDataGridSource source, params int[] path)
    {
        Assert.True(source.TryGetNodeId(new IndexPath(path), out var id)); return id;
    }
    private sealed class Item(string name)
    {
        public string Name { get; } = name;
        public TreeDataGridRowMoveCollection<Item> Children { get; set; } = [];
    }
    private sealed class ObservableItem(string name)
    {
        public string Name { get; } = name;
        public ObservableCollection<ObservableItem> Children { get; } = [];
    }
}
