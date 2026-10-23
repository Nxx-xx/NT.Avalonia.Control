using System.Windows.Input;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private ChartPointHit? _hitSelectedPoint;
    private ChartViewport? _hitViewport;
    private ChartPointHit? _hitDenseSelectedPoint;
    private LevelOfDetailUsageRecord[] _hitDenseRecords = HitRecords("usage-hit-");
    public LevelOfDetailUsageRecord[] HitBackRecords { get; } = [new("back-0", 2, 2), new("back-1", 5, 3), new("back-2", 5, 7)];
    public LevelOfDetailUsageRecord[] HitFrontRecords { get; } = [new("front-0", 2, 2), new("front-1", 2, 2), new("front-2", 5, 3), new("front-3", 5, 7)];
    public LevelOfDetailUsageRecord[] HitRightRecords { get; } = [new("axis-0", 8, 800)];
    public LevelOfDetailUsageRecord[] HitDenseRecords { get => _hitDenseRecords; private set { _hitDenseRecords = value; Notify(); } }
    public ChartPointHit? HitSelectedPoint { get => _hitSelectedPoint; set { if (ReferenceEquals(_hitSelectedPoint, value)) return; _hitSelectedPoint = value; Notify(); Notify(nameof(HitSelectionText)); } }
    public ChartPointHit? HitDenseSelectedPoint { get => _hitDenseSelectedPoint; set { if (ReferenceEquals(_hitDenseSelectedPoint, value)) return; _hitDenseSelectedPoint = value; Notify(); Notify(nameof(HitSelectionText)); } }
    public ChartViewport? HitViewport { get => _hitViewport; set { if (_hitViewport == value) return; _hitViewport = value; Notify(); } }
    public string HitSelectionText => (HitDenseSelectedPoint ?? HitSelectedPoint) is { } hit ? $"原始记录 {hit.StableId} · 源索引 {hit.Index} · 原始值 {hit.Point.Y:0.000}" : "命中与选择始终保留原始记录";
    public ICommand HitReplaceCommand => new ActionCommand(() => { HitDenseRecords = HitRecords("replacement-hit-"); HitDenseSelectedPoint = null; });
    public ICommand HitOverviewCommand => new ActionCommand(() => HitViewport = null);
    public ICommand HitDetailCommand => new ActionCommand(() => HitViewport = new ChartViewport(2300, 2400, 40, 50));
    private static LevelOfDetailUsageRecord[] HitRecords(string prefix) => Enumerable.Range(0, 5000).Select(i => new LevelOfDetailUsageRecord(prefix + i, i, i * .02)).ToArray();
}
