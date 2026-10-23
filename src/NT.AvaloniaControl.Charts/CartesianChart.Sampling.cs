using Avalonia;
using Avalonia.Controls;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private double _samplingSceneRenderScale = 1;
    private SamplingExportState? _samplingExport;
    private sealed record SamplingExportState(double Scale, bool Enabled, CancellationToken Cancellation);

    /// <summary>Returns the current display plan without changing source data or selection. Requires the UI thread.</summary>
    /// <remarks>Only ordered, linear, unstacked LineSeries without markers/labels and spatial brushes can be reduced.
    /// A cancelled request never publishes a partial plan. FigureCount is zero for conservative fallback paths.</remarks>
    public ChartSamplingInfo GetSamplingInfo(ChartSeries series, CancellationToken cancellationToken = default)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(series); cancellationToken.ThrowIfCancellationRequested();
        var scene = GetScene();
        var snapshot = scene?.Snapshots.FirstOrDefault(item => ReferenceEquals(item.Series, series));
        if (snapshot?.Context is not { } context)
            throw new ArgumentException("The series must be visible in an arranged chart.", nameof(series));
        return ChartSamplingPlanner.For(context, cancellationToken).Info;
    }

    private double SamplingRenderScale => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    private void CheckSamplingRenderScale()
    {
        var scale = SamplingRenderScale;
        if (_samplingSceneRenderScale.Equals(scale)) return;
        _samplingSceneRenderScale = scale;
        // Only view/retained geometry changes. Numeric data and its hierarchy survive DPI changes.
        InvalidateScene(clearAxisText: false);
    }

    private ChartSeriesRenderContext SamplingRenderContext(ChartSeriesRenderContext context) =>
        _samplingExport is { } export && context.Series is LineSeries { SamplingMode: not ChartSamplingMode.None }
            ? context with { RenderScaling = export.Scale, SamplingDisabled = !export.Enabled, SamplingCancellation = export.Cancellation }
            : context;

    private IDisposable BeginSamplingExport(double scale, bool enabled, CancellationToken cancellationToken)
    {
        var previous = _samplingExport;
        _samplingExport = new(scale, enabled, cancellationToken);
        return new SamplingExportScope(this, previous);
    }
    private sealed class SamplingExportScope(CartesianChart chart, SamplingExportState? previous) : IDisposable
    { public void Dispose() => chart._samplingExport = previous; }
}
