using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

public sealed class ContractViewerTests
{
    [Fact]
    public Task Bindable_state_and_commands_follow_load_navigation_close_and_dispose() => HeadlessTestScope.Run(async () =>
    {
        using var reader = new Reader();
        var states = new List<PdfDocumentStateChangedEventArgs>();
        reader.DocumentStateChanged += (_, args) => states.Add(args);
        var commandChanges = 0;
        reader.NextPageCommand.CanExecuteChanged += (_, _) => commandChanges++;
        var legacyLoaded = 0;
        reader.DocumentLoaded += (_, _) => legacyLoaded++;
        Assert.Equal(PdfViewerDocumentState.Empty, reader.DocumentState);
        Assert.False(reader.CopyCommand.CanExecute(null));
        Assert.False(reader.NextPageCommand.CanExecute(null));
        Assert.True(reader.OpenCommand.CanExecute(null));
        await reader.LoadDocumentAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(PdfViewerDocumentState.Ready, reader.DocumentState);
        Assert.NotNull(reader.DocumentSessionId);
        var session = reader.DocumentSessionId;
        Assert.True(reader.Capabilities.CanRead);
        Assert.True(reader.CopyCommand.CanExecute(null));
        Assert.True(reader.NextPageCommand.CanExecute(null));
        Assert.False(reader.PreviousPageCommand.CanExecute(null));
        reader.NextPageCommand.Execute(null);
        Assert.Equal(2, reader.CurrentPage);
        Assert.False(reader.NextPageCommand.CanExecute(null));
        Assert.True(reader.PreviousPageCommand.CanExecute(null));
        reader.NextPageCommand.Execute(null);
        Assert.Equal(2, reader.CurrentPage);
        Assert.Equal(1, legacyLoaded);
        Assert.True(commandChanges > 0);
        Assert.Contains(states, state => state.State == PdfViewerDocumentState.Loading);
        Assert.Contains(states, state => state.State == PdfViewerDocumentState.Ready && state.SessionId == session);
        reader.CloseCommand.Execute(null);
        Assert.Equal(PdfViewerDocumentState.Empty, reader.DocumentState);
        Assert.Null(reader.DocumentSessionId);
        Assert.Null(reader.Metadata);
        Assert.Null(reader.Permissions);
        Assert.False(reader.Capabilities.CanRead);
        reader.Dispose();
        Assert.Equal(PdfViewerDocumentState.Disposed, reader.DocumentState);
        Assert.False(reader.OpenCommand.CanExecute(null));
    });

