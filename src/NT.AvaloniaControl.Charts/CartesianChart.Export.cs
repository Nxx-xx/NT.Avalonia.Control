using System.Buffers.Binary;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace NT.AvaloniaControl.Charts;

/// <summary>Image output uses the current arranged chart layout, uniformly scaled and centered.</summary>
public sealed record ChartExportOptions
{
    /// <summary>Output device pixels; null derives pixels from Bounds and Dpi.</summary>
    public PixelSize? PixelSize { get; init; }
    /// <summary>Resolution for inferred output pixels and image metadata. Explicit PixelSize keeps the same raster appearance at every Dpi.</summary>
    public double Dpi { get; init; } = 96;
    /// <summary>Canvas backdrop; null leaves it transparent. The chart's own Background is still drawn.</summary>
    public IBrush? Background { get; init; }
    /// <summary>JPEG quality from 1 to 100. PNG ignores this setting.</summary>
    public int JpegQuality { get; init; } = 90;
    /// <summary>Opt in to eligible series sampling at the output pixel scale. False exports original line geometry.</summary>
    public bool UseSampling { get; init; }
    /// <summary>Per-export pixel budget; maximum accepted value is 67,108,864.</summary>
    public long MaximumPixelCount { get; init; } = 16_777_216;
}

public sealed class ChartExportFailedEventArgs(Exception exception, string? path) : EventArgs
{
    public Exception Exception { get; } = exception;
    /// <summary>Requested file path, or null for a caller-owned stream.</summary>
    public string? Path { get; } = path;
}

internal readonly record struct ChartExportLayout(PixelSize Pixels, Size Dips, double Scale, Vector Offset, double Dpi, long MaximumEncodedBytes);

public sealed partial class CartesianChart
{
    // Other projections share the bounded capture/codec/file pipeline through an
    // internal, detached export host. Ordinary Cartesian output never sets this.
    internal Action<DrawingContext, ChartExportLayout, IBrush?>? ExportRenderOverride { get; set; }
    private bool _exportInProgress;
    private bool _reportingExportFailure;
    public event EventHandler<ChartExportFailedEventArgs>? ExportFailed;

    /// <summary>Writes PNG on the UI thread. Leaves the stream open; a failed write may leave partial output.</summary>
    public bool TryExportPng(Stream destination, ChartExportOptions? options = null)
    {
        VerifyAccess();
        var reserved = false;
        try { ReserveExport(); reserved = true; WriteExportPng(destination, options); return true; }
        catch (Exception exception) { ReportExportFailure(exception, null); return false; }
        finally { if (reserved) _exportInProgress = false; }
    }

    /// <summary>Writes PNG through a same-directory temporary file. Failure preserves an existing destination.</summary>
    public bool TryExportPng(string path, ChartExportOptions? options = null)
    {
        VerifyAccess();
        string? temporary = null;
        var reserved = false;
        try
        {
            ReserveExport(); reserved = true;
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            // Validate before opening any output, including a temporary file.
            _ = GetExportLayout(options);
            var destination = Path.GetFullPath(path);
            temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                WriteExportPng(stream, options);
            File.Move(temporary, destination, overwrite: true);
            temporary = null;
            return true;
        }
        catch (Exception exception) { ReportExportFailure(exception, path); return false; }
        finally
        {
            if (temporary is not null)
                try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            if (reserved) _exportInProgress = false;
        }
    }

