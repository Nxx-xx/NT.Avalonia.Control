using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

public sealed class P3CacheBudgetTests
{
    [Fact]
    public Task Page_budget_bounds_retained_bytes_and_sheds_under_pressure() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await BudgetFixture.Create(PdfFixture.ManyPages(60), reader => reader.PageCacheBudget = 16L * 1024 * 1024);
        for (var page = 1; page <= 24; page += 2)
        {
            fixture.Reader.GoToPage(page);
            await fixture.Idle();
        }
        var stats = fixture.Reader.CacheStatistics;
        Assert.True(stats.PageBitmapBytes <= fixture.Reader.PageCacheBudget,
            $"Retained {stats.PageBitmapBytes} page-bitmap bytes exceed the {fixture.Reader.PageCacheBudget} budget.");
        Assert.Equal(fixture.Reader.CachedBytes, stats.PageBitmapBytes);
        // Pressure sheds the retention window but keeps a live working set beyond the viewport.
        Assert.InRange(fixture.Reader.CachedPageCount, 6, 22);
    });

    [Fact]
    public Task Retention_hysteresis_keeps_recently_viewed_pages_until_pressure() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await BudgetFixture.Create(PdfFixture.ManyPages(60));
        for (var page = 1; page <= 20; page++)
        {
            fixture.Reader.GoToPage(page);
            await fixture.Idle();
        }
        // The retention contract: visiting twenty pages keeps them cached instead of
        // discarding every page that left the viewport.
        Assert.True(fixture.Reader.CachedPageCount >= 16,
            $"Only {fixture.Reader.CachedPageCount} pages retained after visiting twenty; the retention window closed early.");
        var retained = fixture.Reader.CachedPageCount;
        fixture.Reader.PageCacheBudget = 16L * 1024 * 1024;
        await fixture.Idle();
        Assert.True(fixture.Reader.CachedBytes <= fixture.Reader.PageCacheBudget,
            $"Retained {fixture.Reader.CachedBytes} bytes after shrinking the budget.");
        Assert.True(fixture.Reader.CachedPageCount < retained,
            $"Shrinking the budget kept all {retained} pages.");
    });

    [Fact]
    public Task Prerender_rows_decode_pages_beyond_the_viewport() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await BudgetFixture.Create(PdfFixture.ManyPages(60, width: 100, height: 30),
            reader => reader.PrerenderRows = 0);
        fixture.Reader.ZoomLevel = .1;
        await fixture.Idle();
        var baseline = fixture.Reader.CachedPageCount;
        var presenter = Assert.IsType<PdfDocumentPresenter>(fixture.Reader.DocumentPresenter);
        var scroll = presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
        var viewport = new Rect(scroll.Offset.X, scroll.Offset.Y, scroll.Viewport.Width, scroll.Viewport.Height);
        var visible = presenter.PageBounds.Count(bounds => bounds.Intersects(viewport));
        Assert.InRange(baseline, Math.Max(1, visible - 1), visible + 2);
        fixture.Reader.PrerenderRows = 2;
        await fixture.Idle();
        var widened = fixture.Reader.CachedPageCount;
        // Two pre-render rows below the top of the document decode two whole extra rows.
        Assert.InRange(widened, baseline + 2, baseline + 4);
    });

    [Fact]
    public Task Thumbnail_and_text_budgets_are_enforced_and_accounted() => HeadlessTestScope.Run(async () =>
    {
        var bytes = PdfFixture.ManyPages(100);
        using var fixture = await BudgetFixture.Create(bytes, reader =>
        {
            reader.IsSidebarVisible = true;
            reader.ThumbnailCacheBudget = Reader.MinimumCacheBudget;
            reader.TextCacheBudget = Reader.MinimumCacheBudget;
        });
        await Until(() => fixture.Reader.Thumbnails.Any(thumbnail => thumbnail.Image is not null));
        for (var page = 1; page <= 100; page++)
        {
            fixture.Reader.GoToPage(page);
            await fixture.Reader.SelectAllAsync();
        }
        await fixture.Idle();
        // Thumbnail work outlives the presenter's render pass; wait for all decode buffers
        // to be handed off before asserting the idle accounting.
        await Until(() => fixture.Reader.CacheStatistics.DecodeBufferBytes == 0);
        var stats = fixture.Reader.CacheStatistics;
        Assert.True(stats.ThumbnailBitmapBytes <= fixture.Reader.ThumbnailCacheBudget,
            $"Retained {stats.ThumbnailBitmapBytes} thumbnail bytes exceed the {fixture.Reader.ThumbnailCacheBudget} budget.");
        Assert.True(stats.TextBytes <= fixture.Reader.TextCacheBudget,
            $"Retained {stats.TextBytes} text bytes exceed the {fixture.Reader.TextCacheBudget} budget.");
        Assert.True(stats.TextPageCount < 100, $"All {stats.TextPageCount} extracted text pages were retained.");
        Assert.Equal(stats.PageBitmapBytes + stats.ThumbnailBitmapBytes + stats.TextBytes, stats.BudgetedBytes);
        Assert.Equal(stats.BudgetedBytes + stats.InputBytes + stats.DecodeBufferBytes, stats.TrackedBytes);
        Assert.Equal(bytes.Length, stats.InputBytes);
        Assert.Equal(0, stats.DecodeBufferBytes);
        Assert.True(stats.DecodeBufferPeakBytes >= 1_000_000,
            $"Decode peak {stats.DecodeBufferPeakBytes} bytes is smaller than a single rendered page buffer.");
    });

    [Fact]
    public Task Multi_viewer_global_quota_bounds_the_process_total() => HeadlessTestScope.Run(async () =>
    {
        Reader.GlobalCacheBudget = 3L * 1024 * 1024;
        try
        {
            using var first = await BudgetFixture.Create(PdfFixture.ManyPages(60), reader => reader.PageCacheBudget = 2L * 1024 * 1024);
            using var second = await BudgetFixture.Create(PdfFixture.ManyPages(60), reader => reader.PageCacheBudget = 2L * 1024 * 1024);
            for (var page = 1; page <= 30; page += 3)
            {
                first.Reader.GoToPage(page);
                second.Reader.GoToPage(page);
                await first.Idle();
                await second.Idle();
            }
            var a = first.Reader.CacheStatistics;
            var b = second.Reader.CacheStatistics;
            Assert.True(a.PageBitmapBytes <= first.Reader.PageCacheBudget,
                $"First viewer retained {a.PageBitmapBytes} bytes over its {first.Reader.PageCacheBudget} budget.");
            Assert.True(b.PageBitmapBytes <= second.Reader.PageCacheBudget,
                $"Second viewer retained {b.PageBitmapBytes} bytes over its {second.Reader.PageCacheBudget} budget.");
            Assert.True(a.BudgetedBytes + b.BudgetedBytes <= Reader.GlobalCacheBudget,
                $"Two viewers retained {a.BudgetedBytes + b.BudgetedBytes} bytes under a {Reader.GlobalCacheBudget} global quota.");
            Assert.Equal(a.BudgetedBytes + b.BudgetedBytes, Reader.GlobalCacheBytes);
        }
        finally
        {
            Reader.GlobalCacheBudget = 0;
        }
    });

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            HeadlessTestScope.Drain();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class BudgetFixture : IDisposable
    {
        internal Reader Reader { get; } = new() { IsSidebarVisible = false };
        internal Window Window { get; }

        internal BudgetFixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<BudgetFixture> Create(byte[] bytes, Action<Reader>? configure = null)
        {
            var fixture = new BudgetFixture();
            try
            {
                configure?.Invoke(fixture.Reader);
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

        public void Dispose()
        {
            Reader.Dispose();
            Window.Close();
        }
    }
}
