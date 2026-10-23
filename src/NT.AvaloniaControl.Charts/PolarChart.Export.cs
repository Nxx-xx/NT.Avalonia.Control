using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

public partial class PolarChart
{
    private CartesianChart? _exportHost;
    public event EventHandler<ChartExportFailedEventArgs>? ExportFailed;
    private CartesianChart ExportHost()
    {
        VerifyAccess();
        if (_exportHost is null)
        {
            _exportHost = new CartesianChart { ShowLegend = false, ShowGridLines = false };
            _exportHost.ExportRenderOverride = RenderPolarExport;
            _exportHost.ExportFailed += (_, e) => ExportFailed?.Invoke(this, e);
        }
        _exportHost.Measure(Bounds.Size); _exportHost.Arrange(new Rect(Bounds.Size)); return _exportHost;
    }
    private void RenderPolarExport(DrawingContext context, ChartExportLayout layout, IBrush? background)
    {
        using var clip = context.PushClip(new Rect(layout.Dips));
        if (background is not null) context.DrawRectangle(background, null, new Rect(layout.Dips));
        using var transform = context.PushTransform(new Matrix(layout.Scale, 0, 0, layout.Scale, layout.Offset.X, layout.Offset.Y));
        Render(context);
    }
    internal ChartExportLayout GetExportLayout(ChartExportOptions? options) => ExportHost().GetExportLayout(options);
    internal void RenderExport(DrawingContext context, ChartExportLayout layout, IBrush? background) => RenderPolarExport(context, layout, background);
    public bool TryExportPng(Stream destination, ChartExportOptions? options = null) => ExportHost().TryExportPng(destination, options);
    public bool TryExportPng(string path, ChartExportOptions? options = null) => ExportHost().TryExportPng(path, options);
    public bool TryExportJpeg(Stream destination, ChartExportOptions? options = null) => ExportHost().TryExportJpeg(destination, options);
    public bool TryExportJpeg(string path, ChartExportOptions? options = null) => ExportHost().TryExportJpeg(path, options);
    public Task<bool> TryExportPngAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => ExportAsync(host => host.TryExportPngAsync(destination, options, cancellationToken), cancellationToken);
    public Task<bool> TryExportPngAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => ExportAsync(host => host.TryExportPngAsync(path, options, cancellationToken), cancellationToken);
    public Task<bool> TryExportJpegAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => ExportAsync(host => host.TryExportJpegAsync(destination, options, cancellationToken), cancellationToken);
    public Task<bool> TryExportJpegAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => ExportAsync(host => host.TryExportJpegAsync(path, options, cancellationToken), cancellationToken);
    private async Task<bool> ExportAsync(Func<CartesianChart, Task<bool>> export, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pending = Dispatcher.UIThread.CheckAccess() ? export(ExportHost()) : await Dispatcher.UIThread.InvokeAsync(() => export(ExportHost()), DispatcherPriority.Normal, cancellationToken).GetTask().ConfigureAwait(false);
        return await pending.ConfigureAwait(false);
    }
}
