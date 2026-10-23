using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class DistributionProfileTests
{
    [Fact]
    public void Profiles_share_a_domain_and_absolute_density_scale_and_preserve_original_sample_indices()
    {
        var a = new DistributionSamples("a", "first", new[] { double.NaN, 1d, 2, double.PositiveInfinity, 3, 4, 5 });
        var b = new DistributionSamples("b", "second", new[] { 10d, 20, 30 });
        var result = ChartDistributionProfiles.Calculate([a, b], 33, .75, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(-1.25, result.Minimum); Assert.Equal(32.25, result.Maximum);
        Assert.Equal(10, result.InputCount); Assert.Equal(2, result.OmittedCount); Assert.Equal(2, result.GroupCount);
        Assert.Same(a, result.Groups[0].SourceItem); Assert.Same(b, result.Groups[1].SourceItem);
        Assert.Equal(new[] { 1, 2, 4, 5, 6 }, result.Groups[0].Observations.Select(point => point.OriginalIndex));
        Assert.Equal(3, result.Groups[0].Summary.Median);
        Assert.Equal(result.Groups[0].Density.Points.Select(point => point.X), result.Groups[1].Density.Points.Select(point => point.X));
        foreach (var group in result.Groups)
        {
            var expected = ChartStatistics.KernelDensity(group.Source.Samples, 33, .75, result.Minimum, result.Maximum);
            Assert.Equal(expected.Points, group.Density.Points);
        }
        Assert.Equal(result.Groups.SelectMany(group => group.Density.Points).Max(point => point.Y), result.MaximumDensity);
    }

    [Fact]
    public void Empty_missing_singleton_and_constant_groups_have_explicit_rules()
    {
        var result = ChartDistributionProfiles.Calculate([
            new(null, "empty", Array.Empty<double>()), new(null, "missing", new[] { double.NaN }),
            new(null, "one", new[] { 2d }), new(null, "constant", new[] { 7d, 7, 7 })], 17, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 0, 1 }, result.EmptyGroupIndices); Assert.Equal(new[] { 2, 3 }, result.Groups.Select(group => group.SourceIndex));
        Assert.Equal(1, result.OmittedCount);
        // The fallback is the scale, then Silverman's .9 * scale * n^(-.2) is still applied.
        Assert.Equal(.9, result.Groups[0].Density.Bandwidth);
        Assert.Equal(.9 * Math.Pow(3, -.2), result.Groups[1].Density.Bandwidth);
        Assert.Equal(7, result.Groups[1].Summary.LowerQuartile); Assert.Equal(7, result.Groups[1].Summary.UpperQuartile);
        Assert.True(result.Groups.All(group => group.Density.Points.All(point => double.IsFinite(point.X) && point.Y >= 0)));
        var empty = ChartDistributionProfiles.Calculate([], cancellationToken: TestContext.Current.CancellationToken); Assert.Empty(empty.Groups); Assert.Equal(0, empty.Minimum); Assert.Equal(1, empty.Maximum);
    }

    [Fact]
    public void Explicit_domain_does_not_filter_samples_or_force_trapezoidal_area_to_one()
    {
        var values = new[] { 0d, 1, 2, 20 };
        var result = ChartDistributionProfiles.Calculate([new("g", "g", values)], 9, 1, 0, 2, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(4, result.Groups[0].Density.Count); Assert.Equal(1.5, result.Groups[0].Summary.Median);
        Assert.Equal(ChartStatistics.KernelDensity(values, 9, 1, 0, 2).SampledArea, result.Groups[0].Density.SampledArea);
        Assert.True(result.Groups[0].Density.SampledArea < 1);
    }

    [Fact]
    public void Invalid_parameters_and_unrepresentable_domains_are_rejected()
    {
        DistributionSamples[] rows = [new(1, "a", new[] { 1d, 2 })];
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartDistributionProfiles.Calculate(rows, 1, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartDistributionProfiles.Calculate(rows, bandwidth: 0, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartDistributionProfiles.Calculate(rows, minimum: double.NaN, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartDistributionProfiles.Calculate(rows, minimum: 4, maximum: 3, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartDistributionProfiles.Calculate(rows, minimum: -double.MaxValue, maximum: double.MaxValue, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Work_and_actual_enumeration_budgets_count_omitted_entries_and_dispose_enumerators()
    {
        var samples = new EndlessSamples();
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartDistributionProfiles.Calculate([new(1, "missing", samples)], 2, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(100_001, samples.Enumerated); Assert.True(samples.Disposed);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartDistributionProfiles.Calculate([new(1, "work", Enumerable.Repeat(1d, 1000).ToArray())], 4096, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartDistributionProfiles.Calculate(Enumerable.Range(0, 33).Select(index => new DistributionSamples(index, "knots", [1d])), 4096, cancellationToken: TestContext.Current.CancellationToken));
        var enumerated = 0;
        IEnumerable<DistributionSamples> Groups() { while (true) { enumerated++; yield return new(enumerated, "empty", []); } }
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartDistributionProfiles.Calculate(Groups(), cancellationToken: TestContext.Current.CancellationToken)); Assert.Equal(257, enumerated);
    }

    [Fact]
    public void Cancellation_is_observed_before_and_during_enumeration_without_partial_results()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => ChartDistributionProfiles.Calculate([], cancellationToken: cancelled.Token));
        using var during = new CancellationTokenSource(); var samples = new EndlessSamples(() => during.Cancel());
        Assert.Throws<OperationCanceledException>(() => ChartDistributionProfiles.Calculate([new(1, "cancel", samples)], cancellationToken: during.Token));
        Assert.Equal(1, samples.Enumerated); Assert.True(samples.Disposed);
        Assert.Throws<OperationCanceledException>(() => ChartDistributionProfiles.FromCurves([], cancellationToken: cancelled.Token));
    }

    [Fact]
    public void Precomputed_curves_preserve_source_knots_without_inventing_statistics_or_normalizing_heights()
    {
        var a = new DistributionCurve("a", "a", new[] { new ChartPoint(-2, 0), new ChartPoint(0, 4), new ChartPoint(2, 0) });
        var b = new DistributionCurve("b", "b", new[] { new ChartPoint(0, 0), new ChartPoint(1, 2), new ChartPoint(4, 0) });
        var result = ChartDistributionProfiles.FromCurves([a, b], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(-2, result.Minimum); Assert.Equal(4, result.Maximum); Assert.Equal(4, result.MaximumDensity); Assert.Equal(6, result.PointCount);
        Assert.Same(a, result.Groups[0].SourceItem); Assert.Equal(a.Points, result.Groups[0].Points);
        Assert.Equal(b.Points, result.Groups[1].Points); Assert.Equal(1, result.Groups[1].SourceIndex);
        Assert.Throws<ArgumentException>(() => ChartDistributionProfiles.FromCurves([new(1, "bad", new[] { new ChartPoint(0, 0), new ChartPoint(0, 2) })], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartDistributionProfiles.FromCurves([new(1, "bad", new[] { new ChartPoint(0, 0), new ChartPoint(1, -2) })], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartDistributionProfiles.FromCurves([new(1, "bad", new[] { new ChartPoint(0, 0), new ChartPoint(double.NaN, 2) })], cancellationToken: TestContext.Current.CancellationToken));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Charts_return_the_real_source_group_and_immutable_sample_metadata_after_hit_zoom_and_reorder(bool ridge)
    {
        var a = new DistributionSamples("a", "A", new[] { 1d, 2, 3, 4, 5 }); var b = new DistributionSamples("b", "B", new[] { 3d, 4, 5, 6, 7 });
        var rows = new ObservableCollection<DistributionSamples> { a, b };
        DistributionProfileChartBase chart = ridge ? new RidgelineChart() : new ViolinPlot(); chart.ItemsSource = rows;
        using var host = new Host(chart); Assert.Equal(2, chart.GetMarks().Count); Assert.Null(chart.DataError);
        var mark = chart.GetMarks()[0]; var hit = chart.HitTest(mark.Position)!;
        Assert.Same(a, Assert.Single(hit.Members).Item); var metadata = Assert.IsType<ChartDistributionProfile>(hit.Members[0].Value);
        Assert.Same(a, metadata.SourceItem); Assert.Equal(new[] { 0, 1, 2, 3, 4 }, metadata.Observations.Select(point => point.OriginalIndex));
        chart.SelectedIndex = 1; rows.Move(1, 0); host.Drain(); Assert.Equal(0, chart.SelectedIndex); Assert.Same(b, chart.SelectedPoint!.Members[0].Item);
        chart.Zoom = 1.05; chart.PanX = .01; chart.PanY = -.01; host.Drain(); Assert.Same(b, chart.SelectedPoint!.Members[0].Item);
        Assert.Equal(5, chart.GetGridLines().Count); Assert.NotEmpty(Record(chart).Children);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Nested_collection_and_owner_changes_rebuild_and_release_replaced_sources_and_detached_observers(bool ridge)
    {
        var first = new SamplesCollection([1, 2, 3]); var second = new SamplesCollection([4, 5, 6]); var payload = new Payload(first);
        var row = new Group("a", payload); DistributionProfileChartBase chart = ridge ? new RidgelineChart() : new ViolinPlot();
        chart.ItemsSource = new[] { row }; chart.SamplesPath = "Payload.Samples";
        using var host = new Host(chart); Assert.Single(chart.GetMarks()); Assert.True(first.Observers > 0); Assert.True(payload.Observers > 0);
        first.Add(9); host.Drain(); Assert.Equal(4, chart.Layout!.Groups[0].Observations.Count);
        payload.Samples = second; host.Drain(); Assert.Equal(0, first.Observers); Assert.True(second.Observers > 0); Assert.Equal(5, chart.Layout!.Groups[0].Summary.Median);
        chart.ItemsSource = new[] { new Group("b", new Payload(new SamplesCollection([7, 8]))) };
        Assert.Equal(0, second.Observers); Assert.Equal(0, payload.Observers); host.Drain();
        host.Detach(); Assert.Equal(0, second.Observers); host.Attach(); Assert.Single(chart.GetMarks());
    }

    [AvaloniaFact]
    public void Missing_groups_keep_category_spacing_and_failed_inputs_recover_without_stale_geometry()
    {
        var chart = new ViolinPlot { ItemsSource = new[] { new DistributionSamples("empty", "", []), new DistributionSamples("a", "A", new[] { 1d, 2, 3 }) } };
        using var host = new Host(chart); var mark = Assert.Single(chart.GetMarks()); Assert.Equal(1, mark.Members[0].SourceIndex);
        Assert.True(mark.Position.X > chart.PlotArea.Center.X); chart.Minimum = 9; chart.Maximum = 1;
        Assert.Empty(chart.GetMarks()); Assert.IsType<ArgumentException>(chart.DataError); Assert.Null(chart.Layout);
        chart.Minimum = 0; chart.Maximum = 4; Assert.Single(chart.GetMarks()); Assert.Null(chart.DataError);
        chart.ItemsSource = new[] { new DistributionSamples("no", "", new[] { double.NaN }) }; Assert.Empty(chart.GetMarks()); Assert.Null(chart.DataError);
    }

    [AvaloniaFact]
    public void Violin_aliases_and_small_sample_box_rule_are_independent_from_median_visibility()
    {
        var chart = new ViolinPlot { IdPath = null, ItemsSource = new[] { new { Key = "A", Data = new[] { 1d, 2, 3, 4 } } }, ValuesPath = "Data", CategoryPath = "Key", ShowLabels = false, ShowValues = false, ShowGrid = false };
        using var host = new Host(chart); Assert.Equal("Data", chart.SamplesPath); Assert.Equal("Key", chart.LabelPath); Assert.Single(chart.GetMarks());
        var small = GeometryCount(Record(chart)); chart.ShowBoxPlot = false; Assert.Equal(small, GeometryCount(Record(chart)));
        chart.ShowMedian = false; Assert.True(GeometryCount(Record(chart)) < small);
        chart.ItemsSource = new[] { new { Key = "B", Data = new[] { 1d, 2, 3, 4, 5 } } }; chart.ShowBoxPlot = true;
        var box = GeometryCount(Record(chart)); chart.ShowBoxPlot = false; Assert.True(GeometryCount(Record(chart)) < box);
    }

    [AvaloniaFact]
    public void Precomputed_ridge_uses_original_DTOs_shared_height_and_nested_curve_updates()
    {
        var first = new ObservableCollection<ChartPoint> { new(0, 0), new(1, 4), new(2, 0) };
        var second = new ObservableCollection<ChartPoint> { new(0, 0), new(1, 2), new(2, 0) };
        var a = new { Id = "a", Label = "A", Points = first }; var b = new { Id = "b", Label = "B", Points = second };
        var chart = new RidgelineChart { ItemsSource = new[] { a, b }, ProfilePath = "Points", ShowLabels = false };
        using var host = new Host(chart); Assert.Equal(.5, chart.Overlap); Assert.Equal(2, chart.GetMarks().Count); Assert.Null(chart.Layout);
        var marks = chart.GetMarks(); Assert.Equal(marks[0].Bounds.Height / 2, marks[1].Bounds.Height, 7);
        Assert.Same(a, chart.HitTest(marks[0].Position)!.Members[0].Item);
        var metadata = Assert.IsType<ChartDistributionCurveProfile>(marks[1].Members[0].Value); Assert.Same(b, metadata.SourceItem);
        first[1] = new(1, 8); host.Drain(); Assert.Equal(8, chart.CurveLayout!.MaximumDensity);
        chart.Overlap = 0; Assert.Equal(2, chart.GetMarks().Count); Assert.Equal(2, chart.CurveLayout.Groups.Count);
        chart.Overlap = 1; Assert.Equal(2, chart.GetMarks().Count);
        second[1] = new(1, -1); host.Drain(); Assert.Empty(chart.GetMarks()); Assert.IsType<ArgumentException>(chart.DataError);
        second[1] = new(1, 1); host.Drain(); Assert.Equal(2, chart.GetMarks().Count); Assert.Null(chart.DataError);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Distribution_families_share_keyboard_selection_and_export_failure_cancellation_contracts(bool ridge)
    {
        DistributionProfileChartBase chart = ridge ? new RidgelineChart() : new ViolinPlot(); chart.ItemsSource = new[] { new DistributionSamples("a", "A", new[] { 1d, 2, 3 }) };
        using var host = new Host(chart); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Assert.Equal(0, chart.SelectedIndex);
        using var stream = new MemoryStream(); Assert.False(chart.TryExportPng(stream, new ChartExportOptions { Dpi = 0 })); Assert.Equal(0, stream.Length);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportPngAsync(stream, cancellationToken: cancel.Token));
    }

    private static DrawingGroup Record(EngineeringChartBase chart) { var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context); return group; }
    private static int GeometryCount(DrawingGroup group) => group.Children.Sum(child => child is DrawingGroup nested ? GeometryCount(nested) : child is GeometryDrawing ? 1 : 0);
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly EngineeringChartBase _chart;
        public Host(EngineeringChartBase chart) { _chart = chart; _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Attach() { _window.Content = _chart; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class EndlessSamples(Action? action = null) : IReadOnlyList<double>
    {
        public int Count => 0; public double this[int index] => double.NaN; public int Enumerated { get; private set; } public bool Disposed { get; private set; }
        public IEnumerator<double> GetEnumerator() { try { while (true) { Enumerated++; action?.Invoke(); yield return double.NaN; } } finally { Disposed = true; } }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class SamplesCollection(IEnumerable<double> values) : IEnumerable<double>, INotifyCollectionChanged
    {
        private readonly List<double> _values = values.ToList(); private NotifyCollectionChangedEventHandler? _changed;
        public int Observers { get; private set; }
        public event NotifyCollectionChangedEventHandler? CollectionChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
        public void Add(double value) { _values.Add(value); _changed?.Invoke(this, new(NotifyCollectionChangedAction.Add, value)); }
        public IEnumerator<double> GetEnumerator() => _values.GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class Payload(SamplesCollection samples) : INotifyPropertyChanged
    {
        private SamplesCollection _samples = samples; private PropertyChangedEventHandler? _changed; public int Observers { get; private set; }
        public SamplesCollection Samples { get => _samples; set { _samples = value; _changed?.Invoke(this, new(nameof(Samples))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
    private sealed record Group(string Id, Payload Payload) { public string Label => Id; }
}
