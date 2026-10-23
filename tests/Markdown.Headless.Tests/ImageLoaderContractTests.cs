using System.Net;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ImageLoaderContractTests
{
    [Fact]
    public Task HttpHeaderByteLimitRejectsBeforeDecodeAndBorrowedHttpClientRemainsUsable() => HeadlessTestScope.Run(() => HttpHeaderByteLimitRejectsBeforeDecodeAndBorrowedHttpClientRemainsUsableCore());

    private async Task HttpHeaderByteLimitRejectsBeforeDecodeAndBorrowedHttpClientRemainsUsableCore()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[16]) });
        using var client = new HttpClient(handler); var loader = new MarkdownImageLoader(client, maximumBytes: 4);
        await Assert.ThrowsAsync<IOException>(async () => await loader.LoadAsync(new("https://example.test/image"), TestContext.Current.CancellationToken));
        loader.Dispose();
        using var response = await client.GetAsync("https://example.test/after-dispose", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(2, handler.CallCount); Assert.False(handler.Disposed);
    }

    [Fact]
    public Task UnknownHttpContentLengthStillUsesBoundedReading() => HeadlessTestScope.Run(() => UnknownHttpContentLengthStillUsesBoundedReadingCore());

    private async Task UnknownHttpContentLengthStillUsesBoundedReadingCore()
    {
        using var stream = new NonSeekableStream(new byte[16]);
        using var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(stream) });
        using var client = new HttpClient(handler); using var loader = new MarkdownImageLoader(client, maximumBytes: 4);
        await Assert.ThrowsAsync<IOException>(async () => await loader.LoadAsync(new("https://example.test/image"), TestContext.Current.CancellationToken));
        Assert.True(stream.Disposed);
    }

    [Fact]
    public Task HttpFailureAndPreCancellationHaveExplicitResultsWithoutDecode() => HeadlessTestScope.Run(() => HttpFailureAndPreCancellationHaveExplicitResultsWithoutDecodeCore());

    private async Task HttpFailureAndPreCancellationHaveExplicitResultsWithoutDecodeCore()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.Forbidden)); using var client = new HttpClient(handler); using var loader = new MarkdownImageLoader(client);
        await Assert.ThrowsAsync<HttpRequestException>(async () => await loader.LoadAsync(new("https://example.test/forbidden"), TestContext.Current.CancellationToken));
        using var cancelled = new CancellationTokenSource(); await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await loader.LoadAsync(new("https://example.test/cancelled"), cancelled.Token));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public Task FileLoadingHasTheSameByteLimitAndClosesTheInputOnFailure() => HeadlessTestScope.Run(() => FileLoadingHasTheSameByteLimitAndClosesTheInputOnFailureCore());

    private async Task FileLoadingHasTheSameByteLimitAndClosesTheInputOnFailureCore()
    {
        var path = Path.Combine(Path.GetTempPath(), "markdown-image-limit-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(path, new byte[16], TestContext.Current.CancellationToken);
            using var loader = new MarkdownImageLoader(maximumBytes: 4);
            await Assert.ThrowsAsync<IOException>(async () => await loader.LoadAsync(new(path), TestContext.Current.CancellationToken));
            using var reopened = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(16, reopened.Length);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public Task AwaresResourceLoadingUsesTheSameBoundedReader() => HeadlessTestScope.Run(() => AwaresResourceLoadingUsesTheSameBoundedReaderCore());

    private async Task AwaresResourceLoadingUsesTheSameBoundedReaderCore()
    {
        using var loader = new MarkdownImageLoader(maximumBytes: 4);
        await Assert.ThrowsAsync<IOException>(async () => await loader.LoadAsync(new("avares://Markdown.Headless.Tests/Assets/image-limit.fixture.txt"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public Task InvalidLimitsAndUnsupportedSchemesAreRejectedExplicitly() => HeadlessTestScope.Run(() => InvalidLimitsAndUnsupportedSchemesAreRejectedExplicitlyCore());

    private async Task InvalidLimitsAndUnsupportedSchemesAreRejectedExplicitlyCore()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarkdownImageLoader(maximumBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarkdownImageLoader(maximumDecodedPixels: 0));
        using var loader = new MarkdownImageLoader();
        Assert.Equal(16 * 1024 * 1024, loader.MaximumBytes); Assert.Equal(32L * 1024 * 1024, loader.MaximumDecodedPixels);
        await Assert.ThrowsAsync<NotSupportedException>(async () => await loader.LoadAsync(new("ftp://example.test/image"), TestContext.Current.CancellationToken));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; } public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); CallCount++; return Task.FromResult(response(request)); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes); public bool Disposed { get; private set; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { Disposed = true; if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}

