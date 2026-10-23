using System.Collections.Specialized;
using System.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;
using Xunit;

namespace TreeDataGrid.Core.Tests;

public sealed class BatchImportTests
{
    [Fact]
    public void Range_add_retains_old_node_selection_current_edit_draft_and_single_model_subscription()
    {
        var first = new Item("first"); var second = new Item("second"); var added = new Item("added");
        var items = new TreeDataGridRowMoveCollection<Item> { first, second };
        using var source = new FlatTreeDataGridSource<Item>(items);
        using var state = new TreeDataGridState(source);
        var column = new ColumnId(1); state.SetColumns([column]);
        var firstId = source.Rows[0].Id; var secondId = source.Rows[1].Id;
        var address = new CellAddress(firstId, column); var writes = 0;
        state.Select(firstId); state.BeginEdit(address, model => ((Item)model).Name, (_, _) => writes++);
        var session = state.EditSession!; session.Value = "uncommitted draft";
        var projections = 0; source.ProjectionChanged += (_, _) => projections++;
        NotifyCollectionChangedEventArgs? notification = null; items.CollectionChanged += (_, args) => notification = args;
        items.AddRange([added, first]);

        Assert.Equal(1, projections); Assert.Equal(4, source.Rows.Count);
        Assert.Equal(firstId, source.Rows[0].Id); Assert.Equal(secondId, source.Rows[1].Id);
        Assert.NotEqual(firstId, source.Rows[3].Id); Assert.Same(first, source.Rows[3].Model);
        Assert.Equal(new[] { firstId }, state.SelectedNodes); Assert.Equal(address, state.CurrentCell);
        Assert.Same(session, state.EditSession); Assert.Equal("uncommitted draft", session.Value); Assert.Equal(0, writes);
        Assert.Equal(1, first.Subscribers); Assert.Equal(1, second.Subscribers); Assert.Equal(1, added.Subscribers);
        Assert.Equal(NotifyCollectionChangedAction.Add, notification!.Action); Assert.Equal(2, notification.NewStartingIndex);
        Assert.Equal(2, notification.NewItems!.Count);
    }

    [Fact]
    public void Child_range_add_keeps_existing_tree_identity_and_subscriptions_in_one_projection()
    {
        var child = new Item("child"); var root = new Item("root"); root.Children.Add(child);
        var roots = new TreeDataGridRowMoveCollection<Item> { root };
        using var source = new HierarchicalTreeDataGridSource<Item>(roots, item => item.Children);
        source.TryGetNodeId(new IndexPath(0, 0), out var childId); var rootId = source.Rows[0].Id;
        source.SetExpanded(rootId, true);
        using var state = new TreeDataGridState(source); state.Select(childId);
        var notifications = 0; source.ProjectionChanged += (_, _) => notifications++;
        var added = new Item("added"); root.Children.AddRange([added, new("last")]);
        Assert.Equal(1, notifications); Assert.Equal(4, source.Rows.Count);
        Assert.Equal(rootId, source.Rows[0].Id); Assert.Equal(childId, source.Rows[1].Id);
        Assert.Equal(new[] { childId }, state.SelectedNodes); Assert.Equal(1, child.Subscribers); Assert.Equal(1, added.Subscribers);
        source.Dispose(); Assert.Equal(0, child.Subscribers); Assert.Equal(0, added.Subscribers);
    }

    [Fact]
    public void Empty_range_does_not_notify_or_change_source_version()
    {
        var items = new TreeDataGridRowMoveCollection<Item> { new("existing") };
        using var source = new FlatTreeDataGridSource<Item>(items); var version = source.Version; var id = source.Rows[0].Id;
        var collectionNotifications = 0; var propertyNotifications = 0;
        items.CollectionChanged += (_, _) => collectionNotifications++; items.PropertyChanged += (_, _) => propertyNotifications++;
        items.AddRange([]);
        Assert.Equal(0, collectionNotifications); Assert.Equal(0, propertyNotifications);
        Assert.Equal(version, source.Version); Assert.Equal(id, Assert.Single(source.Rows).Id); Assert.Single(items);
    }

