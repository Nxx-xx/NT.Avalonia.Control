using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Usage;

public sealed partial class Person : ObservableObject
{
    private string _name = "";
    private int _quantity;

    [ObservableProperty]
    private bool? _enabled = true;

    public string Name
    {
        get => _name;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("名称不能为空。");
            SetProperty(ref _name, value);
        }
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (value is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(value), "数量须在 0–100 之间。");
            SetProperty(ref _quantity, value);
        }
    }
}

public sealed partial class PeopleViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private Person? _selectedItem;

    [ObservableProperty]
    private CellAddress? _currentCell;

    [ObservableProperty]
    private IndexPath _selectedIndex;

    private int _nextNumber = 3;

    public ObservableCollection<Person> Items { get; } = new()
    {
        new Person { Name = "记录 A", Quantity = 10, Enabled = true },
        new Person { Name = "记录 B", Quantity = 20, Enabled = null },
    };

    // 可变集合本身的增删会双向同步；不需要替换集合实例。
    public ObservableCollection<Person> SelectedItems { get; } = new();
    public TreeDataGridEditCommands Editing { get; } = new();

    [RelayCommand]
    private void Add()
    {
        Items.Add(new Person
        {
            Name = $"新记录 {_nextNumber++}", Quantity = 1, Enabled = true,
        });
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        if (SelectedItem is { } item) Items.Remove(item);
    }

    private bool CanRemove() => SelectedItem is not null;
}
