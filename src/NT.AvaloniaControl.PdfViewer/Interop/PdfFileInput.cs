using System.Buffers;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NT.AvaloniaControl.PdfViewer.Interop;

/// <summary>
/// Random-access input for <c>FPDF_LoadCustomDocument</c>: PDFium reads file blocks on demand
/// through GetBlock, so no managed copy of the whole file is ever made. The instance owns the
/// file handle, the native FPDF_FILEACCESS block and the GetBlock delegate, and must stay alive
/// until after <c>FPDF_CloseDocument</c>.
/// External-change policy: the file is opened for shared read/write/delete and its length is
/// snapshotted at open as <c>m_FileLen</c>; later growth is ignored. When the file is truncated
/// or replaced under us, GetBlock still satisfies the full request (real bytes, zero tail) and
/// records a gap — a failed block read aborts PDFium's render path at a native CHECK — and the
/// document layer turns the recorded gap into a managed error instead of returning mixed content.
/// Staged copies of non-addressable streams live in a process-unique temp file that is deleted
/// on dispose (delete-on-close on the read handle, plus best-effort delete as backstop).
/// </summary>
internal sealed class PdfFileInput : IDisposable
{
    // PDFium block requests are bounded in practice; serve them in modest segments so a large
    // request never needs a single huge managed buffer.
    private const int MaxSegmentBytes = 1 << 20;

    private static readonly PdfiumNative.GetBlockCallback GetBlockCore = GetBlock;

    private SafeFileHandle? _handle;
    private readonly GCHandle _self;
    private readonly long _length;
    private readonly string? _stagedPath;
    private IntPtr _nativeAccess;
    private long _bytesServed;
    private long _readGaps;

    private PdfFileInput(SafeFileHandle handle, long length, string? stagedPath)
    {
        _handle = handle;
        _length = length;
        _stagedPath = stagedPath;
        _self = GCHandle.Alloc(this);
        try
        {
            _nativeAccess = Marshal.AllocHGlobal(Marshal.SizeOf<PdfiumNative.FpdfFileAccess>());
            var access = new PdfiumNative.FpdfFileAccess
            {
                FileLen = new CULong((nuint)length),
                GetBlock = Marshal.GetFunctionPointerForDelegate(GetBlockCore),
                Param = GCHandle.ToIntPtr(_self),
            };
            Marshal.StructureToPtr(access, _nativeAccess, false);
        }
        catch
        {
            _self.Free();
            throw;
        }
    }

    /// <summary>The native FPDF_FILEACCESS block; valid while this instance is alive.</summary>
    internal IntPtr NativeAccess => _nativeAccess;

    /// <summary>Snapshot of the input length taken at open; the value PDFium sees as m_FileLen.</summary>
    internal long Length => _length;

    /// <summary>Path of the owned staged temp copy, or null for direct file inputs.</summary>
    internal string? StagedPath => _stagedPath;

    /// <summary>Total bytes GetBlock has served; random access keeps this far below Length.</summary>
    internal long BytesServed => Interlocked.Read(ref _bytesServed);

    internal byte[] ReadSnapshot(CancellationToken token)
    {
        if (_length > 256L * 1024 * 1024) throw new NotSupportedException("Editing input exceeds 256 MiB.");
        var handle = _handle ?? throw new ObjectDisposedException(nameof(PdfFileInput));
        var bytes = GC.AllocateUninitializedArray<byte>((int)_length);
        var offset = 0;
        while (offset < bytes.Length)
        {
            token.ThrowIfCancellationRequested();
            var read = RandomAccess.Read(handle, bytes.AsSpan(offset, Math.Min(MaxSegmentBytes, bytes.Length - offset)), offset);
            if (read == 0) throw new IOException("The PDF input changed while preparing editing.");
            offset += read;
        }
        return bytes;
    }

    /// <summary>
    /// Number of block requests that had to be zero-filled because the file was truncated or
    /// became unreadable after open. Non-zero means the snapshot no longer matches the bytes
    /// PDFium parsed, so the document layer must refuse to return content instead of mixing
    /// versions.
    /// </summary>
    internal long ReadGaps => Interlocked.Read(ref _readGaps);

    /// <summary>
    /// Largest length a C `unsigned long` m_FileLen can address on this platform: 32-bit on
    /// Windows (LLP64), 64-bit on the supported Linux and macOS targets (LP64).
    /// </summary>
    internal static long MaxAddressableLength => OperatingSystem.IsWindows() ? uint.MaxValue : long.MaxValue;

    internal static void EnsureAddressableLength(long length)
    {
        if (length < 0 || length > MaxAddressableLength)
            throw new PdfDocumentException(PdfViewerStrings.ErrorFileTooLargeForRandomAccess, 3);
    }

