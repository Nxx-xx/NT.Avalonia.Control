using System.Collections.ObjectModel;
using System.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Core.Tests;

public sealed class ProjectionTests
{
    private static readonly ColumnId Column = new(42);
    private static readonly Comparison<object> Compare = (left, right) => ((Model)left).Value.CompareTo(((Model)right).Value);

    [Fact]
    public void Flat_sort_is_stable_in_both_directions_and_clear_restores_source_order()
    {
        var repeated = new Model(2);
        var items = new ObservableCollection<Model> { repeated, new(1), new(2), repeated, new(3) };
        using var source = new FlatTreeDataGridSource<Model>(items);
        var ids = source.Rows.Select(row => row.Id).ToArray();
        var notifications = new List<ProjectionChangedEventArgs>();
        source.ProjectionChanged += (_, args) => notifications.Add(args);
        source.Sort(Column, Compare, TreeDataGridSortDirection.Ascending);
        Assert.Equal(new[] { ids[1], ids[0], ids[2], ids[3], ids[4] }, source.Rows.Select(row => row.Id));
        source.Sort(Column, Compare, TreeDataGridSortDirection.Descending);
        Assert.Equal(new[] { ids[4], ids[0], ids[2], ids[3], ids[1] }, source.Rows.Select(row => row.Id));
        for (var index = 0; index < ids.Length; index++)
        {
            Assert.True(source.TryGetNodeId(new IndexPath(index), out var id));
            Assert.Equal(ids[index], id);
            Assert.True(source.TryGetRow(id, out var row));
            Assert.Same(items[index], row!.Model);
        }
        Assert.Equal(Column, source.SortColumn);
        source.ClearSort();
        Assert.Equal(ids, source.Rows.Select(row => row.Id));
        Assert.Null(source.SortColumn);
        Assert.Equal(TreeDataGridSortDirection.None, source.SortDirection);
        Assert.All(notifications, args => { Assert.Equal(ProjectionChangeReason.SortChanged, args.Reason); Assert.Empty(args.RemovedNodes); });
    }

    [Fact]
    public void Descending_custom_comparer_handles_int_minimum_without_overflow()
    {
        using var source = new FlatTreeDataGridSource<Model>([new(1), new(2), new(3)]);
        source.Sort(Column, (left, right) => ((Model)left).Value < ((Model)right).Value ? int.MinValue
            : ((Model)left).Value == ((Model)right).Value ? 0 : int.MaxValue, TreeDataGridSortDirection.Descending);
        Assert.Equal(new[] { 3, 2, 1 }, Values(source));
    }

    [Fact]
    public void Tree_sort_keeps_subtrees_intact_and_paths_point_to_source_siblings()
    {
        var first = new Model(2, new Model(4), new Model(1));
        var second = new Model(1, new Model(9), new Model(3));
        using var source = Tree([first, second]);
        source.ExpandAll();
        var ids = source.Rows.ToDictionary(row => row.ModelIndex, row => row.Id);
        source.Sort(Column, Compare, TreeDataGridSortDirection.Ascending);
        Assert.Equal(new[] { 1, 3, 9, 2, 1, 4 }, Values(source));
        Assert.Equal(new[] { new IndexPath(1), new IndexPath(1, 1), new IndexPath(1, 0), new IndexPath(0), new IndexPath(0, 1), new IndexPath(0, 0) },
            source.Rows.Select(row => row.ModelIndex));
        foreach (var pair in ids)
        {
            Assert.True(source.TryGetNodeId(pair.Key, out var id));
            Assert.Equal(pair.Value, id);
        }
        source.ClearSort();
        Assert.Equal(new[] { 2, 4, 1, 1, 9, 3 }, Values(source));
    }

