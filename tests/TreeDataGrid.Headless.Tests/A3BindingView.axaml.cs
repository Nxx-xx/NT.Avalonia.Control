using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace TreeDataGrid.Headless.Tests;

public sealed partial class A3BindingView : UserControl
{
    public A3BindingView() => AvaloniaXamlLoader.Load(this);
}
public abstract class A3Notifying : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    protected void Set<T>(ref T field, T value, string name) { field = value; _changed?.Invoke(this, new(name)); }
}
public sealed class A3Row : A3Notifying
{
    private string _name = "name";
    private int _quantity = 12;
    private decimal _amount = 12.5m;
    private A3Detail _detail = new();
    private A3Hierarchy _hierarchy = new();
    private int? _optionalNumber = 5;
    public string Name { get => _name; set { NameWrites++; Set(ref _name, value, nameof(Name)); } }
    public int Quantity { get => _quantity; set { QuantityWrites++; Set(ref _quantity, value, nameof(Quantity)); } }
    public decimal Amount { get => _amount; set { AmountWrites++; Set(ref _amount, value, nameof(Amount)); } }
    public A3Detail Detail { get => _detail; set => Set(ref _detail, value, nameof(Detail)); }
    public A3Hierarchy Hierarchy { get => _hierarchy; set => Set(ref _hierarchy, value, nameof(Hierarchy)); }
    public int? OptionalNumber { get => _optionalNumber; set { OptionalWrites++; Set(ref _optionalNumber, value, nameof(OptionalNumber)); } }
    public int NameWrites { get; private set; }
    public int QuantityWrites { get; private set; }
    public int AmountWrites { get; private set; }
    public int OptionalWrites { get; private set; }
    public ObservableCollection<A3Detail> Slots { get; } = [new()];
    public Dictionary<string, A3Detail> Map { get; } = new() { ["key"] = new() };
    public A3Detail[] Array { get; } = [new()];
    public A3Struct Struct { get; set; }
}
public sealed class A3Detail : A3Notifying
{
    private string _name = "nested";
    public string Name { get => _name; set { Writes++; Set(ref _name, value, nameof(Name)); } }
    public int Writes { get; private set; }
}
public struct A3Struct { public string Name { get; set; } }
public sealed class A3Hierarchy : A3Notifying
{
    private ObservableCollection<A3Row>? _children = [];
    private bool _hasChildren;
    private bool _expanded;
    public ObservableCollection<A3Row>? Children { get => _children; set => Set(ref _children, value, nameof(Children)); }
    public bool HasChildren { get => _hasChildren; set => Set(ref _hasChildren, value, nameof(HasChildren)); }
    public bool Expanded { get => _expanded; set => Set(ref _expanded, value, nameof(Expanded)); }
}
public sealed class A3Converter : IValueConverter
{
    public static CultureInfo French => CultureInfo.GetCultureInfo("fr-FR");
    public Func<object?, Type, object?, CultureInfo, object?>? Back { get; set; }
    public Type? LastType { get; private set; }
    public object? LastParameter { get; private set; }
    public CultureInfo? LastCulture { get; private set; }
    public int Calls { get; private set; }
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => "amount " + System.Convert.ToString(value, culture);
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        Calls++; LastType = targetType; LastParameter = parameter; LastCulture = culture;
        return Back is { } back ? back(value, targetType, parameter, culture) : decimal.Parse(value!.ToString()!.Replace("amount ", "", StringComparison.Ordinal), culture);
    }
}
