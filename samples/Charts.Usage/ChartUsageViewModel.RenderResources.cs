using System.Windows.Input;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private long _resourceBudgetBytes = 128 * 1024;
    private string _resourceCacheSummary = "展开后可刷新估算缓存统计；未计原生/GPU和共享数据索引。";
    private ChartPointHit? _resourceSelectedPoint;
    public long ResourceBudgetBytes { get => _resourceBudgetBytes; set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); if (_resourceBudgetBytes == value) return; _resourceBudgetBytes = value; Notify(); Notify(nameof(ResourceBudgetSummary)); } }
    public string ResourceBudgetSummary => $"本图缓存预算 {ResourceBudgetBytes:N0} B（估算模型）";
    public string ResourceCacheSummary { get => _resourceCacheSummary; internal set { _resourceCacheSummary = value; Notify(); } }
    public string ResourcePageSummary => $"保留 {ResourceRecords.Count} 条，当前数据页 {ResourceRecords.StoragePageCount}；Compact显式复制保留行，旧快照另计。";
    public ChartPointHit? ResourceSelectedPoint { get => _resourceSelectedPoint; set { if (ReferenceEquals(_resourceSelectedPoint, value)) return; _resourceSelectedPoint = value; Notify(); Notify(nameof(ResourceSelectionText)); } }
    public string ResourceSelectionText => ResourceSelectedPoint is { } hit ? $"原始 {hit.StableId} · 当前索引 {hit.Index}" : "选择点检查压实前后的原始记录";
    public ChartDataBuffer<LevelOfDetailUsageRecord> ResourceRecords { get; } = CreateResourceRecords();
    public SolidColorBrush ResourceStroke { get; } = new(Color.Parse("#4385F4"));
    public ICommand ResourceNormalBudgetCommand => new ActionCommand(() => ResourceBudgetBytes = 128 * 1024);
    public ICommand ResourceSmallBudgetCommand => new ActionCommand(() => ResourceBudgetBytes = 4096);
    public ICommand ResourceDisableCacheCommand => new ActionCommand(() => ResourceBudgetBytes = 0);
    public ICommand ResourceCompactCommand => new ActionCommand(() => { ResourceRecords.Compact(); NotifyRenderResourcePages(); });
    public ICommand ResourceBrushCommand => new ActionCommand(() => ResourceStroke.Color = ResourceStroke.Color == Color.Parse("#4385F4") ? Color.Parse("#9A60D0") : Color.Parse("#4385F4"));
    internal void NotifyRenderResourcePages() => Notify(nameof(ResourcePageSummary));
    private static ChartDataBuffer<LevelOfDetailUsageRecord> CreateResourceRecords()
    {
        var source = new ChartDataBuffer<LevelOfDetailUsageRecord>(128);
        for (var q = 0; q < 64; q++) source.AppendRange(new[] { new LevelOfDetailUsageRecord("usage-resource-" + q, q, 20 + q % 16) });
        return source;
    }
}