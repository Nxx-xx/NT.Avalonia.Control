using System.Globalization;
using System.Text;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;
using PdfViewer.Tests;

namespace PdfViewer.Core.Tests;

public sealed class P4BookmarkPersistenceTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
    private static Task<PdfEditSession> Open() => PdfEditSession.OpenAsync(PdfFixture.SinglePage("Original page", 300, 220), TestToken);

    private static readonly PdfEditUserBookmark First = new(Guid.NewGuid(), "第一章 概览", 0, PdfUserBookmarkFlags.Bold);
    private static readonly PdfEditUserBookmark Second = new(Guid.NewGuid(), "Second page note", 0, PdfUserBookmarkFlags.Bold | PdfUserBookmarkFlags.Italic);

    [Fact]
    public async Task Bookmark_set_export_and_reopen_round_trip()
    {
        await using var edit = await Open();
        await edit.SetUserBookmarksAsync([First, Second], TestToken);
        Assert.True(edit.IsDirty);
        Assert.Equal(1, edit.Revision);
        Assert.Equal(0, edit.UndoCount);

        var exported = await edit.ExportAsync(TestToken);
        Assert.True(ContainsMarker(exported));
        using (var reader = await PdfDocument.LoadAsync(exported, cancellationToken: TestToken))
        {
            Assert.Single(reader.Pages);
            Assert.Contains("Original page", (await reader.GetTextAsync(0, TestToken)).Text);
        }
        await using var reopened = await PdfEditSession.OpenAsync(exported, TestToken);
        Assert.False(reopened.IsDirty);
        Assert.Equal([First, Second], await reopened.GetUserBookmarksAsync(TestToken));

        // A clean session never injects a block.
        await using var clean = await Open();
        Assert.False(ContainsMarker(await clean.ExportAsync(TestToken)));
    }

    [Fact]
    public async Task Injected_output_is_byte_identical_across_repeat_exports_and_saves_of_one_state()
    {
        // PDFsharp 6.2.4 regenerates XMP document IDs on every structural save, so byte identity
        // across serialization passes only holds for one session state; the session caches the
        // injected array per state to guarantee it.
        await using var edit = await Open();
        await edit.SetUserBookmarksAsync([First, Second], TestToken);
        Assert.Equal(await edit.ExportAsync(TestToken), await edit.ExportAsync(TestToken));
        using var saved = new MemoryStream();
        await edit.SaveAsync(saved, TestToken);
        Assert.Equal(saved.ToArray(), await edit.ExportAsync(TestToken));
        // A bookmark change re-injects, so the new state's output is a fresh array (still self-consistent).
        await edit.SetUserBookmarksAsync([First], TestToken);
        Assert.Equal(await edit.ExportAsync(TestToken), await edit.ExportAsync(TestToken));
        Assert.True(ContainsMarker(await edit.ExportAsync(TestToken)));
    }

    [Fact]
    public async Task Bookmark_save_to_file_persists_and_reads_back()
    {
        var directory = CreateTestDirectory();
        try
        {
            var path = Path.Combine(directory, "bookmarked.pdf");
            await using var edit = await Open();
            await edit.SetUserBookmarksAsync([First], TestToken);
            await edit.SaveAsync(path, TestToken);
            Assert.False(edit.IsDirty);
            Assert.True(ContainsMarker(await File.ReadAllBytesAsync(path, TestToken)));
            await using var reopened = await PdfEditSession.OpenAsync(await File.ReadAllBytesAsync(path, TestToken), TestToken);
            Assert.False(reopened.IsDirty);
            Assert.Equal([First], await reopened.GetUserBookmarksAsync(TestToken));
        }
        finally { DeleteTestDirectory(directory); }
    }

    [Fact]
    public async Task Bookmarks_survive_undo_and_redo_without_entering_history()
    {
        await using var edit = await Open();
        var font = Font();
        await edit.AddFreeTextAsync(0, Bounds, "中文 ABC", font, 18, TestToken);
        Assert.Equal(1, edit.UndoCount);
        await edit.SetUserBookmarksAsync([First, Second], TestToken);
        Assert.Equal(1, edit.UndoCount);

        await edit.UndoAsync(TestToken);
        Assert.Empty(await edit.GetFreeTextAnnotationsAsync(0, TestToken));
        Assert.Equal([First, Second], await edit.GetUserBookmarksAsync(TestToken));
        var exported = await edit.ExportAsync(TestToken);
        Assert.True(ContainsMarker(exported));

        await edit.RedoAsync(TestToken);
        Assert.Single(await edit.GetFreeTextAnnotationsAsync(0, TestToken));
        Assert.Equal([First, Second], await edit.GetUserBookmarksAsync(TestToken));

        await edit.SetUserBookmarksAsync([], TestToken);
        Assert.Empty(await edit.GetUserBookmarksAsync(TestToken));
        Assert.False(ContainsMarker(await edit.ExportAsync(TestToken)));
        Assert.Equal(1, edit.UndoCount);
    }

    [Fact]
    public async Task Identical_bookmark_set_is_a_noop_and_dirty_tracks_saved_output()
    {
        await using var edit = await Open();
        await edit.SetUserBookmarksAsync([First], TestToken);
        Assert.Equal(1, edit.Revision);
        Assert.True(edit.IsDirty);
        await edit.SetUserBookmarksAsync([First], TestToken);
        Assert.Equal(1, edit.Revision);
        Assert.True(edit.IsDirty);

        using var saved = new MemoryStream();
        await edit.SaveAsync(saved, TestToken);
        Assert.False(edit.IsDirty);
        Assert.Equal(1, edit.Revision);
        await edit.SetUserBookmarksAsync([First], TestToken);
        Assert.False(edit.IsDirty, "Setting the same saved set must not dirty the session.");
        Assert.Equal(1, edit.Revision);

        await edit.SetUserBookmarksAsync([First, Second], TestToken);
        Assert.True(edit.IsDirty);
        Assert.Equal(2, edit.Revision);
    }

    [Fact]
    public async Task Bookmark_validation_rejects_invalid_input_before_commit()
    {
        await using var edit = await Open();
        var expected = await edit.ExportAsync(TestToken);
        var good = new PdfEditUserBookmark(Guid.NewGuid(), "T", 0, PdfUserBookmarkFlags.None);
        await Assert.ThrowsAsync<ArgumentNullException>(() => edit.SetUserBookmarksAsync(null!, TestToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => edit.SetUserBookmarksAsync([good, null!], TestToken));
        await Assert.ThrowsAsync<ArgumentException>(() => edit.SetUserBookmarksAsync([good with { Id = Guid.Empty }], TestToken));
        await Assert.ThrowsAsync<ArgumentException>(() => edit.SetUserBookmarksAsync([good, good], TestToken));
        await Assert.ThrowsAsync<ArgumentException>(() => edit.SetUserBookmarksAsync([good with { Title = "  " }], TestToken));
        await Assert.ThrowsAsync<ArgumentException>(() => edit.SetUserBookmarksAsync([good with { Title = new string('长', 513) }], TestToken));
        await Assert.ThrowsAsync<ArgumentException>(() => edit.SetUserBookmarksAsync([good with { Title = "A\0B" }], TestToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => edit.SetUserBookmarksAsync([good with { Flags = (PdfUserBookmarkFlags)4 }], TestToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => edit.SetUserBookmarksAsync([good with { PageIndex = 1 }], TestToken));
        Assert.Equal(0, edit.Revision);
        Assert.False(edit.IsDirty);
        Assert.Empty(await edit.GetUserBookmarksAsync(TestToken));
        Assert.Equal(expected, await edit.ExportAsync(TestToken));
    }

    [Fact]
    public async Task Persisted_block_is_stripped_from_content_bytes_at_open()
    {
        await using var writer = await Open();
        var annotation = await writer.AddFreeTextAsync(0, Bounds, "中文 ABC", Font(), 18, TestToken);
        await writer.SetUserBookmarksAsync([First, Second], TestToken);
        var withBlock = await writer.ExportAsync(TestToken);

        await using var edit = await PdfEditSession.OpenAsync(withBlock, TestToken);
        Assert.Equal([First, Second], await edit.GetUserBookmarksAsync(TestToken));
        Assert.Equal(annotation, Assert.Single(await edit.GetFreeTextAnnotationsAsync(0, TestToken)));
        // Clearing the overlay set must remove the block entirely: it lives in the session overlay,
        // not in the content snapshots a later undo could restore.
        await edit.SetUserBookmarksAsync([], TestToken);
        Assert.False(ContainsMarker(await edit.ExportAsync(TestToken)));
        await edit.AddFreeTextAsync(0, OtherBounds, "B", Font(), 18, TestToken);
        await edit.UndoAsync(TestToken);
        Assert.False(ContainsMarker(await edit.ExportAsync(TestToken)));
    }

    [Fact]
    public async Task Malformed_persisted_blocks_are_rejected_at_open()
    {
        var id = "11111111-1111-1111-1111-111111111111";
        var blocks = new[]
        {
            "(not an array)",
            "[ (just a string) ]",
            $"[ << /Title (T) /Page 0 /Flags 0 >> ]",
            $"[ << /Id (not-a-guid) /Title (T) /Page 0 /Flags 0 >> ]",
            $"[ << /Id ({id}) /Title (T) /Page 0 /Flags 0 >> << /Id ({id}) /Title (U) /Page 0 /Flags 0 >> ]",
            $"[ << /Id ({id}) /Title () /Page 0 /Flags 0 >> ]",
            $"[ << /Id ({id}) /Title (T) /Page -1 /Flags 0 >> ]",
            $"[ << /Id ({id}) /Title (T) /Page 0 >> ]",
            $"[ << /Id ({id}) /Title (T) /Page 0 /Flags (two) >> ]",
            $"[ << /Id ({id}) /Title (T) /Page 5 /Flags 0 >> ]",
        };
        foreach (var block in blocks)
            await Assert.ThrowsAsync<InvalidDataException>(() => PdfEditSession.OpenAsync(BookmarkDocument(block), TestToken));
    }

    [Fact]
    public async Task Valid_persisted_block_reads_back_on_open()
    {
        var first = new PdfEditUserBookmark(new Guid("11111111-1111-1111-1111-111111111111"), "First", 0, PdfUserBookmarkFlags.Bold | PdfUserBookmarkFlags.Italic);
        var second = new PdfEditUserBookmark(new Guid("22222222-2222-2222-2222-222222222222"), "Second", 0, PdfUserBookmarkFlags.Bold);
        var block = $"[ << /Id ({first.Id:D}) /Title (First) /Page 0 /Flags 3 >> << /Id ({second.Id:D}) /Title (Second) /Page 0 /Flags 1 >> ]";
        await using var edit = await PdfEditSession.OpenAsync(BookmarkDocument(block), TestToken);
        Assert.False(edit.IsDirty);
        Assert.Equal([first, second], await edit.GetUserBookmarksAsync(TestToken));
        // The normalized content snapshot never carries the block.
        await edit.SetUserBookmarksAsync([], TestToken);
        Assert.False(ContainsMarker(await edit.ExportAsync(TestToken)));
    }

    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "P0Font.ttf");
    private static PdfEmbeddedTrueTypeFont Font() => new(File.ReadAllBytes(FontPath));
    private static readonly PdfEditRectangle Bounds = new(15, 90, 250, 45);
    private static readonly PdfEditRectangle OtherBounds = new(15, 160, 250, 45);

    private static bool ContainsMarker(byte[] bytes) => bytes.AsSpan().IndexOf("NTUserBookmarks"u8) >= 0;

    private static byte[] BookmarkDocument(string block)
    {
        const string content = "BT /F1 14 Tf 20 250 Td (Bookmark fixture) Tj ET\n";
        var objects = new[]
        {
            $"<< /Type /Catalog /Pages 2 0 R /NTUserBookmarks {block} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{content}endstream",
        };
        using var output = new MemoryStream();
        void Append(string value) => output.Write(Encoding.Latin1.GetBytes(value));
        Append("%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(output.Position);
            Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = output.Position;
        Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Append(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "p4-bookmark-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTestDirectory(string directory)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "p4-bookmark-test")) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(directory);
        if (!target.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("The test directory is outside its owned cleanup root.");
        Directory.Delete(target, true);
    }
}
