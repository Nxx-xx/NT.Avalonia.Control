using NT.AvaloniaControl.PdfViewer;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer.Saving;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

public sealed class P403ConflictTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Original_save_and_same_path_save_as_cannot_bypass_opened_baseline(bool saveAs) => HeadlessTestScope.Run(async () =>
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "p403-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "original.pdf");
        try
        {
            var source = PdfFixture.SinglePage("AAAA"); var external = PdfFixture.SinglePage("BBBB");
            await File.WriteAllBytesAsync(path, source, Token);
            await using var reader = new Reader { IsReadOnly = false };
            await reader.LoadDocumentAsync(path, cancellationToken: Token); reader.AddUserBookmark("Unsaved");
            var time = File.GetLastWriteTimeUtc(path); await File.WriteAllBytesAsync(path, external, Token); File.SetLastWriteTimeUtc(path, time);
            PdfSaveConflictEventArgs? conflict = null; reader.SaveConflict += (_, args) => conflict = args;
            await Assert.ThrowsAsync<PdfSaveConflictException>(() => saveAs ? reader.SaveAsAsync(path, Token) : reader.SaveAsync(Token));
            Assert.True(reader.IsDirty); Assert.True(reader.HasDocument); Assert.NotNull(reader.LastSaveConflict); Assert.NotNull(conflict);
            Assert.Equal(PdfErrorKind.SaveConflict, reader.LastError?.Kind); Assert.Equal(external, await File.ReadAllBytesAsync(path, Token));
            reader.UnsavedChangesService = new SaveBeforeClose();
            Assert.False(await reader.RequestCloseDocumentAsync(Token)); Assert.True(reader.HasDocument);
            var copy = Path.Combine(directory, "copy.pdf"); Assert.True(await reader.SaveAsAsync(copy, Token));
            Assert.False(reader.IsDirty); Assert.Null(reader.LastSaveConflict); Assert.Null(reader.LastError); Assert.Equal(copy, reader.SavePath);
            Assert.Equal(external, await File.ReadAllBytesAsync(path, Token));
        }
        finally { Directory.Delete(directory, true); }
    });

    [Fact]
    public Task AutoSave_conflict_pauses_even_after_new_edits_and_success_rebases_future_saves() => HeadlessTestScope.Run(async () =>
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "p403-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "original.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, PdfFixture.SinglePage("AAAA"), Token);
            await using var reader = new Reader { IsReadOnly = false, AutoSaveInterval = TimeSpan.FromMilliseconds(100) };
            await reader.LoadDocumentAsync(path, cancellationToken: Token);
            var bookmark = reader.AddUserBookmark("A"); Assert.True(await reader.SaveAsync(Token));
            reader.RenameUserBookmark(bookmark.Id, "B"); Assert.True(await reader.SaveAsync(Token)); // Uses the last successful output, not the opened input.
            var external = PdfFixture.SinglePage("BBBB"); await File.WriteAllBytesAsync(path, external, Token);
            var conflicts = 0; reader.SaveConflict += (_, args) => { Assert.True(args.IsAutoSave); conflicts++; };
            reader.AutoSave = true; reader.RenameUserBookmark(bookmark.Id, "C");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
            while (reader.LastSaveConflict is null || reader.IsEditing) await Task.Delay(10, timeout.Token);
            reader.RenameUserBookmark(bookmark.Id, "D"); await Task.Delay(350, Token);
            Assert.Equal(1, conflicts); Assert.True(reader.IsDirty); Assert.Equal(external, await File.ReadAllBytesAsync(path, Token));
        }
        finally { Directory.Delete(directory, true); }
    });

    private sealed class SaveBeforeClose : IPdfUnsavedChangesService
    {
        public Task<PdfUnsavedChangesDecision> ConfirmAsync(PdfUnsavedChangesContext context, CancellationToken cancellationToken = default) => Task.FromResult(PdfUnsavedChangesDecision.Save);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Published_conflict_or_uncertain_durability_pauses_AutoSave_until_file_resolution(bool conflict) => HeadlessTestScope.Run(async () =>
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "p403-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "original.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, PdfFixture.SinglePage("AAAA"), Token);
            await using var reader = new Reader { IsReadOnly = false, AutoSaveInterval = TimeSpan.FromMilliseconds(100) };
            await reader.LoadDocumentAsync(path, cancellationToken: Token);
            var bookmark = reader.AddUserBookmark("Output"); var attempts = 0;
            PdfSaveFaultInjection.Current.Value = async (probe, ct) =>
            {
                if (probe.Stage != PdfFileSaveStage.Replaced) return;
                attempts++;
                if (conflict) await File.WriteAllBytesAsync(path, PdfFixture.SinglePage("BBBB"), ct);
                else throw new IOException("No durability acknowledgement");
            };
            if (conflict) await Assert.ThrowsAsync<PdfSaveConflictException>(() => reader.SaveAsync(Token));
            else await Assert.ThrowsAsync<PdfSaveDurabilityException>(() => reader.SaveAsync(Token));
            Assert.True(reader.IsDirty);
            if (conflict) Assert.True(reader.LastSaveConflict?.WasPublished);
            else Assert.NotNull(reader.LastSaveDurabilityFailure);
            reader.AutoSave = true; reader.RenameUserBookmark(bookmark.Id, "Later edit"); await Task.Delay(350, Token);
            Assert.Equal(1, attempts);
            PdfSaveFaultInjection.Current.Value = null;
            Assert.True(await reader.SaveAsAsync(Path.Combine(directory, "resolved.pdf"), Token));
            Assert.Null(reader.LastSaveConflict); Assert.Null(reader.LastSaveDurabilityFailure); Assert.False(reader.IsDirty);
        }
        finally { PdfSaveFaultInjection.Current.Value = null; Directory.Delete(directory, true); }
    });
}
