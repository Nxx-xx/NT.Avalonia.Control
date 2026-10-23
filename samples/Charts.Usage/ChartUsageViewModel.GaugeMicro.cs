using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private double _gaugeValue = 68, _gaugeMinimum, _gaugeMaximum = 100;
    private double? _gaugeTarget = 80;
    private Orientation _gaugeOrientation;
    private bool _gaugeWave, _gaugeReduceMotion, _gaugePaused;
    private GaugeLiquidShape _gaugeLiquidShape;
    public double GaugeValue { get => _gaugeValue; set { if (_gaugeValue == value) return; _gaugeValue = value; Notify(); } }
    public double GaugeMinimum { get => _gaugeMinimum; set { if (_gaugeMinimum == value) return; _gaugeMinimum = value; Notify(); } }
    public double GaugeMaximum { get => _gaugeMaximum; set { if (_gaugeMaximum == value) return; _gaugeMaximum = value; Notify(); } }
    public double? GaugeTarget { get => _gaugeTarget; set { if (_gaugeTarget == value) return; _gaugeTarget = value; Notify(); } }
    public Orientation GaugeOrientation { get => _gaugeOrientation; set { if (_gaugeOrientation == value) return; _gaugeOrientation = value; Notify(); } }
    public bool GaugeWave { get => _gaugeWave; set { if (_gaugeWave == value) return; _gaugeWave = value; Notify(); } }
    public bool GaugeReduceMotion { get => _gaugeReduceMotion; set { if (_gaugeReduceMotion == value) return; _gaugeReduceMotion = value; Notify(); } }
    public bool GaugePaused { get => _gaugePaused; set { if (_gaugePaused == value) return; _gaugePaused = value; Notify(); } }
    public GaugeLiquidShape GaugeLiquidShape { get => _gaugeLiquidShape; set { if (_gaugeLiquidShape == value) return; _gaugeLiquidShape = value; Notify(); } }
    public Orientation[] GaugeOrientations { get; } = Enum.GetValues<Orientation>();
    public GaugeLiquidShape[] GaugeLiquidShapes { get; } = Enum.GetValues<GaugeLiquidShape>();
    public GaugeObserverCollection<GaugeRange> GaugeRanges { get; } = [new() { Start = 0, End = 40, Brush = Brushes.Teal }, new() { Start = 40, End = 75, Brush = Brushes.Orange }, new() { Start = 75, End = 100, Brush = Brushes.Crimson }];
    public GaugeObserverCollection<GaugeValue> GaugeRings { get; } = [new() { Value = 72, Label = "本期", Brush = Brushes.CornflowerBlue }, new() { Value = 48, Label = "上期", Brush = Brushes.Teal }, new() { Value = 90, Label = "目标", Brush = Brushes.Orange }];
    public GaugeObserverCollection<MicroUsageRecord> MicroRecords { get; } = [new("micro-a", "A", 25), new("micro-b", "B", 55), new("micro-c", "C", -15), new("micro-d", "D", 75), new("micro-e", "E", 0), new("micro-f", "F", 45), new("micro-g", "G", 30)];
}

public sealed class GaugeObserverCollection<T> : ObservableCollection<T>
{
    private NotifyCollectionChangedEventHandler? _changed;
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public override event NotifyCollectionChangedEventHandler? CollectionChanged { add => _changed += value; remove => _changed -= value; }
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) { base.OnCollectionChanged(e); _changed?.Invoke(this, e); }
}

public sealed class MicroUsageRecord(string id, string label, double value) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private double _value = value;
    public string Id { get; } = id;
    public string Label { get; } = label;
    public double Value { get => _value; set { if (_value == value) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
