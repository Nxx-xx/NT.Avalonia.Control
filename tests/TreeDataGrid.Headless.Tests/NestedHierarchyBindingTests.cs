using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class NestedHierarchyBindingTests
{
    [AvaloniaFact]
    public void Every_intermediate_level_tracks_expansion_hints_and_children_replacement()
    {
        var root = new Model("root");
        var child = new Model("child");
        root.Container!.State!.Children.Add(child);
        root.Container!.State!.IsExpanded = true;
        var grid = CreateHierarchy();
        grid.ItemsSource = new[] { root };
        try
        {
            var source = grid.Source!;
            var id = source.Rows[0].Id;
            Assert.Equal(2, source.Rows.Count);
            Assert.True(source.SetExpanded(id, false));
            Assert.False(root.Container!.State!.IsExpanded);
            Assert.Single(source.Rows);
            root.Container!.State!.IsExpanded = true;
            Assert.Equal(2, source.Rows.Count);
            var oldChildren = root.Container!.State!.Children;
            var replacement = new TrackedCollection<Model> { new("replacement") };
            root.Container!.State!.Children = replacement;
            Assert.Equal(0, oldChildren.SubscriptionCount);
            Assert.Equal(0, child.SubscriptionCount);
            Assert.Equal("replacement", ((Model)source.Rows[1].Model).Name);
            var version = source.Version;
            oldChildren.Add(new("detached"));
            Assert.Equal(version, source.Version);
            replacement.Clear();
            root.Container!.State!.HasChildren = true;
            Assert.True(source.Rows[0].HasChildren);
            root.Container!.State!.HasChildren = false;
            Assert.False(source.Rows[0].HasChildren);
        }
        finally { grid.ItemsSource = null; }
        Assert.Equal(0, root.SubscriptionCount);
        Assert.Equal(0, root.Container!.SubscriptionCount);
        Assert.Equal(0, root.Container!.State!.SubscriptionCount);
        Assert.Equal(0, root.Container!.State!.Children.SubscriptionCount);
    }

    [AvaloniaFact]
    public void Parent_and_child_object_replacement_detaches_old_objects_before_returning()
    {
        var root = new Model("root");
        var originalContainer = root.Container!;
        var originalState = originalContainer.State!;
        originalState.IsExpanded = true;
        originalState.Children.Add(new("old"));
        var grid = CreateHierarchy();
        grid.ItemsSource = new[] { root };
        try
        {
            var source = grid.Source!;
            var id = source.Rows[0].Id;
            var replacementState = new State { IsExpanded = true, Children = new() { new("new state") } };
            originalContainer.State = replacementState;
            Assert.Equal(id, source.Rows[0].Id);
            Assert.Equal(0, originalState.SubscriptionCount);
            Assert.Equal(0, originalState.Children.SubscriptionCount);
            Assert.Equal("new state", ((Model)source.Rows[1].Model).Name);
            var replacementContainer = new Container { State = new() { IsExpanded = true, Children = new() { new("new parent") } } };
            root.Container = replacementContainer;
            Assert.Equal(0, originalContainer.SubscriptionCount);
            Assert.Equal(0, replacementState.SubscriptionCount);
            Assert.Equal(0, replacementState.Children.SubscriptionCount);
            Assert.Equal("new parent", ((Model)source.Rows[1].Model).Name);
            var version = source.Version;
            originalState.IsExpanded = false;
            originalContainer.State = originalState;
            replacementState.Children.Add(new("detached"));
            Assert.Equal(version, source.Version);
        }
        finally { grid.ItemsSource = null; }
        Assert.Equal(0, root.Container!.SubscriptionCount);
        Assert.Equal(0, root.Container!.State!.SubscriptionCount);
    }

    [AvaloniaFact]
    public void Null_intermediate_objects_show_empty_children_and_rebind_when_the_path_returns()
    {
        var root = new Model("root") { Container = null };
        var grid = CreateHierarchy();
        grid.ItemsSource = new[] { root };
        try
        {
            var source = grid.Source!;
            var id = source.Rows[0].Id;
            Assert.Single(source.Rows);
            Assert.False(source.Rows[0].HasChildren);
            Assert.False(source.Rows[0].IsExpanded);
            var error = Assert.Throws<InvalidOperationException>(() => source.SetExpanded(id, true));
            Assert.Contains("intermediate object is null", error.Message);
            Assert.False(source.Rows[0].IsExpanded);

            root.Container = new Container { State = null };
            Assert.False(source.Rows[0].HasChildren);
            root.Container!.State = new State { IsExpanded = true, Children = new() { new("child") } };
            Assert.Equal(2, source.Rows.Count);
            var old = root.Container!.State!;
            root.Container!.State = null;
            Assert.Single(source.Rows);
            Assert.Equal(0, old.SubscriptionCount);
            Assert.Equal(0, old.Children.SubscriptionCount);
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Collapsed_filtered_and_shared_models_keep_nested_notifications_and_release_removed_models()
    {
        var shared = new Model("shared");
        var roots = new ObservableCollection<Model> { shared, shared };
        var hiddenChild = new Model("hidden child");
        shared.Container!.State!.Children.Add(hiddenChild);
        var grid = CreateHierarchy();
        grid.ItemsSource = roots;
        try
        {
            var source = grid.Source!;
            var first = source.Rows[0].Id;
            var second = source.Rows[1].Id;
            Assert.NotEqual(first, second);
            Assert.Equal(1, shared.Container!.State!.SubscriptionCount);
            hiddenChild.Container!.State!.HasChildren = true;
            Assert.True(source.TryGetNodeId(new(0, 0), out var hidden));
            Assert.True(source.TryGetRow(hidden, out var row));
            Assert.True(row!.HasChildren);
            source.SetFilter(_ => false);
            shared.Container!.State!.IsExpanded = true;
            Assert.Empty(source.Rows);
            source.SetFilter(null);
            Assert.Equal(4, source.Rows.Count);
            Assert.True(source.TryGetRow(first, out var firstRow));
            Assert.True(source.TryGetRow(second, out var secondRow));
            Assert.True(firstRow!.IsExpanded);
            Assert.True(secondRow!.IsExpanded);
            roots.Clear();
            Assert.Equal(0, shared.Container!.State!.SubscriptionCount);
            Assert.Equal(0, hiddenChild.Container!.State!.SubscriptionCount);
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Projection_callback_nested_changes_are_queued_without_reentering_graph_construction()
    {
        var root = new Model("root");
        var grid = CreateHierarchy();
        grid.ItemsSource = new[] { root };
        try
        {
            var source = grid.Source!;
            var changed = false;
            source.ProjectionChanged += (_, _) =>
            {
                if (changed) return;
                changed = true;
                root.Container!.State!.Children.Add(new("callback child"));
                root.Container!.State!.IsExpanded = true;
            };
            root.Container!.State!.HasChildren = true;
            Assert.True(changed);
            Assert.Equal(2, source.Rows.Count);
            Assert.Equal("callback child", ((Model)source.Rows[1].Model).Name);
            Assert.True(source.Rows[0].IsExpanded);
        }
        finally { grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Invalid_paths_types_and_readonly_expansion_have_clear_diagnostics_and_release_subscriptions()
    {
        var column = new TreeDataGridHierarchicalExpanderColumn();
        Assert.Throws<NotSupportedException>(() => column.ChildrenBinding = new ReflectionBinding("Container..Children"));
        Assert.Throws<NotSupportedException>(() => column.ChildrenBinding = new ReflectionBinding("Children[0]"));
        var model = new Model("root");
        var grid = new GridControl();
        grid.Columns.Add(new TreeDataGridHierarchicalExpanderColumn { ChildrenBinding = new ReflectionBinding("Container.Missing.Children") });
        var missing = Assert.Throws<InvalidOperationException>(() => grid.ItemsSource = new[] { model });
        Assert.Contains("Missing", missing.Message);
        Assert.Null(grid.ItemsSource);
        Assert.Equal(0, model.SubscriptionCount);
        var readonlyGrid = new GridControl();
        readonlyGrid.Columns.Add(new TreeDataGridHierarchicalExpanderColumn
        {
            ChildrenBinding = new ReflectionBinding("Container.State.Children"),
            IsExpandedBinding = new ReflectionBinding("Container.State.ReadonlyExpanded"),
        });
        var readOnly = Assert.Throws<InvalidOperationException>(() => readonlyGrid.ItemsSource = new[] { model });
        Assert.Contains("setter", readOnly.Message);
        Assert.Equal(0, model.Container!.State!.Children.SubscriptionCount);
    }

    [AvaloniaFact]
    public void Nested_cell_binding_tracks_replaced_objects_and_only_writes_after_conversion_and_validation()
    {
        var root = new Model("root");
        root.Container!.State!.Number = 12;
        using var source = new FlatTreeDataGridSource<Model>([root]);
        var grid = RenderedGrid(source);
        var column = new TreeDataGridTextColumn
        {
            Binding = new ReflectionBinding("Container.State.Number") { Mode = BindingMode.TwoWay },
            ValueValidator = value => (int)value! < 0 ? "positive only" : null,
        };
        grid.Columns.Add(column);
        var window = Show(grid);
        try
        {
            var original = root.Container!.State!;
            Assert.Equal("12", Display(grid).Text);
            root.Container!.State = new State { Number = 7 };
            window.UpdateLayout();
            Assert.Equal("7", Display(grid).Text);
            original.Number = 999;
            Assert.Equal("7", Display(grid).Text);
            var current = root.Container!.State!;
            var address = new CellAddress(source.Rows[0].Id, column.Id);
            Assert.True(grid.BeginEdit(address));
            grid.State!.EditSession!.Value = "bad";
            Assert.False(grid.CommitEdit());
            Assert.Equal(7, current.Number);
            Assert.Equal(1, current.NumberWrites);
            grid.State.EditSession.Value = "-1";
            Assert.False(grid.CommitEdit());
            Assert.Equal(1, current.NumberWrites);
            grid.State.EditSession.Value = "25";
            Assert.True(grid.CommitEdit());
            Assert.Equal(25, current.Number);
            Assert.Equal(2, current.NumberWrites);
            Assert.True(grid.BeginEdit(address));
            grid.State.EditSession!.Value = "100";
            grid.CancelEdit();
            Assert.Equal(25, current.Number);
            Assert.Equal(2, current.NumberWrites);
            root.Container!.State = null;
            Assert.False(grid.BeginEdit(address));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void OneWay_is_readonly_and_a_oneway_converter_can_fail_commit_before_using_explicit_accessors()
    {
        var root = new Model("root");
        using var source = new FlatTreeDataGridSource<Model>([root]);
        var grid = new GridControl { Source = source };
        var oneWay = new TreeDataGridTextColumn { Binding = new ReflectionBinding("Name") { Mode = BindingMode.OneWay } };
        var converter = new TreeDataGridTextColumn
        {
            Binding = new ReflectionBinding("Name") { Converter = new FuncValueConverter<string, string>(value => "display " + value) }
        };
        grid.Columns.Add(oneWay);
        grid.Columns.Add(converter);
        Assert.False(grid.BeginEdit(new(source.Rows[0].Id, oneWay.Id)));
        Assert.True(grid.BeginEdit(new(source.Rows[0].Id, converter.Id)));
        Assert.Equal("display root", grid.State!.EditSession!.Value);
        Assert.False(grid.CommitEdit()); Assert.Equal("root", root.Name);
        grid.CancelEdit();
        converter.SetValueAccessors<Model, string>(item => item.Name, (item, value) => item.Name = value);
        Assert.True(grid.BeginEdit(new(source.Rows[0].Id, converter.Id)));
        Assert.Equal("root", grid.State!.EditSession!.Value);
        grid.State.EditSession.Value = "updated";
        Assert.True(grid.CommitEdit());
        Assert.Equal("updated", root.Name);
        oneWay.SetValueAccessors<Model, string>(item => item.Name);
        Assert.False(grid.BeginEdit(new(source.Rows[0].Id, oneWay.Id)));
    }

    private static GridControl CreateHierarchy()
    {
        var grid = new GridControl();
        grid.Columns.Add(new TreeDataGridHierarchicalExpanderColumn
        {
            ChildrenBinding = new ReflectionBinding("Container.State.Children"),
            HasChildrenBinding = new ReflectionBinding("Container.State.HasChildren"),
            IsExpandedBinding = new ReflectionBinding("Container.State.IsExpanded") { Mode = BindingMode.TwoWay },
        });
        return grid;
    }

    private static GridControl RenderedGrid(TreeDataGridSource source) => new()
    {
        Source = source,
        Template = new FuncControlTemplate<GridControl>((_, scope) =>
        {
            var rows = new TreeDataGridRowsPresenter();
            var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scope.Register("PART_RowsPresenter", rows);
            scope.Register("PART_ScrollViewer", scroll);
            return scroll;
        })
    };

    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 240, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static TextBlock Display(GridControl grid) => Assert.Single(grid.RowsPresenter!.RealizedRows).RealizedCells.Single().GetVisualDescendants().OfType<TextBlock>().Single();

    public abstract class ObservableModel : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public int SubscriptionCount { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; SubscriptionCount++; }
            remove { _changed -= value; SubscriptionCount--; }
        }
        protected void Changed(string name) => _changed?.Invoke(this, new(name));
    }

    public sealed class Model(string name) : ObservableModel
    {
        private Container? _container = new();
        private string _name = name;
        public string Name { get => _name; set { _name = value; Changed(nameof(Name)); } }
        public Container? Container { get => _container; set { _container = value; Changed(nameof(Container)); } }
    }

    public sealed class Container : ObservableModel
    {
        private State? _state = new();
        public State? State { get => _state; set { _state = value; Changed(nameof(State)); } }
    }

    public sealed class State : ObservableModel
    {
        private TrackedCollection<Model> _children = new();
        private bool _hasChildren;
        private bool _expanded;
        private int _number;
        public TrackedCollection<Model> Children { get => _children; set { _children = value; Changed(nameof(Children)); } }
        public bool HasChildren { get => _hasChildren; set { _hasChildren = value; Changed(nameof(HasChildren)); } }
        public bool IsExpanded { get => _expanded; set { _expanded = value; Changed(nameof(IsExpanded)); } }
        public bool ReadonlyExpanded => IsExpanded;
        public int Number { get => _number; set { _number = value; NumberWrites++; Changed(nameof(Number)); } }
        public int NumberWrites { get; private set; }
    }

    public sealed class TrackedCollection<T> : Collection<T>, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _changed;
        public int SubscriptionCount { get; private set; }
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { _changed += value; SubscriptionCount++; }
            remove { _changed -= value; SubscriptionCount--; }
        }
        protected override void InsertItem(int index, T item) { base.InsertItem(index, item); _changed?.Invoke(this, new(NotifyCollectionChangedAction.Add, item, index)); }
        protected override void RemoveItem(int index) { var old = this[index]; base.RemoveItem(index); _changed?.Invoke(this, new(NotifyCollectionChangedAction.Remove, old, index)); }
        protected override void ClearItems() { base.ClearItems(); _changed?.Invoke(this, new(NotifyCollectionChangedAction.Reset)); }
    }
}
