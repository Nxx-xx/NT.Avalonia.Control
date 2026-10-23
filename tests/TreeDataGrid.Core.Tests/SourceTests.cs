using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;
using Xunit;

namespace TreeDataGrid.Core.Tests;

public sealed class SourceTests
{
    [Fact]
    public void IndexPath_is_immutable_and_value_equatable()
    {
        var input = new[] { 1, 2 };
        var path = new IndexPath(input);
        input[0] = 99;
        Assert.Equal(new IndexPath(1, 2), path);
        Assert.Equal(new IndexPath(1, 2).GetHashCode(), path.GetHashCode());
        Assert.Equal(new[] { 1, 2 }, path.ToArray());
        Assert.Equal(new IndexPath(1, 2, 3), path.Append(3));
        Assert.Equal(default, new IndexPath());
        Assert.Equal(2, path.Count);
        Assert.Equal("1.2", path.ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => new IndexPath(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => path.Append(-1));
    }

    [Fact]
    public void Hierarchy_maps_hidden_nodes_and_reuses_identity_across_expansion()
    {
        var child = new Model("child", new Model("grandchild"));
        var root = new Model("root", child);
        using var source = Tree([root, new Model("other")]);
        var rootId = Id(source, 0);
        var childId = Id(source, 0, 0);
        var grandchildId = Id(source, 0, 0, 0);
        Assert.Equal(2, source.Rows.Count);
        Assert.True(source.TryGetRow(childId, out var hidden));
        Assert.Same(child, hidden!.Model);
        Assert.Equal(1, hidden.Depth);
        Assert.Equal(-1, source.GetViewIndex(childId));
        Assert.True(source.SetExpanded(rootId, true));
        Assert.True(source.SetExpanded(childId, true));
        Assert.Equal(new[] { "root", "child", "grandchild", "other" }, Names(source));
        Assert.Equal(2, source.GetViewIndex(grandchildId));
        Assert.True(source.SetExpanded(rootId, false));
        Assert.Equal(-1, source.GetViewIndex(grandchildId));
        Assert.Equal(grandchildId, Id(source, 0, 0, 0));
        Assert.True(source.SetExpanded(rootId, true));
        Assert.Equal(2, source.GetViewIndex(grandchildId));
        Assert.False(source.SetExpanded(rootId, true));
        Assert.False(source.SetExpanded(default, true));
        Assert.False(source.TryGetNodeId(new IndexPath(8), out _));
    }

    [Fact]
    public void Indexed_move_preserves_exact_occurrence_identity_even_for_duplicate_models()
    {
        var repeated = new Model("same");
        var models = new ObservableCollection<Model> { repeated, repeated, new("other") };
        using var source = new FlatTreeDataGridSource<Model>(models);
        var first = Id(source, 0);
        var second = Id(source, 1);
        Assert.NotEqual(first, second);
        models.Move(0, 2);
        Assert.Equal(second, Id(source, 0));
        Assert.Equal(first, Id(source, 2));
        Assert.Equal(2, source.GetViewIndex(first));
        Assert.Equal(new IndexPath(2), source.Rows[2].ModelIndex);
    }

    [Fact]
    public void Add_remove_replace_and_reset_have_explicit_identity_semantics()
    {
        var a = new Model("a");
        var models = new ResetCollection<Model>([a, new("b")]);
        using var source = new FlatTreeDataGridSource<Model>(models);
        var aId = Id(source, 0);
        var bId = Id(source, 1);
        ProjectionChangedEventArgs? last = null;
        source.ProjectionChanged += (_, args) => last = args;
        models.Insert(0, new Model("inserted"));
        Assert.Equal(aId, Id(source, 1));
        Assert.Equal(bId, Id(source, 2));
        models.RemoveAt(1);
        Assert.Equal(new[] { aId }, last!.RemovedNodes);
        Assert.False(source.TryGetRow(aId, out _));
        var beforeReplace = Id(source, 0);
        models[0] = models[0];
        Assert.NotEqual(beforeReplace, Id(source, 0));
        Assert.Equal(new[] { beforeReplace }, last!.RemovedNodes);
        var beforeReset = source.Rows.Select(row => row.Id).ToArray();
        models.ResetTo(models.ToArray());
        Assert.Equal(beforeReset, last!.RemovedNodes);
        Assert.Empty(beforeReset.Intersect(source.Rows.Select(row => row.Id)));
    }

    [Fact]
    public void Removing_a_collapsed_parent_reports_all_removed_descendants()
    {
        var roots = new ObservableCollection<Model> { new("parent", new Model("child", new Model("grandchild"))) };
        using var source = Tree(roots);
        var ids = new[] { Id(source, 0), Id(source, 0, 0), Id(source, 0, 0, 0) };
        ProjectionChangedEventArgs? last = null;
        source.ProjectionChanged += (_, args) => last = args;
        roots.Clear();
        Assert.Equal(ids, last!.RemovedNodes);
        Assert.Empty(source.Rows);
        foreach (var id in ids)
            Assert.False(source.TryGetRow(id, out _));
    }

    [Fact]
    public void Shared_collections_and_models_keep_separate_nodes_but_single_subscriptions()
    {
        var leaf = new Model("shared");
        var shared = new TrackingCollection<Model> { leaf, leaf };
        var parent = new Model("parent") { Children = shared };
        var roots = new TrackingCollection<Model> { parent, parent };
        using var source = Tree(roots);
        Assert.Equal(1, parent.Subscribers);
        Assert.Equal(1, leaf.Subscribers);
        Assert.Equal(1, shared.Subscribers);
        Assert.NotEqual(Id(source, 0, 0), Id(source, 1, 0));
        Assert.NotEqual(Id(source, 0, 0), Id(source, 0, 1));
        var first = Id(source, 0, 0);
        var second = Id(source, 1, 0);
        var notifications = 0;
        source.ProjectionChanged += (_, _) => notifications++;
        shared.Move(0, 1);
        Assert.Equal(first, Id(source, 0, 1));
        Assert.Equal(second, Id(source, 1, 1));
        Assert.Equal(1, notifications);
        leaf.Name = "updated";
        Assert.Equal(2, notifications);
        roots.RemoveAt(0);
        Assert.Equal(1, shared.Subscribers);
        Assert.Equal(1, leaf.Subscribers);
        Assert.Equal(second, Id(source, 0, 1));
        roots.Clear();
        Assert.Equal(0, parent.Subscribers);
        Assert.Equal(0, shared.Subscribers);
        Assert.Equal(0, leaf.Subscribers);
    }

    [Fact]
    public void Replacing_child_collection_releases_old_subscriptions_and_creates_new_nodes()
    {
        var leaf = new Model("leaf");
        var oldChildren = new TrackingCollection<Model> { leaf };
        var parent = new Model("parent") { Children = oldChildren };
        using var source = Tree([parent]);
        var parentId = Id(source, 0);
        var oldChildId = Id(source, 0, 0);
        source.SetExpanded(parentId, true);
        var nextChildren = new TrackingCollection<Model> { leaf };
        parent.Children = nextChildren;
        Assert.Equal(0, oldChildren.Subscribers);
        Assert.Equal(1, nextChildren.Subscribers);
        Assert.Equal(parentId, Id(source, 0));
        Assert.NotEqual(oldChildId, Id(source, 0, 0));
        Assert.Equal(2, source.Rows.Count);
        var version = source.Version;
        oldChildren.Add(new Model("detached"));
        Assert.Equal(version, source.Version);
        nextChildren.Add(new Model("attached"));
        Assert.Equal(3, source.Rows.Count);
    }

    [Fact]
    public void Notifications_observe_committed_maps_and_hold_immutable_snapshots()
    {
        var roots = new ObservableCollection<Model> { new("a"), new("b") };
        using var source = new FlatTreeDataGridSource<Model>(roots);
        var originalRows = source.Rows;
        var removedId = Id(source, 0);
        ProjectionChangedEventArgs? removal = null;
        source.ProjectionChanged += (_, args) =>
        {
            Assert.Equal(args.Version, source.Version);
            foreach (var row in source.Rows)
            {
                Assert.Equal(row.Id, Id(source, row.ModelIndex.ToArray()));
                Assert.Same(row, source.Rows[source.GetViewIndex(row.Id)]);
            }
            foreach (var id in args.RemovedNodes)
                Assert.False(source.TryGetRow(id, out var removedRow));
            removal ??= args;
        };
        roots.RemoveAt(0);
        roots.Add(new Model("c"));
        Assert.Equal(new[] { removedId }, removal!.RemovedNodes);
        Assert.Equal(2, originalRows.Count);
        Assert.Equal(removedId, originalRows[0].Id);
        Assert.Throws<NotSupportedException>(() => ((IList<NodeId>)removal.RemovedNodes).Add(default));
        Assert.Throws<NotSupportedException>(() => ((IList<TreeDataGridRow>)source.Rows).Clear());
    }

    [Fact]
    public void Dispose_unsubscribes_every_collection_and_model_and_is_idempotent()
    {
        var child = new Model("child");
        var parent = new Model("parent", child);
        var roots = new TrackingCollection<Model> { parent };
        var source = Tree(roots);
        var notifications = 0;
        source.ProjectionChanged += (_, _) => notifications++;
        Assert.Equal(1, roots.Subscribers);
        Assert.Equal(1, parent.Children!.Subscribers);
        source.Dispose();
        source.Dispose();
        Assert.Equal(0, roots.Subscribers);
        Assert.Equal(0, parent.Children.Subscribers);
        Assert.Equal(0, parent.Subscribers);
        Assert.Equal(0, child.Subscribers);
        child.Name = "detached";
        roots.Add(new Model("other"));
        Assert.Equal(0, notifications);
        Assert.Throws<ObjectDisposedException>(() => source.Refresh());
        Assert.Throws<ObjectDisposedException>(() => source.Rows);
    }

    [Fact]
    public void Wrong_thread_access_and_notifications_are_rejected_and_owner_can_reconcile()
    {
        var roots = new ObservableCollection<Model> { new("one") };
        using var source = new FlatTreeDataGridSource<Model>(roots);
        Assert.IsType<InvalidOperationException>(OnOtherThread(() => _ = source.Rows));
        Assert.IsType<InvalidOperationException>(OnOtherThread(source.Refresh));
        Assert.IsType<InvalidOperationException>(OnOtherThread(() => roots.Add(new Model("two"))));
        Assert.Single(source.Rows);
        source.Refresh();
        Assert.Equal(2, source.Rows.Count);
    }

    [Fact]
    public void Cycles_and_excessive_depth_fail_without_leaking_subscriptions()
    {
        var cyclic = new Model("cycle");
        cyclic.Children!.Add(cyclic);
        Assert.Throws<InvalidOperationException>(() => Tree([cyclic]));
        Assert.Equal(0, cyclic.Subscribers);
        Assert.Equal(0, cyclic.Children.Subscribers);
        var deepest = new Model("deep");
        var root = deepest;
        for (var i = 1; i < HierarchicalTreeDataGridSource<Model>.MaximumDepth; i++)
            root = new Model("parent", root);
        using var source = Tree([root]);
        Assert.Throws<InvalidOperationException>(() => deepest.Children!.Add(new Model("too deep")));
        Assert.Single(source.Rows);
        deepest.Children!.Clear();
        source.Refresh();
        Assert.Single(source.Rows);
    }

    [Fact]
    public void Refresh_reconciles_non_notifying_collections_by_reference_occurrence()
    {
        var repeated = new Model("same");
        var roots = new List<Model> { repeated, new("other"), repeated };
        using var source = new FlatTreeDataGridSource<Model>(roots);
        var first = Id(source, 0);
        var second = Id(source, 2);
        roots.RemoveAt(1);
        roots.Add(new Model("added"));
        source.Refresh();
        Assert.Equal(first, Id(source, 0));
        Assert.Equal(second, Id(source, 1));
        Assert.Equal(new[] { "same", "same", "added" }, Names(source));
    }

    [Fact]
    public void Identities_are_global_across_source_instances_and_model_types()
    {
        using var models = new FlatTreeDataGridSource<Model>([new("a")]);
        using var strings = new FlatTreeDataGridSource<string>(["a"]);
        Assert.NotEqual(models.Rows[0].Id, strings.Rows[0].Id);
        Assert.NotEqual(default, strings.Rows[0].Id);
    }

    [Fact]
    public void Nested_defer_scopes_preserve_old_projection_until_single_final_notification()
    {
        var roots = new ObservableCollection<Model> { new("one"), new("two") };
        using var source = Tree(roots);
        var oldRows = source.Rows;
        var oldVersion = source.Version;
        var removed = Id(source, 0);
        var surviving = Id(source, 1);
        var events = new List<ProjectionChangedEventArgs>();
        source.ProjectionChanged += (_, args) => events.Add(args);
        Assert.False(source.IsRefreshDeferred);
        using (source.DeferRefresh())
        {
            Assert.True(source.IsRefreshDeferred);
            roots.RemoveAt(0);
            using (source.DeferRefresh())
            {
                roots.Add(new Model("three"));
                roots.Move(0, 1);
                Assert.True(source.IsRefreshDeferred);
            }
            Assert.True(source.IsRefreshDeferred);
            Assert.Same(oldRows, source.Rows);
            Assert.Equal(oldVersion, source.Version);
            Assert.Equal(removed, Id(source, 0));
            Assert.Empty(events);
        }
        Assert.False(source.IsRefreshDeferred);
        Assert.Equal(oldVersion + 1, source.Version);
        Assert.Equal(new[] { "three", "two" }, Names(source));
        Assert.Equal(surviving, Id(source, 1));
        Assert.Equal(new[] { removed }, Assert.Single(events).RemovedNodes);
    }

    [Fact]
    public void Deferred_updates_subscribe_to_new_child_collections_immediately()
    {
        var parent = new Model("parent");
        using var source = Tree([parent]);
        var rootId = Id(source, 0);
        var replacement = new TrackingCollection<Model> { new("child") };
        using (source.DeferRefresh())
        {
            parent.Children = replacement;
            Assert.Equal(1, replacement.Subscribers);
            Assert.Equal(1, replacement[0].Subscribers);
            replacement.Add(new Model("added"));
            source.SetExpanded(rootId, true);
            Assert.Single(source.Rows);
        }
        Assert.Equal(new[] { "parent", "child", "added" }, Names(source));
        using var emptyScope = source.DeferRefresh();
        var wrongThreadScope = source.DeferRefresh();
        Assert.IsType<InvalidOperationException>(OnOtherThread(wrongThreadScope.Dispose));
        wrongThreadScope.Dispose();
        wrongThreadScope.Dispose();
    }

    [Fact]
    public void Subscriber_mutations_are_batched_after_all_subscribers_see_current_version()
    {
        var roots = new ObservableCollection<Model> { new("a"), new("b") };
        using var source = new FlatTreeDataGridSource<Model>(roots);
        var aId = Id(source, 0);
        var bId = Id(source, 1);
        var triggered = false;
        var seenVersions = new List<long>();
        source.ProjectionChanged += (_, _) =>
        {
            if (triggered) return;
            triggered = true;
            roots.Insert(0, new Model("new"));
            roots.Move(1, 2);
        };
        source.ProjectionChanged += (_, args) =>
        {
            Assert.Equal(args.Version, source.Version);
            seenVersions.Add(args.Version);
        };
        source.Refresh();
        Assert.Equal(new long[] { 2, 3 }, seenVersions);
        Assert.Equal(new[] { "new", "b", "a" }, Names(source));
        Assert.Equal(bId, Id(source, 1));
        Assert.Equal(aId, Id(source, 2));
    }

    [Fact]
    public void Seeded_random_changes_preserve_identity_and_all_flat_mapping_invariants()
    {
        const int seed = 45117;
        var random = new Random(seed);
        var pool = Enumerable.Range(0, 12).Select(i => new Model($"m{i}")).ToArray();
        var models = new ResetCollection<Model>([]);
        using var source = new FlatTreeDataGridSource<Model>(models);
        var expected = new List<NodeId>();
        for (var step = 0; step < 500; step++)
        {
            var operation = models.Count == 0 ? 0 : random.Next(5);
            var index = models.Count == 0 ? 0 : random.Next(models.Count);
            switch (operation)
            {
                case 0:
                    models.Insert(index, pool[random.Next(pool.Length)]);
                    expected.Insert(index, Id(source, index));
                    break;
                case 1:
                    models.RemoveAt(index);
                    expected.RemoveAt(index);
                    break;
                case 2:
                    var oldId = expected[index];
                    models[index] = pool[random.Next(pool.Length)];
                    expected[index] = Id(source, index);
                    Assert.NotEqual(oldId, expected[index]);
                    break;
                case 3:
                    var destination = random.Next(models.Count);
                    var moving = expected[index];
                    models.Move(index, destination);
                    expected.RemoveAt(index);
                    expected.Insert(destination, moving);
                    break;
                case 4:
                    var previous = expected.ToArray();
                    models.ResetTo(models.ToArray());
                    expected = source.Rows.Select(row => row.Id).ToList();
                    Assert.Empty(previous.Intersect(expected));
                    break;
            }
            Assert.Equal(models.Count, source.Rows.Count);
            Assert.Equal(expected.Count, expected.Distinct().Count());
            for (var i = 0; i < models.Count; i++)
            {
                Assert.Equal(expected[i], Id(source, i));
                Assert.Same(models[i], source.Rows[i].Model);
                Assert.Equal(i, source.GetViewIndex(expected[i]));
                Assert.Equal(new IndexPath(i), source.Rows[i].ModelIndex);
                Assert.True(source.TryGetRow(expected[i], out var resolved));
                Assert.Same(source.Rows[i], resolved);
            }
        }
    }

    [Fact]
    public void Seeded_tree_changes_keep_source_paths_and_visible_preorder_consistent()
    {
        var random = new Random(89231);
        var roots = new TrackingCollection<Model>(Enumerable.Range(0, 5)
            .Select(i => new Model($"r{i}", new Model($"c{i}"))));
        using var source = Tree(roots);
        for (var step = 0; step < 300; step++)
        {
            var collections = new List<TrackingCollection<Model>> { roots };
            void FindCollections(IEnumerable<Model> siblings)
            {
                foreach (var model in siblings)
                {
                    if (model.Children is not { } children) continue;
                    collections.Add(children);
                    FindCollections(children);
                }
            }
            FindCollections(roots);
            var collection = collections[random.Next(collections.Count)];
            var operation = collection.Count == 0 ? 0 : random.Next(5);
            var index = collection.Count == 0 ? 0 : random.Next(collection.Count);
            switch (operation)
            {
                case 0:
                    collection.Insert(index, new Model($"insert-{step}"));
                    break;
                case 1:
                    collection.RemoveAt(index);
                    break;
                case 2:
                    collection.Move(index, random.Next(collection.Count));
                    break;
                case 3:
                    collection[index] = new Model($"replace-{step}", new Model($"child-{step}"));
                    break;
                case 4:
                    collection[index].Name = $"renamed-{step}";
                    break;
            }

            // Toggle any node, including currently hidden nodes, using its stable source path.
            var allPaths = new List<IndexPath>();
            void GatherPaths(IEnumerable<Model> siblings, IndexPath parent)
            {
                var index = 0;
                foreach (var model in siblings)
                {
                    var path = parent.Append(index++);
                    allPaths.Add(path);
                    GatherPaths(model.Children ?? [], path);
                }
            }
            GatherPaths(roots, default);
            if (allPaths.Count != 0)
            {
                var id = Id(source, allPaths[random.Next(allPaths.Count)].ToArray());
                Assert.True(source.TryGetRow(id, out var row));
                source.SetExpanded(id, !row!.IsExpanded);
            }

            var expectedVisible = new List<NodeId>();
            var allIds = new HashSet<NodeId>();
            void Validate(IEnumerable<Model> siblings, IndexPath parent, bool visible)
            {
                var index = 0;
                foreach (var model in siblings)
                {
                    var path = parent.Append(index++);
                    var id = Id(source, path.ToArray());
                    Assert.True(allIds.Add(id));
                    Assert.True(source.TryGetRow(id, out var row));
                    Assert.Same(model, row!.Model);
                    Assert.Equal(path, row.ModelIndex);
                    Assert.Equal(path.Count - 1, row.Depth);
                    Assert.Equal(model.Children?.Count > 0, row.HasChildren);
                    if (visible)
                    {
                        Assert.Equal(expectedVisible.Count, source.GetViewIndex(id));
                        expectedVisible.Add(id);
                    }
                    else
                    {
                        Assert.Equal(-1, source.GetViewIndex(id));
                    }
                    Validate(model.Children ?? [], path, visible && row.IsExpanded);
                }
            }
            Validate(roots, default, true);
            Assert.Equal(expectedVisible, source.Rows.Select(row => row.Id));
        }
    }

    [Fact]
    public void Invalid_graph_change_retains_last_valid_projection_and_can_be_corrected()
    {
        var parent = new Model("root", new Model("child"));
        using var source = Tree([parent]);
        var child = parent.Children![0];
        var originalRows = source.Rows;
        var version = source.Version;
        var originalChildId = Id(source, 0, 0);
        Assert.Throws<InvalidOperationException>(() => child.Children!.Add(parent));
        Assert.Same(originalRows, source.Rows);
        Assert.Equal(version, source.Version);
        child.Children!.Clear();
        Assert.Equal(originalChildId, Id(source, 0, 0));
        Assert.Equal(version + 1, source.Version);
        Assert.Equal(1, parent.Subscribers);
        Assert.Equal(1, child.Subscribers);
    }

    [Fact]
    public void Throwing_subscriber_does_not_skip_other_listeners_or_lose_queued_moves()
    {
        var roots = new ObservableCollection<Model> { new("a"), new("b") };
        using var source = new FlatTreeDataGridSource<Model>(roots);
        var aId = Id(source, 0);
        var bId = Id(source, 1);
        var firstNotification = true;
        var observed = new List<long>();
        source.ProjectionChanged += (_, _) =>
        {
            if (!firstNotification) return;
            firstNotification = false;
            roots.Insert(0, new Model("new"));
            roots.Move(1, 2);
            throw new FormatException("user callback failure");
        };
        source.ProjectionChanged += (_, args) =>
        {
            Assert.Equal(source.Version, args.Version);
            observed.Add(args.Version);
        };
        Assert.Throws<FormatException>(source.Refresh);
        Assert.Equal(new long[] { 2, 3 }, observed);
        Assert.Equal(new[] { "new", "b", "a" }, Names(source));
        Assert.Equal(aId, Id(source, 2));
        Assert.Equal(bId, Id(source, 1));
        roots.RemoveAt(0);
        Assert.Equal(aId, Id(source, 1));
        Assert.Equal(bId, Id(source, 0));
    }

    [Fact]
    public void Scope_disposal_restores_normal_updates_after_listener_exception()
    {
        var roots = new ObservableCollection<Model> { new("a") };
        using var source = new FlatTreeDataGridSource<Model>(roots);
        EventHandler<ProjectionChangedEventArgs> failing = (_, _) => throw new FormatException("listener");
        source.ProjectionChanged += failing;
        var scope = source.DeferRefresh();
        roots.Add(new Model("b"));
        Assert.Throws<FormatException>(scope.Dispose);
        scope.Dispose();
        Assert.False(source.IsRefreshDeferred);
        source.ProjectionChanged -= failing;
        roots.Add(new Model("c"));
        Assert.Equal(new[] { "a", "b", "c" }, Names(source));
        Assert.Equal(3, source.Version);
    }

    [Fact]
    public void Callback_queued_removal_blocks_editing_against_a_stale_published_projection()
    {
        var roots = new ObservableCollection<Model> { new("target") };
        using var source = new FlatTreeDataGridSource<Model>(roots);
        using var state = new TreeDataGridState(source);
        var writes = 0;
        state.BeginEdit(new CellAddress(source.Rows[0].Id, new ColumnId(1)), model => ((Model)model).Name,
            (_, _) => writes++);
        var mutated = false;
        var attemptedEdit = false;
        source.ProjectionChanged += (_, _) =>
        {
            if (mutated) return;
            mutated = true;
            roots.Clear();
            Assert.True(source.IsRefreshDeferred);
        };
        source.ProjectionChanged += (_, _) =>
        {
            if (attemptedEdit) return;
            attemptedEdit = true;
            Assert.Throws<InvalidOperationException>(() => state.CommitEdit("stale write"));
        };
        source.Refresh();
        Assert.True(attemptedEdit);
        Assert.Equal(0, writes);
        Assert.Empty(source.Rows);
        Assert.Null(state.EditSession);
        Assert.False(source.IsRefreshDeferred);
    }

    private static HierarchicalTreeDataGridSource<Model> Tree(IEnumerable<Model> models) => new(models, model => model.Children);

    private static NodeId Id(TreeDataGridSource source, params int[] path)
    {
        Assert.True(source.TryGetNodeId(new IndexPath(path), out var id));
        return id;
    }

    private static string[] Names(TreeDataGridSource source) => source.Rows.Select(row => ((Model)row.Model).Name).ToArray();

    private static Exception? OnOtherThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception caught) { exception = caught; }
        });
        thread.Start();
        thread.Join();
        return exception;
    }

    private sealed class Model(string name, params Model[] children) : INotifyPropertyChanged
    {
        private string _name = name;
        private TrackingCollection<Model>? _children = new(children);
        private PropertyChangedEventHandler? _propertyChanged;
        public int Subscribers { get; private set; }
        public string Name
        {
            get => _name;
            set { _name = value; _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }
        public TrackingCollection<Model>? Children
        {
            get => _children;
            set { _children = value; _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Children))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { Subscribers++; _propertyChanged += value; }
            remove { Subscribers--; _propertyChanged -= value; }
        }
    }

    private sealed class TrackingCollection<T>(IEnumerable<T> items) : ObservableCollection<T>(items), INotifyCollectionChanged
    {
        public TrackingCollection() : this(Array.Empty<T>()) { }
        public int Subscribers { get; private set; }
        event NotifyCollectionChangedEventHandler? INotifyCollectionChanged.CollectionChanged
        {
            add { Subscribers++; CollectionChanged += value; }
            remove { Subscribers--; CollectionChanged -= value; }
        }
    }

    private sealed class ResetCollection<T>(IEnumerable<T> items) : ObservableCollection<T>(items)
    {
        public void ResetTo(IEnumerable<T> values)
        {
            var snapshot = values.ToArray();
            Items.Clear();
            foreach (var value in snapshot)
                Items.Add(value);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
