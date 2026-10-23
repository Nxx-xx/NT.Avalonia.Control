using System.ComponentModel;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private Orientation _barOrientation = Orientation.Vertical;
    private ChartStackMode _barStackMode = ChartStackMode.Stacked;
    private ChartSeriesSortMode _barSortMode;
    private double? _barWidth;
    private bool _barLabels;
    private readonly ChartAxis _barCategoryAxis = new CategoryAxis { Minimum = -.5, Maximum = 2.5 };
    private readonly ChartAxis _barValueAxis = new NumericAxis { Minimum = -120, Maximum = 120 };
    public Orientation BarOrientation { get => _barOrientation; set { if (_barOrientation == value) return; _barOrientation = value; Notify(); Notify(nameof(BarXAxis)); Notify(nameof(BarYAxis)); } }
    public ChartStackMode BarStackMode { get => _barStackMode; set { if (_barStackMode == value) return; _barStackMode = value; Notify(); } }
    public ChartSeriesSortMode BarSortMode { get => _barSortMode; set { if (_barSortMode == value) return; _barSortMode = value; Notify(); } }
    public double? BarWidth { get => _barWidth; set { if (_barWidth == value) return; _barWidth = value; Notify(); } }
    public bool BarLabels { get => _barLabels; set { if (_barLabels == value) return; _barLabels = value; Notify(); } }
    public Orientation[] BarOrientations { get; } = [Orientation.Vertical, Orientation.Horizontal];
    public ChartStackMode[] BarStackModes { get; } = Enum.GetValues<ChartStackMode>();
    public ChartSeriesSortMode[] BarSortModes { get; } = Enum.GetValues<ChartSeriesSortMode>();
    public double?[] BarWidths { get; } = [null, 12, 24];
    public ChartAxis BarXAxis => BarOrientation == Orientation.Vertical ? _barCategoryAxis : _barValueAxis;
    public ChartAxis BarYAxis => BarOrientation == Orientation.Vertical ? _barValueAxis : _barCategoryAxis;
    public double BarXMinimum => BarOrientation == Orientation.Vertical ? 0 : -120;
    public double BarXMaximum => BarOrientation == Orientation.Vertical ? 6 : 120;
    public double BarYMinimum => BarOrientation == Orientation.Vertical ? -120 : 0;
    public double BarYMaximum => BarOrientation == Orientation.Vertical ? 120 : 6;
    public BarComparisonUsageRecord[] BarRecords { get; } = [new("usage-bar-a", 1, 20), new("usage-bar-b", 3, -20), new("usage-bar-c", 5, 30)];
    public ChartPoint[] BarSecondPoints { get; } = [new(1, 30), new(3, -30), new(5, 70)];
    public RangeAreaUsageRecord[] BarRangeRecords { get; } = [new("usage-bar-range", 1, 20, 80)];
    public BarComparisonUsageRecord[] WaterfallRecords { get; } = [new("usage-waterfall-a", 1, 40), new("usage-waterfall-b", 2, -15), new("usage-waterfall-c", 3, 20), new("usage-waterfall-total", 4, 45, true)];
    public ChartPoint[] ParetoPoints { get; } = [new(1, 30), new(2, 50), new(3, 20)];
    public ChartPoint[] DotPoints { get; } = [new(2, 60)];
    public ChartPoint[] LollipopPoints { get; } = [new(3, 50)];
    public RangeAreaUsageRecord[] VarianceRecords { get; } = [new("usage-variance", 4, 25, 75)];
}

public sealed class BarComparisonUsageRecord(string id, double category, double value, bool isTotal = false) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private double _value = value;
    private bool _isTotal = isTotal;
    public string Id { get; } = id;
    public double Category { get; } = category;
    public double Value { get => _value; set { if (_value == value) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
    public bool IsTotal { get => _isTotal; set { if (_isTotal == value) return; _isTotal = value; _changed?.Invoke(this, new(nameof(IsTotal))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
