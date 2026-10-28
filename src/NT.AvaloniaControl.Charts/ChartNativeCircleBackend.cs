using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;

namespace NT.AvaloniaControl.Charts;

// Optional platform packages install a provider. Core consumes only the normal
// Avalonia reference API and keeps its original rendering path when absent.
internal interface IChartNativeCirclePlan
{
    long EstimatedBytes { get; }
    long SharedHistoryEstimatedBytes { get; }
    bool SharedHistoryActive { get; }
    ICustomDrawOperation CreateFrame(IBrush fill, Pen stroke, bool snapshotPaint);
}

internal static class ChartNativeCircleBackend
{
    private sealed record Provider(Func<ChartSeries, Point[], double, IBrush, Pen, IChartNativeCirclePlan?> Factory, Action<ChartSeries> Clear);
    private static Provider? _provider;
    internal static long EstimateManagedBytes(int count) => checked(1024L + count * 576L);
    internal static void Register(Func<ChartSeries, Point[], double, IBrush, Pen, IChartNativeCirclePlan?> factory, Action<ChartSeries> clear)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(clear);
        Volatile.Write(ref _provider, new(factory, clear));
    }
    internal static IChartNativeCirclePlan? TryCreate(ChartSeries series, Point[] centers, double radius, IBrush fill, Pen stroke) =>
        Volatile.Read(ref _provider)?.Factory(series, centers, radius, fill, stroke);
    internal static void Clear(ChartSeries series) => Volatile.Read(ref _provider)?.Clear(series);
}
