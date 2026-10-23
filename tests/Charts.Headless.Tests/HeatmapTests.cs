using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class HeatmapTests
{
    [AvaloniaFact]
    public void Defaults_validation_and_empty_source_do_not_create_matrix_business_items()
    {
        var chart = new HeatmapChart();
        Assert.Equal(2, chart.CellGap); Assert.True(chart.ShowAxes); Assert.True(chart.ShowColorScale);
        Assert.Null(chart.MissingBrush); Assert.Null(chart.Minimum); Assert.Null(chart.Maximum);
        Assert.Throws<ArgumentException>(() => chart.Minimum = double.NaN);
        Assert.Throws<ArgumentException>(() => chart.Maximum = double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => chart.CellGap = -1);
        Assert.Throws<ArgumentException>(() => chart.CellGap = double.NaN);
        using var host = new Host(chart);
        Assert.Empty(chart.GetMarks()); Assert.Empty(chart.RowKeys); Assert.Empty(chart.ColumnKeys);
        Assert.False(chart.IsColorDomainValid); Assert.Null(chart.EffectiveMinimum); Assert.Null(chart.EffectiveMaximum);
    }

    [AvaloniaFact]
    public void Two_by_three_matrix_has_independently_expected_rectangles_midpoint_colors_and_raw_hits()
    {
        var rows = new[] { new Cell("north", "A", -10), new("north", "B", 0), new("north", "C", 10), new("south", "A", 10), new("south", "B", 0), new("south", "C", -10) };
        var chart = Chart(rows); chart.LowColor = Colors.Black; chart.HighColor = Colors.White;
        using var host = new Host(chart);
        Assert.Equal(new Rect(0, 0, 300, 200), chart.PlotArea);
        var marks = chart.GetMarks(); Assert.Equal(6, marks.Count);
        Assert.Equal(new Rect(1, 1, 98, 98), marks[0].Bounds);
        Assert.Equal(new Rect(101, 1, 98, 98), marks[1].Bounds);
        Assert.Equal(new Rect(201, 1, 98, 98), marks[2].Bounds);
        Assert.Equal(new Rect(1, 101, 98, 98), marks[3].Bounds);
        Assert.Equal(new Rect(201, 101, 98, 98), marks[5].Bounds);
        Assert.Equal(new[] { Colors.Black, Color.FromRgb(128, 128, 128), Colors.White, Colors.White, Color.FromRgb(128, 128, 128), Colors.Black }, marks.Select(ColorOf));
        Assert.Equal(-10, chart.EffectiveMinimum); Assert.Equal(10, chart.EffectiveMaximum); Assert.True(chart.IsColorDomainValid);
        for (var index = 0; index < marks.Count; index++)
        {
            Assert.Same(rows[index], marks[index].Data.Item);
            var hit = chart.HitTest(marks[index].Bounds.Center); Assert.NotNull(hit);
            Assert.Same(rows[index], hit.Item); Assert.Equal(index, hit.Index); Assert.Equal(rows[index].Value, hit.Point.Y);
        }
        Assert.Null(chart.HitTest(new Point(100, 50))); Assert.Null(chart.HitTest(new Point(50, 100))); Assert.Null(chart.HitTest(new Point(-1, 50)));
    }

    [AvaloniaFact]
    public void Stable_first_seen_typed_keys_do_not_merge_string_numeric_or_value_equal_keys()
    {
        var chart = Chart(new[] { new Cell(1, "1", 1), new Cell("1", 1, 2), new Cell(1d, "1", 3), new Cell(new Key("west"), "1", 4), new Cell(new Key("west"), 1, 5) });
        using var host = new Host(chart);
        Assert.Equal(4, chart.RowKeys.Count); Assert.IsType<int>(chart.RowKeys[0]); Assert.IsType<string>(chart.RowKeys[1]); Assert.IsType<double>(chart.RowKeys[2]);
        Assert.Equal(new Key("west"), chart.RowKeys[3]); Assert.Equal(new object[] { "1", 1 }, chart.ColumnKeys);
        var marks = chart.GetMarks(); Assert.Equal(5, marks.Count);
        Assert.Equal(marks[3].Bounds.Y, marks[4].Bounds.Y); Assert.NotEqual(marks[0].Bounds.Y, marks[1].Bounds.Y);
    }

    [AvaloniaFact]
    public void Missing_values_reserve_axes_but_absent_combinations_and_nan_cells_stay_blank()
    {
        var chart = Chart(new[] { new Cell("A", "X", 0), new("B", "Y", double.NaN), new("B", "Z", -1) });
        using var host = new Host(chart);
        Assert.Equal(new object[] { "A", "B" }, chart.RowKeys); Assert.Equal(new object[] { "X", "Y", "Z" }, chart.ColumnKeys);
        Assert.Equal(new[] { 0, 2 }, chart.GetMarks().Select(m => m.Data.SourceIndex));
        Assert.Null(chart.HitTest(new Point(150, 150))); // The actual NaN at B/Y.
        Assert.Null(chart.HitTest(new Point(250, 50))); // No supplied item at A/Z.
        Assert.Equal(-1, chart.EffectiveMinimum); Assert.Equal(0, chart.EffectiveMaximum);
    }

    [AvaloniaFact]
    public void Duplicate_cells_use_last_source_item_for_domain_geometry_and_hit_identity()
    {
        var old = new Cell("A", "X", -1000); var winner = new Cell("A", "X", 10); var other = new Cell("A", "Y", 20);
        var chart = Chart(new[] { old, other, winner });
        using var host = new Host(chart);
        var marks = chart.GetMarks(); Assert.Equal(new[] { 1, 2 }, marks.Select(m => m.Data.SourceIndex));
        Assert.Equal(10, chart.EffectiveMinimum); Assert.Equal(20, chart.EffectiveMaximum);
        var hit = chart.HitTest(new Point(75, 100)); Assert.NotNull(hit); Assert.Same(winner, hit.Item); Assert.Equal(2, hit.Index);
    }

    [AvaloniaFact]
    public void Invalid_last_duplicate_erases_the_old_fill_and_does_not_fall_back_to_the_previous_value()
    {
        var chart = Chart(new[] { new Cell("A", "X", 10), new("A", "Y", 20), new("A", "X", double.NaN) });
        using var host = new Host(chart);
        Assert.Equal(1, Assert.Single(chart.GetMarks()).Data.SourceIndex);
        Assert.Null(chart.HitTest(new Point(75, 100))); Assert.Equal(20, chart.EffectiveMinimum); Assert.Equal(20, chart.EffectiveMaximum);
    }

    [AvaloniaFact]
    public void Missing_brush_paints_only_explicit_missing_cells_and_preserves_their_raw_item()
    {
        var missing = new Cell("B", "Y", double.NaN);
        var chart = Chart(new[] { new Cell("A", "X", 1), missing }); chart.MissingBrush = Brushes.Magenta;
        using var host = new Host(chart);
        Assert.Equal(2, chart.GetMarks().Count); var mark = chart.GetMarks().Single(m => m.Data.IsEmpty);
        Assert.Same(missing, mark.Data.Item); Assert.Equal(Colors.Magenta, ColorOf(mark)); Assert.Equal(string.Empty, mark.Label);
        Assert.Null(chart.HitTest(new Point(225, 50))); // A/Y is absent, even with MissingBrush.
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(-15)] [InlineData(1e308)]
    public void Equal_automatic_values_have_a_stable_midpoint_without_perturbing_the_raw_value(double value)
    {
        var chart = Chart(new[] { new Cell("A", "X", value) }); chart.LowColor = Colors.Black; chart.HighColor = Colors.White;
        using var host = new Host(chart);
        Assert.True(chart.IsColorDomainValid); Assert.Equal(value, chart.EffectiveMinimum); Assert.Equal(value, chart.EffectiveMaximum);
        var mark = Assert.Single(chart.GetMarks()); Assert.Equal(Color.FromRgb(128, 128, 128), ColorOf(mark)); Assert.Equal(value, mark.Data.Point.Y);
    }

    [AvaloniaFact]
    public void Explicit_bounds_clamp_color_without_changing_source_values_or_removing_outliers()
    {
        var chart = Chart(new[] { new Cell("A", "X", -50), new("A", "Y", 150) }); chart.Minimum = 0; chart.Maximum = 100;
        chart.LowColor = Colors.Black; chart.HighColor = Colors.White;
        using var host = new Host(chart);
        Assert.Equal(new[] { Colors.Black, Colors.White }, chart.GetMarks().Select(ColorOf));
        Assert.Equal(new[] { -50d, 150d }, chart.GetMarks().Select(m => m.Data.Point.Y));
    }

    [AvaloniaTheory]
    [InlineData(100, 0)] [InlineData(10, 10)]
    public void Invalid_explicit_color_domains_remain_queryable_empty_and_restore_when_bindings_recover(double minimum, double maximum)
    {
        var chart = Chart(new[] { new Cell("A", "X", 5) }); using var host = new Host(chart);
        Assert.Single(chart.GetMarks()); chart.Minimum = minimum; chart.Maximum = maximum;
        Assert.False(chart.IsColorDomainValid); Assert.Empty(chart.GetMarks()); Assert.Null(chart.HitTest(new Point(150, 100)));
        Assert.Single(chart.RowKeys); Assert.Single(chart.ColumnKeys);
        chart.Minimum = 0; chart.Maximum = 10;
        Assert.True(chart.IsColorDomainValid); Assert.Single(chart.GetMarks());
    }

    [AvaloniaFact]
    public void Opposite_max_double_endpoints_interpolate_finite_midpoint_and_close_endpoints_keep_distinct_colors()
    {
        var chart = Chart(new[] { new Cell("A", "X", -double.MaxValue), new("A", "Y", 0), new("A", "Z", double.MaxValue) });
        chart.LowColor = Colors.Black; chart.HighColor = Colors.White; using var host = new Host(chart);
        Assert.Equal(new[] { Colors.Black, Color.FromRgb(128, 128, 128), Colors.White }, chart.GetMarks().Select(ColorOf));
        chart.ItemsSource = new[] { new Cell("A", "X", 1e308), new("A", "Y", Math.BitIncrement(1e308)) };
        Assert.Equal(new[] { Colors.Black, Colors.White }, chart.GetMarks().Select(ColorOf));
    }

    [AvaloniaFact]
    public void Gap_zero_fills_slots_and_excessive_gap_creates_no_invisible_hit_targets()
    {
        var chart = Chart(new[] { new Cell("A", "X", 1), new("A", "Y", 2) }); using var host = new Host(chart);
        chart.CellGap = 0; Assert.Equal(new Rect(0, 0, 150, 200), chart.GetMarks()[0].Bounds);
        chart.CellGap = 1e300; Assert.Empty(chart.GetMarks()); Assert.Null(chart.HitTest(new Point(75, 100)));
        chart.CellGap = 2; Assert.Equal(2, chart.GetMarks().Count);
    }

    [AvaloniaFact]
    public void Missing_null_or_unreadable_axis_keys_skip_only_the_affected_source_rows()
    {
        var chart = Chart(new[] { new Cell(null, "X", 1), new("A", null, 2), new("B", "Y", 3) }); using var host = new Host(chart);
        Assert.Equal(2, Assert.Single(chart.GetMarks()).Data.SourceIndex); Assert.Equal(new object[] { "B" }, chart.RowKeys);
        chart.RowPath = "DoesNotExist"; Assert.Empty(chart.GetMarks()); Assert.Empty(chart.RowKeys);
        chart.RowPath = nameof(Cell.Row); Assert.Single(chart.GetMarks());
    }

    [AvaloniaFact]
    public void Collection_and_nested_row_column_notifications_refresh_matrix_and_release_all_observers()
    {
        var row = new MutableCell { RowKey = new NotifyValue("A"), ColumnKey = new NotifyValue("X"), Value = 10 };
        var source = new ObservableCollection<MutableCell> { row };
        var chart = Chart(Array.Empty<Cell>()); chart.RowPath = "RowKey.Value"; chart.ColumnPath = "ColumnKey.Value"; chart.ValuePath = nameof(MutableCell.Value); chart.ItemsSource = source;
        using var host = new Host(chart);
        Assert.Equal(new object[] { "A" }, chart.RowKeys); Assert.True(row.Observers > 0); Assert.True(row.RowKey.Observers > 0); Assert.True(row.ColumnKey.Observers > 0);
        row.RowKey.Value = "B"; row.ColumnKey.Value = "Y"; host.Drain();
        Assert.Equal(new object[] { "B" }, chart.RowKeys); Assert.Equal(new object[] { "Y" }, chart.ColumnKeys);
        var oldKey = row.RowKey; row.RowKey = new NotifyValue("C"); host.Drain();
        Assert.Equal(0, oldKey.Observers); Assert.True(row.RowKey.Observers > 0); Assert.Equal(new object[] { "C" }, chart.RowKeys);
        row.Value = double.NaN; host.Drain(); Assert.Empty(chart.GetMarks()); Assert.Single(chart.RowKeys);
        row.Value = 25; host.Drain(); Assert.Equal(25, Assert.Single(chart.GetMarks()).Data.Point.Y);
        source.Add(new MutableCell { RowKey = new NotifyValue("D"), ColumnKey = new NotifyValue("Z"), Value = 50 }); host.Drain(); Assert.Equal(2, chart.GetMarks().Count);
        host.Window.Content = null; host.Drain(); Assert.Equal(0, row.Observers); Assert.Equal(0, row.RowKey.Observers); Assert.Equal(0, row.ColumnKey.Observers);
        host.Window.Content = chart; host.Drain(); Assert.True(row.RowKey.Observers > 0);
        source.Clear(); host.Drain(); Assert.Empty(chart.GetMarks()); Assert.Equal(0, row.Observers); Assert.Equal(0, row.RowKey.Observers);
    }

    [AvaloniaFact]
    public void Axes_and_color_scale_reserve_space_and_record_labels_gradient_and_readable_cell_contrast()
    {
        var chart = Chart(new[] { new Cell("North", "January", 0), new("South", "February", 100) });
        chart.ShowAxes = chart.ShowColorScale = true; chart.LowColor = Colors.Black; chart.HighColor = Colors.White; chart.ShowLabels = true;
        using var host = new Host(chart);
        Assert.True(chart.PlotArea.Left > 0); Assert.True(chart.PlotArea.Right < 300); Assert.True(chart.PlotArea.Bottom < 200);
        var marks = chart.GetMarks(); Assert.Same(Brushes.White, marks[0].LabelBrush); Assert.Same(Brushes.Black, marks[1].LabelBrush);
        var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context);
        var text = string.Join("|", Flatten(drawing).OfType<GlyphRunDrawing>().Select(g => g.GlyphRun is { } glyph ? new string(glyph.Characters.Span) : string.Empty));
        Assert.Contains("North", text); Assert.Contains("January", text); Assert.Contains("100", text);
        Assert.DoesNotContain("NaN", text); Assert.DoesNotContain("∞", text);
        Assert.Contains(Flatten(drawing).OfType<GeometryDrawing>(), d => d.Geometry?.Bounds.Width == 12 && d.Geometry.Bounds.Height == chart.PlotArea.Height);
    }

    [AvaloniaFact]
    public void ShowValues_controls_cell_text_independently_of_axes_and_category_labels()
    {
        var chart = Chart(new[] { new Cell("North", "January", 42) }); chart.ShowLabels = true;
        using var host = new Host(chart);
        var hidden = new DrawingGroup(); using (var context = hidden.Open()) chart.Render(context);
        Assert.Empty(Flatten(hidden).OfType<GlyphRunDrawing>());
        chart.ShowValues = true;
        var shown = new DrawingGroup(); using (var context = shown.Open()) chart.Render(context);
        Assert.Contains(Flatten(shown).OfType<GlyphRunDrawing>(), g => g.GlyphRun is { } glyph && new string(glyph.Characters.Span).Contains("42", StringComparison.Ordinal));
        chart.ShowValues = false;
        var hiddenAgain = new DrawingGroup(); using (var context = hiddenAgain.Open()) chart.Render(context);
        Assert.Empty(Flatten(hiddenAgain).OfType<GlyphRunDrawing>());
    }

    private static HeatmapChart Chart(IEnumerable<Cell> data) => new()
    {
        Width = 300, Height = 200, Padding = new(0), ItemsSource = data,
        RowPath = nameof(Cell.Row), ColumnPath = nameof(Cell.Column), ValuePath = nameof(Cell.Value),
        ShowAxes = false, ShowColorScale = false, ShowLabels = false, ShowValues = false
    };
    private static Color ColorOf(AnalysisMarkInfo mark) => Assert.IsAssignableFrom<ISolidColorBrush>(mark.Fill).Color;
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var item in Flatten(child)) yield return item;
    }
    private sealed record Cell(object? Row, object? Column, double Value);
    private sealed record Key(string Value);
    private abstract class Notify : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public int Observers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
        protected void Changed(string name) => _changed?.Invoke(this, new(name));
    }
    private sealed class NotifyValue(object value) : Notify
    {
        private object _value = value;
        public object Value { get => _value; set { _value = value; Changed(nameof(Value)); } }
    }
    private sealed class MutableCell : Notify
    {
        private NotifyValue _row = new("A"), _column = new("X"); private double _value;
        public NotifyValue RowKey { get => _row; set { _row = value; Changed(nameof(RowKey)); } }
        public NotifyValue ColumnKey { get => _column; set { _column = value; Changed(nameof(ColumnKey)); } }
        public double Value { get => _value; set { _value = value; Changed(nameof(Value)); } }
    }
    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(HeatmapChart chart) { Window = new Window { Width = 300, Height = 200, Content = chart }; Window.Show(); Drain(); }
        public void Drain() { Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() { Window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
