using System.Buffers.Binary;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class CircularAnimationExportTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public void Entrance_uses_normalized_angle_and_current_frame_hit_and_labels()
    {
        using var host = new Host(ChartAnimationTriggers.Entrance);
        Assert.Equal(0, host.Chart.AnimationProgress);
        Assert.All(host.Chart.GetSlices(), s => { Assert.Equal(270, s.StartAngle); Assert.Equal(0, s.SweepAngle); });
        host.Advance(500);
        var slices = host.Chart.GetSlices();
        Assert.Equal(90, slices[0].SweepAngle, 8); Assert.Equal(270, slices[0].StartAngle, 8);
        Assert.Equal(1, host.Chart.HitTest(Polar(slices[0].Center, 45, slices[0].OuterRadius * .8))?.Data.SourceIndex);
        Assert.Null(host.Chart.HitTest(Polar(slices[0].Center, 180, slices[0].OuterRadius * .8)));
        Assert.All(host.Chart.GetLabels(), label => Assert.Contains(slices, s => ReferenceEquals(s, label.Slice)));
        host.Advance(500); Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); Assert.Equal(1, host.Chart.AnimationProgress);
    }

    [AvaloniaFact]
    public void Data_transition_preserves_target_business_data_and_selected_current_frame_anchor()
    {
        using var host = new Host(); host.Enable(ChartAnimationTriggers.Data);
        Assert.True(host.Chart.SelectSlice(host.Chart.GetSlices()[0])); host.Drain();
        host.Series.ItemsSource = new[] { new Row("a", 20, "Blue"), new Row("b", 80, "Red") };
        host.Drain(); host.Advance(500);
        var slice = host.Chart.GetSlices()[0];
        Assert.Equal(126, slice.SweepAngle, 8); Assert.Equal(.2, slice.Fraction, 8); Assert.Equal(20, slice.Data.Point.Y);
        Assert.Equal(slice.Hit.Position, host.Chart.SelectedPoint!.Position);
        Assert.Equal("a", host.Chart.SelectedPoint.StableId); Assert.Equal(20, host.Chart.SelectedPoint.Point.Y);
        Assert.Equal(20, host.Series.CreateSnapshot().Points[0].Point.Y);
    }

    [AvaloniaFact]
    public void View_trigger_animates_angle_hole_and_radius_without_data_trigger()
    {
        using var host = new Host(); host.Enable(ChartAnimationTriggers.Viewport);
        var before = host.Chart.GetSlices()[0];
        host.Series.InnerRadius = .5; host.Series.Radius = .75; host.Series.StartAngle = 0;
        host.Drain(); Assert.Equal(ChartAnimationState.Running, host.Chart.AnimationState);
        host.Advance(500); var mid = host.Chart.GetSlices()[0];
        Assert.Equal(before.OuterRadius * .875, mid.OuterRadius, 7);
        Assert.Equal(before.OuterRadius * .1875, mid.InnerRadius, 7);
        host.Advance(500); Assert.Equal(0, host.Chart.GetSlices()[0].StartAngle);
    }

    [AvaloniaFact]
    public void Delay_pause_resume_and_cancel_preserve_latest_target()
    {
        using var host = new Host(); host.Enable(ChartAnimationTriggers.Data); host.Chart.AnimationDelay = TimeSpan.FromMilliseconds(200);
        host.Series.ItemsSource = new[] { new Row("a", 20, "Blue"), new Row("b", 80, "Red") }; host.Drain();
        Assert.Equal(ChartAnimationState.Delayed, host.Chart.AnimationState); host.Advance(100); Assert.Equal(0, host.Chart.AnimationProgress);
        host.Chart.PauseAnimations(); host.Advance(1000); Assert.Equal(0, host.Chart.AnimationProgress);
        host.Chart.ResumeAnimations(); host.Advance(600); Assert.Equal(.5, host.Chart.AnimationProgress, 8);
        host.Series.ItemsSource = new[] { new Row("a", 10, "Blue"), new Row("b", 90, "Red") };
        host.Chart.CancelAnimations();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); Assert.Equal(36, host.Chart.GetSlices()[0].SweepAngle, 8);
    }

    [AvaloniaFact]
    public void Outgoing_sectors_are_painted_but_not_hit_or_reselected()
    {
        using var host = new Host(); host.Enable(ChartAnimationTriggers.Data);
        var removed = host.Chart.GetSlices()[1]; host.Chart.SelectSlice(removed); host.Drain();
        host.Series.ItemsSource = new[] { new Row("a", 100, "Blue") }; host.Drain(); host.Advance(500);
        var outgoing = Assert.Single(host.Chart.GetSlices(), s => s.IsExiting);
        Assert.True(outgoing.IsVisible); Assert.NotEqual(0, outgoing.SweepAngle);
        Assert.False(host.Chart.SelectSlice(outgoing)); Assert.Empty(host.Chart.SelectedPoints);
        Assert.NotEqual("b", host.Chart.HitTest(outgoing.Anchor)?.Data.StableId);
        host.Advance(500); Assert.Single(host.Chart.GetSlices());
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reduced_motion_and_hidden_ancestors_settle_paused_motion(bool reduce)
    {
        using var host = new Host(); host.Enable(ChartAnimationTriggers.Entrance); host.Chart.ReplayEntranceAnimation(); host.Drain(); host.Advance(200); host.Chart.PauseAnimations();
        if (reduce) host.Chart.ReduceMotion = true; else host.Panel.IsVisible = false;
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); Assert.Equal(1, host.Chart.AnimationProgress);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Invalid_application_easing_falls_back_to_linear(bool throws)
    {
        using var host = new Host(); host.Chart.AnimationEasing = new BadEasing(throws); host.Chart.AnimationDuration = TimeSpan.FromSeconds(1); host.Chart.AnimationTriggers = ChartAnimationTriggers.Entrance;
        host.Chart.ReplayEntranceAnimation(); host.Drain(); host.Advance(500);
        Assert.Equal(.5, host.Chart.AnimationProgress); Assert.Equal(90, host.Chart.GetSlices()[0].SweepAngle, 8);
    }

    [AvaloniaFact]
    public void Huge_finite_angles_and_reentrant_easing_never_publish_nonfinite_frames()
    {
        using var host = new Host(); host.Series.StartAngle = double.MaxValue; host.Enable(ChartAnimationTriggers.Entrance); host.Chart.ReplayEntranceAnimation(); host.Drain(); host.Advance(500);
        Assert.All(host.Chart.GetSlices(), s => Assert.True(double.IsFinite(s.StartAngle)));
        host.Chart.AnimationEasing = new CallbackEasing(() => host.Chart.CancelAnimations()); host.Chart.ReplayEntranceAnimation(); host.Drain(); host.Advance(200);
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); Assert.Equal(1, host.Chart.AnimationProgress);
    }

    [AvaloniaTheory]
    [InlineData(96)]
    [InlineData(144)]
    [InlineData(192)]
    public void Circular_export_layout_and_recorded_capture_use_fixed_pixel_size_and_requested_density(int dpi)
    {
        // The Headless bitmap Save/ArcTo implementation is not a native PNG
        // encoder. Native smoke independently checks actual bytes and pixels.
        using var host = new Host();
        var bounds = host.Chart.Bounds; var slices = host.Chart.GetSlices();
        var layout = host.Chart.GetExportLayout(new() { PixelSize = new(320, 240), Dpi = dpi, Background = Brushes.White });
        Assert.Equal(new PixelSize(320, 240), layout.Pixels); Assert.Equal(dpi, layout.Dpi); Assert.Equal(.64, layout.Scale, 8); Assert.Equal(new Vector(0, 24), layout.Offset);
        var recording = new DrawingGroup(); using (var drawing = recording.Open()) host.Chart.RenderExport(drawing, layout, Brushes.White);
        var target = Assert.IsType<DrawingGroup>(Assert.Single(recording.Children));
        Assert.Equal(new Rect(0, 0, 320, 240), Assert.IsType<RectangleGeometry>(target.ClipGeometry).Rect);
        var fit = Assert.Single(target.Children.OfType<DrawingGroup>());
        Assert.Equal(new Matrix(.64, 0, 0, .64, 0, 24), fit.Transform?.Value);
        Assert.Equal(bounds, host.Chart.Bounds); Assert.Equal(slices.Select(s => (s.StartAngle, s.SweepAngle)), host.Chart.GetSlices().Select(s => (s.StartAngle, s.SweepAngle)));
    }

    [AvaloniaFact]
    public void Fractional_center_content_and_chart_are_exported_in_one_visual_coordinate_scene()
    {
        var content = new Border { Width = 600, Height = 300, Background = Brushes.Magenta };
        var chart = new CircularChart { ShowLegend = false, ShowDataLabels = false, SelectionMode = ChartSelectionMode.None, Padding = new(0), CenterContent = content };
        chart.Series.Add(new DonutSeries { ItemsSource = new[] { 100d } });
        chart.Measure(new Size(700, 530)); chart.Arrange(new Rect(0, 0, 700, 530));
        Assert.NotEqual(Math.Round(chart.CenterContentBounds.Width), chart.CenterContentBounds.Width);
        var layout = chart.GetExportLayout(new() { Dpi = 192 });
        var fit = new Matrix(layout.Scale, 0, 0, layout.Scale, layout.Offset.X, layout.Offset.Y);
        var recording = new DrawingGroup(); using (var drawing = recording.Open()) chart.RenderExport(drawing, layout, null);
        AssertDirectVectorTree(recording);
        var painted = Assert.Single(RecordedGeometry(recording), value => ColorOf(value.Drawing) == Colors.Magenta);
        Assert.Equal(new Rect(content.Bounds.Size), painted.Drawing.Geometry!.Bounds);
        var native = content.TransformToVisual(chart)!.Value * fit;
        foreach (var point in new[] { default(Point), new Point(17.25, 31.5), new Point(600, 300) })
            AssertPoint(native.Transform(point), painted.Transform.Transform(point));
        var clip = Assert.Single(painted.Clips, value => value.Geometry is RectangleGeometry rect && rect.Rect.Size == chart.CenterContentBounds.Size);
        Assert.NotEqual(Math.Round(clip.Geometry.Bounds.Width), clip.Geometry.Bounds.Width);
        AssertRect(chart.CenterContentBounds.TransformToAABB(fit), clip.Geometry.Bounds.TransformToAABB(clip.Transform));
    }

    [AvaloniaFact]
    public void Center_tree_export_keeps_native_nested_transforms_stable_z_order_opacity_and_clips()
    {
        var firstColor = Color.Parse("#EA1271"); var secondColor = Color.Parse("#148E2A"); var thirdColor = Color.Parse("#2B31E9");
        var first = new RenderProbe(firstColor) { Width = 30.25, Height = 24.5, ZIndex = 4, Opacity = .5,
            RenderTransform = new ScaleTransform(1.2, .8), RenderTransformOrigin = new RelativePoint(.25, .75, RelativeUnit.Relative), FlowDirection = FlowDirection.RightToLeft };
        var second = new RenderProbe(secondColor) { Width = 27.5, Height = 21.25, ZIndex = -2, RenderTransform = new TranslateTransform(3.5, -2.125) };
        var third = new RenderProbe(thirdColor) { Width = 19.75, Height = 18.5, ZIndex = 4 };
        Canvas.SetLeft(first, 7.25); Canvas.SetTop(first, 9.5);
        Canvas.SetLeft(second, 14.125); Canvas.SetTop(second, 4.25);
        Canvas.SetLeft(third, 26.75); Canvas.SetTop(third, 19.125);
        var geometryClip = new EllipseGeometry(new Rect(2.25, 1.5, 136, 100));
        var canvas = new Canvas { Width = 140.5, Height = 104.25, Opacity = .7, ClipToBounds = true,
            Clip = geometryClip, OpacityMask = new SolidColorBrush(Colors.White, .8), RenderTransform = new RotateTransform(17),
            RenderTransformOrigin = new RelativePoint(.25, .75, RelativeUnit.Relative), Children = { first, second, third,
                new RenderProbe(Colors.Orange) { Width = 10, Height = 10, IsVisible = false },
                new RenderProbe(Colors.Yellow) { Width = 10, Height = 10, Opacity = 0 } } };
        var chart = new CircularChart { ShowLegend = false, ShowDataLabels = false, SelectionMode = ChartSelectionMode.None, CenterContent = canvas };
        chart.Series.Add(new DonutSeries { ItemsSource = new[] { 1d } });
        chart.Measure(new Size(640, 440)); chart.Arrange(new Rect(0, 0, 640, 440));
        var layout = chart.GetExportLayout(new() { PixelSize = new(800, 600), Dpi = 144 });
        var fit = new Matrix(layout.Scale, 0, 0, layout.Scale, layout.Offset.X, layout.Offset.Y);
        var recording = new DrawingGroup(); using (var drawing = recording.Open()) chart.RenderExport(drawing, layout, null);
        AssertDirectVectorTree(recording);
        var colored = RecordedGeometry(recording).Where(value => new[] { firstColor, secondColor, thirdColor }.Contains(ColorOf(value.Drawing) ?? default)).ToArray();
        Assert.Equal(new[] { secondColor, firstColor, thirdColor }, colored.Select(value => ColorOf(value.Drawing)!.Value));
        foreach (var (probe, color, opacity) in new[] { (first, firstColor, .35), (second, secondColor, .7), (third, thirdColor, .7) })
        {
            var actual = Assert.Single(colored, value => ColorOf(value.Drawing) == color);
            Assert.Equal(opacity, actual.Opacity, 12);
            var native = probe.TransformToVisual(chart)!.Value * fit;
            foreach (var point in new[] { new Point(3.25, 4.75), new Point(probe.Bounds.Width, probe.Bounds.Height) })
                AssertPoint(native.Transform(point), actual.Transform.Transform(point));
            Assert.Contains(actual.Clips, clip => clip.Geometry is EllipseGeometry && clip.Geometry.Bounds == geometryClip.Bounds);
            Assert.Contains(actual.Groups, group => group.OpacityMask is ISolidColorBrush brush && brush.Color == Colors.White && brush.Opacity == .8);
        }
        Assert.DoesNotContain(RecordedGeometry(recording), value => ColorOf(value.Drawing) is { } color && (color == Colors.Orange || color == Colors.Yellow));
    }

    [AvaloniaFact]
    public void Offscreen_center_parent_is_culled_while_its_translated_child_is_still_rendered()
    {
        var outsideColor = Color.Parse("#D31982"); var insideColor = Color.Parse("#16BD43");
        var child = new RenderProbe(insideColor) { Width = 12.5, Height = 15.25 };
        Canvas.SetLeft(child, -1000); Canvas.SetTop(child, -1000);
        var outside = new Canvas { Width = 20, Height = 20, Background = new SolidColorBrush(outsideColor), Children = { child } };
        Canvas.SetLeft(outside, 1000); Canvas.SetTop(outside, 1000);
        var canvas = new Canvas { Width = 120, Height = 90, Children = { outside } };
        var chart = new CircularChart { ShowLegend = false, ShowDataLabels = false, SelectionMode = ChartSelectionMode.None, CenterContent = canvas };
        chart.Series.Add(new DonutSeries { ItemsSource = new[] { 1d } });
        chart.Measure(new Size(600, 400)); chart.Arrange(new Rect(0, 0, 600, 400));
        var layout = chart.GetExportLayout(new());
        var recording = new DrawingGroup(); using (var drawing = recording.Open()) chart.RenderExport(drawing, layout, null);
        AssertDirectVectorTree(recording);
        var commands = RecordedGeometry(recording).ToArray();
        Assert.DoesNotContain(commands, value => ColorOf(value.Drawing) == outsideColor);
        var actual = Assert.Single(commands, value => ColorOf(value.Drawing) == insideColor);
        AssertPoint(child.TransformToVisual(chart)!.Value.Transform(new Point(3, 4)), actual.Transform.Transform(new Point(3, 4)));
    }

    [AvaloniaFact]
    public void Selection_before_offline_arrangement_is_visible_to_the_first_export_layout()
    {
        var series = new PieSeries { ItemsSource = new[] { new Row("a", 50, "Blue"), new Row("b", 50, "Red") }, ValuePath = "Value", IdPath = "Id" };
        var chart = new CircularChart { ShowLegend = false, ShowDataLabels = false }; chart.Series.Add(series);
        chart.SelectedPoint = new ChartPointHit(series, new(1, 50), 1, default) { StableId = "b" };
        chart.Measure(new Size(500, 300)); chart.Arrange(new Rect(0, 0, 500, 300));
        _ = chart.GetExportLayout(new()); Assert.Equal("b", chart.SelectedPoint?.StableId);
        Assert.True(chart.GetSlices()[1].Center.X < chart.PlotArea.Center.X);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void State_callback_cancellation_never_leaves_idle_with_incomplete_progress(bool reduced)
    {
        using var host = new Host(); host.Enable(ChartAnimationTriggers.Entrance);
        host.Chart.PropertyChanged += (_, e) =>
        {
            if (e.Property != CircularChart.AnimationStateProperty || host.Chart.AnimationState != ChartAnimationState.Running) return;
            if (reduced) host.Chart.ReduceMotion = true; else host.Chart.CancelAnimations();
        };
        host.Chart.ReplayEntranceAnimation(); host.Drain();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); Assert.Equal(1, host.Chart.AnimationProgress);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Easing_pause_resume_cannot_reenter_the_frame_updater_or_overwrite_paused_state(bool resume)
    {
        using var host = new Host(); var calls = 0;
        host.Chart.AnimationEasing = new CallbackEasing(() => { if (++calls != 1) return; host.Chart.PauseAnimations(); if (resume) host.Chart.ResumeAnimations(); });
        host.Chart.AnimationTriggers = ChartAnimationTriggers.Entrance; host.Chart.ReplayEntranceAnimation(); host.Drain(); host.Advance(500);
        Assert.Equal(1, calls);
        Assert.Equal(resume ? ChartAnimationState.Running : ChartAnimationState.Paused, host.Chart.AnimationState);
        Assert.Equal(!resume, host.Chart.IsAnimationPaused);
    }

    [AvaloniaFact]
    public void Running_state_observer_pauses_the_already_published_zero_progress_frame()
    {
        using var host = new Host(); host.Enable(ChartAnimationTriggers.Entrance);
        host.Chart.PropertyChanged += (_, e) => { if (e.Property == CircularChart.AnimationStateProperty && host.Chart.AnimationState == ChartAnimationState.Running) host.Chart.PauseAnimations(); };
        host.Chart.ReplayEntranceAnimation(); host.Drain();
        Assert.Equal(ChartAnimationState.Paused, host.Chart.AnimationState); Assert.Equal(0, host.Chart.AnimationProgress);
        Assert.All(host.Chart.GetSlices(), s => Assert.Equal(0, s.SweepAngle));
    }

    [AvaloniaFact]
    public void New_animation_state_bindings_are_published_after_drawing()
    {
        using var host = new Host(); host.Enable(ChartAnimationTriggers.Data);
        var inDrawing = false; var callbackInDrawing = false; var status = new TextBlock();
        host.Chart.PropertyChanged += (_, e) => { if (e.Property != CircularChart.AnimationStateProperty) return; callbackInDrawing |= inDrawing; status.Text = host.Chart.AnimationState.ToString(); };
        host.Series.ItemsSource = new[] { new Row("a", 25, "Blue"), new Row("b", 75, "Red") };
        inDrawing = true; var drawing = new DrawingGroup(); using (var context = drawing.Open()) host.Chart.Render(context); inDrawing = false;
        host.Drain(); Assert.False(callbackInDrawing); Assert.Equal("Running", status.Text);
    }

    [AvaloniaFact]
    public void Formatter_actions_are_published_after_the_render_pass()
    {
        using var host = new Host(); var requested = false; var callbackInFormatter = false; var inFormatter = false;
        host.Chart.SelectionChanged += (_, _) => callbackInFormatter |= inFormatter;
        host.Chart.ShowDataLabels = true;
        host.Chart.LabelFormatter = slice =>
        {
            inFormatter = true;
            try { if (!requested) { requested = true; host.Chart.SelectSlice(slice); host.Chart.CancelAnimations(); } }
            finally { inFormatter = false; }
            return "Share";
        };
        // RunJobs/layout do not guarantee a Headless compositor render tick.
        var drawing = new DrawingGroup(); using (var context = drawing.Open()) host.Chart.Render(context);
        host.Drain();
        Assert.True(requested); Assert.False(callbackInFormatter); Assert.NotNull(host.Chart.SelectedPoint);
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState);
    }

    [AvaloniaFact]
    public void Headless_sync_save_failure_is_forwarded_and_leaves_caller_stream_open()
    {
        using var host = new Host(); using var stream = new MemoryStream(); ChartExportFailedEventArgs? failure = null;
        host.Chart.ExportFailed += (_, e) => failure = e;
        Assert.False(host.Chart.TryExportPng(stream));
        Assert.NotNull(failure); output.WriteLine(failure.Exception.ToString());
        Assert.True(stream.CanWrite); Assert.Equal(0, stream.Length);
    }

    [AvaloniaFact]
    public async Task Circular_async_png_jpeg_cancellation_and_invalid_options_report_correctly()
    {
        using var host = new Host(); var failures = 0; host.Chart.ExportFailed += (sender, _) => { Assert.Same(host.Chart, sender); failures++; };
        using var png = new MemoryStream(); Assert.True(await host.Chart.TryExportPngAsync(png, new() { PixelSize = new(120, 90) }));
        using var jpeg = new MemoryStream(); Assert.True(await host.Chart.TryExportJpegAsync(jpeg, new() { PixelSize = new(120, 90), Dpi = 144 })); Assert.Equal(0xFF, jpeg.ToArray()[0]);
        using var cancelled = new MemoryStream(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Chart.TryExportPngAsync(cancelled, cancellationToken: new CancellationToken(true))); Assert.Equal(0, failures); Assert.Equal(0, cancelled.Length);
        using var invalid = new MemoryStream(); Assert.False(host.Chart.TryExportJpeg(invalid, new() { JpegQuality = 0 })); Assert.Equal(1, failures); Assert.Equal(0, invalid.Length);
    }

    private static Point Polar(Point center, double angle, double radius) => center + new Vector(Math.Cos(angle * Math.PI / 180) * radius, Math.Sin(angle * Math.PI / 180) * radius);
    private static Color? ColorOf(GeometryDrawing drawing) => (drawing.Brush as ISolidColorBrush)?.Color;
    private static void AssertPoint(Point expected, Point actual)
    { Assert.Equal(expected.X, actual.X, 9); Assert.Equal(expected.Y, actual.Y, 9); }
    private static void AssertRect(Rect expected, Rect actual)
    { Assert.Equal(expected.X, actual.X, 9); Assert.Equal(expected.Y, actual.Y, 9); Assert.Equal(expected.Width, actual.Width, 9); Assert.Equal(expected.Height, actual.Height, 9); }
    private static void AssertDirectVectorTree(Drawing drawing)
    {
        var descendants = Flatten(drawing).ToArray();
        Assert.Empty(descendants.OfType<ImageDrawing>());
        Assert.DoesNotContain(descendants.OfType<GeometryDrawing>(), geometry => geometry.Brush is VisualBrush);
    }
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var descendant in Flatten(child)) yield return descendant;
    }
    private sealed record RecordedClip(Geometry Geometry, Matrix Transform);
    private sealed record GeometryCommand(GeometryDrawing Drawing, Matrix Transform, double Opacity,
        IReadOnlyList<RecordedClip> Clips, IReadOnlyList<DrawingGroup> Groups);
    private static IEnumerable<GeometryCommand> RecordedGeometry(Drawing drawing) => Visit(drawing, Matrix.Identity, 1, [], []);
    private static IEnumerable<GeometryCommand> Visit(Drawing drawing, Matrix parent, double opacity,
        IReadOnlyList<RecordedClip> clips, IReadOnlyList<DrawingGroup> groups)
    {
        if (drawing is GeometryDrawing geometry) yield return new(geometry, parent, opacity, clips, groups);
        if (drawing is not DrawingGroup group) yield break;
        var transform = (group.Transform?.Value ?? Matrix.Identity) * parent;
        if (group.ClipGeometry is { } clip) clips = clips.Append(new(clip, transform)).ToArray();
        groups = groups.Append(group).ToArray();
        foreach (var child in group.Children)
            foreach (var item in Visit(child, transform, opacity * group.Opacity, clips, groups)) yield return item;
    }
    private sealed class RenderProbe(Color color) : Control
    {
        public override void Render(DrawingContext context) => context.DrawRectangle(new SolidColorBrush(color), null, new Rect(Bounds.Size));
    }
    private sealed record Row(string Id, double Value, string Color);
    private sealed class BadEasing(bool throws) : Easing { public override double Ease(double progress) => throws ? throw new InvalidOperationException() : double.NaN; }
    private sealed class CallbackEasing(Action callback) : Easing { public override double Ease(double progress) { callback(); return progress; } }
    private sealed class ManualClock : TimeProvider
    { public long Ticks; public override long TimestampFrequency => TimeSpan.TicksPerSecond; public override long GetTimestamp() => Ticks; }
    private sealed class Host : IDisposable
    {
        public PieSeries Series { get; } = new() { ItemsSource = new[] { new Row("a", 50, "Blue"), new Row("b", 50, "Red") }, ValuePath = "Value", IdPath = "Id", CategoryPath = "Id", ColorPath = "Color" };
        public CircularChart Chart { get; }
        public Panel Panel { get; }
        private Window Window { get; }
        private ManualClock Clock { get; } = new();
        public Host(ChartAnimationTriggers triggers = ChartAnimationTriggers.None)
        {
            Chart = new() { ShowLegend = false, ShowDataLabels = false, Padding = new(0), AnimationTimeProvider = Clock, AnimationDuration = TimeSpan.FromSeconds(1), AnimationEasing = null, AnimationTriggers = triggers };
            Chart.Series.Add(Series); Panel = new Grid { Children = { Chart } }; Window = new() { Width = 500, Height = 300, Content = Panel }; Window.Show(); Drain();
        }
        public void Enable(ChartAnimationTriggers triggers) { Chart.AnimationTriggers = triggers; Chart.AnimationDuration = TimeSpan.FromSeconds(1); Chart.AnimationEasing = null; Drain(); }
        public void Advance(double ms) { Clock.Ticks += TimeSpan.FromMilliseconds(ms).Ticks; Chart.UpdateAnimationFrame(); Drain(); }
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = Chart.GetSlices(); }
        public void Dispose() => Window.Close();
    }
}
