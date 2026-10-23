using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CircularChart
{
    private CartesianChart? _exportHost;
    public event EventHandler<ChartExportFailedEventArgs>? ExportFailed;
    private CartesianChart ExportHost()
    {
        VerifyAccess();
        if (_exportHost is null)
        {
            _exportHost = new CartesianChart { ShowLegend = false, ShowGridLines = false };
            _exportHost.ExportRenderOverride = RenderCircularExport;
            _exportHost.ExportFailed += (_, e) => ExportFailed?.Invoke(this, e);
        }
        _exportHost.Measure(Bounds.Size); _exportHost.Arrange(new Rect(Bounds.Size));
        return _exportHost;
    }
    private void RenderCircularExport(DrawingContext context, ChartExportLayout layout, IBrush? background)
    {
        using var clip = context.PushClip(new Rect(layout.Dips));
        if (background is not null) context.DrawRectangle(background, null, new Rect(layout.Dips));
        using var transform = context.PushTransform(new Matrix(layout.Scale, 0, 0, layout.Scale, layout.Offset.X, layout.Offset.Y));
        using var chartClip = context.PushClip(new Rect(Bounds.Size));
        _ = GetSlices();
        ArrangeCenter();
        Render(context);
        // Draw the child tree into the same context as the chart. An intermediate
        // brush surface rounds fractional host/child offsets independently and
        // can add a second antialiasing pass to otherwise identical exports.
        if (_centerHost is { } host) RenderCenterVisual(context, host, Matrix.Identity, new Rect(Bounds.Size));
    }
    private static void RenderCenterVisual(DrawingContext context, Visual visual, Matrix parentTransform, Rect clipRect)
    {
        if (!visual.IsVisible || visual.Opacity <= 0) return;
        var rect = new Rect(visual.Bounds.Size);
        var transform = Matrix.CreateTranslation(visual.Bounds.Position);
        if (visual.RenderTransform is { } renderTransform)
        {
            var origin = visual.RenderTransformOrigin.ToPixels(visual.Bounds.Size);
            transform = Matrix.CreateTranslation(-origin.X, -origin.Y) * renderTransform.Value *
                Matrix.CreateTranslation(origin.X, origin.Y) * transform;
        }
        using var text = context.PushTextOptions(TextOptions.GetTextOptions(visual));
#pragma warning disable CS0618 // Preserve the installed renderer's legacy rendering option too.
        using var rendering = context.PushRenderOptions(new RenderOptions
        {
            TextRenderingMode = RenderOptions.GetTextRenderingMode(visual),
            BitmapInterpolationMode = RenderOptions.GetBitmapInterpolationMode(visual),
            EdgeMode = RenderOptions.GetEdgeMode(visual),
            BitmapBlendingMode = RenderOptions.GetBitmapBlendingMode(visual),
            RequiresFullOpacityHandling = RenderOptions.GetRequiresFullOpacityHandling(visual)
        });
#pragma warning restore CS0618
        using var position = context.PushTransform(transform);
        using var mirror = context.PushTransform(visual.HasMirrorTransform ? new Matrix(-1, 0, 0, 1, rect.Width, 0) : Matrix.Identity);
        using var opacity = context.PushOpacity(visual.Opacity);
        using IDisposable? boundsClip = visual.ClipToBounds
            ? visual is Border border ? context.PushClip(new RoundedRect(rect, border.ClipToBoundsRadius)) : context.PushClip(rect)
            : null;
        using IDisposable? geometryClip = visual.Clip is { } geometry ? context.PushGeometryClip(geometry) : null;
        using IDisposable? mask = visual.OpacityMask is { } brush ? context.PushOpacityMask(brush, rect) : null;
        var accumulated = transform * parentTransform;
        if (rect.TransformToAABB(accumulated).Intersects(clipRect)) visual.Render(context);
        // An offscreen parent may have visible overflowing children. Only its
        // own drawing is culled; a bounds clip resets the children's clip space.
        foreach (var child in visual.GetVisualChildren().OrderBy(v => v.ZIndex))
            RenderCenterVisual(context, child, visual.ClipToBounds ? Matrix.Identity : accumulated, visual.ClipToBounds ? rect : clipRect);
    }
    internal ChartExportLayout GetExportLayout(ChartExportOptions? options) => ExportHost().GetExportLayout(options);
    internal void RenderExport(DrawingContext context, ChartExportLayout layout, IBrush? background) => RenderCircularExport(context, layout, background);
    public bool TryExportPng(Stream destination, ChartExportOptions? options = null) => ExportHost().TryExportPng(destination, options);
    public bool TryExportPng(string path, ChartExportOptions? options = null) => ExportHost().TryExportPng(path, options);
    public bool TryExportJpeg(Stream destination, ChartExportOptions? options = null) => ExportHost().TryExportJpeg(destination, options);
    public bool TryExportJpeg(string path, ChartExportOptions? options = null) => ExportHost().TryExportJpeg(path, options);
    public Task<bool> TryExportPngAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => CircularExportAsync(host => host.TryExportPngAsync(destination, options, cancellationToken), cancellationToken);
    public Task<bool> TryExportPngAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => CircularExportAsync(host => host.TryExportPngAsync(path, options, cancellationToken), cancellationToken);
    public Task<bool> TryExportJpegAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => CircularExportAsync(host => host.TryExportJpegAsync(destination, options, cancellationToken), cancellationToken);
    public Task<bool> TryExportJpegAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => CircularExportAsync(host => host.TryExportJpegAsync(path, options, cancellationToken), cancellationToken);
    private async Task<bool> CircularExportAsync(Func<CartesianChart, Task<bool>> export, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pending = Dispatcher.UIThread.CheckAccess() ? export(ExportHost()) :
            await Dispatcher.UIThread.InvokeAsync(() => export(ExportHost()), DispatcherPriority.Normal, cancellationToken).GetTask().ConfigureAwait(false);
        return await pending.ConfigureAwait(false);
    }
}
