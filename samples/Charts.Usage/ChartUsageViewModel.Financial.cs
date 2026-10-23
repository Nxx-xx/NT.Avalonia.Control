using System.ComponentModel;
using System.Windows.Input;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private double _financialBoxSize = 2;
    private ChartPointHit? _financialSelectedPoint;
    private bool _financialVariant;
    public FinancialUsageRecord[] FinancialRecords { get; } = CreateFinancialRecords();
    public double[] FinancialBoxSizes { get; } = [1, 2, 4];
    public double FinancialBoxSize { get => _financialBoxSize; set { if (_financialBoxSize == value) return; _financialBoxSize = value; Notify(); } }
    public ChartPointHit? FinancialSelectedPoint { get => _financialSelectedPoint; set { if (ReferenceEquals(_financialSelectedPoint, value)) return; _financialSelectedPoint = value; Notify(); Notify(nameof(FinancialSelectionText)); } }
    public string FinancialSelectionText => FinancialSelectedPoint?.Financial is { } sample
        ? $"原始 {FinancialSelectedPoint.StableId} · {sample.SourceEnd.Time:yyyy-MM-dd} · O/H/L/C {sample.SourceEnd.Open}/{sample.SourceEnd.High}/{sample.SourceEnd.Low}/{sample.SourceEnd.Close} · 显示 {sample.Kind} {sample.Close}"
        : "点击金融图查看原始交易记录；HA、砖块与 X/O 显示价均为派生值。";
    public ICommand FinancialUpdateCommand => new ActionCommand(() =>
    {
        _financialVariant = !_financialVariant;
        FinancialRecords[1].Close = _financialVariant ? 13 : 12;
    });
    public ChartFinancialPoint[] FinancialHostPoints => FinancialRecords.Select(record => record.ToPoint()).ToArray();
    private static FinancialUsageRecord[] CreateFinancialRecords()
    {
        double[] closes = [10, 12, 15, 11, 7, 10, 14];
        int[] days = [2, 5, 6, 7, 8, 9, 12];
        return closes.Select((close, i) => new FinancialUsageRecord($"financial-{i}",
            new DateTimeOffset(2026, 10, days[i], 0, 0, 0, TimeSpan.Zero), close - 1, close + 2,
            close - 2, close, (i + 1) * 100)).ToArray();
    }
}

public sealed class FinancialUsageRecord(string id, DateTimeOffset time, double open, double high,
    double low, double close, double volume) : INotifyPropertyChanged
{
    private double _close = close;
    public string Id { get; } = id;
    public DateTimeOffset Time { get; } = time;
    public double Open { get; } = open;
    public double High { get; } = high;
    public double Low { get; } = low;
    public double Close { get => _close; set { if (_close == value) return; _close = value; PropertyChanged?.Invoke(this, new(nameof(Close))); } }
    public double Volume { get; } = volume;
    public ChartFinancialPoint ToPoint() => new(Time, Open, High, Low, Close, Volume);
    public event PropertyChangedEventHandler? PropertyChanged;
}
