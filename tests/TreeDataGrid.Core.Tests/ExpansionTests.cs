using System.Collections.ObjectModel;
using System.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Core.Tests;

public sealed class ExpansionTests
{
    [Fact]
    public void Single_node_events_surround_one_committed_projection_and_use_model_rows()
    {
        using var source = Tree([new("root", new Model("child"))]);
        var id = source.Rows[0].Id;
        var originalVersion = source.Version;
        var order = new List<string>();
        source.RowExpanding += (_, args) =>
        {
            Assert.Equal(id, args.Row.Id);
            Assert.False(args.Row.IsExpanded);
            Assert.Equal(originalVersion, source.Version);
            Assert.True(source.IsRefreshDeferred);
            order.Add("before expand");
        };
        source.RowExpanded += (_, args) =>
        {
            Assert.True(args.Row.IsExpanded);
            Assert.True(source.TryGetRow(id, out var row));
            Assert.Same(row, args.Row);
            Assert.Equal(2, source.Rows.Count);
            order.Add("after expand");
        };
        source.RowCollapsing += (_, args) => { Assert.True(args.Row.IsExpanded); order.Add("before collapse"); };
        source.RowCollapsed += (_, args) => { Assert.False(args.Row.IsExpanded); order.Add("after collapse"); };
        source.ProjectionChanged += (_, args) =>
        {
            Assert.Equal(ProjectionChangeReason.ExpansionChanged, args.Reason);
            order.Add("projection");
        };
        Assert.True(source.SetExpandedAt(new IndexPath(0), true));
        Assert.False(source.SetExpanded(id, true));
        Assert.True(source.SetExpanded(id, false));
        Assert.Equal(new[] { "before expand", "projection", "after expand", "before collapse", "projection", "after collapse" }, order);
        Assert.Equal(originalVersion + 2, source.Version);
        Assert.False(source.SetExpandedAt(new IndexPath(99), true));
    }

