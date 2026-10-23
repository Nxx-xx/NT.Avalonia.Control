using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class SparklineControlTests
{
    [AvaloniaFact]
    public void Defaults_are_compact_axis_free_and_validate_finite_properties()
    {
        var chart = new SparklineChart();
        Assert.Equal(SparklineType.Line, chart.SparklineType); Assert.False(chart.ShowMarkers); Assert.True(chart.ShowMinMax);
        Assert.False(chart.ShowFirstLast); Assert.False(chart.ShowBaseline); Assert.Null(chart.Minimum); Assert.Null(chart.Maximum);
        chart.Measure(Size.Infinity); Assert.Equal(new Size(160, 40), chart.DesiredSize);
        Assert.Throws<ArgumentException>(() => chart.StrokeThickness = -1);
        Assert.Throws<ArgumentException>(() => chart.MarkerSize = double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => chart.BarSpacing = 1);
        Assert.Throws<ArgumentException>(() => chart.Baseline = double.NaN);
        Assert.Throws<ArgumentException>(() => chart.Minimum = double.NegativeInfinity);
        Assert.Throws<ArgumentException>(() => chart.SparklineType = (SparklineType)8);
    }

    [AvaloniaFact]
    public void Line_source_slots_use_fixed_inset_and_explicit_domain_without_axes()
    {
        var chart = Chart(new[] { 0d, 50, 100 }); using var host = new Host(chart);
        Assert.Equal(new Rect(2.5, 2.5, 295, 95), chart.PlotArea);
        Near(new(2.5, 97.5), chart.GetPoints()[0].Position); Near(new(150, 50), chart.GetPoints()[1].Position); Near(new(297.5, 2.5), chart.GetPoints()[2].Position);
        Assert.Equal(0, chart.EffectiveMinimum); Assert.Equal(100, chart.EffectiveMaximum); Assert.Equal(97.5, chart.BaselinePosition);
        Assert.DoesNotContain(Flatten(Render(chart)), d => d is GlyphRunDrawing);
    }

    [AvaloniaTheory]
    [InlineData(SparklineType.Bar)] [InlineData(SparklineType.WinLoss)]
    public void Bar_families_keep_source_slots_spacing_and_signed_baseline(SparklineType kind)
    {
        var chart = Chart(new[] { -2d, 0, 2 }); chart.SparklineType = kind; chart.Minimum = -2; chart.Maximum = 2;
        using var host = new Host(chart); var points = chart.GetPoints();
        Near(new(2.5 + 295d / 6, 97.5), points[0].Position); Near(new(150, 50), points[1].Position); Near(new(2.5 + 295d * 5 / 6, 2.5), points[2].Position);
        Assert.Equal(295d / 3 * .8, points[0].BarBounds.Width, 8); Assert.Equal(47.5, points[0].BarBounds.Height, 8);
        Assert.Equal(1, points[1].BarBounds.Height); Assert.Equal(50, points[1].BarBounds.Top);
        Assert.Equal(50, chart.BaselinePosition);
        if (kind == SparklineType.WinLoss) { Assert.Equal(-1, chart.EffectiveMinimum); Assert.Equal(1, chart.EffectiveMaximum); }
    }

    [AvaloniaFact]
    public void Automatic_area_and_bar_domains_include_the_configurable_baseline()
    {
        var chart = Chart(new[] { 20d, 40 }); chart.Minimum = chart.Maximum = null;
        using var host = new Host(chart); Assert.Equal(20, chart.EffectiveMinimum); Assert.Equal(40, chart.EffectiveMaximum);
        chart.SparklineType = SparklineType.Area; Assert.Equal(0, chart.EffectiveMinimum); Assert.Equal(40, chart.EffectiveMaximum);
        chart.SparklineType = SparklineType.Bar; chart.Baseline = 100;
        Assert.Equal(20, chart.EffectiveMinimum); Assert.Equal(100, chart.EffectiveMaximum); Assert.Equal(2.5, chart.BaselinePosition);
    }

    [AvaloniaFact]
    public void Win_loss_compares_raw_values_to_baseline_and_retains_raw_business_values()
    {
        var chart = Chart(new[] { 99d, 100, 101 }); chart.SparklineType = SparklineType.WinLoss; chart.Baseline = 100;
        using var host = new Host(chart); var points = chart.GetPoints();
        Assert.Equal(new[] { 97.5, 50, 2.5 }, points.Select(p => p.Position.Y));
        Assert.Equal(new[] { 99d, 100, 101 }, points.Select(p => p.Hit.Point.Y));
        Assert.Equal(101, chart.HitTest(points[2].BarBounds.Center)?.Point.Y);
    }

    [AvaloniaFact]
    public void Missing_points_preserve_slots_split_native_paths_and_never_create_interpolated_hits()
    {
        var chart = Chart(new[] { 0d, 20, double.NaN, 80, 100 }); chart.ShowMarkers = chart.ShowMinMax = false;
        using var host = new Host(chart); var points = chart.GetPoints();
        Assert.Equal(5, points.Count); Assert.False(points[2].IsVisible); Assert.Equal(2, points[2].Data.SourceIndex);
        Assert.Equal(150, points[2].Position.X); Assert.Null(chart.HitTest(new(150, 50), 0));
        var sink = new Sink(); SparklineChart.WritePathGeometry(points, chart.BaselinePosition, false, sink);
        Assert.Equal(2, sink.Starts.Count); Assert.Equal(2, sink.Lines.Count); Assert.All(sink.Closed, value => Assert.False(value));
        Assert.Equal(new[] { points[0].Position, points[3].Position }, sink.Starts);
        sink = new(); SparklineChart.WritePathGeometry(points, chart.BaselinePosition, true, sink);
        Assert.Equal(2, sink.Starts.Count); Assert.Equal(6, sink.Lines.Count); Assert.All(sink.Closed, value => Assert.True(value));
        Assert.Contains(new Point(points[1].Position.X, chart.BaselinePosition), sink.Lines);
        Assert.Contains(new Point(points[3].Position.X, chart.BaselinePosition), sink.Lines);
    }

    [AvaloniaTheory]
    [InlineData(SparklineType.Line)] [InlineData(SparklineType.Area)] [InlineData(SparklineType.Bar)] [InlineData(SparklineType.WinLoss)]
    public void Empty_and_all_invalid_sources_do_not_draw_invented_values(SparklineType kind)
    {
        var chart = Chart(Array.Empty<double>()); chart.SparklineType = kind;
        using var host = new Host(chart); Assert.Empty(chart.GetPoints()); Assert.Null(chart.HitTest(chart.PlotArea.Center));
        chart.ItemsSource = new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity };
        Assert.All(chart.GetPoints(), p => Assert.False(p.IsVisible)); Assert.Null(chart.HitTest(chart.PlotArea.Center));
        var sink = new Sink(); SparklineChart.WritePathGeometry(chart.GetPoints(), chart.BaselinePosition, true, sink); Assert.Empty(sink.Starts);
    }

    [AvaloniaFact]
    public void Single_constant_and_extreme_finite_domains_remain_finite()
    {
        var chart = Chart(new[] { 10d }); chart.Minimum = chart.Maximum = null;
        using var host = new Host(chart); var point = Assert.Single(chart.GetPoints()); Near(new(150, 50), point.Position);
        chart.ItemsSource = new[] { double.MaxValue, double.MaxValue }; Assert.True(chart.EffectiveMaximum > chart.EffectiveMinimum);
        Assert.All(chart.GetPoints(), p => Assert.True(double.IsFinite(p.Position.Y)));
        chart.ItemsSource = new[] { -double.MaxValue, 0, double.MaxValue };
        Assert.Equal(new[] { 97.5, 50, 2.5 }, chart.GetPoints().Select(p => p.Position.Y));
        chart.Minimum = double.Epsilon; chart.Maximum = double.Epsilon * 2; chart.ItemsSource = new[] { double.MaxValue, double.Epsilon };
        Assert.Equal(new[] { 2.5, 97.5 }, chart.GetPoints().Select(p => p.Position.Y));
    }

    [AvaloniaFact]
    public void Intermediate_binding_domains_stay_empty_until_limits_are_valid_and_reset_restores_auto_limits()
    {
        var chart = Chart(new[] { 10d, 30 }); using var host = new Host(chart);
        Key(chart, Avalonia.Input.Key.Home); Assert.NotNull(chart.ActivePoint);
        chart.Minimum = 50; chart.Maximum = 10; Assert.False(chart.IsDomainValid); Assert.All(chart.GetPoints(), p => Assert.False(p.IsVisible)); Assert.Null(chart.ActivePoint); _ = Render(chart);
        chart.Minimum = 10; chart.Maximum = 10; Assert.False(chart.IsDomainValid); Assert.All(chart.GetPoints(), p => Assert.False(p.IsVisible)); Assert.Null(chart.HitTest(chart.PlotArea.Center));
        chart.Maximum = 100; Assert.True(chart.IsDomainValid); Assert.All(chart.GetPoints(), p => Assert.True(p.IsVisible));
        chart.ResetViewportCommand.Execute(null); Assert.Null(chart.Minimum); Assert.Null(chart.Maximum);
        Assert.Equal(10, chart.EffectiveMinimum); Assert.Equal(30, chart.EffectiveMaximum);
    }

    [AvaloniaFact]
    public void Tiny_layout_returns_empty_finite_geometry_without_negative_rectangles()
    {
        var chart = Chart(new[] { 0d, 1 }); chart.Width = 2; chart.Height = 2; chart.Padding = new(20);
        using var host = new Host(chart); Assert.Equal(0, chart.PlotArea.Width); Assert.Equal(0, chart.PlotArea.Height);
        Assert.All(chart.GetPoints(), p => { Assert.False(p.IsVisible); Assert.True(double.IsFinite(p.Position.X)); Assert.True(double.IsFinite(p.Position.Y)); });
        Assert.Null(chart.HitTest(new(1, 1))); _ = Render(chart);
    }

    [AvaloniaFact]
    public void Categories_are_metadata_while_mapping_retains_original_item_stable_id_and_index()
    {
        var rows = new[] { new Row("b", "Later", 20), new Row("a", "Earlier", 80) };
        var chart = Chart(rows); chart.ValuePath = nameof(Row.Value); chart.CategoryPath = nameof(Row.Category); chart.IdPath = nameof(Row.Id);
        using var host = new Host(chart); var points = chart.GetPoints();
        Assert.Same(rows[0], points[0].Hit.Item); Assert.Equal("b", points[0].Hit.StableId); Assert.Equal("Later", points[0].Data.Category);
        Assert.Equal(0, points[0].Hit.Index); Assert.Equal(1, points[1].Hit.Index); Assert.Same(chart.Series, points[0].Hit.Series);
        Assert.Equal(2.5, points[0].Position.X); Assert.Equal(297.5, points[1].Position.X);
    }

    [AvaloniaFact]
    public void Arbitrary_category_objects_are_retained_as_metadata_without_invalidating_numeric_values()
    {
        var first = new CategoryKey("North"); var last = new CategoryKey("South");
        var rows = new[] { new { Id = "a", Category = first, Value = 20d }, new { Id = "b", Category = last, Value = 80d } };
        var chart = Chart(rows); chart.ValuePath = "Value"; chart.CategoryPath = "Category"; chart.IdPath = "Id";
        using var host = new Host(chart); var points = chart.GetPoints();
        Assert.All(points, p => Assert.True(p.IsVisible)); Assert.Same(first, points[0].Data.Category); Assert.Same(last, points[1].Data.Category);
        Assert.Equal(2.5, points[0].Position.X); Assert.Equal(297.5, points[1].Position.X); Assert.Equal(80, points[1].Hit.Point.Y);
        Assert.Same(rows[1], chart.HitTest(points[1].Position, 0)?.Item);
    }

    [AvaloniaFact]
    public void Live_item_property_and_collection_changes_refresh_projection_and_detach_releases_observers()
    {
        var row = new MutableRow("a", 20); var source = new ObservableCollection<MutableRow> { row };
        var chart = Chart(source); chart.ValuePath = nameof(MutableRow.Value); chart.IdPath = nameof(MutableRow.Id);
        using var host = new Host(chart); Assert.True(row.Observers > 0); Assert.Equal(78.5, Assert.Single(chart.GetPoints()).Position.Y);
        Key(chart, Avalonia.Input.Key.Home); row.Value = 80; host.Drain(); Assert.Equal(21.5, chart.ActivePoint?.Position.Y);
        var next = new MutableRow("a", 50); source[0] = next; host.Drain(); Assert.Equal(0, row.Observers); Assert.Same(next, chart.ActivePoint?.Item);
        source.Add(new("b", 100)); host.Drain(); Assert.Equal(2, chart.GetPoints().Count);
        source.Move(0, 1); host.Drain(); Assert.Equal(1, chart.ActivePoint?.Index); Assert.Equal("a", chart.ActivePoint?.StableId);
        host.Window.Content = null; host.Drain(); Assert.Equal(0, next.Observers);
        next.Value = 25; host.Window.Content = chart; host.Drain(); Assert.True(next.Observers > 0); Assert.Equal(25, chart.GetPoints()[1].Data.Point.Y);
        source.Clear(); host.Drain(); Assert.Empty(chart.GetPoints()); Assert.Null(chart.ActivePoint);
    }

    [AvaloniaFact]
    public void Replacing_source_unsubscribes_old_rows_and_updates_real_point_identity()
    {
        var a = new MutableRow("a", 10); var b = new MutableRow("b", 80);
        var chart = Chart(new[] { a }); chart.ValuePath = nameof(MutableRow.Value); chart.IdPath = nameof(MutableRow.Id);
        using var host = new Host(chart); Key(chart, Avalonia.Input.Key.Home); chart.ItemsSource = new[] { b }; host.Drain();
        Assert.Equal(0, a.Observers); Assert.True(b.Observers > 0); Assert.Null(chart.ActivePoint); Assert.Same(b, Assert.Single(chart.GetPoints()).Hit.Item);
    }

    [AvaloniaFact]
    public void Replacing_a_nested_mapping_owner_releases_old_path_observers_and_tracks_new_values()
    {
        var oldValue = new MutableRow("old", 10); var row = new NestedRow(oldValue);
        var chart = Chart(new[] { row }); chart.ValuePath = "Reading.Value"; chart.IdPath = nameof(NestedRow.Id);
        using var host = new Host(chart); Assert.True(row.Observers > 0); Assert.True(oldValue.Observers > 0);
        oldValue.Value = 80; host.Drain(); Assert.Equal(80, Assert.Single(chart.GetPoints()).Data.Point.Y);
        var replacement = new MutableRow("new", 40); row.Reading = replacement; host.Drain();
        Assert.Equal(0, oldValue.Observers); Assert.True(replacement.Observers > 0); Assert.Equal(40, Assert.Single(chart.GetPoints()).Data.Point.Y);
        replacement.Value = 60; host.Drain(); Assert.Equal(60, Assert.Single(chart.GetPoints()).Hit.Point.Y);
        host.Window.Content = null; host.Drain(); Assert.Equal(0, row.Observers); Assert.Equal(0, replacement.Observers);
    }

    [AvaloniaFact]
    public void Duplicate_business_ids_fail_with_existing_mapping_contract()
    {
        var chart = Chart(new[] { new Row("same", "A", 10), new Row("same", "B", 20) }); chart.ValuePath = nameof(Row.Value); chart.IdPath = nameof(Row.Id);
        Assert.Throws<InvalidOperationException>(() => chart.GetPoints());
    }

    [AvaloniaFact]
    public void Shared_mapping_reports_missing_paths_and_recovers_after_a_corrected_binding()
    {
        var rows = new[] { new Row("a", "A", 10), new Row("b", "B", 80) };
        var chart = Chart(rows); chart.IdPath = nameof(Row.Id); chart.ValuePath = "Missing.Value";
        var issues = new List<ChartDataIssue>(); chart.Series.DataError += (_, e) => issues.Add(e.Issue);
        using var host = new Host(chart); Assert.All(chart.GetPoints(), p => Assert.False(p.IsVisible));
        Assert.Contains(issues, i => i.SourceIndex == 0 && i.Path == "Missing.Value");
        Assert.Contains(issues, i => i.SourceIndex == 1 && i.Path == "Missing.Value");
        chart.ValuePath = nameof(Row.Value); host.Drain(); Assert.All(chart.GetPoints(), p => Assert.True(p.IsVisible));
        Assert.Same(rows[1], chart.GetPoints()[1].Hit.Item); Assert.Equal("b", chart.GetPoints()[1].Hit.StableId);
    }

    [AvaloniaFact]
    public void Shared_duplicate_id_failure_does_not_poison_later_valid_snapshots_or_active_identity()
    {
        var source = new ObservableCollection<Row> { new("a", "A", 10), new("b", "B", 80) };
        var chart = Chart(source); chart.ValuePath = nameof(Row.Value); chart.IdPath = nameof(Row.Id);
        using var host = new Host(chart); Key(chart, Avalonia.Input.Key.End); Assert.Equal("b", chart.ActivePoint?.StableId);
        source[1] = new("a", "Duplicate", 40); Assert.Throws<InvalidOperationException>(() => chart.GetPoints());
        source[1] = new("c", "Recovered", 60); var points = chart.GetPoints();
        Assert.All(points, p => Assert.True(p.IsVisible)); Assert.Equal("c", points[1].Hit.StableId); Assert.Null(chart.ActivePoint);
        Key(chart, Avalonia.Input.Key.End); Assert.Same(source[1], chart.ActivePoint?.Item);
    }

    [AvaloniaFact]
    public void Keyboard_skips_missing_slots_wraps_and_escape_clears_active_business_identity()
    {
        var chart = Chart(new[] { 10d, double.NaN, 90 }); using var host = new Host(chart);
        Key(chart, Avalonia.Input.Key.Home); Assert.Equal(0, chart.ActivePoint?.Index);
        Key(chart, Avalonia.Input.Key.Right); Assert.Equal(2, chart.ActivePoint?.Index);
        Key(chart, Avalonia.Input.Key.Right); Assert.Equal(0, chart.ActivePoint?.Index);
        Key(chart, Avalonia.Input.Key.Left); Assert.Equal(2, chart.ActivePoint?.Index);
        Key(chart, Avalonia.Input.Key.End); Assert.Equal(2, chart.ActivePoint?.Index);
        Key(chart, Avalonia.Input.Key.Escape); Assert.Null(chart.ActivePoint);
    }

    [AvaloniaFact]
    public void Bar_hit_testing_respects_spacing_and_zero_band_instead_of_nearest_slot()
    {
        var chart = Chart(new[] { -100d, 0, 100 }); chart.Minimum = -100; chart.SparklineType = SparklineType.Bar;
        using var host = new Host(chart); var points = chart.GetPoints();
        Assert.Equal(0, chart.HitTest(points[0].BarBounds.Center, 0)?.Index); Assert.Equal(1, chart.HitTest(points[1].BarBounds.Center, 0)?.Index);
        Assert.Null(chart.HitTest(new(2.5, 75), 100)); Assert.Null(chart.HitTest(new(150, 20), 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => chart.HitTest(default, -1));
    }

    [AvaloniaFact]
    public void Marker_hits_include_painted_inset_caps_but_do_not_expand_hidden_markers()
    {
        var chart = Chart(new[] { 0d, 100 }); using var host = new Host(chart);
        var outsideEndpoint = chart.GetPoints()[0].Position - new Vector(1, 0);
        Assert.Equal(0, chart.HitTest(outsideEndpoint, 0)?.Index);
        chart.ShowMinMax = false; Assert.Null(chart.HitTest(outsideEndpoint, 0));
        chart.ShowMarkers = true; Assert.Equal(0, chart.HitTest(outsideEndpoint, 0)?.Index);
        chart.ShowMarkers = false; chart.StrokeThickness = 0; Assert.Null(chart.HitTest(chart.PlotArea.Center, 0));
    }

    [AvaloniaFact]
    public void First_last_and_min_max_flags_keep_all_ties_and_only_valid_endpoints()
    {
        var chart = Chart(new[] { double.NaN, 20d, 80, 20, 80, double.NaN }); using var host = new Host(chart); var points = chart.GetPoints();
        Assert.Equal(new[] { 1, 3 }, points.Where(p => p.IsMinimum).Select(p => p.Data.SourceIndex));
        Assert.Equal(new[] { 2, 4 }, points.Where(p => p.IsMaximum).Select(p => p.Data.SourceIndex));
        Assert.Equal(1, Assert.Single(points, p => p.IsFirst).Data.SourceIndex); Assert.Equal(4, Assert.Single(points, p => p.IsLast).Data.SourceIndex);
    }

    [AvaloniaFact]
    public void Emphasis_brush_precedence_and_positive_negative_zero_paints_reach_native_drawing()
    {
        var chart = Chart(new[] { -1d, 0, 1 }); chart.Minimum = -1; chart.Maximum = 1;
        chart.SparklineType = SparklineType.WinLoss; chart.ShowMinMax = false; chart.WinBrush = Brushes.Lime; chart.LossBrush = Brushes.Red; chart.ZeroBrush = Brushes.Gold;
        using var host = new Host(chart); var paints = Flatten(Render(chart)).OfType<GeometryDrawing>().Select(d => ColorOf(d.Brush)).ToArray();
        Assert.Contains(Colors.Lime, paints); Assert.Contains(Colors.Red, paints); Assert.Contains(Colors.Gold, paints);
        chart.SparklineType = SparklineType.Bar; chart.BarBrush = Brushes.Magenta;
        paints = Flatten(Render(chart)).OfType<GeometryDrawing>().Select(d => ColorOf(d.Brush)).ToArray();
        Assert.Contains(Colors.Magenta, paints); Assert.Contains(Colors.Gold, paints); Assert.DoesNotContain(Colors.Lime, paints); Assert.DoesNotContain(Colors.Red, paints);
        chart.SparklineType = SparklineType.Line; chart.ShowFirstLast = true; chart.ShowMinMax = true;
        chart.FirstBrush = Brushes.Magenta; chart.LastBrush = Brushes.Cyan; chart.MinBrush = Brushes.Red; chart.MaxBrush = Brushes.Green;
        paints = Flatten(Render(chart)).OfType<GeometryDrawing>().Select(d => ColorOf(d.Brush)).ToArray();
        Assert.Contains(Colors.Magenta, paints); Assert.Contains(Colors.Cyan, paints); Assert.DoesNotContain(Colors.Red, paints); Assert.DoesNotContain(Colors.Green, paints);
    }

    [AvaloniaFact]
    public void Formatter_reentrant_source_change_keeps_current_frame_and_refreshes_next_frame()
    {
        var chart = Chart(new[] { 20d, 80 }); using var host = new Host(chart); Key(chart, Avalonia.Input.Key.Home);
        var calls = 0; chart.TooltipFormatter = hit => { calls++; chart.ItemsSource = new[] { 50d }; Assert.Equal(2, chart.GetPoints().Count); return $"item {hit.Index}"; };
        _ = Render(chart); Assert.Equal(1, calls); Assert.Single(chart.GetPoints()); Assert.Null(chart.ActivePoint);
    }

    [AvaloniaTheory]
    [InlineData(96)] [InlineData(144)] [InlineData(192)]
    public void Export_layout_preserves_live_geometry_and_draws_vectors_at_requested_dpi(int dpi)
    {
        var chart = Chart(new[] { 10d, 90 }); chart.LineBrush = Brushes.Magenta; using var host = new Host(chart);
        var before = chart.GetPoints().Select(p => p.Position).ToArray(); var layout = chart.GetExportLayout(new() { PixelSize = new(600, 200), Dpi = dpi });
        Assert.Equal(new PixelSize(600, 200), layout.Pixels); Assert.Equal(2, layout.Scale); Assert.Equal(dpi, layout.Dpi);
        var group = new DrawingGroup(); using (var context = group.Open()) chart.RenderExport(context, layout, Brushes.White);
        Assert.DoesNotContain(Flatten(group), d => d is ImageDrawing || d is GeometryDrawing { Brush: VisualBrush });
        Assert.Contains(Flatten(group), d => d is GeometryDrawing { Pen.Brush: ISolidColorBrush brush } && brush.Color == Colors.Magenta);
        Assert.Equal(before, chart.GetPoints().Select(p => p.Position));
    }

    [AvaloniaFact]
    public async Task Export_cancellation_and_validation_failures_keep_owner_sender_and_stream_open()
    {
        var chart = Chart(new[] { 10d, 90 }); using var host = new Host(chart); using var stream = new MemoryStream();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportPngAsync(stream, cancellationToken: cancellation.Token));
        object? sender = null; ChartExportFailedEventArgs? failure = null; chart.ExportFailed += (s, e) => { sender = s; failure = e; };
        Assert.False(await chart.TryExportJpegAsync(stream, new() { PixelSize = new(0, 1) })); Assert.Same(chart, sender); Assert.NotNull(failure); Assert.True(stream.CanWrite);
    }

    private static SparklineChart Chart(System.Collections.IEnumerable source) => new() { Width = 300, Height = 100, Padding = new(0), ItemsSource = source, Minimum = 0, Maximum = 100 };
    private static void Key(SparklineChart chart, Key key) => chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
    private static void Near(Point expected, Point actual) { Assert.Equal(expected.X, actual.X, 8); Assert.Equal(expected.Y, actual.Y, 8); }
    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;
    private static DrawingGroup Render(SparklineChart chart) { var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context); return drawing; }
    private static IEnumerable<Drawing> Flatten(Drawing drawing) { yield return drawing; if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var item in Flatten(child)) yield return item; }
    private sealed record Row(string Id, string Category, double Value);
    private sealed record CategoryKey(string Name);
    private sealed class MutableRow(string id, double value) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        private double _value = value;
        public string Id { get; } = id; public int Observers { get; private set; }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
    private sealed class NestedRow(MutableRow reading) : INotifyPropertyChanged
    {
        private MutableRow _reading = reading;
        private PropertyChangedEventHandler? _changed;
        public string Id => "nested"; public int Observers { get; private set; }
        public MutableRow Reading { get => _reading; set { _reading = value; _changed?.Invoke(this, new(nameof(Reading))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(SparklineChart chart) { Window = new Window { Width = 300, Height = 100, Content = chart }; Window.Show(); Drain(); }
        public void Drain() { Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() { Window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class Sink : ICircularGeometrySink
    {
        public List<Point> Starts { get; } = []; public List<Point> Lines { get; } = []; public List<bool> Closed { get; } = [];
        public void SetFillRule(FillRule rule) { }
        public void BeginFigure(Point start, bool isFilled) => Starts.Add(start);
        public void ArcTo(Point end, Size radii, double rotation, bool largeArc, SweepDirection direction) => throw new InvalidOperationException("Sparkline paths must not contain arcs.");
        public void LineTo(Point end) => Lines.Add(end);
        public void EndFigure(bool close) => Closed.Add(close);
    }
}
