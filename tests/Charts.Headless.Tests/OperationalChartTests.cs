using System.Collections.ObjectModel;
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

public sealed class OperationalChartTests
{
    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public async Task Every_operational_family_retains_original_items_hits_navigates_and_exports_with_cancellation(int kind)
    {
        var chart = Create(kind); using var host = new Host(chart); Assert.Null(chart.DataError); var mark = chart.GetMarks().First(); Assert.NotEmpty(mark.Members); var item = mark.Members[0].Item; Assert.NotNull(item); var hit = chart.HitTest(mark.Position); Assert.NotNull(hit); Assert.Contains(hit!.Members, x => ReferenceEquals(item, x.Item)); chart.SelectedIndex = 0; Assert.Same(item, chart.SelectedPoint!.Members[0].Item); var position = mark.Position; chart.Zoom = 1.1; chart.PanX = .01; chart.PanY = -.01; var updated = chart.GetMarks().First(); Assert.Same(item, updated.Members[0].Item); Assert.NotEqual(position, updated.Position); Assert.Same(item, chart.SelectedPoint!.Members[0].Item); chart.ResetViewport(); SendKey(chart, Key.Home); SendKey(chart, Key.Space); Assert.Equal(0, chart.SelectedIndex); var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context); using var stream = new MemoryStream(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportPngAsync(stream, cancellationToken: cancellation.Token)); Assert.Equal(0, stream.Length);
    }
    [AvaloniaFact]
    public void Custom_paths_two_way_selection_inpc_incc_reorder_and_source_replacement_are_observed_and_released()
    {
        var first = new Row("a", 3); var second = new Row("b", 2); var source = new ObservableCollection<Row> { first, second }; var model = new Model { Items = source }; var chart = new WaffleChart { DataContext = model, ValuePath = "Amount", IdPath = "Key", LabelPath = "Name" }; chart.Bind(EngineeringChartBase.ItemsSourceProperty, new Binding(nameof(Model.Items))); chart.Bind(EngineeringChartBase.SelectedIndexProperty, new Binding(nameof(Model.Selected)) { Mode = BindingMode.TwoWay }); using var host = new Host(chart); Assert.True(first.Subscribers > 0); Assert.Equal(60, chart.Allocation[0].Units); chart.SelectedIndex = 0; Assert.Equal(0, model.Selected); first.Amount = 1; Assert.Equal(33, chart.Allocation[0].Units); source.Move(0, 1); Assert.Same(first, chart.SelectedPoint!.Members[0].Item); Assert.Equal(1, chart.SelectedPoint.Members[0].SourceIndex); source.Remove(first); Assert.Null(chart.SelectedPoint); Assert.Equal(0, first.Subscribers); var replacement = new Row("c", 8); model.Items = new ObservableCollection<Row> { replacement }; Assert.Single(chart.Allocation); Assert.Equal(0, second.Subscribers); Assert.True(replacement.Subscribers > 0); host.Detach(); Assert.Equal(0, replacement.Subscribers); host.Attach(); Assert.True(replacement.Subscribers > 0); replacement.Amount = double.NaN; Assert.Empty(chart.GetMarks()); Assert.NotNull(chart.DataError); replacement.Amount = 7; Assert.NotEmpty(chart.GetMarks()); Assert.Null(chart.DataError);
    }
    [AvaloniaFact]
    public void Calendar_missing_day_is_not_selectable_and_zero_day_is_real_business_record()
    {
        var item = new CalendarPoint("zero", new(2024, 2, 29), 0); var chart = new CalendarHeatmapChart { ItemsSource = new[] { item }, StartDate = new(2024, 2, 26), EndDate = new(2024, 3, 3) }; using var host = new Host(chart); var mark = Assert.Single(chart.GetMarks()); Assert.Same(item, mark.Members[0].Item); var missing = new Point(chart.PlotArea.Center.X, chart.PlotArea.Top + chart.PlotArea.Height / 14); Assert.Null(chart.HitTest(missing)); Assert.Same(item, chart.HitTest(mark.Position)!.Members[0].Item);
    }
    [AvaloniaFact]
    public void Ranking_missing_periods_do_not_create_connectors_or_fabricated_members()
    {
        var chart = new BumpChart { ItemsSource = new[] { new RankingPoint("a0", "a", 0, 5), new RankingPoint("b1", "b", 1, 2), new RankingPoint("a2", "a", 2, 3) } }; using var host = new Host(chart); Assert.Equal(3, chart.GetMarks().Count); Assert.Equal(3, chart.Ranking!.Periods.Count); Assert.All(chart.GetMarks(), mark => Assert.Single(mark.Members));
    }
    [AvaloniaFact]
    public void Word_measurement_uses_real_typeface_and_never_overlaps_placed_boxes()
    {
        var chart = new WordCloudChart { ItemsSource = new[] { new WordPoint("a", "Avalonia", 8), new WordPoint("b", "数据", 4), new WordPoint("c", "Linux", 3), new WordPoint("d", "Windows", 2) }, MinFontSize = 12, MaxFontSize = 35 }; using var host = new Host(chart); Assert.Null(chart.DataError); var words = chart.Words!; Assert.NotEmpty(words.Words); Assert.Equal(4, words.Words.Count + words.OmittedIndices.Count); for (var i = 0; i < words.Words.Count; i++) for (var j = i + 1; j < words.Words.Count; j++) { var a = words.Words[i]; var b = words.Words[j]; Assert.False(new Rect(a.X, a.Y, a.Width, a.Height).Intersects(new(b.X, b.Y, b.Width, b.Height))); }
    }
    [AvaloniaFact]
    public void River_selectable_band_contains_every_raw_slot_and_baseline_switch_retains_identity()
    {
        ThemeRiverPoint[] source = [new("a0", "a", 0, 1), new("a1", "a", 1, 3), new("b0", "b", 0, 4), new("b1", "b", 1, 2)]; var chart = new ThemeRiverChart { ItemsSource = source }; using var host = new Host(chart); var a = chart.GetMarks()[0]; Assert.Equal(new[] { 0, 1 }, a.Members.Select(x => x.SourceIndex)); Assert.Same(source[0], a.Members[0].Item); Assert.Same(source[1], a.Members[1].Item); chart.SelectedIndex = 0; chart.Baseline = OperationalRiverBaseline.Zero; Assert.Same(source[0], chart.SelectedPoint!.Members[0].Item); Assert.Equal(0, chart.River!.Bands[0].Lower[0].Y);
    }
    [AvaloniaFact]
    public void Bullet_scalar_source_is_real_owner_and_nonnull_collection_has_precedence()
    {
        var ranges = new ObservableCollection<double> { 30, 60, 100 }; var chart = new BulletChart { Value = 25, Target = 75, MinValue = 0, MaxValue = 100, Ranges = ranges, Label = "Revenue" }; using var host = new Host(chart); var row = Assert.Single(chart.Layout); Assert.Equal(.25, row.Position); Assert.Equal(.75, row.TargetPosition); Assert.Same(chart, Assert.Single(chart.GetMarks()).Members[0].Item); ranges[0] = 40; Assert.Equal(.4, chart.Layout[0].Boundaries[0]); chart.SelectedIndex = 0; chart.Value = 50; Assert.Same(chart, chart.SelectedPoint!.Members[0].Item); var actual = new BulletPoint("record", 90, 80); chart.ItemsSource = new[] { actual }; Assert.Equal(90, Assert.Single(chart.Layout).Source.Value); Assert.Same(actual, Assert.Single(chart.GetMarks()).Members[0].Item); chart.ItemsSource = Array.Empty<BulletPoint>(); Assert.Empty(chart.GetMarks());
    }
    [AvaloniaTheory]
    [InlineData(0, 0)] [InlineData(12.5, 1)] [InlineData(50, 2)] [InlineData(125, 4)] [InlineData(-3, 0)]
    public void Waffle_scalar_progress_rounds_away_from_zero_and_empty_cells_are_not_business_points(double value, int expected)
    {
        var chart = new WaffleChart { Rows = 2, Columns = 2, Value = value, MaxValue = 100, FilledBrush = Brushes.Blue, EmptyBrush = Brushes.Gray }; using var host = new Host(chart); Assert.Equal(expected, chart.GetMarks().Count); Assert.Equal(value, Assert.Single(chart.Allocation).Source.Value); Assert.All(chart.GetMarks(), m => { Assert.Same(chart, m.Members[0].Item); Assert.Equal(Brushes.Blue, m.Fill); }); var empty = new Point(chart.PlotArea.Right - chart.PlotArea.Width / 4, chart.PlotArea.Top + chart.PlotArea.Height / 4); if (expected < 4) Assert.Null(chart.HitTest(empty)); chart.ItemsSource = new[] { new WafflePoint("a", 1), new WafflePoint("b", 3) }; Assert.Equal(new[] { 1, 3 }, chart.Allocation.Select(a => a.Units)); chart.ItemsSource = Array.Empty<WafflePoint>(); Assert.Empty(chart.GetMarks());
    }
    [AvaloniaFact]
    public void Kpi_scalar_explicit_percentage_points_and_previous_derivation_are_distinct_observed_modes()
    {
        var trend = new ObservableCollection<double> { 80, 100, 120 }; var chart = new KpiCard { Value = 120, Previous = 100, Delta = 12.5, Unit = "$", Subtitle = "monthly", SparklineData = trend }; using var host = new Host(chart); var row = Assert.Single(chart.Layout); Assert.True(row.IsExplicitDelta); Assert.Equal(.125, row.DisplayDelta); Assert.Equal(20, row.Change); Assert.Equal(.2, row.RelativeChange); Assert.Same(chart, Assert.Single(chart.GetMarks()).Members[0].Item); trend.Add(140); Assert.Equal(4, chart.Layout[0].Trend.Count); chart.DeltaType = OperationalDeltaType.Absolute; Assert.Equal(12.5, chart.Layout[0].DisplayDelta); chart.Delta = null; Assert.False(chart.Layout[0].IsExplicitDelta); Assert.Equal(20, chart.Layout[0].DisplayDelta); chart.Previous = 0; chart.DeltaType = OperationalDeltaType.Percentage; Assert.Null(chart.Layout[0].DisplayDelta); chart.ItemsSource = new[] { new KpiPoint("raw", 30, 20) }; Assert.Equal(.5, chart.Layout[0].DisplayDelta); chart.ItemsSource = Array.Empty<KpiPoint>(); Assert.Empty(chart.GetMarks());
    }
    [AvaloniaFact]
    public void Tabular_columns_bind_original_business_rows_with_numeric_string_and_boolean_cells_and_updates()
    {
        var row = new Row("a", 3); var columns = new ObservableCollection<OperationalTableColumn> { new("Amount", "Amount") { Id = "amount", Format = "0.0", Minimum = 0, Maximum = 10 }, new("Name", "Name") { Id = "name", UseColorScale = false } }; var chart = new TableChart { ItemsSource = new[] { row }, Columns = columns, IdPath = "Key", LabelPath = "Name" }; using var host = new Host(chart); Assert.Null(chart.Matrix); Assert.Equal(2, chart.Cells.Count); Assert.Equal("3.0", chart.Cells[0].DisplayText); Assert.Equal("a", chart.Cells[1].DisplayText); Assert.Null(chart.Cells[1].NumericValue); Assert.All(chart.GetMarks(), m => Assert.Same(row, m.Members[0].Item)); chart.SelectedIndex = 0; row.Amount = 5; Assert.Equal(5, chart.Cells[0].NumericValue); Assert.Same(row, chart.SelectedPoint!.Members[0].Item); columns.Move(0, 1); Assert.Same(row, chart.SelectedPoint!.Members[0].Item); Assert.Equal("amount", chart.Cells[chart.SelectedPoint.Index].ColumnId); columns[1].Format = "0.00"; Assert.Equal("5.00", chart.Cells[1].DisplayText); columns.RemoveAt(1); Assert.Null(chart.SelectedPoint); host.Detach(); Assert.Equal(0, row.Subscribers); host.Attach(); Assert.True(row.Subscribers > 0); var dictionary = new OperationalTableRow("dictionary", new Dictionary<string, object?> { ["enabled"] = false }); chart.Columns = new[] { new OperationalTableColumn("Enabled", "enabled") }; chart.IdPath = "Id"; chart.ItemsSource = new[] { dictionary }; Assert.False(Assert.IsType<bool>(chart.GetMarks()[0].Members[0].Value)); Assert.Equal(0, chart.Cells[0].NumericValue); Assert.Same(dictionary, chart.Cells[0].Source);
    }
    [AvaloniaFact]
    public void Endpoint_labels_have_explicit_omission_without_losing_ranked_points()
    {
        var source = Enumerable.Range(0, 40).SelectMany(i => new[] { new RankingPoint($"{i}-0", i, 0, 1), new RankingPoint($"{i}-1", i, 1, 1) }).ToArray(); var chart = new BumpChart { ItemsSource = source }; using var host = new Host(chart); Assert.Equal(80, chart.GetMarks().Count); Assert.True(chart.OmittedLabelCount > 0); Assert.Equal(80, chart.GetMarks().Sum(m => m.Members.Count)); chart.ShowLabels = false; Assert.Equal(0, chart.OmittedLabelCount); Assert.Equal(80, chart.GetMarks().Count);
    }
    [AvaloniaFact]
    public void Kpi_dark_and_light_recorded_drawing_use_neutral_cards_and_independent_delta_accents_with_custom_alpha()
    {
        var chart = new KpiCard { Value = 120, Previous = 100, Delta = 12.5, Label = "Revenue", Foreground = Brushes.White, Background = Brushes.Black, ShowSparkline = false }; using var host = new Host(chart);
        static IEnumerable<Drawing> Leaves(Drawing drawing) { if (drawing is DrawingGroup group) { foreach (var child in group.Children) foreach (var leaf in Leaves(child)) yield return leaf; } else yield return drawing; }
        DrawingGroup Render() { var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context); return drawing; }
        static bool Is(IBrush? brush, string hex) => brush is ISolidColorBrush solid && solid.Color == Color.Parse(hex) && solid.Opacity == 1;
        Assert.True(Is(Assert.Single(chart.GetMarks()).Fill, "#1E293B")); var dark = Leaves(Render()).OfType<GlyphRunDrawing>().ToArray(); Assert.Contains(dark, glyph => Is(glyph.Foreground, "#86EFAC")); Assert.Contains(dark, glyph => Is(glyph.Foreground, "#FFFFFF")); chart.Delta = -2; Assert.Contains(Leaves(Render()).OfType<GlyphRunDrawing>(), glyph => Is(glyph.Foreground, "#FCA5A5"));
        chart.Foreground = Brushes.Black; chart.Delta = 3; Assert.True(Is(Assert.Single(chart.GetMarks()).Fill, "#F5F7FA")); Assert.Contains(Leaves(Render()).OfType<GlyphRunDrawing>(), glyph => Is(glyph.Foreground, "#166534")); chart.Delta = -3; Assert.Contains(Leaves(Render()).OfType<GlyphRunDrawing>(), glyph => Is(glyph.Foreground, "#991B1B"));
        chart.Delta = 5; chart.PositiveBrush = new SolidColorBrush(Color.FromArgb(128, 10, 20, 30)) { Opacity = .5 }; Assert.Contains(Leaves(Render()).OfType<GlyphRunDrawing>(), glyph => glyph.Foreground is ISolidColorBrush solid && solid.Color == Color.FromArgb(128, 10, 20, 30) && solid.Opacity == .5); Assert.True(Is(Assert.Single(chart.GetMarks()).Fill, "#F5F7FA")); Assert.Same(chart, chart.GetMarks()[0].Members[0].Item);
    }
    [AvaloniaFact]
    public void Table_cell_text_contrasts_with_fill_without_changing_external_label_foreground()
    {
        var chart = new TableChart { ItemsSource = new[] { new MatrixPoint("a", "Row", "Col", 1, "Cell") }, Foreground = Brushes.White, Background = Brushes.Black, MinimumColor = Colors.White, MaximumColor = Colors.White };
        using var host = new Host(chart);
        static IEnumerable<GlyphRunDrawing> Glyphs(Drawing drawing) { if (drawing is DrawingGroup group) { foreach (var child in group.Children) foreach (var glyph in Glyphs(child)) yield return glyph; } else if (drawing is GlyphRunDrawing glyph) yield return glyph; }
        DrawingGroup Render() { var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context); return group; }
        Assert.Contains(Glyphs(Render()), glyph => glyph.Foreground is ISolidColorBrush solid && solid.Color == Colors.Black);
        Assert.Contains(Glyphs(Render()), glyph => glyph.Foreground is ISolidColorBrush solid && solid.Color == Colors.White);
        Assert.Same(Brushes.White, chart.Foreground);
        chart.MinimumColor = chart.MaximumColor = Color.FromArgb(32, 255, 255, 255);
        Assert.All(Glyphs(Render()), glyph => Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(glyph.Foreground).Color));
        Assert.Same(Brushes.White, chart.Foreground);
    }
    private static OperationalChartBase Create(int kind) => kind switch
    {
        0 => new BulletChart { ItemsSource = new[] { new BulletPoint("b", 70, 85, Ranges: new[] { 30d, 60, 100 }, Label: "Sales") } },
        1 => new KpiCardChart { ItemsSource = new[] { new KpiPoint("k", 15, 10, new[] { 1d, 3, 2 }, "Orders") } },
        2 => new CalendarHeatmapChart { ItemsSource = new[] { new CalendarPoint("c", new(2026, 1, 1), 4) } },
        3 => new MatrixChart { ItemsSource = new[] { new MatrixPoint("m", "Row", "Column", 1) } },
        4 => new TableChart { ItemsSource = new[] { new MatrixPoint("t", "Row", "Column", 8, "eight") } },
        5 => new WaffleChart { Rows = 2, Columns = 2, ItemsSource = new[] { new WafflePoint("w", 3) } },
        6 => new WordCloudChart { MaxFontSize = 35, ItemsSource = new[] { new WordPoint("word", "Avalonia", 8) } },
        7 => new BumpChart { ItemsSource = new[] { new RankingPoint("r0", "A", 0, 1), new RankingPoint("r1", "A", 1, 2) } },
        8 => new SlopeChart { ItemsSource = new[] { new RankingPoint("s0", "A", 0, 1), new RankingPoint("s1", "A", 1, 2) } },
        9 => new ParliamentChart { Rows = 2, ItemsSource = new[] { new ParliamentParty("p", 10, "Party") } },
        _ => new ThemeRiverChart { ItemsSource = new[] { new ThemeRiverPoint("t0", "A", 0, 1), new ThemeRiverPoint("t1", "A", 1, 2) } },
    };
    private static void SendKey(EngineeringChartBase chart, Key key) => chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
    private sealed class Host : IDisposable
    { private readonly Window _window; private readonly OperationalChartBase _chart; public Host(OperationalChartBase chart) { _chart = chart; _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); Drain(); } public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); } public void Detach() { _window.Content = null; Drain(); } public void Attach() { _window.Content = _chart; Drain(); } public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); } }
    private sealed class Row(string key, double amount) : INotifyPropertyChanged
    { private double _amount = amount; private PropertyChangedEventHandler? _changed; public string Key { get; } = key; public string Name => Key; public double Amount { get => _amount; set { _amount = value; _changed?.Invoke(this, new(nameof(Amount))); } } public int Subscribers { get; private set; } public event PropertyChangedEventHandler? PropertyChanged { add { Subscribers++; _changed += value; } remove { Subscribers--; _changed -= value; } } }
    private sealed class Model : INotifyPropertyChanged
    { private ObservableCollection<Row> _items = []; private int _selected = -1; public ObservableCollection<Row> Items { get => _items; set { _items = value; PropertyChanged?.Invoke(this, new(nameof(Items))); } } public int Selected { get => _selected; set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); } } public event PropertyChangedEventHandler? PropertyChanged; }
}