    [Fact]
    public Task Host_policies_change_real_operations_without_misrepresenting_future_features() => HeadlessTestScope.Run(async () =>
    {
        using var reader = new Reader();
        await reader.LoadDocumentAsync(ContractFixture.Create(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(reader.IsReadOnly);
        Assert.True(reader.Capabilities.CanCopy);
        Assert.True(reader.Capabilities.CanSearch);
        Assert.False(reader.Capabilities.CanAnnotate);
        Assert.False(reader.Capabilities.CanFillForms);
        Assert.False(reader.Capabilities.CanSave);
        Assert.False(reader.Capabilities.CanPrint);
        Assert.False(reader.Capabilities.CanShare);
        Assert.False(reader.Capabilities.Annotate.IsImplemented);
        reader.IsReadOnly = false;
        Assert.Equal(PdfCapabilityUnavailableReason.NotImplemented, reader.Capabilities.Annotate.Reason);
        reader.AllowCopy = false;
        Assert.False(reader.CopyCommand.CanExecute(null));
        Assert.False(reader.SelectAllCommand.CanExecute(null));
        Assert.Equal(PdfCapabilityUnavailableReason.HostDisabled, reader.Capabilities.Copy.Reason);
        Assert.NotEmpty(await reader.SearchAsync("needle", TestContext.Current.CancellationToken));
        reader.AllowSearch = false;
        Assert.False(reader.SearchCommand.CanExecute("needle"));
        Assert.Empty(reader.SearchResults);
        Assert.Empty(await reader.SearchAsync("needle", TestContext.Current.CancellationToken));
        reader.AllowSearch = true;
        Assert.NotEmpty(await reader.SearchAsync("needle", TestContext.Current.CancellationToken));
        reader.AllowCopy = true;
        reader.IsReadOnly = true;
        Assert.True(reader.CopyCommand.CanExecute(null));
        Assert.True(reader.Capabilities.CanSearch);
    });

    [Fact]
    public Task Typed_sources_and_legacy_loads_replace_each_other_and_password_reload_keeps_the_current_source() => HeadlessTestScope.Run(async () =>
    {
        using var reader = new Reader();
        var source = PdfDocumentSource.FromBytes(ContractFixture.Create(), "Typed contract");
        await reader.LoadDocumentAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(source, reader.DocumentSource);
        Assert.Null(reader.Source);
        Assert.Equal("Typed contract", reader.DocumentName);
        Assert.Equal("契约标题", reader.Metadata?.Title);
        var first = reader.DocumentSessionId;
        await reader.LoadDocumentAsync(PdfFixture.SinglePage(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(reader.DocumentSource);
        Assert.NotEqual(first, reader.DocumentSessionId);
        var secure = PdfDocumentSource.FromBytes(ContractFixture.Create(restricted: true), "Restricted");
        await reader.LoadDocumentAsync(secure, "contract-user", TestContext.Current.CancellationToken);
        Assert.Equal(PdfPasswordAccess.User, reader.Permissions?.PasswordAccess);
        Assert.False(reader.Capabilities.CanCopy);
        Assert.True(reader.Capabilities.CanSearch);
        Assert.NotEmpty(await reader.SearchAsync("needle", TestContext.Current.CancellationToken));
        var reloadStates = new List<string>();
        reader.DocumentStateChanged += (_, state) => reloadStates.Add(state.State.ToString());
        PdfError? reloadFailure = null;
        reader.DocumentLoadFailed += (_, failure) => reloadFailure = failure.Error;
        reader.Password = "contract-owner";
        await WaitUntil(() => reader.DocumentState == PdfViewerDocumentState.Ready && reader.Permissions?.PasswordAccess == PdfPasswordAccess.Owner,
            () => $"Password reload did not reach owner Ready: state={reader.DocumentState}, role={reader.Permissions?.PasswordAccess}, " +
                $"sourcePreserved={ReferenceEquals(secure, reader.DocumentSource)}, loading={reader.IsLoading}, " +
                $"lastError={reader.LastError?.Kind}/{reader.LastError?.Exception.GetType().Name}, " +
                $"loadFailure={reloadFailure?.Kind}/{reloadFailure?.Exception.GetType().Name}, states={string.Join(',', reloadStates)}");
        Assert.Same(secure, reader.DocumentSource);
        Assert.True(reader.CopyCommand.CanExecute(null));
        reader.CloseDocument();
        Assert.Null(reader.DocumentSource);
    });

    [Fact]
    public Task Typed_failure_keeps_legacy_event_and_exposes_error_kind_without_changing_exception_identity() => HeadlessTestScope.Run(async () =>
    {
        using var reader = new Reader();
        var legacyErrors = 0;
        PdfDocumentLoadFailedEventArgs? failure = null;
        reader.LoadError += (_, _) => legacyErrors++;
        reader.DocumentLoadFailed += (_, args) => failure = args;
        var source = PdfDocumentSource.FromBytes(ContractFixture.Create(restricted: true), "Password source");
        var error = await Assert.ThrowsAsync<PdfDocumentException>(() => reader.LoadDocumentAsync(source, "incorrect", TestContext.Current.CancellationToken));
        Assert.Equal(1, legacyErrors);
        Assert.NotNull(failure);
        Assert.Same(source, failure.Source);
        Assert.Same(error, failure.Error.Exception);
        Assert.Equal(PdfErrorKind.PasswordRequiredOrIncorrect, reader.LastError?.Kind);
        Assert.Equal(PdfViewerDocumentState.Error, reader.DocumentState);
        Assert.Null(reader.DocumentSessionId);
        Assert.False(reader.CopyCommand.CanExecute(null));
        reader.CloseDocument();
        Assert.Null(reader.LastError);
        Assert.Equal(PdfViewerDocumentState.Empty, reader.DocumentState);
    });

    [Fact]
    public Task Canceled_factory_load_cannot_publish_stale_metadata_or_session_after_replacement() => HeadlessTestScope.Run(async () =>
    {
        using var reader = new Reader();
        using var gated = new GatedReadStream(ContractFixture.Create());
        var source = PdfDocumentSource.FromStreamFactory(_ => ValueTask.FromResult<Stream>(gated), "Blocked source");
        var pending = reader.LoadDocumentAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await gated.ReadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PdfViewerDocumentState.Loading, reader.DocumentState);
        Assert.Equal(PdfCapabilityUnavailableReason.Busy, reader.Capabilities.Read.Reason);
        Assert.False(reader.OpenCommand.CanExecute(null));
        Assert.True(reader.CloseCommand.CanExecute(null));
        await reader.LoadDocumentAsync(PdfFixture.SinglePage(), cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        var session = reader.DocumentSessionId;
        gated.Release.TrySetResult();
        HeadlessTestScope.Drain();
        Assert.Equal(PdfViewerDocumentState.Ready, reader.DocumentState);
        Assert.Equal(session, reader.DocumentSessionId);
        Assert.Null(reader.DocumentSource);
        Assert.Equal("", reader.Metadata?.Title);
        Assert.Null(reader.LastError);
    });

    private static async Task WaitUntil(Func<bool> condition, Func<string>? diagnostic = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            while (!condition()) await Task.Delay(10, deadline.Token);
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(diagnostic?.Invoke() ?? "The expected viewer state was not reached.");
        }
    }
}
