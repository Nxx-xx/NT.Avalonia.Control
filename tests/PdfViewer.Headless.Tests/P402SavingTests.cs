using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;
using PdfViewer.Tests;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

public sealed class P402SavingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Target() => Path.Combine(AppContext.BaseDirectory, "p402-" + Guid.NewGuid().ToString("N") + ".pdf");
    private static Task<PdfEmbeddedTrueTypeFont> Font() => Task.FromResult(new PdfEmbeddedTrueTypeFont(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "P0Font.ttf"))));
    private static Task Edit(Reader reader, string text) => reader.ExecuteEditAsync(async (session, token) =>
        await session.AddFreeTextAsync(0, new(15, 90, 250, 45), text, await Font(), cancellationToken: token), Token);
    private static async Task<Reader> Open()
    {
        var reader = new Reader { IsReadOnly = false, IsSidebarVisible = false };
        await reader.LoadDocumentAsync(PdfFixture.SinglePage("Original page", 300, 220), cancellationToken: Token);
        return reader;
    }
    private static async Task Until(Func<bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!predicate()) { HeadlessTestScope.Drain(); await Task.Delay(10, timeout.Token); }
    }

    [Fact]
    public Task Save_as_original_path_undo_redo_and_bookmarks_form_one_control_workflow() => HeadlessTestScope.Run(async () =>
    {
        var path = Target();
        try
        {
            await using var reader = await Open();
            var sessionId = reader.DocumentSessionId;
            var before = await reader.ExportPageImageAsync(0, cancellationToken: Token);
            await Edit(reader, "A");
            Assert.True(reader.IsDirty); Assert.True(reader.CanUndo);
            Assert.NotEqual(before, await reader.ExportPageImageAsync(0, cancellationToken: Token));
            var bookmark = reader.AddUserBookmark("Persisted bookmark", flags: PdfUserBookmarkFlags.Bold);
            Assert.True(await reader.SaveAsAsync(path, Token));
            Assert.False(reader.IsDirty); Assert.Equal(path, reader.SavePath); Assert.Equal(path, reader.Source);
            Assert.Equal(sessionId, reader.DocumentSessionId);
            await reader.UndoAsync(Token);
            Assert.True(reader.IsDirty); Assert.True(reader.CanRedo);
            Assert.Equal(bookmark.Id, Assert.Single(reader.UserBookmarks).Id);
            Assert.Equal(before, await reader.ExportPageImageAsync(0, cancellationToken: Token));
            await reader.RedoAsync(Token); Assert.False(reader.IsDirty);
            await reader.UndoAsync(Token); await Edit(reader, "B"); Assert.True(reader.IsDirty);
            Assert.True(await reader.SaveAsync(Token)); Assert.False(reader.IsDirty);
            await using var reopened = await PdfEditSession.OpenAsync(await File.ReadAllBytesAsync(path, Token), Token);
            Assert.Equal("B", Assert.Single(await reopened.GetFreeTextAnnotationsAsync(0, Token)).Text);
            Assert.Equal(bookmark.Id, Assert.Single(await reopened.GetUserBookmarksAsync(Token)).Id);
            await reader.LoadDocumentAsync(path, cancellationToken: Token);
            Assert.Equal(bookmark.Id, Assert.Single(reader.UserBookmarks).Id);
            Assert.False(reader.IsDirty); Assert.False(reader.CanUndo);
        }
        finally { File.Delete(path); }
    });

    [Fact]
    public Task Export_is_owned_and_does_not_save_stream_save_marks_clean_and_leaves_stream_open() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = await Open();
        await Edit(reader, "A");
        var first = await reader.ExportDocumentAsync(Token);
        first[0] = 0;
        Assert.Equal((byte)'%', (await reader.ExportDocumentAsync(Token))[0]); Assert.True(reader.IsDirty);
        using var output = new MemoryStream(); output.Write("prefix"u8);
        await reader.SaveAsync(output, Token);
        Assert.True(output.CanWrite); Assert.False(reader.IsDirty); Assert.Null(reader.SavePath);
        await using var pdf = await PdfEditSession.OpenAsync(output.ToArray()[6..], Token);
        Assert.Equal("A", Assert.Single(await pdf.GetFreeTextAnnotationsAsync(0, Token)).Text);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Failed_file_or_stream_save_retains_dirty_history_and_original_bytes(bool stream) => HeadlessTestScope.Run(async () =>
    {
        var path = Target();
        try
        {
            await File.WriteAllBytesAsync(path, PdfFixture.SinglePage(), Token);
            var original = await File.ReadAllBytesAsync(path, Token);
            await using var reader = await Open(); await Edit(reader, "A");
            var failures = 0; reader.SaveFailed += (_, _) => failures++;
            if (stream) await Assert.ThrowsAsync<IOException>(() => reader.SaveAsync(new FailingStream(), Token));
            else
            {
                using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                // Windows denies replacement; Unix allows renaming locked files. A directory target fails on both.
                if (OperatingSystem.IsWindows()) await Assert.ThrowsAnyAsync<IOException>(() => reader.SaveAsAsync(path, Token));
                else await Assert.ThrowsAnyAsync<IOException>(() => reader.SaveAsAsync(AppContext.BaseDirectory, Token));
            }
            Assert.Equal(1, failures); Assert.True(reader.IsDirty); Assert.True(reader.CanUndo);
            Assert.Null(reader.SavePath); Assert.False(reader.IsSaving);
            Assert.Equal(original, await File.ReadAllBytesAsync(path, Token));
            Assert.Empty(Directory.GetFiles(AppContext.BaseDirectory, "." + Path.GetFileName(path) + ".*.writing"));
        }
        finally { File.Delete(path); }
    });

    [Fact]
    public Task Save_request_and_picker_can_cancel_override_and_report_saved_target() => HeadlessTestScope.Run(async () =>
    {
        var path = Target();
        try
        {
            await using var reader = await Open(); reader.AddUserBookmark("Bookmark");
            var picker = new Dialogs(); reader.FileDialogService = picker;
            Assert.False(await reader.SaveAsync(Token)); Assert.True(reader.IsDirty); Assert.Equal(1, picker.Calls);
            EventHandler<PdfSaveRequestedEventArgs> cancel = (_, args) => args.Cancel = true;
            reader.SaveRequested += cancel;
            Assert.False(await reader.SaveAsAsync(path, Token)); Assert.False(File.Exists(path));
            reader.SaveRequested -= cancel;
            PdfDocumentSavedEventArgs? saved = null;
            reader.SaveRequested += (_, args) => args.Path = path;
            reader.DocumentSaved += (_, args) => saved = args;
            Assert.True(await reader.SaveAsync(Token)); Assert.Equal(path, saved?.Path); Assert.False(saved!.IsAutoSave);
            Assert.Equal(1, picker.Calls); Assert.False(reader.IsDirty);
        }
        finally { File.Delete(path); }
    });

    [Theory]
    [InlineData(PdfUnsavedChangesDecision.Cancel)]
    [InlineData(PdfUnsavedChangesDecision.Save)]
    [InlineData(PdfUnsavedChangesDecision.Discard)]
    public Task Close_prompt_decisions_preserve_or_save_or_discard(PdfUnsavedChangesDecision decision) => HeadlessTestScope.Run(async () =>
    {
        var path = Target();
        try
        {
            await using var reader = await Open(); reader.AddUserBookmark("Unsaved");
            reader.SaveRequested += (_, args) => args.Path = path;
            var close = reader.RequestCloseDocumentAsync(Token);
            Assert.True(reader.IsUnsavedChangesPromptVisible); Assert.False(close.IsCompleted);
            reader.RespondToUnsavedChanges(decision);
            Assert.Equal(decision != PdfUnsavedChangesDecision.Cancel, await close);
            Assert.Equal(decision == PdfUnsavedChangesDecision.Cancel, reader.HasDocument);
            Assert.False(reader.IsUnsavedChangesPromptVisible);
            Assert.Equal(decision == PdfUnsavedChangesDecision.Save, File.Exists(path));
            if (decision == PdfUnsavedChangesDecision.Save)
            {
                await using var pdf = await PdfEditSession.OpenAsync(await File.ReadAllBytesAsync(path, Token), Token);
                Assert.Equal("Unsaved", Assert.Single(await pdf.GetUserBookmarksAsync(Token)).Title);
            }
        }
        finally { File.Delete(path); }
    });

    [Fact]
    public Task Failed_save_and_canceled_save_picker_abort_close_without_discarding() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = await Open(); reader.AddUserBookmark("Keep");
        reader.UnsavedChangesService = new Confirmation(PdfUnsavedChangesDecision.Save);
        reader.FileDialogService = new Dialogs();
        Assert.False(await reader.RequestCloseDocumentAsync(Token)); Assert.True(reader.HasDocument); Assert.True(reader.IsDirty);
        reader.SaveRequested += (_, args) => args.Path = Path.Combine(AppContext.BaseDirectory, "missing-" + Guid.NewGuid(), "fail.pdf");
        Assert.False(await reader.RequestCloseDocumentAsync(Token)); Assert.True(reader.HasDocument); Assert.True(reader.IsDirty);
        Assert.NotNull(reader.LastError);
    });

    [Fact]
    public Task Replacement_cancel_restores_binding_and_newer_transition_invalidates_old_confirmation() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = await Open(); var sessionId = reader.DocumentSessionId;
        reader.AddUserBookmark("Keep");
        reader.UnsavedChangesService = new Confirmation(PdfUnsavedChangesDecision.Cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.LoadDocumentAsync(PdfDocumentSource.FromBytes(PdfFixture.ReadingDocument(), "Replacement"), cancellationToken: Token));
        Assert.Equal(sessionId, reader.DocumentSessionId); Assert.Null(reader.DocumentSource); Assert.True(reader.IsDirty);
        reader.UnsavedChangesService = null;
        var first = reader.RequestCloseDocumentAsync(Token);
        var replacement = reader.LoadDocumentAsync(PdfFixture.ReadingDocument(), cancellationToken: Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Until(() => reader.IsUnsavedChangesPromptVisible);
        reader.RespondToUnsavedChanges(PdfUnsavedChangesDecision.Discard);
        await replacement;
        Assert.Equal(2, reader.PageCount); Assert.False(reader.IsDirty); Assert.NotEqual(sessionId, reader.DocumentSessionId);
    });

    [Fact]
    public Task Event_decision_supersedes_service_and_dispose_cancels_pending_prompt() => HeadlessTestScope.Run(async () =>
    {
        var reader = await Open(); reader.AddUserBookmark("Keep");
        reader.UnsavedChangesService = new Confirmation(PdfUnsavedChangesDecision.Discard);
        EventHandler<PdfUnsavedChangesEventArgs> cancel = (_, args) => args.Decision = Task.FromResult(PdfUnsavedChangesDecision.Cancel);
        reader.UnsavedChangesRequested += cancel;
        Assert.False(await reader.RequestCloseDocumentAsync(Token));
        reader.UnsavedChangesRequested -= cancel; reader.UnsavedChangesService = null;
        var closing = reader.RequestCloseDocumentAsync(Token);
        await reader.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closing);
        Assert.False(reader.HasDocument); Assert.False(reader.IsUnsavedChangesPromptVisible);
    });

    [Fact]
    public Task Close_waits_for_pending_transaction_before_asking_and_cancel_keeps_new_content() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = await Open();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var edit = reader.ExecuteEditAsync(async (session, token) =>
        {
            entered.SetResult(); await release.Task.WaitAsync(token);
            await session.AddFreeTextAsync(0, new(15, 90, 250, 45), "A", await Font(), cancellationToken: token);
        }, Token);
        await entered.Task;
        var close = reader.RequestCloseDocumentAsync(Token);
        Assert.False(close.IsCompleted); Assert.False(reader.IsUnsavedChangesPromptVisible);
        release.SetResult(); await edit;
        await Until(() => reader.IsUnsavedChangesPromptVisible);
        reader.RespondToUnsavedChanges(PdfUnsavedChangesDecision.Cancel);
        Assert.False(await close); Assert.True(reader.IsDirty); Assert.True(reader.CanUndo);
    });

    [Fact]
    public Task AutoSave_debounces_changes_requires_target_and_uses_new_save_as_path() => HeadlessTestScope.Run(async () =>
    {
        var first = Target(); var second = Target();
        try
        {
            await using var reader = await Open(); reader.AutoSaveInterval = TimeSpan.FromMilliseconds(100); reader.AutoSave = true;
            var saves = 0; reader.DocumentSaved += (_, args) => { if (args.IsAutoSave) saves++; };
            var bookmark = reader.AddUserBookmark("A");
            await Task.Delay(250, Token); Assert.Equal(0, saves); Assert.True(reader.IsDirty);
            Assert.True(await reader.SaveAsAsync(first, Token));
            reader.RenameUserBookmark(bookmark.Id, "B"); reader.RenameUserBookmark(bookmark.Id, "C"); reader.RenameUserBookmark(bookmark.Id, "D");
            await Until(() => saves == 1 && !reader.IsDirty);
            Assert.True(await reader.SaveAsAsync(second, Token));
            reader.RenameUserBookmark(bookmark.Id, "E");
            await Until(() => saves == 2 && !reader.IsDirty);
            await using var a = await PdfEditSession.OpenAsync(await File.ReadAllBytesAsync(first, Token), Token);
            await using var b = await PdfEditSession.OpenAsync(await File.ReadAllBytesAsync(second, Token), Token);
            Assert.Equal("D", Assert.Single(await a.GetUserBookmarksAsync(Token)).Title);
            Assert.Equal("E", Assert.Single(await b.GetUserBookmarksAsync(Token)).Title);
            reader.AutoSave = false; reader.RenameUserBookmark(bookmark.Id, "F");
            await Task.Delay(250, Token); Assert.Equal(2, saves); Assert.True(reader.IsDirty);
        }
        finally { File.Delete(first); File.Delete(second); }
    });

    [Fact]
    public Task Serialized_stream_saves_and_late_bookmark_changes_keep_unsaved_state() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = await Open(); var bookmark = reader.AddUserBookmark("A");
        var output = new GatedWriteStream(); var saved = reader.SaveAsync(output, Token);
        await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        reader.RenameUserBookmark(bookmark.Id, "B");
        var export = reader.ExportDocumentAsync(Token); Assert.False(export.IsCompleted);
        output.Release.SetResult(); await saved;
        Assert.True(reader.IsDirty);
        await using var old = await PdfEditSession.OpenAsync(output.ToArray(), Token);
        await using var current = await PdfEditSession.OpenAsync(await export, Token);
        Assert.Equal("A", Assert.Single(await old.GetUserBookmarksAsync(Token)).Title);
        Assert.Equal("B", Assert.Single(await current.GetUserBookmarksAsync(Token)).Title);
        using var final = new MemoryStream(); await reader.SaveAsync(final, Token); Assert.False(reader.IsDirty);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Readonly_or_security_boundaries_disable_control_writes(bool encrypted) => HeadlessTestScope.Run(async () =>
    {
        await using var reader = new Reader();
        await reader.LoadDocumentAsync(ContractFixture.Create(restricted: encrypted, signature: !encrypted), encrypted ? "contract-owner" : null, Token);
        Assert.True(reader.Capabilities.Save.IsImplemented); Assert.False(reader.SaveCommand.CanExecute(null));
        reader.IsReadOnly = false;
        Assert.False(reader.Capabilities.CanSave); Assert.Equal(PdfCapabilityUnavailableReason.UnsupportedDocument, reader.Capabilities.Save.Reason);
        await Assert.ThrowsAsync<NotSupportedException>(() => reader.ExportDocumentAsync(Token));
        Assert.True(reader.HasDocument);
    });

    [Fact]
    public Task Theme_buttons_and_unsaved_prompt_are_wired_to_control_commands() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = await Open();
        var window = new Window { Width = 1100, Height = 800, Content = reader }; window.Show(); window.UpdateLayout(); HeadlessTestScope.Drain();
        try
        {
            Button Find(string name) => reader.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);
            Assert.True(Find("PART_Save").IsEnabled); Assert.True(Find("PART_SaveAs").IsEnabled); Assert.False(Find("PART_Undo").IsEnabled);
            await Edit(reader, "B"); Assert.True(Find("PART_Undo").IsEnabled);
            Find("PART_Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => reader.CanRedo && !reader.IsEditing);
            Find("PART_Redo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => reader.CanUndo && !reader.IsEditing);
            var close = reader.RequestCloseDocumentAsync(Token);
            Find("PART_UnsavedCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(await close); Assert.True(reader.HasDocument);
        }
        finally { window.Content = null; window.Close(); }
    });

    [Fact]
    public Task Canceling_stream_commit_preserves_dirty_and_queued_failure_does_not_poison_next_save() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = await Open(); reader.AddUserBookmark("A");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var output = new GatedWriteStream(); var save = reader.SaveAsync(output, cancellation.Token);
        await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => save);
        Assert.True(reader.IsDirty); Assert.False(reader.IsSaving);
        using var final = new MemoryStream(); await reader.SaveAsync(final, Token);
        Assert.False(reader.IsDirty); Assert.NotEmpty(final.ToArray()); Assert.Null(reader.LastError);
    });

    [Fact]
    public Task Failed_edit_rolls_back_and_readonly_blocks_history_without_losing_dirty() => HeadlessTestScope.Run(async () =>
    {
        await using var reader = await Open();
        await Assert.ThrowsAsync<IOException>(() => reader.ExecuteEditAsync(async (session, token) =>
        {
            await session.AddFreeTextAsync(0, new(15, 90, 250, 45), "A", await Font(), cancellationToken: token);
            throw new IOException("Injected transaction failure");
        }, Token));
        Assert.False(reader.IsDirty); Assert.False(reader.CanUndo);
        await Edit(reader, "B"); reader.IsReadOnly = true;
        Assert.True(reader.IsDirty); Assert.False(reader.UndoCommand.CanExecute(null)); Assert.False(reader.SaveCommand.CanExecute(null));
        await Assert.ThrowsAsync<NotSupportedException>(() => reader.UndoAsync(Token));
        reader.IsReadOnly = false; await reader.UndoAsync(Token); Assert.False(reader.IsDirty);
    });

    [Fact]
    public Task AutoSave_failure_is_reported_once_and_retains_dirty_until_explicit_retry() => HeadlessTestScope.Run(async () =>
    {
        var path = Target();
        try
        {
            await using var reader = await Open(); var bookmark = reader.AddUserBookmark("A");
            Assert.True(await reader.SaveAsAsync(path, Token));
            reader.AutoSaveInterval = TimeSpan.FromMilliseconds(100); reader.AutoSave = true;
            reader.SaveRequested += (_, args) => { if (args.IsAutoSave) args.Path = Path.Combine(AppContext.BaseDirectory, "missing-" + Guid.NewGuid(), "failed.pdf"); };
            var failures = 0; reader.SaveFailed += (_, args) => { if (args.IsAutoSave) failures++; };
            reader.RenameUserBookmark(bookmark.Id, "B");
            await Until(() => failures == 1 && !reader.IsEditing);
            await Task.Delay(350, Token); Assert.Equal(1, failures); Assert.True(reader.IsDirty);
            Assert.True(await reader.SaveAsync(Token)); Assert.False(reader.IsDirty); Assert.Null(reader.LastError);
        }
        finally { File.Delete(path); }
    });

    [Fact]
    public Task Keyboard_save_and_history_respect_text_input_ownership_and_shortcut_switch() => HeadlessTestScope.Run(async () =>
    {
        var path = Target();
        try
        {
            await using var reader = await Open(); await Edit(reader, "A"); reader.SaveRequested += (_, args) => args.Path = path;
            var window = new Window { Width = 1100, Height = 800, Content = reader }; window.Show(); window.UpdateLayout(); HeadlessTestScope.Drain();
            try
            {
            void Key(Key key, KeyModifiers modifiers) => reader.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });
            Key(Avalonia.Input.Key.Z, KeyModifiers.Control); await Until(() => reader.CanRedo && !reader.IsEditing);
            Key(Avalonia.Input.Key.Z, KeyModifiers.Control | KeyModifiers.Shift); await Until(() => reader.CanUndo && !reader.IsEditing);
            reader.AllowShortcuts = false; Key(Avalonia.Input.Key.S, KeyModifiers.Control); Assert.False(File.Exists(path));
            reader.AllowShortcuts = true;
            var box = reader.GetVisualDescendants().OfType<TextBox>().Single(b => b.Name == "PART_SearchBox");
            box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Z, KeyModifiers = KeyModifiers.Control });
            Assert.True(reader.CanUndo); Assert.False(reader.IsEditing);
            Key(Avalonia.Input.Key.S, KeyModifiers.Control); await Until(() => File.Exists(path) && !reader.IsDirty && !reader.IsEditing);
            }
            finally { window.Content = null; window.Close(); }
        }
        finally { File.Delete(path); }
    });

    [Fact]
    public Task Save_path_notification_can_dispose_without_publishing_stale_source_or_name() => HeadlessTestScope.Run(async () =>
    {
        var path = Target();
        try
        {
            var reader = await Open(); reader.AddUserBookmark("A");
            reader.PropertyChanged += (_, change) => { if (change.Property == Reader.SavePathProperty && reader.SavePath is not null) reader.Dispose(); };
            Assert.True(await reader.SaveAsAsync(path, Token));
            await reader.DisposeAsync();
            Assert.True(File.Exists(path)); Assert.Equal(PdfViewerDocumentState.Disposed, reader.DocumentState);
            Assert.Null(reader.Source); Assert.Null(reader.SavePath); Assert.Empty(reader.DocumentName); Assert.False(reader.HasDocument);
        }
        finally { File.Delete(path); }
    });

    private sealed class Dialogs : IPdfFileDialogService
    {
        public int Calls { get; private set; }
        public bool CanOpen => true; public bool CanSave => true;
        public Task<PdfDocumentSource?> OpenAsync(CancellationToken cancellationToken = default) => Task.FromResult<PdfDocumentSource?>(null);
        public Task<string?> GetSavePathAsync(string suggestedFileName, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult<string?>(null); }
    }
    private sealed class Confirmation(PdfUnsavedChangesDecision decision) : IPdfUnsavedChangesService
    {
        public Task<PdfUnsavedChangesDecision> ConfirmAsync(PdfUnsavedChangesContext context, CancellationToken cancellationToken = default) => Task.FromResult(decision);
    }
    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException(new IOException("Injected write failure"));
    }
    private sealed class GatedWriteStream : MemoryStream
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { Entered.SetResult(); await Release.Task.WaitAsync(cancellationToken); await base.WriteAsync(buffer, cancellationToken); }
    }
}
