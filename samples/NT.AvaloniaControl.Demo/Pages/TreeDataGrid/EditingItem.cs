using CommunityToolkit.Mvvm.ComponentModel;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class EditingItem(int id) : ObservableObject
{
    [ObservableProperty]
    private string _name = $"项目 {id:0000}";

    [ObservableProperty]
    private int _quantity = 10 + id * 7 % 90;

    [ObservableProperty]
    private bool? _enabled = id % 3 == 0 ? null : id % 3 == 1;

    [ObservableProperty]
    private string _note = "可使用自定义模板编辑此说明";

    public int Id { get; } = id;
}