    [Fact]
    public void Enumeration_failure_or_null_item_keeps_storage_and_source_unchanged_with_zero_notifications()
    {
        var existing = new Item("existing"); var items = new TreeDataGridRowMoveCollection<Item> { existing };
        using var source = new FlatTreeDataGridSource<Item>(items); var version = source.Version; var id = source.Rows[0].Id;
        var notifications = 0; items.CollectionChanged += (_, _) => notifications++;
        IEnumerable<Item> Broken() { yield return new("enumerated first"); throw new InvalidOperationException("enumeration failed"); }
        Assert.Throws<InvalidOperationException>(() => items.AddRange(Broken()));
        Assert.Throws<ArgumentException>(() => items.AddRange([new("valid"), null!]));
        Assert.Throws<ArgumentNullException>(() => items.AddRange(null!));
        Assert.Same(existing, Assert.Single(items)); Assert.Equal(0, notifications);
        Assert.Equal(version, source.Version); Assert.Equal(id, Assert.Single(source.Rows).Id);
        items.AddRange([new("later success")]); Assert.Equal(2, source.Rows.Count);
    }

    [Fact]
    public void Mutating_input_enumerator_is_rejected_before_range_commit()
    {
        var items = new TreeDataGridRowMoveCollection<Item> { new("existing") };
        using var source = new FlatTreeDataGridSource<Item>(items); var version = source.Version;
        IEnumerable<Item> Reentrant()
        {
            yield return new("range item");
            items.Add(new("enumerator side effect"));
        }
        Assert.Throws<InvalidOperationException>(() => items.AddRange(Reentrant()));
        Assert.Equal("existing", Assert.Single(items).Name); Assert.Equal(version, source.Version);
        items.AddRange([new("safe")]); Assert.Equal(2, source.Rows.Count);
    }

