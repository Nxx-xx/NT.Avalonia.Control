using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class MvvmBindingTests
{
    [AvaloniaFact]
    public void Default_two_way_selection_binding_accepts_vm_input_and_writes_state_changes()
    {
        var a = new Item("a"); var b = new Item("b"); using var source = new FlatTreeDataGridSource<Item>([a, b]);
        var vm = new Vm { Source = source, SelectedItem = b }; var grid = Create();
        Bind(grid, GridControl.SelectedItemProperty, nameof(Vm.SelectedItem));
        Bind(grid, GridControl.SelectedIndexProperty, nameof(Vm.SelectedIndex));
        Bind(grid, GridControl.SourceProperty, nameof(Vm.Source));
        grid.DataContext = vm; var window = Show(grid);
        try
        {
            Assert.Same(b, grid.SelectedItem); Assert.Equal(new IndexPath(1), grid.SelectedIndex);
            grid.State!.Select(source.Rows[0].Id); Assert.Same(a, vm.SelectedItem); Assert.Equal(new IndexPath(0), vm.SelectedIndex);
            vm.SelectedItem = b; Assert.Equal(source.Rows[1].Id, Assert.Single(grid.State.SelectedNodes));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Selected_index_addresses_an_occurrence_and_tracks_model_path_after_move_and_sort()
    {
        var shared = new Item("shared"); var other = new Item("other"); var items = new ObservableCollection<Item> { shared, other, shared };
        using var source = new FlatTreeDataGridSource<Item>(items); var grid = Create(); grid.SelectedIndex = new IndexPath(2); grid.Source = source; var window = Show(grid);
        try
        {
            var chosen = source.Rows[2].Id; Assert.Equal(chosen, Assert.Single(grid.State!.SelectedNodes));
            items.Move(2, 0); Assert.Equal(chosen, Assert.Single(grid.State.SelectedNodes)); Assert.Equal(new IndexPath(0), grid.SelectedIndex);
            source.Sort(grid.Columns[0].Id, (a, b) => string.Compare(((Item)a).Name, ((Item)b).Name, StringComparison.Ordinal), TreeDataGridSortDirection.Descending);
            Assert.Equal(chosen, Assert.Single(grid.State.SelectedNodes)); Assert.Equal(new IndexPath(0), grid.SelectedIndex);
            source.SetFilter(_ => false); Assert.Same(shared, grid.SelectedItem); Assert.Equal(new IndexPath(0), grid.SelectedIndex);
            grid.SelectedItem = other; Assert.Null(grid.SelectedItem); Assert.Empty(grid.State.SelectedNodes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Observable_selected_items_support_both_directions_and_detach_releases_the_collection()
    {
        var a = new Item("a"); var b = new Item("b"); using var source = new FlatTreeDataGridSource<Item>([a, b]);
        var vm = new Vm { Source = source }; var grid = Create(); grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        Bind(grid, GridControl.SourceProperty, nameof(Vm.Source)); Bind(grid, GridControl.SelectedItemsProperty, nameof(Vm.SelectedItems)); grid.DataContext = vm;
        var window = Show(grid);
        try
        {
            vm.SelectedItems.Add(a); vm.SelectedItems.Add(b); Assert.Equal(2, grid.State!.SelectedNodes.Count);
            grid.State.Select(source.Rows[1].Id); Assert.Same(vm.SelectedItems, grid.SelectedItems); Assert.Same(b, Assert.Single(vm.SelectedItems));
            window.Content = null; vm.SelectedItems.Clear(); vm.SelectedItems.Add(a);
            Assert.Equal(source.Rows[1].Id, Assert.Single(grid.State.SelectedNodes));
            window.Content = grid; window.UpdateLayout(); Assert.Equal(source.Rows[0].Id, Assert.Single(grid.State.SelectedNodes));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Fixed_selected_items_are_replaced_without_mutation_and_keep_the_binding()
    {
        var a = new Item("a"); var b = new Item("b"); using var source = new FlatTreeDataGridSource<Item>([a, b]);
        var original = new[] { a }; var vm = new ListVm { Items = original }; var grid = Create(); grid.Source = source;
        Bind(grid, GridControl.SelectedItemsProperty, nameof(ListVm.Items)); grid.DataContext = vm; var window = Show(grid);
        try
        {
            grid.State!.Select(source.Rows[1].Id); Assert.Same(a, original[0]); Assert.NotSame(original, vm.Items); Assert.Same(b, vm.Items[0]);
            vm.Items = new[] { a }; Assert.Equal(source.Rows[0].Id, Assert.Single(grid.State.SelectedNodes));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Current_cell_binding_is_independent_of_selection_and_clears_on_source_replacement()
    {
        var a = new Item("a"); var b = new Item("b"); using var source = new FlatTreeDataGridSource<Item>([a, b]);
        using var replacement = new FlatTreeDataGridSource<Item>([new("c")]); var vm = new Vm { Source = source, SelectedItem = a }; var grid = Create();
        Bind(grid, GridControl.SourceProperty, nameof(Vm.Source)); Bind(grid, GridControl.SelectedItemProperty, nameof(Vm.SelectedItem)); Bind(grid, GridControl.CurrentCellProperty, nameof(Vm.CurrentCell)); grid.DataContext = vm;
        var window = Show(grid);
        try
        {
            vm.CurrentCell = new(source.Rows[1].Id, grid.Columns[0].Id);
            Assert.Equal(vm.CurrentCell, grid.State!.CurrentCell); Assert.Same(a, grid.SelectedItem);
            grid.State.SetCurrentCell(new(source.Rows[0].Id, grid.Columns[0].Id)); Assert.Equal(grid.State.CurrentCell, vm.CurrentCell);
            vm.Source = replacement; Assert.Null(vm.SelectedItem); Assert.Null(vm.CurrentCell); Assert.Empty(grid.State.SelectedNodes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Reentrant_vm_selection_setter_can_redirect_the_selection_without_losing_the_binding()
    {
        var a = new Item("a"); var b = new Item("b"); var c = new Item("c"); using var source = new FlatTreeDataGridSource<Item>([a, b, c]);
        var vm = new Vm { Source = source }; var grid = Create(); Bind(grid, GridControl.SourceProperty, nameof(Vm.Source)); Bind(grid, GridControl.SelectedItemProperty, nameof(Vm.SelectedItem)); grid.DataContext = vm;
        var window = Show(grid);
        try
        {
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Vm.SelectedItem) && ReferenceEquals(vm.SelectedItem, b)) vm.SelectedItem = c; };
            grid.State!.Select(source.Rows[1].Id); Assert.Same(c, grid.SelectedItem); Assert.Equal(source.Rows[2].Id, Assert.Single(grid.State.SelectedNodes));
            vm.SelectedItem = a; Assert.Same(a, grid.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Nonvisual_commands_drive_draft_validation_commit_cancel_and_detach_reattach()
    {
        var item = new Item("old"); using var source = new FlatTreeDataGridSource<Item>([item]); var vm = new Vm { Source = source }; var grid = Create();
        grid.Columns[0].ValueValidator = value => Equals(value, "bad") ? "invalid" : null;
        Bind(grid, GridControl.SourceProperty, nameof(Vm.Source)); Bind(grid, GridControl.CurrentCellProperty, nameof(Vm.CurrentCell)); Bind(grid, GridControl.EditCommandsProperty, nameof(Vm.Editing)); grid.DataContext = vm;
        var window = Show(grid);
        try
        {
            vm.CurrentCell = new(source.Rows[0].Id, grid.Columns[0].Id);
            Assert.True(vm.Editing.BeginEditCommand.CanExecute(null)); vm.Editing.BeginEditCommand.Execute(null);
            Assert.True(grid.IsEditing); Assert.Same(grid.EditSession, vm.Editing.EditSession); Assert.Same(grid.BeginEditCommand, vm.Editing.BeginEditCommand);
            vm.Editing.EditSession!.Value = "bad"; vm.Editing.CommitEditCommand.Execute(null); Assert.True(vm.Editing.IsEditing); Assert.Equal("invalid", vm.Editing.EditSession.ValidationError); Assert.Equal("old", item.Name);
            vm.Editing.EditSession.Value = "good"; vm.Editing.CommitEditCommand.Execute(null); Assert.False(grid.IsEditing); Assert.Equal("good", item.Name);
            vm.Editing.BeginEditCommand.Execute(null); var session = grid.EditSession; session!.Value = "draft";
            window.Content = null; Assert.False(vm.Editing.IsEditing); Assert.False(vm.Editing.BeginEditCommand.CanExecute(null)); Assert.Same(session, grid.EditSession);
            window.Content = grid; window.UpdateLayout(); Assert.Same(session, vm.Editing.EditSession);
            vm.Editing.CancelEditCommand.Execute(null); Assert.False(vm.Editing.IsEditing); Assert.Equal("good", item.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Selected_items_output_callbacks_can_replace_the_source_without_adding_stale_models()
    {
        var a = new Item("a"); var b = new Item("b"); var c = new Item("c");
        using var source = new FlatTreeDataGridSource<Item>([a, b]); using var replacement = new FlatTreeDataGridSource<Item>([c]);
        var vm = new Vm { Source = source }; var grid = Create(); grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        Bind(grid, GridControl.SourceProperty, nameof(Vm.Source)); Bind(grid, GridControl.SelectedItemsProperty, nameof(Vm.SelectedItems)); grid.DataContext = vm; var window = Show(grid);
        try
        {
            vm.SelectedItems.Add(a); var armed = true;
            vm.SelectedItems.CollectionChanged += (_, e) => { if (armed && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) { armed = false; vm.Source = replacement; grid.SelectedItem = c; } };
            grid.State!.Select(source.Rows[1].Id);
            Assert.Same(replacement, grid.Source); Assert.Same(c, grid.SelectedItem); Assert.Same(c, Assert.Single(vm.SelectedItems));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Begin_command_tracks_column_readonly_and_writes_can_execute_notifications()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("a")]); var grid = Create(); grid.Source = source; var window = Show(grid);
        try
        {
            grid.CurrentCell = new(source.Rows[0].Id, grid.Columns[0].Id); var notifications = 0;
            grid.BeginEditCommand.CanExecuteChanged += (_, _) => notifications++;
            Assert.True(grid.BeginEditCommand.CanExecute(null)); grid.Columns[0].IsReadOnly = true;
            Assert.False(grid.BeginEditCommand.CanExecute(null)); Assert.True(notifications > 0);
            grid.Columns[0].IsReadOnly = false; Assert.True(grid.BeginEditCommand.CanExecute(null));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Clearing_an_existing_source_clears_all_bound_selection_mirrors()
    {
        var item = new Item("a"); using var source = new FlatTreeDataGridSource<Item>([item]); var vm = new Vm { Source = source }; var grid = Create();
        Bind(grid, GridControl.SourceProperty, nameof(Vm.Source)); Bind(grid, GridControl.SelectedItemProperty, nameof(Vm.SelectedItem));
        Bind(grid, GridControl.SelectedIndexProperty, nameof(Vm.SelectedIndex)); Bind(grid, GridControl.SelectedItemsProperty, nameof(Vm.SelectedItems));
        Bind(grid, GridControl.CurrentCellProperty, nameof(Vm.CurrentCell)); grid.DataContext = vm; var window = Show(grid);
        try
        {
            vm.SelectedItem = item; vm.CurrentCell = new(source.Rows[0].Id, grid.Columns[0].Id);
            vm.Source = null;
            Assert.Null(vm.SelectedItem); Assert.Equal(default, vm.SelectedIndex); Assert.Empty(vm.SelectedItems); Assert.Null(vm.CurrentCell); Assert.Null(grid.State);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Binding_selection_changes_after_a_writer_moves_the_model_run_after_the_transaction()
    {
        var a = new Item("a"); var b = new Item("b"); var items = new ObservableCollection<Item> { b, a };
        using var source = new FlatTreeDataGridSource<Item>(items); var vm = new Vm { Source = source }; var grid = Create();
        grid.Columns[0].ValueSetter = (model, value) => { ((Item)model).Name = (string)value!; items.Move(1, 0); };
        Bind(grid, GridControl.SourceProperty, nameof(Vm.Source)); Bind(grid, GridControl.SelectedItemProperty, nameof(Vm.SelectedItem));
        Bind(grid, GridControl.SelectedIndexProperty, nameof(Vm.SelectedIndex)); grid.DataContext = vm; var window = Show(grid);
        try
        {
            vm.SelectedItem = a; grid.CurrentCell = new(source.Rows[1].Id, grid.Columns[0].Id);
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Vm.SelectedIndex) && vm.SelectedIndex == new IndexPath(0)) vm.SelectedItem = b; };
            grid.BeginEditCommand.Execute(null); grid.EditSession!.Value = "edited";
            Assert.True(grid.CommitEdit()); Assert.Equal("edited", a.Name); Assert.Same(b, grid.SelectedItem); Assert.False(grid.IsEditing);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Controller_notification_can_unload_the_view_without_retaining_its_owner()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("a")]); var grid = Create(); grid.Source = source; var window = Show(grid);
        try
        {
            grid.CurrentCell = new(source.Rows[0].Id, grid.Columns[0].Id); grid.BeginEditCommand.Execute(null); var session = grid.EditSession;
            var controller = new TreeDataGridEditCommands(); var armed = true;
            controller.PropertyChanged += (_, e) => { if (armed && e.PropertyName == nameof(TreeDataGridEditCommands.EditSession) && controller.EditSession is not null) { armed = false; window.Content = null; } };
            grid.EditCommands = controller;
            Assert.False(controller.IsEditing); Assert.False(controller.CancelEditCommand.CanExecute(null)); Assert.Same(session, grid.EditSession);
            window.Content = grid; window.UpdateLayout(); Assert.Same(session, controller.EditSession); controller.CancelEditCommand.Execute(null); Assert.False(grid.IsEditing);
        }
        finally { window.Close(); }
    }

    private static void Bind<T>(GridControl grid, Avalonia.AvaloniaProperty<T> property, string path) => grid.Bind(property, new ReflectionBinding(path));
    private static GridControl Create()
    {
        var grid = new GridControl { Template = new FuncControlTemplate<GridControl>((_, scope) => { var rows = new TreeDataGridRowsPresenter(); var viewer = new ScrollViewer { Content = rows }; scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ScrollViewer", viewer); return viewer; }) };
        grid.Columns.Add(new TreeDataGridTextColumn { Width = new Avalonia.Controls.GridLength(200), ValueGetter = x => ((Item)x).Name, ValueSetter = (x, value) => ((Item)x).Name = (string)value! }); return grid;
    }
    private static Window Show(Control content) { var window = new Window { Width = 400, Height = 220, Content = content }; window.Show(); window.UpdateLayout(); return window; }
    public sealed class Item(string name) { public string Name { get; set; } = name; }
    public sealed class Vm : INotifyPropertyChanged
    {
        private TreeDataGridSource? _source; private object? _selectedItem; private IndexPath _selectedIndex; private CellAddress? _current;
        public TreeDataGridSource? Source { get => _source; set { _source = value; Changed(nameof(Source)); } }
        public object? SelectedItem { get => _selectedItem; set { if (ReferenceEquals(_selectedItem, value)) return; _selectedItem = value; Changed(nameof(SelectedItem)); } }
        public IndexPath SelectedIndex { get => _selectedIndex; set { if (_selectedIndex == value) return; _selectedIndex = value; Changed(nameof(SelectedIndex)); } }
        public CellAddress? CurrentCell { get => _current; set { if (_current == value) return; _current = value; Changed(nameof(CurrentCell)); } }
        public ObservableCollection<Item> SelectedItems { get; } = [];
        public TreeDataGridEditCommands Editing { get; } = new();
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
    }
    public sealed class ListVm : INotifyPropertyChanged
    {
        private IList _items = Array.Empty<object>();
        public IList Items { get => _items; set { _items = value; PropertyChanged?.Invoke(this, new(nameof(Items))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
