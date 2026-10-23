using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TreeDataGrid.Headless.Tests;

public sealed partial class CompiledBindingConsumerView : UserControl
{
    public CompiledBindingConsumerView() => AvaloniaXamlLoader.Load(this);
}

public sealed class CompiledBindingItem : INotifyPropertyChanged
{
    private string _name = "name";
    private CompiledBindingDetails _details = new();
    private int _quantity = 12;
    public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
    public CompiledBindingDetails Details { get => _details; set { _details = value; PropertyChanged?.Invoke(this, new(nameof(Details))); } }
    public int Quantity { get => _quantity; set { _quantity = value; PropertyChanged?.Invoke(this, new(nameof(Quantity))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class CompiledBindingDetails : INotifyPropertyChanged
{
    private string _name = "nested";
    public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
