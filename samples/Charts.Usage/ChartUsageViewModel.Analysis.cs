using System.ComponentModel;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private double _analysisGap = 2, _analysisNeck = .2;
    private int _analysisSelectedIndex = -1;
    private bool _analysisLabels = true, _analysisSelection = true, _analysisRiverVariant;
    public double AnalysisGap { get => _analysisGap; set { if (_analysisGap == value) return; _analysisGap = value; Notify(); } }
    public double AnalysisNeck { get => _analysisNeck; set { if (_analysisNeck == value) return; _analysisNeck = value; Notify(); } }
    public int AnalysisSelectedIndex { get => _analysisSelectedIndex; set { if (_analysisSelectedIndex == value) return; _analysisSelectedIndex = value; Notify(); Notify(nameof(AnalysisSelectionText)); } }
    public string AnalysisSelectionText => AnalysisSelectedIndex >= 0 && AnalysisSelectedIndex < AnalysisRecords.Count ? $"业务项 {AnalysisRecords[AnalysisSelectedIndex].Id}" : "尚未选择";
    public bool AnalysisLabels { get => _analysisLabels; set { if (_analysisLabels == value) return; _analysisLabels = value; Notify(); } }
    public bool AnalysisSelection { get => _analysisSelection; set { if (_analysisSelection == value) return; _analysisSelection = value; Notify(); Notify(nameof(AnalysisCartesianSelection)); } }
    public ChartSelectionMode AnalysisCartesianSelection => AnalysisSelection ? ChartSelectionMode.Single : ChartSelectionMode.None;
    public bool AnalysisRiverVariant { get => _analysisRiverVariant; set { if (_analysisRiverVariant == value) return; _analysisRiverVariant = value; Notify(); Notify(nameof(AnalysisRiverBaseline)); Notify(nameof(AnalysisRiverFirst)); Notify(nameof(AnalysisRiverSecond)); } }
    public GaugeObserverCollection<AnalysisUsageRecord> AnalysisRecords { get; } = [new("analysis-a", "触达", 120), new("analysis-b", "访问", 85), new("analysis-c", "意向", 50), new("analysis-d", "成交", 25)];
    public GaugeObserverCollection<AnalysisUsageCell> AnalysisCells { get; } = [new("heat-a", 1, "周一", 20), new("heat-b", 1, "周二", 55), new("heat-c", 1, "周三", double.NaN), new("heat-d", "1", "周一", 80), new("heat-e", "1", "周二", 40)];
    public GaugeObserverCollection<AnalysisUsageRecord> AnalysisSymbols { get; } = [new("symbol-a", "线上", 75), new("symbol-b", "门店", -55), new("symbol-c", "订阅", 35)];
    public Geometry AnalysisSymbolGeometry { get; } = new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Avalonia.Rect(0, 0, 1, 1)), new EllipseGeometry(new Avalonia.Rect(.3, .3, .4, .4)));
    private double[] RiverFirstValues => AnalysisRiverVariant ? [25, 34, 20, 28, 22, 15, 25] : [15, 24, 30, 18, 12, 25, 15];
    private double[] RiverSecondValues => [20, 16, 22, 35, 26, 15, 20];
    public IReadOnlyList<ChartPoint> AnalysisRiverBaseline => ThemeRiverStack.CreateBaseline(new IReadOnlyList<double>[] { RiverFirstValues, RiverSecondValues }, 50);
    public IReadOnlyList<ChartPoint> AnalysisRiverFirst => RiverFirstValues.Select((value, index) => new ChartPoint(index, value)).ToArray();
    public IReadOnlyList<ChartPoint> AnalysisRiverSecond => RiverSecondValues.Select((value, index) => new ChartPoint(index, value)).ToArray();
}

public sealed class AnalysisUsageRecord(string id, object category, double value) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed; private double _value = value;
    public string Id { get; } = id; public object Category { get; } = category;
    public double Value { get => _value; set { if (_value.Equals(value)) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
public sealed class AnalysisUsageCell(string id, object row, object column, double value) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed; private double _value = value;
    public string Id { get; } = id; public object Row { get; } = row; public object Column { get; } = column;
    public double Value { get => _value; set { if (_value.Equals(value)) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
