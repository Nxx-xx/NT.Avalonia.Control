using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Charts.Headless.Tests;

public sealed partial class SharedTooltipPresentationView : UserControl
{
    public SharedTooltipPresentationView() => AvaloniaXamlLoader.Load(this);
}

public sealed class SharedTooltipPresentationModel
{
    public string Title { get; init; } = "Business title";
    public ObservableCollection<SharedTooltipRow> Records { get; init; } =
        [new("a", "A", 4, "R1", "C1"), new("b", "B", 3, "R2", "C1"), new("c", "C", 2, "R1", "C1")];
}

public sealed class SharedTooltipRow(string id, string label, double value, string row, string column) : INotifyPropertyChanged
{
    private double _value = value;
    public string Id { get; } = id;
    public string Label { get; } = label;
    public string Row { get; } = row;
    public string Column { get; } = column;
    public double Value { get => _value; set { _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
