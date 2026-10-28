using NT.AvaloniaControl.PdfViewer;
using PdfViewer.Tests;

namespace PdfViewer.Core.Tests;

public sealed class StreamRetrySourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Staged_password_input_survives_retries_and_releases_without_invalidating_the_open_document()
    {
        using var input = new NonSeekableStream(PdfFixture.PasswordProtected());
        using var source = new PdfDocument.StreamSource(input);
        await Assert.ThrowsAsync<PdfDocumentException>(() => source.OpenAsync("wrong", Token));
        var path = source.StagedPath;
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.True(input.CanRead);
        input.Dispose();

        var document = await source.OpenAsync("reader", Token);
        try
        {
            source.Dispose();
            Assert.Null(source.StagedPath);
            var page = await document.RenderAsync(0, 128, 128, cancellationToken: Token);
            Assert.Equal(128, page.Width);
            Assert.Contains(page.Pixels, value => value != 255);
        }
        finally { await document.DisposeAsync(); }
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Abandoning_a_failed_password_source_deletes_its_staged_file_and_leaves_the_caller_open()
    {
        using var input = new NonSeekableStream(PdfFixture.PasswordProtected());
        using var source = new PdfDocument.StreamSource(input);
        await Assert.ThrowsAsync<PdfDocumentException>(() => source.OpenAsync(null, Token));
        var path = source.StagedPath;
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        source.Dispose();
        Assert.False(File.Exists(path));
        Assert.True(input.CanRead);
    }

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
