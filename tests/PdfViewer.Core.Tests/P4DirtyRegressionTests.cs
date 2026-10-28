using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;
using PdfViewer.Tests;

namespace PdfViewer.Core.Tests;

public sealed class P4DirtyRegressionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly PdfEditRectangle Bounds = new(15, 90, 250, 45);
    private static Task<PdfEditSession> Open() => PdfEditSession.OpenAsync(PdfFixture.SinglePage("Original page", 300, 220), Token);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Editing_a_new_branch_after_undo_cannot_reuse_the_saved_content_identity(bool saveToFile)
    {
        await using var edit = await Open();
        var font = new PdfEmbeddedTrueTypeFont(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "P0Font.ttf"), Token));
        await edit.AddFreeTextAsync(0, Bounds, "A", font, cancellationToken: Token);
        using var saved = new MemoryStream();
        var path = Path.Combine(AppContext.BaseDirectory, "nt-dirty-branch-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            if (saveToFile) await edit.SaveAsync(path, Token);
            else await edit.SaveAsync(saved, Token);
            var savedBytes = saveToFile ? await File.ReadAllBytesAsync(path, Token) : saved.ToArray();
            Assert.False(edit.IsDirty);

            await edit.UndoAsync(Token);
            await edit.AddFreeTextAsync(0, Bounds, "B", font, cancellationToken: Token);
            Assert.True(edit.IsDirty, "A new branch is not the saved A state.");
            Assert.False(edit.CanRedo);
            var current = await edit.ExportAsync(Token);
            Assert.False(savedBytes.SequenceEqual(current));
            await using var reopened = await PdfEditSession.OpenAsync(current, Token);
            Assert.Equal("B", Assert.Single(await reopened.GetFreeTextAnnotationsAsync(0, Token)).Text);

            await edit.UndoAsync(Token);
            Assert.True(edit.IsDirty);
            await edit.RedoAsync(Token);
            Assert.True(edit.IsDirty);
            await edit.SaveAsync(saved, Token);
            Assert.False(edit.IsDirty);
            await edit.UndoAsync(Token);
            await edit.AddFreeTextAsync(0, Bounds, "C", font, cancellationToken: Token);
            Assert.True(edit.IsDirty, "Successive branches must also have distinct saved identities.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Undoing_cleanup_keeps_removed_saved_bookmarks_dirty(bool redact)
    {
        await using var edit = await Open();
        var bookmark = new PdfEditUserBookmark(Guid.NewGuid(), "Saved bookmark", 0, PdfUserBookmarkFlags.Bold);
        await edit.SetUserBookmarksAsync([bookmark], Token);
        using var saved = new MemoryStream();
        await edit.SaveAsync(saved, Token);
        if (redact)
            await edit.ApplyRedactionsAsync([new PdfRedactionRegion(0, new(0, 0, 300, 220))],
                PdfHiddenInformationPolicy.RemoveAllNonPageData, Token);
        else
            await edit.SanitizeAsync(PdfHiddenInformationPolicy.RemoveAllNonPageData, Token);
        await edit.UndoAsync(Token);
        Assert.Empty(await edit.GetUserBookmarksAsync(Token));
        Assert.True(edit.IsDirty, "Undo restores content, not the saved bookmark overlay.");
        await using var savedSession = await PdfEditSession.OpenAsync(saved.ToArray(), Token);
        Assert.Equal(bookmark, Assert.Single(await savedSession.GetUserBookmarksAsync(Token)));
        await using var current = await PdfEditSession.OpenAsync(await edit.ExportAsync(Token), Token);
        Assert.Empty(await current.GetUserBookmarksAsync(Token));

        await edit.SetUserBookmarksAsync([bookmark], Token);
        Assert.False(edit.IsDirty, "Restoring the saved content and bookmark set restores a clean state.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Returning_to_the_saved_bookmark_set_clears_only_bookmark_dirty(bool nonEmptySavedSet)
    {
        await using var edit = await Open();
        var first = new PdfEditUserBookmark(Guid.NewGuid(), "A", 0, PdfUserBookmarkFlags.None);
        var second = new PdfEditUserBookmark(Guid.NewGuid(), "B", 0, PdfUserBookmarkFlags.Italic);
        PdfEditUserBookmark[] savedSet = nonEmptySavedSet ? [first] : [];
        await edit.SetUserBookmarksAsync(savedSet, Token);
        using var saved = new MemoryStream();
        await edit.SaveAsync(saved, Token);
        await edit.SetUserBookmarksAsync([second], Token);
        Assert.True(edit.IsDirty);
        await edit.SetUserBookmarksAsync(savedSet, Token);
        Assert.False(edit.IsDirty);

        await edit.SanitizeAsync(PdfHiddenInformationPolicy.RemoveAllNonPageData, Token);
        await edit.SetUserBookmarksAsync(savedSet, Token);
        Assert.True(edit.IsDirty, "Matching bookmarks must not clear an unsaved content change.");
        await edit.UndoAsync(Token);
        Assert.False(edit.IsDirty);
    }
}
