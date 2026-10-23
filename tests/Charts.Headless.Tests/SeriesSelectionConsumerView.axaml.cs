using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed partial class SeriesSelectionConsumerView : UserControl
{
    public SeriesSelectionConsumerView() => AvaloniaXamlLoader.Load(this);
}

public sealed class SeriesSelectionConsumerModel : INotifyPropertyChanged
{
    private ChartSelectionMode _mode = ChartSelectionMode.Multiple;
    private IReadOnlyList<ChartSeries>? _selection;
    private double _opacity = .3;
    public ObservableCollection<ChartSeries> Series { get; } =
    [
        new LineSeries { ItemsSource = new[] { new ChartPoint(1, 20), new ChartPoint(9, 20) } },
        new ScatterSeries { ItemsSource = new[] { new ChartPoint(7, 80) } },
    ];
    public ChartSelectionMode Mode { get => _mode; set { if (_mode == value) return; _mode = value; Changed(); } }
    public IReadOnlyList<ChartSeries>? Selection { get => _selection; set { if (ReferenceEquals(_selection, value)) return; _selection = value; Changed(); } }
    public double Opacity { get => _opacity; set { if (_opacity == value) return; _opacity = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
