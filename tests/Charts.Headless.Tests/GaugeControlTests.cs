using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class GaugeControlTests
{
    [AvaloniaTheory]
    [InlineData(-50, 0)] [InlineData(0, 0)] [InlineData(25, .25)] [InlineData(100, 1)] [InlineData(150, 1)]
    public void Business_values_clamp_without_rewriting_the_bound_raw_value(double value, double fraction)
    {
        var chart = new ProgressDonutChart { Value = value, TargetValue = 125 };
        Assert.Equal(fraction, chart.NormalizedValue!.Value, 9); Assert.Equal(value, chart.Value); Assert.Equal(1, chart.NormalizedTarget!.Value, 9);
        chart.Minimum = -100; chart.Maximum = 100; chart.Value = -50; Assert.Equal(.25, chart.NormalizedValue!.Value, 9);
    }

    [AvaloniaTheory]
    [InlineData(double.NaN, 100, 50)] [InlineData(0, double.PositiveInfinity, 50)]
    [InlineData(100, 0, 50)] [InlineData(10, 10, 10)] [InlineData(0, 100, double.NaN)] [InlineData(0, 100, double.NegativeInfinity)]
    public void Invalid_domains_and_nonfinite_values_render_a_track_without_throwing(double minimum, double maximum, double value)
    {
        foreach (var chart in AllGauges())
        {
            chart.Minimum = minimum; chart.Maximum = maximum; chart.Value = value; chart.TargetValue = double.NaN;
            using var host = new Host(chart); Assert.Null(chart.NormalizedValue); Assert.Null(chart.NormalizedTarget);
            var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context);
        }
    }

    [AvaloniaFact]
    public void Opposite_extreme_and_subnormal_scales_normalize_without_overflow_or_fake_midpoints()
    {
        var chart = new CircularGaugeChart { Minimum = -double.MaxValue, Maximum = double.MaxValue, Value = 0 };
        Assert.Equal(.5, chart.NormalizedValue!.Value, 9); Assert.Equal(0, chart.ValueAt(.5));
        chart.Value = double.MaxValue / 2; Assert.Equal(.75, chart.NormalizedValue!.Value, 9);
        chart.Minimum = double.Epsilon; chart.Maximum = 3 * double.Epsilon; chart.Value = 2 * double.Epsilon; Assert.Equal(.5, chart.NormalizedValue!.Value, 9);
    }

    [AvaloniaFact]
    public void Readonly_scale_semantics_update_real_text_bindings_through_invalid_and_valid_domains()
    {
        var chart = Plain(new CircularGaugeChart { Value = 25, TargetValue = 75 });
        var valueText = new TextBlock(); valueText.Bind(TextBlock.TextProperty, new Binding(nameof(GaugeBase.NormalizedValue)) { Source = chart });
        var targetText = new TextBlock(); targetText.Bind(TextBlock.TextProperty, new Binding(nameof(GaugeBase.NormalizedTarget)) { Source = chart });
        var domainText = new TextBlock(); domainText.Bind(TextBlock.TextProperty, new Binding(nameof(GaugeBase.IsDomainValid)) { Source = chart });
        using var host = new Host(new StackPanel { Children = { chart, valueText, targetText, domainText } });
        void Fraction(TextBlock text, double expected) => Assert.Equal(expected, double.Parse(text.Text!, CultureInfo.CurrentCulture), 10);
        Fraction(valueText, .25); Fraction(targetText, .75); Assert.Equal("True", domainText.Text);
        chart.Value = 50; host.Drain(); Fraction(valueText, .5);
        chart.Minimum = 25; host.Drain(); Fraction(valueText, 1d / 3); Fraction(targetText, 2d / 3);
        chart.Maximum = 125; host.Drain(); Fraction(valueText, .25); Fraction(targetText, .5);
        chart.TargetValue = 100; host.Drain(); Fraction(targetText, .75);
        chart.Maximum = 25; host.Drain(); Assert.True(string.IsNullOrEmpty(valueText.Text)); Assert.True(string.IsNullOrEmpty(targetText.Text)); Assert.Equal("False", domainText.Text);
        chart.Maximum = 125; host.Drain(); Fraction(valueText, .25); Fraction(targetText, .75); Assert.Equal("True", domainText.Text);
        chart.Value = double.NaN; chart.TargetValue = null; host.Drain(); Assert.True(string.IsNullOrEmpty(valueText.Text)); Assert.True(string.IsNullOrEmpty(targetText.Text)); Assert.Equal("True", domainText.Text);
    }

    [AvaloniaFact]
    public void Reentrant_normalized_listener_keeps_all_bound_text_on_the_latest_scale_revision()
    {
        var chart = Plain(new CircularGaugeChart { Value = 25, TargetValue = 50 });
        var valueText = new TextBlock(); valueText.Bind(TextBlock.TextProperty, new Binding(nameof(GaugeBase.NormalizedValue)) { Source = chart });
        var targetText = new TextBlock(); targetText.Bind(TextBlock.TextProperty, new Binding(nameof(GaugeBase.NormalizedTarget)) { Source = chart });
        var domainText = new TextBlock(); domainText.Bind(TextBlock.TextProperty, new Binding(nameof(GaugeBase.IsDomainValid)) { Source = chart });
        using var host = new Host(new StackPanel { Children = { chart, valueText, targetText, domainText } });
        var changed = false;
        chart.PropertyChanged += (_, e) =>
        {
            if (changed || e.Property != GaugeBase.NormalizedValueProperty) return;
            changed = true; chart.Maximum = 200; chart.TargetValue = 150;
        };
        chart.Maximum = 80; host.Drain(); Assert.True(changed);
        Assert.Equal(.125, double.Parse(valueText.Text!, CultureInfo.CurrentCulture)); Assert.Equal(.75, double.Parse(targetText.Text!, CultureInfo.CurrentCulture)); Assert.Equal("True", domainText.Text);
        Assert.Equal(.125, chart.NormalizedValue!.Value, 9); Assert.Equal(.75, chart.NormalizedTarget!.Value, 9);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Actual_renderer_draws_target_in_the_correct_scale_position_and_public_brush(int family)
    {
        GaugeBase chart = family switch
        {
            0 => new CircularGaugeChart { StartAngle = 0, SweepAngle = 360, ShowNeedle = false, ShowMajorTicks = false },
            1 => new LinearGaugeChart { ShowNeedle = false },
            2 => new LiquidFillGaugeChart { Shape = GaugeLiquidShape.Rectangle, WaveAmplitude = 0 },
            _ => new GradientRingChart { StartAngle = 0, ShowLabels = false }
        };
        Plain(chart); chart.TargetValue = 25; chart.NeedleBrush = Brushes.Magenta; chart.AxisBrush = null;
        if (chart is GradientRingChart rings) { rings.Values.Add(new GaugeValue { Value = 30 }); rings.Values.Add(new GaugeValue { Value = 60 }); }
        using var host = new Host(chart); var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context);
        var targets = Leaves(group).OfType<GeometryDrawing>().Where(g => g.Pen?.Brush is ISolidColorBrush b && b.Color == Colors.Magenta).ToArray();
        Rect[] expected = family switch
        {
            0 => [new Rect(210, 249, 0, 26)],
            1 => [new Rect(109, 122, 0, 36)],
            2 => [new Rect(9, 205.5, 402, 0)],
            _ => [new Rect(210, 254, 0, 18), new Rect(210, 230, 0, 18)]
        };
        Assert.Equal(expected.Length, targets.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            var actual = targets[index].Geometry!.Bounds; var target = expected[index]; Assert.Equal(3, targets[index].Pen!.Thickness);
            Assert.Equal(target.X, actual.X, 8); Assert.Equal(target.Y, actual.Y, 8); Assert.Equal(target.Width, actual.Width, 8); Assert.Equal(target.Height, actual.Height, 8);
        }
    }

    [AvaloniaFact]
    public void Circle_semicircle_and_progress_use_size_constraints_and_independent_centers()
    {
        var circle = Plain(new CircularGaugeChart()); using (var host = new Host(circle))
        { Assert.Equal(new Rect(8, 8, 404, 264), circle.GetGaugeLayout().Bounds); Assert.Equal(new Point(210, 140), circle.GetGaugeLayout().Center); Assert.Equal(132, circle.GetGaugeLayout().Radius); }
        var semi = Plain(new GaugeChart()); using (var host = new Host(semi))
        { Assert.Equal(new Point(210, 260), semi.GetGaugeLayout().Center); Assert.Equal(202, semi.GetGaugeLayout().Radius); Assert.Equal(-180, semi.StartAngle); Assert.Equal(180, semi.SweepAngle); }
        var donut = Plain(new ProgressDonutChart()); using (var host = new Host(donut)) Assert.Equal(132, donut.GetGaugeLayout().Radius);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Arc_direction_and_nonzero_minimum_use_business_fraction(bool reversed)
    {
        var chart = new CircularGaugeChart { Minimum = 20, Maximum = 60, Value = 30, Clockwise = !reversed, StartAngle = -90, SweepAngle = 180 };
        Assert.Equal(.25, chart.NormalizedValue!.Value, 9); Assert.Equal(reversed ? -135 : -45, chart.AngleAt(chart.NormalizedValue!.Value));
        chart.TargetValue = 50; Assert.Equal(.75, chart.NormalizedTarget!.Value, 9);
    }

    [AvaloniaTheory]
    [InlineData(-180, true, 260)] [InlineData(180, true, 260)] [InlineData(-180, false, 20)] [InlineData(180, false, 20)]
    public void Semicircle_reverse_direction_and_equivalent_starts_keep_the_arc_inside_the_view(double start, bool clockwise, double centerY)
    {
        var chart = Plain(new GaugeChart { StartAngle = start, Clockwise = clockwise, Value = 50, ShowNeedle = false, ShowMajorTicks = false, TrackBrush = Brushes.Cyan, ValueBrush = Brushes.Magenta }); using var host = new Host(chart);
        var layout = chart.GetGaugeLayout(); Assert.Equal(new Point(210, centerY), layout.Center); Assert.Equal(202, layout.Radius);
        var halfway = GaugeGeometry.PointAt(layout.Center, chart.AngleAt(.5), layout.Radius);
        Assert.Equal(clockwise ? 58 : 222, halfway.Y, 9); Assert.True(new Rect(new Size(420, 280)).Contains(halfway));
        var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context);
        var geometryBounds = Leaves(group).OfType<GeometryDrawing>().Where(d => d.Brush is ISolidColorBrush brush && (brush.Color == Colors.Cyan || brush.Color == Colors.Magenta)).Select(d => d.Geometry!.Bounds).Where(b => double.IsFinite(b.Top) && double.IsFinite(b.Bottom)).ToArray();
        Assert.NotEmpty(geometryBounds);
        Assert.All(geometryBounds, bounds => { Assert.True(bounds.Top >= 8 - 1e-8); Assert.True(bounds.Bottom <= 272 + 1e-8); Assert.True(bounds.Left >= 8 - 1e-8); Assert.True(bounds.Right <= 412 + 1e-8); });
    }

    [AvaloniaFact]
    public void Custom_semicircle_start_or_sweep_falls_back_to_the_full_circular_fit()
    {
        var chart = Plain(new GaugeChart { StartAngle = -90 }); using var host = new Host(chart);
        Assert.Equal(new Point(210, 140), chart.GetGaugeLayout().Center); Assert.Equal(132, chart.GetGaugeLayout().Radius);
        chart.StartAngle = 180; chart.SweepAngle = 270; Assert.Equal(new Point(210, 140), chart.GetGaugeLayout().Center); Assert.Equal(132, chart.GetGaugeLayout().Radius);
    }

    [AvaloniaTheory]
    [InlineData(null)] [InlineData("{bad format")] [InlineData("{0:F0}")]
    public void Null_or_composite_value_formats_render_on_a_nonempty_control(string? format)
    {
        var chart = Plain(new CircularGaugeChart { Value = 50, ValueFormat = format! }); chart.ShowValue = chart.ShowScale = true;
        using var host = new Host(chart); var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context);
        Assert.NotEmpty(group.Children);
    }

    [AvaloniaTheory]
    [InlineData(Orientation.Horizontal, true, 109, 140)] [InlineData(Orientation.Horizontal, false, 311, 140)]
    [InlineData(Orientation.Vertical, true, 210, 206)] [InlineData(Orientation.Vertical, false, 210, 74)]
    public void Linear_orientation_and_direction_have_exact_endpoints_and_fill_extent(Orientation orientation, bool clockwise, double x, double y)
    {
        var chart = Plain(new LinearGaugeChart { Orientation = orientation, Clockwise = clockwise }); using var host = new Host(chart);
        Assert.Equal(new Point(x, y), chart.PositionAt(.25));
        var fill = chart.IntervalBounds(0, .25); Assert.Equal(orientation == Orientation.Horizontal ? 101 : 18, fill.Width); Assert.Equal(orientation == Orientation.Vertical ? 66 : 18, fill.Height);
        Assert.Equal(orientation == Orientation.Horizontal ? new Rect(8, 131, 404, 18) : new Rect(201, 8, 18, 264), chart.TrackBounds);
    }

    [AvaloniaFact]
    public void Threshold_binding_inherits_host_data_context_and_is_released_on_detach()
    {
        var row = new Row { Reading = 40 }; var range = new GaugeRange { Start = 10, Brush = Brushes.Lime };
        range.Bind(GaugeRange.EndProperty, new Binding(nameof(Row.Reading)));
        var chart = Plain(new LinearGaugeChart { DataContext = row }); chart.Ranges.Add(range);
        using var host = new Host(chart); Assert.Same(chart, ((ILogical)range).LogicalParent); Assert.Equal(40, range.End); Assert.True(row.Observers > 0);
        row.Reading = 70; host.Drain(); Assert.Equal(70, range.End);
        host.Window.Content = null; host.Drain(); Assert.Null(((ILogical)range).LogicalParent); Assert.Equal(0, row.Observers);
        host.Window.Content = chart; host.Drain(); Assert.Equal(70, range.End); Assert.True(row.Observers > 0);
        chart.Ranges.Clear(); host.Drain(); Assert.Null(((ILogical)range).LogicalParent); Assert.Equal(0, row.Observers);
    }

    [AvaloniaFact]
    public void Concentric_items_share_the_scale_and_keep_their_own_brush_identity()
    {
        var gradient = new LinearGradientBrush { GradientStops = [new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1)] };
        var a = new GaugeValue { Value = 40, Label = "A", Brush = gradient }; var b = new GaugeValue { Value = 80, Label = "B" };
        var chart = Plain(new GradientRingChart { Minimum = 20, Maximum = 100, RingSpacing = 6 }); chart.Values.Add(a); chart.Values.Add(b);
        using var host = new Host(chart); var rings = chart.GetRings(); Assert.Equal(2, rings.Count);
        Assert.Equal(132, rings[0].OuterRadius); Assert.Equal(114, rings[0].InnerRadius); Assert.Equal(.25, rings[0].Fraction!.Value, 9);
        Assert.Equal(108, rings[1].OuterRadius); Assert.Equal(90, rings[1].InnerRadius); Assert.Equal(.75, rings[1].Fraction!.Value, 9); Assert.Same(gradient, rings[0].Item.Brush);
        a.Value = 100; Assert.Equal(1, chart.GetRings()[0].Fraction!.Value, 9); chart.Maximum = 180; Assert.Equal(.5, chart.GetRings()[0].Fraction!.Value, 9);
        chart.Values.Add(new GaugeValue { Value = double.NaN }); Assert.Null(chart.GetRings()[2].Fraction);
    }

    [AvaloniaFact]
    public void Source_replacement_updates_concentric_items_and_releases_removed_item_bindings()
    {
        var row = new Row { Reading = 25 }; var first = new GaugeValue(); first.Bind(GaugeValue.ValueProperty, new Binding(nameof(Row.Reading)));
        var source = new ObservableCollection<GaugeValue> { first }; var chart = Plain(new GradientRingChart { DataContext = row, ValuesSource = source });
        using var host = new Host(chart); Assert.Equal(.25, Assert.Single(chart.GetRings()).Fraction!.Value, 9); Assert.True(row.Observers > 0);
        source.Clear(); host.Drain(); Assert.Empty(chart.GetRings()); Assert.Equal(0, row.Observers);
        source.Add(first); host.Drain(); Assert.True(row.Observers > 0); chart.ValuesSource = null; host.Drain(); Assert.Empty(chart.GetRings()); Assert.Equal(0, row.Observers);
    }

    [AvaloniaTheory]
    [InlineData(GaugeLiquidShape.Circle, 79, 9, 262, 262)] [InlineData(GaugeLiquidShape.Rectangle, 9, 9, 402, 262)]
    public void Liquid_shape_clipping_uses_its_own_inner_bounds(GaugeLiquidShape shape, double x, double y, double width, double height)
    {
        var chart = Plain(new LiquidFillGaugeChart { Shape = shape, Value = 25, WaveAmplitude = 0 }); using var host = new Host(chart);
        Assert.Equal(new Rect(x, y, width, height), chart.LiquidBounds); Assert.Equal(205.5, chart.SurfaceAt(0)); Assert.Equal(205.5, chart.SurfaceAt(.5));
        chart.Value = 50; Assert.Equal(140, chart.SurfaceAt(.5));
    }

    [AvaloniaTheory]
    [InlineData(0, 271)] [InlineData(100, 9)] [InlineData(-100, 271)] [InlineData(200, 9)]
    public void Liquid_zero_and_full_remain_exact_even_with_large_wave_amplitude(double value, double expectedY)
    {
        var chart = Plain(new LiquidFillGaugeChart { Value = value, WaveAmplitude = 4096, WavePhase = Math.PI / 2 }); using var host = new Host(chart);
        foreach (var x in new[] { 0, .125, .5, 1 }) Assert.Equal(expectedY, chart.SurfaceAt(x));
        var drawing = new DrawingGroup(); using var context = drawing.Open(); chart.Render(context);
    }

    [AvaloniaFact]
    public void Wave_motion_is_opt_in_and_pause_reduce_motion_and_detach_release_the_timer()
    {
        var chart = Plain(new LiquidFillGaugeChart { Value = 50 }); using var host = new Host(chart); Assert.False(chart.IsWaveTimerRunning);
        chart.AdvanceWave(TimeSpan.FromSeconds(.5)); Assert.Equal(0, chart.WavePhase);
        chart.IsWaveAnimationEnabled = true; Assert.True(chart.IsWaveTimerRunning); chart.AdvanceWave(TimeSpan.FromSeconds(.5)); Assert.Equal(Math.PI / 2, chart.WavePhase, 10);
        chart.IsPaused = true; Assert.False(chart.IsWaveTimerRunning); var phase = chart.WavePhase; chart.AdvanceWave(TimeSpan.FromSeconds(.5)); Assert.Equal(phase, chart.WavePhase);
        chart.IsPaused = false; chart.ReduceMotion = true; Assert.False(chart.IsWaveTimerRunning); chart.AdvanceWave(TimeSpan.FromSeconds(.5)); Assert.Equal(phase, chart.WavePhase);
        chart.ReduceMotion = false; Assert.True(chart.IsWaveTimerRunning); host.Window.Content = null; host.Drain(); Assert.False(chart.IsWaveTimerRunning); chart.AdvanceWave(TimeSpan.FromSeconds(.5)); Assert.Equal(phase, chart.WavePhase);
        host.Window.Content = chart; host.Drain(); Assert.True(chart.IsWaveTimerRunning); chart.IsWaveAnimationEnabled = false; Assert.False(chart.IsWaveTimerRunning);
    }

    [AvaloniaFact]
    public void Wave_phase_has_repeatable_sine_sample_and_frequency_without_background_animation()
    {
        var chart = Plain(new LiquidFillGaugeChart { Value = 50, WaveAmplitude = 5, WaveFrequency = 2 }); using var host = new Host(chart);
        Assert.Equal(145, chart.SurfaceAt(.125), 10); Assert.Equal(135, chart.SurfaceAt(.375), 10);
        chart.WavePhase = Math.PI / 2; Assert.Equal(145, chart.SurfaceAt(0), 10); Assert.False(chart.IsWaveTimerRunning);
    }

    [AvaloniaFact]
    public async Task Export_uses_the_shared_layout_and_reports_failure_on_the_public_gauge_sender()
    {
        var chart = Plain(new ProgressDonutChart { Value = 60 }); using var host = new Host(chart);
        var layout = chart.GetExportLayout(new ChartExportOptions { PixelSize = new PixelSize(840, 560), Dpi = 192 }); Assert.Equal(2, layout.Scale); Assert.Equal(192, layout.Dpi);
        object? sender = null; ChartExportFailedEventArgs? failure = null; chart.ExportFailed += (s, e) => { sender = s; failure = e; };
        Assert.False(chart.TryExportPng(new MemoryStream(), new ChartExportOptions { PixelSize = new PixelSize(0, 2) })); Assert.Same(chart, sender); Assert.NotNull(failure);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportJpegAsync(new MemoryStream(), cancellationToken: cancellation.Token));
    }

    [AvaloniaFact]
    public void Tiny_and_zero_size_controls_and_invalid_format_have_a_finite_nonthrowing_render()
    {
        foreach (var chart in AllGauges())
        {
            chart.Width = chart.Height = 1; chart.Value = 50; chart.ValueFormat = "{bad format";
            using var host = new Host(chart); Assert.True(double.IsFinite(chart.GetGaugeLayout().Radius));
            var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context);
            chart.Measure(default); chart.Arrange(default); chart.Render(context);
            chart.ValueFormat = null!; chart.ShowValue = true; chart.Render(context);
        }
    }

    private static T Plain<T>(T chart) where T : GaugeBase { chart.Width = 420; chart.Height = 280; chart.Padding = new Thickness(0); chart.ShowScale = chart.ShowValue = false; return chart; }
    private static GaugeBase[] AllGauges() => [Plain(new CircularGaugeChart()), Plain(new GaugeChart()), Plain(new ProgressDonutChart()), Plain(new GradientRingChart()), Plain(new LinearGaugeChart()), Plain(new LiquidFillGaugeChart())];
    private static IEnumerable<Drawing> Leaves(Drawing drawing)
    {
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var leaf in Leaves(child)) yield return leaf;
        else yield return drawing;
    }
    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(Control control) { Window = new Window { Width = 500, Height = 400, Content = control }; Window.Show(); Drain(); }
        public void Drain() { Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() { Window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class Row : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed; private double _reading;
        public int Observers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { _changed += value; Observers++; } remove { _changed -= value; Observers--; } }
        public double Reading { get => _reading; set { _reading = value; _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Reading))); } }
    }
}
