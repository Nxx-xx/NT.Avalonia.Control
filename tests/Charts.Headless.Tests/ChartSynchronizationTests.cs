using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartSynchronizationTests
{
    [AvaloniaFact]
    public void Normalized_viewport_is_unavailable_before_layout_and_invalid_input_is_rejected()
    {
        var chart = Create(0, 10);
        Assert.Null(chart.GetNormalizedViewport());
        chart.SetNormalizedViewport(new(.2, .8, 0, 1));
        Assert.Null(chart.Viewport);
        Assert.Throws<ArgumentOutOfRangeException>(() => chart.SetNormalizedViewport(new(-.1, .8, 0, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => chart.SetNormalizedViewport(new(.2, .8, 0, 1), (ChartInteractionAxes)8));
        Assert.Throws<ArgumentOutOfRangeException>(() => chart.GetVisibleFraction(ChartInteractionAxes.Both));
        Assert.Throws<ArgumentOutOfRangeException>(() => chart.SetVisibleFraction(new(.7, .2)));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fractional_windows_use_logarithmic_data_order_even_when_axes_are_inversed(bool inversed)
    {
        var chart = Create(1, 10000);
        chart.XAxis = new LogarithmicAxis { Minimum = 1, Maximum = 10000, IsInversed = inversed };
        chart.YAxis.IsInversed = inversed;
        using var host = new Host(chart);
        chart.SetNormalizedViewport(new(.25, .75, .2, .6));
        AssertRange(chart, new(10, 1000, 20, 60));
        AssertNormalized(chart, new(.25, .75, .2, .6));
        Assert.Equal(new ChartRange(.25, .75), chart.GetVisibleFraction(ChartInteractionAxes.X));
    }

    [AvaloniaFact]
    public void Fractional_date_windows_use_Unix_milliseconds_and_preserve_the_other_dimension()
    {
        const double day = 86400000;
        var start = DateTimeAxis.ToUnixMilliseconds(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var chart = Create(start, start + 10 * day);
        chart.XAxis = new DateTimeAxis { Minimum = start, Maximum = start + 10 * day, IsInversed = true };
        using var host = new Host(chart);
        chart.Viewport = new(start, start + 10 * day, 25, 75);
        chart.SetVisibleFraction(new(.2, .8));
        AssertRange(chart, new(start + 2 * day, start + 8 * day, 25, 75));
        Assert.Equal(new ChartRange(.25, .75), chart.GetVisibleFraction(ChartInteractionAxes.Y));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fractional_windows_include_scale_break_compression_without_reversing_data_order(bool inversed)
    {
        var chart = Create(0, 10);
        chart.XAxis.IsInversed = inversed;
        chart.XAxis.ScaleBreaks.Add(new() { Start = 4, End = 6, CompressionRatio = .1 });
        using var host = new Host(chart, Create(0, 100));
        using var group = Group(chart, host.Charts[1]);
        chart.SetVisibleFraction(new(.25, .75));
        // Total weighted length is 4 + 2*.1 + 4 = 8.2 raw units.
        // A quarter lies at 2.05; three quarters at 6 + (6.15 - 4.2) = 7.95.
        AssertRange(chart, new(2.05, 7.95, 0, 100));
        AssertRange(host.Charts[1], new(25, 75, 0, 100));
        AssertNormalized(chart, new(.25, .75, 0, 1));
    }

    [AvaloniaFact]
    public void Fractional_windows_enforce_target_zoom_limits_and_None_changes_nothing()
    {
        var chart = Create(0, 100);
        chart.XAxis.MinimumZoom = 2; chart.XAxis.MaximumZoom = 4;
        using var host = new Host(chart);
        chart.SetVisibleFraction(new(.4, .5));
        var narrow = Assert.IsType<ChartViewport>(chart.GetNormalizedViewport());
        Assert.Equal(.25, narrow.XMaximum - narrow.XMinimum, 10);
        chart.SetVisibleFraction(new(0, 1));
        var wide = Assert.IsType<ChartViewport>(chart.GetNormalizedViewport());
        Assert.Equal(.5, wide.XMaximum - wide.XMinimum, 10);
        var before = chart.Viewport;
        chart.SetNormalizedViewport(new(.1, .2, .1, .2), ChartInteractionAxes.None);
        Assert.Equal(before, chart.Viewport);
    }

    [AvaloniaFact]
    public void Fractional_windows_update_explicit_extra_axis_views_in_that_axes_own_transform()
    {
        var chart = Create(0, 100);
        var extra = new LogarithmicAxis { Key = "extra", Minimum = 1, Maximum = 10000, ViewMinimum = 1, ViewMaximum = 10000, IsInversed = true };
        chart.Axes.Add(extra);
        chart.Series.Add(new LineSeries { XAxisKey = "extra", ItemsSource = new ChartPoint[] { new(1, 0), new(10000, 100) } });
        using var host = new Host(chart);
        chart.SetVisibleFraction(new(.25, .75));
        Assert.Equal(10, extra.ViewMinimum!.Value, 8);
        Assert.Equal(1000, extra.ViewMaximum!.Value, 8);
        AssertRange(chart, new(25, 75, 0, 100));
    }

    [AvaloniaFact]
    public void Group_registration_and_configuration_do_not_implicitly_choose_a_source()
    {
        using var host = new Host(Create(0, 10), Create(100, 200));
        var source = host.Charts[0]; var target = host.Charts[1];
        source.SetVisibleFraction(new(.2, .8));
        using var group = new ChartSynchronizationGroup();
        Assert.Equal(ChartInteractionAxes.X, group.Axes);
        Assert.Equal(ChartSynchronizationCoordinateMode.Normalized, group.CoordinateMode);
        group.Add(source); group.Add(target); group.Add(target);
        AssertRange(target, new(100, 200, 0, 100));
        group.CoordinateMode = ChartSynchronizationCoordinateMode.DataCoordinates;
        group.Axes = ChartInteractionAxes.Both;
        AssertRange(target, new(100, 200, 0, 100));
        group.CoordinateMode = ChartSynchronizationCoordinateMode.Normalized;
        group.SynchronizeFrom(source);
        AssertRange(target, new(120, 180, 0, 100));
    }

    [AvaloniaFact]
    public void Normalized_group_copies_each_fraction_into_different_extents_and_log_domains()
    {
        var source = Create(0, 100);
        var target = Create(1, 10000);
        target.XAxis = new LogarithmicAxis { Minimum = 1, Maximum = 10000, IsInversed = true };
        target.YAxis = new NumericAxis { Minimum = 1000, Maximum = 2000, IsInversed = true };
        using var host = new Host(source, target);
        using var group = Group(source, target, ChartInteractionAxes.Both);
        source.SetNormalizedViewport(new(.25, .75, .2, .6));
        AssertRange(target, new(10, 1000, 1200, 1600));
        AssertNormalized(source, new(.25, .75, .2, .6));
        AssertNormalized(target, new(.25, .75, .2, .6));
    }

    [AvaloniaFact]
    public void Raw_coordinate_group_keeps_real_values_and_constrains_disjoint_target_domains()
    {
        using var host = new Host(Create(0, 100), Create(0, 1000), Create(200, 300));
        using var group = Group(host.Charts[0], host.Charts[1]); group.Add(host.Charts[2]);
        group.CoordinateMode = ChartSynchronizationCoordinateMode.DataCoordinates;
        host.Charts[0].Viewport = new(20, 80, 10, 60);
        AssertRange(host.Charts[1], new(20, 80, 0, 100));
        // There is no overlap: the ordinary target viewport constraint restores its full extent.
        AssertRange(host.Charts[2], new(200, 300, 0, 100));
        AssertRange(host.Charts[0], new(20, 80, 10, 60));
    }

    [AvaloniaTheory]
    [InlineData(ChartInteractionAxes.None)]
    [InlineData(ChartInteractionAxes.X)]
    [InlineData(ChartInteractionAxes.Y)]
    [InlineData(ChartInteractionAxes.Both)]
    public void Group_axes_preserve_unselected_target_dimensions(ChartInteractionAxes axes)
    {
        using var host = new Host(Create(0, 100), Create(0, 100));
        var source = host.Charts[0]; var target = host.Charts[1];
        target.Viewport = new(10, 90, 15, 85);
        using var group = Group(source, target, axes);
        source.Viewport = new(25, 75, 30, 60);
        AssertRange(target, new(
            (axes & ChartInteractionAxes.X) != 0 ? 25 : 10,
            (axes & ChartInteractionAxes.X) != 0 ? 75 : 90,
            (axes & ChartInteractionAxes.Y) != 0 ? 30 : 15,
            (axes & ChartInteractionAxes.Y) != 0 ? 60 : 85));
    }

    [AvaloniaFact]
    public void Wheel_zoom_and_reset_propagate_both_ways_without_rewriting_axis_bounds()
    {
        using var host = new Host(Create(0, 100), Create(1000, 2000));
        var first = host.Charts[0]; var second = host.Charts[1];
        first.IsZoomEnabled = true;
        using var group = Group(first, second);
        host.Window.MouseWheel(first.TranslatePoint(first.PlotArea.Center, host.Window)!.Value, new Vector(0, 1));
        host.Drain();
        var view = Assert.IsType<ChartViewport>(first.GetNormalizedViewport());
        Assert.InRange(view.XMaximum - view.XMinimum, .8, .9);
        AssertNormalized(second, new(view.XMinimum, view.XMaximum, 0, 1));
        second.ResetViewport();
        AssertNormalized(first, new(0, 1, view.YMinimum, view.YMaximum));
        AssertNormalized(second, new(0, 1, 0, 1));
        Assert.Equal(100d, first.XAxis.Maximum); Assert.Equal(1000d, second.XAxis.Minimum);
    }

    [AvaloniaFact]
    public void A_full_range_no_op_keeps_automatic_viewports_and_target_growth_cannot_shrink_other_charts()
    {
        var source = Create(0, 100); var target = Create(0, 100);
        target.XAxis.Maximum = null; target.XAxis.Interval = 50;
        using var host = new Host(source, target);
        using var group = Group(source, target);
        group.SynchronizeFrom(source);
        Assert.Null(source.Viewport); Assert.Null(target.Viewport);
        target.Series[0].ItemsSource = new ChartPoint[] { new(0, 0), new(200, 100) };
        host.Drain();
        // The automatic horizontal extent adds 2% padding: 200 -> 204, then the
        // explicitly configured 50-unit interval rounds the upper bound to 250.
        AssertRange(target, new(0, 250, 0, 100));
        AssertRange(source, new(0, 100, 0, 100));
        Assert.Null(source.Viewport); Assert.Null(target.Viewport);
    }

    [AvaloniaFact]
    public void Reset_after_synchronized_zoom_restores_automatic_full_extent_growth()
    {
        var source = Create(0, 100); var target = Create(0, 100);
        target.XAxis.Maximum = null; target.XAxis.Interval = 50;
        using var host = new Host(source, target);
        using var group = Group(source, target);
        source.SetVisibleFraction(new(.25, .75));
        Assert.NotNull(target.Viewport);
        source.ResetViewport();
        Assert.Null(source.Viewport); Assert.Null(target.Viewport);
        target.Series[0].ItemsSource = new ChartPoint[] { new(0, 0), new(200, 100) };
        host.Drain();
        AssertRange(target, new(0, 250, 0, 100));
        AssertRange(source, new(0, 100, 0, 100));
        Assert.Null(source.Viewport); Assert.Null(target.Viewport);
    }

    [AvaloniaFact]
    public void Reset_only_the_selected_dimension_preserves_a_manual_window_on_the_other_axis()
    {
        using var host = new Host(Create(0, 100), Create(0, 100));
        var source = host.Charts[0]; var target = host.Charts[1];
        target.Viewport = new(10, 90, 20, 80);
        using var group = Group(source, target);
        source.SetVisibleFraction(new(.25, .75)); source.ResetViewport();
        AssertRange(target, new(0, 100, 20, 80));
        Assert.NotNull(target.Viewport);
    }

    [AvaloniaFact]
    public void Axis_configuration_and_source_extent_changes_propagate_current_effective_fractions()
    {
        var source = Create(0, 10); source.XAxis.Maximum = null;
        using var host = new Host(source, Create(0, 100));
        using var group = Group(source, host.Charts[1]);
        source.SetVisibleFraction(new(.25, .75));
        AssertRange(host.Charts[1], new(25, 75, 0, 100));
        source.Series[0].ItemsSource = new ChartPoint[] { new(0, 0), new(20, 100) };
        host.Drain();
        AssertRange(host.Charts[1], new(12.5, 37.5, 0, 100));
        source.XAxis.Maximum = 40;
        host.Drain();
        AssertRange(host.Charts[1], new(0, 100, 0, 100));
    }

    [AvaloniaFact]
    public void Hidden_ancestor_detach_and_reattach_suspend_event_membership_without_a_strong_chart_registration()
    {
        using var host = new Host(Create(0, 100), Create(0, 100));
        var source = host.Charts[0]; var target = host.Charts[1];
        using var group = Group(source, target);
        host.Containers[1].IsVisible = false;
        source.SetVisibleFraction(new(.2, .8));
        Assert.Null(target.Viewport);
        host.Containers[1].IsVisible = true; host.Drain();
        group.SynchronizeFrom(source);
        AssertRange(target, new(20, 80, 0, 100));
        host.Containers[1].Child = null;
        source.SetVisibleFraction(new(.3, .7));
        AssertRange(target, new(20, 80, 0, 100));
        host.Containers[1].Child = target; host.Drain();
        group.SynchronizeFrom(source);
        AssertRange(target, new(30, 70, 0, 100));
        target.IsVisible = false;
        source.SetVisibleFraction(new(.4, .6));
        AssertRange(target, new(30, 70, 0, 100));
    }

    [AvaloniaFact]
    public void Removing_a_chart_and_disposing_the_group_release_membership_and_stop_synchronization()
    {
        using var host = new Host(Create(0, 100), Create(0, 100));
        var group = Group(host.Charts[0], host.Charts[1]);
        Assert.True(group.Remove(host.Charts[1])); Assert.False(group.Remove(host.Charts[1]));
        host.Charts[0].SetVisibleFraction(new(.2, .8));
        Assert.Null(host.Charts[1].Viewport);
        group.Add(host.Charts[1]); group.SynchronizeFrom(host.Charts[0]);
        AssertRange(host.Charts[1], new(20, 80, 0, 100));
        group.Dispose(); group.Dispose();
        host.Charts[0].SetVisibleFraction(new(.3, .7));
        AssertRange(host.Charts[1], new(20, 80, 0, 100));
        Assert.Throws<ObjectDisposedException>(() => group.Add(host.Charts[0]));
        Assert.Throws<ObjectDisposedException>(() => group.SynchronizeFrom(host.Charts[0]));
    }

    [AvaloniaFact]
    public void A_reentrant_target_binding_wins_and_updates_all_other_members()
    {
        using var host = new Host(Create(0, 100), Create(0, 100), Create(0, 100));
        using var group = Group(host.Charts[0], host.Charts[1]); group.Add(host.Charts[2]);
        var changed = false;
        host.Charts[1].ViewportChanged += (_, _) =>
        {
            if (changed) return;
            changed = true;
            host.Charts[1].SetVisibleFraction(new(.6, .8));
        };
        host.Charts[0].SetVisibleFraction(new(.2, .4));
        Assert.True(changed);
        foreach (var chart in host.Charts) AssertRange(chart, new(60, 80, 0, 100));
    }

    [AvaloniaFact]
    public void A_reentrant_source_update_is_not_lost_while_its_previous_notification_is_publishing()
    {
        using var host = new Host(Create(0, 100), Create(0, 100), Create(0, 100));
        using var group = Group(host.Charts[0], host.Charts[1]); group.Add(host.Charts[2]);
        var changed = false;
        host.Charts[1].ViewportChanged += (_, _) =>
        {
            if (changed) return;
            changed = true;
            host.Charts[0].SetVisibleFraction(new(.6, .8));
        };
        host.Charts[0].SetVisibleFraction(new(.2, .4));
        foreach (var chart in host.Charts) AssertRange(chart, new(60, 80, 0, 100));
    }

    [AvaloniaFact]
    public void Adding_a_second_group_during_a_reentrant_update_does_not_acknowledge_it_for_existing_observers()
    {
        using var host = new Host(Create(0, 100), Create(0, 100), Create(0, 100));
        var source = host.Charts[0];
        using var first = Group(source, host.Charts[1]);
        using var second = new ChartSynchronizationGroup();
        var changed = false;
        source.ViewSynchronizationChanged += (_, _) =>
        {
            if (changed) return;
            changed = true;
            source.SetVisibleFraction(new(.6, .8));
            second.Add(source); second.Add(host.Charts[2]);
        };
        source.SetVisibleFraction(new(.2, .4));
        foreach (var chart in host.Charts) AssertRange(chart, new(60, 80, 0, 100));
    }

    [AvaloniaFact]
    public void Disposing_or_removing_members_during_callbacks_does_not_visit_retired_members()
    {
        using var host = new Host(Create(0, 100), Create(0, 100), Create(0, 100));
        var group = Group(host.Charts[0], host.Charts[1]); group.Add(host.Charts[2]);
        host.Charts[1].ViewportChanged += (_, _) => { group.Remove(host.Charts[2]); group.Dispose(); };
        host.Charts[0].SetVisibleFraction(new(.2, .8));
        AssertRange(host.Charts[1], new(20, 80, 0, 100));
        Assert.Null(host.Charts[2].Viewport);
        group.Dispose();
    }

    [AvaloniaFact]
    public void Invalid_group_configuration_and_unregistered_sources_are_rejected()
    {
        using var group = new ChartSynchronizationGroup();
        Assert.Throws<ArgumentOutOfRangeException>(() => group.Axes = (ChartInteractionAxes)16);
        Assert.Throws<ArgumentOutOfRangeException>(() => group.CoordinateMode = (ChartSynchronizationCoordinateMode)12);
        Assert.Throws<ArgumentException>(() => group.SynchronizeFrom(Create(0, 100)));
        Assert.Throws<ArgumentNullException>(() => group.Add(null!));
    }

    [AvaloniaFact]
    public void Contradictory_reentrant_observers_stop_with_an_error_instead_of_posting_forever()
    {
        using var host = new Host(Create(0, 100), Create(0, 100));
        using var group = Group(host.Charts[0], host.Charts[1]);
        var calls = 0;
        host.Charts[1].ViewportChanged += (_, _) =>
        {
            calls++;
            host.Charts[0].SetVisibleFraction(calls % 2 == 0 ? new(.2, .4) : new(.6, .8));
        };
        var error = Assert.Throws<InvalidOperationException>(() => host.Charts[0].SetVisibleFraction(new(.2, .4)));
        Assert.Contains("64 changes", error.Message);
        Assert.InRange(calls, 1, 65);
        group.Dispose();
        host.Drain();
    }

    private static ChartSynchronizationGroup Group(CartesianChart first, CartesianChart second,
        ChartInteractionAxes axes = ChartInteractionAxes.X)
    {
        var result = new ChartSynchronizationGroup { Axes = axes };
        result.Add(first); result.Add(second); return result;
    }

    private static CartesianChart Create(double minimum, double maximum)
    {
        var chart = new CartesianChart
        {
            Width = 600, Height = 250, ShowLegend = false,
            XAxis = new NumericAxis { Minimum = minimum, Maximum = maximum },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
        };
        chart.Series.Add(new LineSeries { ItemsSource = new ChartPoint[] { new(minimum, 0), new(maximum, 100) } });
        return chart;
    }

    private static void AssertRange(CartesianChart chart, ChartViewport expected) =>
        AssertViewport(expected, Assert.IsType<ChartViewport>(chart.VisibleRange));
    private static void AssertNormalized(CartesianChart chart, ChartViewport expected) =>
        AssertViewport(expected, Assert.IsType<ChartViewport>(chart.GetNormalizedViewport()));
    private static void AssertViewport(ChartViewport expected, ChartViewport actual)
    {
        Assert.Equal(expected.XMinimum, actual.XMinimum, 8); Assert.Equal(expected.XMaximum, actual.XMaximum, 8);
        Assert.Equal(expected.YMinimum, actual.YMinimum, 8); Assert.Equal(expected.YMaximum, actual.YMaximum, 8);
    }

    private sealed class Host : IDisposable
    {
        internal Host(params CartesianChart[] charts)
        {
            Charts = charts;
            Containers = charts.Select(chart => new Border { Child = chart }).ToArray();
            var panel = new StackPanel(); foreach (var container in Containers) panel.Children.Add(container);
            Window = new Window { Width = 660, Height = Math.Max(400, 260 * charts.Length), Content = panel };
            Window.Show(); Drain();
            foreach (var chart in charts) _ = chart.VisibleRange;
        }
        internal Window Window { get; }
        internal CartesianChart[] Charts { get; }
        internal Border[] Containers { get; }
        internal void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() => Window.Close();
    }
}
