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

public sealed class CategoricalDistributionTests
{
    [Fact]
    public void Swarm_preserves_category_order_original_indices_ids_and_values_and_uses_one_numeric_scale()
    {
        CategoricalSample[] rows = [new("b", "B", 8), new("a", "A", 2), new("c", "B", 5)];
        var result = ChartCategoricalDistribution.Layout(rows, width: 200, height: 100, markerRadius: 5, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new object[] { "B", "A" }, result.Categories.Select(category => category.Category));
        Assert.Equal(new[] { 0, 1, 2 }, result.Points.Select(point => point.SourceIndex));
        Assert.Equal(rows.Select(row => row.Id), result.Points.Select(point => point.Id));
        Assert.Equal(rows.Select(row => row.Value), result.Points.Select(point => point.Value));
        Assert.Equal(new[] { 5d, 95d, 50d }, result.Points.Select(point => point.Y));
        Assert.Equal(new[] { 50d, 150d, 50d }, result.Points.Select(point => point.X));
        Assert.Equal(6.5, result.Categories[0].Mean); Assert.Equal(2, result.Categories[0].Count);
        Assert.Equal(0, result.CollisionCount); Assert.Equal(0, result.OverflowCount);
    }

    [Fact]
    public void Swarm_equal_values_spread_without_changing_the_value_or_vertical_coordinate()
    {
        var rows = Enumerable.Range(0, 9).Select(index => new CategoricalSample(index, "A", 10)).ToArray();
        var result = ChartCategoricalDistribution.Layout(rows, width: 300, height: 100, markerRadius: 5, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 150d, 160, 140, 170, 130, 180, 120, 190, 110 }, result.Points.Select(point => point.X));
        Assert.All(result.Points, point => { Assert.Equal(50, point.Y); Assert.Equal(10, point.Value); Assert.False(point.IsOverflow); });
        for (var i = 0; i < result.Points.Count; i++) for (var j = i + 1; j < result.Points.Count; j++)
        { var dx = result.Points[i].X - result.Points[j].X; var dy = result.Points[i].Y - result.Points[j].Y; Assert.True(dx * dx + dy * dy >= 100 - 1e-8); }
    }

    [Fact]
    public void Strip_is_deterministic_and_seed_changes_only_horizontal_jitter()
    {
        var rows = Enumerable.Range(0, 20).Select(index => new CategoricalSample(index, index % 2, index * .5)).ToArray();
        var a = ChartCategoricalDistribution.Layout(rows, CategoricalDistributionKind.Strip, seed: 17, cancellationToken: TestContext.Current.CancellationToken);
        var b = ChartCategoricalDistribution.Layout(rows, CategoricalDistributionKind.Strip, seed: 17, cancellationToken: TestContext.Current.CancellationToken);
        var c = ChartCategoricalDistribution.Layout(rows, CategoricalDistributionKind.Strip, seed: 18, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(a.Points, b.Points); Assert.Contains(a.Points.Zip(c.Points), pair => pair.First.X != pair.Second.X);
        Assert.Equal(a.Points.Select(point => point.Y), c.Points.Select(point => point.Y));
        Assert.Equal(rows.Select(row => row.Value), c.Points.Select(point => point.Value));
        Assert.All(a.Points, point => Assert.InRange(point.X, point.CategoryIndex * 186 + 4, (point.CategoryIndex + 1) * 186 - 4));
    }

    [Fact]
    public void Zero_jitter_dense_swarm_keeps_all_observations_and_reports_collisions_and_overflow()
    {
        var result = ChartCategoricalDistribution.Layout(Enumerable.Range(0, 100).Select(i => new CategoricalSample(i, "A", 2)), jitter: 0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(100, result.Points.Count); Assert.Equal(99, result.CollisionCount); Assert.Equal(99, result.OverflowCount);
        Assert.False(result.WorkBudgetExceeded); Assert.All(result.Points, point => { Assert.Equal(186, point.X); Assert.Equal(116, point.Y); });
    }

    [Fact]
    public void Work_budget_returns_every_original_point_and_exposes_the_degraded_layout()
    {
        var result = ChartCategoricalDistribution.Layout(Enumerable.Range(0, 1800).Select(i => new CategoricalSample(i, "A", 2)), width: 100_000, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.WorkBudgetExceeded); Assert.Equal(ChartCategoricalDistribution.MaximumWork, result.WorkCount);
        Assert.Equal(1800, result.Points.Count); Assert.True(result.OverflowCount > 0);
        Assert.Equal(Enumerable.Range(0, 1800), result.Points.Select(point => point.SourceIndex));
        Assert.All(result.Points, point => { Assert.True(double.IsFinite(point.X)); Assert.True(double.IsFinite(point.Y)); });
    }

    [Fact]
    public void Extreme_signed_values_use_a_finite_scale_and_overflow_safe_mean()
    {
        var result = ChartCategoricalDistribution.Layout(new[] { new CategoricalSample(1, "A", -double.MaxValue), new CategoricalSample(2, "A", double.MaxValue) }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, result.Categories[0].Mean); Assert.Equal(4, result.Points[1].Y); Assert.Equal(228, result.Points[0].Y);
        var same = ChartCategoricalDistribution.Layout(new[] { new CategoricalSample(1, "A", double.MaxValue), new CategoricalSample(2, "A", double.MaxValue) }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(double.MaxValue, same.Categories[0].Mean); Assert.All(same.Points, point => Assert.True(double.IsFinite(point.Y)));
    }

    [Fact]
    public void Tiny_lanes_and_small_height_remain_finite_and_report_insufficient_space()
    {
        var result = ChartCategoricalDistribution.Layout(new[] { new CategoricalSample(1, "A", 2), new CategoricalSample(2, "B", 3) }, width: 1, height: 1, markerRadius: 5, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, result.OverflowCount); Assert.All(result.Points, point => { Assert.InRange(point.X, 0, 1); Assert.Equal(.5, point.Y); });
    }

    [Fact]
    public void Empty_input_is_owned_and_has_zero_diagnostics()
    {
        var result = ChartCategoricalDistribution.Layout([], cancellationToken: TestContext.Current.CancellationToken); Assert.Empty(result.Points); Assert.Empty(result.Categories);
        Assert.Equal(0, result.Minimum); Assert.Equal(0, result.Maximum); Assert.Equal(0, result.WorkCount);
    }

    [Fact]
    public void Result_is_a_snapshot_of_the_input_collection()
    {
        var source = new List<CategoricalSample> { new(1, "A", 2) }; var result = ChartCategoricalDistribution.Layout(source, cancellationToken: TestContext.Current.CancellationToken);
        source[0] = new(1, "B", 99); Assert.Equal("A", result.Points[0].Category); Assert.Equal(2, result.Points[0].Value);
        Assert.Throws<NotSupportedException>(() => ((IList<CategoricalDistributionPoint>)result.Points).Clear());
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void Nonfinite_values_fail_without_fabricating_observations(double value) =>
        Assert.Throws<ArgumentException>(() => ChartCategoricalDistribution.Layout(new[] { new CategoricalSample(1, "A", value) }, cancellationToken: TestContext.Current.CancellationToken));

    [Fact]
    public void Invalid_categories_dimensions_jitter_kind_and_radius_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => ChartCategoricalDistribution.Layout(new[] { new CategoricalSample(1, null!, 2) }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartCategoricalDistribution.Layout([], width: 0, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartCategoricalDistribution.Layout([], height: double.NaN, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartCategoricalDistribution.Layout([], jitter: .51, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartCategoricalDistribution.Layout([], markerRadius: -1, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartCategoricalDistribution.Layout([], (CategoricalDistributionKind)99, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartCategoricalDistribution.Layout(Enumerable.Range(0, ChartCategoricalDistribution.MaximumCount + 1).Select(i => new CategoricalSample(i, "A", i)), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Cancellation_is_honored_before_and_during_enumeration()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => ChartCategoricalDistribution.Layout([], cancellationToken: cancellation.Token));
        using var during = new CancellationTokenSource();
        IEnumerable<CategoricalSample> Source() { yield return new(1, "A", 2); during.Cancel(); yield return new(2, "A", 3); }
        Assert.ThrowsAny<OperationCanceledException>(() => ChartCategoricalDistribution.Layout(Source(), cancellationToken: during.Token));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Both_controls_keep_original_items_values_categories_and_all_hits(bool strip)
    {
        CategoricalDistributionChartBase chart = strip ? new StripPlot() : new BeeswarmChart();
        var first = new Sample("first", "A", 3); var second = new Sample("second", "A", 3);
        chart.ItemsSource = new[] { first, second }; chart.Jitter = 0;
        using var host = new Host(chart); var marks = chart.GetMarks(); Assert.Equal(2, marks.Count); Assert.Null(chart.DataError);
        Assert.Same(first, marks[0].Members[0].Item); Assert.Equal("first", marks[0].Members[0].StableId); Assert.Equal("A", marks[0].Members[0].Category);
        Assert.Equal(3, marks[0].Members[0].Point.Y); Assert.Equal(3d, marks[0].Members[0].Value);
        var hits = chart.HitTestAll(marks[0].Position); Assert.Equal(2, hits.Count);
        Assert.Same(second, hits[0].Members[0].Item); Assert.Same(first, hits[1].Members[0].Item);
        Assert.Equal(1, chart.HitTest(marks[0].Position)!.Index); Assert.Equal(1, chart.CollisionCount);
        Assert.Empty(chart.HitTestAll(new Point(-1, -1)));
    }

    [AvaloniaFact]
    public void Typed_samples_work_with_default_mapping_and_keep_their_ids()
    {
        var rows = new[] { new CategoricalSample("a", "A", 5, "first"), new CategoricalSample("b", "B", 8, "second") };
        var chart = new BeeswarmChart { ItemsSource = rows }; using var host = new Host(chart);
        Assert.Equal(2, chart.GetMarks().Count); Assert.Equal("a", chart.GetMarks()[0].Members[0].StableId);
        Assert.Contains("first", chart.GetMarks()[0].TooltipText); Assert.Equal(rows[0], chart.GetMarks()[0].Members[0].Item);
        Assert.Equal(0, chart.GetMarks()[0].Members[0].SourceIndex); Assert.Equal(1, chart.GetMarks()[1].Members[0].SourceIndex);
    }

    [AvaloniaFact]
    public void Convenience_names_and_styled_aliases_stay_synchronized_and_custom_fills_and_strokes_are_painted()
    {
        var chart = new StripPlotChart { ItemsSource = new[] { new CategoricalSample("a", "A", 2) }, Fill = Brushes.Red, Stroke = Brushes.Blue, StrokeThickness = 3, ShowMeanLine = false, ShowAxes = false };
        using var host = new Host(chart); chart.PointRadius = 6; Assert.Equal(12, chart.MarkerSize);
        chart.MarkerSize = 10; Assert.Equal(5, chart.PointRadius);
        chart.JitterAmount = .2; Assert.Equal(.2, chart.Jitter); chart.Jitter = .1; Assert.Equal(.1, chart.JitterAmount);
        Assert.False(chart.ShowMean); chart.ShowMean = true; Assert.True(chart.ShowMeanLine); chart.ShowMeanLine = false;
        var mark = Assert.Single(chart.GetMarks()); Assert.Same(Brushes.Red, mark.Fill); Assert.Equal(10, mark.Bounds.Width);
        var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context);
        var geometry = Assert.Single(Flatten(drawing).OfType<GeometryDrawing>(), item => ReferenceEquals(item.Brush, Brushes.Red));
        Assert.Same(Brushes.Red, geometry.Brush); Assert.Same(Brushes.Blue, geometry.Pen!.Brush); Assert.Equal(3, geometry.Pen.Thickness);
        Assert.IsAssignableFrom<BeeswarmChart>(new BeeswarmPlotChart());
    }

    [AvaloniaFact]
    public void Omitting_the_id_path_uses_original_reference_identity_for_plain_business_objects()
    {
        var a = new Sample("a", "A", 2); var b = new Sample("b", "B", 8); var rows = new ObservableCollection<Sample> { a, b };
        var chart = new BeeswarmChart { ItemsSource = rows, IdPath = null }; using var host = new Host(chart);
        chart.SelectedIndex = 1; rows.Move(1, 0); host.Drain(); Assert.Same(b, chart.SelectedPoint!.Members[0].Item); Assert.Same(b, chart.SelectedPoint.Members[0].StableId);
    }

    [AvaloniaFact]
    public void DTO_notifications_collection_reorder_and_replacement_follow_ids_and_release_retired_observers()
    {
        var a = new Sample("a", "A", 2); var b = new Sample("b", "B", 8); var rows = new ObservableCollection<Sample> { a, b };
        var chart = new BeeswarmChart { ItemsSource = rows }; using var host = new Host(chart);
        chart.SelectedIndex = 1; rows.Move(1, 0); host.Drain(); Assert.Equal(0, chart.SelectedIndex); Assert.Same(b, chart.SelectedPoint!.Members[0].Item);
        b.Value = 11; host.Drain(); Assert.Equal(11, chart.SelectedPoint.Mark.Value);
        b.Category = "A"; host.Drain(); Assert.Single(chart.Layout!.Categories); Assert.Equal("A", chart.SelectedPoint.Members[0].Category);
        var replacement = new Sample("b", "C", 7); chart.ItemsSource = new[] { replacement }; host.Drain();
        Assert.Same(replacement, chart.SelectedPoint!.Members[0].Item); Assert.Equal(0, a.Observers); Assert.Equal(0, b.Observers); Assert.True(replacement.Observers > 0);
        host.Detach(); Assert.Equal(0, replacement.Observers); host.Attach(); Assert.True(replacement.Observers > 0);
    }

    [AvaloniaFact]
    public void Custom_nested_paths_are_observed_and_invalid_values_recover_without_stale_layout()
    {
        var detail = new Sample("a", "A", 2); var chart = new StripPlot { ItemsSource = new[] { new { Detail = detail } }, IdPath = "Detail.Id", ValuePath = "Detail.Value", CategoryPath = "Detail.Category", LabelPath = "Detail.Id" };
        using var host = new Host(chart); Assert.Single(chart.GetMarks());
        detail.Value = double.NaN; host.Drain(); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); Assert.Null(chart.Layout);
        detail.Value = 4; host.Drain(); Assert.Single(chart.GetMarks()); Assert.Null(chart.DataError); Assert.Equal(4, chart.GetMarks()[0].Value);
    }

    [AvaloniaFact]
    public void Duplicate_explicit_ids_clear_the_frame_and_corrected_source_recovers()
    {
        var chart = new BeeswarmChart { ItemsSource = new[] { new CategoricalSample("a", "A", 1), new CategoricalSample("a", "B", 2) } };
        using var host = new Host(chart); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); Assert.Null(chart.Layout);
        chart.ItemsSource = new[] { new CategoricalSample("b", "A", 3) }; Assert.Single(chart.GetMarks()); Assert.Null(chart.DataError);
    }

    [AvaloniaFact]
    public void Zoom_pan_resize_and_sort_keep_the_selected_original_sample()
    {
        var rows = new[] { new CategoricalSample("a", "A", 1), new CategoricalSample("b", "A", 4), new CategoricalSample("c", "B", 8) };
        var chart = new StripPlot { ItemsSource = rows }; using var host = new Host(chart);
        chart.SelectedIndex = 1; chart.Zoom = 1.2; chart.PanX = .03; chart.PanY = -.01; host.Resize(520, 320);
        Assert.Equal("b", chart.SelectedPoint!.Members[0].StableId); Assert.Equal(4, chart.SelectedPoint.Mark.Value);
        chart.Sort = ComparisonSort.ValueDescending; Assert.Equal("b", chart.SelectedPoint.Members[0].StableId);
        Assert.Equal(new[] { 2, 1, 0 }, chart.GetMarks().Select(mark => mark.Members[0].SourceIndex));
        var position = chart.SelectedPoint.Position; Assert.Contains(chart.HitTestAll(position), hit => Equals(hit.Members[0].StableId, "b"));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Both_families_support_keyboard_mean_decorations_theme_and_export_error_contracts(bool strip)
    {
        CategoricalDistributionChartBase chart = strip ? new StripPlot() : new BeeswarmChart(); chart.Foreground = Brushes.White;
        chart.ItemsSource = new[] { new CategoricalSample("a", "A", 2), new CategoricalSample("b", "A", 4) }; using var host = new Host(chart);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home });
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Assert.Equal(0, chart.SelectedIndex);
        var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context); Assert.NotEmpty(group.Children);
        chart.ShowMean = false; Assert.Equal(2, chart.GetMarks().Count); Assert.Same(Brushes.White, chart.Foreground);
        using var stream = new MemoryStream(); var failures = 0; chart.ExportFailed += (_, _) => failures++;
        Assert.False(chart.TryExportPng(stream, new ChartExportOptions { Dpi = 0 })); Assert.Equal(1, failures); Assert.Equal(0, stream.Length);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportPngAsync(stream, cancellationToken: cancelled.Token)); Assert.Equal(1, failures);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Assert.Null(chart.SelectedPoint);
    }

    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly EngineeringChartBase _chart;
        public Host(EngineeringChartBase chart) { _chart = chart; _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); }
        public void Resize(double width, double height) { _window.Width = width; _window.Height = height; Drain(); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Attach() { _window.Content = _chart; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    { yield return drawing; if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var nested in Flatten(child)) yield return nested; }
    private sealed class Sample(string id, string category, double value) : INotifyPropertyChanged
    {
        private double _value = value; private string _category = category; private PropertyChangedEventHandler? _changed;
        public string Id { get; } = id; public int Observers { get; private set; }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public string Category { get => _category; set { _category = value; _changed?.Invoke(this, new(nameof(Category))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
}
