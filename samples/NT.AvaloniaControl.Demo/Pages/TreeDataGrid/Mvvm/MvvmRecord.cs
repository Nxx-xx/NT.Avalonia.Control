using CommunityToolkit.Mvvm.ComponentModel;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid.Mvvm;

public sealed partial class MvvmRecord(int id, string name, int quantity, bool? enabled) : ObservableObject
{
    private string _name = name;
    private int _quantity = quantity;

    [ObservableProperty]
    private bool? _enabled = enabled;

    public int Id { get; } = id;

    // Validate before comparing values so repeated invalid assignments still report editing errors.
    public string Name
    {
        get => _name;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("名称不能为空。");
            if (value.Length > 40) throw new ArgumentException("名称最多 40 个字符。");
            SetProperty(ref _name, value);
        }
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value), "数量必须为 0 到 100。");
            SetProperty(ref _quantity, value);
        }
    }
}
