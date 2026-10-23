using System.Windows.Input;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private bool _lodEnabled = true;
    private int _lodPixelWidth = 1;
    private ChartViewport? _lodViewport;
    private ChartPointHit? _lodSelectedPoint;
    public bool LodEnabled { get => _lodEnabled; set { if (_lodEnabled == value) return; _lodEnabled = value; Notify(); Notify(nameof(LodSamplingMode)); } }
    public ChartSamplingMode LodSamplingMode => LodEnabled ? ChartSamplingMode.MinMax : ChartSamplingMode.None;
    public int LodPixelWidth { get => _lodPixelWidth; set { if (_lodPixelWidth == value) return; _lodPixelWidth = value; Notify(); } }
    public int[] LodPixelWidths { get; } = [1, 2, 4, 8];
    public ChartViewport? LodViewport { get => _lodViewport; set { if (_lodViewport == value) return; _lodViewport = value; Notify(); } }
    public ChartPointHit? LodSelectedPoint { get => _lodSelectedPoint; set { if (ReferenceEquals(_lodSelectedPoint, value)) return; _lodSelectedPoint = value; Notify(); Notify(nameof(LodSelectionText)); } }
    public string LodSelectionText => LodSelectedPoint is { } point ? $"原始源索引 {point.Index} · {point.StableId} · 原始值 {point.Point.Y:0.000}" : "尚未选择原始记录";
    public LevelOfDetailUsageRecord[] LodRecords { get; } = CreateLodRecords();
    public ICommand LodOverviewCommand => new ActionCommand(() => LodViewport = null);
    public ICommand LodDetailCommand => new ActionCommand(() => LodViewport = new ChartViewport(3310, 3360, -110, 110));
    public ICommand LodGapCommand => new ActionCommand(() => LodViewport = new ChartViewport(4976, 5024, -110, 110));
    private static LevelOfDetailUsageRecord[] CreateLodRecords()
    {
        var rows = new LevelOfDetailUsageRecord[10_000];
        for (var i = 0; i < rows.Length; i++)
        {
            var value = Math.Sin(i * .0123) * 14 + Math.Sin(i * .173) * 3;
            if (i == 3334) value = 95;
            if (i == 6667) value = -95;
            if (i >= 4998 && i <= 5002) value = double.NaN;
            rows[i] = new("usage-sample-" + i, i, value);
        }
        return rows;
    }
}

public sealed record LevelOfDetailUsageRecord(string Id, double X, double Y);
