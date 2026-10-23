using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

/// <summary>
/// Touch pinch and trackpad magnify contract: the presenter registers PinchGestureRecognizer,
/// applies the cumulative scale from the gesture start, resets on end / cancel, and settles
/// conflicts with drag selection. Real touchpad / touchscreen devices stay pending hardware
/// verification.
/// </summary>
public sealed class TouchGestureTests
{
    [Fact]
    public Task Presenter_registers_the_pinch_gesture_recognizer() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await TouchFixture.Create(PdfFixture.ManyPages(2));
        Assert.Contains(fixture.Presenter.GestureRecognizers, recognizer => recognizer is PinchGestureRecognizer);
    });

    [Fact]
    public Task Pinch_uses_cumulative_scale_from_the_gesture_start_and_rebases_after_end() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await TouchFixture.Create(PdfFixture.ManyPages(3));
        fixture.Reader.ZoomLevel = 1;
        await fixture.Idle();
        var origin = fixture.Presenter.GetPageBounds(0).Center;
        ScopedInput(() => fixture.Presenter.RaiseEvent(new PinchEventArgs(1.25, origin)));
        HeadlessTestScope.Drain();
        Assert.Equal(1.25, fixture.Reader.ZoomLevel, 3);
        // Second event of the same gesture: still measured from the gesture start.
        ScopedInput(() => fixture.Presenter.RaiseEvent(new PinchEventArgs(1.5, origin)));
        HeadlessTestScope.Drain();
        Assert.Equal(1.5, fixture.Reader.ZoomLevel, 3);
        // Zooming back out mid-gesture recovers toward the base.
        ScopedInput(() => fixture.Presenter.RaiseEvent(new PinchEventArgs(1.1, origin)));
        HeadlessTestScope.Drain();
        Assert.Equal(1.1, fixture.Reader.ZoomLevel, 3);
        ScopedInput(() => fixture.Presenter.RaiseEvent(new PinchEndedEventArgs()));
        HeadlessTestScope.Drain();
        // A new gesture takes the zoom at its start (1.1) as its base.
        ScopedInput(() => fixture.Presenter.RaiseEvent(new PinchEventArgs(1.5, origin)));
        HeadlessTestScope.Drain();
        Assert.Equal(1.65, fixture.Reader.ZoomLevel, 3);
    });

    [Fact]
    public Task Pinch_cancels_an_in_progress_drag_selection_and_suppresses_new_presses() => HeadlessTestScope.Run(async () =>
    {
        var bytes = PdfFixture.ReadingDocument();
        using var native = await PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var text = await native.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        var start = text.Characters.First(character => character.Index == 0).Bounds.Center;
        var end = text.Characters.First(character => character.Index == 4).Bounds.Center;
        using var fixture = await TouchFixture.Create(bytes);
        fixture.Reader.FitPage();
        await fixture.Idle();
        var presenter = fixture.Presenter;
        Point Map(Point original)
        {
            var page = presenter.GetPageBounds(0);
            var scale = fixture.Reader.ZoomLevel * 96 / 72;
            return presenter.TranslatePoint(new Point(page.X + original.X * scale, page.Y + original.Y * scale), fixture.Window)!.Value;
        }

        // Drag-select "Hello" and interrupt it with a pinch mid-drag.
        ScopedInput(() => fixture.Window.MouseDown(Map(start), MouseButton.Left));
        await Until(() => fixture.Reader.SelectedText == "H");
        ScopedInput(() => fixture.Window.MouseMove(Map(end)));
        await Until(() => fixture.Reader.SelectedText == "Hello");
        ScopedInput(() => fixture.Presenter.RaiseEvent(new PinchEventArgs(1.25, presenter.GetPageBounds(0).Center)));
        HeadlessTestScope.Drain();
        // The pinch aborts the in-progress drag selection.
        Assert.Equal("", fixture.Reader.SelectedText);
        ScopedInput(() => fixture.Window.MouseMove(Map(end)));
        await fixture.Idle();
        Assert.Equal("", fixture.Reader.SelectedText);

        // While the gesture is active, new presses start no selection.
        ScopedInput(() => fixture.Window.MouseDown(Map(start), MouseButton.Left));
        ScopedInput(() => fixture.Window.MouseMove(Map(end)));
        await fixture.Idle();
        Assert.Equal("", fixture.Reader.SelectedText);
        ScopedInput(() => fixture.Window.MouseUp(Map(end), MouseButton.Left));
        ScopedInput(() => fixture.Presenter.RaiseEvent(new PinchEndedEventArgs()));
        HeadlessTestScope.Drain();

        // After the gesture ends a fresh drag selects normally again.
        ScopedInput(() => fixture.Window.MouseDown(Map(start), MouseButton.Left));
        await Until(() => fixture.Reader.SelectedText == "H");
        ScopedInput(() => fixture.Window.MouseMove(Map(end)));
        await Until(() => fixture.Reader.SelectedText == "Hello");
        ScopedInput(() => fixture.Window.MouseUp(Map(end), MouseButton.Left));
        Assert.Equal("Hello", fixture.Reader.SelectedText);
    });

    [Fact]
    public Task Touchpad_magnify_zooms_around_the_pointer() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await TouchFixture.Create(PdfFixture.ManyPages(3));
        fixture.Reader.ZoomLevel = 1;
        await fixture.Idle();
        var presenter = fixture.Presenter;
        var origin = presenter.GetPageBounds(0).Center;
        // IPointer is not implementable by user code; capture the platform pointer from a move.
        IPointer? pointer = null;
        void Capture(object? sender, PointerEventArgs args) => pointer = args.Pointer;
        presenter.AddHandler(InputElement.PointerMovedEvent, Capture);
        ScopedInput(() => fixture.Window.MouseMove(presenter.TranslatePoint(origin, fixture.Window)!.Value));
        HeadlessTestScope.Drain();
        presenter.RemoveHandler(InputElement.PointerMovedEvent, Capture);
        Assert.NotNull(pointer);
        // PointerDeltaEventArgs.Delta.X is the fractional scale change of this event.
        ScopedInput(() => presenter.RaiseEvent(Magnify(presenter, origin, pointer!, 0.25)));
        HeadlessTestScope.Drain();
        Assert.Equal(1.25, fixture.Reader.ZoomLevel, 3);
        ScopedInput(() => presenter.RaiseEvent(Magnify(presenter, origin, pointer!, -0.2)));
        HeadlessTestScope.Drain();
        Assert.Equal(1, fixture.Reader.ZoomLevel, 3);
    });

    private static PointerDeltaEventArgs Magnify(Visual root, Point origin, IPointer pointer, double delta) => new(
        InputElement.PointerTouchPadGestureMagnifyEvent, new object(), pointer, root, origin,
        0, default, KeyModifiers.None, new Vector(delta, 0));

    private static void ScopedInput(Action input)
    {
        var caller = ExecutionContext.Capture();
        if (caller is null) input();
        else ExecutionContext.Run(caller, _ => input(), null);
    }

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            HeadlessTestScope.Drain();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class TouchFixture : IDisposable
    {
        internal Reader Reader { get; } = new();
        internal Window Window { get; }
        internal PdfDocumentPresenter Presenter => (PdfDocumentPresenter)Reader.DocumentPresenter!;

        private TouchFixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<TouchFixture> Create(byte[] bytes)
        {
            var fixture = new TouchFixture();
            try
            {
                await fixture.Reader.LoadDocumentAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
                fixture.Window.UpdateLayout();
                HeadlessTestScope.Drain();
                await fixture.Reader.WaitForIdleAsync(TestContext.Current.CancellationToken);
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
