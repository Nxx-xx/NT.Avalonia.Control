using System.ComponentModel;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private ChartSizeScale _scatterBubbleSizeScale = ChartSizeScale.Area;
    private ChartPointDrawOrder _scatterBubbleDrawOrder;
    private bool _scatterBubbleLabels;
    private bool _scatterBubbleShowLine = true;
    public ChartSizeScale ScatterBubbleSizeScale { get => _scatterBubbleSizeScale; set { if (_scatterBubbleSizeScale == value) return; _scatterBubbleSizeScale = value; Notify(); } }
    public ChartPointDrawOrder ScatterBubbleDrawOrder { get => _scatterBubbleDrawOrder; set { if (_scatterBubbleDrawOrder == value) return; _scatterBubbleDrawOrder = value; Notify(); } }
    public bool ScatterBubbleLabels { get => _scatterBubbleLabels; set { if (_scatterBubbleLabels == value) return; _scatterBubbleLabels = value; Notify(); } }
    public bool ScatterBubbleShowLine { get => _scatterBubbleShowLine; set { if (_scatterBubbleShowLine == value) return; _scatterBubbleShowLine = value; Notify(); } }
    public ChartSizeScale[] ScatterBubbleSizeScales { get; } = Enum.GetValues<ChartSizeScale>();
    public ChartPointDrawOrder[] ScatterBubbleDrawOrders { get; } = Enum.GetValues<ChartPointDrawOrder>();
    public ScatterBubbleUsageRecord[] ScatterBubbleRecords { get; } =
    [
        new("usage-bubble-a", 2, 25, 0, 0),
        new("usage-bubble-b", 5, 55, 50, 50),
        new("usage-bubble-c", 8, 75, 100, 100),
    ];
    public ChartPoint[] ScatterLinePoints { get; } =
        [new(1, 20), new(7, 80), new(3, 60), new(4, double.NaN), new(7, 20), new(9, 40)];
}

public sealed class ScatterBubbleUsageRecord(string id, double x, double y, double weight, double color) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private double _x = x, _y = y, _weight = weight, _color = color;
    public string Id { get; } = id;
    public double X { get => _x; set { if (_x == value) return; _x = value; _changed?.Invoke(this, new(nameof(X))); } }
    public double Y { get => _y; set { if (_y == value) return; _y = value; _changed?.Invoke(this, new(nameof(Y))); } }
    public double Weight { get => _weight; set { if (_weight == value) return; _weight = value; _changed?.Invoke(this, new(nameof(Weight))); } }
    public double Color { get => _color; set { if (_color == value) return; _color = value; _changed?.Invoke(this, new(nameof(Color))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
