using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class TrackballPresentationTests
{
    [AvaloniaFact]
    public void Default_trackball_creates_no_controls_or_readings_and_preserves_tooltip_enum_values()
    {
        using var host = CreateHost();
        Assert.Equal(0, (int)ChartTooltipMode.NearestPoint); Assert.Equal(1, (int)ChartTooltipMode.SharedX); Assert.Equal(2, (int)ChartTooltipMode.SharedY);
        Assert.Equal(ChartTrackballMode.None, host.Chart.TrackballMode);
        Assert.Empty(host.Chart.TrackballReadings); Assert.Empty(host.Chart.HoveredSegments); Assert.Null(host.Chart.PrimarySegment);
        Assert.Empty(Cards(host.Chart));
        Assert.DoesNotContain(host.Chart.GetVisualDescendants().OfType<Control>(), control => control.Name == "PART_ChartTrackballOverlay");
        _ = TemplateFeaturesHost.Record(host.Chart);
        Assert.Empty(host.Chart.TrackballReadings);
    }

    [AvaloniaFact]
    public void Linear_snap_keeps_isolated_real_samples_only_at_their_shared_coordinate_without_bridging_a_gap()
    {
        var single = LineInterpolationTests.CreateLine([new("single", 4, 40)]);
        var separated = LineInterpolationTests.CreateLine([new("left", 1, 10), new("missing", 5, double.NaN), new("right", 9, 90)]);
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), single, separated);
        host.Chart.IsTooltipEnabled = false;
        host.Chart.TrackballMode = ChartTrackballMode.Vertical;
        host.Chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        foreach (var (x, series, sourceIndex, value) in new[] { (4d, single, 0, new ChartPoint(4, 40)), (1d, separated, 0, new ChartPoint(1, 10)), (9d, separated, 2, new ChartPoint(9, 90)) })
        {
            host.Move(host.Project(new(x, 50)));
            var reading = Assert.Single(host.Chart.TrackballReadings);
            Assert.Same(series, reading.Series);
            Assert.False(reading.IsInterpolated); Assert.Null(reading.Segment);
            Assert.Equal(sourceIndex, reading.Original.Index); Assert.Equal(value, reading.Point);
            Assert.Same(((SegmentRow[])series.ItemsSource!)[sourceIndex], reading.Original.Item);
            Assert.Empty(host.Chart.HoveredSegments); Assert.Null(host.Chart.PrimarySegment);
        }
        host.Move(host.Project(new(2, 50)));
        Assert.Empty(host.Chart.TrackballReadings); Assert.Empty(host.Chart.HoveredSegments); Assert.Null(host.Chart.PrimarySegment);
        host.Move(host.Project(new(5, 50)));
        Assert.Empty(host.Chart.TrackballReadings); Assert.Empty(host.Chart.HoveredSegments); Assert.Null(host.Chart.PrimarySegment);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Trackball_interpolates_each_line_in_its_direction_independently_of_disabled_tooltips(bool horizontal)
    {
        using var host = CreateHost();
        var chart = host.Chart;
        chart.IsTooltipEnabled = false;
        chart.TooltipMode = ChartTooltipMode.SharedY;
        chart.TrackballMode = horizontal ? ChartTrackballMode.Horizontal : ChartTrackballMode.Vertical;
        chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        host.Move(host.Project(new(3, 50)));
        Assert.Equal(2, chart.TrackballReadings.Count);
        Assert.Equal(2, chart.HoveredSegments.Count);
        foreach (var series in chart.Series)
        {
            var reading = Assert.Single(chart.TrackballReadings, reading => ReferenceEquals(reading.Series, series));
            var primary = ReferenceEquals(series, chart.Series[0]);
            var expected = horizontal ? new ChartPoint(4, 50) : new ChartPoint(3, primary ? 40 : 60);
            if (horizontal && !primary) expected = new(4, 50);
            SegmentTestHost.AssertPoint(expected, reading.Point);
            SegmentTestHost.AssertPosition(host.Project(expected), reading.Position);
            Assert.True(reading.IsInterpolated);
            var segment = Assert.IsType<ChartSegmentHit>(reading.Segment);
            var rows = (SegmentRow[])series.ItemsSource!;
            Assert.Same(rows[0], segment.Start.Item); Assert.Same(rows[1], segment.End.Item);
            Assert.Same(segment.Original, reading.Original);
            Assert.Equal(reading.Original.Point, Assert.Single(chart.HoveredPoints, hit => ReferenceEquals(hit.Series, series)).Point);
            Assert.NotEqual(reading.Original.Point, reading.Point);
        }
        Assert.Equal(ChartTooltipMode.SharedY, chart.TooltipMode);
        chart.TrackballMode = ChartTrackballMode.None;
        Assert.Empty(chart.TrackballReadings); Assert.Empty(chart.HoveredSegments); Assert.Null(chart.HoveredPoint);
        Assert.Empty(Cards(chart));
        Assert.Equal(ChartTooltipMode.SharedY, chart.TooltipMode);
    }

    [AvaloniaTheory]
    [InlineData(ChartTrackballDisplayMode.GroupAllPoints, 1)]
    [InlineData(ChartTrackballDisplayMode.FloatAllPoints, 2)]
    public void Real_content_controls_receive_grouped_or_individual_readings_and_export_one_host(ChartTrackballDisplayMode mode, int expectedCards)
    {
        using var host = CreateHost();
        var chart = host.Chart;
        chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        chart.TrackballDisplayMode = mode;
        var builds = 0;
        var formatterSizes = new List<int>();
        chart.TrackballFormatter = readings => { formatterSizes.Add(readings.Count); return null; };
        chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => { builds++; return Card(); });
        host.Move(host.Project(new(3, 50)));
        var cards = Cards(chart);
        Assert.Equal(expectedCards, cards.Length);
        Assert.All(cards, card =>
        {
            Assert.True(card.Bounds.Width > 0 && card.Bounds.Height > 0);
            var context = Assert.IsType<ChartTooltipContext>(card.DataContext);
            Assert.Empty(context.Text);
            Assert.Equal(mode == ChartTrackballDisplayMode.GroupAllPoints ? 2 : 1, context.Readings.Count);
            Assert.Equal(context.Readings.Count, context.Segments.Count);
            Assert.All(context.Readings, reading => Assert.Contains(chart.TrackballReadings, current =>
                ReferenceEquals(current.Series, reading.Series) && current.Point == reading.Point && ReferenceEquals(current.Original.Item, reading.Original.Item)));
        });
        Assert.NotEmpty(formatterSizes);
        Assert.All(formatterSizes, size => Assert.Equal(mode == ChartTrackballDisplayMode.GroupAllPoints ? 2 : 1, size));
        var overlay = Assert.Single(chart.GetVisualDescendants().OfType<Control>(), control => control.Name == "PART_ChartTrackballOverlay");
        Assert.False(overlay.IsHitTestVisible); Assert.False(overlay.Focusable);
        var before = builds;
        _ = TemplateFeaturesHost.Record(chart);
        var recording = new DrawingGroup();
        using (var drawing = recording.Open()) chart.RenderExport(drawing, chart.GetExportLayout(new() { PixelSize = new(960, 540), Dpi = 192 }), Brushes.White);
        var brushes = TemplateFeaturesHost.Walk(recording).OfType<GeometryDrawing>().Select(item => item.Brush).OfType<VisualBrush>();
        Assert.Single(brushes, brush => ReferenceEquals(brush.Visual, overlay));
        Assert.Equal(before, builds);
    }

    [AvaloniaTheory]
    [InlineData("none")]
    [InlineData("source")]
    [InlineData("template")]
    [InlineData("display")]
    [InlineData("detach")]
    public void Presentation_changes_release_old_controls_and_do_not_leave_obsolete_endpoint_contexts(string action)
    {
        using var host = CreateHost();
        var chart = host.Chart;
        chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => Card());
        host.Move(host.Project(new(3, 50)));
        var previous = Assert.Single(Cards(chart));
        if (action == "none") chart.TrackballMode = ChartTrackballMode.None;
        else if (action == "source") chart.Series.Clear();
        else if (action == "template") chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => new Border { Width = 40, Height = 20 });
        else if (action == "display") chart.TrackballDisplayMode = ChartTrackballDisplayMode.FloatAllPoints;
        else host.Window.Content = null;
        host.Drain();
        TemplateFeaturesHost.AssertReleased(previous);
        if (action is "none" or "source" or "detach")
        { Assert.Empty(chart.TrackballReadings); Assert.Empty(chart.HoveredSegments); Assert.Null(chart.PrimarySegment); }
    }

    [AvaloniaTheory]
    [InlineData("none")]
    [InlineData("source")]
    public void Template_build_reentrancy_cannot_publish_old_controls_or_readings(string action)
    {
        using var host = CreateHost();
        var chart = host.Chart;
        chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        var obsolete = Card();
        chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) =>
        {
            if (action == "none") chart.TrackballMode = ChartTrackballMode.None;
            else chart.Series.Clear();
            return obsolete;
        });
        host.Move(host.Project(new(3, 50)));
        Assert.Empty(chart.TrackballReadings); Assert.Empty(chart.HoveredSegments); Assert.Null(chart.PrimarySegment);
        Assert.Empty(Cards(chart));
        TemplateFeaturesHost.AssertReleased(obsolete);
    }

    [AvaloniaFact]
    public void Each_interpolated_reading_uses_its_own_linear_or_logarithmic_axis()
    {
        using var host = CreateHost();
        var chart = host.Chart;
        var log = new LogarithmicAxis { Key = "secondary-log", Minimum = 1, Maximum = 10_000, Position = ChartAxisPosition.Far };
        chart.Axes.Add(log);
        chart.Series[1].YAxisKey = log.Key;
        chart.Series[1].ItemsSource = new SegmentRow[] { new("log-left", 1, 1), new("log-right", 5, 10_000) };
        chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        host.Drain();
        host.Move(host.Project(new(3, 50)));
        var linear = Assert.Single(chart.TrackballReadings, reading => ReferenceEquals(reading.Series, chart.Series[0]));
        var logarithmic = Assert.Single(chart.TrackballReadings, reading => ReferenceEquals(reading.Series, chart.Series[1]));
        SegmentTestHost.AssertPoint(new(3, 40), linear.Point);
        SegmentTestHost.AssertPoint(new(3, 100), logarithmic.Point);
        Assert.Equal(chart.PlotArea.Bottom - chart.PlotArea.Height * .5, logarithmic.Position.Y, 8);
        Assert.NotEqual(linear.Position.Y, logarithmic.Position.Y);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reading_notification_reentrancy_clears_all_published_segment_and_card_state(bool clearOnSegments)
    {
        using var host = CreateHost();
        var chart = host.Chart;
        chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => Card());
        chart.PropertyChanged += (_, change) =>
        {
            if (clearOnSegments ? change.Property == CartesianChart.HoveredSegmentsProperty && chart.HoveredSegments.Count > 0
                : change.Property == CartesianChart.TrackballReadingsProperty && chart.TrackballReadings.Count > 0)
                chart.Series.Clear();
        };
        host.Move(host.Project(new(3, 50)));
        Assert.Empty(chart.TrackballReadings); Assert.Empty(chart.HoveredSegments); Assert.Null(chart.PrimarySegment);
        Assert.Empty(chart.HoveredPoints); Assert.Null(chart.HoveredPoint); Assert.Empty(chart.AxisReadings);
        Assert.Empty(Cards(chart));
    }

    [AvaloniaFact]
    public void A_new_pointer_reading_published_during_mode_cleanup_is_not_overwritten_by_the_previous_anchor()
    {
        using var host = CreateHost();
        var chart = host.Chart;
        chart.IsTooltipEnabled = false;
        chart.TrackballMode = ChartTrackballMode.Vertical;
        chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => Card());
        host.Move(host.Project(new(2, 35)));
        var previousCard = Assert.Single(Cards(chart));
        var armed = true;
        var nestedPointerPublished = false;
        chart.PropertyChanged += (_, change) =>
        {
            if (!armed || change.Property != CartesianChart.HoveredPointsProperty || chart.HoveredPoints.Count != 0) return;
            armed = false;
            var point = host.Project(new(4, 55));
            SegmentTestHost.RaiseExactMove(chart, point, 2);
            nestedPointerPublished = chart.TrackballReadings.Count == 2 && chart.TrackballReadings.All(reading => Math.Abs(reading.Point.Y - 55) < 1e-7);
        };
        chart.TrackballMode = ChartTrackballMode.Horizontal;
        host.Drain();
        Assert.False(armed);
        Assert.True(nestedPointerPublished);
        Assert.Equal(2, chart.TrackballReadings.Count);
        SegmentTestHost.AssertPoint(new(4.5, 55), Assert.Single(chart.TrackballReadings, reading => ReferenceEquals(reading.Series, chart.Series[0])).Point);
        SegmentTestHost.AssertPoint(new(3.5, 55), Assert.Single(chart.TrackballReadings, reading => ReferenceEquals(reading.Series, chart.Series[1])).Point);
        TemplateFeaturesHost.AssertReleased(previousCard);
        var current = Assert.IsType<ChartTooltipContext>(Assert.Single(Cards(chart)).DataContext);
        Assert.Equal(2, current.Readings.Count);
        Assert.All(current.Readings, reading => Assert.Equal(55, reading.Point.Y, 7));
    }

    [AvaloniaFact]
    public void Null_build_hides_the_custom_card_without_falling_back_or_rebuilding_during_render()
    {
        using var host = CreateHost();
        var chart = host.Chart;
        chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        var builds = 0;
        chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => { builds++; return null; });
        host.Move(host.Project(new(3, 50)));
        Assert.Equal(2, chart.TrackballReadings.Count);
        Assert.Empty(Cards(chart));
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Border>(), control => control.Name == "PART_ChartTrackballCard");
        var before = builds;
        _ = TemplateFeaturesHost.Record(chart); _ = TemplateFeaturesHost.Record(chart);
        host.Drain();
        Assert.Equal(before, builds);
    }

    [AvaloniaFact]
    public void A_throwing_business_template_is_cleaned_once_and_a_replacement_can_recover()
    {
        using var host = CreateHost();
        var chart = host.Chart;
        chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        var builds = 0;
        chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) =>
        { builds++; throw new InvalidOperationException("business trackball template failure"); });
        Assert.Throws<InvalidOperationException>(() => host.Move(host.Project(new(3, 50))));
        Assert.Empty(Cards(chart));
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Border>(), control => control.Name == "PART_ChartTrackballCard");
        var before = builds;
        _ = TemplateFeaturesHost.Record(chart); _ = TemplateFeaturesHost.Record(chart);
        Assert.Equal(before, builds);
        chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => Card());
        host.Move(host.Project(new(3, 50)));
        Assert.Single(Cards(chart));
        Assert.Equal(2, Assert.IsType<ChartTooltipContext>(Cards(chart)[0].DataContext).Readings.Count);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Line_and_marker_styles_apply_to_actual_derived_positions_and_can_be_hidden_separately(bool horizontal)
    {
        using var host = CreateHost();
        var chart = host.Chart;
        chart.IsTooltipEnabled = false;
        chart.TrackballMode = horizontal ? ChartTrackballMode.Horizontal : ChartTrackballMode.Vertical;
        chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        chart.TrackballFormatter = _ => null;
        chart.TrackballLineStroke = Brushes.Fuchsia;
        chart.TrackballLineStrokeThickness = 3;
        chart.TrackballMarkerFill = Brushes.LawnGreen;
        chart.TrackballMarkerStroke = Brushes.MediumVioletRed;
        chart.TrackballMarkerStrokeThickness = 2.5;
        chart.TrackballMarkerSize = 18;
        host.Move(host.Project(new(3, 50)));
        var leaves = TemplateFeaturesHost.Walk(TemplateFeaturesHost.Record(chart)).OfType<GeometryDrawing>().ToArray();
        var line = Assert.Single(leaves, item => item.Pen?.Brush is ISolidColorBrush brush && brush.Color == Colors.Fuchsia);
        Assert.Equal(3, line.Pen!.Thickness);
        Assert.Equal(horizontal ? chart.PlotArea.Width : 0, line.Geometry!.Bounds.Width, 8);
        Assert.Equal(horizontal ? 0 : chart.PlotArea.Height, line.Geometry.Bounds.Height, 8);
        var markers = leaves.Where(item => item.Brush is ISolidColorBrush brush && brush.Color == Colors.LawnGreen).ToArray();
        Assert.Equal(2, markers.Length);
        foreach (var location in chart.TrackballReadings.GroupBy(reading => reading.Position))
        {
            Assert.Equal(location.Count(), markers.Count(item => Math.Abs(item.Geometry!.Bounds.Center.X - location.Key.X) < 1e-7 &&
                Math.Abs(item.Geometry.Bounds.Center.Y - location.Key.Y) < 1e-7));
        }
        foreach (var marker in markers)
        {
            Assert.Equal(18, marker.Geometry!.Bounds.Width, 8); Assert.Equal(18, marker.Geometry.Bounds.Height, 8);
            Assert.Equal(2.5, marker.Pen!.Thickness);
            Assert.Equal(Colors.MediumVioletRed, Assert.IsAssignableFrom<ISolidColorBrush>(marker.Pen.Brush).Color);
        }
        chart.ShowTrackballLine = false;
        leaves = TemplateFeaturesHost.Walk(TemplateFeaturesHost.Record(chart)).OfType<GeometryDrawing>().ToArray();
        Assert.DoesNotContain(leaves, item => item.Pen?.Brush is ISolidColorBrush brush && brush.Color == Colors.Fuchsia);
        Assert.Equal(2, leaves.Count(item => item.Brush is ISolidColorBrush brush && brush.Color == Colors.LawnGreen));
        chart.ShowTrackballMarkers = false;
        leaves = TemplateFeaturesHost.Walk(TemplateFeaturesHost.Record(chart)).OfType<GeometryDrawing>().ToArray();
        Assert.DoesNotContain(leaves, item => item.Brush is ISolidColorBrush brush && brush.Color == Colors.LawnGreen);
        Assert.Equal(2, chart.TrackballReadings.Count);
    }

    [AvaloniaFact]
    public void Compiled_templates_bind_derived_values_and_real_endpoint_items_then_follow_resources_and_mode_changes()
    {
        var view = new TrackballConsumerView();
        var model = new TrackballConsumerModel { ContentTemplate = (IDataTemplate)view.Resources["BusinessTrackballTemplate"]! };
        view.DataContext = model;
        var chart = view.FindControl<CartesianChart>("TrackballChart")!;
        using var host = new TemplateFeaturesHost(chart, view);
        void Move()
        {
            var position = new Point(chart.PlotArea.Left + chart.PlotArea.Width * .25, chart.PlotArea.Bottom - chart.PlotArea.Height * .5);
            SegmentTestHost.RaiseExactMove(chart, position);
            host.Drain();
        }
        Move();
        var root = Assert.Single(chart.GetVisualDescendants().OfType<Border>(), control => control.Name == "CompiledTrackballCard");
        var context = Assert.IsType<ChartTooltipContext>(root.DataContext);
        var reading = Assert.Single(context.Readings, item => ReferenceEquals(item.Series, chart.Series[0]));
        SegmentTestHost.AssertPoint(new(2.5, 35), reading.Point);
        Assert.Same(model.Rows[0], reading.Segment!.Start.Item); Assert.Same(model.Rows[1], reading.Segment.End.Item);
        var items = Assert.Single(root.GetVisualDescendants().OfType<ItemsControl>());
        Assert.Same(context, items.DataContext);
        Assert.Same(context.Readings, items.ItemsSource);
        Assert.Equal(2, items.ItemCount);
        Assert.True(items.GetVisualDescendants().OfType<TextBlock>().Any(), System.Text.Json.JsonSerializer.Serialize(new
        {
            items.ItemCount, items.Bounds, items.DesiredSize, rootBounds = root.Bounds,
            template = items.Template?.GetType().FullName, theme = items.Theme?.GetType().FullName,
            presenter = items.Presenter?.GetType().FullName, panel = items.ItemsPanelRoot?.GetType().FullName,
            panelChildCount = items.ItemsPanelRoot?.Children.Count,
            realizedContainers = items.GetRealizedContainers().Count(),
            logicalParent = ((Avalonia.LogicalTree.ILogical)items).LogicalParent?.GetType().FullName,
            visualParent = items.GetVisualParent()?.GetType().FullName,
            descendants = items.GetVisualDescendants().Select(control => control.GetType().Name).ToArray(),
        }));
        Assert.Contains(root.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "CompiledTrackballLeftId" && text.Text == "primary-left");
        Assert.Contains(root.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "CompiledTrackballRightId" && text.Text == "primary-right");
        model.Rows[0].Name = "updated original left";
        host.Drain();
        // Any source-item notification invalidates its snapshot, including a
        // business-only property. Re-enter to inspect the newly mapped context.
        var previousRoot = root;
        TemplateFeaturesHost.AssertReleased(previousRoot);
        Assert.Empty(chart.TrackballReadings);
        Move();
        root = Assert.Single(chart.GetVisualDescendants().OfType<Border>(), control => control.Name == "CompiledTrackballCard");
        Assert.NotSame(previousRoot, root);
        Assert.Same(model.Rows[0], Assert.Single(Assert.IsType<ChartTooltipContext>(root.DataContext).Readings,
            item => ReferenceEquals(item.Series, chart.Series[0])).Original.Item);
        Assert.Contains(root.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "CompiledTrackballOriginal" && text.Text == "updated original left");
        view.Resources["TrackballAccent"] = Brushes.Orange;
        host.Drain();
        Assert.Equal(Colors.Orange, Assert.IsAssignableFrom<ISolidColorBrush>(root.Background).Color);
        model.Rows[1].Y = 80;
        host.Drain(); Move();
        SegmentTestHost.AssertPoint(new(2.5, 42.5), Assert.Single(chart.TrackballReadings, item => ReferenceEquals(item.Series, chart.Series[0])).Point);
        model.Display = ChartTrackballDisplayMode.FloatAllPoints;
        host.Drain();
        Assert.Equal(2, chart.GetVisualDescendants().OfType<Border>().Count(control => control.Name == "CompiledTrackballCard"));
        model.Mode = ChartTrackballMode.None;
        host.Drain();
        Assert.Empty(chart.TrackballReadings);
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Border>(), control => control.Name == "CompiledTrackballCard");
    }

    private static Border Card() => new() { Name = "TrackballTestCard", Width = 130, Height = 36, Background = Brushes.DarkCyan };
    private static Border[] Cards(CartesianChart chart) => chart.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "TrackballTestCard").ToArray();
    private static SegmentTestHost CreateHost() => new(new(0, 10), new(0, 100),
        LineInterpolationTests.CreateLine([new("primary-left", 1, 20), new("primary-right", 5, 60)]),
        LineInterpolationTests.CreateLine([new("secondary-left", 1, 80), new("secondary-right", 5, 40)]));
}
