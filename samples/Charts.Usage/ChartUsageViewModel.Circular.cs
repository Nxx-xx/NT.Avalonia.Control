using System.ComponentModel;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private double _circularInnerRadius = .6;
    private double _circularSweep = 360;
    private bool _circularLabels = true;
    private bool _circularReduceMotion = true;
    private bool _circularPaused;
    private ChartCircularNegativeValueMode _circularNegativeMode;
    public double CircularInnerRadius { get => _circularInnerRadius; set { if (_circularInnerRadius == value) return; _circularInnerRadius = value; Notify(); } }
    public double CircularSweep { get => _circularSweep; set { if (_circularSweep == value) return; _circularSweep = value; Notify(); } }
    public bool CircularLabels { get => _circularLabels; set { if (_circularLabels == value) return; _circularLabels = value; Notify(); } }
    public bool CircularReduceMotion { get => _circularReduceMotion; set { if (_circularReduceMotion == value) return; _circularReduceMotion = value; Notify(); } }
    public bool CircularPaused { get => _circularPaused; set { if (_circularPaused == value) return; _circularPaused = value; Notify(); } }
    public ChartCircularNegativeValueMode CircularNegativeMode { get => _circularNegativeMode; set { if (_circularNegativeMode == value) return; _circularNegativeMode = value; Notify(); } }
    public double[] CircularInnerRadii { get; } = [0, .4, .6, .8];
    public double[] CircularSweeps { get; } = [360, 270, 180];
    public ChartCircularNegativeValueMode[] CircularNegativeModes { get; } = [ChartCircularNegativeValueMode.Ignore, ChartCircularNegativeValueMode.Absolute];
    public CircularUsageRecord[] CircularRecords { get; } =
    [
        new("circular-a", "线上", 50, "#4385F4"),
        new("circular-b", "门店", 25, "#19A798"),
        new("circular-c", "合作", 25, "#F2A33A"),
        new("circular-zero", "零值", 0, "#9C247A"),
        new("circular-negative", "负值", -10, "#D7256A"),
    ];
}

public sealed class CircularUsageRecord(string id, string label, double value, string color) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private double _value = value;
    private string _color = color;
    public string Id { get; } = id;
    public string Label { get; } = label;
    public double Value { get => _value; set { if (_value == value) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
    public string Color { get => _color; set { if (_color == value) return; _color = value; _changed?.Invoke(this, new(nameof(Color))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
