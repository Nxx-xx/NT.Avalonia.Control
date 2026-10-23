using System.Text;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Interop;

namespace PdfViewer.Core.Tests;

/// <summary>
/// P3-01 random-access and staging loads: file loads must not buffer the whole file in managed
/// memory, non-seekable inputs stage to an owned temp file, and the external-change / handle
/// release policies stay explicit (snapshot at open, clean errors after truncation).
/// </summary>
public sealed class RandomAccessLoadTests
{
    [Fact]
    public async Task File_load_matches_memory_load_including_cjk_paths()
    {
        var bytes = PdfFixture.FitDestinationsDocument();
        var path = Path.Combine(Path.GetTempPath(), "随机访问-读取-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            using var fromFile = await PdfDocument.LoadFromFileAsync(path, cancellationToken: TestContext.Current.CancellationToken);
            using var fromMemory = await PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(fromMemory.Pages.Count, fromFile.Pages.Count);
            for (var i = 0; i < fromMemory.Pages.Count; i++)
            {
                Assert.Equal(fromMemory.Pages[i].Width, fromFile.Pages[i].Width, 3);
                Assert.Equal(fromMemory.Pages[i].Height, fromFile.Pages[i].Height, 3);
            }
            Assert.Equal(fromMemory.PageLabels, fromFile.PageLabels);
            Assert.Equal(fromMemory.Bookmarks.Count, fromFile.Bookmarks.Count);
            var textToken = TestContext.Current.CancellationToken;
            for (var i = 0; i < fromMemory.Pages.Count; i++)
                Assert.Equal((await fromMemory.GetTextAsync(i, cancellationToken: textToken)).Text,
                    (await fromFile.GetTextAsync(i, cancellationToken: textToken)).Text);
            Assert.Null(fromFile.StagedTempPath);
            Assert.True(fromFile.InputBytesServed > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task File_load_serves_unreferenced_bulk_on_demand_only()
    {
        // The padding stream is not referenced by any page, so a random-access load must skip
        // it: a regression back to whole-file buffering would serve (or copy) all of it.
        const int paddingBytes = 16 * 1024 * 1024;
        var bytes = PdfFixture.PaddedDocument(paddingBytes);
        var path = Path.Combine(Path.GetTempPath(), "nt-pdf-padded-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            using var document = await PdfDocument.LoadFromFileAsync(path, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Single(document.Pages);
            Assert.True(document.InputBytesServed < bytes.Length / 4,
                $"random access served {document.InputBytesServed} of {bytes.Length} bytes");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Non_seekable_stream_is_staged_to_an_owned_temp_file_and_deleted_on_dispose()
    {
        var bytes = PdfFixture.ReadingDocument();
        await using var source = new NonSeekableStream(bytes);
        var document = await PdfDocument.LoadAsync(source, leaveOpen: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(source.WasRead);
        var staged = document.StagedTempPath;
        Assert.NotNull(staged);
        Assert.True(File.Exists(staged), "staged temp file exists while the document is open");
        Assert.Contains("Hello PDF reader", (await document.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken)).Text);

        await document.DisposeAsync();
        Assert.False(File.Exists(staged), "staged temp file is deleted with the document");
    }

    [Fact]
    public async Task Non_seekable_empty_stream_reports_the_same_empty_document_error()
    {
        await using var source = new NonSeekableStream([]);
        var error = await Assert.ThrowsAsync<PdfDocumentException>(() =>
            PdfDocument.LoadAsync(source, leaveOpen: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(PdfViewerStrings.ErrorEmptyDocument, error.Message);
    }

    [Fact]
    public async Task External_truncation_after_load_surfaces_errors_instead_of_mixed_content()
    {
        var path = Path.Combine(Path.GetTempPath(), "nt-pdf-truncate-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(path, PdfFixture.ManyPages(30), TestContext.Current.CancellationToken);
            using var document = await PdfDocument.LoadFromFileAsync(path, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(30, document.Pages.Count);

            // Snapshot policy: external truncation must fail reads cleanly. The late pages were
            // never referenced, so their data still lives in the file beyond the new length. A
            // failed block read aborts PDFium's render path natively, so the input zero-fills the
            // gap and the document layer surfaces the change as a managed error instead of
            // returning blank or mixed content.
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                file.SetLength(2048);

            var renderError = await Assert.ThrowsAsync<PdfDocumentException>(() =>
                document.RenderAsync(29, 200, 200, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(PdfViewerStrings.ErrorFileChanged, renderError.Message);
            Assert.Equal(PdfErrorKind.FileAccess, renderError.Kind);
            Assert.True(document.InputReadGaps > 0, "truncation must be recorded as a read gap");

            // The poisoned snapshot sticks: even pages whose bytes still exist refuse to render
            // text or pixels instead of silently mixing cached and replacement data.
            var textError = await Assert.ThrowsAsync<PdfDocumentException>(() =>
                document.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(PdfViewerStrings.ErrorFileChanged, textError.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task File_load_reports_missing_empty_and_unaddressable_inputs()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            PdfDocument.LoadFromFileAsync(Path.Combine(Path.GetTempPath(), "nt-pdf-missing-" + Guid.NewGuid().ToString("N") + ".pdf"),
                cancellationToken: TestContext.Current.CancellationToken));

        var path = Path.Combine(Path.GetTempPath(), "nt-pdf-empty-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(path, [], TestContext.Current.CancellationToken);
            var error = await Assert.ThrowsAsync<PdfDocumentException>(() =>
                PdfDocument.LoadFromFileAsync(path, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(PdfViewerStrings.ErrorEmptyDocument, error.Message);
        }
        finally
        {
            File.Delete(path);
        }

        // The addressable length is 32-bit on Windows (C unsigned long) and 64-bit on LP64.
        PdfFileInput.EnsureAddressableLength(0);
        PdfFileInput.EnsureAddressableLength(PdfFileInput.MaxAddressableLength);
        if (PdfFileInput.MaxAddressableLength < long.MaxValue)
        {
            var tooLarge = Assert.Throws<PdfDocumentException>(() => PdfFileInput.EnsureAddressableLength(PdfFileInput.MaxAddressableLength + 1));
            Assert.Equal(PdfViewerStrings.ErrorFileTooLargeForRandomAccess, tooLarge.Message);
        }
        Assert.Throws<PdfDocumentException>(() => PdfFileInput.EnsureAddressableLength(-1));
    }

    [Fact]
    public async Task File_load_with_password_keeps_the_same_error_contract_as_memory_load()
    {
        var bytes = PdfFixture.PasswordProtected();
        var path = Path.Combine(Path.GetTempPath(), "nt-pdf-password-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            var fromFile = await Assert.ThrowsAsync<PdfDocumentException>(() =>
                PdfDocument.LoadFromFileAsync(path, cancellationToken: TestContext.Current.CancellationToken));
            var fromMemory = await Assert.ThrowsAsync<PdfDocumentException>(() =>
                PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(fromMemory.Kind, fromFile.Kind);
            Assert.Equal(PdfErrorKind.PasswordRequiredOrIncorrect, fromFile.Kind);

            using var unlocked = await PdfDocument.LoadFromFileAsync(path, "reader", TestContext.Current.CancellationToken);
            Assert.Single(unlocked.Pages);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class NonSeekableStream(byte[] payload) : Stream
    {
        private int _position;
        internal bool WasRead { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            WasRead = true;
            var read = Math.Min(count, payload.Length - _position);
            Array.Copy(payload, _position, buffer, offset, read);
            _position += read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WasRead = true;
            var read = Math.Min(buffer.Length, payload.Length - _position);
            payload.AsSpan(_position, read).CopyTo(buffer.Span);
            _position += read;
            return ValueTask.FromResult(read);
        }
    }
}
