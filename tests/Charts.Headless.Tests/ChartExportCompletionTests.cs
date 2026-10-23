using System.Buffers.Binary;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartExportCompletionTests
{
    // Independently generated using Pillow 11/libjpeg: RGB(48,128,224), 1x1, quality90.
    private const string Fixture = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDnKKKK/qM/n8//2Q==";

    [Fact]
    public void Independent_fixture_identity_is_fixed()
    {
        var bytes = Convert.FromBase64String(Fixture);
        Assert.Equal(634, bytes.Length);
        Assert.Equal("C4C29B2E97C240AB47387D9B710A335373EE04D9538A39FC23A8DFC2BAD8057E", Convert.ToHexString(SHA256.HashData(bytes)));
    }

    [Theory]
    [InlineData(.1, 1)]
    [InlineData(96, 96)]
    [InlineData(144, 144)]
    [InlineData(192, 192)]
    [InlineData(150.12345, 150)]
    [InlineData(9600, 9600)]
    public void Jpeg_density_uses_compatible_Jfif_and_fractional_Exif_without_changing_entropy(double dpi, int jfifDpi)
    {
        var original = Convert.FromBase64String(Fixture); using var output = new MemoryStream();
        CartesianChart.WriteJpegWithResolution(original, output, dpi); var bytes = output.ToArray();
        var segments = Segments(bytes);
        var jfif = Assert.Single(segments, segment => segment.Marker == 0xE0 && segment.Payload.AsSpan().StartsWith("JFIF\0"u8)).Payload;
        Assert.Equal(1, jfif[7]); Assert.Equal(jfifDpi, BinaryPrimitives.ReadUInt16BigEndian(jfif.AsSpan(8))); Assert.Equal(jfifDpi, BinaryPrimitives.ReadUInt16BigEndian(jfif.AsSpan(10)));
        var exif = Assert.Single(segments, segment => segment.Marker == 0xE1 && segment.Payload.AsSpan().StartsWith("Exif\0\0"u8)).Payload;
        var density = ReadExif(exif); Assert.Equal(dpi, density.X, 5); Assert.Equal(dpi, density.Y, 5); Assert.Equal(2, density.Unit);
        Assert.Equal(Entropy(original), Entropy(bytes));
        foreach (var marker in new byte[] { 0xDB, 0xC0, 0xC4 })
            Assert.Equal(Segments(original).Where(part => part.Marker == marker).SelectMany(part => part.Payload), segments.Where(part => part.Marker == marker).SelectMany(part => part.Payload));
        Assert.True(output.CanWrite);
    }

    [Fact]
    public void Replacing_jpeg_resolution_keeps_one_Jfif_and_Exif_and_unchanged_scan()
    {
        using var first = new MemoryStream(); CartesianChart.WriteJpegWithResolution(Convert.FromBase64String(Fixture), first, 144);
        using var second = new MemoryStream(); CartesianChart.WriteJpegWithResolution(first.ToArray(), second, 75.25);
        var segments = Segments(second.ToArray());
        Assert.Single(segments, part => part.Marker == 0xE0 && part.Payload.AsSpan().StartsWith("JFIF\0"u8));
        var exif = Assert.Single(segments, part => part.Marker == 0xE1 && part.Payload.AsSpan().StartsWith("Exif\0\0"u8));
        Assert.Equal(75.25, ReadExif(exif.Payload).X, 5); Assert.Equal(Entropy(first.ToArray()), Entropy(second.ToArray()));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(9601)]
    public void Invalid_jpeg_density_is_rejected_before_destination_write(double dpi)
    {
        using var output = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => CartesianChart.WriteJpegWithResolution(Convert.FromBase64String(Fixture), output, dpi));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void Malformed_jpeg_is_rejected_without_touching_existing_stream_bytes()
    {
        var valid = Convert.FromBase64String(Fixture); var invalidLength = valid.ToArray(); invalidLength[4] = 0xFF; invalidLength[5] = 0xFF;
        foreach (var data in new[] { Array.Empty<byte>(), new byte[64], valid[..^2], invalidLength })
        {
            using var output = new MemoryStream(); output.Write(new byte[] { 7, 8, 9 });
            Assert.Throws<InvalidDataException>(() => CartesianChart.WriteJpegWithResolution(data, output, 96));
            Assert.Equal(new byte[] { 7, 8, 9 }, output.ToArray()); Assert.True(output.CanWrite);
        }
    }

    [AvaloniaTheory]
    [InlineData(false, 0)]
    [InlineData(false, 101)]
    [InlineData(true, 0)]
    [InlineData(true, 101)]
    public async Task Jpeg_quality_is_validated_before_capture_and_raises_one_Ui_failure(bool asynchronous, int quality)
    {
        using var host = new Host(); using var stream = new MemoryStream(); var failures = new List<ChartExportFailedEventArgs>(); var onUi = true;
        host.Chart.ExportFailed += (_, args) => { failures.Add(args); onUi &= Dispatcher.UIThread.CheckAccess(); };
        var result = asynchronous ? await host.Chart.TryExportJpegAsync(stream, new() { JpegQuality = quality }) : host.Chart.TryExportJpeg(stream, new() { JpegQuality = quality });
        Assert.False(result); Assert.IsType<ArgumentOutOfRangeException>(Assert.Single(failures).Exception); Assert.Null(failures[0].Path);
        Assert.True(onUi); Assert.Equal(0, stream.Length); Assert.True(stream.CanWrite);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Precancelled_stream_export_writes_nothing_reports_no_failure_and_does_not_capture(bool jpeg)
    {
        using var host = new Host(); using var stream = new MemoryStream(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var failures = 0; var drawings = 0; host.Chart.ExportFailed += (_, _) => failures++;
        host.Chart.Annotations.Add(new CustomAnnotation { Renderer = (_, _) => drawings++ });
        host.Detach(); // A normal compositor frame must not be mistaken for export capture.
        var options = new ChartExportOptions { PixelSize = new(0, 0) };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Export(host.Chart, stream, options, jpeg, cancellation.Token));
        Assert.Equal(0, failures); Assert.Equal(0, drawings); Assert.Equal(0, stream.Length); Assert.True(stream.CanWrite);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled_background_dispatch_does_not_render_the_queued_snapshot(bool jpeg)
    {
        using var host = new Host(); using var stream = new MemoryStream(); using var cancellation = new CancellationTokenSource(); using var queued = new ManualResetEventSlim();
        var drawings = 0; var failures = 0; host.Chart.Annotations.Add(new CustomAnnotation { Renderer = (_, _) => drawings++ }); host.Chart.ExportFailed += (_, _) => failures++;
        host.Detach(); // Keep the renderer counter exclusive to the queued offline export.
        Task<bool>? operation = null;
        var caller = Task.Run(() => { operation = Export(host.Chart, stream, new() { PixelSize = new(64, 64) }, jpeg, cancellation.Token); queued.Set(); });
        // Keep the UI dispatcher in this turn until the worker has queued capture.
        Assert.True(queued.Wait(TimeSpan.FromSeconds(5))); cancellation.Cancel(); await caller;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation!);
        Assert.Equal(0, drawings); Assert.Equal(0, failures); Assert.Equal(0, stream.Length);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Background_argument_failure_is_reported_once_on_Ui_and_releases_the_lease(bool jpeg)
    {
        using var host = new Host(); using var stream = new MemoryStream(); var failures = new List<ChartExportFailedEventArgs>(); var onUi = true;
        host.Chart.ExportFailed += (_, args) => { failures.Add(args); onUi &= Dispatcher.UIThread.CheckAccess(); };
        Assert.False(await Task.Run(() => Export(host.Chart, stream, new() { PixelSize = new(0, 10) }, jpeg)));
        Assert.IsType<ArgumentOutOfRangeException>(Assert.Single(failures).Exception); Assert.True(onUi);
        failures.Clear();
        Assert.False(await Export(host.Chart, stream, new() { PixelSize = new(32_769, 1) }, jpeg));
        Assert.IsType<ArgumentOutOfRangeException>(Assert.Single(failures).Exception); Assert.Equal(0, stream.Length);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unwritable_stream_remains_caller_owned_and_causes_one_failure(bool jpeg)
    {
        using var host = new Host(); using var stream = new MemoryStream([2, 3], writable: false); ChartExportFailedEventArgs? failure = null; var count = 0;
        host.Chart.ExportFailed += (_, args) => { count++; failure = args; };
        Assert.False(await Export(host.Chart, stream, new(), jpeg)); Assert.Equal(1, count);
        Assert.IsType<ArgumentException>(failure!.Exception); Assert.Null(failure.Path); Assert.True(stream.CanRead); Assert.Equal(2, stream.Length);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled_file_export_preserves_existing_file_and_creates_no_temporary_file(bool jpeg)
    {
        using var host = new Host(); using var directory = new TempDirectory(); var path = Path.Combine(directory.Path, "existing.image"); byte[] sentinel = [11, 12, 13]; File.WriteAllBytes(path, sentinel);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var failures = 0; host.Chart.ExportFailed += (_, _) => failures++;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => jpeg ? host.Chart.TryExportJpegAsync(path, cancellationToken: cancellation.Token) : host.Chart.TryExportPngAsync(path, cancellationToken: cancellation.Token));
        Assert.Equal(sentinel, File.ReadAllBytes(path)); Assert.Single(Directory.GetFiles(directory.Path)); Assert.Equal(0, failures);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task File_validation_failure_preserves_destination_and_reports_original_path(bool jpeg)
    {
        using var host = new Host(); using var directory = new TempDirectory(); var path = Path.Combine(directory.Path, "existing.image"); byte[] sentinel = [11, 12, 13]; File.WriteAllBytes(path, sentinel);
        var errors = new List<ChartExportFailedEventArgs>(); host.Chart.ExportFailed += (_, error) => errors.Add(error);
        var options = new ChartExportOptions { Dpi = double.NaN };
        Assert.False(await (jpeg ? host.Chart.TryExportJpegAsync(path, options) : host.Chart.TryExportPngAsync(path, options)));
        Assert.Equal(path, Assert.Single(errors).Path); Assert.Equal(sentinel, File.ReadAllBytes(path)); Assert.Single(Directory.GetFiles(directory.Path));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_subscriber_exception_propagates_but_does_not_strand_export_lease(bool asynchronous)
    {
        using var host = new Host(); using var stream = new MemoryStream(); var expected = new HandlerException();
        void Handler(object? sender, ChartExportFailedEventArgs args) => throw expected;
        host.Chart.ExportFailed += Handler;
        Assert.Same(expected, await Assert.ThrowsAsync<HandlerException>(() => asynchronous ? host.Chart.TryExportJpegAsync(stream, new() { JpegQuality = 0 }) : Task.FromResult(host.Chart.TryExportJpeg(stream, new() { JpegQuality = 0 }))));
        host.Chart.ExportFailed -= Handler; ChartExportFailedEventArgs? failure = null; host.Chart.ExportFailed += (_, args) => failure = args;
        Assert.False(await host.Chart.TryExportJpegAsync(stream, new() { JpegQuality = 101 })); Assert.IsType<ArgumentOutOfRangeException>(failure!.Exception);
    }

    [AvaloniaFact]
    public void Failure_callback_reentry_is_rejected_without_recursively_reporting_or_releasing_the_outer_lease()
    {
        using var host = new Host(); using var stream = new MemoryStream(); var events = 0; var innerAccepted = true;
        host.Chart.ExportFailed += (_, _) => { events++; innerAccepted = host.Chart.TryExportPng(stream); };
        Assert.False(host.Chart.TryExportJpeg(stream, new() { JpegQuality = 0 })); Assert.False(innerAccepted); Assert.Equal(1, events); Assert.Equal(0, stream.Length);
    }

    private static Task<bool> Export(CartesianChart chart, Stream stream, ChartExportOptions options, bool jpeg, CancellationToken cancellationToken = default)
        => jpeg ? chart.TryExportJpegAsync(stream, options, cancellationToken) : chart.TryExportPngAsync(stream, options, cancellationToken);

    private static List<(byte Marker, byte[] Payload)> Segments(byte[] bytes)
    {
        var segments = new List<(byte, byte[])>();
        for (var index = 2; index + 3 < bytes.Length;)
        {
            Assert.Equal(0xFF, bytes[index]); var marker = bytes[index + 1]; if (marker == 0xDA) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(index + 2));
            segments.Add((marker, bytes.AsSpan(index + 4, length - 2).ToArray())); index += length + 2;
        }
        return segments;
    }
    private static byte[] Entropy(byte[] bytes)
    {
        for (var index = 2; index + 3 < bytes.Length;)
        {
            if (bytes[index + 1] == 0xDA) return bytes[index..];
            index += BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(index + 2)) + 2;
        }
        throw new InvalidDataException("Fixture has no scan");
    }
    private static (double X, double Y, int Unit) ReadExif(byte[] payload)
    {
        var data = payload.AsSpan(6).ToArray(); Assert.Equal("II", System.Text.Encoding.ASCII.GetString(data, 0, 2));
        var directory = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4))); var entries = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(directory));
        double x = 0, y = 0; var unit = 0;
        for (var index = 0; index < entries; index++)
        {
            var entry = data.AsSpan(directory + 2 + index * 12, 12); var tag = BinaryPrimitives.ReadUInt16LittleEndian(entry);
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]));
            if (tag == 0x0128) unit = BinaryPrimitives.ReadUInt16LittleEndian(entry[8..]);
            if (tag is not (0x011A or 0x011B)) continue;
            Assert.Equal(5, BinaryPrimitives.ReadUInt16LittleEndian(entry[2..])); var location = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]));
            var value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(location)) / (double)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(location + 4));
            if (tag == 0x011A) x = value; else y = value;
        }
        return (x, y, unit);
    }
    private sealed class HandlerException : Exception { }
    private sealed class Host : IDisposable
    {
        public CartesianChart Chart { get; } = new() { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        private Window Window { get; }
        public Host() { Chart.Series.Add(new LineSeries { ItemsSource = new ChartPoint[] { new(1, 20), new(9, 80) } }); Window = new Window { Width = 640, Height = 360, Content = Chart }; Window.Show(); Window.UpdateLayout(); }
        public void Detach() { Window.Content = null; Window.UpdateLayout(); }
        public void Dispose() => Window.Close();
    }
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "charts-c208-" + Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { foreach (var file in Directory.GetFiles(Path)) File.Delete(file); Directory.Delete(Path); }
    }
}