    internal ChartExportLayout GetExportLayout(ChartExportOptions? options)
    {
        options ??= new();
        if (!double.IsFinite(Bounds.Width) || !double.IsFinite(Bounds.Height) || Bounds.Width <= 0 || Bounds.Height <= 0)
            throw new InvalidOperationException("Arrange the chart at a finite positive size before exporting.");
        if (!double.IsFinite(options.Dpi) || options.Dpi < .1 || options.Dpi > 9_600)
            throw new ArgumentOutOfRangeException(nameof(options), "Dpi must be between 0.1 and 9600.");
        if (options.MaximumPixelCount <= 0 || options.MaximumPixelCount > 67_108_864)
            throw new ArgumentOutOfRangeException(nameof(options), "The pixel budget must be between 1 and 67108864.");
        var width = options.PixelSize is { } size ? size.Width : Math.Ceiling(Bounds.Width * (options.Dpi / 96));
        var height = options.PixelSize is { } other ? other.Height : Math.Ceiling(Bounds.Height * (options.Dpi / 96));
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 1 || height < 1 || width > 32_768 || height > 32_768 || width * height > options.MaximumPixelCount)
            throw new ArgumentOutOfRangeException(nameof(options), "Output dimensions exceed the export pixel/dimension budget.");
        var pixels = new PixelSize((int)width, (int)height);
        // Rasterize in a 96-DPI pixel coordinate space. Some backends' initial DPI
        // transform also participates in nested DrawingContext transforms; mixing that
        // transform with chart-fit scaling can enlarge and clip non-96-DPI exports.
        // Requested DPI still determines inferred pixels and the encoded pHYs chunk.
        var dips = new Size(width, height);
        var scale = Math.Min(dips.Width / Bounds.Width, dips.Height / Bounds.Height);
        if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        return new(pixels, dips, scale,
            new Vector((dips.Width - Bounds.Width * scale) / 2, (dips.Height - Bounds.Height * scale) / 2),
            options.Dpi, checked((long)width * (long)height * 4 + 1_048_576));
    }

    internal void RenderExport(DrawingContext context, ChartExportLayout layout, IBrush? background,
        bool useSampling = false, CancellationToken cancellationToken = default)
    {
        if (ExportRenderOverride is { } render) { render(context, layout, background); return; }
        using var backgroundLayout = BeginBackgroundLayoutExport();
        using var sampling = BeginSamplingExport(layout.Scale, useSampling, cancellationToken);
        using var targetClip = context.PushClip(new Rect(layout.Dips));
        if (background is not null) context.DrawRectangle(background, null, new Rect(layout.Dips));
        using var transform = context.PushTransform(new Matrix(layout.Scale, 0, 0, layout.Scale, layout.Offset.X, layout.Offset.Y));
        using var chartClip = context.PushClip(new Rect(Bounds.Size));
        Render(context);
        RenderPresentationForExport(context);
    }

    private void WriteExportPng(Stream destination, ChartExportOptions? options)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("The export stream must be writable.", nameof(destination));
        var layout = GetExportLayout(options);
        try
        {
            // Detached charts do not subscribe to mutable series/axis/annotation
            // sources. Rebuild from their current values for each offline export.
            if (!_attached) { SynchronizeSubscriptions(); InvalidateScene(); }
            PreparePresentationForExport(Bounds.Size);
            using var bitmap = new RenderTargetBitmap(layout.Pixels, new Vector(96, 96));
            using (var context = bitmap.CreateDrawingContext()) RenderExport(context, layout, options?.Background, options?.UseSampling ?? false);
            using var encoded = new ExportBuffer(layout.MaximumEncodedBytes);
            bitmap.Save(encoded);
            WritePngWithResolution(encoded.GetBuffer().AsSpan(0, checked((int)encoded.Length)), destination, layout.Dpi);
        }
        finally
        {
            // Offline export may temporarily create control-based plot content.
            // It must not reintroduce an owned subtree after visual detachment.
            if (!_attached)
            {
                ClearPlotAreaContent();
                ClearForegroundPresentation();
                LegendPresentationDetached();
            }
        }
    }

    private void ReserveExport()
    {
        VerifyAccess();
        if (_exportInProgress) throw new InvalidOperationException("Only one export may be in progress for a chart.");
        _exportInProgress = true;
    }

    private void ReportExportFailure(Exception exception, string? path)
    {
        if (_reportingExportFailure) return;
        _reportingExportFailure = true;
        try { ExportFailed?.Invoke(this, new(exception, path)); }
        finally { _reportingExportFailure = false; }
    }

    // The platform encoder owns pixel compression. Validate its PNG envelope and replace
    // resolution metadata explicitly: some backends omit it, and Bitmap.Dpi on decode may ignore it.
    internal static void WritePngWithResolution(ReadOnlySpan<byte> png, Stream destination, double dpi)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length < 45 || !png[..8].SequenceEqual(signature)) throw new NotSupportedException("The rendering backend did not produce PNG.");
        var offset = 8;
        var ended = false;
        while (offset < png.Length)
        {
            if (png.Length - offset < 12) throw new InvalidDataException("Incomplete PNG chunk.");
            var length = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(offset, 4));
            if (length > png.Length - offset - 12) throw new InvalidDataException("Invalid PNG chunk length.");
            var type = png.Slice(offset + 4, 4);
            if (offset == 8 && (!type.SequenceEqual("IHDR"u8) || length != 13)) throw new InvalidDataException("PNG is missing IHDR.");
            offset += checked((int)length + 12);
            if (type.SequenceEqual("IEND"u8)) { ended = length == 0 && offset == png.Length; break; }
        }
        if (!ended) throw new InvalidDataException("PNG is missing its final IEND.");
        Span<byte> resolution = stackalloc byte[21];
        BinaryPrimitives.WriteUInt32BigEndian(resolution, 9);
        "pHYs"u8.CopyTo(resolution[4..]);
        var pixelsPerMeter = checked((uint)Math.Round(dpi / .0254, MidpointRounding.AwayFromZero));
        BinaryPrimitives.WriteUInt32BigEndian(resolution[8..], pixelsPerMeter);
        BinaryPrimitives.WriteUInt32BigEndian(resolution[12..], pixelsPerMeter);
        resolution[16] = 1;
        var crc = uint.MaxValue;
        foreach (var value in resolution.Slice(4, 13))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB8_8320u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(resolution[17..], ~crc);
        destination.Write(signature);
        for (offset = 8; offset < png.Length;)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.Slice(offset, 4)) + 12);
            var type = png.Slice(offset + 4, 4);
            if (!type.SequenceEqual("pHYs"u8)) destination.Write(png.Slice(offset, length));
            if (offset == 8) destination.Write(resolution);
            offset += length;
        }
    }

    private sealed class ExportBuffer(long limit) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Position > limit - count) throw new IOException("Encoded image exceeds the export byte budget.");
            base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (Position > limit - buffer.Length) throw new IOException("Encoded image exceeds the export byte budget.");
            base.Write(buffer);
        }
        public override void WriteByte(byte value)
        {
            if (Position >= limit) throw new IOException("Encoded image exceeds the export byte budget.");
            base.WriteByte(value);
        }
        public override void SetLength(long value)
        {
            if (value > limit) throw new IOException("Encoded image exceeds the export byte budget.");
            base.SetLength(value);
        }
    }
}
