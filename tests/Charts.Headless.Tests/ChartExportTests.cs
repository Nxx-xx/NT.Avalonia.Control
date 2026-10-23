using System.Buffers.Binary;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartExportTests
{
    [AvaloniaFact]
    public void Export_geometry_uses_current_layout_uniform_scale_pixel_size_and_dpi()
    {
        var chart = ArrangeChart();
        var layout = chart.GetExportLayout(new() { PixelSize = new(960, 540), Dpi = 144 });
        Assert.Equal(new PixelSize(960, 540), layout.Pixels);
        Assert.Equal(new Size(960, 540), layout.Dips);
        Assert.Equal(.9, layout.Scale, 10);
        Assert.Equal(new Vector(120, 0), layout.Offset);
        var inferred = chart.GetExportLayout(new() { Dpi = 144 });
        Assert.Equal(new PixelSize(1200, 900), inferred.Pixels);
        Assert.Equal(1.5, inferred.Scale, 10);
        foreach (var dpi in new[] { 96d, 192d })
        {
            var fixedPixels = chart.GetExportLayout(new() { PixelSize = new(960, 540), Dpi = dpi });
            Assert.Equal(layout.Dips, fixedPixels.Dips);
            Assert.Equal(layout.Scale, fixedPixels.Scale);
            Assert.Equal(layout.Offset, fixedPixels.Offset);
        }
    }

    [AvaloniaFact]
    public void Export_drawing_does_not_rearrange_chart_or_change_the_viewport()
    {
        var chart = ArrangeChart();
        chart.Viewport = new(2, 8, 1, 9);
        var beforeBounds = chart.Bounds;
        var beforePlot = chart.PlotArea;
        var beforeView = chart.VisibleRange;
        var layout = chart.GetExportLayout(new() { PixelSize = new(960, 540), Dpi = 144 });
        var group = new DrawingGroup();
        using (var drawing = group.Open()) chart.RenderExport(drawing, layout, Brushes.White);
        Assert.Equal(beforeBounds, chart.Bounds);
        Assert.Equal(beforePlot, chart.PlotArea);
        Assert.Equal(beforeView, chart.VisibleRange);
        Assert.Equal(new ChartViewport(2, 8, 1, 9), chart.Viewport);
        // DrawingGroup.GetBounds unions geometry without intersecting ClipGeometry.
        // Inspect the final recording contract instead of treating raw text overhang as pixels.
        var target = Assert.IsType<DrawingGroup>(Assert.Single(group.Children));
        Assert.Equal(new Rect(layout.Dips), Assert.IsType<RectangleGeometry>(target.ClipGeometry).Rect);
        var backdrop = Assert.IsType<GeometryDrawing>(target.Children[0]);
        Assert.Same(Brushes.White, backdrop.Brush);
        Assert.Equal(new Rect(layout.Dips), backdrop.GetBounds());
        var fitted = Assert.Single(target.Children.OfType<DrawingGroup>());
        Assert.Equal(new Matrix(.9, 0, 0, .9, 120, 0), fitted.Transform?.Value);
        var content = Assert.IsType<DrawingGroup>(Assert.Single(fitted.Children));
        Assert.Equal(new Rect(beforeBounds.Size), Assert.IsType<RectangleGeometry>(content.ClipGeometry).Rect);
    }

    [AvaloniaTheory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(10000d)]
    public void Invalid_resolution_reports_one_failure_without_writing_or_closing_stream(double dpi)
    {
        var chart = ArrangeChart();
        using var stream = new MemoryStream();
        var errors = new List<ChartExportFailedEventArgs>();
        chart.ExportFailed += (_, error) => errors.Add(error);
        Assert.False(chart.TryExportPng(stream, new() { Dpi = dpi }));
        var failure = Assert.Single(errors);
        Assert.IsType<ArgumentOutOfRangeException>(failure.Exception);
        Assert.Null(failure.Path);
        Assert.True(stream.CanWrite);
        Assert.Equal(0, stream.Length);
    }

    [AvaloniaFact]
    public void Pixel_budget_is_validated_before_allocating_a_bitmap()
    {
        var chart = ArrangeChart();
        foreach (var options in new ChartExportOptions[]
        {
            new() { PixelSize = new(0, 1) },
            new() { PixelSize = new(32769, 1) },
            new() { PixelSize = new(8192, 8192) },
            new() { PixelSize = new(100, 100), MaximumPixelCount = 9999 },
            new() { MaximumPixelCount = 0 },
            new() { MaximumPixelCount = 67_108_865 },
        })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => chart.GetExportLayout(options));
        }
        Assert.Throws<InvalidOperationException>(() => new CartesianChart().GetExportLayout(null));
    }

    [AvaloniaFact]
    public void File_validation_failure_preserves_existing_output_and_creates_no_temporary_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"charts-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "existing.png");
        byte[] original = [1, 2, 3, 4];
        try
        {
            File.WriteAllBytes(path, original);
            var chart = ArrangeChart();
            ChartExportFailedEventArgs? error = null;
            chart.ExportFailed += (_, args) => error = args;
            Assert.False(chart.TryExportPng(path, new() { PixelSize = new(32769, 1) }));
            Assert.Equal(path, error?.Path);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }

    [AvaloniaFact]
    public void Unwritable_stream_reports_a_failure_and_remains_caller_owned()
    {
        var chart = ArrangeChart();
        using var stream = new MemoryStream([1, 2], writable: false);
        var failures = 0;
        chart.ExportFailed += (_, _) => failures++;
        Assert.False(chart.TryExportPng(stream));
        Assert.Equal(1, failures);
        Assert.True(stream.CanRead);
        Assert.Equal(2, stream.Length);
    }

    [Fact]
    public void Png_resolution_is_written_once_and_replaced_without_changing_pixel_chunks()
    {
        var original = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jF4YAAAAASUVORK5CYII=");
        using var first = new MemoryStream();
        CartesianChart.WritePngWithResolution(original, first, 144);
        var chunks = Chunks(first.ToArray());
        var physical = Assert.Single(chunks, chunk => chunk.Type == "pHYs");
        Assert.Equal(5669u, BinaryPrimitives.ReadUInt32BigEndian(physical.Data));
        Assert.Equal(5669u, BinaryPrimitives.ReadUInt32BigEndian(physical.Data.AsSpan(4)));
        Assert.Equal(1, physical.Data[8]);
        // Standard CRC-32 for pHYs + big-endian (5669, 5669, unit=1), independently
        // calculated with zlib rather than repeating the production CRC implementation.
        Assert.Equal(0x4952_24F0u, physical.Crc);
        Assert.Equal(Chunks(original).Single(chunk => chunk.Type == "IDAT").Data, chunks.Single(chunk => chunk.Type == "IDAT").Data);
        Assert.Equal("pHYs", chunks[1].Type);
        using var second = new MemoryStream();
        CartesianChart.WritePngWithResolution(first.ToArray(), second, 96);
        physical = Assert.Single(Chunks(second.ToArray()), chunk => chunk.Type == "pHYs");
        Assert.Equal(3780u, BinaryPrimitives.ReadUInt32BigEndian(physical.Data));
        Assert.True(first.CanWrite && second.CanWrite);
    }

    [Fact]
    public void Invalid_backend_png_is_rejected_before_writing_any_destination_bytes()
    {
        using var destination = new MemoryStream();
        Assert.Throws<NotSupportedException>(() => CartesianChart.WritePngWithResolution(new byte[64], destination, 96));
        var truncated = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jF4YAAAAASUVORK5CYII=")[..^2];
        Assert.Throws<InvalidDataException>(() => CartesianChart.WritePngWithResolution(truncated, destination, 96));
        Assert.Equal(0, destination.Length);
    }

    private static CartesianChart ArrangeChart()
    {
        var chart = new CartesianChart
        {
            ShowLegend = false,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        };
        chart.Series.Add(new LineSeries { ItemsSource = new ChartPoint[] { new(0, 0), new(10, 10) } });
        chart.Measure(new Size(800, 600)); chart.Arrange(new Rect(0, 0, 800, 600));
        return chart;
    }

    private static List<(string Type, byte[] Data, uint Crc)> Chunks(byte[] png)
    {
        var result = new List<(string, byte[], uint)>();
        for (var offset = 8; offset < png.Length;)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset)));
            result.Add((System.Text.Encoding.ASCII.GetString(png, offset + 4, 4), png.AsSpan(offset + 8, length).ToArray(),
                BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + length))));
            offset += length + 12;
        }
        return result;
    }
}
