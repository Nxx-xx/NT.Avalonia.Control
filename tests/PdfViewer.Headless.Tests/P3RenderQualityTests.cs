using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

/// <summary>
/// P3-03 quality contracts: visible pages settle at full quality while pre-render rows stay
/// light, a scroll / zoom burst fills at preview tier and upgrades after it settles (停滚高清),
/// zoom keeps the old bitmaps as previews (缩放复用旧图), budget / cap reductions are recorded
/// as degradations instead of degrading silently, the decode caps are configurable, and a
/// display-scaling change re-rasterizes the cache.
/// </summary>
public sealed class P3RenderQualityTests
{
    [Fact]
    public Task Settled_visible_pages_render_full_quality_while_prefetch_stays_preview() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await QualityFixture.Create(PdfFixture.ManyPages(40), reader => reader.PrerenderRows = 1);
        await fixture.Idle();
        var presenter = fixture.Presenter;
        var visible = fixture.VisiblePages();
        Assert.NotEmpty(visible);
        foreach (var index in visible)
        {
            var state = presenter.GetCachedPageState(index);
            Assert.NotNull(state);
            Assert.False(state!.Value.Preview, $"Visible page {index + 1} settled at preview quality.");
        }
        // Pre-render rows are decoded, but at the preview tier until they scroll into view.
        Assert.True(presenter.PreviewPageCount >= 1,
            $"Expected the pre-render row to stay preview; preview count is {presenter.PreviewPageCount}.");
        // The default budget covers the on-screen raster: no forced reduction is recorded.
        Assert.Equal(0, presenter.DegradedPageCount);
    });

    [Fact]
    public Task Scroll_burst_fills_preview_then_upgrades_after_settle() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await QualityFixture.Create(PdfFixture.ManyPages(40), reader => reader.PrerenderRows = 0);
        await fixture.Idle();
        var presenter = fixture.Presenter;
        // Stretch the debounce so the burst state is observable before the upgrade pass.
        presenter.SettleDelay = TimeSpan.FromSeconds(30);
        fixture.Reader.GoToPage(10);
        await Until(() => presenter.GetCachedPageState(9) is not null);
        var burst = presenter.GetCachedPageState(9)!.Value;
        Assert.True(burst.Preview, "A page filled during a scroll burst must land at preview quality first.");
        // Let the burst end: the visible page upgrades to its full on-screen raster (停滚高清).
        presenter.SettleDelay = TimeSpan.FromMilliseconds(30);
        await Until(() => presenter.GetCachedPageState(9) is { Preview: false } settled && settled.Width > burst.Width);
        var upgraded = presenter.GetCachedPageState(9)!.Value;
        Assert.False(upgraded.Preview);
        Assert.Equal(0, presenter.DegradedPageCount);
    });

    [Fact]
    public Task Zoom_keeps_cached_bitmaps_as_previews_until_the_burst_settles() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await QualityFixture.Create(PdfFixture.ManyPages(20));
        await fixture.Idle();
        var presenter = fixture.Presenter;
        var before = presenter.GetCachedPageState(0)!.Value;
        Assert.False(before.Preview);
        // A zoom burst must not touch the cached bitmap: it keeps serving stretched as the preview.
        presenter.SettleDelay = TimeSpan.FromSeconds(30);
        fixture.Reader.ZoomLevel = fixture.Reader.ZoomLevel * 2;
        HeadlessTestScope.Drain();
        var held = presenter.GetCachedPageState(0)!.Value;
        Assert.Equal(before.Width, held.Width);
        Assert.Equal(before.Height, held.Height);
        // Once the burst settles, the page re-rasterizes at the new geometry.
        presenter.SettleDelay = TimeSpan.FromMilliseconds(30);
        await Until(() => presenter.GetCachedPageState(0) is { } state && state.Width != before.Width && !state.Preview);
    });

    [Fact]
    public Task Budget_pressure_records_degradation_instead_of_silently_degrading() => HeadlessTestScope.Run(async () =>
    {
        // A 64 KiB page budget cannot hold the on-screen raster; the pages shrink and every
        // shrunk page is recorded instead of quietly winning with less resolution.
        using var tight = await QualityFixture.Create(PdfFixture.ManyPages(20),
            reader => reader.PageCacheBudget = Reader.MinimumCacheBudget);
        await tight.Idle();
        Assert.True(tight.Presenter.DegradedPageCount >= 1,
            $"Budget-forced reductions must be recorded; DegradedPageCount is {tight.Presenter.DegradedPageCount}.");
        var degraded = tight.Presenter.GetCachedPageState(0);
        Assert.True(degraded is { Degraded: true, Preview: false });
        // The default budget reaches the on-screen raster at ordinary zoom: nothing degraded.
        using var full = await QualityFixture.Create(PdfFixture.ManyPages(20));
        await full.Idle();
        Assert.Equal(0, full.Presenter.DegradedPageCount);
    });

    [Fact]
    public Task Quality_caps_are_configurable_coerced_and_binding() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await QualityFixture.Create(PdfFixture.ManyPages(10));
        await fixture.Idle();
        var reader = fixture.Reader;
        Assert.Equal(Reader.DefaultMaxRenderDimension, reader.MaxRenderDimension);
        Assert.Equal(Reader.DefaultMaxRenderPixels, reader.MaxRenderPixels);
        Assert.Equal(Reader.DefaultMaxRenderScale, reader.MaxRenderScale);
        reader.MaxRenderDimension = 10;
        Assert.Equal(256, reader.MaxRenderDimension);
        reader.MaxRenderDimension = 999_999;
        Assert.Equal(16384, reader.MaxRenderDimension);
        reader.MaxRenderPixels = 1;
        Assert.Equal(65_536, reader.MaxRenderPixels);
        reader.MaxRenderPixels = long.MaxValue;
        Assert.Equal(64L * 1024 * 1024, reader.MaxRenderPixels);
        reader.MaxRenderScale = 0;
        Assert.Equal(1, reader.MaxRenderScale);
        reader.MaxRenderScale = 100;
        Assert.Equal(8, reader.MaxRenderScale);
        Assert.Throws<ArgumentException>(() => reader.MaxRenderScale = double.NaN);
        // The caps bound the actual decode of the settled visible page.
        reader.MaxRenderDimension = 512;
        await fixture.Idle();
        var state = fixture.Presenter.GetCachedPageState(0)!.Value;
        Assert.True(state.Width <= 512 && state.Height <= 512,
            $"Page raster {state.Width}x{state.Height} exceeds the 512px edge cap.");
        reader.MaxRenderPixels = 150_000;
        await fixture.Idle();
        state = fixture.Presenter.GetCachedPageState(0)!.Value;
        Assert.True((long)state.Width * state.Height <= 150_000,
            $"Page raster {state.Width}x{state.Height} exceeds the 150,000-pixel cap.");
        // A cap-forced reduction is a recorded degradation, same as a budget-forced one.
        Assert.True(fixture.Presenter.DegradedPageCount >= 1,
            $"Cap-forced reductions must be recorded; DegradedPageCount is {fixture.Presenter.DegradedPageCount}.");
    });

    [Fact]
    public Task Display_scaling_change_re_rasterizes_cached_pages() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await QualityFixture.Create(PdfFixture.ManyPages(10));
        fixture.Reader.ZoomMode = PdfZoomMode.Manual;
        fixture.Reader.ZoomLevel = 1;
        await fixture.Idle();
        var presenter = fixture.Presenter;
        var baseline = presenter.GetCachedPageState(0)!.Value;
        var bounds = presenter.GetPageBounds(0);
        Assert.False(baseline.Preview);
        // Moving the window to a 200% display must invalidate the cache and re-decode at the
        // new scale while the DIP layout stays put.
        fixture.Window.SetRenderScaling(2);
        fixture.Reader.GoToPage(1);
        await fixture.Idle();
        var scaled = presenter.GetCachedPageState(0)!.Value;
        Assert.False(scaled.Preview);
        Assert.True(scaled.Width > baseline.Width * 1.5,
            $"Raster width {scaled.Width} did not follow the doubled display scale (baseline {baseline.Width}).");
        var after = presenter.GetPageBounds(0);
        Assert.Equal(bounds.Width, after.Width, 1);
        Assert.Equal(bounds.Height, after.Height, 1);
        // Scaling back down re-rasterizes again instead of keeping the 200% bitmaps forever.
        fixture.Window.SetRenderScaling(1);
        fixture.Reader.GoToPage(1);
        await fixture.Idle();
        var restored = presenter.GetCachedPageState(0)!.Value;
        Assert.Equal(baseline.Width, restored.Width);
        Assert.Equal(baseline.Height, restored.Height);
    });

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!condition())
        {
            HeadlessTestScope.Drain();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class QualityFixture : IDisposable
    {
        internal Reader Reader { get; } = new() { IsSidebarVisible = false };
        internal Window Window { get; }
        internal PdfDocumentPresenter Presenter => (PdfDocumentPresenter)Reader.DocumentPresenter!;

        internal QualityFixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<QualityFixture> Create(byte[] bytes, Action<Reader>? configure = null)
        {
            var fixture = new QualityFixture();
            try
            {
                configure?.Invoke(fixture.Reader);
                await fixture.Reader.LoadDocumentAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
                await fixture.Idle();
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        internal int[] VisiblePages()
        {
            var presenter = Presenter;
            var scroll = presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
            var viewport = new Rect(scroll.Offset.X, scroll.Offset.Y, scroll.Viewport.Width, scroll.Viewport.Height);
            return presenter.PageBounds
                .Select((bounds, index) => (bounds, index))
                .Where(entry => entry.bounds.Intersects(viewport))
                .Select(entry => entry.index)
                .ToArray();
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
