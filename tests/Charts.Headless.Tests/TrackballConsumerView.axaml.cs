using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed partial class TrackballConsumerView : UserControl
{
    public TrackballConsumerView() => AvaloniaXamlLoader.Load(this);
}

public sealed class TrackballBusinessRow(string id, double x, double y, string name) : INotifyPropertyChanged
{
    private double _y = y;
    private string _name = name;
    public string Id { get; } = id;
    public double X { get; } = x;
    public double Y { get => _y; set { if (_y.Equals(value)) return; _y = value; PropertyChanged?.Invoke(this, new(nameof(Y))); } }
    public string Name { get => _name; set { if (_name == value) return; _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TrackballConsumerModel : INotifyPropertyChanged
{
    private ChartTrackballMode _mode = ChartTrackballMode.Vertical;
    private ChartTrackballDisplayMode _display = ChartTrackballDisplayMode.GroupAllPoints;
    private ChartTrackballSnapMode _snap = ChartTrackballSnapMode.LinearInterpolation;
    private IDataTemplate? _contentTemplate;
    public ChartTrackballMode Mode { get => _mode; set { if (_mode == value) return; _mode = value; Changed(nameof(Mode)); } }
    public ChartTrackballDisplayMode Display { get => _display; set { if (_display == value) return; _display = value; Changed(nameof(Display)); } }
    public ChartTrackballSnapMode Snap { get => _snap; set { if (_snap == value) return; _snap = value; Changed(nameof(Snap)); } }
    public IDataTemplate? ContentTemplate { get => _contentTemplate; set { if (ReferenceEquals(_contentTemplate, value)) return; _contentTemplate = value; Changed(nameof(ContentTemplate)); } }
    public ObservableCollection<TrackballBusinessRow> Rows { get; } = [new("primary-left", 1, 20, "original left"), new("primary-right", 5, 60, "original right")];
    public ObservableCollection<TrackballBusinessRow> SecondaryRows { get; } = [new("secondary-left", 1, 80, "second left"), new("secondary-right", 5, 40, "second right")];
    private void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}
