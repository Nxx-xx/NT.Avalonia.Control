using Avalonia.Controls;
using Avalonia.Threading;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Interop;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

public sealed class P1IntegrationTests
{
    [Fact]
    public Task Reentrant_source_binding_and_legacy_load_error_preserve_the_failed_source_and_replacement_state() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = new Reader();
        var failedSource = PdfDocumentSource.FromBytes("not a PDF"u8.ToArray(), "Failed source A");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementSource = PdfDocumentSource.FromStreamFactory(async token =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return new MemoryStream(PdfFixture.SinglePage("Replacement B"));
        }, "Replacement source B");
        Task? replacement = null;
        PdfDocumentLoadFailedEventArgs? failure = null;
        var sourceBindingReentered = false;
        reader.PropertyChanged += (_, change) =>
        {
            if (change.Property == Reader.DocumentSourceProperty && ReferenceEquals(reader.DocumentSource, failedSource))
            {
                // A host binding can replace the displayed source while the explicit
                // LoadDocumentAsync(A) call is still publishing its initial source.
                sourceBindingReentered = true;
                reader.DocumentSource = replacementSource;
            }
        };
        reader.LoadError += (_, _) => replacement = reader.LoadDocumentAsync(replacementSource,
            cancellationToken: TestContext.Current.CancellationToken);
        reader.DocumentLoadFailed += (_, args) => failure = args;
        try
        {
            var error = await Assert.ThrowsAsync<PdfDocumentException>(() => reader.LoadDocumentAsync(failedSource,
                cancellationToken: TestContext.Current.CancellationToken));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.NotNull(replacement);
            Assert.NotNull(failure);
            Assert.True(sourceBindingReentered);
            Assert.Same(failedSource, failure.Source);
            Assert.Same(error, failure.Error.Exception);
            Assert.Same(replacementSource, reader.DocumentSource);
            Assert.True(reader.IsLoading);
            Assert.Equal(PdfViewerDocumentState.Loading, reader.DocumentState);
            Assert.False(reader.HasDocument);
            Assert.Null(reader.LastError);
            release.SetResult();
            await replacement.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(PdfViewerDocumentState.Ready, reader.DocumentState);
            Assert.True(reader.HasDocument);
            Assert.False(reader.IsLoading);
            Assert.Equal(1, reader.PageCount);
            Assert.Equal("Replacement source B", reader.DocumentName);
            Assert.Same(replacementSource, reader.DocumentSource);
            Assert.Null(reader.LastError);
        }
        finally { release.TrySetResult(); }
    });

    [Fact]
    public Task Reentrant_loading_dispose_waits_for_the_registered_load() => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader();
        Task? disposed = null;
        reader.DocumentStateChanged += (_, state) =>
        {
            if (state.State != PdfViewerDocumentState.Loading) return;
            disposed = reader.DisposeAsync().AsTask();
            Assert.False(disposed.IsCompleted);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.LoadDocumentAsync(
            PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotNull(disposed);
        await disposed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(PdfViewerDocumentState.Disposed, reader.DocumentState);
        Assert.False(reader.IsLoading);
    });

    [Fact]
    public Task Page_count_handler_can_close_a_loading_document_without_stale_publication() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = new Reader();
        reader.PropertyChanged += (_, change) =>
        {
            if (change.Property == Reader.PageCountProperty && reader.PageCount > 0) reader.CloseDocument();
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.LoadDocumentAsync(
            PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken));
        await reader.CloseDocumentAsync();
        Assert.False(reader.HasDocument);
        Assert.Equal(0, reader.PageCount);
        Assert.Null(reader.LastError);
    });

    [Fact]
    public Task Empty_state_handler_can_open_the_next_document_without_the_old_close_clearing_it() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = new Reader();
        await reader.LoadDocumentAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        Task? next = null;
        reader.DocumentStateChanged += (_, state) =>
        {
            if (state.State == PdfViewerDocumentState.Empty && next is null)
                next = reader.LoadDocumentAsync(PdfFixture.SinglePage(), cancellationToken: TestContext.Current.CancellationToken);
        };
        reader.CloseDocument();
        Assert.NotNull(next);
        await next;
        Assert.True(reader.HasDocument);
        Assert.Equal(1, reader.PageCount);
        Assert.Equal(PdfViewerDocumentState.Ready, reader.DocumentState);
    });

    [Fact]
    public Task Close_and_dispose_do_not_block_the_dispatcher_behind_a_busy_native_worker() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = new Reader();
        await reader.LoadDocumentAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = PdfiumRuntime.RunAsync(() =>
        {
            entered.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken))
                throw new TimeoutException("The test did not release its native worker.");
            return 0;
        }, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            var closed = reader.CloseDocumentAsync();
            Assert.False(reader.HasDocument);
            Assert.False(closed.IsCompleted);
            var heartbeat = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.UIThread.Post(() => heartbeat.SetResult());
            await heartbeat.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var disposed = reader.DisposeAsync().AsTask();
            Assert.False(disposed.IsCompleted);
            Assert.Equal(PdfViewerDocumentState.Disposed, reader.DocumentState);
            release.Set();
            await blocker;
            await closed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await disposed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally { release.Set(); await blocker; }
    });

    [Fact]
    public Task Dispose_async_waits_for_a_noncooperative_source_without_blocking_the_dispatcher() => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = PdfDocumentSource.FromStreamFactory(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return new MemoryStream(PdfFixture.ReadingDocument());
        });
        var loading = reader.LoadDocumentAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            var disposed = reader.DisposeAsync().AsTask();
            Assert.False(disposed.IsCompleted);
            var heartbeat = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.UIThread.Post(() => heartbeat.SetResult());
            await heartbeat.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            release.SetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading);
            await disposed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.False(reader.HasDocument);
        }
        finally { release.TrySetResult(); reader.Dispose(); }
    });

    [Fact]
    public Task Injected_picker_is_optional_and_its_late_result_cannot_replace_a_newer_load() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = new Reader();
        var selected = new TaskCompletionSource<PdfDocumentSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogs = new Dialogs(selected.Task);
        reader.FileDialogService = dialogs;
        var opening = reader.OpenDocumentAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, dialogs.Opens);
        await reader.LoadDocumentAsync(PdfFixture.SinglePage(), cancellationToken: TestContext.Current.CancellationToken);
        var session = reader.DocumentSessionId;
        selected.SetResult(PdfDocumentSource.FromBytes(PdfFixture.ReadingDocument()));
        await opening;
        Assert.Equal(1, reader.PageCount);
        Assert.Equal(session, reader.DocumentSessionId);
        dialogs.CanOpen = false;
        await reader.OpenDocumentAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, dialogs.Opens);
        Assert.True(reader.Capabilities.CanRead);
        Assert.False(reader.Capabilities.CanPrint);
        Assert.False(reader.Capabilities.CanShare);
    });

    [Fact]
    public Task Ancestor_visibility_suspends_work_and_resumes_without_reloading_the_document() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = new Reader { IsSidebarVisible = false };
        var panel = new Border { Child = reader };
        var window = new Window { Width = 800, Height = 600, Content = panel };
        window.Show();
        try
        {
            await reader.LoadDocumentAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
            HeadlessTestScope.Drain();
            await reader.WaitForIdleAsync(TestContext.Current.CancellationToken);
            var session = reader.DocumentSessionId;
            Assert.True(reader.IsPresentationActive);
            panel.IsVisible = false;
            HeadlessTestScope.Drain();
            Assert.False(reader.IsPresentationActive);
            var count = reader.RenderedPageCount;
            reader.ZoomIn();
            HeadlessTestScope.Drain();
            Assert.Equal(count, reader.RenderedPageCount);
            panel.IsVisible = true;
            HeadlessTestScope.Drain();
            await reader.WaitForIdleAsync(TestContext.Current.CancellationToken);
            Assert.True(reader.IsPresentationActive);
            Assert.Equal(session, reader.DocumentSessionId);
            Assert.True(reader.CachedPageCount > 0);
        }
        finally { window.Close(); }
    });

    [Fact]
    public async Task Host_document_snapshot_owns_bytes_and_exposes_only_nonwritable_independent_streams()
    {
        var bytes = PdfFixture.SinglePage();
        var expected = (byte[])bytes.Clone();
        var snapshot = new PdfPlatformDocument(bytes, "Host document");
        Array.Fill(bytes, (byte)0);
        await using var first = snapshot.OpenRead();
        await using var second = snapshot.OpenRead();
        Assert.False(first.CanWrite);
        Assert.Equal(expected.LongLength, snapshot.Length);
        Assert.Throws<UnauthorizedAccessException>(() => ((MemoryStream)first).GetBuffer());
        Assert.Equal(expected[0], first.ReadByte());
        Assert.Equal(0, second.Position);
        using var output = new MemoryStream();
        await second.CopyToAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(expected, output.ToArray());
    }

    private sealed class Dialogs(Task<PdfDocumentSource?> selected) : IPdfFileDialogService
    {
        public bool CanOpen { get; set; } = true;
        public bool CanSave => false;
        public int Opens { get; private set; }
        public Task<PdfDocumentSource?> OpenAsync(CancellationToken cancellationToken = default) { Opens++; return selected; }
        public Task<string?> GetSavePathAsync(string suggestedFileName, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
