using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Interop;
using PdfViewer.Tests;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

public sealed class ReadingRegressionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public Task Partial_xyz_preserves_unspecified_viewport_axes(bool hasLeft, bool hasTop) => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await Fixture.Open(PdfFixture.ManyPages(3));
        var reader = fixture.Reader;
        reader.ZoomMode = PdfZoomMode.Manual;
        reader.ZoomLevel = 3;
        await fixture.Idle();
        fixture.Scroll.Offset = new Vector(100, 250);
        await fixture.Idle();
        var before = fixture.Scroll.Offset;
        Assert.Equal(100, before.X, 1);
        Assert.Equal(250, before.Y, 1);

        reader.NavigateToDestination(new PdfLinkDestination(0, null, null, PdfDestinationFit.Xyz,
            Left: hasLeft ? 100 : null, Top: hasTop ? 100 : null));
        await fixture.Idle();
        if (!hasLeft) Assert.Equal(before.X, fixture.Scroll.Offset.X, 1);
        else Assert.NotEqual(before.X, fixture.Scroll.Offset.X);
        if (!hasTop) Assert.Equal(before.Y, fixture.Scroll.Offset.Y, 1);
        else Assert.NotEqual(before.Y, fixture.Scroll.Offset.Y);
    });

    [Theory]
    [InlineData(PdfDestinationFit.FitB)]
    [InlineData(PdfDestinationFit.FitBH)]
    [InlineData(PdfDestinationFit.FitBV)]
    public Task A_pending_content_fit_cannot_override_a_newer_page_request(PdfDestinationFit fit) => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await Fixture.Open(PdfFixture.ContentBoxDocument());
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = PdfiumRuntime.RunAsync(() =>
        {
            entered.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10), Token)) throw new TimeoutException();
            return true;
        }, Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
            fixture.Reader.NavigateToDestination(new PdfLinkDestination(0, null, null, fit));
            var pending = fixture.Reader.PendingContentFit;
            Assert.False(pending.IsCompleted);
            fixture.Reader.GoToPage(2);
            var zoom = fixture.Reader.ZoomLevel;
            Assert.Equal(2, fixture.Reader.CurrentPage);
            release.Set();
            await blocker;
            await pending.WaitAsync(TimeSpan.FromSeconds(10), Token);
            await fixture.Idle();
            Assert.Equal(2, fixture.Reader.CurrentPage);
            Assert.Equal(zoom, fixture.Reader.ZoomLevel);
            Assert.Null(fixture.Reader.LastError);
        }
        finally { release.Set(); await blocker; }
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task Stream_password_retries_use_the_captured_input_without_borrowing_the_callers_stream(bool seekable) => HeadlessTestScope.Run(async () =>
    {
        using var fixture = new Fixture();
        var bytes = PdfFixture.PasswordProtected();
        // Seekable input starts after a prefix; capture must use its current position.
        using var stream = new CountingStream(seekable ? [1, 2, 3, .. bytes] : bytes, seekable);
        if (seekable) stream.Position = 3;
        var error = await Assert.ThrowsAsync<PdfDocumentException>(() => fixture.Reader.LoadDocumentAsync(stream, cancellationToken: Token));
        Assert.Equal(PdfErrorKind.PasswordRequiredOrIncorrect, error.Kind);
        Assert.True(stream.CanRead, "The loader must leave the caller's stream open.");
        var reads = stream.ReadCalls;
        stream.Dispose(); // The retry belongs to the viewer, not the original stream.

        fixture.Reader.SubmitPassword("wrong");
        await Until(() => fixture.Reader.IsPasswordPromptVisible);
        Assert.Equal(PdfErrorKind.PasswordRequiredOrIncorrect, fixture.Reader.LastError!.Kind);
        Assert.Equal(reads, stream.ReadCalls);
        fixture.Reader.CancelPasswordPrompt();
        fixture.Reader.SubmitPassword("reader");
        await Until(() => fixture.Reader.HasDocument);
        await fixture.Idle();
        Assert.Equal(PdfViewerDocumentState.Ready, fixture.Reader.DocumentState);
        Assert.Null(fixture.Reader.LastError);
        Assert.Null(fixture.Reader.Source);
        Assert.False(fixture.Reader.IsPasswordPromptVisible);
        Assert.Equal(1, fixture.Reader.PageCount);
        Assert.Equal(reads, stream.ReadCalls);
    });

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            HeadlessTestScope.Drain();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal Reader Reader { get; } = new();
        private Window Window { get; }
        internal ScrollViewer Scroll => Reader.DocumentPresenter!.GetVisualAncestors().OfType<ScrollViewer>().First();
        internal Fixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }
        internal static async Task<Fixture> Open(byte[] bytes)
        {
            var fixture = new Fixture();
            try { await fixture.Reader.LoadDocumentAsync(bytes, cancellationToken: Token); await fixture.Idle(); return fixture; }
            catch { fixture.Dispose(); throw; }
        }
        internal async Task Idle()
        {
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
            await Reader.WaitForIdleAsync(Token);
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }
        public void Dispose() { Reader.Dispose(); Window.Close(); }
    }

    private sealed class CountingStream(byte[] bytes, bool seekable) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        internal int ReadCalls { get; private set; }
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => seekable;
        public override bool CanWrite => false;
        public override long Length => seekable ? _inner.Length : throw new NotSupportedException();
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { ReadCalls++; return _inner.ReadAsync(buffer, cancellationToken); }
        public override int Read(byte[] buffer, int offset, int count) { ReadCalls++; return _inner.Read(buffer, offset, count); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => seekable ? _inner.Seek(offset, origin) : throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
