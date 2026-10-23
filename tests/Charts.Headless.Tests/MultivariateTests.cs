using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class MultivariateTests
{
    [Fact]
    public void Mosaic_aggregates_duplicate_pairs_and_keeps_original_indices_and_contingency_proportions()
    {
        MosaicObservation[] input = [new("a", "North", "One", 2), new("b", "South", "Two", 6), new("c", "North", "Two", 3), new("d", "North", "One", 1)];
        var result = ChartMultivariate.Mosaic(input, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "North", "South" }, result.Columns); Assert.Equal(new[] { "One", "Two" }, result.Rows); Assert.Equal(4, result.SourceCount);
        var first = result.Cells[0]; Assert.Equal(new[] { 0, 3 }, first.SourceIndices); Assert.Equal(3, first.Value);
        Near(.25, first.Share); Near(.5, first.Width); Near(.5, first.Height); Near(0, first.X); Near(0, first.Y);
        var second = result.Cells[1]; Assert.Equal(new[] { 2 }, second.SourceIndices); Near(.5, second.Y); Near(.5, second.Width); Near(.5, second.Height);
        var last = result.Cells[2]; Assert.Equal(new[] { 1 }, last.SourceIndices); Near(.5, last.X); Near(.5, last.Width); Near(1, last.Height);
        Near(1, result.Cells.Sum(cell => cell.Width * cell.Height));
        Assert.All(result.Cells, cell => Near(cell.Share, cell.Width * cell.Height));
    }

    [Fact]
    public void Mosaic_category_equality_preserves_types_and_zero_groups_remain_explicit()
    {
        var result = ChartMultivariate.Mosaic([new("int", 1, "R", 0), new("string", "1", "R", 0), new("int-second", 1, "R", 0)], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Columns.Count); Assert.IsType<int>(result.Columns[0]); Assert.IsType<string>(result.Columns[1]);
        Assert.Equal(2, result.Cells.Count); Assert.Equal(new[] { 0, 2 }, result.Cells[0].SourceIndices);
        Assert.All(result.Cells, cell => { Assert.Equal(0, cell.Share); Assert.Equal(0, cell.Width); Assert.Equal(0, cell.Height); });
        Assert.Empty(ChartMultivariate.Mosaic([], cancellationToken: TestContext.Current.CancellationToken).Cells);
    }

    [Fact]
    public void Mosaic_scaled_normalization_handles_large_weights_and_rejects_unrepresentable_cell_totals()
    {
        var result = ChartMultivariate.Mosaic([new("a", "A", "R", double.MaxValue), new("b", "B", "R", double.MaxValue)], cancellationToken: TestContext.Current.CancellationToken);
        Assert.All(result.Cells, cell => { Near(.5, cell.Share); Near(.5, cell.Width); Near(1, cell.Height); Assert.Equal(double.MaxValue, cell.Value); });
        Assert.Throws<OverflowException>(() => ChartMultivariate.Mosaic([new("a", "A", "R", double.MaxValue), new("b", "A", "R", double.MaxValue)], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartMultivariate.Mosaic([new("bad", "A", "R", -1)], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartMultivariate.Mosaic([new("bad", "A", "R", double.NaN)], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartMultivariate.Mosaic([new("bad", null!, "R", 1)], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Mosaic_owns_result_members_and_accepts_twenty_thousand_unique_columns_with_bounded_source_enumeration()
    {
        var rows = Enumerable.Range(0, ChartMultivariate.MaximumCount).Select(i => new MosaicObservation(i, i, "R", 1)).ToArray();
        var result = ChartMultivariate.Mosaic(rows, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(20_000, result.Cells.Count); Assert.Equal(20_000, result.Columns.Count);
        Assert.Equal(19_999, Assert.Single(result.Cells[^1].SourceIndices)); Near(1d / 20_000, result.Cells[^1].Width);
        rows[0] = new("replacement", "Other", "Other", 99); Assert.Equal(0, result.Cells[0].Column); Assert.Equal(1, result.Cells[0].Value);
        var enumerated = 0; var disposed = false;
        IEnumerable<MosaicObservation> Endless() { try { while (true) { enumerated++; yield return new(enumerated, "A", "R", 0); } } finally { disposed = true; } }
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMultivariate.Mosaic(Endless(), cancellationToken: TestContext.Current.CancellationToken)); Assert.Equal(20_001, enumerated); Assert.True(disposed);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); Assert.Throws<OperationCanceledException>(() => ChartMultivariate.Mosaic([], cancelled.Token));
    }

    [Fact]
    public void Parallel_scales_axes_independently_reverses_clamps_and_centers_constant_values()
    {
        ParallelDimension[] dimensions = [new("a", "A"), new("b", "B", 0, 10, true), new("c", "C")];
        ParallelRecord[] source = [new("one", new[] { -2d, 20, 7 }), new("two", new[] { 2d, 4, 7 })];
        var result = ChartMultivariate.Parallel(source, dimensions, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(3, result.Dimensions.Count);
        Assert.Equal(-2, result.Dimensions[0].Minimum); Assert.Equal(2, result.Dimensions[0].Maximum);
        Assert.Equal(7, result.Dimensions[2].Minimum); Assert.Equal(7, result.Dimensions[2].Maximum);
        var a = result.Records[0]; Assert.Equal(0, a.SourceIndex); Assert.Equal("one", a.Source.Id);
        Assert.Equal(new ChartPoint(0, 1), a.Coordinates[0].Position); Assert.Equal(new ChartPoint(.5, 1), a.Coordinates[1].Position); Assert.Equal(new ChartPoint(1, .5), a.Coordinates[2].Position);
        Near(.4, result.Records[1].Coordinates[1].Position.Y); Assert.Equal(20, a.Coordinates[1].Value);
    }

    [Fact]
    public void Parallel_missing_values_keep_dimension_indices_and_owned_snapshots_and_empty_domains_are_explicit()
    {
        var values = new[] { 1d, double.NaN, 3 }; var axes = new[] { new ParallelDimension("a", "A"), new ParallelDimension("b", "B"), new ParallelDimension("c", "C") };
        var result = ChartMultivariate.Parallel([new("gapped", values)], axes, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, result.OmittedValues); Assert.Equal(new[] { 0, 2 }, result.Records[0].Coordinates.Select(point => point.DimensionIndex));
        Assert.Equal(0, result.Dimensions[1].Minimum); Assert.Equal(1, result.Dimensions[1].Maximum);
        values[0] = 99; axes[0] = new("new", "New"); Assert.Equal(1, result.Records[0].Source.Values[0]); Assert.Equal("a", result.Dimensions[0].Id);
        Assert.True(double.IsNaN(result.Records[0].Source.Values[1])); Assert.Equal(new ChartPoint(0, .5), result.Records[0].Coordinates[0].Position);
        var empty = ChartMultivariate.Parallel([], axes, cancellationToken: TestContext.Current.CancellationToken); Assert.Empty(empty.Records); Assert.All(empty.Dimensions, axis => { Assert.Equal(0, axis.Minimum); Assert.Equal(1, axis.Maximum); });
        var extreme = ChartMultivariate.Parallel([new("a", new[] { -double.MaxValue, 1d }), new("b", new[] { double.MaxValue, 3d })], axes.Take(2), cancellationToken: TestContext.Current.CancellationToken);
        Assert.All(extreme.Records.SelectMany(row => row.Coordinates), point => { Assert.True(double.IsFinite(point.Position.X)); Assert.True(double.IsFinite(point.Position.Y)); });
    }

    [Fact]
    public void Parallel_rejects_invalid_axes_records_and_budgets_and_observes_cancellation()
    {
        ParallelDimension[] valid = [new("a", "A"), new("b", "B")];
        Assert.Throws<ArgumentException>(() => ChartMultivariate.Parallel([], valid.Take(1), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartMultivariate.Parallel([], [valid[0], valid[0]], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartMultivariate.Parallel([], [new("a", "A", 5, 1), valid[1]], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartMultivariate.Parallel([new("bad", new[] { 1d })], valid, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartMultivariate.Parallel([new("bad", new[] { 1d, double.PositiveInfinity })], valid, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMultivariate.Parallel([], Enumerable.Range(0, 65).Select(i => new ParallelDimension(i, "Axis")), cancellationToken: TestContext.Current.CancellationToken));
        var manyAxes = Enumerable.Range(0, 64).Select(i => new ParallelDimension(i, "Axis")).ToArray();
        var dense = Enumerable.Range(0, 3126).Select(i => new ParallelRecord(i, new double[64]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMultivariate.Parallel(dense, manyAxes, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMultivariate.Parallel(Enumerable.Range(0, 20_001).Select(i => new ParallelRecord(i, new[] { 0d, 1 })), valid, cancellationToken: TestContext.Current.CancellationToken));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); Assert.Throws<OperationCanceledException>(() => ChartMultivariate.Parallel([], valid, cancelled.Token));
        using var during = new CancellationTokenSource();
        IEnumerable<ParallelRecord> Cancelling() { yield return new("a", new[] { 1d, 2 }); during.Cancel(); yield return new("b", new[] { 3d, 4 }); }
        Assert.Throws<OperationCanceledException>(() => ChartMultivariate.Parallel(Cancelling(), valid, during.Token));
    }

    [AvaloniaFact]
    public void Mosaic_DTO_marks_preserve_all_real_duplicate_members_and_selection_follows_source_reorder()
    {
        var a = new MosaicRow("a", "A", "R", 2); var b = new MosaicRow("b", "B", "R", 6); var c = new MosaicRow("c", "A", "R", 1);
        var source = new ObservableCollection<MosaicRow> { a, b, c }; var chart = new MosaicChart { ItemsSource = source, ShowLabels = false };
        using var host = new Host(chart); Assert.Equal(2, chart.GetMarks().Count);
        var mark = chart.GetMarks()[0]; Assert.Equal(new object[] { a, c }, mark.Members.Select(member => member.Item)); Assert.Equal(new[] { 0, 2 }, mark.Members.Select(member => member.SourceIndex));
        var hit = chart.HitTest(mark.Position)!; Assert.Same(a, hit.Members[0].Item); Assert.Same(c, hit.Members[1].Item); Assert.Equal(3, hit.Mark.Value);
        chart.SelectedIndex = 0; source.Move(0, 2); host.Drain(); Assert.Contains(chart.SelectedPoint!.Members, member => ReferenceEquals(member.Item, a));
        a.Value = 8; host.Drain(); Assert.Equal(9, chart.SelectedPoint!.Mark.Value);
        Assert.NotEmpty(Record(chart).Children); Assert.All(chart.GetMarks().SelectMany(item => item.Members), member => Assert.Contains(source, item => ReferenceEquals(item, member.Item)));
        chart.ItemsSource = new[] { new MosaicRow("new", "C", "S", 0) }; Assert.Equal(0, a.Observers); host.Drain(); Assert.Empty(chart.GetMarks()); Assert.Null(chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Parallel_Axes_VM_binding_INPC_INCC_and_nested_value_changes_rebuild_without_refresh()
    {
        var values = new Values(2, 8, 4); var row = new Row("a", values); var axes = new TrackedAxes { Axis("x", "Data.A"), Axis("y", "Data.B") }; var vm = new AxesVm(axes);
        var chart = new ParallelCoordinatesChart { ItemsSource = new[] { row }, ShowLabels = false, ShowValues = false };
        using var binding = chart.Bind(ParallelCoordinatesChart.AxesProperty, new Binding(nameof(AxesVm.Axes)) { Source = vm });
        using var host = new Host(chart); Assert.Same(axes, chart.Axes); Assert.Single(chart.GetMarks()); Assert.True(axes.Observers > 0); Assert.True(values.Observers > 0);
        Assert.Equal(2, chart.Layout!.Records[0].Coordinates[0].Value); values.A = 6; host.Drain(); Assert.Equal(6, chart.Layout!.Records[0].Coordinates[0].Value);
        axes[0].IsReversed = true; host.Drain(); Assert.True(chart.Layout!.Dimensions[0].IsReversed);
        axes.Add(Axis("z", "Data.C")); host.Drain(); Assert.Equal(3, chart.Layout!.Dimensions.Count); Assert.Equal(4, chart.Layout.Records[0].Coordinates[2].Value);
        axes[0].ValuePath = "Data.B"; host.Drain(); Assert.Equal(8, chart.Layout!.Records[0].Coordinates[0].Value);
        var replacementAxes = new TrackedAxes { Axis("new-x", "Data.A"), Axis("new-y", "Data.C") }; vm.Axes = replacementAxes; host.Drain();
        Assert.Same(replacementAxes, chart.Axes); Assert.Equal(0, axes.Observers); Assert.True(replacementAxes.Observers > 0); Assert.Equal(2, chart.Layout!.Dimensions.Count);
        host.Detach(); Assert.Equal(0, values.Observers); Assert.Equal(0, replacementAxes.Observers);
        values.A = 9; host.Attach(); Assert.Equal(9, chart.Layout!.Records[0].Coordinates[0].Value); Assert.True(values.Observers > 0);
    }

    [AvaloniaFact]
    public void Parallel_real_DTO_selection_follows_reorder_and_releases_removed_and_replaced_nested_owners()
    {
        var first = new Values(1, 9, 3); var second = new Values(8, 2, 4); var a = new Row("a", first); var b = new Row("b", second);
        var rows = new ObservableCollection<Row> { a, b }; var chart = new ParallelCoordinatesChart { ItemsSource = rows, Axes = [Axis("x", "Data.A"), Axis("y", "Data.B")] };
        using var host = new Host(chart); Assert.Equal(2, chart.GetMarks().Count); chart.SelectedIndex = 1;
        rows.Move(1, 0); host.Drain(); Assert.Equal(0, chart.SelectedIndex); Assert.Same(b, chart.SelectedPoint!.Members[0].Item);
        second.B = 7; host.Drain(); Assert.Equal(7, Assert.IsAssignableFrom<IReadOnlyList<double>>(chart.SelectedPoint!.Members[0].Value)[1]);
        rows.Remove(b); host.Drain(); Assert.Null(chart.SelectedPoint); Assert.Equal(0, second.Observers);
        chart.ItemsSource = new[] { new Row("new", new Values(2, 3, 4)) }; Assert.Equal(0, first.Observers); host.Drain();
        Assert.All(chart.GetMarks(), mark => Assert.Same(Assert.Single(Assert.IsType<Row[]>(chart.ItemsSource)), Assert.Single(mark.Members).Item));
    }

    [AvaloniaFact]
    public void Parallel_markers_and_segments_hit_exactly_while_distant_interior_of_the_enclosing_box_misses()
    {
        var row = new Row("a", new Values(0, 10, 0)); var chart = new ParallelCoordinatesChart { ItemsSource = new[] { row }, Axes = [Axis("x", "Data.A"), Axis("y", "Data.B")], ShowLabels = false, ShowValues = false };
        using var host = new Host(chart); var mark = Assert.Single(chart.GetMarks()); var plot = chart.PlotArea;
        Assert.Same(row, chart.HitTest(new Point(plot.Left, plot.Bottom))!.Members[0].Item);
        Assert.Same(row, chart.HitTest(plot.Center)!.Members[0].Item);
        var far = new Point(plot.Center.X, plot.Center.Y + plot.Height * .25); Assert.True(mark.Bounds.Contains(far)); Assert.Null(chart.HitTest(far));
        chart.Zoom = 1.1; chart.PanX = .01; host.Drain(); Assert.Same(row, chart.HitTest(new Point(plot.Center.X + plot.Width * .01, plot.Center.Y))!.Members[0].Item);
    }

    [AvaloniaFact]
    public void Parallel_NaN_gap_has_no_spanning_segment_or_phantom_middle_hit_and_keeps_real_source()
    {
        var row = new ParallelRecord("a", new[] { 0d, double.NaN, 10 }, "Gapped");
        var chart = new ParallelCoordinatesChart { ItemsSource = new[] { row }, Axes = [Axis("a", null), Axis("b", null), Axis("c", null)], ShowLabels = false, ShowValues = false };
        using var host = new Host(chart); var mark = Assert.Single(chart.GetMarks()); Assert.Same(row, mark.Members[0].Item);
        var geometry = Assert.IsType<GeometryGroup>(mark.Geometry); Assert.Equal(2, geometry.Children.OfType<EllipseGeometry>().Count()); Assert.Empty(geometry.Children.OfType<PolylineGeometry>());
        Assert.Null(chart.HitTest(chart.PlotArea.Center)); Assert.Same(row, chart.HitTest(mark.Position)!.Members[0].Item);
        Assert.True(double.IsNaN(Assert.IsAssignableFrom<IReadOnlyList<double>>(mark.Members[0].Value)[1]));
    }

    [AvaloniaFact]
    public void Parallel_typed_observable_values_and_failed_axis_recovery_keep_source_identity()
    {
        var values = new ObservableCollection<double> { 1, 2 }; var row = new ParallelRecord("a", values, "Typed");
        var chart = new ParallelCoordinatesChart { ItemsSource = new[] { row }, Axes = [Axis("a", null), Axis("b", null)] };
        using var host = new Host(chart); Assert.Single(chart.GetMarks()); values[0] = 7; host.Drain(); Assert.Equal(7, chart.Layout!.Records[0].Coordinates[0].Value);
        chart.Axes[0].Minimum = 9; chart.Axes[0].Maximum = 1; host.Drain(); Assert.Empty(chart.GetMarks()); Assert.IsType<ArgumentException>(chart.DataError);
        chart.Axes[0].Minimum = 0; chart.Axes[0].Maximum = 10; host.Drain(); Assert.Single(chart.GetMarks()); Assert.Null(chart.DataError); Assert.Same(row, chart.GetMarks()[0].Members[0].Item);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Assert.Equal(0, chart.SelectedIndex);
        using var stream = new MemoryStream(); Assert.False(chart.TryExportPng(stream, new ChartExportOptions { Dpi = 0 })); Assert.Equal(0, stream.Length);
    }

    [AvaloniaFact]
    public void Parallel_invalid_dimension_frame_keeps_every_typed_values_subscription_until_INCC_repairs_all_rows()
    {
        var firstValues = new ObservableCollection<double> { 1, 2, 3 };
        var secondValues = new ObservableCollection<double> { 7, 8, 9 };
        var first = new ParallelRecord("first", firstValues, "First");
        var second = new ParallelRecord("second", secondValues, "Second");
        var chart = new ParallelCoordinatesChart
        {
            ItemsSource = new[] { first, second },
            Axes = [Axis("a", null), Axis("b", null), Axis("c", null)]
        };
        using var host = new Host(chart); Assert.Equal(2, chart.GetMarks().Count); Assert.Null(chart.DataError);

        chart.Axes.Add(Axis("d", null)); host.Drain();
        Assert.Empty(chart.GetMarks()); Assert.IsType<ArgumentException>(chart.DataError); Assert.Null(chart.Layout);
        firstValues.Add(double.NaN); host.Drain();
        Assert.Empty(chart.GetMarks()); Assert.IsType<ArgumentException>(chart.DataError);
        // The first invalid row must not retire the later row's collection observer.
        secondValues.Add(double.NaN); host.Drain();
        Assert.Equal(2, chart.GetMarks().Count); Assert.Null(chart.DataError); Assert.Equal(4, chart.Layout!.Dimensions.Count);
        Assert.Equal(2, chart.Layout.OmittedValues);
        Assert.Same(first, chart.GetMarks()[0].Members[0].Item); Assert.Same(second, chart.GetMarks()[1].Members[0].Item);

        // Both rows can then be repaired through their own collection notifications in the opposite order.
        firstValues[3] = 4; secondValues[3] = 6; host.Drain(); Assert.Equal(0, chart.Layout!.OmittedValues);
        chart.Axes.RemoveAt(3); host.Drain(); Assert.Empty(chart.GetMarks()); Assert.IsType<ArgumentException>(chart.DataError);
        secondValues.RemoveAt(3); host.Drain(); Assert.Empty(chart.GetMarks()); Assert.IsType<ArgumentException>(chart.DataError);
        firstValues.RemoveAt(3); host.Drain();
        Assert.Equal(2, chart.GetMarks().Count); Assert.Null(chart.DataError); Assert.Equal(3, chart.Layout!.Dimensions.Count);
        Assert.Equal(new[] { 0, 1, 2 }, chart.Layout.Records[1].Coordinates.Select(point => point.DimensionIndex));
        Assert.Same(second, chart.HitTest(chart.GetMarks()[1].Position)!.Members[0].Item);
    }

    private static ParallelAxis Axis(string id, string? path) => new() { Id = id, Header = id, ValuePath = path, Minimum = 0, Maximum = 10 };
    private static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) <= 1e-9 * Math.Max(1, Math.Abs(expected)), $"Expected {expected:R}, got {actual:R}");
    private static DrawingGroup Record(EngineeringChartBase chart) { var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context); return group; }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly EngineeringChartBase _chart;
        public Host(EngineeringChartBase chart) { _chart = chart; _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Attach() { _window.Content = _chart; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private abstract class Observed : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed; public int Observers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
        protected void Changed(string property) => _changed?.Invoke(this, new(property));
    }
    private sealed class MosaicRow(string id, string column, string row, double value) : Observed
    {
        private double _value = value; public string Id { get; } = id; public string Column { get; } = column; public string Row { get; } = row; public string Label => Id;
        public double Value { get => _value; set { _value = value; Changed(nameof(Value)); } }
    }
    private sealed class Values(double a, double b, double c) : Observed
    {
        private double _a = a, _b = b; public double A { get => _a; set { _a = value; Changed(nameof(A)); } }
        public double B { get => _b; set { _b = value; Changed(nameof(B)); } } public double C { get; } = c;
    }
    private sealed record Row(string Id, Values Data) { public string Label => Id; }
    private sealed class AxesVm(ObservableCollection<ParallelAxis> axes) : INotifyPropertyChanged
    {
        private ObservableCollection<ParallelAxis> _axes = axes; public ObservableCollection<ParallelAxis> Axes { get => _axes; set { _axes = value; PropertyChanged?.Invoke(this, new(nameof(Axes))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class TrackedAxes : ObservableCollection<ParallelAxis>
    {
        public int Observers { get; private set; }
        public override event NotifyCollectionChangedEventHandler? CollectionChanged { add { Observers++; base.CollectionChanged += value; } remove { Observers--; base.CollectionChanged -= value; } }
    }
}