    [Fact]
    public void Tree_filter_evaluates_every_occurrence_and_rejected_parent_hides_matching_descendant()
    {
        var duplicate = new Model(4);
        using var source = Tree([new(1, duplicate), new(2, duplicate, new Model(5))]);
        source.ExpandAll();
        var ids = source.Rows.Select(row => row.Id).ToArray();
        var calls = 0;
        ProjectionChangedEventArgs? notification = null;
        source.ProjectionChanged += (_, args) => notification = args;
        source.SetFilter(model => { calls++; return ((Model)model).Value % 2 == 0; });
        Assert.Equal(5, calls);
        Assert.Equal(new[] { 2, 4 }, Values(source));
        Assert.Equal(ProjectionChangeReason.FilterChanged, notification!.Reason);
        Assert.Empty(notification.RemovedNodes);
        Assert.All(ids, id => Assert.True(source.TryGetRow(id, out _)));
        Assert.Equal(-1, source.GetViewIndex(ids[1]));
        source.SetFilter(null);
        Assert.Equal(ids, source.Rows.Select(row => row.Id));
        Assert.True(source.Rows[0].IsExpanded);
    }

    [Fact]
    public void Property_collection_and_closure_refresh_reapply_sort_and_filter()
    {
        var items = new ObservableCollection<Model> { new(3), new(1), new(2) };
        using var source = new FlatTreeDataGridSource<Model>(items);
        var three = source.Rows[0].Id;
        var minimum = 2;
        source.SetFilter(model => ((Model)model).Value >= minimum);
        source.Sort(Column, Compare, TreeDataGridSortDirection.Ascending);
        items[0].Value = 0;
        Assert.Equal(new[] { 2 }, Values(source));
        items.Insert(0, new(4));
        items.Move(3, 0);
        Assert.Equal(new[] { 2, 4 }, Values(source));
        minimum = 0;
        source.Refresh();
        Assert.Equal(new[] { 0, 1, 2, 4 }, Values(source));
        Assert.Equal(0, source.GetViewIndex(three));
        source.ClearSort();
        Assert.Equal(items.Select(model => model.Value), Values(source));
    }

