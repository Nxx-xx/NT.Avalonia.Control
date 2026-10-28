using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;
using PdfViewer.Tests;

namespace PdfViewer.Core.Tests;

public sealed class P4UndoRedoTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
    private static readonly PdfEditRectangle Bounds = new(15, 90, 250, 45);
    private static readonly PdfEditRectangle OtherBounds = new(15, 160, 250, 45);

    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "P0Font.ttf");
    private static PdfEmbeddedTrueTypeFont Font() => new(File.ReadAllBytes(FontPath));
    private static Task<PdfEditSession> Open() => PdfEditSession.OpenAsync(PdfFixture.SinglePage("Original page", 300, 220), TestToken);

    [Fact]
    public async Task Undo_redo_round_trip_restores_bytes_annotations_and_dirty_state()
    {
        await using var edit = await Open();
        var original = await edit.ExportAsync(TestToken);
        var annotation = await edit.AddFreeTextAsync(0, Bounds, "中文 ABC", Font(), 18, TestToken);
        var edited = await edit.ExportAsync(TestToken);
        Assert.True(edit.CanUndo);
        Assert.False(edit.CanRedo);
        Assert.Equal(1, edit.UndoCount);
        Assert.Equal(original.Length, edit.HistoryBytes);

        await edit.UndoAsync(TestToken);
        Assert.False(edit.CanUndo);
        Assert.True(edit.CanRedo);
        Assert.Equal(0, edit.UndoCount);
        Assert.Equal(1, edit.RedoCount);
        Assert.Equal(original, await edit.ExportAsync(TestToken));
        Assert.Empty(await edit.GetFreeTextAnnotationsAsync(0, TestToken));
        Assert.False(edit.IsDirty);

        await edit.RedoAsync(TestToken);
        Assert.True(edit.CanUndo);
        Assert.False(edit.CanRedo);
        Assert.Equal(edited, await edit.ExportAsync(TestToken));
        Assert.Equal(annotation, Assert.Single(await edit.GetFreeTextAnnotationsAsync(0, TestToken)));
        Assert.True(edit.IsDirty);
    }

    [Fact]
    public async Task New_edit_clears_redo()
    {
        await using var edit = await Open();
        var original = await edit.ExportAsync(TestToken);
        var font = Font();
        await edit.AddFreeTextAsync(0, Bounds, "A", font, 18, TestToken);
        await edit.UndoAsync(TestToken);
        await edit.AddFreeTextAsync(0, Bounds, "B", font, 18, TestToken);
        Assert.False(edit.CanRedo);
        Assert.Equal(0, edit.RedoCount);
        Assert.Equal(1, edit.UndoCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => edit.RedoAsync(TestToken));
        Assert.Equal("B", Assert.Single(await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Text);

        await edit.UndoAsync(TestToken);
        Assert.Empty(await edit.GetFreeTextAnnotationsAsync(0, TestToken));
        Assert.Equal(original, await edit.ExportAsync(TestToken));
        Assert.False(edit.IsDirty);
    }

    [Fact]
    public async Task Update_undo_restores_previous_text()
    {
        await using var edit = await Open();
        var font = Font();
        var annotation = await edit.AddFreeTextAsync(0, Bounds, "A", font, 18, TestToken);
        await edit.UpdateFreeTextAsync(annotation.Id, 0, Bounds, "B", font, 18, TestToken);
        Assert.Equal("B", Assert.Single(await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Text);

        await edit.UndoAsync(TestToken);
        Assert.Equal("A", Assert.Single(await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Text);
        await edit.UndoAsync(TestToken);
        Assert.Empty(await edit.GetFreeTextAnnotationsAsync(0, TestToken));
        await edit.RedoAsync(TestToken);
        Assert.Equal("A", Assert.Single(await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Text);
        await edit.RedoAsync(TestToken);
        Assert.Equal("B", Assert.Single(await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Text);
    }

    [Fact]
    public async Task Undo_count_limit_evicts_oldest_entries()
    {
        await using var edit = await Open();
        edit.UndoLimit = 2;
        var font = Font();
        await edit.AddFreeTextAsync(0, Bounds, "A", font, 18, TestToken);
        await edit.AddFreeTextAsync(0, OtherBounds, "B", font, 18, TestToken);
        await edit.AddFreeTextAsync(0, new PdfEditRectangle(15, 20, 250, 45), "C", font, 18, TestToken);
        Assert.Equal(2, edit.UndoCount);

        await edit.UndoAsync(TestToken);
        Assert.Equal(new[] { "A", "B" }, (await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Select(item => item.Text));
        await edit.UndoAsync(TestToken);
        Assert.Equal(new[] { "A" }, (await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Select(item => item.Text));
        Assert.False(edit.CanUndo);
        Assert.Equal(2, edit.RedoCount);
    }

    [Fact]
    public async Task History_byte_limit_evicts_oldest_and_keeps_newest()
    {
        await using var edit = await PdfEditSession.OpenAsync(PdfFixture.PaddedDocument(200_000), TestToken);
        var original = await edit.ExportAsync(TestToken);
        await edit.AddFreeTextAsync(0, Bounds, "A", Font(), 18, TestToken);
        Assert.Equal(original.Length, edit.HistoryBytes);
        // The next snapshot pushes the total over the budget; the newest entry survives.
        edit.HistoryByteLimit = original.Length + 1;
        await edit.AddFreeTextAsync(0, OtherBounds, "B", Font(), 18, TestToken);
        Assert.Equal(1, edit.UndoCount);
        Assert.True(edit.CanUndo);
        Assert.Equal(2, (await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Count);
        await edit.UndoAsync(TestToken);
        Assert.Equal(new[] { "A" }, (await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Select(item => item.Text));
        Assert.False(edit.CanUndo);
    }

    [Fact]
    public async Task Lowering_limits_trims_immediately_and_values_clamp()
    {
        await using var edit = await Open();
        var font = Font();
        await edit.AddFreeTextAsync(0, Bounds, "A", font, 18, TestToken);
        await edit.AddFreeTextAsync(0, OtherBounds, "B", font, 18, TestToken);
        await edit.AddFreeTextAsync(0, new PdfEditRectangle(15, 20, 250, 45), "C", font, 18, TestToken);
        Assert.Equal(3, edit.UndoCount);
        edit.UndoLimit = 1;
        Assert.Equal(1, edit.UndoCount);
        Assert.True(edit.CanUndo);
        edit.UndoLimit = 0;
        Assert.Equal(1, edit.UndoLimit);
        edit.HistoryByteLimit = 1;
        Assert.Equal(PdfEditSession.MinimumHistoryByteLimit, edit.HistoryByteLimit);
        Assert.Equal(1, edit.UndoCount);
        Assert.True(edit.HistoryBytes > 0);
    }

    [Fact]
    public async Task Dirty_is_correct_across_stream_and_file_save_points()
    {
        await using var edit = await Open();
        var font = Font();
        await edit.AddFreeTextAsync(0, Bounds, "A", font, 18, TestToken);
        Assert.True(edit.IsDirty);
        using var saved = new MemoryStream();
        await edit.SaveAsync(saved, TestToken);
        Assert.False(edit.IsDirty);

        await edit.AddFreeTextAsync(0, OtherBounds, "B", font, 18, TestToken);
        Assert.True(edit.IsDirty);
        await edit.UndoAsync(TestToken);
        Assert.False(edit.IsDirty, "Undoing back to the saved state must clear IsDirty.");
        Assert.Equal(saved.ToArray(), await edit.ExportAsync(TestToken));
        await edit.RedoAsync(TestToken);
        Assert.True(edit.IsDirty);
        await edit.UndoAsync(TestToken);

        var directory = CreateTestDirectory();
        try
        {
            var path = Path.Combine(directory, "saved.pdf");
            await edit.SaveAsync(path, TestToken);
            Assert.False(edit.IsDirty);
            await edit.RedoAsync(TestToken);
            Assert.True(edit.IsDirty);
            await edit.UndoAsync(TestToken);
            Assert.False(edit.IsDirty);
        }
        finally { DeleteTestDirectory(directory); }
    }

    [Fact]
    public async Task Undo_and_redo_bump_revision()
    {
        await using var edit = await Open();
        await edit.AddFreeTextAsync(0, Bounds, "A", Font(), 18, TestToken);
        Assert.Equal(1, edit.Revision);
        await edit.UndoAsync(TestToken);
        Assert.Equal(2, edit.Revision);
        await edit.RedoAsync(TestToken);
        Assert.Equal(3, edit.Revision);
    }

    [Fact]
    public async Task Undo_and_redo_with_empty_history_throw()
    {
        await using var edit = await Open();
        var original = await edit.ExportAsync(TestToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => edit.UndoAsync(TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => edit.RedoAsync(TestToken));
        Assert.Equal(original, await edit.ExportAsync(TestToken));
        Assert.Equal(0, edit.Revision);
        Assert.False(edit.IsDirty);
    }

    [Fact]
    public async Task Precancelled_undo_leaves_session_unchanged()
    {
        await using var edit = await Open();
        await edit.AddFreeTextAsync(0, Bounds, "A", Font(), 18, TestToken);
        var expected = await edit.ExportAsync(TestToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => edit.UndoAsync(cancellation.Token));
        Assert.True(edit.CanUndo);
        Assert.Equal(1, edit.Revision);
        Assert.Equal(expected, await edit.ExportAsync(TestToken));
    }

    [Fact]
    public async Task Undo_restores_rendered_appearance()
    {
        await using var edit = await Open();
        await edit.AddFreeTextAsync(0, Bounds, "中文 ABC", Font(), 18, TestToken);
        var edited = await edit.ExportAsync(TestToken);
        using (var reader = await PdfDocument.LoadAsync(edited, cancellationToken: TestToken))
        {
            var image = await reader.RenderAsync(0, 300, 220, cancellationToken: TestToken);
            Assert.True(HasDarkPixel(image.Pixels, image.Stride, 15, 85, 270, 140));
        }
        await edit.UndoAsync(TestToken);
        using (var reader = await PdfDocument.LoadAsync(await edit.ExportAsync(TestToken), cancellationToken: TestToken))
        {
            var image = await reader.RenderAsync(0, 300, 220, cancellationToken: TestToken);
            Assert.False(HasDarkPixel(image.Pixels, image.Stride, 15, 85, 270, 140));
        }
    }

    [Fact]
    public async Task Transaction_groups_operations_into_one_entry()
    {
        await using var edit = await Open();
        var original = await edit.ExportAsync(TestToken);
        var font = Font();
        await edit.ExecuteTransactionAsync(async _ =>
        {
            await edit.AddFreeTextAsync(0, Bounds, "A", font, 18, TestToken);
            await edit.AddFreeTextAsync(0, OtherBounds, "B", font, 18, TestToken);
        }, TestToken);
        Assert.Equal(1, edit.UndoCount);
        Assert.Equal(2, (await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Count);
        Assert.Equal(2, edit.Revision);

        await edit.UndoAsync(TestToken);
        Assert.Empty(await edit.GetFreeTextAnnotationsAsync(0, TestToken));
        Assert.Equal(original, await edit.ExportAsync(TestToken));
        Assert.False(edit.IsDirty);
        await edit.RedoAsync(TestToken);
        Assert.Equal(2, (await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Count);
        Assert.True(edit.IsDirty);
    }

    [Fact]
    public async Task Transaction_failure_rolls_back_bytes_revision_and_history()
    {
        await using var edit = await Open();
        var snapshot = await edit.ExportAsync(TestToken);
        var font = Font();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => edit.ExecuteTransactionAsync(async _ =>
        {
            await edit.AddFreeTextAsync(0, Bounds, "A", font, 18, TestToken);
            await edit.UpdateFreeTextAsync(Guid.NewGuid(), 0, Bounds, "B", font, 18, TestToken);
        }, TestToken));
        Assert.Equal(0, edit.Revision);
        Assert.False(edit.IsDirty);
        Assert.Equal(0, edit.UndoCount);
        Assert.False(edit.CanUndo);
        Assert.Equal(snapshot, await edit.ExportAsync(TestToken));

        await edit.AddFreeTextAsync(0, Bounds, "C", font, 18, TestToken);
        Assert.Equal(1, edit.UndoCount);
        Assert.Equal(1, edit.Revision);
    }

    [Fact]
    public async Task Transaction_without_changes_records_nothing()
    {
        await using var edit = await Open();
        await edit.ExecuteTransactionAsync(async _ =>
        {
            Assert.Empty(await edit.GetFreeTextAnnotationsAsync(0, TestToken));
        }, TestToken);
        Assert.Equal(0, edit.UndoCount);
        Assert.Equal(0, edit.Revision);
        Assert.False(edit.IsDirty);
        Assert.False(edit.CanUndo);
    }

    [Fact]
    public async Task Nested_transaction_is_rejected()
    {
        await using var edit = await Open();
        var snapshot = await edit.ExportAsync(TestToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => edit.ExecuteTransactionAsync(async _ =>
        {
            await edit.ExecuteTransactionAsync(_ => Task.CompletedTask, TestToken);
        }, TestToken));
        Assert.Equal(snapshot, await edit.ExportAsync(TestToken));
        Assert.Equal(0, edit.Revision);
        Assert.False(edit.IsDirty);
    }

    [Fact]
    public async Task Undo_redo_save_and_bookmark_changes_are_rejected_inside_transactions()
    {
        await using var edit = await Open();
        var snapshot = await edit.ExportAsync(TestToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => edit.ExecuteTransactionAsync(
            _ => edit.UndoAsync(TestToken), TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => edit.ExecuteTransactionAsync(
            _ => edit.RedoAsync(TestToken), TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => edit.ExecuteTransactionAsync(
            _ => edit.SetUserBookmarksAsync([new PdfEditUserBookmark(Guid.NewGuid(), "S", 0, PdfUserBookmarkFlags.None)], TestToken), TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => edit.ExecuteTransactionAsync(
            async _ => { using var output = new MemoryStream(); await edit.SaveAsync(output, TestToken); }, TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => edit.ExecuteTransactionAsync(_ =>
        {
            edit.UndoLimit = 2;
            return Task.CompletedTask;
        }, TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => edit.ExecuteTransactionAsync(_ =>
        {
            edit.HistoryByteLimit = 1024 * 1024;
            return Task.CompletedTask;
        }, TestToken));
        Assert.Equal(snapshot, await edit.ExportAsync(TestToken));
        Assert.Equal(0, edit.Revision);
        Assert.False(edit.IsDirty);
        Assert.Empty(await edit.GetUserBookmarksAsync(TestToken));
    }

    [Fact]
    public async Task Sanitize_records_history_clears_bookmarks_and_undo_restores_original()
    {
        await using var edit = await Open();
        var original = await edit.ExportAsync(TestToken);
        await edit.SetUserBookmarksAsync([new PdfEditUserBookmark(Guid.NewGuid(), "S", 0, PdfUserBookmarkFlags.Bold)], TestToken);
        Assert.True(edit.IsDirty);

        await edit.SanitizeAsync(PdfHiddenInformationPolicy.RemoveAllNonPageData, TestToken);
        Assert.Equal(1, edit.UndoCount);
        Assert.Empty(await edit.GetUserBookmarksAsync(TestToken));
        Assert.True(edit.IsDirty);
        Assert.Equal(2, edit.Revision);
        var sanitized = await edit.ExportAsync(TestToken);
        Assert.NotEqual(original, sanitized);

        await edit.UndoAsync(TestToken);
        Assert.Equal(original, await edit.ExportAsync(TestToken));
        Assert.Empty(await edit.GetUserBookmarksAsync(TestToken));
        Assert.False(edit.IsDirty);
        await edit.RedoAsync(TestToken);
        Assert.Equal(sanitized, await edit.ExportAsync(TestToken));
        Assert.Empty(await edit.GetUserBookmarksAsync(TestToken));
    }

    [Fact]
    public async Task Redaction_records_history_and_undo_restores_original()
    {
        await using var edit = await PdfEditSession.OpenAsync(PdfFixture.ContentBoxDocument(), TestToken);
        var original = await edit.ExportAsync(TestToken);
        var regions = new[]
        {
            new PdfRedactionRegion(0, new PdfEditRectangle(10, 90, 80, 60)),
            new PdfRedactionRegion(0, new PdfEditRectangle(90, 190, 70, 70)),
        };
        var result = await edit.ApplyRedactionsAsync(regions, PdfHiddenInformationPolicy.RemoveAllNonPageData, TestToken);
        Assert.Equal(1, edit.UndoCount);
        Assert.Equal(2, result.RedactionRegionCount);
        Assert.NotEqual(original, await edit.ExportAsync(TestToken));

        await edit.UndoAsync(TestToken);
        Assert.Equal(original, await edit.ExportAsync(TestToken));
        Assert.False(edit.IsDirty);
        await edit.RedoAsync(TestToken);
        Assert.NotEqual(original, await edit.ExportAsync(TestToken));
    }

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "p4-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTestDirectory(string directory)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "p4-test")) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(directory);
        if (!target.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("The test directory is outside its owned cleanup root.");
        Directory.Delete(target, true);
    }

    private static bool HasDarkPixel(byte[] pixels, int stride, int left, int top, int right, int bottom)
    {
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var offset = y * stride + x * 4;
            if (pixels[offset] < 100 && pixels[offset + 1] < 100 && pixels[offset + 2] < 100) return true;
        }
        return false;
    }
}
