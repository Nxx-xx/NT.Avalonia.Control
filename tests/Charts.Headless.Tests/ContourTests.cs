using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ContourTests
{
    [Fact]
    public void Affine_contours_are_exact_and_filled_bands_partition_the_whole_cell()
    {
        var result = ChartContours.Build(Cell(0, 1, 2, 1), new[] { 1d }, TestContext.Current.CancellationToken);
        Assert.Equal(0, result.SaddleCellCount); Assert.Equal(2, result.Lines.Count);
        Assert.All(result.Lines, line => { Equal(1, line.Start.X + line.Start.Y); Equal(1, line.End.X + line.End.Y); Assert.Equal(4, line.SourceIndices.Count); });
        Equal(.5, result.Bands.Where(b => b.BandIndex == 0).Sum(b => Area(b.Vertices)));
        Equal(.5, result.Bands.Where(b => b.BandIndex == 1).Sum(b => Area(b.Vertices)));
        Assert.All(result.Bands, band => Assert.All(band.Vertices, point => Assert.InRange(point.X + point.Y, band.BandIndex == 0 ? 0 : 1 - 1e-12, band.BandIndex == 0 ? 1 + 1e-12 : 2)));
    }
    [Fact]
    public void Automatic_levels_are_even_interior_levels_and_constant_fields_collapse()
    {
        Assert.Equal(new[] { .5, 1, 1.5 }, ChartContours.Build(Cell(0, 1, 2, 1), 3, TestContext.Current.CancellationToken).Levels);
        Assert.Equal(new[] { 7d }, ChartContours.Build(Cell(7, 7, 7, 7), 8, TestContext.Current.CancellationToken).Levels);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartContours.Build(Cell(0, 1, 2, 1), 0, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void Asymmetric_saddle_connectivity_switches_at_the_bilinear_stationary_value()
    {
        // f=4-4x-4y+5xy has saddle (.8,.8) and f_s=.8.
        var below = ChartContours.Build(Cell(4, 0, 1, 0), new[] { .5 }, TestContext.Current.CancellationToken);
        var above = ChartContours.Build(Cell(4, 0, 1, 0), new[] { .9 }, TestContext.Current.CancellationToken);
        Assert.Equal(1, below.SaddleCellCount); Assert.Equal(1, above.SaddleCellCount);
        Assert.Equal(new[] { "BL", "RT" }, BoundaryConnections(above.Lines));
        Assert.Equal(new[] { "BR", "LT" }, BoundaryConnections(below.Lines));
        Equal(1, below.Bands.Sum(b => Area(b.Vertices))); Equal(1, above.Bands.Sum(b => Area(b.Vertices)));
    }
    [Fact]
    public void Exact_saddle_tie_has_four_finite_nonzero_rays_and_consistent_fill()
    {
        var result = ChartContours.Build(Cell(1, -1, 1, -1), new[] { 0d }, TestContext.Current.CancellationToken);
        Assert.Equal(1, result.SaddleCellCount); Assert.Equal(4, result.Lines.Count);
        Assert.All(result.Lines, line => { Assert.True(Near(line.Start, new(.5, .5)) || Near(line.End, new(.5, .5))); Assert.NotEqual(line.Start, line.End); });
        Equal(.5, result.Bands.Where(b => b.BandIndex == 0).Sum(b => Area(b.Vertices)));
        Equal(.5, result.Bands.Where(b => b.BandIndex == 1).Sum(b => Area(b.Vertices)));
    }
    [Theory]
    [InlineData(0, 1)] [InlineData(1, 2)] [InlineData(-1, 0)]
    public void Constant_plateau_uses_upper_band_for_equal_threshold_without_fabricated_isolines(double value, int expectedBand)
    {
        var result = ChartContours.Build(Cell(value, value, value, value), new[] { 0d, 1d }, TestContext.Current.CancellationToken);
        Assert.Empty(result.Lines); Assert.All(result.Bands, band => Assert.Equal(expectedBand, band.BandIndex)); Equal(1, result.Bands.Sum(b => Area(b.Vertices)));
    }
    [Fact]
    public void Equal_threshold_edge_and_vertex_do_not_produce_zero_length_or_duplicate_internal_segments()
    {
        var result = ChartContours.Build(Cell(0, 1, 0, -1), new[] { 0d }, TestContext.Current.CancellationToken);
        Assert.NotEmpty(result.Lines); Assert.All(result.Lines, line => Assert.NotEqual(line.Start, line.End));
        Assert.Equal(result.Lines.Count, result.Lines.Select(line => (line.Start, line.End)).Distinct().Count());
        Equal(1, result.Bands.Sum(b => Area(b.Vertices)));
    }
    [Fact]
    public void Threshold_equal_shared_ridge_edge_is_emitted_once_with_both_cells_original_members()
    {
        var input = Enumerable.Range(0, 2).SelectMany(y => Enumerable.Range(0, 3).Select(x => new ScalarGridPoint($"{x}/{y}", x, y, -Math.Abs(x - 1)))).ToArray();
        var result = ChartContours.Build(input, new[] { 0d }, TestContext.Current.CancellationToken); var line = Assert.Single(result.Lines);
        Assert.Equal(new[] { 0, 1 }, line.CellIndices); Assert.Equal(6, line.SourceIndices.Count); Equal(1, line.Start.X); Equal(1, line.End.X); Equal(2, result.Bands.Sum(b => Area(b.Vertices)));
    }
    [Fact]
    public void Every_interior_isoline_is_an_edge_of_both_adjacent_filled_bands()
    {
        var result = ChartContours.Build(Cell(1, -1, 1, -1), new[] { -.5, .5 }, TestContext.Current.CancellationToken);
        bool Edge(ContourBand band, ContourLine line)
        {
            for (var i = 0; i < band.Vertices.Count; i++)
            { var a = band.Vertices[i]; var b = band.Vertices[(i + 1) % band.Vertices.Count]; if (Near(a, line.Start) && Near(b, line.End) || Near(a, line.End) && Near(b, line.Start)) return true; }
            return false;
        }
        foreach (var line in result.Lines)
        {
            var level = result.Levels.ToList().IndexOf(line.Level);
            Assert.Contains(result.Bands, band => band.BandIndex == level && Edge(band, line)); Assert.Contains(result.Bands, band => band.BandIndex == level + 1 && Edge(band, line));
        }
    }
    [Fact]
    public void Arbitrary_source_order_is_sorted_numerically_without_losing_original_indices_or_ids()
    {
        var original = Cell(0, 1, 2, 1).Reverse().ToList(); var levels = new List<double> { .5, 1.5 };
        var result = ChartContours.Build(original, levels, TestContext.Current.CancellationToken); var ids = result.Grid.Select(p => p.Id).ToArray();
        Assert.Equal(new[] { 3, 2, 0, 1 }, result.SourceIndices);
        for (var i = 0; i < result.Grid.Count; i++) Assert.Same(original[result.SourceIndices[i]], result.Grid[i]);
        Assert.All(result.Bands, band => Assert.Equal(new[] { 3, 2, 1, 0 }, band.SourceIndices));
        original.Clear(); levels[0] = -100; Assert.Equal(.5, result.Levels[0]); Assert.Equal(ids, result.Grid.Select(p => p.Id));
        Assert.Throws<NotSupportedException>(() => ((IList<ScalarGridPoint>)result.Grid).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ChartPoint>)result.Bands[0].Vertices).Clear());
    }
    [Fact]
    public void Missing_values_mask_adjacent_cells_while_missing_or_duplicate_coordinates_are_rejected()
    {
        var grid = Enumerable.Range(0, 3).SelectMany(y => Enumerable.Range(0, 3).Select(x => new ScalarGridPoint($"{x}/{y}", x, y, x == 1 && y == 1 ? double.NaN : x + y))).ToArray();
        var masked = ChartContours.Build(grid, new[] { 1d }, TestContext.Current.CancellationToken); Assert.Equal(4, masked.MaskedCellCount); Assert.Empty(masked.Lines); Assert.Empty(masked.Bands); Assert.Equal(9, masked.Grid.Count);
        Assert.Throws<ArgumentException>(() => ChartContours.Build(Cell(0, 1, 2, 1).Take(3), new[] { 1d }, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartContours.Build(Cell(0, 1, 2, 1).Append(new("duplicate", 0, 0, 9)), new[] { 1d }, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartContours.Build(Cell(0, 1, double.PositiveInfinity, 1), new[] { 1d }, TestContext.Current.CancellationToken));
        Assert.Empty(ChartContours.Build(Array.Empty<ScalarGridPoint>(), new[] { 1d }, TestContext.Current.CancellationToken).Grid);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Invalid_levels_are_explicit_errors(int kind)
    {
        double[] levels = kind switch { 0 => [], 1 => [1, 1], 2 => [2, 1], _ => [double.NaN] };
        Assert.ThrowsAny<ArgumentException>(() => ChartContours.Build(Cell(0, 1, 2, 1), levels, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void Extreme_finite_coordinates_and_values_stay_finite()
    {
        ScalarGridPoint[] input = [new("a", -1e308, -1e308, -1e308), new("b", 1e308, -1e308, 1e308), new("c", 1e308, 1e308, 1e308), new("d", -1e308, 1e308, -1e308)];
        var result = ChartContours.Build(input, new[] { 0d }, TestContext.Current.CancellationToken); Assert.NotEmpty(result.Lines);
        Assert.All(result.Lines, line => { Assert.True(double.IsFinite(line.Start.X) && double.IsFinite(line.Start.Y)); Assert.True(double.IsFinite(line.End.X) && double.IsFinite(line.End.Y)); Equal(0, line.Start.X); Equal(0, line.End.X); });
        Assert.All(result.Bands, band => Assert.All(band.Vertices, point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y))));
    }
    [Fact]
    public void Small_variation_at_large_offset_and_subnormal_values_do_not_collapse_or_divide_by_zero()
    {
        var low = 1e308; var middle = Math.BitIncrement(low); var high = Math.BitIncrement(middle);
        foreach (var input in new[] { Cell(low, high, high, low), Cell(0, 2 * double.Epsilon, 2 * double.Epsilon, 0) })
        {
            var level = input[0].Value == 0 ? double.Epsilon : middle; var result = ChartContours.Build(input, new[] { level }, TestContext.Current.CancellationToken);
            Assert.NotEmpty(result.Lines); Assert.All(result.Lines, line => { Equal(.5, line.Start.X); Equal(.5, line.End.X); Assert.True(double.IsFinite(line.Start.Y) && double.IsFinite(line.End.Y)); });
        }
    }
    [Fact]
    public void Cancellation_and_work_budgets_reject_before_unbounded_processing()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); Assert.Throws<OperationCanceledException>(() => ChartContours.Build(Cell(0, 1, 2, 1), new[] { 1d }, cancelled.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartContours.Build(Cell(0, 1, 2, 1), Enumerable.Range(0, ChartContours.MaximumLevelCount + 1).Select(x => (double)x), TestContext.Current.CancellationToken));
        var large = Enumerable.Range(0, 256).SelectMany(y => Enumerable.Range(0, 256).Select(x => new ScalarGridPoint(null, x, y, x + y)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartContours.Build(large, Enumerable.Range(0, 32).Select(x => (double)x), TestContext.Current.CancellationToken));
        using var mid = new CancellationTokenSource();
        IEnumerable<ScalarGridPoint> Interrupted() { yield return new(null, 0, 0, 0); mid.Cancel(); yield return new(null, 1, 0, 1); }
        Assert.Throws<OperationCanceledException>(() => ChartContours.Build(Interrupted(), new[] { 0d }, mid.Token));
    }
    [AvaloniaFact]
    public async Task Control_hits_return_raw_cell_members_and_navigation_rendering_export_cancellation_are_supported()
    {
        var source = Cell(0, 1, 2, 1); var chart = new ContourPlot { ItemsSource = source, Levels = new[] { 1d } }; using var host = new Host(chart);
        Assert.Null(chart.DataError); var initial = chart.Contours; Assert.NotNull(initial); Assert.NotEmpty(chart.GetMarks());
        var hit = chart.HitTest(chart.Project(.25, .25)); Assert.NotNull(hit); Assert.Equal(4, hit!.Members.Count);
        Assert.All(hit.Members, member => Assert.Same(source[member.SourceIndex], member.Item));
        var lineHit = chart.HitTest(chart.Project(.25, .75)); Assert.NotNull(lineHit); Equal(1, lineHit!.Mark.Value);
        chart.SelectedIndex = lineHit.Index; var selectedItems = chart.SelectedPoint!.Members.Select(x => x.Item).ToArray(); var oldPosition = chart.SelectedPoint.Position;
        chart.Zoom = 1.2; chart.PanX = .03; chart.PanY = -.02; Assert.Same(initial, chart.Contours); Assert.Equal(selectedItems, chart.SelectedPoint!.Members.Select(x => x.Item)); Assert.NotEqual(oldPosition, chart.SelectedPoint.Position);
        var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context); Assert.NotEmpty(drawing.Children);
        chart.ShowFilledBands = false; Assert.All(chart.GetMarks(), mark => Assert.Equal(1, mark.Value));
        chart.ShowContourLines = false; Assert.Empty(chart.GetMarks()); chart.ShowFilledBands = true; Assert.NotEmpty(chart.GetMarks());
        using var stream = new MemoryStream(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportPngAsync(stream, cancellationToken: cancellation.Token)); Assert.Equal(0, stream.Length);
    }
    [AvaloniaFact]
    public void Automatic_level_count_and_official_fill_line_aliases_have_explicit_level_precedence()
    {
        var chart = new ContourPlot { ItemsSource = Cell(0, 1, 2, 1), ContourLevels = 3 }; using var host = new Host(chart);
        Assert.Equal(new[] { .5, 1, 1.5 }, chart.Contours!.Levels);
        chart.Levels = new[] { 1d }; chart.ContourLevels = 4; Assert.Single(chart.Contours!.Levels);
        chart.ShowFill = false; Assert.False(chart.ShowFilledBands); chart.ShowLines = false; Assert.Empty(chart.GetMarks());
        chart.ShowLines = true; Assert.NotEmpty(chart.GetMarks()); chart.Levels = null; Assert.Equal(4, chart.Contours!.Levels.Count);
    }
    [AvaloniaFact]
    public void Custom_paths_notify_mutation_reorder_masking_level_changes_and_source_detach()
    {
        var a = new Row("a", 0, 0, 0); var b = new Row("b", 1, 0, 1); var c = new Row("c", 1, 1, 2); var d = new Row("d", 0, 1, 1);
        var source = new ObservableCollection<Row> { a, b, c, d }; var levels = new ObservableCollection<double> { 1 };
        var chart = new ContourPlot { ItemsSource = source, XPath = "East", YPath = "North", ValuePath = "Scalar", IdPath = "Key", Levels = levels }; using var host = new Host(chart);
        Assert.Null(chart.DataError); Assert.True(a.Subscribers > 0); var original = chart.Contours; c.Scalar = 3; Assert.NotSame(original, chart.Contours); Assert.Equal(3, chart.Contours!.Grid.Single(p => Equals(p.Id, "c")).Value);
        source.Move(0, 3); var mark = chart.GetMarks()[0]; Assert.All(mark.Members, member => Assert.Same(source[member.SourceIndex], member.Item));
        a.Scalar = double.NaN; Assert.Equal(1, chart.Contours!.MaskedCellCount); Assert.Empty(chart.GetMarks()); Assert.Null(chart.DataError);
        a.Scalar = 0; levels[0] = .5; Assert.Equal(.5, chart.Contours!.Levels[0]);
        // Keep the custom mapping valid so this failure isolates the incomplete grid contract.
        chart.ItemsSource = new[] { new Row("bad", 0, 0, 1) }; Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); Assert.Equal(0, a.Subscribers);
        chart.ItemsSource = null; chart.XPath = "X"; chart.YPath = "Y"; chart.ValuePath = "Value"; chart.IdPath = "Id"; chart.ItemsSource = Cell(0, 1, 2, 1); Assert.NotEmpty(chart.GetMarks()); Assert.Null(chart.DataError);
        chart.ItemsSource = null; chart.XPath = "East"; chart.YPath = "North"; chart.ValuePath = "Scalar"; chart.IdPath = "Key"; chart.ItemsSource = source; Assert.NotEmpty(chart.GetMarks());
        host.Detach(); Assert.Equal(0, a.Subscribers);
        c.Scalar = 9; host.Attach(chart); Assert.Equal(9, chart.Contours!.Grid.Single(point => Equals(point.Id, "c")).Value);
    }
    private static ScalarGridPoint[] Cell(double a, double b, double c, double d) => [new("a", 0, 0, a), new("b", 1, 0, b), new("c", 1, 1, c), new("d", 0, 1, d)];
    private static bool Near(ChartPoint a, ChartPoint b) => Math.Abs(a.X - b.X) < 1e-10 && Math.Abs(a.Y - b.Y) < 1e-10;
    private static void Equal(double expected, double actual) => Assert.InRange(actual, expected - 1e-10, expected + 1e-10);
    private static double Area(IReadOnlyList<ChartPoint> polygon) { var sum = 0d; for (var i = 0; i < polygon.Count; i++) { var next = polygon[(i + 1) % polygon.Count]; sum += polygon[i].X * next.Y - next.X * polygon[i].Y; } return Math.Abs(sum) / 2; }
    private static string[] BoundaryConnections(IReadOnlyList<ContourLine> lines)
    {
        var nodes = new List<ChartPoint>(); var links = new List<(int A, int B)>();
        int Node(ChartPoint point) { var index = nodes.FindIndex(x => Near(x, point)); if (index >= 0) return index; nodes.Add(point); return nodes.Count - 1; }
        foreach (var line in lines) links.Add((Node(line.Start), Node(line.End)));
        var seen = new HashSet<int>(); var result = new List<string>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (!seen.Add(i)) continue; var queue = new Queue<int>(); queue.Enqueue(i); var boundaries = new HashSet<char>();
            while (queue.TryDequeue(out var n))
            {
                var p = nodes[n]; if (Math.Abs(p.Y) < 1e-10) boundaries.Add('B'); if (Math.Abs(p.X - 1) < 1e-10) boundaries.Add('R'); if (Math.Abs(p.Y - 1) < 1e-10) boundaries.Add('T'); if (Math.Abs(p.X) < 1e-10) boundaries.Add('L');
                foreach (var link in links) { var neighbor = link.A == n ? link.B : link.B == n ? link.A : -1; if (neighbor >= 0 && seen.Add(neighbor)) queue.Enqueue(neighbor); }
            }
            result.Add(new(boundaries.Order().ToArray()));
        }
        return result.Order().ToArray();
    }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; public Host(ContourPlot chart) { _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = chart.GetMarks(); }
        public void Detach() { _window.Content = null; Dispatcher.UIThread.RunJobs(); }
        public void Attach(ContourPlot chart) { _window.Content = chart; _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = chart.GetMarks(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class Row(string key, double east, double north, double scalar) : INotifyPropertyChanged
    {
        private double _scalar = scalar; private PropertyChangedEventHandler? _changed;
        public string Key { get; } = key; public double East { get; } = east; public double North { get; } = north;
        public double Scalar { get => _scalar; set { _scalar = value; _changed?.Invoke(this, new(nameof(Scalar))); } }
        public int Subscribers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { Subscribers++; _changed += value; } remove { Subscribers--; _changed -= value; } }
    }
}
