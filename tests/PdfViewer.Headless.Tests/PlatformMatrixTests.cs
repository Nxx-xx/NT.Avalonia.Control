using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

/// <summary>
/// WP7 layout × rotation × mixed page sizes × DPI regression: link positioning and hit-testing,
/// spread layout, and reading-position capture / restore must agree in every combination. The
/// fixture mixes portrait, narrow portrait and landscape page sizes. Real Windows IME, Linux
/// IBus / Fcitx5, touch and touchpad hardware behavior is not covered here and stays on the
/// device-acceptance list.
/// </summary>
public sealed class PlatformMatrixTests
{
    [Fact]
    public Task Hit_testing_follows_rotation_on_mixed_page_sizes() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await MatrixFixture.Create(PdfFixture.FitDestinationsDocument());
        var reader = fixture.Reader;
        await using var document = await PdfDocument.LoadAsync(PdfFixture.FitDestinationsDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var links = await document.GetLinksAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        // The /FitR link targets page 2 (narrow portrait, 250x400 points).
        var fitr = links.Single(link => link.Destination?.Fit == PdfDestinationFit.FitR);

        foreach (var rotation in new[] { 0, 90, 180, 270 })
        {
            reader.Rotation = rotation;
            reader.ZoomMode = PdfZoomMode.Manual;
            reader.ZoomLevel = 1;
            reader.GoToPage(1);
            await fixture.Idle();

            // Positioning: every page reports its rotated display size in the presenter layout.
            var portrait = fixture.Presenter.GetPageBounds(0);
            var landscape = fixture.Presenter.GetPageBounds(2);
            if (rotation is 90 or 270)
            {
                Assert.Equal(400 * 96 / 72.0, portrait.Width, 1);
                Assert.Equal(300 * 96 / 72.0, portrait.Height, 1);
                Assert.Equal(300 * 96 / 72.0, landscape.Width, 1);
                Assert.Equal(800 * 96 / 72.0, landscape.Height, 1);
            }
            else
            {
                Assert.Equal(300 * 96 / 72.0, portrait.Width, 1);
                Assert.Equal(400 * 96 / 72.0, portrait.Height, 1);
                Assert.Equal(800 * 96 / 72.0, landscape.Width, 1);
                Assert.Equal(300 * 96 / 72.0, landscape.Height, 1);
            }

            // Hit-testing: the rotated link rect resolves to the same annotation.
            var center = await fixture.LinkCenter(fitr);
            Assert.Equal(fitr, fixture.Presenter.LinkAt(center));
            var scroll = fixture.Presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
            var windowPoint = fixture.Presenter.TranslatePoint(center, fixture.Window)!.Value;
            var viewportOrigin = scroll.TranslatePoint(new Point(0, 0), fixture.Window)!.Value;
            Assert.True(windowPoint.X >= viewportOrigin.X && windowPoint.Y >= viewportOrigin.Y
                && windowPoint.X <= viewportOrigin.X + scroll.Viewport.Width
                && windowPoint.Y <= viewportOrigin.Y + scroll.Viewport.Height,
                $"rotation {rotation}: link center outside viewport: window={windowPoint}, viewport origin={viewportOrigin}, size={scroll.Viewport}, offset={scroll.Offset}, extent={scroll.Extent}");

            // A real click inside the link activates it and navigates to page 2. Click toward the
            // top of the rect: at rotation 180 the exact center can fall under the overlay
            // scrollbars at the viewport edges, where chrome (not the document) is correctly on
            // top of the link.
            var linkRect = fixture.Presenter.GetSearchBounds(
                new PdfSearchResult(fitr.PageIndex, 0, 0, [fitr.Bounds], fitr.Target ?? ""));
            var clickPoint = new Point(linkRect.Center.X, linkRect.Y + Math.Min(10, linkRect.Height / 3));
            Assert.Equal(fitr, fixture.Presenter.LinkAt(clickPoint));
            PdfLinkActivatedEventArgs? activation = null;
            object? hitSource = null;
            EventHandler<PointerPressedEventArgs> probe = (_, args) => hitSource = args.Source;
            fixture.Window.AddHandler(InputElement.PointerPressedEvent, probe, RoutingStrategies.Tunnel);
            reader.LinkActivated += OnActivated;
            try
            {
                fixture.Click(clickPoint);
                await fixture.Idle();
            }
            finally
            {
                fixture.Window.RemoveHandler(InputElement.PointerPressedEvent, probe);
                reader.LinkActivated -= OnActivated;
            }
            var clickWindowPoint = fixture.Presenter.TranslatePoint(clickPoint, fixture.Window)!.Value;
            Assert.True(hitSource is not null, $"rotation {rotation}: no pointer press hit (windowPoint={clickWindowPoint})");
            Assert.True(hitSource == fixture.Presenter,
                $"rotation {rotation}: press hit {hitSource?.GetType().Name}, expected presenter (windowPoint={clickWindowPoint}), " +
                $"presenterPoint={clickPoint}, presenterOrigin={fixture.Presenter.TranslatePoint(new Point(0, 0), fixture.Window)}, " +
                $"presenterBounds={fixture.Presenter.Bounds}, offset={scroll.Offset}, extent={scroll.Extent}");
            Assert.True(activation is not null, $"no link activation at rotation {rotation}");
            Assert.Equal(PdfDestinationFit.FitR, activation!.Link.Destination?.Fit);
            Assert.Equal(2, reader.CurrentPage);
            void OnActivated(object? sender, PdfLinkActivatedEventArgs args) => activation = args;
        }
    });

    [Fact]
    public Task Reading_positions_roundtrip_across_rotation_and_mixed_sizes() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await MatrixFixture.Create(PdfFixture.FitDestinationsDocument());
        var reader = fixture.Reader;
        reader.ZoomMode = PdfZoomMode.Manual;
        reader.ZoomLevel = 1;
        await fixture.Idle();
        var scroll = fixture.Presenter.GetVisualAncestors().OfType<ScrollViewer>().First();

        // Capture on the narrow portrait page (250x400) while rotated; the anchor keeps page coords.
        reader.Rotation = 90;
        reader.GoToPage(2);
        await fixture.Idle();
        scroll.Offset = new Vector(scroll.Offset.X, scroll.Offset.Y + 40);
        await fixture.Idle();
        var offset = scroll.Offset.Y;
        var rotatedPosition = reader.CaptureReadingPosition();
        Assert.NotNull(rotatedPosition);
        Assert.Equal(1, rotatedPosition!.PageIndex);
        Assert.Equal(90, rotatedPosition.Rotation);
        Assert.Equal(PdfZoomMode.Manual, rotatedPosition.ZoomMode);

        // Capture on the landscape page (800x300) unrotated.
        reader.Rotation = 0;
        reader.ZoomLevel = 1.5;
        reader.GoToPage(3);
        await fixture.Idle();
        var landscapePosition = reader.CaptureReadingPosition();
        Assert.NotNull(landscapePosition);
        Assert.Equal(2, landscapePosition!.PageIndex);
        Assert.Equal(0, landscapePosition.Rotation);

        // Restoring the rotated position re-applies rotation, zoom and the anchor offset.
        reader.RestoreReadingPosition(rotatedPosition);
        await fixture.Idle();
        Assert.Equal(90, reader.Rotation);
        Assert.Equal(1, reader.ZoomLevel, 3);
        Assert.Equal(2, reader.CurrentPage);
        Assert.Equal(offset, scroll.Offset.Y, 1);

        // The landscape position restores independently on the mixed-size page.
        reader.RestoreReadingPosition(landscapePosition);
        await fixture.Idle();
        Assert.Equal(0, reader.Rotation);
        Assert.Equal(1.5, reader.ZoomLevel, 3);
        Assert.Equal(3, reader.CurrentPage);
    });

    [Fact]
    public Task Layout_hit_testing_and_reading_position_survive_render_scaling() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await MatrixFixture.Create(PdfFixture.FitDestinationsDocument());
        var reader = fixture.Reader;
        await using var document = await PdfDocument.LoadAsync(PdfFixture.FitDestinationsDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var links = await document.GetLinksAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        var fitr = links.Single(link => link.Destination?.Fit == PdfDestinationFit.FitR);
        // The hard case stays rotated for the whole scaling loop; the baseline is taken at the
        // same rotation so only render scaling may move the geometry.
        reader.Rotation = 90;
        reader.ZoomMode = PdfZoomMode.Manual;
        reader.ZoomLevel = 1;
        reader.GoToPage(1);
        await fixture.Idle();
        var baseline = fixture.Presenter.GetPageBounds(0);

        foreach (var scaling in new[] { 1.0, 1.5, 2.0 })
        {
            fixture.Window.SetRenderScaling(scaling);
            reader.GoToPage(1);
            await fixture.Idle();

            // Layout stays in DIPs: identical geometry at every render scale.
            var page = fixture.Presenter.GetPageBounds(0);
            Assert.Equal(baseline.Width, page.Width, 1);
            Assert.Equal(baseline.Height, page.Height, 1);

            // Hit-testing still resolves the rotated link and a real click still navigates.
            var center = await fixture.LinkCenter(fitr);
            Assert.Equal(fitr, fixture.Presenter.LinkAt(center));
            PdfLinkActivatedEventArgs? activation = null;
            reader.LinkActivated += OnActivated;
            try
            {
                fixture.Click(center);
                await fixture.Idle();
            }
            finally
            {
                reader.LinkActivated -= OnActivated;
            }
            Assert.NotNull(activation);
            Assert.Equal(2, reader.CurrentPage);
            void OnActivated(object? sender, PdfLinkActivatedEventArgs args) => activation = args;

            // Reading position capture / restore behaves the same at this scale.
            reader.Rotation = 0;
            reader.ZoomMode = PdfZoomMode.Manual;
            reader.ZoomLevel = 1;
            reader.GoToPage(2);
            await fixture.Idle();
            var position = reader.CaptureReadingPosition();
            Assert.NotNull(position);
            reader.GoToPage(3);
            reader.ZoomLevel = 2;
            await fixture.Idle();
            reader.RestoreReadingPosition(position!);
            await fixture.Idle();
            Assert.Equal(2, reader.CurrentPage);
            Assert.Equal(1, reader.ZoomLevel, 3);

            // Back to the rotated state for the next scaling step.
            reader.Rotation = 90;
            reader.ZoomMode = PdfZoomMode.Manual;
            reader.ZoomLevel = 1;
        }
    });

    [Fact]
    public Task Spreads_keep_mixed_sizes_side_by_side_under_rotation() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await MatrixFixture.Create(PdfFixture.FitDestinationsDocument());
        var reader = fixture.Reader;
        reader.PageDisplayMode = PdfPageDisplayMode.TwoPage;
        reader.ShowCoverPage = true;
        reader.ZoomMode = PdfZoomMode.Manual;
        reader.ZoomLevel = 1;

        foreach (var rotation in new[] { 0, 90 })
        {
            reader.Rotation = rotation;
            reader.GoToPage(2);
            await fixture.Idle();
            // Two-page mode shows only the active spread: pages 2-3 (narrow portrait + landscape),
            // with the cover page 1 unlayouted.
            var first = fixture.Presenter.GetPageBounds(0);
            var second = fixture.Presenter.GetPageBounds(1);
            var third = fixture.Presenter.GetPageBounds(2);
            Assert.Equal(0, first.Width);
            Assert.True(second.Width > 0 && second.Height > 0, $"spread page shown at rotation {rotation}");
            Assert.True(third.Width > 0 && third.Height > 0, $"spread page shown at rotation {rotation}");
            Assert.True(second.Right <= third.X, $"spread side-by-side at rotation {rotation}");
            // Rotation swaps the reported display dimensions of both spread pages.
            if (rotation == 90)
            {
                Assert.Equal(400 * 96 / 72.0, second.Width, 1);
                Assert.Equal(300 * 96 / 72.0, third.Width, 1);
            }
            else
            {
                Assert.Equal(250 * 96 / 72.0, second.Width, 1);
                Assert.Equal(800 * 96 / 72.0, third.Width, 1);
            }
        }
    });

    private sealed class MatrixFixture : IDisposable
    {
        internal Reader Reader { get; } = new();
        internal Window Window { get; }
        internal PdfDocumentPresenter Presenter => (PdfDocumentPresenter)Reader.DocumentPresenter!;

        private MatrixFixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<MatrixFixture> Create(byte[] bytes)
        {
            var fixture = new MatrixFixture();
            try
            {
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

        /// <summary>Maps a link's page-coordinates rect into the presenter's rotated layout space.</summary>
        internal async Task<Point> LinkCenter(PdfLink link)
        {
            await Idle();
            var rect = Presenter.GetSearchBounds(new PdfSearchResult(link.PageIndex, 0, 0, [link.Bounds], link.Target ?? ""));
            var center = rect.Center;
            // Link prefetch is asynchronous; wait until hit-testing can see the annotation.
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                while (Presenter.LinkAt(center) is null)
                {
                    HeadlessTestScope.Drain();
                    await Task.Delay(10, timeout.Token);
                }
            }
            return center;
        }

        /// <summary>Real pointer input at a presenter-space point.</summary>
        internal void Click(Point presenterPoint) =>
            ClickWindowPoint(Presenter.TranslatePoint(presenterPoint, Window)!.Value);

        /// <summary>Real pointer input at a window-space point.</summary>
        internal void ClickWindowPoint(Point windowPoint)
        {
            ScopedInput(() => Window.MouseDown(windowPoint, MouseButton.Left));
            ScopedInput(() => Window.MouseUp(windowPoint, MouseButton.Left));
            HeadlessTestScope.Drain();
        }

        private static void ScopedInput(Action input)
        {
            var caller = ExecutionContext.Capture();
            if (caller is null) input();
            else ExecutionContext.Run(caller, _ => input(), null);
        }

        public void Dispose()
        {
            Reader.Dispose();
            Window.Close();
        }
    }
}
