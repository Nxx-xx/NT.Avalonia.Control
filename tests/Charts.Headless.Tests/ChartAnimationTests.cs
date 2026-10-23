using Avalonia;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartAnimationTests
{
    [AvaloniaFact]
    public void Disabled_default_updates_immediately_and_keeps_target_source_unmodified()
    {
        var series = Series("line", [new(2, 20), new(8, 80)]);
        using var host = new Host(series);
        Assert.Equal(ChartAnimationTriggers.None, host.Chart.AnimationTriggers);
        Assert.Equal(TimeSpan.FromMilliseconds(450), host.Chart.AnimationDuration);
        Assert.Equal(TimeSpan.Zero, host.Chart.AnimationDelay);
        Assert.Equal(10_000, host.Chart.AnimationPointLimit);
        series.ItemsSource = new ChartPoint[] { new(2, 70), new(8, 30) }; host.Drain();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState);
        AssertHit(host, new(2, 70));
        Assert.Equal(new ChartPoint(2, 70), series.CreateSnapshot().Points[0].Point);
    }

    [AvaloniaTheory]
    [InlineData("line")]
    [InlineData("bar")]
    [InlineData("scatter")]
    public void Entrance_grows_from_zero_to_target_and_all_queries_share_current_frame(string kind)
    {
        var series = Series(kind, [new(2, 20), new(8, 80)]);
        using var host = new Host(series); host.Enable(ChartAnimationTriggers.Entrance);
        host.Chart.ReplayEntranceAnimation(); host.Drain();
        if (kind == "bar") Assert.Null(host.Chart.HitTestPoint(host.Project(new(8, 0)), 1));
        else AssertHit(host, new(8, 0));
        host.Advance(500);
        Assert.Equal(.5, host.Chart.AnimationProgress, 9); AssertHit(host, new(8, 40));
        var state = host.Chart.AnimationState;
        host.Clock.Advance(TimeSpan.FromSeconds(10));
        // A query, a drawing recording and export preparation must not advance time.
        var drawing = new DrawingGroup(); using (var context = drawing.Open()) host.Chart.Render(context);
        AssertHit(host, new(8, 40)); Assert.Equal(.5, host.Chart.AnimationProgress, 9); Assert.Equal(state, host.Chart.AnimationState);
        host.Chart.UpdateAnimationFrame(); host.Drain();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); AssertHit(host, new(8, 80));
        Assert.Equal(new ChartPoint(8, 80), series.CreateSnapshot().Points[1].Point);
    }

    [AvaloniaFact]
    public void Delay_and_quadratic_easing_follow_elapsed_time_without_early_motion()
    {
        using var host = new Host(Series("scatter", [new(5, 80)])); host.Enable(ChartAnimationTriggers.Entrance);
        host.Chart.AnimationDelay = TimeSpan.FromMilliseconds(200); host.Chart.AnimationEasing = new SquareEasing();
        host.Chart.ReplayEntranceAnimation(); host.Drain();
        Assert.Equal(ChartAnimationState.Delayed, host.Chart.AnimationState);
        host.Advance(199); Assert.Equal(0, host.Chart.AnimationProgress); AssertHit(host, new(5, 0));
        host.Advance(1); Assert.Equal(0, host.Chart.AnimationProgress);
        host.Advance(500); Assert.Equal(.25, host.Chart.AnimationProgress, 9); AssertHit(host, new(5, 20));
        host.Advance(500); Assert.Equal(1, host.Chart.AnimationProgress); AssertHit(host, new(5, 80));
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(250)]
    public void Pause_freezes_delay_or_running_elapsed_and_resume_excludes_paused_time(int elapsedBeforePause)
    {
        using var host = new Host(Series("scatter", [new(5, 80)])); host.Enable(ChartAnimationTriggers.Entrance);
        host.Chart.AnimationDelay = TimeSpan.FromMilliseconds(100); host.Chart.ReplayEntranceAnimation(); host.Drain();
        host.Advance(elapsedBeforePause); var before = host.Chart.AnimationProgress;
        host.Chart.IsAnimationPaused = true; host.Drain(); host.Advance(10_000);
        Assert.Equal(ChartAnimationState.Paused, host.Chart.AnimationState); Assert.Equal(before, host.Chart.AnimationProgress);
        host.Chart.IsAnimationPaused = false; host.Drain();
        host.Advance(600);
        var expected = Math.Clamp((elapsedBeforePause + 600 - 100) / 1000d, 0, 1);
        Assert.Equal(expected, host.Chart.AnimationProgress, 9); AssertHit(host, new(5, 80 * expected));
    }

    [AvaloniaTheory]
    [InlineData(ChartAnimationTriggers.Entrance)] [InlineData(ChartAnimationTriggers.Data)]
    public void Native_pointer_enter_and_leave_preserve_running_animation_and_current_frame_tooltip(ChartAnimationTriggers trigger)
    {
        var series = Series("scatter", [new(5, trigger == ChartAnimationTriggers.Entrance ? 80 : 20)]);
        using var host = new Host(series, chart => chart.Margin = new Thickness(20));
        host.Window.MouseMove(new(5, 5)); host.Drain(); Assert.False(host.Chart.IsPointerOver);
        host.Enable(trigger); if (trigger == ChartAnimationTriggers.Entrance) host.Chart.ReplayEntranceAnimation(); else series.ItemsSource = new ChartPoint[] { new(5, 80) }; host.Drain(); host.Advance(250);
        var frameY = trigger == ChartAnimationTriggers.Entrance ? 20 : 35; var progress = host.Chart.AnimationProgress;
        host.Window.MouseMove(host.Chart.TranslatePoint(host.Project(new(5, frameY)), host.Window)!.Value); host.Drain();
        Assert.True(host.Chart.IsPointerOver); Assert.Equal(ChartAnimationState.Running, host.Chart.AnimationState); Assert.Equal(progress, host.Chart.AnimationProgress);
        var hovered = Assert.IsType<ChartPointHit>(host.Chart.HoveredPoint); Assert.Same(series, hovered.Series); AssertPoint(host.Project(new(5, frameY)), hovered.Position);
        host.Window.MouseMove(new(5, 5)); host.Drain(); Assert.False(host.Chart.IsPointerOver); Assert.Null(host.Chart.HoveredPoint); Assert.Equal(ChartAnimationState.Running, host.Chart.AnimationState); Assert.Equal(progress, host.Chart.AnimationProgress); AssertHit(host, new(5, frameY));
        host.Advance(250); Assert.Equal(.5, host.Chart.AnimationProgress, 9); AssertHit(host, new(5, trigger == ChartAnimationTriggers.Entrance ? 40 : 50));
    }

    [AvaloniaTheory]
    [InlineData(ChartAnimationTriggers.Entrance)] [InlineData(ChartAnimationTriggers.Data)]
    public void Native_pointer_enter_and_leave_preserve_paused_drawing_and_export_frame(ChartAnimationTriggers trigger)
    {
        var color = Color.Parse("#F2A33A"); var series = new ScatterSeries { ItemsSource = new ChartPoint[] { new(5, trigger == ChartAnimationTriggers.Entrance ? 80 : 20) }, Fill = new SolidColorBrush(color), MarkerSize = 10 };
        using var host = new Host(series, chart => chart.Margin = new Thickness(20)); host.Window.MouseMove(new(5, 5)); host.Drain(); Assert.False(host.Chart.IsPointerOver);
        host.Enable(trigger); if (trigger == ChartAnimationTriggers.Entrance) host.Chart.ReplayEntranceAnimation(); else series.ItemsSource = new ChartPoint[] { new(5, 80) }; host.Drain(); host.Advance(250); host.Chart.IsAnimationPaused = true; host.Drain();
        var progress = host.Chart.AnimationProgress; var frameY = trigger == ChartAnimationTriggers.Entrance ? 20 : 35; var before = MarkerBounds(false);
        host.Window.MouseMove(host.Chart.TranslatePoint(host.Project(new(5, frameY)), host.Window)!.Value); host.Drain(); Assert.True(host.Chart.IsPointerOver); Assert.Equal(ChartAnimationState.Paused, host.Chart.AnimationState); Assert.Equal(progress, host.Chart.AnimationProgress); Assert.NotNull(host.Chart.HoveredPoint);
        host.Window.MouseMove(new(5, 5)); host.Drain(); Assert.False(host.Chart.IsPointerOver); Assert.Null(host.Chart.HoveredPoint); host.Advance(10_000); Assert.Equal(ChartAnimationState.Paused, host.Chart.AnimationState); Assert.Equal(progress, host.Chart.AnimationProgress); Assert.Equal(before, MarkerBounds(false)); Assert.Equal(before, MarkerBounds(true));
        using var stream = new MemoryStream(); host.Chart.TryExportPng(stream); // The headless encoder may reject saving; the production export renderer is checked above.
        Assert.Equal(ChartAnimationState.Paused, host.Chart.AnimationState); Assert.Equal(progress, host.Chart.AnimationProgress); AssertHit(host, new(5, frameY));
        host.Chart.IsAnimationPaused = false; host.Drain(); host.Advance(250); Assert.Equal(.5, host.Chart.AnimationProgress, 9); AssertHit(host, new(5, trigger == ChartAnimationTriggers.Entrance ? 40 : 50));

        Rect MarkerBounds(bool export)
        {
            var drawing = new DrawingGroup(); using (var context = drawing.Open())
                if (export) host.Chart.RenderExport(context, host.Chart.GetExportLayout(new() { PixelSize = new((int)host.Chart.Bounds.Width, (int)host.Chart.Bounds.Height) }), Brushes.White); else host.Chart.Render(context);
            IEnumerable<Drawing> Flatten(Drawing item) { yield return item; if (item is DrawingGroup group) foreach (var child in group.Children) foreach (var descendant in Flatten(child)) yield return descendant; }
            return Assert.Single(Flatten(drawing).OfType<GeometryDrawing>(), item => item.Brush is ISolidColorBrush brush && brush.Color == color).Geometry!.Bounds;
        }
    }

    [AvaloniaFact]
    public void Stable_ids_follow_reordered_target_items_without_using_their_old_ordinals()
    {
        var first = new Row("a", 2, 20); var second = new Row("b", 8, 80);
        var series = new ScatterSeries { ItemsSource = new[] { first, second }, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", MarkerSize = 10 };
        using var host = new Host(series); host.Enable(ChartAnimationTriggers.Data);
        var targetB = new Row("b", 6, 40); var targetA = new Row("a", 4, 60);
        series.ItemsSource = new[] { targetB, targetA }; host.Drain(); host.Advance(500);
        var b = AssertHit(host, new(7, 60)); var a = AssertHit(host, new(3, 40));
        Assert.Same(targetB, b.Item); Assert.Same(targetA, a.Item);
        Assert.Equal("b", b.StableId); Assert.Equal(0, b.Index); Assert.Equal("a", a.StableId); Assert.Equal(1, a.Index);
        Assert.Equal(new ChartPoint(6, 40), series.CreateSnapshot().Points[0].Point);
    }

    [AvaloniaFact]
    public void New_identity_grows_from_baseline_and_removed_identity_is_not_hit()
    {
        var series = new ScatterSeries { ItemsSource = new[] { new Row("old", 2, 80) }, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", MarkerSize = 10 };
        using var host = new Host(series); host.Enable(ChartAnimationTriggers.Data);
        var added = new Row("new", 8, 60); series.ItemsSource = new[] { added }; host.Drain(); host.Advance(500);
        Assert.Same(added, AssertHit(host, new(8, 30)).Item);
        Assert.Null(host.Chart.HitTestPoint(host.Project(new(2, 80)), 1));
    }

    [AvaloniaFact]
    public void Rapid_data_retarget_starts_at_presented_values_and_latest_target_wins()
    {
        var series = Series("scatter", [new(5, 20)]); using var host = new Host(series); host.Enable(ChartAnimationTriggers.Data);
        series.ItemsSource = new ChartPoint[] { new(5, 80) }; host.Drain(); host.Advance(500); AssertHit(host, new(5, 50));
        series.ItemsSource = new ChartPoint[] { new(5, 90) }; host.Drain(); AssertHit(host, new(5, 50));
        host.Advance(500); AssertHit(host, new(5, 70));
        host.Chart.CancelAnimations(); host.Drain(); AssertHit(host, new(5, 90));
        host.Advance(10_000); AssertHit(host, new(5, 90)); Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState);
    }

    [AvaloniaFact]
    public void Viewport_interpolates_effective_scene_without_overwriting_the_requested_target()
    {
        using var host = new Host(Series("scatter", [new(5, 50)])); host.Enable(ChartAnimationTriggers.Viewport);
        var target = new ChartViewport(2, 8, 20, 80); host.Chart.Viewport = target; host.Drain();
        Assert.Equal(target, host.Chart.Viewport); Assert.Equal(new ChartViewport(0, 10, 0, 100), host.Chart.VisibleRange);
        host.Advance(500);
        Assert.Equal(new ChartViewport(1, 9, 10, 90), host.Chart.VisibleRange); Assert.Equal(target, host.Chart.Viewport);
        host.Advance(500); Assert.Equal(target, host.Chart.VisibleRange);
    }

    [AvaloniaFact]
    public void Reduced_motion_completes_a_paused_transition_and_keeps_future_updates_immediate()
    {
        var series = Series("scatter", [new(5, 20)]); using var host = new Host(series); host.Enable(ChartAnimationTriggers.All);
        host.Chart.IsAnimationPaused = true; series.ItemsSource = new ChartPoint[] { new(5, 80) }; host.Drain();
        Assert.Equal(ChartAnimationState.Paused, host.Chart.AnimationState);
        host.Chart.ReduceMotion = true; host.Drain(); Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); AssertHit(host, new(5, 80));
        series.ItemsSource = new ChartPoint[] { new(5, 60) }; host.Chart.Viewport = new(2, 8, 20, 80); host.Drain();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); AssertHit(host, new(5, 60));
    }

    [AvaloniaFact]
    public void Point_budget_skips_the_whole_transition_and_zero_duration_ignores_delay()
    {
        var series = Series("scatter", [new(2, 20), new(8, 80)]); using var host = new Host(series); host.Enable(ChartAnimationTriggers.All);
        host.Chart.AnimationPointLimit = 1; host.Chart.ReplayEntranceAnimation(); host.Drain();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); AssertHit(host, new(8, 80));
        host.Chart.AnimationPointLimit = 10_000; host.Chart.AnimationDuration = TimeSpan.Zero; host.Chart.AnimationDelay = TimeSpan.FromDays(1);
        series.ItemsSource = new ChartPoint[] { new(2, 70), new(8, 30) }; host.Drain();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); AssertHit(host, new(8, 30));
    }

    [AvaloniaFact]
    public void Hide_and_detach_complete_and_do_not_allow_a_stale_frame_to_resume()
    {
        var series = Series("scatter", [new(5, 80)]); using var host = new Host(series); host.Enable(ChartAnimationTriggers.All);
        host.Chart.ReplayEntranceAnimation(); host.Advance(300); host.Chart.IsVisible = false; host.Drain();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState);
        host.Advance(2000); host.Chart.IsVisible = true; host.Drain(); host.Chart.CancelAnimations();
        host.Chart.ReplayEntranceAnimation(); host.Advance(200); host.Window.Content = null; host.Drain();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState);
        host.Advance(2000); host.Window.Content = host.Chart; host.Drain(); host.Chart.CancelAnimations();
        AssertHit(host, new(5, 80));
    }

    [AvaloniaFact]
    public void Plot_content_projection_and_annotation_follow_the_same_animated_view_as_hit_testing()
    {
        var child = new Border(); using var host = new Host(Series("scatter", [new(3, 30)]));
        host.Chart.PlotAreaContent = new object(); host.Chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => child);
        ChartAnnotationRenderContext? annotation = null;
        host.Chart.Annotations.Add(new CustomAnnotation { Renderer = (_, context) => annotation = context }); host.Drain();
        host.Enable(ChartAnimationTriggers.Viewport); host.Chart.Viewport = new(2, 8, 20, 80); host.Drain(); host.Advance(500);
        var result = new DrawingGroup(); using (var context = result.Open()) host.Chart.Render(context);
        var content = Assert.IsType<ChartPlotAreaContext>(child.DataContext);
        Assert.Equal(host.Chart.VisibleRange, content.VisibleRange);
        var expected = host.Project(new(3, 30));
        var local = content.Project(3, 30); AssertPoint(expected, new(local.X + content.PlotArea.X, local.Y + content.PlotArea.Y));
        Assert.NotNull(annotation); AssertPoint(expected, annotation.Projection.Project(new(3, 30)));
        AssertHit(host, new(3, 30));
    }

    [AvaloniaFact]
    public void Export_reads_the_current_frame_even_if_wall_time_has_advanced()
    {
        using var host = new Host(Series("scatter", [new(5, 80)])); host.Enable(ChartAnimationTriggers.Entrance);
        host.Chart.ReplayEntranceAnimation(); host.Drain(); host.Advance(250); host.Clock.Advance(TimeSpan.FromSeconds(30));
        using var output = new MemoryStream(); host.Chart.TryExportPng(output);
        Assert.Equal(.25, host.Chart.AnimationProgress, 9); AssertHit(host, new(5, 20));
        host.Chart.CancelAnimations(); host.Drain(); AssertHit(host, new(5, 80));
    }

    [AvaloniaTheory]
    [InlineData("duration")]
    [InlineData("delay")]
    [InlineData("limit")]
    [InlineData("flags")]
    public void Invalid_animation_configuration_is_rejected(string property)
    {
        var chart = new CartesianChart();
        Assert.Throws<ArgumentException>(() =>
        {
            switch (property)
            {
                case "duration": chart.AnimationDuration = TimeSpan.FromTicks(-1); break;
                case "delay": chart.AnimationDelay = TimeSpan.FromTicks(-1); break;
                case "limit": chart.AnimationPointLimit = -1; break;
                default: chart.AnimationTriggers = (ChartAnimationTriggers)16; break;
            }
        });
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nonfinite_or_throwing_application_easing_falls_back_to_linear_and_completes(bool throws)
    {
        using var host = new Host(Series("scatter", [new(5, 80)])); host.Enable(ChartAnimationTriggers.Entrance);
        host.Chart.AnimationEasing = new InvalidEasing(throws); host.Chart.ReplayEntranceAnimation(); host.Drain(); host.Advance(500);
        Assert.Equal(.5, host.Chart.AnimationProgress, 9); AssertHit(host, new(5, 40));
        host.Advance(500); Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); AssertHit(host, new(5, 80));
    }

    [AvaloniaFact]
    public void Logarithmic_view_animation_is_linear_in_transformed_space()
    {
        using var host = new Host(Series("scatter", [new(5, 100)]));
        host.Chart.YAxis = new LogarithmicAxis { Minimum = 1, Maximum = 10_000 }; host.Drain(); host.Enable(ChartAnimationTriggers.Viewport);
        host.Chart.Viewport = new(2, 8, 10, 1000); host.Drain(); host.Advance(500);
        var actual = host.Chart.VisibleRange!.Value;
        Assert.Equal(1, actual.XMinimum, 9); Assert.Equal(9, actual.XMaximum, 9);
        Assert.Equal(Math.Sqrt(10), actual.YMinimum, 9); Assert.Equal(Math.Sqrt(10_000_000), actual.YMaximum, 7);
        host.Advance(500); Assert.Equal(new ChartViewport(2, 8, 10, 1000), host.Chart.VisibleRange);
    }

    [AvaloniaFact]
    public void Explicit_secondary_axis_view_and_named_content_projection_animate_together()
    {
        var child = new Border(); using var host = new Host(Series("scatter", [new(5, 50)]));
        var extra = new LogarithmicAxis { Key = "extra", Minimum = 1, Maximum = 10_000 };
        host.Chart.Axes.Add(extra); host.Chart.Annotations.Add(new LineAnnotation { YAxisKey = "extra", Value = 100 });
        host.Chart.PlotAreaContent = new object(); host.Chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => child); host.Drain();
        host.Enable(ChartAnimationTriggers.Viewport); extra.ViewMinimum = 100; extra.ViewMaximum = 1000; host.Drain(); host.Advance(500);
        var context = Assert.IsType<ChartPlotAreaContext>(child.DataContext);
        Assert.True(context.TryGetVisibleRange(null, "extra", out var range));
        Assert.Equal(10, range.YMinimum, 8); Assert.Equal(Math.Sqrt(10_000_000), range.YMaximum, 7);
        Assert.True(context.TryGetProjection(null, "extra", out var projection));
        Assert.Equal(host.Chart.PlotArea.Height * .6, projection.MapY(100), 6);
    }

    [AvaloniaFact]
    public void Progress_callback_retarget_does_not_allow_the_old_frame_to_overwrite_newer_business_state()
    {
        var series = Series("scatter", [new(5, 20)]); using var host = new Host(series); host.Enable(ChartAnimationTriggers.Data);
        var changed = false;
        host.Chart.PropertyChanged += (_, change) =>
        {
            if (change.Property != CartesianChart.AnimationProgressProperty || changed || host.Chart.AnimationProgress < .5) return;
            changed = true; series.ItemsSource = new ChartPoint[] { new(5, 90) };
        };
        series.ItemsSource = new ChartPoint[] { new(5, 80) }; host.Drain(); host.Advance(500);
        Assert.True(changed); AssertHit(host, new(5, 50));
        host.Advance(500); AssertHit(host, new(5, 70)); host.Advance(500); AssertHit(host, new(5, 90));
    }

    [AvaloniaFact]
    public void Linked_charts_exchange_target_intent_once_instead_of_echoing_intermediate_frames()
    {
        using var first = new Host(Series("scatter", [new(5, 50)]));
        using var second = new Host(Series("scatter", [new(5, 50)]));
        first.Enable(ChartAnimationTriggers.Viewport); second.Enable(ChartAnimationTriggers.Viewport);
        using var group = new ChartSynchronizationGroup { Axes = ChartInteractionAxes.Both }; group.Add(first.Chart); group.Add(second.Chart);
        first.Chart.Viewport = new(2, 8, 20, 80); first.Drain(); second.Drain();
        Assert.Equal(first.Chart.Viewport, second.Chart.Viewport);
        first.Advance(500); second.Advance(250);
        Assert.Equal(new ChartViewport(1, 9, 10, 90), first.Chart.VisibleRange);
        Assert.Equal(new ChartViewport(.5, 9.5, 5, 95), second.Chart.VisibleRange);
        Assert.Equal(new ChartViewport(2, 8, 20, 80), second.Chart.Viewport);
        first.Advance(500); second.Advance(750);
        Assert.Equal(first.Chart.VisibleRange, second.Chart.VisibleRange); Assert.Equal(ChartAnimationState.Idle, first.Chart.AnimationState);
    }

    [AvaloniaFact]
    public void Ancestor_visibility_change_releases_motion_even_when_the_chart_itself_stays_visible()
    {
        using var host = new Host(Series("scatter", [new(5, 80)])); var parent = new Border();
        host.Window.Content = null; parent.Child = host.Chart; host.Window.Content = parent; host.Drain(); host.Enable(ChartAnimationTriggers.Entrance);
        host.Chart.ReplayEntranceAnimation(); host.Drain(); host.Advance(300); parent.IsVisible = false; host.Drain();
        Assert.True(host.Chart.IsVisible); Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); Assert.False(host.Chart.AnimationsActive);
        host.Advance(2000); parent.IsVisible = true; host.Drain(); Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); AssertHit(host, new(5, 80));
    }

    [AvaloniaFact]
    public void Shrinking_an_over_budget_previous_snapshot_also_skips_the_transition()
    {
        var series = Series("scatter", Enumerable.Range(0, 101).Select(index => new ChartPoint(index / 10d, 50)).ToArray());
        using var host = new Host(series); host.Enable(ChartAnimationTriggers.Data); host.Chart.AnimationPointLimit = 100;
        series.ItemsSource = new ChartPoint[] { new(5, 80) }; host.Drain();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); Assert.False(host.Chart.AnimationsActive); AssertHit(host, new(5, 80));
    }

    [AvaloniaFact]
    public void Direct_wheel_navigation_preempts_programmatic_view_motion()
    {
        using var host = new Host(Series("scatter", [new(5, 50)])); host.Enable(ChartAnimationTriggers.Viewport); host.Chart.IsZoomEnabled = true;
        host.Chart.Viewport = new(2, 8, 20, 80); host.Drain(); host.Advance(250);
        var pointer = host.Chart.TranslatePoint(host.Chart.PlotArea.Center, host.Window)!.Value;
        host.Window.MouseWheel(pointer, new Vector(0, 1)); host.Drain();
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); Assert.Equal(host.Chart.Viewport, host.Chart.VisibleRange);
        Assert.True(host.Chart.VisibleRange!.Value.XMaximum - host.Chart.VisibleRange.Value.XMinimum < 10);
    }

    [AvaloniaFact]
    public void Enabled_entrance_starts_automatically_on_first_usable_attachment()
    {
        using var host = new Host(Series("scatter", [new(5, 80)]), chart =>
        {
            chart.AnimationTriggers = ChartAnimationTriggers.Entrance; chart.AnimationDuration = TimeSpan.FromSeconds(1);
            chart.AnimationEasing = new LinearEasing(); chart.IsAnimationPaused = true;
        });
        Assert.Equal(ChartAnimationState.Paused, host.Chart.AnimationState); Assert.Equal(0, host.Chart.AnimationProgress); AssertHit(host, new(5, 0));
        host.Chart.IsAnimationPaused = false; host.Drain(); host.Advance(500); AssertHit(host, new(5, 40));
    }

    [AvaloniaFact]
    public void Deferred_observable_collection_changes_coalesce_into_one_latest_target()
    {
        var points = new ObservableCollection<ChartPoint> { new(2, 20), new(8, 80) };
        var series = new ScatterSeries { ItemsSource = points, MarkerSize = 10 }; using var host = new Host(series); host.Enable(ChartAnimationTriggers.Data);
        using (series.DeferUpdates()) { points[0] = new(2, 60); points[1] = new(8, 40); points.Add(new(5, 50)); }
        host.Drain(); host.Advance(500);
        AssertHit(host, new(2, 40)); AssertHit(host, new(8, 60)); AssertHit(host, new(5, 25));
        host.Chart.CancelAnimations(); host.Drain(); AssertHit(host, new(5, 50));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Progress_callback_can_pause_or_cancel_without_being_overwritten_by_outer_tick(bool cancel)
    {
        using var host = new Host(Series("scatter", [new(5, 80)])); host.Enable(ChartAnimationTriggers.Entrance); var changed = false;
        host.Chart.PropertyChanged += (_, change) =>
        {
            if (change.Property != CartesianChart.AnimationProgressProperty || changed || host.Chart.AnimationProgress < .5) return;
            changed = true;
            if (cancel) host.Chart.CancelAnimations(); else host.Chart.IsAnimationPaused = true;
        };
        host.Chart.ReplayEntranceAnimation(); host.Drain(); host.Advance(500); Assert.True(changed);
        Assert.Equal(cancel ? ChartAnimationState.Idle : ChartAnimationState.Paused, host.Chart.AnimationState);
        AssertHit(host, cancel ? new(5, 80) : new(5, 40)); host.Advance(2000);
        AssertHit(host, cancel ? new(5, 80) : new(5, 40));
    }

    [AvaloniaFact]
    public void Automatic_data_range_uses_the_real_target_extent_while_display_range_interpolates()
    {
        var series = Series("scatter", [new(2, 20), new(8, 40)]); using var host = new Host(series);
        host.Chart.YAxis.Minimum = null; host.Chart.YAxis.Maximum = null; host.Drain(); var before = host.Chart.VisibleRange!.Value;
        host.Enable(ChartAnimationTriggers.Data); series.ItemsSource = new ChartPoint[] { new(2, 80), new(8, 120) }; host.Drain(); host.Advance(500);
        var middle = host.Chart.VisibleRange!.Value; host.Advance(500); var after = host.Chart.VisibleRange!.Value;
        Assert.Equal((before.YMinimum + after.YMinimum) / 2, middle.YMinimum, 8); Assert.Equal((before.YMaximum + after.YMaximum) / 2, middle.YMaximum, 8);
        Assert.True(after.YMinimum > 0); Assert.True(after.YMaximum >= 120); Assert.Equal(new ChartPoint(8, 120), series.CreateSnapshot().Points[1].Point);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Same_version_snapshot_publication_animates_without_changing_source_version(bool asynchronousEntry)
    {
        var series = Series("scatter", [new(5, 20)]); using var host = new Host(series); host.Enable(ChartAnimationTriggers.Data);
        host.Chart.IsAnimationPaused = true; var source = series.CreateSnapshot();
        var next = new ChartDataSnapshot(source.Version, source.Points.Select(point => point with { Point = new ChartPoint(5, 80) }).ToArray());
        var accepted = asynchronousEntry ? await series.PublishSnapshotAsync(next, source.Version) : series.TryPublishSnapshot(next, source.Version);
        Assert.True(accepted); Assert.Equal(source.Version, series.SourceVersion); Assert.Same(next, series.CreateSnapshot()); host.Drain();
        Assert.Equal(ChartAnimationState.Paused, host.Chart.AnimationState); AssertHit(host, new(5, 20));
        host.Chart.IsAnimationPaused = false; host.Drain(); host.Advance(500); AssertHit(host, new(5, 50));
        host.Advance(500); AssertHit(host, new(5, 80)); Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState);
    }

    [AvaloniaFact]
    public void Same_version_snapshot_retarget_uses_current_frame_and_rejected_publication_does_not_reset_it()
    {
        var series = Series("scatter", [new(5, 20)]); using var host = new Host(series); host.Enable(ChartAnimationTriggers.Data);
        var source = series.CreateSnapshot();
        ChartDataSnapshot At(double y) => new(source.Version, source.Points.Select(point => point with { Point = new ChartPoint(5, y) }).ToArray());
        Assert.True(series.TryPublishSnapshot(At(80), source.Version)); host.Drain(); host.Advance(500); AssertHit(host, new(5, 50));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.False(series.TryPublishSnapshot(At(70), source.Version, cancelled.Token)); host.Drain(); Assert.Equal(.5, host.Chart.AnimationProgress, 9); AssertHit(host, new(5, 50));
        Assert.True(series.TryPublishSnapshot(At(90), source.Version)); host.Drain(); AssertHit(host, new(5, 50));
        host.Advance(500); AssertHit(host, new(5, 70)); host.Chart.CancelAnimations(); host.Drain(); AssertHit(host, new(5, 90));
        Assert.Equal(source.Version, series.SourceVersion);
    }

    [AvaloniaFact]
    public void Repeated_series_instances_in_a_source_remain_valid_during_data_matching()
    {
        var series = Series("scatter", [new(5, 20)]); using var host = new Host(series);
        host.Chart.SeriesSource = new[] { series, series }; host.Drain(); host.Enable(ChartAnimationTriggers.Data);
        series.ItemsSource = new ChartPoint[] { new(5, 80) }; host.Drain(); host.Advance(500);
        AssertHit(host, new(5, 50)); host.Advance(500); AssertHit(host, new(5, 80));
    }

    [AvaloniaFact]
    public void Replacing_easing_after_cancellation_releases_the_old_application_object()
    {
        using var host = new Host(Series("scatter", [new(5, 80)])); host.Enable(ChartAnimationTriggers.Entrance);
        var retired = ConfigureAndRetireApplicationEasing(host);
        for (var attempt = 0; attempt < 3; attempt++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(retired.TryGetTarget(out _));
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState); AssertHit(host, new(5, 80));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Easing> ConfigureAndRetireApplicationEasing(Host host)
    {
        var easing = new RetainingEasing(new byte[65_536]); host.Chart.AnimationEasing = easing;
        host.Chart.ReplayEntranceAnimation(); host.Drain(); host.Advance(500);
        host.Chart.CancelAnimations(); host.Chart.AnimationEasing = null; host.Drain();
        return new WeakReference<Easing>(easing);
    }

    private static ChartSeries Series(string kind, ChartPoint[] points) => kind switch
    {
        "line" => new LineSeries { ItemsSource = points, MarkerSize = 10 },
        "bar" => new BarSeries { ItemsSource = points },
        _ => new ScatterSeries { ItemsSource = points, MarkerSize = 10 },
    };
    private static ChartPointHit AssertHit(Host host, ChartPoint point)
    {
        var expected = host.Project(point); var hit = Assert.IsType<ChartPointHit>(host.Chart.HitTestPoint(expected, 1));
        // Bars may report a clamped edge/inside point; every kind must hit the drawn frame.
        var dx = hit.Position.X - expected.X; var dy = hit.Position.Y - expected.Y;
        Assert.InRange(Math.Sqrt(dx * dx + dy * dy), 0, 1.01); return hit;
    }
    private static void AssertPoint(Point expected, Point actual) { Assert.Equal(expected.X, actual.X, 7); Assert.Equal(expected.Y, actual.Y, 7); }
    private sealed record Row(string Id, double X, double Y);
    private sealed class SquareEasing : Easing { public override double Ease(double progress) => progress * progress; }
    private sealed class InvalidEasing(bool throws) : Easing
    { public override double Ease(double progress) => throws ? throw new InvalidOperationException("Application easing failed") : double.NaN; }
    private sealed class RetainingEasing(object retained) : Easing
    { public override double Ease(double progress) { GC.KeepAlive(retained); return progress; } }
    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }
    private sealed class Host : IDisposable
    {
        public ManualClock Clock { get; } = new();
        public CartesianChart Chart { get; }
        public Window Window { get; }
        public Host(ChartSeries series, Action<CartesianChart>? configure = null)
        {
            Chart = new CartesianChart { ShowLegend = false, ShowGridLines = false, AnimationTimeProvider = Clock,
                XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
            configure?.Invoke(Chart); Chart.Series.Add(series); Window = new Window { Width = 800, Height = 450, Content = Chart }; Window.Show(); Drain(); _ = Chart.VisibleRange;
        }
        public void Enable(ChartAnimationTriggers triggers)
        { Chart.AnimationDuration = TimeSpan.FromSeconds(1); Chart.AnimationDelay = TimeSpan.Zero; Chart.AnimationEasing = new LinearEasing(); Chart.AnimationTriggers = triggers; Drain(); }
        public void Advance(double milliseconds) { Clock.Advance(TimeSpan.FromMilliseconds(milliseconds)); Chart.UpdateAnimationFrame(); Drain(); }
        public Point Project(ChartPoint point)
        { var plot = Chart.PlotArea; var range = Chart.VisibleRange!.Value; return new(plot.Left + (point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height); }
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); _ = Chart.VisibleRange; }
        public void Dispose() => Window.Close();
    }
}
