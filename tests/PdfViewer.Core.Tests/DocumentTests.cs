using Avalonia;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Core.Tests;

public sealed class DocumentTests
{
    [Fact]
    public async Task Native_reader_loads_page_geometry_rotation_and_nested_bookmarks()
    {
        using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Collection(document.Pages,
            page => { Assert.Equal(0, page.Index); Assert.Equal(300d, page.Width, 2); Assert.Equal(400d, page.Height, 2); },
            page => { Assert.Equal(1, page.Index); Assert.Equal(400d, page.Width, 2); Assert.Equal(250d, page.Height, 2); });
        var chapter = Assert.Single(document.Bookmarks);
        Assert.Equal("Chapter one", chapter.Title);
        Assert.Equal(0, chapter.PageIndex);
        var second = Assert.Single(chapter.Children);
        Assert.Equal("Second page", second.Title);
        Assert.Equal(1, second.PageIndex);
    }

    [Fact]
    public async Task Native_renderer_returns_opaque_pixels_and_respects_page_rotation()
    {
        using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        var rendered = await document.RenderAsync(0, 300, 400, 0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(300, rendered.Width);
        Assert.Equal(400, rendered.Height);
        Assert.Equal(rendered.Width * 4, rendered.Stride);
        Assert.Equal(rendered.Stride * rendered.Height, rendered.Pixels.Length);
        // The fixture paints a blue rectangle in PDF bottom-left coordinates.
        AssertBlue(rendered.Pixels, rendered.Stride, 40, 365);
        AssertWhite(rendered.Pixels, rendered.Stride, 280, 380);
        var rotated = await document.RenderAsync(0, 400, 300, 90, cancellationToken: TestContext.Current.CancellationToken);
        AssertBlue(rotated.Pixels, rotated.Stride, 35, 40);
        AssertWhite(rotated.Pixels, rotated.Stride, 380, 280);
        var nativeRotation = await document.RenderAsync(1, 400, 250, 0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(nativeRotation.Pixels.Where((_, i) => i % 4 != 3), value => value < 100);
    }

    [Fact]
    public async Task Text_positions_use_top_left_coordinates_including_intrinsic_rotation()
    {
        using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        var text = await document.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("Hello PDF reader", text.Text);
        var first = text.Characters.First(character => character.Text == "H");
        Assert.InRange(first.Bounds.Left, 39, 44);
        Assert.InRange(first.Bounds.Top, 60, 85);
        Assert.True(first.Bounds.Width > 0 && first.Bounds.Height > 0);
        Assert.Equal("H", text.Text.Substring(first.Index, first.Text.Length));
        var rotated = await document.GetTextAsync(1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("Second page hello", rotated.Text);
        var second = rotated.Characters.First(character => character.Text == "S");
        Assert.InRange(second.Bounds.Left, 320, 350);
        Assert.InRange(second.Bounds.Top, 25, 45);
        Assert.True(second.Bounds.Width > 0 && second.Bounds.Height > 0);
        Assert.All(rotated.Characters.Where(character => !string.IsNullOrWhiteSpace(character.Text)), character =>
        {
            Assert.True(new Rect(0, 0, 400, 250).Intersects(character.Bounds));
        });
    }

    [Fact]
    public async Task Search_uses_case_word_limits_and_highlight_bounds_across_pages()
    {
        using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        var insensitive = await document.SearchAsync("hello", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(5, insensitive.Count);
        Assert.Equal(3, (await document.SearchAsync("hello", matchCase: true, cancellationToken: TestContext.Current.CancellationToken)).Count);
        Assert.Equal(4, (await document.SearchAsync("hello", wholeWord: true, cancellationToken: TestContext.Current.CancellationToken)).Count);
        Assert.Equal(2, (await document.SearchAsync("hello", maxResults: 2, cancellationToken: TestContext.Current.CancellationToken)).Count);
        Assert.Empty(await document.SearchAsync("absent", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await document.SearchAsync("", cancellationToken: TestContext.Current.CancellationToken));
        foreach (var result in insensitive)
        {
            var text = await document.GetTextAsync(result.PageIndex, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("hello", text.Text.Substring(result.StartIndex, result.Length), ignoreCase: true);
            Assert.NotEmpty(result.Bounds);
            Assert.All(result.Bounds, rect => Assert.True(rect.Width > 0 && rect.Height > 0));
        }
        Assert.Equal(1, insensitive[^1].PageIndex);
    }

    [Fact]
    public async Task Unicode_CMap_text_and_search_keep_native_indices_and_respect_the_crop_origin()
    {
        using var document = await PdfDocument.LoadAsync(PdfFixture.UnicodeAndCropDocument(), cancellationToken: TestContext.Current.CancellationToken);
        var page = Assert.Single(document.Pages);
        Assert.Equal(500, page.Width);
        Assert.Equal(600, page.Height);
        var text = await document.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("A中文🚀Bé", text.Text);
        var chinese = Assert.Single(await document.SearchAsync("中文", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("中文", chinese.Text);
        Assert.Equal(2, chinese.Length);
        Assert.NotEmpty(chinese.Bounds);
        Assert.All(chinese.Bounds, bounds =>
        {
            Assert.InRange(bounds.Left, 0, 150);
            Assert.InRange(bounds.Top, 20, 70);
        });
        var tail = Assert.Single(await document.SearchAsync("Bé", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Bé", tail.Text);
        var nativeCharacters = text.Characters.Where(character => character.Index >= tail.StartIndex && character.Index < tail.StartIndex + tail.Length);
        Assert.Equal("Bé", string.Concat(nativeCharacters.Select(character => character.Text)));
    }

    [Fact]
    public async Task Caller_can_reuse_or_modify_input_bytes_after_loading()
    {
        var bytes = PdfFixture.ReadingDocument();
        using var document = await PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        Array.Fill(bytes, (byte)0);
        Assert.Contains("Hello PDF reader", (await document.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken)).Text);
        Assert.Equal(100, (await document.RenderAsync(0, 100, 120, 0, cancellationToken: TestContext.Current.CancellationToken)).Width);
    }

    [Fact]
    public async Task Password_error_is_distinct_and_correct_password_unlocks_text_and_pixels()
    {
        var bytes = PdfFixture.PasswordProtected();
        var missing = await Assert.ThrowsAsync<PdfDocumentException>(() => PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken));
        var wrong = await Assert.ThrowsAsync<PdfDocumentException>(() => PdfDocument.LoadAsync(bytes, "wrong", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(4u, missing.ErrorCode);
        Assert.Equal(4u, wrong.ErrorCode);
        using var document = await PdfDocument.LoadAsync(bytes, "reader", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("Protected text", (await document.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken)).Text);
        Assert.Equal(200, (await document.RenderAsync(0, 200, 300, 0, cancellationToken: TestContext.Current.CancellationToken)).Width);
    }

    [Fact]
    public async Task Invalid_document_reports_native_error_and_does_not_poison_future_loads()
    {
        var error = await Assert.ThrowsAsync<PdfDocumentException>(() => PdfDocument.LoadAsync("not a PDF"u8.ToArray(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3u, error.ErrorCode);
        using var document = await PdfDocument.LoadAsync(PdfFixture.SinglePage(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("Replacement document", (await document.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken)).Text);
    }

    [Fact]
    public async Task Stream_loading_honors_current_position_and_explicit_ownership_on_success_and_failure()
    {
        var prefix = "ignored prefix"u8.ToArray();
        using var owned = new MemoryStream(prefix.Concat(PdfFixture.SinglePage()).ToArray());
        owned.Position = prefix.Length;
        using (var document = await PdfDocument.LoadAsync(owned, cancellationToken: TestContext.Current.CancellationToken))
            Assert.Contains("Replacement document", (await document.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken)).Text);
        Assert.False(owned.CanRead);

        using var borrowed = new MemoryStream(PdfFixture.ReadingDocument());
        using (var document = await PdfDocument.LoadAsync(borrowed, leaveOpen: true, cancellationToken: TestContext.Current.CancellationToken))
            Assert.Equal(2, document.Pages.Count);
        Assert.True(borrowed.CanRead);

        using var invalid = new MemoryStream("invalid"u8.ToArray());
        await Assert.ThrowsAsync<PdfDocumentException>(() => PdfDocument.LoadAsync(invalid, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(invalid.CanRead);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var unread = new MemoryStream(PdfFixture.SinglePage());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfDocument.LoadAsync(unread, cancellationToken: cancelled.Token));
        Assert.False(unread.CanRead);
    }

    [Fact]
    public async Task Invalid_page_rotation_and_excessive_pixel_allocation_are_rejected_before_native_access()
    {
        using var document = await PdfDocument.LoadAsync(PdfFixture.SinglePage(), cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.GetTextAsync(-1, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.GetTextAsync(1, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.RenderAsync(0, 0, 100, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.RenderAsync(0, 100, -1, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.RenderAsync(0, 20000, 10, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.RenderAsync(0, 12000, 12000, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.RenderAsync(0, 100, 100, 45, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.SearchAsync("text", maxResults: 0, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => document.SearchAsync("text\0ignored", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("Replacement document", (await document.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken)).Text);
    }

    [Fact]
    public async Task Cancelled_operations_do_not_dispose_or_poison_the_document()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfDocument.LoadAsync(PdfFixture.SinglePage(), cancellationToken: cancelled.Token));
        using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => document.RenderAsync(0, 300, 400, 0, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => document.GetTextAsync(0, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => document.SearchAsync("hello", cancellationToken: cancelled.Token));
        Assert.Equal(5, (await document.SearchAsync("hello", cancellationToken: TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task Multiple_documents_and_mixed_parallel_operations_keep_native_state_isolated()
    {
        using var first = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        using var second = await PdfDocument.LoadAsync(PdfFixture.SinglePage("Independent file"), cancellationToken: TestContext.Current.CancellationToken);
        var tasks = Enumerable.Range(0, 12).Select(async i =>
        {
            if (i % 3 == 0) Assert.Contains("Independent file", (await second.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken)).Text);
            else if (i % 3 == 1) Assert.Equal(5, (await first.SearchAsync("hello", cancellationToken: TestContext.Current.CancellationToken)).Count);
            else Assert.Equal(120, (await first.RenderAsync(1, 120, 90, 0, cancellationToken: TestContext.Current.CancellationToken)).Width);
        });
        await Task.WhenAll(tasks);
        first.Dispose();
        Assert.Contains("Independent file", (await second.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken)).Text);
        Assert.Equal(80, (await second.RenderAsync(0, 80, 120, 0, cancellationToken: TestContext.Current.CancellationToken)).Width);
    }

    [Fact]
    public async Task Dispose_is_idempotent_and_subsequent_native_access_is_rejected()
    {
        var document = await PdfDocument.LoadAsync(PdfFixture.SinglePage(), cancellationToken: TestContext.Current.CancellationToken);
        document.Dispose();
        document.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => document.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => document.RenderAsync(0, 100, 100, 0, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => document.SearchAsync("text", cancellationToken: TestContext.Current.CancellationToken));
    }

    private static void AssertBlue(byte[] pixels, int stride, int x, int y)
    {
        var offset = y * stride + x * 4;
        Assert.Equal(255, pixels[offset]);
        Assert.Equal(0, pixels[offset + 1]);
        Assert.Equal(0, pixels[offset + 2]);
        Assert.Equal(255, pixels[offset + 3]);
    }

    private static void AssertWhite(byte[] pixels, int stride, int x, int y)
    {
        var offset = y * stride + x * 4;
        Assert.All(pixels.AsSpan(offset, 4).ToArray(), channel => Assert.Equal(255, channel));
    }
}
