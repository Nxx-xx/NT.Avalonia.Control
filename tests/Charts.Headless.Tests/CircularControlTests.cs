using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class CircularControlTests
{
    [AvaloniaFact]
    public void Chart_mapping_preserves_categories_items_signed_values_and_direct_color_contracts()
    {
        var custom = new LinearGradientBrush();
        var rows = new[] { new Row("zero", "Zero", 0, Colors.Red), new Row("negative", "Negative", -2, "Blue"),
            new Row("color", "Color", 3, Colors.Green), new Row("brush", "Brush", 4, custom), new Row("fallback", "Fallback", 5, 42d) };
        var series = Pie(rows); series.NegativeValueMode = ChartCircularNegativeValueMode.Absolute;
        var chart = Chart(series); chart.ColorPalette = new[] { Brushes.Purple };
        using var host = new Host(chart);
        var slices = chart.GetSlices();
        Assert.Equal(4, slices.Count); Assert.Equal(new[] { 1, 2, 3, 4 }, slices.Select(s => s.Data.SourceIndex));
        Assert.Equal(-2, slices[0].Data.Point.Y); Assert.Equal(2d / 14, slices[0].Fraction, 12);
        Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(slices[0].Fill).Color);
        Assert.Equal(Colors.Green, Assert.IsAssignableFrom<ISolidColorBrush>(slices[1].Fill).Color);
        Assert.Same(custom, slices[2].Fill);
        Assert.Equal(Colors.Purple, Assert.IsAssignableFrom<ISolidColorBrush>(slices[3].Fill).Color);
        Assert.Same(rows[1], slices[0].Data.Item); Assert.Equal("negative", slices[0].Hit.StableId);
        Assert.Equal("Negative", slices[0].Data.Category); Assert.Equal(-2, series.CreateSnapshot().Points[1].Point.Y);
    }

    [AvaloniaFact]
    public void Item_value_color_and_identity_notifications_refresh_current_scene_and_selected_metadata()
    {
        var row = new MutableRow("a", "Alpha", 2, "Red");
        var series = Pie(new[] { row });
        var chart = Chart(series);
        using var host = new Host(chart);
        var first = Assert.Single(chart.GetSlices()); Assert.True(chart.SelectSlice(first)); host.Drain();
        var revision = series.SourceVersion;
        row.Value = 8; row.Tone = Colors.Blue; row.Category = "Updated"; host.Drain();
        var current = Assert.Single(chart.GetSlices());
        Assert.True(series.SourceVersion > revision); Assert.Equal(8, current.Data.Point.Y);
        Assert.Equal("Updated", current.Data.Category); Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(current.Fill).Color);
        Assert.Same(row, chart.SelectedPoint!.Item); Assert.Equal(8, chart.SelectedPoint.Point.Y);
        Assert.Equal(Colors.Blue, chart.SelectedPoint.ColorValue);
        row.Id = "changed"; host.Drain(); _ = chart.GetSlices();
        Assert.Null(chart.SelectedPoint); Assert.Empty(chart.SelectedPoints);
    }

    [AvaloniaFact]
    public void External_selection_before_first_layout_is_resolved_when_the_chart_becomes_usable()
    {
        var series = Pie(new[] { new Row("a", "Alpha", 2, null), new Row("b", "Beta", 4, null) });
        var source = series.CreateSnapshot().Points[1];
        var chart = Chart(series);
        chart.SelectedPoint = ChartPointHit.FromData(series, source, default);
        using var host = new Host(chart);
        _ = chart.GetSlices();
        Assert.Equal("b", chart.SelectedPoint?.StableId); Assert.Single(chart.SelectedPoints);
        var selected = chart.GetSlices().Single(s => Equals(s.Data.StableId, "b"));
        Assert.Equal(selected.Anchor, chart.SelectedPoint!.Position);
    }

    [AvaloniaFact]
    public void External_selection_after_a_source_change_uses_the_new_snapshot_before_reconciliation()
    {
        var rows = new ObservableCollection<Row> { new("a", "Alpha", 2, null) };
        var series = Pie(rows); var chart = Chart(series);
        using var host = new Host(chart);
        rows.Add(new("b", "Beta", 4, null));
        var source = series.CreateSnapshot().Points[1];
        chart.SelectedPoint = ChartPointHit.FromData(series, source, default);
        host.Drain();
        Assert.Equal("b", chart.SelectedPoint?.StableId); Assert.Equal(1, chart.SelectedPoint?.Index);
    }

    [AvaloniaFact]
    public void Selection_cancellation_and_source_removal_callbacks_cannot_commit_a_stale_slice()
    {
        var rows = new ObservableCollection<Row> { new("a", "Alpha", 2, null), new("b", "Beta", 4, null) };
        var series = Pie(rows); var chart = Chart(series);
        using var host = new Host(chart);
        EventHandler<ChartSelectionChangingEventArgs> cancel = (_, e) => { if (e.CanCancel) e.Cancel = true; };
        chart.SelectionChanging += cancel;
        Assert.False(chart.SelectSlice(chart.GetSlices()[0])); Assert.Null(chart.SelectedPoint);
        chart.SelectionChanging -= cancel;
        chart.SelectionChanging += (_, e) => { if (e.CanCancel) rows.Clear(); };
        Assert.False(chart.SelectSlice(chart.GetSlices()[0]));
        Assert.Null(chart.SelectedPoint); Assert.Empty(chart.GetSlices());
    }

    [AvaloniaFact]
    public void Reentrant_selection_keeps_the_newer_request_and_configuration_clears_without_veto()
    {
        var chart = Chart(Pie(new[] { new Row("a", "Alpha", 2, null), new Row("b", "Beta", 4, null) }));
        using var host = new Host(chart);
        var slices = chart.GetSlices(); var nested = false;
        chart.SelectionChanging += (_, e) =>
        {
            if (e.CanCancel && !nested) { nested = true; chart.SelectSlice(slices[1]); }
            if (!e.CanCancel) e.Cancel = true;
        };
        Assert.False(chart.SelectSlice(slices[0])); Assert.Equal("b", chart.SelectedPoint?.StableId);
        chart.SelectionMode = ChartSelectionMode.None; host.Drain();
        Assert.Null(chart.SelectedPoint); Assert.Empty(chart.SelectedPoints);
    }

    [AvaloniaFact]
    public void Multiple_selection_toggles_by_stable_identity_and_reconciles_moves_and_removals()
    {
        var rows = new ObservableCollection<Row> { new("a", "Alpha", 2, null), new("b", "Beta", 4, null), new("c", "Gamma", 3, null) };
        var series = Pie(rows); var chart = Chart(series); chart.SelectionMode = ChartSelectionMode.Multiple;
        using var host = new Host(chart);
        Assert.True(chart.SelectSlice(chart.GetSlices()[0])); Assert.True(chart.SelectSlice(chart.GetSlices()[1]));
        Assert.Equal(2, chart.SelectedPoints.Count);
        rows.Move(1, 0); host.Drain(); _ = chart.GetSlices();
        Assert.Equal(new[] { "a", "b" }, chart.SelectedPoints.Select(p => p.StableId));
        Assert.Equal(new[] { 1, 0 }, chart.SelectedPoints.Select(p => p.Index));
        Assert.True(chart.SelectSlice(chart.GetSlices()[0], toggle: true)); Assert.Equal("a", Assert.Single(chart.SelectedPoints).StableId);
        rows.RemoveAt(1); host.Drain(); _ = chart.GetSlices();
        Assert.Empty(chart.SelectedPoints);
    }

    [AvaloniaFact]
    public void Concentric_series_have_independent_totals_and_topmost_overlap_hit_order()
    {
        var outer = new DonutSeries { ItemsSource = new[] { 1d, 3d }, InnerRadius = .7 };
        var inner = new DonutSeries { ItemsSource = new[] { 1d, 1d }, Radius = .6, InnerRadius = .3 };
        var chart = Chart(outer, inner); chart.SelectedExplodeOffset = 0;
        using var host = new Host(chart);
        var slices = chart.GetSlices(); Assert.Equal(4, slices.Count);
        Assert.Equal(new[] { .25, .75, .5, .5 }, slices.Select(s => s.Fraction));
        var center = slices[0].Center; var radius = slices[0].OuterRadius;
        Assert.Same(outer, chart.HitTest(center + new Vector(radius * .9, 0))!.Series);
        Assert.Same(inner, chart.HitTest(center + new Vector(radius * .5, 0))!.Series);
        Assert.Null(chart.HitTest(center));
        var top = new PieSeries { ItemsSource = new[] { 1d } };
        chart.Series.Add(top); host.Drain();
        Assert.Same(top, chart.HitTest(center + new Vector(radius * .5, 0))!.Series);
    }

    [AvaloniaFact]
    public void Donut_holes_gaps_and_explosion_follow_the_current_translated_sector()
    {
        var series = new DonutSeries { ItemsSource = new[] { 1d, 1d }, StartAngle = 0, SliceGap = 10,
            ExplodedIndex = 0, ExplodeOffset = 12 };
        var chart = Chart(series); chart.SelectedExplodeOffset = 0;
        using var host = new Host(chart);
        var slices = chart.GetSlices(); var first = slices[0]; var second = slices[1];
        Assert.Equal(second.Center.X, first.Center.X, 9); Assert.Equal(second.Center.Y + 12, first.Center.Y, 9);
        Assert.Same(series, chart.HitTest(first.Anchor)!.Series); Assert.Equal(0, chart.HitTest(first.Anchor)!.Data.SourceIndex);
        Assert.Null(chart.HitTest(first.Center));
        var gap = CircularChartGeometry.PointAt(second, 0, (second.InnerRadius + second.OuterRadius) / 2);
        Assert.Null(chart.HitTest(gap));
        Assert.Null(chart.HitTest(new Point(-1, -1)));
    }

    [AvaloniaFact]
    public void Semi_ring_uses_the_available_height_and_keeps_center_content_inside_its_layout()
    {
        var series = new SemiDonutSeries { ItemsSource = new[] { 1d, 2d } };
        var content = new Border { Width = 600, Height = 300, Background = Brushes.Blue };
        var chart = Chart(series); chart.CenterContent = content;
        using var host = new Host(chart, 600, 260);
        var slice = chart.GetSlices()[0];
        Assert.True(slice.OuterRadius > chart.PlotArea.Height / 2);
        Assert.True(chart.PlotArea.Contains(chart.CenterContentBounds), $"plot {chart.PlotArea}; center clip {chart.CenterContentBounds}");
        var clipHost = content.GetVisualParent(); Assert.NotNull(clipHost); Assert.True(clipHost.ClipToBounds);
        Assert.Equal(chart.CenterContentBounds, clipHost.Bounds); Assert.Equal(600, content.Width);
        Assert.Same(chart, ((ILogical)content).LogicalParent);
        Assert.Null(chart.HitTest(slice.Center + new Vector(0, slice.OuterRadius * .8)));
        chart.CenterContent = null;
        Assert.Null(((ILogical)content).LogicalParent); Assert.Null(content.GetVisualParent()); Assert.DoesNotContain(content, chart.GetLogicalChildren());
        var replacement = new TextBlock { Text = "Center" }; chart.CenterContent = replacement; host.Drain();
        Assert.Same(chart, ((ILogical)replacement).LogicalParent);
    }

    [AvaloniaFact]
    public void Dense_outside_labels_are_bounded_collision_free_and_keep_a_leader_for_each_retained_identity()
    {
        var series = Pie(Enumerable.Range(0, 40).Select(i => new Row($"id-{i}", $"Category {i}", i + 1, null)).ToArray());
        var chart = Chart(series); chart.ShowDataLabels = true; chart.LabelPosition = CircularLabelPosition.Outside;
        chart.LabelFormatter = s => $"{s.Data.Category}\n{s.Fraction:P1}";
        using var host = new Host(chart, 660, 340);
        var labels = chart.GetLabels(); Assert.NotEmpty(labels); Assert.True(labels.Count < 40);
        foreach (var label in labels)
        {
            Assert.DoesNotContain('\n', label.Text);
            Assert.True(new Rect(chart.Bounds.Size).Contains(label.Bounds), $"canvas {chart.Bounds.Size}; label {label.Text}: {label.Bounds}");
            Assert.Equal(3, label.Connector.Count); Assert.Equal(label.Slice.Data.SourceIndex, label.Slice.Hit.Index);
            var expected = CircularChartGeometry.PointAt(label.Slice, label.Slice.StartAngle + label.Slice.SweepAngle / 2, label.Slice.OuterRadius);
            Assert.Equal(expected, label.Connector[0]);
        }
        for (var index = 0; index < labels.Count; index++)
        for (var other = index + 1; other < labels.Count; other++)
            Assert.False(labels[index].Bounds.Intersects(labels[other].Bounds));
        chart.LabelPosition = CircularLabelPosition.None;
        Assert.Empty(chart.GetLabels());
    }

    [AvaloniaFact]
    public void Inside_labels_omit_sectors_that_cannot_fit_and_have_no_outer_connector()
    {
        var chart = Chart(new DonutSeries { ItemsSource = new[] { 99d, 1d }, InnerRadius = .2 });
        chart.ShowDataLabels = true; chart.LabelPosition = CircularLabelPosition.Inside; chart.LabelFormatter = _ => "Share";
        using var host = new Host(chart);
        var label = Assert.Single(chart.GetLabels()); Assert.Equal(0, label.Slice.Data.SourceIndex);
        Assert.Empty(label.Connector); Assert.True(new Rect(chart.Bounds.Size).Contains(label.Bounds));
    }

    [AvaloniaFact]
    public void Series_and_item_subscriptions_release_on_detach_and_reconnect_without_duplicates()
    {
        var row = new MutableRow("a", "Alpha", 2, "Red"); var series = Pie(new[] { row });
        var source = new SeriesCollection(series); var chart = Chart(); chart.SeriesSource = source;
        using var host = new Host(chart);
        Assert.Equal(1, row.Subscribers); Assert.Equal(1, source.Subscribers);
        Assert.Same(chart, ((ILogical)series).LogicalParent);
        host.Window.Content = null;
        Assert.Equal(0, row.Subscribers); Assert.Equal(0, source.Subscribers);
        Assert.Null(((ILogical)series).LogicalParent);
        row.Value = 9; host.Window.Content = chart; host.Drain();
        Assert.Equal(1, row.Subscribers); Assert.Equal(1, source.Subscribers);
        Assert.Equal(9, Assert.Single(chart.GetSlices()).Data.Point.Y);
        chart.SeriesSource = Array.Empty<CircularSeries>(); host.Drain();
        Assert.Equal(0, row.Subscribers); Assert.Equal(0, source.Subscribers);
    }

    [AvaloniaFact]
    public void Keyboard_left_starts_at_the_last_sector_and_space_toggles_selection()
    {
        var chart = Chart(Pie(new[] { new Row("a", "Alpha", 2, null), new Row("b", "Beta", 4, null), new Row("c", "Gamma", 3, null) }));
        using var host = new Host(chart);
        chart.Focus(); host.Key(Key.Left); host.Key(Key.Space); host.Drain();
        Assert.Equal("c", chart.SelectedPoint?.StableId);
        host.Key(Key.Space); host.Drain(); Assert.Null(chart.SelectedPoint);
        host.Key(Key.Home); host.Key(Key.Enter); host.Drain(); Assert.Equal("a", chart.SelectedPoint?.StableId);
        host.Key(Key.Escape); host.Drain(); Assert.Empty(chart.SelectedPoints);
    }

    private sealed record Row(string Id, string Category, double Value, object? Tone);
    private static PieSeries Pie(System.Collections.IEnumerable rows) => new()
    { ItemsSource = rows, CategoryPath = "Category", ValuePath = "Value", IdPath = "Id", ColorPath = "Tone" };
    private static CircularChart Chart(params CircularSeries[] series)
    {
        var chart = new CircularChart { ShowLegend = false, ShowDataLabels = false, AnimationTriggers = ChartAnimationTriggers.None };
        foreach (var item in series) chart.Series.Add(item);
        return chart;
    }
    private sealed class Host : IDisposable
    {
        internal Window Window { get; }
        internal Host(CircularChart chart, double width = 620, double height = 380)
        { Window = new Window { Width = width, Height = height, Content = chart }; Window.Show(); Drain(); }
        internal void Drain() { Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        internal void Key(Key key)
        { Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null); Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null); }
        public void Dispose() => Window.Close();
    }
    private sealed class MutableRow(string id, string category, double value, object? tone) : INotifyPropertyChanged
    {
        private string _id = id, _category = category;
        private double _value = value;
        private object? _tone = tone;
        private PropertyChangedEventHandler? _changed;
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
        public string Id { get => _id; set { _id = value; _changed?.Invoke(this, new(nameof(Id))); } }
        public string Category { get => _category; set { _category = value; _changed?.Invoke(this, new(nameof(Category))); } }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public object? Tone { get => _tone; set { _tone = value; _changed?.Invoke(this, new(nameof(Tone))); } }
    }
    private sealed class SeriesCollection(params CircularSeries[] values) : Collection<CircularSeries>(values.ToList()), INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _changed;
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event NotifyCollectionChangedEventHandler? CollectionChanged { add => _changed += value; remove => _changed -= value; }
    }
}