    /// <summary>Opens a local file for random access. The file is not copied.</summary>
    internal static PdfFileInput OpenFile(string path, string? stagedPath = null)
    {
        var length = new FileInfo(path).Length;
        EnsureAddressableLength(length);
        if (length == 0)
            throw new PdfDocumentException(PdfViewerStrings.ErrorEmptyDocument, 3);
        // Shared read/write/delete keeps the input non-hostile to other applications; the
        // snapshot semantics above cover external edits. RandomAccess enables position-free
        // block reads from GetBlock. Staged copies add delete-on-close so the temp file cannot
        // outlive the handle.
        var options = FileOptions.RandomAccess | (stagedPath is null ? FileOptions.None : FileOptions.DeleteOnClose);
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, options);
        return new PdfFileInput(handle, length, stagedPath);
    }

    /// <summary>
    /// Copies a non-addressable stream to an owned temp file and opens it for random access.
    /// The caller keeps ownership of the source stream. The temp file is deleted when the
    /// returned input is disposed; a failure before that deletes it immediately.
    /// </summary>
    internal static async Task<PdfFileInput> StageStreamAsync(Stream source, CancellationToken cancellationToken)
    {
        var stagedPath = Path.Combine(Path.GetTempPath(), "nt-pdf-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var staging = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await source.CopyToAsync(staging, cancellationToken).ConfigureAwait(false);
            }
            return OpenFile(stagedPath, stagedPath);
        }
        catch
        {
            try { File.Delete(stagedPath); } catch { /* best effort; the name is process-unique */ }
            throw;
        }
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, null);
        try
        {
            handle?.Dispose();
        }
        finally
        {
            if (_stagedPath is not null)
            {
                // Delete-on-close removed the name already; this covers replaced read handles.
                try { File.Delete(_stagedPath); } catch { /* best effort */ }
            }
            if (_self.IsAllocated)
                _self.Free();
            var nativeAccess = Interlocked.Exchange(ref _nativeAccess, IntPtr.Zero);
            if (nativeAccess != IntPtr.Zero)
                Marshal.FreeHGlobal(nativeAccess);
        }
    }

    // Never throws across the native boundary. Every in-range request is satisfied in full:
    // a failed block read aborts PDFium's render path at a native CHECK, so an unreadable tail
    // is zero-filled and recorded as a gap instead, and the document layer surfaces the gap as
    // a managed error. Only structurally invalid requests fail with a zero return.
    private static int GetBlock(IntPtr param, CULong position, IntPtr buffer, CULong size)
    {
        if (param == IntPtr.Zero || buffer == IntPtr.Zero)
            return 0;
        try
        {
            if (GCHandle.FromIntPtr(param).Target is not PdfFileInput input)
                return 0;
            var offset = (long)position.Value;
            var remaining = (long)size.Value;
            if (offset < 0 || remaining < 0 || offset > input._length || remaining > input._length - offset)
                return 0;
            if (remaining == 0)
                return 1;

            var filled = ServeReadable(input, offset, buffer, remaining);
            if (filled < remaining)
            {
                ZeroFill(AddOffset(buffer, filled), remaining - filled);
                Interlocked.Increment(ref input._readGaps);
            }
            if (filled > 0)
                Interlocked.Add(ref input._bytesServed, filled);
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Copies the readable prefix of the request and returns how many real bytes were served.</summary>
    private static long ServeReadable(PdfFileInput input, long offset, IntPtr buffer, long remaining)
    {
        var handle = Volatile.Read(ref input._handle);
        if (handle is null || handle.IsClosed)
            return 0;
        var segment = ArrayPool<byte>.Shared.Rent((int)Math.Min(remaining, MaxSegmentBytes));
        try
        {
            var written = 0L;
            while (written < remaining)
            {
                var chunk = (int)Math.Min(remaining - written, segment.Length);
                int read;
                try
                {
                    read = RandomAccess.Read(handle, segment.AsSpan(0, chunk), offset + written);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or ArgumentException)
                {
                    break;
                }
                if (read <= 0)
                    break;
                Marshal.Copy(segment, 0, AddOffset(buffer, written), read);
                written += read;
            }
            return written;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(segment);
        }
    }

    private static void ZeroFill(IntPtr buffer, long count)
    {
        var zeros = ArrayPool<byte>.Shared.Rent((int)Math.Min(count, MaxSegmentBytes));
        try
        {
            Array.Clear(zeros, 0, zeros.Length);
            var written = 0L;
            while (written < count)
            {
                var chunk = (int)Math.Min(count - written, zeros.Length);
                Marshal.Copy(zeros, 0, AddOffset(buffer, written), chunk);
                written += chunk;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(zeros);
        }
    }

    private static IntPtr AddOffset(IntPtr pointer, long offset) => (IntPtr)((long)pointer + offset);
}
