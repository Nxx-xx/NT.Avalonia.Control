using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Benchmarks;

/// <summary>
/// PERF17 present harness. It names the stages the product must not conflate: decode complete
/// and UI submit come from the product's spans, and only this probe records a compositor
/// render — a <see cref="CompositionBatch.Rendered"/> notification after a batch commit.
/// That notification is not a scan-out or physical present; <see cref="ScanOutVerified"/> stays
/// false and the measurement method is named honestly in the record.
/// </summary>
internal static class PresentProbe
{
    internal const string MethodName = "composition-batch-rendered";
    internal const bool ScanOutVerified = false;

    internal static Compositor? TryGetCompositor(Window? window) =>
        window is null ? null : ElementComposition.GetElementVisual(window)?.Compositor;

    /// <summary>
    /// Requests a composition batch commit and waits for its rendered notification.
    /// Returns the commit-to-rendered latency in ms, or null with a reason when no compositor
    /// is reachable (headless, torn-down window).
    /// </summary>
    internal static async Task<(double? LatencyMs, string? Method, string? UnavailableReason)> CaptureAsync(
        Window? window, CancellationToken token)
    {
        var compositor = TryGetCompositor(window);
        if (compositor is null)
            return (null, null, "no-compositor: window lacks a composition target (headless or closed)");
        var started = Stopwatch.GetTimestamp();
        var batch = compositor.RequestCompositionBatchCommitAsync();
        await batch.Rendered.WaitAsync(token);
        return (PdfPerformanceSession.MillisecondsBetween(started, Stopwatch.GetTimestamp()), MethodName, null);
    }

    /// <summary>
    /// Marks one frame of continuous interaction: the caller moves the content, this waits for
    /// the compositor to render the batch and returns both the commit-to-rendered latency and
    /// the absolute rendered timestamp so consecutive frames give an interval series.
    /// </summary>
    internal static async Task<(double LatencyMs, long RenderedAt)> CaptureFrameAsync(Window window, CancellationToken token)
    {
        var compositor = TryGetCompositor(window) ?? throw new InvalidOperationException("Frame capture requires a compositor.");
        var started = Stopwatch.GetTimestamp();
        var batch = compositor.RequestCompositionBatchCommitAsync();
        await batch.Rendered.WaitAsync(token);
        var renderedAt = Stopwatch.GetTimestamp();
        return (PdfPerformanceSession.MillisecondsBetween(started, renderedAt), renderedAt);
    }
}
