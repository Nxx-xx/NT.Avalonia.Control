using System.ComponentModel;
using System.Windows.Input;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private bool _statisticsShowOutliers = true, _statisticsVariant;
    private ChartErrorMode _statisticsErrorMode;
    private double _statisticsBandwidth = .75;
    private ChartHistogramNormalization _statisticsNormalization = ChartHistogramNormalization.Density;
    private ChartPointHit? _statisticsSelectedPoint;
    private IReadOnlyList<ChartHistogramBin> _statisticsBins = ChartStatistics.Histogram(StatisticsSamples(false), 9, ChartHistogramNormalization.Density, -3, 6).Bins;
    private IReadOnlyList<ChartPoint> _statisticsDensity = ChartStatistics.KernelDensity(StatisticsSamples(false), 129, .75, -3, 6).Points;

    public StatisticsUsageGroup[] StatisticsGroups { get; } =
    [new("stats-group-a", "批次 A", [10, 11, 12, 13, 14, 95]), new("stats-group-b", "批次 B", [25, 28, 30, 32, 35]), new("stats-group-c", "批次 C", [45, 45, 45])];
    public StatisticsUsageError[] StatisticsErrors { get; } =
    [new("stats-error-a", "样本 A", 20, 2, 4), new("stats-error-b", "样本 B", 40, 3, 6), new("stats-error-c", "样本 C", 60, 4, 8)];
    public bool StatisticsShowOutliers { get => _statisticsShowOutliers; set { if (_statisticsShowOutliers == value) return; _statisticsShowOutliers = value; Notify(); } }
    public ChartErrorMode StatisticsErrorMode { get => _statisticsErrorMode; set { if (_statisticsErrorMode == value) return; _statisticsErrorMode = value; Notify(); } }
    public double StatisticsBandwidth { get => _statisticsBandwidth; set { if (_statisticsBandwidth == value) return; _statisticsBandwidth = value; Notify(); RebuildStatisticsDistributions(); } }
    public ChartHistogramNormalization StatisticsNormalization { get => _statisticsNormalization; set { if (_statisticsNormalization == value) return; _statisticsNormalization = value; Notify(); RebuildStatisticsDistributions(); } }
    public IReadOnlyList<ChartHistogramBin> StatisticsBins => _statisticsBins;
    public IReadOnlyList<ChartPoint> StatisticsDensity => _statisticsDensity;
    public ChartErrorMode[] StatisticsErrorModes { get; } = Enum.GetValues<ChartErrorMode>();
    public ChartHistogramNormalization[] StatisticsNormalizations { get; } = Enum.GetValues<ChartHistogramNormalization>();
    public double[] StatisticsBandwidths { get; } = [.35, .75, 1.5];
    public ChartPointHit? StatisticsSelectedPoint { get => _statisticsSelectedPoint; set { if (ReferenceEquals(_statisticsSelectedPoint, value)) return; _statisticsSelectedPoint = value; Notify(); Notify(nameof(StatisticsSelectionText)); } }
    public string StatisticsSelectionText => StatisticsSelectedPoint is { } selected ? $"选中原始组 {selected.StableId} · 中位数 {selected.Point.Y:0.##}" : "点击箱体或离群点查看原始组身份。";
    public ICommand StatisticsUpdateCommand => new ActionCommand(() =>
    {
        _statisticsVariant = !_statisticsVariant;
        StatisticsGroups[0].Samples = _statisticsVariant ? [20, 21, 22, 23, 24, 85] : [10, 11, 12, 13, 14, 95];
        StatisticsErrors[0].Center = _statisticsVariant ? 30 : 20;
        RebuildStatisticsDistributions();
    });

    private void RebuildStatisticsDistributions()
    {
        var samples = StatisticsSamples(_statisticsVariant);
        _statisticsBins = ChartStatistics.Histogram(samples, 9, StatisticsNormalization, -3, 6).Bins;
        _statisticsDensity = ChartStatistics.KernelDensity(samples, 129, StatisticsBandwidth, -3, 6).Points;
        Notify(nameof(StatisticsBins)); Notify(nameof(StatisticsDensity));
    }
    private static double[] StatisticsSamples(bool variant) => new double[] { -2, -1, -.5, 0, .5, 1, 1.5, 2, 3, 4 }.Select(value => value + (variant ? 1 : 0)).ToArray();
}

public sealed class StatisticsUsageGroup(string id, string name, double[] samples) : INotifyPropertyChanged
{
    private double[] _samples = samples;
    public string Id { get; } = id;
    public string Name { get; } = name;
    public double[] Samples { get => _samples; set { _samples = value; PropertyChanged?.Invoke(this, new(nameof(Samples))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class StatisticsUsageError(string id, string name, double center, double lower, double upper) : INotifyPropertyChanged
{
    private double _center = center;
    public string Id { get; } = id;
    public string Name { get; } = name;
    public double Center { get => _center; set { if (_center == value) return; _center = value; PropertyChanged?.Invoke(this, new(nameof(Center))); } }
    public double Lower { get; } = lower;
    public double Upper { get; } = upper;
    public event PropertyChangedEventHandler? PropertyChanged;
}
