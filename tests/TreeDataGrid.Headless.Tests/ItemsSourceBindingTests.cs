using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Metadata;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class ItemsSourceBindingTests
{
    [AvaloniaFact]
    public void ItemsSource_binding_tracks_viewmodel_replacement_and_releases_previous_models()
    {
        var first = new Model("first"); var second = new Model("second");
        var vm = new ViewModel { Items = new ObservableCollection<Model> { first } };
        var grid = new GridControl { DataContext = vm };
        using var binding = grid.Bind(GridControl.ItemsSourceProperty, new ReflectionBinding(nameof(ViewModel.Items)));
        var firstSource = grid.Source!;
        Assert.Same(first, Assert.Single(firstSource.Rows).Model);
        Assert.True(first.SubscriptionCount > 0);
        vm.Items = new ObservableCollection<Model> { second };
        Assert.Same(vm.Items, grid.ItemsSource);
        Assert.Same(second, Assert.Single(grid.Source!.Rows).Model);
        Assert.Equal(0, first.SubscriptionCount);
        Assert.Throws<ObjectDisposedException>(() => firstSource.Rows);
        vm.Items = null;
        Assert.Null(grid.Source);
        Assert.Equal(0, second.SubscriptionCount);
    }

    [AvaloniaFact]
    public void Compiled_viewmodel_binding_can_supply_ItemsSource_while_hierarchy_rejects_compiled_bindings()
    {
        var vm = new ViewModel { Items = new[] { new Model("first") } };
        var grid = new GridControl { DataContext = vm };
        using var binding = grid.Bind(GridControl.ItemsSourceProperty, CompiledBinding.Create<ViewModel, IEnumerable?>(model => model.Items));
        try
        {
            Assert.Equal("first", ((Model)Assert.Single(grid.Source!.Rows).Model).Name);
            vm.Items = new[] { new Model("second") };
            Assert.Equal("second", ((Model)Assert.Single(grid.Source!.Rows).Model).Name);
            var column = new TreeDataGridHierarchicalExpanderColumn();
            Assert.Throws<NotSupportedException>(() => column.ChildrenBinding = CompiledBinding.Create<Model, ObservableCollection<Model>>(model => model.Children));
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Non_generic_collection_notifications_preserve_repeated_occurrence_identity()
    {
        var shared = new Model("shared");
        var items = new ObservableCollection<Model> { shared, new("other"), shared };
        var grid = new GridControl { ItemsSource = (IEnumerable)items };
        try
        {
            var source = grid.Source!;
            var ids = source.Rows.Select(row => row.Id).ToArray();
            items.Move(0, 2);
            Assert.Equal(new[] { ids[1], ids[2], ids[0] }, source.Rows.Select(row => row.Id));
            shared.Name = "updated";
            Assert.Equal("updated", ((Model)source.Rows[2].Model).Name);
            items.RemoveAt(1);
            Assert.False(source.TryGetRow(ids[2], out _));
            Assert.True(source.TryGetRow(ids[0], out _));
        }
        finally { grid.ItemsSource = null; }
        Assert.Equal(0, shared.SubscriptionCount);
    }

    [AvaloniaFact]
    public void Boxed_value_occurrences_survive_move_insert_and_refresh()
    {
        var items = new ObservableCollection<int> { 1, 1, 2 };
        var grid = new GridControl { ItemsSource = items };
        try
        {
            var source = grid.Source!;
            var ids = source.Rows.Select(row => row.Id).ToArray();
            items.Move(0, 1);
            Assert.Equal(new[] { ids[1], ids[0], ids[2] }, source.Rows.Select(row => row.Id));
            items.Insert(0, 1);
            Assert.Equal(new[] { ids[1], ids[0], ids[2] }, source.Rows.Skip(1).Select(row => row.Id));
            source.Refresh();
            Assert.Equal(new[] { ids[1], ids[0], ids[2] }, source.Rows.Skip(1).Select(row => row.Id));
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Source_and_ItemsSource_conflicts_reject_before_replacing_valid_state()
    {
        using var external = new FlatTreeDataGridSource<Model>([new("external")]);
        var grid = new GridControl { Source = external };
        Assert.Throws<InvalidOperationException>(() => grid.ItemsSource = new[] { new Model("items") });
        Assert.Same(external, grid.Source);
        Assert.Null(grid.ItemsSource);
        grid.Source = null;
        grid.ItemsSource = new[] { new Model("owned") };
        var owned = grid.Source!;
        Assert.Throws<InvalidOperationException>(() => grid.Source = external);
        Assert.Throws<InvalidOperationException>(() => grid.SetValue(GridControl.SourceProperty, external));
        Assert.Same(owned, grid.Source);
        var window = new Window { Content = grid }; window.Show();
        Assert.Same(owned, grid.Source); // Re-coercing defaults during styling does not revive rejected external values.
        window.Close();
        Assert.Throws<InvalidOperationException>(() => grid.Source = null);
        grid.ItemsSource = null;
        grid.Source = external;
        Assert.Single(external.Rows); // The control never disposed the application's source.
        Assert.Throws<ObjectDisposedException>(() => owned.Rows);
    }

    [AvaloniaFact]
    public void Invalid_items_replacement_retains_previous_source_and_binding_value()
    {
        var items = new[] { new Model("valid") };
        var grid = new GridControl { ItemsSource = items };
        try
        {
            var previous = grid.Source;
            Assert.Throws<InvalidOperationException>(() => grid.ItemsSource = new object?[] { null });
            Assert.Same(items, grid.ItemsSource);
            Assert.Same(previous, grid.Source);
            Assert.Single(grid.Source!.Rows);
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Display_column_changes_do_not_replace_the_owned_source_or_node_identities()
    {
        var grid = new GridControl { ItemsSource = new[] { new Model("row") } };
        try
        {
            var source = grid.Source!; var id = source.Rows[0].Id;
            var column = new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(Model.Name)) };
            grid.Columns.Add(column);
            grid.Columns.Add(new TreeDataGridCheckBoxColumn());
            grid.Columns.Move(0, 1);
            grid.Columns.Remove(column);
            Assert.Same(source, grid.Source);
            Assert.Equal(id, source.Rows[0].Id);
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Direct_hierarchy_bindings_synchronize_expansion_children_replacement_and_hints()
    {
        var child = new Model("child");
        var root = new Model("root") { Children = new ObservableCollection<Model> { child }, Expanded = true };
        var grid = HierarchicalGrid(); grid.ItemsSource = new[] { root };
        try
        {
            var source = grid.Source!;
            var rootId = source.Rows[0].Id;
            Assert.Equal(2, source.Rows.Count);
            Assert.True(source.SetExpanded(rootId, false));
            Assert.False(root.Expanded);
            Assert.Single(source.Rows);
            root.Expanded = true;
            Assert.Equal(2, source.Rows.Count);
            var oldChildren = root.Children;
            root.Children = new ObservableCollection<Model> { new("replacement") };
            Assert.Equal(0, child.SubscriptionCount);
            Assert.Equal("replacement", ((Model)source.Rows[1].Model).Name);
            var version = source.Version;
            oldChildren.Add(new("detached"));
            Assert.Equal(version, source.Version);
            root.Children.Clear(); root.HasChildren = true;
            Assert.True(source.Rows[0].HasChildren);
            source.SetExpanded(rootId, false);
            source.RowExpanding += (_, _) => root.Children.Add(new("lazy"));
            source.SetExpanded(rootId, true);
            Assert.Equal("lazy", ((Model)source.Rows[1].Model).Name);
        }
        finally { grid.ItemsSource = null; }
        Assert.Equal(0, root.SubscriptionCount);
    }

    [AvaloniaFact]
    public void Shared_models_in_hierarchy_keep_distinct_ids_and_synchronize_bound_expansion()
    {
        var shared = new Model("shared") { Children = new ObservableCollection<Model> { new("child") } };
        var grid = HierarchicalGrid(); grid.ItemsSource = new[] { shared, shared };
        try
        {
            var source = grid.Source!;
            var first = source.Rows[0].Id; var second = source.Rows[1].Id;
            Assert.NotEqual(first, second);
            source.SetExpanded(first, true);
            Assert.Equal(4, source.Rows.Count);
            Assert.True(source.TryGetRow(second, out var row));
            Assert.True(row!.IsExpanded);
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Detach_and_reattach_keep_owned_source_identity_selection_and_draft()
    {
        var model = new Model("row");
        var grid = new GridControl { ItemsSource = new[] { model } };
        var column = new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(Model.Name)) };
        grid.Columns.Add(column);
        var window = new Window { Content = grid }; window.Show();
        try
        {
            var source = grid.Source!; var id = source.Rows[0].Id;
            grid.State!.Select(id);
            grid.State.BeginEdit(new(id, column.Id), item => ((Model)item).Name, (item, value) => ((Model)item).Name = (string)value!);
            grid.State.EditSession!.Value = "draft";
            window.Content = null;
            Assert.Same(source, grid.Source);
            window.Content = grid;
            Assert.Same(source, grid.Source);
            Assert.Equal(id, source.Rows[0].Id);
            Assert.Contains(id, grid.State!.SelectedNodes);
            Assert.Equal("draft", grid.State.EditSession!.Value);
        }
        finally { window.Close(); grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Xaml_content_contract_and_unsupported_hierarchy_bindings_are_explicit()
    {
        Assert.NotNull(typeof(GridControl).GetProperty(nameof(GridControl.Columns))!.GetCustomAttributes(typeof(ContentAttribute), true).SingleOrDefault());
        Assert.NotNull(typeof(TreeDataGridHierarchicalExpanderColumn).GetProperty(nameof(TreeDataGridHierarchicalExpanderColumn.InnerColumn))!.GetCustomAttributes(typeof(ContentAttribute), true).SingleOrDefault());
        var column = new TreeDataGridHierarchicalExpanderColumn();
        Assert.Throws<NotSupportedException>(() => column.ChildrenBinding = new ReflectionBinding("Nested..Children"));
        Assert.Throws<NotSupportedException>(() => column.IsExpandedBinding = new ReflectionBinding("Expanded") { Mode = BindingMode.OneWay });
        Assert.Null(column.ChildrenBinding);
        Assert.Null(column.IsExpandedBinding);
    }

    [AvaloniaFact]
    public void Automatically_assigning_source_preserves_an_existing_source_binding_expression()
    {
        using var external = new FlatTreeDataGridSource<Model>([new("external")]);
        var vm = new SourceViewModel();
        var grid = new GridControl { DataContext = vm };
        using var binding = grid.Bind(GridControl.SourceProperty, new ReflectionBinding(nameof(SourceViewModel.Source)));
        grid.ItemsSource = new[] { new Model("automatic") };
        var owned = grid.Source!;
        grid.ItemsSource = null;
        vm.Source = external;
        Assert.Same(external, grid.Source);
        Assert.Throws<ObjectDisposedException>(() => owned.Rows);
        vm.Source = null;
        Assert.Null(grid.Source);
        Assert.Single(external.Rows);
    }

    [AvaloniaFact]
    public void Replacing_children_releases_old_collection_subscription_and_all_owned_subscriptions_on_clear()
    {
        var oldChildren = new TrackedItems { new Model("old") };
        var newChildren = new TrackedItems { new Model("new") };
        var root = new UntypedModel { Children = oldChildren };
        var grid = new GridControl();
        grid.Columns.Add(new TreeDataGridHierarchicalExpanderColumn { ChildrenBinding = new ReflectionBinding(nameof(UntypedModel.Children)) });
        grid.ItemsSource = new[] { root };
        Assert.Equal(1, oldChildren.SubscriptionCount);
        root.Children = newChildren;
        Assert.Equal(0, oldChildren.SubscriptionCount);
        Assert.Equal(1, newChildren.SubscriptionCount);
        grid.ItemsSource = null;
        Assert.Equal(0, newChildren.SubscriptionCount);
    }

    [AvaloniaFact]
    public void Invalid_new_graph_does_not_install_or_unbalance_collection_subscriptions()
    {
        var invalid = new TrackedItems { null! };
        var grid = new GridControl { ItemsSource = new[] { new Model("valid") } };
        try
        {
            var source = grid.Source;
            Assert.Throws<InvalidOperationException>(() => grid.ItemsSource = invalid);
            Assert.Equal(0, invalid.SubscriptionCount);
            Assert.Same(source, grid.Source);
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Source_change_callbacks_cannot_replace_or_clear_an_in_progress_items_assignment()
    {
        using var external = new FlatTreeDataGridSource<Model>([new("external")]);
        var grid = new GridControl();
        grid.PropertyChanged += (_, args) =>
        {
            if (args.Property != GridControl.SourceProperty || grid.ItemsSource is null) return;
            Assert.Throws<InvalidOperationException>(() => grid.ItemsSource = null);
            Assert.Throws<InvalidOperationException>(() => grid.Source = external);
            Assert.Throws<InvalidOperationException>(() => grid.SetValue(GridControl.SourceProperty, external));
        };
        grid.ItemsSource = new[] { new Model("items") };
        Assert.Equal("items", ((Model)Assert.Single(grid.Source!.Rows).Model).Name);
        grid.ItemsSource = null;
        Assert.Single(external.Rows);
    }

    [AvaloniaFact]
    public void ItemsSource_replacement_inside_writer_finishes_old_callback_without_accessing_disposed_source()
    {
        var original = new Model("original"); var replacement = new Model("replacement");
        var vm = new ViewModel { Items = new[] { original } };
        var grid = new GridControl { DataContext = vm };
        using var binding = grid.Bind(GridControl.ItemsSourceProperty, new ReflectionBinding(nameof(ViewModel.Items)));
        var column = new TreeDataGridTextColumn
        {
            ValueGetter = item => ((Model)item).Name,
            ValueSetter = (item, value) =>
            {
                ((Model)item).Name = (string)value!;
                vm.Items = new[] { replacement };
            },
        };
        grid.Columns.Add(column);
        try
        {
            var previous = grid.Source!;
            Assert.True(grid.BeginEdit(new(previous.Rows[0].Id, column.Id)));
            grid.State!.EditSession!.Value = "written";
            Assert.False(grid.CommitEdit()); // The owning source changed, so navigation/continuation must stop.
            Assert.Equal("written", original.Name); // An already executed user setter cannot be rolled back.
            Assert.Equal("replacement", replacement.Name);
            Assert.Same(replacement, Assert.Single(grid.Source!.Rows).Model);
            Assert.Null(grid.State!.EditSession);
            Assert.Equal(0, original.SubscriptionCount);
            Assert.Throws<ObjectDisposedException>(() => previous.Rows);
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void ItemsSource_replacement_inside_validator_prevents_the_old_writer()
    {
        var original = new Model("original"); var replacement = new Model("replacement");
        var grid = new GridControl { ItemsSource = new[] { original } };
        var writes = 0;
        var column = new TreeDataGridTextColumn
        {
            ValueGetter = item => ((Model)item).Name,
            ValueSetter = (_, _) => writes++,
            ValueValidator = _ => { grid.ItemsSource = new[] { replacement }; return null; },
        };
        grid.Columns.Add(column);
        try
        {
            var previous = grid.Source!;
            Assert.True(grid.BeginEdit(new(previous.Rows[0].Id, column.Id)));
            grid.State!.EditSession!.Value = "draft";
            Assert.False(grid.CommitEdit());
            Assert.Equal(0, writes);
            Assert.Same(replacement, Assert.Single(grid.Source!.Rows).Model);
            Assert.Throws<ObjectDisposedException>(() => previous.Rows);
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void ItemsSource_replacement_inside_reader_does_not_install_an_orphaned_edit()
    {
        var grid = new GridControl { ItemsSource = new[] { new Model("old") } };
        var replacement = new Model("replacement");
        var column = new TreeDataGridTextColumn
        {
            ValueGetter = item => { grid.ItemsSource = new[] { replacement }; return ((Model)item).Name; },
            ValueSetter = (_, _) => throw new InvalidOperationException("must not write"),
        };
        grid.Columns.Add(column);
        try
        {
            var previous = grid.Source!;
            Assert.False(grid.BeginEdit(new(previous.Rows[0].Id, column.Id)));
            Assert.Null(grid.State!.EditSession);
            Assert.Same(replacement, Assert.Single(grid.Source!.Rows).Model);
            Assert.Throws<ObjectDisposedException>(() => previous.Rows);
        }
        finally { grid.ItemsSource = null; }
    }

    private static GridControl HierarchicalGrid()
    {
        var grid = new GridControl();
        grid.Columns.Add(new TreeDataGridHierarchicalExpanderColumn
        {
            ChildrenBinding = new ReflectionBinding(nameof(Model.Children)),
            HasChildrenBinding = new ReflectionBinding(nameof(Model.HasChildren)),
            IsExpandedBinding = new ReflectionBinding(nameof(Model.Expanded)) { Mode = BindingMode.TwoWay },
            InnerColumn = new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(Model.Name)) },
        });
        return grid;
    }

    public sealed class Model(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private bool _expanded;
        private bool _hasChildren;
        private ObservableCollection<Model> _children = [];
        private PropertyChangedEventHandler? _changed;
        public int SubscriptionCount { get; private set; }
        public string Name { get => _name; set { _name = value; Raise(nameof(Name)); } }
        public bool Expanded { get => _expanded; set { _expanded = value; Raise(nameof(Expanded)); } }
        public bool HasChildren { get => _hasChildren; set { _hasChildren = value; Raise(nameof(HasChildren)); } }
        public ObservableCollection<Model> Children { get => _children; set { _children = value; Raise(nameof(Children)); } }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { SubscriptionCount++; _changed += value; }
            remove { SubscriptionCount--; _changed -= value; }
        }
        private void Raise(string name) => _changed?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    public sealed class ViewModel : INotifyPropertyChanged
    {
        private IEnumerable? _items;
        public IEnumerable? Items { get => _items; set { _items = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    public sealed class SourceViewModel : INotifyPropertyChanged
    {
        private TreeDataGridSource? _source;
        public TreeDataGridSource? Source { get => _source; set { _source = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Source))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    public sealed class UntypedModel : INotifyPropertyChanged
    {
        private IEnumerable? _children;
        public IEnumerable? Children { get => _children; set { _children = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Children))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class TrackedItems : Collection<object>, System.Collections.Specialized.INotifyCollectionChanged
    {
        private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _changed;
        public int SubscriptionCount { get; private set; }
        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { SubscriptionCount++; _changed += value; }
            remove { SubscriptionCount--; _changed -= value; }
        }
    }
}
