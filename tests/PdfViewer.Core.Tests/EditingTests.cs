using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;

namespace PdfViewer.Core.Tests;

public sealed class EditingTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
    private static readonly PdfEditRectangle Bounds = new(15, 90, 250, 45);

    [Fact]
    public async Task Cjk_FreeText_round_trips_updates_the_same_ID_and_renders_after_reopening()
    {
        await using var edit = await Open();
        var font = Font();
        var annotation = await edit.AddFreeTextAsync(0, Bounds, "中文批注 ABC 123", font, 18, TestToken);
        Assert.Equal(1, edit.Revision);
        Assert.True(edit.IsDirty);
        Assert.Equal(annotation, Assert.Single(await edit.GetFreeTextAnnotationsAsync(0, TestToken)));
        var exported = await edit.ExportAsync(TestToken);
        using (var reader = await PdfDocument.LoadAsync(exported, cancellationToken: TestToken))
        {
            Assert.Single(reader.Pages);
            Assert.Contains("Original page", (await reader.GetTextAsync(0, TestToken)).Text);
            var image = await reader.RenderAsync(0, 300, 220, cancellationToken: TestToken);
            Assert.True(HasDarkPixel(image.Pixels, image.Stride, 15, 85, 270, 140), "The FreeText AP must paint visible glyphs in its rectangle.");
        }
        await using var reopened = await PdfEditSession.OpenAsync(exported, TestToken);
        Assert.False(reopened.IsDirty);
        Assert.Equal(annotation, Assert.Single(await reopened.GetFreeTextAnnotationsAsync(0, TestToken)));
        var updated = await reopened.UpdateFreeTextAsync(annotation.Id, 0, Bounds, "中文批注 编辑后 ABC", font, 18, TestToken);
        Assert.Equal(annotation.Id, updated.Id);
        Assert.Equal(1, reopened.Revision);
        var secondBytes = await reopened.ExportAsync(TestToken);
        await using var second = await PdfEditSession.OpenAsync(secondBytes, TestToken);
        Assert.Equal(updated, Assert.Single(await second.GetFreeTextAnnotationsAsync(0, TestToken)));
        using var secondReader = await PdfDocument.LoadAsync(secondBytes, cancellationToken: TestToken);
        var secondImage = await secondReader.RenderAsync(0, 300, 220, cancellationToken: TestToken);
        Assert.True(HasDarkPixel(secondImage.Pixels, secondImage.Stride, 15, 85, 270, 140));
    }

    [Fact]
    public async Task Input_PDF_font_and_export_arrays_are_owned_copies()
    {
        var source = PdfFixture.SinglePage("Original page", 300, 220);
        var original = (byte[])source.Clone();
        await using var edit = await PdfEditSession.OpenAsync(source, TestToken);
        Array.Fill(source, (byte)0);
        Assert.Equal(original, await edit.ExportAsync(TestToken));
        var fontData = File.ReadAllBytes(FontPath);
        var font = new PdfEmbeddedTrueTypeFont(fontData);
        Array.Fill(fontData, (byte)0);
        await edit.AddFreeTextAsync(0, Bounds, "中文 ABC", font, 18, TestToken);
        var first = await edit.ExportAsync(TestToken);
        var expected = (byte[])first.Clone();
        Array.Fill(first, (byte)0);
        Assert.Equal(expected, await edit.ExportAsync(TestToken));
        Assert.True(edit.IsDirty);
    }

    [Fact]
    public async Task Invalid_font_and_missing_glyph_leave_the_snapshot_and_revision_unchanged()
    {
        await using var edit = await Open();
        var snapshot = await edit.ExportAsync(TestToken);
        await Assert.ThrowsAsync<ArgumentException>(() => edit.AddFreeTextAsync(0, Bounds, "ABC", new PdfEmbeddedTrueTypeFont(new byte[32]), 18, TestToken));
        var missing = await Assert.ThrowsAsync<ArgumentException>(() => edit.AddFreeTextAsync(0, Bounds, "Z", Font(), 18, TestToken));
        Assert.Contains("U+005A", missing.Message);
        await AssertUnchanged(edit, snapshot);
    }

    [Theory]
    [InlineData(2)] // Restricted license embedding.
    [InlineData(4)] // Preview and print only, not editable embedding.
    [InlineData(512)] // Bitmap embedding only.
    public async Task Font_embedding_restrictions_are_enforced_before_editing(int permissions)
    {
        await using var edit = await Open();
        var expected = await edit.ExportAsync(TestToken);
        var font = File.ReadAllBytes(FontPath);
        var tableCount = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4, 2));
        var found = false;
        for (var index = 0; index < tableCount; index++)
        {
            var entry = 12 + index * 16;
            if (!font.AsSpan(entry, 4).SequenceEqual("OS/2"u8)) continue;
            var offset = checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(entry + 8, 4)));
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(offset + 8, 2), (ushort)permissions);
            found = true;
            break;
        }
        Assert.True(found);
        await Assert.ThrowsAsync<ArgumentException>(() => edit.AddFreeTextAsync(0, Bounds, "ABC", new PdfEmbeddedTrueTypeFont(font), 18, TestToken));
        await AssertUnchanged(edit, expected);
    }

    [Fact]
    public async Task Invalid_UTF16_multiline_shaping_and_clipping_are_rejected_before_commit()
    {
        await using var edit = await Open();
        var snapshot = await edit.ExportAsync(TestToken);
        var font = Font();
        await Assert.ThrowsAsync<ArgumentException>(() => edit.AddFreeTextAsync(0, Bounds, "A\uD800", font, 18, TestToken));
        foreach (var text in new[] { "A\nB", "العربية", "A\u0301", "A\u200FB" })
            await Assert.ThrowsAsync<NotSupportedException>(() => edit.AddFreeTextAsync(0, Bounds, text, font, 18, TestToken));
        await Assert.ThrowsAsync<ArgumentException>(() => edit.AddFreeTextAsync(0, new PdfEditRectangle(0, 0, 3, 3), "中文", font, 18, TestToken));
        await AssertUnchanged(edit, snapshot);
    }

    [Fact]
    public async Task Export_does_not_mark_saved_but_successful_stream_save_does()
    {
        await using var edit = await Open();
        await edit.AddFreeTextAsync(0, Bounds, "中文 ABC", Font(), 18, TestToken);
        var expected = await edit.ExportAsync(TestToken);
        Assert.True(edit.IsDirty);
        using var destination = new MemoryStream();
        destination.WriteByte(42);
        await edit.SaveAsync(destination, TestToken);
        Assert.True(destination.CanWrite);
        Assert.Equal(expected, destination.ToArray()[1..]);
        Assert.False(edit.IsDirty);
        Assert.Equal(1, edit.Revision);
    }

    [Fact]
    public async Task Stream_write_or_flush_failure_retains_dirty_snapshot_and_revision()
    {
        await using var edit = await Open();
        await edit.AddFreeTextAsync(0, Bounds, "中文 ABC", Font(), 18, TestToken);
        var expected = await edit.ExportAsync(TestToken);
        foreach (var failFlush in new[] { false, true })
        {
            using var destination = new FailureStream(failFlush);
            await Assert.ThrowsAsync<IOException>(() => edit.SaveAsync(destination, TestToken));
            Assert.True(edit.IsDirty);
            Assert.Equal(1, edit.Revision);
            Assert.Equal(expected, await edit.ExportAsync(TestToken));
        }
    }

    [Fact]
    public async Task Precancelled_edit_export_and_save_do_not_mutate_the_session_or_destination()
    {
        await using var edit = await Open();
        await edit.AddFreeTextAsync(0, Bounds, "中文 ABC", Font(), 18, TestToken);
        var expected = await edit.ExportAsync(TestToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => edit.AddFreeTextAsync(0, Bounds, "ABC", Font(), 18, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => edit.ExportAsync(cancellation.Token));
        using var output = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => edit.SaveAsync(output, cancellation.Token));
        Assert.Empty(output.ToArray());
        Assert.True(edit.IsDirty);
        Assert.Equal(1, edit.Revision);
        Assert.Equal(expected, await edit.ExportAsync(TestToken));
    }

    [Fact]
    public async Task Atomic_file_save_replaces_target_and_cleans_temporary_files()
    {
        var directory = CreateTestDirectory();
        try
        {
            var path = Path.Combine(directory, "saved.pdf");
            await File.WriteAllTextAsync(path, "previous file", TestToken);
            await using var edit = await Open();
            var annotation = await edit.AddFreeTextAsync(0, Bounds, "中文 ABC", Font(), 18, TestToken);
            await edit.SaveAsync(path, TestToken);
            Assert.False(edit.IsDirty);
            Assert.Equal(new[] { path }, Directory.GetFiles(directory).Where(file => !Path.GetFileName(file).StartsWith(".nt-pdf-save-", StringComparison.Ordinal)));
            // Coordination inodes survive saves to avoid splitting cooperating Unix writers.
            // They contain no document data and are separate from temporary transactions.
            var coordination = Assert.Single(Directory.GetFiles(directory, ".nt-pdf-save-*.lock"));
            Assert.Equal(0, new FileInfo(coordination).Length);
            Assert.Empty(Directory.GetDirectories(directory));
            await using var reopened = await PdfEditSession.OpenAsync(await File.ReadAllBytesAsync(path, TestToken), TestToken);
            Assert.Equal(annotation, Assert.Single(await reopened.GetFreeTextAnnotationsAsync(0, TestToken)));
        }
        finally { DeleteTestDirectory(directory); }
    }

    [Fact]
    public async Task Failed_file_save_preserves_target_and_session_and_removes_temp_file()
    {
        var directory = CreateTestDirectory();
        try
        {
            // A directory is an invalid file target on both Windows and Linux.
            var path = Path.Combine(directory, "existing-target");
            Directory.CreateDirectory(path);
            var sentinel = Path.Combine(path, "keep.txt");
            await File.WriteAllTextAsync(sentinel, "keep", TestToken);
            await using var edit = await Open();
            await edit.AddFreeTextAsync(0, Bounds, "中文 ABC", Font(), 18, TestToken);
            var expected = await edit.ExportAsync(TestToken);
            var failure = await Record.ExceptionAsync(() => edit.SaveAsync(path, TestToken));
            Assert.True(failure is IOException or UnauthorizedAccessException, $"Unexpected I/O failure: {failure}");
            Assert.Equal("keep", await File.ReadAllTextAsync(sentinel, TestToken));
            Assert.Empty(Directory.GetFiles(directory));
            Assert.True(edit.IsDirty);
            Assert.Equal(1, edit.Revision);
            Assert.Equal(expected, await edit.ExportAsync(TestToken));
        }
        finally { DeleteTestDirectory(directory); }
    }

    [Fact]
    public async Task Invalid_page_rectangle_and_unknown_annotation_do_not_commit()
    {
        await using var edit = await Open();
        var expected = await edit.ExportAsync(TestToken);
        var font = Font();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => edit.AddFreeTextAsync(1, Bounds, "ABC", font, 18, TestToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => edit.AddFreeTextAsync(0, new PdfEditRectangle(double.NaN, 0, 10, 10), "ABC", font, 18, TestToken));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => edit.UpdateFreeTextAsync(Guid.NewGuid(), 0, Bounds, "ABC", font, 18, TestToken));
        await AssertUnchanged(edit, expected);
    }

    [Fact]
    public async Task Encrypted_PDF_is_rejected_instead_of_silently_removing_security()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => PdfEditSession.OpenAsync(PdfFixture.PasswordProtected(), TestToken));
    }

    [Fact]
    public async Task Adding_FreeText_preserves_inherited_page_font_and_image_resources()
    {
        await using var edit = await PdfEditSession.OpenAsync(InheritedResourcesFixture(), TestToken);
        await edit.AddFreeTextAsync(0, Bounds, "中文 ABC", Font(), 18, TestToken);
        using var reader = await PdfDocument.LoadAsync(await edit.ExportAsync(TestToken), cancellationToken: TestToken);
        Assert.Contains("Inherited original", (await reader.GetTextAsync(0, TestToken)).Text);
        var rendered = await reader.RenderAsync(0, 300, 220, cancellationToken: TestToken);
        var offset = 195 * rendered.Stride + 270 * 4;
        Assert.InRange(rendered.Pixels[offset], 220, 255);
        Assert.InRange(rendered.Pixels[offset + 1], 0, 30);
        Assert.InRange(rendered.Pixels[offset + 2], 0, 30);
        Assert.True(HasDarkPixel(rendered.Pixels, rendered.Stride, 15, 85, 270, 140));
    }

    [Fact]
    public async Task Disposed_edit_session_refuses_export_and_mutation()
    {
        var edit = await Open();
        await edit.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => edit.ExportAsync(TestToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => edit.AddFreeTextAsync(0, Bounds, "ABC", Font(), 18, TestToken));
    }

    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "P0Font.ttf");
    private static PdfEmbeddedTrueTypeFont Font() => new(File.ReadAllBytes(FontPath));
    private static Task<PdfEditSession> Open() => PdfEditSession.OpenAsync(PdfFixture.SinglePage("Original page", 300, 220), TestToken);
    private static async Task AssertUnchanged(PdfEditSession edit, byte[] expected)
    {
        Assert.Equal(0, edit.Revision);
        Assert.False(edit.IsDirty);
        Assert.Equal(expected, await edit.ExportAsync(TestToken));
        Assert.Empty(await edit.GetFreeTextAnnotationsAsync(0, TestToken));
    }

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "edit-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTestDirectory(string directory)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "edit-test")) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(directory);
        if (!target.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("The test directory is outside its owned cleanup root.");
        Directory.Delete(target, true);
    }

    private static byte[] InheritedResourcesFixture()
    {
        const string content = "BT /F1 14 Tf 20 180 Td (Inherited original) Tj ET\nq 30 0 0 30 260 10 cm /Im Do Q\n";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /Resources << /Font << /F1 4 0 R >> /XObject << /Im 6 0 R >> >> >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 220] /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{content}endstream",
            "<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode /Length 7 >>\nstream\n0000FF>\nendstream",
        };
        using var output = new MemoryStream();
        void Write(string value) => output.Write(System.Text.Encoding.ASCII.GetBytes(value));
        Write("%PDF-1.4\n");
        var positions = new List<long>();
        for (var index = 0; index < objects.Length; index++)
        {
            positions.Add(output.Position);
            Write($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = output.Position;
        Write($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var position in positions) Write(position.ToString("D10", System.Globalization.CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Root 1 0 R /Size {objects.Length + 1} >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
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

    private sealed class FailureStream(bool failFlush) : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (failFlush) return base.WriteAsync(buffer, cancellationToken);
            Write(buffer.Span[..Math.Min(16, buffer.Length)]);
            return ValueTask.FromException(new IOException("Injected destination write failure."));
        }
        public override Task FlushAsync(CancellationToken cancellationToken) =>
            failFlush ? Task.FromException(new IOException("Injected destination flush failure.")) : base.FlushAsync(cancellationToken);
    }
}
