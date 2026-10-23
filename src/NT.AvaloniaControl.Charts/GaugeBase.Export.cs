using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public abstract partial class GaugeBase
{
    private ChartRasterExporter? _rasterExporter;
    private ChartRasterExporter RasterExporter => _rasterExporter ??= new ChartRasterExporter(this, Render, e => ExportFailed?.Invoke(this, e));
    public event EventHandler<ChartExportFailedEventArgs>? ExportFailed;
    internal ChartExportLayout GetExportLayout(ChartExportOptions? options) => RasterExporter.GetLayout(options);
    internal void RenderExport(DrawingContext context, ChartExportLayout layout, IBrush? background) => RasterExporter.Render(context, layout, background);
    public bool TryExportPng(Stream destination, ChartExportOptions? options = null) => RasterExporter.TryExportPng(destination, options);
    public bool TryExportPng(string path, ChartExportOptions? options = null) => RasterExporter.TryExportPng(path, options);
    public bool TryExportJpeg(Stream destination, ChartExportOptions? options = null) => RasterExporter.TryExportJpeg(destination, options);
    public bool TryExportJpeg(string path, ChartExportOptions? options = null) => RasterExporter.TryExportJpeg(path, options);
    public Task<bool> TryExportPngAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => RasterExporter.TryExportPngAsync(destination, options, cancellationToken);
    public Task<bool> TryExportPngAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => RasterExporter.TryExportPngAsync(path, options, cancellationToken);
    public Task<bool> TryExportJpegAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => RasterExporter.TryExportJpegAsync(destination, options, cancellationToken);
    public Task<bool> TryExportJpegAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => RasterExporter.TryExportJpegAsync(path, options, cancellationToken);
}
