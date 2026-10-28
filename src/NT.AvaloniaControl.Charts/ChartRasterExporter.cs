using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>Shares the existing encoder and its limits with compact chart projections.</summary>
internal sealed class ChartRasterExporter(ChartBase owner, Action<DrawingContext> render, Action<ChartExportFailedEventArgs> onFailed, Action? prepare = null, Action? finished = null)
{
    private CartesianChart? _host;

    private CartesianChart Host()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_host is null)
        {
            _host = new CartesianChart { ShowLegend = false, ShowGridLines = false };
            _host.ExportRenderOverride = Render;
            _host.ExportPrepareOverride = prepare;
            _host.ExportCleanupOverride = finished;
            _host.ExportFailed += (_, args) => onFailed(args);
        }
        _host.Measure(owner.Bounds.Size);
        _host.Arrange(new Rect(owner.Bounds.Size));
        return _host;
    }

    internal ChartExportLayout GetLayout(ChartExportOptions? options) => Host().GetExportLayout(options);

    internal void Render(DrawingContext context, ChartExportLayout layout, IBrush? background)
    {
        using var clip = context.PushClip(new Rect(layout.Dips));
        if (background is not null) context.DrawRectangle(background, null, new Rect(layout.Dips));
        using var transform = context.PushTransform(new Matrix(layout.Scale, 0, 0, layout.Scale, layout.Offset.X, layout.Offset.Y));
        render(context);
    }

    internal bool TryExportPng(Stream destination, ChartExportOptions? options = null) => Host().TryExportPng(destination, options);
    internal bool TryExportPng(string path, ChartExportOptions? options = null) => Host().TryExportPng(path, options);
    internal bool TryExportJpeg(Stream destination, ChartExportOptions? options = null) => Host().TryExportJpeg(destination, options);
    internal bool TryExportJpeg(string path, ChartExportOptions? options = null) => Host().TryExportJpeg(path, options);
    internal Task<bool> TryExportPngAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => ExportAsync(host => host.TryExportPngAsync(destination, options, cancellationToken), cancellationToken);
    internal Task<bool> TryExportPngAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => ExportAsync(host => host.TryExportPngAsync(path, options, cancellationToken), cancellationToken);
    internal Task<bool> TryExportJpegAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => ExportAsync(host => host.TryExportJpegAsync(destination, options, cancellationToken), cancellationToken);
    internal Task<bool> TryExportJpegAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => ExportAsync(host => host.TryExportJpegAsync(path, options, cancellationToken), cancellationToken);

    private async Task<bool> ExportAsync(Func<CartesianChart, Task<bool>> export, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pending = Dispatcher.UIThread.CheckAccess()
            ? export(Host())
            : await Dispatcher.UIThread.InvokeAsync(() => export(Host()), DispatcherPriority.Normal, cancellationToken).GetTask().ConfigureAwait(false);
        return await pending.ConfigureAwait(false);
    }
}