    [Fact]
    public void Cancellation_retains_child_loading_but_does_not_expand_or_send_completion()
    {
        var root = new Model("lazy") { MayHaveChildren = true };
        using var source = Tree([root]);
        var id = source.Rows[0].Id;
        var completed = 0;
        source.RowExpanding += (_, args) =>
        {
            root.Children.Add(new Model("loaded"));
            args.Cancel = true;
        };
        source.RowExpanding += (_, args) => args.Cancel = false; // Cancellation cannot be undone by a later listener.
        source.RowExpanded += (_, _) => completed++;
        Assert.False(source.SetExpanded(id, true));
        Assert.Single(source.Rows);
        Assert.True(source.TryGetNodeId(new IndexPath(0, 0), out _));
        Assert.False(source.Rows[0].IsExpanded);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void Lazy_expansion_visits_synchronously_loaded_descendants_in_one_projection()
    {
        var root = new Model("lazy") { MayHaveChildren = true };
        using var source = Tree([root]);
        var projections = 0;
        var before = new List<string>();
        var after = new List<string>();
        Assert.True(source.Rows[0].HasChildren);
        source.RowExpanding += (_, args) =>
        {
            var model = (Model)args.Row.Model;
            before.Add(model.Name);
            if (model.Name == "lazy") model.Children.Add(new Model("branch", new Model("leaf")));
        };
        source.RowExpanded += (_, args) => after.Add(((Model)args.Row.Model).Name);
        source.ProjectionChanged += (_, _) => projections++;
        Assert.Equal(2, source.ExpandAll());
        Assert.Equal(new[] { "lazy", "branch", "leaf" }, Names(source));
        Assert.Equal(new[] { "lazy", "branch" }, before);
        Assert.Equal(before, after);
        Assert.Equal(1, projections);
    }

    [Fact]
    public void All_and_conditional_operations_prune_subtrees_and_collapse_in_postorder()
    {
        using var source = Tree([new("root", new Model("child", new Model("leaf"))), new("other", new Model("hidden"))]);
        Assert.Equal(1, source.SetExpandedRecursive(true, row => ((Model)row.Model).Name == "root"));
        Assert.Equal(new[] { "root", "child", "other" }, Names(source));
        Assert.Equal(2, source.ExpandAll());
        var order = new List<string>();
        var projected = 0;
        source.RowCollapsing += (_, args) => order.Add(((Model)args.Row.Model).Name);
        source.ProjectionChanged += (_, _) => projected++;
        Assert.Equal(3, source.CollapseAll());
        Assert.Equal(new[] { "child", "root", "other" }, order);
        Assert.Equal(new[] { "root", "other" }, Names(source));
        Assert.Equal(1, projected);
        Assert.Equal(0, source.CollapseAll());
    }

    [Fact]
    public void Cancelled_branch_is_not_recursively_visited()
    {
        using var source = Tree([new("root", new Model("branch", new Model("leaf")))]);
        var calls = 0;
        source.RowExpanding += (_, args) => { calls++; args.Cancel = true; };
        Assert.Equal(0, source.ExpandAll());
        Assert.Equal(1, calls);
        Assert.Equal(1, source.Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Before_event_removal_or_replace_never_completes_the_old_node(bool replace)
    {
        var roots = new ObservableCollection<Model> { new("target", new Model("child")) };
        using var source = Tree(roots);
        var oldId = source.Rows[0].Id;
        var completed = 0;
        source.RowExpanding += (_, _) =>
        {
            if (replace) roots[0] = roots[0];
            else roots.RemoveAt(0);
        };
        source.RowExpanded += (_, _) => completed++;
        Assert.False(source.SetExpanded(oldId, true));
        Assert.Equal(0, completed);
        Assert.False(source.TryGetRow(oldId, out _));
        if (replace) Assert.False(source.Rows[0].IsExpanded);
    }

    [Fact]
    public void Projection_listener_deletion_suppresses_completion_for_removed_node()
    {
        var roots = new ObservableCollection<Model> { new("target", new Model("child")) };
        using var source = Tree(roots);
        var completed = 0;
        source.ProjectionChanged += (_, _) => { if (roots.Count != 0) roots.Clear(); };
        source.RowExpanded += (_, _) => completed++;
        Assert.True(source.SetExpanded(source.Rows[0].Id, true));
        Assert.Empty(source.Rows);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void Outer_defer_delays_completion_and_skips_superseded_expansion_state()
    {
        using var source = Tree([new("root", new Model("leaf"))]);
        var id = source.Rows[0].Id;
        var expanded = 0;
        var collapsed = 0;
        var projections = 0;
        source.RowExpanded += (_, _) => expanded++;
        source.RowCollapsed += (_, _) => collapsed++;
        source.ProjectionChanged += (_, _) => projections++;
        using (source.DeferRefresh())
        {
            Assert.True(source.SetExpanded(id, true));
            Assert.True(source.SetExpanded(id, false));
            Assert.Equal(0, expanded);
            Assert.Equal(0, collapsed);
            Assert.Equal(0, projections);
        }
        Assert.Equal(0, expanded);
        Assert.Equal(1, collapsed);
        Assert.Equal(1, projections);
    }

    [Fact]
    public void Repeated_toggles_in_one_scope_publish_only_the_last_completion_for_each_identity()
    {
        using var source = Tree([new("root", new Model("leaf"))]);
        var id = source.Rows[0].Id;
        var before = 0;
        var after = 0;
        source.RowExpanding += (_, _) => before++;
        source.RowExpanded += (_, _) => after++;
        using (source.DeferRefresh())
        {
            source.SetExpanded(id, true);
            source.SetExpanded(id, false);
            source.SetExpanded(id, true);
        }
        Assert.Equal(2, before);
        Assert.Equal(1, after);
        Assert.True(source.Rows[0].IsExpanded);
    }

    [Fact]
    public void Scope_opened_by_a_completion_listener_delays_remaining_completions_even_without_changes()
    {
        using var source = Tree([new("first", new Model("leaf")), new("second", new Model("leaf"))]);
        IDisposable? delayedScope = null;
        var completed = new List<string>();
        source.RowExpanded += (_, args) =>
        {
            var name = ((Model)args.Row.Model).Name;
            completed.Add(name);
            if (name == "first") delayedScope = source.DeferRefresh();
        };
        Assert.Equal(2, source.ExpandAll());
        Assert.Equal(new[] { "first" }, completed);
        Assert.True(source.IsRefreshDeferred);
        delayedScope!.Dispose();
        Assert.Equal(new[] { "first", "second" }, completed);
        Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Bound_expansion_reads_initial_and_external_state_without_command_events()
    {
        var root = new Model("root", new Model("child")) { IsExpanded = true };
        using var source = Tree([root], bound: true);
        Assert.Equal(2, source.Rows.Count);
        var commands = 0;
        ProjectionChangeReason? reason = null;
        source.RowCollapsing += (_, _) => commands++;
        source.RowCollapsed += (_, _) => commands++;
        source.ProjectionChanged += (_, args) => reason = args.Reason;
        root.IsExpanded = false;
        Assert.Single(source.Rows);
        Assert.Equal(0, commands);
        Assert.Equal(ProjectionChangeReason.ModelChanged, reason);
    }

    [Fact]
    public void Bound_shared_model_updates_all_occurrences_with_one_writer_and_group_cancellation()
    {
        var shared = new Model("shared", new Model("leaf"));
        var writes = 0;
        using var source = new HierarchicalTreeDataGridSource<Model>([shared, shared], model => model.Children,
            new HierarchicalTreeDataGridSourceOptions<Model>
            {
                IsExpandedSelector = model => model.IsExpanded,
                IsExpandedWriter = (model, value) => { writes++; model.IsExpanded = value; },
            });
        var first = source.Rows[0].Id;
        var second = source.Rows[1].Id;
        var before = new List<NodeId>();
        var after = new List<NodeId>();
        EventHandler<TreeDataGridRowCancelEventArgs> cancelSecond = (_, args) =>
        {
            before.Add(args.Row.Id);
            if (args.Row.Id == second) args.Cancel = true;
        };
        source.RowExpanding += cancelSecond;
        Assert.False(source.SetExpanded(first, true));
        Assert.Equal(new[] { first, second }, before);
        Assert.Equal(0, writes);
        Assert.False(shared.IsExpanded);
        source.RowExpanding -= cancelSecond;
        source.RowExpanded += (_, args) => after.Add(args.Row.Id);
        Assert.Equal(2, source.ExpandAll());
        Assert.Equal(new[] { first, second }, after);
        Assert.Equal(1, writes);
        Assert.True(shared.IsExpanded);
        Assert.Equal(4, source.Rows.Count);
    }

    [Fact]
    public void Without_binding_duplicate_model_expansion_is_occurrence_local()
    {
        var shared = new Model("shared", new Model("leaf"));
        using var source = Tree([shared, shared]);
        var second = source.Rows[1].Id;
        Assert.True(source.SetExpanded(source.Rows[0].Id, true));
        Assert.Equal(3, source.Rows.Count);
        Assert.True(source.TryGetRow(second, out var row));
        Assert.False(row!.IsExpanded);
        Assert.False(shared.IsExpanded);
    }

    [Fact]
    public void HasChildren_hint_is_combined_with_actual_children_and_leaf_expansion_is_retained()
    {
        var root = new Model("root");
        using var source = Tree([root]);
        var id = source.Rows[0].Id;
        Assert.False(source.Rows[0].HasChildren);
        root.MayHaveChildren = true;
        Assert.True(source.Rows[0].HasChildren);
        root.MayHaveChildren = false;
        Assert.True(source.SetExpanded(id, true));
        root.Children.Add(new Model("late"));
        Assert.True(source.Rows[0].HasChildren);
        Assert.Equal(2, source.Rows.Count);
        Assert.Equal(1, source.CollapseAll());
    }

    [Fact]
    public void Binding_delegate_pair_is_validated_at_construction()
    {
        Assert.Throws<ArgumentException>(() => new HierarchicalTreeDataGridSource<Model>([], model => model.Children,
            new HierarchicalTreeDataGridSourceOptions<Model> { IsExpandedSelector = model => model.IsExpanded }));
        Assert.Throws<ArgumentException>(() => new HierarchicalTreeDataGridSource<Model>([], model => model.Children,
            new HierarchicalTreeDataGridSourceOptions<Model> { IsExpandedWriter = (model, value) => model.IsExpanded = value }));
    }

    [Fact]
    public void Rejecting_or_ignoring_binding_writer_does_not_complete_and_can_be_retried()
    {
        var mode = 0;
        var model = new Model("root", new Model("child"));
        using var source = new HierarchicalTreeDataGridSource<Model>([model], item => item.Children,
            new HierarchicalTreeDataGridSourceOptions<Model>
            {
                IsExpandedSelector = item => item.IsExpanded,
                IsExpandedWriter = (item, value) =>
                {
                    if (mode == 0) throw new FormatException("writer rejected");
                    if (mode == 2) item.IsExpanded = value;
                },
            });
        var completed = 0;
        source.RowExpanded += (_, _) => completed++;
        Assert.Throws<FormatException>(() => source.ExpandAll());
        mode = 1;
        Assert.Throws<InvalidOperationException>(() => source.ExpandAll());
        Assert.False(source.IsRefreshDeferred);
        Assert.False(model.IsExpanded);
        Assert.False(source.Rows[0].IsExpanded);
        Assert.Equal(0, completed);
        mode = 2;
        Assert.Equal(1, source.ExpandAll());
        Assert.Equal(1, completed);
    }

    [Fact]
    public void Expansion_reentry_is_rejected_in_before_after_and_predicate_callbacks()
    {
        using var source = Tree([new("root", new Model("child"))]);
        var id = source.Rows[0].Id;
        source.RowExpanding += (_, _) => Assert.Throws<InvalidOperationException>(() => source.SetExpanded(id, false));
        source.RowExpanded += (_, _) => Assert.Throws<InvalidOperationException>(() => source.ExpandAll());
        Assert.Equal(1, source.SetExpandedRecursive(true, _ =>
        {
            Assert.Throws<InvalidOperationException>(() => source.CollapseAll());
            return true;
        }));
    }

    [Fact]
    public void Before_exception_commits_loaded_data_without_expanding_and_releases_operation_guard()
    {
        var root = new Model("root") { MayHaveChildren = true };
        using var source = Tree([root]);
        EventHandler<TreeDataGridRowCancelEventArgs> fail = (_, _) =>
        {
            root.Children.Add(new Model("loaded"));
            throw new FormatException("loader failed");
        };
        source.RowExpanding += fail;
        Assert.Throws<FormatException>(() => source.ExpandAll());
        Assert.Single(source.Rows);
        Assert.True(source.TryGetNodeId(new IndexPath(0, 0), out _));
        Assert.False(source.IsRefreshDeferred);
        source.RowExpanding -= fail;
        Assert.Equal(1, source.ExpandAll());
    }

    [Fact]
    public void After_exception_does_not_skip_other_completions_and_guards_are_released()
    {
        using var source = Tree([new("first", new Model("leaf")), new("second", new Model("leaf"))]);
        var events = new List<string>();
        EventHandler<TreeDataGridRowModelEventArgs> fail = (_, args) =>
        {
            events.Add(((Model)args.Row.Model).Name);
            if (((Model)args.Row.Model).Name == "first") throw new FormatException("listener failed");
        };
        source.RowExpanded += fail;
        Assert.Throws<FormatException>(() => source.ExpandAll());
        Assert.Equal(new[] { "first", "second" }, events);
        Assert.Equal(4, source.Rows.Count);
        Assert.False(source.IsRefreshDeferred);
        source.RowExpanded -= fail;
        Assert.Equal(2, source.CollapseAll());
    }

    [Fact]
    public void Dispose_in_before_event_cancels_pending_completions_and_detaches_models()
    {
        var root = new Model("root", new Model("child"));
        var source = Tree([root]);
        var completed = 0;
        source.RowExpanding += (_, _) => source.Dispose();
        source.RowExpanded += (_, _) => completed++;
        Assert.False(source.SetExpanded(source.Rows[0].Id, true));
        Assert.Equal(0, completed);
        root.Children.Add(new Model("detached"));
        Assert.Throws<ObjectDisposedException>(() => source.Rows);
    }

    [Fact]
    public void Bound_expand_all_reads_graph_at_most_twice_and_commits_once()
    {
        var roots = Enumerable.Range(0, 2500).Select(i => new Model($"root-{i}", new Model("leaf"))).ToArray();
        var childReads = 0;
        using var source = new HierarchicalTreeDataGridSource<Model>(roots, model => { childReads++; return model.Children; },
            new HierarchicalTreeDataGridSourceOptions<Model>
            {
                IsExpandedSelector = model => model.IsExpanded,
                IsExpandedWriter = (model, value) => model.IsExpanded = value,
            });
        childReads = 0;
        var projections = 0;
        source.ProjectionChanged += (_, _) => projections++;
        Assert.Equal(roots.Length, source.ExpandAll());
        Assert.Equal(1, projections);
        Assert.Equal(roots.Length * 2, source.Rows.Count);
        Assert.InRange(childReads, 0, roots.Length * 2);
    }

    [Fact]
    public void Maximum_depth_can_expand_and_failed_deeper_lazy_load_can_be_repaired()
    {
        var deepest = new Model("deepest") { MayHaveChildren = true };
        var root = deepest;
        for (var level = 1; level < HierarchicalTreeDataGridSource<Model>.MaximumDepth; level++)
            root = new Model($"level-{level}", root);
        using var source = Tree([root]);
        EventHandler<TreeDataGridRowCancelEventArgs> loadTooDeep = (_, args) =>
        {
            if (ReferenceEquals(args.Row.Model, deepest)) deepest.Children.Add(new Model("too deep"));
        };
        source.RowExpanding += loadTooDeep;
        Assert.Throws<InvalidOperationException>(() => source.ExpandAll());
        Assert.False(source.IsRefreshDeferred);
        source.RowExpanding -= loadTooDeep;
        deepest.Children.Clear();
        Assert.Equal(1, source.ExpandAll());
        Assert.Equal(HierarchicalTreeDataGridSource<Model>.MaximumDepth, source.Rows.Count);
        Assert.Equal(HierarchicalTreeDataGridSource<Model>.MaximumDepth, source.CollapseAll());
        Assert.Single(source.Rows);
    }

    [Fact]
    public void Moving_target_during_before_event_keeps_identity_and_updates_completed_path()
    {
        var roots = new ObservableCollection<Model> { new("first", new Model("child")), new("second") };
        using var source = Tree(roots);
        var target = source.Rows[0].Id;
        source.RowExpanding += (_, _) => roots.Move(0, 1);
        TreeDataGridRow? completed = null;
        source.RowExpanded += (_, args) => completed = args.Row;
        Assert.True(source.SetExpanded(target, true));
        Assert.Equal(target, completed!.Id);
        Assert.Equal(new IndexPath(1), completed.ModelIndex);
        Assert.Equal(1, source.GetViewIndex(target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Random_tree_commands_loading_and_mutations_preserve_projection_invariants(bool bound)
    {
        var random = new Random(57911);
        var roots = new ObservableCollection<Model>(Enumerable.Range(0, 5)
            .Select(index => new Model($"r-{index}", new Model($"c-{index}"))));
        using var source = Tree(roots, bound);
        source.RowExpanding += (_, args) =>
        {
            var model = (Model)args.Row.Model;
            if (model.MayHaveChildren && model.Children.Count == 0 && args.Row.Depth < 4)
                model.Children.Add(new Model($"loaded-{model.Name}"));
        };
        source.RowExpanded += (_, args) =>
        {
            Assert.True(source.TryGetRow(args.Row.Id, out var current));
            Assert.Same(current, args.Row);
            Assert.True(current!.IsExpanded);
        };
        source.RowCollapsed += (_, args) =>
        {
            Assert.True(source.TryGetRow(args.Row.Id, out var current));
            Assert.Same(current, args.Row);
            Assert.False(current!.IsExpanded);
        };

        for (var step = 0; step < 300; step++)
        {
            var allRows = new List<TreeDataGridRow>();
            void Gather(IndexPath parent)
            {
                for (var index = 0; source.TryGetNodeId(parent.Append(index), out var id); index++)
                {
                    Assert.True(source.TryGetRow(id, out var row));
                    allRows.Add(row!);
                    Gather(parent.Append(index));
                }
            }
            Gather(default);
            var target = allRows.Count == 0 ? null : allRows[random.Next(allRows.Count)];
            var model = target?.Model as Model;
            switch (target is null ? 0 : random.Next(9))
            {
                case 0:
                    roots.Add(new Model($"add-{step}") { MayHaveChildren = random.Next(2) == 0 });
                    break;
                case 1:
                    source.ExpandAll();
                    break;
                case 2:
                    source.CollapseAll();
                    break;
                case 3:
                    var maxDepth = random.Next(1, 4);
                    source.SetExpandedRecursive(true, row => row.Depth < maxDepth);
                    break;
                case 4:
                    source.SetExpanded(target!.Id, !target.IsExpanded);
                    break;
                case 5:
                    if (target!.Depth < 4) model!.Children.Add(new Model($"child-{step}"));
                    break;
                case 6:
                    if (model!.Children.Count != 0) model.Children.RemoveAt(random.Next(model.Children.Count));
                    else if (roots.Count != 0) roots.RemoveAt(random.Next(roots.Count));
                    break;
                case 7:
                    if (bound) model!.IsExpanded = !model.IsExpanded;
                    else if (roots.Count > 1) roots.Move(0, roots.Count - 1);
                    break;
                case 8:
                    using (source.DeferRefresh())
                    {
                        source.SetExpanded(target!.Id, true);
                        source.SetExpanded(target.Id, false);
                        source.SetExpanded(target.Id, true);
                    }
                    break;
            }

            var expectedVisible = new List<NodeId>();
            var ids = new HashSet<NodeId>();
            void Validate(IEnumerable<Model> siblings, IndexPath parent, bool visible)
            {
                var index = 0;
                foreach (var item in siblings)
                {
                    var path = parent.Append(index++);
                    Assert.True(source.TryGetNodeId(path, out var id));
                    Assert.True(ids.Add(id));
                    Assert.True(source.TryGetRow(id, out var row));
                    Assert.Same(item, row!.Model);
                    Assert.Equal(path, row.ModelIndex);
                    Assert.Equal(item.Children.Count != 0 || item.MayHaveChildren, row.HasChildren);
                    if (bound) Assert.Equal(item.IsExpanded, row.IsExpanded);
                    if (visible)
                    {
                        Assert.Equal(expectedVisible.Count, source.GetViewIndex(id));
                        expectedVisible.Add(id);
                    }
                    else Assert.Equal(-1, source.GetViewIndex(id));
                    Validate(item.Children, path, visible && row.IsExpanded);
                }
            }
            Validate(roots, default, true);
            Assert.Equal(expectedVisible, source.Rows.Select(row => row.Id));
            Assert.False(source.IsRefreshDeferred);
        }
    }

    private static HierarchicalTreeDataGridSource<Model> Tree(IEnumerable<Model> items, bool bound = false) =>
        new(items, model => model.Children, new HierarchicalTreeDataGridSourceOptions<Model>
        {
            HasChildrenSelector = model => model.MayHaveChildren,
            IsExpandedSelector = bound ? model => model.IsExpanded : null,
            IsExpandedWriter = bound ? (model, value) => model.IsExpanded = value : null,
        });

    private static string[] Names(TreeDataGridSource source) => source.Rows.Select(row => ((Model)row.Model).Name).ToArray();

    private sealed class Model(string name, params Model[] children) : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _mayHaveChildren;
        public string Name { get; } = name;
        public ObservableCollection<Model> Children { get; } = new(children);
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); }
        }
        public bool MayHaveChildren
        {
            get => _mayHaveChildren;
            set { _mayHaveChildren = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MayHaveChildren))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