    [Fact]
    public void Bad_configuration_retains_rows_version_and_public_configuration()
    {
        using var source = new FlatTreeDataGridSource<Model>([new(2), new(1)]);
        source.Sort(Column, Compare, TreeDataGridSortDirection.Ascending);
        Func<object, bool> filter = model => ((Model)model).Value > 0;
        source.SetFilter(filter);
        var rows = source.Rows;
        var version = source.Version;
        Assert.Throws<FormatException>(() => source.Sort(new ColumnId(99), (_, _) => throw new FormatException("comparison"), TreeDataGridSortDirection.Descending));
        Assert.Throws<FormatException>(() => source.SetFilter(_ => throw new FormatException("predicate")));
        Assert.Throws<ArgumentException>(() => source.Sort(default, Compare, TreeDataGridSortDirection.Ascending));
        Assert.Throws<ArgumentNullException>(() => source.Sort(Column, null!, TreeDataGridSortDirection.Ascending));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Sort(Column, Compare, TreeDataGridSortDirection.None));
        Assert.Same(rows, source.Rows);
        Assert.Equal(version, source.Version);
        Assert.Equal(Column, source.SortColumn);
        Assert.Equal(TreeDataGridSortDirection.Ascending, source.SortDirection);
        Assert.Same(filter, source.Filter);
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Deferred_configuration_publishes_once_and_remains_atomic_if_final_evaluation_fails()
    {
        var items = new ObservableCollection<Model> { new(2), new(1) };
        using var source = new FlatTreeDataGridSource<Model>(items);
        var oldRows = source.Rows;
        var calls = 0;
        source.ProjectionChanged += (_, _) => calls++;
        using (source.DeferRefresh())
        {
            source.Sort(Column, Compare, TreeDataGridSortDirection.Descending);
            source.SetFilter(model => ((Model)model).Value > 1);
            items.Add(new(3));
            Assert.Same(oldRows, source.Rows);
            Assert.Null(source.SortColumn);
            Assert.Null(source.Filter);
        }
        Assert.Equal(1, calls);
        Assert.Equal(new[] { 3, 2 }, Values(source));
        var successful = source.Rows;
        var oldFilter = source.Filter;
        var fail = false;
        var scope = source.DeferRefresh();
        source.SetFilter(_ => fail ? throw new FormatException("final") : true);
        fail = true;
        Assert.Throws<FormatException>(scope.Dispose);
        Assert.Same(successful, source.Rows);
        Assert.Same(oldFilter, source.Filter);
        Assert.True(source.IsRefreshDeferred);
        source.Refresh(); // The failed pending configuration was discarded.
        Assert.Equal(new[] { 3, 2 }, Values(source));
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Automatic_projection_failure_retains_old_snapshot_and_recovery_preserves_occurrences()
    {
        var shared = new Model(1);
        var items = new ObservableCollection<Model> { shared, shared, new(2) };
        using var source = new FlatTreeDataGridSource<Model>(items);
        var ids = source.Rows.Select(row => row.Id).ToArray();
        var fail = false;
        source.SetFilter(_ => fail ? throw new FormatException("model") : true);
        var oldRows = source.Rows;
        fail = true;
        Assert.Throws<FormatException>(() => items.Move(0, 1));
        Assert.Same(oldRows, source.Rows);
        Assert.True(source.IsRefreshDeferred);
        using var state = new TreeDataGridState(source);
        Assert.Throws<InvalidOperationException>(() => state.BeginEdit(new(ids[0], Column), _ => 0, (_, _) => { }));
        source.SetFilter(null);
        Assert.Equal(new[] { ids[1], ids[0], ids[2] }, source.Rows.Select(row => row.Id));
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Reentrant_source_commands_fail_even_if_callback_swallows_exception()
    {
        using var source = new FlatTreeDataGridSource<Model>([new(2), new(1)]);
        var rows = source.Rows;
        Action[] commands = [source.Refresh, source.Dispose, () => source.DeferRefresh(),
            () => source.Sort(Column, Compare, TreeDataGridSortDirection.Ascending), source.ClearSort,
            () => source.SetFilter(null), () => source.SetExpanded(source.Rows[0].Id, true)];
        foreach (var command in commands)
        {
            Assert.Throws<InvalidOperationException>(() => source.SetFilter(_ =>
            {
                Assert.True(source.IsRefreshDeferred);
                Assert.Throws<InvalidOperationException>(command);
                return true;
            }));
            Assert.Same(rows, source.Rows);
            Assert.False(source.IsRefreshDeferred);
            Assert.Null(source.Filter);
        }
    }

    [Fact]
    public void Callback_collection_side_effect_is_reconciled_on_repair_without_writing_stale_rows()
    {
        var shared = new Model(1);
        var items = new ObservableCollection<Model> { shared, shared, new(2) };
        using var source = new FlatTreeDataGridSource<Model>(items);
        var ids = source.Rows.Select(row => row.Id).ToArray();
        var once = true;
        Assert.Throws<InvalidOperationException>(() => source.SetFilter(_ =>
        {
            if (once)
            {
                once = false;
                Assert.Throws<InvalidOperationException>(() => items.Move(0, 1));
            }
            return true;
        }));
        Assert.Equal(ids, source.Rows.Select(row => row.Id));
        Assert.True(source.IsRefreshDeferred);
        source.Refresh();
        Assert.Equal(new[] { ids[1], ids[0], ids[2] }, source.Rows.Select(row => row.Id));
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Projection_listener_failure_keeps_committed_configuration_and_drains_collection_updates()
    {
        var items = new ObservableCollection<Model> { new(2), new(1) };
        using var source = new FlatTreeDataGridSource<Model>(items);
        var once = true;
        source.ProjectionChanged += (_, _) =>
        {
            if (!once) return;
            once = false;
            Assert.Equal(Column, source.SortColumn);
            items.Add(new(0));
            throw new FormatException("listener");
        };
        Assert.Throws<FormatException>(() => source.Sort(Column, Compare, TreeDataGridSortDirection.Ascending));
        Assert.Equal(new[] { 0, 1, 2 }, Values(source));
        Assert.Equal(Column, source.SortColumn);
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Edits_and_selection_follow_identity_through_sort_and_filter()
    {
        var items = new ObservableCollection<Model> { new(2), new(1), new(3) };
        using var source = new FlatTreeDataGridSource<Model>(items);
        using var state = new TreeDataGridState(source);
        var id = source.Rows[0].Id;
        state.Select(id);
        state.BeginEdit(new(id, Column), model => ((Model)model).Value, (model, value) => ((Model)model).Value = (int)value!);
        source.Sort(Column, Compare, TreeDataGridSortDirection.Ascending);
        Assert.Equal(id, state.EditSession!.Cell.Node);
        var projectionSawFinishedEdit = false;
        source.ProjectionChanged += (_, _) => projectionSawFinishedEdit = state.EditSession is null;
        Assert.True(state.CommitEdit(4));
        Assert.Equal(4, items[0].Value);
        Assert.Equal(1, items[1].Value);
        Assert.Equal(2, source.GetViewIndex(id));
        Assert.True(projectionSawFinishedEdit);
        state.BeginEdit(new(id, Column), model => ((Model)model).Value, (model, value) => ((Model)model).Value = (int)value!);
        source.SetFilter(model => ((Model)model).Value < 4);
        Assert.Null(state.EditSession);
        Assert.Contains(id, state.SelectedNodes);
        source.SetFilter(null);
        Assert.Contains(id, state.SelectedNodes);
        Assert.Equal(2, source.GetViewIndex(id));
    }

    [Fact]
    public void Commit_value_that_fails_filter_finishes_write_before_hiding_node()
    {
        var model = new Model(2);
        using var source = new FlatTreeDataGridSource<Model>([model]);
        using var state = new TreeDataGridState(source);
        source.SetFilter(item => ((Model)item).Value > 0);
        var id = source.Rows[0].Id;
        state.BeginEdit(new(id, Column), item => ((Model)item).Value, (item, value) => ((Model)item).Value = (int)value!);
        Assert.True(state.CommitEdit(0));
        Assert.Equal(0, model.Value);
        Assert.Empty(source.Rows);
        Assert.True(source.TryGetRow(id, out _));
        Assert.Null(state.EditSession);
    }

    [Fact]
    public void Deferred_repeated_sort_filter_and_changes_publish_only_final_configuration()
    {
        var items = new ObservableCollection<Model> { new(3), new(1), new(2) };
        using var source = new FlatTreeDataGridSource<Model>(items);
        var notifications = new List<ProjectionChangedEventArgs>();
        source.ProjectionChanged += (_, args) => notifications.Add(args);
        Func<object, bool> finalFilter = model => ((Model)model).Value <= 3;
        using (source.DeferRefresh())
        {
            source.Sort(new(1), Compare, TreeDataGridSortDirection.Ascending);
            source.SetFilter(_ => false);
            using (source.DeferRefresh())
            {
                source.ClearSort();
                source.SetFilter(null);
                items.Add(new(4));
                source.Sort(Column, Compare, TreeDataGridSortDirection.Descending);
                source.SetFilter(finalFilter);
            }
            Assert.Null(source.SortColumn);
            Assert.Null(source.Filter);
        }
        Assert.Single(notifications);
        Assert.Equal(ProjectionChangeReason.Refresh, notifications[0].Reason);
        Assert.Equal(Column, source.SortColumn);
        Assert.Same(finalFilter, source.Filter);
        Assert.Equal(new[] { 3, 2, 1 }, Values(source));
    }

    [Fact]
    public void Predicate_property_side_effect_cannot_be_published_and_safe_filter_repairs_it()
    {
        var model = new Model(1);
        using var source = new FlatTreeDataGridSource<Model>([model]);
        var version = source.Version;
        Assert.Throws<InvalidOperationException>(() => source.SetFilter(_ =>
        {
            Assert.Throws<InvalidOperationException>(() => model.Value = 2);
            return true;
        }));
        Assert.Equal(version, source.Version);
        Assert.Null(source.Filter);
        Assert.True(source.IsRefreshDeferred);
        source.SetFilter(item => ((Model)item).Value == 2);
        Assert.Single(source.Rows);
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Comparator_reentry_cannot_publish_even_when_callback_swallows_the_error()
    {
        using var source = new FlatTreeDataGridSource<Model>([new(2), new(1)]);
        var rows = source.Rows;
        Assert.Throws<InvalidOperationException>(() => source.Sort(Column, (left, right) =>
        {
            Assert.Throws<InvalidOperationException>(() => source.SetFilter(_ => true));
            return Compare(left, right);
        }, TreeDataGridSortDirection.Ascending));
        Assert.Same(rows, source.Rows);
        Assert.Null(source.SortColumn);
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Deferred_callback_side_effect_recovery_does_not_publish_an_intermediate_stale_graph()
    {
        var items = new ObservableCollection<Model> { new(2), new(1) };
        using var source = new FlatTreeDataGridSource<Model>(items);
        var notifications = 0;
        source.ProjectionChanged += (_, _) =>
        {
            notifications++;
            Assert.Equal(3, source.Rows.Count);
            Assert.False(source.IsRefreshDeferred);
        };
        using (source.DeferRefresh())
        {
            source.Sort(Column, Compare, TreeDataGridSortDirection.Ascending);
            Assert.Throws<InvalidOperationException>(() => source.SetFilter(_ =>
            {
                items.Add(new(0));
                return true;
            }));
        }
        Assert.Equal(1, notifications);
        Assert.Equal(new[] { 0, 1, 2 }, Values(source));
    }

    [Fact]
    public void Filter_failure_after_removal_reports_removed_identity_only_when_repaired()
    {
        var items = new ObservableCollection<Model> { new(1), new(2) };
        using var source = new FlatTreeDataGridSource<Model>(items);
        var removed = source.Rows[0].Id;
        var fail = false;
        source.SetFilter(_ => fail ? throw new FormatException() : true);
        fail = true;
        var notifications = new List<ProjectionChangedEventArgs>();
        source.ProjectionChanged += (_, args) => notifications.Add(args);
        Assert.Throws<FormatException>(() => items.RemoveAt(0));
        Assert.Empty(notifications);
        Assert.True(source.TryGetRow(removed, out _));
        source.SetFilter(null);
        Assert.False(source.TryGetRow(removed, out _));
        Assert.Equal(new[] { removed }, Assert.Single(notifications).RemovedNodes);
    }

    [Fact]
    public void Projection_configuration_obeys_owner_thread_and_dispose_contract()
    {
        var source = new FlatTreeDataGridSource<Model>([new(1)]);
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { source.SetFilter(_ => true); }
            catch (Exception exception) { error = exception; }
        });
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidOperationException>(error);
        source.Dispose();
        Assert.Throws<ObjectDisposedException>(() => source.Sort(Column, Compare, TreeDataGridSortDirection.Ascending));
        Assert.Throws<ObjectDisposedException>(source.ClearSort);
        Assert.Throws<ObjectDisposedException>(() => source.SetFilter(null));
        Assert.Throws<ObjectDisposedException>(() => source.Filter);
    }

    [Fact]
    public void Maximum_depth_filter_retains_hidden_identity_and_expansion()
    {
        var root = new Model(1);
        var tail = root;
        for (var depth = 1; depth < TreeDataGridSource<Model>.MaximumDepth; depth++)
        {
            var next = new Model(depth + 1);
            tail.Children.Add(next);
            tail = next;
        }
        using var source = Tree([root]);
        source.ExpandAll();
        var last = source.Rows[^1].Id;
        source.SetFilter(model => ((Model)model).Value != 100);
        Assert.Equal(99, source.Rows.Count);
        Assert.True(source.TryGetRow(last, out _));
        source.SetFilter(null);
        Assert.Equal(256, source.Rows.Count);
        Assert.Equal(last, source.Rows[^1].Id);
    }

    [Fact]
    public void Random_tree_mutations_sort_filter_and_expansion_keep_all_mappings_consistent()
    {
        var random = new Random(91357);
        var roots = new ObservableCollection<Model>(Enumerable.Range(0, 5)
            .Select(index => new Model(index, new Model(index + 2), new Model(index + 4))));
        using var source = Tree(roots);
        var direction = TreeDataGridSortDirection.None;
        var threshold = int.MinValue;
        var previous = ReadIdentities();
        var notifications = new List<ProjectionChangedEventArgs>();
        source.ProjectionChanged += (_, args) => notifications.Add(args);
        for (var step = 0; step < 600; step++)
        {
            notifications.Clear();
            var collection = roots.Count != 0 && random.Next(2) == 0 ? roots[random.Next(roots.Count)].Children : roots;
            switch (random.Next(10))
            {
                case 0:
                    collection.Insert(random.Next(collection.Count + 1), new Model(random.Next(8)));
                    break;
                case 1 when collection.Count > 0:
                    collection.RemoveAt(random.Next(collection.Count));
                    break;
                case 2 when collection.Count > 1:
                    collection.Move(random.Next(collection.Count), random.Next(collection.Count));
                    break;
                case 3 when collection.Count > 0:
                    collection[random.Next(collection.Count)].Value = random.Next(8);
                    break;
                case 4:
                    direction = (TreeDataGridSortDirection)random.Next(3);
                    if (direction == TreeDataGridSortDirection.None) source.ClearSort();
                    else source.Sort(Column, Compare, direction);
                    break;
                case 5:
                    threshold = random.Next(5) - 1;
                    var thresholdSnapshot = threshold;
                    source.SetFilter(model => ((Model)model).Value >= thresholdSnapshot);
                    break;
                case 6:
                    threshold = int.MinValue;
                    source.SetFilter(null);
                    break;
                case 7 when previous.Count != 0:
                    var candidate = previous.ElementAt(random.Next(previous.Count)).Value;
                    source.SetExpanded(candidate, random.Next(2) == 0);
                    break;
                case 8 when collection.Count > 0:
                    collection[random.Next(collection.Count)] = new Model(random.Next(8));
                    break;
                case 9:
                    using (source.DeferRefresh())
                    {
                        collection.Add(new(random.Next(8)));
                        if (collection.Count > 1) collection.Move(0, collection.Count - 1);
                    }
                    break;
            }
            var current = ReadIdentities();
            foreach (var pair in current)
            {
                if (previous.TryGetValue(pair.Key, out var identity)) Assert.Equal(identity, pair.Value);
            }
            var removed = previous.Where(pair => !current.ContainsKey(pair.Key)).Select(pair => pair.Value).ToHashSet();
            Assert.True(removed.SetEquals(notifications.SelectMany(args => args.RemovedNodes)));
            var expected = new List<Model>();
            void Visit(IEnumerable<Model> siblings, bool visible)
            {
                var ordered = direction == TreeDataGridSortDirection.Ascending ? siblings.OrderBy(model => model.Value)
                    : direction == TreeDataGridSortDirection.Descending ? siblings.OrderByDescending(model => model.Value) : siblings;
                foreach (var model in ordered)
                {
                    var accepted = model.Value >= threshold;
                    Assert.True(source.TryGetRow(current[model], out var row));
                    if (visible && accepted) expected.Add(model);
                    Visit(model.Children, visible && accepted && row!.IsExpanded);
                }
            }
            Visit(roots, true);
            Assert.Equal(expected, source.Rows.Select(row => (Model)row.Model));
            for (var index = 0; index < source.Rows.Count; index++)
                Assert.Equal(index, source.GetViewIndex(source.Rows[index].Id));
            foreach (var pair in current)
                if (!expected.Contains(pair.Key)) Assert.Equal(-1, source.GetViewIndex(pair.Value));
            previous = current;
        }

        Dictionary<Model, NodeId> ReadIdentities()
        {
            var result = new Dictionary<Model, NodeId>();
            void Walk(IList<Model> siblings, IndexPath parent)
            {
                for (var index = 0; index < siblings.Count; index++)
                {
                    var path = parent.Append(index);
                    Assert.True(source.TryGetNodeId(path, out var identity));
                    Assert.True(source.TryGetRow(identity, out var row));
                    Assert.Same(siblings[index], row!.Model);
                    Assert.Equal(path, row.ModelIndex);
                    result.Add(siblings[index], identity);
                    Walk(siblings[index].Children, path);
                }
            }
            Walk(roots, default);
            return result;
        }
    }

    private static HierarchicalTreeDataGridSource<Model> Tree(IEnumerable<Model> items) => new(items, model => model.Children);
    private static int[] Values(TreeDataGridSource source) => source.Rows.Select(row => ((Model)row.Model).Value).ToArray();

    private sealed class Model(int value, params Model[] children) : INotifyPropertyChanged
    {
        private int _value = value;
        public int Value
        {
            get => _value;
            set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }
        public ObservableCollection<Model> Children { get; } = new(children);
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
