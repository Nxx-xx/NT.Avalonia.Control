using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;
using PdfViewer.Tests;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.P1;

internal static partial class Program
{
    private static async Task VerifyP402Async(Reader reader, Window window, CancellationToken token)
    {
        reader.IsReadOnly = false;
        await reader.LoadDocumentAsync(PdfFixture.SinglePage("P4-02 control save workflow", 400, 300), cancellationToken: token);
        var sessionId = reader.DocumentSessionId;
        var font = new PdfEmbeddedTrueTypeFont(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "P0Font.ttf"), token));
        await reader.ExecuteEditAsync(async (session, ct) => await session.AddFreeTextAsync(0, new(40, 150, 280, 60), "A", font, 32, ct), token);
        var bookmark = reader.AddUserBookmark("Saved bookmark", flags: PdfUserBookmarkFlags.Bold);
        Check("p402-edit-dirty-history", reader.IsDirty && reader.CanUndo && reader.DocumentSessionId == sessionId);
        var pathA = Path.Combine(_output, "p402-saved-A.pdf");
        Check("p402-save-as", await reader.SaveAsAsync(pathA, token) && !reader.IsDirty && reader.SavePath == pathA);
        await reader.UndoAsync(token);
        Check("p402-undo-keeps-bookmarks", reader.IsDirty && reader.CanRedo && reader.UserBookmarks.Single().Id == bookmark.Id);
        await reader.RedoAsync(token);
        Check("p402-redo-savepoint-clean", !reader.IsDirty);
        await reader.UndoAsync(token);
        await reader.ExecuteEditAsync(async (session, ct) => await session.AddFreeTextAsync(0, new(40, 150, 280, 60), "B", font, 32, ct), token);
        Check("p402-new-branch-dirty", reader.IsDirty && !reader.CanRedo);
        var pathB = Path.Combine(_output, "p402-saved-B.pdf");
        Check("p402-second-save-as", await reader.SaveAsAsync(pathB, token) && !reader.IsDirty && reader.SavePath == pathB);
        await using (var stream = new FileStream(Path.Combine(_output, "p402-stream.pdf"), FileMode.CreateNew, FileAccess.Write))
        {
            await reader.SaveAsync(stream, token);
            Check("p402-stream-left-open", stream.CanWrite && !reader.IsDirty && reader.SavePath == pathB);
        }
        var autoSaves = 0;
        reader.DocumentSaved += (_, args) => { if (args.IsAutoSave) autoSaves++; };
        reader.AutoSaveInterval = TimeSpan.FromMilliseconds(100);
        reader.AutoSave = true;
        reader.RenameUserBookmark(bookmark.Id, "AutoSaved bookmark");
        while (autoSaves == 0 || reader.IsEditing) await Task.Delay(10, token);
        reader.AutoSave = false;
        Check("p402-autosave", autoSaves == 1 && !reader.IsDirty);
        await using (var reopened = await PdfEditSession.OpenAsync(await File.ReadAllBytesAsync(pathB, token), token))
            Check("p402-reopen-content-and-bookmarks", (await reopened.GetFreeTextAnnotationsAsync(0, token)).Single().Text == "B" &&
                (await reopened.GetUserBookmarksAsync(token)).Single().Title == "AutoSaved bookmark");
        reader.RenameUserBookmark(bookmark.Id, "Unsaved bookmark");
        var closing = reader.RequestCloseDocumentAsync(token);
        Check("p402-prompt-awaitable", reader.IsUnsavedChangesPromptVisible && !closing.IsCompleted);
        var image = Path.Combine(_output, "p402-unsaved-prompt.png");
        window.UpdateLayout();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96)))
        { bitmap.Render(window); bitmap.Save(image); }
        var cancel = reader.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_UnsavedCancel");
        cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("p402-prompt-cancel-button", !await closing && reader.HasDocument && reader.IsDirty);
        Check("p402-save-original-target", await reader.SaveAsync(token) && !reader.IsDirty && reader.SavePath == pathB);
        await reader.LoadDocumentAsync(pathB, cancellationToken: token);
        Check("p402-control-reopen-bookmarks", reader.UserBookmarks.Single().Title == "Unsaved bookmark" && !reader.IsDirty && !reader.CanUndo);
        await VerifyP403Async(reader, token);
        // Leave the previous reader smoke's screenshot content unchanged; the P4 prompt has its own image.
        reader.IsReadOnly = true;
        await reader.LoadDocumentAsync(PdfFixture.ReadingDocument(), cancellationToken: token);
        reader.Rotation = 90; reader.ZoomLevel = 1.25; reader.GoToPage(1);
        await reader.WaitForIdleAsync(token);
        Observations["P402"] = new { AutoSaves = autoSaves, SavedA = pathA, SavedB = pathB, PromptImage = image, Boundary = "Native window / programmatic toolbar events; no physical keyboard, IME, OS file picker or scan-out certification." };
    }

    private static async Task VerifyP403Async(Reader reader, CancellationToken token)
    {
        var target = Path.Combine(_output, "p403-original.pdf");
        await File.WriteAllBytesAsync(target, PdfFixture.SinglePage("AAAA"), token);
        await reader.LoadDocumentAsync(target, cancellationToken: token);
        reader.AddUserBookmark("Native conflict bookmark");
        var external = PdfFixture.SinglePage("BBBB");
        var time = File.GetLastWriteTimeUtc(target);
        await File.WriteAllBytesAsync(target, external, token); File.SetLastWriteTimeUtc(target, time);
        var rejected = false;
        try { await reader.SaveAsync(token); } catch (PdfSaveConflictException) { rejected = true; }
        Check("p403-original-conflict-rejected", rejected && reader.LastError?.Kind == PdfErrorKind.SaveConflict);
        Check("p403-conflict-keeps-ui-and-dirty", reader.HasDocument && reader.IsDirty && reader.LastSaveConflict is not null && reader.ErrorMessage is { Length: > 0 });
        var retained = await File.ReadAllBytesAsync(target, token);
        Check("p403-external-content-preserved", external.SequenceEqual(retained));
        var copy = Path.Combine(_output, "p403-conflict-copy.pdf");
        Check("p403-save-copy-resolves-conflict", await reader.SaveAsAsync(copy, token) && !reader.IsDirty && reader.LastSaveConflict is null && reader.LastError is null);
        Check("p403-no-pending-recovery", (await PdfSaveRecovery.ScanAsync(_output, token)).Count == 0);
    }
}
