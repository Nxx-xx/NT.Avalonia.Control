using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class MvvmIntegrationTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Data_context_replacement_preserves_the_incoming_selection_independent_of_binding_order(bool selectionFirst)
    {
        var first = new Vm("first"); var second = new Vm("second");
        var grid = new GridControl();
        grid.Columns.Add(new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(Item.Name)) });
        if (selectionFirst) grid.Bind(GridControl.SelectedItemProperty, new ReflectionBinding(nameof(Vm.SelectedItem)));
        grid.Bind(GridControl.ItemsSourceProperty, new ReflectionBinding(nameof(Vm.Items)));
        if (!selectionFirst) grid.Bind(GridControl.SelectedItemProperty, new ReflectionBinding(nameof(Vm.SelectedItem)));
        grid.Bind(GridControl.EditCommandsProperty, new ReflectionBinding(nameof(Vm.Edits)));
        grid.DataContext = first;
        var window = new Window { Content = grid };
        window.Show();
        try
        {
            Assert.Same(first.Items[1], grid.SelectedItem);
            var oldSource = grid.Source!;
            grid.CurrentCell = new CellAddress(oldSource.Rows[1].Id, grid.Columns[0].Id);
            first.Edits.BeginEditCommand.Execute(null);
            Assert.True(first.Edits.IsEditing);
            first.Edits.EditSession!.Value = "uncommitted";
            grid.DataContext = second;
            Assert.Same(second.Items[1], second.SelectedItem);
            Assert.Same(second.Items[1], grid.SelectedItem);
            Assert.Same(second.Items, grid.ItemsSource);
            Assert.False(first.Edits.IsEditing);
            Assert.False(first.Edits.BeginEditCommand.CanExecute(null));
            Assert.Equal("first-1", first.Items[1].Name);
            Assert.False(second.Edits.IsEditing);
            Assert.Throws<ObjectDisposedException>(() => oldSource.Rows);
            grid.CurrentCell = new CellAddress(grid.Source!.Rows[0].Id, grid.Columns[0].Id);
            second.Edits.BeginEditCommand.Execute(null);
            second.Edits.EditSession!.Value = "updated";
            second.Edits.CommitEditCommand.Execute(null);
            Assert.Equal("updated", second.Items[0].Name);
            Assert.False(second.Edits.IsEditing);
        }
        finally { window.Close(); grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Items_source_reorder_sort_and_filter_keep_selection_identity_and_model_path()
    {
        var vm = new Vm("row");
        var grid = new GridControl { SelectionMode = TreeDataGridSelectionMode.Multiple };
        grid.Columns.Add(new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(Item.Name)) });
        grid.Bind(GridControl.ItemsSourceProperty, new ReflectionBinding(nameof(Vm.Items)));
        grid.Bind(GridControl.SelectedItemProperty, new ReflectionBinding(nameof(Vm.SelectedItem)));
        grid.DataContext = vm;
        var window = new Window { Content = grid }; window.Show();
        try
        {
            var source = grid.Source!; var selected = source.Rows[1].Id;
            vm.Items.Move(1, 0);
            Assert.Equal(new IndexPath(0), grid.SelectedIndex);
            Assert.Equal(selected, Assert.Single(grid.State!.SelectedNodes));
            Assert.True(grid.SortByColumn(grid.Columns[0].Id, TreeDataGridSortDirection.Ascending));
            Assert.Equal(new IndexPath(0), grid.SelectedIndex);
            Assert.Equal(selected, Assert.Single(grid.State.SelectedNodes));
            source.SetFilter(item => !ReferenceEquals(item, vm.SelectedItem));
            Assert.Same(vm.Items[0], grid.SelectedItem);
            Assert.Equal(selected, Assert.Single(grid.State.SelectedNodes));
            source.SetFilter(null);
            vm.Items.RemoveAt(0);
            Assert.Null(vm.SelectedItem);
            Assert.Equal(default, grid.SelectedIndex);
        }
        finally { window.Close(); grid.ItemsSource = null; }
    }

    public sealed class Item(string name)
    {
        public string Name { get; set; } = name;
    }
    private sealed class Vm : INotifyPropertyChanged
    {
        private Item? _selectedItem;
        public Vm(string prefix) { Items.Add(new(prefix + "-0")); Items.Add(new(prefix + "-1")); _selectedItem = Items[1]; }
        public ObservableCollection<Item> Items { get; } = [];
        public TreeDataGridEditCommands Edits { get; } = new();
        public Item? SelectedItem
        {
            get => _selectedItem;
            set { if (ReferenceEquals(_selectedItem, value)) return; _selectedItem = value; PropertyChanged?.Invoke(this, new(nameof(SelectedItem))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
