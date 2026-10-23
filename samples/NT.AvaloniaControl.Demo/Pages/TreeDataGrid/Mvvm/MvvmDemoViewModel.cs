using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid.Mvvm;

/// <summary>Owns models and source; it never receives a Window, control, or visual tree.</summary>
public sealed partial class MvvmDemoViewModel : ObservableObject, IDisposable
{
    private FlatTreeDataGridSource<MvvmRecord> _source;
    private ObservableCollection<MvvmRecord> _items;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionSummary))]
    private ObservableCollection<MvvmRecord> _selectedItems = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionSummary))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand), nameof(ModifyCommand))]
    private MvvmRecord? _selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionSummary))]
    private IndexPath _selectedIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCellSummary))]
    [NotifyCanExecuteChangedFor(nameof(RememberCurrentCommand))]
    private CellAddress? _currentCell;

    private CellAddress? _rememberedCell;
    private string _message = "单击名称、数量或启用列的单元格，再点击“开始编辑”；右侧可同步修改草稿。";
    private int _nextId = 101;
    private int _generation = 1;
    private bool _disposed;

    public MvvmDemoViewModel()
    {
        _items = CreateItems(1);
        _source = new FlatTreeDataGridSource<MvvmRecord>(_items);
        _items.CollectionChanged += ItemsChanged;
        SelectedItems.CollectionChanged += SelectionChanged;
    }

    public ObservableCollection<MvvmRecord> Items => _items;
    public FlatTreeDataGridSource<MvvmRecord> Source => _source;
    public TreeDataGridEditCommands Editing { get; } = new();
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string CollectionSummary => $"数据集 {_generation} · 共 {Items.Count} 条记录 · 表格显示 {Source.Rows.Count} 条";
    public string SelectionSummary => $"SelectedItem：{SelectedItem?.Name ?? "无"} · 源路径：{SelectedIndex} · SelectedItems：{SelectedItems.Count} 条";
    public string CurrentCellSummary => CurrentCell is { } cell ? $"CurrentCell：节点 {cell.Node} / 列 {cell.Column}" : "CurrentCell：无（点击单元格建立当前项）";

    partial void OnSelectedItemsChanging(ObservableCollection<MvvmRecord> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        SelectedItems.CollectionChanged -= SelectionChanged;
    }

    partial void OnSelectedItemsChanged(ObservableCollection<MvvmRecord> value) => value.CollectionChanged += SelectionChanged;

    partial void OnSelectedItemChanging(MvvmRecord? value)
    {
        if (SelectedItem is { } previous) previous.PropertyChanged -= SelectedModelChanged;
    }

    partial void OnSelectedItemChanged(MvvmRecord? value)
    {
        if (value is not null) value.PropertyChanged += SelectedModelChanged;
    }

    private bool HasSelectedItem() => SelectedItem is not null;
    private bool HasCurrentCell() => CurrentCell is not null;
    private bool HasRememberedCell() => _rememberedCell is not null;

    [RelayCommand]
    private void Add()
    {
        var id = _nextId++;
        Items.Add(new MvvmRecord(id, $"新增记录 {id}", 25, null));
        SelectedItem = Items[^1];
        Message = $"已新增并选中“新增记录 {id}”。";
    }
    [RelayCommand(CanExecute = nameof(HasSelectedItem))]
    private void Remove()
    {
        if (SelectedItem is not { } item) return;
        Items.Remove(item);
        Message = $"已删除“{item.Name}”。";
    }
    [RelayCommand(CanExecute = nameof(HasSelectedItem))]
    private void Modify()
    {
        if (SelectedItem is not { } item) return;
        item.Quantity = (item.Quantity + 1) % 101;
        item.Enabled = item.Enabled is null ? true : !item.Enabled;
        Message = $"已更新“{item.Name}”：数量为 {item.Quantity}，启用状态为 {(item.Enabled == true ? "启用" : "停用")}。";
    }
    [RelayCommand]
    private void SelectFirst()
    {
        SelectedItem = Items.FirstOrDefault();
        Message = "已选中数据中的第一条记录。";
    }

    [RelayCommand]
    private void SelectTwo()
    {
        SelectedItems.Clear();
        foreach (var item in Items.Take(2)) SelectedItems.Add(item);
        Message = "已同时选中数据中的前两条记录。";
    }
    [RelayCommand]
    private void SelectThirdByPath()
    {
        SelectedIndex = new IndexPath(2);
        Message = "已定位原始数据中的第三条记录；排序后仍选中同一记录。";
    }

    [RelayCommand(CanExecute = nameof(HasCurrentCell))]
    private void RememberCurrent()
    {
        if (CurrentCell is not { } cell) return;
        _rememberedCell = cell;
        RestoreCurrentCommand.NotifyCanExecuteChanged();
        Message = "已记住当前单元格。点击其他单元格后，可用“恢复单元格”返回。";
    }

    [RelayCommand(CanExecute = nameof(HasRememberedCell))]
    private void RestoreCurrent()
    {
        if (_rememberedCell is not { } cell) return;
        CurrentCell = cell;
        Message = "已恢复之前记住的单元格。";
    }

    [RelayCommand]
    private void ReplaceSource()
    {
        Editing.CancelEditCommand.Execute(null);
        SelectedItems.Clear();
        SelectedItem = null;
        SelectedIndex = default;
        CurrentCell = null;
        _rememberedCell = null;
        RestoreCurrentCommand.NotifyCanExecuteChanged();
        var previous = _source;
        _items.CollectionChanged -= ItemsChanged;
        _items = CreateItems(++_generation);
        _items.CollectionChanged += ItemsChanged;
        _source = new FlatTreeDataGridSource<MvvmRecord>(_items);
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(Source));
        OnPropertyChanged(nameof(CollectionSummary));
        previous.Dispose();
        Message = $"已切换到数据集 {_generation}，当前选择和编辑草稿已清空。";
    }
    private void ItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(CollectionSummary));
    private void SelectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(SelectionSummary));
    private void SelectedModelChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(SelectionSummary));
    private static ObservableCollection<MvvmRecord> CreateItems(int generation) => new(Enumerable.Range(1, 100)
        .Select(id => new MvvmRecord(id, $"数据集 {generation} · 记录 {id:000}", 10 + id % 80, id % 3 == 0 ? null : id % 3 == 1)));
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _items.CollectionChanged -= ItemsChanged;
        SelectedItems.CollectionChanged -= SelectionChanged;
        if (SelectedItem is { } selectedItem) selectedItem.PropertyChanged -= SelectedModelChanged;
        _source.Dispose();
    }
}
