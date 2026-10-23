using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

/// <summary>
/// Reading-interaction contract: the default password prompt with retry and cancel, wholesale
/// shortcut disabling (AllowShortcuts / IsEnabled), the public selection state with its clear
/// entry, and the CurrentPageChanged / SearchCompleted events.
/// </summary>
public sealed class ReadingContractTests
{
    [Fact]
    public Task Password_prompt_retries_and_accepts_the_correct_password() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = ReadingFixture.CreateEmpty();
        var reader = fixture.Reader;
        var error = await Assert.ThrowsAsync<PdfDocumentException>(() =>
            reader.LoadDocumentAsync(PdfFixture.PasswordProtected(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(PdfErrorKind.PasswordRequiredOrIncorrect, error.Kind);
        Assert.True(reader.IsPasswordPromptVisible);
        Assert.False(reader.HasDocument);

        // The template's password box submits with Enter: a wrong password re-offers the prompt.
        var box = fixture.PasswordBox;
        ScopedInput(() =>
        {
            box.Text = "wrong";
            box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        });
        // Submitting hides the prompt synchronously; the wrong password re-offers it when it fails.
        Assert.False(reader.IsPasswordPromptVisible);
        await Until(() => reader.IsPasswordPromptVisible);
        Assert.False(reader.HasDocument);
        Assert.NotNull(reader.ErrorMessage);

        // The template's submit button sends the box's text; enter the correct password first.
        var submit = fixture.PasswordSubmit;
        ScopedInput(() =>
        {
            box.Text = "reader";
            submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        HeadlessTestScope.Drain();
        await Until(() => reader.HasDocument);
        await fixture.Idle();
        Assert.False(reader.IsPasswordPromptVisible);
        Assert.Equal(PdfViewerDocumentState.Ready, reader.DocumentState);
        Assert.Null(reader.ErrorMessage);
        Assert.Equal("reader", reader.Password);
        // The retried document is fully usable.
        Assert.Equal(1, reader.PageCount);
    });

    [Fact]
    public Task Password_prompt_cancel_reports_the_error_without_a_document() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = ReadingFixture.CreateEmpty();
        var reader = fixture.Reader;
        await Assert.ThrowsAsync<PdfDocumentException>(() =>
            reader.LoadDocumentAsync(PdfFixture.PasswordProtected(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(reader.IsPasswordPromptVisible);

        reader.CancelPasswordPrompt();
        Assert.False(reader.IsPasswordPromptVisible);
        Assert.False(reader.HasDocument);
        Assert.Equal(PdfViewerDocumentState.Error, reader.DocumentState);
        Assert.NotNull(reader.ErrorMessage);
        // Cancel keeps the retry data: a host can still submit a password after hiding the prompt.
        reader.SubmitPassword("reader");
        await Until(() => reader.HasDocument);
        await fixture.Idle();
        Assert.Equal(PdfViewerDocumentState.Ready, reader.DocumentState);
    });

    [Fact]
    public Task Password_prompt_can_be_suppressed_by_the_host() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = ReadingFixture.CreateEmpty();
        var reader = fixture.Reader;
        reader.IsPasswordPromptEnabled = false;
        await Assert.ThrowsAsync<PdfDocumentException>(() =>
            reader.LoadDocumentAsync(PdfFixture.PasswordProtected(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(reader.IsPasswordPromptVisible);
        Assert.Equal(PdfViewerDocumentState.Error, reader.DocumentState);
        Assert.NotNull(reader.ErrorMessage);
    });

    [Fact]
    public Task Password_retry_reopens_a_file_path_source_through_the_opener() => HeadlessTestScope.Run(async () =>
    {
        // File-path sources must stay re-openable across password retries: the failed attempt
        // cannot consume a one-shot stream, and the retry re-opens the same path from disk.
        var path = Path.Combine(Path.GetTempPath(), "nt-pdf-password-retry-" + Guid.NewGuid().ToString("N") + ".pdf");
        await File.WriteAllBytesAsync(path, PdfFixture.PasswordProtected(), TestContext.Current.CancellationToken);
        try
        {
            using var fixture = ReadingFixture.CreateEmpty();
            var reader = fixture.Reader;
            var error = await Assert.ThrowsAsync<PdfDocumentException>(() =>
                reader.LoadDocumentAsync(path, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(PdfErrorKind.PasswordRequiredOrIncorrect, error.Kind);
            Assert.True(reader.IsPasswordPromptVisible);
            Assert.False(reader.HasDocument);

            reader.SubmitPassword("reader");
            await Until(() => reader.HasDocument);
            await fixture.Idle();
            Assert.False(reader.IsPasswordPromptVisible);
            Assert.Equal(PdfViewerDocumentState.Ready, reader.DocumentState);
            Assert.Null(reader.ErrorMessage);
            Assert.Equal(1, reader.PageCount);
            Assert.Equal(Path.GetFileName(path), reader.DocumentName);
        }
        finally
        {
            File.Delete(path);
        }
    });

    [Fact]
    public Task AllowShortcuts_false_and_a_disabled_viewer_ignore_every_shortcut() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ReadingFixture.Create(PdfFixture.ManyPages(6));
        var reader = fixture.Reader;
        reader.ZoomMode = PdfZoomMode.Manual;
        reader.ZoomLevel = 1;
        await fixture.Idle();
        var search = fixture.SearchBox;

        reader.AllowShortcuts = false;
        fixture.RaiseKey(Key.Right);
        Assert.Equal(1, reader.CurrentPage);
        fixture.RaiseKey(Key.Home);
        fixture.RaiseKey(Key.End);
        Assert.Equal(1, reader.CurrentPage);
        fixture.RaiseKey(Key.D2, KeyModifiers.Control);
        Assert.Equal(PdfZoomMode.Manual, reader.ZoomMode);
        fixture.RaiseKey(Key.D1, KeyModifiers.Control);
        Assert.Equal(PdfZoomMode.Manual, reader.ZoomMode);
        fixture.RaiseKey(Key.OemPlus, KeyModifiers.Control);
        Assert.Equal(1, reader.ZoomLevel);
        fixture.RaiseKey(Key.F, KeyModifiers.Control);
        Assert.False(search.IsKeyboardFocusWithin);
        // The wholesale switch also covers the search-walk keys.
        fixture.RaiseKey(Key.F3);
        Assert.Equal(-1, reader.CurrentSearchResultIndex);

        // A disabled control suppresses the same layer even when shortcuts are allowed.
        reader.AllowShortcuts = true;
        reader.IsEnabled = false;
        fixture.RaiseKey(Key.Right);
        Assert.Equal(1, reader.CurrentPage);
        fixture.RaiseKey(Key.D2, KeyModifiers.Control);
        Assert.Equal(PdfZoomMode.Manual, reader.ZoomMode);

        reader.IsEnabled = true;
        fixture.RaiseKey(Key.Right);
        Assert.Equal(2, reader.CurrentPage);
        fixture.RaiseKey(Key.D2, KeyModifiers.Control);
        Assert.Equal(PdfZoomMode.FitWidth, reader.ZoomMode);
    });

    [Fact]
    public Task Selection_state_and_clear_entry_are_public() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ReadingFixture.Create(PdfFixture.ManyPages(3));
        var reader = fixture.Reader;
        Assert.False(reader.IsTextSelected);
        Assert.False(reader.ClearSelectionCommand.CanExecute(null));

        await reader.SelectAllAsync();
        Assert.True(reader.IsTextSelected);
        Assert.True(reader.ClearSelectionCommand.CanExecute(null));
        Assert.False(string.IsNullOrEmpty(reader.SelectedText));

        reader.ClearSelection();
        Assert.False(reader.IsTextSelected);
        Assert.False(reader.ClearSelectionCommand.CanExecute(null));
        Assert.Equal("", reader.SelectedText);

        // Esc reaches the same public entry.
        await reader.SelectAllAsync();
        Assert.True(reader.IsTextSelected);
        fixture.RaiseKey(Key.Escape);
        Assert.False(reader.IsTextSelected);

        // The command is a host-bindable entry as well.
        await reader.SelectAllAsync();
        reader.ClearSelectionCommand.Execute(null);
        Assert.False(reader.IsTextSelected);

        // Disabling selection clears the active range.
        await reader.SelectAllAsync();
        Assert.True(reader.IsTextSelected);
        reader.AllowTextSelection = false;
        Assert.False(reader.IsTextSelected);
    });

    [Fact]
    public Task CurrentPageChanged_and_SearchCompleted_report_navigation_and_search() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ReadingFixture.Create(PdfFixture.ManyPages(3));
        var reader = fixture.Reader;
        var pages = new List<int>();
        reader.CurrentPageChanged += (_, _) => pages.Add(reader.CurrentPage);
        reader.GoToPage(2);
        Assert.Equal(new[] { 2 }, pages);
        reader.GoToPage(3);
        Assert.Equal(new[] { 2, 3 }, pages);
        // Host property sets raise the same event.
        reader.CurrentPage = 1;
        Assert.Equal(new[] { 2, 3, 1 }, pages);

        var completed = new List<PdfSearchCompletedEventArgs>();
        reader.SearchCompleted += (_, e) => completed.Add(e);
        var results = await reader.SearchAsync("Page", TestContext.Current.CancellationToken);
        Assert.Equal(3, results.Count);
        var done = Assert.Single(completed);
        Assert.Equal(3, done.ResultCount);
        Assert.False(done.Truncated);

        // An empty query is not a completed search.
        completed.Clear();
        Assert.Empty(await reader.SearchAsync(" ", TestContext.Current.CancellationToken));
        Assert.Empty(completed);

        // A canceled search never completes; only the natural end reports.
        completed.Clear();
        reader.SearchResultLimit = 1;
        await reader.SearchAsync("Page", TestContext.Current.CancellationToken);
        done = Assert.Single(completed);
        Assert.True(done.Truncated);
        Assert.Equal(1, done.ResultCount);
        Assert.True(reader.SearchResultsTruncated);
    });

    private static void ScopedInput(Action input)
    {
        var caller = ExecutionContext.Capture();
        if (caller is null) input();
        else ExecutionContext.Run(caller, _ => input(), null);
    }

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            HeadlessTestScope.Drain();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class ReadingFixture : IDisposable
    {
        internal Reader Reader { get; } = new();
        internal Window Window { get; }
        internal TextBox PasswordBox => Reader.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "PART_PasswordBox");
        internal TextBox SearchBox => Reader.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "PART_SearchBox");
        internal Button PasswordSubmit => Reader.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_PasswordSubmit");

        private ReadingFixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static ReadingFixture CreateEmpty() => new();

        internal static async Task<ReadingFixture> Create(byte[] bytes)
        {
            var fixture = new ReadingFixture();
            try
            {
                await fixture.Reader.LoadDocumentAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
                await fixture.Idle();
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        internal async Task Idle()
        {
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
            await Reader.WaitForIdleAsync(TestContext.Current.CancellationToken);
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal void RaiseKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
        {
            ScopedInput(() => Reader.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers,
            }));
            HeadlessTestScope.Drain();
        }

        public void Dispose()
        {
            Reader.Dispose();
            Window.Close();
        }
    }
}
