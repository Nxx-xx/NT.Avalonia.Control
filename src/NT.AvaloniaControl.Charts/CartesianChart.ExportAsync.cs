using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using StbImageWriteSharp;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    /// <summary>Writes JPEG on the UI thread. Transparent canvas areas are flattened over white. Leaves the caller's stream open.</summary>
    public bool TryExportJpeg(Stream destination, ChartExportOptions? options = null)
    {
        VerifyAccess();
        var reserved = false;
        try
        {
            ReserveExport(); reserved = true;
            ValidateExportStream(destination);
            var capture = CaptureExportPixels(options, jpeg: true);
            using var encoded = EncodeExport(capture, CancellationToken.None);
            encoded.Position = 0;
            encoded.CopyTo(destination);
            return true;
        }
        catch (Exception exception) { ReportExportFailure(exception, null); return false; }
        finally { if (reserved) _exportInProgress = false; }
    }

    /// <summary>Writes JPEG through a same-directory temporary file. Failure preserves an existing destination.</summary>
    public bool TryExportJpeg(string path, ChartExportOptions? options = null)
    {
        VerifyAccess();
        var reserved = false;
        string? temporary = null;
        try
        {
            ReserveExport(); reserved = true;
            var destination = ExportDestination(path);
            var capture = CaptureExportPixels(options, jpeg: true);
            using var encoded = EncodeExport(capture, CancellationToken.None);
            temporary = ExportTemporaryPath(destination);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                encoded.Position = 0;
                encoded.CopyTo(stream);
            }
            File.Move(temporary, destination, overwrite: true);
            temporary = null;
            return true;
        }
        catch (Exception exception) { ReportExportFailure(exception, path); return false; }
        finally
        {
            DeleteExportTemporary(temporary);
            if (reserved) _exportInProgress = false;
        }
    }

    /// <summary>Captures on the UI thread, then encodes and writes on a worker. Cancellation throws without ExportFailed. Leaves the stream open; a write failure/cancellation may leave partial output.</summary>
    public Task<bool> TryExportPngAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default)
        => ExportAsync(destination, null, options, jpeg: false, cancellationToken);

    /// <summary>Captures on the UI thread, then encodes and writes on a worker. Cancellation throws without ExportFailed. Failure/cancellation preserves an existing file.</summary>
    public Task<bool> TryExportPngAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default)
        => ExportAsync(null, path, options, jpeg: false, cancellationToken, file: true);

    /// <summary>Captures on the UI thread, then encodes and writes on a worker. Flattens transparency over white. Cancellation throws without ExportFailed; the caller's stream remains open and may contain partial output.</summary>
    public Task<bool> TryExportJpegAsync(Stream destination, ChartExportOptions? options = null, CancellationToken cancellationToken = default)
        => ExportAsync(destination, null, options, jpeg: true, cancellationToken);

    /// <summary>Captures on the UI thread, then encodes and writes on a worker. Failure/cancellation preserves an existing file. Cancellation throws without ExportFailed.</summary>
    public Task<bool> TryExportJpegAsync(string path, ChartExportOptions? options = null, CancellationToken cancellationToken = default)
        => ExportAsync(null, path, options, jpeg: true, cancellationToken, file: true);

    private sealed record ExportCapture(byte[] Rgba, ChartExportLayout Layout, bool Jpeg, int Quality);

    private async Task<bool> ExportAsync(Stream? stream, string? path, ChartExportOptions? options, bool jpeg,
        CancellationToken cancellationToken, bool file = false)
    {
        var reserved = false;
        string? destination = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var capture = await OnExportUiThread(() =>
            {
                // Check again after a background caller's dispatch has reached the UI.
                cancellationToken.ThrowIfCancellationRequested();
                ReserveExport(); reserved = true;
                if (file) destination = ExportDestination(path!);
                else ValidateExportStream(stream!);
                return CaptureExportPixels(options, jpeg, cancellationToken);
            }, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            // No controls, mutable brushes or platform bitmap handles cross this boundary.
            await Task.Run(async () =>
            {
                using var encoded = EncodeExport(capture, cancellationToken);
                if (!file)
                {
                    encoded.Position = 0;
                    await CopyExportAsync(encoded, stream!, cancellationToken).ConfigureAwait(false);
                    return;
                }
                string? temporary = ExportTemporaryPath(destination!);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                        FileShare.None, 65_536, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        encoded.Position = 0;
                        await CopyExportAsync(encoded, output, cancellationToken).ConfigureAwait(false);
                        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    // This is the commit point. Cancellation arriving after the move cannot roll it back.
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Move(temporary, destination!, overwrite: true);
                    temporary = null;
                }
                finally { DeleteExportTemporary(temporary); }
            }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            await OnExportUiThread(() => { ReportExportFailure(exception, file ? path : null); return true; }).ConfigureAwait(false);
            return false;
        }
        finally
        {
            if (reserved)
                await OnExportUiThread(() => { _exportInProgress = false; return true; }).ConfigureAwait(false);
        }
    }

    private static Task<T> OnExportUiThread<T>(Func<T> action, CancellationToken cancellationToken = default)
        => Dispatcher.UIThread.CheckAccess() ? Task.FromResult(action()) :
            Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken).GetTask();

    private static void ValidateExportStream(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("The export stream must be writable.", nameof(destination));
    }

    private static string ExportDestination(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path);
    }

    private static string ExportTemporaryPath(string destination)
        => Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

    private static void DeleteExportTemporary(string? temporary)
    {
        if (temporary is not null)
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private ExportCapture CaptureExportPixels(ChartExportOptions? options, bool jpeg, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        options ??= new();
        var layout = GetExportLayout(options);
        if (jpeg && options.JpegQuality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(options), "JpegQuality must be between 1 and 100.");
        try
        {
            if (!_attached) { SynchronizeSubscriptions(); InvalidateScene(); }
            PreparePresentationForExport(Bounds.Size);
            using var bitmap = new RenderTargetBitmap(layout.Pixels, new Vector(96, 96));
            using (var drawing = bitmap.CreateDrawingContext())
            {
                if (jpeg) drawing.DrawRectangle(Brushes.White, null, new Rect(layout.Dips));
                RenderExport(drawing, layout, options.Background, options.UseSampling, cancellationToken);
            }
            var rgba = new byte[checked(layout.Pixels.Width * layout.Pixels.Height * 4)];
            var pinned = GCHandle.Alloc(rgba, GCHandleType.Pinned);
            try
            {
                using var framebuffer = new LockedFramebuffer(pinned.AddrOfPinnedObject(), layout.Pixels,
                    checked(layout.Pixels.Width * 4), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul, null!);
                bitmap.CopyPixels(framebuffer);
            }
            finally { pinned.Free(); }
            return new(rgba, layout, jpeg, options.JpegQuality);
        }
        finally
        {
            ExportCleanupOverride?.Invoke();
            if (!_attached)
            {
                ClearPlotAreaContent();
                ClearForegroundPresentation();
                LegendPresentationDetached();
            }
        }
    }

    private static ExportBuffer EncodeExport(ExportCapture capture, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var compressed = new ExportBuffer(capture.Layout.MaximumEncodedBytes);
        using var codecOutput = new CodecOutputStream(compressed);
        var writer = new ImageWriter();
        // The codec call is a bounded, indivisible stage. Do not throw from its callback:
        // upstream PNG compression releases its temporary allocation after that callback.
        if (capture.Jpeg)
            writer.WriteJpg(capture.Rgba, capture.Layout.Pixels.Width, capture.Layout.Pixels.Height,
                ColorComponents.RedGreenBlueAlpha, codecOutput, capture.Quality);
        else
            writer.WritePng(capture.Rgba, capture.Layout.Pixels.Width, capture.Layout.Pixels.Height,
                ColorComponents.RedGreenBlueAlpha, codecOutput);
        codecOutput.ThrowIfFailed();
        cancellationToken.ThrowIfCancellationRequested();
        var output = new ExportBuffer(capture.Layout.MaximumEncodedBytes);
        try
        {
            var bytes = compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length));
            if (capture.Jpeg) WriteJpegWithResolution(bytes, output, capture.Layout.Dpi);
            else WritePngWithResolution(bytes, output, capture.Layout.Dpi);
            cancellationToken.ThrowIfCancellationRequested();
            return output;
        }
        catch { output.Dispose(); throw; }
    }

    private static async Task CopyExportAsync(ExportBuffer source, Stream destination, CancellationToken cancellationToken)
    {
        var remaining = source.GetBuffer().AsMemory(0, checked((int)source.Length));
        while (!remaining.IsEmpty)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(65_536, remaining.Length);
            await destination.WriteAsync(remaining[..count], cancellationToken).ConfigureAwait(false);
            remaining = remaining[count..];
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    // stb frees its native scratch storage after its final write callback. Preserve
    // that cleanup even if the bounded sink fails; report the first error after return.
    private sealed class CodecOutputStream(Stream output) : Stream
    {
        private Exception? _failure;
        internal void ThrowIfFailed() { if (_failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_failure).Throw(); }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_failure is not null) return;
            try { output.Write(buffer, offset, count); }
            catch (Exception exception) { _failure = exception; }
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_failure is not null) return;
            try { output.Write(buffer); }
            catch (Exception exception) { _failure = exception; }
        }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    // JPEG has no alpha. Capture already flattened in pixel space; metadata does not
    // change those pixels. EXIF rational preserves five fractional DPI digits while
    // JFIF supplies a rounded whole-DPI value for older readers.
    internal static void WriteJpegWithResolution(ReadOnlySpan<byte> jpeg, Stream destination, double dpi)
    {
        if (!double.IsFinite(dpi) || dpi < .1 || dpi > 9_600) throw new ArgumentOutOfRangeException(nameof(dpi));
        if (jpeg.Length < 6 || jpeg[0] != 0xFF || jpeg[1] != 0xD8 || jpeg[^2] != 0xFF || jpeg[^1] != 0xD9)
            throw new InvalidDataException("The encoder did not produce a complete JPEG.");
        // Validate every header before touching caller output.
        var scanOffset = -1;
        for (var offset = 2; offset < jpeg.Length - 2;)
        {
            if (jpeg[offset] != 0xFF || jpeg.Length - offset < 4 || jpeg[offset + 1] is 0 or 0xFF or 0xD8 or 0xD9)
                throw new InvalidDataException("Invalid JPEG marker.");
            var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.Slice(offset + 2, 2));
            if (length < 2 || length > jpeg.Length - offset - 2) throw new InvalidDataException("Invalid JPEG segment length.");
            if (jpeg[offset + 1] == 0xDA) { if (length < 6) throw new InvalidDataException("Invalid JPEG scan header."); scanOffset = offset; break; }
            offset += length + 2;
        }
        if (scanOffset < 0) throw new InvalidDataException("JPEG is missing image scan data.");
        Span<byte> metadata = stackalloc byte[94];
        metadata.Clear();
        ReadOnlySpan<byte> jfif = [0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 2, 1, 0, 0, 0, 0, 0, 0];
        jfif.CopyTo(metadata);
        var density = (ushort)Math.Clamp(Math.Round(dpi, MidpointRounding.AwayFromZero), 1, ushort.MaxValue);
        BinaryPrimitives.WriteUInt16BigEndian(metadata[12..], density);
        BinaryPrimitives.WriteUInt16BigEndian(metadata[14..], density);
        var exif = metadata[18..];
        exif[0] = 0xFF; exif[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(exif[2..], 74);
        "Exif\0\0"u8.CopyTo(exif[4..]);
        var tiff = exif[10..];
        tiff[0] = (byte)'I'; tiff[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[2..], 42);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff[4..], 8);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[8..], 3);
        WriteExifEntry(tiff[10..], 0x011A, 5, 50);
        WriteExifEntry(tiff[22..], 0x011B, 5, 58);
        WriteExifEntry(tiff[34..], 0x0128, 3, 2);
        var numerator = checked((uint)Math.Round(dpi * 100_000, MidpointRounding.AwayFromZero));
        foreach (var offset in new[] { 50, 58 })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(tiff[offset..], numerator);
            BinaryPrimitives.WriteUInt32LittleEndian(tiff[(offset + 4)..], 100_000);
        }
        destination.Write(jpeg[..2]);
        destination.Write(metadata);
        for (var offset = 2; offset < scanOffset;)
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.Slice(offset + 2, 2)) + 2;
            var payload = jpeg.Slice(offset + 4, length - 4);
            var replace = jpeg[offset + 1] == 0xE0 && payload.StartsWith("JFIF\0"u8) ||
                jpeg[offset + 1] == 0xE1 && payload.StartsWith("Exif\0\0"u8);
            if (!replace) destination.Write(jpeg.Slice(offset, length));
            offset += length;
        }
        destination.Write(jpeg[scanOffset..]);
    }

    private static void WriteExifEntry(Span<byte> entry, ushort tag, ushort type, uint value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(entry, tag);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], type);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], value);
    }
}