    [Fact]
    public void Observer_errors_follow_commit_and_do_not_skip_source_reconciliation_or_other_listeners()
    {
        var items = new TreeDataGridRowMoveCollection<Item> { new("existing") };
        var properties = new List<string?>(); var laterCollection = 0;
        items.PropertyChanged += (_, _) => throw new InvalidOperationException("property observer");
        items.PropertyChanged += (_, args) => properties.Add(args.PropertyName);
        items.CollectionChanged += (_, _) => throw new InvalidOperationException("collection observer");
        items.CollectionChanged += (_, _) => laterCollection++;
        using var source = new FlatTreeDataGridSource<Item>(items); var id = source.Rows[0].Id;
        Assert.Throws<AggregateException>(() => items.AddRange([new("one"), new("two")]));
        Assert.Equal(new[] { "Count", "Item[]" }, properties); Assert.Equal(1, laterCollection);
        Assert.Equal(3, items.Count); Assert.Equal(3, source.Rows.Count); Assert.Equal(id, source.Rows[0].Id);
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Pre_indexed_notification_refresh_does_not_publish_added_items_twice_or_replace_old_duplicate_ids()
    {
        var repeated = new Item("repeated"); var items = new TreeDataGridRowMoveCollection<Item> { repeated, repeated };
        using var source = new FlatTreeDataGridSource<Item>(items); using var state = new TreeDataGridState(source);
        var first = source.Rows[0].Id; var second = source.Rows[1].Id; var column = new ColumnId(1); state.SetColumns([column]);
        state.Select(second); state.BeginEdit(new(second, column), _ => "original", (_, _) => throw new InvalidOperationException("must not write"));
        var session = state.EditSession!; session.Value = "draft"; var version = source.Version; var pendingObserved = false;
        var notifications = 0; source.ProjectionChanged += (_, _) => notifications++;
        items.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(items.Count)) return;
            pendingObserved = source.IsRefreshDeferred;
            source.Refresh();
            Assert.Equal(version, source.Version); Assert.Equal(2, source.Rows.Count);
            Assert.Throws<InvalidOperationException>(() => state.CommitEdit());
        };
        items.AddRange([repeated, new("new")]);
        Assert.True(pendingObserved); Assert.Equal(1, notifications); Assert.False(source.IsRefreshDeferred);
        Assert.Equal(4, source.Rows.Count); Assert.Equal(first, source.Rows[0].Id); Assert.Equal(second, source.Rows[1].Id);
        Assert.NotEqual(first, source.Rows[2].Id); Assert.NotEqual(second, source.Rows[2].Id);
        Assert.Equal(new[] { second }, state.SelectedNodes); Assert.Same(session, state.EditSession); Assert.Equal("draft", session.Value);
    }

    [Fact]
    public void Refresh_from_an_observer_after_source_received_range_does_not_leave_source_permanently_deferred()
    {
        var items = new TreeDataGridRowMoveCollection<Item> { new("existing") };
        using var source = new FlatTreeDataGridSource<Item>(items); var first = source.Rows[0].Id;
        items.CollectionChanged += (_, _) => source.Refresh();
        items.AddRange([new("added")]);
        Assert.False(source.IsRefreshDeferred); Assert.Equal(2, source.Rows.Count); Assert.Equal(first, source.Rows[0].Id);
        items.AddRange([new("second batch")]);
        Assert.False(source.IsRefreshDeferred); Assert.Equal(3, source.Rows.Count); Assert.Equal(first, source.Rows[0].Id);
    }

    [Fact]
    public void Nested_controlled_collection_notifications_coalesce_indexed_changes_before_rebuilding_the_graph()
    {
        var child = new Item("child"); var root = new Item("root"); root.Children.Add(child);
        var roots = new TreeDataGridRowMoveCollection<Item> { root };
        using var source = new HierarchicalTreeDataGridSource<Item>(roots, item => item.Children);
        var rootId = source.Rows[0].Id; source.TryGetNodeId(new IndexPath(0, 0), out var childId);
        var entered = false;
        roots.PropertyChanged += (_, args) =>
        {
            if (entered || args.PropertyName != nameof(roots.Count)) return;
            entered = true; root.Children.AddRange([new("nested addition")]);
        };
        roots.AddRange([root]);
        Assert.False(source.IsRefreshDeferred); Assert.Equal(rootId, source.Rows[0].Id);
        Assert.True(source.TryGetNodeId(new IndexPath(0, 0), out var stillChild)); Assert.Equal(childId, stillChild);
        Assert.True(source.TryGetNodeId(new IndexPath(1), out var newRoot)); Assert.NotEqual(rootId, newRoot);
        Assert.True(source.TryGetNodeId(new IndexPath(1, 0), out var newChild)); Assert.NotEqual(childId, newChild);
        Assert.Equal(2, root.Children.Count); Assert.Equal(1, child.Subscribers);
    }

    [Fact]
    public void Adding_same_collection_materializes_before_commit_and_allocates_distinct_occurrence_ids()
    {
        var model = new Item("repeated"); var items = new TreeDataGridRowMoveCollection<Item> { model };
        using var source = new FlatTreeDataGridSource<Item>(items); var original = source.Rows[0].Id;
        items.AddRange(items);
        Assert.Equal(2, items.Count); Assert.Same(model, items[0]); Assert.Same(model, items[1]);
        Assert.Equal(original, source.Rows[0].Id); Assert.NotEqual(original, source.Rows[1].Id); Assert.Equal(1, model.Subscribers);
    }

    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _handlers;
        public string Name { get; } = name;
        public TreeDataGridRowMoveCollection<Item> Children { get; } = [];
        public int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _handlers += value; remove => _handlers -= value; }
    }
}
