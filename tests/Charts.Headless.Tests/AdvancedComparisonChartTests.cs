using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class AdvancedComparisonChartTests
{
    [AvaloniaFact]
    public void Diverging_uses_one_baseline_scale_and_signed_raw_values_in_both_orientations()
    {
        var values = new[] { new DivergingValue(-6, "loss"), new DivergingValue(3, "small"), new DivergingValue(9, "gain"), new DivergingValue(0, "zero") };
        var chart = new DivergingBarChart { ItemsSource = values, ShowLabels = false, ShowValues = false };
        using var host = new Host(chart); var marks = chart.GetMarks(); Assert.Equal(3, marks.Count);
        Assert.Equal(new Rect(24, 24, 372, 232), chart.PlotArea);
        Near(24 + 372d / 6, marks[0].Bounds.Left); Near(210, marks[0].Bounds.Right);
        Near(210, marks[1].Bounds.Left); Near(272, marks[1].Bounds.Right);
        Assert.Equal(-6, marks[0].Value); Assert.Equal(values[0], Assert.Single(chart.HitTest(marks[0].Position)!.Members).Item);
        Assert.Null(chart.HitTest(new Point(250, 24 + 232 * .875)));
        chart.Baseline = 3; Assert.Equal(3, chart.GetMarks().Count); Assert.Equal(-6, chart.GetMarks()[0].Members[0].Point.Y);
        chart.Orientation = Orientation.Vertical; Assert.Equal(3, chart.GetMarks().Count); Assert.True(chart.GetMarks().All(mark => double.IsFinite(mark.Position.X) && double.IsFinite(mark.Position.Y)));
    }

    [AvaloniaFact]
    public void Diverging_baseline_overflow_is_visible_and_recovers_without_stale_geometry()
    {
        var chart = new DivergingBarChart { ItemsSource = new[] { new DivergingValue(double.MaxValue) }, Baseline = -double.MaxValue };
        using var host = new Host(chart); Assert.Empty(chart.GetMarks()); Assert.IsType<OverflowException>(chart.DataError);
        chart.Baseline = 0; Assert.Single(chart.GetMarks()); Assert.Null(chart.DataError);
    }

    [AvaloniaFact]
    public void Diverging_does_not_reconstruct_original_values_from_a_lossy_large_baseline_difference()
    {
        var original = new DivergingValue(1, "small original"); var chart = new DivergingBarChart { ItemsSource = new[] { original }, Baseline = 1e308 };
        using var host = new Host(chart); var mark = Assert.Single(chart.GetMarks()); Assert.Equal(1, mark.Value); Assert.Equal(1, mark.Members[0].Point.Y); Assert.Equal(1d, mark.Members[0].Value); Assert.Equal(original, mark.Members[0].Item);
        Assert.Equal(1, chart.HitTest(mark.Position)!.Mark.Value);
    }

    [AvaloniaFact]
    public void Dumbbell_endpoint_sides_survive_reorder_resize_and_explicit_no_getter_selection()
    {
        var a = new Pair("a", 2, 8); var b = new Pair("b", 9, 3); var rows = new ObservableCollection<Pair> { a, b };
        var chart = new DumbbellChart { ItemsSource = rows, LowValuePath = "Value.First", HighValuePath = "Value.Second", LabelPath = "Id", IdPath = "Id" };
        using var host = new Host(chart); Assert.Equal(4, chart.GetMarks().Count);
        chart.SelectedIndex = 3; host.Resize(520, 320); Assert.Equal("high", chart.SelectedPoint!.Mark.Key); Assert.Same(b, chart.SelectedPoint.Members[0].Item);
        rows.Move(1, 0); host.Drain(); Assert.Equal(1, chart.SelectedIndex); Assert.Equal("high", chart.SelectedPoint!.Mark.Key); Assert.Same(b, chart.SelectedPoint.Members[0].Item);
        b.Value.Second = 6; host.Drain(); Assert.Equal(6, chart.SelectedPoint.Mark.Value);
        chart.SelectedIndex = 0; chart.SelectedIndex = 1; host.Resize(560, 350); Assert.Equal("high", chart.SelectedPoint!.Mark.Key); Assert.Same(b, chart.SelectedPoint.Members[0].Item);
    }

    [AvaloniaFact]
    public void Dumbbell_connector_is_decoration_and_equal_endpoints_return_the_visible_high_side()
    {
        var chart = new DumbbellChart { ItemsSource = new[] { new PairedComparisonValue(2, 8), new PairedComparisonValue(5, 5) }, ShowLabels = false, ShowValues = false };
        using var host = new Host(chart); var marks = chart.GetMarks(); Assert.Equal(4, marks.Count);
        var midpoint = new Point((marks[0].Position.X + marks[1].Position.X) / 2, marks[0].Position.Y);
        Assert.Null(chart.HitTest(midpoint)); Assert.Equal("high", chart.HitTest(marks[2].Position)!.Mark.Key);
        var group = Record(chart); Assert.NotEmpty(Drawings(group).OfType<GeometryDrawing>());
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Bilateral_families_share_one_magnitude_scale_and_leave_the_center_gap_unselectable(int kind)
    {
        PairedBarChartBase chart = kind switch { 0 => new MirrorBarChart(), 1 => new PopulationPyramidChart(), _ => new TornadoChart() };
        chart.ItemsSource = new[] { new PairedComparisonValue(6, 4, "A"), new PairedComparisonValue(3, 8, "B"), new PairedComparisonValue(9, 5, "C") };
        chart.ShowLabels = false; chart.ShowValues = false;
        using var host = new Host(chart); var marks = chart.GetMarks(); Assert.Equal(6, marks.Count);
        var first = marks[0]; var originalIndex = kind == 2 ? 2 : 0; var value = kind == 2 ? 9 : 6;
        Assert.Equal(originalIndex, first.Members[0].SourceIndex); Near(180 * value / 9d, first.Bounds.Width); Near(204, first.Bounds.Right);
        Near(216, marks[1].Bounds.Left); Assert.Null(chart.HitTest(new Point(210, first.Position.Y)));
        chart.SelectedIndex = 1; host.Resize(520, 320); Assert.Equal("right", chart.SelectedPoint!.Mark.Key); Assert.Equal(originalIndex, chart.SelectedPoint.Members[0].SourceIndex);
        chart.CenterGap = 4096; Assert.Empty(chart.GetMarks()); Assert.Null(chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Tornado_sorts_total_span_stably_without_replacing_original_source_indices()
    {
        var chart = new TornadoChart { ItemsSource = new[] { new PairedComparisonValue(5, 5, "first"), new PairedComparisonValue(1, 9, "tie"), new PairedComparisonValue(8, 8, "largest") } };
        using var host = new Host(chart); Assert.Equal(new[] { 2, 2, 0, 0, 1, 1 }, chart.GetMarks().Select(mark => mark.Members[0].SourceIndex));
        chart.Sort = ComparisonSort.Source; Assert.Equal(new[] { 0, 0, 1, 1, 2, 2 }, chart.GetMarks().Select(mark => mark.Members[0].SourceIndex));
    }

    [AvaloniaFact]
    public void Population_aliases_map_original_business_members_and_color_channels()
    {
        var row = new { Age = "20–29", Male = 8d, Female = 5d };
        var chart = new PopulationPyramidChart { ItemsSource = new[] { row }, AgeLabelPath = "Age", MaleValuePath = "Male", FemaleValuePath = "Female", MaleBrush = Brushes.Blue, FemaleBrush = Brushes.Red, BarGap = 20 };
        using var host = new Host(chart); var marks = chart.GetMarks(); Assert.Equal(2, marks.Count); Assert.Equal(12, chart.CenterGap); Assert.Equal(20, chart.BarGap); Near(232 * .68 - 20, marks[0].Bounds.Height);
        Assert.Same(Brushes.Blue, marks[0].Fill); Assert.Same(Brushes.Red, marks[1].Fill); Assert.Equal("20–29", marks[0].Members[0].Category); Assert.Same(row, marks[1].Members[0].Item);
    }

    [AvaloniaFact]
    public void Mekko_widths_and_bottom_up_percentages_keep_both_parent_and_child_identity()
    {
        var a = new MekkoColumn(2, new[] { new MekkoSegment(2, "a"), new MekkoSegment(3, "b") }, "A");
        var b = new MekkoColumn(3, new[] { new MekkoSegment(4, "a"), new MekkoSegment(1, "b") }, "B");
        var chart = new MekkoChart { ItemsSource = new[] { a, b }, ColumnGap = 4 };
        using var host = new Host(chart); var marks = chart.GetMarks(); Assert.Equal(4, marks.Count);
        Near(372 * .4 - 4, marks[0].Bounds.Width); Near(232 * .4, marks[0].Bounds.Height); Near(256, marks[0].Bounds.Bottom);
        var hit = chart.HitTest(marks[0].Position)!; Assert.Equal(2, hit.Members.Count); Assert.Same(a, hit.Members[0].Item); Assert.Equal(new MekkoSegment(2, "a"), hit.Members[1].Item);
        Assert.Equal(0, hit.Members[0].SourceIndex); Assert.Equal(0, hit.Members[1].SourceIndex); Assert.Equal(2, hit.Mark.Value);
        Assert.Null(chart.HitTest(new Point(24 + 372 * .4, 140)));
    }

    [AvaloniaFact]
    public void Mekko_nested_collection_edits_and_reorders_follow_child_ids_and_release_all_observers()
    {
        var first = new Segment("first", 2); var second = new Segment("second", 3);
        var row = new Column("col", 2, new ObservableCollection<Segment> { first, second });
        var chart = new MekkoChart { ItemsSource = new[] { row }, IdPath = "Id", WidthPath = "Width", SegmentsPath = "Parts", CategoryPath = "Id", SegmentIdPath = "Id", SegmentValuePath = "Value", SegmentLabelPath = "Id" };
        using var host = new Host(chart); Assert.Equal(1, first.Observers); Assert.Equal(1, second.Observers);
        chart.SelectedIndex = 1; row.Parts.Move(1, 0); host.Drain(); Assert.Equal(0, chart.SelectedIndex); Assert.Same(second, chart.SelectedPoint!.Members[1].Item);
        second.Value = 7; host.Drain(); Assert.Equal(7, chart.SelectedPoint.Mark.Value);
        row.Parts.Remove(second); host.Drain(); Assert.Null(chart.SelectedPoint); Assert.Equal(0, second.Observers);
        row.Parts.Add(second); host.Drain(); Assert.Equal(1, second.Observers);
        host.Detach(); Assert.Equal(0, first.Observers); Assert.Equal(0, second.Observers);
        host.Attach(); Assert.Equal(1, first.Observers); Assert.Equal(1, second.Observers);
    }

    [AvaloniaFact]
    public void Mekko_invalid_nested_values_can_recover_from_the_nested_notification_without_refresh()
    {
        var segment = new Segment("a", -1); var row = new Column("c", 1, new ObservableCollection<Segment> { segment });
        var chart = new MekkoChart { ItemsSource = new[] { row }, WidthPath = "Width", SegmentsPath = "Parts" };
        using var host = new Host(chart); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); Assert.Equal(1, segment.Observers);
        segment.Value = 2; host.Drain(); Assert.Null(chart.DataError); Assert.Single(chart.GetMarks());
    }

    [AvaloniaFact]
    public void Venn_two_set_exclusive_atoms_select_only_their_geometric_region_and_keep_summary_dependencies()
    {
        VennItem[] rows = [new(1, 60, "A"), new(2, 40, "B"), new(3, 20, "AB")];
        var chart = new VennDiagramChart { SetCount = 2, ItemsSource = rows };
        using var host = new Host(chart); Assert.Null(chart.DataError); var marks = chart.GetMarks(); Assert.Equal(3, marks.Count); Assert.False(chart.IsApproximate);
        foreach (var mark in marks)
        {
            var mask = Assert.IsType<int>(mark.Key); var hit = chart.HitTest(mark.Position)!; Assert.Equal(mask, hit.Mark.Key);
            Assert.Equal(mask == 1 ? 40 : 20, mark.Value);
            Assert.Equal(Enumerable.Range(0, rows.Length).Where(index => (rows[index].Mask & mask) == mask), mark.Members.Select(member => member.SourceIndex));
            Assert.All(mark.Members, member => Assert.Equal(rows[member.SourceIndex], member.Item));
        }
        Assert.Null(chart.HitTest(chart.PlotArea.TopLeft)); Assert.Empty(chart.UnrepresentedRegionMasks);
        chart.SelectedIndex = 2; var selectedMask = chart.SelectedPoint!.Mark.Key; host.Resize(520, 320); Assert.Equal(selectedMask, chart.SelectedPoint!.Mark.Key);
    }

    [AvaloniaTheory]
    [InlineData(GeometryCombineMode.Union, true, true, true)]
    [InlineData(GeometryCombineMode.Intersect, false, true, false)]
    [InlineData(GeometryCombineMode.Exclude, true, false, false)]
    [InlineData(GeometryCombineMode.Xor, true, false, true)]
    public void Compound_hits_evaluate_boolean_circle_regions_instead_of_backend_bounds(GeometryCombineMode mode, bool firstOnly, bool overlap, bool secondOnly)
    {
        var chart = new BooleanRegion(mode) { ItemsSource = new[] { 1d } }; using var host = new Host(chart);
        Assert.Equal(firstOnly, chart.HitTest(new Point(100, 120)) is not null);
        Assert.Equal(overlap, chart.HitTest(new Point(140, 120)) is not null);
        Assert.Equal(secondOnly, chart.HitTest(new Point(180, 120)) is not null);
        Assert.Null(chart.HitTest(new Point(81, 81))); // Enclosing rectangle corner is outside either circle.
    }

    [AvaloniaFact]
    public void Venn_three_set_approximation_is_disclosed_and_every_visible_atom_uses_the_exact_exclusive_hit_shape()
    {
        VennItem[] rows = [new(1, 100), new(2, 100), new(4, 100), new(3, 30), new(5, 30), new(6, 30), new(7, 10)];
        var chart = new VennDiagramChart { SetCount = 3, ItemsSource = rows };
        using var host = new Host(chart); Assert.Null(chart.DataError); Assert.True(chart.IsApproximate); Assert.NotNull(chart.Layout); Assert.True(chart.MaximumRelativeAreaError >= 0);
        Assert.Equal(7, chart.GetMarks().Count);
        foreach (var mark in chart.GetMarks()) { Assert.Equal(mark.Key, chart.HitTest(mark.Position)!.Mark.Key); Assert.True(mark.Value > 0); Assert.NotEmpty(mark.Members); }
        chart.Zoom = 1.1; chart.PanX = .01; chart.PanY = -.01;
        foreach (var mark in chart.GetMarks().Where(mark => chart.PlotArea.Contains(mark.Position))) Assert.Equal(mark.Key, chart.HitTest(mark.Position)!.Mark.Key);
    }

    [AvaloniaFact]
    public void Venn_business_set_names_and_zero_regions_do_not_fabricate_set_elements()
    {
        var rows = new[] { new { Sets = new[] { "Red" }, Value = 10d, Name = "Red total" }, new { Sets = new[] { "Blue" }, Value = 5d, Name = "Blue total" }, new { Sets = new[] { "Red", "Blue" }, Value = 0d, Name = "intersection" } };
        var chart = new VennDiagramChart { SetCount = 2, SetNames = new[] { "Red", "Blue" }, NamePath = "Name", ItemsSource = rows };
        using var host = new Host(chart); Assert.Equal(2, chart.GetMarks().Count); Assert.DoesNotContain(chart.GetMarks(), mark => Equals(mark.Key, 3));
        var a = Assert.Single(chart.GetMarks(), mark => Equals(mark.Key, 1)); Assert.Equal(new[] { 0, 2 }, a.Members.Select(member => member.SourceIndex)); Assert.Same(rows[0], a.Members[0].Item);
    }

    [AvaloniaFact]
    public void Venn_inconsistent_inclusive_intersections_clear_old_shapes_and_recover()
    {
        var chart = new VennDiagramChart { SetCount = 2, ItemsSource = new[] { new VennItem(1, 10), new VennItem(2, 5), new VennItem(3, 8) } };
        using var host = new Host(chart); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); Assert.Null(chart.Layout);
        chart.ItemsSource = new[] { new VennItem(1, 10), new VennItem(2, 5), new VennItem(3, 2) }; Assert.Equal(3, chart.GetMarks().Count); Assert.Null(chart.DataError);
    }

    [AvaloniaFact]
    public void Venn_invalid_nested_set_names_recover_when_the_same_observable_set_list_is_corrected()
    {
        var invalid = new ObservableCollection<string> { "A", "B", "unknown" };
        var rows = new[] { new { Sets = new ObservableCollection<string> { "A" }, Value = 10d }, new { Sets = new ObservableCollection<string> { "B" }, Value = 5d }, new { Sets = invalid, Value = 2d } };
        var chart = new VennDiagramChart { SetCount = 2, ItemsSource = rows };
        using var host = new Host(chart); Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError);
        invalid.Remove("unknown"); host.Drain(); Assert.Null(chart.DataError); Assert.Equal(3, chart.GetMarks().Count);
    }

    [AvaloniaFact]
    public void Render_publication_defers_selection_notifications_and_reentrant_binding_changes_remain_consistent()
    {
        var rows = new ObservableCollection<Pair> { new("a", 6, 4), new("b", 3, 8), new("c", 9, 5) };
        var chart = new MirrorBarChart { ItemsSource = rows, IdPath = "Id", LeftValuePath = "Value.First", RightValuePath = "Value.Second", SelectedIndex = 3 };
        using var host = new Host(chart); var inRender = false; var publishedInRender = false;
        chart.PropertyChanged += (_, args) => { if (inRender && (args.Property == EngineeringChartBase.SelectedPointProperty || args.Property == EngineeringChartBase.SelectedIndexProperty)) publishedInRender = true; };
        rows.Move(1, 0); inRender = true; _ = Record(chart); inRender = false; Assert.False(publishedInRender); host.Drain();
        Assert.Equal("right", chart.SelectedPoint!.Mark.Key); Assert.Equal("b", chart.SelectedPoint.Members[0].StableId);
        var overrideOnce = false;
        chart.PropertyChanged += (_, args) => { if (!overrideOnce && args.Property == EngineeringChartBase.SelectedIndexProperty && chart.SelectedIndex == 3) { overrideOnce = true; chart.SelectedIndex = 0; } };
        rows.Move(0, 1); host.Drain(); Assert.True(overrideOnce); Assert.Equal(0, chart.SelectedIndex); Assert.Equal("left", chart.SelectedPoint!.Mark.Key); Assert.Equal("a", chart.SelectedPoint.Members[0].StableId);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public async Task Each_comparison_family_uses_shared_keyboard_export_failure_and_cancellation_contracts(int kind)
    {
        ComparisonChartBase chart = kind switch
        {
            0 => new DivergingBarChart { ItemsSource = new[] { new DivergingValue(-2), new DivergingValue(3) } },
            1 => new DumbbellChart { ItemsSource = new[] { new PairedComparisonValue(2, 8) } },
            2 => new MekkoChart { ItemsSource = new[] { new MekkoColumn(2, new[] { new MekkoSegment(1) }) } },
            3 => new MirrorBarChart { ItemsSource = new[] { new PairedComparisonValue(2, 3) } },
            4 => new PopulationPyramidChart { ItemsSource = new[] { new PairedComparisonValue(2, 3) } },
            5 => new TornadoChart { ItemsSource = new[] { new PairedComparisonValue(2, 3) } },
            _ => new VennDiagramChart { SetCount = 2, ItemsSource = new[] { new VennItem(1, 5), new VennItem(2, 4), new VennItem(3, 2) } },
        };
        using var host = new Host(chart); Assert.NotEmpty(chart.GetMarks()); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Assert.Equal(0, chart.SelectedIndex);
        var group = Record(chart); Assert.NotEmpty(Drawings(group).OfType<GeometryDrawing>());
        using var stream = new MemoryStream(); var failures = 0; chart.ExportFailed += (_, _) => failures++;
        Assert.False(chart.TryExportPng(stream, new ChartExportOptions { Dpi = 0 })); Assert.Equal(1, failures); Assert.Equal(0, stream.Length);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportPngAsync(stream, cancellationToken: cancel.Token)); Assert.Equal(1, failures);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Assert.Equal(-1, chart.SelectedIndex); Assert.Null(chart.SelectedPoint);
    }

    private static DrawingGroup Record(EngineeringChartBase chart) { var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context); return group; }
    private static IEnumerable<Drawing> Drawings(Drawing drawing) { yield return drawing; if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var nested in Drawings(child)) yield return nested; }
    private static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) <= 1e-7 * Math.Max(1, Math.Abs(expected)), $"Expected {expected:R}, got {actual:R}");
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
    private sealed class Pair(string id, double first, double second) { public string Id { get; } = id; public PairValues Value { get; } = new(first, second); }
    private sealed class PairValues(double first, double second) : INotifyPropertyChanged
    {
        private double _second = second; public double First { get; } = first;
        public double Second { get => _second; set { _second = value; PropertyChanged?.Invoke(this, new(nameof(Second))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Column(string id, double width, ObservableCollection<Segment> parts) { public string Id { get; } = id; public double Width { get; } = width; public ObservableCollection<Segment> Parts { get; } = parts; }
    private sealed class Segment(string id, double value) : INotifyPropertyChanged
    {
        private double _value = value; private PropertyChangedEventHandler? _changed; public string Id { get; } = id; public int Observers { get; private set; }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
    private sealed class BooleanRegion(GeometryCombineMode mode) : EngineeringChartBase
    {
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) =>
            [new(new[] { data.Points[0] }, new CombinedGeometry(mode, new EllipseGeometry(new Rect(80, 80, 80, 80)), new EllipseGeometry(new Rect(120, 80, 80, 80))), Brushes.Blue, new Point(140, 120))];
    }
}
