using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public abstract partial class AnalysisChartBase
{
        private ChartRasterExporter Exporter => _exporter ??= new(this, Render, e => ExportFailed?.Invoke(this, e));
    public event EventHandler<ChartExportFailedEventArgs>? ExportFailed;
    internal ChartExportLayout GetExportLayout(ChartExportOptions? options) => Exporter.GetLayout(options);
    internal void RenderExport(DrawingContext context, ChartExportLayout layout, IBrush? background) => Exporter.Render(context, layout, background);
    public bool TryExportPng(Stream destination, ChartExportOptions? options = null) => Exporter.TryExportPng(destination, options);
    public bool TryExportPng(string path, ChartExportOptions? options = null) => Exporter.TryExportPng(path, options);
    public bool TryExportJpeg(Stream destination, ChartExportOptions? options = null) => Exporter.TryExportJpeg(destination, options);
    public bool TryExportJpeg(string path, ChartExportOptions? options = null) => Exporter.TryExportJpeg(path, options);
    public Task<bool> TryExportPngAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => Exporter.TryExportPngAsync(destination, options, cancellationToken);
    public Task<bool> TryExportPngAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => Exporter.TryExportPngAsync(path, options, cancellationToken);
    public Task<bool> TryExportJpegAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => Exporter.TryExportJpegAsync(destination, options, cancellationToken);
    public Task<bool> TryExportJpegAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default) => Exporter.TryExportJpegAsync(path, options, cancellationToken);
}
